# Wardrobe içeriklerinin daha geniş fırlaması · 2026-10-23

Kullanıcı içeriklerin daha geniş alana fırlatılmasını istedi. İçeriklerin yatay başlangıç hızı90→260, yukarı başlangıç hızı260→420 yerel UI birimi/sn oldu. Yatay hızda±%15, yukarı hızda±%10 başlangıç farkı farklı yaylar oluşturur; mevcut rastgele sağ/sol seçimi ve dönüş korunur.

0,65sn süre ve1800 yerçekimi aynı. Analitik yatay mesafe58,5 yerine yaklaşık143,7–194,4 yerel UI birimi; nominal yükselme18,8→49 birim. Bunlar fizik formülünden tahmindir, Unity/telefon ölçümü değildir. Son kısımdaki fade, küçülme, hedef sayımı, kapı animasyonu ve `RecommendedClearDestroyDelay` değişmedi. Canlı sahne prefab fallback kullandığı için yeni varsayılanlar geçerli; ayrı serialized eski hız override'ı bulunmadı.

Mevcut içerik Image'ı hareket ettirilir; yeni ghost/texture/material/VFX veya kare başına tahsis/tarama eklenmedi. İki Random.Range yalnız atış başlangıcındadır. Mevcut coroutine/yerel item oluşturma maliyeti sürer; item bu yolda havuza dönmez, sonunda Destroy edilir. Yeni havuz sistemi eklenmedi. Dolap root'u yok edildiğinde Unity child item'ları ve ona ait coroutine'leri de kaldırır; son item için mevcut spawner bekleme süresi aynı kalır. Cihaz performansı ölçülmedi.

Tam `WardrobeObstacleView` kurulu Unity UI metadata'sına bağlandı; syntax/API kontrolü geçti, assembly üretilmedi. İlgili `git diff --check` geçti. Unity Play/build veya görsel cihaz testi yapılmadı. Önceki kaynak `Before/` içinde korunur.
