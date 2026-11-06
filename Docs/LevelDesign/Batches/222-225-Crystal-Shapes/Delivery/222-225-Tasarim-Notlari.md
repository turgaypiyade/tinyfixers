# 222–225 · Tasarım notları

Statik kontrol ve gerçek sprite görsel incelemesi yapıldı. Oynanış dengesi henüz denenmedi. Önceki 1–221 ve oyun/akış kodu korundu.

## 222 · Zümrüt Yaprak / Emerald Leaf

**24 hamle · Normal**

- İlk bakış: Sağa uzanan yaprak silueti; ortadaki kesintisiz çapraz çim damarı ve sapı tamamlayan yeşil vitrin.
- İlk karar: Çim damarının dış yüzünden mi ilerleyeceksin, sapın üstünde yeşil eşleşme mi hazırlayacaksın?
- İlk katman sonrası: Çim kalktıkça yaprağın iki yüzü tek eşleşme alanına dönüşür; yeşil kepenk için daha çok hizalama açılır.
- Kalan iş: Yeşil vitrini aç, toplam 10 elması üret ve aynı renk elmas eşleşmeleri/special ile ayrıca topla.

[Detay önizleme](Level222-Zumrut-Yaprak.png)

## 223 · Uçan Anahtar / Flying Key

**23 hamle · Normal**

- İlk bakış: Yatay anahtar silueti: solda yuvarlak baş, sağda iki diş ve uçtaki bitişik 2×2 anahtar makineleri.
- İlk karar: Sağdaki porselen şaftı indirip makinelerin üst yüzünü mü açacaksın, soldaki oyma bloğunu mu çözeceksin?
- İlk katman sonrası: Şaft açılınca makinelerle seri üretim başlar; sol baştaki 2×3 oyma bloğu kalkınca çamurlu taban görünür.
- Kalan iş: 14 anahtarı kendi aralarında eşleştir/special ile temizle; oyma bloğunun altındaki 6 çamuru bitir.

[Detay önizleme](Level223-Ucan-Anahtar.png)

## 224 · Kristal Çapa / Crystal Anchor

**24 hamle · Normal**

- İlk bakış: Düz tahtanın içinde engeller bir çapa çizer: brandalı tepe halkası, ahşap gövde/kuşak, mavi-kırmızı kristal uçlar.
- İlk karar: Üstteki dört çimi kaldırıp heykelin mavi etiketini mi başlatacaksın, altta iki renkli vitrinin yüzlerini mi açacaksın?
- İlk katman sonrası: Ahşap çapa çözülürken yatay vuruş alanları genişler; çim kalkınca heykel mavi taş toplamaya başlar.
- Kalan iş: Heykelin 8 mavi taşından sonra üç special isabeti ve ortak hedefte 12 elması ayrıca toplama işi kalır.

[Detay önizleme](Level224-Kristal-Capa.png)

## 225 · Mühürlü Kristal Kalesi / Sealed Crystal Keep — v2

**25 hamle · Hard.** Aynı 88 hücre; 16 elmas, 1 mühür, 16 pusula taşı ve 16 çamur. Mühür sayaçları 9/9/9 olarak korundu.

Kutular alt köşelere alındı. Yanlarda iki açık besleme şeridi, kutuların arasında dört hücre genişliğinde çalışma alanı var. Alt iki sıra başlangıçta doğal olarak doluyor. Başlangıçtaki boş hücre sayısı 12’den 2’ye indi; kalan iki hücre kapalı mührün hemen altında.

- İlk bakış: İki uzun pusula suru, ortada 4×4 mühürlü avlu, alt köşelerde kristal kapıları ve açık besleme yolları.
- İlk karar: Yan şeritlerden alt çalışma alanına in; mührün renk sırasını ilerletirken surlar için special çizgileri hazırla.
- İlk katman sonrası: Alt iki sıra başlangıçtan beslenir. Mühür açılınca 16 çamur hücresi ve altındaki son iki boş yuva da açılır.
- Kalan iş: 16 pusula taşını ve iki kat çamuru temizle; köşelerdeki kapılardan toplam 16 elması üretip ayrıca topla.

[225 detay önizleme](Level225-Muhurlu-Kristal-Kalesi.png)

Dimerdal bütün seviyelerde güncellendi. Kabul edilen vuruşta 0,26 saniye büyüme, silkelenme ve iç parlama var. Ortak kotanın son elması tahtaya yerleşince kepenkler 0,30 saniyede kapanıp gri ve %45 opak hale geliyor. İptal veya başarısız uçuş erken kapanmaya yol açmıyor. Toplama, sayaç, ses ve taş akışı değişmedi.

222–224 görsel olarak beğenildi ve korundu. 225’in yeni düzeni ve animasyonlar cihazda henüz denenmedi. Build, Play veya oyun simülasyonu çalıştırılmadı.
