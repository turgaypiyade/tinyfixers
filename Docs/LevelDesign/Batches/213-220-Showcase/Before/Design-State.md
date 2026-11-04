# Level tasarım çalışma durumu

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
