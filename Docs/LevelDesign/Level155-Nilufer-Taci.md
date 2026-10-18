# Level 155 — Nilüfer Tacı

`LevelCatalogPro` global 155 → `LevelP_001590.asset`, chapter 7. **9×11, 26 hamle, dört renk**, zor tasarım. [Önizleme ve taban katmanı](Level155-Nilufer-Taci.png).

Sekiz pembe nilüfer, ortadaki eşleşme alanının çevresinde simetrik taç oluşturur. Taş kanatlar başlığı çerçeveler; alttaki ahşap sandıklar daralan sapı tamamlar. Önceki 151’in düz iki sütun düzeni tekrarlanmaz.

```text
...ooo...
..MoooM..
.MMLoLMM.
MMLoooLMM
MoooooooM
MMLoooLMM
.MMLoLMM.
..MoooM..
..OOoOO..
...OoO...
....o....
```

`.` hole · `o` normal taş · `L` GrassWaterLily (69, çimiyle birlikte) · `M` altta Mud (25), üstte SculptingStone (28) · `O` altta Oil (14), üstte chest3 (6).

| Hedef | Başlangıç adedi | Gerekli ilerleme |
| --- | ---: | --- |
| GrassWaterLily | 8 | İçerisi boşken normal/special ile dört yaprak, sonra altındaki çim; yutulan special varsa yalnız special vuruşuyla geri atılır |
| Mud | 22 | Önce üç aşamalı taş örtüsü, ardından mevcut iki aşamalı çamur |
| Oil | 6 | Önce üç aşamalı sandık, sonra alttaki yağ |

## Katmanlar — alttan üste

- **22 hücre:** `Mud → SculptingStone`.
- **6 hücre:** `Oil → chest3`.
- Sıvılar doğrudan `obstacles[]` tabanındadır. 28 `stackedObstacles[]` girdisinin tamamı katıdır, `stackOrder=1`.
- Üst katmanda Mud/Oil yoktur. Sandığın üstünde Oil yoktur. Sıvılar aynı hücrede üst üste bindirilmez.
- Nilüferler başlangıçtan görünürdür; üstlerine örtü konmadı. Kendi çim tabanı ID 69’un mevcut birleşik yerleşiminden gelir, ayrıca Grass eklenmez.

Taşlar ve sandıklar ayrı hedef yapılmadı: bütün Mud/Oil hedeflerini bitirmek zaten örtülerin kaldırılmasını gerektirir. HUD üç hedef taşır. Yağ açıldıktan sonra mevcut yayılma kuralı çalışır; `oilSpreadMaxCells=6` aynı anda açıkta bulunan yağı sınırlar. Bu, toplam seviye boyunca üretilen yağı veya kapak altında saklı yağı altıyla sınırlama iddiası değildir. Yeni yayılan yağ mevcut dinamik hedef akışındadır.

## Zorluk ve açılış

- 26 hamle; hazır special, EggBird, sepet veya kartuş yardımı yoktur.
- Sekiz nilüfer toplam 32 yaprak ister. Bir special birden fazla çiçeğe ulaşabilir; aynı kaynaktan çok vuruş garantisi varsayılmaz. Çiçeğin special yutması, hedeflemenin zamanlamasını önemli kılar.
- Üç orta sütun üst gövdede special kurma alanı sağlar. Sol/sağ çiçeklere ve iki örtülü kanada aynı etkiyi ulaştıran çizgiler/combo’lar değerlidir.
- 27 açık normal hücrenin başlangıç rengi sabitlenmiştir. Otomatik üçlü veya aynı renk 2×2 yok; **13 geçerli açılış takası** vardır. En az bir açılış takası dört veya daha çok taş temizleyebilir; başlangıçta doğrudan special verilmez. Nilüfer altındaki taşlar ayrıca sabitlenmedi.
- 22 taş/çamur hücresi ve altı sandık/yağ hücresi aynı hamle bütçesini paylaşır. Zorluk yalnızca nilüfer adedini yükselterek kurulmadı.

## Statik doğrulama ve performans

63 aktif hücre, 36 hole; yedi dizi 99 eleman. Maske, nilüfer yerleri ve katı örtüler sağ-sol simetriktir. Bütün engeller/katmanlar 1×1, origin’leri kendi indeksidir. Maske bağlılığı, hiçbir hole’da içerik bulunmaması, bütün sıvıların tabanda oluşu, kapak eşleşmeleri, hedef sayıları, pinler, sprite’lar ve GUID/katalog bağlantısı kontrol edildi.

Sabit örtülerle açılış spawn erişimi statik düşüş/çapraz grafiğiyle kontrol edildi; normal 27 hücrenin tamamına yol vardır. Taş/sandık köşeleri mevcut `allowDiagonal=1` ayarını kullanır. Runtime veya ortak kütüphane ayarı değiştirilmedi.

Sekiz nilüfer mevcut servis, çiçek başına yeniden kullanılan uçan special ve 32 yuvalı yaprak havuzunu kullanır; mevcut teardown/OnDisable akışı korunur. Yeni coroutine, tarama veya VFX üretim kodu eklenmedi. Profiler ölçümü yapılmadı.

**26 hamle zor ilk ayardır; kazanılabilirlik veya kaç denemede geçileceği ölçülmedi.** Unity Play/build, bot veya oyun simülasyonu çalıştırılmadı. Önizleme oyunun ekran görüntüsü değildir; sağdaki küçük tahta gizli sıvı tabanını açıklamak içindir.

Nilüferin normal eşleşme hasarı kullanıcı isteğiyle açıldı. Bu global davranış düzeltmesinde 155’in hamlesi, hedef sayısı, sıvı/katı katmanları ve başlangıç dizilimi değiştirilmedi; oynanış zorluğu yeniden test edilmelidir.
