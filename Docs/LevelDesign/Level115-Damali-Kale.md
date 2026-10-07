# Level 115 — Damalı Kale (zor)

`LevelCatalogPro` global 115 → `LevelP_001210.asset`. Zor normal level, tam 9×11 board, başlangıç ayarı 25 hamle. Çiçek/çim kullanılmadı.

## Kompozisyon

Ortada 7×5 dama: baykuş (HelmetPorcelain) ve turuncu (PlasticTwoStage) kareler; hepsinin altında iki katmanlı çamur. Yanlarda boydan boya MetalWall kuleler, altta orta sütunda kısa MetalWall; sütunun iki yanında RocketBasket.

```text
ooooooooo
ooooooooo
ooooooooo
1HTHTHTH2
1THTHTHT2
1HTHTHTH2
1THTHTHT2
1HTHTHTH2
1ooooooo2
1ooo3ooo2
1ook3koo2
```

`o` normal taş, `H` HelmetPorcelain (30), `T` PlasticTwoStage (40), `k` RocketBasket (38), `1`/`2`/`3` MetalWall (56) parçaları (8, 8, 2 hücre). Dama hücrelerinde taban `obstacles[] = Mud (25)`, üst katman `stackedObstacles` kaydıdır (hareketli engel çamur üstünde).

Önizleme: `Level115-Damali-Kale.png` (yerleşim şeması; çamur hücre zemininde gösterilir).

## Hedefler ve zorluk

- 3 MetalWall, 18 HelmetPorcelain, 17 PlasticTwoStage, 35 Mud. Hücre başı 3–4 vuruş.
- Dama yalnız üst ve alt sıradan açılır; hareketli engeller açıldıkça düşer, desen bozulur.
- RocketBasket'ler hedef değildir; kale altından yardım verir. Duvarı/sepeti alta koymak üstten taş akışını açık bırakır.

## Statik kontrol

Diziler, 3 duvar parçası/origin, 35 çamur tabanı + 35 üst katman kaydı, hedef adetleri ve katalog bağlantısı kontrol edildi. 44 açık taş sağ-sol simetrik sabitlendi; hazır üçlü/2×2 yok, 10 geçerli takas. Unity, bot, build veya simülasyon çalıştırılmadı; 25 hamle ilk tahmin.
