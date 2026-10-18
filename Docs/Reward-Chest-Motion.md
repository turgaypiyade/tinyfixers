# Ödül sandığı açılışları

`RewardChestMotion` bütün açılan ödül sandıklarına ortak hazırlık verir: iki nefeste genişleme/gevşeme, ters yönlerde giderek hızlanan sarsılma, son sıkışma ve ardından mevcut açılma patlaması.

- `RewardChestRevealOverlay`: köprü ödülleri, hasat finali, harika, dünya haritası/ada ve level sonu ilerleme etkinliği paketleri; yeni dönüş paketi de aynı yolu kullanır. Sprite değişimi, flaş, ses ve ödül uçuşları hazırlıktan sonra çalışır.
- `RepairProgressBar`: atölyenin küçük ödül sandığı, aynı hazırlıktan sonra açılıp mevcut pop hareketini yapar.
- `RegionProgressBar`: bölge çubuğunun sandığı, aynı hazırlıktan sonra açılır.

Safari/tırmanış finali para sayacı, GearCup sonuç makbuzu, satın alma kutlaması, çark/slot ödülleri ve nasıl-oynanır içindeki sabit sandık ikonları ayrıca incelendi; bunlar açılan sandık animasyonu kullanmıyor. Oyun tahtasındaki chest engel VFX'i ödül töreni değildir.

Hareket `Time.unscaledDeltaTime` kullanır; popup oyunu duraklatsa da sürer. Yerel ölçek/rotasyon korunur; küçük bar sandıklarında yol genişliğe oranlanır. Atölye ve bölge paneli kapanınca coroutine'ler durur ve sandık eski konum/ölçeğine döner. Çift tetikleme sırasında yeni sandık hareketi başlatılmaz.

Yeni frame başına nesne, liste, delegate veya efekt üretimi yok; tek coroutine mevcut RectTransform'u değiştirir. Ortak ödül overlay'inin mevcut öğe üretimi değişmedi. `csi Tools/check_reward_chest_motion.csx` hareket yönlerini, boyut değişimini, hızlanmayı ve yarıda kesme temizliğini üretim kaynağıyla denetler. Unity'de görsel oynatım ve cihaz performansı ölçümü yapılmadı. Play Mode'da mevcut **TinyFixers → Debug → Test Chest Reveal (Play Mode)** menüsüyle önizlenebilir.
