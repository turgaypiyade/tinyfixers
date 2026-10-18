# Level 110 — Çiçekli Kemer (Boss Duel)

Asset: `LevelP_001165.asset` → `LevelCatalogPro` `level: 110` (chapter 5, `LevelCL_0110`). Boss Duel, 9×10 tam board, başlangıç ayarı 28 hamle. Hedef zorluk: **normal** (100 zor bulundu).

## Kompozisyon

Üstte kubbeli bir çiçek kemeri, kubbenin altında kilit taşı gibi duran RocketBasket; altta mazgallı bir sur, mazgal aralarında oyuncu kalkanları. 100–109 döneminin iki yeni engeli (Wall 101, GrassFlower 105) birlikte kullanılır.

```text
ooooooooo
oooFFFooo
oFFFFFFFo
FFFoooFFF
FFooRooFF
FoooooooF
ooooooooo
WSWSWSWSW
WWWWWWWWW
WWWWWWWWW
```

`o` normal taş (otomatik dolum), `F` GrassFlower (58, 22 hücre, hedef değil), `R` RocketBasket (38, yardımcı), `S` PlayerShieldPickup (35, 4 adet → +8 koruma), `W` tuğla Wall (55): 3 parça — sol 8 hücre (origin 63), orta 7 hücre (origin 67), sağ 8 hücre (origin 69).

Önizleme: `Level110-Cicekli-Kemer.png` (oyunun gerçek sprite'larıyla, duvar oyundaki çeyrek-kare çizimiyle).

## Oynanış

- Çiçek altındaki taşlar akmaya devam eder, yalnız o hücrede eşleşme/takas kilitlidir. Kemer akışı tıkamaz; komşu eşleşmelerle açıldıkça oyun alanı genişler.
- Başlangıçta serbest ~40 hücre: üst iki sıra, kubbe içi cep ve surun hemen üstündeki iki sıra.
- Kalkanlar mazgal aralarında, ilk hamlelerden erişilebilir (erken ödül).
- Sur parçaları 5 vuruşta bütünüyle yıkılır; yıkılınca alt sıralar açılır ve hamle başına kırılan taş artar.
- RocketBasket kemerin kırılmasına ve rakibe hasar toplamaya yardım eder.

## Boss ayarları

| Alan | Değer | Not |
|---|---|---|
| BossDamage (toplam rakip canı) | 400 | Formül: 180 + 220 |
| Rakip | 2 (`bossWaveCount`) | Profil formülden (aynı porsuk) |
| `enemyAttackBaseDamage` | 6 | 2. rakip ×1.25 → 8 |
| `playerMaxHp` | 190 | 100'de 175 |
| Fırlatma | 4 karşılıkta 1, tavan 6 | Havuz: Oil, PlasticTwoStage, HelmetPorcelain, Plastic_Red |
| Alet tehdidi | Açık, 4 karşılıkta, geri sayım 3 | Türler 47–51 (90/100 ile aynı) |
| Ek hedef | Yok | 100'deki jel ve su tankı hedefleri zorluğu artırıyordu |

Tanıtım sırası kontrolü: Wall 101, GrassFlower 105, RocketBasket 71, kalkan 30; fırlatılanlar Oil 6, PlasticTwoStage 47, HelmetPorcelain 37, Plastic_Red 15. Hepsi 110'dan önce açılmış.

## Hasar bütçesi (duyarlılık hesabı, ölçüm değil)

Sabit taş/hamle varsayımı; kalkanlar erken toplanır, fırlatma ve kilit etkisi yok sayılır.

| Taş/hamle | 110 | 100 (karşılaştırma, ek hedefsiz) |
|---|---|---|
| 15 | 27 hamlede kazanır, 20 can kalır | Hamle yetmez |
| 18 | 23 hamle, 48 can | 23 hamle, can biter (−17) |
| 20 | 20 hamle, 70 can | 20 hamle, 11 can |
| 25 | 17 hamle, 92 can | 17 hamle, 39 can |

İlk hamlelerde kemer nedeniyle taş/hamle düşük, sur yıkıldıktan sonra yüksek olacaktır. Hamle sayısı ilk tahmindir; Unity, bot veya simülasyon çalıştırılmadı. Gerekirse hamle `config/levels` (Firestore) üzerinden merkezi ayarlanır.

`Tools/audit_boss_progression.py` bu değişiklikten bağımsız olarak eskimiş (ilk 50 level için yazılmış; `LevelP_00100`'de hata veriyor), bu level için kullanılmadı.
