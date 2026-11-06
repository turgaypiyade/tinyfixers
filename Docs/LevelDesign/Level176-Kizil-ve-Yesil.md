# 176 — Kızıl ve Yeşil / Crimson and Green

2026-11-06 revizyonu. Aktif V1 katalogdaki `LevelCL_0176`, `LevelV1_00001760.asset` ve GUID `f6068583d99e4456b6e205a8bf6f553b` korunur. Tarihsel `Level176-Dolapli-Gecit.*` farklı katalog numaralandırmasına aittir; bu revizyonun kaynağı değildir.

Kullanıcı eski yerleşimi reddetti: “bu bizim kuralımıza uymuyor ortaya saç sapan bir kutu koymak doğru değil”. Eski 7×7 tam tahtada iki beşli renkli sandık şeridi normal taşlar arasında asılı duruyordu. Yeni yerleşimde maskenin daralan alt kenarıyla aynı V şeklini tamamlayan tek, kesintisiz kırmızı/yeşil sandık konturu var.

**Yeni:** 8×8, 54 aktif hücre, 44 başlangıç normal taşı yuvası. **Korunan:** Normal seviye türü, 26 hamle, 5 kırmızı ColorCrate72 + 5 yeşil ColorCrate74 hedefi, sandık başına üç vuruş, dört normal renk, isimler, müzik ve diğer ayarlar. Çim, hazır special, yardımcı veya yeni aile eklenmedi.

176 aktif V1’de kırmızı/yeşil ColorCrate’ın ilk tanıtımıdır; sonu6 olan rahat tanıtım konumu korunur. Eski seviyeyi Hard’a çevirmek için hedef/hamle değiştirilmedi. Bu, sonraki yeni tasarımlarda ColorCrate dinlendirme tercihinin kaldırılması değildir; mevcut tanıtım seviyesinin iki ailesi korunur.

İlk karar: kırmızı veya yeşil eşleşmeyle ilgili kenarı açmak. Üst avlu geniştir; daralan alt dirsekler açılınca daha içteki kutuların normal eşleşmeye açık yüzleri artar. Special iki renkte de kullanılabilir. Ortadaki iki alt kutu da üstlerindeki normal taş sütunlarından erişilebilir; bütün sandıkların ilk hamlede her yüzünün açık olması beklenmez.

Toplam zorunlu iş aynı 30 kabul edilen sandık isabetidir; bu sayı 30 oyuncu hamlesi anlamına gelmez. Tek bir satır/sütun vuruşu iki sıranın tamamını tek vuruşta bitirmez; sandıklar üç aşamalıdır. Hazır global yardımcı zinciri verilmedi. 26 hamle korunmuş ilk deneme ayarıdır, ölçülmüş kazanma oranı değildir.

Statik dolum: açılış44/44, yalnız kırmızı kalkınca49/49, yalnız yeşil kalkınca49/49, tüm sandıklar kalkınca54/54 normal yuva beslenir. Başlangıç pini ve boş cep yok. Mevcut maximum-stage bayrakları ve çapraz köşe kuralları incelendi; oyun veya kaskat simülasyonu çalıştırılmadı.

175 önceki aktif katalog kaydı şekil tekrarına karşı tarandı. Küçültülmüş engel kütlesinde2’nin alt çanak kütlesi benzer;176’daki fark yeni iki renk kuralı ve maskeyi tamamlayan ince kenardır.178’in dört renkli kapalı çerçevesi tanıtıma taşınmadı. Geometri skorları yenilik veya denge yüzdesi sayılmaz. Gerçek sprite önce/sonra ve2/176/178 karşılaştırmaları açılarak incelendi; normal renkler temsilidir.

Yalnız176assetinin boyut ve hücre dizileri değişti. Diğer6107 mevcutAssets dosyası; katalog, TR/EN isimler, meta/GUID,232/233, kullanıcının108 değişikliği ve önceki ses/runtime çalışmaları aynı. Mevcut LevelEditor renkli sandık paleti, boyama/silme/resize ve Goals/PreLevel sprite çözümü kullanılır; runtime/editor kodu değiştirilmedi. Yeni Update, tarama, nesne havuzu veya ses yolu eklenmedi; cihaz performansı ölçülmedi.

Manuel test kullanıcıdadır. UnityUI, Play, build, bot/kaskat simülasyonu veya yayın yapılmadı. Yeni düzen henüz kullanıcı onaylı değildir.

[Yeni önizleme](Level176-Kizil-ve-Yesil.png) · [Önce/sonra](Batches/176-Contour-Revision/Before-After.png) · [Doğrulama](Batches/176-Contour-Revision/validation.json)
