# 242 · Pil Kavşağı / Battery Junction

9×11, 97 aktif hücre; **21 hamle · Normal**. 4 renk. Oyuncu hedefi: %50–60; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002420.asset) · [Önizleme](Level242-Pil-Kavsagi.png) · [Batch](Batches/235-250-Late-Game/README.md)

Enerji rıhtımı ve dört renkli pil kutusu karşılıklı; oyma omurga alttan ortaya uzanır.

**İlk karar:** Bir rengin pilini bitirmek yerine dört rengi dengeli ilerlet; büyük specialı çok renge temas edecek yerde hazırla.

**Açılınca:** Pilin her rengi kendi üç vuruşunu ister. Oyma omurgası kalkınca alttaki çamur açılır.

**Kalan iş:** Pil, on enerji ve omurganın iki aşaması kalır; aynı renkleri sürekli toplamak yetmez.

Hedefler: 10 Enerji, 12 Pil vuruşu, 10 Oyma taşı, 10 Çamur.

Açılışta 79 normal yuva: 79 doğal dolum, 0 gerçek gölge pini. Pin indexleri: []. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 170, kırpılmış siluette 226. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.
Pil hedefi kutu sayısı değildir: bir kutudaki dört renk × üç kabul edilen vuruş = 12. Goals/PreLevel için mevcut dolu pil sprite’ı kullanılır; boş kasa resmi hedef ikonu yapılmadı.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
