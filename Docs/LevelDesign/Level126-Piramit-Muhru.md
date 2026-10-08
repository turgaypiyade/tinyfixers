# Level 126 — Piramit Mührü

`LevelCatalogPro` global 126 → `LevelP_001310.asset`. 9×11, 26 hamle. Yeni kasa görünümünün (Piramit Mührü) tanıtım levelı: zor değil, eğlenceli.

## Kompozisyon

```text
...ooo...
..ooooo..
.ooooooo.
ooooooooo
ooooGoooo
oooGGGooo
ooGGGGGoo
oGGGGGGGo
$$$GGG$$$
$$$GGG$$$
$$$GGG$$$
```

- Tahtanın üstü piramit silüeti gibi daralır (üst köşeler delik).
- `G` ortada basamaklı çim piramidi (25 hücre) — altında normal taş.
- `$` alt köşelerde iki 3×3 Piramit Mührü (Safe), Ordered: kırmızı → sarı → yeşil, her kilit 4 vuruş.
  Mühürler en alt satırlarda: altlarında gölge (refill almayan hücre) kalmaz.
- Mühür altı boş; her mührün orta-alt hücresinde (1,9) / (7,9) sabit **PulseCore** ödülü — mühür açılınca çıkar.

## Hedefler

Mühür ×2, Çim ×25.

## Akış notu

Mühürlere bitişik eşleşmeler 7. satırdaki çimin üstünden yapılır: çim temizlenirken mühür de vurulur. Special'lar mührün aktif kilidine joker vuruş sayılır. İlk mühür açılınca çıkan PulseCore, orta çim bloğunu ve diğer mührü hızlandırır.
