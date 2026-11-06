# TinyFixers level tasarım hafızası

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
