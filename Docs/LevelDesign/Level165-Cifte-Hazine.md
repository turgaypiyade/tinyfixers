# Level 165 — Çifte Hazine

Katalog 165 → `LevelP_001690.asset`, chapter 7. **9×11, 99 aktif hücre, 30 hamle, dört renk.** Orta-zor; 164 sonrasında daha rahat bütçeyle renk toplama ve katman açma planı. [Önizleme](Level165-Cifte-Hazine.png).

```text
ooooooooo
SSoooooSS
SSoooooSS
SSoooooSS
SSoooooSS
SSoooooSS
SoooooooS
ooooooooo
AAAoooBBB
AAAoooBBB
AAAoooBBB
```

`S` Mud (25) → SculptingStone (28); A/B iki ayrı 3×3 AncientSeal (64), her birinin altında dokuz GoldMoney (32). Hole yoktur.

| Hedef | Adet |
| --- | ---: |
| AncientSeal | 2 mühür |
| SculptingStone | 22 |
| Mud | 22 hücre |

Mühür origin'leri **72, 78**, yani (0,8), (6,8). Solda kırmızı → sarı → yeşil, sağda yeşil → kırmızı → sarı sırası vardır. **Her renk altı**, her mühür 18, toplam 36 renk kredisi ister. Taşlar uçuş sonunda sayılır. İki mühür aynı rengi istediğinde eşit bölüşülmez; mevcut servis o rengin sıralı hedefini doldurur. Farklı başlangıç renkleri iki renkte aynı anda ilerlemeye izin verir.

22 oyma taş üç vuruşludur; altında iki vuruşlu çamur vardır. Mühürler açıkta başlayıp hemen renk toplar; oyma taşlar mühürlerin üstüne örtülmez. Mühürler açılınca **18 altın** görünür. Altın ana goal değildir; mevcut kazanma erteleme akışı açıkta kalan altınları gözetir. GoldMoney + statik mühür yığınında tek hareketli engel bulunur.

**59 rastgele taş hücresi**; hazır special ve yardımcı yoktur. Üst sekiz sırada beşli merkez alan, mühür üstünde geniş yatay eşleşme sırası vardır. Alt üç sırada mühürler arasındaki üçlü açıklık üstten dolar. Mühürlerin altında normal hücre kalmaz; yan şeritlerin altı izinli köşelerden beslenir.

Özel footprint'ler, altınlar, renk kotaları, hedefler, dolum ve katalog kontrolü geçti. **30 hamle ilk ayardır; kazanma oranı ölçülmedi.** [Ortak doğrulama ve performans notları](Levels163-166.md). Unity Play/build ve oyun/bot simülasyonu yapılmadı; normal renkler temsili.
