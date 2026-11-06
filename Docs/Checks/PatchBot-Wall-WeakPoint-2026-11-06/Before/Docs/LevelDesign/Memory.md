# TinyFixers level tasarım hafızası

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

## Dimerdal kutu başına üretim adedi — son kullanıcı yönü · 2026-11-05

Kullanıcı “leveleditor de kutuyu koyarken ben sayı vereyim… goals tabında onların toplamını kullan… 8 8… biri için 6 diğerine 10” istedi. **Ortak üretim kotası kaldırıldı:** her kutunun ayrı adedi vardır; yalnız kendi son ürünü başarıyla yerleşince kırılır. Diamond collectible hedefi bu adetlerin toplamıdır (varsa başlangıç elmasları da eklenir), mor ortak ikonla görünür ve toplandığında azalır. Obstacle/Overlay Dimerdal paletinde “Üreteceği elmas” ve mevcut kutuları koordinat/tür bazında ayrı düzenleyen liste vardır. Goals miktarı/ikonu otomatik yönetilir. Yeni kutu varsayılan6; kullanıcı değiştirebilir. Mevcut toplamları koruyan geçiş:221=4+4,222=10,224=6+6,225=8+8. Yerleşim/hamle/diğer hedefler değişmedi;223 ve diğer seviyeler korundu. Yeni alan `LevelData.dimerdals`, kayıt `Docs/Checks/Dimerdal/Per-Box-Production/`; batch aktif v4. 289 yeni gerçek yöntem kontrolü,409/39 regresyon,ortak toplama/kırılma kontrolleri ve3032 dosya koruma var. Unity GUI/Play/build/cihaz/denge/yayın yok. Önceki “bir kutu ortak hedefi doldurunca bütün kutular kırılır” açıklamaları tarihseldir.

## Ortak elmas hedef ikonu — mor Dimerdal · 2026-11-05

Kullanıcı, hedefte yeşil elmas görünürken sarı elmasın da sayılmasının kafa karıştırdığını söyledi ve verdiği `modimerdal.png` görselini Goals/PreLevel için ortak ikon istedi. Bütün elmas renkleri aynı `CollectibleId.Diamond` hedefine sayılır; ortak hedef sembolü mordur. Tahta/kutu/üretim/toplama uçuşu kendi renginde kalır; eşleşme ve üretim kotası kuralı değişmez. Varsayılan kütüphane hedef ikonları ve 221/222/224/225 explicit hedef ikonları güncellendi; yalnız ikon satırları, aynı hamle/adet/yerleşim. 3035 diğer asset/kod dosyası bu turun başlangıcına göre korundu. Kayıt `Batches/222-225-Crystal-Shapes/Revisions/v3-shared-goal-icon/`; PNG incelendi, build/Play/cihaz/yayın yok.

## Son mekanik yönü — Dimerdal üretim sonunda kırılır · 2026-11-05

Kullanıcı, kapalı/gri görünümü KeyGenerator'a benzetti ve “taş üretimi bittiğinde kırılıp yok mu olsalar” önerisini verdi. Bu son yön önceki gri/%45 alfa kuralının yerini alır. Son gerekli elmas başarıyla tahtaya yerleşince ortak kotaya bağlı görünür kutular gerçek renkli çerçevelerinden büyük parçalara ayrılıp kaldırılır; tüm footprint normal engel temizleme yoluyla serbest kalır. Örtülü kaynak örtü açılınca emekli olur; örtü ve alt katmanlar korunur. İptal/başarısız uçuş veya yalnız rezervasyon kutu kırmaz. Vuruş tepkisi ve elmas toplama kuralı korunur. Level assetleri, 225 v2 düzeni ve onaylı 222–224 değişmedi. Teknik kayıt: `Docs/Checks/Dimerdal/Exhaustion-Break/`. Yeni davranış cihazda oynanmadı; build/Play/simülasyon/yayın yok.

## Son kullanıcı düzeltmesi — 225 alt geçiş / Dimerdal tepkisi · 2026-11-05

- Kaynak: kullanıcı “hepsi güzel”; sadece225alt boşluğunu besleyecek geçiş ve Dimerdal vuruş animasyonu/üretim sonrası gri, düşük alfa kapalı görünüm istedi. **222–224 görsel onaylı ve korundu**.225genel beğeni aldı; v2yerel revizyon henüz oynanmadı.
- 225: aynı8×11/88hücre/25hamleHard,hedef16elmas/1mühür/16pusula/16çamur ve9/9/9mühür. Pusulalar iki1×8dış sütuna,kutular alt köşelere alındı;x1/x6açıkbeslemeşeritleri,ortada4hücrelikaltalan. Alt iki sıra doğal doluyor; başlangıçboş12→2,beslenen36→46. Son2yuva yalnızmühüraltı. Pin/hazırspecial/motor değişikliği yok.
- Dimerdal tüm levellarda: kabul edilenhit0.26sn büyüme/silkelenme/içparlama. Ortak kotanın son elması tahtaya yerleşince0.30sn'de kepenkler kapanır ve gerçekgri/%45opak olur. İptal/başarısız uçuş kotayı geri verir; erken kapanma yok. Reveal/disable/build görseli/animasyon/işleri temizler; tekservisdöngüsü ve paylaşılanmaterial,perhitGameObject/texture/material üretimi yok. Ses yolu değişmedi.
- Güncel kayıt `Batches/222-225-Crystal-Shapes/Revisions/v2-lower-feed/`; active-revision.jsonparentauthor'u eski taslağı basmaktan korur.3391kapsamdışı dosya ve1–224aynı;225not/gerçekPNGgüncel.409+39+46üretim/MatchFinder/feedback test ve ortak toplama rutini geçti(Unitydoubles);shader kaynak kontratıkontrolü var,GPUcompileyok. Build/Play/cihaz/bot/gravity-cascade simülasyonu/yayın/baseline yok;oynandengesi/FPSölçülmedi.226+istenmedi.

## Güncel devam — 222–225 · 2026-11-05

- **Son açık yetki:** “222–225 level tasarlamamız lazım”; “bazen board gridleri bir şekle benzer bazen içindeki engelleri de bir şekle benzetebiliriz”. Bu istek eski “222 istenmedi” kayıtlarının önündedir. 226+ istenmedi.
- **222 Zümrüt Yaprak:** 9×11, 60 aktif, 24 hamle Normal; `LevelV1_00002220.asset` / `LevelCL_0222`.
- **223 Uçan Anahtar:** 9×7, 45 aktif, 23 hamle Normal; `LevelV1_00002230.asset` / `LevelCL_0223`.
- **224 Kristal Çapa:** 8×11, 88 aktif, 24 hamle Normal; `LevelV1_00002240.asset` / `LevelCL_0224`.
- **225 Mühürlü Kristal Kalesi:** 8×11, 88 aktif, 25 hamle Hard; `LevelV1_00002250.asset` / `LevelCL_0225`.
- 222 dış yaprak maskesi, çapraz 16 çim ve tek yeşil Dimerdal ile 10 elmas; 223 yatay anahtar maskesi, 14 Key / 8 porselen / 6 oyma / 6 çamur; 224 iç engellerden kesintisiz çapa, 12 elmas / 34 tek-hit ahşap / 1 heykel / 4 üst çim; 225 4×4 mühür (9/9/9) üstünde kapalı çamur avlusu, iki 2×4 special-only pusula suru ve tabanda kristal kapıları, 16 elmas / 1 mühür / 16 pusula taşı / 16 çamur.
- Asset/meta, aktif V1 katalog, TR/EN adlar ve dört not/gerçek sprite PNG'si hazır. Kayıt `Batches/222-225-Crystal-Shapes/`; tekrar envanterleri `Reviews/222-Crystal-Shapes` … `225-Crystal-Shapes`. `design.py` sahip olunan hash'leri kontrol eder; kullanıcı değişikliğini sessizce ezmez.
- Statik YAML/dizi/origin/katman/hedef/ikon/dolum/katalog/isim kontrolü geçti; 3.409 kapsam dışı dosya ve 1–221 aynı. Kalıcı kaynak grupları tabana dayanır. Açılabilir kapak gölgesindeki boş başlangıç yuvaları PNG'de gösterilir; renk pini/hazır special yok. Açılabilir katmanlar kalkınca kaynak altında beslemesiz cep kalmaz.
- PNG'ler açılıp incelendi. **Bu agent görsel değerlendirmesidir; kullanıcı dört taslağı henüz oynamadı/onaylamadı.** Hamleler ilk ayar. Build/Play/cihaz/bot/gravity-cascade simülasyonu, yayın ve release baseline yok. Gameplay/akış kodu veya library değiştirilmedi.
- 211=24, onaylı 212 yelkeni, 213–220 son hamleleri ve 221'in onaylı 24 hamle/8 elmas/6 pin düzeni korundu. 221–230'daki Dimerdal renkleri tek mekanik ailesidir; ikinci mekanik 226+ isteğinde ayrıca seçilir.
- **Güncel mekanik:** Elmaslar artık engel değil normal `TileType.Diamond*` taşıdır: takas/düşüş/yana kayma/special oluşturma; yalnız aynı renk elmaslarla eşleşme veya special ile temizlenme. Komşu normal match elması kırmaz. Üretim yalnız hareketsiz, iniş görseli tamamlanmış normal taşa yerleşir; hareketlenirse hedef yeniden seçilir. Sayaç TarpStatue uçuşu varışında bir kez iner. Eski havada dönüştürme/komşu match ile toplama notları tarihseldir. Son davranış cihazda tekrar oynanmadı; `Docs/Checks/Dimerdal/README.md` günceldir.

## 221 · Mavi Dimerdal tanıtımı — 2026-11-04

- **Açık kullanıcı isteği:** “şimdi sadece tek rengi tanıştırdığımız 221.levelı yaparmısın”. Önceki “221 istenmedi” kayıtları tarihsel kalmıştır; bu tur yalnız221 yetkilidir.
- **İlk deneme:** Mavi Vitrin / Blue Showcase;8×10,68aktif,60doğal dolumlu normal hücre. Alt kaidede bitişik iki2×2 mavi Dimerdal85, origin66/68; başka engel veya Dimerdal rengi yok. **24hamle, Collectible Diamond4 hedef8**; ikon atanmış. Normal taş havuzu4renk;6gerçek renk pini iki kutunun üstünde tam mavi üçlü fırsatı sunar. Hazır special/başlangıç elması/örtü yok.
- **Mekanik / son kullanıcı yönü:** 4uygun renk/special hitinde açılır. Açık kutu yanındaki mavi eşleşme veya renk bağımsız special/joker vuruşu1elmas üretir. Elmas canlı, havada bile olan taşın yerine geçerken düşüş/kimlik/bump/diyagonal akışını korur; PatchBot hedefi kalabilir. Boardda ayrıca toplanır; TarpStatue ile aynı toplanma/dönüş/uçuş efekti kullanılır, hedef sayacı varışta iner. Kare çizim esnemesin diye1×1/2×2/3×3varyantları korundu.
- **Kayıt:** `Level221-Mavi-Vitrin.md/.png`, `Batches/221-Dimerdal-Intro/` (önceki snapshot/hash, yazıcı, doğrulama, renderer), `Reviews/221-Dimerdal-Intro/`. Aktif katalog `LevelCL_0221`, asset `LevelV1_00002210.asset`, TR/EN adlar eklendi. Önceki220level ve mevcut Dimerdal runtime/art/diğer kaynaklar hash ile korundu. Gerçek sprite önizlemesi ve220level tekrar envanteri incelendi.
- **Kullanıcı görsel onayı / sınır:** “güzel kurdun oyunu beğendim”;221düzeni korundu, hamle/hedef/pin değişmedi. Görsel beğeni oynanmış denge onayı değildir. Kullanıcının üretim sonrası donma raporu için runtime düzeltmesi ve gerçek motor/yöntemlerle izole kontroller yapıldı; `Docs/Checks/Dimerdal/README.md`. Yeni davranış Unity Play/cihazda tekrar oynanmadı; build, level denge simülasyonu, releasebaseline veya yayın yapılmadı. **222 istenmedi.**

## Son düzeltme — Boss hedef ikonu · 2026-11-04

- **Boss ikonları — 2026-11-04:** Kullanıcı goals ve PreLevel/editörde boss ikonunun görünmediğini bildirdi. Aktif V1’de22BossDamage hedefi tarandı; yalnız210/220ikonları fileID0 idi. Önceki20bossun BadgerIdle sprite’ı (bd8a2a2aabdf46fcabea5eb683bc76d9) bu iki hedefe atandı. HUD/PreLevel override önceliği ve editör Sprite alanı kontrol edildi; runtime UI değişmedi. İki authoring yazıcısı ve statik PNG renderer explicit boss ikonunu korur. V4 `Batches/213-220-Showcase/Revisions/v4-boss-goal-icons/` aktif; iki assette yalnız ikon değiştiği ve diğer443seviye/meta/katalog/isim dosyasının aynı kaldığı doğrulandı. Hamle/HP/hedef miktarı/tahta aynı. Unity build/Play/cihaz/yayın yok.

## Güncel durum — örtülü içerikler ve hamle v3 · 2026-11-04

- **2026-11-04 · ortak örtü/içerik düzeltmesi ve hamle v3:** Kullanıcı çim altındaki GelLauncher’ı boş gördü; aynı yolu tüm kodla kurulan içeriklerde kontrol etmemizi istedi. GridSpawner ilk çizim+promotion ortak katman HP sorgusunu kullanıyor (Gel4yön, Paint, Laser, Rhodes); Tarp.Register de saved katmanı okur. İçerik çocukları yeniden üretilmez; gerçek partial state dolmaz. BatteryBox cover-cleanup kaybı da düzeltildi; ColorChest/Battery/Wardrobe typed kayıtları sadece kendi yok edilmesinde temizlenir, board yeniden kurulumunda tüm typed haritalar bırakılır.103içerik/press/wardrobe,30registry,26paint,437promotion,384chest,6Rhodes kontrolleri geçti; eski Gel/Battery hatası fixture ile üretildi. **V2 oynandı:**213–218ilk can,219ve220birer kayıp. **Yeni hamleler213–220:21/21/22/20/21/22/24/28.** Yalnızmoves satırları; maske/hedef/engel/layer/pin/isim/katalog/GUID aynı. V3 `Batches/213-220-Showcase/Revisions/v3-moves-2026-11-04/` aktif. Ayrı MasterLeague/menü/backend/test ayarı eşzamanlı düzenlemeler kayda alındı, görev onları yazmadı. Yenihamleler cihazda oynanmadı; Unitybuild/Play/bot/kaskat/gravity/yayın yok.221istenmedi.

Önceki v2 için “oynanmadı” kayıtları tarihsel kaldı; kullanıcıv2oynadı. V3yenihamleler oynanmadı.

## Güncel revizyon — LampStrip / GelLauncher · 2026-11-03

- **213–220 v2 / ColorChest görünümü — 2026-11-03:** Kullanıcı219’da çim altındaki sandığın ampullerinin görünmediğini bildirdi.Grass destruction artık yalnız gerçekColorChest yıkımında `_chestViews` kaydını kaldırır; mevcut prefab korunur. Eski hata üretildi,384/48sıra test ve413/26/6 regresyon kontrolleri geçti. Kullanıcı isteğiyle **LampStrip217+218, GelLauncher213+215**:217iki kayanL,218ikiC(sol sabit/sağ kayan), kanal aradan;213sepet yanında ikiUp kartuş+Gel61,215dipte dört yatay kartuş/çim üstü dört sandık+Gel99. Seçilen4levelden renkli plastikler kalktı. Hamle213=24(22→24),215=26,217=24,218=25; isim/GUID/katalog aynı.214/216/219/220assetleri ve1–212, diğer kodlar2989hash ile korundu. Batch `Revisions/v2-lamps-launchers/` aktif author/audit/önceki snapshot; dörtPNG/toplu önizleme güncel. Statik ve görsel kontrol var; Unity build/Play/bot/kaskat/gravity/cihaz dengesi/yayın yok. **Oynanıp onaylanmadı;221 istenmedi.**

Önceki213–220v1 durumu tarihsel kaldı. Kullanıcı yardımcı jel kartuşlarını iki levelde, sıralı lambaları217 ve bir başka levelde istedi; yerel revizyon yetkisidir.

## Güncel devam noktası — 213–220 · 2026-11-03

- **213–220 tamamlandı — 2026-11-03, kullanıcı denemesi bekliyor:** Açık sekiz seviye isteğiyle213Işıklı Balon22 ·214Guguk Çanı24 ·215Vitray Atölyesi26Hard ·216Sulu Lale22 ·217Kanatlı Kum Saati24 ·218Kıvrılan Nehir25 ·219Çiçekli Taç25SuperHard ·220Büyük Festival29Boss(9×10,620HP/3dalga) eklendi. Fener/kuş evi/kanal/değirmen set boyunca dağıtıldı; porselen/plastik/ColorChest/çiçekli çim grupları tamamlıyor. Sarı/yeşil plastik mevcut renkli plastik ailesinin varyantlarıdır.1–212(kullanıcının211=24hamlesi ve212 onaylı maskesi dahil), mevcut runtime ve2976 korunan dosya aynı. Batch `Batches/213-220-Showcase/` snapshot/yazıcı/statik audit/katman PNG/toplu önizleme/karşılaştırmalar; sekiz asset/meta, katalog ve TR/EN adlar hazır. Statik YAML/dizi/origin/hedef/başlangıç dolumu/kanal/boss/gruplama kontrolleri ve gerçek PNG incelemesi var; hareketli engellerin düşüşü simüle edilmedi. Oynanmış denge/onay, Unity build/Play, bot/kaskat, yayın veya app-release baseline yok. **221 istenmeden eklenmez.**

Bu kayıt aşağıdaki “213 istenmedi / yalnız211 yetkisi” gibi tarihsel durumların önündedir. Yeni çalışma221’e geçmek değil, kullanıcının213–220 deneme geri bildirimini almaktır.

Bu dosya kullanıcının kalıcı tasarım kurallarını ve oturumlar arası devam noktasını taşır.
Yeni oturumda önce bunu, sonra `Design-State.md` ve ilgili batch'in kayıtlarını oku.
Kullanıcının en yeni açık talimatı eski notlardan önceliklidir. Agent taslağı kullanıcı onayı değildir.

## Güncel ve öncelikli kullanıcı kuralları

- **212 siluet onayı + çim üstte · 2026-11-03:** kullanıcı “yelken şekli çok güzel”, “ben genelde çimi en üstte kullanıyorum”, “paintboxları en alta koyalım ... otlar ... paintboxların üstünde dursun” dedi; kasa kalabilir. Yelken maskesi onaylı ve korunur. Çim için genel tercih **üst örtü**; özel açık talep olmadıkça otomatik alt katman yapma, önceki levellara toplu değişiklik uygulama. 212 v2'de iki 2×2 boya kasası alt gövdede origin74/76, üstlerinde toplam8 Grass; Tube'lar üst banda54/62 taşındı. Kasa, 24 hamle, beş hedef/14 çim aynı; yeni dolumda6 gerçek pin yeterli. Siluet onayı v2 oynanış dengesi onayı değildir. 211 kullanıcının 24 hamlesinde korunur; 213 istenmedi.

- **Son yetki / 212 · 2026-11-03:** kullanıcı “212'yi deneyelim” dedi. 212 Renkli Yelken / Painted Sail taslağı eklendi: 59 aktif hücreli yelkenli, 24 hamle; Safe altında 4 Cargo, 2 PaintCanBox altında 8 Grass, 2 yatay Tube ve 6 açık Grass. Beş hedef toplamı 1/4/2/2/14. Dokuz gerçek başlangıç taşı kapalı alt gövdeyi doldurur. Statik/PNG incelemesi var; henüz kullanıcı onayı/oynanmış denge yok. **211'in hamlesini kullanıcı kendisi 24 yaptı (“ben değiştirdim”); 24 korunur.** Önceki 20 hamle kaydı tarihsel. Sonraki level 213 için talep yok.

- **Engel örneğine saplanma · 2026-11-03:** kullanıcı 211 sonrasında Wall/MetalWall örneklerini ana obje olarak seçmemizi eleştirdi: geniş mevcut engel havuzu değerlendirilmeliydi. “Eski engeller” küçük bir duvar/taş/dolap alt kümesi demek değildir. Önce açık mekanik ailelerinin farklı oynanış ilişkilerinden birkaç aday fikir kur; görsel kompozisyon ve oynanış fikrine uygun aileleri seç. Kullanıcının saydığı örnekleri otomatik ana tema yapma. Bütün aileleri tek tahtaya doldurmak veya çeşitlilik için rastgele eklemek de çözüm değildir. Bu geri bildirim tek başına 211'i yeniden yazma/212'yi tasarlama yetkisi sayılmaz.

- **211 oyun geri bildirimi · 2026-11-03:** kullanıcı 28 hamleyle oldukça kolay geçti, 10 hamle arttı; açık talebiyle yalnız hamle **28→20**. Tahta/hedef/katman/refill korunur. 20 hamle denenmedi; başka levellara otomatik kesinti uygulanmaz.

- **2026-11-03 · 211–220 istisnası:** bu onlukta yeni engel tanıtılmayacak; mevcut engeller ve katmanlı yapıyla devam edilecek. MetalWall/Wall ve alt-üst engel örnekleri seçenek, zorunlu şablon değil. Kullanıcının ana önceliği güzel board/güzel görüntü. Bu açık istisna, yalnız bu onluk için genel 2–3 yeni tanıtım kuralının önündedir. Şu an yalnız **211** tasarım yetkisi var; 212–220 kendiliğinden yapılmaz. 211 Çelik Arma inceleme taslağı; durum ve dosyalar Design-State.md'de.

1. **131 ve sonrasını yeniden incele.** Güncel katalog 205'e kadar; kapsam 131–205, toplam 75 seviye.
   Özellikle 150 sonrası normal seviyeler fazla kolay ve heyecansız bulunuyor.
   Çok geniş, bedelsiz special üretme alanlarını azalt; karar, açılma sırası ve zorunlu iş yarat.
   Her tahtayı aynı dar koridora çevirme; sadece hamle azaltmak veya tür değiştirmek yeterli değil.
2. **Sonu 5 ve 9 olanların mevcut zorluk yönü beğeniliyor.** Bunlar referans:
   5 → Hard, 9 → Super Hard; 10'un katları → Boss Duel.
   Normal seviyeleri bunlarla aynı sertliğe otomatik çıkarma. Zor olması bitirilemez olması değildir.
3. **Tahtaya konan engel hedefte de temsil edilsin.** Bu kural yardımcı engeller için de geçerli;
   özellikle boss düellolarında yalnız boss HP hedefi bırakıp engelleri hedefsiz bırakma.
   Kullanıcı genellikle hedef listesini okumadan ekrandaki bütün engelleri kırmaya çalışıyor.
   **En fazla 5 goal** olabilir. Fazla tür varsa türleri/yerleşimi sadeleştir; altıncı slot ekleme.
   **İstisna (kullanıcı, 2026-11-01): yardımcılar hedef DEĞİLDİR.** "kalkanlar ve roket sepetini hedefe koymuyorum asla
   onlar yardımcı pilotlar gibi.. rocket sepetini bazen koyuyorum". PlayerShieldPickup (kalkan) asla hedef olmaz;
   RocketBasket ve EggBird varsayılan olarak hedef olmaz (kullanıcı "yumurtayı da bazen koyuyorum"; bazen kendisi ekler,
   mevcut kullanıcı hedeflerine dokunma). Bu, 2026-10-31 revizyonunun
   "yardımcılar da hedefte" yorumunu geçersiz kılar.
   Kuş evi, anahtar/enerji üreticisi gibi kalıcı üreticiler kendi collectible/üretim hedefiyle temsil edilir;
   kırılmayan kaynağa imkânsız kırma hedefi yazma.
4. Hedef miktarı yanlışlıkla başlangıçtaki engellerin küçük bir bölümünü istemesin.
   Örnek sorun: 36 engel konmuşken yalnız 4'ünü istemek. Bilinçli refill/üretim kotalarını ayrı değerlendir.
   Su deposu → WaterPuddle gibi sonradan eklenen hedefler de 5 slot hesabına dahildir.
5. **Boss fırlatmaları (kullanıcı düzeltmesi, 2026-11-01):** "eğer hedefte yoksa fırlatmaya devam etsin... hedefte o obje
   varsa sadece onu fırlatmasın sayacı bozduğu için... arada movable obstacle atması güzel oluyor. onların bitirilmesi
   zorunlu değil". Boss'un fırlattığı / alet tehdidi olarak koyduğu türler **hedefte OLMAMALI**; hedef dışı fırlatmalar
   serbest ve istenir. (2026-10-31 revizyonu tersini yapmıştı → 140–200 eski çeşitli fırlatma ve renkli alet listelerine
   döndürüldü, yalnız hedefle çakışanlar çıkarıldı.) Başlangıçta hiç bulunmayan objeyi zorunlu hedef yapma.

## Katalog, kapsam ve korunacak içerik

- Aktif akış: `Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset` ve `LevelV1_*.asset`.
  Asset dosya adından veya tarihsel MD başlığından level numarası çıkarma; katalog entry'sini çöz.
  Güncel V1 numaraları eski ProductionLevels numaralarından farklıdır.
- Eski `LevelCatalogPro.asset` / `ProductionLevels` referans havuzudur; otomatik yazma hedefi değildir.
- İstenen aralık dışını değiştirme. Güncel revizyonda 1–130, katalog, GUID/meta ve TR/EN isimler korunur.
- Onaylı tasarımları sırf yeni görsel referansı geldi diye yeniden yapma. Açık revizyon kapsamını uygula.
- **201'in kuş evi yerleşimi kullanıcı tarafından beğenildi.** İki bitişik 2×3 grup ve 50 kuş hedefi korunur.
  50 hedefini bütün kuş seviyelerine yayma; bu hata daha önce 202–205 için reddedildi.
- Her seviyenin Türkçe/İngilizce adı `Assets/_Project/Resources/LevelNames.json` içinde gerçek
  `level` ve `levelKey` ile eşleşir. Kullanıcı istemedikçe isimleri değiştirme.

## Tasarım dili ve ilerleme

- Okunaklı ana fikir, ilk karar, açılan yeni alan ve açılış sonrası kalan iş birlikte tasarlanır.
- Engel kütleleri bitişik blok, sıra, sütun, bant veya anlamlı kontur oluştursun.
  Normal taşlar arasında açıklamasız tekil sandık adacıkları ve rastgele engel serpme tercih edilmez.
  Uyumlu iki aileli dama deseni yasak değildir.
- Aynı iki kanat/orta koridoru yalnız engel değiştirerek tekrarlama. Siluet, çalışma alanı,
  ilk karar ve açılma sırasını komşu seviyelerle ve tarihsel benzerlerle karşılaştır.
- Hole zorunlu değildir; maskenin ve engel bölgelerinin bir amacı olsun. İmkânsız refill veya
  tek hücrelik kullanışsız alanla zorluk üretme. Açılması beklenen boş cepleri önizlemede dolu gösterme.
- Kullanılmış bütün açık engel havuzunu değerlendir; her levelde sadece son eklenen engeli kullanma.
  Yeni mekanik ile renk/aşama varyantını farklı say.
- V1 her onluk sette min 2 / max 3 yeni engel tanıtımı; ilk tanıtım anlaşılır ve görece rahat tutulur.
- Yardımcılar kullanılabilir; bedelsiz zincirlerle bütün işi oyuncu karar vermeden bitirmesin.
- **Boss board yüksekliği en fazla 10 satır.** Normal level için 11 satır kullanılabilir.

## Katman ve mekanik kuralları

- Mud/Oil gibi sıvılar altta, katı örtüler üstte olur. Üst kapak için `stackedObstacles` kullan;
  sıvıyı sandığın üstüne yazma. Refill, örtü altı taş, kargo çıkışı ve hazır special'ın başlangıçta
  düşüp düşmediği güncel runtime kurallarıyla kontrol edilir.
- Çok hücreli engelde goal adedi genellikle hücre değil **origin/parça sayısıdır**.
  Wall/MetalWall özel şekil/parça kurallarına sahiptir. WaterChannel hedefi tamamlanan kanal sayısıdır.
- Jel kırılmaz; kaplama hedefidir. Tam kaplama kotası aktif alanla ve kalıcı üreticilerle uyumlu olmalı.
  Kaynak/bulaşma kurallarını rehberden/koddan oku; her bağımsız cascade otomatik jel yaymaz.
- BirdHouse78 kırılmaz; hedef Collectible Bird3. İlk hit açar; açıkken sonraki hit kuş toplar ve
  kota kaldıkça yeni kuş çıkarır. Vuruşsuz oyuncu hamlesi evi kapatır. Normal/scripted kaynakta
  ev başına hamlede 1 kuş; special/joker için bu tavan yoktur. Güncel kodu esas al.
- Kuş evleri için 2×3 / 2×4 / 3×3 bitişik gruplar veya yan tek sütun / alt tek sıra tercihleri var.
  Alt taş akışı ve kargo çıkışı kapanmasın; evler tek tek rastgele serpilmesin.
- Her revizyonda güncel `ObstacleLibrary.asset`, `LevelData.cs` ve ilgili servis incelenir.
  Eski doküman yeni kodun önüne geçmez; görselden bilinmeyen mekanik uydurma.

## Doğrulama ve çalışma biçimi

- Önce snapshot/hash, sonra scoped authoring. Maskeler, diziler, origin/footprint, katmanlar,
  goal sayıları/kapsamı, renkler, refill, special erişimi ve katalog referansları statik doğrulanır.
- Güncel proje sprite'larıyla PNG üret, gerçek PNG'leri açıp incele. Temsili taş renklerini açıkça belirt.
  Statik doğruluk, görsel kalite ve oynanmış denge birbirinden farklıdır.
- Kullanıcı bu tur açıkça istemedikçe **Unity build / dotnet build / başka proje build'i çalıştırma**.
  Skill gereği bot/kaskat simülasyonu da çalıştırma. İzole test ve emit etmeyen API/syntax kontrolü yapılabilir.
- Ölçülmemiş kazanma oranı/FPS için garanti verme. %40–50 gibi eski notlar yön hedefidir, ölçüm değildir.
- Normal authoring için runtime/VFX davranışı değiştirme. Tekrarlı efektlerde mevcut havuzlar,
  temizleme/iptal ve performans kuralları korunur; ses merkezi sisteme bağlıdır.
- Kullanıcı istemeden level upload/publish yapma. “Bu ilk release” açıkça denmeden release baseline oluşturma.

## Tamamlanan son iş ve sonraki adım — 2026-10-31

- **131–205 incelemesi/revizyonu uygulandı.** 75 seviye incelendi; 51 asset değişti,
  24 asset aynen korundu. 17 maskede/boyutta değişiklik var. Sonu 5/9 olan 15 levelin
  maskeleri ve hamle bütçeleri aynı; 179/185'te beş hedef sınırı için tür sadeleşmesi yapıldı.
- Her levelde en fazla 5 goal; yedi bossun tamamında tam 5. Başlangıçtaki bütün engel aileleri,
  su deposunun ürettiği WaterPuddle ve bossun atabildiği türler temsil ediliyor.
  Kalıcı üreticiler collectible/anahtar hedefiyle temsil edilir. 201'in onaylı yerleşimi/50 kuşu aynı.
- Boss HP/dalgalar/saldırı sayıları değişmedi. (Tek gri ID47 alet ve hedefle eşleşen fırlatma kararı 2026-11-01'de
  kullanıcı düzeltmesiyle geri alındı; kural 5.) Fazla aileler beş slota sığacak şekilde sadeleştirildi.
- Çalışma klasörü: `Docs/LevelDesign/Batches/131-205-Review-2026-10-31/`.
  **`Review.md`** 75 levelin tek tek kararlarını içerir. `Before/` snapshot'lar,
  `After/` 75 güncel PNG ve 8 toplu sayfa; `manifest.json`, `baseline.json`,
  `validation-after.json`, `separate-rhodes-fix.json` kayıtları mevcut.
- `python3 .../audit.py --assets`: güncel 75 asset statik olarak geçiyor. Gerçek YAML Ruby/Psych
  ile okunuyor (sistem Python'unda PyYAML yok). Mask/dizi/origin/katman/path/goal/stage/slot,
  boss 10 satır ve baskı türleri, bağlantısız yeni bölge olmaması kontrol edildi.
  2.337 kapsam dışı baseline dosyasından 2.336'sı aynı; ayrı yetkili Rodos düzeltmesi GridSpawner.cs'te.
  İlk 130, katalog, adlar ve bütün meta/GUID'ler korundu.
- `revise.py --apply` **zaten uygulandı**; sonraki oturumda eski snapshot'ı tekrar authoring'e basma.
  Yeni kullanıcı düzeltmesi için güncel assetten yeni snapshot/revizyon oluştur. Read-only audit kullanılabilir.
- 194'ün base `colorCrateInitialHits` alanları doğrulandı:
  `originCellIndex`, `obstacleId`, **`remainingHits`**. `initialRemainingHits` sadece stacked entry içindir.
  Bu ayrım hem authoring hem validator'da düzeltildi; 198'in mevcut doğru girdileri aynı.
- **Aynı turdaki ayrı Rodos düzeltmesi uygulandı:** İlk destruction olayı görseli, iki-parça
  efektinin bağlı olduğu sonraki visual-change olayından önce siliyordu. `GridSpawner.TryPlayRhodesFinalBreak`
  iki olay yolunda ortak devir yapıyor; önce parçaları oluşturup sonra kaynağı siliyor, tekrar olayları çoğaltmıyor.
  Eski fixture hatayı üretir; `check_rhodes_final_event_order.csx` 6 ve `check_obstacle_scatter.csx`
  1.992 davranış kontrolü geçti. `Docs/Checks/Rhodes-Final-Event-Order-2026-10-31.md` ayrıntıdır.
- **Henüz kullanıcı tarafından oynanıp onaylanmadı.** Statik/görsel inceleme ölçülmüş denge değildir.
  Unity Play/build, bot/kaskat simülasyonu, FPS/win-rate ölçümü, yayın/release baseline yapılmadı.
  Rodos'un telefon görüntüsü de henüz doğrulanmadı. Sonraki iş cihaz geri bildirimini almak;
  özellikle 143/151/176/178/188/194/196 ve boss hedef baskısını izlemek. 206'ya kendiliğinden geçme.

## İlgili kalıcı kaynaklar

- `Skills/tinyfixers-level-design/SKILL.md` — uygulama iş akışı.
- `Design-State.md` — önceki onaylar, batch'ler ve doğrulama sınırları.
- `Design-Principles.md` / `Design-Feedback.md` — kurallar ve kullanıcının kaynak ifadeleri.
- `Obstacle-Design-Guide.md` — mekanik ayrıntıları; güncel kodla kontrol edilir.
- `References/Index.md` — kalıcı görsel referansların kapsamları.
