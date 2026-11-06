# Sinek kutusu / düşen yumurta / meyve

2026-11-06. Kullanıcının son açık kuralı uygulandı: special başına1yumurta,her yumurtadan3sinek,her elma için iki sürü(6lokma). Önceki fikirdeki5sinek/tek yumurta bir meyve anlatımı geçersizdir. Hedef yalnız meyvedir;kutu ve yumurta yardımcı kaynaklardır. Yeni aile hiçbir numaralı levele otomatik eklenmedi.

## Editörde kullanım

Obstacle paletinde **FlyEggBox100** koy:2×2kasa,dört görünen yumurta. **FlyFruit102** seçip Width/Height ayarla;1×1,2×2ve dikdörtgen boyutlar aynı ID’yi kullanır. Her meyve bir origin ve bir hedeftir;hücre sayısı hedefi büyütmez. Yerleştirilmiş elmaların boyutları koordinat listesinden değiştirilebilir. Hole,dolu alan veya üst örtü varsa geçersiz resize uygulanmaz;önce örtüyü kaldır. Silgi/üzerine boyama meyvenin tüm footprint’ini temizler;metadata ve hedef sayısı güncellenir.

Bir kutu +iki elma toplam4yumurta/12sinek sağlar. İstenirse **FlyEgg101** doğrudan1×1movable olarak da konabilir;örneğin yedek kaynak veya ilk deneme için. Yeterli açık normal taş iniş alanı bırakılmalıdır. Meyveler başlangıçta yerleştirilir,kutu meyve üretmez.

Goals eşitlemesi meyve origin’lerini(base ve stacked kayıtlarında aynı origin’i bir kez) sayar. Bu aile için kutu/yumurta hedefini kaldırıp tek FlyFruit hedefi ve bütün elma ikonunu kurar. Diğer ailelerin hedeflerini korur. TopHUD ve PreLevel mevcut obstacle/iconOverride yolunu kullanır;UI sistemine ayrı sayaç eklenmedi. Meyve altıncı gerçek sinek varışında kaldırılır ve hedef bir kez azalır. Kutudan üretim veya yumurta kırılması hedef ilerletmez.

## Oynanış ve uçuş

- Kutu yalnız `SpecialActivation` kabul eder;normal match,booster veya scripted vuruşla yumurta çıkmaz. Ortak special-origin dedup yolu kullanılır;aynı special footprint’inin kasa hücreleri dört ayrı üretim sayılmaz.
- Geçerli vuruşta kendi yuvası rezerve edilip gizlenir;yalnız uçan görsel hareket eder. Hareketsiz,hazır,normal renkli ve specialsız bir taş seçilir. View/lifetime/planlı düşüş/iniş görseli/rezervasyon tekrar doğrulanır;taş hareketlenirse başka hedef seçilir. Başarılı varışta aynı TileView mevcut movable sisteme dönüştürülür;normal taş temizleme/puan/hedef olayı üretilmez. Başarısızlık/timeout kendi yumurtasını geri görünür yapar. Son dört başarılı yerleşimden sonra kasa normal katman temizleme yoluyla kalkar.
- Yumurtanın library kuralı1hit,Any,movable1×1’dir. Normal komşu eşleşme veya special kırar;swap/düşüş/yana kayma mevcut motorla yürür. Gelecekte iki aşama istenirse hasar/evre library’den ayarlanabilir;hatch yalnız gerçek yok oluş olayındadır.
- Hatch,goal dinleyicilerinden önce uçuş işini kaydeder. Üç sinek yükselir,ayrışır,kavisle meyveye gider;kanatlar ayrı menteşelerden çırpar. Yükselme/ayrılma ön görünüm;seyahat sağ profil veya aynalanmış sol profildir. EggBird kaynak uçuşu incelendi;görsel ritim ve koordinat ölçümü esas alındı,çevre hücrelere bomba hasarı veya obstacle-local AudioSource kopyalanmadı.
- Bir sürü hedefte3lokma rezerve eder. Aynı meyve en fazla iki sürüyü kabul eder;üçüncü sürü başka meyveye yönelir/bekler. Her gerçek varış1lokma tüketir;3lokmada verilen yenmiş görsel,6lokmada temizleme. Meyve normal/special/booster ile doğrudan hasar almaz. Sinekler başka taş,yumurta veya meyveye ek alan hasarı vermez.
- Örtülü meyve açılana kadar sürü işi bekleyebilir;oyuncu akışı bloke edilmez. Meyveler bitince fazladan bekleyen sürüler level-end’i tutmaz. PatchBot,aktif meyve hedefinin vurulabilir kaynakları olarak kutu/yumurtayı da değerlendirir;meyvenin kendisine boşa gitmez. Kaynak üretimi meyve kalmayınca hedeflenmez.

## Assetler ve performans

Dokuz gönderilen kaynak `Sources/` içinde SHA ile saklandı. Kasa boş çerçeve +dört ayrı yumurta Image’ı ile kurulur;full reference palet içindir. Elmanın bütün/yenmiş halleri,önden/yandan sinek gövde ve kanatları kullanılır. Yan sheet’teki geniş gri halo korunmuş kaynakta kalır;opak parça +sınırlı antialias kenarı ayırılarak oyun sprite’ı hazırlandı. Yeniden çizim yok.

Altı sinek parçası **tek1536×1024texture/6sprite** üzerinde;fly Image’ları ortaktexture ve standartUI materyali kullanır. Böylece ayrı parçalar için farklıtexture gereksinimi azaltılır;gerçek drawcall/FPS ölçülmedi. Diğer beş texture512×512. İlk ayrı kesilmiş parçalar `LegacySplitParts/` altında proje dışında(Assets dışında) saklanır;oyun atlası kullanır. `prepare_flyfruit_assets.py` sahip olunan art hash’leri değişmişse yeniden yazmayı reddeder.

Tek servis Update:en fazla8aktif sürü/24sinek. Sonraki sürüler kuyrukta bekler;uçuşlar önceden hazırlanmış24flyroot/72partImage ve mevcutUiVfxPool’dan kullanılır. Yumurta görselleri/kayıtları kaynak kapasitesine göre başlangıçta hazırlanır. Kasa vuruşunda kaynak çocukları yeniden oluşturulmaz;varışta aynı movableTileView korunur. Sprite/Texture.Create,Linq veya yeni coroutine eklenmedi. Native görsel nesne tekrar kullanımı doubles’ta kontrol edildi;gerçek CPU/GC/GPU ölçülmedi. Pool/HashSet/job/kuyruk büyümesi gibi mevcut managed maliyetler için sıfır allocation iddia edilmez.

Tam board araması yalnız iniş hedefi gerektiğinde,flight başına0.1snretry sınırıyla yapılır;her kare taranmaz. Sürü hedefi küçük meyve listesinde0.1snaralıkla aranır. Stage/scene değişimi bütün uçuşları,brood lokma rezervasyonlarını,işleri ve interceptor’ları temizler. Goal callback’i varışın içinde servisi kapatsa da generation kontrolü listeyi yeniden kullanmaz. UI ölçek farkları için hücre adımı VFXroot’un yerel uzayında ölçülür;üçlü formasyon ekran kenarına taşmayacak apex sınırı kullanır. Ses normal central obstacle presentation/BoardAudioDirector yolundadır;obstacle-local AudioSource veya fallback yok,yeni ses dosyası seçilmedi.

## Kontrol kanıtı ve sınır

- `csi Tools/check_flyfruit.csx`: **349 PASS**;üretim counter,delivery,shared-update,flyview,goal ve resize yöntemleri Unity doubles ile çalıştırıldı. Düşen/reused view reddi,retarget,timeoutta slot iadesi,aynı meyveye eşzamanlı sürü,covered/reveal,1×1/2×2 hedef,resize atomikliği,scene/goal-callback teardown ve pooled görsel geri dönüşü kapsandı.
- `csi Tools/check_dimerdal_per_box.csx`:409mevcut üretim/yüksek hareket ve289kutu başına kota kontrolü geçti. Paylaşılan idle-replacement guard çıkarıldı;eski Dimerdal davranışı korunur.
- `csi Tools/check_patchbot_wall_weakpoint.csx`:147genel ve260duvar/kombo/kaynak kontrolü geçti;mevcut Wall seçimi ve un önceliği korunur.
- `python3 Tools/check_flyfruit_assets.py`:7mevcut proje dosyası değişti;6058diğer proje dosyası,tüm level/katalog/ad assetleri ve önceki obstacle tanımları aynı. Yeni100/101/102,spriteGUID/fileID/atlasrect/ikon/normal-movable kuralları ve özgün kaynakSHA’ları doğrulandı.
- `Prepared-Assets.png` ve `Review.png` açıldı. `Review.png` gerçek sprite’larla statik parça birleşimidir;oyun ekranı değildir. Yeni bir level veya denge simülasyonu üretmez.

Unity build/Play/cihaz render testi,bot/düşüş/kaskat simülasyonu,yayın,releasebaseline veya commit yapılmadı. Motorun tamamı derlenmiş/oynanmış diye sunulmaz. İlk cihaz denemesinde yumurtanın gerçek düşüşü,kutu başına dört ayrı special,kanatların gerçek Canvas görünümü ve son meyveden sonra level-end gözlenmeli.

[Görsel inceleme](Review.png) · [Asset audit](asset-validation.json)
