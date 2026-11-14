# TinyFixers level tasarım hafızası

## 235 v2 — tek kütleli katmanlı kemer uygulandı · 2026-11-07

Bu turdaki kullanıcı isteği: “şimdi 235 üzerinde çok düşünerek değerlendir. katmanlı yapı kullanabilirsin”. Yalnız 235 revize edildi. İlk U taslağı 176/182’ye benzediği için bırakıldı; son fikir **alt kenardan yükselen tek taş kemer kütlesi + iki heykel ayağı + geniş üst çalışma avlusu**. Bunlar agent değerlendirmesidir; yeni sürümün kullanıcı görsel/oyun onayı yoktur.

**Bakır Kemer / Copper Arch:** 9×11/85 aktif, **28 hamle ilk ayar · Hard**. 19 oyma→19 çamur; iki 2×2 heykel altında sekiz çamur daha. Sol 14 mavi/sağ 14 yeşil; branda sonrası kişi başına üç special. Hedefler **2 heykel /19 oyma /27 çamur**. Açılış58/58, oyma kaldırılınca77/77, heykeller kaldırılınca66/66, ikisi kalkınca85/85 normal yuva statik beslenir. Tek heykel açılması da kontrol edildi. Pin/hazır special/çim/ColorCrate/BatteryBox/Omega yok. Eski18hamle/%35bot verisi yeni sürüme ait değildir; yeni denge ölçülmedi.

Kayıt `Batches/235-Copper-Arch-v2/`; tek seferlik `revise.py --apply` uygulandı, tekrar yazmayı reddeder. `--verify` statik gerçek asset kontrolüdür. Güncel not/PNG `Level235-Bakir-Kemer.*`, karşılaştırma `Comparison.png`, tam envanter `Reviews/235-Copper-Arch-v2/`. Yalnız235’te yedi veri alanı değişti; uygulama sonrası diğer6.985Assetsdosyası/meta/GUID/katalog/isimler aynı. Master modelde yalnız235 ve Contact’ın235kartı güncellendi; eski sim girdileri sabit. Bu tur kod/library/ses/editor değişmedi. Build/Play/sim/UI/yayın/baseline/commit yok. Manuel test kullanıcıdadır; ayrı236çalışması ve son mantıklı katman kuralları korunur.

## Güncel kullanıcı kuralı — sinek kutusu/elma oranı · 2026-11-07

**Kullanıcı 2026-11-07: “plastik obstacle’lar çok kolay hedefler.”** Hareketli plastikler (Plastic_*, plastic, PlasticTwoStage) geç dönemde ana zorluk/kahraman engel olamaz; düşüp dağıldıkları için şekil de kaybolur. Kalıcı şekil için sabit engel (ColorCrate, Wall, Dimerdal, Safe…) kullan. 240 bu yüzden ColorCrate gökkuşağıyla yeniden yapıldı (ColorCrate dinlendirme yönü bu boss için bilinçli istisna).

**Kuş evi kuralı — kullanıcı 2026-11-07:** “kuş evi 1x2, 1x3, 1x4 ya da 2x2, 2x3, 2x4 gibi gruplar halinde konulunca çok şık duruyor; tek başına koyduğumuzda 2x2’lik bir yere koymak daha iyi.” BirdHouse78 tek tek serpiştirilmez; bitişik gruplar (en az 1×2, tercihen 2×2) halinde konur. Su kanalı tahtanın kenarına/altına kadar uzansın ki akış animasyonu görünsün (244).

**245 dersi (kullanıcı “biraz zor olmadı mı”):** Dimerdal açılış ve üretimi kendi rengindeki KOMŞU eşleşmeye bağlıdır; kutunun yanında en az 4 (tercihen 6) oynanabilir taş hücresi bırak. 2–3 komşulu kutu (dar sap sütununa bitişik) neredeyse imkânsızdır.

**Kullanıcı 2026-11-07: elmalar düşmez ve normal taşla kırılmaz → “boardda izole adacıklar yaparak oraya koyabiliriz, böyle bir avantajımız var.”** FlyFruit tercihen oyun alanının dışında, maskeye bağlı ya da ayrı küçük adacıklara (ağaçtan sarkan elma gibi) konur; oyun alanını işgal etmez. Sinekler için yalnız elmanın açıkta (örtüsüz) olması yeter. Elma allowDiagonal=1 olduğundan komşu sütunun beslemesini kesmez; yine de altında dolmayan cep bırakma.

**Kural — kullanıcı 2026-11-07: “fly egg box kutusu 4 yumurta taşıyor … iki yumurta bir elmayı ancak bitiriyor. bir kutu koyduysam iki elma olmalı, iki kutu koyduysam 4 elma olmalı.”** Elma sayısı = FlyEggBox sayısı × 2 (tam eşit; fazla elma bitirilemez, eksik elma yumurtayı boşa harcar). `Batches/240-250-Nature/author.py` bunu assert eder.

## Güncel kullanıcı kuralı — 236 yeniden; şekil güzelliği + MANTIKLI katman · 2026-11-07

Kullanıcı başka bir yapay zekâya yaptırılan236için “kesinlikle güzel değil, çok güzel engellerimiz var” dedi ve iki kalıcı kural verdi:
1. **“boardun şekli veya boardun içerisinde engellerle oluşturulan şekil görsel beğeni sunmalı.”** Maske ya da engellerin birlikte çizdiği şekil tanınır/güzel bir siluet olmalı; dağınık bloklar ve rastgele raylar kabul edilmez.
2. **“artık overlay engeli çok kullanabiliriz, ancak insanlar için mantıklı olanı kullanalım … bir çimin bir dolabın arkasında durması çok mantıklı değil, veya duvar bir şey örter o ortaya çıkar gibi.”** Katman (stackedObstacles) bol kullanılabilir ama fiziksel olarak inandırıcı olmalı.

Mantıklı katman ölçütü (agent yorumu, kullanıcı örneklerinden): (a) zemin katmanları (çamur, jel, yağ, un, su birikintisi, su kanalı) yalnız ALTTA durur, üstünde ağır nesneler (heykel/oyma taş, porselen, sandık, fener) durabilir; (b) örten/saklayan katmanlar içini gerçekten saklar: duvar→arkasında fener, branda→heykel, kasa/dolap/sandık→içindeki eşya/kargo/altın, nilüfer→su kanalı üstünde; (c) bitkiler (çim/çiçek) yalnız toprak/zemin hücresinde büyür; mobilya, makine, sandık, heykel, kuş evi, boya kasası, kargo ÜSTÜNE çim/çiçek konmaz. Eski levellardaki Grass→ColorChest/PaintCanBox/Cargo/TarpStatue/KeyGenerator/RocketBasket gibi eşleşmeler yeni tasarımlarda tekrarlanmaz (eski onaylı levellar kendiliğinden değiştirilmez). Bu kural 2026-11-03 “çim genellikle en üstte, ör. boya kasasının üstüne çim” tercihinin önüne geçer.

## Güncel kullanıcı geri bildirimi — 235–250 görselleri beğenilmedi; iki engel kullanım dışı · 2026-11-07

Kullanıcı son tasarımları için “levelların görselleri pek güzel değil” dedi; kapsamı ardından **“235 ve sonrası”**, **“dün ve bugün yaptıkların”** olarak açıkladı. Mevcut teslimde bu **235–250** grubudur. Bu grup görsel olarak beğenilmemiştir; önceki statik kontroller ve bot sonuçları görsel kabul anlamına gelmez. Kullanıcı tek tek kompozisyon kusurlarını henüz belirtmedi; agent'ın olası nedenlerini kullanıcı görüşü diye kaydetme. Önceki levellara bu eleştiriden ret veya yeniden tasarım yetkisi çıkarılmaz.

**BatteryBox (23) ve OmegaModul (24) şimdilik yeni tasarımlarda KULLANILMAYACAK.** Kullanıcı mevcut görsellerini çirkin bulduğu için zaten kullanmadığını, doğru imaj bulursa ileride kullanabileceğini söyledi. Kütüphanede bulunmaları kullanım onayı değildir. Yeniden kullanım, uygun görsel ve kullanıcının tekrar kullanım yönüne bağlıdır; EnergyContainer (22) ve OverrideBatteryBox (43) bu isimlerden hareketle yasaklanmaz. Mevcut gruptaki 242/246 kullanımları sorunlu olarak kayıtlıdır. Bu tur yalnız tercih/durum belgeleri güncellendi; level assetleri, kütüphane ve sprite'lar değiştirilmedi. Yeni level, otomatik toplu revizyon, build veya sim çalışması başlatılmadı.

## Güncel — 250’ye kadar yerel teslim hazır; üç booster koşusu izin bekliyor · 2026-11-07

**Yetki ve hedefler:** Kullanıcı 225’ten itibaren Mac simülasyonunu, sonuçlara göre hamle ayarını ve ardından 250’ye kadar tasarımı istedi. Geç dönem sonu1–4 oyuncu hedefi **%50–60**;5/9 için **%30 normal**.6–8 için önceki%40–50 yönü agent’ın açık çalışma varsayımıdır; yeni doğrudan6–8 kullanıcı cümlesi değildir. Bot yüzdesi insan yüzdesi değildir. Yayın/build/Unity UI için genel izin çıkarılmaz.

**225–234 tamam:** Hamleler sıra ile **29,23,26,29,32,30,28,35,21,46**. Yalnız moves değişti; maskeler, engeller, goal miktarları, meta/GUID/adlar aynı.232’nin5/5/5 elmas kotası ve231’in kullanıcı yerleşimi korunur.233:27→21, bot18/30=%60.234:24→46, bot11/20=%55; üç katmanlı v2’nin bütçesi24’te çok ağırdı.230:30 korunur;34 hamle denemesinde de can bitimi baskındı.225/228/232’de Dimerdal üretici önceliği botta eksik olduğu için oranı kovalamak amacıyla aşırı hamle eklenmedi. Kayıt: `Reviews/225-234-Balance-2026-11-07/Review.md`, `applied-moves.json`.632 gerçek oyun.234 önizlemesi artık hamleyi canlı assetten okuyarak46 gösterir; eski yazıcıları tekrar çalıştırma.

**235–250 tamam:** Aktif V1 katalog sonu250;16 yeni level/meta ve16 TR/EN ad eklendi. Önceki470 level/meta dosyası hash olarak korunur. Names.md250’ye uzatıldı. Batch: `Batches/235-250-Late-Game/`; finalmodel `designs.json`, guard kayıtları `written.json`/`finalized.json`, check `verify.py`, görsel `Contact.png` ve16 kök PNG/MD. `author.py --write` ve `finish.py` bir kez çalıştı, tekrar yazmayı reddeder. Güncel sim girdileri `SimLevels-Current`; ilk girdiler ve deneme klasörleri sabittir.

Yeni hamleler235→250: **18,16,26,30,33,24,24,21,30,24,24,25,16,17,22,36**.240=900 HP/320 oyuncu HP/3dalga;16 oyma→16 çamur ve2 depo,5goal.250=680 HP/340 oyuncu HP/3dalga;2 elma,10 elmas,16 fener,12 oyma ve boss,5goal. Çim42/58/69 veColorCrate71–74 yok. Yalnız gölge normal yuvalarına destekli sıradan pin, hazır special yok.245,238’e benzer kanatlardan L kaidesine değişti.241 EnergyContainer22 ortak enerji;242 BatteryBox23 dört renk×3=12 vuruş;246 Omega24 aynı iki-hit movable ailesinin görsel varyantı, bağımsız yeni mekanik sayılmaz.241/242’nin EnergyBall ve dolu pil goal ikonları düzeltildi; yeni runtime obstacle eklenmedi.

**Denge/kanıt sınırı:** Yeni batch862 gerçek oyun; bütün görev toplam1494. Yeni son bot örnekleri235%35,236%55,237%50,238%50,239%0/2hata,240%70,241%50,242%65,243%60,244%40,245%40,246%45,247%45,248%55,249%30,250%20.239/250’de Dimerdal üreticisi körlüğü baskın;244’te Cargo çevresi puanlaması/örnek varyansı nedeniyle insan%50–60 doğrulanmadı.240 kombo odaklı daha rahat boss olarak kaldı. Bu tasarımlar kullanıcı tarafından henüz oynanmadı/onaylanmadı. Öncelikli manuel deneme:234,239,244,250;230 için HP/karşı baskı ayrıca değerlendirilmeli. Rapor `Batches/235-250-Late-Game/Balance-Review.md`. Hata kayıtları kayıplara katılmaz; toplam11 runtime hata örneği saklıdır. CPU/GC/FPS ölçümü yok.

**Tek kalan istek: üç başlangıç booster’ı karşılaştırması.** LineH/PulseCore/SystemOverride için `RuntimeSimulationSession`/CLI `--boosters 3` desteği hazır. Gerçek dedicated injector kullanılır, envanter harcanmaz,3 teslimat raporlanmadan bot başlamaz. Ufo sim guard kaydedilmiş streak eklerini engeller; idle event aboneliği normal/iptal/disable/destroy yollarında temizlenir.21 Python test +56 izole sim C# +8 injector cleanup kontrolü PASS. Yeni kaynak kod mevcut Mac binary’sinde yok. **AGENTS.md açık build isteği olmadan build’i yasakladığı için async izin soruldu, henüz cevap yok. Build ve üç booster’lı koşu YAPILMADI.** İzin gelirse kullanıcıdan tekrar sim izni istemeden Macsim’i derle veya kullanıcının yeni derlemesiyle devam et; önce1 oyun booster smoke (raporda tam3), sonra225–234 son hamlelerle koş. UI kontrolü/yayın/release baseline/commit yok. Bütün sim süreçleri kapandı.

**176 kullanıcı tarafından bitirildi; iş kapalı, dokunma.**251 istenmedi.

## Güncel — 234 oynanışta kolay bulundu; v2 katmanlı revizyon · 2026-11-07

Kullanıcı234için önce “grid güzel ama çok rahat geçilir ... kullanıcı öğrendi ... katmanlı yapı kullanman lazım” dedi,sonra oynamayı seçti ve **“evet düşündüğüm gibi bu seviyede kolay bir level olmuş”** diye doğruladı. Bu gerçekv1oynanış geri bildirimidir;v2için denge/onay değildir. Beğenilen8×11/74aktif grid ve24hamle aynen korundu;zorluk sadece hamle azaltılarak değiştirilmedi.

Aktif234/Boya Dalgası v2: üst solda2ColorChest→2PaintCanBox;alt sol2×6kenar vesağ omuz2×2de16hücre **SculptingStone28→PlasticTwoStage40→Mud25**. Hedefler2/2/16/16/16,tam5goal. Üstoyma3hit,orta plastik2hit,altçamur2hit. Plastik movable’dır;örtü açılınca düşüp çamuru bırakabilir,sonradan ayrıca temizlenir. Yeni ham hücre-hit yükü oyuncu hamle sayısı veya ölçülmüş denge diye sunulmaz. Çim42/58/69veColorCrate71–74yok.49otomatik başlangıç yuvası+yalnız32indexinde1gerçekGearpini;kapaklar kalkınca gölge yuvası doğal beslenir.

Kayıt `Batches/234-Paint-Wave/active-revision.json`→`revise_v2.py`;v1asset/görsel `Revisions/v2-layered/Before/` içinde. Eski `author.py` aktif revizyonda çalışmayı reddeder. Güncelnot/PNG `Level234-Boya-Dalgasi.*`,3katman kesiti `Batches/234-Paint-Wave/Layers.png`. StatikYAML/dizi/katman/5goal/refill/koruma ve gerçekPNG kontrolleri geçti.6109diğerAssets dosyası aynı;isim/GUID/meta/katalog korunur. V2henüz oynanmadı;UnityUI/Play/build/bot-kaskat simülasyonu/yayın/baseline/commit veölçülmüşFPS yok. Manueltest kullanıcıda;235istenmedi.

**176 kullanıcı tarafından bitirildi: “senin176ile artık işin yok onu ben halletim”.** Ona yeni revizyon/önizleme çalışması yapma;eski26/5+5taslağını geri uygulama. Fikir önce,kenarda şekil kurma veyalnız dolum ulaşmayan açılış hücrelerine taş döşeme tercihleri `Design-Principles.md`, `Design-Feedback.md` velevel-designskill’ine işlendi.

**Ayrı tamamlanan görsel düzeltme:** Kullanıcı amaçladığı iki yanından yenmiş elmayı yeniden gönderdi. `Resources/FlyFruit/AppleHalf.png` tamyeniPNGbytes’ıyla değişti;GUID/importer/şeffaflık aynı. FlyFruit102aux[0],3/6lokmadan sonra bu sprite’ı seçer;mekanik,Goal/fullapple/ses değişmedi. Kayıt `Docs/Checks/FlyFruit-Bitten-Apple-2026-11-07/`;234v2koruma snapshot’ı bu yeni görseli zaten içerir.

**Kalıcı iş sırası — kullanıcı,2026-11-06:** “önce bir fikir belirlemen önemli ... önce tarzımı seçiyoruz”. Önce ana fikir/tarz,şekil veilk oyuncu kararı seçilip paylaşılır;sonra yerleşim yazılır. Sonradan rastgele engel dizisine isim/tema uydurma. Bu yöntem beğenildi;234için önce açıklanan fikir Boya Dalgası’dır. Yeni bir onay zorunluluğu çıkarılmaz;görsel/oynanmış onay ayrıca kaydedilir.

## Güncel yetki — 176 yaklaşımı beğenildi; yalnız 234 · 2026-11-06

Kullanıcı176için “çok iyi oldu ... biraz değiştirdim ama mantığını sevdim” dedi. **Canlı kullanıcı176=23hamle/9kırmızı+9yeşil**, aynı8×8maske; bu yeni hali koru, eski26/5+5modeline geri basma. Kabul edilen yaklaşım: yok olan engeller kenarlarda maskeyle birlikte okunabilir şekil kurar, açılış/refill kararına hizmet eder ve gereksiz engel-altı boş cepleri azaltır. Bu bir görsel/yaklaşım beğenisidir, agent’ın eski hamle dengesi onayı değildir.

Sonraki açık görev **yalnız234**, üstelik “234farklı ona farklı yaklaş”.176V’si veya233altkilidi kopyalanmaz;çim ailesini ve yeni tasarımlarda ColorCrate71–74’ü dinlendirme yönü sürer. Kenar kullanmak bütün levelları aynı çerçeveye dönüştürmek değildir.

**Yeni açık başlangıç taşı istisnası:** Normalde otomatik dolum kullanılır. Yalnız açılışta doğal olarak hiç taş ulaşmayan normal hücrelere gerçek başlangıç taşı döşemek uygundur; boş ekran görüntüsü olmasın. Tüm tahtayı pinleme veya hazır special zorlama değildir. Pinin açılışta desteklenmesi/yerinde kalması ve kapak açıldıktan sonraki refill erişimi ayrı doğrulanır. Kaynak ifadeler `Batches/234-Paint-Wave/user-feedback.json`; kapsam `Design-Principles.md` ve `Design-Feedback.md` başında.235istenmedi.

## Güncel — V1 176 kopuk kutu yerleşimi reddi ve revizyonu · 2026-11-06

Kullanıcı `levelv1_00001760` için “bu bizim kuralımıza uymuyor ortaya saç sapan bir kutu koymak doğru değil” dedi ve mevcut zorluk derecesine uygun yeniden tasarım istedi. Bu açık ret, eski176 için “rahat geçtik/dokunulmadı” kaydından önceliklidir. Yetki yalnız176’dır;233 veya başka level revizyonu çıkarılmaz.

Aktif V1’de176 **Kızıl ve Yeşil / Crimson and Green**, `LevelCL_0176`, GUID `f6068583d99e4456b6e205a8bf6f553b`; tarihsel176Dolaplı Geçit ile karıştırılmaz. Eski7×7içindeki iki kopuk5’li şerit yerine8×8/54aktif daralan çanak ve kenarı tamamlayan tek10hücrelik kırmızı/yeşilV konturu kuruldu. Her renk5hücre ve bağlıdır. **26hamle Normal,5+5hedef,üç-hit sandıklar korunur.** Sonu6 rahat tanıtımdır; Hard’a çevrilmez. ColorCrate72/74 aktifV1ilk kullanım176 olduğu için bu mevcut tanıtımda korunur; yeni geç dönem tasarımlarında aileyi dinlendirme tercihi sürer. Çim ailesi yok.

44/44başlangıç normalyuvası doğal besleme; tek renk temizlenince49/49, tümü kalkınca54/54. Pin/hazırspecial/boşcep yok. Önce/sonra ve2/176/178gerçek sprite karşılaştırması açıldı;2ile alt çanak benzerliği kabul edilir,benzersizlik iddiası yok. Kayıt `Batches/176-Contour-Revision/`, aktifnot/PNG `Level176-Kizil-ve-Yesil.*`, inceleme `Reviews/176-Contour-Revision/`.

Yalnız176assetinin boyut/hücre dizileri değişti; isim,katalog,meta/GUID,müzik,6107diğerAssets dosyası ve232/233/108kullanıcı değişiklikleri korunur. Önceki233koruma snapshot’ının bu sonradan yetkili176değişimini drift sayması normaldir; eskiauthor’la176’yı geri basma. StatikYAML/hedef/katman/akış/koruma ve agentgörsel kontrolü geçti; yeni176henüz kullanıcı onaylı/oynanmış değil. UnityUI/Play/build/bot-kaskat simülasyonu/yayın veya ölçülmüşdenge/FPS yok. Manuel test kullanıcıda.

## Güncel — 233 Kutup Kilidi; çimi dinlendirme yönü · 2026-11-06

Kullanıcı yalnız233’ü istedi: “en güzel levelını yap”; ardından **“çim istemiyorum ama artık”** dedi. Yeni tasarımlarda çimi otomatik varsayılan örtü olarak kullanma;233’te Grass42,GrassFlower58,GrassWaterLily69 tamamen dışarıda. Eski onaylı levellardan çim silme yetkisi değildir. Bu açık son yön eski “çimi üstte kullan” tercihiyle çeliştiği yerde önceliklidir.

233 → `LevelV1_00002330.asset` / `LevelCL_0233` / chapter10; **Kutup Kilidi / Polar Lock**,8×11/82aktif,27hamle Normal. Alt orta iki2×2Safe,soldan kırmızı→sarı→yeşil2/1/1,sağdan yeşil→sarı→kırmızı2/1/1;her biri4Cargo gizler.12hücrelik tekU mıknatısı,sekiz special-only pusula çıkış hücresi ve12porselen kenar sütunu. Beş hedef8Cargo/2Safe/1Magnet/8SculptingSpecial/12Porcelain. Dimerdal/FlyFruit de dinlendirildi;üst çalışma avlusu42/42doğal dolum,pin/hazırspecial/başlangıçboşluğu yok. Refill special koruması auto(-1). Çıkış bandı kasalardan önce temizlenirse8alt hücre geçici gölgede kalır;kasa açılınca beslenir.

**232’nin güncel kullanıcı ayarı32hamle ve5+5+5Dimerdal=15elmas korunur.** Önceki28hamle/6+6+6kayıtları ilk agent taslağıdır;geri uygulanmaz. Çalışma sırasında108dışarıdan değişti;mevcut hali ayrı kaydedilip korundu. Yeni Dimerdal sesleri ve örtü-altı hit düzeltmesi de korundu.

Kayıt `Batches/233-Polar-Lock/`;gerçekPNG/not `Level233-Kutup-Kilidi.*`,232önceki katalog incelemesi `Reviews/233-Polar-Lock/`.142alt kütle/iki kasa bakımından benzer,198mıknatıs için karşılaştırıldı;benzersizlik iddiası yok.6103diğerAssets dosyası hash olarak aynı;2append-onlykatalog/isim ve1dış108ayrı. Statik YAML/katman/hedef/dolum/çıkış ve gerçek sprite görsel kontrolleri geçti. Kullanıcı henüz233oynanışını/görselini onaylamadı;27hamle ilkayar. UnityUI/Play/build/bot-kaskat simülasyonu/yayın/releasebaseline/commit veya ölçülmüşdenge/FPS yok. Manuel test kullanıcıda;234istenmedi.

## Güncel — 232 Kristal Hilal hazır; kullanıcı 231’i korundu · 2026-11-06

Kullanıcı 231’i kendisi tamamladı ve yalnız 232 istedi: board şekli, az kullanılan aileler, özellikle katmanlar; örnek olarak renkli Dimerdal tarafı ve ayrı 2×2 elma. Aktif V1 katalogda 231 → GUID `2c9e5fc93433340bea1dc11fc1796fd8` çözüldü; asset/meta ve önceki bütün level kayıtları korundu.

232 → `LevelV1_00002320.asset` / `LevelCL_0232` / chapter10; TR Kristal Hilal, EN Crystal Crescent. 9×11, 73 aktif (69 ana hilal + 4 hücre ayrı ada), 28 hamle Normal. Solda bitişik üç2×2 mavi/yeşil/kırmızı Dimerdal, her biri6; iç kenarda6 GrassFlower üstünde4 Hamster (kişi başına4 lokma); alt kolda1 FlyEggBox ve1 adet2×2 elma, ayrı adada ikinci2×2 elma. Hedefler18 elmas /2 elma /4 hamster /6 çiçek /6 çim. Çiçek→çim→hamster açılması gerçek yığındır. Bir kutunun4 yumurtası iki meyveye yeter. Sıçrama yardımı üretim+elmas toplama+meyve lokması işinin yerine sayılmaz.

45 normal başlangıç yuvası statik dolumda45/45 besleniyor; pin/hazır special/boş cep yok. Sonraki kesitlerde49/61/73 normal yuva besleniyor. Kayıt `Batches/232-Crystal-Orchard/`; not/PNG `Level232-Kristal-Hilal.*`; tekrar envanteri `Reviews/232-Crystal-Crescent/` ve gerçek sprite231/73/174 karşılaştırması var. 6.097 önceki proje dosyası korundu; eski Assets dosyalarında yalnız katalog + TR/EN isimlere232 eklendi. Runtime/library/akış/GUI kodu değişmedi.

Statik YAML/katman/footprint/hedef/erişim ve gerçek PNG görsel incelemesi geçti. Bu agent değerlendirmesidir; kullanıcı oynanmış denge veya görsel onayı değildir. UI/Unity/Play/build/bot-kaskat simülasyonu/yayın/baseline/commit yok. Manuel test kullanıcıdadır; 233 istenmedi. 231 isim kataloğunda kullanıcı adı henüz yoktu; yeni isim uydurulmadı ve level dosyasına dokunulmadı.

## Güncel — elma önceden boyut seçimi ve ses/uçuş düzeltmesi · 2026-11-06

Kullanıcı elmanın Safe gibi yerleştirmeden önce Width/Height ile ayarlanmasını istedi. Elma modu eklendi;paletten Elma seçmek bu moda geçer. Sliderlar yalnız sonraki yerleştirmeyi ayarlar. Mevcut elma boyut listesi/tıklayarak resize kaldırıldı;dolu alanın üstüne yerleştirme bütün footprint için atomik reddedilir. Örtü/kasa/mühür/tüp/mıknatıs/kanal/pinli içerik korunur. Elma adedi Goals/PreLevel hedefidir.

Sinek sesi kullanıcıya düşük geldi: ilk artışa son istekte+3dB daha eklendi, toplam yaklaşık+11.6dB;yumurta uçuşu+6.5dB ve yükselişin0.12s anında merkezi ses çağrısı. Yumurta yükselişte0.65→2.0 büyür,inişte1.0olur;hedef yeniden seçilince boyut/açı sıçramaz. Sineklerin2.1tepe ve farklı rastgele yolları korunur. Son kullanıcı isteğiyle üçlü formasyon0.65→1.05hücre aralığına açılır; yükselişte kademeli ayrılır, ayrı kavis/iniş bölgeleriyle toplu ama daha açık uçar.357izole yöntem kontrolü ve WAV/asset auditPASS;Unity/UI/Play çalıştırılmadı,manuel test kullanıcıdadır. Level tasarım işi açılmadı;mevcut levellar korunur.


## Güncel — editör erişimi, sesler ve kullanıcı test tercihi · 2026-11-06

Kullanıcı yeni engellerin LevelEditor’da görünür/seçilebilir/yerleştirilebilir olmasını kalıcı kural yaptı. AGENTS.md’ye eklendi. FlyFruit ailesi palette Elma / Sinek Yumurtası / Sinek Kutusu isimleriyle üstte seçilebilir; arama Türkçe ve enum adını destekler. Yanlış baykuş/oyma taşı görüntüsüne yol açan100–102legacySprite yedekleri düzeltildi; kopyadan kalan wardrobeItemSprites temizlendi. Son görsel doğrulama kullanıcıdadır.

Kullanıcı “ben test ederim, sen normal işleri yap” dedi: açıkça istemedikçe bilgisayar/Unity/browser/native UI kontrolü yok. Build/Play/simülasyon/test leveli oluşturma yok. Dosya/kod ve izole yöntem kontrolleri yapılabilir; manuel editör/oyun testi kullanıcıya aittir. Bu tercih önceki UI doğrulama beklentisini değiştirir.

Beş yeni FlyFruit WAV sessizlik/gain/peak/loop düzenlemesiyle merkezi ses sistemine bağlandı; Downloads orijinalleri korundu. Sinekler kullanıcının son isteğiyle yükselirken0.85→2.10 büyür, ayrılmada1.20’ye ve elmaya yaklaşırken1.00 normal boyuta döner. Son kullanıcı isteğiyle yükseliş/kavis/zamanlama/salınım ve elmadaki iniş noktaları hafif rastgeleleştirildi; parametreler uçuş başında bir kez seçilip havuzdaki sürüde saklanır. Kayıtlar Docs/Checks/FlyFruit-Editor-Visibility-2026-11-06 ve FlyFruit-Audio-2026-11-06.354izole mekanik ve12ses sahipliği kontrolü; Unity oynanış veya performans ölçümü değildir. Kullanıcının231dahil mevcut levelları korunur; level tasarım görevi açılmadı.


## Güncel — sinek kutusu / yumurta / meyve mekaniği · 2026-11-06

Kullanıcı yeni engel ailesini verdiği görsellerle uygulamamızı istedi. Son kural önceki5sinek/tek sürü bir meyve fikrini değiştirir: FlyEggBox100special başına bir yumurta,dört başarılı teslimden sonra kırılma;FlyEgg101tek vuruşlu1×1movable,normal match veya special kırar;her yumurtadan3sinek,elma6lokmayla(iki sürü) biter. FlyFruit102tekID,LevelData.fruits üzerinden origin başına Width/Height;1×1ve2×2aynı şekilde1meyve hedefi. Goals/PreLevelyalnızmeyve;bu aile için kutu/yumurta hedefe eklenmez. Diğer ailelerin hedefleri korunur.

Editör placement/resize/erase/katman-footprint ve otomatik hedef sayımı eklendi. Meyve direct hasarla kırılmaz;sinekler çevreye hasar vermez. Eşzamanlı sürüler lokma rezerve eder. Yumurta yalnız aynı lifetime’da hareketsiz normal taşa iner;hareketlenirse retarget,başarısızlıkta kaynak yuvası geri gelir. Uçuş EggBird ritminden yararlanır;ön/sağ/aynalısol görünüm,ayrı kanatlar,tek fly atlası,havuzlu24aktif sinek. Yeni obstacle-local ses kaynağı yok.

Kayıt `Docs/Checks/FlyFruit-2026-11-06/README.md`;9kaynak görsel saklandı,asset ve parçaPNG açıldı. Tüm mevcut level/katalog/ad assetleri aynı;bu aile henüz numaralı bir levele eklenmedi.229v4ve225/226kullanıcı düzenlemeleri korunur. Unitybuild/Play/cihaz/denge/FPS testi/yayın/baseline/commit yok.231+level tasarımı yetkilendirilmedi.


## Güncel — nilüfer yönlü yutma görseli tamamlandı · 2026-11-06

229v4level işi tamamlandıktan sonra kullanıcı dört lilly sheet adımıyla yönlü special yutma istedi. Grid yönüne göre Up/Right/Down/Left açık→kapalı kareleri kullanılır;sol kaynak ters sıradadır. Dönüş yalnız kapalı kareyle,merkezde eskişişkinCF5ile biter. EskiCF1–CF5,yaprak/hasar/hedef/bekleme/geri fırlatma/ses kuralları korundu.

Kayıt `Docs/Checks/WaterLily-Directional-Swallow-2026-11-06/README.md`;662üretim-yöntemi kontrolü ve20spritebağlantısı/6054dosyakoruma auditPASS. Gerçek kareler/statikTimeline açıldı;Unitybuild/Play/cihazrender/ölçülmüşperformans/yayın/commit yok. Bu ayrı görsel iş yalnızGrassWaterLilyService,library69auxlistesi ve dört yeniatlası değiştirir;229v4vebütün diğerlevelassetleri aynı.229v4:28hamle,12nilüfer/4dolap/12oyma/12iki aşamalı plastik;henüzoynanmadı/onaylanmadı. Eski229koruma snapshot’ının bu sonradan yetkiliiki dosya değişimini drift sayması beklenir;hashleri sessizce güncelleme.231+istenmedi.


## Güncel — 229 jel/pres reddi ve v4 · 2026-11-06

Kullanıcı229v3jel/presi erken bitecek oyun ve kuralları anlamama gerekçesiyle reddetti;devam istedi. Açılış sonrası kalan gerçek iş değerlendirilmeden statik PASS/PNG ile SuperHard iddia edilmez. Bu yalnız229revizyonudur.

Aktif229v4:9×11/99aktif,28hamle SuperHard;12GrassWaterLily69/4Wardrobe27/12SculptingStone28/12PlasticTwoStage40. Jel/pres/çim-altı-sandık yok.47açık eşleşme yuvası +12kilitli nilüferaltı taş=59/59doğal dolum,pin0,boş0. İlk nilüfer speciali sonraki yaprak/çimi;dolap kapağı içeriği bitirmez. Mevcut isim/GUID/katalog korunur.

Kayıt `Batches/226-230-Lantern-Wind-Crystal/Revisions/v4-229-lily-workshop/`;root229not/PNG güncel.6055diğer proje dosyası/diğer levellar ve225/226kullanıcı düzenlemeleri korundu. Eski parentauthor/226modeli çalıştırılmaz. Statik kontrol PASS;gerçek229PNG ve3/126karşılaştırması açıldı;şekil benzersiz veya denge ölçülmüş diye sunulmaz.28hamle ilkayar,yeni taslak kullanıcı onaylı/oynanmış değil. Build/Play/bot/düşüş-kaskat simülasyonu/yayın/baseline/commit yok.231+istenmedi.

Sıradaki açık iş: kullanıcı 229bitince dört yönlü lillydown/left/right/up sprite sheet adımlarıyla nilüfer special yutma animasyonu istedi. Geri toplanırken kapalı kareden mevcut şişkin görsele geçilecek. Bu ayrı yetkili görsel iştir;level geometrisini değiştirmez.


## Güncel — yalnız229 yeni tasarım · 2026-11-06

Kullanıcı229 için yeni level istedi;“çim altında sandık kullanma” dedi. GrassFlower58 ve GrassWaterLily69 bütün havuzda değerlendirilecek seçeneklerdir; bu levele hemen eklenmeleri gerekmiyor. Bu açık istek,önceki “levelı ben tasarlayacağım” kapsamını **yalnız229 için** değiştirir;diğer levelları yeniden tasarlama yetkisi vermez.

229 Rüzgâr Burgusu / Crystal Whirl yeni taslak:9×11/99aktif,28hamle SuperHard;12jel tabanı üstünde12oyma,sağda12porselen,tabanda iki yukarı jel presi ve ikiRodos. Hedef99jel/12oyma/2Rodos/12porselen. Çim/sandık yok;63/63başlangıç yuvası doğal beslenir,pin0,boş0. Güncel kayıt `Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/README.md`; root229not/gerçekPNG güncel. Eski parenttopluPNG229 için tarihseldir.

6055diğer proje dosyası ve1–228/230 aynı;225=27hamle,226=20hamle ve mevcut kullanıcı geometri değişiklikleri korundu. Eski226model/hash’ini kullanıcı assetine benimsetme veya eski batchauthor’ı çalıştırma.229isim/GUID/meta/katalog korundu. Statik YAML/katman/hedef/footprint/besleme kontrolleri PASS;114/183gerçek sprite karşılaştırması ve yeniPNG açıldı. Bu agent görsel değerlendirmesidir;28hamle ilkayar,yeni229henüz oynanmadı/onaylanmadı. Runtime/art/akış kodu değişmedi;build/Play/bot/düşüş-kaskat simülasyonu,yayın/baseline/commit yok.231+istenmedi. Alttaki eski229çim/sandık/27hamle kayıtları tarihseldir.

## Güncel — PatchBot special / duvar zayıf noktası / değirmen önceliği · 2026-11-06

Kullanıcı tek PatchBot’un Wall zayıf hücre seçiminin doğru olduğunu, PatchBot+special kombosunun yanlış seçtiğini netleştirdi. Sonraki açık yön: değirmen un kaynağı olduğu için hedef sayılmalı, mevcut undan önce seçilmeli. Bu tur yalnız bu hedefleme/iniş/Pulse merkezi düzeltmeleri yetkilidir; level tasarımı kullanıcıdadır ve assetler değiştirilmez.

Kombo puanlayıcısı Wall/MetalWall parçasında en yüksek aşamadaki vurulabilir hücreleri aday yapar; eşit aşamada alan faydası karşılaştırılır. Dikdörtgen duvarın merkezine geri kaydırma dash görselinden ve sanal Pulse’tan kaldırıldı. Tek botun Wall seçim algoritması korunur. Aktif Flour hedefi Windmill’i de kaynak hedef yapar; tek botta bloklayan hedef grubunda un zemininden önce gelir, komboda uygun/rezerve edilmemiş değirmen varken doğrudan Flour hedefi ertelenir. Diğer hedef engellerin önüne koşulsuz birincilik verilmez; HUD’da hâlâ un hedefi vardır. Dolgu taşı merkezleri Wall/kaynak varken öncelik çalmaz; gerçek taş hedefleri/cargo/jel seçenekleri korunur.

Kayıt `Docs/Checks/PatchBot-Wall-WeakPoint-2026-11-06/README.md`. Eski üretim yöntemleriyle sorun yeniden üretildi;260 yeni +147 mevcut hedefleme +85 mıknatıs/uç/merkez kontrolü geçti. Mevcut snapshot dizileri/scorer yeniden kullanılır; yeni flight-tick taraması veya efekt/ses kaynağı yok.6052 diğer proje dosyası ve bütün level/katalog/adlar aynı. Unity build/Play/cihaz/bot/düşüş-kaskat simülasyonu,yayın/baseline/commit veya ölçülmüş FPS yok.226’daki kullanıcı düzenlemeleri korunur; eski taslak assetine döndürülmez.

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

## 108 Altın Kupa — hamle 32→37 (kullanıcı kararı) · 2026-11-13

Kullanıcı 108'i oynayınca "çok zor geldi, ardından 109 geliyor, iki zor üst üste" dedi. Agent analizi (ölçülmüş denge değil): ~100 vuruş iş (16 kenar Sandık3+altında çamur, 25 çamur, 8 PlasticTwoStage, 12 Sandık1), kupa içinde ~18 serbest hücre, ayak sandıklarının çoğuna yalnız special/üst kırılınca ulaşılıyor → x8 normal level için fazla, 109'dan (süper zor) ağır. Kullanıcı yalnız hamleyi seçti: **37**. Şekil/engeller/hedefler değişmedi (kullanıcının daha önce elle düzenlediği tahta). Hâlâ zorsa sonraki adım: kenar Sandık3→Sandık2.

## 217 Kanatlı Kum Saati — hamlesiz açılış düzeltmesi · 2026-11-14

Kullanıcı: "217'de hamle şansı yok, oto shuffle çalışmıyormuş kullanıcılarda." Kök: gövdedeki 35 pin çapraz 1-2-3-4 deseninde, olası kaydırma = 0; rastgele hücreler yalnız üst köşelerde 6 tane ve izole (eşleşme kuramaz) → açılış garantili hamlesiz. Motorun "hamle yok → karıştır" kontrolü yalnız oyuncu hamlesinden sonra çalışıyor, açılışta yok → oyuncu kilitli. Düzeltme (yalnız pin rengi, şekil/engel/hedef/hamle aynı): (4,3) 4→3 ve (5,6) 3→2 → 6 oynanabilir hamle (üst ve bel bölgesi), açılışta hazır eşleşme yok. Tüm V1 taraması: garantili hamlesiz açılış yalnız 23 (kargo tasarımı) ve 27 (bilinçli karıştırma öğreticisi). Yedek: scratchpad LevelV1_00002170.bak.asset. **Kural: pinli tasarımda en az birkaç oynanabilir hamle bırak; doğrula.**

## 1–25 hamle ayarı (oyuncu verisi) · 2026-11-14

Kullanıcı: "25'e kadar bak, gerekiyorsa artıralım." Kaynak: GA4 level_end 8–14 Kas (testçiler dahil, küçük örneklem) + iş yükü. Gözlem: oyuncular İLK KAYIPTA bırakıyor (L1, L6, L9, L19). Kazanma oranı düşük olanlar artırıldı (yalnız moves; şekil/engel/hedef aynı): **L1 30→40** (%79), **L4 33→38** (%67), **L9 25→28** (%60), **L15 26→30** (%46), **L19 18→21** (%62), **L23 25→32** (%23, 17 kayıp — bloğun 3. levelı ama en zoru). Diğerleri %86–100, dokunulmadı. Canlı için aynı değerler Firestore config/levels moves'a önerildi; build yayılınca Firestore'dan silinebilir.
