# 249 · Son Köprü / Last Bridge

9×11, 97 aktif hücre; **22 hamle · Super Hard**. 4 renk. Oyuncu hedefi: %30 yönü; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002490.asset) · [Önizleme](Level249-Son-Kopru.png) · [Batch](Batches/235-250-Late-Game/README.md)

Karşılıklı dolap galerileri altta C biçimli uzun mıknatıs yoluna bağlanır; orta köprü pusulalıdır.

**İlk karar:** Önce dolap yüzlerini mi açacaksın, mıknatısın hangi ucunu yaklaşma yoluna sokacaksın?

**Açılınca:** Pusula köprüsü special ile kalkınca çamur açılır; dolap kapaklarının arkasında içerikler kalır.

**Kalan iş:** Dört dolap, uzun mıknatısın iki ucunun buluşması ve köprü zemini ayrı zorunlu işlerdir.

Hedefler: 4 Dolap, 1 Mıknatıs, 12 Pusula, 12 Çamur.

Açılışta 53 normal yuva: 46 doğal dolum, 7 gerçek gölge pini. Pin indexleri: [63, 64, 71, 76, 82, 83, 84]. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 197, kırpılmış siluette 75. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
