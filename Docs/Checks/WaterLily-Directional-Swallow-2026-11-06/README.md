# Nilüfer · dört yönlü yutma kareleri

2026-11-06. Kullanıcının verdiği lillyup/right/down/left sheet adımları yutma animasyonuna bağlandı. Hedefin gerçek grid hücresi yönü seçer. Soldaki kaynak kapalıdan açığa sıralandığı için ters çevrildi; bütün oynatımlar açık→kapalıdır. Çiçek kapanınca dönüş boyunca yalnız o yönün son kapalı karesi görünür; merkezde eski şişkin CF5’e geçer. Geri fırlatmada eski CF5→CF1 animasyonu korunur.

Eski CF1–CF5 dosyaları,ikon,normal/yaprak hasarı,hedef sayımı,bir oyuncu hamlesi bekleme,geri fırlatma ve ses ayarları değişmedi. Special kendi hücresinde küçülürken nilüfer ona uzanır;normal taş kalır. Yeni çizimlerde yön şeklin içinde olduğu için ek eksen esnetmesi/3D eğme uygulanmaz;mevcut hit salınımı üstüne eklenmeye devam eder.

## Asset hazırlığı

Orijinal dört sheet `Sources/` altında SHA ile saklandı. Kaynaklarda komşu karelerin X sınırları örtüştüğü için eşit beş parçaya bölmek yaprakları kesiyordu. `Tools/prepare_waterlily_swallow.py` beş büyük opak bağlı parçayı bulur,antialias/glow piksellerini ilgili parçaya bağlar. Yeniden çizim/renklendirme yok. Her adım aynı640×640saydam tuvale alınır ve512’ye ölçeklenir;adımlar arasında bağımsız büyütüp küçültme yapılmaz. Kaynak gövdesinin merkezine göre hizalanır. EskiCF1görünür büyüklüğünü korumak için sadece bu padli kareler UI’da1.44ölçek kullanır.

Dört2560×512atlas/20sprite vardır;metaGUID/internalID/pivot/rect bağlantıları doğrulandı. `ObstacleLibrary` ID69 auxiliarySprites ilk beş referansı aynıdır;5–9Up,10–14Right,15–19Down,20–24Left eklenmiştir. Diğer obstacle tanımları byte düzeyinde aynı. Mipmap/ReadWrite kapalı,sıkıştırma açık,maxTextureSize4096;dosya boyutları toplam yaklaşık3.1MiB. GPUformatı/cihazdaki bellek ölçülmedi. Orijinal idle/hedef/yaprak görselleri değişmedi.

## Performans,iptal ve kontroller

Yeni güncelleme döngüsü,coroutine,Texture/Sprite.Create,efekt GameObject’i veya board taraması yok. Mevcut `IList<Sprite>` ve tek servis Update kullanılır;yalnız kare değişince sprite atanır. Atlaslar mevcut kütüphane referanslarıyla yüklenir;vuruşta Resources/asset araması yapılmaz. Dört ek texture’ın yükleme/bellek maliyeti vardır;performans artışı veya sıfır toplam allocation iddia edilmez.

Mevcut UiVfxPool special/yaprak yolları,job/CellHold/rezervasyon cleanup ve merkezi BoardAudioDirector.PlayOneShotClip yolu korunur. OnDisable hareketi durdurur,pooled uçuşu bırakır,poz/ölçeği geri alır. Yutma sırasında gelen special hit mevcut tek işi geri fırlatma işine aktarır;normal taş silinmez. Sprite indeks önbelleği artık doğrudan idle/enable sprite atamasını da kontrol eder;aynı indeks yeni çizimi yanlışlıkla bastırmaz.

- `csi Tools/check_waterlily_response.csx`: **662 PASS**;582 mevcut kontrol +80 yön/kare/sabit kapalı dönüş/eski şişkin poz/normal taşı koruma/kesilme/cleanup kontrolü. Üretim yöntemleri Unity doubles ile çalıştırıldı;cihaz render testi değildir.
- `python3 Tools/check_waterlily_swallow_assets.py`: **PASS**;20spritebağlantısı,4kaynakSHA,5eski referans,bütün level assetleri ve6054diğer proje dosyası aynı. Yalnız mevcutservice/library değişti;4yeni atlas ve metaları eklendi.
- `Frames.png` ve `Timeline.png` açılıp incelendi. Timeline üretim zamanlamasından statik çizimdir;Unity ekran görüntüsü değildir.

Unity build/Play,bot/düşüş-kaskat simülasyonu,yayın/baseline/commit yok. GPU/FPS/GC ölçülmedi;gerçek cihazdaki animasyon henüz görülmedi. Bu görsel iş229’un tamamlanan v4verilerini veya diğer levelları değiştirmedi.229’un önceki kapsam snapshot’ı bu sonradan yetkili service/library değişimini drift olarak yakalar;hash’leri sessizce benimsetmek yerine bu kaydı kullanın.

[Kareler](Frames.png) · [Zaman çizgisi](Timeline.png)
