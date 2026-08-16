# TinyFixers — Profesyonel Match-3 Mimarisi: Ana Yol Haritası

Tüm konuşulanların tek listesi: mimari şartlar, fazlar, durum. (2026-08-16)
Detaylar: `UnifiedSpecialFlow_Plan.md` (senkron akış), `ObstacleFlow_Inventory.md` (obstacle haritası),
`DecoupledResolve_Plan.md` (fall/clear overlap geçmişi).

**Durum kodları:** ✅ test edildi+onaylandı · 🧪 kodlandı, CİHAZ TESTİ BEKLİYOR · 📋 planlandı (kod yok) · 🔍 açık sorun

---

## BÖLÜM A — Profesyonel Match-3'ün 8 temel şartı (karne)

| # | Şart | Durum | Not |
|---|---|---|---|
| A1 | **Mantık/Görsel ayrımı (MVC)** | ✅ var | `GridData`(veri) ↔ `TileView`(kukla). `CalculateCascades` mantığı ANINDA çözer |
| A2 | **Hücre/Taş ayrımı** (buz taşta değil hücrede) | ✅ ilke var | Obstacle'lar hücre-index'li (`level.obstacles[]`); `ClearCell` obstacle'a dokunmaz. `Cell` NESNESİ yok → paralel diziler (bilinçli karar, §C) |
| A3 | **Ease-in düşüş** (fizik/Translate değil) | ✅ var | `TileView` custom hız+ivme modeli (`initialSpeed`+`acceleration`), fps profili ile kalibre |
| A4 | **Chain reaction** (işaretle→kuyruk→çöz) | ✅ var | `ActivationQueueProcessor`: `Queued`+`Processed` çift guard (=isDestroying), **iteratif** while → Stack Overflow riski YOK |
| A5 | **Object Pooling** (Instantiate/Destroy yok) | 🧪 kısmi | Taş havuzu kodlandı (flag `useTilePool`, default KAPALI). VFX multi-pool YOK |
| A6 | **Örtüşen patlama** (efekt beklenmeden düşüş) | 🧪 kısmi | Normal cascade fall→clear overlap ✅; **special/obstacle patlamaları hâlâ akışı bekletiyor** 🔍 |
| A7 | **Sütun-bazlı bağımsız işlem** | 📋 yok | `CalculateCascades` TÜM board'u tek batch işliyor (global) → Faz 7 |
| A8 | **Dynamic Board** (düşerken hamle) | 📋 yok | `InputLocked => Locked \|\| IsBusy` = global kilit → Faz 8 |

---

## BÖLÜM B — Faz listesi ve durumu

### ✅ TAMAMLANDI (cihazda onaylandı)
- **Decoupled Resolve Faz 1** — fall→clear overlap (event-driven `FallArrived`, `fallArrivalLeadCells=0.2`, special-safe). Onay: 2026-07-25.
- **Decoupled Faz 4 / ilk adım** — sticky `hadSpecialActivityThisResolve` → hassas `IsSpecialVisualInFlight` predikatı; post-special ~1.6s donma çözüldü. Onay + kullanıcı commit'i: `780c7bd`.
- **OBB lost-wakeup + self-count** — `[OBBTL]` trace ile kök bulundu: (a) edge-triggered resolve → global pump, (b) `SpecialChainRunner` root final-settle KENDİ job'unu sayıyordu (hep 5s cap) → baseline fix. Kullanıcı: "evet çözüldü".
- **OBB çoklu-kutu zinciri** — kutular artık senkronize/bağlantılı patlıyor.
- **Override+PatchBot kalkış spike'ı** — `LaunchGroupParallel` → `CoLaunchGroupSpread` (1 bot/frame). Kullanıcı: "uçuşta taşlar beklemiyor".

### 🧪 TEST BEKLİYOR (kodlandı + commit'lendi, cihazda DOĞRULANMADI)
| İş | Nasıl test edilir | Commit |
|---|---|---|
| **Tile Object Pool** | Inspector: `useTilePool` = **AÇIK** → bol cascade oyna. Bak: (a) görsel glitch (havuzdan eski renk/special/efektle çıkan taş), (b) GC düşüşü | `83e03d0`, `42bf31a` |
| **Perf Monitor** | Inspector: `enablePerfMonitor` = **AÇIK** → log'da `[PerfSpike]` / `[PerfBaseline]`; pooling açık/kapalı `gcKB` farkı | `83e03d0` |
| **FlowScheduler Faz 1** | Bayrak YOK, aktif. Normal oyun + OBB + mega-combo → **fark olmamalı** (adapter). Fark varsa ilk şüpheli bu | `83e03d0` |
| **OBB break sesi + 2x2 footprint** | OBB patlat: ses **burst anında** mı (wind-up başında değil), footprint hücrelerine erken taş giriyor mu | `f61417b` |
| **OBB bar tükenişi** | Üst/sağ barlar da **içeriden dışarıya** eksiliyor mu (sol/alt ile aynı) | `f61417b` |
| **Streak/UFO ertelenmiş teslimat** | Level 25+, streak>0, açılışta kapalı board → ilk hamleden sonra UFO gelip special koyuyor mu (tutarlı) | `f61417b` |

### 🔍 AÇIK SORUNLAR (teşhis var, çözüm bekliyor)
- **Mud plastik altında görünmüyor** (LevelP_00540) — level datası SAĞLAM (27 hücrede mud authored), 27 view de spawn oluyor (`hasView=True`). Kök: init-render; `RefreshAllBorders` denemesi durumu KÖTÜLEŞTİRDİ → geri alındı. `[MudBeneath]` trace kodda. **Ev görevi.**
- **PatchBot vuruş-anı beklemesi** — kalkış düzeldi ama her bot hedefe vurduğunda akış duruyor → Faz 3/5 hedefi.
- **special_resolve showpiece ~1.5-2.5s** — kasıtlı gösteri; ayrı iş.

### 📋 SIRADAKİ FAZLAR (kod yok)
| Faz | İş | Neden gerekli |
|---|---|---|
| **Faz 2** | MatchClear'ı Activity sözleşmesine taşı | Sticky special-phase türev'e döner |
| ~~Faz 3~~ ✅ | Special'lar | **BİTTİ.** PatchBot arrival clear'ı blocking kuyruğa giriyordu → `isBlocking:false` ile çözüldü (vuruşlar arası donma gitti). LineV/H·Pulse·Override'da yapılacak iş YOK: `SpecialChainRunner` halkaları `StartImmediateAction` ile paralel koşuyor, zincirler arrival-trigger ile eşzamanlı → oradaki sıralılık **kasıtlı koreografi**, dokunmak showpiece'i bozar |
| **Faz 4** | 9 bespoke combo'yu taşı | Zamanlama birleşir (davranış korunur) |
| **Faz 5** | Perf sözleşmesi: frame-bütçesi + VFX multi-pool + hot-path alloc pool | Tek-frame spike yapısal olarak imkânsız |
| **Faz 6** | Temizlik: eski sinyaller/settle-wait/sticky flag sil | Bir faz, eski yol silinene kadar BİTMİŞ sayılmaz |
| **Faz 7** | **Per-column async** (global gravity → sütun bazlı) | A7; dinamik tahtanın ön koşulu |
| **Faz 8** | **Cell-based FSM + Dynamic Board** | A8: `TileState`(Idle/Swapping/Matched/Falling) + destination-cell rezervasyonu + input global `IsBusy` yerine **dokunulan iki taşa** bakar + sütun kilidi |
| **Faz 9** | Obstacle'ları ortak API'ye bağla | `ObstacleFlow_Inventory.md` sırası: Barrel/Key/Rocket → EnergyContainer → Oil spread → Safe hold → **OBB en son** |
| **Faz 10** | Level design L1-50 | Motor akıcı olsa da ilk 50 sıkarsa oyuncu kalmaz (§D) |

---

## BÖLÜM C — Bilinçli kararlar (tekrar açılmasın)

1. **`Cell` NESNESİNE tam geçiş YAPILMAYACAK (şimdilik).** Gerekçe: asıl kazanç (çevresel state hücrede)
   ZATEN var — obstacle'lar hücre-index'li, `ClearCell` obstacle'a dokunmuyor, mud taş patlayınca kalıyor.
   Geriye kalan "paralel dizi → nesne" dönüşümü `gridData`/`tiles`/obstacle servisi/cascade/matchfinder'ın
   TAMAMINA dokunan big-bang; kazanç/risk kötü. **Bunun yerine Faz 8'de eksik iki parça resmîleşecek:**
   `ReservedFor` (rezervasyon; altyapı = `pendingTriggeredSpecialCells`) + `TileState` FSM (bugün dağınık:
   `IsPlannedToMoveThisFallPass`, `FallGeneration`, `activeMoveToken`).
2. **Faz 3-4 ≠ Dynamic Board.** Onlar ön koşul; dinamik tahta Faz 7-8.
3. **Perf = ölçüm, tahmin değil.** Her perf iddiası `[PerfSpike]`/`[PerfBaseline]` ile kanıtlanacak.
4. **Her faz flag'li + geri alınabilir + regresyon setiyle cihazda doğrulanacak.**
5. **Süreler kodda dağınık** (ObstacleDef'te duration alanı yok; OBB wind-up İKİ kaynakta) → Faz 9'da tek kaynağa.

## BÖLÜM D — Level design (L1-50) merkez kurallar
Tek-seferde-tek-yenilik · testere-dişi zorluk (düz rampa değil) · ilk ~10 bölüm neredeyse garanti kazanç ·
special'ı önce YARATTIR sonra GEREKTİR · goal çeşitliliği (3-4 bölümde bir tip değiştir) · obstacle sırası
(Mud→Plastic→Safe/Magnet→Oil/Barrel→OBB/KeyGen) · zorluk kollarını TEK TEK çevir (hamle→şekil→obstacle→goal→renk) ·
~15. bölümden sonra "az kaldı" (near-miss) · **SimRunner/SimBot ile kazanma-oranı bandı** (L1-10 ~%90, L11-25 ~%70-80,
L26-50 ~%50-65) · 3-yıldız erken ulaşılabilir olsun.

## BÖLÜM E — Regresyon seti (her faz sonunda)
LevelP_0060 (diagonal) · boss-duel · OBB (tek + çoklu) · mega-combo (Pulse+Override+PatchBot+2×LineV+LineH) ·
Override+PatchBot toplu fanout · mud-yoğun level · barrel/oil spread · goal-orb + PatchBot uçuşları ·
streak deferred delivery.
