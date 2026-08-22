# Unified Special / Combo / MatchClear — Tek Senkron Akış Planı

Amaç: Tüm **special davranışları + combo davranışları + MatchClear**'ı, kendi zamanlamasını/settle'ını
ayrı yöneten dağınık yapıdan çıkarıp **tek, senkron, tek-otoriteli** bir akışa taşımak. Sonuç: takılmayan
(perf), kesilmeyen (flow), tutarlı (davranış) bir board akışı.

Kullanıcı yönergesi (2026-08-16): "Bütün special/combo/matchclear'ı inceleyip senkron bir yapı kurmalıyız."
Kural: **perf = ölçüm, tahmin değil.** Bu plan önce tasarım; kod değişikliği ayrı fazlarda, flag'li, cihaz-testli.

**Güncel kontrol notu (2026-08-21):** Bu doküman artık yalnız başlangıç planı değil, yaşayan teknik kayıt olarak
tutuluyor. Repo tarafında FlowScheduler adapter'ı, per-column async fall, TileRuntimeState/ReservedFor temeli,
normal dynamic input ve special dynamic input'un ilk güvenli kapısı kodlandı. Aşağıdaki faz listesinde eski
"gelecek iş" ifadeleri korunuyor ama güncel durum Bölüm 8'de takip ediliyor.

---

## 1. Problem: neden dağınık, neden bug üretiyor

Her special/combo/clear, icra için 5 primitivi **ayrı ayrı ve tutarsız** karıştırıyor:

| Primitiv | Ne yapar | Kim bekler |
|---|---|---|
| ActionSequencer (blocking) | `Blocking=true` action kuyrukta sırayla oynar | `while(IsPlaying)` |
| Non-blocking sequencer action | `StartCoroutine`, beklemez; kendi Resolve-job'unu tutar | `DetachedActionsInFlight` (yeni), `BlockingBackgroundJobs` |
| StartImmediateAction/Sequence | Resolve-kind background job (paralel) | `BlockingBackgroundJobs` |
| Async flight sayaçları | PatchBotDash / GoalOrb / KeyFlight (non-blocking) | yalnız level-end |
| RequestResolveAfterActionSequence | resolve'u "iste" (edge-triggered) | FlowScheduler pump (yeni) |
| Kendi settle-wait'i | `while(BlockingBackgroundJobs>0 && safety<5f)` | — |

**Tek bir "kim ne zaman düşer / board ne zaman oturdu" sözleşmesi YOK.** Her davranış kendi kombinasyonunu
seçiyor. Bu seansın TÜM bug'ları bunun sonucu:

- **Lost-wakeup** (OBB): `RequestResolveAfterActionSequence` edge-triggered; blocking job sessizce bitince
  kimse resolve'u uyandırmadı → board Idle ama taşlar havada. (Faz A global pump ile kısmen kapandı.)
- **Self-count 5s** (SpecialChainRunner): root final-settle `while(BlockingBackgroundJobs>0)` ama root'un
  KENDİ job'unu sayıyor → hep 5s cap. (baseline ile yamandı.)
- **Sticky special-phase**: `IsSpecialActivationPhase` elle toggle → coroutine cleanup atlarsa asılı kalıyor.
- **Tek-frame spike** (Override+PatchBot): N bot Execute (tarama+alloc) tek frame. (spread ile yamandı.)
- **OBB 2x wind-up + kuyruk**: kendi ProcessQueue settle-wait'i, ayrı zamanlama.

Yani her yama aynı kök-hastalığın farklı belirtisi. Tek çözüm: **senkron sözleşme + tek otorite.**

---

## 2. Mevcut envanter (kod haritası)

### 2.1 Dispatch (kim seçilir, nasıl koşar)
- **Swap-combo yolu:** `SpecialBehaviorDispatcher.ApplyComboEffect` → bespoke combo sınıfları
  (LineVLineHCombo, PulsePulseCombo, LineVHPulseCoreCombo, LineH/V PatchBotCombo, PulseCorePatchBotCombo,
  OverrideSpecializedCombo, OverrideOverrideCombo, PatchBotCombo). **Her combo kendi Execute + kendi runtime
  + kendi zamanlama.** (9 ayrı sınıf, ortak sözleşme yok.)
- **Tekli/zincir special yolu:** `ActivationQueueProcessor` (Queue + `ProcessQueue` while-loop) →
  `SpecialResolver` → per-special behavior (LineV/H, PulseCore, Override, PatchBot) veya `SpecialChainRunner`.
- **MatchClear yolu:** `MatchClearAction.RunClear` — special CREATION + presentation/clear + arrival-tetikli
  special ACTIVATION (line sweep, pulse). Hem sequencer'da hem PresentationFx job'ı olarak koşabilir.

### 2.2 İcra primitivi kullanımı (kim hangi yolu seçmiş)
- **Blocking sequencer action:** MatchClearAction, SpecialChainRunner, OverridePatchBotAirborneGroupAction,
  PendingTriggeredSpecialScopeAction, OverrideSpecializedCombo iç-action'ları.
- **StartImmediateAction/Sequence (Resolve job):** LineVHPulseCoreCombo, SpecialChainRunner, SpecialResolver,
  BossDuel.
- **RequestResolveAfterActionSequence:** LineH/V PatchBotCombo, PatchBotSpecial, OverridePatchBotAirborneGroup,
  RocketBasketLaunchAction, OverrideBatteryBoxDetonationAction, KeyGeneratorService.
- **Async flight sayaçları:** PatchBotDash (dash/rocket/combo), GoalOrbFlight, KeyFlight, BossStrikeDrain.
- **Kendi settle-wait'i:** SpecialChainRunner (root final settle), OBB ProcessQueue, oil spread, BonusMoves.

### 2.3 Zamanlama otoriteleri (dağınık — sorunun kalbi)
`ActionSequencer.IsPlaying` + `BlockingBackgroundJobs` + `DetachedActionsInFlight` + `IsBusy/busyScopeDepth`
+ `IsSpecialActivationPhase` + `ActiveBackgroundJobs` + her davranışın kendi `while/safety` döngüsü.
**Yedi ayrı "meşgul mü" sinyali**, hiçbiri tek otorite değil.

---

## 3. Hedef: tek senkron model (FlowScheduler)

### 3.1 Temel ilke — tek otorite, activity kaydı
Tek bir **FlowScheduler** "board oturdu mu / sıradaki adım koşabilir mi"nin TEK sahibi olur. Her async iş
(fall, clear, special-sweep, combo-step, obstacle-spread, dash, orb, chain) tipli bir **Activity** olarak
kaydolur (`Begin(kind)` → handle) ve bitince kapanır. Scheduler, **activity seti her değiştiğinde** (Begin/End)
"pipeline ilerleyebilir mi" diye yeniden değerlendirir (continuous, edge DEĞİL) → **lost-wakeup imkânsız**.

`IsSpecialActivationPhase` elle-toggle bool DEĞİL, **türev** olur: "kayıtlı special-sweep activity var mı".
→ stuck-flag imkânsız. Kimse global sayaç üzerinde `while(count>0)` beklemez (self-count imkânsız); "kendi
spawn ettiğim activity'ler bitti mi"yi handle üzerinden bilir.

### 3.2 Tek icra sözleşmesi — her special/combo/clear aynı arayüz
Bugün 3 yol (bespoke combo Execute / ActivationQueue / MatchClear) var. Hedef: **tek `ISpecialFlowStep`**
sözleşmesi. Her special, combo ve clear şunu üretir/uygular:
- **Karar (anında, senkron):** hangi hücreler etkilenir, ne oluşur/tetiklenir (mevcut karar mantığı KALIR).
- **Sunum (Activity olarak):** görsel+hasar, FlowScheduler'a kayıtlı; kendi "bekle" döngüsü YOK.
- **Bağımlılık:** "benim tetiklediğim alt-zincir/fall'lar" scheduler'a bağlı; root final-settle = scheduler'ın
  "benim activity ağacım bitti mi"sı (global sayaç değil).

Combolar bespoke kalabilir (davranış farkı) ama **hepsi aynı Activity sözleşmesini** kullanır → zamanlama
birleşir. SpecialChainRunner tek yayılım motoru olarak korunur ve bu sözleşmeye oturur.

### 3.3 Perf sözleşmesi — iş kontrollü serpilir + Object Pooling
Scheduler bir **frame-bütçesi** bilir: tek frame'de N'den fazla ağır iş (board taraması, GameObject/VFX
instantiate, clear+cascade) başlamaz; fazlası sıradaki frame'e kayar. Override+PatchBot spread'i bunun
manuel/özel hali — genelleştirilir.

**OBJECT POOLING (en büyük GC kazancı — DURUM: YOK).** Ölçüldü (2026-08-16): taşlar `GridSpawner.SpawnTile`
`Instantiate(tilePrefab)` + `BoardController.ClearAndDestroyTile` `Destroy(tile.gameObject)`; toplam kod
tabanında **70 Instantiate + 231 Destroy + 218 `new GameObject`** (taş + VFX dust/dash/mud/beam + floating).
Match-3'te saniyede onlarca taş yarat-yok-et → bellek parçalanması + GC spike → cihaz donması. **Çözüm:**
(1) **TileView havuzu** — oyun başında max taş (grid + yedek) yaratılıp Deactive kuyruğa alınır; spawn =
`Dequeue`+Active+veri set, clear = anim sonra reset+Deactive+`Enqueue` (Destroy YOK). Dinamik genişleme
(havuz boşalırsa 5-10 ekle). **Kirli-veri sıfırlama ŞART** (renk/stage/obstacle/combo kalıntısı temizlenmeli
— yoksa havuzdan eski efektle çıkar). (2) **Multi-pool** — particle/break-fx, floating-text/skor, dash/beam
görselleri için ayrı küçük havuzlar (`Dictionary<key, Queue<GameObject>>`). **Enabler HAZIR:** MVC ayrımı
zaten var (`GridData`=saf veri ↔ `TileView`=kukla); mantık taş yaratmıyor → pooling yalnız SpawnTile/
ClearAndDestroyTile + VFX create/destroy'unu "al/iade"ye çevirir, oyun mantığına DOKUNMAZ. GC: ayrıca
hot-path `new List/HashSet/Action` da havuzlanır. Böylece "toplu" görsel korunurken tek-frame + GC spike
yapısal olarak imkânsız olur.

---

## 4. Bug sınıfları → tek modelde nasıl kökten biter

| Bug | Bugünkü yama | Tek modelde |
|---|---|---|
| Lost-wakeup | FlowScheduler pump (Faz A) | Activity End'de otomatik re-değerlendirme (pump gereksiz) |
| Self-count 5s | baseline | Kendi activity ağacını bekler, global sayaç yok |
| Sticky special-phase | sticky flag | Türev property (kayıtlı sweep var mı) |
| Tek-frame spike | manuel spread | Scheduler frame-bütçesi (genel) |
| OBB kuyruk/wind-up | özel ProcessQueue | Genel Activity + bağımlılık |
| Çift-patlama / çift-clear | işlenmiş-set + barrier | Tek pipeline, tek işlenmiş-set |

---

## 5. Fazlı yol haritası (her faz flag'li, cihaz-testli, geri-alınabilir)

**Ön koşul (ölçüm):** Perf logger (frame-time + GC.GetTotalAllocatedMemory delta, spike'ta yaz — OBBTL
trace deseni). Cihazda repro → Editor.log'dan hotspot. Bu, her fazın "spike gitti mi"sini doğrular.
Bkz. [[project_device_stutter_perf_pass.md]].

- **Faz 0 — Envanter+harita:** Bu doküman yazıldı; perf ölçüm yaklaşımı belirlendi.
- **Faz 1 — FlowScheduler çekirdeği / adapter:** Activity kaydı + tek "settling" otoritesi. Mevcut 7 sinyali
  (IsPlaying, BlockingBackgroundJobs, DetachedActionsInFlight, IsBusy, IsSpecialActivationPhase...) TEK
  API'nin arkasına alınmaya başladı; davranış aynı kalsın (adapter). RequestResolveAfterActionSequence + pump
  scheduler üzerinden ilerliyor, eski sinyaller tamamen silinmedi.
- **Faz 2 — MatchClear'ı sözleşmeye oturt:** creation/clear/arrival-activation Activity olarak. Sticky
  special-phase türev'e çevrilir.
- **Faz 3 — Special'ları (LineV/H, PulseCore, Override, PatchBot) sözleşmeye taşı:** SpecialChainRunner tek
  yayılım motoru; kendi settle-wait yerine scheduler bağımlılığı.
- **Faz 4 — Combo'ları taşı:** 9 bespoke combo aynı Activity sözleşmesini kullanır (davranış korunur, zamanlama
  birleşir). Bespoke kalabilirler ama tek zamanlama.
- **Faz 5 — Perf sözleşmesi:** frame-bütçesi + hot-path pool'ları. Spike'ları ölçümle doğrula.
- **Faz 6 — Temizlik:** eski sinyaller/kendi settle-wait'ler/sticky flag'ler kaldırılır (artık ölü).
  Bu hâlâ açık teknik borç.

### NİHAİ HEDEF: Dynamic Board (Faz 7-8) — ayrı iş, Faz 2-6'nın ÜSTÜNE kurulur
Kullanıcı hedefi (2026-08-16): taşlar düşerken/zincir sürerken oyuncu boş köşede hamle yapabilsin
("asenkron girdi"). **Faz 3-4 BU DEĞİL** — onlar special/combo'yu ortak sözleşmeye taşır, yani ön koşul.
Sıra zorunlu: special'lar hâlâ "tüm board'u durduran" modeldeyken hücre-bazlı kilit koymak kazanç
getirmez (oyuncu hamle yapar ama zincir yine her şeyi dondurur).

- **Faz 7 — Per-column async:** Düşüş/çözüm sütun bazına ayrıldı: 2-3. sütun çalkalanırken 6-7. sütun
  gerçekten boşta kalabilir.
  (Perf faydası da var: sadece aktif sütunlara CPU.)
- **Faz 8 — Cell-based FSM + input-while-falling:**
  - Her taşta `TileRuntimeState` (Idle / Swapping / Falling vb.); her hücrede rezervasyon durumu.
  - Girdi kontrolü busy sırasında **dokunulan iki taşın** durumuna, hücre snapshot'ına ve sütun busy sinyaline
    bakar. İkisi de stabil ise board'un geri kalanında akış sürerken hamleye izin.
  - **Destination-cell rezervasyonu:** düşen taş hedef hücreyi rezerve eder → oyuncu oraya kaydıramaz
    (klasik dinamik-tahta race condition'ı: iki taş aynı hücreye girip çökme).
  - Sütun kilidi: patlama/düşüş olan sütun "meşgul", diğerleri Idle.
  - Special dynamic input ilk kapı: special taş + stabil komşu varsa gravity/fall sırasında swap açılır;
    special visual/sweep uçuşta ise kapalı kalır.

**MEVCUT ALTYAPI (sıfırdan değil):** `pendingTriggeredSpecialCells` = hücre rezervasyonunun bir formu
(gravity-blok; OBB 2x2 footprint'i bununla çözüldü). `activeMoveToken` ([[project_move_token_ownership]])
= taş sahipliği/çift-sürme guard'ı. `TileView.FallGeneration` + `CancelActiveSettle` = animasyon sahipliği.
`IsPlannedToMoveThisFallPass` = taş fall bayrağı. Güncel kodda buna ek olarak `TileRuntimeState`,
`BoardCellStateSnapshot`, target reservation ve `Flow.IsColumnSettling` üzerinden dynamic input gate çalışıyor.
Eksik kalan ana parça: special/combo/clear yollarındaki eski zamanlama sinyallerinin tamamen silinmesi ve tüm
special dynamic input senaryolarının cihazda regresyon setiyle kapatılması.

Her faz: flag arkasında (eski yol yanında), regresyon setiyle cihazda doğrulanır → onaylanınca eski yol silinir.

## 6. Regresyon seti (her fazda çalıştırılacak)
LevelP_0060 (diagonal), boss-duel, OBB detonation (tek + çoklu), mega-combo (Pulse+Override+PatchBot+2×LineV+
LineH), Override+PatchBot toplu fanout, mud-yoğun level, obstacle spread (barrel/oil), goal-orb + PatchBot
uçuşları, deferred streak delivery. Kırılırsa tek faz geri alınır.

## 7. Riskler
- En büyük risk: davranış regresyonu (özellikle combo timing/feel). → faz faz, flag, cihaz-testi zorunlu.
- İkinci: "birleştirme yine kısmi kalır" ([[feedback_verify_dont_claim_done]]). → her faz sonunda ESKİ yolun
  silinmesi şart; combo hâlâ bespoke Execute kullanıyorsa faz BİTMEMİŞ sayılır.
- Kullanıcı teknikte yorum yapmıyor → kararları ben veririm, sonucu (akıcı/kesilmeyen/takılmayan) cihazda test eder.

## 8. Durum
Güncel durum (2026-08-21):

- **Faz 1 adapter:** `BoardFlowScheduler` repo içinde var; `IsSettling`, `CanStartResolveStep`,
  `IsSpecialVisualInFlight`, activity count ve continuous pump tek API arkasında çalışıyor.
- **Faz 7:** Per-column gravity/fall yolu kodlandı (`ColumnFlowEngine`, `ParallelColumnFallAction`,
  column busy/fall visual counters). Dynamic board'un sütun bazlı ön koşulu artık mevcut.
- **Faz 8A-8D:** `TileRuntimeState`, `BoardCellStateSnapshot`, reservation kontrolleri, busy sırasında normal
  idle tile swap, entry revalidation, busy-drag gate ve late busy-click suppress kodlandı.
- **Faz 8E / special dynamic input:** Special taşlar artık düşüş akarken tamamen yasak değil. Dynamic gate,
  special taşın kendi hücresi idle/stabil ise ve en az bir komşusu da stable dynamic-input cell ise swap'a izin
  veriyor. `Flow.IsSpecialVisualInFlight` açıkken kapı hâlâ kapalı; special sweep/dash/chain sırasında resolver
  race'i engelleniyor.
- **Faz 9 tarafı:** Obstacle/OBB ve bazı non-blocking işlerin Flow activity adaptörleri başladı; eski sinyaller
  tamamen temizlenmediği için bu hâlâ adapter dönemi.

Sıradaki teknik borç:
- Special dynamic input'u cihazda özellikle `Line`, `PulseCore`, `PatchBot`, `SystemOverride`, special+special
  combo ve special+normal match yaratma senaryolarıyla doğrula.
- `useDynamicBoardInputGate`, `usePerColumnGravity`, `usePerColumnAsyncFalls`, `useFlowActivities` açıkken
  regresyon setini aynı pass içinde çalıştır.
- Eski global busy/special-phase sinyallerini ancak bu doğrulama sonrası sadeleştir.

İlgili: [[project_device_stutter_perf_pass]], [[project_special_chain_runner]], [[project_decoupled_resolve]].
