# 183 — Fenerli Bent: tekrar ve akış incelemesi

1–182 katalog üzerinden tarandı. [Sprite karşılaştırması](Comparison.png), [benzerlik kısa listesi](Similarity.json). Skorlar normalize statik engel kütlesini karşılaştırır; yenilik yüzdesi ve denge sonucu değildir. Yeni kanal taş altında olduğundan solid-blocker skorunda bariyer sayılmaz; bağlantı yolu ayrıca incelenir.

- **67 Hassas Sevkiyat:** kenar boyunca hareketli sütunlar ve alt çıkışlarla benzer çerçeve yönü. 183'te kargo çıkarmak yerine hücreleri kaynağa bağlı su yolunda açma hedefi var; tek kenar fener, tek alt set.
- **15 Kızıl Hat:** iki yan şerit ve küçük yatay sandık bandı. 183 tam dikdörtgen, bir fener sütunu ve alt kenara bağlı set; orta eşleşme alanından kıvrımlı kanal geçiyor.
- **182 Fener Şenliği:** fener sütunu + blok görsel grameri ortak. 183'ün 99 hücrelik tam boardu, taş-altı kanal bağlantısı ve taş seti yeni açılma kararını oluşturur. Salt yerleşimin bütünüyle benzersiz olduğu iddia edilmez; tek sütun bilinçli kullanıcı tercihi.

Kanal açıldığında kaynak dışındaki hücreler normal kalır; eski pass-through planına göre board ikiye bölünmüş sayılmaz. Harita ve açılış sprite'lı PNG incelendi. Yeni editör path verisi analiz aracına eklendi; mıknatıs ve kanalın aynı `pathCellIndices` alan adı farklı YAML bölümlerinden okunur, kanal yanlışlıkla mıknatıs sayılmaz.

Sonuç bir tasarım değerlendirmesidir; kullanıcı onayı ve gerçek kazanma oranı yoktur.
