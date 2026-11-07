# 248 · Renkli Sahne / Color Stage

8×11, 88 aktif hücre; **17 hamle · Normal**. 4 renk. Oyuncu hedefi: %40–50; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002480.asset) · [Önizleme](Level248-Renkli-Sahne.png) · [Batch](Batches/235-250-Late-Game/README.md)

İki yükseklikte ışık balkonu, dipte iki boya kasası ve aralarındaki porselen sahne.

**İlk karar:** Kayan lambanın rengini beklemekle boya kutularını çoklu special isabetiyle indirmek arasında seçim yap.

**Açılınca:** Porselen sahne kalkınca çamur açılır; bir ışık çerçevesi bitince bütün yolu boşalır.

**Kalan iş:** Dört raf, iki kasadaki dokuzar kova ve sahnenin zemini; tek açılış atışı bitirmez.

Hedefler: 4 Lamba çerçevesi, 2 Boya kasası, 12 Porselen, 12 Çamur.

Açılışta 56 normal yuva: 50 doğal dolum, 6 gerçek gölge pini. Pin indexleri: [32, 33, 40, 62, 63, 71]. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 229, kırpılmış siluette 181. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
