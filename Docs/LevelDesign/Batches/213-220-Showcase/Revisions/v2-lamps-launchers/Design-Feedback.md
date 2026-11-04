# Tasarım geri bildirimi

## 213–220 · yaratıcı şekiller / dağıtılmış engeller · 2026-11-03

- **Kullanıcı:** “8 tane daha seviye yap”, “fener, bir home, water channel, yel değirmeni ... helmet porcealen, plastic two obstacle, color chest, grass with flower, plastic_xxx ... hepsini bir anda değil uygun şekilde dağıt”, “boardun ortasına ... tek başına duran objeler koyma”, “yelken gibi yaratıcı”, “çok engel koyduysa ... daha düz bir board”.
- **Yetki/tercih:** Aktif V1 devamı213–220 açıkça yetkilendirildi. Yeni aile tanıtımı gerekmiyor; mevcut türler set içinde dağıtılır. Okunaklı, güzel siluet; anlamlı blok/bant/sütun/kenar konturu, yoğun işte daha sade maske. Ortada rastgele tekil obje yok.
- **Ajan uygulaması (onay değildir):** Balon/çan/vitray/lale/kum saati/kıvrılan nehir/taç/festival.215Hard,219SuperHard,220Boss10satır. Kuş evleri kenarda, fenerler sütunda, kanallar alt çıkışlı;219Grass sandıkların üstünde.2976 önceki dosya hash ile aynı.
- **Durum/sınır:** Sekiz taslak statik/görsel olarak incelendi; kullanıcı tarafından henüz oynanıp onaylanmadı.221 veya önceki levelları yeniden tasarlama yetkisi yok.

## 212 yelken beğenisi / boya altta, çim üstte · 2026-11-03

- **Kullanıcı:** “ben genelde çimi en üstte kullanıyorum ... yelken şekli çok güzel ... paintboxları en alta koyalım ... kasa kalabilir ... otlar ... paintboxların üstünde dursun”.
- **Kabul edilen:** yelkenin görsel silueti beğenildi; maske aynen korunur. Çim için genel yön üst örtüdür; Paint→Grass katmanı bu level için açık taleptir. Önceki Grass→Paint tasarımı bu kapsamda geçersiz. Kasa korunabilir ve aynı yerde bırakıldı.
- **Uygulama:** iki2×2 Paint, onaylı maskede birlikte sığabildikleri en alt banda (origin74/76) taşındı; üstte8 ayrı Grass. İki Tube üst banda alındı; 24hamle/hedefler/Safe/Cargo/isim/katalog/GUID aynı. Açılış dolumu35 doğal+6 gerçek renk pini; maskede hiçbir hücre değişmedi.
- **Ders/sınır:** katman çeşitliliğini çimi otomatik olarak alta koymakla kurma; kullanıcı özel ters sıralama istemedikçe önce üstte kullanımını değerlendir. Bu kayıt eski levelları topluca değiştirme yetkisi değildir. Siluet için kullanıcı beğenisi vardır; v2 yerleşimi/oynanış dengesi henüz kullanıcı tarafından denenip onaylanmadı. 213 istenmedi.

## 212 deneme yetkisi ve 211 kullanıcı hamlesi · 2026-11-03

- **Kullanıcı:** “212 yi deneyelim”. Engel örneğine saplanma eleştirisinin ardından yalnız bir sonraki leveli tasarlama yetkisi; hazır tasarıma onay değildir.
- **Ajan uygulaması:** geniş havuz ve üç oyun fikri değerlendirildi; yelkenli şekli içinde Safe→Cargo, PaintCanBox→Grass ve küçülen Tube→kargo çıkışı ilişkileri seçildi. 24 hamle ilk ayar. 212 not/PNG/asset/katalog/TR+EN ad ve statik kontrol hazır; kullanıcı henüz oynamadı.
- **211 düzeltmesi:** başlangıç koruma kaydı alınırken 211 dosyasında 24 hamle görüldü; kullanıcı “ben değiştirdim” diyerek değişikliğin kendisine ait olduğunu doğruladı. 211'e yazılmadı; 24 korunur. Önceki ajan 20 hamle kaydı tekrar uygulanmaz.
- **Sınır:** 211–220 yeni tanıtım molası devam eder. 213 henüz istenmedi; bu çalışma bütün onluğu doldurma yetkisi değildir.

## Örnek verilen engelleri otomatik ana tema yapma · 2026-11-03

- **Kullanıcı:** “düşünme şeklin garip ... wall metal wall vb dedim sen sadece ana obje olarak bunları kullandın bizim 50'ye yakın engelimiz var”. 211'in ardından tasarım yöntemine açık eleştiri.
- **Kabul edilen düzeltme:** kullanıcı duvarları örnek verdi; geniş mevcut havuzdan bağımsız seçenek geliştirmemizi bekliyordu. Önce farklı mekanik ilişkiler ve görsel fikirler, sonra uygun engel seçimi. “Eski engellerle devam” yalnız birkaç alışılmış aileyi döndürmek değildir. Çok türü tek tahtaya doldurmak veya rastgele serpiştirmek de amaç değildir.
- **Kanıt/sınır:** güncel ObstacleLibrary ve LevelData tekrar okundu. Kütüphanede 76 ID tanımı var; renk/aşama/yön varyantları, üretim ürünleri ve eski tanımlar bulunduğundan bu sayı 76 bağımsız tasarım ailesi demek değildir. Seçimde tanıtılmışlık ve güncel runtime davranışı korunur.
- **Kapsam:** yöntem geri bildirimi kaydedildi; kullanıcı 211'i yeniden tasarlamayı veya 212'ye geçmeyi açıkça istemedi. 211 mevcut 20 hamle halinde korunur; bu eleştiri görsel onay veya bütün tasarımın açık ret kararı olarak genişletilmez.

## 211 hamle bütçesi fazla rahat · 2026-11-03

- **Kullanıcı:** “bunun hamlesini düşürebilirsin ... oldukça kolaydı 10 hamle kaldı geride”. Oynanmış 28 hamle sürümüne ait zorluk geri bildirimidir; görsel onay diye yorumlanmaz.
- **Uygulama:** yalnız 211'in hamlesi 28→20; diğer bütün asset alanları aynı. Bildirilen 18 hamlelik bitirişe yaklaşık iki hamle pay hedeflendi. 20 hamle henüz denenmedi; bu bir başarı oranı ölçümü veya diğer levellar için otomatik kural değildir.

## 211 isteği / 211–220 yeni mekanik molası · 2026-11-03

- **Kullanıcı:** “211-220 arası için eski engelleri, kullanarak devam edeceğiz, katmanlı yapı metall wall wall altı farklı engeller vb ... bunlar örnek ama en önemli board güzel olacak görüntü güzel olacak”.
- **Kabul edilen yön:** bu onluk için yeni tanıtım kotası uygulanmaz. Eski engel havuzu, alt/üst katman ve görsel kompozisyon birlikte değerlendirilir; duvar örnekleri bütün onluğu aynı tarife dönüştürmez. Yetki yalnız 211'i tasarlamak.
- **Ajan uygulaması:** 211 Çelik Arma; aşağı daralan 75 hücrelik maske, asimetrik kiremit/metal kuşak, bağlı taş omurga ve 2×2 dolap altında çamur. İlk eş-kanat taslağı ajan görsel incelemesiyle değiştirildi; kullanıcı ret/onayı değildir.
- **Durum:** asset/katalog/TR+EN ad/not ve gerçek sprite PNG hazır. Statik kontrol ve görsel inceleme var; kullanıcı henüz oynamadı veya onaylamadı. 212'ye başlanmaz.

## 131–205 genel inceleme / hedef politikası · 2026-10-31

- **Kullanıcı:** “130dan sonra tüm levelları incele ... 5 ve9daki zorluklar fena değil ...
  özellikle150den sonra çok kolay heyecan vermiyor ... boardu çok geniş kullanıyorsun.”
  Yardımcı kullanımına itirazı yok; “özellikle bossduelde ekrana engel koyduysan goals'ta da olsun ...
  başka levellar için de geçerli ... kullanıcı ekranda ne varsa kırmaya çalışır ... maks5goals.”
- **Durum:** Açık inceleme/revizyon yetkisi; 131–205 için51asset revizyonu uygulandı.
  Tek tek yeni düzenler **agent tasarım kararıdır, kullanıcı onayı veya ölçülmüş denge değildir**.
- **Kalıcı kural:** Yardımcılar da hedefte temsil edilir; max5. Eski RocketBasket/OverrideBatteryBox/
  opsiyonel EggBird hedefsiz yardımcı kuralı bu yeni talimatla geçersizdir. Kırılmaz üreticiler kendi
  kuş/anahtar/enerji hedefiyle temsil edilir. Gelecekte üretilmesi garanti olmayan nesneyi tek başına
  zorunlu hedef yapma. Goal miktarı, başlangıç engellerini sebepsizce erken bitirmesin.
- **Agent uygulaması:**51değişiklik/24koruma;17maske/boyut revizyonu. Yedi boss5goal ve hedefle
  uyumlu baskı/gri alet; ilk1–130ve201korundu.5/9maskeleri/hamleleri aynı,179/185aile sadeleşmesi
  goal limiti nedeniyle. Normal zorlukta alan/açılma/sonraki iş çeşitlendirildi; tek tip dar koridor yok.
- **Kapsam/sınır:** Eski geniş alan tercihinin önüne son kolaylık geri bildirimi geçer; her seviyeyi
  dar/Hard yapma zorunluluğu değildir. Önizleme/statik kontrolden kazanma oranı çıkarma.
  [Kalıcı hafıza](Memory.md), [75level karar kaydı](Batches/131-205-Review-2026-10-31/Review.md).


## 202–205 v1 reddi /50 yalnız201 /oyun fikri önce · 2026-10-31

- **Kullanıcı:** “birdhouse da her seviyede50yapmışsın ... bazı yerler için o
  çok fazla ... boardlar tek düze olmuş ... daha insan gibi düşün ve keyifli
  olanları yap ... birdhouseun çalışma mantığını bir bak biraz değişiklik var”.
- **Durum:**202–205v1 reddedildi;aynı büyük dikdörtgen alana birkaç engel grubu
  koymak yeterli çeşitlilik sayılmadı. V1 asset/meta/PNG/not/betikler
  `Batches/202-205/Revisions/v1-rejected/` altında saklandı.
- **Düzeltme:**50yalnız201'e ait açık sayıydı;bu değeri202–205'e genellemek agent
  hatasıydı. V2kuş hedefleri18/12/16/24;201hedef50ve onaylı şekil korunuyor.
- **Mekanik doğrulama:**Güncel kaynakta ilk vuruş açar,sonraki isabet kuşu
  toplar ve kota kaldıkça evi açık tutar. Vuruşsuz hamle kapanmaya yol açar.
  Normal/scripted ev başına1kuş/hamle,special için bu sınır yok. Hedef seçerken
  yalnız ev sayısı değil,kaç evin aynı anda normal eşleşmeyle vurulabildiği,
  diğer hedefler ve vuruş ritmini bölmenin maliyeti düşünülür.
- **Agent v2 tasarım kararı:**202halka avlu/iki boya omzu;203aynı hücreyi
  ilerleterek duvarı çökertip kargoyu akıtma;204S biçimli kıyı/sağ kanal çıkışı;
  205hangi renk mührünü önce açıp roket yardımını ne zaman tüketme kararı.
  Bunlar kullanıcı onayı veya ölçülmüş eğlence/denge sonucu değildir.
- **Kalıcı ders/kapsam:**Bir level için istenen sayıyı bütün sete yayma.
  Sadece engel isimlerini ve miktarlarını değiştirmek yeterli değildir;
  çalışma alanı,ilk karar,açılma ödülü ve sonraki iş birlikte çeşitlenmeli.
  Her tahta mutlaka çok hole'lu olsun diye bir kullanıcı kuralı çıkarılmaz.

## 201 onayı / hedef50 ve202–205 kenar yerleşimleri · 2026-10-31

- **Kullanıcı:** “olmuş kuş kafesleri ... 202den205e ... diğer obstaclelarıda
  kullanarak4level ...12hedefi çok az ... bunu50yap”. Devamında “özellikle2sütunlu”,
  “bazen sadece kenarlara tek sırada”, “altına taş geçmesi gerekecek ... alt
  kenarlar veya yan kenarlar tek sütun” yönünü ekledi.
- **Durum:**201'in kuş evi yerleşimi beğenildi/korunur; yalnız hedef12→50 yapıldı.
  202–205v1 sonradan tekdüzelik/yük gerekçesiyle reddedildi;üstteki v2kaydı günceldir.
- **Uygulama:**202'de sağ kenar1×6;203'te alt kenarda iki3×1;204'te sol2×4;
  205'te iki2×4(tek grup mühürlü). Toplu bloklar yanında kesintisiz kenar
  sıraları da artık açıkça istenen alternatiflerdir; tekil serpme yapılmadı.
  Kargo çıkışları BirdHouse ile kapatılmadı; alttaki refill rotaları kontrol edildi.
- **Agent değerlendirmesi:**201'de çok düşük kuş hedefi, birden fazla ev ve
  special zincirleriyle çabuk bitebilir; ev sayısı goal miktarı olarak kopyalanmaz.
  Güncel serviste ilk açılıştan sonra isabet sürdükçe kuşlar yenilenir.50 hedefi
  kullanıcı istedi; bunun26 hamlede ölçülmüş denge sağladığı iddia edilmez.
- **Ders/kapsam:** BirdHouse tekrarlarında2sütunlu blok,yan tek sütun,alt tek
  sıra kompozisyonu kullanılabilir. Kalıcı evler üst dolumu durdurabildiği için
  her tasarımda alt besleme ve komşu eşleşme yüzleri doğrulanır. Yeni dört taslakta
  eski ailelerle çeşitlilik var;201'in onaylı şeklini değiştirme izni çıkarılmaz.

## Birdhouse tanıtımı / 201 · çapraz toplu bölgeler · 2026-10-31

- **Kullanıcı:** “birdhouse isimli bir engel oluşturdum. 201.level yapalım bunu
  tanıtadan ... collectible bir obje ... 2x3 2x4 yada 3x3 gibi gruplar halinde
  ... ayrı ayrı koyma ... bir grup sol alt bir grup sağ üst köşe”.
- **Durum:** Yerleşim tercihi açık talimat olarak uygulandı. Sonraki mesajda
  kullanıcı kuş evlerini beğendi;12 hedefini50'ye çıkardı. Kazanma oranı ölçülmedi.
- **Uygulama:** İki bitişik2×3 BirdHouse78 grubu, sol alt/sağ üst bölgeler.
  Kenarda birer taş sütunu bırakıldı; tüm evler normal eşleşmeyle erişilebilir.
  Evler kırılmadığından goal obstacle78 değil,12 adet Collectible Bird(3).
- **Agent değerlendirmesi:** Üst grubun iki yanından dolum bütün76 normal yuvaya
  ulaşıyor;3×3 merkezin yalnız-special erişimini ilk tanıtıma zorlamamak için2×3
  seçildi. Asıl karar, açılan evlere zamanında ikinci vuruşu planlamak. Bu beklenen
  oynanış yönüdür; kullanıcı tarafından oynanıp doğrulanmış bir sonuç değildir.
- **Tekrar kullanılabilir ders/kapsam:** Kuş evi üreticileri tekil serpme yerine
  okunaklı bitişik gruplar olarak düşünülür. Sonraki açık yönlendirme,kenarda tek
  sıra/sütunu da bu seçeneklere ekledi. Verilen grup ölçüleri alternatiflerdir; bütün collectible türlerine
  zorunlu aynı çapraz şablon veya eski levelları değiştirme izni değildir.

## 199 Deniz Kabuğu v3 — jeli mühür altına taşıma · 2026-10-29

- **Kullanıcı:** “level 199 ... kolay olmuş ... 2 ve 3.satıdaki jeli silip ancient
  seal altına 4 tane jel koysak ... 2. v 3. stırda sadece jel sil diğerli kalsın”.
- **Durum:** Kolaylık geri bildirimi kaydedildi; istenen yerel revizyon uygulandı.
  v3 henüz oynanıp onaylanmadı, ölçülmüş kazanma oranı yok.
- **Uygulama:** Üstten2./3. satırda yalnız dört jel tabanı kaldırıldı; TNT, üç
  pusula örtüsü ve bütün diğer içerik korundu. Dört jel2×2 Ancient Seal'in altına
  kondu. Hamle26, hedefler87/17/1 ve mühür sayaçları8/8/8 aynı kaldı.
- **Agent değerlendirmesi:** Başlangıçta açık jel kalmadığından TNT ilk hamlede
  jel yayamaz; önce mührün renk işi tamamlanır, kaplama ondan sonra başlar.
  Bu beklenen zorluk etkisidir; ölçülmüş denge sonucu değildir.
- **Ders/kapsam:** Jel kaynağını geç açılan örtü altına taşımak kaplama zamanını
  değiştirir. Bu yalnız199 için istenen düzeltmedir; bütün jel levellarına yeni
  kural değildir. v2 snapshot'ı `Batches/ProLevels-V1-090-100-199/Revisions/199-v2/`.

## V1 12–15 v2 tasarım onayı — 2026-10-29

- Kullanıcı: “dostum güzel oldu eline sağlık”. Pencere/odalar/yağlı geçiş/üç bent v2 yönü
  kabul edildi. 11–15 korunur; yeni izin olmadan yeniden tasarlanmaz,16'ya geçilmez.
- Görsel/tasarım onayıdır; hedef geçiş yüzdelerinin tutturulduğu veya FPS ölçüldüğü anlamına gelmez.

## V1 12–15 ilk taslakları reddedildi — 2026-10-28

- **Kullanıcı:** "dostum bunlar ne ama bunlar ben level demem ne göreli var. ne bir atraksiyonu..."
- **Durum: reddedildi.** Açık Avlu'nun basit alt U'su, Katmanlı Geçit'in tek L'si, Yağlı Köşe'nin
  küçük köşe cebi ve Üç Katlı Bent'in düz alt üç sırası kullanıcıya tasarlanmış bir level hissi vermedi.
- **Somut ders:** Başlangıç seviyesi/Normal olmak, büyük boş dikdörtgene kenar engeli koymak
  anlamına gelmez. Görsel şekil, ilk oyuncu kararı ve o kararın açtığı yeni alan birlikte kurulmalı.
  Yalnız teknik mask/hedef/dolum doğruluğu veya "bu kadar açık taş var" sayısı yeterli değil.
- **Sınır:** Bütün U/L/satır yerleşimleri yasaklanmadı. Her levele hazır special veya yeni mekanik
  koyma talebi de değil. Bu dört taslakta formun ve açılma olayının eksikliği reddedildi.
- **v2 agent revizyonu, henüz kullanıcı onayı değil:** 12 pencere + roketle açılan alt avlu;
  13 iki alt oda ve yeni uzun çalışma kanadı; 14 Z biçimindeki yağlı geçiş; 15 combo ile başlayan
  üç kaymış bent. Aynı izinli engeller, aynı isim/GUID/katalog;11 korundu. Yeni runtime kodu yok.
  İlk taslaklar asset/meta/PNG/kod/notlarıyla `Batches/ProLevels-V1-011-015/Revisions/v1/` içinde
  ret gerekçesiyle saklandı. Yeni görsel/oynanış beğenisi veya ölçülmüş zorluk varsayılmaz.

## Başlangıç akışının yeniden kurulması — ProLevels V1 · 2026-10-28

- **Açık kullanıcı yönlendirmesi:** "yeni levellar prolevels folderı altında levelv1_ olarak ilerliyor";
  önceki seviyeleri zorluk düzeni iyi olmadığı için kopyalayıp yeni düzene göre düzenliyor.
  Yeni sıra `ProLevels/LevelCatalogProV1.asset` ile çözülür; eski üretim kataloğu kullanılmaz.
- **Açık kural:** "bir onluk sette maks 3 min 2 yeni obstacle ... 5 zor 9 daha zor ... 10 lu levellar boss duel".
  Önceki esnek tanıtım notu yeni V1 akışında bu kurala göre güncellendi. Aşama/varyant ve
  aile envanteri ayrı tutulur; kullanıcıdan gelmeyen bir kota sayım politikası icat edilmez.
- **Sıradaki istek:** 12–14 Normal (%55–85), 15 Zor (%35–55); yalnız Sandık1/2/3, Çamur, Yağ.
  "önce kontrol et bu levellar başlangıç düzeyi levelları". İlk tur hazırlık/incelemeydi;
  ölçülmüş denge veya tasarım onayı değildir.
- **Devam isteği:** "sadece11–15 yapmanı istiyorum zaten". Kapsam bu grupla sınırlandı:
  mevcut11/Yağlı Rampa korundu; boş12–15 yeni tasarlandı.12Açık Avlu29,13Katmanlı Geçit30,
  14Yağlı Köşe28,15Üç Katlı Bent29 hamle. SonuncuZor, diğerleriNormal.
  `Batches/ProLevels-V1-011-015/` tasarım/önizleme/koruma kaydıdır. Kullanıcı oynanış veya
  görsel onayı henüz vermedi;16'ya devam edilmedi.
- **Agent değerlendirmesi:** 11 yağ tanıtımında 36 yağ ve 23 tek-hit sandık, 80 aktif hücrenin
  yalnız 21'ini açılışta örtüsüz/etkileşime açık bırakıyor. 12'de yükü rahatlatıp pekiştirmek
  uygun olabilir. Bu kullanıcı oynanış geri bildirimi veya ölçülmüş zorluk değildir.
- **Doğrulanan kayıt:** 12–15 katalogda `fileID: 0`, ilgili V1 asset adları henüz yok.
  1–11'deki beş izinli ID doğrulandı; 10 Boss Duel. Görsel/statik envanter
  `Reviews/ProLevels-V1-012-015/` altında; 767 mevcut level/katalog asset/meta dosyası aynı.
  Unity arayüzü, build, bot/kaskat simülasyonu kullanılmadı.

## 101+ otomatik special engelleme · 2026-10-24

Kullanıcı: “%40-90 agresif olmuş sadece yumuşatalım ... randomize ... %30-70 olabilir”. Önceki isteğin kapsamı 100 sonrası; tamamen kapatma yerine otomatik engelleme 101+ için %30–70 oldu. Önceki levellar ve elle atanmış oranlar korunur. Seviyeye bağlı mevcut karma farklı oranlar verir; aynı seviyenin oranı tekrar denemelerde sabittir, her aday şeklin engellenmesi mevcut rastgele kararla belirlenir. Bu bir geçiş oranı veya special çıkma garantisi değildir; oynanış dengesi henüz ölçülmedi.

## 89–90 oynanış düzeltmesi · 2026-10-23

Kullanıcı: “89 ve90 levellar zor olmuş”; 90'a5 hamle, 89'un sağ alt taş alanı üstüne1 oynanabilir satır ve5 hamle istedi. İki level28→33 hamle oldu. 89'da4. satır/5–9. sütunlarda5 normal hücre açıldı; tam-board jel hedefi41→46 ile yeni maskeye eşitlendi. Diğer mevcut içerikler korundu. Bu, iki level için açık yerel düzeltmedir; bütün eski levelları kolaylaştırma veya ölçülmüş kazanılabilirlik onayı değildir. [Önce/sonra ve kayıt](Batches/089-090-Ease-2026-10-23/Review.md).

Kanıt, yorum ve oyun ölçümü ayrı tutulur. Açık kullanıcı onayı olmadan bir tasarım onaylı sayılmaz. Mevcut ilkelerin ayrıntıları Design-Principles.md içindedir.

| Kayıt | Kullanıcı kanıtı / durum | Çıkarılan ders | Sınır |
| --- | --- | --- | --- |
| 171 Restorasyon Avlusu | Reddedildi: “daha yaratıcı olmanı bekliyordum”, yeni renkli sandıklar varken “çok sıradan” | Güncel engel havuzunu kontrol et; eski sandık/çamur/fener/heykel kanatlarına dönüp durma | Her levelde en yeni engel zorunlu değil |
| 171 Renk Çarkı | Görsel düzeltme: sandıkların “ortada adacık gibi” durması hoş değil | Renk eklemek tek başına kompozisyonu düzeltmez; kontur/sütun/şekil bütünlüğü gerekir | Tüm board'lar çerçeve olmak zorunda değil |
| 171 Renkli Çerçeve | **Onaylı:** “171 oldu bence onu kullanabiliriz” | Hole etrafında tamamlanan kesintisiz kontur ve geniş oyun alanı kabul edildi | Görsel/kullanım onayı; %40–50 geçiş ölçümü değil. İstek olmadan değiştirme |
| Beş dış oyun görseli | Tasarım yönünü geliştirmek için verildi | Görsel ritim, yoğun desen, sütunlar, negatif alan ve işlevsel kenarlar; bkz. References/Index.md | Başka oyundaki engellerin kuralları bilinmiyor; referanslar yeni level talebi değil |
| 172 isteği | “sadece tahta kullanmayalım elimizde çok fazla obstacle var” | 172'de sandık ağırlığından uzaklaş; farklı mekanikleri bir bütün içinde kullan | Çok engel türü eklemek veya aynı U çerçeveyi kopyalamak tek başına yenilik değil |

Yeni geri bildirimde ilgili satırı güncelle veya somut bir örnek ekle. Öz değerlendirme skorlarını kullanıcı beğenisi gibi sunma. Gerçek kazanma oranını ancak oynanış verisi desteklediğinde kaydet.

## Kaynak önceliği ve düzeltme

Kullanıcı son açıklama: “sadece verdiklerimi öğrenmen için veriyorum”, “170'e kadarki levellar görüntü olarak güzel ... oradan da öğren”. Yeni dış referans gelmesi, aktif leveli o örneğe göre yeniden yapma talebi değildir. 1–170 görsel havuzu ana yerel referanstır; dış görseller bunu genişletir. Görsel olarak beğenilme, oynanış denge ölçümü değildir.

170 öncesi havuzdan 125, 135, 145, 147, 155, 163, 167 ve 168'in mevcut PNG'leri toplu incelendi; 135, 145 ve 155'in tasarım notları ayrıca okundu. Bütün 170 levelin tek tek detaylı incelemesi yapılmış gibi sunulmaz.

| Yerel örnek | İşe yarayan karar | Yeni tasarıma aktarılacak ders |
| --- | --- | --- |
| [135 Bölmeli Pencere](Level135.md) | Hole satırı/sütunu dört odanın sınırını kuruyor; ana dolum kenara yapışık; iki sandık grubu çapraz gradyan | Bölme ve erişim kuralı aynı hikâyeyi anlatsın; sırf dolum kolay olsun diye deseni ortadan bölme |
| [145 Kanal Adaları](Level145-Kanal-Adalari.md) | Dar merkez kanalın çevresinde kasıtlı, hizalı special hedef adaları | Adalar evrensel olarak yasak değil: açıklanabilir maske, ritim ve mekanik ilişki varsa geçerli. Reddedilen 171'de normal taş içinde kalan açıklamasız sandık bloklarıydı |
| [147 Su Feneri](Level147-Su-Feneri.md) | Değişik boyuttaki engeller tanınan tek siluete yerleşiyor | Engel çeşitliliği bir form ve rol hiyerarşisi içinde çalışsın |
| [155 Nilüfer Tacı](Level155-Nilufer-Taci.md) | Sıvı tabanı/katı örtü, simetrik taç ve daralan alt şekil birlikte tasarlanmış | Hole, renk ve katman birlikte düşünülsün; aynı nilüfer sütunu tekrarlanmasın |
| [163 Renkli Onarım](Level163-Renkli-Onarim.md) | Geniş merkez ve kenarları tamamlayan büyük engeller; ön-arka katman ayrımı | Açık alan da güçlü kompozisyon kurabilir; hole zorunlu değil |
| [167 Lazerli Restorasyon](Level167-Lazerli-Restorasyon.md) | Sıkışık engel grubu ile sağ dolum alanı bilinçli ayrılmış; özel mekanik bir arada | Her level için aynı eşleşme alanı oranını dayatma; akış ve erişimi seçilen konsepte göre incele |
| [168 Zincir Atölyesi](Level168-Zincir-Atolyesi.md) | Kenarlara bağlanan katmanlı kanatlar ve ortak yardımcı bölgesi | Eski engeller kullanılabilir; aynı kanat tarifini art arda yeniden üretme |

Gelecek öğrenme döngüsü: alakalı 2–3 yerel örneği seç → yeni kompozisyonu kur → gerçek sprite'larla görsel kontrol → açık kullanıcı geri bildirimini ve gerekçeyi kaydet → sonraki işe kısa çalışma durumundan devam et. Bu prosedür tutarlılığı artırmayı amaçlar; her gün otomatik daha iyi sonuç garantisi veya model ağırlığı eğitimi iddiası değildir.

## 173–180 ve isim kaydı

- Kullanıcı: “biraz olayı yakaladık ... 180'e kadar git ... olmadı geri alırız”. Bu, tasarım yönüne olumlu geri bildirim ve sekiz taslak için yetkidir; her taslağın görsel/oyun onayı değildir. İnceleme ve geri alma kayıtları ayrı tutulur.
- Kullanıcı: “her level için isimleri de tutalım ... bunları kullanacağız”, ardından “isim olmayanlara da isim ver”. 1–180 isimleri numara ve gerçek levelKey ile tek JSON'da tutulur. Mevcut 61 isim korunur, eksik 119 isim eklenir; yeni tasarımlarda isim kaydetmek workflow'un parçasıdır.

## 181 yönlendirmesi — 2026-10-20

- **Açık kullanıcı tercihi:** “şimdilik renkli sandıkları artık kullanmayalım”, “geçmişteki color chest wardrobe ve katmanlı yapı ... tekrara mı girmeye başladık”. Yeni ColorCrate 71–74 geçici olarak seçimden çıkarılır. Eski ColorChest 21 ile Wardrobe 27 yasaklanmadı; kullanıcı bunları yeniden değerlendirmeyi istedi. 171–180 otomatik değiştirilmez.
- **Tasarım değerlendirmesi, kullanıcı onayı değil:** Son grupta çerçeve/iki yan kanat ve renkli sandık örtüsü tekrar riski taşıyor. Engel değişimi kadar geometri, çalışma alanı yönü ve açılma sırası da değişmeli. 181 için bağlı, tek yana yaslanan basamak seçildi; dolapların arkasında renk sandıkları, sağ alt dolap altında taş ve çamur var.
- **Sınır:** Her yeni levelin asimetrik, dolaplı veya çamur katmanlı olması gerekmiyor. 181 henüz inceleme taslağı; beğeni ve oynanış dengesi onaylanmış sayılmaz.

### 181 Basamaklı Arşiv — reddedildi

Kullanıcı: **“bunu yapmıştık benzerleri var.. lütfen sistemdekileri bir kontrol et”**. 181'in önceki yenilik değerlendirmesi geri çekildi. 1–180 katalog assetlerinin tamamı tarandı, beş yerleşim haritası incelendi; 7/23/48/68/121 ve 181 gerçek sprite'larla yan yana kontrol edildi. 7/68'de basamak; 23/48'de açık üst köşe ve kenar/alt engel kütlesi; 121'de ColorChest + Wardrobe + SculptingStone birlikteliği var. 181 bu motifleri yeniden birleştiriyor. [Kanıt ve kapsam](Reviews/181-Repetition/Review.md), [yan yana görsel](Reviews/181-Repetition/Comparison.png).

Yeni ders: sadece yakın levellara veya engelin son kullanım numarasına bakmak yetmez. Katalog genelindeki benzer siluetleri, çalışma alanı yönünü ve açılma faydasını karşılaştır. Basamağı küçültmek, çevirmek veya türleri değiştirmek tek başına yeterli fark değil. Önceki levellar korunur; reddedilen 181 karşı örnek olarak saklanır, 182'ye ilerlenmez. Yeni bir alternatif bu kontrol isteği sırasında yazılmadı.

### 181 açıklaması ve zorluk revizyonu · v2

**Son kullanıcı açıklaması:** “şekle çok itiraz etmiyorum ama çok kolay ... bu level için”. Önceki incelemeyi yalnız şekil reddi olarak yorumlamak fazla genişti. Benzerlik kaydı kalır; ana revizyon nedeni zorluk yüküdür. Kullanıcının açık şekil onayı veya ölçülmüş win-rate sonucu olduğu da varsayılmaz.

181'de basamak ailesi korundu, kütle 24'ten 40 hücreye büyüdü; başlangıç taşı 75→59, ColorChest 3→6, dolap 3→4, oyma taşı 4→16, çamur 24→40 oldu. Dört dolabın tamamı üç katmanlıdır; hamle yalnız 27→26 değişti. İsim/GUID/katalog ve 1–180 korundu. Eski taslak `Batches/181/Revisions/v1/` içinde saklı; yeni sürüm inceleme bekliyor, dengesi ölçülmedi.

Ders: zorluk geri bildirimini şekil yeniliği problemine çevirmeden, hedef yükü ve açılma sonrasındaki iş üzerinden yanıtla. Kullanıcı şekle büyük itirazı olmadığını söylüyorsa sırf benzerlik nedeniyle görseli terk etme.

### 181 v3 — kullanıcının çapraz hole isteği

Kullanıcı v2 için **“bu fena değil”**, ardından **“sol üst köşeden başlayıp x,y göre 1,1, 2,2, 3,3, 4,4 gibi ... hole ... boardu ikiye bölmüş oluruz daha zor olur”** dedi. Bu, genel yöne olumlu geri bildirim ve belirli maske revizyonudur; ölçülmüş zorluk veya v3 onayı değildir.

9×11 ölçüsü korundu. Bir tabanlı `(1,1)…(9,9)` çizgisi dokuz hole oldu; eşit x/y çizgisi sağ kenara 9. satırda ulaşır. Maske 36/54 hücrelik tam iki dört-yönlü bileşene ayrıldı. Hole'lar mevcut gövdeleri kesmedi; bütün hedef/katmanlar ve 26 hamle aynı kaldı, açık başlangıç taşı 59→50 oldu. Dolum kontrol edildi, LineH/LineV'nin hole hücresini atlayıp karşı tarafa devam ettiği koddan doğrulandı. v2 saklandı. Bu tercih 181'e özeldir; her leveli çapraz bölme kuralı değildir.

### 181 v4 — kesintisiz boşluk ve korunacak köşeler

Kullanıcı oyun içi görselle **“böyle bir durumda tamamen boş bir diyagonal satır oluşması doğru olmaz mıydı”** dedi; ardından **“bu zor bir konu ben çok uğraşmıştım köşe yuvarlakları çok doğru birleşmeli ... başka yerler bozulmasın”** diye özellikle belirtti. [Kalıcı oyun görseli](References/11-Level181-Ayrik-Capraz-Hole.png).

v3'te delikler yalnız köşeden değiyor ve ayrı kutular gibi görünüyordu. İstenen sürekli boşluk için v4'te üst sağdan sekiz ara hücre daha hole oldu; 17 hücre kenardan bağlı tek şerit. Alt bölgenin 14 başlangıç taşı ve bütün hedef/katmanlar korundu. Ortak köşe/yuvarlatma kodu değiştirilmedi. Aktif board maskesinde çapraz-only 5/10 köşe sayısı 8→0; sınır ve delik bağlılığı kontrol edildi, mevcut sprite'larla büyütülmüş statik border görünümü incelendi. Unity'de son piksel görünümü henüz doğrulanmadı; diğer 683 border/grid/prefab/theme/sahne dosyası değişmedi.

Ders: kesintisiz bir boşluk isteniyorsa yalnız çapraz koordinatları boşaltıp tamamlandı sayma; hole'ların kenardan birleşmesini ve köşe konturunu da kontrol et. Eski, emek verilmiş ortak yuvarlatma çözümünü bir levelin maskesi için yeniden yazma. Bu, bütün çapraz temasların hatalı veya bütün board'ların bu maskede olması gerektiği anlamına gelmez.

## 182 — yeni Lantern tanıtımı

Kullanıcı “yeni bir engel ekledik ... fener ... bunu ekleyerek 182. level tasarlar mısın” dedi. Bu istek 182'yi yetkilendirir; önceki 181 değişmez. Mevcut kod/library'de yeni Lantern75 iki aşamalı movable, ID40 ile aynı temel davranışta bir görsel aile olarak doğrulandı. 182 **Fener Şenliği**: 30 fener, 21 porselen, 28 hamle; üstte 30 normal taş, altta daralan dama. Yeni renkli sandıklar kullanılmadı. Referans 03 ve eski 115/123 karşılaştırıldı; dama motifinin ilk kez kullanıldığı iddia edilmedi.

182 yeni bir inceleme taslağıdır; kullanıcı onayı veya %40–50 kazanma ölçümü değildir. Yeni fenerin tanıtımını ağır ek katmanlarla örtmek yerine iki görsel aileyle okunur tutmak bu tasarım kararıdır; bütün sonraki fener levelları için aynı yerleşim kuralı oluşturmaz.

### 182 v2 — kandilleri tek sütunda toplama · 2026-10-20

Kullanıcı: **“kandil böyle parçalı durunca hoş olmuyor ... 1 sütuna dizilmeli onun solunda ya da sağında diğer engeller ... board fena değil ... sıralı olması daha hoş”**. Dama/kandil dağılımı reddedildi; board siluetini tamamen değiştirme talebi yok.

v2'de aynı maskede 4. sütuna 11 kesintisiz kandil, hemen sağına 21 porselenlik 3×7 blok yerleştirildi. 30 kandil hedefi ve 28 hamle korununca 19 kandil mevcut hedef dolumuna taşındı; bu denge farkı not edildi. v1 asset/not/PNG arşivlendi. v2 henüz kullanıcı tarafından onaylanmadı.

**Ders ve kapsam:** Yeni kandillerde parçalı dağılım yerine kesintisiz tek sütun, diğer ailenin yanında toplu blok okunurluğu tercih ediliyor. Bu istek 182'nin açılış kompozisyonu içindir; her engel ailesinin damasını yasaklama veya bütün sonraki levelları aynı sütuna dönüştürme kuralı değildir. Hareketli engellerin oyun boyunca bu düzende kalacağı söylenmez.

## 2026-10-21 — 183 isteği ve 179 denge düzeltmesi

- **183 yetkisi:** “183. leveli tasarlayacağız ... fener ve bir su kanalı ekledik ... bunları içeriyor olabilir.” Güncel kod/skin kontrol edilerek su kanalının ilk production kullanımı tasarlandı: **Fenerli Bent**. Bu, iki yeni engelin her sonraki levelde zorunlu olduğu anlamına gelmez. 182 ve öncekiler otomatik yeniden tasarlanmaz. Önceki katalog183 test asseti korundu; yeni production tasarımına referans verildi. Henüz kullanıcı onayı yok.
- **179 açık oynanış geri bildirimi:** Kullanıcı `LevelP_001830` için “hiç geçilmiyor”, “4–5 hamle daha ihtiyacı var”, “kolay ... değil ... çözülebilir olsun” dedi. 1/2 hit renkli sandık dizilimi opsiyon olarak önerildi. Super Hard etiketi çözümsüzlük gerekçesi değildir; hedef, mevcut zorluğu koruyarak bitirilebilirlik payı vermek.
- **Uygulanan düzeltme:** Katalog179 doğrulandı, **26→31 hamle** yapıldı. Sandık aşamaları/maske/hedef/katman/yardım aynı kaldı. Hücre bazlı başlangıç hit authoring alanı olmadığı için global library değiştirilmedi. Bu bir ilk kalibrasyon; kullanıcı yeniden oynamadan “çözüldü / dengeli” diye kaydetme. Eski sürüm `Batches/183/Before/` içinde.
- **Kapsam:** Kullanıcı mevcut179'u açıkça istisna olarak revize etti. ColorCrate71–74 yeni tasarımlarda dinlenmeye devam eder; eski179'daki varlıkları bu tur kaldırılmadı. Bütün Super Hard levellara otomatik +5 hamle kuralı çıkarılmaz.

### 179 — açık başlangıç aşaması talebi ve kullanıcı düzenini koruma

Kullanıcı: **“179'da da değişiklik yaptım onu koru ... bu haliyle geçilmesi mümkün değil ... sandıkların ilk katman 3 ... şu andaki kalsın ... sonra 2 ... sonrakiler tek stage”**. Önceki yalnız +5 hamle düzeltmesi yeterli bulunmadı; bu tur başlangıç sandık aşamaları açık taleptir.

Son assetten çalışıldı; eski pinler geri getirilmedi. İlk sandık sırası3, sonraki2, alt iki sıra1 hit oldu; 31hamle ve kullanıcının PulseCore20/LineV21 değişiklikleri korundu. Paylaşılan sandıkların global hitini değiştirmek yerine instance başlangıç aşaması eklendi. 72→42 sandık vuruş yükü; henüz yeniden oyun onayı yok.

Ders: Kullanıcı bir leveli sonradan değiştirdiğini söylüyorsa önce güncel asseti snapshot al, eski tasarım JSON'undan asseti yeniden üretme. “İlk güçlü, sonra hafifleyen” yükte yalnız hamle artırmak yerine istenen aşama gradyanını uygula. Bu mevcut179'a özel revizyondur; tüm Super Hard levellara veya bütün yeni sandıklara otomatik uygulanmaz.

## 185 — yatay ayna ve satır değişimi isteği

Kullanıcı ekli görselle **“böyle bir level ... tam zıttı ... alt köşe sağ ... diğer boardun da sola doğru simetrisi ... yumurtalar yerine bir sıra fener bir sıra plastic two stages ... diğer sarı yerlere sculpting stone”** dedi. [Kaynak](References/12-Aynali-Serit-Referansi.png). Bu açık tasarım talebidir; önceki öğrenme-only görsellerin kapsamından ayrılır.

185IşıkMerdiveni: yatay ayna, ana gövde solda, küçük kıvrım sağda;24Lantern+21PlasticTwoStage yatay dönüşümlü;9bejblokoymataşı. Sarı normal eşleşme taşları renk havuzunda kaldı. Mor alt engel belirsiz: opsiyonel pusula/enerji sorusu yanıtsızken belirtilen pusula varsayımı draftta kullanıldı. 32hamleHard ve1başlangıçTNT tasarım kararıdır, kullanıcı onayı/denge ölçümü değildir.

Ders/kapsam:182'deki tek sütun tercihi yatay sıraları genel olarak yasaklamaz; son açık görsel yerleşim talebi önceliklidir. Ayna hem maskeyi hem engel bölgelerini kapsar. Başka oyunun yumurtasının yardımını, hareketini veya alt mor engelin mekaniğini görüntüden çıkarma. Kullanıcının istemediği ilave köprülerle referans geometrisini değiştirme; ortak köşe kodunu koru. Önceki levellar yeniden tasarlanmadı.

## ColorCrate başlangıç aşamasının editördeki yeri

Kullanıcı: “overlaye neden koydun normal chest obstacle gibi birşey bu ama”. İlk uygulama 179’daki katmanlı veriden dolayı Overlay ile sınırlandı; bu normal sandık yerleşimi için uygun değildi. Aşama seçimi normal Obstacle paletinde bulunmalı; başlangıç hasarı ile üst üste engel katmanı ayrı kavramlardır. Normal sandıkta 1/2/3 vuruş seçimi ve doğrudan yeniden boyayarak güncelleme eklendi. Gerçek katman desteği ve 179 asseti korundu.


## 189 — iki nilüferli kanal ve duvar arkası bahçe isteği · 2026-10-22

Kullanıcı zor seviye istedi: iki bölgede water channel üstünde nilüfer, 2×2 boşluklarda EggBird, alt solda/sağda yatay ve dikey GelLauncher; sağda 3 veya4 ayrı2×2 Wall ve arkasında GrassFlower altında fener. 189 bu açık yerleşim talebiyle yetkilendirildi; önceki levellar korunur.

Uygulama: iki C biçimli kanal, kuşlu2×2 pencereler, dört bağımsız duvar parçası ve çiçek üstü/fener altı iki sütun. Alt yardımcıların atışları birbirine bağlanır. “Boşluk” nilüfersiz oynanabilir pencere olarak açıklandı; opsiyonel soruya yanıt gelmeden bu varsayımla taslak tamamlandı. Kullanıcı onayı veya ölçülmüş denge olarak kaydedilmez.

Ders/kapsam: Bölge ve katman talebini yalnız engel sayısına dönüştürme; açıklanan açılma sırasını ve yardımcı yönlerini de doğrula. Bu özel 189 kompozisyonudur; sonraki her leveli aynı iki gölet/duvar düzenine dönüştürme. 32 hamle ilk Super Hard ayarıdır.


## 2026-10-29 — V1 90 / 100 / 199, doğadan şekil ve önce oynanış fikri

**Kullanıcı:** “hemen çizme ... üzerinde insan gibi çok düşün ... neyi oynamaktan keyif
alırdın onu düşün ... doğadan güzel şekillere uygun tasarlamaya çalışalım”. Bu istek,
90/100 Boss ve199 Süper Zor için yeni tasarım yetkisidir; eski levelları yeniden
çizme veya mevcut onayları kaldırma talebi değildir.

**Ders:** Önce görünen şekil, ilk oyuncu kararı ve açılmanın kazandırdığı fayda
birlikte kurulmalı. Bir doğa adını rastgele engel döşemesine eklemek yeterli değildir.
Düelloda engellerin temizlenmesi alan/koruma avantajı sunabilir; hepsini yan hedefe
çevirerek boss sonrası temizlik işi bırakmak gerekmez. Bu son cümle bu üç tasarımdaki
ajan kararıdır; kullanıcının bütün Boss levelları için koyduğu genel yasak değildir.

**Uygulama / durum:** 90 dalga kıvrımı,100 mıknatıs damarlı yaprak,199 katmanlı deniz
kabuğu seçildi. Statik/refill ve gerçek sprite incelemesi yapıldı; kullanıcıdan henüz
beğeni/oynanış onayı yok. Şekil benzerliği ve zorluk ölçümü birbirine karıştırılmaz.
Önceki onaylı11–15 korunur. Sonraki level bu talepten otomatik yetki almaz.


## 199 Deniz Kabuğu v2 — kullanıcı zorluk ve dolu açılış isteği · 2026-10-29

**Kullanıcı:** “bu seviye için biraz kolay oldu ... kabuğun içine tile koyarsan ilk
açılışda daha şık görünür ...31 ... fazla bir26 ile deneyelim”.31 hamleli v1 için
zorluk yeterli bulunmadı; kabuk şekli terk edilmiyor, iç normal yuvaların baştan
dolu görünmesi isteniyor. Bu v2 onayı veya ölçülmüş kazanma oranı değildir.

**Uygulama:** Yalnız199,31→26 hamle; boş18 normal hücre başlangıç taşlarıyla dolu.
Mevcut GridSpawner pin desteği kullanılır; taşlar normal hareket eder, ilave special
yok. Rastgele dış renklerle sınırda kendiliğinden eşleşme oluşmasını önlemek için
mevcut ağız taşının1 rengi de belirlenir. Şekil/engel/hedef/mühür/TNT korunur.

**Ders ve kapsam:** Bir odanın dolum gölgesinde olması, bilinçli başlangıç taşı
konulamayacağı anlamına gelmez. Görselde taşı çizmek yetmez; runtime spawn desteği,
başlangıç desteği, renk eşleşmeleri ve sonraki refill ayrı kontrol edilir. İç taşlar
eşleşme olanağını da artırır;26 hamlenin net zorluk etkisini kullanıcı yeniden oynar.
Bu199'a özel revizyondur, bütün levellara pin veya hamle azaltma kuralı değildir.
90/100 ve diğer levellar korunur; eski199 snapshot'ı batch `Revisions/199-v1/` içindedir.


## Kolay çıkan seviyelerde tahta tekdüzeliği — kullanıcı · 2026-10-31

**Kullanıcı:** “hamle azaltmaktan çok bordu incelermisin çokda güzel olmamış olabilir boardlar bazen çok
tekdüze gözükmeye başladım.. engelleri doğru seçmiyor olabilir ... kuralımızı unutma renk uyumu, ufak
blokların yan yana durup blok olması şık duruyor ama hepsinden önemlisi ilk bakışta insanın bir şekle
benzetmesi ya da gridin güzel tasarımı çok önemli.” Kaynak: 130–205 Usta bot simi (15 oyun/seviye,
`~/Desktop/Sim/L130_205_Usta/summary.csv`); %80+ kazanma + bol kalan hamle: 130,131,137,146,161,167,
172,181,183,191 (+171 kutuplaşmış).

**Durum:** İnceleme isteği (review scope) — asset değişikliği yetkilendirilmedi. Önce görsel/kompozisyon,
sonra gerekirse hamle. Sim sonucu denge ölçüsüdür, görsel onay değildir.

**Ders:** Kolaylık çoğu zaman tasarım zayıflığıyla birlikte geliyor: geniş düz taş alanı + kenara/alta
yığılmış birkaç büyük engel (161, 146) hem tekdüze görünür hem kolay oynanır. Hamle kısmak bu
kompozisyonu düzeltmez. Öncelik sırası: (1) ilk bakışta okunan şekil / güzel ızgara, (2) küçük
blokların yan yana birleşip bant/kütle oluşturması, (3) renk uyumu (aileler arası ton dengesi),
(4) en son hamle sayısı.


## Hedefte kaynak + ürün çifti yok — kullanıcı · 2026-10-31

**Kullanıcı:** “ben hedefleri değiştirdim saçma hedefler eklenmişti mesela gellauncher ile gel bunlar aynı”.
131–205 genel revizyonunda (aynı gün) “yardımcılar dahil hedefte temsil, ≤5 goal” uygulanırken bir
mekaniğin kaynağı ile ürünü ayrı hedef yapılmış; kullanıcı elle düzeltti. **Reddedildi.**

**Kural:** Aynı mekaniğin iki ucu tek hedeftir: Jel Fırlatıcı → Yayılan Jel, Su Deposu → Su Birikintisi,
Varil → Çamur, Anahtar Makinesi → Anahtar taşı, Kuş Evi → Kuş, Enerji Kabı/Şapka → Enerji. Hedef ürünü
(oyuncunun gerçekten temizlediği/topladığı şeyi) sayar; kaynak hedefe yazılmaz. Yardımcı engel, oyuncunun
ayrı bir iş yaptığı başka bir aile değilse hedefe eklenmez; “≤5 goal” üst sınırdır, doldurulacak kota değil.

**Durum:** Kullanıcının düzeltmesinden sonra tarama (ProLevels V1 goals): L138 Su Deposu+Birikinti,
L144 ve L148 Jel Fırlatıcı+Yayılan Jel hâlâ çift görünüyor — kullanıcıya soruldu, değiştirilmedi.


## Tekil küçük engel yok, en az 2×2 — kullanıcı · 2026-10-31

**Kullanıcı:** “koca bir boardda 1,2,3 gibi hücreleri ortaya koyduğumuzda çirkin oluyor.. örneğin rocketbasket
bir hücreye konmuş ve şekli çok bozuyor. bunlar 2x2 minimumda iyi duruyor”. **Kural:** tek başına 1–3 hücrelik
engel yerleşimi reddedilir; en az 2×2 küme ya da kompozisyonu tamamlayan uzun kesintisiz bant/sütun. Yardımcı
engeller (RocketBasket, OverrideBatteryBox, EggBird) dahil. Tasarım Principles'a işlendi.
Ek (aynı gün): “bazen dikdörtgen şekilde daha iyi 2x3, 2x4 yatayda ya da sütunda”.

## 2026-11-01 · Super Hard kalibrasyonu (199, kullanıcı oyunu)
- Kaynak: "199 da ise 2 kez başarısız oldum ama bir bir hakla kaldı. 3. de geçtim yani biraz rahat geldi acaba ancient ı 13 mü yapsan"
- Durum: kabul; mühür 11 → 13/13/13 uygulandı (hamle 24 aynı).
- Ders: kullanıcı için **Super Hard = 3 denemede geçmek "biraz rahat"**; daha fazla deneme bekleniyor. Zorluğu kaynak
  engelin (jel kaynağını örten mühür) iş miktarıyla ayarlamak tercih edildi, hamle kesmek değil.
- Aynı oturum: 197 rahatlatma + 198 olduğu gibi "çok iyi olmuş"; 191 v2 dikey şeritler "çok sevdim"; 196 halka "süper".
- 135 (Hard) kullanıcı oyunu: "3 kez oynadım birinde geçemedim 2 sinde en az 12 hamle daha vardı ... zor değil".
  Ders: galibiyette ≥10 hamle artıyorsa bütçe gevşek; Hard için tasarım kaldıracından sonra hamle de kesilir (27 → 22).
- 2026-11-01 · Yardımcılar hedef değil: "kalkanlar ve roket sepetini hedefe koymuyorum asla onlar yardımcı pilotlar gibi..
  rocket sepetini bazen koyuyorum". Kabul; Memory.md kural 3'e istisna yazıldı. Kalkan (35) hiç, RocketBasket varsayılan
  olarak hedef olmaz.
- 2026-11-01 · "yumurtayı da bazen koyuyorum" → EggBird da RocketBasket gibi: varsayılan hedef değil, kullanıcı bazen ekler; mevcutlara dokunulmaz.
- 2026-11-01 · Boss fırlatmaları: "eğer hedefte yoksa fırlatmaya devam etsin... hedefte o obje varsa sadece onu fırlatmasın sayacı bozduğu için... arada movable obstacle atması güzel oluyor. onların bitirilmesi zorunlu değil". Kabul; Memory kural 5 tersine çevrildi, 140–200 düzeltildi.
- 2026-11-01 · "205 deki rocket basket tek başına kötü duran örneklerden biri ..onu ya birşeyin altına koyalım ya da hiç kullanmayalım". Ders: yardımcı (roket sepeti vb.) tahtada tek başına adacık gibi durmaz; bir örtünün altına saklanır ya da hiç konmaz. 205 önerisinden kaldırıldı.
- 2026-11-01 · "201 hariç seviyeler çok kolay oldu... tüm geçmiş güzel obstacle lara bak bence bir kaç tane daha kullanmaya çalış" + "water channel aşağıya dönük biterse iyi olur (animasyonumuz çalışıyor)". Ders: yeni engel eklerken eski işi silip yerine az emekli şerit koymak seviyeyi kolaylaştırır; iş miktarını koru. Su kanalı yolu aşağı dönerek alt kenarda bitsin.
- 2026-11-01 · "202-204 çok kolay hocam en azından 1-2 hamle kala bitirsem 205 oldu bence zorluyor". Kalibrasyon: normal seviyede hedef his 1–2 hamle kala bitirmek; 205 (Hard, fener sütunları) doğru. Kaldıraç: kuş kotası + hamle.
- 2026-11-01 · Zorluk kalibrasyonu: "oyunu çok rahat geçtim geniş boardlarda oyun rahat geçiliyor. insanların bir iki kez yanması bu seviyelerde bana anormal gelmiyor" (206, 4 değirmen + kanal, 25 hamle). Ders: 200+ normal seviyelerde geniş tahta = kolay; 1–2 fail beklenen his. 206'yı kullanıcı kendisi tasarladı ve onayladı.

- 2026-11-01 · 207–210 v1 RET: "bunlar çok sıradan boardlar ... hiç yaratıcılık yok hepsi aynı" (dikdörtgen + kenar
  değirmen + üst bant + alt blok). Ders: her seviyeye ayrı siluet + ayrı oyun fikri; aynı iskeleti engel değiştirerek tekrarlama.
- 2026-11-01 · "fenerlerin hepsinin bir yerde olduğu senaryo güzel olmuyor fenerler sütunda dizildiğinde güzel oluyor".
- 2026-11-01 · "kuş evi için lütfen kenarları kullan L ve ters L planlayabilirsin böylelikle iyi bir hedef olur".
- 2026-11-02 · 207 oyunu: "bir linev patlatıyorum tam alt koridordan yukarı yine boşluklar oluşuyor" (+ ekran). İlk tepki
  (değirmeni tasarımda dibe indirmek) REDDEDİLDİ: "hayır bu kurallar tüm seviyelerde bizim taş akışımızı ilgilendiren kurallar,
  taş akışımız her zaman mükemmel olmalı". Ders: akış boşluğu = motor hatası; tasarımla etrafından dolanma. Kök: değirmen
  4 vuruş/1 aşama → EnsureStageSlots boş varsayılan aşama (allowDiagonal=false) ekliyordu; ilk kanat kırılınca köşe çapraz
  kaymaya kapanıyordu. Kütüphanede 4 aşama + motor eksik aşamayı son aşamayla doldurur. 207/210 v2.1'e döndü.
