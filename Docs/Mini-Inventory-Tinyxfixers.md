# TinyFixers Mini Envanter (Repo Düzeyi)

Bu doküman, mevcut kod yapısındaki oyun akışını ve merkezi sınıfları kısa envanter formatında özetler.

## 1) İlgili class listesi

### Boot / Level yükleme
- `BootLoader`
- `LevelRuntimeSelector`
- `GridSpawner`

### Board çekirdeği
- `BoardController`
- `BoardState`
- `BoardInitService`
- `MatchFinder`
- `CascadeLogic`
- `ActionSequencer`
- `BoardAction` (+ `SwapAction`, `MatchClearAction`, `FallAction`, `ObstacleDamageAction`, `PulseLineComboAction`, `SystemOverrideFanoutPlacementAction`)
- `BoardAnimator`
- `BoardVfxService`
- `LineSweepService`

### Special / combo çözümleme
- `SpecialResolver`
- `ResolutionContext`
- `ActivationQueueProcessor`
- `SpecialBehaviorDispatcher`
- `SpecialFanoutService`
- `SpecialImplantService`
- `SpecialVisualService`
- `SpecialCellUtils`
- `SpecialBehaviorRegistry`
- `ISpecialBehavior`, `IComboBehavior`, `IComboExecutor`, `ILightningBehavior`, `ILightningComboBehavior`
- Davranış/Combo implementasyonları: `LineHorizontalBehavior`, `LineVerticalBehavior`, `PulseCoreBehavior`, `PatchBotBehavior`, `SystemOverrideBehavior`, `LineCrossCombo`, `PulseLineCombo`, `PulseLineCrossCombo`, `PulsePulseCombo`, `PatchBotLineCombo`, `PatchBotPulseCombo`, `PatchBotPatchBotCombo`, `OverrideSpecialCombo`, `OverrideOverrideCombo`

### Obstacle hattı
- `ObstacleStateService`
- `ObstacleResolutionService`
- `ObstacleStateServiceCompat`
- `ObstacleStateServiceLegacyApiExtensions`

### Level / Data / UI
- `LevelData`, `LevelCatalog`, `ObstacleLibrary`
- `TopHudController`, `TopHudGoalSlot`, `JokerFocusOverlayController`, `JokerBoosterSlotMapping`, `LevelEndSimplePopupController`, `MainMenuLevelButtonController`

## 2) Her class için tek satır rol (çekirdek sınıflar)

- `BootLoader`: Giriş sahnesinden gecikmeli şekilde oyun sahnesine geçiş yapar.
- `GridSpawner`: Level verisini çözüp board hiyerarşisini/başlangıç gridini kurar, `BoardController` ile event bağlarını yapar.
- `BoardController`: Oyun döngüsünün merkezi orkestratörü; input, swap, resolve, cascade, booster ve obstacle akışını yönetir.
- `BoardInitService`: Başlangıç tile tiplerini ilk eşleşmeyi engelleyecek şekilde üretir.
- `MatchFinder`: Grid üzerinde eşleşme kümelerini tespit eder.
- `SpecialCreationService`: Match sonrası oluşacak special üretim kararını verir.
- `SpecialResolver`: Special aktivasyon ve combo çözümleme zincirini action listesine çevirir.
- `ResolutionContext`: Special resolve sırasında etkilenen tile/hücre ve zincir durumunu taşır.
- `ActivationQueueProcessor`: Special aktivasyon kuyruğunu genişletip sıralı işler.
- `SpecialBehaviorDispatcher`: Special tipine göre doğru behavior/combo yürütmesini seçer.
- `SpecialBehaviorRegistry`: Special davranış implementasyonlarının kayıt/navigasyon noktasıdır.
- `CascadeLogic`: Boş hücreleri doldurmak için düşme/slide/spawn action planını çıkarır.
- `ActionSequencer`: `BoardAction` kuyruğunu görsel bloklama kuralına göre çalıştırır.
- `BoardAnimator`: Board aksiyonlarının animasyon ve görsel uygulamasını yürütür.
- `BoosterService`: Tekil/satır/sütun/shuffle booster etkilerini uygular.
- `ObstacleStateService`: Obstacle durumunun (hit/stage/destroy) tek state otoritesidir.
- `ObstacleResolutionService`: Hücre bazında obstacle hasarını board akışına bağlayan çözümleyicidir.
- `BoardVfxService`: Board seviyesindeki VFX/SFX tetiklerini merkezler.
- `LineSweepService`: Satır/sütun lightning tarama zamanlamasını ve event yayılımını yönetir.

## 3) Çağrı akışı (mevcut)

1. `BootLoader.Start()` -> sonraki sahneyi yükler.
2. `GridSpawner.Start()` -> level çözümü, board init (`Init`, `SetLevelData`, `SetupFactory`), başlangıç grid kurulumu.
3. Oyuncu etkileşimi `BoardController.RequestSwapFromDrag` / `SelectOrSwap` ile gelir.
4. `BoardController.ProcessSwap` içinde:
   - `ActionSequencer` ile `SwapAction` oynatılır.
   - Normal match + special üretimi (`MatchFinder` + `SpecialCreationService`) değerlendirilir.
   - Special swap/solo durumunda `SpecialResolver` action listesi üretir ve kuyruklar.
5. Her temizleme sonrası `MatchClearAction` içinde tile clear + obstacle damage (`ObstacleResolutionService` -> `ObstacleStateService`) çalışır.
6. `CascadeLogic.CalculateCascades()` ile düşme/slide/spawn planı çıkar, `ActionSequencer` ile uygulanır.
7. Döngü `BoardController.ResolveBoard` içinde match/special/cascade kalmayana kadar tekrar eder.
8. Board idle olduğunda `BoardController` eventleri (`OnBecameIdle`, goal/move/obstacle eventleri) UI tarafına akar.

## 4) Benzer/çakışabilecek class isimleri

- `SpecialResolver` <-> `SpecialBehaviorDispatcher` <-> `ActivationQueueProcessor` (üçü de resolve katmanında, sorumluluk sınırları yakın).
- `ObstacleResolutionService` <-> `ObstacleStateService` (biri uygulama/entegrasyon, diğeri state otoritesi).
- `ObstacleStateServiceCompat` <-> `ObstacleStateServiceLegacyApiExtensions` (ikisi de geçiş/uyumluluk yüzeyi).
- `BoardAnimator` <-> `ActionSequencer` (ikisi de aksiyon yürütmede görünür; biri animasyon içeriği, diğeri kuyruk motoru).
- `BoardVfxService` <-> `SpecialVisualService` (genel board VFX ile special-e özel VFX katmanları).
- `PatchbotComboService` <-> `PatchBot*Combo` sınıfları (servis/strateji ve combo implementasyonları isim olarak yakın).
- `LineSweepService` <-> `LightningSpawner` <-> `LightningBeam` (line/lightning görsel-akış bileşenleri isim olarak benzer).

## 5) Dokunulmaması gereken merkezi classlar

- `BoardController` (oyun state makinesi + servis orkestrasyonu merkezi).
- `SpecialResolver` (special/combo zinciri giriş noktası).
- `ResolutionContext` (special zincirinin ortak veri taşıyıcısı).
- `ActionSequencer` (tüm board action yürütme kuyruğu).
- `CascadeLogic` (düşme/spawn hesaplamasının merkezi).
- `ObstacleStateService` (obstacle state/hit otoritesi).
- `GridSpawner` (runtime board kurulum ve event bağlama merkezi).
