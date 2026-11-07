# 241 · Enerji Rıhtımı / Energy Dock

8×10, 78 aktif hücre; **24 hamle · Normal**. 4 renk. Oyuncu hedefi: %50–60; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002410.asset) · [Önizleme](Level241-Enerji-Rihtimi.png) · [Batch](Batches/235-250-Late-Game/README.md)

Tek sol rıhtım: oyma kapakların devamında dört enerji kabı; sağda geniş ilk çalışma alanı.

**İlk karar:** Rıhtımı açıp aynı kaynağa komşu eşleşmelerle enerji toplamayı öğren.

**Açılınca:** Kaplar ortak sekiz enerji kotasına çalışır; kabı kırma hedefi yoktur.

**Kalan iş:** Enerji geldikten sonra açılan rıhtım çamuru ayrı bir son iştir.

Hedefler: 8 Enerji, 12 Oyma taşı, 12 Çamur.

Açılışta 62 normal yuva: 62 doğal dolum, 0 gerçek gölge pini. Pin indexleri: []. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 77, kırpılmış siluette 1. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
