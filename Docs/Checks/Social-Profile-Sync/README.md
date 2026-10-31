# Sosyal profil senkronu — 2026-10-29

Kullanıcı arkadaşının değiştirdiği ismi takımda, ranks'te ve arkadaş listesinde göremediğini
bildirdi. Canlı iki cihazın kayıtları/kimlikleri görülmedi; aşağıdaki kod eksikleri doğrulandı
ve yerelde düzeltildi. Arkadaşın kodla eklenip eklenmediği ve aynı takıma katılım bilgisi
henüz verilmedi. Yalnız isim değiştirmek arkadaş ilişkisi oluşturmaz.

## Nedenler ve değişiklik

- `FriendState.RealFriends` isim/bölümün ekleme anındaki kopyasıydı. Artık Friends
  açılırken ve ekran açıkken UID ile `players/{uid}` okunur; başarılı profiller tek olayla
  kalıcı listeyi günceller. Eksik/hatalı okuma mevcut arkadaşı silmez, yabancı eklemez.
- `PlayerProfile.OnChanged` bulut kaydı dirty olayına dahil değildi. Profil değişikliği
  artık yedeklemeyi işaretler ve restore çözülmüşse dizin/skor/takım üyeliğine iletilir.
  Açılış default'ları restore öncesinde gerçek profilin üzerine gönderilmez.
- Skor yazımı yalnız ilgili ranks sekmesi açılınca oluyordu. Artık açılış/restore,
  isim/skor değişiminde global, haftalık ve ülke skorları yazılır. Bunlar mevcut
  `name/score/updatedAt` şemasını kullanan üç skor dokümanıdır. Ülke için
  `leaderboards/region_TR/scores/{uid}` benzeri yol kullanılır; rules/index yayını gerekmez.
- Ülke görünümü yalnız botlardan oluşuyordu. Artık kendi ülkesinin gerçek skorlarını da
  okur. Okumalar bekleyen skor yazımının bitmesini beklemez. Aynı sekmenin eski sorgu
  cevabı daha yeni cevabı ezmez. Auth bekleme abonelikleri yinelenmez ve boşaltılır.
- Üye ismi yalnız takıma ilk katılınca yazılıyordu. Profil değişince `members/{uid}.name`
  merge edilir; katılım zamanı ve üye sayısı yeniden yazılmaz. Takım belgesi ve üye
  kayıtları dinlenir. Mevcut takım bilgi penceresi gerçek üye isimlerini sekizerli sayfalarla
  gösterir. Servis dispose olduğunda eklenen iki dinleyici de diğerleriyle durdurulur.
- Yeni ülke skorunun mevcut ülke kaydı hesap silme yoluna eklendi.

Başarılı yazımlar imza ile bastırılır; başarısız yazım başarılı sayılmaz. Her projeksiyonda
tek bekleyen yazım vardır; o sırada değişen profil, onaydan sonra en güncel haliyle gönderilir.
Mevcut 15 saniyelik cloud-save tikinde bağımsız başarısız sosyal yazımlar yeniden denenir.
Yeni arka plan nesnesi veya oyun tahtası taraması yoktur. Sıralama ekranı yalnız açıkken
30 saniyede yenilenir; arkadaş okumaları 15 saniye ve tek bekleyen istek ile sınırlandırılır.
Bu maliyet değerlendirmesidir; cihaz FPS'i ölçülmedi.

## Kontroller

`csi Tools/check_social_profile_sync.csx`:

- On değişen kaynak ve bağımlı takım sınıfları kurulu Unity/Firebase API metadatasıyla
  bağlanır; proje build edilmez, derleme dosyası çıkarılmaz.
- 35 kontrol: üretimdeki PlayerDirectoryService, FriendState, FirebaseLeaderboardService ve gerçek
  takım profil yazım metodu sahte SDK taşımasıyla çalıştırılır. Auth/restore kapıları,
  isim değişimi, bekleyen yazım sırasında yeni isim, onaylanmayan yazımı tekrar deneme,
  dizin değişmeden skor denemesi, UID değişimi, hesap silme kapısı, kısmi arkadaş okuma
  hatası, gereksiz render/write bastırma, gerçek ülke okuması ve auth abonelik temizliği
  denetlenir. Canlı Firebase'e hiçbir veri gönderilmez.

Ek eski `Tools/check_team_lives.csx` çalıştırıldı; mevcut harness'in LivesManager'daki
`System.Security.Cryptography`/`System.Text` using'lerini sınıflardan önce toplamaması
nedeniyle CS1529 verdi. Can/bağış mantığı değiştirilmedi. Bu eski harness'in sonucu
başarılı sayılmadı; ilgili güncel takım kaynakları yeni API kontrolüne dahildir.

Unity, Play, proje build'i veya gerçek iki cihaz testi yapılmadı. `Before/` bu iş öncesindeki
kaynakları içerir; mevcut diğer kullanıcı değişiklikleri geri alınmadı. Level assetleri değişmedi.

## Görünürlük sınırları ve cihaz doğrulaması

- İki cihaz güncel kodu içeren uygulamayı çalıştırmalıdır. Eski uygulama yeni ülke skorunu
  yazmaz; yeni sürümü açıp restore tamamlanınca kayıt oluşur. Eski kurulumlara ait isim
  verisinin sunucuda toplu migration'ı yapılmadı.
- Arkadaş listesi için karşı tarafın kodla eklenmiş olması gerekir; takımda görmek için
  aynı gerçek `TeamId` gerekir. Aynı isimli eski yerel/sim takım, gerçek üyelik garantisi değildir.
- Dünya/ülke ranks listeleri skor sırasındaki ilk100 + kendi satırındır. Her gerçek oyuncunun
  bu listelerde görünmesi beklenmez; botlar hâlâ sıralama dolgusuna dahildir.
- Ranks içindeki **takımların genel puan sıralaması** hâlâ yerel simülasyondur; gerçek takım
  toplam puanı/yarışı bu değişiklik kapsamında yapılmadı. Üye listesi bu sekmeden farklıdır.
- Geçmiş sohbet mesajlarında isim gönderildiği anda kaydedilen isimdir; mesaj geçmişi
  topluca değiştirilmedi. Güncel isim takım bilgi penceresindeki üyelik kaydından okunur.
- Canlı ağ/auth/rules hataları, arkadaş kodu ve iki cihaz logları olmadan dışlanamaz.
  Cihaz kontrolü: kodla ekle → aynı takıma katıl → bir cihazda isim değiştir → diğerinde
  Friends'i aç ve takım bilgisini yeniden aç. Friends açık kalırsa en geç sonraki ekran
  yenilemesinde güncelleme beklenir; çevrimdışında bu süre garanti değildir.
