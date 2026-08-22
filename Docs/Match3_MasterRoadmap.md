# TinyFixers — Profesyonel Match-3 Mimarisi: Ana Yol Haritası

Tüm konuşulanların tek listesi: mimari şartlar, fazlar, durum. (2026-08-16)
Detaylar: `UnifiedSpecialFlow_Plan.md` (senkron akış), `ObstacleFlow_Inventory.md` (obstacle haritası),
`DecoupledResolve_Plan.md` (fall/clear overlap geçmişi).

**Durum kodları:** ✅ test edildi+onaylandı · 🧪 kodlandı, CİHAZ TESTİ BEKLİYOR · 📋 planlandı (kod yok) · 🔍 açık sorun

---

## TEMEL PRENSİP — Board akışı en az durdurulacak

Her yeni special, obstacle, booster ve VFX çalışmasında ana kural: **board akışı mümkün olan en erken
anda serbest bırakılır.** Bir efektin görsel süresi, taş düşüşünü veya dynamic input'u durdurmak için
tek başına yeterli sebep değildir.

- Bir hücre/taş gameplay açısından temizlendiyse veri **hemen** commit edilir (`GridData` + `TileView`
  senkron boşaltılır), ardından cascade/refill çalışır.
- Sonrasında oynayan uçuş, parçalanma, ışın, tube, splash, orb veya kırılma efektleri **detached visual**
  olmalıdır. Bu işler en fazla level-end için beklenir; resolve/input'u bloklamaz.
- Async görsel, gerçek `TileView` objesini board dışına sürüklememelidir. Board'dan çıkan taş için
  gerekiyorsa bağımsız sprite/image klonu kullanılır; canlı `TileView` ya gridde kalır ya pool'a döner.
- `PresentationFx` veya blocking job yalnızca sonraki gameplay verisi o işin sonucuna gerçekten bağlıysa
  kullanılır. Saf görsel VFX için default seçim non-blocking async'tir.
- `GridData` ↔ `TileView` mismatch kabul edilemez. `Mismatch count > 0` board state bozulmasıdır; fail-fast
  edilmeli, oyun bozuk state ile devam etmemelidir.

Örnek: Magnet bir taşı seçtiği anda hedef hücre board verisinden temizlenir ve düşüş başlar; magnete giden
taş yalnızca detached görsel klondur. Magnetlerin yok olma parçalanması da board düşüşünden bağımsız,
asenkron overlay VFX olmalıdır.

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
| A7 | **Sütun-bazlı bağımsız işlem** | ✅ var | Faz 7A ✅: `CalculateCascades` sim'i `ColumnFlowEngine`'e taşındı (flag `usePerColumnGravity`) ve cihazda birebir doğrulandı. Faz 7B ✅: fall görseli `ParallelColumnFallAction` ile hedef sütunlara bölünüp paralel oynatılıyor (flag `usePerColumnAsyncFalls`) ve cihazda problem görülmedi. Faz 7C ✅: runtime `ColumnBusy` + `Flow.IsColumnSettling()` hook'u cihazda doğrulandı (normal log yok, underflow yok) |
| A8 | **Dynamic Board** (düşerken hamle) | 🧪 8D hardening kodlandı | Faz 8A ✅: `TileRuntimeState` + generic `ReservedFor` snapshot. Faz 8B ✅: busy sırasında idle normal tile/sütun swap input'u cihazda akıcı/doğru çalıştı. Faz 8C ✅: entry revalidation + special/PatchBot görsel zinciri sırasında dynamic input kapısı cihazda doğrulandı. Faz 8D: busy-drag TileView katmanında da dynamic gate'e bağlandı; test slow-mo release build'de no-op |

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
| **Faz 8D dynamic hardening** | `useDynamicBoardInputGate` + per-column flag'ler **AÇIK** → 8C'deki normal busy swap hâlâ hemen çalışmalı. Busy sırasında uygun olmayan tile'a (falling/special/special-chain sütunu) drag başlatınca tile parmakla sürüklenmemeli. Busy'de başlayıp idle'a sarkan click/drag sonradan normal swap'a dönüşmemeli. `useDynamicBoardInputTestSlowMo` release build'de etkisiz; editor/dev test knob'u olarak kalır | (bu seans) |
| **LevelP_00540 mud beneath plastic init render** | LevelP_00540 aç: sol tarafta Plastic Two Stages/movable altındaki mud'lar **ilk frame'den** görünmeli; herhangi bir plastic hareketini beklememeli. Sonra plastic hareket edince mud görünümü kaybolmamalı/çiftlenmemeli | (bu seans) |
| **Faz 9A obstacle flow adapter** | `useFlowActivities` **AÇIK** → Barrel/Barrell_v2 mud splatter, KeyGenerator key flight, RocketBasket/PatchBot dash, goal orb ve boss strike uçuşlarında davranış değişmemeli: board gereksiz beklememeli, level-end erken bitmemeli, resolve stuck/underflow olmamalı | (bu seans) |
| **Faz 9B EnergyContainer/HatLauncher flow** | `useFlowActivities` **AÇIK** → EnergyContainer/HatLauncher tetikle: open/close/exhausted sunumu akarken board gereksiz kilitlenmemeli, orb hedefe gidince goal düşmeli, exhausted frame kalmalı, resolve stuck/underflow olmamalı | (bu seans) |
| **Faz 9C Safe hold flow** | `useFlowActivities` **AÇIK** → Safe kır: ilk patlama/reveal anı hâlâ 0.5s korunmalı, sonra board akmalı; safe goal düşmeli, alt içerik reveal olmalı, resolve stuck/underflow olmamalı | (bu seans) |
| **Faz 9D OBB flow adapter** | `useFlowActivities` **AÇIK** → tek/çoklu OBB patlat: wind-up, burst sesi, 2x2 footprint açılışı, zincir OBB tetiklenmesi ve taş akışı eskiyle aynı kalmalı. Dynamic input OBB dalgası sırasında uygunsuz swap başlatmamalı | (bu seans) |

### 🔍 AÇIK SORUNLAR (teşhis var, çözüm bekliyor)
- **Mud plastik altında görünmüyor** (LevelP_00540) — fix kodlandı: `DrawMudOverlays()` stamped-beneath mud'ı temizlediği için çağrı sırası değiştirildi. Cihaz testi bekliyor.
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
| **Faz 7** | ✅ **Per-column async** tamamlandı (global gravity → sütun bazlı): 7A `ColumnFlowEngine`, 7B `ParallelColumnFallAction`, 7C runtime `ColumnBusy` + `Flow.IsColumnSettling`. Flag'ler hâlâ geri alınabilirlik için duruyor; Faz 8 bunların üstüne `ReservedFor`/`TileState` input gating'i kuracak. Plan: `~/.claude/plans/calm-wibbling-sutherland.md` | A7; dinamik tahtanın ön koşulu |
| **Faz 8** | **Cell-based FSM + Dynamic Board** başladı: **8A ✅ onaylı** (`TileRuntimeState` + `ReservedFor`). **8B ✅ onaylı**: normal tile canlı input cihazda akıcı çalıştı. **8C ✅ onaylı**: dynamic swap entry revalidation + special visual sırasında dynamic input kapalı + late busy-click suppress. **8D hardening kodlandı**: busy-drag erken gate + release build'de slow-mo no-op. Special/PatchBot dynamic input kapsamı ve eski global gate temizliği sonra | A8 |
| **Faz 9** | Obstacle'ları ortak API'ye bağla. **9A kodlandı**: `BoardFlowScheduler` blocking/non-blocking activity ayrımı aldı; `BeginJob` ve eski paired uçuş shim'leri non-blocking Flow activity kaydı üretir. **9B kodlandı**: EnergyContainer/HatLauncher open-close-exhausted sunumu non-blocking Presentation activity. **9C kodlandı**: Safe 0.5s reveal hold'u blocking `Clear` activity olarak görünür. **9D kodlandı**: OBB detonasyon dalgası non-blocking `SpecialSweep` activity olarak izlenir; kendi OBB settle döngüsü henüz kaldırılmadı. **Oil spread ayrı görsel/UV pass'e bırakıldı.** | `ObstacleFlow_Inventory.md` sırası: Barrel/Key/Rocket/PatchBot → EnergyContainer/HatLauncher → Safe hold → OBB cleanup |
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
