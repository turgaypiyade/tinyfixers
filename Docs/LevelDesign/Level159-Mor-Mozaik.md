# Level 159 — Mor Mozaik

`LevelCatalogPro` global 159 → `LevelP_001630.asset`, chapter 7. **9×11, 32 hamle, dört renk.** Beklenen zorluk: zor. [Yerleşim ve taban önizlemesi](Level159-Mor-Mozaik.png).

```text
XX.....XX
XX.ooo.XX
XX.ooo.XX
...ooo...
PPoogooPP
PPoooooPP
PPoogooPP
...ooo...
XX.ooo.XX
XX.EEo.XX
XX.....XX
```

`.` hole · `o` rastgele normal taş · `X` SculptingSpecial (39) · `P` SpreadingGel (46) → mor plastic (7) · `g` açık jel · `E` yardımcı EggBird (45).

| Hedef | Adet | Anlamı |
| --- | ---: | --- |
| SpreadingGel | 69 | Hole dışında bütün aktif hücrelerin kaplanması |
| SculptingSpecial | 24 | Dört köşe odasında altışar yalnız-special engeli |
| plastic | 12 | Ortadaki bandın iki ucunda bitişik 2×3 mor örtüler |

Soyut kompozisyon: ana eşleşme alanı artı dört ayrı **2×3 köşe odası**; toplam beş bağlı parça, 30 hole. Köşe odalarının ana alanla normal komşuluğu yoktur. Orta dikey koridor üç, geniş merkez bant beş normal hücre genişliğindedir. Pusulaların krem/gri çerçevesi, iki uçtaki mor plastik ve jel ile sakin bir renk düzeni kurar.

Köşe odaları normal eşleşmeyle hasar almayan engellerden oluşur. Merkezden yatay etkiler iç satırlara, yan bantlardan dikey etkiler en üst/en alt satırlara erişebilir. Hole'lar özel etkiler için engel duvarı olarak varsayılmaz; normal eşleşme de hole üzerinden yapılmaz. Oyuncu önce üretim alanını genişletip sonra special/combo etkisini doğru oda veya sütuna taşır.

Başlangıçta **iki açık jel kaynağı** (4,4) ve (4,6) vardır. On iki mor plastiğin altında da jel saklıdır; örtü kalkmadan bunlar görünür kaplamaya veya yayılma kaynağına sayılmaz. Sıvı `obstacles[]` tabanında, mor örtü `stackedObstacles[]` üstündedir. Jel kırma hedefi değildir: kaplama hedefi 14 başlangıç seed'iyle değil, sonradan açılacak engel hücreleri dahil **69 aktif hücreyle** hesaplanır. Bir odanın engellerini kırmak, o odanın kendiliğinden jellendiği anlamına gelmez; jel kaynağından gelen eşleşme/special etkisi ayrıca taşınmalıdır.

Alttaki dar koyda yan yana iki yumurta vardır; goal değildir. Kuşlar uygun hedefler arasından rastgele seçim yapar; belirli bir odayı vurma veya bütün board'u jel yapma garantisi tasarıma dahil edilmez. Tüm-board jel hedefi yumurtaların hücrelerini de kapsar; doğrudan goal olmamaları, bu hücreleri kaplama gereğini kaldırmaz.

Başlangıçtaki **31 normal taş hücresi** rastgele doldurulur; pin veya hazır special yoktur. Hole'ların dikey spawn geçirgenliği ve aktif köşelerde izinli diyagonal geçişler kullanılır. Hareketli mor plastikler/yumurtalar oyun sırasında yer değiştirebilir. İzole köşeler başlangıçta tamamen engellidir; içlerinde dolması beklenen normal taş cebi yoktur.

69 aktif hücre, 30 hole; yedi dizi 99 eleman. Statik kontrol hedefleri, katmanları, origin'leri, maskeyi, beş bağlı parçayı, 31 normal hücrenin geometrik spawn erişimini, kütüphane ve katalog bağlantısını doğrular. Normal başlangıç rastgele olduğundan sabit bir açılış takası sayısı verilmez.

Yeni runtime/VFX kodu veya kare başı tarama eklenmedi. Mevcut jel görünümü aynı hücreyi tekrar oluşturmaz ve board yeniden kurulduğunda `ClearAll` görünümleri/köşeleri temizleyip bekleyen border refresh'i iptal eder; görünür jel hücresi sayısı 69 ile sınırlıdır. Mevcut kuş hedef seçiminde hatch başına liste/küme tahsisi ve board taraması, uçuşta geçici nesne üretimi vardır; iki yumurta ile sınırlı tutuldu, bu çalışma bunları havuzlanmış gibi sunmaz. Kuş aksiyonu level değişimini kontrol eder, `finally` içinde uçuşu ve iş kilidini bırakır; uçuş sesi `OnDisable`'da kapanır. Profiler ölçümü yapılmadı.

**32 hamle ilk zor denge ayarıdır; kazanılabilirlik/kazanma oranı ölçülmedi.** Unity Play/build, bot veya oyun simülasyonu çalıştırılmadı. Önizleme oyun ekran görüntüsü değildir; normal taş renkleri temsili, gerçek başlangıç rastgeledir.
