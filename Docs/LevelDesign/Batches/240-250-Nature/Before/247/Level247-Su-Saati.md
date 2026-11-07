# 247 · Su Saati / Water Clock

9×11, 99 aktif hücre; **16 hamle · Normal**. 4 renk. Oyuncu hedefi: %40–50; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002470.asset) · [Önizleme](Level247-Su-Saati.png) · [Batch](Batches/235-250-Late-Game/README.md)

İki uzun su yolu, dipte iki depo ve orta fener sarkacı; yan ayaklar aynı gövdeyi tamamlar.

**İlk karar:** Kanal bağlantısını tamamlamakla depoyu kırıp sonradan doğacak suyu başlatmak arasında seçim yap.

**Açılınca:** Yan oyma kalkınca depo yüzü açılır. Depo suyu kanaldan farklı bir temizleme işidir.

**Kalan iş:** İki kanal, iki depo, gerçek su birikintileri ve fenerler; başlangıçtan en fazla beş hedef.

Hedefler: 2 Su deposu, 16 Su birikintisi, 2 Su kanalı, 8 Oyma taşı, 12 Fener.

Açılışta 71 normal yuva: 71 doğal dolum, 0 gerçek gölge pini. Pin indexleri: []. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 125, kırpılmış siluette 242. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.
Su birikintisi hedefi dinamik olarak yaşayan su hücrelerinden yeniden sayılır. İki depo × mevcut 8 damla başlangıç gösterimi16’dır; kaynaklar açıldıktan sonraki gerçek yerleşim/çakışmalar sayaçta takip edilir. En fazla beş hedef sınırına bu slot baştan dahildir.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
