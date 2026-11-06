# 224 · Kristal Çapa / Crystal Anchor

**2026-11-05 bağımsız kutu adetleri:** 6+6 = 12. Her kutu kendi adedini üretip son elması yerleşince ayrı kırılır. Editör bu adetlerden ortak mor Diamond hedefini otomatik hesaplar. Yerleşim/hamle/diğer hedefler aynı.

**2026-11-05 hedef ikonu:** Ortak Diamond hedefi kullanıcının mor elmasıyla gösterilir. Diğer renklerin toplamını temsil eder; hamle/hedef adedi/yerleşim ve tahtadaki renkler aynı. Güncel PNG yeniden üretildi.

İnceleme taslağı · 2026-11-05 · Normal · 24 hamle · 8×11 · 88 aktif hücre · dört normal renk.

Asset: `Assets/_Project/Settings/ProLevels/LevelV1_00002240.asset`. Aktif katalog: `LevelCatalogProV1.asset`, `LevelCL_0224`. TR/EN isim ürün verisine eklendi.

- **İlk bakış:** Düz tahtanın içinde engeller bir çapa çizer: brandalı tepe halkası, ahşap gövde/kuşak, mavi-kırmızı kristal uçlar.
- **İlk karar:** Üstteki dört çimi kaldırıp heykelin mavi etiketini mi başlatacaksın, altta iki renkli vitrinin yüzlerini mi açacaksın?
- **İlk katman açılınca:** Ahşap çapa çözülürken yatay vuruş alanları genişler; çim kalkınca heykel mavi taş toplamaya başlar.
- **Sonraki iş:** Heykelin 8 mavi taşından sonra üç special isabeti ve ortak hedefte 12 elması ayrıca toplama işi kalır.

Hedefler: **12 Elmas · 34 Ahşap · 1 Heykel · 4 Çim**. Hedef sayısı 4; kaynak ve ürün aynı anda hedeflenmez.

34 tek vuruş ahşap, örtü sonrası 8 mavi + 3 special heykel işi, 12 elmas. 24 hamle ilk tahmin. Hamle bütçesi erişim, katman sırası, özel güç alanı ve üretim/toplama işiyle seçildi; oynanmış denge değildir.

Başlangıçta 42 taş yuvası, 38 doğal beslemeli hücre var. Renk pini ve hazır special yok. Başlangıçtaki 4 boş yuva, açılabilir engel gölgesidir; önizlemede noktalı gösterilir. Son açılabilir katmanlar kalkınca tüm normal yuvalar beslenir. Kalıcı kaynak grupları alt sınıra dayanır.

Elmaslar normal taş katmanındadır: takas, düşüş, yana kayma, kendi rengindeki elmaslarla eşleşme/special ile temizlenme. Komşu normal eşleşme elması kırmaz. Üretim yalnız hareketsiz taşın yerine yerleşir. Dimerdal renkleri ortak elmas kotasını paylaşır; bütün kutuları açmak veya her renkten belirli sayıda üretmek zorunlu değildir.

Alternatiflerden seçim:
- Çapa biçimli dış maske: alt uçların eşleşme alanını gereksiz küçültürdü.
- Üç vuruş oyma çapa: normal 224’ü sadece yüksek vuruş yüküyle Hard’a yaklaştırırdı.

**Statik doğrulama:** Unity YAML, dizi boyu, maske bağlantısı, footprint/origin, sıvı-alt/çim-üst sırası, hedef miktarı/ikon, kalıcı kaynak erişimi, dolum kesitleri, isim/katalog/GUID ve kapsam dışı hash koruması geçti.

**Agent görsel değerlendirmesi:** Güncel proje sprite'larıyla açılış ve kesit PNG'si açılarak incelendi. Bu değerlendirme kullanıcı onayı değildir. Serbest taş renkleri temsili; sağ kesit oynanmış an değildir. Hareketli porselenlerin başlangıç şekli oyun sırasında değişir.

**Oynanmış denge:** Yok. Unity build/Play, cihaz, bot veya gravity/cascade simülasyonu çalıştırılmadı. FPS/GC/kazanma oranı ölçülmedi. Yayın veya release baseline yok. Yeni runtime/VFX kodu eklenmedi.

Önizleme: [Level224-Kristal-Capa.png](Level224-Kristal-Capa.png). Batch: [README](Batches/222-225-Crystal-Shapes/README.md).
