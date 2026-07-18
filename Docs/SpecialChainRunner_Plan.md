# Special Chain — Tek Motor (SpecialChainRunner) Tasarım Planı

Amaç: 5 special (LineV, LineH, PulseCore, PatchBot, Override) ve tüm solo/combo
varyasyonları **tek bir gerçek yayılım (propagation) motoruyla** çalışsın.

## 0. Çekirdek ilke

> Bir special tetiklenir → gerçek bir animasyon hücreden hücreye yol alır →
> **vardığı her hücrede** ne varsa (taş / obstacle / launcher / başka special)
> o ANDA tetiklenir → tetiklenen special kendi animasyonunu yayar → o da
> varışında bir sonrakini tetikler.

Sanal/önceden-hesaplanmış (deferred, snapshot affected, "X sn sonra patlat") YOK.
Zamanlama, animasyonun gerçekten yol almasından doğal çıkar.

Temel zaten var: `PulseChainSequenceAction` bunu PulseCore'lu/Line'lı bazı yollarda
yapıyor (doc'u birebir bu ilke). Plan: onu **genelleştirip** her şeyi ona bağlamak.

## 1. Çekirdek soyutlamalar

### 1.1 `ISpecialEmission` (yol alan animasyon)
Bir special'ın aktive olunca yaydığı gerçek animasyon. Tek sözleşme:
- `IEnumerator Travel(SpecialChainContext ctx)` — adım adım ilerler.
- Her yeni hücreye **varışta** `ctx.OnArrive(cell, sourceSpecial)` çağırır.
- Şekiller (emission tipleri):
  - **LineBeam(axis, originCell)** — sütun/satır boyunca iki yöne ilerler (mevcut
    `LineTravelPlayer.PlayLineTravelInstanceAsymmetric` + `OnStep` ZATEN bu).
  - **RadialWave(center, radius)** — merkezden halka halka genişler (PulseCore 5x5,
    Pulse+Pulse radius 4).
  - **DiveBurst(fromCell, targetCell, payloadEmission)** — PatchBot: hedefe uçar,
    VARIŞTA `payloadEmission`'ı (pulse/line/square) başlatır.
  - **AreaSweep(cells, order)** — Override fanout: kapsanan hücreler merkez-dışı
    sırayla "varır" (mevcut `OverrideRadialClearDelays` bunun zamanlaması).

### 1.2 `SpecialChainContext` (paylaşılan durum)
- `HashSet<Vector2Int> triggered` — aktive olmuş special hücreleri (tekrar tetikleme yok).
- `HashSet<Vector2Int> cleared` — temizlenmiş hücreler (çift temizleme yok).
- `Queue<PendingActivation> queue` — varışta bulunan ama henüz aktive olmamış special'lar.
- `anchoredCells` — kuyruktaki special'lar gravity-blocked (yerinde patlasın) — mevcut
  `PulseChainSequenceAction.anchoredCells` mekaniği.
- `OnArrive(cell, sourceSpecial)` — TEK ortak varış davranışı (aşağıda).

### 1.3 `OnArrive(cell, sourceSpecial)` — her emisyon için AYNI
1. `cleared`/`triggered` kontrol; gerekiyorsa skip.
2. Hücrede obstacle var → `ApplyObstacleDamageAt(cell, SpecialActivation)`
   (launcher/energy stack zaten doğru çalışıyor — bkz. launcher emit model).
3. Hücrede normal taş → temizle (`cleared.Add`), gravity tetikle.
4. Hücrede **tetiklenmemiş special** → `queue`'ya ekle + **anchor**la. (Deferred
   override listesi YERİNE bu — varışta gerçek kuyruğa girer, sanal park yok.)

### 1.4 `SpecialChainRunner` (tek BoardAction) — PARALEL
- Tohum (seed) special listesiyle başlar.
- **Aktif emisyonlar PARALEL ilerler** (karar #2): birden çok beam/wave aynı anda yol
  alır; her biri kendi adımında `OnArrive` çağırır. Runner tek koroutin içinde tüm
  aktif emisyonları her frame ilerletir (frontier listesi), tetiklenen yeni special
  hemen yeni bir emisyon olarak listeye eklenir (anchor'lı).
- Kuyruk/aktif-emisyon boşalınca biter. Kök/alt-zincir + final wait/release: mevcut
  `PulseChainSequenceAction` `root` mekaniği korunur. Cascade-catch (`catchOverlap`)
  paralel modda emisyon-bazlı uygulanır.

### 1.5 `ActivateSpecialAt(cell)` — GERÇEK `<Ad>Special` sınıfını tetikler (karar #ana)
Zincir bir special'a varınca **o special'ın gerçek sınıfı** (`LineVSpecial`,
`LineHSpecial`, `PulseCoreSpecial`, `PatchBotSpecial`, `OverrideSpecial`) `Execute`
edilir — bespoke reimplementation YOK. Her sınıf kendi davranışını çalıştırır ve
karşılığı emisyonu yayar (runner'a geri besler):

| Special sınıfı | Yaydığı emission |
|---|---|
| `LineVSpecial` | LineBeam(Vertical) |
| `LineHSpecial` | LineBeam(Horizontal) |
| `PulseCoreSpecial` | RadialWave(r=2) |
| `PatchBotSpecial` | DiveBurst(target, payload)  *(solo: 5x5 square)* |
| `OverrideSpecial` | mevcut implant/fanout AYNEN (karar #1 — zincir düğümü olarak sadece tetiklenir, içi değişmez) |

## 2. Solo emisyon şekilleri (doğrulanmış)

- **LineV** = tam sütun (CollectColumn, full height).
- **LineH** = tam satır.
- **PulseCore** = 5x5 kare (radius 2, affectedCellCount 25), radyal.
- **PatchBot** = hedef(ler)e uçar, varışta 5x5 square burst (`AddSquare r=2`).
- **Override** = partner/base type'ın TÜM taşları (`AddAllOfType`); partner yoksa
  kendi base type'ı (`OverrideSpecial.cs:63-70`).

## 3. Combo başlangıç emisyonları (seed) — sonrası uniform propagation

| Combo | Başlangıç emisyonu (seed) |
|---|---|
| Line+Line | Merkez: 1 tam satır + 1 tam sütun (cross) |
| Line+Pulse | **Tek (fused) emisyon**: 3 satır + 3 sütun (BuildPulseEmitterTargets). Karar #3: iki ayrı special emisyonunun kombinasyonu DEĞİL — kendi tek emisyon tipi. |
| Line+PatchBot | PatchBot hedefe uçar, payload = LineBeam |
| Line+Override | Override implant + line beam'leri |
| Pulse+Pulse | RadialWave radius 4 (büyük) |
| Pulse+PatchBot | PatchBot hedefe uçar, payload = RadialWave r=2 |
| Pulse+Override | Override implant, kapsananlarda pulse |
| PatchBot+PatchBot | 2 bot, her biri DiveBurst (5x5) — eşzamanlı dive |
| PatchBot+Override | Override + airborne patchbot dive |
| Override+Override | Tüm board (AddAllTiles) + chain |

> Combo = SADECE bu başlangıç şeklini tanımlar; ardından gelen "varış→tetikle→
> alt-emisyon" TÜM combolarda **aynı** `SpecialChainRunner` ile işler.

## 4. Emekliye ayrılacaklar (retire)

- `DeferredLineHitOverrideCells` + `DrainDeferredLineOverrides` (sanal deferral; data/
  visual faz uyumsuzluğu bug'ı — override "bazen patlamıyor" bundandı).
- `OverrideDeferredPulseExplosions` (sanal sıralama).
- `SpecialBehaviorDispatcher.ApplySpecialActivation` içindeki override-defer branch'i
  (`case SystemOverride` HasLineActivation → defer).
- `LineVHPulseCoreComboAction` içindeki ad-hoc OnStep/executeSpecialActions zinciri →
  yerine SpecialChainRunner.
- Combo'ların önceden-snapshot'lanan `Affected`/`ImpactCells` + "final clear owns
  context" merge mantığı (her special kendi emisyonunu yayar; merge yok → stacking
  zaten doğru çalışır, bkz. launcher emit model).

> İSTİSNA (karar #1): `OverrideSpecial`'ın implant/fanout iç mantığı KORUNUR
> (`OverrideDeferredPulseExplosions` dahil — implant onu kullanıyor). Override zincir
> düğümü olarak sadece `OverrideSpecial.Execute` ile tetiklenir; içi aynı kalır.
> Yalnızca `DeferredLineHitOverrideCells` (line/pulse beam'in override'ı park edip
> kaybetmesi) kaldırılır — varışta artık gerçekten `OverrideSpecial` tetiklenecek.

## 5. Migrasyon fazları (her biri test edilebilir)

1. **Çekirdek**: `PulseChainSequenceAction` → `SpecialChainRunner` genelle; emisyon
   soyutlamasını çıkar; 5 solo special'ı motordan geçir.
2. **Uniform varış-tetikleme**: her emisyonun `OnArrive`'ı special'ı anchor'layıp
   kuyruğa alsın; deferred-override yolunu KALDIR.
3. **Combo tohumlama** (sırayla, her biri test): Line+Pulse → Pulse+Pulse → Line+Line
   → *+PatchBot → *+Override → Override+Override.
4. **Temizlik**: bespoke combo action'ları + deferred path'leri sil.

## 6. Riskler / test noktaları

- **Görsel zamanlama**: emisyon hızları (beam/wave) feel'i belirler — mevcut
  `LineTravelPlayer` + `catchOverlap` korunur; ayar gerekebilir.
- **Gravity/anchor yarışı**: kuyruktaki special düşmemeli — mevcut anchor mekaniği.
- **Sonsuz döngü**: `triggered` set + safety cap (mevcut `MaxResolveLoops` benzeri).
- **Launcher stacking**: her emisyon ayrı geçtiği için launcher her emisyonda 1 emit
  (stack) — istenen davranış; cascade/normal hit move-cap'li kalır.
- **Test matrisi**: 5 solo + 10 combo = 15 senaryo; her fazda ilgili olanı oyna.

## 7. Kararlar (KİLİTLENDİ)

1. **Override implant** — şu anki haliyle kalır; tam bir chain oluşturmuyor. Motora
   taşınmaz; zincir düğümü olarak yalnızca `OverrideSpecial.Execute` tetiklenir.
2. **Paralel** — emisyonlar aynı anda ilerler (Bölüm 1.4).
3. **Line+Pulse** — iki special emisyonunun kombinasyonu DEĞİL; kendi tek (fused)
   emisyonu (3×3 cross).
4. **Ana direktif** — diğer comboların zincir reaksiyonlarında, varılan hücredeki
   special **gerçek `<Ad>Special` sınıfıyla** tetiklenir (Bölüm 1.5). Bespoke
   reimplementation yok.

## 8. Sıradaki adım

~~Faz 1 (çekirdek `SpecialChainRunner` + 5 solo special'ı motordan geçir).~~ **Faz 1
UYGULANDI (2026-07-18, aşağıda Bölüm 10). Sıradaki: oyun içi test (5 solo + zincirde
Override arrival), sonra Faz 2 (dispatcher'daki deferred-override dallarının kaldırılması —
kalan kullanıcılar: eski combo yolları + ExpandSpecialChain/booster).**

## 9. Faz 1 öncesi güncel bulgular (2026-07-18 oturumu)

Override fanout'unun İKİ AYRI MOTORDAN aktığı sahada doğrulandı — kullanıcı gözlemi:
takas yolunda tempo çalışıyor, zincir yolunda çalışmıyor.

**Yol A (takas / top-level):** Resolver `ProcessFanout` delegesini bağlar →
`SpecialFanoutService.ProcessFanout` → `SystemOverrideFanoutPlacementAction`
(ışın→yerleşim→tetikleme töreni). Tempo artık board metronomuna bağlı:
hedefler arası ¾ CellTime, yerleşim→tetikleme 1 CellTime
(`BoardController.CellTime` = 1 hücre düşüş süresi; 2026-07-18'de eklendi).

**Yol B (zincir):** `SpecialBehaviorDispatcher.ApplySpecialActivation`'ın
SystemOverride dalı ("OverrideExecuteNow") ve `ExecuteSpecialActions`'ın override
dalı, `FinalizeAtEnd=false` + DELEGESİZ çalışır → fanout hedefleri ctx'e yazılır
ama tören action'ı üretilmez. Dış special'ın finalize'ı fanout'u işlerse tören
oynar (kuyruk yolu); İŞLEMEZSE (aşağıdaki drain boşluğu) hiç oynamaz.

**Drain boşluğu:** `SpecialResolver.DrainDeferredPulseComboOverrides` dış
finalize'dan SONRA koşar; `ExecuteSpecialActions`'ın override dalı aksiyon
üretmediği için `overrideActions.Count == 0 → continue` ile tören/renk temizliği
kaybolabilir (fanout ctx'te işlenmeden kalır). `DrainDeferredLineOverrides`
(dispatcher) dönen aksiyonları zaten yok sayıyor (void yol).

**Çifte temizlik tuzağı:** Zincirde `FinalizeAtEnd=true` açmak YETMEZ:
`OverrideSpecial.Finalize → BuildClearAction(ctx.Affected)` paylaşılan context'in
TÜM taşlarını ikinci kez temizler. Zincir override'ı için taze alt-context (veya
motorun kendi emisyon closure'ı) şart — Faz 1'in çözmesi gereken çekirdek durum.

**Faz 1 ek hedefleri:**
- Override fanout töreni (yerleşim+tetikleme) tek motordan üretilsin; Yol A/B farkı
  kalksın. Tören pacing'i `CellTime` çarpanlarından okusun.
- `DeferredPulseComboOverrideCells` / `DeferredLineHitOverrideCells` drain'leri
  motora taşınınca SİLİNSİN (plan Bölüm 5.2 ile uyumlu).
- İlgili taze düzeltme: PatchBot+Pulse arrival'da drain delegate'i
  (`PulseCorePatchBotComboExecutionRuntime.DrainDeferredOverrides`) eklendi —
  motor göçünde bu da sadeleşip kalkmalı.

## 10. Faz 1 UYGULANDI (2026-07-18)

**5 solo special'ın tüm aktivasyonları (tap + swap) artık tek motordan.**

- `SpecialChainRunner`'a `protectedCells` eklendi: swap'ın normal tarafında aynı
  hamlede oluşan yeni special hücrelerine emisyonlar hiç dokunmaz (eski Execute
  yolundaki `ProtectedCells`'in karşılığı). Yalnız o instance'ın kendi emisyonlarına
  uygulanır; alt-zincirlere taşınmaz (gravity sonrası hücre bayatlar).
- `ResolveSpecialSolo`: 5 special de runner seed'i. Eski `Execute(FinalizeAtEnd=true)`
  + `DrainDeferredLineOverrides`/`DrainDeferredPulseComboOverrides` solo dalları ve
  alttaki generic queue/fanout yolu SİLİNDİ. Solo Override da motor üzerinden
  `ResolveOtherSpecialInline → OverrideSpecial.Execute` (fanout delegeleri bağlı =
  Yol A töreni; karar #1 gereği implant/fanout içi aynen).
- `ResolveSpecialSwap` tekli dallar: Line/Pulse/PatchBot tek runner dalına indi
  (protectedCells = externalProtectedCells ∪ special-partner hücresi). 2+ special
  isteyen `IsSupportedSpecialChain`/`EnumerateChainRegion` guard'ları silindi
  (tekli aktivasyon da motorda). Override+Normal swap direkt `OverrideSpecial.Execute`
  yolunda kaldı (karar #1; `ForcedPartnerType` gerekiyor).
- Yol A/B birleşmesi (Bölüm 9): solo/swap kaynaklı zincirlerde Override artık HER
  ZAMAN arrival sub-chain → taze scoped context + `FinalizeAtEnd=true` + fanout
  delegeleri → tören action'ı gerçekten üretilir ve oynar. Çifte temizlik yok
  (paylaşılan ctx kullanılmıyor). Yol B (dispatcher defer + delegesiz FinalizeAtEnd=false)
  yalnızca eski combo yolları + `ExpandSpecialChain`/booster'da kaldı → Faz 2.
- Ölü kod temizliği: resolver'da `ExecuteSpecialActionsNoFinalize`, presentation-plan
  altyapısı (`IsPureSolo*`, `Build*PresentationPlanIfNeeded`, `BuildMatchClearAction`,
  `BuildPulseStagger`) ve eski yorum bloğu silindi (1892 → ~1011 satır).
  `DrainDeferredPulseComboOverrides(ctx, actions)` internal overload'u KALDI
  (PatchBot+Pulse combo delegate'i hâlâ kullanıyor); `ExecuteSpecialActions` +
  `DrainDeferredLineOverrides` onun bağımlılığı olarak kaldı.

**Test matrisi (oynanacak):** 5 solo (tap + swap); swap'ta yeni special oluşurken
Line/Pulse aktivasyonu (protected doğrula); Line/Pulse yolunda Override (tören + renk
temizliği); launcher üstünden geçen line (emit stack); PatchBot solo hedefleme.

### 10.1 Saha bug'ı → PatchBot+Pulse varışı motora bağlandı (2026-07-18)

Kullanıcı raporu: PatchBot+PulseCore uçtu, varış alanındaki Override HİÇ tetiklenmedi.
Kök sebep: varış patlaması motora girmiyordu — `ExecutePulseCoreImpactAndFinalCascade`
→ `pulseCoreSpecial.ExecuteAtTarget` → alandaki Override `IsPulseCoreActive` ile
dispatcher'da deferred → drain delegate'i paylaşılan arrival ctx üzerinde kırılgan
(Bölüm 9 "drain boşluğu" sınıfı). Kullanıcı direktifi: "PatchBot+Pulse'ta sadece hedef
seçimi farklı; sonrası pulse yolunun aynısı olmalı" = plan §1.1 DiveBurst.

Fix: `SpecialChainRunner`'a `virtualPulseBurstCenters` — merkezde PulseCore TILE'I
OLMADAN pulse patlaması (payload havadan geldi). `ExplodePulsesSimultaneously`
genelleşti (`centersArePulseTiles` bayrağı; sanal merkezde merkez hücredeki special de
zincirlenir). `PulseCorePatchBotComboExecutionRuntime.BuildPulseBurstChain` factory'si
resolver'da bağlandı; airborne varışı factory varsa runner'ı çalıştırıyor
(ExecuteAtTarget+deferral+drain yalnız factory'siz fallback). Alandaki Override artık
arrival'da gerçek sınıfıyla patlar (tören dahil), solo pulse ile aynı yol.

NOT (Faz 2-3 adayı): solo PatchBot standard varışı (groupCtx PHASE 3-6) hâlâ motor dışı
tek MatchClearAction — hedef hücre tek-hücre hit olduğu için special çiğneme riski düşük
ama DiveBurst'e (sanal burst) taşınınca o yol da tekleşir.

### 10.2 PatchBot+special kombolarının tam denetimi (2026-07-18)

Kullanıcı direktifi: tüm PatchBot+special kombolarında YALNIZ hedef seçimi farklı olsun;
varıştaki special tetiklemesi `<Ad>Special` ile aynı (motor) yolu kullansın.

| Combo | Durum |
|---|---|
| PatchBot+Pulse | ✓ motora bağlı (§10.1, `virtualPulseBurstCenters`) |
| PatchBot+LineV | ✓ motora bağlandı: `virtualLineSweeps` (dikey sanal beam) |
| PatchBot+LineH | ✓ motora bağlandı: `virtualLineSweeps` (yatay sanal beam) |
| PatchBot+PatchBot | payload YOK — her bot tek hücre vurur (özel tetikleme durumu yok) |
| PatchBot+Override | Override implant yolu (karar #1, içi korunur); implant botlar tek hücre vurur |

LineV/H fix ayrıntısı: eski varış, combo İÇİNDE sıfırdan kurulan dispatcher/queue/fanout/
implant servis KOPYALARI + `Line*Special.Execute(VirtualOriginCell)` + kendi
`DrainDeferredLineOverrides`'ı ile çalışıyordu (pulse bug'ıyla aynı kırılgan sınıf; Override
yol üstündeyse deferral'a düşüyordu). Şimdi: `SweepLinesSimultaneously` genelleşti
(`linesAreTiles` bayrağı; sanal modda origin'de tile aranmaz, origin hücredeki special de
zincirlenir), `LineV/HPatchBotComboExecutionRuntime.BuildLineBurstChain` factory'si
resolver'da bağlı; dash callback'i factory varsa runner'ı EnqueueFront ediyor. Eski
bespoke yol yalnız factory'siz fallback (dispatcher combo dalı) — Faz 2'de silinir.

PatchBot+PatchBot / +Override notu: bot varışı tek-hücre hit; hedefleme special seçmez →
"special tetiklenmedi" sınıfı bug bu yollarda oluşamaz. Ancak bot-varış mantığı İKİ ayrı
implementasyonda (solo `PatchBotSpecial` EnqueueDash+onArrived vs
`OverridePatchBotAirborneGroupAction` groupCtx PHASE 3-6) — Faz 2-3'te tekleştirme adayı.
