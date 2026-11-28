# iOS kurulum boyutu ve içerik planı · 26 Kasım 2026

Cihaz ölçümü 492 MB. İncelenen yerel ürün ReleaseForRunning-iphoneos/WonderFixers.app. Son Editor.log player bölümünde 248,1 MiB kullanıcı içeriği, 217,2 MiB texture ve 15 MiB ses bildiriliyor. Frameworks yaklaşık 207 MiB, Data yaklaşık 263 MiB. UnityFramework tek ARM64 ikilisi yaklaşık 191 MiB. Frameworks ve Data diskteki boyutlardır. Aşağıdaki asset satırları Unity’nin yuvarlanmış sıkıştırılmamış değerleridir.

## MainMenu, 01_Game ve açılış

Her sahneden metin biçimli Unity asset referansları GUID ile izlendi. Aşağıda yalnız son player raporunda da görülen görseller var. Ortak görseller birden fazla sahnede görünebilir. Build işlemi kaynak sahne referanslarını değiştirebildiği için bu tablo yükleme sırasını veya tek sahipliği göstermez.

### MainMenu

Eşleşen görsel satırı 380, toplam 84.9 MiB.

| En büyük eşleşmeler | MiB |
|---|---:|
| `Assets/_Project/Art/UI/Obstacles/RobotStyle/MudOverlay/MudOverlaySTG1V1.png` | 1.50 |
| `Assets/_Project/Art/UI/Missions/ND_M001/1/GreatWall.png` | 1.20 |
| `Assets/_Project/Art/UI/Leaderboard/V2/WeeklyStageBG.png` | 1.20 |
| `Assets/_Project/Art/UI/Missions/ND_M001/1/GrandKanyonV1.png` | 1.10 |
| `Assets/_Project/Art/UI/Missions/ND_M001/1/PyramidV11.png` | 1.10 |
| `Assets/_Project/Art/UI/DailySlotMachine/DailyBonusPanel.png` | 0.88 |
| `Assets/_Project/Art/UI/SettingsImg/SettingsPanelV1.png` | 0.88 |
| `Assets/_Project/Art/UI/DailySlotMachine/spinBG2.png` | 0.88 |

### 01_Game

Eşleşen görsel satırı 453, toplam 109.9 MiB.

| En büyük eşleşmeler | MiB |
|---|---:|
| `Assets/_Project/Art/Icons/FX/vfx_pulsecore_coreburst.png` | 1.80 |
| `Assets/_Project/Art/UI/Obstacles/RobotStyle/MudOverlay/MudOverlaySTG1V1.png` | 1.50 |
| `Assets/_Project/Art/Icons/Specials/OverrideCopper_TurnTopV2.png` | 1.50 |
| `Assets/_Project/Art/Icons/Specials/OverrideCopper_TurnBottom.png` | 1.50 |
| `Assets/_Project/Art/UI/BattleField/Robot_001/FireWorkPart.png` | 1.20 |
| `Assets/_Project/Art/UI/Missions/ND_M001/1/GreatWall.png` | 1.20 |
| `Assets/_Project/Art/UI/Missions/ND_M001/1/GrandKanyonV1.png` | 1.10 |
| `Assets/_Project/Art/UI/Obstacles/RobotStyle/Magnet/MagnetLockerV2.png` | 0.70 |

### 00_Boot

Eşleşen görsel satırı 2, toplam 1.7 MiB.

| En büyük eşleşmeler | MiB |
|---|---:|
| `Assets/_Project/Art/UI/BackGroundImgs/marka.png` | 1.00 |
| `Assets/_Project/Art/UI/BackGroundImgs/loading.png` | 0.67 |

## Eventler, World Tour ve görevler

Manifestteki her kaynak hem kayıtlı yolu hem mevcut GUID yolu ile player listesinde arandı. Aynı dosyadaki alt sprite’lar bir kez sayıldı.

| Paket | Kaynak dosya | Player listesinde eşleşen |
|---|---:|---:|
| safari | 8 | 0 |
| bridge-repair | 32 | 0 |
| harvest | 17 | 0 |
| gift-festival | 25 | 0 |
| gear-cup | 18 | 0 |
| mission-mis1 | 2 | 0 |
| mission-mis2 | 3 | 0 |
| mission-mis3 | 2 | 0 |
| mission-mis4 | 2 | 0 |
| mission-mis5 | 2 | 0 |
| mission-mis6 | 2 | 0 |
| mission-mis7 | 3 | 0 |
| mission-mis8 | 2 | 0 |
| mission-mis9 | 2 | 0 |
| mission-mis10 | 2 | 0 |
| şehir mis1 | 2 | 0 |
| şehir mis2 | 2 | 0 |
| şehir mis3 | 2 | 0 |
| şehir mis4 | 2 | 0 |
| şehir mis5 | 2 | 0 |
| şehir mis6 | 2 | 0 |
| şehir mis7 | 2 | 0 |
| şehir mis8 | 2 | 0 |
| şehir mis9 | 2 | 0 |
| şehir mis10 | 2 | 0 |

## Uygulama sırası

1. **01_Game — ilk hedef AncientSeal.** LevelCatalogProV1 kaynaklarında AncientSeal (64) ilk kez 151’de. AncientSealView gövdeyi ve parçaları Resources/SealSafe içinden adla alıyor. Son player listesindeki bu klasör 13,44 MiB. Bu engelin sprite listesini 151–200 paketine bağla. Sonraki kullanan paketler aynı bağımlılığı kullansın. Resources dışına taşıma ile yükleyici değişikliği aynı adımda olmalı. 136’da ön indirme, 151’de giriş kapısı ve 225’te tekrar kullanım kontrol edilmeli. Diğer geç engeller için de gerçek ilk level ve player satırını eşleştir. BossDuelIntro 9 MiB ancak ilk 50’de boss kullanılıyor; yerelde tut.

2. **MainMenu ve görevler.** Journey kartları bütün harikaların arka planını kullanıyor. Listede küçük önizleme, açılan görevde tam çözünürlük kullan. Ana menünün varsayılan arka planı yerel olsun. Önceden seçilmiş arka planı kayıtlı wonderId ile geri yükle. ND_M001/1 altındaki GreatWall, GrandKanyonV1 ve PyramidV11 hâlâ player listesinde. Ana menü, görev ve Journey ortak referanslarını birlikte değiştir. Kazancı küçük önizlemelerin maliyetini düşerek ölç.

3. **Event ve World Tour.** Yukarıdaki eşleşmelere göre önce mevcut paketleri koru. Yeni büyük popup veya şehir eklenince manifest kaynaklarını player raporunda ara. Menü logoları, ortak karakter, taş ve sandık yerel. Event kapısı canlı config ve takvimden, şehir seçimi oyuncunun kayıtlı sırasından gelmeli. İlk kurulumda ve kayıtlı ileri seviyede açılışı ayrı kontrol et.

4. **300 MB hedefinin fizibilitesi.** 492 MB’den 300 MB’ye fark 192 MB. Yaklaşık 207 MiB framework tek başına 217 MB eder. Bugünkü ikili ile hedefte tüm Data ve kalan dosyalara yaklaşık 83 MB kalıyor. Yalnız doğrulanmış geç içerik adaylarından bu miktarda kazanç çıkmadı. Native kodu incelemek için aynı ReleaseForRunning ayarlarında Xcode link map ile UnityFramework içindeki motor, IL2CPP ve statik SDK katkılarını ölç. İşlev ve boyut katkısı doğrulanmadan SDK kaldırma.

5. **Kabul ölçütü.** Önce mevcut 492 MB kurulumunu referans al. Her değişiklikte aynı platform, cihaz ve build ayarlarıyla player asset listesi ve .app/Data, Frameworks byte farkını karşılaştır. Beklenen görseller player listesinden çıkmış ve doğru pakette olmalı. İlk 50 çevrimdışı oynanabilmeli. Yeni cihaz kurulumu yalnız yerel raporda anlamlı fark görülünce yapılmalı.
