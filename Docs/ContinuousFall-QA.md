# Kesintisiz düşüş kontrolü

`01_Game.unity` içinde `useContinuousFallMotion: 1`, `useReferenceFallMotion: 0`.
Karşılaştırma için Play oturumları arasında yalnız kesintisiz motor kutusunu değiştirin.

## Build almadan çalıştırılan kontroller

```sh
csi Tools/check_continuous_fall.csx
```

Test, gerçek `TileFallMotionSystem`, `BoardVisualCoordinator` ve TileView iniş bildirimi kodlarını
Unity yerine küçük test nesneleriyle çalıştırır. Ayrıca sekiz C# kaynağını sözdizimi için tarar.
Bu kontrol Unity derlemesinin, gerçek coroutine zamanlamasının veya Play Mode testinin yerine geçmez.

- 30/60/120 FPS altında ivme ve tavan hız hesabı.
- Havada yeniden hedefleme: konum ve momentum; eski biletlerin tamamlanması.
- Settle ofsetinden geriye dönüş olmaması; kare ortasında biten başlangıç gecikmesi.
- Geciken sütun liderini takip ve ivme sıfırken yeniden hareket.
- Spawn taşının mevcut akış hızını alması.
- Ters sırada çalışan planlarda spawn başlangıcının ve engel çevresindeki yolun korunması.
- Havuzda yeniden kullanılan taşa eski hareketin yazmaması; park edilmiş taşta sahiplik kontrolü.
- İniş callback'i sırasında yeniden kullanım; plan başına tek iniş bildirimi.
- Clear sonrası diğer düşüşleri beklemeden refill; eski motorun bekleme davranışının korunması.
- Arkada kalan düşüş için bölüm sonu job'unun korunması ve bitince bırakılması.
- Önceki cascade'e ait eşleşme taşının varışının ayrıca beklenmesi.
- Dış konum yazısı nedeniyle motorun bıraktığı, hücresine oturmuş taşta eksik iniş bildiriminin eşleşmeyi kilitlememesi.
- Bu varış kontrolünün havadaki, kuyruktaki veya başka taşla değiştirilmiş hücreyi yanlışlıkla hazır saymaması.
- Bekleme sırasında havuzda yeniden kullanılan eşleşme taşının eski temizleme işlemine alınmaması.

## Unity'de gözle doğrulanacaklar

1. Uzun bir sütun düşerken aynı sütunun altında yeni boşluk açın: geriye sıçrama veya yeniden kalkış olmamalı.
2. Bir sütunda erken eşleşme, diğerinde uzun düşüş oluşturun: eşleşme temizlenince dolum uzun sütunu beklememeli.
3. Line/Pulse/Override/PatchBot zincirlerini ve engel çevresindeki çapraz yolları deneyin.
   Special süpürmesinin kasıtlı hücre kilitleri korunur; henüz serbest bırakılmamış hücreye refill beklenmez.
4. Yeni spawn, special ve hareketli engellerin aynı sütunda birbirini geçmediğini gözleyin.
5. Düşerken duraklatıp devam edin; 10 saniyelik bekleme uyarısı duraklama yüzünden çıkmamalı.
6. Hareketli taşla hamle engellenmeli; uygun, oturmuş taşlarda dinamik hamle çalışmalı.
7. Son hamlede arkada düşüş sürerken bölüm sonu penceresi açılmamalı.
8. Console'da `[FallMotion]`, eksik referans veya coroutine hatası olmamalı.

Mevcut hız profili korunur. Kesintisiz yolda sütun/rank bazlı ek bekleme kaldırılmıştır;
sütunun ritmini fiziksel spawn aralığı ve takip kuralı belirler. Görsel tempo Unity'de ayrıca değerlendirilmelidir.

## Diyagonal akış sırası (2026-09-25)

Çapraz/horizontal yol segmenti, geçtiği yerel hücre bölgesini yalnız geçiş süresince tutar.
Ortak kaynak, ortak çıkış veya kesişen komşu geçişte arkadaki taş öndekinin dönüşünü bekler.
Öndeki taşın bütün düşüşünü bitirmesi gerekmez; köşeyi geçtikten sonra sıra açılır.
Uzak satır/sütunlardaki geçişler ve ilgisiz dikey düşüşler paralel devam eder.

Ek bağımsız testler: farklı hedeflere aynı anda yönelen taşlar; 30/60/120 FPS; gecikmiş lider;
çaprazı bekleyen taşın arkasındaki dikey kuyruk; dolu görsel çıkış; havada yeniden hedefleme;
liderin havuza dönmesi; reset; uzun kare; art arda dönüşler ve karşılıklı sütunlardan birleşme.

Unity ağır çekim kontrolü: aynı kaynaktan iki boşluğa kayışta önce alt taş dönüşe başlamalı,
üst taş onun ardından gelmeli. Düz düşen takipçi sırayı bozmamalı. Tahtanın başka tarafındaki
bağımsız dönüş bu sırayı beklememeli. Bu görsel kontrol bağımsız test düzeneğinde doğrulanamaz.

## Kısa sütunda temizlik ve dolumun devri (2026-09-25)

Normal pop temizliğinde hücre, küçülme animasyonu başlamadan boşaltılıyordu. Akış pompası bir
sonraki karede doluma geçtiği için gelen taş, hâlâ görünen eski taşın gövdesiyle çakışabiliyordu.
Kesintisiz modda artık her normal pop kendi gövdesi kaybolunca hücresini bırakır (mevcut küçülme
süresi en fazla 0.06 oyun saniyesi). Bağımsız patlama parçacıkları ve diğer hücrelerin temizliği
beklenmez. Aynı devir radial dalganın normal pop taşlarına da uygulanır; uzun HUD uçuşu bitene
kadar hücre tutulmaz. Bu bir global düşüş bariyeri değildir.

`csi Tools/check_tile_lifetime.csx`: gerçek PlayPop ve temizleme/devir metotlarıyla eski erken
boşaltma hatası, 30/60/120 FPS ve ağır çekim, gecikmiş pop, havuzda yeniden kullanım, hücredeki
taşın değişmesi ve iki temizliğin bağımsız bırakılması doğrulanır.
`csi Tools/check_continuous_fall.csx`: beş satırlı sütunda dolum sürerken yeni alt boşluk açılması,
konumun korunması, yeni spawn'ın kuyruğun arkasına katılması, en az 0.92 hücre takip mesafesi ve
bağımsız sütunun tamamlanması eklenmiştir. Diyagonal sıra testleri de aynı pakettedir.

Unity'de henüz doğrulanmadı: level 27'de üç alt taşı kırıp 0.1 timeScale ile izleyin. Eski gövde
kaybolmadan gelen taş içinden geçmemeli; yeni taşlar üst girişten sırayla görünmeli. Paylaşılan
`RESULT OK` ve sıfır grid mismatch, hareket sırasında görsel çakışma olmadığı anlamına gelmez.

## Dinamik takas ve mobil sürükleme (2026-09-25)

Takas sonrası eşleşme kontrolü, yeni girdi kabul kontrolünden ayrıldı. Takasın kendi
`CellHold` kaydı eşleşmeyi reddetmez; ikinci bir hücre sahibi, pending special, rezervasyon,
hareket eden veya kuyrukta düşüş bekleyen eşleşme katılımcısı hâlâ reddedilir. İlgisiz sütunun
animasyonu, oturmuş eşleşmenin geçerliliğini değiştirmez.

Unity reddedilmiş BeginDrag sonrasında da EndDrag gönderebilir. Sürükleme artık kabul edilmiş
olmalı ve aynı taş ömrüne, hareket token'ına ve hücreye ait olmalı; aksi hâlde taşı grid hedefine
snap etmez. Kabul edilmiş sürükleme, kozmetik iniş hareketini durdurur. Boss hedef seçimi ve
çarpma kontrolü de o anda sürüklenen taşı engel hedefine dönüştürmez.

Düşüşte iki saniyelik bekleme uyarısı mesafe/dönüş korumasını kaldırmaz. Bekleme uzasa da takipçi
öndeki taşın içinden geçemez. Gerçek bir rota kilitlenmesi varsa `[FallMotion]` uyarısı incelenmeli.

- `csi Tools/check_dynamic_input.csx`: üretim metotlarıyla 21 eşleşme ve sürükleme sahipliği kontrolü.
- `csi Tools/check_continuous_fall.csx`: 79 düşüş kontrolü; üç saniye geciken lideri izleme ve
  çapraz sıra için 15/30/60/120 FPS ve 0.2 saniyelik kareler dahil.

Mobil/Play Mode kontrolü henüz yapılmadı:

1. Düşen bir taşı sürüklemeyi deneyip bırakın: hedefe aniden sıçramamalı, diğer taşları geçmemeli.
2. Bir sütun düşerken başka yerde oturmuş taşlarla geçerli eşleşme yapın: takas geri dönmemeli.
3. Boss oil atarken oil olmayan bölgede aynı hamleyi deneyin. Yağlı hücrelerin mevcut oyun
   kuralıyla kilitli kalması beklenir; ilgisiz oturmuş eşleşmeler takasın kendi kilidine takılmamalı.
4. Bir taşı basılı tutarken boss atışı gelsin: sürüklenen taşın üzerine yeni engel yerleşmemeli.
