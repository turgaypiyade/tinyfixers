# Level 108 — Jel Kristali

`LevelCatalogPro` global 108 → `LevelP_001150.asset`. Normal level, 9×11 zarf / 75 aktif hücre. Başlangıç ayarı 30 hamle; oynanarak dengelenmedi.

## Görsel ve mekanik kompozisyon

Köşeleri basamaklı kesilmiş kristal silueti. Merkezde 13 hücrelik mor jel elması, iki yanda 2×3 krem SculptingStone blokları. Dört EggBird'ün krem/mor renkleri merkezi ve kenarları bağlar. Dikey eksende iki RocketBasket yardımcı odaktır. Board ve engeller iki eksende; ilk normal taş renkleri sağ-sol simetriktir.

```text
...ooo...
..ooRoo..
.oEoooEo.
ooooGoooo
SSoGGGoSS
SSGGGGGSS
SSoGGGoSS
ooooGoooo
.oEoooEo.
..ooRoo..
...ooo...
```

`.` hole, `o` normal taş, `G` açık jel, `S` altında mühürlü jel bulunan SculptingStone, `E` EggBird, `R` RocketBasket. `Level108-Jel-Kristali.svg` mevcut engel sprite'larıyla yerleşim şemasıdır; gerçek oyun ekran görüntüsü değildir. Normal taşlar renk şemasıyla, jel hücreleri ayrı sprite'larla gösterilir; runtime'da jel birleşir.

## Hedefler

- **75 hücre jel kaplama:** bütün aktif board; engellerin açılacağı hücreler dahil. 13 açık seed baştan sayılır, başlangıç kalan hedefi 62'dir. 12 mühürlü seed baştan sayılmaz.
- **12 SculptingStone:** üçer vuruş; her biri kırılınca altındaki jel açılır ve hedefe sayılır.
- **4 EggBird:** ikişer vuruş; her yumurtadan üç hedefe yardım gelir. Kuş hedefleri rastgele olduğundan hangi taşı açacakları garanti edilmez.
- **2 RocketBasket yardımcıdır**, ayrı goal değildir. Normal üç renk şarj davranışı açık; `rocketBasketFireAllOnHit=0`.

Jel 86, SculptingStone 38, RocketBasket 71, EggBird 87'de kullanılmıştır. 108 yeni engel tanıtmaz; önceki mekanikleri farklı bir kompozisyonda geri getirir.

## Amaçlanan akış

1. Merkezde jele değen eşleşmelerle yayılmaya başla; jel kaynaklı special üretimi güçlü fırsattır.
2. Yumurtaları ve roketleri kullanarak yan blokları aşındır. Blokların açılması 12 yeni jel kaynağı kazandırır.
3. Üst/alt uçları ve yardımcıların boşalttığı hücreleri de kaplayarak bütün board'u tamamla.

Açılışta hazır special verilmedi. Statik dizilimde `(4,4) ↔ (4,5)` takası jel kaynaklı dört taşlık eşleşme fırsatıdır (sıfır tabanlı x/y). Eşleşmeler ve obstacle açma birlikte ilerleyebilir; kaplama için ayrı bir oyun fazı eklenmedi.

## Yapısal kontrol

99 elemanlı diziler, 75 hücrelik bağlı maske, origin'ler, katmanların jel üzerinde durması, goal toplamları ve katalog bağlantısı kontrol edilir. 57 açık normal/jel taş hücresi vardır. Hazır üçlü veya aynı renk 2×2 yoktur; normal taşlar arasında 14 geçerli açılış takası vardır. Her iki sepetin de başlangıçta doğru renkle tetiklenebilen komşu eşleşme seçeneği vardır. Hareketli yumurta takasları bu sayıya dahil değildir.

Unity, bot, build ve oynanış simülasyonu çalıştırılmadı. Statik açılış denetimi kazanılabilirlik veya hamle dengesi garantisi değildir; 30 hamleyi kullanıcı oynayarak ayarlayacak.
