# Level 133 — Roma Tapınağı

Asset: `LevelP_001380.asset` → `LevelCatalogPro` `level: 133` (chapter 6, `LevelCL_0133`). 9×11, 30 hamle, 4 renk.
Kullanıcı fikri: tüp + duvarla antik Roma yapısı. Hedef zorluk: normal-zor.

## Kompozisyon

Tapınak cephesi: hole silüetiyle üçgen alınlık (kiremit rengi Mud çerçeve + saçak), düz friz satırı, dört sütun
(Tube), altta iki kademeli Wall basamak.

```text
    M
   MoM
  MoooM
 MMMMMMM
ooooooooo
oToToToTo
oToToToTo
oToToToTo
oToToToTo
 WWWWWWW
WWWWWWWWW
```

`M` Mud (25) ×12 · `T` Tube (26): taban y=8, yukarı, 4 hücre; origin 73/75/77/79 · `W` Wall (55): üst basamak tek
parça (origin 82, 7 hücre), alt basamak 3 parça (origin 90/93/96) · ` ` hole.

Hedefler: Tube 4 · Mud 12 · Wall 4.

Önizleme: `Level133-Roma-Tapinagi.png`.

## Oynanış

- Alınlık engel değil zemin (Mud taşın altında): akışı kesmez; alınlığın üstü hole olduğundan dolum hole'dan geçer.
- Sütun aralıkları tek sütun: orada yalnız dikey eşleşme var; her dikey eşleşme iki yandaki tüpü birden vurur.
  Tüp her vuruşta açık ucundan (üstten) kısalır. Special'lar (özellikle yatay roket) sütunları hızla eritir.
- Basamaklar katmanlı: üst basamak (tek parça) yıkılmadan alt basamağa eşleşme ulaşmaz; alt basamağın kenarları
  hole altında. Duvar hücresi 3. vuruşta tüm parçayı yıkar.
- Blokajlar (tüp, duvar) alt bölgede: altlarında beslenecek hücre yok.

Yük: tüp 16 + çamur 24 (2 katman) + duvar ≥12 vuruş. Hamle ilk tahmindir; Unity/bot/simülasyon çalıştırılmadı.
