# Oil yayılması — 2026-10-16

Eski yol OilSpreadAction’ı blocking sequencer’a koyup animasyon bitene kadar ResolveBoard’u bekletiyordu. Ayrıca genel `ObstacleSpread` job/activity sayaçları dinamik hamleyi tüm tahtada kapatıyor. Bu nedenle Oil’i yalnızca bu mevcut job’a taşımak yeterli değildi.

Hedef seçimi, hamlede Oil vurulduysa yayılmama, en fazla bir hücre yayılma, tavan ve oynanabilir hamle bırakma kuralları korundu. Hedef, taşın uygunluğu tekrar doğrulandıktan sonra anında Oil yapılır ve dinamik hedef sayımı bildirilir. Animasyon kendi `OilSpread` job’unu ve non-blocking `Presentation` kaydını tutar; resolve, input ve düşüş beklemez. Bölüm sonu ve tahtayı bütünüyle yeniden yazan shuffle işlemi görselin tamamlanmasını bekler.

Bulaşan hücre animasyon sırasında da Oil olduğu için normal match/swap’a katılmaz. Bu kalıcı engel kuralıdır: hücre Oil temizlenince açılır. Ek bir sütun/tahta kilidi alınmaz. Barrel/water tank’in mevcut global input politikası bu değişiklikte değiştirilmedi.

Kalıcı overlay iki renderer yolunda da uçuş bitene kadar hedefi çizmez. Başka hamle overlay yenilese de bu kural korunur. Special uçuş sırasında Oil’i temizlerse efekt bir sonraki animasyon güncellemesinde iptal olur; varışta veri tekrar yazılmaz. Special’ın hücreye varışta hasar uygulayan ortak akışı değiştirilmedi.

Efekt prefab’ı, drop ve blend çocukları ortak `UiVfxPool` üzerinden tekrar kullanılır. Flight nesnesi/frame listesi de yeniden kullanılır. Tek Update yalnız aktif hedefleri O(1) Oil sorgusuyla kontrol eder; animasyon karelerinde tahta taraması, coroutine/WaitForSeconds veya obje üretimi yoktur. Overlay yenilemesi tamamlanma/iptalde toplu yapılır. Disable, board yeniden Init ve sahne kapanışında job’lar bırakılır, efektler iade edilir; teardown yeni havuz kökü üretmez. Bunlar kod ve davranış kontrolleridir; cihazda CPU/GC ölçümü yapılmadı.

## Kontroller

`csi Tools/check_oil_async.csx`: production action, flight lifetime/animasyon/cleanup ve input gate metotları, UI kurulum doubles’ı ile çalışır. Anında commit, görsel beklemeden dönüş, yerel overlay, çift hedef önleme, tekrar kullanım, uçuşta isabet, örtüşen uçuşlar, disable ve tekrarlı iptal test edilir. Kaynak/API kontrolü Unity’nin kurulu referanslarıyla yapılır; Unity/project build çalıştırılmaz.

Unity’de gözle doğrulama:

1. Oil’e vurmayan hamle yap. Yayılma sürerken başka bölgede match veya special hamlesi başlat; uçuş bitmesini beklememeli. Bulaşan hücreden swap yapılamamalı.
2. Aynı hamlede Oil kır: yayılmamalı. Hiçbir aday oynanabilir hamle bırakmıyorsa yayılmamalı.
3. Yayılma sürerken hedefe Line/Pulse/Override isabet ettir; hasar varış event’inde olmalı, Oil sonra geri görünmemeli.
4. İki hızlı hamle, yönler arası geçiş ve yatay/dikey kare geçişlerini kontrol et; havuzdan dönen drop/bridge eski konum/rotasyon taşımamalı.
5. Animasyon ortasında sahneyi kapat/yeniden aç; kalmış efekt veya bölüm sonunu bekleten job olmamalı. Bileşeni disable/enable yapınca kalıcı Oil tekrar görünmeli.
6. Uçuş sürerken son hedefi tamamla: popup mevcut bölüm sonu kapısından sonra açılmalı. Hedefler bitmişken/hamle kalmamışken yeni yayılma başlamamalı.
