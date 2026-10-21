# Level 179 — Çelik Kilit

Katalog 179 → `LevelP_001830.asset`, chapter 7. **9×11, 74 aktif / 25 hole, 26 hamle, dört renk, Super Hard.** İnceleme taslağı; kullanıcı henüz bu levele ayrı onay vermedi. [Görsel önizleme](Level179-Celik-Kilit.png). [173–180 toplu inceleme](Levels173-180.md).

```text
oooooo.RR
oooooo.RR
oooooo...
CCCCCC.RR
CCCCCC.RR
CCCCCC...
CCCCCC...
.........
SSSSSS.MM
SSSSSS.MM
SSKKSS.MM
```

`o` otomatik normal taş; `.` hole. C Mud → kırmızı/mavi ColorCrate; R Rodos; M üç ayrı metal parçası; S special-only pusula; K yardımcı sepet. Koordinatlar sıfır tabanlı; y üstten aşağı.

## Oyun fikri

Hazır Override + LineH ile ilk katmanları aşındır; oda sınırlarını geçen special hatlarını Rodos ve metal kilitlere zamanla.

- 24 renkli üç aşamalı örtü → 24 çamur.
- 2 Rodos ve 3 bağımsız metal parçası.
- 16 pusula; 2 sepet küçük destek sağlar.

| Hedef | Adet |
| --- | ---: |
| Mud | 24 |
| RhodesStatue | 2 |
| MetalWall parçası | 3 |
| SculptingSpecial | 16 |

Ana oda x=0…5, y=0…6; alt oda y=8…10. x=6 ve y=7 hole sınırlarıdır; sağdaki iki Rodos ayrıca hole ile ayrılmış 2×2 odacıklardadır. 24 Mud üstünde solda 12 kırmızı, sağda 12 mavi ColorCrate vardır. Rodos origin'leri 7/34, her biri dört special ister. Metal origin'leri 79/88/97 ve parça ID'leri 1/2/3; her biri iki hücrelik bağımsız parçadır. Hazır red Override (11) ve LineH (12) yan yanadır, geçersiz swap beklenmez. İki sepet 92/93; toplam altı roket, salvo kapalı. Sepetlerin bitmesi ayrı goal değildir.

## Katmanlar

- 12 hücre: taban ID 25 → üst ID 72, `stackOrder=1`.
- 12 hücre: taban ID 25 → üst ID 73, `stackOrder=1`.

## Tasarım notları

- Metal parça hücre değil üç parçadır. Her parçada bir hücreye dört vuruş gerekir; vurulmayan hamlede bir aşama geri gider.
- Override+LineH açılışı bütün hedefleri tek hamlede bitirmez; sağ odalara uygun satır etkisi gönderilmelidir.
- Sepet salvosu kapalıdır: tek hit bütün roketleri boşaltmaz.

Başlangıçta **18 tile hücresinin tamamına** üstten/izinli köşe geçişinden geometrik dolum yolu vardır; hole hücreleri yalnız dikey spawn geçişidir. İki hazır special dışında renkler rastgele; iki special'ın base rengi Core olarak pinlidir. Sıvılar tabanda, katılar üsttedir; yığınlarda en fazla bir hareketli engel vardır.

**26 hamle ilk denge ayarıdır.** Geçiş oranı ölçülmedi; normal levellarda %40–50 yönü tasarım hedefidir. Hard/Super Hard burada tasarım zorluğu sınıfıdır; yeni runtime/UI zorluk alanı eklenmedi. Statik mask, footprint/origin, hedef/katman, tanıtım sırası, refill, pin ve katalog/GUID kontrolleri geçti; Unity YAML ayrıştırıldı. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build ve oyun/bot/kaskat simülasyonu yapılmadı.

[Ortak performans ve doğrulama sınırları](Levels173-180.md) bu level için de geçerlidir. Yeni runtime/VFX kodu eklenmedi; önceki levellar ve ortak servisler korunur. Bu grubu geri alma kaydı `Batches/173-180/manifest.json` içindedir.
