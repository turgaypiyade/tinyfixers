# Level dağıtımı ve ücretsiz servis bütçesi

## 2026-11-26 — R2 üzerinden ilerlemeye bağlı paketler

Kullanıcı ilk **50 seviyenin yerel kalmasını** onayladı. Yeni yol Unity'nin mevcut AssetBundle API'sini kullanır (Addressables paketi eklenmedi). 51–100, 101–150, 151–200, 201–250 ve mevcut katalog sonu 251 için paketler hazırlanır. Bir sonraki paket 15 seviye önceden, ilk kez seviye 35'te indirilir. Bu, aşağıdaki eski JSON level-güncelleme planından ayrı, uygulama sürümüne bağlı içerik dağıtımıdır. Yeni release baseline oluşturmaz.

Durum: `wonder-fixers-content` R2 Standard bucket oluşturuldu; `content.wondigames.com` özel alan adı bağlandı (sahiplik ve TLS aktif olarak doğrulandı). Yönetim tokenı yalnız `~/.wondi-cloudflare.env` içinde kalır. Runtime manifest ve paketlerde yönetim kimlik bilgisi yoktur. İçerik upload, içerik/player build veya release kaydı henüz yapılmadı. CDN cache kuralı ayrı Zone Cache Rules Edit yetkisi gerektirir; ilk deneme 403 verdi.

### Yerel uygulama

- `ProgressiveContent`: tek indirme worker'ı; HTTPS, dosyaya indirme, SHA-256/boyut doğrulaması arka plan işinde, geçici dosyadan tam dosyaya geçiş, üç deneme ve artan bekleme. Kesilen dosya baştan denenir; tamamlanmış dosyalar korunur. Yeniden kurulumda bulut ilerlemesi beklendikten sonra yalnız mevcut paket ve eşiğe giren sonraki paket istenir; örneğin 137 → 101–150 ve arka planda 151–200. Aradaki 51–100 zorunlu değildir (ortak asset bağımlılıkları ayrıca gelebilir). İndirme oyun açıkken çalışır, uygulama arka plana alındığında iptal edilir. OS seviyesinde uygulama kapalı indirme yoktur.
- Prefetch yalnız diske indirir. Unity asset'leri PreLevel/tahta/sahne geçişinden önce asenkron yüklenir. Açılışta bulut kayıt çözümü beklendikten sonra gerekli paket yüklenir; paket hazır olmadan booster harcanmaz. Eksik içerikte yüzde/boyut ve tekrar deneme ekranı gösterilir.
- İlgili olmayan paketlerin yönetilen referansları sahne geçişinde bırakılır ve bundle kapsayıcıları `Unload(false)` ile kapanır; yaşayan referanslar zorla yok edilmez. Son referans bırakıldıktan sonra Unity sahne temizliği varlıkları geri alabilir. Bellek/FPS ölçümü yapılmadı.
- Katalog GUID'leri editörde asıl `.asset` dosyalarına çözülür; LevelEditor kaynak seviyeleri korunur. Usta Ligi havuzu indirilmiş olma durumuna göre değişmez. Son kısmi pakette (251) önceki bölüm teması bağımlılığı korunur.
- İlk 50 ve ortak oyun kaynakları yereldir. Paketleme 50 sonrası LevelData/ChapterTheme ve bunların bağımlılıklarını kapsar. `Resources.Load` ile kullanılan tüm etkinlik/engel görselleri otomatik taşınmaz; bu ayrı bir sonraki küçültme işidir. Ortak yerel/uzak bağımlılıkların olası kopyaları `plan.json` içinde raporlanır. Gerçek mağaza indirme boyutu ancak player build raporuyla ölçülür.

### Dünya haritası / şehir görselleri — yerel kod hazır, henüz etkin değil

`WorldTour/City_*.png` altında 10 şehrin tamamlanmış ve şantiye sürümlerini içeren 20 PNG toplam **145,55 MiB kaynak dosya** tutuyor. Bu değer player boyutu veya tasarruf ölçümü değildir; platform texture sıkıştırması sonucu değiştirir. Statik GUID taramasında bu 20 görsele sahne/prefab/asset dosyalarından doğrudan referans bulunmadı.

Önceki “ilk iki şehri yerel tut” önerisi kod incelemesinde düzeltildi: `WonderOrder` ilk şehri ülkeye göre seçer (TR → mis7, US → mis6) ve sıralamayı bulutta saklar. İlk iki şehir herkes için aynı değildir. Kullanıcıya sorulan tercih için yanıt gelmeyince belirtilen varsayımla devam edildi: **tüm büyük şehir panoramaları ayrı paket, ilk 50 oyun seviyesi yerel**. İlk şehrin çevrimdışı ilk kurulumda gezilebilmesi bu varsayımda garanti edilmez; ilk 50 seviyenin oynanması şehir indirmesine bağlanmaz.

- Manifest şeması 2: `packs` seviye paketleri ve `cities` bağımsız şehir paketleri. Her şehir paketi aynı şehrin `City_misN` ve `City_misN_Site` görsellerini taşır. Dünya haritası, pinler, uçak, ortak karakter animasyonları ve sesler yerel kalır. Diğer harika/restorasyon görselleri bu değişikliğin kapsamına taşınmadı.
- Menüdeki `WonderBackgroundView` ve `WorldTourMap` mevcut kataloğu indirme servisine kaydeder. Bulut kayıt çözümü beklendikten sonra `WonderCatalog.Get(slot)` ile oyuncunun kayıtlı sırası kullanılır; şehir numarasına göre tahmin edilmez. Mevcut bölümün şehri diske hazırlanır; sonraki şehir açılmadan 15 seviye önce indirilir (25 seviyelik bölüm düzeninde ikinci slot için seviye 10, üçüncü için 35). Yeni bulut geri yüklemesi ön indirme planını tekrar değerlendirir. Seviye ve şehir indirmeleri aynı tek worker'ı paylaşır; ekranda istenen içerik önceliklidir.
- Yeniden kurulumda geçmiş şehirler topluca indirilmez. Önce mevcut bölümün şehri ve eşikteki sonraki şehir hazırlanır; eski şehre ziyarette eksik paket alınır. Tamamlanan dosyalar disk cache'inde kalır. Ön indirme texture yüklemez.
- `CityTourView` manifestte bulunan fakat indirilmemiş şehri yok saymaz. Şehir istenince gerekirse ilerleme/tekrar deneme ekranı açılır. Şehir indirmesi **Vazgeç** ile kapatılabilir; zorunlu seviye indirmesiyle aynı anda gösterilirse zorunlu kapı iptal edilmez. Sahne kapanırsa bekleyen şehir açılışı iptal edilir. Yalnız görüntülenecek tamamlanmış veya şantiye sprite'ı asenkron yüklenir; iki panorama birden decode edilmez.
- Şehir görünümü bir kullanım referansı tutar. Son görünüm yok olduğunda worker güvenli noktada şehir bundle'ını `Unload(true)` ile bırakır; aktif yükleme sırasında unload yapılmaz. Bunu güvenli kılmak için şehir bundle'ları birbirinden ve seviye bundle'larından bağımsızdır; paketleme/paylaşım kontrolleri aksi durumu reddeder. Seviye paketlerinin sahne temizliği de aynı worker'ın güvenli noktasına alındı.
- `ProgressiveCityBuild` kaynak sprite türünü, paket bağımlılıklarını ve yerel sahne/Resources/atlas referanslarını denetler. Dosya ve importer hash'leri plan kaydında korunur. **Doğrulanmış yayın etkinleştirildiğinde** 20 panorama GUID'leri korunarak `Assets/_Project/RemoteContent/WorldTour` içine taşınır. Kaynaklar şu an hâlâ Resources içinde; bu turda taşınmadı. Editör önizlemeleri her iki konumu destekler. Etkinleştirme veya yerel moda dönüşte hata olursa katalog/tema ve dosya taşımaları geri alınır. Manifest olmadan taşınmış kaynaklarla player build engellenir.
- `Tools/check_r2_content.py`: 15 çevrimdışı test; bağımsız şehir paketi, eksik sürüm/bağımlılık, kimlik tekrarı, adres yolu ve şehir–seviye bundle paylaşımı reddi dahil. `Tools/check_level_release.py`: 15 test başarılı. Unity compile/build/Play çalıştırılmadı. Cihazda ülkeye göre ilk şehir, restore sonrası sıra, 10/35 prefetch eşikleri, kesinti/iptal, offline tekrar ziyaret, şehir kapanışı ve belleğin geri verilmesi kullanıcı tarafından doğrulanmalı.

### 2026-11-26 — Bu Mac üzerinde iOS içerik build sonucu

Kullanıcının iOS build talebiyle mevcut Unity 6000.3.9f1 editöründe **AssetBundle içerik build'i tamamlandı** (uygulama sürümü 1.0). Player/Xcode/IPA build yapılmadı. Editör ve iOS içerik script derlemeleri tamamlandı; bu çalıştırmada build'i durduran hata görülmedi. Çıktı: `Exports/ProgressiveContent/iOS/`.

- 5 seviye paketi + 10 şehir paketi + 1 ortak bundle = 16 dosya, toplam **96.937.570 bayt / 92,45 MiB**.
- 10 şehir paketinin toplamı **44,73 MiB**; şehir başına iki panorama birlikte yaklaşık **3,92–5,03 MiB**. Bunlar üretilmiş paket boyutlarıdır; player/App Store boyut kazancı ölçümü değildir.
- En büyük dosya **40,90 MiB**. Yayın aracı `plan` kontrolü tüm dosyaların SHA-256 ve boyutunu doğruladı. Manifest SHA-256: `dd46fb2aed3a38690f9ff58a10ed4c919ec3059ae32afcbad27dadba12a298de`.
- `plan.json` içinde 483 olası ortak yerel bağımlılık raporlandı. Bu nedenle bütün uzak level paketlerinin boyutu kadar başlangıç boyutu azalacağı iddia edilmez.
- R2 upload/uzak doğrulama/etkinleştirme yapılmadı. Runtime manifest yok, 20 şehir kaynağı Resources içinde, release baseline oluşturulmadı. Sıradaki yayın işlemi ayrı açık kullanıcı talebi gerektirir.

**Windows / Android:** Kullanıcı Android build'lerini Windows makinesinde alacak. Aynı proje ve Unity sürümünde Android hedefi seçilip içerik paketleri yeniden üretilmeli (`Exports/ProgressiveContent/Android`). iOS bundle'ları Android'e kopyalanmaz. Android dosyaları ayrıca yayımlanıp doğrulandıktan sonra Android manifesti etkinleştirilir; platform guard iOS manifestiyle Android player build'ini reddeder. Cloudflare kimlik bilgileri o geliştirici makinesinde yerel olarak sağlanır; oyuna/repository'ye kopyalanmaz.

### Event görselleri — sonraki içerik grubu, henüz pakete alınmadı

İlk iOS çıktısında event art'ı yoktur. `Assets/_Project/Art/UI/MainScreenEvents` altında 91 kaynak dosya, toplam **62,16 MiB** (84 PNG ve diğer dosyalar) vardır; bu dosyalar uygulamadan çıkarılmadı. Kullanım yerleri bazı event'lerde Resources config alanları, bazı ekranlarda serialized prefab alanları veya doğrudan runtime kaynak yollarıdır. Yalnız dosyaları bundle'a eklemek mevcut ekranları çalışır kılmaz.

Event'ler tekrar ziyaret edilebilir ve takvim pencerelerinde açılır; seviyeye bağlı etkinlik şartı ile zamanlama birbirinden ayrı. Her event için ayrı paket kimliği tutulmalı. Menu/promo butonları, join popup, tutorial, ana ekran sahnesindeki overlay ve event ekranı art kullanmadan önce hazır olmalı. Event live olur olmaz arka planda indirme başlatılabilir; live değilken art indirilmez. İlk kez açarken paket yoksa indir/tekrar dene/iptal ekranı gösterilir; event durumu ve ödül işlemleri art yüklenmeden önce çalışmamalı. Sahne ve event penceresi kapanışında paket referansları bırakılır; event'ler ortak görseller paylaşabileceğinden dependency analizi bunları bağımsız pakete veya yerel ortak pakete ayırmalı.

Öncelikli paket grupları: **BridgeRepair, TinySafari (RisingEvent), Harvest, GiftFestival, GearCup**. Her biri için serialized referans envanteri ve Resources.Load adresleri çıkarılmalı; config.asset/resource dosyaları, sahne/prefab referansları, atlas üyelikleri ve runtime string adresleri birlikte çözülmeli. Güncel 62,16 MiB yalnız UI art klasörüdür; art dışındaki kaynaklar ve gerçek bundle boyutu ayrıca ölçülmelidir. Ortak buton/ikon, kilitli event kartı ve global ödül art'ı yerelde tutulabilir. Event saatine göre content prefetch, cloud restore ve offline davranışı cihazda kontrol edilmelidir. Bu iş yapılmadan ilk 50 seviye/şehir paketlerinin 92,4 MiB toplamını uygulama boyutu tasarrufu gibi değerlendirmemek gerekir.

### Kullanım ve etkinleştirme

1. Unity: `TinyFixers > Yayın > Parçalı İçerik (R2)`. Hedef Android veya iOS. İlk düğme **yalnız kullanıcı isteğiyle** AssetBundle içerik build'i çalıştırır; agent bu işlem sırasında çalıştırmadı.
2. Çıktı `Exports/ProgressiveContent/<Android|iOS>/`: immutable paketler `publish/`, `manifest.json`, kaynak bağımlılık ve katalog eşleme hash'leri `plan.json`. Manifest uygulama/Unity/platform sürümünü sabitler. Büyük paketler için Wrangler 300 MiB sınırı kontrol edilir.
3. Salt yerel kontrol: `python3 Tools/r2_content.py plan --directory Exports/ProgressiveContent/Android`.
4. **Açık yayın talebi sonrası**: `python3 Tools/r2_content.py publish --directory Exports/ProgressiveContent/Android --apply`. Paketler hash isimleriyle yüklenir; mutable latest manifest veya release baseline yazılmaz.
5. `python3 Tools/r2_content.py verify --directory Exports/ProgressiveContent/Android`: tüm yayın dosyalarını özel alan adından okuyup SHA/boyut doğrular; ancak tamamı başarılıysa yerel `verified.sha256` makbuzu üretir.
6. Unity penceresinde `Doğrulanmış yayını ... ETKİNLEŞTİR`: yalnız doğrulama makbuzu ve güncel kaynaklar varsa 50 sonrası yerel referansları GUID'e dönüştürür ve runtime manifesti ekler. Bu adım henüz yapılmadı; mevcut oyun yerel modda çalışmaya devam eder. Kaynak level/theme dosyaları değişmez. `Yerel içerik moduna geri dön` kaynak referanslarını geri yükler.
7. Android ve iOS için kendi içerik build'i/etkinleştirmesi gerekir; player build öncesi guard yanlış platform, değişmiş kaynak, katalog eşlemesi ve eksik manifesti reddeder. Yeni uygulama sürümü/Unity sürümünde içerik yeniden hazırlanır. Eski uygulamaların hash isimli R2 paketlerini silmeyin.

Cloudflare araçları: `status` salt okunur; `setup --apply` bucket/özel alan adı, `cache --apply` yalnız `.bundle` ve içerik host'una özgü CDN cache kuralı ekler. Öteki site/cache kurallarını değiştirmez; abonelik/fatura açmaz. `publish` için `--apply` yoksa upload yapılmaz.

Kontroller: `python3 Tools/check_r2_content.py` (15 çevrimdışı test), `python3 Tools/check_level_release.py` (15 test), Python sözdizimi, JSON/tekil çeviri anahtarları, `git diff --check`. Unity compile/build/Play veya simülasyon çalıştırılmadı. Cihazda 34→35 prefetch, 50→51 geçiş, kesinti/tekrar deneme, dolu disk, indirilen içerikle offline açılış, cloud restore 51+, Usta Ligi, 251 fallback ve sahne değişiminde bellek davranışı manuel doğrulanmalı.

---


İnceleme: 2026-10-23. Bu belge önerilen mimariyi ve doğrulanan mevcut durumu kaydeder. Uzaktan level indirme kodu henüz eklenmedi; sunucuya dosya yayımlanmadı ve bulut yapılandırması değiştirilmedi.

## Karar

Yeni/değişen level verileri için ilk tercih mevcut Cloudflare Workers Static Assets alanıdır. Firebase Hosting de kullanılabilir; ancak mevcut Cloudflare dağıtımı hazır olduğundan yeni bir servis kurmak gereksizdir. Firestore'u herkese aynı level dosyalarını dağıtmak için kullanmak yerine oyuncuya özgü veriler için ayırmak daha uygundur.

Unity `.asset` dosyaları editörde asıl tasarım kaynağı olarak kalır. Mobil istemciye küçük, sürümlenmiş JSON level tanımları gönderilir; oyun bu veriyi kendi `LevelData` runtime kopyasına dönüştürür. Uygulamada bulunmayan yeni engel davranışı/C# kodu bu yöntemle eklenemez. Yeni görsel/işitsel bağımlılıklar ayrıca paketlenmeli veya uygulama güncellemesiyle gelmelidir.

## Canlı bağlantı doğrulaması

- Kayıtlı Cloudflare kimlik bilgisi dosyası bulundu; değerleri çıktıya veya bu belgeye yazılmadı.
- `https://www.wondigames.com/` ve `https://wondigames.com/`: HTTP 200, Cloudflare; başlık Wonder Fixers — Match-3 Puzzle Game | Wondi Games.
- `https://wondigames.com/legal/version.json`: HTTP 200, ETag mevcut.
- Cloudflare token doğrulaması: HTTP 200, active.
- `wondigames` Worker ayarları: HTTP 200; assets binding mevcut.
- İki alan adı da Cloudflare API'sinde `wondigames` servisine bağlı.
- Abonelik/faturalandırma sorgusu: HTTP 403. Gerçek hesap planı ve kullanımı bu tokenla doğrulanamadı; aşağıdaki kotalar resmî ürün belgeleridir, hesaba ait ölçümler değildir.

`Website/wrangler.jsonc` yerel ayarında Worker yalnız `/img/clips/*` ve `/social/reels/*` yollarında önce çalışır. Level dosyaları bu desenlerin dışında statik sunulmalıdır. `Website/build.py` şu anda oyun verisi klasörünü yayıma kopyalamıyor; ileride bu klasör açıkça eklenmelidir.

## Veriyi nereye koyalım?

| Veri | Önerilen yer | İstemci davranışı |
| --- | --- | --- |
| Level düzeni, holes, hamleler, hedefler, engel aşamaları, isim/zorluk bilgisi | Cloudflare statik JSON | Sürüm değişince indir, yerelde doğrula ve sakla |
| Küçük ortak etkinlik/denge tanımları | Aynı statik içerik kataloğu | Uygulamada varsayılan + son geçerli önbellek |
| Hesap ve giriş | Mevcut Firebase Auth | Mevcut kimlik akışı |
| Oyuncu ilerlemesi, sahiplik ve cloud save | Mevcut korumalı Firestore belgeleri | Değişiklikleri birleştirerek senkronla |
| Takım sohbeti ve sıralama | Firestore | Sınırlı sorgu, ekran açıkken dinleme, sonuç önbelleği |
| Toplu telemetri | Önce mevcut Firestore gönderim sıklığını azalt | Yerel dayanıklı kuyruk, toplu gönderim, geri çekilerek yeniden deneme |
| Büyük/çok sık yayımlanan medya veya platform paketleri | İhtiyaç doğunca Cloudflare R2 + özel alan adı | Yerel içerik cache'i; r2.dev üretim adresi olarak kullanılmaz |

## İndirme akışı

Örnek yollar henüz canlı değildir:

```text
/game-content/stable/manifest.json
/game-content/levels/188/<content-hash>.json
/game-content/levels/189/<content-hash>.json
```

1. Uygulama kendi gömülü levelleriyle başlayabilmeli. İnternet veya Firebase erişimi oyun açılışının zorunlu koşulu olmamalı.
2. Başlangıçta kısa zaman aşımıyla küçük manifest kontrol edilir. İlk öneri oturum başına kontrol, 15–30 dakika istemci bekleme süresi ve rastgele küçük gecikmedir; her level girişinde tam katalog indirilmez.
3. Manifest yalnız sürüm/uyumluluk ve dosya metadatasını taşır. Dosyalar değişmez, içerik hash'i olan adreslerden sunulur. İçerik aynıysa yeniden indirilmez.
4. Mevcut ve yakın gelecek leveller önceliklendirilir. Tüm eski levellerin her gün tekrar indirilmesi gerekmez. Dosyalar geçici yola indirilip doğrulandıktan sonra kalıcı önbelleğe atomik geçirilir.
5. Boyut/şema, hücre dizilerinin uzunluğu, obstacle ID/footprint/origin, stage, wall/channel yolları ve desteklenen özellikler doğrulanır. Eksik veya uyumsuz veride son geçerli önbellek/gömülü level kullanılır. Hash bütünlüğü içindir; üretim manifesti ayrıca imzalanabilir, imza anahtarı yayımlayıcıda kalır.
6. Aktif oyun tahtası indirme nedeniyle değiştirilmez; yeni revizyon sonraki level açılışında uygulanır. Manifest önceki sürüme çevrilerek geri dönüş yapılabilir; eski uyumlu içerikler yeterince tutulmalıdır.

Manifestte `schemaVersion`, `contentRevision`, `minClientBuild`, ilgili level numaraları, dosya hash/boyutları ve gerekli özellikler bulunmalı. Geri alınan sürüm de istemci tarafından kabul edilebilmeli; yalnız daha büyük revizyonu kabul etmek geri dönüşü bozar.

Level kimliği dosya adından çıkarılmaz: bu projede `LevelCatalog` içindeki global sıra esas alınır. Yeni level eklemek katalog metadatasını da güncellemeyi gerektirir. Sprite, AudioClip ve obstacleLibrary gibi Unity referansları JSON içinde Editor instance ID olarak taşınmaz; istemcinin bildiği sabit anahtarlarla bağlanır.

## Projeye entegrasyon

`LevelRuntimeSelector` şu anda yerel kataloğu çözüyor; `GridSpawner.ResolveLevelData` kaynağı klonlayıp Firestore hamle ayarını uyguluyor. Yeni içerik kaynağı yalnız GridSpawner'a eklenmemeli: prelevel hedefleri, gerçek tahta, katalog, isimler ve ilerleme sınırları aynı sürümü okumalı. Ortak bir level repository üzerinden doğrulanmış kaynak seçilmeli ve runtime mutasyonları için yine bağımsız kopya kullanılmalı.

`LevelRemoteTuning` zaten `config/levels` belgesinden hamle düzeltmesi alıyor, yerelde saklıyor ve dönüşte 10 dakika koşuluyla yeniden okuyor. Uzaktan tam level geldiğinde eski Firestore hamle override'ının yeni leveli yanlışlıkla ezmemesi için tek yetkili kaynak/sürüm kuralı belirlenmeli. Geçişte eski istemciler bu mevcut yolu sürdürebilir.

Yayımlama: `.asset` → doğrulanan JSON export → sürümlü dosyalar → en son manifest. Website'nin statik build çıktısına içerik ve uygun `_headers` kuralları eklenir. Manifest kısa cache, hash'li dosyalar uzun `immutable` cache alır. Mevcut web sayfalarının `max-age=0` yanıtı level dosyalarına aynen taşınmamalıdır. Cloudflare yönetim tokenı yalnız geliştirici/yayın ortamında kalır; oyuncu uygulamasına gömülmez.

## Ücretsiz planlar ve büyüme

- Cloudflare Workers Static Assets: statik istekler ücretsiz/sınırsız; Worker kodunu çalıştıran istekler ayrı kotaya girer. Workers Free için günlük 100.000 dinamik istek, statik dağıtımda sürüm başına 20.000 dosya ve dosya başına 25 MiB sınırı belgeleniyor. [Statik ücretlendirme](https://developers.cloudflare.com/workers/static-assets/billing-and-limitations/), [ürün sınırları](https://developers.cloudflare.com/workers/platform/limits/).
- Firebase Hosting, Firestore ve Firebase Storage farklı ürünlerdir. Hosting'in ücretsiz aktarımı aylık 10 GB; Spark'ta aşım sonrası siteler devre dışı kalabilir. Cloud Storage for Firebase ise güncel kurallara göre Blaze gerektiriyor; Blaze'de ücretsiz kullanım payı olması faturalandırma hesabı gerekmediği anlamına gelmez. [Hosting](https://firebase.google.com/docs/hosting/usage-quotas-pricing), [Storage gereksinimi](https://firebase.google.com/docs/storage/faqs-storage-changes-announced-sept-2024).
- Firestore ücretsiz payı: günlük 50.000 belge okuması, 20.000 yazma, 20.000 silme; 1 GiB saklama ve ayda 10 GiB dış aktarım. Limitler kullanıcı başına değil proje/veritabanı kotasıdır. [Firestore kotaları](https://firebase.google.com/docs/firestore/quotas).
- R2 Standard ücretsiz payı ayda 10 GB-month saklama, 1 milyon Class A ve 10 milyon Class B işlem; internet çıkışı ücretsiz. Kota üstü işlemler ücretlidir. Üretimde özel alan adı/cache gerekir; geliştirme r2.dev adresi oran sınırlıdır. [R2 fiyatları](https://developers.cloudflare.com/r2/pricing/), [erişim](https://developers.cloudflare.com/r2/buckets/public-buckets/).

Kullanıcı sayısı tek başına kapasite ölçüsü değildir: istek/oturum, belge okuma-yazma/oyuncu-gün, indirilen bayt, sohbet dinleyicilerinin dağıtımı ve tekrar denemeler birlikte ölçülmelidir. CDN büyümeyi karşılar; kota/sunucu kesintisinde oyun yerel içerikle devam etmelidir.

Kaynak kodda özellikle önemli bulgu: `PlayerStatsCloudReporter` her 15 saniyede oynama süresini kuyruğa ekleyip gönderiyor. Level içermeyen payload iki belge okur ve iki belge yazar; level payload'ı genellikle üç okuma/üç yazma yapar. Bu, SDK tekrarları ve diğer servisler hariç koddan çıkarımdır; canlı kullanım ölçümü değildir. Örnek varsayım: 1.000 oyuncunun günde 10 dakika çevrimiçi kalması, yalnız 15 saniyelik süre payload'larıyla yaklaşık 80.000 belge yazmasına ulaşabilir. Level dosyasını CDN'e almak bunu çözmez.

İlk maliyet iyileştirmesi: telemetriyi daha uzun aralıklarla/oturum sonunda toplamak, sıralamayı ekran açılışı veya sonuçlarla sınırlamak, sohbet dinleyicilerini görünürlükle yönetmek ve cloud save dirty yazmalarını birleştirmek. Mevcut kalıcı kuyruk/sequence-idempotency korunmalı. Ücretsiz kota kullanımı ve başarısız istekler için uyarı eşikleri belirlenmeli. Blaze'e geçilirse bütçe uyarıları Firestore için otomatik harcama tavanı değildir. [Bütçe davranışı](https://firebase.google.com/docs/projects/billing/avoid-surprise-bills).

Medya notu: projeye yeni eklenen Harbor Adventure kaynak WAV'ı yaklaşık 32,75 MiB; ham hali statik dosya başına 25 MiB sınırını aşar. Level JSON bundan bağımsız ve küçüktür. İleride uzaktan müzik dağıtılacaksa uygun yayın formatı/paketi ya da R2 değerlendirilmelidir; bu incelemede ses dönüştürülmedi.

## Uygulama sırası

1. Level JSON şeması, katalog kimlikleri ve export/doğrulama.
2. Ortak runtime repository, sürüm kontrolü, yerel cache ve çevrimdışı fallback.
3. Cloudflare statik yayımlama ve geri dönüş akışı; eski/yeni istemci uyumluluğu.
4. Firebase telemetri/cloud save/sosyal sorgu bütçesi düzenlemesi ve gözlemleme.
5. Gerektiğinde büyük medya için R2; işlem başına maliyet ölçülmeden yeni dinamik endpoint eklememek.

Bu incelemede Unity build/Play çalıştırılmadı; ağ testleri salt okunurdu. Yayındaki siteye ve Firebase'e veri yazılmadı.
