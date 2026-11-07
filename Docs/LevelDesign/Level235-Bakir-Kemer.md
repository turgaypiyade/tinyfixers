# 235 · Bakır Kemer / Copper Arch — v2

**9×11 · 85 aktif hücre · 28 hamle ilk ayar · Hard · 4 renk.**

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002350.asset) · [Üç kesitli önizleme](Level235-Bakir-Kemer.png) · [Revizyon](Batches/235-Copper-Arch-v2/README.md)

Alt kenardan yükselen tek taş kemer kütlesi; iki brandalı heykel bu yapının ayaklarıdır. Üstte geniş bir çalışma avlusu vardır. Eski iki uzun, ince yan sütun kaldırıldı. Kemerin tepesi üç, altındaki sıra beş hücre; merkez ve alt bant heykelleri aynı kütleye bağlar. Başlangıç engelleri kenarla birleşen tek bağlı kompozisyondur.

**İlk karar:** Tepeden merkez açmak veya yan açıklıklardan heykel yüzüne special hazırlamak. Mavi/yeşil taşlar tahtanın her yerinden brandaları ilerletir. Specialı taşa kullanmakla açık heykelin aşamasına saklamak arasında zamanlama vardır; zorunlu tek sıra değildir.

**Katmanlar:** 19 oyma taşının altında 19 çamur; iki 2×2 heykelin altında sekiz çamur daha vardır. Sol heykel 14 mavi, sağ heykel 14 yeşil taş ister; brandadan sonra kişi başına üç special isabeti gerekir. Normal komşu eşleşme heykel aşamalarına vurmaz. Heykel kalkınca alttaki çamur ayrı zorunlu iştir. Son heykel bitmeden bütün çamur hedefi tamamlanamaz; aynı kombonun devamı açılan zemini de ilerletebilir. Ekstra oyuncu hamlesi kesinmiş gibi sayılmaz.

|Hedef|Miktar|
|---|---:|
|Örtülü heykel · 68|2|
|Oyma taşı · 28|19|
|Çamur · 25|27|

**Akış:** Açılış 58/58 normal yuva; oyma kaldırılırsa 77/77, heykeller kaldırılırsa 66/66, ikisi kaldırılırsa 85/85 statik besleme erişimi. Tek tek heykel açılması da kontrol edildi. Taş pini, hazır special ve başlangıç boş cebi yok. Statik erişim bütün dinamik düşüş sıralarının ölçümü değildir.

**Agent değerlendirmesi:** İlk U taslağı176/182 alt çerçevelerine yaklaştığı için uygulanmadı. Son düzen geniş üst alanı alttaki yoğun kütleye açar. 234’ün oyma→plastik→çamur üçlüsü tekrarlanmadı. Konumsal50/68/126 ve kırpılmış101/216/48 kısa listesi;2/176/182/205/233/234 ve eski235 gerçek sprite’larla incelendi. Alt kütle/basamak dili ortak olabilir; benzersizlik veya kullanıcı beğenisi iddiası yoktur.

**Denge:** Eski18hamle/7–13bot sonucu eski yerleşime aittir. Yeni28hamle, daha yoğun katman ve iki14’lük renk kotası için ilk ayardır. Yeni sürüm oynanmadı/simüle edilmedi; insan geçiş oranı veya%30hedefi doğrulanmış değildir.

YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/isim, statik akış ve gerçekPNG kontrolü geçti. İsimler, GUID/meta/katalog ve diğer6.985Assetsdosyası uygulama sonrası aynıydı. Mevcut LevelEditor Obstacle/Overlay ve heykel renk/sayı/boyut verileri kullanılır; Goals/PreLevel mevcut üç ikonla temsil edilir. Runtime/library/editor kodu değişmedi; manuel test kullanıcıdadır.

İki heykel korunur; yeni servis, ses kaynağı veya per-frame tarama yok. Artan renk kotası mevcut uçuş/efekt maliyetini artırabilir; uçuş başına managed nesne/coroutine maliyeti sürer. CPU/GC/FPS ölçülmedi. Build,Play,bot/kaskat simülasyonu,UI,yayın ve release baseline yapılmadı.
