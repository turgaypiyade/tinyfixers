# Level 182 — Fener Şenliği

Katalog 182 → `LevelP_001860.asset`, chapter 7. **9×11, 81 aktif / 18 hole, 28 hamle, dört renk, Normal.** Kullanıcının yeni Lantern/fener ile 182 tasarlama isteği üzerine hazırlandı; henüz onaylanmadı. [Görsel önizleme](Level182-Fener-Senligi.png).

```text
..ooooo..
.ooooooo.
ooooooooo
ooooooooo
LLLLLLLLL
LHLHLHLHL
HLHLHLHLH
.LHLHLHL.
.HLHLHLH.
..LHLHL..
..HLHLH..
```

`o` otomatik normal taş; `.` hole; `L` yeni **Lantern · 75**; `H` **HelmetPorcelain · 30**. Bütün engeller 1×1 ve hareketlidir. Desen açılış kompozisyonudur; kırılma ve gravity ile değişir.

Üstte 30 normal taşlık çalışma alanı var. İlk engel sırası dokuz kesintisiz fener; altındaki dama alanında lacivert/altın fenerler ile krem porselenler dönüşümlü. Alt gövde iki sıra arayla 9→7→5 sütuna daralıyor; son iki sıranın beş hücre genişliği kalıyor. Uçta tek hücreye sıkışmış bir bitiş hedefi yok. Şekil kenara yaslanan iki kanat veya 181'in çapraz boşluk düzenini tekrarlamıyor.

| Hedef | Adet | İlerleme |
| --- | ---: | --- |
| Lantern · 75 | **30** | İlk hit sönük feneri yakar; ikinci hit feneri toplar |
| HelmetPorcelain · 30 | **21** | Mevcut tek vuruşlu hareketli porselen |

Yeni fener ilk production kullanımını burada yapıyor. Kendi renk eşleşmesini şart koşmuyor; iki aşaması mevcut generic movable sistemiyle çalışıyor. 40 PlasticTwoStage ile aynı temel iki-vuruş/hareket davranışı; yeni bir ışın/yayılma mekaniği varmış gibi sunulmaz. `goalIconSprite` yanan fenerdir, iki stage sprite'ı mevcut library'den kullanılır. Hint 75 kaydı ve lokalizasyonu mevcut; bunlar bu tasarım turunda değiştirilmedi.

Oyuncu üstte special üreterek fener sırasını önce yakar, sonra açar. Porselenler tek vuruşta boşluk sağlayarak dama alanının açılmasını hızlandırabilir; alttaki fenerler de iki hit istediği için hedef işi ilk şerit kalkınca bitmez. Yeni engel tanıtımında ek çamur, metal geri dönüşü, renk kilidi veya yardımcı yağmuru eklenmedi. Normal renkler rastgele; pin/hazır special yok. ColorCrate 71–74, sandık, dolap ve sıvı katman kullanılmadı.

Lantern hedefi başlangıçtaki **30**, porselen hedefi başlangıçtaki **21** adede eşit. Ek refill toplama kotası tasarlanmadı. Mevcut `TryPickMovableGoalToSpawn` kalan hedef ile yaşayan/gömülü engel ve planlanmış spawn sayılarını karşılaştırır. Lantern genel movable refill yolundadır; PlasticTwoStage ID 40'a özel hamle başına spawn sınırı ID 75'e uygulanmaz. Bu levelin hedefleri başlangıç sayısının üstüne çıkarılmadı; gelecekte artırılırsa bu fark yeniden değerlendirilmelidir.

**28 hamle ilk ayardır.** Yeni engel tanıtımı, 181'e eklenen çok katmanlı yükün ardından daha okunur bir hedef düzeniyle gelir. 170 sonrası normalde %40–50 yönü korunur; bu level için geçiş oranı ölçülmedi. Hard/Super Hard sınıfı verilmedi.

Tekrar incelemesi: 1–181 assetleri tarandı; 123'ün açık üst/yoğun alt alanı ve 115'in iki aileli daması yakındır. Kullanıcının [03 yoğun dama referansı](References/03-Yogun-Dama-Alani.png) da bilinçli kaynak oldu. 115'in metal kule/çamur yükü burada yok; 123'ün tam dikdörtgen kasa alanı yerine basamaklı daralan, tamamen hareketli alt alan var. Bu motifin ilk kez kullanıldığı iddia edilmez. Kayıt ve şematik karşılaştırma `Batches/182/repetition-review.json` / `repetition-comparison.png` içindedir; 115'in gerçek sprite'lı eski PNG'si ve dış referans da incelendi.

Statik kontrol geçti: yedi veri dizisi, maske, bütün 51 tek hücreli origin/footprint, hedefler, ilk Lantern tanıtım izni, sprite/hint bağlantıları, katalog/GUID ve isim. **30 açılış taş hücresinin tamamı** doğrudan üstten dolum alır; maskede tek aktif bileşen, köşeden tek başına temas eden mask 5/10 düğümü yok. Hole'lar normal hücre sayılmaz; dikey spawn geçişi değerlendirilir. Önceki **1.861 ayar/script dosyası**, 181 dahil geçmiş levellar, yeni Lantern library ayarları ve mevcut kullanıcı kod değişiklikleri korundu. Katalog yalnız 182 eklenerek uzatıldı; 1–181 isimleri korundu. Mevcut sprite'larla önizleme incelendi; normal renkler temsilidir.

Performans incelemesi: yeni runtime/VFX kodu, update döngüsü veya tarama eklenmedi. `useTilePool` sahnede açık; mevcut `AcquireTileObject/ReleaseTile` yolları kullanılır. `TileView.Init/OnDisable` hareket token'larını, aktif settle'ı ve görsel durumu sıfırlar; level kurulumunda `ClearTilePool` eski referansları temizler. Genel kırılma FX havuzu kullanım anahtarı başına yeniden kullanır, normal lifetime sonunda parçacıkları durdurup objeyi geri alır; kapasite 24/key. Mevcut spawn hedef taraması ve `CountAliveOrStampedOrigins` board taramaları sürer; Lantern 75 için ayrı servis/nesne ağı eklenmedi. Genel `GoalFlyFx` uçuşlarında ghost GameObject/Image/Shadow tahsisleri ve normal uçuş sonunda Destroy vardır; bütün uçuşların havuzlandığı veya iptal yolunun kusursuz olduğu iddia edilmez. Scene teardown kök/çocuk ömrüne dayanır; cihaz profili veya Play doğrulaması yapılmadı. Bu tasarımda performans artışı iddia edilmez.

Authoring, eski katalog/isim snapshot'ı, korunan dosya hash'leri ve doğrulama raporu `Batches/182/` içindedir. Unity Play/build ve oyun/bot/kaskat simülasyonu çalıştırılmadı. 183 başlatılmadı.
