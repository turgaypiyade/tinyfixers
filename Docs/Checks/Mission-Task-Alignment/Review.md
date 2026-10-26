# Missions — bütün görsellerde kademeli açılış

## Kullanıcının açık geri bildirimi

- "Pissa kulesinin olduğu resimde önce tamamını açıp sadece kuleyle uğraşıyor; bu güzel bir görsel değil."
- "Tümünde lütfen böyle sorun varsa bak istedim ben aslında."

Önceki inceleme görev adlarını ve yapı parçalarını eşleştirmiş, ilk/son görevde çevrenin büyük bölümünün açılmasını korumuştu. Bu davranış kullanıcı tarafından reddedildi. Aşağıdaki değişiklikler açılma akışını sekizinin tamamında düzeltir; yalnızca açıklama değişikliği değildir.

## Yapılan davranış değişikliği

`regions: []` artık bütün kalan resmin sahibi olmaz. Kaynak/temizleme/boyama işlemi yalnız tanımlı hedef parçaya uygulanır. Çevre, sekiz adım boyunca ayrı bir renk geçişiyle yumuşakça geri gelir; ilk adımda tamamen normale dönmez ve son göreve kadar bütünüyle kapalı kalmaz. Gökyüzü rastgele renkli parçalara ayrılmaz.

Pisa, piramit, Sed ve Rodos'un ilk temizlik adımlarına açık, sınırlı zemin/çalışma alanı hedefleri verildi. Köprü, Eyfel ve Ayasofya'nın son temizliği zemindeki küçük hedef alanlarına bağlandı. Sed'in ikinci adımında temel hedefi daraltıldı; bütün alt zemini ve eşya grubunu tek seferde açmaz. Kanyonun ilk patika temizliği de küçültüldü. Kanyonun son manzara adımı kalan çevre rengini tamamlar ve kısa ortam aydınlığı verir; geniş bir manzara dikdörtgenini son ana kadar kahverengi tutmaz.

Kaynak sırasında çevre rengi kesim ve tabaka kıvrılması boyunca ilerler. Boyama/temizleme/parlatmada aynı geçiş adım animasyonu boyunca sürer. İptal/kapanma, kısmi çevre geçişini kaydedilmiş aşamaya geri alır. Kıvrılan tabaka çevre piksellerini beraberinde taşımaz.

Görev sırası/sayısı, yıldız maliyetleri ve kayıt anahtarları korunur. Hiçbir resim yeni bir çalışma maskesi veya piksel üretimiyle yeniden çizilmedi; mevcut sanat kullanılır.

## Sekiz görselin önce/sonra kontrolü

"Eski en büyük adım" değeri, önceki tanımda tek görevde tamamen renklendirilen alanın yaklaşık oranıdır. Yeni düzende çevrenin kademeli renk harmanı ile hedef parçanın tam açılması ayrı işlerdir; aynı yüzdeyi yeni tam-açık alan gibi okumamak gerekir.

| Görsel | Eski en büyük adım | İlk adım karşılaştırması | Bütün aşamalar |
|---|---:|---|---|
| Eğik Pati Kulesi | %79.8 | [Eski / yeni 1. adım](mis1-first-step.jpg) | [0–8 adım](mis1-progression.jpg) |
| Altın Kuyruk Köprüsü | %72.3 | [Eski / yeni 1. adım](mis2-first-step.jpg) | [0–8 adım](mis2-progression.jpg) |
| Eyfil Kulesi | %80.2 | [Eski / yeni 1. adım](mis3-first-step.jpg) | [0–8 adım](mis3-progression.jpg) |
| Miyav Piramidi | %84.0 | [Eski / yeni 1. adım](mis4-first-step.jpg) | [0–8 adım](mis4-progression.jpg) |
| Büyük Pati Seddi | %64.4 | [Eski / yeni 1. adım](mis5-first-step.jpg) | [0–8 adım](mis5-progression.jpg) |
| Kuyruk Kanyonu | %56.1 | [Eski / yeni 1. adım](mis6-first-step.jpg) | [0–8 adım](mis6-progression.jpg) |
| Ayasofya | %80.5 | [Eski / yeni 1. adım](mis7-first-step.jpg) | [0–8 adım](mis7-progression.jpg) |
| Rodos Devi | %75.6 | [Eski / yeni 1. adım](mis8-first-step.jpg) | [0–8 adım](mis8-progression.jpg) |

Örneğin Pisa'da eski ilk adım resmin yaklaşık %79,8'ini birden açıyordu. Yeni ilk adım resmin yaklaşık %12'sindeki ön zemin hedefini temizler. Bunun dışındaki çevre o aşamada yalnızca 1/8 oranında renk kazanmıştır; tüm çevre açılmış değildir.

Bu tabloların bütün 0–8 aşamaları görsel olarak incelendi. Görevlerin kalan dikdörtgen hedefleri yaklaşık maske tanımlarıdır; tam nesne silüeti segmentasyonu değildir. `*.jpg` dosyaları kaynak görsele göre hesaplanmış statik maske/renk önizlemeleridir; Unity ekran görüntüsü, shader derlemesi veya animasyon kaydı değildir. Önizlemenin kahverengi tonu shader'ın gürültülü patinasının yaklaşık temsilidir.

## Korunan önceki düzeltmeler

27 Türkçe/İngilizce adın resimdeki parçalara uyarlanması, ayrı minare/kule bölgelerinin kaynak rotası, yalnız açılmış yüzeylerin parlatılması ve Rodos kol/meşale ayrımı korunur. Önceki karşılaştırma verisi `Before.json`, kullanıcı geri bildirimi öncesindeki durum `Before-Progression.json`, güncel veri `After.json` dosyalarındadır. Görev bloğu dışındaki asset verileri SHA-256 ile karşılaştırıldı; maliyetler, sandıklar, kuşlar ve karakter verileri değişmedi.

## Kontroller

- `python3 -B Tools/review_wonder_tasks.py --export Docs/Checks/Mission-Task-Alignment`: 8 görsel / 64 görev; bölge sınırları, ikonlar, Türkçe/İngilizce eşleşmesi ve korunan veriler kontrol edilir; her görselin bütün aşamaları dışa aktarılır.
- `csi Tools/check_wonder_task_regions.csx`: 177.392 üretim kodu kontrolü. Bölge sahipliği, çok bölgeli kaynak yolu, çevrenin adım boyunca sürekli ilerlemesi, kapanma/iptalde kısmi geçiş temizliği ve bütün görevler tanımlıyken son görevin hedefinin korunması dahil.
- `csi Tools/check_wonder_costs.csx`: 589 maliyet kontrolü geçti.
- `csi Tools/check_obstacle_scatter.csx`: değişen C# kaynakları kurulu Unity/UI/TMP API'lerine bağlandı; assembly üretilmedi. Mevcut 1.982 hareket/temizlik kontrolü geçti.

Yeni runtime işlem, kare başına bir materyal float değerinin güncellenmesidir. Yeni obje, materyal, coroutine veya kare başına dizi tahsisi eklenmedi. Görev dışındaki çevre shader'da kademeli harmanlanır. Cihaz CPU/GPU performansı ölçülmedi.

Unity build, Play Mode, gerçek shader derlemesi ve cihazda animasyon doğrulaması yapılmadı. Gerçek pas kenarları, kaynak hareketinin okunurluğu ve zamanlama Unity'de ayrıca görülmeli.

## Journey kartları

Journey'nin eski kartı yalnız `_Reveal` değerini yazdığı için alttan yukarı hologram görünümü kullanıyordu. Kart artık `WonderRevealView.ShowRestorationSnapshot` üzerinden aynı görev bölgelerini, paslı görünümü, çevre rengini ve `WonderProgress.RevealNormalized` ilerlemesini uygular. Görev animasyonu oynatılmaz ve ilerleme kaydı yazılmaz. Her kartın materyalini görünüm bileşeni sahiplenip kart yok edilirken temizler; liste yenilenirken eski kartlar aynı anda görünmesin diye önce kapatılır. Başlangıç, kısmi ve tamamlanmış görüntü aynı Missions verisinden gelir. C# API/kaynak kontrolleri geçti; Journey'nin canlı Unity görüntüsü doğrulanmadı.
