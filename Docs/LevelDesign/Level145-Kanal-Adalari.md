# Level 145 — Kanal Adaları (zor)

Asset: `LevelP_001490.asset` (guid 388bc9b9…) → `LevelCatalogPro` `level: 145` (chapter 6, `LevelCL_0145`).
9×11, 30 hamle, 4 renk. Kullanıcı isteği: zor, görsel olarak şık; Lazer Topu / AncientSeal / Brandalı Heykel;
grid'de bağımsız adacıklar, çoğu PatchBot ile kırılsın.

```text
TT.ooo.TT
TT.ooo.TT
...ooo...
qq.ooo.qq
qq.ooo.qq
...ooo...
AA.ooo.AA
AA.ooo.AA
...ooo...
KK.ooo.KK
KK.ooo.KK
```

- **Kanal:** ortada tek oyun alanı x=3–5 (3×11, 33 taş). Dar kanal → 2×2 kare sık oluşur (PatchBot); dikey
  eşleşmeler yatay roket verir ve tek roket aynı satırdaki **iki yan adayı birden** vurur.
- **Adalar:** x=2 ve x=6 tam sütun hole + y=2/5/8 hole satırları → her yanda 4 kat, toplam 8 bağımsız 2×2 ada.
  - `T` Brandalı Heykel (68) — sol kırmızı, sağ sarı, sayaç 25 (renk taşı her yerden toplanır), sonra 3 special vuruş.
  - `q` SculptingSpecial (39) ×8 — yalnız special.
  - `A` AncientSeal 2×2 ×2 (6/6/6; sol sarı, sağ yeşil başlangıç) — **altında SculptingSpecial ×8** (mühür açılınca
    ikinci iş: special'la kırılacak).
  - `K` Lazer Topu (65) ×2 — hedef; 4 special vuruş; o kırılana dek kanaldaki special'ları kafesler.
- **Açılma akışı:** ada boşalınca üstündeki hole satırından taş düşer → ada oyuna katılır (yukarıdan aşağı sırayla).
  Üst heykel adaları açılınca 2×2 mini alan olur — orada da PatchBot karesi çıkar.

Hedefler: Brandalı Heykel 2 · SculptingSpecial 16 · AncientSeal 2 · Lazer Topu 2.
Special vuruş ihtiyacı ≈ 6 (heykel) + 16 (sculpt) + 8 (top) = 30; yatay roket satır başına 2 ada vurur.

Hamle ilk tahmin (30); Unity/bot/simülasyon çalıştırılmadı. Çok zor gelirse ilk düğme hamle (+3), ikincisi
Lazer Topu'nu hedef olmaktan çıkarıp tek topa indirmek. Önizleme: `Level145-Kanal-Adalari.png`.
