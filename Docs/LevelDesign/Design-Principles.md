# Üretim level tasarım ilkeleri

Kullanıcının 107 tasarımı için verdiği yönlendirmeler; sonraki seviyelerde de esas alınır.

- Yalnızca oyuncunun o seviyeye kadar gördüğü engeller kullanılabilir. Asset numarası yerine `LevelCatalogPro` sırası kontrol edilir.
- Engel seçimi son tanıtılanlarla sınırlı değildir. O ana kadar açılmış tüm engeller dönem dönem yeniden kullanılır; her levelde son çıkan engeli kullanmak zorunlu değildir. Amaç seviyeler boyunca çeşitlilik sağlamak, arada yeni engeller tanıtarak yeni heyecan yaratmaktır.
- İlk bakışta okunabilen güzel bir kompozisyon hedeflenir: altıgen, oval, uçurtma, simetrik şekiller veya belirgin renk blokları. Verilen şekiller örnektir, zorunlu şablon değildir.
- Hole/board maskesi silueti destekleyebilir. Tam dikdörtgen board üzerinde de engel dizilimi tek başına güzel bir desen oluşturmalıdır.
- Engel yoğun tasarımlarda 9×10 veya 9×11 gibi geniş alanlar tercih edilir.
- Tek hücreli engeller bitişik gruplar, satırlar, sütunlar ve dikdörtgenler halinde yerleştirilir; renkler bölgesel olarak uyumlu tutulur. Plastic, PlasticTwoStage ve HelmetPorcelain aynı renk ailesi olarak birlikte değerlendirilebilir.
- Her onluk normal level grubunda genellikle ilk beşte bir, ikinci beşte başka bir engel tanıtılır; bu kesin bir kota değildir. Yeni engeller, kompozisyona yakışan eski engellerle birlikte kullanılır.
- 101–109 dönemi için Wall (ilk 101) ve GrassFlower (ilk 105) mevcut yeni engellerdir.
- Açılacak bölgeyi veya odaklanılacak engeli seçme gibi puzzle kararları desteklenebilir.
- Engellerin hepsi yalnızca zorluk kaynağı değildir. RocketBasket, OverrideBatteryBox ve Egg (kodda EggBird) oyuncuya yardım eden öğeler olarak da değerlendirilir. Özellikle zor levellarda bunların faydasını açığa çıkaran yerleşimler kullanılarak oyuncuya destek ve rahatlama fırsatı sağlanır; her levelde bulunmaları zorunlu değildir. Bunlar da ancak tanıtıldıkları seviyeden itibaren kullanılabilir.
- RocketBasket ve OverrideBatteryBox doğrudan level hedefine eklenmez; yardımcıdır. EggBird yardımcı rolündeyse sırf board'a konduğu için hedef yapılmaz, bütün yumurtalar kırılmadan da level bitebilir. EggBird'ün ayrıca temizleme hedefi olması ancak bilinçli bir tasarım kararıdır.
- Yardımcı engeller uçlara tek tek dağıtılmak yerine, uygun olduğunda yan yana ve toplu bir görsel bölge halinde yerleştirilir.
- İleride eklenecek yeni engeller, zorluk ve yardım işlevleriyle birlikte değerlendirilerek tanıtım sıralarına uygun biçimde tasarım havuzuna katılır.
- 10, 20, 30… seviyeler Boss Duel olarak ayrılır. Şu anki görev kapsamında boss tasarlanmaz.
- Hamle sayısı ilk tahmindir; kullanıcı oynayarak ayarlar. Unity simülasyonu/bot çalıştırılmaz. Asset tutarlılığı ve açılış dizilimi statik kontrol edilebilir.
- Taşları elle döşemek/sabitlemek ile otomatik akışa bırakmak tasarıma göre seçilen iki araçtır; biri her level için varsayılan veya zorunlu değildir. Hangisi görseli ve oynanışı iyileştiriyorsa o kullanılır, gerektiğinde birlikte uygulanır. Yukarıdan taş akışı engellenmediyse sistem taşları otomatik düşürür/doldurur. Akışı engellenen bölgeler ayrıca değerlendirilir. Board maskesi/hole tanımı ile başlangıç taşı ve renk sabitlemesi ayrı kararlardır; otomatik taş beklenen oynanabilir hücre yanlışlıkla hole yapılmaz.
- Zor bir açılışta ekran neredeyse tamamen engellerle kapatılabilir: normal match alanı yerine birkaç hazır special, zincirleme tetikleme veya special combo ilk örtüyü açar; alttaki zor hedeflerle oyun devam eder. LevelP_00930 bu yaklaşımın mevcut örneğidir. Pulse şart değildir; Override çifti, LineH/LineV zinciri gibi seçenekler tasarıma göre seçilir. Açılış gösterisi levelin asıl zorluğunu tüketmemelidir.

Her seviye sonrasında kullanıcıdan gelen görsel ve oynanış geri bildirimiyle bu ilkeler geliştirilir.

Engel seçmeden önce [engel tasarım rehberi](Obstacle-Design-Guide.md) ve gerektiğinde ilgili güncel servis incelenir. Jel gibi kaplama hedefleri, kırılma hedeflerinden ayrı değerlendirilir; tüm board hedefinde sonradan açılacak engel hücreleri de kapsanır.

108 sonrası geri bildirim: kullanıcı leveli kolay geçti; EggBird ve RocketBasket yardımı belirgin biçimde kolaylaştırdı. 108 değiştirilmez. Sonraki tasarımlarda yardım yoğunluğu ve hedef yükü birlikte ayarlanır; zorluk yalnız hamle sayısından ibaret düşünülmez. Tam 9×11 dış çerçeve içinde hole şeritleri ve toplu engel desenleri de denenir.
