# Level 148 — Çiçekli Yonca

Asset: `LevelP_001520.asset` → `LevelCatalogPro` `level: 148` (chapter 6, `LevelCL_0148`).
Normal level, **9×11**, **77 aktif hücre / 22 hole**, **32 hamle**, dört renk.
Kullanıcı isteği: Wardrobe, PaintCanBox, GrassFlower ve TarpStatue; güzel yerleşim ve şekilli board.
Hedef zorluk normal–zor; hamle ilk tahmindir, oynanarak doğrulanmadı.

## Kompozisyon

Dört loblu yonca: üst/alt çentikler ve beldeki daralma hole'larla çizilir.
24 GrassFlower hücresi lobların dışını kesintili bir yeşil/beyaz/lila çerçeveyle sarar.
Üst loblarda iki yeşil Wardrobe, alt loblarda iki kırmızı çerçeveli PaintCanBox vardır.
Merkezde turuncu çerçeveli, mavi brandalı **3×3 tek TarpStatue**; yeşil etiket doğa paletini bağlar.
Maske ve engeller sağ-sol ve üst-alt simetrik konumludur; üst-alt engel türleri bilerek farklıdır.

```text
..FF.FF..
.FoooooF.
FWWoooWWF
FWWoooWWF
.FoTTToF.
..oTTTo..
.FoTTToF.
FPPoooPPF
FPPoooPPF
.FoooooF.
..FF.FF..
```

`.` hole · `o` açık normal taş · `F` GrassFlower (58) · `W` Wardrobe (27, 2×2)
· `P` PaintCanBox (63, 2×2) · `T` TarpStatue (68, 3×3).
Koordinatlar sıfır tabanlı, y üstten aşağıdır.

Önizleme: `Level148-Cicekli-Yonca.png`. Mevcut sprite'larla yerleşim şemasıdır, oyun ekran görüntüsü değildir.
Heykel önizlemesi mevcut Background/Tarp/Frame/Tag_Green katmanlarından çizilir; sayaç 24'tür.
Runtime çim birleşimleri, çiçek dağılımı ve kutu/makine animasyonları farklı olabilir.

## Hedefler

| Hedef | Adet | Yerleşim / davranış |
|---|---:|---|
| Wardrobe | 2 | Origin 19 ve 24. Kapak açıldıktan sonra içerik çıkarılır; güncel library'de 10 öğe, normal vuruşta 1 / special vuruşunda 2 öğe. |
| PaintCanBox | 2 | Origin 64 ve 69. Kasa başına 9 boya kutusu; normal vuruşta 1 / special vuruşunda 2 kutu. İkisi aynı satır bandında, mevcut renk/görsel seçimi aynı. |
| Grass | 24 | Board'da 24 GrassFlower vardır. **Çiçeklerin dökülmesi yetmez; alttaki çim de temizlenir.** Hedef ID 42, authored ID 58'dir. |
| TarpStatue | 1 | Origin 39, boyut 3×3. Önce tahtanın herhangi bir yerinden **24 yeşil Plate**, ardından **3 special vuruşu**. |

Yalnız istenen dört authored engel türü vardır. Ek katman veya yardımcı eklenmez.
Heykel çimle örtülmez; yeşil toplaması ilk hamleden itibaren açıktır.
Dört renk havuzu korunur; sayaç tamamlandığında normal eşleşme taş evresini ilerletmez.

## Oynanış ve dolum

Üst loblarda dolapları, alt loblarda boya kasalarını açma arasında tercih vardır.
Lobların üç sütunluk iç alanlarında yatay eşleşmeler iki yandaki engeli aynı anda aşındırabilir.
Kutular temizlendikçe dış çiçeklere normal eşleşmelerle erişilir; kenar çiçekleri başlangıçta kutular ve
hole'lar yüzünden doğrudan komşu eşleşmeye kapalı olabilir. Special'lar bu erişimi erken sağlayabilir.
Bu işler sürerken yeşil taşlar merkez heykelin brandasını kaldırır; sonra PatchBot/line/combo heykeli tamamlar.

28 açık normal hücre ve çiçek altındaki 24 hücrede başlangıç taş renkleri sabittir (52 pinned taş).
Çiçek altı taşlar başlangıçta takas/eşleşmeye kilitlidir; çim düşüşü kesmez.
Refill dört renk havuzundan rastgele gelir; hazır special yoktur.
`(4,1) ↔ (4,2)` dört yeşil; `(4,8) ↔ (4,9)` dört sarı taşlık eşleşme fırsatıdır.
Açık normal taşlarla **13 geçerli başlangıç takası** vardır: üstte 8, altta 5.
Başlangıçta otomatik üçlü veya aynı renk 2×2 yoktur; örtü açıldıktan sonraki zincirler ölçülmedi.

Heykel yanındaki iki dikey şerit, üst dolapların altındadır; açık merkezden çapraz dolumla beslenir.
Alt oyun alanının iki yanı bu şeritlerden çapraz erişim alır. Yalnız `(4,7)` normal hücresi, 3×3 heykelin
hemen altında başlangıçta dolum gölgesindedir: pinned taşla başlar; boşalınca heykel kırılana kadar
yeniden dolmaz. Heykelin kalkması merkez sütunu ve bu cebi açar. Bu bilinçli bir bölge açma adımıdır.
Hole'lar düşüş/spawn için geçirgendir; kalıcı üretici engel yoktur.

### Diyagonal dolum geri bildirimi

İlk oynanış snapshot'ında yan şeritler ve alt bölge boş kaldı. Önceki statik erişim kontrolü,
çapraz geçişteki iki köşe koşulunu atladığı için dolumu yanlış değerlendirmişti: Wardrobe,
PaintCanBox ve TarpStatue bütün aşamalarda `allowDiagonal = 0` durumundaydı.
Kullanıcı şeklin korunmasını ve mevcut köşe geçiş ayarının açılmasını istedi.
Üç engelin tüm stage'leri ortak `ObstacleLibrary` içinde **`allowDiagonal = 1`** yapıldı;
level maskesi, engel yerleri, hedefler ve hamle sayısı değiştirilmedi.
Bu ayarla dolap/heykel ve heykel/boya kasası arasındaki çapraz girişler açılır.
`(4,7)` hücresinin üst ve çapraz kaynak hücreleri heykelin içinde olduğundan bu tek cep
heykel kırılana kadar hâlâ yeniden dolmaz; `allowDiagonal` engel hücresini taş yuvasına çevirmez.
Güncel stage ayarları ve gerçek köşe kuralıyla statik erişim tekrar kontrol edildi; canlı oyun testi yapılmadı.

## Doğrulama ve performans sınırı

99 elemanlı diziler, hole'larda boş içerik, bağlı/simetrik maske, dört 2×2 ayak izi, tek 3×3 heykel,
origin'ler, 24 çiçekli çim, heykelin renk/sayı/boyut kaydı, dört hedef, tanıtım sırası, açılış eşleşmeleri
ve katalog/GUID kontrol edildi. Statik dolum erişiminde tek heykel-altı cep belirlendi; heykel kalkınca
erişim açılır. Bu kontrol runtime gravity zamanlaması veya kazanılabilirlik ölçümü değildir.
Katalog yalnız 148 eklenerek güncellendi; önceki production level asset'leri değişmedi.

Yeni gameplay/VFX kodu yazılmaz. Heykel toplama yükü tek heykel ve 24 yeşil taşla sınırlıdır;
mevcut TarpStatueService ve AncientSealCollectFx kullanılır. Kaynak incelemesinde uçuş başına mevcut
nesne oluşturma/Dispose yolu görülür, havuzlu olduğu varsayılmaz. TarpStatueService.Clear/OnDestroy,
CancelFlights ve ReleaseFlight; uçuşları durdurma, görselleri ve job'ları bırakma yollarını içerir.
Bu kaynak incelemesidir; canlı sahne çıkışı, nesne sayısı ve kare süresi ölçülmedi.

Unity, bot, oynanış simülasyonu ve build çalıştırılmadı. İlk testte 32 hamle, kenar çiçeklerinin erişimi,
24 yeşil taşın toplama süresi ve heykel altındaki geçici dolum cebi değerlendirilir.
