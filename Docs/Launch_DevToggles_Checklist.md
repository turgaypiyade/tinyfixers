# Canlıya Çıkış — Test Ayarları Kontrol Listesi

Test için açık tutulan, canlıdan (mağaza sürümü) önce kapatılacak/gözden geçirilecek ayarlar.
İlk tarama: 2026-10-21 (ilk talep yalnız listelemekti). 2026-10-30 satın alma/reklam hazırlığı güncellendi; diğer satırların durumları ilk tarama notlarıdır.

## A. Cihazda ETKİLİ — kapatılmazsa oyuncu görür
| # | Ayar | Nerede | Şu an | Canlıda |
|---|---|---|---|---|
| 1 | Seviye seçici 3 sn basılı tut hilesi (debug level paneli) | `MainMenu.unity` → MainMenuLevelButtonController `enableDebugLevelSelector` | **1 (açık)**, `#if` koruması YOK → cihazda da çalışır | 0 yap (ya da kodda `DEVELOPMENT_BUILD` ile koru) |
| 2 | Çekiçte ekran çatlağı ihtimali | `01_Game.unity` → BoardController `hammerScreenCrackChance` | **1.0 (%100)** | 0.3 (%30, kod varsayılanı) |
| 3 | Bostan Hasadı (kazı) event takvimi | `Resources/Events/HarvestConfig.asset` `activeDays` | 5/7 (2026-10-27 düzeltildi) | Tamam |
| 4 | AdMob gerçek reklam kimlikleri | `Resources/MonetizationConfig.json` + `GoogleMobileAdsSettings.asset` | İki platformun gerçek ID’leri bağlandı; dev yalnız test reklamı kullanır | AdMob ödeme profili, mağaza bağlantısı ve UMP mesajları tamamlanmalı |
| 5 | IAP sunucu taraflı makbuz doğrulama | `UI/Shop/StoreIap.cs` (not: "canlıdan önce eklenecek") | yok | Eklenmeli (sahte satın alma koruması) |
| 6 | Log bayrakları | `Resources/GameDiagnostics.asset` | `analyticsEvents: 1` | 0 (gereksiz log) |

## B. Yalnız editör/dev build — cihazda zaten pasif (temizlik için)
| # | Ayar | Not |
|---|---|---|
| 7 | Köprü Tamiri `debugForceAvailable: 1` (`Resources/Events/BridgeRepairConfig.asset`) | Kod `#if UNITY_EDITOR` korumalı; yine de 0 yap |
| 8 | `SlowMoDebug` (S/,/./0 tuşları, 01_Game) | Yalnız Editor + Development Build. **Mağaza build'i "Development Build" KAPALI alınmalı** |
| 9 | Editör test override'ları (TestLevelProgressionBootstrap, EditorTestSettings) | `#if UNITY_EDITOR`; cihazı etkilemez |
| 10 | Event debug menüleri (BridgeRepairDebugMenu vb.) | Editor klasöründe; build'e girmez |
| 11 | `useDynamicBoardInputTestSlowMo` (01_Game) | 0 — doğru |

## C. Mağaza / build ayarları
- Ürün kimlikleri iki mağazada ORTAK (`ShopOffer.StoreProductId`, ör. `...` + id). App Store Connect ve Play Console'da aynı kimliklerle, eşdeğer fiyat kademesiyle açılmalı. Oyun fiyatı mağazadan okur (oyuncunun para birimi); katalogdaki `priceLabel` yalnız yedek/editör.
- Editörde fiyat: Unity IAP sahte mağazası $0.01 döner → editörde katalog etiketi gösteriliyor (2026-10-21).
- Scripting define'lar (TF_IAP, TF_ADMOB, TF_FIREBASE_ANALYTICS) iki platformda da açık; IAP_FAKE_STORE_DEFAULT kaldırıldı. Bu fake-store sembolü tek başına native mağazayı seçtirmiyordu, FakeStore UI davranışını belirliyordu.
- Firestore `config/levels` BOŞ kalmalı (yalnız acil durum).

Yeni bir test kolaylığı eklendiğinde bu listeye satır ekle.

## D. Event açılış seviyeleri (2026-10-27, kullanıcı kararı)
Haftalık Görevler (Progress Event) 20 · Seri Bonusu 30 · Tırmanış/Safari 45 · Bostan Hasadı 50 (5 gün açık / 2 gün ara) · Köprü Tamiri 60 · Gear Cup 70.
Firestore `config/progressEvent` → `minLevel` en az 20 olmalı (uygulama zaten 20'nin altına inmez).

## E. Satın alma / reklam hazırlığı (2026-10-31)
12 ürün Unity IAP Catalog’da USD fiyatlar ve TR/EN adlarla eşitlendi. Google Play satıcı profili engeli kalktı; 12 ürünün tamamı oluşturuldu ve `standard` satın alma seçenekleri **Active** olarak doğrulandı. Bölgesel fiyatlar tek USD girdisinden Google tarafından hesaplandı. AdMob’da son kontrolde **Payment setup incomplete** görülüyordu; UMP mesajı taslak, App Store Connect giriş bekliyor. Sunucu makbuz doğrulaması ve cihaz testleri tamamlanmadı. Ayrıntılar ve ürün listesi: [Monetization/Release-Setup.md](Monetization/Release-Setup.md).
