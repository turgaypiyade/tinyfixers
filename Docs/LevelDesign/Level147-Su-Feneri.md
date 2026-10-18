# Level 147 — Su Feneri

Asset: `LevelP_001510.asset` → `LevelCatalogPro` `level: 147` (chapter 6, `LevelCL_0147`).
Normal level, **9×11**, **75 aktif hücre / 24 hole**, **30 hamle**, dört renk.
Kullanıcı isteği: uyumlu renkler, güzel şekil; ColorChest, WaterTank, LaserCannon ve KeyGenerator.
Hedef zorluk normal–zor; hamle ilk tahmindir, oynanarak doğrulanmadı.

## Kompozisyon

Hole'larla basamaklı daralan başlık, iki katlı gövde ve geniş taban: deniz feneri silueti.
Üst katta mavi çerçeveli iki ColorChest pencere, orta katta iki kırmızı WaterTank,
tabanın köşelerinde mavi/turuncu lazer topları, altta yan yana üç anahtar üretici.
Maske ve engel yerleşimi sağ-sol simetriktir. Ana renk ailesi mavi, vurgu kırmızı ve turuncudur.

```text
...ooo...
..ooooo..
.ooooooo.
.CCoooCC.
.CCoooCC.
..ooooo..
.WWoooWW.
.WWoooWW.
ooooooooo
LLoooooLL
LLoKKKoLL
```

`.` hole · `o` normal taş · `C` ColorChest (21, 2×2) · `W` WaterTank (52, 2×2)
· `L` LaserCannon (65, 2×2) · `K` KeyGenerator (44, 1×1).
Koordinatlar sıfır tabanlı, y üstten aşağıdır.

Önizleme: `Level147-Su-Feneri.png`. Mevcut library/taş sprite'larıyla yerleşim şemasıdır;
oyun ekran görüntüsü değildir. Üreticiler library ikonu ile gösterilir, runtime makine görünümü farklıdır.

## Engeller ve hedefler

| Engel | Adet / origin | Rol |
|---|---|---|
| ColorChest | 2 / 28, 33 | Hedef 2 sandık. Önce kapak açılır, sonra dört renk tüketilir; açık sandığa normal eşleşme içeride kalan rengini ister. |
| WaterTank | 2 / 55, 60 | Hedef 2 tank. İkişer special vuruş; normal eşleşme hasar vermez. Kırılınca su birikintileri doğar. |
| LaserCannon | 2 / 81, 88 | Hedef değildir; special biriktirmeye karşı tehdit. Dörder special vuruşla kırılabilir. |
| KeyGenerator | 3 / 93, 94, 95 | Hedef **24 anahtar**. Üretici adedi hedef değildir; üretmek yetmez, tahtaya inen anahtarlar temizlenmelidir. |

Üç authored hedef vardır. Tanklar kırılınca **WaterPuddle** dinamik hedefi açılır; böylece HUD en fazla
dört hedef gösterir. Başlangıçta birikinti yerleştirilmez, tahmini 16 birikinti sabit goal yapılmaz.
Güncel `WaterTankConfig.puddleCount = 8`; gerçek saçılım uygun hücrelere göre depo başına en fazla sekizdir.
Lazerleri kırmak kazanma şartı değildir; kırılmaları kafesledikleri special'ları serbest bırakır.

## Oynanış ve dolum

1. Üstte ColorChest kapaklarını aç, farklı renklerle içeriklerini temizle; normal eşleşmelerle ilerleme mümkündür.
2. Orta koridorda special üretip WaterTank'lara taşı. Aynı satırdaki iki tankı vuran yatay etkiler değerlidir.
3. Taban üstünde eşleşerek yan yana üreticileri çalıştır; 24 anahtarı üretip temizle. Su birikintilerini de kurut.
4. Lazerle işaretlenen special'ı sonraki hamlede kullan veya kafesi mevcut normal eşleşme/special kurallarıyla aç.

Üst ve alt bölgelerde geniş yatay eşleşme alanı vardır; engel çiftleri arasındaki koridor üç hücredir.
KeyGenerator'lar **en alt sırada**: kalıcı oldukları hâlde aşağıda beslenecek hücre bırakmazlar.
Üstlerindeki beş hücrelik sıra açık tutulur; tek eşleşmeyle birden fazla üreticiye erişilebilir.
Lazerler alt köşelere oturur, altlarında taş hücresi yoktur; altlarına katman konmaz.

ColorChest'ler altlarındaki yan dolumu geçici keser; merkezi koridor/çapraz dolum ve sandık açılması
bu bölgeleri besler. WaterTank'ların hemen altındaki `(1,8)` ve `(7,8)` hücreleri pinned taşla dolu başlar;
boşaldıklarında tank kırılana kadar doğrudan/tek adımlı çapraz dolumları kapalıdır. Tankların açılması
tabana dolum kazandıran bilinçli bir ilerleme adımıdır. Hole'lar düşüş/spawn için geçirgendir.

48 normal taşın başlangıç renkleri sabittir; refill dört normal renkten rastgele gelir.
Hazır special yoktur. `(4,0) ↔ (4,1)` üstte dört mavi, `(4,8) ↔ (4,9)` altta dört sarı taşlık fırsattır.
Başlangıçta otomatik üçlü/aynı renk 2×2 yoktur; açık normal taşlarla **24 geçerli takas** vardır
(üst bölge 13, alt bölge 9, bölgeler arası 2). Bunlar statik açılış sayılarıdır.

## Doğrulama ve performans sınırı

99 elemanlı diziler, hole'larda boş içerik, bağlı/simetrik maske, altı 2×2 ayak izi ve üç 1×1 üretici,
origin'ler, hedef semantiği, tanıtım sırası, dört renk havuzu, açılış eşleşmeleri ve katalog/GUID kontrol edilir.
Katman yoktur; yalnız istenen dört authored engel türü kullanılır. Mevcut level asset'leri korunur.

Yeni gameplay/VFX kodu yazılmaz. Yük üç üretici, toplam 24 anahtar kotası, iki lazer ve iki tank saçılımıyla
sınırlanır. Kaynak incelemesi: anahtar uçuşlarının normal bitişinde mevcut ghost havuzu, lazer kafeslerinde
mevcut havuz/ortak Update kullanılır; tank damlaları mevcut kodda oluşturulup finally'de yok edilir,
havuzlu değildir. Anahtar hedef hesabında mevcut LateUpdate board taraması vardır; performans ölçülmedi.
Lazer teardown temizliği, anahtar rezervasyon/job finally yolları ve tank yayılımının finally bildirimi
kaynakta incelendi; uçuş ortasında sahne çıkışı ve havuza geri dönüşler canlı test edilmedi.

Unity, bot, oynanış simülasyonu ve build çalıştırılmadı. İlk oyun testinde 30 hamle,
iki lazerin baskısı, 24 anahtar yükü ve tank altındaki geçici dolum cepleri değerlendirilir.
