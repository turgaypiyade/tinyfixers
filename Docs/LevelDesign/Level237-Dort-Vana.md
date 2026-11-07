# 237 · Dört Vana / Four Valves

9×11, 97 aktif hücre; **26 hamle · Normal**. 4 renk. Oyuncu hedefi: %40–50; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002370.asset) · [Önizleme](Level237-Dort-Vana.png) · [Batch](Batches/235-250-Late-Game/README.md)

Dört kısa su yolu, karşılıklı oyma basamaklarının içinden kenara çıkar; dipte fener havuzu vardır.

**İlk karar:** Bir su yolunu bütünüyle açmakla yeni eşleşme alanı açan oyma grubunu ilerletmek arasında seçim yap.

**Açılınca:** Oyma kalkınca örtülü kanal hücreleri de erişilir; kanalın bütün bağlantısını açmak gerekir.

**Kalan iş:** Dört ayrı çıkış, fenerlerin iki vuruşu ve alttaki çamur bağımsız işlerdir.

Hedefler: 4 Su kanalı, 16 Oyma taşı, 16 Fener, 12 Çamur.

Açılışta 65 normal yuva: 61 doğal dolum, 4 gerçek gölge pini. Pin indexleri: [36, 62, 72, 98]. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 229, kırpılmış siluette 83. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
