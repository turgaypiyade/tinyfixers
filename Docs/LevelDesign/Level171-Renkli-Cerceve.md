# Level 171 — Renkli Çerçeve

Katalog 171 → `LevelP_001750.asset`, chapter 7. **9×11, 78 aktif hücre / 21 hole, 28 hamle, dört renk.** Zorlayıcı normal bölüm; %40–50 geçiş oranına doğru ilerleyen ilk denge ayarıdır, ölçülmüş sonuç değildir. [Görsel önizleme](Level171-Renkli-Cerceve.png).

Kullanıcının iki görsel referansındaki ana ilke uygulanır: sandıklar bağımsız adacıklar yerine kenarı/şekli tamamlar; boşluk, engel sıraları ve eşleşme alanı tek kompozisyon oluşturur. Önceki renk çarkı bu düzenle değiştirilmiştir. Asset ve GUID korunur.

**Kullanıcı onayı (2026-10-19):** “171 oldu bence onu kullanabiliriz”. Mevcut düzen onaylıdır; kazanma oranı ölçümü ayrı tutulur.

```text
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
RRRRRGGGG
R.......G
Y.......G
Y.......B
YYYYBBBBB
```

`o` otomatik normal taş; `.` gerçek hole; `R/G/Y/B` kırmızı/yeşil/sarı/mavi ColorCrate (72/74/71/73). Koordinatlar sıfır tabanlı, y üstten aşağıdır.

## Kompozisyon ve oynanış

Üstteki **9×6 eşleşme alanı** tam genişliktedir. Altta **9×5 kesintisiz sandık çerçevesi**, **7×3 hole boşluğunu** çevreler. Kırmızı/yeşil üst kenarı, sarı/mavi alt kenarı oluşturur; yan sütunlar üst ve alt bantları birbirine bağlar. Hem çerçevenin tamamı hem her bir renk bölgesi dört-komşulukla bağlantılıdır. Sandıkların yanında bağımsız yardımcı blok veya tek başına batarya bulunmaz.

İlk eşleşmeler üstteki kırmızı/yeşil bandı aşındırır. Açılan hücrelerle eşleşme alanı genişler. Oyuncu üst alanda special/combo üretip alt çerçeveye ulaştırır; yanları açmakla aşağıya dikey special göndermek arasında seçim yapar. İç boşluğun altında kalan y=10, x=1…7 hücreleri, başlangıçta normal komşu eşleşmelerle doğrudan vurulamaz. Bunlara yukarıdan geçen dikey special ve uygun combo etkileri ulaştırılmalıdır. `LineVSpecial.CollectColumn` hole hücrelerinde durmaz, sonraki satırlara devam eder. Bu erişim kararı alt sırayı basit bir intro hedefinden ayırır.

| Hedef | Adet |
| --- | ---: |
| ColorCrateRed | 6 |
| ColorCrateGreen | 6 |
| ColorCrateBlue | 6 |
| ColorCrateYellow | 6 |

**24 sandığın her biri üç aşamalıdır.** Normal eşleşme yalnız sandığın kendi renginden hasar verir; special renk kısıtını aşar. Kırmızı/yeşil 169'da tanıtılmıştır. Sarı/mavi aynı ailenin yeni renk varyantları olarak 171'de ilk kez kullanılır. Yardımcı, ek hedef türü, overlay veya hazır special yoktur.

## Dolum ve denge

Başlangıçta **54 normal taş hücresinin tamamı üstten otomatik dolabilir**. Taş renkleri sabitlenmez; `pinnedTileTypes` ve `pinnedSpecialTypes` tamamen sıfırdır. Üstteki geniş alanın altında rastgele boş bırakılmış normal cep yoktur: orta boşluğun 21 hücresi bilinçli hole'dur. Hole'larda tile veya engel oluşturulmaz. Çerçeve açıldıkça aktif kenar hücreleri mevcut akışa katılır; hole'lar motorun spawn geçiş kuralında geçirgendir. Bu, üstte kapalı kalan bir sandığın altına hemen taş doğacağı anlamına gelmez; üst girişlerin de açılması gerekir.

Önceki 32 sandıklık düzene göre toplam aşama yükü azalır; ancak alttaki yedi hücrenin special erişimi istemesi ve yardımcı bataryanın kaldırılması zorluk kaynaklarıdır. **28 hamle ilk ayar olarak korunur**. %40–50 bandına ulaşıldığı iddia edilmez; gerçek geçiş oranı, special üretimi ve alt sıradaki son hedeflerin süresi oynanışta değerlendirilmelidir. 175 Hard ve 179 Super Hard için bunun üzerinde zorluk payı bırakılır.

## Doğrulama ve performans

Geçici statik kontrol; yedi 99 elemanlı dizi, 78 aktif/21 hole maskesi, hole üzerinde engel/pin bulunmaması, dört hedefin altışar sayımı, üç aşama/renk kısıtları, footprint/origin'ler, tek bağlı çerçeve, dört bağlı renk bandı, 54/54 normal hücrenin dolum erişimi ve bütün hedef sütunlarının üst oyun alanına hizalanması için geçti. Katalog/GUID korunur. Revizyon öncesindeki **566 diğer settings/level dosyası** hash ile aynı; önceki 350 dosyalık koruma listesi de geçti. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build, bot ve oyun/kaskat simülasyonu çalıştırılmadı.

Yeni runtime/VFX kodu, update döngüsü veya kare başı tarama eklenmedi. Sandıklar mevcut genel hasar yolunu ve `BoardBreakFxService` havuzunu kullanır. Efekt ömrü sonunda particle sistemleri durdurulup temizlenir, görsel pasifleştirilip anahtar başına en fazla 24 örnek tutan havuza döner. Normal taş havuzu ve GridSpawner'ın event/hit animasyonu temizliği korunur. Önceki batarya artık bu levelde olmadığı için onun detonasyon taraması ve geçici List/HashSet'leri bu tasarımda oluşmaz; bunun cihaz maliyetine etkisi ölçülmedi. İptal ve sahne çıkışı yolları değiştirilmedi, canlı sahne çıkışı testi yapılmadı.
