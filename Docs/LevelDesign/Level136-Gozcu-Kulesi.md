# Level 136 — Gözcü Kulesi (oda bölmeli, L135 düşüncesiyle)

Asset: `LevelP_001410.asset` → `LevelCatalogPro` `level: 136` (chapter 6, `LevelCL_0136`). 9×10, 35 hamle, 4 renk.

```text
KK oooooo
KK bbbbbo
qq cbbbbo
qq ccbbbo
qq cccbbo
qq ccccbo
qq ccccco
qq
qq gggggg
qq gggggg
```

- **Bölme:** x=2 tam sütun hole + y=7 satırı (x=3–8) hole → üç oda: sol kopuk şerit 2×10, ana oda 6×7, alt oda 6×2.
- **Sol kopuk şerit (yalnız special ulaşır):** üstte Lazer Topu (65, 2×2, hedef değil — oradan oyun alanındaki
  special'lara nişan alır, roketle vurulur), altında SculptingSpecial (39) ×16.
- **Ana oda:** L bant dolum — üst satır + sağ sütun taş (spawn köşesi sağ üst). Kalan 5×6 blok çapraz gradyan:
  besleme köşesine yakın üçgen chest2 (5) ×15, uzak üçgen chest3 (6) ×15. Blok bütün, kanal yok.
- **Alt oda:** Grass (42) ×12 — taş üstü örtü. Hole satırı dolum için geçirgen: ana odanın sütunları açıldıkça
  alt odaya taş yağar; başta dolu, sonra açılan sütunlardan beslenir.

Hedefler: chest2 15 · chest3 15 · SculptingSpecial 16 · Grass 12. Önizleme: `Level136-Gozcu-Kulesi.png`.

135'ten farkı: ayna yön (dolum köşesi sağ üst, kopuk oda solda), kopuk odada tehdit (Lazer Topu), üçüncü oda
tek tür örtü (çim) ve hole satırının altında.
Hamle ilk tahmindir; Unity/bot/simülasyon çalıştırılmadı.
