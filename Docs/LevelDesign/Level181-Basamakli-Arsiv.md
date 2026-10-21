# Level 181 — Basamaklı Arşiv · v4

Katalog 181 → `LevelP_001850.asset`, chapter 7. **9×11, 82 aktif / 17 hole, 26 hamle, dört renk, Normal.** Kullanıcı v3'teki ayrı delikler yerine tamamen boş çapraz şerit ve mevcut köşe yuvarlatmasının korunmasını istedi. v4 henüz onaylanmadı. [Güncel önizleme](Level181-Basamakli-Arsiv.png), [büyütülmüş köşe kontrolü](Level181-Capraz-Kose-Kontrolu.png).

**v4:** v3'teki dokuz çapraz deliğin arasına üst sağ taraftan sekiz hücre daha eklendi: sıfır tabanlı `(k+1,k)`, `k=0…7`. 17 hole artık kenardan bağlı tek bir basamaklı şerit. Üst sağdaki çalışma bölgesi **28**, alt soldaki bölge **54** aktif hücre. Alt bölgenin 14 normal taşı ve bütün 40 engel hücresi korundu. `DynamicBoardBorder.GetNodeMask` düzeninde mask 5/10 olan köşe teması sayısı **8→0** oldu. Ortak köşe/yuvarlatma kodu, border theme'leri, prefablar ve sahne ayarları değişmedi; production assette yalnız `cells[]` değişti.

**v3:** Sol üst hücreyi `(1,1)` kabul eden kullanıcı koordinatlarıyla `(1,1), (2,2), …, (9,9)` hole yapıldı. Kodda sıfır tabanlı `(0,0)…(8,8)`, flat index `0/10/20/30/40/50/60/70/80`. Board 9 sütun ve 11 satır olduğundan eşit x/y çaprazı sağ kenara 9. satırda ulaşır; alttaki iki satır korunur. Maske normal dört yönlü komşulukta tam iki parçadır: **üst sağ 36**, **alt sol 54** aktif hücre. Engel footprint'leri çizginin altında kaldığından hiçbir gövdeyi veya katmanı taşımak gerekmedi; production assette yalnız `cells[]` değişti.

Kullanıcı açıklaması: **“şekle çok itiraz etmiyorum ama çok kolay ... bu level için”**. Önceki değerlendirme şekil tekrarına fazla ağırlık verdi. Basamak fikri korunarak engel alanı büyütüldü ve katman yükü artırıldı. Önceki 75 açık hücre ile yalnız 24 engel hücresi, special üretmek için geniş çalışma alanı bırakıyordu; kullanıcının kolaylık geri bildirimi doğrultusunda bu oran değiştirildi. Kazanma oranı ölçümü yok.

```text
..ooooooo
o..oooooo
oo..ooooo
WWo..oooo
WWoo..ooo
CCWWo..oo
CCWWoo..o
CCCCWWo..
CCCCWWoo.
CCCCCCWWo
CCCCCCWWo
```

`.` hole; `o` otomatik normal taş, `W` 2×2 Wardrobe, `C` eski 2×2 ColorChest. Sol ve alt kenara bağlı engel kütlesi korundu; bütün gövdeler birbirine bitişik. Çapraz boşluk üst sağdaki çalışma alanını alt soldaki engel alanından ayırıyor. Sabit renk, hazır special ve yardımcı engel yok. Yeni ColorCrate 71–74 kullanılmadı.

| Ayar | İlk taslak | v2 | v3 | v4 |
| --- | ---: | ---: | ---: | ---: |
| Başlangıç taşı hücresi | 75 | 59 | 50 | **42** |
| Hole | 0 | 0 | 9 | **17** |
| Engelle kaplı hücre | 24 | 40 | 40 | **40** |
| Eski ColorChest · 21 | 3 | 6 | 6 | **6** |
| Wardrobe · 27 | 3 / 30 eşya | 4 / 40 eşya | 4 / 40 eşya | **4 / 40 eşya** |
| SculptingStone · 28 | 4 | 16 | 16 | **16** |
| Mud · 25 | 24 | 40 | 40 | **40** |
| Üç katmanlı hücre | 4 | 16 | 16 | **16** |
| Hamle | 27 | 26 | 26 | **26** |

Hedefler: **21 × 6, 27 × 4, 28 × 16, 25 × 40**. Üç katmanlı hücreler ayrı hedef değildir.

Her dış basamakta dolap bulunur. Dört dolabın her birinde `Mud → dört SculptingStone → Wardrobe` yığını vardır. Dolap boşalınca alan hemen serbest kalmaz; önce üç vuruşlu taşlar, sonra iki aşamalı çamur temizlenir. İçteki altı ColorChest’in yığını `Mud → ColorChest` olur. Kapak açıldıktan sonra dört farklı kalan renk tüketilir; normal match uygun rengi, special kalan renklerden birini ilerletir. Dolaplarda kapak sonrası normal hit bir, special iki eşya çıkarır.

Dolap origin’leri **27/47/67/87**: `(0,3)/(2,5)/(4,7)/(6,9)`. Sandık origin’leri **45/63/65/81/83/85**: `(0,5)/(0,7)/(2,7)/(0,9)/(2,9)/(4,9)`. Koordinatlar sıfır tabanlı ve y üstten aşağıdır. Mud’un 40 hücresi ayrı origin’lerdir. Oyma taşları `stackOrder=1`, dolaplar `stackOrder=2`; dolaplar alttaki dört taşı tamamen örter. Hareketli engel yok.

Üst sağdaki 28 hücre başlangıçta normal taştır. Alt solda 40 engel hücresi ve aralarında 14 normal taş vardır; bu küçük cepler tek başlarına geniş bir eşleşme alanı sağlamaz. Üst bölümde üretilen special hatlarıyla dolapları açmak, ardından alt alanda sandık renkleri/taş/çamur işini ilerletmek amaçlanır. `LineHSpecial.CollectRow` ve `LineVSpecial.CollectColumn`, etkilenemeyen hole hücresinde `continue` eder; satır/sütun taramasını durdurmaz. İki bölge normal eşleşme komşuluğunda ayrıdır; gravity ve special etkileri açısından bütünüyle yalıtılmış değillerdir. **26 hamle ilk denge ayarıdır**; 181 Normal kalır. %40–50 yönü hedeftir; v4'ün zorluğu veya geçiş oranı ölçülmedi.

Statik kontrol geçti: Unity YAML ayrıştırması, diziler, 17 hole'un kenardan tek bileşen olması, tam iki aktif maske bileşeni, çapraz-only köşe temasının olmaması, hole üzerinde engel/pin bulunmaması, origin/footprint, tam örtme, katman sırası, hedefler, tanıtılmış engeller, isim/GUID/katalog ve bütün **42 başlangıç hücresinin dikey dolumu**. Hole'lar yalnız dikey spawn geçişinde değerlendirilir, oynanabilir taş hücresi sayılmaz. Mevcut sprite’larla render edilip görsel kontrol yapıldı. Önceki **1.859 ayar/script dosyası** ve border ile ilgili ayrıca **683 grid script/prefab/theme/sahne dosyası**, katalog ve 181’in GUID/ismi korundu. v2 assetiyle karşılaştırma yalnız maske alanının değiştiğini doğruladı. Unity Play/build veya oyun/bot/kaskat simülasyonu çalıştırılmadı.

Köşe kontrolü: `render_border_review.py`, sahnedeki DarkNavy sprite seti, borderThickness/coreRatio/outsideGap ve `DynamicBoardBorder` yerleşim formülleriyle v3/v4'ü statik olarak çizdi. Büyütülmüş birleşimler görsel olarak incelendi. Bu çalışma mevcut yuvarlatma algoritmasının yerine geçmez ve Unity'deki son piksel görünümünün doğrulanması değildir; ortak renderer değiştirilmedi. Köşe temasındaki belirsizliği maske düzeyinde kaldırır.

Performans: yeni runtime/VFX kodu eklenmedi. Mevcut Wardrobe görünümü havuzlanmayan eşya/kapak objeleri ve yarım kapak sprite’ları oluşturur; üç dolap yerine dört dolap kullanılması 30 yerine 40 eşya ve 6 yerine 8 kapak getirir. Mevcut hit yolu `CollectCellsForOrigin` içinde board taraması ve List/ToArray tahsisi yapar. Normal tamamlanmada eşya/kapak objeleri coroutine sonunda, dolap görünümü son eşyanın düşüşünden sonra yok edilir. Parçalar dolabın çocuğudur; component/scene yok edilmesi coroutine’leri sonlandırır. Ayrı OnDisable/reset temizliği ve üretilen yarım sprite’ların açık Destroy çağrısı yok; havuz yeniden kullanımı veya eksiksiz reset güvenliği iddiası yapılmaz. Cihaz performansı ölçülmedi; bu tasarım turunda ortak servis kapsamı genişletilmedi.

Önceki v1/v2/v3 asset/meta, design JSON, not ve PNG'leri `Batches/181/Revisions/` altında saklandı. v4'te engel/efekt sayısı ve runtime yolları değişmedi; yukarıdaki mevcut performans sınırları korunur. [Önceki tekrar incelemesi](Reviews/181-Repetition/Review.md) v1’i anlatır; şekil benzerliği kaydı son kullanıcı açıklamasındaki zorluk önceliğiyle birlikte okunur. İsim **Basamaklı Arşiv** olarak kaldı; 182 tasarlanmadı.
