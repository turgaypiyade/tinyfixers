# Wonder Fixers çıkış bildirimi listesi

Kullanıcı ücretsiz seçeneğin kurulup yayımlanmasını açıkça istedi. Yeni ücretli abonelik veya hesap açılmaz; mevcut Cloudflare Worker’a SQLite-backed Durable Object eklenir. Bu ürün Workers Free planında desteklenir; plan veya faturalandırma ayarı değiştirilmez. Hesabın diğer servislerle paylaşılan ücretsiz kotaları geçerlidir. Kaynak: https://developers.cloudflare.com/durable-objects/platform/pricing/

Akış: ana sayfa `#launch-list` → açık izinli e-posta formu → aynı origin üzerindeki `POST /api/launch-list` → özel SQLite kayıtları. Türkçe ve İngilizce desteklenir. Başarı ancak kayıt kabul edildikten sonra gösterilir; backend yoksa veya kota/hata varsa form başarısızlık gösterir. API, e-postanın varlığını herkese açıklamaz. E-posta kutusu sahipliği doğrulanmış değildir; otomatik e-posta gönderimi henüz yoktur. İlk gönderimden önce liste temizliği ve gönderim servisi ayrıca hazırlanmalıdır. Kullanıcı yalnız tek çıkış duyurusuna izin verir; düzenli bültene dönüştürülmez.

Koruma ve işletim:

- 4 KiB gövde sınırı, format ve izin doğrulaması, same-origin kontrolü, bot tuzağı.
- Ağ başına saatte 5 kayıt denemesi; günde toplam 1.000 işlenen deneme ve 100 yeni kayıt; toplam 5.000 aktif kayıt. Sınırda ücretli plana otomatik geçiş yapılmaz; yeni kayıtlar geçici reddedilir. Bunlar uygulama sınırlarıdır, Cloudflare’ın tüm hesap için faturalandırma tavanı değildir.
- E-posta tekil; SQL parametreli; kayıt işlemi ve kotalar transaction ile atomik.
- Açık IP saklanmaz; gizli yönetim anahtarıyla HMAC özeti kısa süreli rate-limit anahtarıdır.
- Kayıtlar 180 günde sona erer; günlük alarm süresi dolan kayıtları ve eski limit kayıtlarını temizler. Sürekli çalışan zamanlayıcı veya oyun kodu yoktur.
- Yönetim `export`/`remove` uçları gizli Bearer anahtarı gerektirir. Kamuya açık liste veya e-posta sayacı yoktur. Anahtar Cloudflare Worker secret’ında ve yerelde gitignore kapsamındaki `Website/.wrangler/waitlist-admin.env` dosyasında bulunur; statik yayın paketine veya Unity asset’lerine girmez.
- İngilizce/Türkçe gizlilik sayfası gerçek veri akışıyla güncellendi. Tawk.to destek bağlantısı korunur.

Yönetim: `python3 Website/waitlist_admin.py export` dosyayı yalnız `.wrangler/waitlist-exports/` altında oluşturur; adresleri terminale yazdırmaz. Silme: `python3 Website/waitlist_admin.py remove --email <adres>`. Bu araçlar e-posta göndermez. Üretimde test kaydı açılırsa testten sonra özel yönetim ucuyla silinir.

Kontrol: `node Website/check_waitlist.mjs` gerçek bellek içi SQLite ile 13 kontrol yapar: izin, origin, boyut, bot tuzağı, tekilleştirme, kota, yönetim yetkisi, silme/son kullanma ve eski video Range/206 davranışı. Bu kontrol ağı/bulutu veya Unity build’ini çalıştırmaz.

Dağıtım: mevcut `wondigames` Worker; `LAUNCH_LIST` binding, `LaunchWaitlist` sınıfı, `v1-launch-list` SQLite migration. Bu sınıf verisini korumadan silmeyin/yeniden adlandırmayın. Yalnız site görünümüne geri dönmek için storage migration’ını silmek gerekmez.

Yayın tamamlandı: https://www.wondigames.com/#launch-list . Canlı sağlık kontrolü HTTP 200; sahte .invalid adresiyle kayıt ve tekilleştirme doğrulandı, yetkisiz dışa aktarım HTTP 401 döndü, test kaydı yönetim ucuyla silindi ve silme doğrulandı. Gerçek e-posta gönderilmedi. Kayıt: `Launch-Waitlist-Live-Check.json`; site sürümü `62a421c7-6f66-43ae-99fa-0561bdc6d838`. Ücretli plan açılmadı.
