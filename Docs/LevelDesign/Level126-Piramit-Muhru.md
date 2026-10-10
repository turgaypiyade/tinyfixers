# Level 126 — Piramit Mührü

`LevelCatalogPro` global 126 → `LevelP_001310.asset`. 9×11, 26 hamle. Safe’ten ayrı Ancient Seal (Antik Mühür) engelinin tanıtım levelı: zor değil, eğlenceli.

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
- `$` alt köşelerde iki 3×3 Antik Mühür (AncientSeal): soldaki kırmızı → sarı → yeşil, sağdaki yeşil → kırmızı → sarı; her aşama 4 taş toplama.
  Mühürler en alt satırlarda: altlarında gölge (refill almayan hücre) kalmaz.
- Mühür altı boş; her mührün orta-alt hücresinde (1,9) / (7,9) sabit **PulseCore** ödülü — mühür açılınca çıkar.

## Hedefler

Mühür ×2, Çim ×25.

## Akış notu

Mühür, tahtanın herhangi bir yerinde aktif renkten temizlenen taşları uçuşla toplar. Taşın kopyası kırıldığı konumdan renkli iz bırakarak mührün ilgili yuvasına gider; sayaç yalnız varışta 1 azalır. Uçuşta olan taşlar kalan ihtiyaca rezerve edilir: sayaç 2 ise en fazla 2 taş yola çıkar. Son taş varmadan aşama/mühür tamamlanmaz; uçuş sırasında gravity devam eder, bölüm sonucu bekler. Örneğin kırmızı 4 sayacı, 3 kırmızı taş toplanınca 1 olur. Renk bitince sıradaki aşama açılır; fazla taş sonraki renge aktarılmaz. Special doğrudan mührü ilerletmez; special’ın temizlediği uygun renk taşlar sayılır. Her temizlenen taş yalnızca bir mühre ayrılır. Aynı rengi isteyen birden fazla açık mühür varsa taşlar sırayla paylaştırılır; uçuşta rezerve edilenler de kapasiteden düşülür. Üzeri başka bir katmanla örtülü mühür, açığa çıkana kadar toplamaz.

Editörde **Ancient Seal** sekmesinden boyut, üç rengin taş sayıları ve her mührün başlangıç rengi ayarlanır. `Auto` (eski kayıtların varsayılanı) mühürleri konuma göre sıralayıp başlangıç renklerini kırmızı, yeşil, sarı önceliğiyle dengeler. Açıkça seçilmiş renkler önce ayrılır. Her mühür seçilen renkten başlayarak kırmızı → sarı → yeşil döngüsünde üç aşamayı tamamlar; verisi `ancientSeals[]`, hedefi `AncientSeal` (64). Mevcut `Safe` (33), eski kasa görseli ve komşu vuruş mekaniğiyle korunur.
