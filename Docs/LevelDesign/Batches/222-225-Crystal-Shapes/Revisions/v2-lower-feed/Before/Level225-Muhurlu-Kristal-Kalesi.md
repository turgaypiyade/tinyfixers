# 225 · Mühürlü Kristal Kalesi / Sealed Crystal Keep

İnceleme taslağı · 2026-11-05 · Hard · 25 hamle · 8×11 · 88 aktif hücre · dört normal renk.

Asset: `Assets/_Project/Settings/ProLevels/LevelV1_00002250.asset`. Aktif katalog: `LevelCatalogProV1.asset`, `LevelCL_0225`. TR/EN isim ürün verisine eklendi.

- **İlk bakış:** Geniş tahtada iki 2×4 pusula suru, ortada 4×4 mühürlü avlu, altta bitişik yeşil ve sarı kristal kapıları.
- **İlk karar:** Üst çalışma alanında mührün kırmızı/sarı/yeşil sırasını ilerletirken surlara ulaşacak special çizgilerini sakla.
- **İlk katman açılınca:** Mühür açılınca 16 hücrelik çamur avlusu oynanır olur; pusula surlarını yalnız special açar ve yan alt ceplere besleme verir.
- **Sonraki iş:** Pusula surları ile iki kat çamuru temizlerken kapıları açıp 16 elması üret/topla; açılış tek başına hedefleri bitirmez.

Hedefler: **16 Elmas · 1 Mühür · 16 Pusula taşı · 16 Çamur**. Hedef sayısı 4; kaynak ve ürün aynı anda hedeflenmez.

Hard: 27 sıralı renk toplama, 16 special-only sur hücresi, 16 iki kat çamur ve 16 elmas. 25 hamle ilk tahmin. Hamle bütçesi erişim, katman sırası, özel güç alanı ve üretim/toplama işiyle seçildi; oynanmış denge değildir.

Başlangıçta 48 taş yuvası, 36 doğal beslemeli hücre var. Renk pini ve hazır special yok. Başlangıçtaki 12 boş yuva, açılabilir engel gölgesidir; önizlemede noktalı gösterilir. Son açılabilir katmanlar kalkınca tüm normal yuvalar beslenir. Kalıcı kaynak grupları alt sınıra dayanır.

Elmaslar normal taş katmanındadır: takas, düşüş, yana kayma, kendi rengindeki elmaslarla eşleşme/special ile temizlenme. Komşu normal eşleşme elması kırmaz. Üretim yalnız hareketsiz taşın yerine yerleşir. Dimerdal renkleri ortak elmas kotasını paylaşır; bütün kutuları açmak veya her renkten belirli sayıda üretmek zorunlu değildir.

Alternatiflerden seçim:
- Dört renkli bütün Dimerdal’lar: hedef elmaslarını aşırı böler, fazladan kaynaklar ayrı karar üretmez.
- Çiçek/çimle beşinci hedef: mühür önüne yeni kapak eklemek yerine dört anlamlı iş korundu.

**Statik doğrulama:** Unity YAML, dizi boyu, maske bağlantısı, footprint/origin, sıvı-alt/çim-üst sırası, hedef miktarı/ikon, kalıcı kaynak erişimi, dolum kesitleri, isim/katalog/GUID ve kapsam dışı hash koruması geçti.

**Agent görsel değerlendirmesi:** Güncel proje sprite'larıyla açılış ve kesit PNG'si açılarak incelendi. Bu değerlendirme kullanıcı onayı değildir. Serbest taş renkleri temsili; sağ kesit oynanmış an değildir. Hareketli porselenlerin başlangıç şekli oyun sırasında değişir.

**Oynanmış denge:** Yok. Unity build/Play, cihaz, bot veya gravity/cascade simülasyonu çalıştırılmadı. FPS/GC/kazanma oranı ölçülmedi. Yayın veya release baseline yok. Yeni runtime/VFX kodu eklenmedi.

Önizleme: [Level225-Muhurlu-Kristal-Kalesi.png](Level225-Muhurlu-Kristal-Kalesi.png). Batch: [README](Batches/222-225-Crystal-Shapes/README.md).
