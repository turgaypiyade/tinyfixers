# Level 121 — Jel Kartuşları (GelLauncher testi)

`LevelCatalogPro` global 121 → `LevelP_001260.asset` (eski "ayna çiftleri" varyantının numarası, yeni asset/GUID). Tam 9×11, 26 hamle. Amaç: yeni jel fırlatıcının (kartuş) oyunda denenmesi.

## Kompozisyon

```text
ooooooooo
ooooooooo
ooooooooo
oHHHoHHHo
ooooooooo
ooogggooo
ooooooooo
oHHHoHHHo
ooooooooo
ooooooooo
>>SSSSS<<
```

- `>>` sol alt: GelLauncherRight (62, 2×1, ağız sağa). `<<` sağ alt: GelLauncherLeft (61, 2×1, ağız sola). Ortak origin sol hücre.
- `S` SculptingStone (28, 3 vuruş) — kartuşların arasında; kapak geçerken hepsini tek atışta kırmalı.
- `H` HelmetPorcelain, `g` açık jel (orta 3 hücre), `o` normal taş.

## Test edilecekler

1. Kartuş yalnız special ile hasar alıyor mu; 1. vuruşta çatlak görsele geçiş ve sarsıntı.
2. 2. vuruşta kapak ağızdan doğru yöne fırlıyor mu; yolundaki SculptingStone'lar tamamen kırılıyor mu.
3. Kapak karşıdaki kartuşa çarpınca onu kırıp **zincirleme** ateşliyor mu (karşı yöne ikinci atış).
4. Jel kapağın ~2 hücre gerisinden geliyor mu; alt satır ve kartuş hücreleri jelleniyor mu; jel sayacı düşüyor mu.
5. Atış sırasında board akışı/level sonu beklemesi düzgün mü.

## Hedefler

99 hücre jel, 12 HelmetPorcelain, 5 SculptingStone. Kartuşlar hedef değil.

## Statik kontrol

Diziler, 2×1 kartuş origin'leri, hedef adetleri ve katalog bağlantısı kontrol edildi. Hazır üçlü/2×2 yok, 11 geçerli takas. Unity'de çalıştırılmadı.
