# Mercy ve dönüş ödülü

16 Ekim 2026 oyuncu kuralı:

- Reklam ödülü almamış ve gerçek satın alma yapmamış oyuncuya sıradan yenilgiler mercy açmaz. En az **72 saatlik aradan dönüşte**, ilk oynadığı seviyeye %15 normal refill renk yardımı verilir; o seviye kazanılana kadar tekrar denemelerde sürer.
- Reklam ödülü almış veya satın alma yapmış oyuncuda **aynı seviyede gerçekten harcanan net canlar** sayılır. 9 / 12 / 15 can, sonraki denemede %15 / %30 / %45 refill yardımı açar. Üç günlük dönüş bu grupta dokuz can şartını kaldırmaz.
- Ücretsiz can dönemleri, bakiyesiz harcama ve devam ederken iade edilen can sayılmaz. Yeniden başlatma sayacı korur; seviye değişimi veya kazanmak sıfırlar. Eski yenilgi sayıları geriye dönük can harcamasına dönüştürülmez.
- Mercy hazır special, ek hamle veya ödül vermez. Yalnız yeni gelen normal taşların renk seçimini etkiler; special olağan eşleşmeyle oluşabilir. Kademe deneme başında sabitlenir. Aktif denemede reklam/satın alma grubu değişirse uygunluk yeniden kontrol edilir.

`AdService` yalnız gerçek SDK ödül callback'inde; `ShopPurchaseService` gerçek para teslimatında kanıt yazar. Eski `ad_continue_used_level` ve `iap_processed_transactions` kayıtları da tanınır. Geliştirme satın alma teşekkür kayıtları satın alma kanıtı sayılmaz. Önceki sürümde diğer reklam türlerinin kalıcı geçmişi tutulmadığından, bu geçmiş geriye dönük çıkarılamaz.

## Dönüş paketi

En az 72 saat aradan dönen **bütün oyunculara**, mercy grubundan bağımsız olarak: **500 altın + 1 çekiç + 1 karıştırma + 1 çizgi jokeri + 1 Pulse jokeri**.

Aktivite saati, bekleyen dönüş ve paket makbuzu cihazda tutulur. Yeni bir 72 saatlik ara yeni paket açabilir; kısa kapat/aç veya aynı teslimat ikinci paket vermez. Bekleyen paket varsa üzerine yenisi birikmez. Başlangıç kurulumu dönüş sayılmaz; bozuk/gelecekteki zaman damgası ödül açmaz.

Bu sürümden önce güvenilir son-aktivite kaydı tutulmadığından, eski profillerde ilk açılış aktivite takibini başlatır; geçmişteki üç günlük ara tahmin edilmez.

Paket ana menüde, **bulut restore kararı tamamlandıktan ve açık popup/market/yükleme kapandıktan sonra** teslim edilir ve ortak sandık töreniyle gösterilir. Çevrimdışı restore çözülemezse paket bekler; bağlantı dönüp restore tamamlandığında teslim edilir. Bu bekleme, sonradan gelen bulut envanterinin hediyeyi ezmesini önler. Altın, iki booster, iki joker, para defteri ve makbuz aynı PlayerPrefs kaydında kalıcılaşır. Gösterim sırasında uygulama kapanırsa kazanılan içerik korunur ve yeniden verilmez.

Can sayacı ve reklam/alım kanıtları cloud manifest içindedir; eski cloud snapshot yerel kanıtı silemez. Cihaza ait aktivite/dönüş token'ları cloud dışında tutulur. Paket envanter ve para bakiyeleri mevcut cloud akışına dahildir.

## Kontrol

`csi Tools/check_mercy_retention.csx` üretim kaynaklarıyla sınır koşullarını, can iadesini, ödül tekrarını, envanteri ve cloud manifestini denetler. `csi Tools/check_mercy_refill.csx` normal renk yardımı ve special üretim sınırlarını denetler. Simülasyon oyuncu kayıtlarına dokunmaz. Unity çalıştırılmadı, proje build edilmedi.

Aktif refill yolunda yeni PlayerPrefs okuması yok; 2.000 tekrarlı okumada stub sayacıyla doğrulandı. Aktivite kaydı mevcut servis üzerinden dakikada bir, sunum kontrolü saniyede bir yapılır. UI nesne taraması yalnız bekleyen ödül varken ana menüde çalışır; gameplay taraması veya yeni kalıcı Update bileşeni eklenmez. Cihaz performansı ölçülmedi.
