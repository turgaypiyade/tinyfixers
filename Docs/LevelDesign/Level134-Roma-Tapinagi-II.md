# Level 134 — Roma Tapınağı II

Asset: `LevelP_001390.asset` → `LevelCatalogPro` `level: 134` (chapter 6, `LevelCL_0134`). 9×11, 28 hamle, 4 renk.
Kullanıcı beklentisi (133'ten sonra): **Wall çatı + tek sıra Wall zemin + Tube sütunlar.** 133 olduğu gibi kaldı.

```text
ooooooooo
ooooooooo
ooooWoooo
oooWWWooo
ooWWWWWoo
oWWWWWWWo
oToToToTo
oToToToTo
oToToToTo
oToToToTo
WWWWWWWWW
```

- Çatı (alınlık) Wall, 3 parça: sol eğim (origin 38, 3 hücre), orta (origin 22, 10 hücre), sağ eğim (origin 42, 3 hücre).
- Zemin tek sıra Wall, 3 parça (origin 90 / 93 / 96).
- Sütunlar Tube ×4: taban y=9, yukarı, 4 hücre (origin 82 / 84 / 86 / 88).

Hedefler: Tube 4 · Wall 6 (parça). Önizleme: `Level134-Roma-Tapinagi-II.png`.

## Oynanış

- **Çatıyı yık, içeri taş yağsın:** sütun aralarındaki üç iç koridor (x=2/4/6) çatı, sütunlar ve zeminle kapalı;
  başlangıçta dolu ama yeniden dolmaz. Üstündeki çatı parçası yıkılınca dikey dolum açılır; tüp kısaldıkça boşalan
  hücreler dış koridorlardan çapraz dolar. Bilinçli katmanlı açılış — çatı üstteki gökyüzü bölgesinden vurulur.
- Dış koridorlar (x=0/8) her zaman açık, gökyüzüne bağlı.
- Koridorlar tek sütun: orada yalnız dikey eşleşme; her biri iki yandaki tüpü vurur. Tüp üstten kısalır.
- Zemin her koridorun dibinde: koridor eşleşmeleri zemini de vurur. Duvar hücresi 3. vuruşta tüm parçayı yıkar.

Risk: iç koridorlar erken boşalırsa sütunlara erişim yalnız dış koridorlardan ve special'larla kalır; çok sıkışık
gelirse orta çatı parçasını ikiye bölmek (x=3 ve x=5 sütunları ayrı) açılmayı hızlandırır.
Hamle ilk tahmindir; Unity/bot/simülasyon çalıştırılmadı.
