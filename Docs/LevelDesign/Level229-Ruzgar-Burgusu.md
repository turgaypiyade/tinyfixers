# 229 · Rüzgâr Burgusu / Crystal Whirl

**2026-11-07 güncel hamle: 32.** Bu tur yalnız hamle ayarı yapıldı (28→32); aşağıdaki önceki tasarım notu ve eski görseller tarihsel değerler içerebilir. [Güncel denge incelemesi](Reviews/225-234-Balance-2026-11-07/Review.md).

2026-11-06 · v4 agent taslağı; kullanıcı henüz oynamadı/onaylamadı.

9×11/99 aktif; **28 hamle Super Hard**,4 renk. Aktif V1 katalog `LevelCL_0229`; isim/GUID/katalog korunur. [Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset).

Hedefler: **12 GrassWaterLily69,4 Wardrobe27,12 SculptingStone28,12 PlasticTwoStage40**. Çim altında sandık,jel ve jel presi yok. Nilüferin kendi çim tabanı mevcut mekanikle otomatik kurulur; nilüfer hedefi alt çim de temizlenince tamamlanır.

## Dört soru ve kalan iş

1. İlk görüntü: solda2×6 nilüfer,sağda dört dolaplık2×8 kule;altta kıvrılan krem kaide ve ona bitişik3×4 plastik bloğu.
2. İlk karar: nilüferin ilk yaprağı için special üret; yanında bekletirsen yutulabileceği için hemen kullanma/geri alma zamanını seç. Aynı zamanda dolap kulesine vuran hatlar hazırla.
3. İlk alan açılınca: oyma kaidesi alt çalışma alanını genişletir. Dolap kapağı açılınca içerikler hâlâ durur. İlk nilüfer hasarı sonraki yaprak ve çim işini bitirmez.
4. Son iş: kalan dolap içeriği,nilüfer yaprak/çim tabanı ve iki aşamalı plastik. Bir yardımcı atışı bütün zorunlu işi otomatik temizlemez.

Reddedilen v3’te12 gizli jel kaynağı + preslerin tam sütun temizlemesi birbirini kolaylaştırıyordu. Statik besleme ve doğru hedef sayıları bunun iyi bir Super Hard olduğunu göstermiyordu. v4’ün gerekçesi bağımsız aşamalar ve special zamanlamasıdır; yalnız hamle azaltılmadı.28 hamle ilk tahmin,ölçülmüş geçiş oranı değildir.

## Veri ve erişim

47 açık eşleşme yuvası + nilüferlerin altında12 kilitli taş yuvası =59 normal yuva;59/59 doğal beslenir. Pin/hazır special/başlangıç boşu yok. Nilüfer normal swap alanı gibi sayılmaz. Plastik bloğu tabana kadar dolu; başlangıçta havada çizilip ilk düşüşte başka bölgeye taşınacağı varsayılmadı. Dolaplar tabana kadar bitişik. Oyma kalkınca71/71,tüm temizlenebilir parçalar kalkınca99/99 besleme yolu var. Bu statik geometri incelemesidir;oyun simülasyonu değildir.

## Havuz,tekrar ve performans

Önceki tüm havuz incelemesi korundu;jel/presin yardım etkisi nedeniyle seçimi reddedildi. Nilüfer ilk special/yutma/geri alma,oyma alan açma,dolap kapak sonrası içerik,plastik hareketli iki aşama işi sağlar. GrassFlower hatırlatması bu levele zorunlu ekleme değildir. Yakın tarihsel3 ve126 gerçek sprite’larla açıldı: kenar/taban bantları ortak görsel dildir;şekle benzersizlik iddiası yok. Kalan iş ve special kullanımı farklıdır. [Karşılaştırma](Batches/226-230-Lantern-Wind-Crystal/Revisions/v4-229-lily-workshop/Comparison.png).

Mevcut nilüfer servisi olayla komşu kontrolü,sabit petal buffer’ı ve havuzlu special uçuşu kullanır;disable/teardown tutma/job/uçuş temizliği vardır. Dolapta ilk kapak/içerik görselleri oluşturulabilir;çıkan öğeler mevcut ObstacleScatterFx üzerinden gider,çocuk görseller kaldırılır. Mevcut maliyetler sürer;sıfır allocation veya FPS iyileşmesi iddiası yok. Bu level için runtime/VFX/ses/akış kodu değiştirilmedi.

Statik YAML/dizi/origin/footprint/hedef/ikon/katman/besleme/katalog/ad kontrolü PASS;6055diğer proje dosyası ve1–228/230 korundu. Gerçek PNG açıldı;serbest taş renkleri temsili ve kesit statiktir. Kullanıcı denge onayı yok. Unity build/Play,bot/düşüş/kaskat simülasyonu,yayın/baseline/commit yapılmadı.

[Önizleme](Level229-Ruzgar-Burgusu.png) · [Revizyon](Batches/226-230-Lantern-Wind-Crystal/Revisions/v4-229-lily-workshop/README.md)
