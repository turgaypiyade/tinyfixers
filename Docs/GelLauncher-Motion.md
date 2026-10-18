# GelCabinet / jel kartuşu kırılma hareketi

`GelLauncherFireAction`, dört yönde aynı yerel hareket sırasını kullanır:

1. **0,34 saniye hazırlık:** kapaklı çatlak sprite korunur. Gövdenin merkezi atış yönünün tersine 0,28 hücre gider; boyu %18 kısalırken eni %24 genişler.
2. **0,075 saniye ileri atılma:** gövde hızlıca başlangıç merkezinin 0,14 hücre ilerisine geçer. Boyu %108, eni %92 olur.
3. **Kapak ayrılması:** aynı karede kapaksız sprite'a geçilir ve kapak deforme olmuş gerçek ağız konumundan çıkar. Ayrı bir kapak büyütme/bekleme evresi yoktur; ilk hedef hücreye hareket 0,03 saniyedir. Gövde 0,035 saniye daha ileri devam edip 0,24 hücreye ulaşır, sonra geri seker.

Gerçek engel görseli devralındığında bekleme animasyonu hemen durdurulur; gecikmeli bileşen silinmesi atış dönüşümünü aynı karede değiştiremez. Son hasar birden fazla stage'i geçmiş olsa da hazırlık açıkça kapaklı çatlak sprite ile başlar.

Kapak ve yalnız gerçek görsel devralınamazsa kullanılan yedek gövde mevcut `UiVfxPool` üzerinden yeniden kullanılır; anahtar başına en fazla 32 görsel tutulur. Gerçek engel görünümü sade Image havuzuna konmaz. Kapak sprite'ı önbelleğe alınır. Buhar mevcut havuzu ve ortak update sürücüsünü, parçalar mevcut `WaterTankFx` yolunu kullanır. Yeni kare başı üretim, board taraması veya ayrı Update bileşeni eklenmedi. Mevcut atış başına yol/hedef listeleri ve coroutine tahsisleri devam eder; cihaz performansı ölçülmedi.

Atış ana coroutine'i gövde sonlanmasını da bekler. İptalde gövde/jel alt coroutine'leri durdurulup ardından görseller bırakılır; eski hareketin havuzdan yeniden kiralanan kapağı veya gövdeyi oynatması engellenir. Gövde hücre tutuşu ve board job'u normal bitiş/iptalde bırakılır. Jel rotası, kapak isabetlerinin hasar yolu ve iki hücrelik jel başlangıç gecikmesi korunur.

Doğrulama: `csi Tools/check_gel_launcher_motion.csx`, üretim coroutine'lerini UI/board taklitleriyle dört yönde 30/60/120 Hz'de çalıştırır; geri çekilme/şişme, çatlak sprite'ın korunması, kapağın başlangıç konumunu geçince ayrılması, gerçek ağız konumu, tekrar kullanım ve yarıda kesme temizliği denetlenir. Bu bir Unity oynatımı veya performans ölçümü değildir. Unity Play/build çalıştırılmadı.
