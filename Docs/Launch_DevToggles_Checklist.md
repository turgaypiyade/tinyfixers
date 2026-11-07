# Canlıya Çıkış — Test Ayarları Kontrol Listesi

Test için açık tutulan, canlıdan (mağaza sürümü) önce kapatılacak/gözden geçirilecek ayarlar.
İlk tarama: 2026-10-21 (ilk talep yalnız listelemekti). 2026-10-30 satın alma/reklam hazırlığı güncellendi; diğer satırların durumları ilk tarama notlarıdır.

## A. Cihazda ETKİLİ — kapatılmazsa oyuncu görür
| # | Ayar | Nerede | Şu an | Canlıda |
|---|---|---|---|---|
| 1 | Seviye seçici 3 sn basılı tut hilesi (debug level paneli) | `MainMenu.unity` → MainMenuLevelButtonController `enableDebugLevelSelector` | editör + Development Build (test cihazında açık; mağaza build'inde Development Build KAPALI olduğu için kapalı) | Tamam |
| 2 | Çekiçte ekran çatlağı ihtimali | `01_Game.unity` → BoardController `hammerScreenCrackChance` | **0.3 (%30)** — 2026-11-04 düzeltildi | Tamam |
| 3 | Bostan Hasadı (kazı) event takvimi | `Resources/Events/HarvestConfig.asset` `activeDays` | 5/7 (2026-10-27 düzeltildi) | Tamam |
| 4 | AdMob gerçek reklam kimlikleri | `Resources/MonetizationConfig.json` + `GoogleMobileAdsSettings.asset` | İki platformun gerçek ID’leri bağlandı; dev yalnız test reklamı kullanır | AdMob ödeme profili, mağaza bağlantısı ve UMP mesajları tamamlanmalı |
| 5 | IAP makbuz doğrulama | `UI/Shop/StoreIap.cs` → `GooglePlayReceiptCheck` | 2026-11-04: Android cihaz üstü Google imza kontrolü eklendi (sahte makbuz → teslim/onay yok, Google 3 günde iade eder). Güvenli varsayılan: anahtar yoksa engellemez. **GooglePlayTangle üretilmeli** (Services ▸ In-App Purchasing ▸ Receipt Validation Obfuscator, Play lisans anahtarı). Sunucu doğrulaması Blaze planı ister | Tangle üretildi mi? |
| 6 | Log bayrakları | `Resources/GameDiagnostics.asset` | `analyticsEvents: 0` — 2026-11-04 kapatıldı | Tamam |
| 7 | Firestore takım kuralı | `firestore.rules` → `teams/{teamId}` `allow create, update: if request.auth != null` | Giriş yapmış HERKES her takımın kaydını (ad, açıklama, giriş seviyesi) değiştirebilir | Açık yayından ÖNCE: oluşturma yalnız kurucu (kendi uid'i), güncelleme yalnız üye / ad-açıklama yalnız kurucu. Kodun yazdığı alanlara göre yazılmalı (FirebaseTeamCloud). Kapalı testte ertelendi (2026-11-04) |

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

**Tarama 2026-11-04:** Resources/Settings/Data asset'leri ve sahne/prefab'larda adı debug/force/cheat/mock/fake/test geçen
açık alanlar arandı. Bulunanlar: `BridgeRepairConfig.debugForceAvailable: 1` (kod `#if UNITY_EDITOR`, cihazda etkisiz),
`WorkshopController.enableDebugKeys: 1` (`#if UNITY_EDITOR`, cihazda derlenmez), `enableDebugLevelSelector` (artık kodda
korumalı). Usta Ligi debug menüleri Editor klasöründe; `TestLevelProgressionBootstrap` `#if UNITY_EDITOR`.

## D. Event açılış seviyeleri (2026-10-27, kullanıcı kararı)
Haftalık Görevler (Progress Event) 20 · Seri Bonusu 30 · Tırmanış/Safari 45 · Bostan Hasadı 50 (5 gün açık / 2 gün ara) · Köprü Tamiri 60 · Gear Cup 70.
Firestore `config/progressEvent` → `minLevel` en az 20 olmalı (uygulama zaten 20'nin altına inmez).

## E. Satın alma / reklam hazırlığı (2026-10-31)
12 ürün Unity IAP Catalog’da USD fiyatlar ve TR/EN adlarla eşitlendi. Google Play satıcı profili engeli kalktı; 12 ürünün tamamı oluşturuldu ve `standard` satın alma seçenekleri **Active** olarak doğrulandı. Bölgesel fiyatlar tek USD girdisinden Google tarafından hesaplandı. AdMob’da son kontrolde **Payment setup incomplete** görülüyordu; UMP mesajı taslak, App Store Connect giriş bekliyor. Sunucu makbuz doğrulaması ve cihaz testleri tamamlanmadı. Ayrıntılar ve ürün listesi: [Monetization/Release-Setup.md](Monetization/Release-Setup.md).

## F. Firestore kota (2026-11-04)
Spark: 20K yazma / 50K okuma günlük. Ölçüm: 3-4 test oyuncusuyla günde 9.6K yazma (%48) — kök: oyun süresi istatistiği
(`PlayerStatsCloudReporter`) 15 sn'de bir işlem (3 okuma + 2 yazma) gönderiyordu, menüde boşta bile. Düzeltme: seviye sonucu
varken ≤60 sn, yalnız oyun süresi 5 dk, arka planda hemen. Bulut kaydı 15 → 60 sn (pause/satın almada anında). Liderlik
yalnız ekrana girince okunur (periyodik yenileme yok; aynı ziyarette sekme/alt seçenek geçişi tekrar okumaz). Beklenen: açık uygulama başına saatte ~480 → ~100-150 yazma (ÖLÇÜLMEDİ — yeni build ile Usage'tan
doğrula). 1.000-2.000 oyuncu için yine Spark yetmez → yayından önce Blaze + bütçe uyarısı.

## G. Android canlı öncesi durum — 2026-11-07
- **Satın alma:** Play Console'da lisans test kullanıcısı olarak ekli hesaplar "test satın alma" görür (para çekilmez) — bu beklenen. Gerçek oyuncu gerçek ödeme yapar. Tangle üretilmiş (`GooglePlayTangle.IsPopulated = true`).
- **Reklam:** Kod: Development Build → Google test reklamı ("Test Ad" yazar); mağaza (Development Build KAPALI) → gerçek birim `ca-app-pub-2688020202720647/2181902554`. Yeni gerçek birimler ilk günlerde düşük/sıfır dolum verebilir; AdMob "Payment setup incomplete" ve uygulamanın Play'e bağlanması/incelemesi tamamlanmadan gerçek reklam gelmeyebilir.
- **Release build:** Build Settings → Development Build KAPALI, Script Debugging kapalı. Aksi halde test reklamı, SlowMo vb. açılır.
- **Firestore:** Spark 50K okuma/20K yazma/gün. Takımlı oyuncu oturum başı ~80 okuma (takım dokümanı + ≤50 üye + 24 saatlik can istekleri + son 30 mesaj; 4 canlı dinleyici oturum boyu açık). 1-2K oyuncu için Blaze + bütçe uyarısı şart.
