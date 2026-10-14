# Level 150 — Onarım Kapısı (Boss Duel)

[Yerleşim önizlemesi](Level150-Onarim-Kapisi.png) · Asset: `Assets/_Project/Settings/ProductionLevels/LevelP_001540.asset` · Catalog: chapter 6 / level 150 / `LevelCL_0150`.

**9×10**, 74 aktif hücre ve 16 hole. Boss karakterlerine yer bırakmak için yüksekliği 10 satırla sınırlıdır. 30 hamle ve dört renk, oynanış testi için ilk ayardır; kazanma oranı ölçülmedi.

```text
..ooooo..
.ooooooo.
ooooooooo
oPPoooPPo
oPPoooPPo
ooooooooo
.ggooogg.
.ggTTTgg.
.ggTTTgg.
..oTTTo..
```

`.` hole, `o` normal taş, `P` 2×2 PaintCanBox, `g` Grass, `T` 3×3 TarpStatue. Maske ve engel yerleşimi sağ-sol simetriktir; taş renkleri simetrik olmak zorunda değildir.

| Yerleşim | Adet | İşlev |
|---|---:|---|
| PaintCanBox (63) | 2 | Üstteki geniş alandan erişilen iki boya kasası; origin 28 ve 33. |
| Grass (42) | 12 | Alt bölümde iki adet 2×3 ayak; kırılınca eşleşme alanı genişler. |
| TarpStatue (68) | 1 | Origin 66, 3×3; 24 sarı taş toplandıktan sonra 3 special vuruşuyla açılır. |
| PlayerShieldPickup (35) | 6 | Heykelin altında alt iki sırada; indeks 75–77 ve 84–86. Toplanınca toplam +12 koruma. |

Dört başlangıç engel türü vardır. Kazanma hedefi yalnızca **520 BossDamage**; kasalar, çimler ve heykelin tümünü bitirmek ya da bütün kalkanları toplamak zorunlu değildir. Oyuncu geniş üst bölgede hasar üretmekle alttaki korumayı açmak arasında tercih yapar. Heykel kalkanları tamamen örter; her hücrede en fazla bir hareketli engel bulunur. Kalkanlar heykel açılır açılmaz otomatik koruma vermez, ayrıca toplanmaları gerekir.

| Boss ayarı | Değer |
|---|---|
| Rakip | 3, otomatik dalga dağılımı; 156 / 172 / 192 can |
| Oyuncu canı | 240 |
| Temizlenen taş başına hasar | 1 |
| Rakip temel hasarı | 7; mevcut dalga ölçeğiyle 7 / 9 / 10 |
| Kalkan başına koruma | 2 |
| Engel fırlatma | Her 4 sayaçta 2; Oil, PlasticBlue, PlasticRed, Helmet |
| Fırlatılmış engel tavanı | 8; başlangıç engelleri bu havuzda yok |
| Yağ yayılım sınırı | 8 hücre |
| Alet tehdidi | Açık; her 4 sayaçta, 3 geri sayım, 2 ek saldırı, 3 saniye kilit; mevcut 140 havuzu |

140'a göre toplam rakip canı 500 → 520, oyuncu canı 230 → 240; elde edilebilir kalkan koruması +10 → +12. Lazer ve dar boğaz yerine geniş açılış alanı ve isteğe bağlı kalkan bölgesi vardır. Bu farklar bir zorluk ölçümü değildir; hamle ve can değerleri kullanıcı oynayarak ayarlanır.

## Akış ve statik doğrulama

- Tüm hücre dizileri 90 eleman; maske bağlı ve simetrik, çok hücreli engeller maskenin içinde.
- Açılışta 45 serbest normal taş, çim altında 12 taş; hazırlanmış special yok. Serbest taşlarda başlangıç eşleşmesi veya aynı renk 2×2 yok; 12 geçerli komşu takası var.
- Yerçekimi grafiğinde başlangıçtaki 57 taş hücresinin tamamına spawn erişimi var. Boya kasalarının altı, açık veya `allowDiagonal` izinli en az bir köşeyi kullanan çapraz yollarla dolar; hole köşe olarak sayılmaz.
- Heykel board'un en alt kenarındadır; altında dolum bekleyen cep bırakmaz. Engeller kaldırılınca 74 aktif hücrenin tamamı erişilebilirdir.
- Katalog/meta GUID eşleşmesi kontrol edilir; önceki seviye assetleri ve ortak ObstacleLibrary değiştirilmez.

Yeni runtime/VFX kodu eklenmedi. İki boya kasası, bir 24 sayaçlı heykel ve altı kalkan mevcut efekt sistemlerini kullanır; fırlatma tavanı ve yağ sınırı 8 tutulur. Mevcut boss fırlatmalarının hedef doğrulaması, animasyon temizliği ve `OnDisable` iptali incelendi; havuzlama veya performans kazanımı iddiası yok, profiler ölçümü yapılmadı.

Unity build'i, bot veya oyun simülasyonu çalıştırılmadı. Önizleme mevcut sprite'larla üretilmiş yerleşim şemasıdır; karakterlerin cihaz ekranına yerleşimi ve gerçek oyun dengesi oynanışta doğrulanmalıdır.
