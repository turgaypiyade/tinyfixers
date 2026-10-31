# Satın alma ve reklam hazırlığı — 2026-10-31

## Mevcut durum

- Oyundaki 12 gerçek para teklifi `Assets/_Project/Resources/ShopCatalog.asset` içinde korunuyor; mağaza ürün kimlikleri açıkça yazıldı.
- Aynı 12 teklif `Assets/Resources/IAPProductCatalog.json` içinde **Consumable** olarak oluşturuldu. Unity IAP Catalog, mağaza konsollarında ürün oluşturmaz. Codeless auto initialization kapalı; tek teslimat sahibi `StoreIap`.
- Android ve iOS için TF_IAP / TF_ADMOB açık. Unity Purchasing 5.4.4 ve Google Mobile Ads 11.6.0 projede.
- AdMob'da iki uygulama ve iki ödüllü reklam birimi oluşturuldu; başarı ekranlarından kimlikler doğrulandı ve projeye yazıldı.
- 2026-10-31: uygulama admin izinleri doğrulandı. Kullanıcının satıcı profili kurulumundan sonra merchant uyarısı kalktı. Google Play'de aşağıdaki **12 ürün oluşturuldu**; her birinin `standard` Buy satın alma seçeneği **Active**, backwards compatible ve 174 ülke/bölgeye açık. Son listede **1 - 12 of 12** ve her üründe bir etkin seçenek doğrulandı. ABD fiyatları tek tek onaylanan USD tablosuyla karşılaştırıldı; TR/EN ad ve açıklamalar kaydedildi. Bu durum, banka/kimlik doğrulamasının veya uygulama yayın onayının tamamlandığı anlamına gelmez.
- App Store Connect giriş ekranında; iOS mağaza ürünleri oluşturulmadı.
- AdMob **Payment setup incomplete** gösteriyor. Uygulamalar henüz herkese açık mağaza sayfalarına bağlanmadı; hesap/uygulama onayı ve gerçek reklam sunumu tamamlanmış değil.
- 2026-10-31: AdMob European regulations altında **Wonder Fixers - European consent** taslağı oluşturuldu; listede **Draft**, **2 apps**, **English (en), +1 more** olarak doğrulandı. Ek dil Türkçe; Consent / Do not consent / Manage options açık, reddetme seçeneği tüm form bölgelerinde açık. İki uygulamaya mevcut `https://wondigames.com/privacy/` adresi bağlandı. Taslak yayınlanmadı; reklam ortakları/amaçları gözden geçirilmeli ve gerçek cihazda form doğrulanmalı. Oyunda UMP güncellemesi ve ayarlardan yeniden açma akışı hazır.
- Güvenilir sunucuda makbuz doğrulaması henüz yok. İstemci makbuzunun varlığını kontrol etmek veya Unity PendingOrder olayı almak, sunucu doğrulaması değildir.

## Satılacak ürünler

2026-10-31 kullanıcı kararı: aşağıdaki 12 ürün ve TR/EN adları esas alınır. Baz fiyat para birimi **USD**. Play Console'da tüm bölgeler seçilip USD fiyat bir kez girildi; Google yerel fiyatları kur, uygulanabilen vergi ve yerel fiyat yuvarlamasıyla hesapladı. Bu tek seferlik dönüşümdür; günlük kura sürekli bağlı fiyat değildir. Gerektiğinde toplu fiyatlandırma yeniden uygulanabilir. [Google'ın one-time products açıklaması](https://support.google.com/googleplay/android-developer/answer/16430488?hl=en). Bu tabloda $12.00 fiyatı yoktur; 12 sayısı ürün adedidir. Oyuncu gerçek mağazanın ülke/para birimine göre fiyat görür. Unity IAP Catalog da USD fiyatlar ve TR/EN isimlerle eşitlendi; Google Play ürünleri ayrıca konsolda oluşturuldu.

| Paket TR / EN | Ürün kimliği | Onaylanan USD baz fiyat | Oyunda teslim edilen içerik |
|---|---|---|---|
| Hoş Geldin Paketi / Welcome Pack | `com.yigefep.tinyfixers.bundle_welcome` | $1.99 | 2,500 altın; 3 güçlendiricinin her birinden ×1; 1 saat sınırsız can |
| Gezginci Çantası / Traveler's Bag | `com.yigefep.tinyfixers.bundle_start` | $9.99 | 5,000 altın; 5 güçlendiricinin her birinden ×1; 3 joker için 1 saat sınırsız kullanım |
| Avcı Ganimeti / Hunter's Loot | `com.yigefep.tinyfixers.bundle_hunter_loot` | $19.99 | 10,000 altın; 5 güçlendiricinin her birinden ×2; 3 joker için 12 saat sınırsız kullanım |
| Kaşif Sandığı / Explorer's Chest | `com.yigefep.tinyfixers.bundle_explorer_chest` | $39.99 | 25,000 altın; 5 güçlendiricinin her birinden ×4; 3 joker için 24 saat sınırsız kullanım |
| Büyük Kasa / Grand Safe | `com.yigefep.tinyfixers.bundle_grand_safe` | $79.99 | 50,000 altın; 5 güçlendiricinin her birinden ×10; 3 joker için 72 saat sınırsız kullanım; 12 saat sınırsız can |
| Efsanevi Paket / Legendary Pack | `com.yigefep.tinyfixers.bundle_legendary` | $99.99 | 65,000 altın; 5 güçlendiricinin her birinden ×13; 3 joker için 100 saat sınırsız kullanım; 18 saat sınırsız can |
| 1.000 Altın / 1,000 Coins | `com.yigefep.tinyfixers.coins_1000` | $1.99 | 1,000 altın |
| 5.000 Altın / 5,000 Coins | `com.yigefep.tinyfixers.coins_5000` | $7.99 | 5,000 altın |
| 10.000 Altın / 10,000 Coins | `com.yigefep.tinyfixers.coins_10000` | $14.99 | 10,000 altın |
| 25.000 Altın / 25,000 Coins | `com.yigefep.tinyfixers.coins_25000` | $29.99 | 25,000 altın |
| 50.000 Altın / 50,000 Coins | `com.yigefep.tinyfixers.coins_50000` | $54.99 | 50,000 altın |
| 100.000 Altın / 100,000 Coins | `com.yigefep.tinyfixers.coins_100000` | $99.99 | 100,000 altın |

Her başarılı gerçek para alımında mevcut teşekkür hediyesi ayrıca uygulanır. Paket ödülleri ve uygunluk kuralları korundu. Fiyat/ad metadata’sı kullanıcının son USD tablosuyla eşitlendi; Hoş Geldin Paketi mevcut katalogdaki OnceEver kuralını korur.
Makine tarafından okunabilir içerik dökümü: `Store-Products.json` (mağazaya doğrudan yüklenen bir API/CSV dosyası değildir).

## Mağaza konsollarında tamamlanacaklar

1. Google Play ürün oluşturma için satıcı profili engeli kalktı. Hesap sahibi kendi ödeme ekranından varsa banka/vergi/kimlik doğrulama adımlarını takip etmeli; bu alanların tamamlandığı ayrıca doğrulanmadı. Banka/vergi bilgileri veya sözleşme onayları depoya yazılmamalı.
2. Google Play ürünleri ve satın alma seçenekleri etkin. Sonraki adım Play test kanalında lisans test kullanıcısıyla fiyatların okunmasını ve satın alma/teslimat akışını doğrulamak. Uygulamanın ülke dağıtımı ürünün ülke ayarlarından önceliklidir.
3. App Store Connect → Wonder Fixers → In-App Purchases altında aynı kimliklerle Consumable ürünler oluşturulmalı; yerelleştirme, fiyat, satışa açıklık ve inceleme bilgileri tamamlanmalı. Paid Apps Agreement / banka / vergi durumunu hesap sahibi tamamlamalı.
4. AdMob ödeme profili tamamlanmalı. Yayınlanınca uygulamalar gerçek mağaza sayfalarına bağlanmalı; app-ads.txt ve uygulama inceleme durumları kontrol edilmeli.
5. AdMob Privacy & messaging altında uygulamalara uygun UMP mesajları ve iOS IDFA/ATT mesajı hazırlanıp yayınlanmalı. UMP formu yayınlandıktan sonra EEA/UK/CH ve iOS cihaz senaryoları doğrulanmalı.
6. Canlıdan önce güvenilir sunucuda Apple/Google makbuz doğrulaması ve hesaplar/cihazlar arası idempotent teslimat tamamlanmalı. Mağaza API anahtarları ve servis hesabı yetkileri yalnız sunucuda tutulmalı.

## AdMob kimlikleri

| Platform | App ID | Rewarded ad unit ID |
|---|---|---|
| Android | `ca-app-pub-2688020202720647~6458516927` | `ca-app-pub-2688020202720647/2181902554` |
| iOS | `ca-app-pub-2688020202720647~6116037462` | `ca-app-pub-2688020202720647/9086523571` |

App ID: `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` / Assets → Google Mobile Ads → Settings.
Reklam birimi ID: `Assets/_Project/Resources/MonetizationConfig.json`. Bunlar public istemci kimlikleridir; yönetim anahtarı değildir.
Development build her zaman Google test reklam birimlerini kullanır. SDK eksik cihaz sürümü reklam izlenmiş gibi ödül vermez; mağaza bağlantısı başarısız cihaz sürümü satın alma simüle etmez.

## Kod davranışı ve yerel araçlar

- Satın alma, mevcut mağaza fiyatıyla başlar; içerik yalnız paid/pending siparişten teslim edilir. İptal/deferred işlemler içerik vermez. Eksik işlem kimliği, eksik makbuz veya bilinmeyen ürün onaylanmadan bırakılır.
- Önceki oturumdan kalan ödenmiş siparişler çekilir. Teslimat boot/hesap geri yükleme kapısı açılana kadar bekler. Geçmiş işlem kayıtları 200 işlemden sonra silinmez; eski bulut kaydı cihazın teslimat kaydını ortadan kaldıramaz.
- Teslimat makbuzu UI bildirimi öncesinde kaydedilir; UI dinleyicisinin hatası tekrar teslimata yol açmaz. Yerel PlayerPrefs tabanlı içerik+makbuz çok anahtarlı atomik bir sunucu işlemi değildir: ödül yazımlarının tam ortasında süreç kapanması, yeniden kurulum ve farklı cihaz yarışları için sunucuda işlem gereklidir.
- Reklamlarda tek önbellek, tek yükleme isteği ve gecikmeli tekrar deneme kullanılır. SDK'nin tek kullanımlık reklam nesnesi gösterim sonunda Destroy edilir; yeniden kullanım/pool yerine SDK'nin desteklediği yeni nesne yüklenir. Teardown geç gelen native callback'leri geçersiz kılar, bekleyen coroutine ve ayar aboneliklerini temizler.
- Ses ayarları AdMob'a da uygulanır. Ödül callback'i gelmeyen kapatma/hata ödül vermez; yinelenen callback'ler ikinci ödül vermez.
- `Tools → TinyFixers → Monetization → Sync Unity IAP Catalog` teklifleri yeniden eşitler. Manuel mağaza metadata/fiyat alanları korunur.
- Aynı menüde Android/iOS release setup kontrolü var. Mobil build ön kontrolü eksik gerçek ID, eksik sembol veya katalog uyuşmazlığında hata verir; kendisi build başlatmaz. Bu kontrol hesap onayı veya mağazadaki gerçek ürün durumunu uzaktan doğrulamaz.
- Firebase'in EDM 1.2.187 kopyası korundu. AdMob'un ikinci EDM UPM kurulumu ayardan kapatıldı. SDK binary dağıtımında bulunmayan source assembly referansları `GoogleMobileAds.Editor.asmdef` içinden kaldırıldı; otomatik referanslanan resmi DLL'ler kullanılıyor. Native Android/iOS bağımlılık çözümlemesi sonraki yetkilendirilmiş cihaz export/build akışında ayrıca doğrulanmalı.

## Kontroller

- `csi Tools/check_monetization.csx`: kurulu Unity/IAP/AdMob DLL'leriyle Android, iOS, development, editor ve SDK'sız dalların API bağlama kontrolü; build veya assembly çıktısı üretmez.
- `csi Tools/test_monetization.csx`: gerçek üretim metotlarına fake SDK bağlayan 25 davranış kontrolü; satın alma kapısı, iptal, exception, deferred, tekrar teslimat, bozuk UI dinleyicisi, eski cloud snapshot, eksik/bilinmeyen sipariş, miktar, reklam ödülü, kapatma, hata, gizlilik değişiminde eski yükleme ve teardown.
- Cihazda gerçek mağaza satın alması veya reklam gösterimi doğrulanmadı. Oyun/player build veya dotnet build yapılmadı (Unity editörü asset import sırasında scriptlerini otomatik derleyebilir); uygulama ya da level yayınlanmadı.
- Ölçülmüş FPS/CPU/native SDK profili yok. Nesne ve görev sınırları koddan kontrol edildi; performans kazancı ölçümü iddia edilmiyor.

## Cihaz kabul senaryoları

Android Play test kanalında lisans test kullanıcısıyla ve iOS TestFlight/sandbox ile: mağazadan yerel fiyat, başarılı alım, iptal, ödeme beklemesi, bağlantı kesintisi, ödeme sonrası uygulamayı kapatma/açma ve teslim edilmiş siparişi yeniden görme. Her birinde içerik ve onay sayısı kontrol edilmeli.
Reklamda: test etiketi, ödülsüz kapatma, tamamlanan ödüllü video, no-fill/offline, arka plana alma, sahne değişimi, gizlilik tercihi değişikliği ve yeniden yükleme. Test sırasında gerçek reklama tıklanmamalı.

Resmi kaynaklar: [Unity IAP satın alma akışı](https://docs.unity.com/en-us/iap/purchases), [Google Unity kurulumu](https://developers.google.com/admob/unity/quick-start), [UMP](https://developers.google.com/admob/unity/privacy), [Rewarded](https://developers.google.com/admob/unity/rewarded).

## 2026-10-31 Google yetkilendirme takibi

Uygulama izinleri salt okunur incelendi; herhangi bir kullanıcının erişimi değiştirilmedi. Google’ın [hesap erişim düzeyleri açıklamasına](https://support.google.com/googleplay/android-developer/answer/9844686?hl=en) göre Payments profile sayfasına yalnız account owner erişip ödeme profili bağlayabilir. Bu adım için kullanıcıdan hesap sahibiyle ilerlemesi istendi.

AdMob UMP taslağı 2026-10-31 tarihinde kaydedildi ve yayın anahtarı kapalı olarak doğrulandı. IDFA explainer veya US regulations mesajı bu adımda oluşturulmadı. Kullanıcının Google Play satıcı profili kurulumundan sonra ürün oluşturma ekranının açıldığı ve 12 ürünün etkinleştirildiği doğrulandı. AdMob ödeme durumunun bu işlemden sonra değiştiği doğrulanmadı.

## USD ürün tanımları ve satıcı profili — 2026-10-31

Kullanıcının 12 ürünlük USD tablosu esas alındı. Hoş Geldin Paketi mevcut oyun kataloğunda zaten bulunuyordu; eksik Unity IAP kaydı eklendi. Büyük Kasa ve Efsanevi Paket adları ile Traveler's Bag / Legendary Pack İngilizce karşılıkları güncellendi. Oyundaki ödüllere dokunulmadı.

Google Play Monetize with Play ekranındaki satıcı hesabı kurma uyarısı kalktı; One-time products ekranı ve Create one-time product bağlantısı açıldı. Bu, ürün oluşturma erişiminin açıldığını gösterir; kimlik/banka doğrulamasının tamamlandığına dair ayrıca bir durum henüz görülmedi.

12 ürünün tamamı oluşturuldu. Her ürünün kaydedilmiş detay ekranında Active durumu, etkinleştirme öncesinde ABD satırında tam USD fiyatı doğrulandı. Son ürün listesinde 12 benzersiz kimlik ve her birinde bir etkin satın alma seçeneği görüldü. Gerçek para tahsilatı veya uygulama yayını yapılmadı.
