# Level 156 — Kargo Kemerleri

`LevelCatalogPro` global 156 → `LevelP_001600.asset`, chapter 7. [Yerleşim önizlemesi](Level156-Kargo-Kemerleri.png). Kullanıcının örneği temel alınarak **9×9, 36 hamle, dört renk**.

```text
..CoooC..
.CsoPosC.
CKsssssKC
KK.....KK
K.CoooC.K
.CKsssKC.
CKKsssKKC
KK.....KK
K.......K
```

`C` Cargo (34), `K` chest2 (5), `s` chest1 (4), `o` normal başlangıç taşı, `P` hazır PulseCore, `.` gerçek hole.

Hedefler **12 Cargo + 20 iki katlı sandık + 13 tek katlı sandık**. Cargo kırılmaz, yalnız aşağı düşer ve sütunun son oynanabilir hücresinden çıkar. Her Cargo'nun altında en az bir kırılabilir sandık var; başlangıçta bedava çıkış yok. Yollar yalnız sandık, diğer Cargo, normal hücre veya düşüşe geçirgen hole içerir. Kalıcı engel veya yatay taşıma zorunluluğu bulunmaz. Üstteki Cargo'nun altında alt kemerin hücreleri varsa önce oraya düşmesi gerekir; ara hole tek başına çıkış değildir.

İki basamaklı kemer ve aradaki siyah alanlar referanstan korundu. Referanstaki üstteki tek sıra üç taş swap tabanlı oyunumuzda yeterli alan vermediği için hemen altındaki üç hücre de normal taş yapıldı; alt kemerin üzerinde üç taşlık ikinci açıklık var. İki ada arasındaki hole'lar düşüşte geçirgendir; maskenin tek bağlı ada olması şart değildir.

Dokuz başlangıç taşı sabit; üst merkezde (x=4, y=1; sıfır tabanlı) bir hazır PulseCore var. Bunun 5×5 etkisi iki iç Cargo koridoruna ulaşır. Bu koridorlardaki dört iki katlı sandık tek katlıya indirildi: ilk patlama sandıkları açarak üst iç Cargo’lara aşağı yol verir. Otomatik başlangıç eşleşmesi yok; oyuncu special ile alanı açmayı seçebilir. Devamındaki refill dört renkten rastgele gelir. Cargo hedef eksikliği için mevcut kredi sistemiyle yeniden doğabilir; bu level yeni spawn davranışı eklemez.

54 aktif hücre, 27 hole. Yedi dizi 81 eleman; origin'ler, hedef miktarları, kütüphane görselleri, katalog ve GUID doğrulandı. Katman yok; önceki level dosyaları ve ortak obstacle library hash kontrolünde değişmedi.

Yeni runtime kodu/VFX yok. Mevcut Cargo düşüş ve hedefe uçuş sistemi kullanılır. 36 hamle ilk denge ayarıdır; Unity, build veya bot çalıştırılmadı, kazanma oranı ve cihaz performansı ölçülmedi.
