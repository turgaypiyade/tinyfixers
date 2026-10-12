# Mağaza (IAP), Reklam (AdMob) ve Android kurulumu — 2026-10-12

Gece yapılanlar ve sıradaki adımlar. Kod derleme denetimi: tüm oyun kodu (544 dosya) + editör kodu, iOS ve Android
için, `TF_IAP` ve `TF_ADMOB` açık/kapalı her durumda **0 hata** (Unity IAP 5.4.4 ve Google Mobile Ads 11.5.0'ın
gerçek kaynak/kütüphanelerine karşı, Unity 6000.3.9f1 referanslarıyla). Cihazda test edilmedi.

## Yapılanlar

| Alan | Değişiklik |
|---|---|
| IAP paketi | `Packages/manifest.json` → `com.unity.purchasing` **5.4.4** |
| Tanım | Player Settings ▸ Scripting Define Symbols (iOS + Android): **`TF_IAP`** eklendi (`TF_ADMOB` KAPALI) |
| Mağaza kodu | `Scripts/UI/Shop/StoreIap.cs` (yeni): açılışta bağlanır, katalogdaki gerçek para tekliflerini ürün olarak çeker, satın alır, onaylar |
| Satın alma akışı | `ShopPurchaseService`: gerçek para → mağaza penceresi; içerik mağaza onayında `FulfillStorePurchase` ile verilir, sonra onaylanır. Aynı işlem (transaction) iki kez teslim edilmez (`iap_processed_transactions`, bulut kaydında) |
| Fiyatlar | Kartlar fiyatı **mağazadan** okur (oyuncunun ülkesi + para birimi). Katalogdaki `priceLabel` yalnız yedek |
| Arayüz | Market ve oyun içi market alım onayı gelince yenilenir; fail ekranındaki teklif şeridi onaydan sonra oyunu sürdürür |
| Katalog | `ShopCatalog.asset` → `Resources/` (sahne bağlantıları guid ile korunur); `ShopOffer.storeProductId` (boşsa `com.yigefep.tinyfixers.<id>`) |
| Reklam kodu | `Scripts/Core/Backend/AdService.cs` (yeni): UMP onay (GDPR) → SDK → ödüllü reklam önceden yüklenir. Üç nokta buna bağlandı: fail'de devam, can teklifi, ana menü can. Reklam hakkı artık **yalnız reklam izlenince** harcanır |
| Android | Paket adı `com.yigefep.tinyfixers`; `Editor/AndroidPlayerSetup.cs` Android modülü olan editörde (Windows) ikonları logodan kendiliğinden kurar; Android'de "Apple ile giriş" butonu gizli |

## Mağaza ürünleri (App Store Connect + Google Play — hepsi **Consumable / Tüketilebilir**)

| Ürün kimliği (Product ID) | Paket | Şu anki TL etiketi |
|---|---|---|
| `com.yigefep.tinyfixers.bundle_start` | Gezginci Çantası | 499,99 |
| `com.yigefep.tinyfixers.bundle_hunter_loot` | Avcı Ganimeti | 999,99 |
| `com.yigefep.tinyfixers.bundle_explorer_chest` | Kâşif Sandığı | 1.999,99 |
| `com.yigefep.tinyfixers.bundle_grand_safe` | Muhteşem Kasa | 3.999,99 |
| `com.yigefep.tinyfixers.bundle_legendary` | Efsanevi Hazine | 4.999,99 |
| `com.yigefep.tinyfixers.coins_1000` | 1.000 altın | 99,99 |
| `com.yigefep.tinyfixers.coins_5000` | 5.000 altın | 399,99 |
| `com.yigefep.tinyfixers.coins_10000` | 10.000 altın | 799,99 |
| `com.yigefep.tinyfixers.coins_25000` | 25.000 altın | 1.499,99 |
| `com.yigefep.tinyfixers.coins_50000` | 50.000 altın | 2.999,99 |
| `com.yigefep.tinyfixers.coins_100000` | 100.000 altın | 4.999,99 |

Kimliği değiştirmek istersen katalogda teklifin `storeProductId` alanına yaz (mağazadakiyle birebir aynı olmalı).
Ana fiyat önerisi: ABD doları ana ülke, Türkiye elle ayrı (bkz. konuşma notları).

## Sabah — Mac (iOS)

1. Unity'yi aç: IAP paketi iner ve derlenir. Konsolda hata olursa: Player Settings ▸ Scripting Define Symbols'tan
   `TF_IAP`'ı sil → proje eski haline döner (mağaza simülasyonu).
2. Editörde markette gerçek para paketine bas: Unity IAP editörde sahte mağaza kullanır (gerçek para yok).
   Başarıda paket içeriği gelmeli, kutlama çıkmalı.
3. Hesap açılınca: App Store Connect'te yukarıdaki ürünleri oluştur (Paid Apps anlaşması aktif olmalı) → TestFlight'ta
   sandbox satın alma (ücretsiz).

## AdMob (hesap açılınca)

1. Google Mobile Ads Unity eklentisini **.unitypackage** olarak indir (github.com/googleads/googleads-mobile-unity/releases).
   İçe aktarırken **ExternalDependencyManager klasörünün işaretini kaldır** (Firebase'in kopyası zaten var; ikisi çakışır).
2. Assets ▸ Google Mobile Ads ▸ Settings: iOS ve Android **App ID** + **User Tracking Usage Description**
   (ör. "Sana daha ilgili reklamlar göstermek için izin istiyoruz.").
3. Player Settings ▸ Scripting Define Symbols (iOS + Android): `TF_ADMOB` ekle.
4. `AdService.cs` içinde `RewardedIos` / `RewardedAndroid` sabitlerine gerçek reklam birimi kimliklerini yaz
   (boşken Google'ın **test** birimi kullanılır — testte hep test birimi; kendi reklamına tıklama).
5. AdMob ▸ Privacy & messaging: GDPR mesajı + iOS IDFA açıklaması (ATT penceresini bu tetikler).

## Windows (Android build)

1. `git pull` (dal: çalıştığın dal) → Unity'yi aç. `AndroidPlayerSetup` paket adını/ikonları kendisi kurar
   (Konsolda "[AndroidPlayerSetup] ... ayarlandı"). Menü: TinyFixers ▸ Android ▸ Oyuncu Ayarlarını Uygula.
2. Assets ▸ External Dependency Manager ▸ Android Resolver ▸ Force Resolve (Firebase + IAP Android kütüphaneleri).
3. Player Settings ▸ Publishing Settings: **yeni keystore oluştur** → dosyayı ve şifreyi GÜVENLİ YERE YEDEKLE
   (kaybolursa Play'e güncelleme çıkamazsın).
4. Build (AAB = Play Store, APK = doğrudan telefona test).
5. Firebase konsolu ▸ Android uygulaması ▸ SHA-1 parmak izini ekle (Google ile giriş için).

## Bilinen eksikler

- Sunucu tarafı makbuz doğrulaması yok (sahte satın almaya karşı) — canlıdan önce.
- Android **adaptive** ikon yok (Legacy + Round var); katmanlı ikon görseli gelince eklenir.
- Ödüllü reklam yüklü değilse buton basılınca bir şey olmaz (hak harcanmaz); "reklam hazır değil" mesajı eklenebilir.
