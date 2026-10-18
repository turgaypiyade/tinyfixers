# Copper Override dönüşü ve şarj efekti

## İşaretleme / special yerleştirme kaynak görseli — 2026-10-18

LineH/LineV batch ve Pulse yerleştirmesinde kaynak çiftin grid kaydı, görsel aksiyonları oynatılmadan önce silinir. Fanout kaynağını oynatma sırasında hücreden aramak `null` veriyor, böylece mevcut büyüme/dönüş/şimşek hiç başlamıyordu.

`SpecialFanoutService`, `ctx.OverrideFanoutOrigin` gerçek görselini ortak yerleştirme aksiyonuna aktarır. Aksiyon kaynağı ve `LifetimeVersion` değerini kurulumda yakalar; kaynak hücresine yeni taş dolsa da o taşı kaynak sanmaz. Aynı yol normal taş işaretleme, LineH/LineV, PulseCore ve PatchBot yerleştirmelerinde kullanılır. Kaynak görsel 0,14 saniyede %25 büyür; `BeginActivation` dönüşü, yatışı ve havuzlanmış yıldırım mesh'ini işaretleme bitene kadar sürdürür. Boyut/konum dönüş animasyonunun dönüş açısına müdahale etmez. Işın kaynağı da saklanan gerçek görseli izler.

İptal/bitişte mevcut `finally` temizliği çalışır. Kaynak havuza dönüp yeni bir ömürle kullanılmışsa eski lift durur; eski temizleme yeni taşın pozunu veya dönüşünü sıfırlamaz. Yeni VFX nesnesi, Update döngüsü, coroutine, board taraması veya kare başı buffer tahsisi eklenmedi; yalnız mevcut aksiyon/handle içine kaynak ve ömür bilgisi kondu. Cihaz performansı ölçülmedi.

`csi Tools/check_override_mark_scale.csx`: 229 kontrol; temizlenmiş kaynak hücresi, yeniden dolmuş hücre, %25 büyüme/rotasyon, eski çağıran için kurulumda yakalama, yeniden kullanılan taş/iptal ve ortak fanout bağlantısı. `csi Tools/check_override_turn_frames.csx`: mevcut dönüş, yıldırım mesh'i, iki-Override combo ve havuz/teardown kontrolleri de geçti. Unity görsel oynatımı veya proje build'i çalıştırılmadı.

2026-10-16: Kullanıcının `ScreenRecording_10-16-2026 17-16-48_1.MP4` referansındaki
hızlı eksen dönüşü, hafif sağ/sol yatış ve kısa çevresel şimşekler temel alındı.

`Tile_test` üzerinde `turnFramesOnly = true`: üst sağa, alt sola döner. Eski tam Z turları,
parçaların ayrılması, birleşme büyümesi ve genel TileView doğuş halo’su kapalı kalır.
İki yarı aynı küre pivotunda birlikte en fazla ±9° yatar; kendi sprite dönüşleri kesilmez.
Bu, 2D sprite kareleriyle yapılan bir yaklaşım; gerçek bir 3D model dönüşü değildir.

- Oluşum/idle: 0,8 sn oynar, 2 sn sonra tekrarlar. Yatış giriş/çıkışta yumuşar;
  şimşekler bitişte söner. Sabit Copper ikonuna dönülür.
- Hız hedefi: 18 → 120 sprite karesi/sn; ilk %50’de (0,4 sn) karesel hızlanma,
  sonra tepe hızını koruma. Render başına sınır 1’den 2 sprite adımına çıkarıldı.
  6/8 karelik mevcut sheet’lerde bu, yarım turdan küçüktür. 60 FPS’de hedef 120;
  düşük render FPS’de görünen hız bu sınıra göre düşer.
- Kullanım/fanout: `BeginActivation()` aynı animasyonu başlatır; ışınlar ve yerleşim boyunca
  tepe hızında sürdürür. 0,8 sn sonunda idle beklemesine geçmez. Mevcut `StopOriginLift`
  ve action `finally` temizliği efekti bitirir. Hedef/hasar/impact zamanları değiştirilmedi.
- Şimşekler: `OverrideChargeGraphic` tek havuzlanmış UI mesh’idir. Üç kısa yay, her biri
  7 segment ve 3 renk/genişlik katmanı: en fazla 252 vertex / 126 üçgen. Beyaza yakın
  çekirdek ve camgöbeği ışık; şekil/flicker 24 Hz’de değişir. Varsayılan UI materyali.
- Görünürlük düzeltmesi: parlak çekirdek genişliği hücrenin %0,8’inden %1,8’ine,
  orta ışık %2,2’den %4’e, dış ışık %5,5’ten %9,5’e çıktı. Yaylar yaklaşık %29 uzadı;
  yarıçap 0,525 hücre, düzensizlik ±0,075 hücre. Dış sınır yaklaşık 0,65 hücre.
- Turuncu kapak: karelerde şimşek sembolü aynı açıda kaldığı için, kapağın eliptik
  kenarında dönüş fazına bağlı sıcak beyaz bir ışık dolaşır. Üst parçanın yönünü ve
  ortak yatış pivotunu izler. Yeni V2 sheet’inde sembolün dönüşü de çizilmiştir; kenar ışığı buna eşlik eder.
  Aynı mesh’e 128 vertex / 64 üçgen ekler: toplam en fazla 380 vertex / 190 üçgen;
  ek obje, materyal veya coroutine yoktur. Reset/return ışık fazını da sıfırlar.
- Hız hissi: hız yükselirken gövdede mavi/kırmızı/yeşil ve altta mavi/mor/yeşil
  yumuşak renk izleri belirir; üst/alt ters yönde akar. Bunlar gerçek görüntü blur’u
  değil, düşük alfa ile çizilen hareket izleridir. Silüet ve turuncu kapak korunur.
  Altı iz × 10 segment × 2 katman: +480 vertex / 240 üçgen. En yüksek toplam
  860 vertex / 430 üçgen; aynı havuzlanmış graphic, materyal ve 24 Hz sürücüsü kullanılır.
  Yatışın sin/cos değerleri mesh güncellemesinde bir kez hesaplanır.

## Kaynak kareler ve hizalama

- `Art/Icons/Specials/OverrideCopper_TurnTopV2.png`: yeni üst sheet’in 8 karesi;
  sağ dönüş için ters sıra: 1 → 8 → 7 → 6 → 5 → 4 → 3 → 2. Turuncu işaret
  farklı açılarda çizildi. Önceki üst sheet korunur ama aktif prefab V2’yi kullanır.
- `Art/Icons/Specials/OverrideCopper_TurnBottom.png`: alt sıra
  1 → 2 → 4 → 3 → 5 → 6; sol dönüş için ileri oynar.
- Kaynak PNG’ler byte olarak korundu. Frame rect’leri Multiple sprite importer metadata’sında;
  Full Rect, Read/Write ve mipmap kapalı, texture max 2048, sıkıştırma açık.
- Görünür kareler orijinal Copper_UP/Down sınırlarına göre sabit ölçü/merkezle kaydedilir.
  Trim merkezleri ortak pivot etrafında döndürülür; yarılar yatışta birbirinden kopmaz.
- Inspector: `turnFramesPerSecond` (120), `rockingDegrees` (9), `chargeLightning` (true),
  `turnBlurStrength` (0,8).
  `turnFramesOnly` kapatılırsa eski Z/ayrılma/birleşme yolu korunur.

## Performans ve yaşam döngüsü

Üç mevcut Image kullanılır. Renk izleri için ilave Image/ghost sprite üretilmez. Şimşek için `UiVfxPool` üzerinden yalnız başlangıçta bir obje
alınır; bileşen ilk kullanımda eklenir, sonraki kullanımda tekrar kullanılır. Çakış/segment
başına obje, array, texture, Sprite.Create, materyal, coroutine veya board taraması yoktur.
Aynı sprite tekrar atanmaz. Mevcut animasyon coroutine’i mesh’i de sürer; yeni Update yoktur.
Stop/OnDisable/normal bitişte mesh havuza döner; sprite, ölçü, konum, yatış ve sabit ikon
alfası sıfırlanır. İkinci Stop tekrar iade yapmaz. Havuzun Play-stop/quit koruması kullanılır;
return sırasında yeni persistent root oluşturulmaz. Cihazda performans ölçülmedi.

## Kontrol

`csi Tools/check_override_turn_frames.csx`: üretim metotlarını UI doubles ile çalıştırır.
15/30/60/120 FPS, yön/sıra/pivot, hızlanma, ortak yatış, eski yol, eksik sprite fallback’i,
uzun aktivasyon, iptal/replay/havuz iadesi, mesh sınırları ve 24 Hz throttling doğrulandı.
8 üst/6 alt prefab GUID/fileID sıraları, TileView preview bağlantısı ve fanout aktivasyonu kontrol edildi.
Kurulu Unity referanslarıyla 614 C# dosyasında kaynak/API tanılaması: 0 hata; build/emit yok.
Unity UI/Play Mode açılmadı, cihaz görünümü doğrulanmadı.

Play’i durdurup yeniden başlatın; mevcut havuzlanmış runtime taşlar prefab ayarını kendiliğinden
almaz. Önce bir Override’ı boşta izleyin, sonra normal taşla kullanın: yatış sırasında hızlı
üst/alt dönüş ve kısa şimşekler birlikte sürmeli; hedeflere ışınlar bittiğinde temizlenmeli.
Sheet karelerinin eşit açı aralıklarında çizilmemesi bazı görsel atlamalara yol açabilir.

Ek kontrol: büyüyen şimşek genişlikleri/dış sınırları, kapak ışığının fazla hareketi, yatışla hizası, eksik kare ve havuz reset davranışı doğrulandı. Unity’de görsel kontrol yapılmadı.

V2 kontrolü: 1774×887 kaynak PNG byte olarak aynen kopyalandı; 4×2 sprite rect’leri
yalnız importer metadata’sında tanımlandı. Test, sekiz üst kareyi, 18→120 hız rampasını,
ikili render adımı sınırını, hız izi geometry tavanını ve yeniden kullanımda iz state’inin
sıfırlanmasını doğrular. Yeni shader/texture oluşturulmadı; cihaz maliyeti ölçülmedi.

## Override + Override

İki kaynaktan `OverrideSpecialView` asset referansları `BoardVfxService` üzerinden `SetTurnSources` ile
combo controller’a iletilir. Aynı üst/alt sheet’ler kullanılır; dizi veya texture kopyalanmaz.
İki havuzlanmış view, combo’nun mevcut coroutine’i tarafından dışarıdan sürülür;
view başına creation/idle coroutine’i başlamaz.

- Ekranda soldaki ilk rolü alır; X eşitse üstteki ilk roldür. İlk rol üstte sağa/altta sola,
  ikinci rol üstte sola/altta sağa döner. Kaynak/partner argüman sırası sonucu değiştirmez.
- Açılma 0,55 sn ve çarpışmaya dönüş 0,15 sn boyunca aynı monoton frame fazı ilerler.
  Faz değişiminde hız/faz yeniden başlamaz. Eski bütün ikonu Z etrafında döndürme, yeni
  sprite view varken uygulanmaz. Kaynak kareler yoksa eski tam ikon yolu korunur.
- Ayrılma yönü iki başlangıç konumu arasındaki eksenden alınır; dikey çiftte üstteki
  yukarı/alttaki aşağı açılır. Her iki top mevcut ortak lift’i de uygular.
- `OverrideComboLinkGraphic` tek bir havuzlanmış mesh ile karşılıklı kenarlar arasında
  camgöbeği/beyaz şimşek bağı çizer. Uçlar konum/ölçekle güncellenir, çarpışmaya kadar
  bağ kısalır; impact callback’inden önce havuza döner. 16 segment, 3 ışık katmanı:
  en fazla 192 vertex / 96 üçgen. Sabit buffer/vertex akışı; segment başına obje yoktur.
- Toplar impact sonrasındaki mevcut sönme boyunca da döner. Sıfır alfada view’ler ve
  charge mesh’leri iade edilir. Pre-clear 0,70 sn, dalga 1,50 sn, toplam 2,35 sn;
  `OnImpact` ve dalga/hasar callback sırası korunur.
- Solo/çift şimşeklerin seçilmiş bölümleri arada 1,6×, bağda 1,7× kalınlaşır; ek geometri yoktur.
  Solo ışık dış sınırı bu kısa vurgularda yaklaşık 0,68 hücreye çıkabilir.

İlk edinmede view çocukları oluşturulur; sıcak havuzda yeni Image/GameObject oluşturulmaz.
İki view, iki charge mesh ve tek bağ yeniden kullanılır. Stop, OnDisable, yeniden oynatma ve
coroutine finally temizliği idempotenttir; havuzlanan view sahne ikonuna referans tutmaz.
Play’deki Image taraması tekrar kullanılan List ile yapılır. Kapalı eski halo için her combo’da
boşuna 256×256 texture üretimi kaldırıldı. Yeni Update veya board taraması eklenmedi.
Bu sonuçlar kaynak incelemesi ve doubles testlerinden gelir; cihaz maliyeti ölçülmedi.

`Tools/check_override_turn_frames.csx`, `Tools/override_combo_harness.txt` ile üretim
combo coroutine’ini 15/30/60/120 FPS’de yatay/dikey, normal/ters argüman sıralarıyla çalıştırır.
Yönler, kesintisiz faz, şimşek uçları, tek impact, monoton dalga, süreler, eksik asset fallback’i,
üç aşamada iptal/disable, tekrar kullanım ve geometri tavanı doğrulanır. Kaynak/API tanılaması
614 dosyada 0 hata verdi; Unity build/Play çalıştırılmadı.

Oyun kontrolü: yatay iki Override’ı birleştirin; sonra altlı üstlü bir çifti birleştirin.
Açılma ve geri çarpış sırasında üstler birbirinin tersine, altlar kendi üstlerinin tersine
kesintisiz dönmeli; mavi/beyaz bağ iki topun kenarına tutunmalı ve çarpışmada bitmeli.
# iPhone kapanış düzeltmesi — 2026-10-17

İşaretleme sırasında kaynak Override, mevcut 0,14 sn kalkış easing’iyle %25 büyür ve kanal bitene kadar bu boyutta dönmeye devam eder. Yalnız kaynak işaretleme görselleri büyür; hedeflere verilen etki ve dönüş hızı değişmez. Başlangıç pozisyonu ve ölçek tek pose dizisinde saklanır; ek per-frame allocation yoktur. Normal bitiş/iptal başlangıçtaki nonuniform ölçeği de geri yükler. `check_override_mark_scale.csx`: 219 üretim metodu kontrolü, 30/60/120 FPS.

`Cannot set the parent of the GameObject 'OvChargeLightning' while activating or deactivating the parent GameObject 'OverrideSpecialView'` hatası: OnDisable → Stop → HideAll → UiVfxPool.Return, Unity üst nesnenin aktiflik ağacını işlerken child üzerinde SetParent çağırıyordu.

OnDisable artık charge mesh’i sıfırlayıp görünmez yapar ve mevcut view altında tutar. Sonraki açık Stop/Play çağrısında normal havuz iadesi yapılır. Owner yok edilirse child Unity tarafından beraber yok edilir; shutdown’da kalıcı root oluşturulmaz. View başına en fazla bir tutulan mesh vardır, yeni coroutine/update kuyruğu yoktur. Normal animasyon bitişi ve combo EndComboTurn doğrudan havuz iadesine devam eder.

`check_override_turn_frames.csx` parent aktivasyon geçişi sırasında pool Return’ü hata veren double ile kapanışı, sonraki kullanımda aynı mesh’in dönüşünü, tekrar Stop’u ve combo iptalini kontrol eder. 1.887 Override ve 11.138 combo kontrolü geçti. Bu kontrol native iPhone testinin yerine geçmez; Unity/build çalıştırılmadı.

## İşaretlemede çizim önceliği — 2026-10-18

Override + special fanout sırasında kaynak ikon, tahta Canvas sırasına göre +200;
ayrı kökteki üst/alt dönüş parçaları ve havuzlanmış charge yıldırımı +201 önceliği alır.
Böylece hedeflerin oluşum ikonları (+100) ve PatchBot pervaneleri (+101) kaynak animasyonu
örtemez. Sorting layer tahtadan alınır. Mevcut ikon raycaster’ı korunur; raycast almayan
dönüş/yıldırım köküne raycaster eklenmez.

Öncelik referans sayacıyla tutulur: devam eden creation reveal varsa activation bitince
ikon +100’e iner, reveal yoksa normal tahta sırasını miras alır. Canvas’lar görünürlüğü
korumak için açık kalır, yalnız overrideSorting kapanır. Normal bitiş, finally/iptal,
OnDisable ve PrepareForRelease temizler. Eski lifetime’a ait action yeniden kullanılan
taşa dokunmaz; aynı handle iki kez temizlense bile sayaç yalnız bir kez bırakılır.

Canvas’lar havuzlanan taş üzerinde tekrar kullanılır; masking yenilemesi yalnız scope
geçişlerinde, Override’ın küçük alt ağacında tekrar kullanılan List ile yapılır. Liste
geçiş sonunda boşaltılır. Yeni per-frame tarama, coroutine veya efekt nesnesi eklenmedi.
Cihaz performansı ölçülmedi.

`csi Tools/check_override_sorting.csx`: 115 üretim metodu kontrolü; öncelik, sorting layer,
geç eklenen yıldırımın Canvas mirası, masking, input, iç içe reveal/activation, 50 tekrar,
disable ve havuz temizliği geçti. `check_override_mark_scale.csx`: 236 kontrol;
`check_override_turn_frames.csx`: 1.887 view ve 11.138 combo kontrolü geçti.
Unity build/Play çalıştırılmadı; oyun içi örtüşme görüntüsü ayrıca kontrol edilmeli.

