# Level 119 — Mor Şevron (süper zor, v4)

`LevelCatalogPro` global 119 → `LevelP_001250.asset`. Tam 9×11 board (99 hücre), 32 hamle. Hedef geçiş oranı ~%10 (kullanıcı hamleyle ayarlar).

## Kompozisyon

Üç iç içe V (şevron), yeşil çiçek / gri metal / mor jel paleti.

```text
#ooooooo#
o#ooooo#o
oo#ooo#oo
Doo#o#ooD
Dooo#oooD
ooDoooDoo
ooDoooDoo
#oooDooo#
o#ogDgo#o
oo#ogo#oo
ooo#o#ooo
```

- `#` (üst V, 9 hücre ve alt V, 8 hücre): **3 katman** — jel (46, `obstacles[]`) → tek hücrelik MetalWall (56, `stackedObstacles`, origin = hücre) → GrassFlower (58, `stackedObstacles`).
- `D` (orta V): beş adet **2 hücrelik dikey MetalWall** (taban, `obstacles[]`; her ikili ortak origin) + üstte GrassFlower; altında jel yok. Basamak atlanarak (sütun 0-2-4-6-8) yerleştirildi: bitişik merdiven duvar, V'nin altına taş akışını tamamen kesip hücreleri boş bırakıyordu.
- Alt V bir satır aşağı kaydırıldı; ucu tahta dışında kaldığı için 8 hücre.
- `g` açık jel (3 hücre: (3,8), (5,8), (4,9)), `o` normal taş.

Önizleme: `Level119-Mor-Sevron.png`.

## Hedefler

99 hücre jel, 22 MetalWall parçası (17 tekli + 5 ikili), 27 Grass.

## Revizyonlar

- v1: iki şevron, 18 yığın — ilk denemede 6 hamle kala geçildi.
- v2: üste üçüncü şevron, 27 yığın — çok zor bulundu.
- v3: orta V 2 kol bloğu (çapraz hücreli parçalar) — duvar bağlantı görseli bozuk göründü. Kolaylaştırılmış ara sürüm 121 kaldırıldı.
- v4 (kullanıcı tarifi): orta V her duvarı 2 hücreden oluşan dikey ikililer; alt V bir satır aşağıda.

## Risk notu

- MetalWall'un `stackedObstacles` ile jel üstüne yığılması (üst/alt V) repoda ilk kullanım; oyunda vuruş sırası, jel açılışı ve sayaçlar kontrol edilmeli.

## Statik kontrol

Diziler, 17 jel tabanı + 44 üst katman kaydı, beş ikili duvarın origin'leri, hedef adetleri ve katalog bağlantısı (GUID korunarak) kontrol edildi. Hazır üçlü/2×2 yok, 9 geçerli takas. Unity, bot, build veya simülasyon çalıştırılmadı.
