# Level 140 — Kum Saati (Boss Duel)

Asset: `LevelP_001445.asset` (guid 2952692a…) → `LevelCatalogPro` `level: 140` (chapter 6, `LevelCL_0140`).
141 hâlâ `LevelP_001450` (Brandalı Heykel tanıtımı). Boss, 9×11, **18 hole** (kum saati silüeti), 30 hamle, 4 renk.

```text
ooooooooo
ooooooooo
ommmmmmmo
.mmmmmmm.
..mmmmm..
...mmm...
..oomoo..
.oomsmoo.
oomsssmoo
KKsssssKK
KKSSSSSKK
```

`m` Mud (25) zemin — üst haznedeki kum + boğazdan akan ince kum çizgisi (dekor; hedef değil) · `s` SculptingStone (28)
×9 — alt haznede kum yığını piramidi · `S` PlayerShieldPickup (35) ×5 (+10 koruma) yığının altında, yalnız yığın
kenardan aşındırılınca ya da special'la alınır · `K` Lazer Topu (65, 2×2) ×2, alt köşeler (origin 81, 88) · ` ` hole.

Akış: boğaz 3 hücre; yan sütunlar hole'lardan geçerek alt hazneye düşer (ölü hücre yok — yalnız kalkan satırı
yığın açılana dek kapalı, bilerek).

| | 130 | **140** |
|---|---|---|
| Rakip canı | 460 (3) | **500 (3): 150/165/185** |
| Oyuncu canı | 220 | **230** |
| Hasar | 7/9/10 | **7/9/10** |
| Kalkan | +18 (mühür altında) | **+10 (kum yığını altında)** |
| Fırlatma | 4'te 2, tavan 7 | **4'te 2, tavan 8: Oil, Plastic_Blue, Plastic_Red, Helmet** |
| Ek zorluk | çim haç | **dar boğaz + 2 Lazer Topu (special'ları kafesler)** |

Denge (hamle başı ortalama hasar → toplam alınan hasar; oyuncunun etkin canı 240):
18 → ~244 (kaybeder) · 19 → ~230 (kıl payı) · 20 → ~219 (kazanır). 130'da 18 taş/hamle kazanıyordu;
140 hamle başına ~1 taş daha ister → "birkaç denemede geçilir" hedefi. Hamle sınırı (500/30 ≈ 17) bağlayıcı değil;
asıl sınır oyuncu canı.

Havuzdaki türlerden board'da yok (tavan tuzağı yok). Unity/bot/sim çalıştırılmadı. Önizleme: `Level140-Kum-Saati.png`.
