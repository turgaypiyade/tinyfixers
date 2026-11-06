# 222 · Zümrüt Yaprak / Emerald Leaf

**2026-11-05 bağımsız kutu adetleri:** 10. Her kutu kendi adedini üretip son elması yerleşince ayrı kırılır. Editör bu adetlerden ortak mor Diamond hedefini otomatik hesaplar. Yerleşim/hamle/diğer hedefler aynı.

**2026-11-05 hedef ikonu:** Ortak Diamond hedefi kullanıcının mor elmasıyla gösterilir. Diğer renklerin toplamını temsil eder; hamle/hedef adedi/yerleşim ve tahtadaki renkler aynı. Güncel PNG yeniden üretildi.

İnceleme taslağı · 2026-11-05 · Normal · 24 hamle · 9×11 · 60 aktif hücre · dört normal renk.

Asset: `Assets/_Project/Settings/ProLevels/LevelV1_00002220.asset`. Aktif katalog: `LevelCatalogProV1.asset`, `LevelCL_0222`. TR/EN isim ürün verisine eklendi.

- **İlk bakış:** Sağa uzanan yaprak silueti; ortadaki kesintisiz çapraz çim damarı ve sapı tamamlayan yeşil vitrin.
- **İlk karar:** Çim damarının dış yüzünden mi ilerleyeceksin, sapın üstünde yeşil eşleşme mi hazırlayacaksın?
- **İlk katman açılınca:** Çim kalktıkça yaprağın iki yüzü tek eşleşme alanına dönüşür; yeşil kepenk için daha çok hizalama açılır.
- **Sonraki iş:** Yeşil vitrini aç, toplam 10 elması üret ve aynı renk elmas eşleşmeleri/special ile ayrıca topla.

Hedefler: **10 Elmas · 16 Çim**. Hedef sayısı 2; kaynak ve ürün aynı anda hedeflenmez.

221’in 8 elmasına karşı 10 elmas + 16 çim. Tek yeni renk varyantı; 24 hamle ilk tahmin. Hamle bütçesi erişim, katman sırası, özel güç alanı ve üretim/toplama işiyle seçildi; oynanmış denge değildir.

Başlangıçta 56 taş yuvası, 56 doğal beslemeli hücre var. Renk pini ve hazır special yok. Başlangıçtaki 0 boş yuva, açılabilir engel gölgesidir; önizlemede noktalı gösterilir. Son açılabilir katmanlar kalkınca tüm normal yuvalar beslenir. Kalıcı kaynak grupları alt sınıra dayanır.

Elmaslar normal taş katmanındadır: takas, düşüş, yana kayma, kendi rengindeki elmaslarla eşleşme/special ile temizlenme. Komşu normal eşleşme elması kırmaz. Üretim yalnız hareketsiz taşın yerine yerleşir. Dimerdal renkleri ortak elmas kotasını paylaşır; bütün kutuları açmak veya her renkten belirli sayıda üretmek zorunlu değildir.

Alternatiflerden seçim:
- İki renkli simetrik vitrin: 221 kaidesini gereksiz tekrar eder.
- Tam dikdörtgen yeşil bant: yaprak silueti daha belirgin bir ilk bakış sağlar.

**Statik doğrulama:** Unity YAML, dizi boyu, maske bağlantısı, footprint/origin, sıvı-alt/çim-üst sırası, hedef miktarı/ikon, kalıcı kaynak erişimi, dolum kesitleri, isim/katalog/GUID ve kapsam dışı hash koruması geçti.

**Agent görsel değerlendirmesi:** Güncel proje sprite'larıyla açılış ve kesit PNG'si açılarak incelendi. Bu değerlendirme kullanıcı onayı değildir. Serbest taş renkleri temsili; sağ kesit oynanmış an değildir. Hareketli porselenlerin başlangıç şekli oyun sırasında değişir.

**Oynanmış denge:** Yok. Unity build/Play, cihaz, bot veya gravity/cascade simülasyonu çalıştırılmadı. FPS/GC/kazanma oranı ölçülmedi. Yayın veya release baseline yok. Yeni runtime/VFX kodu eklenmedi.

Önizleme: [Level222-Zumrut-Yaprak.png](Level222-Zumrut-Yaprak.png). Batch: [README](Batches/222-225-Crystal-Shapes/README.md).
