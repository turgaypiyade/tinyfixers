# Level 135 — Bölmeli Pencere (kullanıcı tasarımı, örnek level)

Asset: `LevelP_001400.asset` → `LevelCatalogPro` `level: 135`. 9×9, 35 hamle, 4 renk. Kullanıcı son hâli verdi
(2026-10-11) ve **"hole ile anlamlı grid" için örnek** olarak işaretledi. Önizleme: `Level135.png`.

```text
oooooo qq
oaaaac qq
oaaacc qq
oaaccc qq
oacccc qq
occccc qq

qqqqqq DD
qqqqqq DD
```

`a` chest1 ×10 · `c` chest3 ×15 · `q` SculptingSpecial ×24 · `D` PaintCanBox 2×2.
Hedefler: chest1 10 · chest3 15 · SculptingSpecial 24 · PaintCanBox 1.

## Neden iyi (tasarım dersleri)

1. **Hole = bölme çizgisi, silüet değil.** Tek tam satır + tek tam sütun hole, 9×9'u dört dikdörtgen "odaya"
   (6×6, 2×6, 6×2, 2×2) böler — pencere kasası gibi. Az hole (17 hücre), çok güçlü okunur yapı.
2. **Her oda tek tür.** Oda = tek engel ailesi; renk blokları net, göz hemen kuralı okur.
3. **Oda içeriği ulaşımla tutarlı.** Oyun alanından hole ile ayrılan odalara yalnız special'lar ulaşır → oraya
   yalnız special ile kırılan engeller konmuş (SculptingSpecial, PaintCanBox). Mekanik ve yerleşim aynı şeyi söyler.
4. **Dolum, kenardan L bant.** Ana odada yalnız üst satır + sol sütun taş (11 hücre); spawn köşesine yapışık.
   "Yardımcı kanal" açılmamış — blok bütün kalır, şık durur.
5. **Çapraz zorluk gradyanı.** Kolay chest1 dolum köşesine yakın üçgen, sert chest3 uzak üçgen; ayrım çizgisi
   çapraz. Kırılma, besleme köşesinden dalga gibi çapraz yayılır; düz şeritten daha zarif.
6. **Sayıları şekil belirler** (10/15), şekil sayıya uydurulmaz. Hamle yüksek (35) çünkü iş special'a dayalı.

Benim ilk taslağımla fark: ben ortaya dikey boş kanal açıp sütun şeritleri koymuştum — blok bölündü, gradyan yoktu.
