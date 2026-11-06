# 225 · Mühürlü Kristal Kalesi / Sealed Crystal Keep

**2026-11-05 bağımsız kutu adetleri:** 8+8 = 16. Her kutu kendi adedini üretip son elması yerleşince ayrı kırılır. Editör bu adetlerden ortak mor Diamond hedefini otomatik hesaplar. Yerleşim/hamle/diğer hedefler aynı.

**2026-11-05 hedef ikonu:** Ortak Diamond hedefi kullanıcının mor elmasıyla gösterilir. Diğer renklerin toplamını temsil eder; hamle/hedef adedi/yerleşim ve tahtadaki renkler aynı. Güncel PNG yeniden üretildi.

## Güncel v2 — alt besleme geçişleri · 2026-11-05

Kullanıcı genel görünümü beğendi, yalnız alttaki büyük boş bölgeye taş akışı istedi. Diğer 222–224 levelları korundu. Bu düzeltme oynanmış denge onayı değildir.

**8×11 / 88 aktif / 25 hamle Hard.** Hedefler aynı: **16 elmas · 1 mühür · 16 special-only pusula taşı · 16 çamur**. Mühür aynı 4×4 ve 9/9/9 renk sayacında; çamur altta, mühür üstte. GUID/meta, isim, katalog ve bütün sayaçlar aynı.

- Yanlardaki 16 pusula taşı iki 1×8 kesintisiz dış sütuna toplandı.
- İçlerinde iki açık besleme şeridi bırakıldı (0 tabanlı x=1 ve x=6).
- Yeşil ve sarı 2×2 Dimerdal alt köşelere taşındı; araları dört normal hücre genişliğinde.
- **Alt iki sıranın sekiz normal yuvası başlangıçta doğal olarak doluyor.** Başlangıçta beslenen normal yuva 36→46; boş yuva 12→2. Kalan iki boş yuva sadece kapalı mührün hemen altında; mühür kalkınca onlar da besleniyor. Pin/hazır special yok.

Dört tasarım sorusu:

1. İlk bakış: İki uzun pusula suru, ortada 4×4 mühürlü avlu, alt köşelerde kristal kapıları ve açık besleme yolları.
2. İlk karar: Yan şeritlerden alt çalışma alanına in; mührün renk sırasını ilerletirken surlar için special çizgileri hazırla.
3. İlk katman açılınca: Alt iki sıra başlangıçtan beslenir. Mühür açılınca 16 çamur hücresi ve altındaki son iki boş yuva da açılır.
4. Kalan iş: 16 pusula taşını ve iki kat çamuru temizle; köşelerdeki kapılardan toplam 16 elması üretip ayrıca topla.

## Dimerdal geri bildirimi

Dimerdal kabul edilen açılış/üretim vuruşlarında kısa büyüme, silkelenme ve iç parlama verir; dört dilimin açılma hareketi sürer. Kullanıcının son yönüyle **ortak üretim kotasının son elması tahtaya yerleşince bütün görünür kutular büyük parçalara ayrılıp kaldırılır**. Footprint normal temizleme yoluyla açılır; alt katman ve örtü korunur. Örtülü kutu açılınca emekli olur. Rezervasyon, iptal veya başarısız uçuş erken kırmaz. Önceki gri/%45 opak kapanış kaldırıldı. Toplama ve HUD varışta sayım değişmedi.

## Kontrol sınırları

225 v2'nin statik YAML/array, alt besleme ve gerçek sprite PNG incelemesi korunuyor; bu mekanik revizyonunda hiçbir level asseti, katalog veya isim değiştirilmedi. Önceki 3.391 dosyalı v2 runtime snapshot'ı o teslim anına aittir; güncel runtime kapsamı ve doğrulama `Docs/Checks/Dimerdal/Exhaustion-Break/` içindedir. Güncel izole kontroller üretim, son yerleşim, footprint/katman, örtüden açılma, reentrant kayıt, iptal ve havuz temizliğini gerçek C# yöntemleriyle kontrol eder.

Unity build/Play, cihaz, bot veya gravity/cascade simülasyonu çalıştırılmadı. Oyun dengesi ve cihaz CPU/GC/FPS ölçülmedi. Yeni ses yolu/ses kaynağı veya düşüş motoru değişikliği, yayın veya release baseline yok.

[Önizleme](Level225-Muhurlu-Kristal-Kalesi.png) · [Revizyon kaydı](Batches/222-225-Crystal-Shapes/Revisions/v2-lower-feed/README.md)
