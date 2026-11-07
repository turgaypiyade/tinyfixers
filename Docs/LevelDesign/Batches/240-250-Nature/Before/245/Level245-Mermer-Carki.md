# 245 · Mermer Çarkı / Marble Wheel

9×11, 97 aktif hücre; **24 hamle · Hard**. 4 renk. Oyuncu hedefi: %30 yönü; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002450.asset) · [Önizleme](Level245-Mermer-Carki.png) · [Batch](Batches/235-250-Late-Game/README.md)

Yağlı L kaidesinin bir ucunda sol omuz heykeli, diğer ucunda sağ dip heykeli; üstte tek geniş avlu.

**İlk karar:** Yağı erken açıp bastırmakla heykellerin ayrı special aşamalarını ilerletmek arasında seçim yap.

**Açılınca:** Oyma açılınca yağ tehdidi başlar; heykelde tek special yalnız bir aşama ilerletir.

**Kalan iş:** İki heykelin son parçaları ve yeniden yayılan yağ; erken alan temizliği kalıcı bitiş değildir.

Hedefler: 2 Rodos, 16 Oyma taşı, 16 Yağ.

Açılışta 73 normal yuva: 73 doğal dolum, 0 gerçek gölge pini. Pin indexleri: []. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 186, kırpılmış siluette 229. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
