# Kullanıcı görsel referansları — 2026-10-19

Bunlar tasarım dili referanslarıdır; bütün görsellerdeki mekaniklerin onayı veya birebir kopya talebi değildir. Tanınmayan görsel öğelerin davranışı varsayılmaz.

| Görsel | Görselden alınan ilke | Sınır |
| --- | --- | --- |
| [01 Hole çerçevesi](01-Hole-Cercevesi.png) | Üstte dolu bir bölge, altta hole'u dolaşan kesintisiz sandık konturu | Kullanıcı intro benzeri olduğunu belirtti; zorluk hedefi kopyalanmaz |
| [02 İç içe renk bantları](02-Ic-Ice-Renk-Bantlari.png) | Renk sıraları ve iç boşluklar birlikte bir şekli tamamlar | Cam görünümlü öğeler doğrudan hole diye yorumlanmaz |
| [03 Yoğun dama alanı](03-Yogun-Dama-Alani.png) | Basamaklı üst siluet; yatay geçiş bandı; iki aileden oluşan yoğun alt desen | Dama, rastgele engel serpme değildir; sıkışmış dolum ayrıca kontrol edilir |
| [04 Sütunlar ve basamaklı boşluk](04-Sutunlar-Ve-Basamakli-Bosluk.png) | Kesintisiz sütun ritmi; altta basamaklı negatif alanla biten kontur | Her sütunun oynanabilir erişimi ve refill'i kendi motorumuzda incelenir |
| [05 Kenar boyaları ve merkez](05-Kenar-Boyalari-Ve-Merkez.png) | Büyük engeller kenar/alt çerçeveyi oluşturur; içeride iki renkli desen; altta anlamlı açıklık | Oyuncak/ördek görsellerinin bizim engellerle eş davranışı olduğu varsayılmaz |

## İkinci referans grubu — hole ve grid yapısı

Kullanıcı açıkça board tasarımı / hole kullanımı için verdi; sandık kullanımı zorunlu değil.

| Görsel | İlke | Sınır |
| --- | --- | --- |
| [06 Dört oda](06-Dort-Oda-Ve-Baglantilar.png) | Girintilerle dört bölge ve merkez bağlantıları | Bağlantıların genişliği ve special erişimi ayrı tasarlanır |
| [07 Asimetrik gövde](07-Asimetrik-Kademeli-Govde.png) | Uzayan gövde ve yan çıkıntılarla işlevsel siluet | Başka oyunun kapı/portal kuralları varsayılmaz |
| [08 İki kanat](08-Iki-Kanat-Ve-Orta-Alan.png) | Büyük kenar engelleri, orta çalışma alanı ve sınırı tamamlayan tek hücreliler | Iskeleti kopyalamak engel ailesini kopyalamayı gerektirmez |
| [09 T silueti](09-T-Silueti.png) | Geniş üst alan, daralan iç desen, yanları hole olan alt gövde | Dar gövdenin refill ve match olanakları doğrulanır |
| [10 Daralan siluet](10-Daralan-Siluet-Ve-Bosluk-Ritmi.png) | Dış maske ile iç boşluklar aynı çapraz ritmi takip eder | Gri/pembe öğelerin davranışı veya hedefi görünüşten türetilmez |

## 181 oyun içi düzeltme kanıtı — 2026-10-20

[11 — Ayrı çapraz hole'lar](11-Level181-Ayrik-Capraz-Hole.png), kullanıcının kendi oyun ekranıdır. Köşeden temas eden delikler ayrı çerçeveli kutular gibi görünüyor; kullanıcı kesintisiz boş çapraz şerit ve mevcut köşe yuvarlatmasının korunmasını istedi. v4 maskesi bu geri bildirime göre düzeltildi; görsel diğer levellarda otomatik renderer değişikliği talebi değildir.

## 185 — açık level tasarım referansı

[12 — Aynalı şerit referansı](12-Aynali-Serit-Referansi.png). Kullanıcı bu görselden level istedi: yatay ayna ile küçük alt bölge sağa, ana gövde sola; yumurta alanına dönüşümlü Lantern/PlasticTwoStage satırları; bej/sarı blok şeritlerine SculptingStone. Bu sefer görsel yalnız öğrenme örneği değil açık authoring talebidir. Alt mor öğenin davranışı varsayılmaz; draftta pusula seçimi ayrıca belirtilir. Referansın kasıtlı köşe temaslarını başka levelin sürekli-hole düzeltmesiyle karıştırma.
