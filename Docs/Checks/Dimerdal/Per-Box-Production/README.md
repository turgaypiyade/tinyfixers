# Dimerdal — kutu başına üretim adedi · 2026-11-05

Kullanıcı her kutuyu yerleştirirken kendi adedini girmek istiyor: 8+8 veya 6+10 gibi. Goals toplamı bu sayılardan hesaplanır. Bu yön, önceki ortak üretim kotası ve bütün kutuların birlikte kırılması davranışının yerini alır.

## Editörde kullanım

1. Obstacle veya Overlay modunda Dimerdal rengini/boyutunu seç.
2. **Üreteceği elmas** alanına adedi yazıp kutuyu yerleştir. Yeni kutular için başlangıç değeri 6'dır.
3. **Tahtadaki Dimerdal kutuları** listesinden her kutunun adedini sonradan ayrı değiştirebilirsin. Koordinatı ve türü listede görünür; taban kutularının grid önizlemesinde sayı rozeti de vardır.
4. Goals içindeki mor **Collectible → Diamond** hedefinin ikonu ve Amount alanı otomatik yönetilir. Toplam, kutuların üretim adetleri ve varsa başlangıçta yerleştirilmiş elmaslardır. Elmas sayacı ürünler toplandığında azalır.

Silme, üstüne başka engel koyma ve maske değişiminde eski kayıtlar temizlenir; bir kutunun origin dışındaki hücresini silmek bütün kutu footprint'ini kaldırır. Örtü altındaki ve üst katman olarak yerleştirilmiş kutular da toplamda birer kez sayılır. Editor değişiklikleri mevcut Undo akışını kullanır; sayı düzenlemesi Undo.RecordObject ile kaydedilir. Unity GUI/Undo tıklamaları ayrıca çalıştırılmadı.

## Veri ve runtime

`LevelData.dimerdals` içindeki `[Serializable] DimerdalEntry` kutuyu `originCellIndex + obstacleId` ile tanımlar ve `count` tutar. Editör doğrulaması ve runtime klonu, katmanlar stamp edilmeden önce kaynakları/toplam hedefi eşitler. Eski veride hiç kutu sayısı yoksa mevcut hedef bir kez eşit dağıtılarak korunur. Normalleştirme oynanan board üzerinde çağrılmaz.

Her kayıtlı runtime kutunun ayrı limiti, ayrılmış üretim hakkı, eşleşme rezervasyonu ve uçuş adedi vardır. Kutunun son ürünü başarıyla yerleşip o kutunun bekleyen işi kalmayınca yalnız o kutu mevcut büyük-parça efektiyle kırılır. Diğer renk/konumdaki kutu kendi hakkını kullanmaya devam eder. Hedef seçicinin uygunluk sorgusu da kutu origin'ine göre yapılır; başka bir kutu açık diye tükenmiş kutuya yönelmez.

Başarısız/timeout uçuş hakkı kendi kutusuna döndürür. İptal edilmiş eşleşme ve disable/re-enable rezervasyonları geri bırakır; eski epoch batch'i yeni üretim yapamaz. Örtülü bitmiş kaynak açılınca kırılır. Aynı origin'de ortaya çıkan alt kaynak kendi ayarıyla başlar. Uçuş tamamlanırken yeniden iptal edilmesi eski tick'i durdurur; pool/iş temizliği finally ile yapılır. Yeni ses yolu veya düşüş motoru değişikliği yoktur.

## Mevcut seviyeler

Aktif V1 katalogdan çözülen dört seviyeye yalnız yeni `dimerdals` listesi eklendi:

| Level | Kutu üretim adetleri | Ortak hedef |
| --- | --- | --- |
| 221 | 4 + 4 | 8 |
| 222 | 10 | 10 |
| 224 | 6 + 6 | 12 |
| 225 | 8 + 8 | 16 |

Yerleşimler, hamleler, diğer hedefler, mor hedef ikonu, meta/GUID, isim ve katalog değişmedi. 223 dahil diğer seviyeler korundu. Kullanıcının önceki Level11 değişikliği bu turun koruma snapshot'ına dahildir.

## Kontrol ve performans sınırları

- `csi Tools/check_dimerdal_per_box.csx`: 289 gerçek yöntem kontrolü. 6+10 ve 8+8, üç kutu boyutu, otomatik/tekrarsız Goals, eski veriyi taşıma, örtü/initial ürünler, silme/footprint, origin bazında hedef uygunluğu, eşleşme rezervasyonu, timeout, disable/re-enable ve uçuş varışında yeniden iptal kontrol edildi. Önce mevcut 409 üretim/yerleşim/lifecycle kontrolü de geçer.
- `csi Tools/check_dimerdal_matching.csx`: mevcut 409 ve 39 gerçek MatchFinder/special/toplama kontrolü geçti.
- `csi Tools/check_dimerdal_collection.csx`: gerçek ortak TarpStatue/AncientSeal toplama rutini ve Dimerdal sürücüsü birlikte geçti.
- `csi Tools/check_dimerdal_shatter.csx`: güncellenmiş 64 feedback/bağımsız kırılma/örtü kontrolü, 7 gerçek katman temizleme/yeniden kayıt kontrolü ve mevcut havuzlu shatter normal bitiş/Dispose/tekrar kullanım kontrolleri geçti.
- `python3 Docs/LevelDesign/Batches/222-225-Crystal-Shapes/verify.py`: dört assette yalnız yeni sayaç listesi, hedef toplamı ve YAML doğrulaması; diğer 3032 asset/kod dosyası hash ile korunur.

Testler gerçek C# yöntemlerini Unity doubles ile çalıştırır; tam Unity derlemesi değildir. Hazırlık taraması yalnız authoring/klon başında, kutu kaydı ve olay sonrası emeklilik kontrolü gerektiğinde yapılır. Her kare yeni board taraması, per-hit delegate/closure/GameObject veya yeni efekt sistemi eklenmedi; mevcut uçuş ve kırılma havuzları kullanılır. Tek completion delegate'i servis başına tekrar kullanılır. Inspector'da boş repaint sırasında board normalleştirmesi yapılmaz. CPU/GC/FPS ölçülmedi.

Unity build/Play, cihaz testi, gameplay/gravity/cascade simülasyonu, commit, yayın veya release baseline yapılmadı. Mevcut oynanış dengesi yeniden ölçülmedi.
