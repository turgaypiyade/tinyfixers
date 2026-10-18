# Level 131 — Gözcü Adaları (Lazer Topu tanıtımı)

Asset: `LevelP_001360.asset` → `LevelCatalogPro` `level: 131` (chapter 6, `LevelCL_0131`). 9×11, 26 hamle, 4 renk.
Hedef zorluk: normal (yeni engel tanıtımı).

## Kompozisyon

Soyut ada düzeni (128–130 şekildi; sıra soyut desende). Üstte basamaklı kule, ortada hole penceresiyle ayrılan iki kanat,
alt köşelerde iki Lazer Topu tahtayı gözetler. Renk: mavi/turuncu top + turuncu fener + kahve çamur (tamamlayıcı ikili),
yeşil çim üstte vurgu.

```text
  ooooo
 ooooooo
ooggoggoo
ooggoggoo
ooooooooo
o  ooo  o
ZZZo oZZZ
ZZZo oZZZ
MMoooooMM
KKoooooKK
KKoooooKK
```

`o` taş, ` ` hole, `g` Grass (42, 8), `Z` PlasticTwoStage (40) Mud (25) üstünde — 2×(3×2) grup, `M` Mud,
`K` Lazer Topu (65) 2×2 ×2 (origin 81 ve 88).

Hedefler: PlasticTwoStage 12 · Mud 16 · Grass 8. Lazer topu hedef değil.

Önizleme: `Level131-Gozcu-Adalari.png`.

## Oynanış

- Toplar alt kenarda: altlarında hücre yok, dolumu kesmez. Kule tüm tahtayı tarar.
- Oyuncu special üretince en yakın top ona kilitlenir; bir hamle içinde kullanılmazsa kafeslenir. Ders: special'ı
  biriktirme, hemen kullan — ya da kafeslinin yanında eşleşme yap / başka special'la kurtar.
- Fenerler çamurun üstünde (tercih edilen örtü); fener düşüp çamuru açar. Kanatlar hole penceresiyle ortadan ayrılır,
  hole'lar dolum için geçirgen.
- Toplar 4 special vuruşunda kırılır; PatchBot onları hedef gibi arar.

Hamle ilk tahmindir; Unity, bot veya simülasyon çalıştırılmadı (sim lazer topunu modellemiyor).
