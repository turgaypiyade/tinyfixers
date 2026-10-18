# Level 115 — Damalı Kale (zor, v2)

`LevelCatalogPro` global 115 → `LevelP_001210.asset`. Zor normal level, tam 9×11 board, başlangıç ayarı 24 hamle. Çiçek/çim kullanılmadı.

## Kompozisyon

Ortada 5×5 dama: baykuş (HelmetPorcelain) ve turuncu (PlasticTwoStage) kareler; hepsinin altında iki katmanlı çamur. İki yanda **2 sütun genişliğinde, parçalı** MetalWall kuleleri (her yanda 2×2 + 2×1 + 2×2). Kulelerin üst ucu ve altı SculptingStone ile kapalı.

```text
ooooooooo
ooooooooo
SSoooooSS
11THTHT44
11HTHTH44
22THTHT55
33HTHTH66
33THTHT66
SSoooooSS
SSoooooSS
SSoooooSS
```

`o` normal taş, `H` HelmetPorcelain (30), `T` PlasticTwoStage (40) — ikisinin tabanı Mud (25), `S` SculptingStone (28, 3 vuruş, hedef değil), `1`–`6` MetalWall (56) parçaları.

Önizleme: `Level115-Damali-Kale.png`.

## Hedefler ve zorluk

- 6 MetalWall, 12 HelmetPorcelain, 13 PlasticTwoStage, 25 Mud (4 hedef; HUD 4 sütun).
- MetalWall'a başta yalnız kule uçlarındaki taş kalkanları kırarak ya da damayı yanlardan açarak ulaşılır. Ardışık vuruş kuralı nedeniyle her parçaya üst üste hamlelerde odaklanmak gerekir; parçalı yapı her parçayı ayrı iş yapar.
- Yardımcı yok.

## Revizyon

v1 (7×5 dama, tek parça 1 sütunluk kuleler, 2 RocketBasket) kullanıcıya kolay geldi: duvar alt boşluktan kolay kırıldı. v2'de kuleler 2 sütun ve parçalı, uçları taşla korunuyor, roketler kaldırıldı.

## Statik kontrol

Diziler, 6 duvar parçası/origin, 25 çamur tabanı + 25 üst katman kaydı, hedef adetleri ve katalog bağlantısı (GUID korunarak) kontrol edildi. 38 açık taş sağ-sol simetrik ve bölgesel dengeli sabitlendi; hazır üçlü/2×2 yok, 9 geçerli takas (üst ve alt). Unity, bot, build veya simülasyon çalıştırılmadı.
