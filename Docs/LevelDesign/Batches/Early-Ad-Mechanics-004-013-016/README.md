# V1 4 / 13 / 16 — reklam mekaniklerini erken gösterme

2026-11-20. Kullanıcı açıkça bu üç levelı yedekleyerek yeniden tasarlamamızı istedi. Görsel ipuçları: ilk seviyeler kareye yakın, içeride anlamlı şekil; kanal mutlaka alt kenarda bitsin; kuş evi minimum 2×3 grup veya kenar L düzeni; elmalar tamamen ayrı adacıklarda olabilir. Bu revizyon, 13'ün eski görsel onayını yalnız bu açık talep kapsamında değiştirir. Yeni taslakların kullanıcı görsel/oynanış onayı henüz yok.

| Level / mevcut ürün adı | Tasarım | Boyut | Hamle | Hedef |
|---|---|---|---|---|
| 4 Kıvrımlı Dere / Winding Stream | Kare içinde S dere; ilk karar kaynağa yakın halkaları açmak. Kaynağa bağlanmadan açılan alt kesimler henüz su almaz. | 9×9 / 81 aktif | 32 | 1 kanal; kaynak + 12 açılacak hücre |
| 13 Katmanlı Geçit / Layered Passage | Ev çatısı silueti; alt ortada kapı biçiminde 2 sütun × 3 sıra kuş evi. Alt grubun iki yanı da eşleşmeye açıktır. İlk hit açar; sonraki hit kuş toplar. | 8×9 / 66 aktif | 30 | 12 kuş / 6 kalıcı ev |
| 16 Turuncu Şeritler / Orange Stripes | Geniş bahçe tacı ve dar gövde; dipte 2×2 sinek kutusu, iki ayrı 2×2 elma adacığı. Üstte special üretip kutuya vur; çıkan yumurtayı kır ve sineklerin elmalara uçmasını gör. | 9×9 / 59 aktif | 30 | 2 elma / 1 kutu / 4 yumurta |

13 ve 16'nın mevcut TR/EN ürün isimleri kullanıcı istemeden değiştirilmedi; fikir başlıkları ayrı açıklamalardır. 4'ün mevcut JSON kaydı yoktu: yalnız onun LevelCL_004 kaydı eklendi. Diğer isimler aynı.

## Yedek ve koruma

`Before/` altında üç asset ve meta, V1 katalog, LevelNames.json, Memory/Design-State/Design-Feedback başlangıç kopyaları var. `protected-before.json` bu turdan önceki mevcut çalışma alanının SHA-256 değerlerini tutar; git HEAD değil, kullanıcının mevcut değişiklikleri esas alındı. Yedekler üzerine yazılmaz.

`author.py --verify` güncel assetleri, dizi/footprint/kanal/izolasyon/dolum ilişkilerini ve korunmuş dosyaları salt okunur kontrol eder. `validation.json`: üç level asseti ve yalnız 4'ün isim kaydı değişmiş; 3409 diğer asset/kod/sahne/prefab/meta/isim-kaynağı aynı. Katalog sırası, GUID ve levelKey korunur. 1/3/5/7/8/9 special öğreticileri, 12/14 joker öğreticileri, Boss10/20 ve diğer seviyeler değişmedi. Bunlar kesin hücre/pin kullanan ScriptedTutorial adımlarıdır; kapalı TutorialManager akışı değildir.

## Statik doğrulama

- Normal başlangıç hücreleri 4:80/80, 13:60/60, 16:47/47 doğrudan yukarıdan beslenir. Hazır special, renk pini, gizli katman veya ek refill kotası yok.
- 4'ün yolu benzersiz, dik komşu, yalnız yana/aşağı; son adım aşağı ve son hücre gerçek tahta alt sınırında. WaterChannelEntry.ExitSide davranışıyla alt şelale çıkışına uygundur. Kanal hedefi hücre sayısı değil 1 tamamlanan yoldur.
- 13'te 2×3 grubun her evine yanındaki normal alandan erişilir; altında beslemesiz taş cebi yok. Goal Collectible(2)/Bird(3), ev kırma hedefi değil.
- 16'da meyvelerin boyut/origin kayıtları 2×2; iki meyve adacığı ana alanla dört-yön bağlantısız. Kutunun oranı 1:2 meyve; dört yumurta × üç ısırık = iki elma × altı ısırık. Kutuya special erişimi üstteki iki açık sütundan mümkün. Yumurta üretim/iniş alanı ana tahtada geniş tutuldu.
- Mevcut LevelEditor kanal çizimi/silme, meyve footprint ve boyut denetimleri; library sprite/goal ve collectible üretici eşlemeleri kontrol edildi. Yeni engel/runtime kodu eklenmedi.
- Güncel proje sprite'larıyla üç PNG ve Overview üretildi, açılıp görsel olarak incelendi. Taş renkleri temsili; gerçek oynanış ekranı değildir.

## Denge, performans ve sınırlar

Bu erken karşılaşmalar sade tanıtımdır. 32/30/30 hamle ilk ayardır, ölçülmüş geçiş oranı yok. 16'da dört special isabeti + yumurtaları kırma işi gerektiği için manuel denemede en önemli kontrol, ilk sinek gösterisine ulaşma süresidir. 4'te ilk su ilerlemesi, 13'te ilk kuşun anlaşılması da manuel kontrol edilmelidir.

Mevcut servisler aynen kullanılır: BirdHouse uçucu havuzu, FlyFruit önceden hazırlanan sinek görselleri/üretim iptal akışı, kanal görselleri ve merkezi ses sistemi. Yeni kare-başı tarama, coroutine veya allocation eklenmedi. Mevcut kurulum/servis maliyetleri sıfır değildir; cihaz FPS/GC ölçümü yapılmadı.

Komşu levellardan ayrışma: 3/5 sandık kompozisyonları arasında 4'ün yalnız kanal iç şekli; 12/14'ün katmanlı sandık/yağ işlerinden farklı 13'ün kalıcı üretici kapısı; 15/17 arasında 16'nın uçuşla erişilen adacıkları. Elma-ağaç fikri geç dönem 242'de de kullanılır; burada 2 kutu/4 elma/kuş evi karışımı yerine 1 kutu/2 elma ve tek hedefle sadeleştirilmiştir. Benzersizlik veya kullanıcı beğenisi iddiası yok.

Plastiğin ilk karşılaşması artık zaten mevcut 17'ye geçer; 17 değiştirilmedi. 11–20 aralığında yağ/kuş/sinek/plastik ile dört mekanik aile olur: eski 2–3 kota yönünden sapma, bu turdaki açık 13/16 erken tanıtım talebinin sonucudur; diğer levelları sessizce değiştirmedik.

Unity UI/Play/build, bot/kaskat simülasyonu, uzak level yayını, release baseline veya commit yapılmadı.
