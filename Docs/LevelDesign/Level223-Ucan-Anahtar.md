# 223 · Uçan Anahtar / Flying Key

İnceleme taslağı · 2026-11-05 · Normal · 23 hamle · 9×7 · 45 aktif hücre · dört normal renk.

Asset: `Assets/_Project/Settings/ProLevels/LevelV1_00002230.asset`. Aktif katalog: `LevelCatalogProV1.asset`, `LevelCL_0223`. TR/EN isim ürün verisine eklendi.

- **İlk bakış:** Yatay anahtar silueti: solda yuvarlak baş, sağda iki diş ve uçtaki bitişik 2×2 anahtar makineleri.
- **İlk karar:** Sağdaki porselen şaftı indirip makinelerin üst yüzünü mü açacaksın, soldaki oyma bloğunu mu çözeceksin?
- **İlk katman açılınca:** Şaft açılınca makinelerle seri üretim başlar; sol baştaki 2×3 oyma bloğu kalkınca çamurlu taban görünür.
- **Sonraki iş:** 14 anahtarı kendi aralarında eşleştir/special ile temizle; oyma bloğunun altındaki 6 çamuru bitir.

Hedefler: **14 Anahtar · 8 Porselen · 6 Oyma taşı · 6 Çamur**. Hedef sayısı 4; kaynak ve ürün aynı anda hedeflenmez.

27 başlangıç taşı yuvası; sınırlı baş ve iki sıra şaftta special hizalama gerekir. 23 hamle ilk tahmin. Hamle bütçesi erişim, katman sırası, özel güç alanı ve üretim/toplama işiyle seçildi; oynanmış denge değildir.

Başlangıçta 27 taş yuvası, 25 doğal beslemeli hücre var. Renk pini ve hazır special yok. Başlangıçtaki 2 boş yuva, açılabilir engel gölgesidir; önizlemede noktalı gösterilir. Son açılabilir katmanlar kalkınca tüm normal yuvalar beslenir. Kalıcı kaynak grupları alt sınıra dayanır.

Bu levelde Dimerdal/elmas yok; mevcut KeyGenerator anahtarları kullanılır. Sonraki Dimerdal seviyeleri için geçerli mekanik notu: Elmaslar normal taş katmanındadır: takas, düşüş, yana kayma, kendi rengindeki elmaslarla eşleşme/special ile temizlenme. Komşu normal eşleşme elması kırmaz. Üretim yalnız hareketsiz taşın yerine yerleşir. Dimerdal renkleri ortak elmas kotasını paylaşır; bütün kutuları açmak veya her renkten belirli sayıda üretmek zorunlu değildir.

Alternatiflerden seçim:
- Yeni bir Dimerdal rengi daha: tekniği tekrar etmek yerine eski anahtar davranışıyla görsel ilişki kuruldu.
- Dik anahtar deliği: 222 ile art arda uzun dar sap tekrarını güçlendirirdi.

**Statik doğrulama:** Unity YAML, dizi boyu, maske bağlantısı, footprint/origin, sıvı-alt/çim-üst sırası, hedef miktarı/ikon, kalıcı kaynak erişimi, dolum kesitleri, isim/katalog/GUID ve kapsam dışı hash koruması geçti.

**Agent görsel değerlendirmesi:** Güncel proje sprite'larıyla açılış ve kesit PNG'si açılarak incelendi. Bu değerlendirme kullanıcı onayı değildir. Serbest taş renkleri temsili; sağ kesit oynanmış an değildir. Hareketli porselenlerin başlangıç şekli oyun sırasında değişir.

**Oynanmış denge:** Yok. Unity build/Play, cihaz, bot veya gravity/cascade simülasyonu çalıştırılmadı. FPS/GC/kazanma oranı ölçülmedi. Yayın veya release baseline yok. Yeni runtime/VFX kodu eklenmedi.

Önizleme: [Level223-Ucan-Anahtar.png](Level223-Ucan-Anahtar.png). Batch: [README](Batches/222-225-Crystal-Shapes/README.md).
