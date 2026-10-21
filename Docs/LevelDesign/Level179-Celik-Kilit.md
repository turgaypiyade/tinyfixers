# Level 179 — Çelik Kilit

Katalog 179 → `LevelP_001830.asset`, chapter 8. **9×11, 74 aktif / 25 hole, 31 hamle, dört renk, Super Hard.** Kullanıcı 179 üzerinde special değişiklikleri yaptı; bunlar ve 31 hamle korunarak sandık başlangıç aşamaları 3/2/1/1 oldu. Yeni düzen inceleme bekliyor. [Görsel önizleme](Level179-Celik-Kilit.png). [173–180 toplu inceleme](Levels173-180.md).

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

Kullanıcının yerleştirdiği PulseCore + LineV ile açıl; 3→2→1→1 vuruşlu örtüden geçerken Rodos ve metal kilitleri bitir.

- 24 renkli örtü: üst sıra 6×3, ikinci sıra 6×2, alt iki sıra 12×1 vuruş → 24 çamur.
- 2 Rodos ve 3 bağımsız metal parçası.
- 16 pusula; 2 sepet küçük destek sağlar.

| Hedef | Adet |
| --- | ---: |
| Mud | 24 |
| RhodesStatue | 2 |
| MetalWall parçası | 3 |
| SculptingSpecial | 16 |

Ana oda x=0…5, y=0…6; alt oda y=8…10. x=6 ve y=7 hole sınırlarıdır; sağdaki iki Rodos ayrıca hole ile ayrılmış 2×2 odacıklardadır. 24 Mud üstünde solda 12 kırmızı, sağda 12 mavi ColorCrate vardır. Rodos origin'leri 7/34, her biri dört special ister. Metal origin'leri 79/88/97 ve parça ID'leri 1/2/3; her biri iki hücrelik bağımsız parçadır. Kullanıcının güncel special pinleri PulseCore (20) ve LineV (21); normal taş rengi pini yok. Önceki 11/12 pinleri geri getirilmedi. İki sepet 92/93; toplam altı roket, salvo kapalı. Sepetlerin bitmesi ayrı goal değildir.

## Katmanlar

- 12 hücre: taban ID 25 → üst ID 72, `stackOrder=1`.
- 12 hücre: taban ID 25 → üst ID 73, `stackOrder=1`.

## Tasarım notları

- Metal parça hücre değil üç parçadır. Her parçada bir hücreye dört vuruş gerekir; vurulmayan hamlede bir aşama geri gider.
- PulseCore + LineV açılışı ve sonraki special üretimi sağ odalara ulaşmak için kullanılır; hedefler yerinde korundu.
- Sepet salvosu kapalıdır: tek hit bütün roketleri boşaltmaz.

Başlangıçta **18 açık taş hücresi** ve son aşamadaki 12 sandığın `holdsTile` kuralıyla altında tutulan **12 örtülü taş hücresi** vardır. Örtülü taşlar açılıştaki serbest eşleşme alanı sayılmaz. Açık alanın geometrik dolum yolları korunur; hole hücreleri yalnız dikey spawn geçişidir. İki hazır special dışında renkler rastgele; kullanıcı normal renk pinlerini kaldırmış, bu değişiklik korundu. Sıvılar tabanda, katılar üsttedir; yığınlarda en fazla bir hareketli engel vardır.

**31 hamle korundu; sandık aşamaları son revizyondur.** Geçiş oranı ölçülmedi; normal levellarda %40–50 yönü tasarım hedefidir. Hard/Super Hard burada tasarım zorluğu sınıfıdır; yeni runtime/UI zorluk alanı eklenmedi. Statik mask, footprint/origin, hedef/katman, tanıtım sırası, refill, pin ve katalog/GUID kontrolleri geçti; Unity YAML ayrıştırıldı. Mevcut sprite'larla render edilen önizleme incelendi. Unity Play/build ve oyun/bot/kaskat simülasyonu yapılmadı.

[Ortak performans ve doğrulama sınırları](Levels173-180.md) bu level için de geçerlidir. Yeni runtime/VFX kodu eklenmedi; önceki levellar ve ortak servisler korunur. Bu grubu geri alma kaydı `Batches/173-180/manifest.json` içindedir.

## 2026-10-21 — oynanış geri bildirimiyle hamle revizyonu

Kullanıcı `LevelP_001830` için “hiç geçilmiyor”, “4–5 hamle daha ihtiyacı var” ve “kolay ... değil ... çözülebilir olsun” dedi. Katalogdan bunun **179** olduğu doğrulandı. **26→31 hamle (+5)** yapıldı; Super Hard engel yükü, 24 renkli sandık, hedefler, maske, katmanlar, iki hazır special ve iki sepet aynen korundu. Böylece ilk denemede erişim/katman işi için süre açılır; kolay bir levele dönüştüğü veya artık kesin geçildiği iddia edilmez.

Kullanıcının 1/2 hit sandık fikri opsiyonel bir öneriydi. Mevcut LevelData/StackedObstacleEntry hücre başına başlangıç-hit override taşımıyor; ortak library'deki hit sayıları değiştirilmedi, ayrı runtime/level kütüphanesi eklenmedi. Bu tur kalibrasyon yalnız ek beş hamledir. Önceki asset/meta, PNG ve not `Batches/183/Before/` içinde saklı. Yeni PNG ve toplu kartın hamle etiketi 31 olarak güncellendi; JSON tasarım kaydı da 31. Eski batch `validation.json` ve rollback manifesti ilk sürümün tarihsel kaydıdır; güncel kontrol `Batches/183/validation.json` içinde. Oynanış simülasyonu yapılmadı.

## 2026-10-21 — kullanıcının güncel düzenini koruyan aşama revizyonu

Son talep: “179'da da değişiklik yaptım onu koru ... sandıkların ilk katman 3 ... sonra 2 ... sonrakiler tek stage”. Kullanıcının mevcut asseti önce snapshot alındı. Değişiklikler yalnız `stackedObstacles` girdilerine `initialRemainingHits` ekler; bu satırlar çıkarılınca asset snapshot ile **birebir aynı**dır. Kullanıcı PulseCore/LineV pinleri 20/21, kaldırılmış renk pinleri, 31 hamle, hedefler, maske, bütün katman/originler korundu. 183'e dokunulmadı.

Bir tabanlı sandık satırları: **4. satır 3 hit**, **5. satır 2 hit**, **6–7. satırlar 1 hit**. Sol kırmızı/sağ mavi renk bölgeleri aynı. Toplam sandık vuruş yükü **72→42**; alttaki 24 Mud ve diğer hedefler aynı. İlk sıra sağlam sprite, ikinci sıra hasarlı ikinci aşama, son iki sıra tek-hit son aşama ile açılır. Çözüm veya geçiş oranı ölçülmedi.

Önceki notta desteklenmediği belirtilen hücreye özel başlangıç aşaması, bu açık taleple **renkli sandık stack girdileri için** eklendi: `initialRemainingHits=0` varsayılan, pozitif değer 1…library.hits aralığına sıkıştırılır. Paylaşılan library ve renk vuruş kuralları değişmez. State hem üstteki sandığı hem beneath kaydını doğru kalan hit ile başlatır; runtime başlangıç görseli, editor Overlay rozeti ve proje-sprite PNG aynı aşamayı kullanır. Editörde Overlay / renkli sandık / “Başlangıç kalan vuruş” ile aynı sandığa tekrar tıklamak yeni katman eklemeden başlangıç aşamasını günceller; Undo mevcut çizim yolundadır.

Performans: küçük origin+id sözlüğü yalnız override içeren ilk kurulumda ayrılır, kurulumda tek stack taramasıyla hazırlanır ve sonraki initialize'da temizlenip kapasitesi kullanılır. Vuruş/dolum/update döngülerine tarama, coroutine, efekt veya yeni havuz eklenmedi. Örtü kırılınca kalan hit mevcut beneath/snapshot akışında korunur. Yeni alanı olmayan eski levellar sıfır varsayılanıyla mevcut başlangıç hitini kullanır. Ölçülmüş performans sonucu yok; kaynak ve veri statik incelendi, C# compile/Unity Play/build/bot çalıştırılmadı.

Son snapshot, güncel tasarım ve doğrulama `Batches/179-StageGradient/` içinde. Dört kaynak dosyanın kullanıcının değişikliklerini içeren önceki kopyaları saklı; 2.390 diğer ayar/script/Resources dosyası korunma kontrolünde. Önceki Batches183 kontrolü tarihsel sürüme aittir. Güncel PNG ve toplu179 kartı yenilendi; diğer kartlar korundu.
