# 244 · Çifte Çıkış / Twin Exits

9×11, 97 aktif hücre; **24 hamle · Normal**. 4 renk. Oyuncu hedefi: %50–60; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002440.asset) · [Önizleme](Level244-Cifte-Cikis.png) · [Batch](Batches/235-250-Late-Game/README.md)

İki dış tüp ve ortadaki kargo yolu üç dikey ritim kurar; çıkış kaidesi dipte doludur.

**İlk karar:** Tüplerin ucunu kısaltırken ortadaki kargoları durduran kaideye special hizala.

**Açılınca:** Oyma kaidesi kalkınca kargolar düz sütunlarından aşağı iner; alt çamur ayrıca temizlenir.

**Kalan iş:** Altı gerçek teslimat, iki tüp ve kaidenin çamuru; tüp hücre sayısı hedef sayılmaz.

Hedefler: 6 Kargo, 2 Tüp, 12 Oyma taşı, 12 Çamur.

Açılışta 65 normal yuva: 65 doğal dolum, 0 gerçek gölge pini. Pin indexleri: []. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 111, kırpılmış siluette 18. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
