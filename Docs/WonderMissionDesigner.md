# Mission Designer

Unity menüsü: **TinyFixers → Wonders → Mission Designer**. Windows ve macOS Unity Editor içinde çalışır.

Araç kodları `Assets/_Project/Editor/WonderMissionDesignerWindow.cs` ve `WonderMissionAuthoring.cs` içindedir. Ayrıca `UNITY_EDITOR` ile korunur. Mobil oyuncu paketine araç penceresi, dosya işlemleri veya yerelleştirme düzenleyicisi girmez. Aracın ürettiği WonderDefinition, sprite ve çeviriler oyunun kullanacağı normal içeriktir.

## Mevcut harikayı düzenleme

1. Üstteki alandan bir WonderDefinition seçin. Project'te seçiliyse **Seçileni Aç** da kullanılabilir.
2. Soldan görevi seçin. TR/EN adını, ikonunu, açılış efektini ve maliyet ağırlığını düzenleyin.
3. **Bölge çiz** açıkken resim üzerinde sürükleyin. Bir göreve birden fazla bölge eklenebilir. Ayrı minareleri veya köprü ayaklarını ayrı bölgeler olarak bırakın.
4. Çizim modunu kapatıp bir bölgeyi taşıyın. Seçili bölgenin sağ-alt beyaz tutamacı boyutlandırır. Sayısal Rect alanları normalize ve sol-alt başlangıçlıdır. **×** veya Delete bölgeyi siler; Cmd/Ctrl+Z taslak düzenlemelerini geri alır.
5. **Tamamlanan adım** kaydırıcısıyla başlangıcı, her görevin sonunu ve tamamlanmış resmi değerlendirin. **Orijinal** karşılaştırma içindir. **Sınırlar** kapatılınca açılışın temiz görüntüsü görünür.
6. **Görev efektini izle** seçili görevin shader efektini oynatır; Play Mode veya oyuncu ilerlemesini kullanmaz. Kaynakçı robotu, kıvrılıp düşen tabaka ve ortam karakterleri bu pencerede kurulmaz; bunlar oyun sahnesinde kontrol edilir.
7. **Kaydet**, mevcut harika dosyasını GUID'sini koruyarak, ilgili TR/EN anahtarlarını ve değiştirdiğiniz katalog toplamlarını kaydeder. Karakter yolları/kareleri, ödüller ve diğer harika ayarları taslaktan korunur. Diğer çeviriler ve JSON alanları korunur. Kayıt öncesi düzenlemeler kaynak asset'e uygulanmaz; resim içe aktarmak seçtiğiniz yeni dosyayı hemen projeye kopyalar.

Kaydedilmiş JSON dosyası Unity Undo kapsamına girmez. Kaydettikten sonra taslağı geri alırsanız TR/EN dosyasına da uygulamak için yeniden **Kaydet** kullanın. Görev ekleme/silme/sıralama mevcut oyuncuların adım indekslerinin anlamını değiştirir; yayımlanmış içerikte bunu dikkate alın.

## Resim ve yeni harika

**Yeni Harika** ile başlayın; benzersiz bir kimlik ve TR/EN harika adlarını girin. Projedeki bir sprite'ı atayabilir veya **Dışarıdan resim içe aktar…** ile PNG/JPG dosyasını yeni bir proje yoluna kopyalayabilirsiniz. Orijinal dosya değiştirilmez; mevcut bir proje resminin üzerine yazılmaz.

Görevleri ekleyip bölgelerini resimde çizin, adlarını ve efektlerini belirleyin. Araç resmi otomatik yorumlamaz; eşleştirmeyi siz yaparsınız. **Farklı Kaydet** mevcut harikadan kopya oluşturur; dosya adı `Wonder_mis9.asset` ise yeni kimlik `mis9` olur ve çeviriler bağımsız anahtarlara yazılır. Kaydettikten sonra katalogda olmayan harika için **Kaydet ve bu harikayı kataloğa ekle** düğmesi görünür. Bu işlem dosya listesinin sonuna ekler; ülkeye göre oyuncu sırası runtime tarafından belirlenir.

## Alan ve efekt kuralları

- İç içe bölgelerde en küçük fiziksel dikdörtgen kazanır. Aynı büyüklükte çakışmada önceki görev önceliklidir.
- **Açık yüzeyde parlama / Shine** alan sahiplenmez; önceki adımlarda açılmış yüzeylerin üstünden geçer. **Parlat / Polish** kendi alanını açar.
- Hiçbir göreve atanmış olmayan çevre, tüm adımlara yayılmış şekilde renk kazanır. Bölgesiz Işık görevi çevre için kullanılabilir; bölgesiz onarım yeni fiziksel alan açmaz.
- Shader en fazla 24 fiziksel bölge taşır. Araç bu sınırın aşılmasını, geçersiz koordinatları ve tamamen bölgesiz bir harikayı kaydetmez.
- Hedef alan yüzdeleri geometrik örneklemedir; shader kenarlarda küçük düzensizlikler uygular. Büyük tek adım açılışları ve görünür yeni alanı olmayan görevler uyarılır.

## Maliyetler

Görev ağırlığı gerçek yıldız bedeli değildir. Katalogdaki toplam oyuncunun sırasına bağlıdır: mevcut ayarda ilk harika 60, ikinci 75, sonraki sıralar son harikada 110'a ulaşır. Ağırlıklar bu toplamın görevlere dağılımını belirler. Görevler artan maliyet sırasında olmak zorunda değildir.

Araç her sıranın gerçek görev maliyetlerini oyundaki kümülatif yuvarlama hesabıyla gösterir. Toplam tam korunur; sıfır maliyetli görev üreten dağılım kaydedilmez. Katalog toplamlarını düzenlemek tüm harikaları etkiler. Kataloğa yeni harika eklemek de ara sıraların toplamlarını yeniden dağıtır.

## Doğrulama

`csi Tools/check_wonder_mission_designer.csx` kaynakları kurulu Unity Editor API'lerine karşı assembly üretmeden kontrol eder; `UNITY_EDITOR` olmadan araç tiplerinin kalmadığını doğrular. Maliyetleri gerçek WonderCatalog metoduyla rastgele dağılımlarda karşılaştırır; bölge sahipliği, doğrulama ve TR/EN birleştirme davranışlarını da çalıştırır. Bu kontrol Unity penceresinin görsel testi veya mobil build değildir.
