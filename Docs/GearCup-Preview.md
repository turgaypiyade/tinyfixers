# Gear Cup

Gear Cup artık kalıcı katılım, bölüm içi dişli kazanımı, takım sıralaması ve ödül kodunu içerir. `Preview` sınıf/nesne adları mevcut Unity referanslarını korumak için bırakılmıştır; ekran ve menü ikonu oyuncu build’lerine dahil edilir.

## Ayarlar

`Assets/_Project/Resources/Events/GearCupConfig.asset` görselleri, animasyon hızını, açılış seviyesini, puan kazanımını ve üç ödül paketini yönetir. Varsayılanlar ilk uygulama tercihleridir; oyun ekonomisi onayı değildir.

- Açılış: 51. seviye; gerçek bir takım üyeliği gerekir.
- Ortak sezon: 72 saat; başlangıçlar 1 Ocak 2024 UTC’den hesaplanır. Katılım zamanı kişisel sayaç başlatmaz.
- Puan: dört normal taş türünün her biri +1 dişli. `onlyYellowTiles` ile yalnız sarı taş seçilebilir; `gearsPerTile` katsayıdır.
- Bölüm başına sınır: 10.000 dişli.
- Kazanılan bölümün dişlileri kaydedilir; yenilgi veya vazgeçmede o denemenin dişlileri kaybedilir ve kayıp listesinde görünür. Etkinlik bitiminden sonra tamamlanan deneme puan vermez.
- Gruplar beş takımdır; boş yerler deterministik bot takımlarıyla gösterilir. Aynı sezonda takım değiştirerek yeni yarışa girilemez.
- İlk üç takım farklı ödül alır. Üye ödül alabilmek için en az bir dişli katkı yapmalıdır.
- Sonuçlar sezon bitiminden 10 dakika sonra kesinleşir. Bu süre içinde gönderilemeyen çevrimdışı puanlar kesin sıralamaya eklenmez.
- Ortak karakter döngüsü 4,333333 saniyedir; önceki 6,5 saniyelik döngüye göre %50 daha hızlıdır.

## Ekran ve görseller

Onaylanan mavi arena, platform, halka, bağımsız dişli ve modern kıyafetli tavşan/domuz kullanılır. Başlık TMP ve `TextArcEffect` ile 3° kavislidir. Bütün metinler TR/EN lokalizasyondan gelir. Üç ödül kartı ortak altın çerçeveyi kullanır; gümüş/bronz renkleri mevcut shader materyalleriyle sağlanır. Takım satırları beyazdır. Tasarım safe-area içinde eşit X/Y ölçeğini korur; uygun panel köşeleri 9-slice kullanır.

Ekranın altındaki devam yazısı mevcut `common_tap_continue` lokalizasyonunu kullanır (TR: “Devam etmek için dokun”, EN: “Tap to continue”) ve yumuşakça yanıp söner. Dokununca Gear Cup kapanır; Bridge/Tırmanış ile aynı `MainMenuLevelButtonController.StartLevel()` akışı PreLevelPopup ve can kontrolünü açar. Giriş baskılanırsa etkinlik ekranı tekrar açılır. Süresi biten etkinlikte sonraki sezona katılım bilgi düğmesinden erişilir. Yazının animasyonu mevcut Update döngüsünde CanvasRenderer opaklığıyla çalışır; ek nesne veya coroutine oluşturmaz ve ekran kapanınca opaklık sıfırlanır.

Katılım penceresi etkinliğin aynı mavi arena arka planını ve mevcut popup renk dönüşüm materyalini kullanır. Katılım başlığı `TextArcEffect` ile −10° kavislidir. Katıl butonundan `EventScreenIris` dairesel geçişi başlar. İptal katılım kaydı oluşturmaz. Katılım kabul edilince kalıcı kaydedilir; sonraki girişte doğrudan etkinlik açılır.

Ana menü badge’i `Gear Trophy Stadium Badge.png` görselidir; süre boş mavi şeritte TMP ile yazılır. Kaynak PNG değiştirilmeden projeye kopyalanmıştır; import sınırı 512, mipmap ve Read/Write kapalıdır. Bridge ve Harvest ile aynı `EventIconAnimator`, üç saniyelik beklemenin ardından 0,55 saniye büyüme/sallanma oynatır. İkon seviye kapısı nedeniyle gizliyken animatör devre dışıdır; nesne kapanınca mevcut ortak bileşen coroutine’i durdurur ve transformu sıfırlar. Sahne ve yeniden kurulum aracı aynı görsel/animasyon bağlantısını kullanır.

Bölümde temizlenen taşların konumunda, koyu maviye çevrilmiş dişlinin üzerinde beyaz `+1` etiketleri görünür. Etiketin dişli ve yazısı birlikte eşit X/Y ölçeğiyle %25 büyütülür (76 taban boyut → 95 tasarım birimi). Aynı kırılma grubunun puanı tek `+N` etiketinde toplanır; yakın etiketler de birleşir. Aynı anda en fazla üç etiket görünür; kapasite dolunca yeni kazanç en yakın etikete eklenir. Bu yalnız görsel birleştirmedir, gerçek kazanım değişmez. Tahta altında toplam puan sayacı yoktur. `gainLabelMaterial`, altın dişliyi mevcut renk dönüşüm shader’ıyla maviye çeviren ortak materyaldir; kaynak PNG değişmez.

## Firestore

Mevcut Firebase SDK, kullanıcı kimliği ve takım üyeliği kullanılır. Yeni Firebase projesi veya ek SDK kurulumu gerekmez. Bu çalışma canlı veritabanına kuralları yayımlamadı.

`firestore.rules` içindeki `gearCupSeasons/{season}` kuralları mevcut kurallara eklendi. Canlı senkronizasyonun çalışması için bu kuralların Firebase Console’dan yayımlanması gerekir. Mevcut diğer kurallar korunmalıdır.

Sezon altında `teams`, beşli `groups/.../slots`, `players`, bölüm başına benzersiz `wins` makbuzları ve kullanıcı başına `claims` tutulur. Puan gönderimi makbuz, oyuncu, takım ve grup toplamını tek transaction ile günceller; aynı makbuzun yeniden gönderilmesi tekrar puan vermez. Başlangıçta, kayıt geri yüklemede ve yeni sezonda üyelik/kişisel toplam sunucudan doğrulanır. Sıralama önbelleği kayıt nesnesine bağlıdır; eski sezon satırları yeni sezonda kullanılmaz.

Ödül/limit ayarları değiştirilirse isteğe bağlı `config/gearcup` sunucu belgesi de eşleştirilmelidir. Kuralların varsayılanları:

- `rewardCoins`: `[1000, 500, 250]`
- `rewardItems`: `[11, 10, 20]` (mevcut `DailySlotRewardType` değerleri)
- `rewardAmounts`: `[2, 1, 1]`
- `settlementMinutes`: `10`
- `maxGearsPerLevel`: `10000`

İstemci ödül ayarları sunucu kurallarıyla uyuşmazsa ödül isteği reddedilir. Sonuç makbuzu sunucuda bir kez oluşturulur; yerel teslimat coin ve eşya için ayrı checkpoint tutar. Eski kayıt geri yüklemesinde oturumda bilinen teslimat bayrakları korunur. Çevrimdışı kazançlar yerel gönderim kuyruğunda bekler; hesap/sezon değişmişken tamamlanan eski ağ çağrıları uygulanmaz.

## Unity’de kontrol

1. `MainMenu.unity` sahnesini aç; scriptlerin derlenmesini bekle.
2. Mevcut sahne yerleşimi günceldir. Ekranı yeniden oluşturmak gerekirse Play kapalıyken **TinyFixers → Gear Cup → 1 - Ekranı Kur** seç; bu işlem elle yapılmış Gear Cup yerleşim değişikliklerini sıfırlar.
3. 51+ seviyeli ve takıma üye hesapla etkinlik ikonunu aç. Önce iptal edip kalıcı katılım olmadığını, sonra katılıp dairesel geçişi ve karakter hareketlerini kontrol et.
4. Bölümde normal taş kır; mavi dişli etiketlerinin konumunu ve kazançların birleşmesini kontrol et. Kazanıp kişisel/takım toplamını, kaybedip kayıp listesini kontrol et.
5. Uygulamayı yeniden aç; katkı ve katılım korunmalı. İnterneti kesip bir bölüm kazan, süre dolmadan bağlan; puan bir kez gönderilmeli.
6. Firestore kuralları yayımlandıktan sonra iki gerçek takım üyesiyle ortak takım puanını ve üye katkılarını kontrol et. Eksik rakipler botla doldurulur.
7. Sonuç/ödül akışı ve sonraki sezon geçişi ayrıca kontrol edilmelidir. Üretim sezonunu kısaltıp mevcut oyuncularla aynı sezon anahtarını paylaşma.

## Performans ve doğrulama durumu

Dişli etiketleri mevcut `UiVfxPool` ile önceden hazırlanan üç nesneyi tekrar kullanır. Dört taş türü için sabit konum toplamları ve tek efekt güncelleme döngüsü vardır. Taş başına Instantiate/Destroy, coroutine, disk/ağ yazımı veya tahta taraması eklenmez. Oyun sahnesi kapanınca abonelikler kaldırılır ve etiketler havuza döner. Kayıt ve ağ yazımı kazanılan bölüm başına yapılır; ağ hataları artan beklemeyle yeniden denenir. Etkinlik ekranı görünürken sıralama en sık 30 saniyede bir alınır. Karakterler ayrı alt Canvas’ta ortak döngü kullanır; kapalı ekran hareketleri durur.

Unity build’i, Play Mode veya cihaz testi çalıştırılmadı. Önceki kaynak kontrolünde Unity/Firebase/TMP API çözümlemesi geçti; son senkronizasyon değişiklikleri kaynak üzerinden gözden geçirildi. Firestore kurallarının emülatör testi tamamlanmadı. Gereksiz indirilen test araçları/emülatör ve bunların npm önbelleği temizlendi. Gerçek cihaz performansı ve sunucu davranışı kullanıcı tarafından uygulamada doğrulanacak; ölçülmüş performans kazanımı iddia edilmez.

Görsel dökümü: [GearCup-Assets.md](GearCup-Assets.md).
