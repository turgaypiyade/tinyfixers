# Level 181 — Basamaklı Arşiv · v2

Katalog 181 → `LevelP_001850.asset`, chapter 7. **9×11, 99 aktif, hole yok, 26 hamle, dört renk, Normal.** Zorluk revizyonu; kullanıcı henüz v2’yi onaylamadı. [Güncel önizleme](Level181-Basamakli-Arsiv.png).

Kullanıcı açıklaması: **“şekle çok itiraz etmiyorum ama çok kolay ... bu level için”**. Önceki değerlendirme şekil tekrarına fazla ağırlık verdi. Basamak fikri korunarak engel alanı büyütüldü ve katman yükü artırıldı. Önceki 75 açık hücre ile yalnız 24 engel hücresi, special üretmek için geniş çalışma alanı bırakıyordu; kullanıcının kolaylık geri bildirimi doğrultusunda bu oran değiştirildi. Kazanma oranı ölçümü yok.

```text
ooooooooo
ooooooooo
ooooooooo
WWooooooo
WWooooooo
CCWWooooo
CCWWooooo
CCCCWWooo
CCCCWWooo
CCCCCCWWo
CCCCCCWWo
```

`o` otomatik normal taş, `W` 2×2 Wardrobe, `C` eski 2×2 ColorChest. Sol ve alt kenara bağlı tek kütle var; bütün gövdeler birbirine bitişik. Üstteki üç tam satır ve sağdaki kademeli alan oynanabilir. Sabit renk, hazır special ve yardımcı engel yok. Yeni ColorCrate 71–74 kullanılmadı.

| Ayar | İlk taslak | v2 |
| --- | ---: | ---: |
| Başlangıç taşı hücresi | 75 | **59** |
| Engelle kaplı hücre | 24 | **40** |
| Eski ColorChest · 21 | 3 | **6** |
| Wardrobe · 27 | 3 / 30 eşya | **4 / 40 eşya** |
| SculptingStone · 28 | 4 | **16** |
| Mud · 25 | 24 | **40** |
| Üç katmanlı hücre | 4 | **16** |
| Hamle | 27 | **26** |

Hedefler: **21 × 6, 27 × 4, 28 × 16, 25 × 40**. Üç katmanlı hücreler ayrı hedef değildir.

Her dış basamakta dolap bulunur. Dört dolabın her birinde `Mud → dört SculptingStone → Wardrobe` yığını vardır. Dolap boşalınca alan hemen serbest kalmaz; önce üç vuruşlu taşlar, sonra iki aşamalı çamur temizlenir. İçteki altı ColorChest’in yığını `Mud → ColorChest` olur. Kapak açıldıktan sonra dört farklı kalan renk tüketilir; normal match uygun rengi, special kalan renklerden birini ilerletir. Dolaplarda kapak sonrası normal hit bir, special iki eşya çıkarır.

Dolap origin’leri **27/47/67/87**: `(0,3)/(2,5)/(4,7)/(6,9)`. Sandık origin’leri **45/63/65/81/83/85**: `(0,5)/(0,7)/(2,7)/(0,9)/(2,9)/(4,9)`. Koordinatlar sıfır tabanlı ve y üstten aşağıdır. Mud’un 40 hücresi ayrı origin’lerdir. Oyma taşları `stackOrder=1`, dolaplar `stackOrder=2`; dolaplar alttaki dört taşı tamamen örter. Hareketli engel yok.

Oyuncunun dolabı boşaltmak ile iç sandıkların renklerini tamamlamak arasında ilerleme seçmesi amaçlanır. Dış gövdeler azalmadan iç sandıklara normal komşuluk erişimi sınırlıdır; special hatları derine ulaşabilir. Açılacak gövde sayısı, renk işi ve gövde sonrası katmanlar artırıldı. **26 hamle ilk denge ayarıdır**; 181 Normal kalır. %40–50 yönü hedef, v2 için kanıtlanmış geçiş oranı değildir.

Statik kontrol geçti: Unity YAML ayrıştırması, diziler, origin/footprint, tam örtme, katman sırası, hedefler, tanıtılmış engeller, isim/GUID/katalog, bütün **59 başlangıç hücresinin doğrudan dikey dolumu** ve dört dolabın başlangıç erişimi. Mevcut sprite’larla render edilip görsel kontrol yapıldı. Önceki **1.859 ayar/script dosyası**, katalog ve 181’in GUID/ismi korundu. Unity Play/build veya oyun/bot/kaskat simülasyonu çalıştırılmadı.

Performans: yeni runtime/VFX kodu eklenmedi. Mevcut Wardrobe görünümü havuzlanmayan eşya/kapak objeleri ve yarım kapak sprite’ları oluşturur; üç dolap yerine dört dolap kullanılması 30 yerine 40 eşya ve 6 yerine 8 kapak getirir. Mevcut hit yolu `CollectCellsForOrigin` içinde board taraması ve List/ToArray tahsisi yapar. Normal tamamlanmada eşya/kapak objeleri coroutine sonunda, dolap görünümü son eşyanın düşüşünden sonra yok edilir. Parçalar dolabın çocuğudur; component/scene yok edilmesi coroutine’leri sonlandırır. Ayrı OnDisable/reset temizliği ve üretilen yarım sprite’ların açık Destroy çağrısı yok; havuz yeniden kullanımı veya eksiksiz reset güvenliği iddiası yapılmaz. Cihaz performansı ölçülmedi; bu tasarım turunda ortak servis kapsamı genişletilmedi.

İlk taslağın asset/meta, design JSON, not ve PNG’si `Batches/181/Revisions/v1/` altında saklandı. [Önceki tekrar incelemesi](Reviews/181-Repetition/Review.md) v1’i anlatır; şekil benzerliği kaydı son kullanıcı açıklamasındaki zorluk önceliğiyle birlikte okunur. İsim **Basamaklı Arşiv** olarak kaldı; 182 tasarlanmadı.
