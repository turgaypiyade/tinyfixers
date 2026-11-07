# 236 · Cam Ray / Glass Rails

9×10, 86 aktif hücre; **16 hamle · Normal**. 4 renk. Oyuncu hedefi: %40–50; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002360.asset) · [Önizleme](Level236-Cam-Ray.png) · [Batch](Batches/235-250-Late-Game/README.md)

Dikey dış raylar ile alt iki yatay ray, sağdaki porselen eteğini çevreler.

**İlk karar:** Kayan yatay lambaları mı, sağdaki dik rayı mı önce tamamlayacağını seç.

**Açılınca:** Bir raf tamamen bittiğinde bütün çerçevesi açılır; porselenin altında çamur işi sürer.

**Kalan iş:** Dört renkli çerçeve ve iki aşamalı zemin kalır; yalnız ilk rafı açmak yeterli olmaz.

Hedefler: 4 Lamba çerçevesi, 12 Porselen, 12 Çamur.

Açılışta 58 normal yuva: 58 doğal dolum, 0 gerçek gölge pini. Pin indexleri: []. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 3, kırpılmış siluette 233. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
