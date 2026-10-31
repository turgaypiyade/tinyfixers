# 131–205 V1 level incelemesi ve revizyonu · 2026-10-31

Kullanıcı isteği: 130 sonrasının tamamını incele; özellikle 150 sonrası kolay/heyecansız normal seviyeler ve gereğinden geniş boardlar yeniden değerlendirilsin. 5/9 sonlu seviyelerin zorluk yönü referans kalsın. Yardımcılar dahil görünen engeller hedefte temsil edilsin; en fazla 5 goal.

**Uygulandı:** 75 seviye incelendi; 51 asset revize, 24 asset aynen korundu. 17 maskede/boyutta değişiklik var. Her seviyenin en fazla 5 hedefi; 7 bossun tamamında tam 5 hedef. İlk 130 seviye, 201, katalog, bütün GUID/meta ve TR/EN adları aynı.

## Kullanıcı kuralı ve agent tasarım kararı

Kullanıcı hedef/difficulty yönünü verdi; aşağıdaki tek tek düzenler agent revizyonudur, oynanmış denge veya kullanıcı onayı sayılmaz. Bütün normaller Hard yapılmadı. Uygun dar/yoğun veya tanıtım düzenleri korunurken gereksiz başlangıç alanı, erken bitiren düşük hedef miktarları ve zorunlu olmayan yardımcı yükler düzeltildi.

Sonu 5/9 olan 15 seviyenin **maskeleri ve hamle bütçeleri korundu**. Hedef limiti için 179’da hamster ailesi kaldırıldı, 185’te alt renk bölgeleri sarı ailede birleştirildi; bu iki içerik sadeleşmesi eski düzenle birebir aynı zorluk iddiası değildir. Diğerlerinde ağırlık hedef kapsamını düzeltmektir.

## Öne çıkan değişiklikler

- 143: Tahtada 36 iki-aşamalı engel varken hedef 4 idi; artık 36 başlangıç engelinin tamamı istenir.
- 151: Mühür tanıtımı 99→73 aktif hücre; altındaki bonus altınlar artık mevcut GoldCoin hedef engeli. Mührü açınca iş bitmez.
- 168: Gereksiz üst sıra ve orta yarıkla iki çalışma şeridi; 99→87 aktif hücre.
- 176: İki renk bandı aynı fikirle 9×10→7×7, 90→49 aktif hücre.
- 178/188: Üç bedelsiz üst sıra kaldırıldı; sırasıyla 66/70 aktif hücre.
- 184/196: İki açık üst sıra kaldırıldı; kargo çıkışları ve basamaklı açılma sırası korundu.
- 190: Düello 7 sütun; kırmızı/yeşil kapaklar, çamur, gri alet ve boss birlikte hedef.
- 202–204: Kuş kotaları 18/12/16 olarak korundu; çamur/kuş ritmi ve dipteki 3×3 fener grubu ile ek iş. 205’te sepet de hedefte. 201’in onaylı 50 kuşu ve iki 2×3 grubu aynı.

## Bosslar: hedefler ve dinamik baskı

Boss HP, dalga sayıları/tanımları, oyuncu HP ve saldırı ayarları değiştirilmedi. Beş slota sığmak için bazı yardımcı aileler birleştirildi. Geri sayımlı alet mekaniği kapatılmadı: yalnız gri ID47 kullanır ve her boss başlangıçta 1 erişilebilir alet içerir; hedef bu yüzden belirsiz gelecekteki spawn’a bağlı değildir. Atılabilecek diğer engeller de gösterilen hedef türleriyle sınırlıdır. Mud hedefleri mevcut ground-truth dinamik sayaçla sonradan atılan çamuru da takip eder.

| Level | Boss HP | Diğer dört hedef | Atılan baskı türleri |
|---|---:|---|---|
| 140 | 450 | Kalkan 4, İki-aşamalı 30, Gri alet 1, Metal parça 3 | İki-aşamalı |
| 150 | 540 | Kırmızı plastik 20, Çamur 12, Oyma taşı 12, Gri alet 1 | Kırmızı plastik, Çamur |
| 160 | 460 | Sandık 2 18, Çamur 21, Gri alet 1, Mühür 1 | Çamur |
| 170 | 500 | Çamur 27, Oyma taşı 13, Gri alet 1, Lazer 2 | Çamur |
| 180 | 520 | Çamur 18, Gri alet 1, Boya kasası 2, Brandalı heykel 1 | Çamur |
| 190 | 560 | Çamur 8, Gri alet 1, Kırmızı sandık 6, Yeşil sandık 6 | Çamur |
| 200 | 580 | Mavi plastik 18, Kalkan 4, Yumurta 2, Gri alet 1 | Mavi plastik |

## Statik doğrulama ve sınırlar

- Gerçek YAML Ruby/Psych ile okundu; 75 mask/dizi/origin/footprint, katman, tube/magnet/channel yolu, kanal çıkışı, initial crate stage, hedef miktarı/kapsamı ve goal limiti denetlendi.
- Değişen maskeler yeni bir bağlantısız aktif bölge yaratmıyor. Başlangıç boşluğu adayları PNG’lerde noktayla gösteriliyor; rastgele taş renkleri temsili. Hazır special hücreleri maskeden kesilmedi. Bu grafik model gerçek cascade/bot oynatımı değildir.
- Su deposu olan bölümlerde sonradan oluşan WaterPuddle hedefi baştan 5 slot hesabına katıldı. Çiçek hedefi alt çimi bırakmasın diye 195’te çim hedefi de var. Kalıcı üreticiler kırma hedefi yerine kuş/anahtar/enerji hedefiyle temsil edilir.
- Katalog/GUID/metalar, 1–130 ve bütün değişmeyen level içerikleri hash ile korundu. Baseline’daki 2.337 kapsam dışı dosyadan 2.336’sı aynı; tek ayrı yetkili fark, kullanıcı bu iş sırasında Rodos kaybolmasını bildirince yapılan `GridSpawner.cs` olay-sırası düzeltmesidir. Bu fark before/after hash ile ayrıca kayıtlıdır.
- Yeni level runtime/VFX/audio kodu, global obstacle library değişikliği, asset import ayarı, bot/kaskat simülasyonu, Unity Play/build, yayın veya release baseline yok. Rodos düzeltmesi ayrı talep kapsamındadır.
- Hedefleri tamamlamak artık daha fazla oyun işi isteyebilir; measured win-rate/FPS iddiası yok. Daha küçük tahtalar hücre işini artırmaz; yeni engel türü/runtime algoritması eklenmedi. HUD en fazla 5; mevcut efekt/pool/servisler kullanılır. Goal kapsamına alınan eski yardımcılar zaten aynı davranışı çalıştırıyordu.

## Tüm 75 seviyenin karar kaydı

| Level | Board / aktif alan önce → sonra | Hamle | Goal sayısı | Karar |
|---|---|---|---|---|
| 131 · Çelik Bahçe | 9×11 / 87 | 26 | 3 → 3 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 132 · Altıgen Petek | 9×11 / 75 | 27 | 4 → 5 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 133 · Arlekin Uçurtma | 9×11 / 99 | 28 | 4 → 4 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 134 · Damalı Kale | 9×11 / 99 | 24 | 4 → 5 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 135 · Kristal Kalp | 9×11 / 68 | 27 | 3 → 4 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 136 · Hamster'ın Kalesi | 9×11 / 99 | 28 | 4 → 5 | İki benzer hareketli tür tek iki-aşamalı şeritte birleşti; taş ve duvar hedefleri için beş slot kaldı. |
| 137 · Roma Tapınağı II | 9×11 / 99 | 26 | 2 → 2 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 138 · Su Kuleleri | 9×11 / 99 | 28 | 2 → 3 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 139 · Jelli Çelik | 9×11 / 99 | 32 | 3 → 3 | Mevcut zor seviye referansı: içerik, maske, hamle ve hedefler aynen korundu. |
| 140 · Porsuğun Tacı | 9×10 / 90 → 9×10 / 82 | 30 | 1 → 5 | Üst çalışma alanı kademeli kısaldı; sepet/hamster yerine iki aşamalı şerit, kalkan, metal, geri sayımlı alet ve boss. Geri sayımlı alet mekaniği korundu: tek gri tür ve başlangıçta bir hedef. Rastgele baskı yalnız gösterilen türlerden. Beş slot için fazla aileler birleştirildi; boss HP/dalgalar/saldırı sayıları aynı. |
| 141 · Jel Kartuşları | 9×11 / 99 | 28 | 4 → 5 | Kartuş/kapı/oyma açılışı beş hedefte toplandı; üç ayrı yardımcı dolgu ailesi kaldırıldı. |
| 142 · Kilitli Alet Çantası | 9×11 / 73 | 32 | 3 → 4 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 143 · Tuğla Geçidi | 9×9 / 76 | 27 | 3 → 3 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 144 · Basamaklı Atölye | 9×11 / 88 | 30 | 4 → 5 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 145 · Mor Mozaik | 9×11 / 69 | 32 | 3 → 4 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 146 · Boya Piramidi | 8×8 / 64 | 24 | 1 → 1 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 147 · Renkli Palet | 9×9 / 61 | 27 | 3 → 3 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 148 · Mor Boyahane | 8×11 / 88 | 28 | 3 → 4 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 149 · Bölmeli Pencere | 9×9 / 64 | 31 | 4 → 4 | Mevcut zor seviye referansı: içerik, maske, hamle ve hedefler aynen korundu. |
| 150 · Kızıl Köprü | 9×10 / 75 | 30 | 1 → 5 | Kalkan/yumurta yardımcıları kırmızı ailede birleşti; köprünün oyma yükü korundu. Çamur/alet ve boss ile beş hedef. Geri sayımlı alet mekaniği korundu: tek gri tür ve başlangıçta bir hedef. Rastgele baskı yalnız gösterilen türlerden. Beş slot için fazla aileler birleştirildi; boss HP/dalgalar/saldırı sayıları aynı. |
| 151 · Antik Mühür | 9×11 / 99 → 9×9 / 73 | 25 | 1 → 2 | Mühür altındaki hedefsiz bonus altın, mevcut GoldCoin hedef engeline dönüştü; mühür sonrası iş de zorunlu. Mühür tanıtımı korundu; gereksiz üst/alt çalışma alanı 99→73 hücreye sıkıştı. |
| 152 · Kargo Kemerleri | 9×11 / 99 → 9×11 / 95 | 30 → 28 | 3 → 4 | Mühür altındaki hedefsiz bonus altın, mevcut GoldCoin hedef engeline dönüştü; mühür sonrası iş de zorunlu. İki kemerin açık kenar cepleri kesildi; 30→28 hamle ve mühür sonrası altın toplama. |
| 153 · Piramit Mührü | 9×11 / 87 | 26 | 4 → 4 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 154 · Anahtarlı Boyahane | 9×11 / 88 | 34 | 4 → 5 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 155 · Mühürlü Dört Kule | 9×11 / 69 | 30 | 3 → 4 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 156 · Gözcü Adaları | 9×11 / 87 | 26 | 3 → 4 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 157 · Gözcü Kulesi | 9×10 / 74 | 35 | 5 → 5 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 158 · Su Feneri | 9×11 / 75 | 30 | 4 → 5 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 159 · Lazerli Hazine | 8×11 / 70 | 26 | 4 → 4 | Mevcut zor seviye referansı: içerik, maske, hamle ve hedefler aynen korundu. |
| 160 · Kalkan | 9×10 / 76 | 30 | 1 → 5 | Beş hedef için mühür altı kalkan ve çim çamurda birleşti; geri sayımlı alet korunurken bossun attığı çamur da aynı hedefte izlenir. Geri sayımlı alet mekaniği korundu: tek gri tür ve başlangıçta bir hedef. Rastgele baskı yalnız gösterilen türlerden. Beş slot için fazla aileler birleştirildi; boss HP/dalgalar/saldırı sayıları aynı. |
| 161 · Brandalı Atölye | 9×10 / 86 | 24 | 1 → 1 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 162 · Çiçekli Yonca | 9×11 / 77 | 32 → 30 | 4 → 4 | Dört zorunlu işi olan yonca silueti korundu; 32→30 hamle. |
| 163 · Örtülü Surlar | 9×11 / 96 | 25 | 3 → 3 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 164 · Kanal Adaları | 9×11 / 75 | 30 | 4 → 4 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 165 · Mor Tesisat | 8×10 / 64 | 28 | 4 → 4 | Mevcut zor seviye referansı: içerik, maske, hamle ve hedefler aynen korundu. |
| 166 · Nilüfer Göleti | 9×11 / 73 | 32 | 3 → 4 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 167 · Üç Atölye | 8×9 / 70 | 36 | 4 → 4 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 168 · Renkli Onarım | 9×11 / 99 → 9×10 / 87 | 28 | 3 → 3 | Boya avlusunda üst boş sıra kalktı; merkezdeki yarık iki eşleşme şeridi arasında seçim yaratır. |
| 169 · Nilüfer Tacı | 9×11 / 63 | 26 | 3 → 5 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 170 · Kum Saati | 9×11 / 72 → 9×10 / 72 | 30 | 1 → 5 | Boş ilk satır kaldırıldı; Boss board 11→10 satır. Kalkan altlığı oyma tabanına katıldı; çamur/lazer/oyma/alet ve boss hedefleri. Geri sayımlı alet mekaniği korundu: tek gri tür ve başlangıçta bir hedef. Rastgele baskı yalnız gösterilen türlerden. Beş slot için fazla aileler birleştirildi; boss HP/dalgalar/saldırı sayıları aynı. |
| 171 · Rodos Atölyesi | 9×11 / 91 | 30 | 1 → 1 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 172 · Mor Akıntı | 9×11 / 99 → 9×11 / 95 | 26 | 2 → 4 | Mor akıntıda 2×2 iç boşluk ve kartuş/plastik hedefleri; bedelsiz geniş merkez azaltıldı. |
| 173 · Büyük Restorasyon | 9×11 / 99 | 24 | 3 → 5 | İki yardım ailesi yerine turuncu bölge ve zorunlu şarj kutusu; tüm görünen yük beş hedefte. |
| 174 · Usta Restoratör | 9×11 / 99 → 9×11 / 94 | 24 | 3 → 5 | Üst orta giriş ve alt orta açıklık daraldı; pusulalar ve sepetler artık bitirilir. |
| 175 · Çifte Hazine | 9×9 / 55 | 36 | 3 → 3 | Mevcut zor seviye referansı: içerik, maske, hamle ve hedefler aynen korundu. |
| 176 · Kızıl ve Yeşil | 9×10 / 90 → 7×7 / 49 | 26 | 2 → 2 | İlk renk sandığı dersi aynı iki bantla 9×10→7×7; 80 yerine 39 açık çalışma hücresi. |
| 177 · Zincir Atölyesi | 9×11 / 99 → 9×11 / 92 | 32 → 30 | 4 → 5 | Beş hedefe sığan dolap/roket atölyesi; bedelsiz toplu patlama yerine bitişik sandık işi, orta geçiş seçimi. |
| 178 · Renkli Çerçeve | 9×11 / 99 → 9×8 / 66 | 28 | 4 → 4 | Renkli çerçevenin üstteki üç boş sırası çıktı; hedefler/hamleler korundu, başlangıç alanı kısaldı. |
| 179 · Lazerli Restorasyon | 9×11 / 95 | 40 | 5 → 5 | Super Hard düzeni/40 hamle korundu; altıncı tür hamster kalktı, lazerler beşinci hedefte açıkça gösterildi. |
| 180 · Onarım Kapısı | 9×10 / 74 | 30 | 1 → 5 | Tarp altı ve yan çim alanı çamurda birleşti; geri sayımlı aletle boss dahil beş hedef ve uyumlu çamur baskısı. Geri sayımlı alet mekaniği korundu: tek gri tür ve başlangıçta bir hedef. Rastgele baskı yalnız gösterilen türlerden. Beş slot için fazla aileler birleştirildi; boss HP/dalgalar/saldırı sayıları aynı. |
| 181 · Fener Şenliği | 9×11 / 81 | 28 | 2 → 2 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 182 · Mor Mercek | 9×11 / 87 | 28 | 2 → 3 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 183 · Boya İskelesi | 9×11 / 87 | 29 | 4 → 5 | Orta hareketli bölge tek mor aile oldu; su deposunun ürettiği su dahil tüm yük beş hedefe sığar. |
| 184 · Kargo Rıhtımı | 9×11 / 92 → 9×9 / 76 | 28 → 27 | 3 → 3 | Kargo rıhtımının iki boş üst sırası kalktı; kapalı çıkışlar ve iki kargo kanadı korunur. |
| 185 · Mühürlü Vitray | 9×11 / 81 | 28 | 2 → 5 | Hard hamle/maske korundu; alt iki renk bölgesi tek sarı aile, çamur/tarp dahil toplam beş hedef. |
| 186 · Fenerli Bent | 9×11 / 99 → 9×11 / 91 | 28 → 26 | 3 → 3 | Kanal tanıtımının üst sağ boş terası kesildi; kaynak/çıkış ve fener sütunu aynı, 28→26 hamle. |
| 187 · Çiçekli Pusula | 9×11 / 59 | 29 | 3 → 3 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 188 · Boru Orgu | 9×11 / 99 → 9×8 / 70 | 27 → 26 | 3 → 3 | Boru orgunun üç boş üst sırası kalktı; anahtar hedefi üreticileri de temsil eder, tüplerin uçları korunur. |
| 189 · Dolaplı Geçit | 9×11 / 83 | 30 | 3 → 3 | Mevcut zor seviye referansı: içerik, maske, hamle ve hedefler aynen korundu. |
| 190 · Kanyon Düellosu | 9×10 / 86 → 7×10 / 70 | 30 | 1 → 5 | Düello 7 sütuna indi; kırmızı/yeşil/çamur/geri sayımlı alet ve boss beşlisi, baskı çamur hedefine bağlı. Geri sayımlı alet mekaniği korundu: tek gri tür ve başlangıçta bir hedef. Rastgele baskı yalnız gösterilen türlerden. Beş slot için fazla aileler birleştirildi; boss HP/dalgalar/saldırı sayıları aynı. |
| 191 · Boya Terasları | 9×11 / 73 | 28 | 3 → 3 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 192 · İkiz Gölet Kalesi | 9×10 / 90 | 32 | 4 → 5 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 193 · Üç Mühürlü Geçit | 9×11 / 95 | 28 | 4 → 4 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 194 · Çelik Kilit | 9×11 / 74 | 31 | 4 → 5 | Yedi tür beşe indi: kırmızı kapak bölgesi, metal, Rodos, pusula, çamur. Kapaklar 2→1 başlangıç aşamasıyla bitirilebilir yükte. |
| 195 · Boyalı Akarsu | 9×11 / 86 | 24 | 4 → 5 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 196 · Basamaklı Arşiv | 9×11 / 99 → 9×9 / 78 | 26 | 4 → 4 | Basamaklı arşivin iki bedelsiz üst sırası ve sağ üst köşesi kesildi; açılan her dolap yeni alan sağlar. |
| 197 · Işık Merdiveni | 9×11 / 79 | 32 | 4 → 4 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 198 · Zümrüt Vitrin | 9×11 / 76 | 17 | 4 → 4 | Agent görsel incelemesi: mevcut açılma/yoğunluk ve hedef kapsamı yeterli; bu tur yapay ek zorluk verilmedi. |
| 199 · Deniz Kabuğu | 9×11 / 87 | 26 | 3 → 5 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |
| 200 · Kanyon Köprüsü | 9×10 / 84 → 9×10 / 78 | 30 | 1 → 5 | Kanyonun üst cepleri küçüldü; boss/mavi/yumurta/kalkan/geri sayımlı alet hedefleri atılan türlerle eşleşir. Geri sayımlı alet mekaniği korundu: tek gri tür ve başlangıçta bir hedef. Rastgele baskı yalnız gösterilen türlerden. Beş slot için fazla aileler birleştirildi; boss HP/dalgalar/saldırı sayıları aynı. |
| 201 · Guguk Bahçesi | 9×10 / 88 | 26 | 1 → 1 | Kullanıcı onaylı iki 2×3 kuş grubu ve 50 kuş: bütünüyle korundu. |
| 202 · Boyalı Kuş Avlusu | 9×9 / 63 → 9×9 / 59 | 26 → 25 | 2 → 3 | Halka avlunun üst tacı daraldı; iki sıralı zorunlu çamur işi ile kuş ritmi boya kasaları arasında bölünür. |
| 203 · Kuşlu Sevkiyat | 9×10 / 62 | 27 → 25 | 3 → 4 | Duvar sonrası iki kargo ayağında çamur işi eklendi; kuş/duvar/kargo açılışını tek hamle zinciriyle bitirmek yetmez. |
| 204 · Kuşlu Akarsu | 9×11 / 74 | 28 → 26 | 3 → 3 | S kıyısı korundu; dip fener grubu 3×3 oldu, hedef 6→9 ve 28→26 hamle. |
| 205 · Mühürlü Kuş Parkı | 9×11 / 77 | 27 | 3 → 4 | Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu. |

## Dosyalar / tekrar kontrol

- `Before/`: 75 asıl asset ve eski sprite önizlemeleri; geri alma için kaynak.
- `After/`: uygulanan güncel 75 sprite PNG’si ve 8 toplu inceleme sayfası.
- `manifest.json`: level bazlı önce/sonra alanlar ve karar notları.
- `revise.py`: baseline korumalı authoring planı; uygulanmıştır, tekrar apply yerine audit kullan.
- `audit.py --assets`: güncel assetlerin bağımsız statik kontrolü.
- `validation-after.json`: son statik sonuç ve ayrı Rodos değişikliği.
- `separate-rhodes-fix.json`: aynı oturumdaki ayrı kullanıcı talebinin runtime hash kaydı.

Bir sonraki adım cihazda oynayıp özellikle 143, 151, 176, 178, 188, 194, 196 ve bossların yeni hedef baskısını değerlendirmek. Otomatik 206 tasarlama/yayınlama yetkisi yok.
