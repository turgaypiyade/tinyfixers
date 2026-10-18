# Proje logları

Konsol çıktılarının anahtarları: `Assets/_Project/Resources/GameDiagnostics.asset`.
Unity Project panelinde asset’i seçip Inspector’dan gerekli alanı açın. Yedi alanın tümü
varsayılan olarak kapalıdır; eksik asset de kapalı kabul edilir.

| Anahtar | Korunan tanılama |
| --- | --- |
| Critical Errors | Eksik referanslar, yakalanan istisnalar, timeout/kurtarma uyarıları |
| Board Flow | Resolve/düşüş/temizleme akış izi ve isteğe bağlı diagonal kayıt |
| Board Integrity | GridData/TileView snapshot ve Editor/Development tutarlılık doğrulaması |
| Special Chains | Special dispatch/kuyruk/zincir teşhisi |
| Performance | Frame/managed-heap örnekleme, spike ve baseline |
| Backend | Hesap, bulut kayıt, ödeme ve bağlantı hataları |
| Analytics Events | Yalnız Editor/Development: level_start/end için gerçek payload değeri, String tipi ve SDK çağrısı; TF sembolü kapalıysa açık uyarı |
| Editor Tools | Editor komutları, import ve simülasyon raporları |

Normal başlatma/başarı, UI tıklama, VFX spawn/konum, workshop ve tekrarlayan taş logları
kaldırıldı. Kalan konsol çağrıları doğrudan kendi `GameDiagnostics` koşulunun içindedir.
Buna yakalanmış hata/uyarı logları da dahildir: açılmadıkları sürece yazılmazlar.
Unity’nin kendi exception/error çıktısı veya üçüncü taraf SDK kodu topluca susturulmaz.
Mevcut release `ReleaseLogGate` Unity/SDK bilgi mesajlarını filtrelemeye devam eder;
ayrıntılı akış incelemesi Editor/Development Build içindir.

## Çalışma maliyeti ve davranış

- Koşul konsol çağrısının dışındadır: kapalıyken interpolation, string.Join/ToString veya
  log argümanlarındaki tanılama sorguları değerlendirilmez. Lazy lambda/params dizisi yoktur.
- Ayar asset’i sahne açılmadan ısıtılır; sonraki okumalar aynı referansı kullanır. Asset yoksa
  her çağrıda Resources araması yapılmaz. Domain reload kapalı Play oturumunda cache sıfırlanır.
- MatchFinder tam snapshot/StringBuilder ve geliştirme tutarlılık kontrolleri yalnız
  Board Integrity açıkken çalışır. Bunlar üretim kurtarma mekanizmaları değildir.
- Frame/GC örneklemesi Performance kapalıyken çağrılmaz. Obstacle hit StackTrace toplama
  ve string birleştirme Board Flow kapalıyken çalışmaz. Trace-only tile üyelik sorguları
  BoardAnimator’ın trace bloğuna taşındı. Joker ve Pulse/Line debug callback’leri kaldırıldı.
- Diagonal rolling capture için Board Flow yanında mevcut menüden Rolling Capture da açılır.
  Board Flow kapalıyken kayıt başlatılmaz; açık bir kayıt kapatılırsa dosya üretmeden temizlenir.
- Oyun kurtarma/drain, exception handling, Firebase Analytics, kayıt yükleme/yazma ve
  satın alma teslimatı korunur. Konsol kontrolleri bu işlemleri devre dışı bırakmaz.

İyileşmeler kaynak incelemesi ve izole kontrollerle doğrulandı; cihazda FPS/GC ölçümü yapılmadı.

## Doğrulama

`csi Tools/check_project_logging.csx`: 615 kaynak dosyasında Editor/iOS/Android,
Development/release ve isteğe bağlı SDK sembolleri açık/kapalı 14 koşul kombinasyonu
incelenir. Parametresiz konsol çağrısı kalmadığı, yedi normal anahtarın asset değerlerinin 0 olduğu,
anahtarların bağımsız/live olması, cache/missing-asset/reset davranışı ve ağır tanılama
entry guard’ları doğrulanır. Script Firebase Analytics çağrısının korunduğunu da kontrol eder.

`check_override_turn_frames.csx`, `check_board_watchdog_pause.csx` ve `check_oil_async.csx`
üretim metotlarını doubles ile çalıştırarak dönüş/çarpışma, gerçek stall kurtarması ve
Oil async/iptal/havuz davranışlarının sürdüğünü kontrol eder. Bu harness’lerin log
anahtarları da kapalıdır.

Kurulu Unity referanslarıyla 615 C# dosyasında kaynak/API tanılaması: 0 hata.
Unity build veya Play Mode çalıştırılmadı. Önceden mevcut LocalNotificationService
`s_initialized` uyarısı bu temizliğin dışında kaldı.

## Analytics testi

Kullanıcı isteğiyle `analyticsEvents: 1` açık bırakıldı; diğer yedi anahtar kapalıdır.
Bu loglar release derlemesinde bulunmaz. Test bitince Inspector’daki Analytics Events
alanını kapatın. `level` anahtarı, `"151"` gibi InvariantCulture metin değeri taşır;
kazanma sonunda ilerleme artmış olsa bile biten denemenin seviyesi korunur.
Unity Editor Analytics masaüstü stub kullanır: log yalnız payload/SDK çağrısını doğrular.
Gerçek alım Android/iOS cihazın Firebase DebugView ekranında doğrulanmalıdır.

`csi Tools/check_backend_startup.csx`: üretim metotlarıyla start/end, string veri tipi,
boş/geçersiz değer, tekrar parametre, ilerleme, simülasyon ve IAP bağlantı callback akışı.

### level_start / level_end alan tipleri

| Analytics tanımı | Parametreler |
| --- | --- |
| Event kapsamlı özel boyut (String) | `level`, `attempt`, `success`, `assist_tier`, `fails_before`, `struggles_before`, `struggled`, `measurement_version`, `is_development` |
| Özel metrik (Int64) | `seconds`, `moves_left`, `moves_total`, `continues`, `extra_moves`, `continue_coins`, `ad_continues` |

`success`, `struggled`, `is_development` metin olarak `"0"` / `"1"` gönderilir.
`measurement_version` değeri korunur; diğer event türlerinin parametre tipleri değişmez.
Sayısal metrikleri özel boyut olarak tanımlamayın. Bu düzeltme önceki eventlerin
parametrelerini geriye dönük değiştirmez. Google kaynakları:
[App custom dimensions](https://support.google.com/analytics/answer/14239696?hl=en),
[Desktop stub](https://firebase.google.com/docs/unity/setup#available-libraries-desktop).
