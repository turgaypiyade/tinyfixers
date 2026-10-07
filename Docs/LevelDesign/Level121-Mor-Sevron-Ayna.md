# Level 121 — Mor Şevron (ayna çiftleri)

`LevelCatalogPro` global 121 → `LevelP_001260.asset` (120 Boss Duel için boş). 119'un kolaylaştırılmış kardeşi; kullanıcı ikisini birlikte deneyip sırayı belirleyecek. Tam 9×11, 32 hamle.

## 119'dan farkı

Aynı üç şevron (27 hücre), ama:

- MetalWall **taban** engel (`obstacles[]`), katman değil. Her satırdaki sol/sağ ayna hücreleri **tek parça** (ortak origin + aynı `wallPieceIds`): biri yıkılınca eşi de yıkılır. V uç hücreleri tek başına. 27 hücre → **15 parça**.
- Duvarların üstünde GrassFlower (`stackedObstacles`, 106'daki kanıtlı yol).
- Duvarların altında **jel tohumu yok** (jel yalnız en alta konabildiği için taban duvarla birlikte olamaz). Başlangıç jeli yine alt ortadaki 3 açık hücre.

```text
1ooooooo1
o1ooooo1o
oo1ooo1oo
1oo1o1oo1
o1oo1oo1o
oo1ooo1oo
1oo1o1oo1
o1oo1oo1o
oo1ggg1oo
ooo1o1ooo
oooo1oooo
```

`1` = MetalWall (56) + üstte GrassFlower (58); aynı satırdaki iki `1` aynı parça. `g` açık jel, `o` normal taş.

Önizleme: `Level121-Mor-Sevron-Ayna.png`.

## Hedefler

99 hücre jel, 15 MetalWall parçası, 27 Grass. Ardışık vuruş kuralı parça başına geçerli.

## Risk notu

Parça hücreleri yan yana değil (aynı satırın iki ucu). Duvar servisi parçaları origin'e göre topladığı için mantık çalışmalı; görsel autotile her hücreyi ayrı kutu gibi çizebilir ve editör bitişik olmayan parçayı uyarabilir. Oyunda kontrol edilmeli.

## Statik kontrol

Diziler, 15 parça/origin eşleşmesi, 27 çiçek katmanı, hedef adetleri ve katalog bağlantısı kontrol edildi. Hazır üçlü/2×2 yok, 9 geçerli takas. Unity, bot, build veya simülasyon çalıştırılmadı.
