# Level dağıtımı ve ücretsiz servis bütçesi

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
