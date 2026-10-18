# iPhone / Xcode log incelemesi

Verilen Development + IL2CPP 6000.3.9f1 kesitinde crash/managed exception yok. Bu, sonraki oynanışta hata olmadığı veya Analytics event’lerinin sunucuya ulaştığı anlamına gelmez.

| Satır | Değerlendirme |
| --- | --- |
| Firebase `No app has been configured yet` | Başlangıçta native SDK default app bulamıyor; daha sonra Analytics başlıyor ve collection enabled diyor. Tamamen kurulamamış bir Firebase sonucu çıkarılamaz. Unity kodu dependency kontrolü sonrası default app’i açıkça oluşturup tutacak şekilde düzeltildi. Native SDK’nın daha erken bastığı uyarının kaybolduğu henüz cihazda doğrulanmadı. |
| `GoogleAppMeasurementIdentitySupport ... IDFA` | Reklam kimliği yeteneği eksik; temel level event’lerinin devre dışı olduğu anlamına gelmez. Sırf uyarıyı susturmak için IDFA/AdSupport eklenmedi. |
| `Ads account not linked ... conversion measurement` | Google Ads dönüşüm ölçümü entegrasyonuyla ilgili; level_start/end aktarımını bu satırla doğrulamak veya reddetmek mümkün değil. |
| StoreKit `SKInternalErrorDomain Code=12`, Sandbox | Apple sunucu kuyruğu sorgusu hata almış. Kesin neden koddan/logdan belirlenemiyor. Ürün çekme ve gerçek sandbox test satın alımı ayrıca doğrulanmalı. Mevcut OnStoreConnected callback bağlantısı doğru. |
| CoreMotion `com.apple.CoreMotion.plist`, permission denied | Apple’ın managed preferences yoluna erişim uyarısı. Oyunun kendi veri dosyası değil; dosya izinlerini değiştiren bir çözüm eklenmedi. |
| QUIC / UDP / endpoint | Ağ katmanı uyarıları; bu kesit hangi isteğin gerçekten başarısız olduğunu veya retry sonucunu göstermiyor. |
| Allocator ayarları, Metal, PhysX init, Unload | Başlangıç bilgileri. PhysX’in yüklenmesi grid’in fizik kullandığını göstermez. 460,6 MB → 460,6 MB olan tek ölçüm bellek sızıntısı tanısı koydurmaz. |
| IAP EarlyTransactionObserver, CrashReporter no pending report | Bu kesitte başlatma/durum mesajları. |

## Kod düzeltmesi

`FirebaseAuthService.Initialize`: canceled/faulted task üzerinde `Result` okunmuyor. Önce `FirebaseApp.DefaultInstance` oluşturulup static referansta tutuluyor, sonra o app’in Auth nesnesi alınıyor. Başlatma başarısızlığı guard’ı kaldırılarak sonraki Initialize çağrısına tekrar deneme imkânı veriliyor. Backend tanısı açılırsa `[Firebase] Default app hazır; kimlik doğrulama başlıyor.` ile doğrulanabilir; varsayılan tanı ayarı değiştirilmedi.

Kaynak `GoogleService-Info.plist` bundle ID’si ve Unity iPhone bundle ID’si eşleşiyor. Cihazdaki üretilmiş .app içeriği incelenmedi. `FirebaseApp.configure()` ekleyen ikinci bir Swift/AppDelegate başlatıcısı eklenmedi; Unity SDK kendi başlangıç akışını kullanır.

`check_firebase_initialization.csx` SDK/task doubles ile dependency success, cancel, fault, unavailable, app-create exception ve yeniden denemeyi kontrol eder. `check_backend_startup.csx` Analytics payload ve IAP callback akışını kontrol eder. Bunlar native SDK/network testleri değildir. Unity/iOS build veya cihaz çalıştırması yapılmadı.

Kaynaklar: [Firebase Unity başlatma](https://firebase.google.com/docs/unity/setup), [Firebase IDFA toplama ayarı](https://firebase.google.com/docs/analytics/ios/configure-data-collection), [Google Ads cihaz içi dönüşüm ölçümü](https://firebase.google.com/docs/tutorials/ads-ios-on-device-measurement-event-data/step-2), [Apple sandbox testi](https://developer.apple.com/help/app-store-connect/test-in-app-purchases/overview-of-testing-in-sandbox).
