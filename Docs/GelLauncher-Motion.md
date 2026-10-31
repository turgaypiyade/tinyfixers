# GelCabinet / jel kartuşu kırılma hareketi

`GelLauncherFireAction`, dört yönde aynı yerel hareket sırasını kullanır:

1. **0,34 saniye hazırlık:** kapaklı çatlak sprite korunur. Gövdenin merkezi atış yönünün tersine 0,28 hücre gider; boyu %18 kısalırken eni %24 genişler.
2. **0,075 saniye ileri atılma:** gövde hızlıca başlangıç merkezinin 0,14 hücre ilerisine geçer. Boyu %108, eni %92 olur.
3. **Kapak ayrılması:** aynı karede kapaksız sprite'a geçilir ve kapak deforme olmuş gerçek ağız konumundan çıkar. Ayrı bir kapak büyütme/bekleme evresi yoktur; ilk hedef hücreye hareket 0,03 saniyedir. Gövde 0,035 saniye daha ileri devam edip 0,24 hücreye ulaşır, sonra geri seker.

Gerçek engel görseli devralındığında bekleme animasyonu hemen durdurulur; gecikmeli bileşen silinmesi atış dönüşümünü aynı karede değiştiremez. Son hasar birden fazla stage'i geçmiş olsa da hazırlık açıkça kapaklı çatlak sprite ile başlar.

Kapak ve yalnız gerçek görsel devralınamazsa kullanılan yedek gövde mevcut `UiVfxPool` üzerinden yeniden kullanılır; anahtar başına en fazla 32 görsel tutulur. Gerçek engel görünümü sade Image havuzuna konmaz. Kapak sprite'ı önbelleğe alınır. Buhar mevcut havuzu ve ortak update sürücüsünü, parçalar mevcut `WaterTankFx` yolunu kullanır. Yeni kare başı üretim, board taraması veya ayrı Update bileşeni eklenmedi. Mevcut atış başına yol/hedef listeleri ve coroutine tahsisleri devam eder; cihaz performansı ölçülmedi.

Atış ana coroutine'i gövde sonlanmasını da bekler. İptalde gövde/jel alt coroutine'leri durdurulup ardından görseller bırakılır; eski hareketin havuzdan yeniden kiralanan kapağı veya gövdeyi oynatması engellenir. Gövde/yol hücre tutuşu ve board job'u normal bitiş/iptalde bırakılır.

2026-10-31: Kapak isabetleri artık Lift jokeriyle aynı `BoosterCellImpactPass` üzerinden, kapak hücreye vardığı anda uygulanır. Hücre başına ayrı action kuyruğu ve engeli altı kez vurma döngüsü kaldırıldı. Engel kendi booster/fallback kuralına göre bir isabet alır; çok hücreli engeller sweep başına origin ile, duvar/magnet gibi engeller kendi hücre kurallarıyla sayılır. Örtüsü açılan taş aynı isabetle tüketilmez; açık special kendi etkisini tetikler. Yol, Lift'teki gibi atış boyunca tutulur. Yol hücresine jel kapak varınca ve hücre açılmışsa bırakılır; kartuşun kendi iki hücresindeki jel animasyonunun iki hücrelik başlangıç gecikmesi korunur.

Doğrulama: `csi Tools/check_gel_launcher_motion.csx`, güncel gerçek pres/gövde devrini UI/board taklitleriyle dört yönde 30/60/120 Hz'de çalıştırır; sıkışma bitmeden kapağın çıkmaması, gerçek pres kafası, isabet/jel sırası, gövde/yol tutuşu, tekrar kullanım ve iptal temizliğini denetler. `csi Tools/check_lift_gel_impacts.csx` ortak isabet kurallarını ayrıca çalıştırır. Bunlar Unity oynatımı veya cihaz performans ölçümü değildir. Unity Play/build çalıştırılmadı.
