# Level 160 — Kızıl Köprü (Boss Duel)

`LevelCatalogPro` global 160 → `LevelP_001640.asset`, chapter 7. **9×10, 30 hamle, dört renk.** [Yerleşim önizlemesi](Level160-Kizil-Kopru.png).

Golden Gate'i çağrıştıran iki kırmızı kule, geniş tabliye, katmanlı iki krem ayak ve altta yeşil kalkan grupları. Boss karakterlerine yer bırakmak için yükseklik 10 satırdır.

```text
.RR...RR.
.RRoooRR.
oRRoooRRo
ooooEoooo
ooooEoooo
ooooooooo
.MMoooMM.
.MMoooMM.
.MMoooMM.
.SSSoSSS.
```

`.` hole · `o` rastgele normal taş · `R` Plastic_Red (12) · `E` yardımcı EggBird (45) · `M` Mud (25) → SculptingStone (28) · `S` PlayerShieldPickup (35).

| Yerleşim | Adet | Rol |
| --- | ---: | --- |
| Plastic_Red | 12 | İki bitişik 2×3 kırmızı kule; normal hasar için kırmızı komşu eşleşme |
| Mud → SculptingStone | 12 | İki 2×3 ayak; üç aşamalı taşın altında iki aşamalı çamur |
| PlayerShieldPickup | 6 | Alt sırada iki üçlü grup; toplanırsa toplam +12 koruma |
| EggBird | 2 | Merkezde bitişik dikey yardım çifti; goal değil |

Kazanma hedefi yalnızca **540 BossDamage**. Bütün kuleleri, ayakları, çamuru veya yumurtaları bitirmek zorunlu değildir. Oyuncu geniş tabliyede hasar/special üretmek ile alt korumayı açmak arasında karar verir. Kalkanlar başlangıçta açıktır; taş ayaklarının yanından normal/special etkileriyle alınabilir. Bir hedef olarak temizlenmeleri gerekmez. Kule plastikleri, kalkanlar ve yumurtalar hareketlidir; gösterilen geometrik diziliş gravity ile değişebilir.

| Boss ayarı | Değer |
| --- | --- |
| Toplam rakip canı | 540; üç otomatik dalga |
| Oyuncu canı | 250 |
| Taş başına hasar | 1 |
| Rakip temel hasarı | 7; mevcut dalga ölçeklemesi |
| Kalkan başına koruma | 2 |
| Engel fırlatma | Her 4 sayaçta 2 |
| Fırlatılan türler | Oil, Plastic_Blue, HelmetPorcelain, PlasticTwoStage |
| Fırlatma tavanı / yağ sınırı | 8 / 8 |
| Alet tehdidi | 150 ile aynı; her 4 sayaçta, 3 geri sayım, 2 ek saldırı, 3 saniye kilit |

150'nin 520 toplam can / 240 oyuncu canı / 30 hamle referansından 540 / 250 / 30'a geçildi. Başlangıçta 12 kırmızı plastik bulunduğu için kırmızı tür fırlatma havuzuna alınmadı; başlangıç kuleleri fırlatma tavanını tüketmez. Kalkan bütçesi 150'deki gibi +12'dir, fakat bu kez heykel arkasında saklanmaz. İki kuş ve daha kolay görülen koruma, 159'un ardından farklı bir tempo sunmayı amaçlar; bu bir ölçülmüş zorluk kıyası değildir.

75 aktif hücre, 15 hole, tek bağlı ve simetrik maske. Yedi dizi 90 eleman. **43 normal başlangıç hücresi** otomatik doldurulur; pin veya hazır special yoktur. İki dikey yumurta ana tabliyede dolumu kesmez; 43 normal hücrenin tamamına geometrik spawn/köşe yolu vardır. Çapraz geçişte mask hole köşesi geçerli kabul edilmez; en az bir aktif izinli köşe gerekir. Sıvılar tabanda, on iki katı örtü üst katmandadır; bir yığında birden fazla hareketli engel bulunmaz.

Yeni runtime/VFX kodu eklenmedi. Mevcut boss fırlatma ve tehdit servisleri, iki kuş uçuşu ve kalkan toplama kullanılır. Fırlatma/yağ tavanları 8 tutulur; kuş başına mevcut tahsis/tek tarama maliyeti kaldırılmış veya ölçülmüş değildir. Boss `OnDisable`'da coroutine'leri durdurur, input/end-evaluation kilitlerini ve event bağlantılarını bırakır. Kuş aksiyonu mevcut level doğrulaması ve `finally` temizliğini kullanır. Performans kazancı iddiası yoktur; cihaz/Profiler ölçümü yapılmadı.

`python3 Tools/check_levels158_160.py` hedef, dizi, maske, origin, katman, sıvı sırası, dolum geometrisi, boss yüksekliği, fırlatma havuzu ve katalog bağlantısını kontrol eder. Önceki level/test assetleri ve ortak ObstacleLibrary byte karşılaştırmasında korunur.

**30 hamle ve can değerleri ilk oynanış ayarıdır; kazanma oranı ölçülmedi.** Unity Play/build, bot veya oyun simülasyonu çalıştırılmadı. Önizleme yerleşim şemasıdır; taş renkleri temsili, gerçek başlangıç rastgeledir.
