# Level 119 — Mor Şevron (süper zor)

`LevelCatalogPro` global 119 → `LevelP_001250.asset`. Tam 9×11 board (99 hücre), başlangıç ayarı 32 hamle. Hedef geçiş oranı ~%10 (kullanıcı hamleyle ayarlayacak).

## Kompozisyon

İki iç içe V (şevron). Şevron hücrelerinin her biri **3 katmanlı yığın**: en altta jel tohumu, üstünde tek hücrelik MetalWall, en üstte GrassFlower. Başlangıçta yalnız alt ortadaki 3 hücrede açık jel var. Renk paleti sade: yeşil çiçek, gri metal, mor jel.

```text
ooooooooo
ooooooooo
ooooooooo
#ooooooo#
o#ooooo#o
oo#ooo#oo
#oo#o#oo#
o#oo#oo#o
oo#ggg#oo
ooo#o#ooo
oooo#oooo
```

`#` = jel (46, `obstacles[]`) → MetalWall (56, `stackedObstacles`, 1×1 parça, origin = hücre) → GrassFlower (58, `stackedObstacles`). Tüm duvar kayıtları çiçek kayıtlarından önce sıralanır (`stackOrder` 1–18 duvar, 19–36 çiçek). `g` = açık jel üstünde normal taş, `o` = normal taş.

Önizleme: `Level119-Mor-Sevron.png` (çiçek örtüsü altındaki metal ve jel şemada görünmez; oyunda çim saydamdır).

## Hedefler ve zorluk

- 99 hücre jel (tüm board): açık kaynak yalnız 3 hücre; şevron tohumları ancak çiçek, çim ve metal duvar kırılınca açılır ve ikinci yayılma cepheleri oluşur.
- 18 MetalWall: tek hücreli parçalar; ardışık vuruş kuralı nedeniyle her biri 3 hamle üst üste vurulmalı.
- 18 Grass (GrassFlower): çiçek + çim.
- Yardımcı yok. Kapalı halka kullanılmadı: V şekli, her sütunun çapraz akışla beslenmesine izin verir.

## Risk notu

Repo'da MetalWall'un `stackedObstacles` üzerinden jel üstüne yığıldığı başka level yok. Kod incelemesi (GridSpawner.StampStackedEntry, WallObstacleService.TryGetVisibleWallLayer, ObstacleStack_Plan) bu yığını destekliyor görünüyor; oyunda kontrol edilmeli: duvar aşamaları, çiçek→çim→duvar vuruş sırası, duvar yıkılınca jel tohumunun açılması, hedef sayaçları.

## Statik kontrol

Diziler, 18 jel tabanı + 36 üst katman kaydı, hedef adetleri ve katalog bağlantısı kontrol edildi. 78 açık taş simetrik sabitlendi; hazır üçlü/2×2 yok, 10 geçerli takas. Unity, bot, build veya simülasyon çalıştırılmadı.
