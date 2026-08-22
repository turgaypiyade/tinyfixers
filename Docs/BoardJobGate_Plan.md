# BoardJobGate — Background-Job Sayacını Typed Token/Gate'e Çevirme

**Durum:** UYGULANDI (2026-08-12) — Unity derleme + §8 oyun-içi test bekliyor.
Değişen dosyalar: BoardController.cs, MatchClearAction.cs, BoardAnimator.cs, KeyGeneratorService.cs,
BarrelSpreadAction.cs, LineV/LineHPatchBotCombo.cs. Barrel: veri up-front commit + view/goal damla
varışında (guard'lı) → decouple edildi. LineV/H: PatchBotDash async + onArrived RequestResolve. Safe:
dokunulmadı (blocking). Pulse: dokunulmadı (§9a).
**Kapsam:** SADECE background-job muhasebesi. Special-chain parçalama, ResolveBoard state-machine
ve obstacle damage/presentation ayrımı BU İŞİN İÇİNDE DEĞİL (bkz. §9).
**Hedef:** (1) Leak'i yapısal olarak imkânsız kıl (typed handle). (2) Yan-etki işlerini (barrel/keygen/
cargo/presentation) resolve'u dondurmayan **async** yapıya taşı — board akmaya devam etsin, yalnız
level-end beklesin. LineV/H PatchBot combo da async'e alınır (varış→resolve kablolamasıyla, §5b).
MatchClearAction BlocksResolve kalır — o resolve adımının kendisi, uçuş değil.

---

## 1. Bugünkü durum (kanıt)

Merkez: `BoardController.cs`
- `public int ActiveBackgroundJobs = 0;` — satır **504**, çıplak public int.
- Eşli helper'lar `Max(0,…)` bandajlı: `BeginBackgroundJob/EndBackgroundJob` (953-954),
  `BeginGoalOrbFlight/End` (968-973), `BeginPatchBotDashFlight/End` (975-980),
  `BeginBossStrikeDrain/End` (984-989).
- 3 subset counter: `FlyingGoalOrbs`, `FlyingPatchBotDashes`, `DrainingBossStrikes` (959-961).
- Türetim: `BlockingBackgroundJobs => Max(0, Active - Flying - Dash - BossStrike)` (965-966).
- Timeout kurtarma: `ResolveBoard` 5sn sonra `ActiveBackgroundJobs=0; Flying…=0` ile sıfırlıyor
  (2758-2761). Bu, sistemin leak'e karşı tek güvenlik ağı → yani leak *biliniyor ve bekleniyor*.

## 2. Asıl risk

Bazı çağrılar eşli helper'ı **bypass edip çıplak `++`/`--`** yapıyor. Coroutine erken `yield break`
ederse / exception atarsa decrement kaçar → kalıcı leak → resolve 5sn timeout'a düşer,
erken-win/geç-resolve/donma. Bandaj `Max(0,…)` sadece negatife düşmeyi engelliyor, *fazla kalan*
job'ı temizlemiyor.

Çıplak mutasyon yapan yerler:
- `Actions/MatchClearAction.cs` — `++` @88, `--` @112, @136, @161
- `Combos/LineHPatchBotCombo.cs` — `++` @88, `--` @118
- `Combos/LineVPatchBotCombo.cs` — `++` @88, `--` @118

## 3. Hedef model — iki gerçek kategori

Kullanıcı kararı: **board'u dondurmayan async yapı**. 3 hardcoded flight subset'i (GoalOrb/Dash/
BossStrike) tek "resolve'u parklamaz, level-end bekler" kategorisine katlıyoruz; ayrıca bugün yanlış
yere Blocking sayılan yan-etki işleri (barrel/keygen/cargo/presentation) bu kategoriye taşıyoruz.

İki resolve-davranışı vardır:
- **BlocksResolve** — resolve/settle loop bunu beklemek ZORUNDA (sonraki fall/clear bu işin *veri*
  sonucuna bağlı). Yalnızca: MatchClearAction, cascade/combo settle işleri,
  StartImmediateAction(Sequence) blocking gövdeleri. (LineV/H PatchBot combo ARTIK burada değil → §5b.)
- **AsyncSideEffect** — resolve akmaya devam eder, input açılır; SADECE level-end bekler. Uçuşlar
  (orb/dash/bossstrike/cargo), barrel splatter, keygen key flight, presentation FX.

`ActiveBackgroundJobs` ve subset counter'lar **private** olur. Dışarı handle + read-only accessor.

```csharp
public enum BoardJobKind
{
    // BlocksResolve grubu
    Resolve,          // clear/cascade/combo — resolve loop bekler

    // AsyncSideEffect grubu (resolve parklamaz, level-end bekler)
    GoalOrbFlight,    // EnergyContainer/HatLauncher orb, CargoExit
    PatchBotDash,     // PatchBot dash, RocketBasket
    BossStrikeDrain,  // BossDuel vuruş kuyruğu
    ObstacleSpread,   // barrel mud splatter
    KeyFlight,        // KeyGenerator key uçuşu
    PresentationFx    // clear sonrası saf görsel efekt
}

static bool BlocksResolve(BoardJobKind k) => k == BoardJobKind.Resolve;

// IDisposable handle. Dispose = tam olarak bir kez decrement. Çift-dispose no-op.
// Force-drain sonrası (bkz. §6) eski handle'lar da dispose'ta no-op olur (epoch damgası).
public IDisposable BeginJob(BoardJobKind kind);
```

İç muhasebe: `int _activeTotal` + `kind` başına sayaç. Handle içinde `bool _disposed`, `int _epoch`,
`BoardJobKind _kind`. Dispose:
```
if (_disposed || _epoch != board._jobEpoch) return;
_disposed = true;
board.DecrementJob(_kind);
```

Türetim:
```csharp
public int ActiveBackgroundJobs => _activeTotal;                    // level-end bunu bekler (değişmedi)
public int BlockingBackgroundJobs => _kindCount[(int)BoardJobKind.Resolve]; // resolve/input bunu bekler
// Eski subset accessor'ları log uyumu için kalır, ilgili kind sayacına bağlanır:
public int FlyingGoalOrbs       => _kindCount[(int)BoardJobKind.GoalOrbFlight];
public int FlyingPatchBotDashes => _kindCount[(int)BoardJobKind.PatchBotDash];
public int DrainingBossStrikes  => _kindCount[(int)BoardJobKind.BossStrikeDrain];
```
`BlockingBackgroundJobs` artık *çıkarma* değil, doğrudan `Resolve` sayacı → "Active − subset"
aritmetiği ve Max(0,…) düzeltmesi ölür. **Level-end davranışı değişmez** (hâlâ tüm `ActiveBackgroundJobs`
bekler); değişen tek şey: resolve/input artık yalnız gerçek `Resolve` işini bekliyor, yan-etkiler akışı
dondurmuyor.

### 3a. Yöneten değişmez kural (tüm modelin dayanağı)

> Bir işi **AsyncSideEffect** yapmanın önkoşulu: grid **verisi** işin BAŞINDA senkron commit edilmiş
> olmalı; async olan yalnızca görsel/uçuş. Aksi halde resolve akarken cascade, henüz mutasyona uğramamış
> hücreye dolar → tutarsızlık.

Bu yüzden §5b'deki her aday, "veriyi ne zaman yazıyor?" diye denetlenir. MatchClearAction bu kuralı
karşılayamaz (fall doğrudan onun sonucuna bağlı) → BlocksResolve kalır.

Ek prensip: AsyncSideEffect saf görseli, board'un canlı `TileView` referansını grid dışına taşıyarak
oynatmamalı. Veri commit edildikten sonra görsel gerekiyorsa detached `Image`/sprite klonu kullanılır ve
gerçek `TileView` hemen pool'a döner ya da gridde kalır. `GridData` ↔ `TileView` mismatch debug çıktısı
çıktığı anda oyun bozuk state'tedir; bu durum logla geçiştirilemez, fail-fast edilmelidir.

## 4. Eski helper'ları koru (thin shim)

Diff'i küçük tutmak için eski Begin/End imzaları kalır, içleri handle'a devreder. Handle'ı `Begin`
çağrısında bir stack/dict'te tut, `End` eşleşeni Dispose etsin:
```csharp
public void BeginGoalOrbFlight()  => PushHandle(BeginJob(BoardJobKind.GoalOrbFlight));
public void EndGoalOrbFlight()    => PopHandle(BoardJobKind.GoalOrbFlight)?.Dispose();
```
`try/finally` gövdesi olan çağrılar (barrel/safe/cargo/keygen) doğrudan `using var h = BeginJob(...)`
kullanmalı — shim'e gerek yok, daha güvenli.

## 5. Değişmeyen çağrılar (kind sadece yeniden etiketlenir)

Bunlar zaten non-blocking kategorideydi; sadece yeni enum'a map olur, davranış aynı:
- RocketBasketLaunchAction.cs 76/98 → `PatchBotDash`
- PatchbotComboService.cs 40/73 → `PatchBotDash`
- EnergyContainerService.cs 148/152 → `GoalOrbFlight`
- HatLauncherService.cs 104/108 → `GoalOrbFlight`
- BossDuelController.cs 481/482/490 → `BossStrikeDrain`

**Çıplak mutasyon (mutlaka handle) — davranış korunur:**
| Dosya | Satır | Şu an | Hedef |
|---|---|---|---|
| MatchClearAction.cs | 88 / 112,136,161 | `++`/`--` | `using BeginJob(Resolve)` — 3 çıkış yolu tek Dispose |

(LineH/V PatchBot combo çıplak `++`/`--`'ları da handle'a çevrilir ama artık DAVRANIŞ değişiyor → §5b.)

**Salt-okuma (accessor korunduğu için DOKUNMA):**
- PreLevelSpecialRuntimeInjector.cs 224, 397
- BonusMovesService.cs 221, 228-229
- LevelEndSimplePopupController.cs 372, 381-383
- SpecialChainRunner.cs 227 (`BlockingBackgroundJobs`)
- BoardController iç okumalar: 915, 1189, 1205-1207, 2667, 2752, 3787

## 5b. DAVRANIŞ DEĞİŞECEK olanlar (kullanıcının asıl istediği — her biri §3a kuralıyla denetlenir)

Bugün Blocking, artık AsyncSideEffect. Her satırda "veri ne zaman yazılıyor?" doğrulanmalı:

| İş | Yer | Yeni kind | Veri-commit denetimi | Not |
|---|---|---|---|---|
| Barrel splatter | BoardController 3310 → `BarrelSpreadAction` | `ObstacleSpread` | Mud verisi `ExecuteVisuals` başında mı yazılıyor? Progressive ise önce toplu commit et, sonra animasyon | Yorumu zaten "taş akışıyla eşzamanlı" |
| KeyGen key flight | KeyGeneratorService 143 | `KeyFlight` | Hedef hücre `inFlightKeys`/rezervasyonla korunuyor mu (cascade dolduramasın)? | `committedKeys`/`inFlightKeys` zaten var; landing cell rezerve mi teyit et |
| CargoExit | BoardController 3880 | `GoalOrbFlight` | Tile veri gridden anında çıkıyor (`SetActive(false)`+Destroy) → veri temiz | Düşük risk |
| Presentation FX | BoardAnimator 1242 | `PresentationFx` | Clear'lar zaten `FinalizePresentationTileClear` ile commit edilmiş | `BackgroundEffectsBlockResolve` flag'i artık gereksiz; kaldır ya da default false |
| **LineH PatchBot combo** | LineHPatchBotCombo 88/118 | `PatchBotDash` | Dash uçarken veri yazılmıyor; mutasyon VARIŞTA sequencer'a devrediliyor (`EnqueueFront`, satır 113) | **Şart:** `onArrived` içine resolve yeniden-tetikleme (solo dash gibi) — yoksa varış line-burst'ü resolve'suz kalır. **Test:** `BuildLineBurstChain` ileri zinciri (Override dahil) kaybolmuyor/çift-resolve olmuyor mu |
| **LineV PatchBot combo** | LineVPatchBotCombo 88/118 | `PatchBotDash` | Aynı yapı (özdeş) | LineH ile birlikte, aynı kablolama + test |

**Safe hold (BoardController 3497) — KARAR: (b) kaldır + yeniden tasarla.** Kullanıcı hold'un tamamen
kalkmasını ve safe patlamasının görsel-öncelikli async yapılmasını istiyor. AMA bu bir **feel/yeniden-
tasarım işi**, muhasebe göçünün parçası değil. Bu adımda: `CoHoldResolveForSafeBreak`'i olduğu gibi
BIRAK (regresyon riski açma), safe patlaması yeniden-tasarımını **ayrı takip işi** olarak aç. Async'e
o iş içinde geçilir (data-commit kuralı orada da denetlenir).

**MatchClearAction — DEĞİŞMEZ:** §3a kuralını karşılayamaz (fall doğrudan clear verisine bağlı).
`Resolve` (blocking) kalır.

## 6. Force-drain / timeout kurtarma (kritik tuzak)

Bugün `ResolveBoard` (2758-2761) sayaçları elle `0`'lıyor. Handle sisteminde bunu yaparsak
**outstanding handle'lar hâlâ Dispose edilince eksiye iter / yeniden decrement eder**. Çözüm:
`_jobEpoch`. Force-drain:
```csharp
void ForceDrainAllJobs() { _jobEpoch++; _activeTotal = 0; Array.Clear(_kindCount,0,4); }
```
Eski epoch damgalı handle'lar Dispose'ta no-op olur. Bu, mevcut "elle sıfırla" davranışını
**leak'siz** yeniden üretir — sistemin asıl kazancı burada.

## 7. StartImmediateAction / StartImmediateActionSequence

Bunlar zaten `++`/`--`'ı coroutine'in başında/sonunda sarıyor (BoardController 939/946, 996/1008).
İç gövdeyi `using var h = BeginJob(Blocking)`'e çevir → exception/erken-break durumunda bile
Dispose garanti (finally). Çağıranlar (SpecialChainRunner 245/426/896/932, SpecialResolver
944/1013/1017, LineVHPulseCoreCombo 740, BossDuel 2537, BoardController 3383) değişmez.

## 8. Doğrulama listesi (build + oyun içi)

**Korunması gerekenler:**
1. Normal match clear → cascade → settle: donma yok, erken-win yok.
2. Orb uçuşu (EnergyContainer/HatLauncher): son hamlede fail, orb goal'a varınca WIN'e dönüyor mu (level-end tüm `ActiveBackgroundJobs`'ı bekler).
3. PatchBot dash + RocketBasket: uçuş sırasında cascade akıyor.
4. BossDuel son vuruş kuyruğu boşalana kadar fail beklemesi.
5. **LineH/V PatchBot combo (async'e geçti):** dash uçarken board akıyor; varıştaki line-burst zinciri resolve oluyor (kaybolmuyor); Override+line arrival zinciri çift-resolve/eksik-temizlik yapmıyor.
6. **Kasıtlı leak testi:** bir `Resolve` job'ı Dispose etmeden bırak → 5sn'de ForceDrain devreye girer, resolve devam eder, sayaç negatife düşmez, sonraki hamle temiz.

**Yeni async davranış (kullanıcının istediği — regresyon riski burada):**
7. **Barrel:** kırılınca board donmuyor, mud yayılırken taşlar akmaya devam ediyor; mud verisi doğru hücrelere yazılı (cascade boş hücreye dolmuyor); mud goal'u level-end'de doğru sayılıyor (erken-win yok).
8. **KeyGen:** key uçarken board akıyor; hedef hücre cascade tarafından işgal edilmiyor; key goal'u level-end'de bekleniyor.
9. **CargoExit:** collectible uçarken board akıyor; goal HUD'a varınca level-end doğru; çift-robot/ghost artefaktı yok.
10. **Presentation FX:** blocking-flag'li efektler artık akışı dondurmuyor; görsel bütünlük bozulmuyor.
11. **Safe:** karar (a) ise 0.5sn hold hâlâ var (dokunulmadı); karar (b) ise ayrı iş.

## 9. Kapsam DIŞI (sonraki fazlar — mevcut planlara bağla)

Bu iş yalnız muhasebe zeminini kurar. Şunlar AYRI ve mevcut dokümanlara bağlanmalı:
- **BoardAction.ActionBlockingPolicy** (BlocksSequencer/BlocksResolve/BlocksLevelEnd/Detached):
  bu gate'in doğal devamı, ayrı adım.
- **Special chain parçalama** (Emission/Arrival/Gravity/Anchor) → `Docs/SpecialChainRunner_Plan.md`
  Faz 1 zaten kısmen yapıldı; onun devamı, buradan bağımsız.
- **ResolveBoard state-machine'e indirgeme** → `Docs/DecoupledResolve_Plan.md` ile örtüşür;
  ikinci bir paralel resolve-refactor AÇMA.
- **Obstacle damage/presentation ayrımı** → bağımsız iş.

### 9a. Pulse / SpecialChainRunner async — BİLİNÇLİ OLARAK DIŞARIDA

Kullanıcı pulse'un da async olmasını istedi; daha önce denendi, **override+pulse ekrana yazılınca
karıştığı için geri alındı.** Sebebi yapısal, tesadüf değil:

- Dash bir **uçuş** → veri tek noktada (varış) yazılır → §3a'yı geçer, async güvenli.
- Pulse bir **genişleyen dalga** → taşları cephe ilerledikçe *zaman içinde progressive* temizler
  (R(t)=t·maxR; VFX + taş temizleme aynı normalizasyonu paylaşır). Yani veri mutasyonu doğası gereği
  zamana yayılı → §3a'yı KARŞILAYAMAZ.
- Cascade dalga sürerken akarsa: taşlar dalganın henüz ulaşmadığı (görselde dolu) hücrelere düşer /
  dalga, cascade'in yeni doldurduğu hücreyi siler → görsel kaos. Geri alınmasının kök sebebi bu.

Async yapmanın TEK temiz yolu: tüm dalga-hücrelerini **anında senkron temizle** + genişleyen dalgayı
saf görsel oynat + cascade'i "hareketli cephe" ile kapıla (front geçmeden o bölgeye taş girmesin).
Bu bir **moving-frontier gate** = ciddi iş, `DecoupledResolve_Plan.md` / `SpecialChainRunner_Plan.md`
konusu. Bu muhasebe göçünün parçası DEĞİL. **Karar: bu adımda pulse blocking kalır**; async'i o plana
"moving-frontier gate" maddesi olarak bırak.

## 10. Diff büyüklüğü / risk

- Default (shim + 3 kritik dosya + StartImmediate + ForceDrain + epoch): ~1 yeni tip + BoardController
  muhasebe bloğu değişimi + 3 combo/clear dosyası. Salt-okuma çağrıları hiç değişmez → risk düşük.
- Tam-göç (15 çağrının hepsi handle): daha temiz, orta diff, shim ölür.
