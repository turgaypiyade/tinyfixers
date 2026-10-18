# Level 158 — Kilitli Alet Çantası

`LevelCatalogPro` global 158 → `LevelP_001620.asset`, chapter 7. **9×11, 32 hamle, dört renk.** Beklenen zorluk: orta-zor. [Yerleşim önizlemesi](Level158-Kilitli-Alet-Cantasi.png) · [Üç yeni level birlikte](Levels158-160-Onizleme.png).

158'deki `LevelP_testt 1.asset` dosyası korunur; katalogdaki 158 artık bu yeni üretim assetine bağlanır. 1–157'nin katalog kayıtları değiştirilmez.

```text
...mmm...
..mm.mm..
..m...m..
.ooooooo.
CoooooooC
CoSSoSSoC
CoSSoSSoC
CoooooooC
CHHHHHHHC
.CCCCCCC.
..CCCCC..
```

`.` hole · `o` rastgele normal taş · `m` açık Mud (25) · `C` chest2 (5) · `S` Mud tabanı üstünde 2×2 Safe (33) · `H` Mud → HelmetPorcelain (30).

| Hedef | Adet | Tasarım rolü |
| --- | ---: | --- |
| Safe | 2 | İki farklı kilit sırasıyla odak seçimi |
| chest2 | 22 | Sıcak ahşap yan çerçeve ve iki basamaklı alt taban |
| Mud | 24 | Sapta 9 açık, kasaların altında 8, porselen şeridinin altında 7 |

Sapın içi gerçek hole'dur. Sapın dokuz hücresindeki çamur taş üretimini kesmez; üstte sabit sandıklarla ana odanın dolumu kapatılmaz. Gövdedeki iki mor kasa ve krem porselen şeridi, turuncu ahşap çerçeveyi tamamlar.

Kasalar (2,5) ve (5,5), origin 47 ve 50'dir; koordinatlar sıfır tabanlı. Her biri **iki kırmızı, iki sarı, iki yeşil vuruş** ister. Solda kırmızı → sarı → yeşil, sağda yeşil → sarı → kırmızı sırası kullanılır. Normal komşu eşleşme aktif kilidin rengini ister; special mevcut sıradaki açık kilide vurabilir. Böylece aynı renkten gelen eşleşmeler iki kasada farklı zamanda değerlidir. Kasa açıldıktan sonra altındaki dört çamuru da temizlemek gerekir.

Yedi porselenin tabanı doğrudan `obstacles[] = Mud`, örtüleri `stackedObstacles[]`, `stackOrder=1`'dir. Kasalar `safes[]` ile aynı şekilde çamurun üstüne kurulur. Üst katmanda sıvı yoktur; hiçbir yığında birden fazla hareketli engel bulunmaz. Porselen hareketlidir; şerit oyun sırasında değişebilir.

Başlangıçtaki **36 normal taş hücresi** otomatik doldurulur; iki pin dizisi tamamen sıfırdır. Hazır special veya yardımcı yoktur. Dar sap, sandık tabanı ve kasa tercihleri zorluk kaynaklarıdır; kasa altındaki çamurun görünmesi ikinci açma aşamasını oluşturur.

73 aktif hücre, 26 hole; yedi hücre dizisi 99 eleman. Maske tek bağlı parçadır. Dizi uzunlukları, origin'ler, 2×2 kasa ayak izleri, hedefler, katman sırası, sıvı tabanı, dört renk havuzu ve GUID/katalog bağlantısı `python3 Tools/check_levels158_160.py` ile statik kontrol edilir. Sabit ve hareketli engeller yerindeyken 36 normal hücrenin tamamına olası spawn/düşüş/izinli köşe yolu bulunur. Bu kontrol zamana bağlı gravity veya kazanma simülasyonu değildir.

Yeni runtime/VFX kodu eklenmedi. İki kasa mevcut servisi ve görünümleri kullanır; servis level kurulumunda temizlenir, interceptor `OnDisable`'da, görünüm event bağlantıları `OnDestroy`'da bırakılır. Efekt havuzlaması değişmedi; performans iyileşmesi iddiası veya cihaz ölçümü yoktur.

**32 hamle ilk denge ayarıdır; kazanma oranı ölçülmedi.** Unity Play/build, bot veya oyun simülasyonu çalıştırılmadı. Önizleme mevcut sprite'larla yapılmış yerleşim şemasıdır; gösterilen normal taş renkleri temsili, gerçek başlangıç rastgeledir.
