# Level 106 — Çiçekli Kelebek

Global seviye 106, `LevelCatalogPro` üzerinden `LevelP_001130.asset` dosyasına bağlıdır.
105, `LevelP_001120.asset` olarak kalır. 106'nın eski `LevelP_testt` dosyası korunmuştur.

## Görsel tasarım

9 × 11 zarf içinde 75 hücre. Dört kanat, daralan orta gövde ve üst/alt çentikler;
sağ-sol simetrisi hem maskede hem engellerde korunur. Yeşil yapraklar ve beyaz/lila
çiçekler ana renk kütleleri; krem duvarlar ve turuncu ahşap sandıklar sıcak vurgulardır.
Dört mevcut taş rengi kullanılır. Yeni görsel asset veya yeni engel mekaniği eklenmez.

Önizleme: `Level106-Cicekli-Kelebek.png`. Mevcut sprite'larla hazırlanmış yerleşim
çizimidir; oyun ekran görüntüsü değildir. Duvarın runtime autotile birleşimleri ve
çiçeklerin dağılımı oyunda farklı çizilir.

## Mekanik kompozisyon

- Dört ayrı Wall parçası, her biri 5 hücre: bir hücreye odaklanmak bütün kanadı açar.
- 20 GrassFlower hücresi: 12 bağımsız, 8'i duvarların üzerinde.
- Sekiz `chest3`: uçlarda ve gövdeye yakın simetrik çiftler; her biri 3 vuruş.
- Hedefler: 4 Wall parçası, 20 Grass, 8 chest3. Grass hedefi bilerek kullanılır:
  yalnız çiçek dökülmesi yetmez, altındaki çim de temizlenmelidir.
- Duvar üzerindeki örtü açılınca o hücre vurulabilir. Parça yıkılırken kalan
  çiçekli çimler korunur; ayrı vuruşlarda temizlenir.
- İlk dizilimde 47 normal/örtü altı taş rengi sabitlenmiştir; 35 hücre takasa açıktır.
  Refill dört renk havuzundan normal şekilde rastgele gelir. Hazır special yoktur.
- Açılışta hazır üçlü veya aynı renk 2 × 2 yoktur; basit eşleşme denetiminde
  8 geçerli komşu takası vardır. Alt kanatta beşli fırsatını görmek bir ödüldür.

## Oyun akışı ve denge

İlk hamlelerde açık kanat içlerinde eşleşme/special kurulur; sandıklar ve çiçekler
kademeli açılır. Orta bölümde duvar parçalarına odaklanıp alan genişletmek, son
bölümde kalan örtü ve köşe hedeflerini temizlemek amaçlanır. Son 3–5 hamledeki
kazanma/kaybetme baskısı tasarım hedefidir; her rastgele oyunda garanti değildir.

İlk ayar: 24 hamle. Gerçek BoardController ile GoalAware bot, 1× hız, üç farklı
seed üzerinden deneme başlatıldı. Nihai ayar ve sonuçlar aşağıya eklenecek.

## Yapısal doğrulama

Dizi uzunlukları, maske dışı boşluklar, bağlantılı tek board, dört bağlantılı duvar
parçası, origin eşleşmeleri, katman yerleşimleri, hedef adetleri, sabit taşların
uygunluğu ve global katalog bağlantısı kontrol edildi. Unity build çalıştırılmadı.
