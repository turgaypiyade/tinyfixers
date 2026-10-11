# Level 132 — Deney Tüpü Rafı

Asset: `LevelP_001370.asset` → `LevelCatalogPro` `level: 132` (chapter 6, `LevelCL_0132`). 9×11, 30 hamle, 4 renk.
Hedef zorluk: normal-zor. Tube (son 100) ve Magnet (son 83) uzun aradan sonra geri dönüyor.

## Kompozisyon

Laboratuvar rafı: üç cam tüp (yan ikisi 5, orta 4 hücre) dikilmiş, altta tam satır mıknatıs, mıknatıs uçlarını
ve tüplerin arasını kapatan heykel taşları; üstte basamaklı tepe ve çim kuşağı. Renk: mavi tüp + mor mıknatıs
(laboratuvar ailesi), kum rengi taş ve yeşil çim sıcak vurgu.

```text
  ooooo
 ooooooo
ooooooooo
oogooogoo
ogggogggo
oTo o oTo
oTo T oTo
oTooTooTo
sTsoTosTs
sTsoTosTs
mmmmmmmmm
```

`T` Tube (26): taban y=9, yukarı (`direction 0`); origin 82 (5), 85 (4), 88 (5). `m` Magnet (29): yol 90→98
(A sol uç, B sağ uç). `s` SculptingStone (28, 3 vuruş) ×8. `g` Grass (42) ×8. ` ` hole.

Hedefler: Tube 3 · Magnet 1 · SculptingStone 8 · Grass 8.

Önizleme: `Level132-Deney-Tupu-Rafi.png`.

## Oynanış

- Tüpler açık uçtan (üstten) her vuruşta bir hücre kısalır; yanındaki eşleşme ya da special vurur.
- Mıknatıs yalnız uçlarından küçülür. Uçların (0,10)/(8,10) tek komşusu taş (0,9)/(8,9): önce taşlar kırılmalı
  (katmanlı açılış) — ya da special ile doğrudan.
- Mıknatıs yanında bekleyen special'ı kafesler: alt kısımda special biriktirme.
- Orta tüpün iki yanındaki hole pencereleri altında ada hücreleri; hole'lar dolum için geçirgen.
- Taşlar alt kenarda: altlarında taş hücresi yok (mıknatıs satırı), dolumu kesmez.

Yük: taş 24 + tüp 14 + mıknatıs ~8 + çim 8 vuruş. Hamle ilk tahmindir; Unity/bot/simülasyon çalıştırılmadı.
