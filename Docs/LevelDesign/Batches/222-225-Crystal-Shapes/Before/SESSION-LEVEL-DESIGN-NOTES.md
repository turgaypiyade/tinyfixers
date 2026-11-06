# Yeni oturum için TinyFixers level tasarım yöntemi

Tarih: 2026-11-05. Kullanıcının isteğiyle hazırlanmış çalışma ve karar ölçütleri özeti. Yeni level veya runtime değişikliği yapılmadı. Bu dosya, ayrıntılı kayıtlara giriş noktasıdır; sonraki kullanıcı talimatları önceliklidir.

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
- Dimerdal açıldıktan sonra uygun renkli komşu grup veya renk bağımsız special/joker 1 elmas üretir. Elmas boardda ayrıca toplanır. Üretim havadaki renkli taşı mevcut düşüşüyle değiştirebilir; PatchBot aynı hedefi vurabilir. Toplama TarpStatue'nin aynı animasyonuyla, sayaç varışta azalacak biçimde kodlandı. Son runtime değişikliği cihazda tekrar denenmedi. Teknik devam notu: `Docs/Checks/Dimerdal/SESSION-HANDOFF.md`.
- **222 ve sonrası için bu not tek başına tasarım yetkisi vermez.** Yeni oturumda kullanıcının istediği seviyeyi/kapsamı esas al.

Kullanıcıya verilecek yeni PNG'leri `/Users/turgayp/Desktop/Wondi Paylaşım/codex/` altında tut; erişemediği geçici klasörleri teslim adresi yapma. Paylaşılan çalışma alanındaki başka değişiklikleri geri alma. Sıradan level tasarımı için düşüş/bump/diyagonal/PatchBot algoritmalarını değiştirme; kullanıcı bu akışın korunmasını özellikle istiyor.
