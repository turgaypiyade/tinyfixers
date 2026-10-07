# Level 119 — Mor Şevron (süper zor, v3)

`LevelCatalogPro` global 119 → `LevelP_001250.asset`. Tam 9×11 board (99 hücre), 32 hamle. Hedef geçiş oranı ~%10 (kullanıcı hamleyle ayarlar).

## Kompozisyon

Üç iç içe V (şevron), yeşil çiçek / gri metal / mor jel paleti.

```text
#ooooooo#
o#ooooo#o
oo#ooo#oo
Loo#o#ooR
oLoo#ooRo
ooLoooRoo
oooLoRooo
#oooLooo#
o#ogggo#o
oo#ooo#oo
ooo#o#ooo
```

- `#` (üst V, 9 hücre ve alt V, 8 hücre): **3 katman** — jel (46, `obstacles[]`) → tek hücrelik MetalWall (56, `stackedObstacles`, origin = hücre) → GrassFlower (58, `stackedObstacles`).
- `L` / `R` (orta V): **2 katman** — MetalWall taban (`obstacles[]`) + GrassFlower (`stackedObstacles`), altında jel yok. Sol kol + uç (5 hücre) tek parça, sağ kol (4 hücre) tek parça: parçanın bir hücresi ardışık 3 hamle vurulunca kolun tamamı yıkılır.
- Alt V bir satır aşağı kaydırıldı; ucu tahta dışında kaldığı için 8 hücre.
- `g` açık jel (3 hücre), `o` normal taş.

Önizleme: `Level119-Mor-Sevron.png`.

## Hedefler

99 hücre jel, 19 MetalWall parçası (17 tekli + 2 kol), 26 Grass.

## Revizyonlar

- v1: iki şevron, 18 yığın — ilk denemede 6 hamle kala geçildi.
- v2: üste üçüncü şevron, 27 yığın — çok zor bulundu.
- v3 (kullanıcı tarifi): orta V 2 katman ve 2 blok, alt V bir satır aşağı. Kolaylaştırılmış ara sürüm olan 121 kaldırıldı.

## Risk notu

- MetalWall'un `stackedObstacles` ile jel üstüne yığılması (üst/alt V) repoda ilk kullanım; oyunda vuruş sırası, jel açılışı ve sayaçlar kontrol edilmeli.
- Orta V kollarının hücreleri çapraz basamaklı (yan yana değil) ama ortak origin'li: duvar servisi parçayı origin'e göre topladığından mantık çalışmalı; görsel autotile ve editör uyarısı kontrol edilmeli.

## Statik kontrol

Diziler, 17 jel tabanı + 43 üst katman kaydı, iki kol parçasının origin'leri, hedef adetleri ve katalog bağlantısı (GUID korunarak) kontrol edildi. Hazır üçlü/2×2 yok, 9 geçerli takas. Unity, bot, build veya simülasyon çalıştırılmadı.
