# Kullanılmayan Kod / Sınıf Taraması

Bu rapor, `Assets/_Project/Scripts` altında yapılan iki aşamalı taramaya dayanır:
1. Sınıf adının `.cs` dosyalarında referanslanma kontrolü.
2. Script `.meta` GUID değerinin sahne/prefab/asset referanslarında aranması.

> Not: Unity'de reflection/dinamik yükleme varsa bazı sonuçlar false-positive olabilir.

## BoardController / SpecialResolver temizlikleri

Aşağıdaki kod parçaları gerçekten kullanılmadığı için kaldırıldı:

- `SpecialResolver` ctor içindeki kullanılmayan `MatchFinder matchFinder` parametresi kaldırıldı.
- `ResolveSpecialSwap` içindeki kullanılmayan `comboOrigin` ve `comboPartner` lokal değişkenleri kaldırıldı.
- `SpecialResolver.ResolvePulseLineCombo(...)` private metodu hiçbir yerden çağrılmadığı için kaldırıldı.

## Kod içinde ve asset tarafında referansı görünmeyen sınıflar

Aşağıdaki sınıflar için hem kod içinde kullanım bulunamadı, hem de script GUID referansı bulunamadı:

- `ObstacleStateServiceLegacyApiExtensions`
- `ObstacleStateServiceCompat`
- `LineTravelPlan`
- `LineCrossCombo`
- `PulseLineCrossCombo`
- `PatchBotBehavior`

## Kod içinde referansı yok ama asset referansı var (muhtemelen kullanımda)

Aşağıdaki sınıfların kod referansı yok; ancak prefab/scene/asset tarafında script referansı mevcut:

- `MainMenuLevelButtonController`
- `SafeAreaFitter`
- `BootLoader`
- `ImpactFlashAnim`
- `LightningTestSpawner`
- `PulsePulseExplosionVfx`

