# 213–220 · Sekiz ayrı kompozisyon

Kullanıcı 2026-11-03 tarihinde sekiz seviye daha istedi. Hedef aralık aktif V1 katalogda **213–220**. 1–212 korunmuştur. Bu taslaklar kullanıcı tarafından henüz oynanıp onaylanmadı.

Fener, kuş evi, su kanalı ve değirmenler aynı tahtaya doldurulmadı; porselen, iki aşamalı plastik, renkli plastikler, ColorChest ve çiçekli çim set boyunca dağıtıldı. 211–220 yeni mekanik tanıtımı istisnası sürer. Sarı9/yeşil13, önceden açık mavi11/kırmızı12 renkli plastik mekaniğinin istenen renk varyantlarıdır; bağımsız yeni aile sayılmadı.

| Seviye | TR / EN | Tür | Hamle | Ana kompozisyon |
|---|---|---|---|---|
| 213 | Işıklı Balon / Lantern Balloon | Normal | 22 | Balon kubbesi, iki fener ipi ve iki aşamalı plastikten tek parça sepet. |
| 214 | Guguk Çanı / Cuckoo Bell | Normal | 24 | Çan gövdesinin kenarlarında L/ters L kuş evleri; alt bordürde iki renkli sandık, çiçekli dil. |
| 215 | Vitray Atölyesi / Stained Glass Workshop | Hard | 26 | Yoğun iş yükünü taşıyan düz vitray: sarı lento, mavi/kırmızı dikey panolar ve sandıklı alt kaide. |
| 216 | Sulu Lale / Water Tulip | Normal | 22 | Üç uçlu lale başı, kanalın kıvrıldığı çiçekli taç ve porselen kök tabanı. |
| 217 | Kanatlı Kum Saati / Winged Hourglass | Normal | 24 | Kum saati siluetinde yukarıdaki yeşil cam, dar bel ve alt ayaklara oturan iki değirmen. |
| 218 | Kıvrılan Nehir / Winding River | Normal | 25 | Dalga silueti içinde uzun kıvrılan kanal; bir kıyıda mavi, öbüründe kırmızı plastik ve iki kıyı sandığı. |
| 219 | Çiçekli Taç / Blossom Crown | Super Hard | 25 | Üç uçlu taç; çiçekli geniş üst bant, çift plastik panel ve çim kaplı sandıklarla tamamlanan değirmen kaidesi. |
| 220 | Büyük Festival / Grand Festival | Boss | 29 | Boss sahnesi: kubbeli üst alan, iki uzun fener sütunu, yan kuş tribünleri ve porselen alt bant. |

## Mekanik ve akış sınırları

- 213: 8 başlangıç feneri / 18 hedef; 10 ek fener yalnız 2 ve 6 numaralı sütunlarda. 220: 14 başlangıç / 18 hedef; 4 ek fener 1 ve 7 numaralı sütunlarda. Sütunlar sıfır tabanlı, fallback 0. Diğer hareketli hedefler başlangıç adedine eşittir.
- 214/218/220 kuş hedefleri 26/24/28; ev adedini kırma hedefi yapmaz. Kenar L köşelerindeki kapalı iç yüzler special isabeti ister; bunların bulunduğu satırlarda açık saldırı hattı var.
- 216/218 kanal uzunlukları 15/19 hücre (kaynak dahil). Tekrar/up-step yok; her biri aşağı yönlü son adımla en alt satırda biter. Kaynak hücresinde taş/engel yok; kalan kanal üstünde yalnız desteklenen 1×1 örtüler var.
- 217/219: ikişer değirmen ve 40 un hedefi → çuval başına20 konum. Kanatlara yalnız special/joker hasarı gelir. LineV bütün sütunu tarar; hole hücresi dalgayı durdurmaz. Unun örtü/tahta dışı/su yüzünden sığmayan kısmı HUD canlı+gelecek ürün hesabında azalabilir; 40 kesin temizlenecek yerleştirilmiş un hücresi değildir.
- 219: üstte18 çiçek →18 çim; ayrıca iki alt sandığın üstünde8 çim, toplam26. Solda yan yana iki değirmen, sağda yan yana iki çim kaplı sandık; 217’nin iki köşe değirmeni düzeni yinelenmedi.
- 220: 9×10 Boss, 620HP / üç dalga. Başlangıç engelleri hedeflerde var. Baskı havuzu8/12/13 hedef DIŞINDA; kullanıcı 2026-11-01 kuralına uygun. Alet tehditleri de hedeflerle çakışmıyor. 210’daki mevcut karakter/HP/saldırı ayarları şablon olarak korundu; başlangıç kalkanı eklenmedi.
- Açılışlarda tüm normal taş yuvaları doğal besleme veya gerçek authored renk pinleriyle dolu. 213/215/217/218 pin sayıları16/13/35/3. Diğerlerinde0. Sabit başlangıç special yok. Pinler kendi aralarında başlangıç üçlüsü oluşturmaz.
- Statik aile-kaldırma kesitleri hareketli engelleri yerçekimiyle oynatmaz. Yeni açılmış sabit-engel yuvaları üzerinde hâlâ hareketli blok varsa bu hücreler ayrı `newlyExposedSlotsBelowMovables` kaydındadır; bunları ölçülmüş gerçek boşluk veya doğrulanmış yerçekimi sonucu sayma. Açılış ve tüm kaldırılabilir katmanların temizlendiği uç durumda beslenemeyen normal yuva yok.

## Doğrulama / görsel inceleme

- `python3 design.py`: yalnız sekiz yeni asset/meta, katalog ekleri ve sekiz TR/EN isim. `Before/` anlık dosya yedekleri ve `protected-before.json` yerel koruma hash’leridir; **app release baseline değildir**.
- `python3 verify.py`: bağımsız Ruby/Psych Unity YAML okuması; tam dizi uzunluğu, mask/origin/footprint, katman, hedef, dört renk, refill kotası/sütunu, hazırlık pinleri, grup boyutları, kanal yönü, boss/tehdit ve GUID/katalog/isim kontrolü.2976 mevcut Settings/Scripts/Resources dosyası hash ile aynı.
- `python3 render.py`: güncel proje sprite’larıyla sekiz büyük açılış + katman kesiti ve `Levels213-220.png`. Serbest taş renkleri temsili; gerçek pinler assette yazılıdır. Kanal kesitlerinde rota için taşlar saydamlaştırılmıştır.
- Tekrar taraması `Reviews/213-220-Showcase/<level>/` içinde aktif V1 katalogla yapıldı. Her seviyenin konum/crop kısa listelerindeki örnekler `Comparisons/` sprite+geometri PNG’lerinde görsel olarak incelendi. Skorlar yenilik yüzdesi veya kullanıcı onayı değildir.
- Unity build, Play Mode, bot/kaskat simülasyonu, cihaz/FPS veya kazanma oranı ölçümü, upload/publish yapılmadı. Hamleler ilk deneme bütçesidir.

## Mevcut performans maliyetleri

Runtime/VFX kodu değiştirilmedi. Her seviyede en fazla10 kuş evi, iki değirmen veya bir kanal var; dört yeni ana mekanik aynı tahtada birleşmiyor. BirdHouseService tek Update ve flyerPool kullanır, boşta kapanır; Clear/OnDestroy uçuşları goal yazmadan bitirip abonelikleri çözer. WaterChannelService mevcut damla havuzunu kullanır; Dispose abonelikleri ve maske dışı şelale kökünü temizler. WindmillService tek Update, tekrar kullanılan aday/yayılım listeleri ve değirmen alt Canvas’ları kullanır; Clear/OnDestroy aktif işleri ve state bağlarını temizler. Ancak çuval/bulut görsellerinde mevcut GameObject/Image üretimi, aday taramaları ve sıralama maliyetleri hâlâ vardır. Boss baskısı mevcut listeler ve tahta taramaları kullanır; aynı anda8 baskı objesi sınırı korunur. Bütün sesler mevcut merkezi board audio yolundadır. Bunlar kod incelemesiyle görülen sınırlardır, ölçülmüş FPS kazanımı değildir.
