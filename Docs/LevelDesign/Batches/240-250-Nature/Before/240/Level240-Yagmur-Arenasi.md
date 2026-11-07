# 240 · Yağmur Arenası / Rain Arena

9×10, 90 aktif hücre; **24 hamle · Boss Duel**. 4 renk. Oyuncu hedefi: Boss ayrı değerlendirilir; kalibre insan geçiş oranı değildir.

[Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002400.asset) · [Önizleme](Level240-Yagmur-Arenasi.png) · [Batch](Batches/235-250-Late-Game/README.md)

Geniş düello alanı; iki alt su deposu ve çift katmanlı geniş oyma ayaklar.

**İlk karar:** Specialı rakibe büyük hasar için mi, su deposunu erkenden açıp kurutma işi başlatmak için mi kullanacaksın?

**Açılınca:** Depo kırılınca rastgele gerçek su birikintileri doğar; önceki oyma işi onları otomatik bitirmez.

**Kalan iş:** Üç rakip, iki depo, yeni birikintiler ve kenar çamuru birlikte tamamlanır.

Hedefler: 900 Boss HP, 2 Su deposu, 16 Su birikintisi, 16 Oyma taşı, 16 Çamur.

Açılışta 66 normal yuva: 64 doğal dolum, 2 gerçek gölge pini. Pin indexleri: [54, 62]. Pinin altında aktif, başlangıçta dolu destek hücresi kontrol edildi; tüm açılabilir engeller kalkınca kalıcı alt kaynaklar yerinde kalsa da kalan bütün normal yuvalar beslenir. Bu statik erişim kontrolüdür; doğal düşüşün tam bir tahmini değildir.

Karşılaştırma kısa listesinde konumsal en yakın 172, kırpılmış siluette 152. Benzerlik puanları özgünlük/onay yüzdesi değildir. Kenar/kolon ve dikdörtgen gruplar ortak görsel dildir; ilk karar ve açılan iş ayrıca incelendi. 245, 238 ile fazla benzer kanatlar nedeniyle teslim öncesi L kaidesine çevrildi.

Gerçek sprite önizlemesi incelendi. Sağ kesit oynanmış bir an değildir; normal renkler temsili, kanal izi şematiktir. YAML/dizi/origin/footprint/katman/hedef/ikon/katalog/TR–EN isim ve statik refill kontrolleri `validation.json` içindedir. Simülasyon kanıtları batch raporunda ayrıca verilir; kullanıcı manuel oyun ve editör onayı henüz yoktur.
Su birikintisi hedefi dinamik olarak yaşayan su hücrelerinden yeniden sayılır. İki depo × mevcut 8 damla başlangıç gösterimi16’dır; kaynaklar açıldıktan sonraki gerçek yerleşim/çakışmalar sayaçta takip edilir. En fazla beş hedef sınırına bu slot baştan dahildir.

Yeni runtime/VFX veya obstacle-local ses kaynağı eklenmedi. Mevcut TileView/UI efekt havuzları ve mevcut servisler kullanılır; bu veri değişikliği FPS/GC iyileşmesi iddiası taşımaz. UI kontrolü, yeni Unity build’i, yayın veya release baseline yapılmadı.
