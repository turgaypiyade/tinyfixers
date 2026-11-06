# 221 · Mavi Vitrin / Blue Showcase

**2026-11-05 bağımsız kutu adetleri:** 4+4 = 8. Her kutu kendi adedini üretip son elması yerleşince ayrı kırılır. Editör bu adetlerden ortak mor Diamond hedefini otomatik hesaplar. Yerleşim/hamle/diğer hedefler aynı.

**2026-11-05 hedef ikonu:** Ortak Diamond hedefi kullanıcının mor elmasıyla gösterilir. Diğer renklerin toplamını temsil eder; hamle/hedef adedi/yerleşim ve tahtadaki renkler aynı. Güncel PNG yeniden üretildi.

Mavi Dimerdal'ın ilk tanıtımı. Normal seviye; rahatlık amaçlı ilk deneme ayarı **24 hamle, 8 elmas**. Oynanmış denge veya başarı oranı değildir.

8×10 siluetin üst köşeleri kademeli açılır, gövdesi genişler ve alt kenarı iki bitişik 2×2 Dimerdal kutusuyla bir vitrin kaidesi oluşturur. Ortada açıklamasız tekil engel adası yoktur. 68 aktif hücrenin 60'ı normal taş çalışma alanıdır; kalıcı kutular tabanı tamamladığı için aşağıda beslemesi kapanan bir cep kalmaz.

- Yalnız **DimerdalBlue2x2 (ID85)**: origin66 / `(2,8)` ve origin68 / `(4,8)`; koordinatlar sol-üst başlangıçlı, sıfır tabanlıdır. Diğer Dimerdal renkleri ve başka engel aileleri kullanılmaz. Normal taş havuzu dört renktir.
- Hedef yalnız **Collectible / Diamond (4), amount8**. İkon mavi elmasa açıkça atanmıştır; HUD ve PreLevel için mevcuttur. Kırılmayan kutulara imkânsız bir kırma hedefi verilmez.
- Altı normal renk pini, `(3,6)` mavi taşı `(3,7)` sarı taşla değiştirince `(2..4,7)` üzerinde tam üçlü fırsatı sunar. İki uçtaki kırmızı/yeşil pin, hazırlanmış hamleyi zorunlu dört eşleşmeye büyütmez. Hazır special veya önceden boarda konmuş elmas yoktur.
- Önce dört uygun renk veya special vuruşuyla kepenkler açılır. Sonra kutunun yanındaki mavi eşleşme veya kutuya special/joker vuruşu bir mavi elmas üretir; oyuncu elması komşu eşleşme veya special ile ayrıca toplar. Aynı üçlü iki kutuya değse de tek elmas üretir.

Önizlemede solda açılış, sağda yalnız kepenk durumunun değiştirilmiş statik açık kesiti, altta 0–4 hit dilimleri vardır. Serbest renkler temsili, altın noktalar gerçek altı pini işaretler. Bu kesit bir hamle/kaskat simülasyonu değildir.

Aktif katalog: `ProLevels/LevelCatalogProV1.asset`, `LevelCL_0221`, `LevelV1_00002210.asset`. TR/EN adlar `Resources/LevelNames.json` içindedir.

[Önizleme](Level221-Mavi-Vitrin.png) · [Yazıcı / doğrulama](Batches/221-Dimerdal-Intro/) · [Tekrar incelemesi](Reviews/221-Dimerdal-Intro/)

Kullanıcı görsel onayı: “güzel kurdun oyunu beğendim”. Onaylı düzen/hamle/hedef/pin korunur. Runtime üretim ve toplama güncellemesi `Docs/Checks/Dimerdal/README.md` içinde; yeni akış cihazda tekrar denenmedi.

Statik kontroller: tüm diziler, 2×2 origin bölgeleri, tek renk, hedef/ikon, altı pin, ilk üçlünün geometrisi, 60 normal hücrenin doğal kaynak yolu ve önceki seviyelerin/runtime'ın hash korunumu geçti. Unity Play/build, cihaz dengesi, bot/yerçekimi/kaskat simülasyonu veya yayın yapılmadı. 222 bu isteğin kapsamında değildir.
