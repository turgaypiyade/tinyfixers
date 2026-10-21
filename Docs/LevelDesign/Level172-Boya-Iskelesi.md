# Level 172 — Boya İskelesi

Katalog 172 → `LevelP_001760.asset`, chapter 7. **9×11, 87 aktif / 12 hole, 29 hamle, dört renk.** Kullanıcının 172 tasarım isteği üzerine oluşturuldu; henüz kullanıcı onayı yoktur. [Görsel önizleme](Level172-Boya-Iskelesi.png). 171'in onaylı asset'i değişmedi.

```text
ooooooooo
ooooooooo
SoooooooS
SSoooooSS
SSSoooSSS
.WWvHvWW.
.WWHvHWW.
.PPvHvPP.
.PPHvHPP.
.PPvHvPP.
.PPHvHPP.
```

`.` hole; `o` otomatik normal taş; `S` SculptingStone (28); `W` iki 2×2 WaterTank (52); `P` dört 2×2 PaintCanBox (63); `v` mor plastic (7); `H` HelmetPorcelain (30). Koordinatlar sıfır tabanlı, y üstten aşağıdır. Normal/renkli chest, katmanlı sıvı tabanı veya hazır special kullanılmadı.

## Kompozisyon ve açılma

Geniş üst bölüm ile yanları hole olan yedi sütunluk alt gövde **T silueti** oluşturur. Üçgen taş omuzlar merkezdeki taş alanına doğru basamaklanır. Alt gövdede su depoları ve boya kasaları iki kesintisiz kenar kolonu; aralarında mor plastik/porselen, üç sütun boyunca altı satırlık tek bir dama alanıdır. Engeller bağımsız adacıklar değildir. Sütunlar, yoğun desen ve biçimli maske yerel 135/147/155/163 örnekleri ve kullanıcının genel görsel referans havuzuyla ilişkilidir; yeni referansların birebir kopyası veya her yeni görselde tekrar değiştirme gereği yoktur.

Başlangıçtaki **33 normal hücre tamamen otomatik dolabilir**. Üstte iki tam sıra ve ortada 7→5→3 genişliğinde çalışma alanı vardır. Dama alanı tek vuruşla açılır, orta kanalı derinleştirir; hareketli plastik/porselenler oyun içinde yer değiştirebilir. Taş omuzlar normal/special hasarıyla üç aşamada oyulur. Su depoları **iki special isabeti** ister; kırıldıklarında su saçılır. Dört boya kasasının her birinde dokuz kutu vardır; normal komşu eşleşme bir, special iki kutu çıkarır. Depoları açmak alt kasalara erişimi genişletir. Bütün kaynaklar yalnız komşu normal eşleşmeyle temizlenebilir varsayılmaz.

| Authored hedef | Adet |
| --- | ---: |
| SculptingStone | 12 |
| PaintCanBox | 4 |
| plastic | 9 |
| WaterTank | 2 |

**Dokuz porselen**, tek vuruşluk alan açan örtüdür; ayrı hedef değildir. Su depoları kırıldığında mevcut TopHUD akışı `EnsureDynamicObstacleGoal(WaterPuddle)` ile **dinamik su temizleme hedefi** oluşturur. Başlangıçta sahte/sabit bir su adedi yazılmadı. Mevcut config depo başına sekiz hedef hücre talep eder; gerçek başarılı spawn ve temizlenme durumları dinamik hedefi belirler. İki depodan kesin 16 farklı su hücresi çıkacağı varsayılmaz. Dört authored hedefe en fazla bir su hedefi eklenerek beş tür olur.

Depo origin'leri **46/51**; boya origin'leri **64/69/82/87**. Üst iki boya kasası kırmızı, alt iki mavi olarak açıkça ayarlandı; resource bulunmazsa mevcut varsayılan kova sprite'ı kullanılır. Refill dört ana renk; bütün tile/special pinleri sıfır. 12 hole tam olarak y=5…10 satırlarının x=0/8 dış kenarlarıdır. Şekil ve engel yerleşimi sağ-sol simetrik; dama renkleri ritmik olarak dönüşür.

## Denge ve doğrulama

**29 hamle ilk denge ayarıdır.** 170 sonrası %40–50 geçiş yönü korunur; gerçek kazanılabilirlik/oran ölçülmedi. Zorluk üç aşamalı omuzlar, 36 boya içeriği, special isteyen depolar ve saçılan suyu temizlemekten gelir. Alt dama bölgesinin tek vuruşta açılması rahatlama sağlar. Başlangıç helper'ı veya garantili özel combo yoktur.

Statik kontrol: yedi 99 elemanlı dizi, 87/12 maskesi, beş engel ailesinin footprint/origin'leri, dört authored hedef, boya renkleri, 18 hücrelik dama, sıfır pinler, bütün 33 normal hücrenin başlangıç dolumu, aktif maskenin bağlılığı, önceki tanıtımlar ve katalog/GUID geçti. **566 önceki ayar/level dosyası**, onaylı 171 dahil, hash ile aynı; katalog önceki öneki koruyarak yalnız 172 ekledi. Önizleme mevcut sprite'larla render edilip incelendi. Unity Play/build, bot veya oyun/kaskat simülasyonu çalıştırılmadı.

## Performans sınırı

Yeni runtime/VFX/update kodu veya kare başı tarama eklenmedi. Genel taş/plastik/porselen kırılmaları mevcut tile ve `BoardBreakFxService` havuzlarından yararlanır; efektler ömür sonunda temizlenip havuza döner. Dört PaintCanBox 36 içerik görseli kurar; düşen kutular mevcut uçan kopya/coroutine yolunda oluşturulup normal bitişte Destroy edilir. Bu özel düşüş yolu havuzlanmış gibi sunulmaz; CoFall'da ayrı bir finally temizliği bulunmaz. Sahne değişiminde bağlı root yaşam döngüsü geçerlidir, canlı iptal testi yapılmadı.

İki WaterTank mevcut saçılım başına hedef List/HashSet'leri ve hedef taraması, meteor/trail/damla/splat GameObject'leri oluşturur; özel su efektleri genel kırılma havuzunda değildir. Tank sayısı ikiyle sınırlı tutuldu. `FlyMeteor` finally'si uçuş görselini ve callback'leri temizler; BoardController saçılım finally'si su placeholder'ını ve job'u bırakır. Mevcut kodda component/sahne kapanış istisnası vardır; bütün özel su efektlerinin her iptalde temizlendiği iddia edilmez. Bu task'ta runtime refaktörü yapılmadı; cihaz maliyeti, iptal ve sahne çıkışı ölçülmedi.
