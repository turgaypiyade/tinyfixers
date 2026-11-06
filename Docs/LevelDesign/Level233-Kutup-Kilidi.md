# 233 — Kutup Kilidi / Polar Lock

Aktif ProLevels V1; `LevelCL_0233`, chapter10. 8×11,82aktif hücre,4renk,**27hamle Normal**. Hamle sayısı ilk tasarım ayarıdır; oynanmış denge ölçümü değildir. Çim42/GrassFlower58/GrassWaterLily69 yok.

| Hedef | Miktar | Açılıştaki karşılığı |
| --- | ---: | --- |
| Kargo34 | 8 | İki kasanın altında dörder gerçek kargo |
| Safe33 | 2 | Bitişik iki2×2 kasa |
| Magnet29 | 1 | Tek12hücrelik U yolu;iki uç,bir hedef |
| SculptingSpecial39 | 8 | Dört sütun×iki sıra çıkış bandı |
| HelmetPorcelain30 | 12 | Dış kenara bağlı iki6hücrelik sütun |

Üst çalışma avlusu,alt kapaklar ve U çıkış hattı tek bir kilit kompozisyonu oluşturur. Soldaki kasa kırmızı→sarı→yeşil,sağdaki yeşil→sarı→kırmızı ilerler. İlk aktif kilit2,isabetleri kalan kilitler1’er ister. Normal eşleşme sadece sıradaki rengini ilerletir;special aktif kilide joker isabettir. İki kasa da kendi dört kargosunu saklar.

İlk seçim: kasalardan birini açmak veya açık yan yüzlerden mıknatıs uçlarını ilerletmek. Kasayı açmak teslimatı tamamlamaz. Kargoların dört düz sütununun altında önce special-only pusulalar,sonra U yolunun tabanı kalır. U orta hücreleri vuruş almaz;yalnız güncel uçlar ilerler.12hücrelik yol11uç ilerlemesiyle biter. Dikey roketler yukarıdaki çalışma alanından aynı kargo sütunlarındaki pusulalara erişebilir. Normal eşleşmeler pusulayı kaldırmaz;kargo diagonal kaymaz.

Başlangıç42/42normal yuva doğal doluma bağlı;pin,hazır special veya boş cep yok. Çıkış katmanları ve kargolar kalkınca alan42→50→62→74→82normal yuvaya açılır. Kasalar hâlâ kapalıyken alttaki pusula bandı önce temizlenirse sekiz hücre geçici kasa gölgesinde kalır;kasalar açılınca hepsi beslenir. Bu,kalıcı akışsız cep değildir ve seçim sırasının bir sonucudur.

Refill special koruması yeni233için otomatik(-1) bırakıldı;güncel101+%30–70kuralını ve mevcut kayıp sonrası rahatlamayı kullanır. Şablondaki232’nin elle verilmiş0.75ayarı233’e kopyalanmadı;232dekorundu.

En güçlü erken zincir değerlendirmesi: doğrudan verilmiş global yardımcı yok. Büyük bir combo bazı kilitleri,pusulaları ve güncel mıknatıs uçlarını birlikte ilerletebilir. Mıknatısın ortasına alan hasarı yapmak bütün yolu bitirmez;kasadan yeni açılan içerik de mevcut reveal korumasıyla korunur. Çoklu vuruşlu kombolar tasarımı kısaltabilir;garanti minimum hamle veya kazanma oranı iddia edilmez.

232veönceki seviyeler korunur;232’nin mevcut32hamle/5+5+5elmas ayarı eski taslağa geri alınmadı. Çalışma sırasında dışarıdan değişen108de korunup ayrıca kaydedildi. Katalog ve TR/EN isimlere yalnız233eklendi.

Gerçek sprite PNG açılışı ve örtü kesiti incelendi. Taş renkleri temsilidir;kesit düşmüş kargo konumu göstermez.232önceki katalog düzeneği tarandı;142alt kütle/iki kasa bakımından benzer,198mıknatıs ailesi bakımından karşılaştırıldı.233’ün farkı iki kasa içeriğinin tek mıknatıslı kargo çıkışına bağlanmasıdır;şeklin benzersiz olduğu iddia edilmez.

Statik YAML,dizi,katman/footprint,renk/hedef,kapalı-açık besleme,porcelen desteği,kargo sütunları,GUID ve koruma kontrolleri geçti. LevelEditor’de mevcut Safe/Magnet/Cargo/Porcelain/SculptingSpecial paletleri ve boyut/yol/erase,Goals/PreLevel ikon yolları kullanılır;yeni runtime davranışı yok.

Performans: yeni Update,düşüş motoru,VFX veya ses kodu yok. Mevcut mıknatıs görsel/kafes yollarında geçici nesneler/listeler ve parça efektleri vardır;tamamen havuzlu olduğu veya yeni FPS kazancı iddia edilmez. Safe ve merkezi sesin mevcut teardown/ses ayarı yolları korunur. UnityUI/Play/build,bot-kaskat simülasyonu,cihaz testi,yayın veya releasebaseline yok. Manuel oyun testi kullanıcıdadır.

Kayıt:[233-Polar-Lock](Batches/233-Polar-Lock/README.md). Önizleme:[Level233-Kutup-Kilidi.png](Level233-Kutup-Kilidi.png).
