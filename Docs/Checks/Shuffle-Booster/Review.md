# Shuffle joker — 2026-10-22

Kullanıcı: “bazen tıklıyorum çalışmıyor; animasyonla beraber taşları karıştırmalı”. Son açıklama: karıştırılabilir taş yokluğu olağan senaryo değil, yalnız hak harcamayan uç durum koruması olarak kalsın.

Bulunan iki somut kod yolu:

- Joker slotu yalnız `BoosterMode.Shuffle` seçiyordu. Başlatmak için ikinci kez bir TileView'a dokunmak gerekiyordu; `TryUseBoosterAtBoardCell` Shuffle'ı açıkça reddettiğinden boş/engel alanına dokunmak tetiklemiyordu.
- Mevcut renk sayılarıyla uygun permütasyon bulunamayınca yeni renkler üretiliyordu. Yeni renk adetleri eski taşlara eşlenemediğinde `ApplyShuffledTypes` doğrudan çağrılıyor ve animasyon atlanıyordu. Değişmemiş oynanabilir dizilim de başarı kabul edilebiliyordu.

## Son davranış

Kullanıcının açık düzeltmesiyle **iki aşamalı kullanım korunur**: Shuffle slotuna dokunmak mevcut overlay'i açar ve jokeri seçer; çalıştırmaz veya hak harcamaz. Tahtadaki bir taş, engel, taşsız hücre veya authored hole'a ikinci kez dokunmak karıştırmayı onaylar. BoardBoosterTapCatcher Shuffle'ı hücre içeriği sorgulamadan ortak hedef yoluna aktarır. Overlay'in hole panelleri de Shuffle seçiliyken yeniden kullanılan ObstacleClickProxy ile onayı iletir; diğer jokerlerde hole kuralı değişmez. İkinci kez aynı joker düğmesine dokunmak mevcut seçim iptal davranışını korur. Busy/popup kilidi ve merkezî kullanım yetkisi korunur. Hareket ettirilebilir en az iki normal taş yoksa kullanım başlamaz. Hak tüketimi geçerli karıştırma planı kurulduktan sonra bir kez yapılır.

İlk düzeltmede düğmeden doğrudan çalıştırma getirilmişti; bu istenen ürün akışını değiştirdi. Kullanıcı bunun özellikle iki dokunuş olarak tasarlandığını belirtti. Doğrudan çalıştırma geri alındı; yalnız boşa düşen board/hole dokunuşları ve animasyon yedek yolu düzeltildi.

Normal permütasyon aynı taşları animasyonla yeni hücrelere taşır. Yeni renk üretimi gereken yedek yol, uygun hücrelerde birebir çevrim haritası kurar; önce mevcut taşlar animasyonla hareket eder, sonra yeni renkler uygulanır. Asa sprite'ı yüklenmese de taşların sarmal animasyonu çalışır. Joker sonrası olası eşleşmeler normal resolve akışında tamamlanır; hamle tüketimi eklenmedi. Otomatik deadlock karıştırma kendi asasız kayma sunumunu korur.

Special'lar, hareketli engeller, oil ve interaction-locked hücreler sabit tutulur. Değişmemiş renk permütasyonu başarı kabul edilmez. Havuzda aynı TileView'ın başka taş olarak yeniden kullanılması lifetime sürümüyle yakalanır; bayat görünüm haritası commit edilmez. Exclusive erişim zaman aşımı, hâlâ çalışan işler üzerine yazma yetkisi sayılmaz.

Busy/flow pause kapsamları `finally` ile kapanır. Yarım kalan animasyonun geçerli eski grid konumları onarılır. Asa görselleri yeniden kullanılır; iptal/sahne yok oluşu/yeniden Init, runId'yi geçersizleştirip kökü gizler. Önceki kullanıcı değişiklikleri üzerine çalışıldı; değişiklik öncesi dört dosya `Before/` içinde.

## Performans ve doğrulama

Yeni Update, per-frame allocation veya efekt GameObject üretim döngüsü eklenmedi. Hole paneli vekilleri yalnız ilk gerektiğinde eklenip mevcut panel havuzuyla yeniden kullanılır; her seçimde tekrar üretilmez. Mevcut asa/parıltı görselleri yeniden kullanılır; harita/snapshot ve listeler yalnız karıştırma başında ayrılır. Ek lifetime/source tamponları yalnız bu kullanımın ömrü kadardır. Sarmal sin/cos hesabı taş başına yerine kare başına bir kez yapılır. SFX mevcut merkezî `PlayJokerSfx` yolunda kalır. Cihaz performansı ölçülmedi.

`csi Tools/check_shuffle_booster.csx`: **9.918 assertion geçti**; dört üretim C# dosyası syntax kontrolünden geçti. Üretimden çıkarılan yöntemler; düzensiz maskelerde birebir kaynak eşlemesi, kilitli hücre koruması, iki aşamalı seçim/onay, taş/engel/boşluk/hole dokunuşu, board dışı dokunuş ve iptal, yedek yolun sprite olmadan hareketi, doğru commit/renk, tek hak tüketimi, iptal sonrası busy/pause/konum temizliği, exclusive zaman aşımı ve havuz lifetime kontrolü için izole stub'larla çalıştırıldı. Bu test Unity UI/coroutine motorunu veya gerçek cihazı çalıştırmaz. `git diff --check` geçti. Unity build, Play veya bot/kaskat simülasyonu yapılmadı.
