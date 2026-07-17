# Boss Duel v2 — Plan (kararlar kilit)

Tarih: 2026-07-13 · Durum: ONAYLI TASARIM, implementasyon fazlara bölündü

## Kilitlenen kararlar

| Karar | Seçim |
|---|---|
| Level kaynağı | Mevcut LevelData asset'leri devam (her 5. level bir boss asset'i) — AMA dalga/denge alanları boş bırakılırsa **formülden otomatik dolar**, elle tasarım minimuma iner |
| Dalga yapısı | Otomatik artan: erken bosslar 1 dalga → ilerledikçe 2 → 3; her dalga yeni görsel + yetenek |
| Mekanikler | Kesilebilir şarj saldırısı + Special bonus hasarı + Renk zayıflığı fazı (board saldırıları EKLENMEDİ; oil baskısı mevcut haliyle kalır) |
| Oyuncu gücü | Kalkan pickup'ları (mevcut) + Süper lazer şarj barı (yeni) |

## Mevcut durum (özet)

`BossDuelController` (1123 satır): tek düşman; taş kırıldıkça `pendingStrikes` birikir → rapid-fire lazer (`damagePerTile`); düşman `enemyAttackInterval` saatinde vurur, hasarı `base + growth×attackCount`; oil baskısı `bossAttackEveryMoves/OilCount`; kalkan pickup'ları (ObstacleId 35/36, `ObstacleVisualChanged` ile) süreli kalkan verir; düşman HP = `BossDamage` collectible goal; hamle sınırsız (refund).

## 1) Veri modeli

### BossWaveDef (Serializable, LevelData içinde liste)
```
enemyBodySprite, enemyDefeatedSprite, bodyTint      // dalga görsel varyantı
hpWeight            // toplam BossDamage goal'ünün bu dalgaya düşen payı (normalize edilir)
attackInterval, attackDamageBase, attackDamageGrowth
oilEveryMoves, oilCount                             // mevcut alanlar dalgaya taşınır
chargeAttack { enabled, chargeSeconds, interruptTileCount, damageMult, stunSeconds }
colorWeakness { enabled, multiplier, rotateSeconds }
pickupSpawner { playerShieldEverySeconds, enemyShieldEverySeconds }   // runtime spawn, level'a elle koymak gerekmez
```

### LevelData eklentileri
- `bossWaves: List<BossWaveDef>` — **boşsa** `BossDifficulty.BuildDefaultWaves(bossIndex)` üretir (bossIndex = levelNumber/5).
- Mevcut alanlar (`playerMaxHp`, `damagePerClearedTile`, `enemyAttack*`, `bossAttack*`) tek-dalga fallback olarak okunmaya devam eder → **eski boss levelları hiç dokunmadan çalışır**.
- LevelDataEditor'a "Boss Auto-Fill (formül)" butonu: goal amount + dalga listesini formülden yazar; istenirse elle oynanır.

### HP / goal entegrasyonu (değişmez çekirdek)
Toplam düşman HP'si = BossDamage goal amount (bugünkü gibi). Dalga HP'si = toplam × hpWeight. `NotifyCollectibleCollected` akışı aynı kalır → WIN, son dalga ölüp goal 0 olunca otomatik tetiklenir. HP bar dalga sayısı kadar segmente bölünür.

## 2) Denge formülü (BossDifficulty, tek merkez)

b = bossIndex (level 5 → b=1):
- enemyTotalHp = 400 × (1 + 0.35·b)
- playerMaxHp = 500 + 15·b
- dmgBase = 15 + 3·b · dmgGrowth = 4 + b/2 · interval = max(1.2, 2.2 − 0.05·b)
- dalga sayısı: b≤2 → 1 · 3–5 → 2 · 6+ → 3; HP payları (1.0) / (0.45, 0.55) / (0.30, 0.33, 0.37); sonraki dalga %10 daha hızlı vurur
- şarj saldırısı b≥2 açılır: 6 sn şarj, kesmek için 10+b taş, hasar 3×, kesilirse 2.5 sn stun (+alınan hasar 1.5×)
- renk zayıflığı b≥1: 2× çarpan, 10 sn'de bir renk döner
- süper lazer: 40 taşta dolar, hasar = 12 × damagePerTile, kalkan deler
- Tüm sabitler `BossDifficulty` içinde tek tablo — tuning tek dosyadan.

## 3) Controller mimarisi

`BossDuelController` orkestratör kalır; parçalar:
- **Wave state machine:** `Intro → Fight → WaveDefeated → NextWaveEntrance → ... → Victory/Defeat`. Dalga geçişi: mevcut `PlayDefeat` çöküşü → yeni robot sağdan girer (intro tween yeniden kullanılır) → "WAVE N" banner → oyuncuya +%15 HP heal → savaş devam. Geçiş sırasında board kilitlenmez (sadece düşman ateşi durur).
- **EnemyWaveRuntime:** hp, attackCount, timer'lar, aktif yetenek durumları — dalga başına sıfırlanır.
- **Şarj saldırısı:** düşman üstünde dolan progress ring + renk değişimi (telegraph). Pencere içinde kırılan taş sayacı `HandleTilesCleared`'den beslenir; hedefe ulaşılırsa iptal + stun, ulaşılmazsa büyük bolt (`damageMult×`). Kalkan yine emer.
- **Renk zayıflığı:** düşman kafasında tile ikon UI'ı; `HandleTilesCleared(type, amount)` zaten tip veriyor → strike'lar tip etiketiyle kuyruğa girer (`Queue<TileType>`), isabet anında zayıf renkse hasar × multiplier + kritik popup.
- **Special bonus hasarı:** `SpecialBoardSignal` (mevcut event) dinlenir → Line +5 strike, Pulse +8, Override +12, PatchBot: bolt yerine robota uçan tek büyük mermi (mevcut PatchBot hedefleme görseliyle uyumlu, hasar 6×damagePerTile).
- **Süper lazer:** her oyuncu strike'ı şarj barını doldurur (UI: oyuncu robotu altı). Dolunca buton parlar; tap → kalın beam, 12×damagePerTile, kalkanı DELER ve söker. Dalga finisher hissi.
- **Pickup spawner:** dalga parametresine göre her N sn'de boş bir hücreye `TrySpawnSingleCellObstacleAt(PlayerShieldPickup/EnemyShieldPickup)` + `RaiseObstacleCreatedDynamic` — level'a elle pickup koymak gerekmez (movable-spawn kuralı: hücrede altta kalabilir içerik varsa beneath'e alınır).
- **Enrage (Faz 4):** son dalga HP < %25 → interval %30 kısalır, gövde kızarır.

## 4) Fazlar

**Faz 1 — Veri + dalga iskeleti** ✅ TAMAMLANDI (2026-07-13)
BossWaveDef + LevelData alanları (`bossWaves`, `bossWaveCount`) + BossDifficulty formülü + fallback okuma; controller'da dalga makinesi (StartWave/WaveTransitionRoutine, çöküş→sağdan giriş→WAVE banner→%15 heal), HpBar dalga pip'leri, oil dalga-parametreli, overkill clamp'i ile goal defteri korunur. Tek dalgalı eski davranış birebir korunur. LevelDataEditor'a Waves bölümü eklendi.

**Faz 2 — Counterplay** ✅ TAMAMLANDI (2026-07-15)
Kesilebilir şarj saldırısı: b≥2'de açılır, radyal ring + ortada "kaç taş kaldı" sayacı, pencerede 10+b taş kırılırsa iptal → 2.5 sn stun (hasar 1.5×, saldırılar durur); kesilemezse 3× çift-namlu atış. Renk zayıflığı: b≥1, boss kafasında rozet (TileIconLibrary atanırsa gerçek taş ikonu, yoksa renk rozeti), 10 sn rotasyon, zayıf renk vuruşu 2× + parlak crit bolt. pendingStrikes int → tip etiketli strikeQueue. Sahne işi: BossDuelController'a TileIconLibrary atamak opsiyonel.

**Faz 2.5 — Okunabilirlik** ✅ TAMAMLANDI (2026-07-15)
Board BossDuel'de BottomArea üstüne yaslanır (`boardBottomAnchor` sahne ref'i + `ShiftBoardHome`; entrance slide hedefi canlı `shakeBasePos`'tan okur). Toast sistemi (kuyruklu, tek tek oynar, `toastAnchoredPos` ile konum): şarj başlangıcı ("X taş kır!"), kesme ("KESTİN! ×1.5"), büyük saldırı, kalkan bildirimleri. Kalıcı etiketler: zayıflık rozetinde "×2", şarj ringinde "KIR!". İlk-karşılaşma öğreticileri (bir kez, PlayerPrefs: boss_tip_charge_seen / boss_tip_weakness_seen) uzun+strong toast olarak. 8 lokalizasyon anahtarı (boss_toast_*/boss_tip_*/boss_charge_break_label).

**Faz 3 — Oyuncu gücü** ✅ TAMAMLANDI (2026-07-16)
Süper lazer: 40 taşta dolan radyal rozet (player HP barının SOL dışı, zayıflık rozetiyle simetrik), dolunca nabız + tıklanabilir; kalın beam, 12×damagePerTile, düşman kalkanını DELER ve söker; dalga geçişinde sıfırlanmaz. Special bonus: BoardController.OnSpecialActivated (SpecialBehaviorDispatcher.ApplySpecialActivation'dan yayınlanır — zincir dahil) → Line +5 / Pulse +8 / Override +12 düz vuruş (crit almaz), PatchBot boss'a direkt 6× mor mermi. Pickup spawner: dalga parametreli (yeşil b≥1 22sn, mor b≥3 30sn), board idle iken rastgele normal taş pickup'a dönüştürülür (TrySpawnSingleCellObstacleAt + RefreshTileObstacleVisual + pop). 2 lokalizasyon anahtarı (boss_toast_laser_ready, boss_tip_laser).

**Faz 4 — Feel & denge**
Hasar sayısı popup'ları, enrage, SFX/VFX pass, LevelDataEditor "Boss Auto-Fill" butonu, formül tuning (gerekirse SimBot'a duel simülasyonu eklenebilir — opsiyonel).

## Dokunulacak dosyalar

- `Core/LevelData.cs` — BossWaveDef + bossWaves listesi
- `Grid/Board/BossDuelController.cs` — state machine + yetenekler (gerekirse `BossDuel/` klasörüne parçalanır: `BossWaveRuntime.cs`, `BossDifficulty.cs`, `BossChargeAttack.cs`)
- `UI/HpBar.cs` — segment desteği
- `Editor/LevelDataEditor.cs` — Auto-Fill butonu (Faz 4)
- Sahne: süper lazer barı + buton, telegraph ring, renk ikonu (prefab/UI kurulumunu tek-tık Editor menüsüyle vermek mümkün — Mockup kalıbı)

## Açık noktalar (implementasyonda karara bağlanacak küçükler)
- Dalga geçişinde oyuncu heal miktarı (%15 öneri) ve pendingStrikes'ın taşınması (taşınır — birikmiş vuruş kaybolmaz).
- Süper lazer barının dalgalar arası sıfırlanıp sıfırlanmayacağı (önerim: sıfırlanmaz).
- PatchBot'un düelloda board hedefi mi boss hedefi mi seçeceği (önerim: boss'a uçar).
