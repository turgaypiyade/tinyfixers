# Tasarım geri bildirimi

Kanıt, yorum ve oyun ölçümü ayrı tutulur. Açık kullanıcı onayı olmadan bir tasarım onaylı sayılmaz. Mevcut ilkelerin ayrıntıları Design-Principles.md içindedir.

| Kayıt | Kullanıcı kanıtı / durum | Çıkarılan ders | Sınır |
| --- | --- | --- | --- |
| 171 Restorasyon Avlusu | Reddedildi: “daha yaratıcı olmanı bekliyordum”, yeni renkli sandıklar varken “çok sıradan” | Güncel engel havuzunu kontrol et; eski sandık/çamur/fener/heykel kanatlarına dönüp durma | Her levelde en yeni engel zorunlu değil |
| 171 Renk Çarkı | Görsel düzeltme: sandıkların “ortada adacık gibi” durması hoş değil | Renk eklemek tek başına kompozisyonu düzeltmez; kontur/sütun/şekil bütünlüğü gerekir | Tüm board'lar çerçeve olmak zorunda değil |
| 171 Renkli Çerçeve | **Onaylı:** “171 oldu bence onu kullanabiliriz” | Hole etrafında tamamlanan kesintisiz kontur ve geniş oyun alanı kabul edildi | Görsel/kullanım onayı; %40–50 geçiş ölçümü değil. İstek olmadan değiştirme |
| Beş dış oyun görseli | Tasarım yönünü geliştirmek için verildi | Görsel ritim, yoğun desen, sütunlar, negatif alan ve işlevsel kenarlar; bkz. References/Index.md | Başka oyundaki engellerin kuralları bilinmiyor; referanslar yeni level talebi değil |
| 172 isteği | “sadece tahta kullanmayalım elimizde çok fazla obstacle var” | 172'de sandık ağırlığından uzaklaş; farklı mekanikleri bir bütün içinde kullan | Çok engel türü eklemek veya aynı U çerçeveyi kopyalamak tek başına yenilik değil |

Yeni geri bildirimde ilgili satırı güncelle veya somut bir örnek ekle. Öz değerlendirme skorlarını kullanıcı beğenisi gibi sunma. Gerçek kazanma oranını ancak oynanış verisi desteklediğinde kaydet.

## Kaynak önceliği ve düzeltme

Kullanıcı son açıklama: “sadece verdiklerimi öğrenmen için veriyorum”, “170'e kadarki levellar görüntü olarak güzel ... oradan da öğren”. Yeni dış referans gelmesi, aktif leveli o örneğe göre yeniden yapma talebi değildir. 1–170 görsel havuzu ana yerel referanstır; dış görseller bunu genişletir. Görsel olarak beğenilme, oynanış denge ölçümü değildir.

170 öncesi havuzdan 125, 135, 145, 147, 155, 163, 167 ve 168'in mevcut PNG'leri toplu incelendi; 135, 145 ve 155'in tasarım notları ayrıca okundu. Bütün 170 levelin tek tek detaylı incelemesi yapılmış gibi sunulmaz.

| Yerel örnek | İşe yarayan karar | Yeni tasarıma aktarılacak ders |
| --- | --- | --- |
| [135 Bölmeli Pencere](Level135.md) | Hole satırı/sütunu dört odanın sınırını kuruyor; ana dolum kenara yapışık; iki sandık grubu çapraz gradyan | Bölme ve erişim kuralı aynı hikâyeyi anlatsın; sırf dolum kolay olsun diye deseni ortadan bölme |
| [145 Kanal Adaları](Level145-Kanal-Adalari.md) | Dar merkez kanalın çevresinde kasıtlı, hizalı special hedef adaları | Adalar evrensel olarak yasak değil: açıklanabilir maske, ritim ve mekanik ilişki varsa geçerli. Reddedilen 171'de normal taş içinde kalan açıklamasız sandık bloklarıydı |
| [147 Su Feneri](Level147-Su-Feneri.md) | Değişik boyuttaki engeller tanınan tek siluete yerleşiyor | Engel çeşitliliği bir form ve rol hiyerarşisi içinde çalışsın |
| [155 Nilüfer Tacı](Level155-Nilufer-Taci.md) | Sıvı tabanı/katı örtü, simetrik taç ve daralan alt şekil birlikte tasarlanmış | Hole, renk ve katman birlikte düşünülsün; aynı nilüfer sütunu tekrarlanmasın |
| [163 Renkli Onarım](Level163-Renkli-Onarim.md) | Geniş merkez ve kenarları tamamlayan büyük engeller; ön-arka katman ayrımı | Açık alan da güçlü kompozisyon kurabilir; hole zorunlu değil |
| [167 Lazerli Restorasyon](Level167-Lazerli-Restorasyon.md) | Sıkışık engel grubu ile sağ dolum alanı bilinçli ayrılmış; özel mekanik bir arada | Her level için aynı eşleşme alanı oranını dayatma; akış ve erişimi seçilen konsepte göre incele |
| [168 Zincir Atölyesi](Level168-Zincir-Atolyesi.md) | Kenarlara bağlanan katmanlı kanatlar ve ortak yardımcı bölgesi | Eski engeller kullanılabilir; aynı kanat tarifini art arda yeniden üretme |

Gelecek öğrenme döngüsü: alakalı 2–3 yerel örneği seç → yeni kompozisyonu kur → gerçek sprite'larla görsel kontrol → açık kullanıcı geri bildirimini ve gerekçeyi kaydet → sonraki işe kısa çalışma durumundan devam et. Bu prosedür tutarlılığı artırmayı amaçlar; her gün otomatik daha iyi sonuç garantisi veya model ağırlığı eğitimi iddiası değildir.

## 173–180 ve isim kaydı

- Kullanıcı: “biraz olayı yakaladık ... 180'e kadar git ... olmadı geri alırız”. Bu, tasarım yönüne olumlu geri bildirim ve sekiz taslak için yetkidir; her taslağın görsel/oyun onayı değildir. İnceleme ve geri alma kayıtları ayrı tutulur.
- Kullanıcı: “her level için isimleri de tutalım ... bunları kullanacağız”, ardından “isim olmayanlara da isim ver”. 1–180 isimleri numara ve gerçek levelKey ile tek JSON'da tutulur. Mevcut 61 isim korunur, eksik 119 isim eklenir; yeni tasarımlarda isim kaydetmek workflow'un parçasıdır.

## 181 yönlendirmesi — 2026-10-20

- **Açık kullanıcı tercihi:** “şimdilik renkli sandıkları artık kullanmayalım”, “geçmişteki color chest wardrobe ve katmanlı yapı ... tekrara mı girmeye başladık”. Yeni ColorCrate 71–74 geçici olarak seçimden çıkarılır. Eski ColorChest 21 ile Wardrobe 27 yasaklanmadı; kullanıcı bunları yeniden değerlendirmeyi istedi. 171–180 otomatik değiştirilmez.
- **Tasarım değerlendirmesi, kullanıcı onayı değil:** Son grupta çerçeve/iki yan kanat ve renkli sandık örtüsü tekrar riski taşıyor. Engel değişimi kadar geometri, çalışma alanı yönü ve açılma sırası da değişmeli. 181 için bağlı, tek yana yaslanan basamak seçildi; dolapların arkasında renk sandıkları, sağ alt dolap altında taş ve çamur var.
- **Sınır:** Her yeni levelin asimetrik, dolaplı veya çamur katmanlı olması gerekmiyor. 181 henüz inceleme taslağı; beğeni ve oynanış dengesi onaylanmış sayılmaz.

### 181 Basamaklı Arşiv — reddedildi

Kullanıcı: **“bunu yapmıştık benzerleri var.. lütfen sistemdekileri bir kontrol et”**. 181'in önceki yenilik değerlendirmesi geri çekildi. 1–180 katalog assetlerinin tamamı tarandı, beş yerleşim haritası incelendi; 7/23/48/68/121 ve 181 gerçek sprite'larla yan yana kontrol edildi. 7/68'de basamak; 23/48'de açık üst köşe ve kenar/alt engel kütlesi; 121'de ColorChest + Wardrobe + SculptingStone birlikteliği var. 181 bu motifleri yeniden birleştiriyor. [Kanıt ve kapsam](Reviews/181-Repetition/Review.md), [yan yana görsel](Reviews/181-Repetition/Comparison.png).

Yeni ders: sadece yakın levellara veya engelin son kullanım numarasına bakmak yetmez. Katalog genelindeki benzer siluetleri, çalışma alanı yönünü ve açılma faydasını karşılaştır. Basamağı küçültmek, çevirmek veya türleri değiştirmek tek başına yeterli fark değil. Önceki levellar korunur; reddedilen 181 karşı örnek olarak saklanır, 182'ye ilerlenmez. Yeni bir alternatif bu kontrol isteği sırasında yazılmadı.

### 181 açıklaması ve zorluk revizyonu · v2

**Son kullanıcı açıklaması:** “şekle çok itiraz etmiyorum ama çok kolay ... bu level için”. Önceki incelemeyi yalnız şekil reddi olarak yorumlamak fazla genişti. Benzerlik kaydı kalır; ana revizyon nedeni zorluk yüküdür. Kullanıcının açık şekil onayı veya ölçülmüş win-rate sonucu olduğu da varsayılmaz.

181'de basamak ailesi korundu, kütle 24'ten 40 hücreye büyüdü; başlangıç taşı 75→59, ColorChest 3→6, dolap 3→4, oyma taşı 4→16, çamur 24→40 oldu. Dört dolabın tamamı üç katmanlıdır; hamle yalnız 27→26 değişti. İsim/GUID/katalog ve 1–180 korundu. Eski taslak `Batches/181/Revisions/v1/` içinde saklı; yeni sürüm inceleme bekliyor, dengesi ölçülmedi.

Ders: zorluk geri bildirimini şekil yeniliği problemine çevirmeden, hedef yükü ve açılma sonrasındaki iş üzerinden yanıtla. Kullanıcı şekle büyük itirazı olmadığını söylüyorsa sırf benzerlik nedeniyle görseli terk etme.

### 181 v3 — kullanıcının çapraz hole isteği

Kullanıcı v2 için **“bu fena değil”**, ardından **“sol üst köşeden başlayıp x,y göre 1,1, 2,2, 3,3, 4,4 gibi ... hole ... boardu ikiye bölmüş oluruz daha zor olur”** dedi. Bu, genel yöne olumlu geri bildirim ve belirli maske revizyonudur; ölçülmüş zorluk veya v3 onayı değildir.

9×11 ölçüsü korundu. Bir tabanlı `(1,1)…(9,9)` çizgisi dokuz hole oldu; eşit x/y çizgisi sağ kenara 9. satırda ulaşır. Maske 36/54 hücrelik tam iki dört-yönlü bileşene ayrıldı. Hole'lar mevcut gövdeleri kesmedi; bütün hedef/katmanlar ve 26 hamle aynı kaldı, açık başlangıç taşı 59→50 oldu. Dolum kontrol edildi, LineH/LineV'nin hole hücresini atlayıp karşı tarafa devam ettiği koddan doğrulandı. v2 saklandı. Bu tercih 181'e özeldir; her leveli çapraz bölme kuralı değildir.

### 181 v4 — kesintisiz boşluk ve korunacak köşeler

Kullanıcı oyun içi görselle **“böyle bir durumda tamamen boş bir diyagonal satır oluşması doğru olmaz mıydı”** dedi; ardından **“bu zor bir konu ben çok uğraşmıştım köşe yuvarlakları çok doğru birleşmeli ... başka yerler bozulmasın”** diye özellikle belirtti. [Kalıcı oyun görseli](References/11-Level181-Ayrik-Capraz-Hole.png).

v3'te delikler yalnız köşeden değiyor ve ayrı kutular gibi görünüyordu. İstenen sürekli boşluk için v4'te üst sağdan sekiz ara hücre daha hole oldu; 17 hücre kenardan bağlı tek şerit. Alt bölgenin 14 başlangıç taşı ve bütün hedef/katmanlar korundu. Ortak köşe/yuvarlatma kodu değiştirilmedi. Aktif board maskesinde çapraz-only 5/10 köşe sayısı 8→0; sınır ve delik bağlılığı kontrol edildi, mevcut sprite'larla büyütülmüş statik border görünümü incelendi. Unity'de son piksel görünümü henüz doğrulanmadı; diğer 683 border/grid/prefab/theme/sahne dosyası değişmedi.

Ders: kesintisiz bir boşluk isteniyorsa yalnız çapraz koordinatları boşaltıp tamamlandı sayma; hole'ların kenardan birleşmesini ve köşe konturunu da kontrol et. Eski, emek verilmiş ortak yuvarlatma çözümünü bir levelin maskesi için yeniden yazma. Bu, bütün çapraz temasların hatalı veya bütün board'ların bu maskede olması gerektiği anlamına gelmez.

## 182 — yeni Lantern tanıtımı

Kullanıcı “yeni bir engel ekledik ... fener ... bunu ekleyerek 182. level tasarlar mısın” dedi. Bu istek 182'yi yetkilendirir; önceki 181 değişmez. Mevcut kod/library'de yeni Lantern75 iki aşamalı movable, ID40 ile aynı temel davranışta bir görsel aile olarak doğrulandı. 182 **Fener Şenliği**: 30 fener, 21 porselen, 28 hamle; üstte 30 normal taş, altta daralan dama. Yeni renkli sandıklar kullanılmadı. Referans 03 ve eski 115/123 karşılaştırıldı; dama motifinin ilk kez kullanıldığı iddia edilmedi.

182 yeni bir inceleme taslağıdır; kullanıcı onayı veya %40–50 kazanma ölçümü değildir. Yeni fenerin tanıtımını ağır ek katmanlarla örtmek yerine iki görsel aileyle okunur tutmak bu tasarım kararıdır; bütün sonraki fener levelları için aynı yerleşim kuralı oluşturmaz.

### 182 v2 — kandilleri tek sütunda toplama · 2026-10-20

Kullanıcı: **“kandil böyle parçalı durunca hoş olmuyor ... 1 sütuna dizilmeli onun solunda ya da sağında diğer engeller ... board fena değil ... sıralı olması daha hoş”**. Dama/kandil dağılımı reddedildi; board siluetini tamamen değiştirme talebi yok.

v2'de aynı maskede 4. sütuna 11 kesintisiz kandil, hemen sağına 21 porselenlik 3×7 blok yerleştirildi. 30 kandil hedefi ve 28 hamle korununca 19 kandil mevcut hedef dolumuna taşındı; bu denge farkı not edildi. v1 asset/not/PNG arşivlendi. v2 henüz kullanıcı tarafından onaylanmadı.

**Ders ve kapsam:** Yeni kandillerde parçalı dağılım yerine kesintisiz tek sütun, diğer ailenin yanında toplu blok okunurluğu tercih ediliyor. Bu istek 182'nin açılış kompozisyonu içindir; her engel ailesinin damasını yasaklama veya bütün sonraki levelları aynı sütuna dönüştürme kuralı değildir. Hareketli engellerin oyun boyunca bu düzende kalacağı söylenmez.

## 2026-10-21 — 183 isteği ve 179 denge düzeltmesi

- **183 yetkisi:** “183. leveli tasarlayacağız ... fener ve bir su kanalı ekledik ... bunları içeriyor olabilir.” Güncel kod/skin kontrol edilerek su kanalının ilk production kullanımı tasarlandı: **Fenerli Bent**. Bu, iki yeni engelin her sonraki levelde zorunlu olduğu anlamına gelmez. 182 ve öncekiler otomatik yeniden tasarlanmaz. Önceki katalog183 test asseti korundu; yeni production tasarımına referans verildi. Henüz kullanıcı onayı yok.
- **179 açık oynanış geri bildirimi:** Kullanıcı `LevelP_001830` için “hiç geçilmiyor”, “4–5 hamle daha ihtiyacı var”, “kolay ... değil ... çözülebilir olsun” dedi. 1/2 hit renkli sandık dizilimi opsiyon olarak önerildi. Super Hard etiketi çözümsüzlük gerekçesi değildir; hedef, mevcut zorluğu koruyarak bitirilebilirlik payı vermek.
- **Uygulanan düzeltme:** Katalog179 doğrulandı, **26→31 hamle** yapıldı. Sandık aşamaları/maske/hedef/katman/yardım aynı kaldı. Hücre bazlı başlangıç hit authoring alanı olmadığı için global library değiştirilmedi. Bu bir ilk kalibrasyon; kullanıcı yeniden oynamadan “çözüldü / dengeli” diye kaydetme. Eski sürüm `Batches/183/Before/` içinde.
- **Kapsam:** Kullanıcı mevcut179'u açıkça istisna olarak revize etti. ColorCrate71–74 yeni tasarımlarda dinlenmeye devam eder; eski179'daki varlıkları bu tur kaldırılmadı. Bütün Super Hard levellara otomatik +5 hamle kuralı çıkarılmaz.

### 179 — açık başlangıç aşaması talebi ve kullanıcı düzenini koruma

Kullanıcı: **“179'da da değişiklik yaptım onu koru ... bu haliyle geçilmesi mümkün değil ... sandıkların ilk katman 3 ... şu andaki kalsın ... sonra 2 ... sonrakiler tek stage”**. Önceki yalnız +5 hamle düzeltmesi yeterli bulunmadı; bu tur başlangıç sandık aşamaları açık taleptir.

Son assetten çalışıldı; eski pinler geri getirilmedi. İlk sandık sırası3, sonraki2, alt iki sıra1 hit oldu; 31hamle ve kullanıcının PulseCore20/LineV21 değişiklikleri korundu. Paylaşılan sandıkların global hitini değiştirmek yerine instance başlangıç aşaması eklendi. 72→42 sandık vuruş yükü; henüz yeniden oyun onayı yok.

Ders: Kullanıcı bir leveli sonradan değiştirdiğini söylüyorsa önce güncel asseti snapshot al, eski tasarım JSON'undan asseti yeniden üretme. “İlk güçlü, sonra hafifleyen” yükte yalnız hamle artırmak yerine istenen aşama gradyanını uygula. Bu mevcut179'a özel revizyondur; tüm Super Hard levellara veya bütün yeni sandıklara otomatik uygulanmaz.

## 185 — yatay ayna ve satır değişimi isteği

Kullanıcı ekli görselle **“böyle bir level ... tam zıttı ... alt köşe sağ ... diğer boardun da sola doğru simetrisi ... yumurtalar yerine bir sıra fener bir sıra plastic two stages ... diğer sarı yerlere sculpting stone”** dedi. [Kaynak](References/12-Aynali-Serit-Referansi.png). Bu açık tasarım talebidir; önceki öğrenme-only görsellerin kapsamından ayrılır.

185IşıkMerdiveni: yatay ayna, ana gövde solda, küçük kıvrım sağda;24Lantern+21PlasticTwoStage yatay dönüşümlü;9bejblokoymataşı. Sarı normal eşleşme taşları renk havuzunda kaldı. Mor alt engel belirsiz: opsiyonel pusula/enerji sorusu yanıtsızken belirtilen pusula varsayımı draftta kullanıldı. 32hamleHard ve1başlangıçTNT tasarım kararıdır, kullanıcı onayı/denge ölçümü değildir.

Ders/kapsam:182'deki tek sütun tercihi yatay sıraları genel olarak yasaklamaz; son açık görsel yerleşim talebi önceliklidir. Ayna hem maskeyi hem engel bölgelerini kapsar. Başka oyunun yumurtasının yardımını, hareketini veya alt mor engelin mekaniğini görüntüden çıkarma. Kullanıcının istemediği ilave köprülerle referans geometrisini değiştirme; ortak köşe kodunu koru. Önceki levellar yeniden tasarlanmadı.

## ColorCrate başlangıç aşamasının editördeki yeri

Kullanıcı: “overlaye neden koydun normal chest obstacle gibi birşey bu ama”. İlk uygulama 179’daki katmanlı veriden dolayı Overlay ile sınırlandı; bu normal sandık yerleşimi için uygun değildi. Aşama seçimi normal Obstacle paletinde bulunmalı; başlangıç hasarı ile üst üste engel katmanı ayrı kavramlardır. Normal sandıkta 1/2/3 vuruş seçimi ve doğrudan yeniden boyayarak güncelleme eklendi. Gerçek katman desteği ve 179 asseti korundu.
