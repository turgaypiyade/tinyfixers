# Level 183 — Fenerli Bent

Katalog **183 → LevelP_001870.asset**, chapter **8**. **9×11, 99 aktif, 0 authored hole, 28 hamle, dört renk, Normal.** Kullanıcının yeni fener ve su kanalını değerlendirme isteğiyle tasarlandı; inceleme taslağıdır. [Önizleme](Level183-Fenerli-Bent.png).

```text
LoDoooooo
Lo~oooooo
Lo~~~~~~o
Loooooo~o
Loooooo~o
Loooooo~o
Lo~~~~~~o
Lo~oooooo
Lo~oooooo
Lo~SSSSSS
Lo~SSSSSS
```

`o` otomatik normal taş; `D` kaynak/depo; `~` taş altında kanal; `L` Lantern75; `S` SculptingStone28. Kanal ve depo `waterChannels[]` ile kaydedilir; kaynak dışındaki 20 hücre runtime'da damgalanır. Önizlemede normal renkler temsilidir, küçük haritada yolun okunması için taşlar gizlenmiştir ve su temsili gösterilir.

| Hedef | Toplam | Açılış | Açıklama |
| --- | ---: | ---: | --- |
| WaterChannel76 | **1 kanal** | 1 kaynak + 20 kapalı kanal hücresi | Su bütün açık yoldan alt çıkışa ulaşınca hedef +1 |
| Lantern75 | **24** | 11 | 13 ek fener mevcut hedef dolumundan gelir; her fener iki hit |
| SculptingStone28 | **12** | 12 | Alt kenarda 6×2, üç aşamalı taş set |

Solda kesintisiz fener sütunu, altta tek parça taş seti var. Fenerin parçalı/dama yerleşimi kullanılmadı. Su yolu kaynak `(3,1)` → `(3,3)` → `(8,3)` → `(8,7)` → `(3,7)` → alt çıkış `(3,11)` şeklinde kıvrılır (bir tabanlı x,y, üstten aşağı). Oyuncu yolun herhangi bir bölümünü açabilir, ancak su kaynakla kesintisiz bağlantı kurulana kadar ilerlemez. Set ve fener hedefleri de devam ettiği için kanalı açmak tek başına leveli bitirmez.

**Güncel kanal modeli:** Açılan kanal hücreleri normal oynanabilir kalır; taşlar üzerinde durur ve eşleşir. Yalnız depo runtime geçirgendir. Kanal üstündeki taşı eşleştirmek veya special isabeti hücreyi açar; komşu normal eşleşme tek başına kanal açma varsayımı değildir. Yol hiç yukarı çıkmaz, hücre tekrarı/kesişme ve engel/yığın çakışması yoktur. Bitiş alt kenara baktığı için mevcut şelale yolu kullanılır. Yeni runtime/VFX veya özel spawn kuralı yazılmadı.

75 normal başlangıç taş hücresinin 20'si kapalı kanal üzerindedir. Hepsi doğrudan dikey dolum alabilir; kaynak altındaki sütun mevcut pass-through yoluyla beslenir. Sabit renk, hazır special, yardımcı ve sıvı/katı yığın yok. Su kanalının ilk production tanıtımı açık kullanıcı isteğiyle yetkilendirildi. Fenerin iki aşaması ve generic movable refill kullanılır; 13 ek fenerin aynı sütuna gelmesi veya en az 13 hamle istemesi garanti edilmez (75, ID40'a özel hamle kilidinde değildir).

**28 hamle ilk ayardır; kazanılabilirlik ve %40–50 geçiş ölçülmedi.** Tam board ve geniş eşleşme alanı seçildi; 182'nin daralan maskesi kopyalanmadı. Yeni kanal tanıtımı ilave renk kilidi/metal geri dönüşüyle örtülmedi. Oyuncu ilk kez kaynaktan bağlı yol açmayı öğrenirken fener ve üç vuruşlu set hedefleri yük sağlar.

Tekrar incelemesi 1–182'yi kapsar. 67 Hassas Sevkiyat'ın kenar sütunları, 15 Kızıl Hat'ın şerit/blok motifi ve yakın 182 ayrıca gerçek sprite'larla karşılaştırıldı. Dikey fener + blok motifi ortak; yenilik iddiası kanalın bağlantı/açılma hedefi ve boardun tamamında oynanan yoldur, yalnız farklı engel rengi değildir. [İnceleme](Reviews/183-Canal/Review.md), [karşılaştırma](Reviews/183-Canal/Comparison.png). Bu, kullanıcı onayı veya özgünlük yüzdesi değildir.

Katalogdaki 183 önceden `LevelP_testt 1.asset` test dosyasına bağlıydı. Test asseti/meta aynen korundu; yalnız 183 referansı yeni üretim assetine bağlandı. 1–182 katalog referansları ve isimleri korundu. Aynı turda ayrıca açık kullanıcı talebiyle **179 / LevelP_001830 yalnız 26→31 hamle** değiştirildi; onun maske/hedef/katmanları aynı kaldı. 2.390 önceki ayar/script/Resources dosyası hash ile korundu. Snapshot `Batches/183/Before/`, statik rapor `Batches/183/validation.json`.

Performans: Tek kanal 20 parça için 40 Image, depo için 3 Image ve tek köpük Image'ı kurar; akış sırasında kanal başına coroutine çalışır, sürekli board taraması yoktur. Mevcut yedi damla Image'ı kanal servisinin havuzunda yeniden kullanılır; `Splash` çağrısı yine Image[] ve Vector2[] tahsis eder. Alt çıkışta tek RawImage/RectTransform şelale kurulur, devam eden akış shader `_Time` ile yürür. `Flow` goal-flight işini `finally` ile bırakır. `Dispose` event aboneliklerini kaldırır ve maske dışındaki çıkış kökünü yok eder; GridSpawner kurulum/OnDestroy yolları Dispose çağırır. Board üzerinde çalışan coroutine'ler için Dispose içinde ayrı iptal tanıtıcısı yoktur; canlı rebuild/scene teardown doğrulanmadı. Bu mevcut sınır level tasarımında runtime refaktörüne çevrilmedi. Lantern mevcut tile/FX havuzlarını kullanır; generic hedef spawn'ının board sayımları ve goal-fly tahsisleri sürer. Ölçülmüş performans iyileştirmesi iddia edilmez.

Statik kontrol: yedi dizi, origin/footprint, kanalın geçerli sıralı yolu, hiç yukarı çıkmama, alt çıkış, hedefin hücre değil kanal sayması, engel çakışmaması, kaynak/dolum ayrımı, sprite/skin/hint/lokalizasyon, katalog/GUID/isim ve korunan dosyalar kontrol edildi; Unity YAML ayrıştırıldı. Gerçek sprite'lı PNG incelendi. Unity Play/build, bot veya kaskat simülasyonu yapılmadı. 184 istenmedi.
