# Level 116 — Kristal Kalp

`LevelCatalogPro` global 116 → `LevelP_001220.asset`. Kalp maskesi (68 aktif hücre), başlangıç ayarı 27 hamle. Çiçek/çim kullanılmadı.

## Kompozisyon

İki lobta 14 SculptingStone; hepsinin altında jel tohumu. Kalbin alt uç kenarlarında iki kısa MetalWall, aralarında iki EggBird; uçta 4 açık jel hücresi oyunun başlangıç jel kaynağıdır.

```text
..oo.oo..
.oSoooSo.
oSSSoSSSo
oSSSoSSSo
ooooooooo
ooooooooo
.ooooooo.
..1eoe2..
..1ooo2..
...ooo...
....o....
```

`.` hole, `o` normal taş, `S` SculptingStone (28, 3 vuruş) — tabanı SpreadingGel (46), `e` EggBird (45), `1`/`2` MetalWall (56) parçaları (2+2). Ayrıca `(3,9)`, `(4,9)`, `(5,9)`, `(4,10)` açık jel.

Önizleme: `Level116-Kristal-Kalp.png`.

## Hedefler ve kararlar

- 68 hücre jel (tüm aktif hücreler; engel hücreleri dahil), 14 SculptingStone, 2 MetalWall.
- Jel alttan yukarı taşınmalı; taşlar kırılınca altlarındaki tohumlar açılır, ikinci yayılma cephesi oluşur.
- EggBird'ler yardımcıdır, hedef değildir.
- Taslaktaki orta dikey duvar kaldırıldı (orta sütunu üstten kapatıyordu); yerine alt kenarlarda iki parçalı duvar var. Duvarların altı hole olduğu için akış kesilmez. Lob taşları üstteki sütunları kapatır; alt kalp ilk başta orta/kenar sütunlardan ve çaprazdan beslenir.

## Statik kontrol

Diziler, 31 hole, 18 jel tabanı + 14 taş üst katmanı, iki duvar parçası, hedef adetleri ve katalog bağlantısı kontrol edildi. 48 açık taş simetrik sabitlendi; hazır üçlü/2×2 yok, 9 geçerli takas. Unity, bot, build veya simülasyon çalıştırılmadı; 27 hamle ilk tahmin.
