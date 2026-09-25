# Ortak popup

`Assets/_Project/Resources/CommonPopupSkin.asset` bütün yardımcı popup'ların görsel ayarıdır.
Inspector'da `background`, `continueButton`, `closeButton`, font ve metin rengi değiştirilebilir.
Başlangıçta Chapter001/NewUI/LevelEndPopup.png ve LevelEndPopup prefab'ındaki yeşil
BtnContinue görseli kullanılır. Ortadaki krem çerçeve içerik alanıdır; kaldırılması gerekmez.
PNG aynı dosyada güncellenirse tüm bu popup'lar yeni görseli kullanır. Yeni görselin
yerleşimi farklıysa `panelSize`, `titleRegion`, `bodyRegion` ve `actionsRegion`
Inspector'dan ayarlanabilir. Bölgeler sol alttan başlayan 0–1 koordinatlarıdır.

Başlık fontu `titleFontSize` (80), yay açısı `titleArcDegrees` (15°), alt aksiyon
fontu `actionFontSize` (56), kapatma butonunun konumu `closeRegion` üzerinden
ayarlanır. Uzun metinler alanına sığacak kadar küçülür. Başlıklardaki `TextArcEffect`
derece modunda satır genişliğinden bağımsız aynı toplam yay açısını korur;
diğer ekranlardaki mevcut yükseklik tabanlı arc ayarları değişmez.
Alt butonların alanları büyütülmüştür; GreenButonEfso görselinin 289:116 oranı ve
yazının görsel içinde kalması korunur.

Save Progress giriş seçenekleri `accountButton` görselini kullanır (BlueButton,
770:172). Butonlar içerik genişliğini doldurur; sağlayıcı adı 48 punto,
"ile devam et"/"Yakında" alt satırı 26 puntodur. Hesap çakışmasında sağlayıcı
listesinin yerine geniş onay/vazgeç butonları gelir; Vazgeç listeyi geri getirir.

Ortak görünümü kullanan akışlar:

- Arkadaş bul, sonuç/ekle, ID kopyala, davet et.
- Takım bilgisi/katıl ve takımdan ayrılma onayı.
- Ayarlar → ilerlemeyi kaydet; Facebook, Google ve Apple hesap bağlama; hesap çakışması onayı.
- Ayarlar → Instagram bağlantısını açma onayı (Instagram hesap bağlama sağlayıcısı değildir).
- Profil → müzik seçimi.
- Can satın alma ve bölüm sonundaki reklam izle/satın al seçimleri.

`RuntimeChoicePopup.Show(title, message, choices)` mevcut kullanımını korur.
İlk/ana aksiyon `BtnContinue`, metni `Choice.Label` üzerinden gelir. Formların alt
butonu da `BtnContinue` adını taşır; Davet Et/Katıl/Tamam metinleri ilgili controller'dan gelir.
Arkadaş ve takım popup'larında mevcut sahne alanları ortak kabuğa taşınır;
Inspector'daki arama, oyuncu, katılma ve davet referanslarını tekrar bağlamak gerekmez.
Bölüm sonu, bölüm öncesi ve etkinliklere özel oyun ekranlarının kendi düzenleri devam eder.

Play Mode kontrolü:

- Arkadaş aramasında bulunan/bulunamayan sonuç; ekleme, ID kopyalama, davet ve kapatma.
- Takım bilgisi, dolu takım/bölüm sınırı, katılma; ayrılma onayında Vazgeç ve Ayrıl.
- Kaydetme ekranını tekrar açma, sağlayıcıların mevcut/Yakında durumu, hesap çakışması.
- Instagram'da Vazgeç'in bağlantı/ödül işlemini çalıştırmaması.
- Müzik listesini kaydırma, seçme, kilitli parça ve yetersiz altın geri bildirimi.
- Reklam, satın al, kapat ve can satın alma callback'leri; popup açıkken arkaya tıklamanın engellenmesi.
- Dar telefon/tablet oranlarında başlık, krem içerik alanı ve alt butonların yerleşimi.

Bu değişiklikte build çalıştırılmadı. Asset GUID'leri, sprite import modları,
1/2/3 aksiyonun yerleşim sınırları ve değiştirilen dosyaların diff kontrolü doğrulandı.

Boş popup düzeltmesi: Unity'nin Rect alanlarını okuyabilmesi için her bölgenin
YAML kaydına `serializedVersion: 2` eklendi. Önceki kayıtlarda Inspector tüm
bölgeleri sıfır boyutla okuyordu. `CommonPopupSkin` yüklenirken ve Inspector'da
doğrulanırken geçersiz bölgeleri varsayılan değerlerle onarır.
Play Mode'da sahnedeki arkadaş popup'ının geçici kopyasıyla başlık, ID input'u,
Ara/Kopyala/Davet Et kontrollerinin görünürlüğü doğrulandı; metin mesh'leri ve
içerik/input alanlarının sıfırdan büyük boyutları kontrol edildi. Test sırasında
arama, giriş veya satın alma callback'leri çalıştırılmadı.
