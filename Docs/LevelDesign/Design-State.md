# Level tasarım çalışma durumu

## 251 Kapadokya Sabahı — Sıcak Hava Balonu tanıtımı · 2026-11-09 · kullanıcı denemesi bekliyor

Kullanıcı: “level 251 i tasarlarmısın.. şekilli olsun ama sol ve sağ köşelerde balonlar olsun … alt tarafı `__XXXXXXXXX__` gibi konumlandırabilirsin”. Yorum (agent): tahta 9 geniş olduğundan `__` = alt köşelerde 2×2 balonlar, `X` = aradaki 5 hücrelik taban sırası. Yeni ekleme: `LevelV1_00002510.asset` / `LevelCL_0251` / GUID `d0d755ef8c224c99a96aee1c2f0b278b`, katalogda chapter 11 (tema yoksa son temaya düşer). 1–250 dokunulmadı. Batch: `Batches/251-Balloon/` (author.py, render.py, design-251.json, Level251.png, Before/).

| Lv | Ad | Zorluk/hamle | Siluet + engeller | Hedefler |
|---|---|---|---|---|
| 251 | Kapadokya Sabahı / Cappadocia Morning | Normal 24 | üstte şapkalı 3 peri bacası (hole’lu boyun), alt köşelerde 2 HotAirBalloon103 (dolap içinde; yalnız special/joker, 3. vuruşta dolabı yarıp uçar, kendi 2 sütununu temizler), aralarında tabanda 5 SculptingStone28, baca şapkalarında 3 Lantern75 (düşer) | 2 balon, 5 oyma, 3 fener |

Statik: 87 aktif, 71 normal yuva %100 doğal dolum, pin/hazır special yok; çim/BatteryBox/Omega yok. Balonun uçuşu sol/sağ kenar sütunlarını tepeye dek tarar (düşen fenerler yolda vurulur). Oynanmadı; Unity/build/sim yok; 24 hamle ilk tahmin.

## 240–250 doğa siluetleri — 241–249 yazıldı · 2026-11-07

Kullanıcı: “240 ve sonrası … son engellerden (lamba, yel değirmeni, flyegg) … hole'larla güzel şekil, doğadan esinlen”; 241/242 için “süper olmuşlar, onları oynayayım, sen diğerlerine devam et”. Mevcut GUID/katalog/levelKey korunur; yalnız asset içeriği ve TR/EN ad değişti. Eski asset/not/PNG: `Batches/240-250-Nature/Before/<n>/`.

| Lv | Ad | Zorluk/hamle | Siluet + engeller | Hedefler |
|---|---|---|---|---|
| 240 | Gökkuşağı | Boss 30 (650 HP) | 2 bantlı ColorCrate gökkuşağı (kırmızı 15×1, sarı 13×2), kemer altı 3 sütun kuytu; kullanıcı “kazanılamaz” → depolar/birikinti hedefi çıkarıldı, bulut köşeleri taş alanı | boss, 15 kırmızı, 13 sarı |
| 241 | Lale Tarlası | Normal 28 | lale + sap + yaprak; toprakta 2 Windmill80 | 16 un, 40 kırmızı |
| 242 | Elma Ağacı | Normal 26 | taç tamamen oyun alanı; 4 elma adacıkta (köşelerde 2×1×1, gövde yanında 2×2×2), 2 BirdHouse, 2 FlyEggBox | 4 elma, 12 kuş |
| 243 | Yusufçuk | Normal 26 | baş/gövde/4 kanat; kanat uçlarında 4 L LampStrip79 (2 kayar, 2 mor) | 4 çerçeve, 30 mavi |
| 244 | Dağ Deresi | Normal 27 | tek dağ; zirveden kıvrılan dere + sağ yamaçtan eteğe inen yan dere (kullanıcı: su animasyonu görünsün), 7 kaya | 2 kanal, 7 oyma |
| 245 | Mantar Ormanı | Hard 27 | 3 mantar; 2×2 Dimerdal (2 kırmızı büyük şapka altında 6 komşulu, 2 sarı yan şapkada 4 komşulu), altları hole | 12 elmas |
| 246 | Kuş Yuvası | Normal 26 | dal + sarkan yuva; dal uçlarında 2 BirdHouse, yuvada 6 EggBird | 6 yumurta, 12 kuş |
| 247 | Şelale | Normal 27 | yamaç + dar boğaz + gölet; şelale kanalı 6 kaya altında, mağarada 4 Wall→Lantern | kanal 1, duvar 4, fener 4, oyma 6 |
| 248 | Ayçiçeği | Normal 24 | hole’lu taç + sap; göbekte 9 Lantern, çevresinde 12 sarı Plastic_Yellow taç yaprağı (kullanıcı: ilk hâli çok kolay) | 9 fener, 12 yaprak, 25 sarı |
| 250 | Hasat Bayramı | Boss 24 (950 HP, saldırı 3 hamlede bir; kullanıcı iki kez “kolay”) | köşe adacıklarında 2×2 elma, ortada 9 fener direği, 2 Windmill, 1 FlyEggBox; 340 oyuncu HP, 3 dalga | boss, 2 elma, 9 fener, 12 un |
| 249 | Meyve Bahçesi | Super Hard 26 | ayrık iki taç; altlarından sarkan 4×1×1 elma, 2 BirdHouse, 2 FlyEggBox | 4 elma, 16 kuş |

Hepsinde normal yuvalar statik kontrolde %100 doğal dolum, pin/hazır special yok; çim/BatteryBox/Omega yok. Oynanmadı, sim/build/Unity yok; hamleler ilk tahmin. 240 boss: kullanıcı “239’da sculpting stone kırıyorum 240’ta yine aynısı geldi, kullanıcıyı çok sıkar” → oyma/çamur kaldırıldı. Ardışık levellarda aynı ana iş tekrar edilmez. 250 yazıldı (Hasat Bayramı). Kural: elma = kutu×2. Kuş evleri tek değil grup (1×2…2×4) — 242/246/249 ikişerli gruplara çevrildi.

## 235 v2 — tek kütleli kemer hazır; kullanıcı değerlendirmesi bekliyor · 2026-11-07

Bu tur yalnız235 revize edildi. AktifV1 `LevelV1_00002350.asset` / `LevelCL_0235` / GUID `a8b9134ba6ea4b7da0bcec0d0c72d0fb`; isimler **Bakır Kemer / Copper Arch** aynı. 9×11/85 aktif, **28 hamle ilk ayar · Hard**. Alt kenardan yükselen tek kütle:19 oyma→19 çamur, iki2×2heykel altında8çamur daha. Sol14mavi/sağ14yeşil, ardından her heykelde3special. Hedef2/19/27. İlk U taslağı176/182benzerliği nedeniyle bırakıldı;234’ün üçlüplastikyığını tekrarlanmadı.

Açılış58/58,oyma kalkınca77/77,heykeller kalkınca66/66,ikisi kalkınca85/85 normal yuva statik beslenir. Tek heykel açılması da kontrol edildi. Pin/hazırspecial/başlangıçboşcebi yok. Yeni28hamle oynanmadı/simüle edilmedi; eski18hamle/%35bot tarihseldir. Yeni kullanıcı görsel/oyun onayı yok.

`Batches/235-Copper-Arch-v2/README.md`, `design.json`, `revise.py --verify`, `validation.json`; not/PNG `Level235-Bakir-Kemer.*`; `Reviews/235-Copper-Arch-v2/`. Bir seferlik yazıcı uygulandı. Master modelde yalnız235 güncel, eskiSimLevels/rapor/hashler tarihsel. Uygulama sonrası yedi235alanı dışında6.985Assetsdosyası/meta/katalog/isimler aynı. Kod/library/editor/audio değişmedi. Statik ve PNG kontrolleri geçti; build/Play/sim/UI/yayın/baseline/commit yok. Manuel test kullanıcıda; ayrı236çalışmasına müdahale edilmez.

## Güncel — 235–250 görsel olarak beğenilmedi; BatteryBox/Omega kullanım dışı · 2026-11-07

Kullanıcının son eleştirisinin kapsamı **235 ve sonrası, dün ve bugün yapılanlar**; mevcut teslimde **235–250**. Grubun görsellerini beğenmediğini açıkça bildirdi. Aşağıdaki “yerel teslim hazır/tamam” kayıtları dosya ve teknik teslim geçmişidir; görsel kabul sayılmaz. Bu geri bildirim önceki levellara genellenmez. Tek tek kompozisyon kusurları henüz kullanıcı tarafından ayrıntılandırılmadı.

**BatteryBox23 ve OmegaModul24: şimdilik KULLANILMAYACAK.** Mevcut görselleri kullanıcı tarafından beğenilmiyor; uygun imaj bulunursa ileride yeniden değerlendirilebilir. 242 ve 246 bu engelleri kullanan mevcut taslaklardır. Kütüphane/runtime/editor kaldırılmadı; level assetleri değiştirilmedi. Bu tur geri bildirim ve tasarım tercihi kaydıdır; toplu yeniden tasarım veya sonraki level başlatılmaz. Ayrıntı: `Design-Feedback.md` ve `Obstacle-Design-Guide.md` başı.

## Güncel — 250’ye kadar yerel teslim hazır; üç booster koşusu izin bekliyor · 2026-11-07

**Yetki ve hedefler:** Kullanıcı 225’ten itibaren Mac simülasyonunu, sonuçlara göre hamle ayarını ve ardından 250’ye kadar tasarımı istedi. Geç dönem sonu1–4 oyuncu hedefi **%50–60**;5/9 için **%30 normal**.6–8 için önceki%40–50 yönü agent’ın açık çalışma varsayımıdır; yeni doğrudan6–8 kullanıcı cümlesi değildir. Bot yüzdesi insan yüzdesi değildir. Yayın/build/Unity UI için genel izin çıkarılmaz.

**225–234 tamam:** Hamleler sıra ile **29,23,26,29,32,30,28,35,21,46**. Yalnız moves değişti; maskeler, engeller, goal miktarları, meta/GUID/adlar aynı.232’nin5/5/5 elmas kotası ve231’in kullanıcı yerleşimi korunur.233:27→21, bot18/30=%60.234:24→46, bot11/20=%55; üç katmanlı v2’nin bütçesi24’te çok ağırdı.230:30 korunur;34 hamle denemesinde de can bitimi baskındı.225/228/232’de Dimerdal üretici önceliği botta eksik olduğu için oranı kovalamak amacıyla aşırı hamle eklenmedi. Kayıt: `Reviews/225-234-Balance-2026-11-07/Review.md`, `applied-moves.json`.632 gerçek oyun.234 önizlemesi artık hamleyi canlı assetten okuyarak46 gösterir; eski yazıcıları tekrar çalıştırma.

**235–250 tamam:** Aktif V1 katalog sonu250;16 yeni level/meta ve16 TR/EN ad eklendi. Önceki470 level/meta dosyası hash olarak korunur. Names.md250’ye uzatıldı. Batch: `Batches/235-250-Late-Game/`; finalmodel `designs.json`, guard kayıtları `written.json`/`finalized.json`, check `verify.py`, görsel `Contact.png` ve16 kök PNG/MD. `author.py --write` ve `finish.py` bir kez çalıştı, tekrar yazmayı reddeder. Güncel sim girdileri `SimLevels-Current`; ilk girdiler ve deneme klasörleri sabittir.

Yeni hamleler235→250: **18,16,26,30,33,24,24,21,30,24,24,25,16,17,22,36**.240=900 HP/320 oyuncu HP/3dalga;16 oyma→16 çamur ve2 depo,5goal.250=680 HP/340 oyuncu HP/3dalga;2 elma,10 elmas,16 fener,12 oyma ve boss,5goal. Çim42/58/69 veColorCrate71–74 yok. Yalnız gölge normal yuvalarına destekli sıradan pin, hazır special yok.245,238’e benzer kanatlardan L kaidesine değişti.241 EnergyContainer22 ortak enerji;242 BatteryBox23 dört renk×3=12 vuruş;246 Omega24 aynı iki-hit movable ailesinin görsel varyantı, bağımsız yeni mekanik sayılmaz.241/242’nin EnergyBall ve dolu pil goal ikonları düzeltildi; yeni runtime obstacle eklenmedi.

**Denge/kanıt sınırı:** Yeni batch862 gerçek oyun; bütün görev toplam1494. Yeni son bot örnekleri235%35,236%55,237%50,238%50,239%0/2hata,240%70,241%50,242%65,243%60,244%40,245%40,246%45,247%45,248%55,249%30,250%20.239/250’de Dimerdal üreticisi körlüğü baskın;244’te Cargo çevresi puanlaması/örnek varyansı nedeniyle insan%50–60 doğrulanmadı.240 kombo odaklı daha rahat boss olarak kaldı. Bu tasarımlar kullanıcı tarafından henüz oynanmadı/onaylanmadı. Öncelikli manuel deneme:234,239,244,250;230 için HP/karşı baskı ayrıca değerlendirilmeli. Rapor `Batches/235-250-Late-Game/Balance-Review.md`. Hata kayıtları kayıplara katılmaz; toplam11 runtime hata örneği saklıdır. CPU/GC/FPS ölçümü yok.

**Tek kalan istek: üç başlangıç booster’ı karşılaştırması.** LineH/PulseCore/SystemOverride için `RuntimeSimulationSession`/CLI `--boosters 3` desteği hazır. Gerçek dedicated injector kullanılır, envanter harcanmaz,3 teslimat raporlanmadan bot başlamaz. Ufo sim guard kaydedilmiş streak eklerini engeller; idle event aboneliği normal/iptal/disable/destroy yollarında temizlenir.21 Python test +56 izole sim C# +8 injector cleanup kontrolü PASS. Yeni kaynak kod mevcut Mac binary’sinde yok. **AGENTS.md açık build isteği olmadan build’i yasakladığı için async izin soruldu, henüz cevap yok. Build ve üç booster’lı koşu YAPILMADI.** İzin gelirse kullanıcıdan tekrar sim izni istemeden Macsim’i derle veya kullanıcının yeni derlemesiyle devam et; önce1 oyun booster smoke (raporda tam3), sonra225–234 son hamlelerle koş. UI kontrolü/yayın/release baseline/commit yok. Bütün sim süreçleri kapandı.

**176 kullanıcı tarafından bitirildi; iş kapalı, dokunma.**251 istenmedi.

## Güncel — 234 v2 katmanlı sürüm hazır; 176 kapandı · 2026-11-07

Kullanıcı234v1gridini beğendi,zorluğunu hafif buldu,oynadıktan sonra kolay olduğunu doğruladı. Yalnız234revize edildi. Beğenilen8×11/74aktifmaske,24hamle,isim/GUID/meta/katalog aynı. 176kullanıcının bitirdiği seviyedir veagent işi kapanmıştır;oraya geri dönme.

234 **Boya Dalgası / Paint Wave** / `LevelV1_00002340.asset` / `LevelCL_0234` / chapter10. Solda2ColorChest→2PaintCanBox;alt sol12ve sağ omuz4hücrede **oyma28→iki-aşamalı plastik40→çamur25**,toplam16üç-katman yuvası.5goal2renkli sandık/2boya/16oyma/16plastik/16çamur. Orta plastik yerinden düşünce deçamur ortaya çıkabilir;ikisi ayrı zorunlu hedef olarak sürer.50normal başlangıç yuvasının49’u otomatik beslenir;yalnız(0,4)index32deGearbaşlangıç taşı var,desteği kontrol edildi. Boya çatısı kalkınca bu hücre de doğal doluma açılır. Hazırspecial/çim/ColorCrate yok.

Aktifrevizyon `Batches/234-Paint-Wave/active-revision.json`;yazıcı `revise_v2.py`,verifier `review_v2.py` (`review.py`yönlendirir),render `render.py`. V1kullanıcı tarafından kolay bulundu ve`Revisions/v2-layered/Before/` içine alındı;eskiauthor artıkaktifrevizyonda durur. Not/PNG `Level234-Boya-Dalgasi.*`;`Layers.png`3gerçek spritekesiti incelendi. Son envanter `Reviews/234-Paint-Wave-v2/`.6109diğerAssets dosyası aynı;v2henüzoynanmadı/onaylanmadı.24hamle yeni katmanlarla manueltest bekler;denge/FPS ölçümü,UnityUI/Play/build/bot-kaskat simülasyonu/yayın/commit yok.235istenmedi.

Kullanıcının **öncefikir/tarz seçip paylaşma→yerleşim kurma**, **yokolan engellerle güzel kenar şekilleri**, **yalnız doğal açılış dolumu ulaşmayan yuvalara başlangıç taşı** tercihleri hafızaya,ilkelere,feedback’e veskill’e işlendi.176’nın eskiagenthamle/hedeflerini mevcutkullanıcıassetine geri basma.

Ayrıgörseltalep tamamlandı: gönderilen yeniısırılmış elma,`Resources/FlyFruit/AppleHalf.png`yerine bytesolarak kondu. SpriteGUID`12e9756b758758baa4722910f6feb45e`aynı;FlyFruit102aux[0] ve3.lokma eşiği doğru. Kayıt `Docs/Checks/FlyFruit-Bitten-Apple-2026-11-07/`;234v2snapshot’ında yeni elma korunur.

## Güncel — yalnız V1 176 yeniden tasarlandı · 2026-11-06

Kullanıcı `LevelV1_00001760` yerleşimini açıkça reddetti; sebep normal taşların ortasında kopuk kutu yerleşiminin mevcut kompozisyon kuralına aykırı olması. Aktif katalog176/`LevelCL_0176`/Kızıl ve Yeşil çözüldü. TarihselDolaplı Geçit notu/PNG’si kaynak değildir ve korunur.

Yeni176:8×8/54aktif,üstte geniş ağız altta daralan çanak;5kırmızı72+5yeşil74 birbiriyle bitişikV altkonturu.26hamle,Normal türü,iki hedef ve3hit/kutu aynı. Bu aktifV1ilk kırmızı/yeşil sandık tanıtımı olduğu için6sonlu rahatlama rolü korunur; aileler178’in dört renkli çerçevesine dönüştürülmedi. Çim/yeni aile/hazırspecial/pin yok.44/44→49/49→54/54normal yuva statik olarak beslenir;başlangıçboşluğu yok.

Kayıt `Batches/176-Contour-Revision/`;öncekiasset,hashes,revise.py,validation ve önce/sonra/2-176-178PNG. Aktifnot/PNG `Level176-Kizil-ve-Yesil.*`,175önceki katalog şekil incelemesi `Reviews/176-Contour-Revision/`.2ile alt çanak benzerliği kaydedildi; yeni görsel agentdeğerlendirmesidir,kullanıcı onayı değildir.

Yalnız176boyut/hücre dizileri değişti; katalog,TR/EN isim,meta/GUID,müzik ve6107diğerAssets dosyası aynı.232/233,108kullanıcı değişikliği ve son runtime/audio işleri korundu. Eski233baseline’ının176’yı drift sayması bu yeni yetkili revizyon nedeniyledir; eskiauthor’la düzeltme yapma. StatikYAML/dizi/goal/refill/koruma ve gerçekPNG kontrolü geçti. Oynanmışdenge/FPS,UnityUI/Play/build/bot-kaskat simülasyonu/yayın/commit yok. Manueltest kullanıcıda. Başka level için yeni yetki yok.

## Güncel — 233 Kutup Kilidi hazır; çim ailesi yok · 2026-11-06

Son yetki yalnız233ve “çim istemiyorum ama artık”.233’te42/58/69yok;eski levellar korunur. Yeni tasarımda çimi varsayılan örtüye çevirme. Katalog V1: `LevelV1_00002330.asset` / `LevelCL_0233` / chapter10, TR Kutup Kilidi / EN Polar Lock.

8×11/82aktif,27hamle Normal,4renk;üst çalışma avlusu,altta bitişik iki2×2kasa ve U biçimli mıknatıs çıkışı. Kasalar ters renk sıralarında2+1+1hit ister;her biri dört gerçekCargo gizler. Hedef8Cargo/2Safe/1Magnet/8SculptingSpecial/12HelmetPorcelain. Mıknatıs12yolhücresi/11uç ilerlemesi;pusulalar yalnızspecial ile kırılır. Kasayı açmak kargoyu teslim etmez;dört düz çıkış sütununun altı açılmalıdır. RefillSpecialGuard=-1(auto);template232’nin0.75manualayarı kopyalanmadı.

42/42başlangıç normalyuvası doğal dolum,pin/hazırspecial/başlangıçboşcep yok;katman/teslimat sonrası50/62/74/82yuvada besleme var. Kasalar kapalıyken pusula tabanı önce temizlenirse8alt yuva geçici gölgede kalır;kasa açılınca tümü erişilir. Bu sınırlama not/validator’da açık.

**232güncel32hamle/5+5+5elmas=15olarak korundu**;eski28/18taslağı geri uygulanmaz.1–232levelları,önceki runtime/ses değişiklikleri korunur.108çalışma sırasında dışarıdan değişti;mevcut hali `External/` ve `external-edits.json` ile korundu;baseline sessizce değiştirilmedi.6103diğerAssets hash aynı;yalnız katalog/isimlere233eklendi ve yeni233asset/meta yazıldı.

Kayıt `Batches/233-Polar-Lock/` (author/review/render,validation,hashes).Not/PNG `Level233-Kutup-Kilidi.*`;232önceki katalog envanteri `Reviews/233-Polar-Lock/`.142alt kütle/iki kasa,198mıknatıs benzeri olarak gerçek sprite karşılaştırmasında incelendi. Statik veriler ve gerçekPNG agentgörsel incelemesi geçti;233henüz kullanıcı onaylı/oynanmış değil.27hamle ilkayar,ölçülmüşdenge/FPS yok. UnityUI/Play/build/bot-kaskat simülasyonu/yayın/baseline/commit yok;manuel test kullanıcıda.234istenmedi.

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


## Güncel — yeni sinek/meyve ailesi, level değişmedi · 2026-11-06

Son görev kullanıcı görsellerinden FlyEggBox100/FlyEgg101/FlyFruit102mekaniğidir. Kutu yalnız special ile4yumurta salar;yumurta1×1movableve1hit;3sinek yarım elma,iki sürü tam elma. Meyve Width/Height origin bazında dinamiktir;Goals/PreLevel yalnız meyve adedidir. İlk özel kayıt `Docs/Checks/FlyFruit-2026-11-06/README.md`. Yeni aile mevcut levellara otomatik konmadı.229v4/diğer levellar ve kullanıcı225/226assetleri aynı;231+istenmedi.

Statik veri,izole gerçek yöntem kontrolleri ve gerçek sprite parça incelemesi yapıldı;Unitybuild/Play/cihaz/denge/FPS ölçümü/yayın/baseline/commit yok.229hamlesi28ve kullanıcı oynanmış onayı yok. Önceki level snapshot’larının sonradan yetkili runtime/library değişikliklerini drift görmesi normaldir;eski yazarları çalıştırıp hash benimsetme.


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

## Güncel — her Dimerdal'ın ayrı üretim adedi / otomatik Goals · 2026-11-05

Son açık kullanıcı isteği: kutu yerleştirirken kendi sayısını belirlemek; Goals bunun toplamı olsun (8+8 veya6+10). `LevelData.dimerdals` origin+tür/count; editörde yeni kutu “Üreteceği elmas”, var olan kutuların koordinatlı ayrı adetleri ve grid rozeti. Goals Diamond miktarı ve mor ikonu otomatik yönetilir. Runtime'da kutular bağımsız limit/rezervasyon/uçuş taşır; yalnız kendi son yerleşimi tamamlanınca kırılır. İptal/timeout hakkı ilgili kutuya döner, başka kutuyu tüketmez. Toplama hedef sayımı aynı. Eski ortak üretim kotası açıklamaları geçersizdir.

Mevcut dört assete yalnız yeni kayıt listesi eklendi:221[4,4] hedef8;222[10] hedef10;224[6,6] hedef12;225[8,8] hedef16. Diziler,hamleler,hedefler,ikon,katalog,ad/meta aynı.223 ve diğer3032 asset/kod dosyası korundu. 225v2yerleşimi ve222–224görsel onayları sürer. Güncel kayıt `Docs/Checks/Dimerdal/Per-Box-Production/`; batch active-revision v4 oraya yönelir. 289 per-box +409 mevcut +39 matching,ortak toplama ve64/7 feedback/katman/kırılma kontrolleri; statik YAML/koruma var. Unity GUI/Play/build/cihaz/CPU/GC/FPS veya denge ölçümü,yayın/commit/baseline yok.226+istenmedi.

## Güncel ikon — bütün elmasların ortak mor hedefi · 2026-11-05

Kullanıcının `modimerdal.png` görseli Goals/HUD, PreLevel ve sonuç hedefleri için ortak elmas ikonu oldu. Aktif katalogdan çözülen 221/222/224/225'te yalnız Diamond `iconOverride` satırları ve kütüphanedeki 16 Dimerdal `goalIconSprite` güncellendi. Diğer hedefler, dört tahta/kutu rengi, hamleler ve yerleşimler korunur. Mor ikon toplam elması temsil eder. Yeni sprite aynı kaynak PNG bytes'ı ve sıkı importer rect ile eklendi. Mevcut batch modelleri, hash ve gerçek sprite önizlemeleri güncellendi. Aktif revizyon `v3-shared-goal-icon`; bağımsız statik doğrulama 3035 dosyayı korur. Unity build/Play/cihaz testi veya yayın yok; oynanmış denge/onay çıkarılmaz.

## Güncel mekanik — Dimerdal üretim sonrası parçalanma · 2026-11-05

Kullanıcı önceki gri kapanışı KeyGenerator'a benzetti; üretim bitince kutuların kırılıp yok olması uygulandı. Son başarılı yerleşimden sonra ortak kotadaki görünür kutular mevcut havuzlu büyük-parça efektiyle kalkar. Bütün footprint hücreleri serbest kalır, alt katman geri açılır, mevcut resolve yeniden istenir; düşüş motoru değiştirilmedi. Örtülü kaynaklar açılınca kaldırılır. Uçuş/rezervasyon/iptal erken kırılma yaratmaz. Gri shader/material kaldırıldı; önceki gri kapanış kayıtları tarihseldir. 1–225 level assetleri/katalog/adlar bu tur aynı; 222–224 onayı ve 225 v2 korunur. İzole gerçek C# kontrolleri ve sınırlar `Docs/Checks/Dimerdal/Exhaustion-Break/README.md` içinde. Cihazda oynanmış denge veya animasyon onayı yok; build/Play/yayın yok.

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

<!-- current: boss-goal-icons-2026-11-04 -->

- **Boss ikonları — 2026-11-04:** Kullanıcı goals ve PreLevel/editörde boss ikonunun görünmediğini bildirdi. Aktif V1’de22BossDamage hedefi tarandı; yalnız210/220ikonları fileID0 idi. Önceki20bossun BadgerIdle sprite’ı (bd8a2a2aabdf46fcabea5eb683bc76d9) bu iki hedefe atandı. HUD/PreLevel override önceliği ve editör Sprite alanı kontrol edildi; runtime UI değişmedi. İki authoring yazıcısı ve statik PNG renderer explicit boss ikonunu korur. V4 `Batches/213-220-Showcase/Revisions/v4-boss-goal-icons/` aktif; iki assette yalnız ikon değiştiği ve diğer443seviye/meta/katalog/isim dosyasının aynı kaldığı doğrulandı. Hamle/HP/hedef miktarı/tahta aynı. Unity build/Play/cihaz/yayın yok.

<!-- current: layered-contents-moves-2026-11-04 -->

- **2026-11-04 · ortak örtü/içerik düzeltmesi ve hamle v3:** Kullanıcı çim altındaki GelLauncher’ı boş gördü; aynı yolu tüm kodla kurulan içeriklerde kontrol etmemizi istedi. GridSpawner ilk çizim+promotion ortak katman HP sorgusunu kullanıyor (Gel4yön, Paint, Laser, Rhodes); Tarp.Register de saved katmanı okur. İçerik çocukları yeniden üretilmez; gerçek partial state dolmaz. BatteryBox cover-cleanup kaybı da düzeltildi; ColorChest/Battery/Wardrobe typed kayıtları sadece kendi yok edilmesinde temizlenir, board yeniden kurulumunda tüm typed haritalar bırakılır.103içerik/press/wardrobe,30registry,26paint,437promotion,384chest,6Rhodes kontrolleri geçti; eski Gel/Battery hatası fixture ile üretildi. **V2 oynandı:**213–218ilk can,219ve220birer kayıp. **Yeni hamleler213–220:21/21/22/20/21/22/24/28.** Yalnızmoves satırları; maske/hedef/engel/layer/pin/isim/katalog/GUID aynı. V3 `Batches/213-220-Showcase/Revisions/v3-moves-2026-11-04/` aktif. Ayrı MasterLeague/menü/backend/test ayarı eşzamanlı düzenlemeler kayda alındı, görev onları yazmadı. Yenihamleler cihazda oynanmadı; Unitybuild/Play/bot/kaskat/gravity/yayın yok.221istenmedi.

<!-- current: 213-220-v2-lamps-launchers -->

- **213–220 v2 / ColorChest görünümü — 2026-11-03:** Kullanıcı219’da çim altındaki sandığın ampullerinin görünmediğini bildirdi.Grass destruction artık yalnız gerçekColorChest yıkımında `_chestViews` kaydını kaldırır; mevcut prefab korunur. Eski hata üretildi,384/48sıra test ve413/26/6 regresyon kontrolleri geçti. Kullanıcı isteğiyle **LampStrip217+218, GelLauncher213+215**:217iki kayanL,218ikiC(sol sabit/sağ kayan), kanal aradan;213sepet yanında ikiUp kartuş+Gel61,215dipte dört yatay kartuş/çim üstü dört sandık+Gel99. Seçilen4levelden renkli plastikler kalktı. Hamle213=24(22→24),215=26,217=24,218=25; isim/GUID/katalog aynı.214/216/219/220assetleri ve1–212, diğer kodlar2989hash ile korundu. Batch `Revisions/v2-lamps-launchers/` aktif author/audit/önceki snapshot; dörtPNG/toplu önizleme güncel. Statik ve görsel kontrol var; Unity build/Play/bot/kaskat/gravity/cihaz dengesi/yayın yok. **Oynanıp onaylanmadı;221 istenmedi.**

<!-- current: 213-220-showcase -->

- **213–220 tamamlandı — 2026-11-03, kullanıcı denemesi bekliyor:** Açık sekiz seviye isteğiyle213Işıklı Balon22 ·214Guguk Çanı24 ·215Vitray Atölyesi26Hard ·216Sulu Lale22 ·217Kanatlı Kum Saati24 ·218Kıvrılan Nehir25 ·219Çiçekli Taç25SuperHard ·220Büyük Festival29Boss(9×10,620HP/3dalga) eklendi. Fener/kuş evi/kanal/değirmen set boyunca dağıtıldı; porselen/plastik/ColorChest/çiçekli çim grupları tamamlıyor. Sarı/yeşil plastik mevcut renkli plastik ailesinin varyantlarıdır.1–212(kullanıcının211=24hamlesi ve212 onaylı maskesi dahil), mevcut runtime ve2976 korunan dosya aynı. Batch `Batches/213-220-Showcase/` snapshot/yazıcı/statik audit/katman PNG/toplu önizleme/karşılaştırmalar; sekiz asset/meta, katalog ve TR/EN adlar hazır. Statik YAML/dizi/origin/hedef/başlangıç dolumu/kanal/boss/gruplama kontrolleri ve gerçek PNG incelemesi var; hareketli engellerin düşüşü simüle edilmedi. Oynanmış denge/onay, Unity build/Play, bot/kaskat, yayın veya app-release baseline yok. **221 istenmeden eklenmez.**

- **212 v2 — kullanıcı siluet onayı ve katman düzeltmesi, 2026-11-03:** “yelken şekli çok güzel”; maskeyi aynen koru. Kullanıcı PaintCanBox'ları en alta, Grass'ı onların üstüne istedi, Safe kalabilir. Uygulandı: Paint base origin74/76 (üstten9–10. satırlar); bu8 hücrenin hepsinde üstte Grass42. İki Tube üst band54/62'ye taşındı. 59 aktif hücre, 24 hamle/4 renk, Safe3/3/3 ve4Cargo, beş hedef (1/4/2/2/14), adlar/GUID/katalog aynı. Açılış41 taş yuvası:35 doğal+6 gerçek pin. Kasa ve yelken korunmuş, diğer assetler hash ile aynı. Önceki v1 `Batches/212-Painted-Sail/Revisions/v2-grass-over-bottom/`; aktif `revise_bottom.py`, `verify.py`→`verify_bottom.py`, not/PNG `Level212-Renkli-Yelken.*`. **Siluet onaylı; v2 oynanmadı/denge ölçülmedi.** Build/Play/bot/kaskat/yayın/baseline yok. 211 kullanıcı24hamle olarak korundu; 213 istenmedi.

- **212 Renkli Yelken / Painted Sail — 2026-11-03, inceleme taslağı:** kullanıcı yalnız 212'yi yetkilendirdi. Güncel geniş havuzdan Safe/Cargo/PaintCanBox/Tube/Grass seçildi. Yelkenli silueti 9×11, 59 aktif hücre, 24 hamle/4 renk; kasa 3/3/3 altında dört kargo, iki boya kasası altında sekiz çim, gövdede içe bakan iki üç-hücrelik boru; alt gövdede dokuz gerçek renk pini. Hedefler 1 Safe / 4 Cargo / 2 PaintCanBox / 2 Tube / 14 Grass. V1 `LevelCL_0212`, asset `LevelV1_00002120.asset`; TR/EN ad ve PNG/not kayıtlı. `Batches/212-Painted-Sail/` snapshot/author/render/verify/validation/Pool-Review, `Reviews/212-Painted-Sail/` tekrar envanteri. Statik veri/katman/hedef/düz kargo çıkışı/başlangıç dolumu kontrolü ve gerçek sprite PNG incelemesi var; oynanmış denge/onay yok. Önceki dosyalar hash ile korundu. **211 kullanıcı değişikliğiyle 24 hamle; kullanıcı “ben değiştirdim” dedi, mevcut 24 korunur.** Unity Play/build, bot/kaskat simülasyonu veya yayın/baseline yok. **213'e istenmeden geçme.**

- **Son kullanıcı yönlendirmesi — 2026-11-03:** 211'de Wall/MetalWall örneklerine bağlanarak ana obje seçmemiz eleştirildi; geniş mevcut mekanik havuzundan farklı fikirler geliştirmeliyiz. Memory/Principles/Feedback güncellendi. 211 hâlen 20 hamle; bu tur asset değiştirilmedi. Yeni tasarım/211'i yeniden yazma talebi yok; 212'ye kendiliğinden geçme.

- **211 hamle düzeltmesi — 2026-11-03:** kullanıcı 28 hamleyle kolay geçti, 10 hamle arttı. Açık isteğiyle yalnız `moves` **28→20**. Tahta/hedef/katman/renk/refill/isim ve katalog aynı; güncel önceki asset/PNG/not `Batches/211-Steel-Crest/Revisions/v3-moves20/` içine alındı. Bu kayıt aşağıdaki ilk taslağın 28 hamle/oynanmadı bilgisinin önündedir. 20 hamle oynanmadı; kullanıcıdan yeni deneme beklenir. 212 istenmedi.

- **211 Çelik Arma / Steel Crest — 2026-11-03, inceleme taslağı:** kullanıcı 211–220'de yeni mekanik yerine eski engeller/katmanlar ve öncelikle güzel görüntü istedi. Yalnız 211 eklendi: 9×11, 75 aktif, 28 başlangıç taşı, 28 hamle, 4 renk; arma silueti içinde asimetrik kuşak. Hedef 2 Wall / 2 MetalWall / 17 oyma taşı / 1 dolap / 4 çamur. 2×2 dolap çamuru örter; kiremit ve metal ikişer ayrı parça. V1 katalog `LevelCL_0211`, asset `LevelV1_00002110.asset`, TR/EN isimler kayıtlı. Önceki 210 level ve runtime korundu. Not/PNG `Level211-Celik-Arma.*`, snapshot/author/revise/render/verify `Batches/211-Steel-Crest/`, tekrar inceleme `Reviews/211-Steel-Crest/`. Statik veri/katman/dolum kontrolü ve gerçek sprite PNG incelemesi yapıldı; oynanmış denge/onay yok. Unity Play/build, bot/kaskat simülasyonu ve yayın/baseline yok. **211–220 yeni engel tanıtımı kuralının açık istisnası; 212'ye talep olmadan geçme.**

> Önce [kalıcı level tasarım hafızasını](Memory.md) oku. **131–205 genel revizyonu uygulandı**;
> aşağıdaki önceki 202–205 kaydı tarihsel kalmıştır. Güncel snapshot/sonuçlar
> `Batches/131-205-Review-2026-10-31/` altında; Memory.md devam noktasını ve son kullanıcı kurallarını içerir.

- **191 Işıklı Kapı v2 (2026-11-01, kullanıcı düzeni):** lamba | taş | lamba | 3 taş | lamba | taş | lamba; 9×10, dört
  dikey şerit (satır 4–10, 7'şer lamba, tepede mor), orta 3×6 çamur; 24 hamle; hedef LampStrip 4 · Mud 18. v1 (iki Γ kemer,
  kullanıcının önceden onayladığı) `Batches/191-LampStrip/Revisions/v1-gamma/`. Not/PNG `Level191-Isikli-Kapi.*`,
  yazıcı `Batches/191-LampStrip/design.py`. Statik kontrol geçti; **oynanmadı**. Kullanıcı: "çok sevdim".
- **196 Işık Çarkı (2026-11-01):** KAYAN Lamba Şeridi tanıtımı; 9×10 kubbe omuzlu, alt ortada 5×5 kapalı lamba halkası
  (16 lamba, saat yönü, içi 3×3 hole), alt köşelerde 2×3 çim ×2; 22 hamle; hedef LampStrip 1 · Grass 12. Ad Basamaklı
  Arşiv → Işık Çarkı / Wheel of Lights. Not/PNG `Level196-Isik-Carki.*`, yazıcı `Batches/196-LampStrip-Shift/design.py`,
  eski asset yedeği aynı klasörde. Kullanıcı: "süper oldu"; kayma animasyonu sön→kay→yan dalgası. Sıradaki: 197–198 rahat→artan, 199 Super Hard.
- **197 Işık Merdiveni rahatlatıldı (2026-11-01, kullanıcı önizlemeyi onayladı):** sağ üst fener/plastik bloğu taş alanı,
  anahtar 30→16; hedef Lantern 12 · Plastic 15 · Stone 9 · Key 16; 32 hamle aynı. `Level197-Isik-Merdiveni.*`,
  `Batches/197-Ease/`. 198'e dokunulmadı. **Sıradaki: 199 Super Hard zorlaştırma** (önce tasarım, hamle en son).
- **199 Deniz Kabuğu zorlaştırma (2026-11-01):** yerleşim aynı; jel kaynağı mührün altında → mühür sayaçları
  8/8/8 → 11 → **13/13/13** (kullanıcı 11 ile oynadı: 2 fail, biri tek hamleyle; 3.'de geçti, "biraz rahat" → 13 önerdi),
  hamle 26 → 24. `Batches/199-Harden/`. 13 hali oynanmadı.
- **Kullanıcı oyun geri bildirimi (2026-11-01):** 197 ve 198 "çok iyi olmuş" (197 rahatlatılmış hali).
- **207–210 Değirmen onluğu v2 siluetler (2026-11-01), oynanmadı:** v1 kullanıcı: "çok sıradan ... hepsi aynı" (yedek
  `Revisions/v1-siradan/`). v2: 207 Dev Pervane (tahta = yel değirmeni) · 208 Baykuşlu Ağaç · 209 Dört Kanat SH (delikle 4
  oda) · 210 Un Çuvalı Boss. Kurallar: fener SÜTUN, kuş evi kenarda L/ters L. Un artık sabit 5×5'e döker, denk gelmeyen un
  hedeften düşer. Ayrıntı `Batches/206-210-Windmill/README-207-210.md`.
- **206 Değirmen Tepesi — KULLANICI TASARIMI (2026-11-01, onaylı: "çok mantıklı ve güzel bir level oldu"):** 8×10,
  4 değirmen iki yanda simetrik (satır 4–5 ve 9–10; yatay roket o satırlarda 4 kanat kırar), S su kanalı aşağı biter;
  hedef Un 100 · Su Kanalı 1; 25 hamle. Kullanıcı rahat geçti. (İlk Claude taslağı: tek değirmen, Un 25.)
  Eski satır: 8×10, tek değirmen, hedef yalnız Un 25
  (kullanıcı: "hedefim UN olsun"), 25 hamle; katalog girişi + LevelNames eklendi. Mekanik kodu yeni, Unity'de oynanmadı.
  207–210 değirmen oynanınca tasarlanacak. `Batches/206-210-Windmill/`, `Docs/Obstacles/Windmill/Plan.md`.
- **202–204 sıkılaştırma (2026-11-01, kullanıcı: "202-204 çok kolay, en azından 1-2 hamle kala bitirsem; 205 oldu"):**
  kuş hedefi 18→24 / 12→18 / 16→22, hamle 24→21 / 24→21 / 25→22. Tahta aynı. v2 yedekleri `Batches/201-205-Lamps/Revisions/`.
- **205 v3 fener sütunları UYGULANDI (2026-11-01, kullanıcı isteği):** kenar sütunları fener (ekranda 22, hedef 30),
  sütun bazlı döküm yalnız 0/8, fallback 0. Ayrıntı README v3. Oynanmadı.
- **202–205 v2 UYGULANDI (2026-11-01):** kullanıcı v1'i "201 hariç çok kolay" buldu → çim/çiçekli çim örtüleri, morlar,
  hamle −1/−2; 204 kanalı aşağı dönerek biter. Ayrıntı `Batches/201-205-Lamps/README.md` (v2). Oynanmadı.
- **201–205 Lamba UYGULANDI (2026-11-01, kullanıcı: "diğerleri ok uygulayabilirsin"; 205'te tek başına roket sepeti
  kullanıcı isteğiyle kaldırıldı):** kademeli lamba kullanımı; statik kontrol geçti (dizi boyu, origin, dallanma yok,
  204 kanal yolu halkayla çakışmıyor). 205 adı "Mühürlü Kuş Parkı" artık mühür içermiyor → yeni ad kullanıcıya soruldu.
  Oynanmadı; ayrıntı `Batches/201-205-Lamps/README.md`,
  önizleme `~/Desktop/Wondi Paylaşım/Lamba_201-205/`. Ayrıca kullanıcı 157 önerisini Unity'de kendisi
  uyguladı (pusula 6 sıra) ve 134 hamlesini 26 yaptı (ikisi de commit dışı, Unity yeniden serileştirdi).
- **Gece incelemesi (2026-11-01, kullanıcı uyurken; yarın Android kapalı test sürümü):** asset'e yazılan öneri YOK.
  Sorular `~/Desktop/Wondi Paylaşım/Gece_Inceleme/Sorular.md`: 134 hamle 24→27 · 143 üst plastik sırası taş alanı (36→28)
  · 157 pusula sütunu 8→6 sıra (16→12). Tek komut: `Batches/Night-2026-11-01/propose.py apply <lv>`. Tanıtımlar
  (141/151/166/176) kullanıcı "rahat geçtik" dediği için dokunulmadı; 173/174 farkı küçük.
  Cevaplar: **134** kullanıcı kendisi hamle 24 → 26 yaptı. **143** aynen kalır — koridordaki TNT+roket açılış combosu
  ilk hamlede geniş alan açıyor (bot bunu iyi kullanmıyor); kapalı testte fails_before ile izlenecek. **157** cevap bekliyor.
- **Boss fırlatmaları düzeltildi + 130 hedefleri (2026-11-01, kullanıcı kuralı → Memory kural 5):** 140–200'de
  fırlatma ve alet tehdidi listeleri revizyon öncesi çeşitli hallerine döndü; hedefte olan türler çıkarıldı
  (160: Mud atılmaz; 180: gri alet 47 hedefte olduğu için yalnız renkli aletler). HP/dalga/saldırı aynı.
  130 Çiçekli Kemer: hedef Boss 400 · GrassFlower 22 · Grass 22 · Wall 3 (kalkan/roket sepeti yardımcı, hedef değil);
  fırlatmalar [Oil, PlasticTwoStage, Helmet, Plastic_Red] aynı (hedefle çakışma yok). `Batches/BossThrows-2026-11-01/`.
- **Kaynak+ürün hedef çiftleri temizlendi (2026-11-01, kullanıcı: "doğru bunları düzeltmeyi unutmuşum"):** kullanıcının elle
  düzelttiği kalıba göre jel atıcılı seviyede hedef yalnız jel, su depolu seviyede yalnız su deposu. 138: WaterPuddle
  hedefi silindi (WaterTank 4 · MetalWall 4). 144 ve 148: GelLauncherUp hedefi silindi. Tarama: başka çift yok.
  Yedekler `Batches/SourceProductGoals-2026-11-01/`. Yardımcı kuralı: kalkan asla, roket sepeti varsayılan olarak hedef değil
  (205'te RocketBasket 1 hedefi duruyor — kullanıcı "bazen koyuyorum").
- **189 Dolaplı Geçit zorlaştırma (2026-11-01, onay bekliyor):** yan plastik şeritler içe bir sütun kalınlaştı (sütun 3 ve 7,
  satır 5–9) → orta alan 5 → 3 sütun; plastik 10 → 20; hamle 30, guard otomatik aynı. `Batches/189-Check/`. Oynanmadı.
- **135 Kristal Kalp zorlaştırma (2026-11-01):** kalp aynı; alt uçtaki 4 AÇIK jel tohumu da kum taşı
  (28) altına alındı → jel ilk taş kırılınca başlar; taş hedefi 14 → 18. Kullanıcı 3 oyun: 1 fail, 2 galibiyette ≥12
  hamle arttı ("zor değil") → **hamle 27 → 22**. `Batches/135-Harden/`.
  Sonra kullanıcı kendisi üstte (4,2)/(6,2)'ye 2 taş ekledi (taş 20), hamle 21 — "yine sürekli geçiyorum".
  Kök: otomatik refill special koruması 135 için yalnız **0.47** (101+ kuralı 0.3–0.7, seviye no karması) → refill çok
  bedava special üretiyor; jel special ile satır/sütun boyunca yayılıyor. Ayrıca kayıptan sonra koruma ×0.5 (gizli yardım).
  → **refillSpecialGuard 0.9** (asset'e elle; design.py artık kaynak değil, kullanıcı düzenlemesi asset'te). Oynanmadı.
- **SIRADAKİ İŞ (kullanıcı kararı, 2026-10-31 akşam): 191–200 yeniden yorum.** 191–200 onluğunda hiç yeni
  engel tanıtımı yoktu (kural: onlukta 2–3). Karar: **191 = Lamba Şeridi (LampStrip, id 79) KAYMAYAN tanıtım**,
  **196 = KAYAN Lamba Şeridi tanıtımı** (lampStripSettings.shift); ikisi de rahat. 197–198 rahatlatılıp 199'a
  doğru artan, **199 Super Hard zorlaştırılır** (Usta sim %67). 201 Kuş Evi aynen. Onluk dışı: **135 Hard
  zorlaştır** (sim %73), **130 Boss çok hedefli yap** (tek BossDamage, sim %100), 189 sınırda. Onluk eğrisi ve
  öncelik Design-Principles'ta; sim tablosu `Reviews/Sim-130-205-Usta/Review.md`. LampStrip henüz Unity'de
  oynanmadı — 191 aynı zamanda engel testi. Kullanıcı hedefleri elle düzeltti (kaynak+ürün çifti yok kuralı).

- **Güncel tamamlanan iş: ProLevels V1 131–205 genel revizyonu · 2026-10-31.**
  Kullanıcı150sonrasını özellikle kolay/heyecansız ve alanları gereğinden geniş buldu;
  5/9sonlu seviyeleri referans gösterdi. Yardımcılar ve boss engelleri dahil hedefte
  temsil,en fazla5goal istedi.75level incelendi,**51asset revize/24aynen korundu**;
  **17maskede/boyutta değişiklik**.5/9sonlu15levelin maskeleri/hamleleri aynı;
  179'da hamster ailesi kaldırıldı,185'te renk ailesi sadeleşti (goal limiti istisnaları).
  **Yedi bossun tamamı5goal**,bossHP/dalgalar/saldırı ayarları aynı;gri geri sayımlı
  alet mekanigi korundu,başlangıçta1adet ve tek türde atış var. Diğer baskı türleri
  hedeflere bağlı.143iki-aşamalı hedef4→36,151mühür sonrası altın hedefli,1767×7,
  178/188üstte3boşsıra,184/196üstte2boşsıra kaldırıldı.202–204'e ek iş;
  **201'in onaylı iki2×3grubu/50kuşu aynı**.1–130,katalog,adlar,GUID/metalar korundu.
  `Batches/131-205-Review-2026-10-31/Review.md`75kararı;Before/After/manifest/
  validation-after kayıtları.75YAML/mask/origin/footprint/katman/path/goal≤5,
  boss≤10satır/atışkapsamı ve görsel kontrol geçti.2.337kapsam dışı baseline
  dosyasından2.336aynı;**ayrı kullanıcı talebiyle Rodos olay-sırası düzeltmesi
  yalnızGridSpawner.cs'te** ve hash'i ayrıca kayıtlı. Önceparçala/sonrakaynağısil
  devri 6olay-sırası ve1.992scatter kontrolüyle geçti. Telefon görseli doğrulanmadı.
  **Tasarımlar oynanıp onaylanmadı;denge/FPS ölçülmedi. Unity Play/build,bot/kaskat
  simülasyonu,remote yayın/release baseline yok.206'ya geçme.**


- **Önceki iş: ProLevels V1 202–205 v2 · 2026-10-31 — v1 reddedildi.**
  Kullanıcı50 hedefinin yalnız201 için olduğunu düzeltti;202–205'i tekdüze buldu
  ve BirdHouse'un güncel davranışını okuyup daha keyifli tasarlamamızı istedi.
  Güncel servis yeniden incelendi:ilk hit kapağı açar,açıkken her isabet kuş verir
  ve yeni kuş çıkarır;bir vuruşsuz oyuncu hamlesi evi kapatır. Normal/scripted
  kaynak ev başına1kuş/hamle,special kaynak bu sınıra takılmaz. Runtime değişmedi.
  **201'in onaylı yerleşimi ve50hedefi aynı.** Yalnız202–205 içerikleri revize:
  **202:**9×9 halka avlu,63aktif/18hole,26hamle;3alt sıra ev,18kuş +2boya kasası.
  Ortada3×2 boşluk;iki alt omuzdaki kasayı hangi yandan boşaltacağını seç.
  **203:**9×10 çatallı kargo alanı,62aktif/28hole,27hamle;3orta sütun ev,
  12kuş +4kargo +1duvar parçası. Tek3×2duvar bendi;aynı hücre beşincihit'e
  gelince komple çöker. Alt yan cepler2hücre genişliğinde;2×2match kurulabilir.
  **204:**9×11 S kıyı,74aktif/25hole,28hamle;4sol kenar ev,16kuş +1kanal +6fener.
  Yol17hücre,sağ kenardan çıkar;fenerler altta3×2gruptur.
  **205:**9×11 iki girişli park,77aktif/22hole,27hamle Zor;6alt2×3ev,24kuş
  +2mühür +8pusula. Üst2×2mühürler kırmızı/yeşil başlar,renk başına5/6ister;
  altlarında pusula vardır. Ortada1roket sepeti yardımcı;hazır combo/pin yok.
  İlk taş yuvası52/49/63/60;boş yuva0/0/0/2.205'in yalnız iki üst köşe cebi,
  mühür ve alt pusula açılana kadar boş. Kargolar bende,fenerler dibe dayanır.
  Statik YAML/mask/origin/Wall parça/goal/katman/refill/yük desteği geçti;
  tam boy dörtPNG ve topluPNG incelendi. **3.192 mevcut dosya** (201,katalog,
  TR/EN isimler,tüm GUID/meta/runtime dahil)hash'e göre aynı;yalnız4asset değişti.
  V1 ret arşivi `Batches/202-205/Revisions/v1-rejected/`;v2çalışma/not/PNG/audit
  `Batches/202-205/`,yeni tekrar inceleme `Reviews/202-205/v2/`.
  **V2 henüz oynanıp onaylanmadı;denge/FPS ölçülmedi. Unity Play/build,
  bot/kaskat simülasyonu ve remote yayın/release yok.206'ya geçme.**

- **Reddedilen önceki taslak: ProLevels V1 202–205 v1 + 201 hedef50 · 2026-10-31.**
  Kullanıcı201 kuş kafesi yerleşimini beğendi,12 hedefi az bulup50 istedi;202–205
  için diğer engelleri de kullanmamızı yetkilendirdi. Son ek tercih: iki sütun,
  bazen yan kenarda tek sütun veya alt kenarda tek sıra; alta taş akışı korunur.
  **201 yalnız Collectible Bird hedef12→50**;26 hamle,iki2×3 grup ve bütün diğer
  asset alanları aynı. Eski12 hedef asset/not/PNG `Batches/201/Revisions/v1-goal12/`.
  Yeni dört taslak,4 renk ve chapter9:
  **202 Boyalı Kuş Avlusu:**9×10,88aktif,30hamle Normal; sağ kenar6ev/tek sütun,
  sol alt8oyma taşı kapağı +2bitişik boya kasası. Hedef50kuş/8taş/2kasa.
  **203 Kuşlu Sevkiyat:**9×11,97aktif,30hamle Normal; altta iki3×1kuş sırası,
  orta3açık çıkışta6kargo +9oyma taşı rampası. Hedef50kuş/6kargo/9taş.
  **204 Kuşlu Akarsu:**9×11,93aktif,30hamle Normal; solda2×4kuş,sağda19hücreli
  1su yolu,sağ altta8fener. Hedef50kuş/1tamamlanmış kanal/8fener.
  **205 Mühürlü Kuş Parkı:**9×11,97aktif,28hamle Zor; altta iki2×4kuş bloğu,
  soluncu8/8/8renk isteyen2×4mühür altında; üstlerinde14yalnız-special pusula.
  Hazır Pulse+LineV30/31'de alttaki sabit pusulalara dayanır. Hedef60kuş/14pusula/1mühür.
  Açılışta normal taş yuvaları202:66,203:75,204:76,205:50 (ikisi hazır special);
  boş başlangıç yuvası0/1/0/17dür ve PNG'lerde boş çizilmiştir.203'te kargolar
  taş rampaya,204'te fenerler dibe dayanır; yerleri ilk gravity'de değişmez.
  205'te iki combo sürükleme yönü de10pusulalık geçit açıp17boş yuvayı besler;
  mührün altından yeni normal yuva değil8kalıcı kuş evi çıkar.
  Assetler `ProLevels/LevelV1_00002020.asset` … `00002050.asset`; gerçek katalog
  anahtarları LevelCL_0202…0205. JSON/TR+EN adları ve MD listesi eşleşiyor.
  Kayıt/not/verifier/gerçek sprite PNG'ler `Batches/202-205/`,tekrar inceleme
  `Reviews/202-205/`. Önceki201katalog referansı/eski isimler ve3.186mevcutdosya
  korundu. Güncel BirdHouse servisinde toplama sonrası kota kaldıkça ev açık
  kalır,yeni kuş çıkar; araya vuruşsuz hamle girerse geri çekilir. Kullanıcının
  güncel runtime/library değişiklikleri korunur; eski iki-vuruş döngü özeti esas alınmaz.
  **Statik YAML/mask/origin/goal/katman/refill/hazır-combo desteği ve görsel kontrol
  geçti. Yeni202–205 ve201hedef50 henüz oynanıp onaylanmadı; ölçülmüşdenge/FPS yok.
  Unity Play/build,bot/kaskat simülasyonu,remote yayın/release baseline yapılmadı.
  206'ya geçme.201–210onluğu tamamlanmadığından2–3yeni aile kotası kapanmış sayılmaz.**

- **Önceki iş: ProLevels V1 201 · Guguk Bahçesi / Cuckoo Garden · 2026-10-31.**
  Kullanıcı yeni Birdhouse'u collectible hedefiyle tanıtmamızı; tek tek dağıtmak
  yerine 2×3/2×4/3×3 gruplar, özellikle sol alt + sağ üst istedi. Yalnız201 eklendi:
  **9×10, 88 aktif/2 dış köşe hole, 26 hamle, 4 renk; ilk taslak hedef12 idi,
  sonraki açık kullanıcı isteğiyle50 Collectible Bird(3) oldu.**
  BirdHouse78 iki ayrı **2 sütun×3 satır** blok; üst sağda sütun7–8/satır1–3,
  alt solda sütun2–3/satır8–10 (üstten, 1 tabanlı). Yanlardaki birer açık sütun
  her evi normal eşleşmeyle erişilebilir tutar; merkez ve bütün76 normal yuva
  statik refill grafına bağlı. Hazır special/pin/kaplama/ek engel yok. Evler kalıcı
  üretici; hedef ev kırmak değil, aynı zincirde/sonraki hamlede ikinci vuruşla
  kuş toplamak. Otomatik78 hint ve TR/EN metinleri zaten mevcut; korunmuş durumda.
  Asset `ProLevels/LevelV1_00002010.asset`, katalog `LevelCL_0201`, chapter9;
  isim JSON/MD ve gerçek sprite önizlemesi hazır. Önceki200 V1 katalog yeri,
  tüm eski isimler ve **3.184 mevcut dosya** koruma kaydına göre aynı; Gift Festival,
  can/envanter düzeltmeleri ve yeni BirdHouse runtime/library de korunuyor.
  Kayıt, verifier, PNG, manifest `Batches/201/`; tekrar envanteri `Reviews/201-Birdhouse/`.
  **Statik YAML/mask/goal/erişim/dolum ve görsel kontrol geçti. Kullanıcı yerleşimi
  beğendi;50 hedef revizyonu henüz oynanmadı. Ölçülmüş denge/FPS yok. Unity Play/build,
  bot/kaskat simülasyonu,remote yayın ve release baseline yapılmadı. Sonraki isteği202–205'i yetkilendirdi.**

- **Önceki iş: ProLevels V1 90 / 100 / 199 · 2026-10-29 — 199 v3 jel revizyonu.**
  Kullanıcı199'u hâlâ kolay buldu; üstten2./3. satırda yalnız jeli silip Ancient
  Seal altına4 jel istedi. v3'te yalnız taban `obstacles`/`obstacleOrigins` değişti:
  jel13/21/22/23'ten kaldırılıp2×2 mühür ayak izine48/49/57/58 kondu. Açık jel0;
  mühür açılınca4 seed ortaya çıkar. TNT, üç pusula örtüsü, bütün diğer içerik,
  26 hamle, hedef87/17/1 ve mühür8/8/8 korundu. v2 `Revisions/199-v2/` altında.
  Güncel çalışma alanından3.334 diğer dosya (UI ses düzeltmesi,90/100,katalog/isimler
  dahil) koruma kaydında; güncel not/PNG/verifier batch klasöründedir.
  **v3 henüz oynanıp onaylanmadı. Statik katman/görsel kontrolü yapıldı; ölçülmüş
  denge, Unity Play/build/bot/kaskat veya remote yayın/release baseline yok.**
  Aşağıdaki v2 açıklaması önceki revizyonun bağlamıdır; aktif jel yerleşimi v3'tür.
  Kullanıcı önce oynama keyfini düşünmemizi, hemen yerleştirmemeyi ve doğadan şekiller
  seçmemizi istedi. Yalnız üç boş katalog yeri dolduruldu:
  **90 Dalga Düellosu:** 9×10, 65 aktif / 25 hole, 30 hamle, toplam420 Boss HP,
  oyuncu180 HP; 13 oyma taşı / 1 sepet / 6 kalkan.
  **100 Yaprak Düellosu:** 9×10, 70 aktif / 20 hole, 32 hamle, toplam480 Boss HP,
  oyuncu210 HP; 7 hücrelik1 mıknatıs / 12 sandık2 / 6 kalkan.
  Boss'lar3 manuel rakip; yalnız BossDamage goal, erken alet tehdidi yok.
  **199 Deniz Kabuğu v2:** 9×11, 87 aktif / 12 hole, **26 hamle** Super Hard;
  spiral17 oyma taşı + 17 special-only pusula, merkezde1 adet2×2 mühür (8/8/8),
  4 kuş, 4 jel seed'i (1 açık / 3 örtülü), jel üzerinde1 sabit TNT.
  Hedefler87 jel / 17 pusula / 1 mühür. Kullanıcı31 hamleyi kolay buldu; kabuğun
  içinin dolu görünmesini ve26 hamle denemesini istedi.18 boş normal hücreye mevcut
  başlangıç pin desteğiyle taş eklendi; ağız sınırındaki mevcut1 taşın rengi de ayarlı.
  Artık44 normal taş + 1 TNT + 4 kuş; görünür boş normal yuva yok. İç renkler ve
  rastgele dış sınırda başlangıç üçlüsü/2×2 oluşmaması kontrol edildi. İlk TNT3
  pusulalık ağzı açar; yukarıdan yenilenen yuvalar27→41 olur (iç taşlar baştan var).
  90'da44,100'de33 normal başlangıç taşı ve1/12 boş yuva önceki gibi korundu.
  Assetler `ProLevels/LevelV1_00000900.asset`, `00001000.asset`, `00001990.asset`;
  gerçek anahtarlar LevelCL_090 / LevelCL_0100 / LevelCL_0199 ve TR+EN isimler kayıtlı.
  Yeni mekanik tanıtılmadı; seçilen ailelerin önceki V1 kullanımları doğrulandı.
  İlk grupta3.098 mevcut dosya korunmuştu.199 v2 revizyonunda90/100, katalog, isimler
  ve3.107 diğer dosya korundu; onaylı11–15 değişmedi. Eski199 `Revisions/199-v1/` altında.
  Not/snapshot/verifier/gerçek sprite PNG'ler `Batches/ProLevels-V1-090-100-199/`;
  aktif V1 tekrar haritaları ve15 board incelemesi `Reviews/ProLevels-V1-090-100-199/`.
  YAML, mask, katman, hedef, custom path, başlangıç dolumu, sabit TNT/movable konumu
  ve kapak sonrası dolum kontrolleri geçti. Runtime/VFX/audio/border kodu değişmedi.
  Mevcut kuş/sepet/mıknatıs geçici allocation maliyetleri notta açıkça kaydedildi;
  tamamen havuzlu veya ölçülmüş performans iddiası yok.
  **199 v1/v2 için “kolay” geri bildirimi var; aktif v3 henüz oynanıp onaylanmadı. Denge ölçümü yok. Unity Play/build/bot/kaskat simülasyonu,
  cihaz FPS ölçümü ve remote yayın/release baseline yapılmadı.** Başka level tasarlama.

- **Önceki iş: ProLevels V1 yalnız 11–15 · 2026-10-28 — v1 reddedildi, v2 görsel olarak onaylandı.**
  Kullanıcı ilk dört taslağı "ben level demem ... ne bir atraksiyonu" diye reddetti. Sadece teknik
  doğruluk ve geniş boş alan yeterli bulunmadı. V1 U/L/köşe/alt sıra tasarımları ret kaydıyla
  `Batches/ProLevels-V1-011-015/Revisions/v1/` altında saklandı. Mevcut **11 / Yağlı Rampa aynı**.
  Güncel v2: **12 Açık Avlu — 9×10,74 aktif/16 hole,31 hamle,19 sandık1+28 Mud,1 dikey roket;
  13 Katmanlı Geçit — 9×9,63 aktif/18 hole,30 hamle,9 sandık1+9 sandık2+18 alt Mud;
  14 Yağlı Köşe — 9×10,63 aktif/27 hole,29 hamle,12 sandık2+6 sandık1+12 yağ/tavan18,1 yatay roket;
  15 Üç Katlı Bent — 9×11,75 aktif/24 hole,30 hamle,6'şar sandık1/2/3+24 Mud,1 Pulse+1 roket.**
  12–14 Normal hedef %55–85,15 Zor hedef %35–55; ölçülmüş oran yok. 12'de pencere arkasındaki
  kapak alt avluya akış açar;13'te sol oda uzun çalışma kanadını açar;14'te yağlı sıra iki alanı
  bağlar;15'te hazır combo ilk bendi, ardından diğer iki hazneyi aşamalı açar. 12'de28,15'te30
  hücre baştan gerçekten boş; taşlar dolmuş gibi çizilmedi. Special konumlarının başlangıçta
  düşmediği ve açılmaların yeni dolum rotaları oluşturduğu statik grafla doğrulandı.
  Assetler `ProLevels/LevelV1_00000120.asset` … `00000150.asset`; GUID/isim/V1 katalog aynı.
  Yeni runtime/library/border/VFX kodu yok. 3.007 mevcut dosya v2 koruma kaydına göre aynı;
  ilk taslaktan sonra dışarıdan değişen LevelV1_00000040 korunup ayrı kaydedildi.
  Not/PNG/verifier/önizleme: `Batches/ProLevels-V1-011-015/`; 189 eski katalog şekli yalnız
  benzerlik adayları için tarandı (`Repetition-v2.*`), benzersizlik/beğeni skoru sayılmadı.
  Yeni PNG'ler incelendi. Unity UI/Play/build/bot/kaskat simülasyonu, cihaz FPS/win-rate ölçümü yok.
  **Kullanıcı v2 için “güzel oldu eline sağlık” dedi (2026-10-29): görsel/tasarım onayı; oynanış dengesi ölçümü değil. 11–15 korunur; 16'ya geçme.** Onluk başına min2/max3 obstacle;
  5 Zor/9 Daha Zor/10 katları Boss. Başlangıca 170+ Normal %40–50 yönünü uygulama.

- **101+ refill special yumuşatma · 2026-10-24:** Kullanıcı %40–90 engellemeyi agresif buldu; tamamen kaldırmak yerine %30–70 istedi. `RefillSpecialGuard` otomatik modda 101+ için %30–70, 51–100 için önceki %40–90, 1–50 için %80 kullanır. Seviyeye bağlı deterministik dağılım ve aday special başına mevcut rastgele karar korundu; elle verilen oranlar önceliklidir. Hamle/yerleşim/hedef/isim ve bütün 296 Settings asseti aynı. Editör açıklaması güncellendi. Statik sınır/kapsam kontrolü; Unity Play/build/sim veya kazanma oranı ölçümü yok.

- **89–90 kullanıcı oynanış düzeltmesi · 2026-10-23:** 89 Gizli Jel Bahçesi (`LevelP_00991.asset` / `LevelCL_089`) ve 90 Mor Düello (`LevelP_00500.asset` / `LevelCL_090`) **28→33 hamle**. 89'un sağ alt 5 sütunluk taş alanı üstüne 4. satır/5–9. sütunlarda 5 normal hücre eklendi; aktif alan ve tam-board jel hedefi41→46. Diğer hedef/engel/katmanlar, 90 Boss düzeni, katalog ve isimler aynı. Önceki kullanıcı değişiklikleri dahil snapshot `Batches/089-090-Ease-2026-10-23/Before/`; not/önce-sonra PNG/statik doğrulama aynı klasörde. 1.897 diğer Settings/Scripts/isim dosyası aynı; YAML ve mask/katman kontrolü geçti. Gerçek sprite önizlemesi incelendi; Play/build/sim ve yeni denge ölçümü yok.

- **189 İkiz Gölet Kalesi hazır, inceleme bekliyor:** `LevelP_001930.asset`, `LevelCL_0189`, chapter8; 9×11, 96 aktif/3 alt kenar hole, **32 hamle Super Hard**. Solda iki C biçimli kanalın üstünde14 nilüfer; içlerinde iki2×2 EggBird penceresi (8 kuş). Sağda dört bağımsız2×2 Wall parçası; sağlarında16 GrassFlower üstü/16 Lantern altı. Alt solda GelLauncherRight, alt sağda GelLauncherUp; yatay atış dik kartuşun alt hücresine ulaşır. Hedefler2 kanal/14 nilüfer/4 duvar/16 fener; yardımcı kuş ve kartuşlara doğrudan hedef yok. Hazır special/pin/ek dolum kotası yok. 2×2 boşluklar gerçek hole yerine nilüfersiz kuş pencereleri olarak yorumlandı; kullanıcıya açıklanan varsayım, onay sayılmaz. Katalog/isim/not/PNG hazır; `Batches/189/validation.json`, tekrar inceleme `Reviews/189-Twin-Ponds/`. Önceki2.790 Settings/Scripts/Resources/Scenes/Prefabs dosyası aynı. Runtime/köşe kodu değişmedi. Statik ve sprite kontrolü geçti; Play/build/sim veya ölçülmüş kazanma oranı yok. 190 istenmedi.

- **188 Üç Mühürlü Geçit hazır, inceleme bekliyor:** `LevelP_001920.asset`, `LevelCL_0188`, chapter8; 9×11, 95aktif/4dışköşehole, 28hamle Normal. 3 bitişik 3×3AncientSeal64 (kırmızı/sarı/yeşil başlangıç; her renk7), altında6Cargo34+21PlasticTwoStage40; altta9SculptingSpecial39 çıkış satırı. 59başlangıçtaşhücresi:34üsttenbeslenen+18altcep+7altçıkış; hazır special/renk pini/ekdolumkotası yok. Önceki187tamkatalog envanteri ve6yerleşim haritası;143/107/123/110/149/173/186/187 spritekarşılaştırması, mekanik farklar `Reviews/188-Catalog/`. Önceki2.788Settings/Scripts/Resources/Scenes/Prefabs aynı;186–187kullanıcılevelları, runtimeveköşekodu korundu. İsimJSON/MD ve not/PNG `Level188-Uc-Muhurlu-Gecit.*`; snapshot/author/verify `Batches/188/`. Statik ve görsel kontrol geçti;Play/build/simveölçülmüşwinrate yok. Sonraki kullanıcı isteğiyle 189 yetkilendirildi.

- **186–187 kullanıcı levelları kataloğa bağlandı:** `LevelP_001900.asset` → 186 / `LevelCL_0186` / Zümrüt Vitrin; `LevelP_001910.asset` → 187 / `LevelCL_0187` / Boyalı Akarsu. İkisi chapter8; yalnız katalog ve isim eşleştirmesi yapıldı, level içerikleri düzenlenmedi. Katalog 1–187 sıralı, anahtarlar benzersiz, yeni GUID referansları ve isim anahtarları doğrulandı; YAML ayrıştırıldı. Yeni tasarım veya oyun dengesi değerlendirmesi yok; Unity Play/build/sim yapılmadı.

- **ColorCrate editör düzeltmesi:** Başlangıç kalan vuruşu artık normal Obstacle paletinde 1/2/3 ve aşama önizlemesiyle seçilir. Normal yerleşim ayrı başlangıç verisi saklar; gerçek katmanlar Overlay desteğini korur. Obstacle fırçası 179’daki mevcut sandığın aşamasını Mud tabanını ezmeden günceller. 2.406 ayar/script/Resources dosyası aynı; level assetleri değişmedi. `Checks/ColorCrate-Obstacle-Brush/`; statik kontrol, Unity compile/Play/build/sim yok.

Son güncelleme: 2026-10-23.

- **171 onaylı ve korundu:** Renkli Çerçeve → `LevelP_001750.asset`; 28 hamle, 78 aktif/21 hole.
- **172 korundu:** Boya İskelesi → `LevelP_001760.asset`; 29 hamle, 87 aktif/12 hole. Kullanıcı genel tasarım yönüne olumlu geri bildirim verdi; bireysel oyun dengesi ölçülmedi.
- **173–180 hazır, inceleme taslakları:** Kullanıcı “180'e kadar git ... bakayım olmadı geri alırız” diyerek grubu yetkilendirdi. Kargo Rıhtımı / Mor Mercek / Mühürlü Vitray / Dolaplı Geçit / Boru Orgu / Çiçekli Pusula / Çelik Kilit / Kanyon Köprüsü. 175 Hard, 179 Super Hard, 180 Boss Duel. İnceleme: `Levels173-180.md`; toplu PNG: `Levels173-180-Onizleme.png`.
- **181 Basamaklı Arşiv v4 korundu:** 9×11, 17 kenardan bağlı hole, 82 aktif, 42 başlangıç taşı; normal komşulukta 28/54 hücrelik iki bölge. 26 hamle, 6 ColorChest, 4 dolap, 16 oyma taşı, 40 Mud. Kullanıcının köşe yuvarlatmasını koruma isteğiyle ortak border kodu/theme/prefab/sahne değiştirilmedi. Not/PNG `Level181-Basamakli-Arsiv.*`; statik köşe yakın planı `Level181-Capraz-Kose-Kontrolu.png`. v4 için ayrı onay/denge ölçümü yok; kullanıcı yeni isteğiyle 182'yi yetkilendirdi. v1/v2/v3 arşivde korundu.
- **182 Fener Şenliği v2, inceleme bekliyor:** Kullanıcı parçalı kandil/dama düzenini beğenmedi; tek sütun ve yanında toplu engel istedi. `LevelP_001860.asset`; aynı 9×11 maske, 81 aktif/18 hole, **49 başlangıç taşı, 28 hamle**. Açılış: 4. sütunda kesintisiz **11 Lantern75**, sağında **3×7 HelmetPorcelain30**. Hedefler **30 fener + 21 porselen** korundu; 19 fener mevcut generic movable refill ile gelir, aynı sütuna sabitlenmez. ID40 hamle kilidi 75 için yok. Statik/görsel kontrol; ölçülmüş denge yok. v1 `Batches/182/Revisions/v1/` içinde. Önceki 1.860 baseline dosyası aynı; eski snapshot ile farklı bulunan mevcut library korundu. Not/PNG `Level182-Fener-Senligi.*`; kayıt `Batches/182/`, tekrar inceleme `Reviews/182-Column/`. Kullanıcı 183 tasarımını yetkilendirdi; 182 korundu.
- **183 Fenerli Bent hazır, inceleme bekliyor:** `LevelP_001870.asset`, chapter8; 9×11 tam board, 99 aktif, 0 authored hole, yalnız kaynak runtime geçirgen. **28 hamle; 1 WaterChannel76 (1 kaynak+20 açılacak hücre), 24 Lantern75 (11 açılış+13 refill), 12 SculptingStone28**. Fener solda tam sütun, taş alt kenarda6×2 set, kanal orta alanda kıvrılıp alta çıkar. Güncel kanalda açılan hücreler normal oynanır kalır. Katalogdaki eski183 test asseti/meta korundu, yalnız183 yeni üretim assetine bağlandı. Not/PNG `Level183-Fenerli-Bent.*`; snapshot/validation `Batches/183/`, tekrar `Reviews/183-Canal/`. 2.390 ayar/script/Resources dosyası korundu. Statik ve sprite kontrolü; Play/build/sim yok, ölçülmüş denge yok. Kullanıcı sonraki isteğiyle184ü yetkilendirdi;183korundu.
- **184 Boya Terasları hazır, inceleme bekliyor:** `LevelP_001880.asset`, chapter8;9×11,73aktif/26hole,47başlangıçtaş,2geçirgenkaynak,28hamle Normal. **4PaintCanBox63 (36kutu),2WaterChannel76 (23açılacak hücre),8SculptingSpecial39**. Kenara bağlı4×4boyaterası;sağda3sütunluk iki bağımsız su yolu, solda2×4specialşeridi. Fener/ColorCrate/pin/hazırspecial yok. İsimJSONveMD kayıtlı. Statik+spritekontrolü;2.401ayar/script/Resourcesdosyası aynı,179kullanıcıdüzeni korundu. Kayıt`Batches/184/`,not/PNG`Level184-Boya-Teraslari.*`,tekrar`Reviews/184-Terraces/`. Oyun dengesi ölçülmedi;Play/build/sim yok.Kullanıcısonrakiisteğiyle185i yetkilendirdi;184korundu.
- **185 Işık Merdiveni hazır, inceleme bekliyor:** `LevelP_001890.asset`,chapter8;9×11,78aktif/21hole,12normalbaşlangıçhücresi,32hamleHard. Kullanıcının referansı yatayaynalandı:merdivensola,küçükkıvrımsağalta. **24Lantern75/21PlasticTwoStage40 yataysatırşeritleri,9SculptingStone28,12SculptingSpecial39**. Alt mor engel için opsiyonel soru yanıtlanmadan açıklanan pusula varsayımı kullanıldı.1PulseCore20;hedeflerbaşlangıçaeşit,ekrefillkotasıyok. Ana66+alt6/6aktifbileşen,referansın2köşetemasıkorundu;ortakborderdeğişmedi. İsim/PNG/not`Level185-Isik-Merdiveni.*`,kayıt`Batches/185/`,referans12kalıcıkopya,karşılaştırma`Reviews/185-Mirror/`.2.403öncekiayar/script/Resourcesaynı. Statik+spritekontrolü;Play/build/simyok,oynandengesiölçülmedi.186–187 kullanıcı tarafından oluşturuldu ve aşağıdaki katalog kaydına bağlandı.
- **179 / LevelP_001830 son revizyon:** Kullanıcı asseti düzenledi; PulseCore20/LineV21 ve kaldırılan renk pinleri dahil bütün mevcut değişiklikleri korundu. **31 hamle**, aynı maske/hedef/katman; 24 sandık üstten **3→2→1→1 hit** (6×3,6×2,12×1), toplam sandık yükü72→42. İlk sıra sağlam kaldı. Yeni `StackedObstacleEntry.initialRemainingHits` yalnız ColorCrate71–74 için başlangıç hasarlı aşama sağlar;0 eski varsayılan. Ortak library aynı. State/başlangıç görseli/editor Overlay ve PNG destekli; üst ve beneath kalan hit korunur. 18 açık+12 örtü altında tutulan taş. Kullanıcı güncel asseti ve4kaynak snapshot `Batches/179-StageGradient/Before/`, doğrulama aynı klasörde. Diğer2.390ayar/script/Resources dosyası aynı. C#compile/UnityPlay/build/sim yok; yeniden oynanış sonucu yok. EskiBatches183 kontrolü tarihsel.
- **181 tarihsel inceleme:** 1–180 assetleri tarandı; 7/23/48/68'de benzer basamak/kenar kütlesi, 121'de aynı engel üçlüsü bulundu. `Reviews/181-Repetition/Review.md` ve görsel v1'i anlatır. Şekil benzerliği geçerlidir; son kullanıcı açıklamasına göre ana sorun bu level için kolaylıktır. Benzer şekli otomatik ret gerekçesine dönüştürme.
- **Yeni yön:** ColorCrate 71–74 şimdilik kullanılmaz. Eski ColorChest 21, Wardrobe 27 ve katmanlı yapı kullanılabilir. Aynı iki kanat/orta koridor kompozisyonunu engel değiştirerek tekrarlama; her yeni tasarımda siluet ve açılma sırasını da karşılaştır. Eski levellara dokunma.
- **İsimler 1–189 eksiksiz:** `Assets/_Project/Resources/LevelNames.json`; görünür liste `Docs/LevelDesign/Level-Names.md`. 1–188 isimleri korundu; 189 İkiz Gölet Kalesi eklendi. Kullanıcı istemedikçe mevcut isim değiştirme. Henüz oyun UI bağlantısı yok.
- **Ana tasarım havuzu:** Kullanıcı 1–170'in görünümünü başarılı buluyor. Dış görseller öğrenme/analiz içindir; her görsel aktif leveli yeniden yapma talebi değildir. Referanstaki sandık zorunlu değil. On kalıcı dış görsel ve ilgili eski level örnekleri `Design-Feedback.md` / `References/Index.md` içindedir.
- **Denge:** 170 sonrası normalde %40–50 yönü hedef; 5 Hard, 9 Super Hard. Hamleler ilk ayar, kazanma oranları ölçülmedi.
- **Doğrulama:** 173–180 mask/footprint/hedef/katman/refill/tanıtım/GUID ve görsel kontrolü geçti; Unity YAML ayrıştırıldı. 1.843 önceki ayar/script dosyası korundu. Unity Play/build veya bot/kaskat simülasyonu yapılmadı. Performans/iptal sınırları ortak inceleme notunda.
- **Geri alma:** `Batches/173-180/manifest.json` ve önceki katalog/çalışma kaydı mevcut. `rollback.py` varsayılan dry-run; 181 eklendiği için eski katalog hash kontrolü artık duracaktır. 181 korunmadan eski gruba `--apply` uygulanmaz; kullanıcı geri alma isterse sonraki katalog ekleri hesaba katılır. 181 öncesi katalog/snapshot `Batches/181/` içinde. İsim kataloğu taslak rollback kapsamı dışında korunur. Henüz geri alma uygulanmadı.
- **Skill:** `.agents/skills/tinyfixers-level-design` kaynak `Skills/tinyfixers-level-design` klasörüne bağlıdır; AGENTS yönlendirmesi de var. Bu dosyadan devam et; onaylı levelları tekrar tasarlama.
