# 229 · v3 jel / oyma revizyonu

2026-11-06. Kullanıcı yalnız229 için yeni tasarım istedi; çim altında sandık istemedi. Grass Flower ve Grass Lily hatırlatması bu levele zorunlu ekleme talebi değildir. Önceki229, Before altında korunur. Eski batch226 modelini güncel kullanıcı assetine benimsetmeyin; diğer levelları yeniden yazmayın.

9×11/99aktif,28hamle SuperHard.12 jel tabanı üstünde12 oyma taşı; sağda12 porselen,tabanda iki yukarı jel presi ve iki Rodos. Hedefler99Jel/12Oyma/2Rodos/12Porselen. Pres yardımcıdır; ayrı hedef yok. Bütün taş yuvaları doğal beslenir; başlangıç63/63,pin0,boş0. Yeni mekanik tanıtımı,refill kotası veya isim değişikliği yok.

## Tekrar ve görsel kontrol

`Pool-Review.json` güncel kütüphane/havuz seçimini kaydeder. `Reviews/229-Gel-Sculpture-v2` bütün aktif katalog tekrar envanteridir.114 konumsal,183 kırpılmış en yakın aday olarak gerçek sprite karşılaştırmasında açıldı.114’ün sol kasa bandı/yumurta üst alanı/çim-oyma alt alanı ile183’ün iki aynalı taş basamağı incelendi.229’daki tek çapraz omurga,alttaki iki pres ve sağdaki porselen/Rodos işi farklı açılma sırası kurar; basamak görsel ritmi ortaktır. Skorlar özgünlük yüzdesi veya kullanıcı onayı değildir.

`Level229.png` ve `Comparison.png` açılıp incelendi. Normal taş renkleri temsili; kesit,oyma katmanı kaldırılmış statik gösterimdir. Gelecekteki jel yayılımı uydurulmadı. Pres,mevcut PressFrame/PressGel/PressHead dokuları ve başlangıç pozu ile çizildi; çubuğun shader şeridi yalnız statik yaklaşımdır. Önizleme motor ekran görüntüsü değildir.

## Mekanik ve performans incelemesi

Seçim güncel kütüphane,LevelData,GridSpawner,ObstacleStateService,GelLauncherFireAction ve ilgili görsel servisler üzerinden kontrol edildi. Oyma üçhit;pres special-only ikihit;Rodos special-only dörthit. Pres yukarı yönde mevcut hücre etki geçişini kullanır; kalan engel katmanları ve gizli jel için yeni runtime kuralı yazılmadı.

Mevcut SpreadingGelOverlayService hücre görsellerini bir kez oluşturup sözlükte tutar; yeni jel hücresi ve köşe yamaları GameObject yaratabilir,ClearAll temizler.99 hücreye yayılmanın mevcut görsel maliyeti sürer; havuzlu/sıfır allocation iddiası yok. Pres paylaşılan hareket sürücüsü ve kaynak dokularını kullanır; hareket iptali,board nesil kontrolü,disable temizliği mevcut kodda incelendi. Atış başına mevcut yol/deferred listeleri ve coroutine maliyeti sürer; finally alt işleri durdurur,kapağı havuza verir,gövdeyi temizler ve hücre tutmalarını bırakır. Buhar UiVfxPool üzerinden kiralanıp paylaşılan sürücüde geri bırakılır; Rodos parçaları mevcut ObstacleScatterFx yolunu kullanır,ilk parça görsellerinde oluşturma maliyeti vardır. Yeni per-frame tarama,coroutine,ses kaynağı veya VFX sistemi eklenmedi. Oyun açılışındaki mevcut taramalar ve efekt maliyetleri ölçülmedi; CPU/GC/FPS iyileşmesi iddia edilmez.

## Kapsam ve yeniden kontrol

`python3 Docs/LevelDesign/Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/verify.py`

Bağımsız YAML okuması/diziler/origin/footprint/katman/hedef/ikon ve dört besleme evresi PASS.6055 diğer Assets/_Project dosyası SHA ile aynı; bütün diğer levellar,katalog,GUID/meta,TR/EN adlar,mevcut runtime/art ve225/226 kullanıcı değişiklikleri korundu. Yalnız229 oyun asseti değişti. Parentmodelde yalnız229 güncellendi;parentactive-revision ek229revizyonuna işaret eder. Eski parent author/verify kullanıcı226 driftini aşmamalıdır; bu revizyon için bu scopedverify kullanılır.

Parent `Levels226-230.png` ve eski parent karşılaştırması tarihsel229 gösterir; güncel229 için bu klasördeki PNG/not kullanılır. Diğer levelları eski modelden render/author etme. `Delivery/` güncel229 not/PNG/asset kopyası ve SHA manifestidir; dış servise yükleme değildir.

Statik kontrol ve agent görsel değerlendirmesi tamamlandı.28 hamle ilk ayardır;kullanıcı henüz oynamadı/onaylamadı. Unity build/Play/cihaz,bot/düşüş/kaskat simülasyonu,ölçülmüş denge, yayın,releasebaseline veya commit yok.231+ istenmedi.
