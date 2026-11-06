# Üretim level tasarım ilkeleri

**Fikir önce gelir — kullanıcı, 2026-11-06:** “önce bir fikir belirlemen önemli ... önce tarzımı seçiyoruz”. İş sırası: seviyenin ana fikrini/tarzını, okunacak şekli ve ilk oyuncu kararını seçip paylaş; sonra bu fikri taşıyan engel ve katmanları yerleştir. Engel dizerek sonradan tema uydurma. Önceki seviyede beğenilen yaklaşım, sonraki seviyeye aynı şekli kopyalamak değildir.

**176 yaklaşımı onaylandı; 234 farklı tasarlanır — kullanıcı, 2026-11-06:** “çok iyi oldu ... biraz değiştirdim ama mantığını sevdim”. Beğenilen, belirli bir V şeklinin her levele kopyalanması değildir: yok olan engeller kenarlarda anlamlı kontur/bant oluşturur, maskeyle birlikte güzel görünür ve açıldıkça gerçek oyun alanı kazandırır. Bu, gereksiz engel tavanlarını ve altlarındaki boş cepleri de azaltır. Sonraki seviyenin silueti, ana mekaniği ve ilk kararı ayrıca seçilir. Kullanıcının elle ayarladığı176 korunur; agent’ın eski hamle/hedef modeline döndürülmez.

**Başlangıç taşı döşeme istisnası — kullanıcının son açıklaması, 2026-11-06:** Normalde taşları otomatik dolum yerleştirir. Yalnız oyunun açılışında doğal olarak hiç taş ulaşmayan normal yuvalara başlangıç taşı koymak uygundur; ekran gereksiz boş görünmesin. Bu istisna bütün tahtayı elle renklendirmek, hazır special zorlamak veya her boş görünen maskeyi taşla doldurmak değildir. Pin gerçekten başlangıçta kalabiliyor mu, altında destek var mı ve kapaklar açılınca bölge nasıl yeniden besleniyor ayrı kontrol edilir. İlk taş doluluğu ile kalıcı refill erişimi aynı iddia değildir.

**213–220 kullanıcı yönü · 2026-11-03:**

Sekiz seviye için fener/kuş evi/su kanalı/değirmen; porselen, iki aşamalı plastik, ColorChest, çiçekli çim ve renkli plastiklerle set boyunca dağıtılır. Kullanıcının örneklerini tek tahtaya yığma. Az/orta türde yelken gibi okunaklı yaratıcı siluet; çok engelli kompozisyonda daha düz maske seçilebilir. Ortaya açıklamasız tekil obje serpiştirme. Bu tur213–220 yetkilidir; önceki onaylı levellar korunur.

**Çim örtüsü tercihi — kullanıcı, 2026-11-03:** çim genellikle en üst katmanda kullanılır. Örneğin 212'de boya kasasının üstüne çim serilir; önce çim, sonra boya işi açılır. Çimi otomatik olarak katı kapağın altına saklama. Bu bir genel tercih; kullanıcı aksini açıkça isteyebilir. Onaylı eski levelları topluca yeniden katmanlama yetkisi değildir. 212'nin yelken silueti açıkça beğenildi ve korunur.

**2026-11-03 kullanıcı istisnası:** 211–220 arasında yeni engel tanıtılmaz; eski engeller ve katmanlı yapılar kullanılır. Görsel olarak güzel bir board ana öncelik; MetalWall/Wall örnekleri zorunlu tek şablon değildir. Bu onluk, genel 2–3 yeni engel tanıtımı kotasından muaftır. 211 ve ardından 212 ayrı taleplerle yetkilendirildi; 213–220 kendiliğinden tasarlanmaz.

**211 sonrası yöntem düzeltmesi:** kullanıcı Wall/MetalWall örneklerine fazla bağlanmamızı eleştirdi. Tasarıma bu isimleri ana obje kabul ederek başlama; açık mevcut ailelerin tamamını aday havuzu olarak değerlendir, birkaç farklı oyun fikri kur ve güzel kompozisyonu destekleyen aileleri seç. Aynı dar duvar/taş/dolap havuzuna dönmek çeşitlilik değildir. Hedef çok tür kullanmak değil; seçilen türlerin anlamlı etkileşimi, katman ilişkisi ve farklı bir açılış sağlamasıdır. Renk/aşama/yön varyantları ayrı yaratıcı mekanik fikirler sayılmaz.

## Güncel öncelik — 131–205 incelemesi · 2026-10-31

- Özellikle 150 sonrası normal levellar kullanıcı tarafından fazla kolay/heyecansız bulundu.
  Gereksiz geniş çalışma alanını, erken bitiren hedefleri ve açılış sonrası kalan işi incele.
  Aynı dar koridoru her levele uygulama; yalnız hamle kısmak çözüm değildir.
- Sonu 5/9 olanların zorluk yönü referanstır; sıradan seviyeler otomatik Hard yapılmaz.
- **Yardımcılar dahil tahtaya konan engel hedefte temsil edilir; en fazla 5 goal.**
  Bu, eski RocketBasket/OverrideBatteryBox/EggBird hedefsiz yardımcı tercihini geçersiz kılar.
  Fazla türü sadeleştir; kırılmaz üreticiyi kuş/anahtar/enerji hedefiyle temsil et.
- Boss'ta yalnız HP hedefi bırakma. Başlangıç engelleri ve sonradan atılan türler hedef listesiyle
  uyumlu olmalı; dinamik WaterPuddle/Mud hedefleri de slot hesabına katılmalı.
- Bu tur 131–205 kapsamıdır; 1–130 korunur. Ayrıntılar [Memory.md](Memory.md) ve
  [uygulanan inceleme kaydında](Batches/131-205-Review-2026-10-31/Review.md).

Kullanıcının 107 tasarımı için verdiği yönlendirmeler; sonraki seviyelerde de esas alınır.

## Yeni başlangıç akışı — ProLevels V1 · 2026-10-28

- Kullanıcı zorlukları yeniden düzenliyor; güncel başlangıç tasarımları
  `Assets/_Project/Settings/ProLevels/LevelV1_*.asset` ve aynı klasördeki
  `LevelCatalogProV1.asset` üzerinden ilerler. Gerçek sıra bu katalogdan çözülür.
  `ProductionLevels/LevelP_*` ve eski katalog tarihsel referanstır; otomatik değiştirilmez.
- Her onluk sette **en az 2, en fazla 3 yeni obstacle tanıtılır**. Bu yeni akış için açık
  kullanıcı kuralıdır; aşağıdaki eski "kesin kota değil" notunun önündedir. Sandık aşaması,
  renk varyantı ve mekanik ailesi envanterde ayrı tutulur; birbirinin yerine sayılmaz.
- Sonu **5: Zor**, sonu **9: Daha Zor / Super Hard**, **10'un katları: Boss Duel**.
- **Onluk zorluk eğrisi (kullanıcı, 2026-10-31):** Sonu **1 ve 6** genelde yeni engel tanıtımıdır → bilerek
  kolay/rahat. **2→3→4 artan** zorlukla **5 Hard**'a çıkar; **6 rahatlama (tanıtım)**, **7→8 artan**,
  **9 Super Hard**; **10'un katları Boss**. Bir seviyenin "kolay" çıkması tek başına kusur değildir —
  önce onluktaki yerine bak (1/6 kolay olmalı; 5/9/boss kolaysa ve 7/8 6'dan kolaysa sorun).
  **Öncelik (kullanıcı):** Eğrinin tamamı yaklaşık tutulsa yeterli; MUTLAKA tutturulacak iki uç:
  **1/6 yeni engel tanıtımı rahat**, **5/9 gerçekten zor**. Ara seviyelerde küçük sapma kabul edilir.
  Yeni engelin tanıtımı anlaşılır/rahat tutulur; Boss board yüksekliği en fazla 10'dur.
- Sıradaki **12, 13, 14 Normal: hedef geçiş %55–85; 15 Zor: %35–55**.
  Bunlar başlangıç levellarıdır. 170 sonrası Normal için kayıtlı %40–50 yönü bu gruba
  uygulanmaz. Verilen yüzdeler tasarım hedefidir; oynanmamış asset ölçülmüş dengeli sayılmaz.
- 12–15'in izinli havuzu yalnız **Sandık 1/2/3 aşama (ID4/5/6), Çamur25, Yağ14**.
  Sonraki levellarda görünen plastik veya başka engeller bu gruba erkenden getirilmez.
- Normal seviyelerde rahat eşleşme/dolum alanı, okunaklı tek ana fikir ve kademeli pekiştirme
  korunur. 15'in zorluğu başlangıç dönemine göre yük, katman ve hamleyle ayarlanır;
  yalnız hamleyi kısmak veya geç dönem karmaşıklığını taşımak çözüm değildir.
- Kullanıcı önce kontrol, ardından **yalnız11–15 grubunu yapma** istedi.11 mevcut yağ
  tanıtımı olarak korundu;12–15 başlangıç taslakları `Batches/ProLevels-V1-011-015/` altında.
  Mevcut diğer levellar korunur;16 ve sonrası bu talebin kapsamında değildir.

## Önceki üretim akışından korunan ilkeler

- **202–205 düzeltmesi (2026-10-31):** Kullanıcının201 için istediği50kuş
  sayısı bütün yeni seviyelerin taban hedefi değildir. Sayısal değişiklik
  açık istenen level kapsamında kalır. Hedefler erişilebilir üretici yüzü,
  diğer hedef yükü ve çalışma ritmiyle seçilir. Aynı geniş dikdörtgen + birkaç
  kenar grubu,engel adları değişse de tekdüze bulunabilir;ilk karar ve açılan
  yeni iş de çeşitlenmelidir. Bu,her seviyeye hole veya dar boğaz ekleme
  zorunluluğu değildir. Güncel BirdHouse kodu,tasarımdan önce tekrar okunur.

- **Başlangıç taslakları için 2026-10-28 düzeltmesi:** Basit/Normal levelin de görünür bir şekli,
  ilk kararı ve açılma olayı olmalı. Büyük açık dikdörtgen + kenar U/L/alt sıra, bunlar oyuncu
  kararına hizmet etmiyorsa tek başına yeterli kompozisyon değildir. Açık taş sayısına sabit bir
  minimum koymak kullanıcı kuralı değildir. Tasarıma göre kapak arkasındaki alanın açılışta
  gerçekten boş kalması, sonradan dolması veya hazırlanmış special ile ilk bölgenin açılması
  kullanılabilir; refill kısıtı açıkça doğrulanır ve gerçek önizlemede saklanmaz. Bu, bütün
  başlangıç levellarına hazır special veya aynı açılış numarasını koyma zorunluluğu değildir.

- **Yeni tasarımlar için katman kuralı (155 isteği):** Mud ve Oil dahil sıvılar en alttadır. Sandık/taş gibi katı engeller sıvının üstüne yerleştirilir; Oil sandığın üstünde kullanılmaz. Sıvı `obstacles[]` tabanı, katı örtü `stackedObstacles[]` üst katmanı olur. Eski level kayıtları bu yeni tasarım talebiyle otomatik değiştirilmez.
- Yalnızca oyuncunun o seviyeye kadar gördüğü engeller kullanılabilir. Asset numarası yerine `LevelCatalogPro` sırası kontrol edilir.
- Engel seçimi son tanıtılanlarla sınırlı değildir. O ana kadar açılmış tüm engeller dönem dönem yeniden kullanılır; her levelde son çıkan engeli kullanmak zorunlu değildir. Amaç seviyeler boyunca çeşitlilik sağlamak, arada yeni engeller tanıtarak yeni heyecan yaratmaktır.
- **Güncel seçim (2026-10-20):** Kullanıcı yeni renkli sandıkları şimdilik kullanmamayı istedi. Sonraki tasarımlarda ColorCrate 71–74 dinlendirilir; eski 2×2 ColorChest 21 ve Wardrobe 27 kullanılabilir. Eski levellar değiştirilmez. Tekrar kontrolü yalnız engel ID'siyle yapılmaz: siluet, engel kütlesinin yönü, ilk açılan bölge ve katman sonrası karar da karşılaştırılır. Aynı kanat/orta koridora farklı engel koymak yeterli yenilik sayılmaz. Her leveli çamur tabanlı yapmak da yeni bir varsayılan değildir.
- **181 açıklaması (2026-10-20):** Kullanıcı şekle çok itirazı olmadığını, asıl sorunun bu level için kolaylık olduğunu belirtti. Benzer şekli otomatik ret sayma; görsel tekrar, oynanış tekrarı ve zorluk geri bildirimini ayrı değerlendir. Revizyonda geniş alanın ürettiği special rahatlığı, zorunlu hedef/renk işi ve örtüden sonra kalan yük incelenir. Hamleyi azaltmak tek başına çözüm sayılmaz; son geri bildirimin önceliği korunur. Bu, bütün normal levelları Hard yapma veya sürekli daha dar tahta kullanma talebi değildir.
- **171 revizyon geri bildirimi:** Eski sandık/çamur/fener/heykel kanatlarını tekrarlamak sıradan bulundu. Yeni engellerin farklı davranışları kompozisyonun ve oyuncu kararlarının merkezine alınmalı; renkli sandıklar yalnız eski sandıkların görsel değişimi olarak kullanılmamalı. Eski engellerle çeşitlilik ilkesi korunur, ancak yeni içerik havuzu tasarımdan önce güncel kod/kütüphaneden kontrol edilir.
- İlk bakışta okunabilen güzel bir kompozisyon hedeflenir: altıgen, oval, uçurtma, simetrik şekiller veya belirgin renk blokları. Verilen şekiller örnektir, zorunlu şablon değildir.
- **Sandık kompozisyonu (171 görsel geri bildirimi):** Tahta ortasındaki bağımsız sandık adacıkları tercih edilmez. Sandıklar bir sütunu, kenarı, çerçeveyi veya tanınabilir şekli tamamlamalı; hole sınırı, iç boşluk ve diğer engel bölgeleriyle birlikte tek bir kompozisyon oluşturmalı. Kullanıcının iki referansındaki gibi kesintisiz şeritler, bir boşluğu çevreleyen konturlar ve iç içe renk bantları tercih edilir. Bu, her leveli aynı çerçeveye çevirmek veya her levelde hole kullanmak zorunluluğu değildir.
- **182 kandil geri bildirimi:** Parçalı kandil/dama yerine açılışta kesintisiz tek kandil sütunu ve hemen yanında toplu diğer engel bölgesi tercih edildi. Beğenilen board siluetini koru. Bu tercih bütün engellerin dama düzenini yasaklamaz veya sonraki her leveli aynı sütuna zorlamaz; hareketli engellerin açılış düzeni oyun boyunca sabit kalmaz.
- Hole/board maskesi silueti destekleyebilir. Tam dikdörtgen board üzerinde de engel dizilimi tek başına güzel bir desen oluşturmalıdır.
- **Güncel zorluk yönü (161/162 isteği, 2026-10-18):** Oyuncuyu zorlamak için tahtayı dar/erişilmesi zor bir şekle sokmak yerine geniş eşleşme alanını, bol kırılma ve patlama akışını öne çıkar. Zorluk yüksek engel/katman yükü ve/veya düşük hamle bütçesinden gelebilir. Hole, adacık veya dar boğaz zorunlu değildir; tam dikdörtgen tahta da tercih edilir. Bu yön, eski referanstaki "hole silueti zorunlu" önerisinin önündedir. **Yeni engelin ilk tanıtımı kolay tutulur:** 161 Rodos tanıtımında düşük yük/rahat hamle; yoğun yük ve sıkı bütçe 162'de başlar. Önceki seviyeler bu istek nedeniyle değiştirilmez.
- Engel yoğun tasarımlarda 9×10 veya 9×11 gibi geniş alanlar tercih edilir.
- **Ufak tekil yerleşim yok (kullanıcı, 2026-10-31):** Büyük tahtada 1, 2 ya da 3 hücrelik tek başına engel
  (ör. tek hücre RocketBasket) şekli bozar, unutulmuş parça gibi durur. Engeller **en az 2×2 küme** (ya da
  kompozisyonu tamamlayan uzun kesintisiz bant/sütun) olarak konur. Yardımcı engeller dahil.
  Çoğu zaman **dikdörtgen bloklar daha iyi durur: 2×3, 2×4** — yatay ya da dikey (kullanıcı ek notu).
- Tek hücreli engeller bitişik gruplar, satırlar, sütunlar ve dikdörtgenler halinde yerleştirilir; renkler bölgesel olarak uyumlu tutulur. Plastic, PlasticTwoStage ve HelmetPorcelain aynı renk ailesi olarak birlikte değerlendirilebilir.
- Eski üretim akışında her onluk normal level grubunda genellikle ilk beşte bir, ikinci beşte başka bir engel tanıtılıyordu; yeni V1 akışında yukarıdaki **2–3 tanıtım** kuralı önceliklidir. Yeni engeller, kompozisyona yakışan eski engellerle birlikte kullanılır.
- 101–109 dönemi için Wall (ilk 101) ve GrassFlower (ilk 105) mevcut yeni engellerdir.
- Açılacak bölgeyi veya odaklanılacak engeli seçme gibi puzzle kararları desteklenebilir.
- Engellerin hepsi yalnızca zorluk kaynağı değildir. RocketBasket, OverrideBatteryBox ve Egg (kodda EggBird) oyuncuya yardım eden öğeler olarak da değerlendirilir. Özellikle zor levellarda bunların faydasını açığa çıkaran yerleşimler kullanılarak oyuncuya destek ve rahatlama fırsatı sağlanır; her levelde bulunmaları zorunlu değildir. Bunlar da ancak tanıtıldıkları seviyeden itibaren kullanılabilir.
- **2026-10-31 güncellemesi:** RocketBasket, OverrideBatteryBox ve EggBird de yerleştirildiyse uygun temizleme hedefiyle temsil edilir; toplam goal sayısı 5'i aşmaz. Eski hedefsiz yardımcı tercihi tarihsel kalmıştır.
- Yardımcı engeller uçlara tek tek dağıtılmak yerine, uygun olduğunda yan yana ve toplu bir görsel bölge halinde yerleştirilir.
- İleride eklenecek yeni engeller, zorluk ve yardım işlevleriyle birlikte değerlendirilerek tanıtım sıralarına uygun biçimde tasarım havuzuna katılır.
- 10, 20, 30… seviyeler Boss Duel olarak ayrılır. **Boss Duel board yüksekliği en fazla 10 satırdır**; 11 satır olduğunda karakter ekrana sığmaz. Normal seviyelerdeki 9×11 tercihi boss seviyelerine uygulanmaz.
- **179 oynanış düzeltmesi (2026-10-21):** “Super Hard” etiketi geçilemeyen yükü meşrulaştırmaz. Kullanıcının bitirişte eksik kalan hamle/katman geri bildirimine göre yerel düzeltme yapılır; 179 bu tur +5 hamle aldı. Bu, bütün Super Hard levellara otomatik hamle ekleme kuralı veya ölçülmüş kazanılabilirlik değildir.
- Hamle sayısı ilk tahmindir; kullanıcı oynayarak ayarlar. Unity simülasyonu/bot çalıştırılmaz. Asset tutarlılığı ve açılış dizilimi statik kontrol edilebilir.
- **170 sonrası zorluk yönü (171 isteği, 2026-10-19):** Normal levellar da çok kolay tutulmaz; geçiş oranı için %40–50 bandına doğru ilerlenir. Sonu 5 ile bitenler **Hard**, sonu 9 ile bitenler **Super Hard** olarak tasarlanır; 10'un katlarındaki Boss Duel düzeni korunur. Bu bir tasarım/denge hedefidir, ölçülmüş kazanma oranı değildir. İlk hamle bütçesi, engel/katman yükü ve yardım miktarı birlikte ayarlanır; gerçek oran oynanış verisiyle doğrulanır. Önceki levellar otomatik değiştirilmez.
- Taşları elle döşemek/sabitlemek ile otomatik akışa bırakmak tasarıma göre seçilen iki araçtır; biri her level için varsayılan veya zorunlu değildir. Hangisi görseli ve oynanışı iyileştiriyorsa o kullanılır, gerektiğinde birlikte uygulanır. Yukarıdan taş akışı engellenmediyse sistem taşları otomatik düşürür/doldurur. Akışı engellenen bölgeler ayrıca değerlendirilir. Board maskesi/hole tanımı ile başlangıç taşı ve renk sabitlemesi ayrı kararlardır; otomatik taş beklenen oynanabilir hücre yanlışlıkla hole yapılmaz.
- Zor bir açılışta ekran neredeyse tamamen engellerle kapatılabilir: normal match alanı yerine birkaç hazır special, zincirleme tetikleme veya special combo ilk örtüyü açar; alttaki zor hedeflerle oyun devam eder. LevelP_00930 bu yaklaşımın mevcut örneğidir. Pulse şart değildir; Override çifti, LineH/LineV zinciri gibi seçenekler tasarıma göre seçilir. Açılış gösterisi levelin asıl zorluğunu tüketmemelidir.

Her seviye sonrasında kullanıcıdan gelen görsel ve oynanış geri bildirimiyle bu ilkeler geliştirilir.

**Kalıcı öğrenme kaydı (2026-10-19):** Kullanıcı 1–170 dönemini görsel olarak başarılı buluyor; bu havuz ana yerel referanstır. Yeni gönderilen dış oyun görselleri, açıkça aksi istenmedikçe öğrenme/analiz içindir; aktif leveli hemen o görsele göre değiştirme talebi değildir. Örneklerin gerekçeleri ve olumsuz karşı örnekler `Design-Feedback.md`, kalıcı görseller `References/Index.md`, kaldığımız yer `Design-State.md` içindedir. Onaylanan 171 korunur. Dış referanstaki sandık veya başka bir engel türü zorunlu değildir; grid ilkesi farklı ailelere uygulanabilir.

Engel seçmeden önce [engel tasarım rehberi](Obstacle-Design-Guide.md) ve gerektiğinde ilgili güncel servis incelenir. Jel gibi kaplama hedefleri, kırılma hedeflerinden ayrı değerlendirilir; tüm board hedefinde sonradan açılacak engel hücreleri de kapsanır.

108 sonrası geri bildirim: kullanıcı leveli kolay geçti; EggBird ve RocketBasket yardımı belirgin biçimde kolaylaştırdı. 108 değiştirilmez. Sonraki tasarımlarda yardım yoğunluğu ve hedef yükü birlikte ayarlanır; zorluk yalnız hamle sayısından ibaret düşünülmez. Tam 9×11 dış çerçeve içinde hole şeritleri ve toplu engel desenleri de denenir.

151 sonrası geri bildirim: ilk tasarım kolay bulundu. Ayrık nilüfer çiftleri iki kesintisiz sütuna birleştirildi; hazır special ve sabit başlangıç renkleri kaldırıldı. Level tasarlarken taşları elle yerleştirmek zorunlu değildir; bu tür kompozisyonlarda engelleri tasarlayıp taş üretimini oyunun rastgele dolumuna bırakmak uygundur.

113–116 sonrası geri bildirim: çiçek/çim çok kullanıldı; sonraki levellarda başka örtü ve katmanlar (çamur, jel, taş) öne çıkarılır. Overlay/katmanlı engeller ekranı hem şık hem zor yapar; zor levellarda 3 katmanlı yığınlar kullanılabilir.

MetalWall için: hamlede vurulmayan hücre geri gittiği için duvara ilk hamlelerde kolay ulaşılmamalı. Örnek: sağa dayalı 2 sütun genişliğinde dikey bir duvar şeridi ve solunda Wall, SculptingStone ya da 3 aşamalı bir engelle korunması. Tek parça büyük blok kolaylaştırır; parçalı (çok sayıda küçük) duvar blokları zorlaştırır.

## Oyun teması (kullanıcı notu)

Robot konsepti uzun süre önce bırakıldı. Güncel tema: **tamirci hayvanlar Dünya Harikalarını (World Wonders: Golden Gate, Büyük Kanyon, Piramitler, Eyfel, Kolezyum vb.) tamir ediyor ve inşa ediyor.** Yeni engel, görsel ve level fikirleri bu temaya göre önerilir; "robot" ya da mitoloji/efsane temalı öneri yapılmaz (eski asset adlarındaki RobotStyle klasörü yalnız tarihsel isimdir).

Engel tasarımında oyuncunun ilerlemeyi görerek sayabilmesi önemlidir: ColorChest'te kapak açılınca ampullerin görünmesi gibi, içerik/kalan adım doğrudan görünür olmalı (ör. önden arkaya dizili eşyalar, silüetli eksik parçalar).
