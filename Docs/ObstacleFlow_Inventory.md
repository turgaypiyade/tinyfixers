# Obstacle Akış Envanteri — hit / yok olma / süreler

Amaç: obstacle davranışlarını ortak API'ye (`BoardFlowScheduler`, bkz. `UnifiedSpecialFlow_Plan.md`)
bağlamadan önce **kim nasıl hasar alıyor, nasıl yok oluyor, süreyi kim sahipleniyor** haritası.
(2026-08-16 kod okumasıyla çıkarıldı; kod DEĞİŞTİRİLMEDİ.)

---

## 1. HASAR YOLU — tek kapı ✅ (iyi haber)

Tüm hit'ler tek huniden geçiyor:

```
<çağıran: match clear / special sweep / booster / scripted>
  → BoardController.ApplyObstacleDamageAt(x, y, context, sourceType?)
  → ApplyObstacleDamage(ObstacleDamageRequest)
  → ObstacleStateService.<damage core>
  → ObstacleHitResult { didHit, consumedHit, rejectedByRule, visualChange, stageTransition, affectedCellIndices }
  → BoardController.TriggerObstacleVisualChange(visualChange)
       ├─ BoardBreakFxService.PlayObstacleBreak → particle + SES (hit/break/stage)
       └─ ObstacleVisualChanged event → GridSpawner (view/sprite/stage güncelle)
```

- **27 çağrı yeri** var ama hepsi aynı iki overload'dan geçiyor → migrasyon için **tek nokta**.
- `ObstacleHitContext` (NormalMatch / SpecialActivation / Booster / Scripted) hasar kuralını belirliyor.
- **Hit yolu SENKRON**: hasar + görsel-değişim tetiği aynı frame. Süre/bekleme YOK. → FlowScheduler'a
  taşınırken hit'in kendisi Activity GEREKTİRMEZ; asıl mesele aşağıdaki **yok-olma** dalları.

## 2. YOK OLMA YOLU — 8 ayrı zamanlama modeli ⚠️ (asıl sorun)

`ObstacleStateService` → `OnObstacleDestroyed` → `BoardController.HandleObstacleDestroyed` — ve burada
her obstacle **kendi zamanlama modeline** dallanıyor:

| Obstacle | Yok olunca ne oluyor | İcra modeli | Akışı bekletir mi? |
|---|---|---|---|
| **Barrel** (mud splat) | `CoSpreadBarrelMudImmediate` → `BarrelSpreadAction` | `BeginJob(ObstacleSpread)` + sonda `RequestResolveAfterActionSequence` | ❌ hayır (async; yalnız level-end bekler) |
| **Safe** | `CoHoldResolveForSafeBreak` | `BeginBackgroundJob()` = **Resolve-kind** + `WaitForSeconds(0.5f)` | ✅ **EVET — resolve'u 0.5s parklıyor** |
| **Oil** | overlay gizle, `return` | senkron | ❌ hayır |
| **Movable** (plastic vb.) | `ClearAndDestroyTile` + `NotifyTilesCleared` | senkron | ❌ hayır |
| **OverrideBatteryBox** | ayrı event → `_obbDetonationQueue` → 2s wind-up → burst → dalga | kendi kuyruğu + **kendi settle-wait döngüsü** (12s safety) | ✅ evet (kendi mantığıyla) |
| **KeyGenerator** | key üretimi/uçuşu | `BeginJob(KeyFlight)` + `RequestResolveAfterActionSequence` | ❌ hayır (non-blocking) |
| **RocketBasket** | settle'da kuyruktan fırlat | `StartCoroutine(...ExecuteVisuals(null))` fire-and-forget (PatchBotDash sayacı) | ❌ hayır |
| **EnergyContainer** | `EnergyContainerFx` (kapak/orb animasyonları) | kendi coroutine'leri, ~12 serialized süre | kısmi |
| *(ayrıca)* **Oil spread** | ResolveBoard döngüsü içinde | `actionSequencer.Enqueue(OilSpreadAction)` + `while(IsPlaying)` | ✅ **EVET — kuyrukta blocking** |

**Sonuç:** hit tek kapıdan, ama yok-olma **8 farklı icra primitifi** kullanıyor (Resolve-job, async-job,
sequencer-blocking, fire-and-forget, kendi kuyruğu, senkron). Bu, special/combo tarafındaki dağınıklığın
birebir aynısı → aynı bug sınıflarını (lost-wakeup, self-count, sıralama) obstacle tarafında da üretebilir.

## 3. SÜRE SAHİPLİĞİ — veri değil, kod ⚠️

**`ObstacleDef`'te tek bir süre/duration alanı YOK.** Yalnız `hits`, sprite'lar, sesler ve davranış
bayrakları var. Tüm zamanlamalar koda/prefab'a gömülü:

| Süre | Yer | Değer |
|---|---|---|
| Safe break hold | `BoardController.SafeBreakResolveHold` | `0.5f` (const) |
| OBB burst stagger | `BoardController.ObbDetonationBurstStaggerSeconds` | `0.30f` (const) |
| OBB wind-up | `OverrideBatteryBoxView.detonationDuration` (prefab) **+** `OverrideBatteryBoxDetonationAction.BoxBurstTime` (const) | `2f` / `2f` — **İKİ YERDE, elle eşlenmeli** ⚠️ |
| Hit FX ömrü | `BoardController.obstacleHitFxLifetime` | `0.30f` |
| Break FX ömrü | `BoardController.obstacleBreakFxLifetime` | `0.40f` |
| Tile break FX | `BoardController.tileBreakFxLifetime` | `0.35f` |
| EnergyContainer | `EnergyContainerFx` ~12 serialized alan | halfOpen 0.07 / fullOpen 0.08 / hold 0.04 / close 0.12 / fly 0.42 / orbStagger 0.035 … |
| KeyGenerator | `KeyGeneratorMachineView` cfg.crankHold vb. | prefab |

**Riskler:** (a) OBB süresi **çift kaynak** — biri değişirse toz/patlama desenkron olur (bugün yaşandı);
(b) süreler data-driven olmadığı için level/obstacle bazında ayar imkânsız; (c) "tempo" ayarı
(`ApplySpecialChainTempo`) yalnız bazı yollara uygulanıyor, obstacle sürelerinin çoğu ondan bağımsız.

## 4. FlowScheduler'a eşleme (migrasyon taslağı)

| Bugünkü | → ActivityKind | Not |
|---|---|---|
| Barrel splat (`ObstacleSpread` job) | `ObstacleSpread` | zaten async; birebir eşlenir |
| Safe hold (`BeginBackgroundJob`) | `Clear` (blocking) | 0.5s park; Activity'ye alınınca "neden bekliyoruz" görünür olur |
| Oil spread (sequencer blocking) | `ObstacleSpread` | döngü-içi `while(IsPlaying)` yerine Activity |
| OBB detonation (kendi kuyruğu) | `SpecialSweep` + `Fall` | kendi settle-wait'i ÖLMELİ (self-count riski burada) |
| KeyGenerator / RocketBasket | `Flight` | akışı bloklamaz, level-end bekler |
| EnergyContainer FX | `Presentation` | saf görsel |
| Movable/Oil senkron dallar | — | Activity gerekmez (anında) |

**Kural (plan §3.1):** hiçbiri global sayaçta `while(count>0)` beklemeyecek; kendi handle'ını bekleyecek.

## 5. Migrasyon için öneriler (uygulanmadı, karar bekliyor)

1. **OBB süresini tek kaynağa indir** (view prefab'tan oku ya da const'u tek yerde tut) — çift-kaynak bugu biter.
2. **Süreleri `ObstacleDef`'e taşımayı düşün** (hitFxLifetime/breakHold/spreadDuration) → data-driven,
   level bazında ayarlanabilir, kodda sabit avlamak biter.
3. **Safe hold'u Activity'ye çevir** — 0.5s'lik resolve parkı bugün görünmez; Activity olunca ölçülebilir/gevşetilebilir.
4. **Oil spread'i döngüden çıkar** — `while(IsPlaying)` yerine Activity → ResolveBoard sadeleşir.
5. Migrasyon sırası önerisi: Barrel/Key/Rocket (zaten async, düşük risk) → EnergyContainer (görsel) →
   Oil spread → Safe hold → **OBB en son** (en karmaşık, kendi kuyruğu + wind-up).

## 6. Durum
Envanter çıkarıldı, kod değişmedi. İlgili: `UnifiedSpecialFlow_Plan.md` (Faz 1 çekirdeği kodlandı),
`project_override_batterybox_bugs`, `project_device_stutter_perf_pass`.
