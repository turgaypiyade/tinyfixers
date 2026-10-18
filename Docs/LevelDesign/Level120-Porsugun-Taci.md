# Level 120 — Porsuğun Tacı (Boss Duel)

Asset: `LevelP_001255.asset` → `LevelCatalogPro` `level: 120` (chapter 5, `LevelCL_0120`). Boss Duel, 9×10 tam board, başlangıç ayarı 30 hamle. Hedef zorluk: 110'dan bir kademe zor (3 rakip).

## Kompozisyon

Board'un alt yarısında büyük bir taç: üç sivri fener ucu, ortada mücevher olarak RocketBasket, uç vadilerinde zümrüt gibi iki kalkan, baykuş inci sırası ve gümüş MetalWall taban. 110–119 döneminin iki yeni engeli (MetalWall 112, Hamster 117) kullanılır.

```text
ooooooooo
ooooooooo
ooooooooo
ooooRoooo
PoooPoooP
PoSoPoSoP
PPhPPPhPP
HHHHHHHHH
PPPPPPPPP
MMMMMMMMM
```

`o` normal taş (otomatik dolum), `R` RocketBasket (38), `P` PlasticTwoStage (40, 22 adet), `S` PlayerShieldPickup (35, 2 adet → +4 koruma), `h` Hamster (57, 2 adet), `H` HelmetPorcelain (30, 9 adet), `M` MetalWall (56): 3 parça × 3 hücre (origin 81 / 84 / 87).

Önizleme: `Level120-Porsugun-Taci.png` (gerçek sprite'lar, metal duvar oyundaki çeyrek-kare çizimiyle).

## Oynanış

- Taç alta oturur; altında akışsız ölü bölge yoktur. Başlangıçta ~45 serbest hücre (üst 4 sıra ve uçların arası).
- Katmanlı açılış: önce vadideki kalkan toplanır, yerine düşen taşla altındaki Hamster beslenebilir hale gelir.
- Hamster bu level'da hedef değildir (`hamsterGoal = 0`): 7 lokmada doyar, bir kez zıplayıp 3×3 vurur (PatchBot hedef havuzu), sonra ayrılır. İki tek seferlik yardım.
- Fenerler (iki vuruş, hareketli) ve baykuşlar kırıldıkça taç aşağı çöker, alan genişler.
- MetalWall taban dekoratiftir, hedef değildir; altında hücre olmadığı için kırılması gerekmez.

## Boss ayarları

| Alan | 110 | **120** |
|---|---|---|
| Hamle | 28 | 30 |
| BossDamage (rakip canı) | 400 (180 + 220) | 450 (135 + 148 + 167) |
| Rakip | 2 | 3 |
| Rakip hasarı | 6 / 8 | 6 / 8 / 9 |
| Oyuncu canı | 190 | 210 |
| Kalkan | 4 (+8) | 2 (+4) |
| Fırlatma | 4 karşılıkta 1, tavan 6 | 4 karşılıkta 2, tavan 7 |
| Havuz | Oil, PlasticTwoStage, Helmet, Plastic_Red | Oil, Mud, Plastic_Blue, Plastic_Red |
| Alet tehdidi | Açık (47–51, 4/3) | Açık (aynı) |

Havuzda HelmetPorcelain bilerek yok: tavan, havuz türünden authored engelleri de sayar; 9 baykuş tavanı (7) baştan doldurup fırlatmayı kapatırdı. PlasticTwoStage da bu yüzden havuzda değil.

Tanıtım sırası: MetalWall 112, Hamster 117, PlasticTwoStage 47, HelmetPorcelain 37, RocketBasket 71, kalkan 30; fırlatılanlar Oil 6, Mud 4, Plastic_Blue 14, Plastic_Red 15. Hepsi 120'den önce.

## Hasar bütçesi (duyarlılık hesabı, ölçüm değil)

Sabit taş/hamle; kalkanlar 4. hamlede toplanır; fırlatma, kilit ve Hamster yardımı yok sayılır.

| Taş/hamle | Sonuç |
|---|---|
| 15 | 31 hamle gerekir — kaybeder |
| 18 | 27 hamlede kazanır, 27 can |
| 20 | 24 hamle, 50 can |
| 25 | 19 hamle, 90 can |

110 hamle başına ~15 taşla geçiliyordu; 120 ~18 taş ister. Hamle ilk tahmindir; Unity, bot veya simülasyon çalıştırılmadı. Gerekirse `config/levels` (Firestore) üzerinden ayarlanır.
