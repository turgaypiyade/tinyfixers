# Yeni oturum için TinyFixers level tasarım yöntemi

## Son kullanıcı kapsamı — tasarım kullanıcıda, yalnız editör · 2026-11-06

Kullanıcının son açık talimatı: “sen levelı tasarlama ben yapacağım sadece leveleditor çalışsın”. Bu görev yalnız LevelEditor düzeltmesidir; level tasarımı veya226/diğer assetleri yeniden üretme yetkisi yoktur. Kullanıcının226 maskesi/baseWall/parça numarası düzenlemeleri korunur. Eski batch modelini/asset hash’ini güncel kullanıcı assetine zorla uydurma; author taslağını yeniden çalıştırma. Diğer Overlay engelleri çalışan örneklerdir; istenen Wall/MetalWall elle Overlay fırçasının çalışmasıdır. Numara/grup, sürükleyerek boyama ve yalnız duvarı silme düzeltildi;130 gerçek editör yöntem kontrolü ve265 mevcut runtime katman kontrolü geçti. Unity GUI/Play veya build yapılmadı. Teknik kayıt `Docs/Checks/Wall-Overlay-Editor-2026-11-06/README.md`.

## Güncel — Wall Overlay elle numaralı boyama · 2026-11-06

**226 kullanıcı düzenlemesi:** 226 asseti bu editör görevinin başlangıcında eski v2 tasarım modelinden zaten farklıydı (maske,baseWall/partNo dizileri). Bu görevin başlangıç SHA’sı ile son SHA’sı aynı; kullanıcı düzenlemeleri korunur. Eski226 batch doğrulaması asset drift korumasında beklenen şekilde durdu; batch PASS iddiası yok, eski model/hash assete benimsetilmedi. Yeni editör yöntem kontrolleri130 ve mevcut runtime katman kontrolleri265 geçti.

Kullanıcı Wall/MetalWall’ı editörde elle Overlay olarak kuramadığını söyledi; diğer Overlay engellerini çalışan örnek olarak verdiğini netleştirdi. Numara seçimi vardı fakat Overlay girişlerine yazılmıyor/gruplar yeniden kurulmuyordu. Artık Overlay → Wall/MetalWall → Parça No → Duvar ekle ile tıklama/sol tuş sürükleme bağlı yolu kurar; Yeni Parça ayırır, yeniden numaralama/silme origin ve bağlantıları yeniler. Yalnız duvarı sil alt feneri/diğer katmanları korur; genel Erase tüm hücreyi temizlemeye devam eder. Numara/bağlı kenarlar ve alt içerik penceresi görünür. Goals/Wall/Eşitle gerçek parça sayısını kullanır.

StackedObstacleEntry.wallPieceId yalnız editör numarasıdır; runtime ortak origin, düşüş/akış, VFX ve ses değişmedi. Inspector görüntülemek kayıtları otomatik yeniden yazmaz;226 tipi eski ortak originli kayıtlar yalnız açık düzenlemede taşınır, numarasız bağımsız kapaklar otomatik birleştirilmez. Eski gerçek boyama yöntemiyle sorun tekrar üretildi;130 editör yöntem kontrolü ve265 mevcut runtime katman kontrolü geçti.6054 diğer proje dosyası ve tüm level/katalog/adlar aynı. Kayıt `Docs/Checks/Wall-Overlay-Editor-2026-11-06/README.md`. Unity GUI/Play/build/cihaz/yayın/commit yok;226v2 tasarım/28hamle ve onay durumu değişmedi.

## Güncel — 226 v1 reddi / bağlı duvar yolu v2 · 2026-11-06

Kullanıcı226 v1’i açıkça reddetti: duvar referanstaki gibi yol boyunca bağlı olmalı, bir hücresi yıkılınca domino gibi ilerlemeli ve altından fenerler çıkmalı. Sağ üstte tavan bloğu altı boşluk/akış sorunu yarattı; değirmenler ana normal alanın yanında kalıp farklı bir oyun kurmadı. Değirmenler special ile erişilecek ayrı hücre grubunda istenir. Ekran kanıtı `References/16-Level226-Akis-Ve-Oyun-Fikri-Reddi.png`. Önceki226 için agent görsel değerlendirmesi kullanıcı onayı değildir.

**226 v2 Rüzgâr Kemeri:**9×11/68aktif,28hamleNormal; tek24hücre duvar yolu, tamamının altında24 gerçek Fener75; sağdaki ayrı2×4adada iki Windmill80. Hedefler1Duvar/24Fener/16Un. Duvar sağ koldan tabana döner, altına normal taş cebi bırakmaz.36 başlangıç taş yuvasının hepsi doğal beslenir; pin/hazırspecial/başlangıçboşu yok. Bir hücrede mevcut beşinci vuruş tek parçanın zincirini başlatır. Değirmen adası gerçek hole ile ayrılır; bütün değirmen satırlarına ana çalışma alanından yatay special erişimi vardır.24 gizli fener hedefi karşılar; fazladan fener üretim kotası yok.

Mevcut yığın damgalama her Wall hücresini ayrı origin yaptığı için gerekli küçük destek eklendi: StackedObstacleEntry.wallPieceOriginPlusOne(0eski davranış), GridSpawner ortak origin ile üst duvar/alt içerik kaydı, editörde tek parça hedef sayımı. Düşüş/akış,duvar animasyonu/ses/FX kodu değişmedi; yeni tarama veya per-hit allocation yok.265 gerçek yöntem katman/origin/hedef/beneath/dalga kontrolü ve1356 mevcut zincir/sırayla açılma/havuz/iptal kontrolü geçti. Eski damgalama ile24 ayrı parça hatası yeniden üretildi. Üç eski genel harness mevcut Dimerdal doubles eksikliğiyle çalışmıyor; bu revizyonun öncesiyle aynı yedi yöntem doğrulandı ve sınırlama kaydedildi.

Aktif kayıt `Batches/226-230-Lantern-Wind-Crystal/Revisions/v2-wall-route/`; parentauthor/notyazıcısı eski226’yı geri basamaz. Yeni gerçek PNG ve katman kesiti açıldı; görsel değerlendirme agent’a aittir.5278 diğer oyun dosyası,1–225ve227–230 assetleri,katalog/TR–EN adlar korundu. Yeni taslak kullanıcı tarafından henüz oynanmadı/onaylanmadı;28hamle ilk ayar. Unitybuild/Play/cihaz/bot/düşüş-kaskat simülasyonu,yayın/baseline/commit yok.231+istenmedi.

## Güncel devam — 226–230 taslakları · 2026-11-06

Son açık istekler226 ve227 için referanslı tasarım, ardından228–229 ve230Boss’un tasarımıdır. Beş taslak tamamlandı:226 Rüzgâr Kemeri28Normal;227 Işık Tezgâhı26Normal;228 Kristal Ünlem26Normal;229 Rüzgâr Burgusu27Super Hard;230 Büyük Işık Düellosu30Boss(9×10,660 toplamHP/3rakip). Bölüm10, aktifV1 katalog1–230 ve TR/EN isimler güncel.226 dört duvar kapağı altında dört gerçek fener ve yalnız sağ iki sütundan16 ek fener/iki değirmen;227 beş üçlü lamba çerçevesi+oyma+plastik;228 ünlem;229 çim üstte sarıL/dört değirmen;230 üst fener sahnesi/yan kristal locaları/dip değirmenleri. Dimerdal kotaları228[12],229[6,10],230[6,6]; bağımsız kırılma, toplam mor hedef.

Kayıt `Batches/226-230-Lantern-Wind-Crystal/README.md`; beş ayrı not/gerçek sprite PNG, toplu PNG ve eski örneklerle karşılaştırma var. Statik audit geçti;3393 kapsam dışı oyun dosyası ve1–225 korundu. Başlangıç pinleri226=20,229=8;227’de dört bilinçli açılabilir gölge boşu var. Açılma sonrası besleme erişimi statik doğrulandı. Görsel inceleme agent değerlendirmesidir; kullanıcı onayı veya oynanmış denge değildir. Hamleler ilk ayar. Build/Play/cihaz/bot/kaskat/düşüş simülasyonu, yayın/baseline/commit yok; runtime/art/akış değiştirilmedi.231+ istenmedi. Alttaki “226+istenmedi” kayıtları tarihseldir.

## Son yön · 2026-11-05 — her Dimerdal'a ayrı sayı

Editörde kutu başına üretim adedi ayarlanır; Goals toplamı otomatik hesaplanır. Her kutu kendi son başarılı yerleşimiyle ayrı kırılır; bir kutu diğerinin kotasını kullanamaz. Yeni alan `LevelData.dimerdals`; mevcut221=4+4,222=10,224=6+6,225=8+8. Düzen/hamle/önceki hedefler ve mor ortak ikon korundu. `Docs/Checks/Dimerdal/Per-Box-Production/README.md` günceldir; önceki ortak kota/toplu kırılma kayıtları tarihseldir. Unity GUI/Play/build/cihaz/yayın yok.

## Son yön · 2026-11-05 — Dimerdal tamamlanınca parçalanır

Gri/%45 opak kapanış KeyGenerator'a fazla benzediği için kullanıcı üretim sonunda kırılıp yok olmasını istedi. Son gerekli elmasın başarılı yerleşiminden sonra ortak kaynaklar havuzlu büyük parçalara ayrılır, footprint hücreleri normal temizleme yoluyla açılır. Örtü/alt katman korunur; iptal/başarısız uçuş kırmaz. 225 v2 ve bütün level assetleri aynı. `Docs/Checks/Dimerdal/Exhaustion-Break/README.md` güncel teknik kayıttır; önceki gri kapanış tarihseldir. Build/Play/cihaz/yayın yok.

**Son revizyon:**222–224kullanıcı görsel onaylı;225alt geçişv2(boş12→2;alt iki sıra dolar,25hamle/hedefaynı). Dimerdal kabul edilenhit animasyonu ve son başarılı elmas yerleşiminden sonra kapanan,gri/%45opak kutular güncel. Teknik/QA: `Batches/222-225-Crystal-Shapes/Revisions/v2-lower-feed/`; Memory/Design-Stateenüst. Cihaztesti yok.

Tarih: 2026-11-05. Kullanıcının isteğiyle hazırlanmış çalışma ve karar ölçütleri özeti. Yeni level veya runtime değişikliği yapılmadı. Bu dosya, ayrıntılı kayıtlara giriş noktasıdır; sonraki kullanıcı talimatları önceliklidir.

**Son devam noktası (sonraki açık kullanıcı isteği):** 222–225 taslakları hazır; Zümrüt Yaprak24, Uçan Anahtar23, Kristal Çapa24, Mühürlü Kristal Kalesi25Hard. 1–221/runtime korundu; dört gerçek sprite PNG açılıp incelendi. Oynanmış/onaylanmış denge yok. Kayıt `Batches/222-225-Crystal-Shapes/`; Memory/Design-State en üst kayıt günceldir. 226+ istenmedi.

## 1. Başlamadan önce gerçek durumu çöz

- `AGENTS.md`, `Docs/LevelDesign/Memory.md`, `Design-State.md` ve `Skills/tinyfixers-level-design/SKILL.md` dosyalarını oku. İlgili `Design-Feedback.md` kayıtlarına bak. Eski notlarda kalan “221 istenmedi” gibi ifadelerin tarihini kontrol et; en güncel açık kullanıcı talebi geçerlidir.
- Aktif akış `Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset`. Level numarasını katalogdan çöz; asset dosyasının adından tahmin etme. Eski `ProductionLevels` ve `LevelCatalogPro.asset` görsel/tarihsel referanstır.
- `ObstacleLibrary.asset`, `LevelData.cs` ve `Obstacle-Design-Guide.md` üzerinden güncel mekanikleri ve o seviyeye kadar açılmış havuzu incele. Gerekirse ilgili servisi oku: kaç hit aldığı, ne ürettiği, hedefinin ne olduğu ve taş akışına etkisi isimden anlaşılamayabilir.
- Onaylı tasarımları ve kullanıcının kendi değişikliklerini koru. İnceleme isteği otomatik yeniden tasarım yetkisi değildir.

## 2. Önce levelin fikrini kur

Tasarımı şu dört somut soruyla tanımla:

1. Oyuncu ilk bakışta hangi şekli veya düzeni görecek?
2. İlk anlamlı karar ne olacak: hangi yüzü açmak, hangi bölgeye ulaşmak, hangi yardımcının faydasını kullanmak?
3. İlk engel/örtü açılınca ne değişecek: alan, dolum yolu, erişim veya hedef işi?
4. Açılış gösterisinden sonra oyuncunun çözmesi gereken hangi iş kalacak?

Birkaç farklı kompozisyon adayı değerlendir; görseli ve oynanış ilişkisi en okunaklı olanı seç. Kullanıcının saydığı Wall/MetalWall gibi örnekleri zorunlu ana tema sayma. Yaklaşık elli engelli havuzu birkaç sandık/duvar ailesine daraltma. Mekanik ailelerini, renk/aşama/boyut varyantlarından ayrı değerlendir.

## 3. Board ve engel kütlelerini birlikte tasarla

- Kullanıcının ana önceliği güzel, şık ve okunaklı board. Yelken, çan, vitray veya vitrin gibi belirgin bir görsel fikir işe yarar; bunlar zorunlu şablon değildir.
- Az/orta mekanikli tasarımda yaratıcı siluet kullanılabilir. Çok engelli/katmanlı tasarımda geniş ve daha düz bir board, deseni ve patlama akışını daha iyi taşıyabilir. Dikdörtgen board da başarılı olabilir.
- Engeller bitişik bölgeler, dikdörtgenler, bantlar, sütunlar veya şekli tamamlayan konturlar oluştursun. Özellikle 2×3 ve 2×4 gruplar kullanıcının sevdiği düzenlerdir. Ortaya açıklamasız tekil engel veya küçük, kopuk sandık adaları koyma; yardımcıları da rastgele serpme.
- Uyumlu iki aileli dama düzeni kullanılabilir. Her seviyeyi aynı çerçeveye, iki kanada veya orta koridora dönüştürme.
- Normal taşların eşleşme ve special üretme alanını, engellere vuruş yüzlerini ve dolum yollarını birlikte bırak. Zorluk için her yeri daraltmak veya tek hücrelik kullanışsız cepler yapmak gerekmez.
- Hareketli engelin açılış kompozisyonunun oyun boyunca aynı kalacağını varsayma. Geçici görüntü ile kalıcı engel geometrisini ayır.
- Güncel tema: tamirci hayvanların Dünya Harikalarını onarması/inşa etmesi. Eski robot teması yeni fikirlerin varsayılanı değildir.

## 4. Katmanı, erişimi ve hedefi aynı anda çöz

- Çim genellikle en üst örtüdür. Kullanıcının açık örneği: PaintCanBox altta, Grass üstte. ColorChest/GelLauncher gibi içerikli engelleri örtüyle kullanırken açılınca gerçek içeriklerinin görünmesi gerekir.
- Mud/Oil gibi sıvı tabanlar altta; katı engeller bunların üstünde olmalıdır. Her leveli otomatik çamur tabanlı yapma.
- Bir katman açıldığında alttaki iş gerçekten erişilebilir olmalı. Kalıcı üretici altında beslemesiz normal hücre bırakma. Hole, başlangıçta boş hücre ve pinlenmiş taş üç farklı authoring kararıdır.
- Hedefler mekanikle eşleşsin; en fazla **5 goal**. Kırılmayan BirdHouse/KeyGenerator/Dimerdal gibi kaynaklara imkânsız kırma hedefi yazma; kuş/anahtar/elmas hedefini kullan.
- En yeni yardımcı istisnalarını uygula: PlayerShieldPickup hedef olmaz. RocketBasket ve EggBird varsayılan olarak hedefsiz yardımcıdır; kullanıcı bazen kendisi hedef ekler. Eski “bütün yardımcılar hedef olmalı” notunu otomatik uygulama.
- Boss'un fırlattığı hedef dışı tehditler zorunlu hedefe çevrilmez. Hedefle çakışan ürün ve sonradan oluşan sıvı/kaplama miktarlarını ayrıca kontrol et.

## 5. Açılış ve hamle bütçesi

- V1 onluk setlerde genel kural 2–3 yeni mekanik tanıtımıdır. 211–220 için kullanıcı açıkça yeni engel istemedi; bu, o onluğa özgü istisnaydı.
- Sonu 1 ve 6 genellikle rahat tanıtımdır. 2→3→4 ile 5 Hard'a; 6 rahatlamasından 7→8 ile 9 Super Hard'a ilerlenir. 10'un katları Boss Duel. Kullanıcının özellikle istediği uçlar: tanıtım rahat, 5/9 gerçekten zor. Ara eğri yaklaşık olabilir.
- Boss board yüksekliği en fazla **10 satır**. Normal seviyede 11 satır kullanılabilir.
- Hamleyi yalnız toplam hit sayısını sayarak belirleme. Erişim maliyeti, renk kısıtı, katmanların sırayla açılması, tekrar üretim/toplama, special alanı, yardımlar ve işlerin aynı hamlede ilerleyebilmesini birlikte değerlendir. Tek special çok hedefi etkileyebilir; aynı hedef sayısı farklı yerleşimlerde farklı yük oluşturur.
- İlk hamle sayısı tasarım tahminidir. Tek denemede kalan hamle, kaybedilen hak, takılınan bölge ve yardım kullanımıyla yerel kalibrasyon yap. Kolaylık geri bildirimi geldiğinde önce açılış/yardım/hedef yükünü de incele; sadece hamle kısmayı genel çözüm yapma.
- Sabit renk pinleri ve hazır special'lar isteğe bağlı araçlardır. Her levele eklenmez. Kullanıldığında ilk kararı öğretmeli veya zor açılışa erişim sağlamalı; asıl hedef yükünü bedelsiz tüketmemelidir.
- Görsel onay, ölçülmüş denge onayı değildir. Tek oyuncunun bir denemesi kazanma yüzdesini vermez. Eski 170+ hedef yüzdelerini rahat tanıtım seviyesine otomatik taşıma.

## 6. Tekrar kontrolü ve somut doğrulama

- Yakın seviyelerle birlikte tarihsel benzerlere de bak. Kullanıcı 1–170 dönemini görsel olarak başarılı buluyor; bu havuz ana yerel referanstır.
- Yalnız engel ID'sini karşılaştırma: board silueti, engel kütlesi, yansıması, açık çalışma alanı, ilk karar ve katman sonrası oynanış aynı mı? Aynı iki kanada farklı engel koymak yeni tasarım sayılmaz. Buna karşılık benzer siluet tek başına ret gerekçesi değildir; kullanıcının eleştirisinin görsel mi, kolaylık mı olduğunu ayır.
- `Tools/review_level_patterns.py` ile adayları listelemek mümkündür. Skorlar görsel inceleme için yardımcıdır; otomatik özgünlük veya kullanıcı onayı ölçüsü değildir.
- Yazmadan önce ilgili assetlerin snapshot/hash kaydını al. İstenen aralığın dışındaki seviyeleri, GUID/meta, kullanıcı ayarlarını ve onaylı şekilleri koru.
- Statik olarak maskeyi, dizi uzunluklarını, origin/footprint bölgelerini, yığın sırasını, hedef/ikonları, pinleri, dolum/erişim yollarını ve katalog bağlantısını denetle. Hedefte boss varsa ikonunu da kontrol et.
- Gerçek oyun sprite'larıyla PNG önizlemesi oluşturup **aç ve incele**. Görsel parçalanma, kesilmiş footprint, yanlış örtü sırası ve görünmeyen içeriği düzelt. Temsili rastgele taşları gerçek sabit açılış gibi sunma.
- TR/EN adlarını `Resources/LevelNames.json`, tasarım notu ve katalog anahtarıyla tutarlı tut.
- Level tasarımı için Unity build, bot veya gravity/cascade simülasyonu çalıştırma. Açık build isteği olmadan proje build'i de çalıştırma. Statik kontrol, görsel inceleme ve cihazda oynanmış dengeyi ayrı raporla.

## 7. Her teslimde bırakılacak kayıt

Level asseti ve gerekli katalog/isim değişikliği; kısa tasarım gerekçesi; gerçek sprite önizlemesi; korunan içerik ve doğrulama sınırları. Kullanıcının geri bildirimini kendi görsel değerlendirmenden ayrı kaydet. Görsel beğeniyi “dengeli” diye etiketleme. Yayın isteği olmadan remote upload yapma; açık app release beyanı olmadan release baseline oluşturma.

Kısa çalışma sırası: **kapsam ve havuz → görsel fikir ve ilk karar → board/engel bölgeleri → katman/akış/hedef → ilk hamle tahmini → tekrar kontrolü → asset yazımı → statik kontrol ve PNG incelemesi → kullanıcı denemesiyle yerel ayar.** Bu bir tasarım iş akışıdır; ölçülmüş otomatik denge algoritması değildir.

## 8. Bu oturumdan korunacak örnekler ve devam noktası

- **211:** kullanıcı hamleyi kendisi 24 yaptı; eski 20 önerisine geri dönme.
- **212:** yelken silueti açıkça beğenildi. PaintCanBox altta, Grass üstte yönü bu geri bildirimden geldi. Silueti koru.
- **213–220:** istenen mekanikler bütün sete dağıtıldı; her şeyi tek levele yığma. LampStrip 217/218, GelLauncher 213/215'te kullanıldı. İlk deneme geri bildirimleriyle hamleler revize edildi; en güncel asset/kayıt geçerlidir.
- **221 / Mavi Vitrin:** kullanıcı “güzel kurdun oyunu beğendim” dedi. 8×10 maskeyi, alt kaidedeki bitişik iki mavi 2×2 Dimerdal'ı, **24 hamle / 8 elmas / 6 renk pinini** koru. Bu düzen, kalıcı kaynakları tabana yerleştirerek altta beslemesiz cep bırakmama örneğidir. Başka Dimerdal rengi yoktur. Görsel onay vardır; ölçülmüş denge onayı yoktur.
- Güncel Dimerdal: Elmaslar artık engel değil normal `TileType.Diamond*` taşıdır: takas/düşüş/yana kayma/special oluşturma; yalnız aynı renk elmaslarla eşleşme veya special ile temizlenme. Komşu normal match elması kırmaz. Üretim yalnız hareketsiz, iniş görseli tamamlanmış normal taşa yerleşir; hareketlenirse hedef yeniden seçilir. Sayaç TarpStatue uçuşu varışında bir kez iner. Eski havada dönüştürme/komşu match ile toplama notları tarihseldir. Son davranış cihazda tekrar oynanmadı; `Docs/Checks/Dimerdal/README.md` günceldir.
- **226 ve sonrası için bu not tek başına tasarım yetkisi vermez.** Yeni oturumda kullanıcının istediği seviyeyi/kapsamı esas al.

Kullanıcıya verilecek yeni PNG'leri `/Users/turgayp/Desktop/Wondi Paylaşım/codex/` altında tut; erişemediği geçici klasörleri teslim adresi yapma. Paylaşılan çalışma alanındaki başka değişiklikleri geri alma. Sıradan level tasarımı için düşüş/bump/diyagonal/PatchBot algoritmalarını değiştirme; kullanıcı bu akışın korunmasını özellikle istiyor.
