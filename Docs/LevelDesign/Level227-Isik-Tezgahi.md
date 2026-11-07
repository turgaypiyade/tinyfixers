# 227 · Işık Tezgâhı / Lightwork Bench

**2026-11-07 güncel hamle: 26.** Bu tur yalnız hamle ayarı yapıldı (26→26); aşağıdaki önceki tasarım notu ve eski görseller tarihsel değerler içerebilir. [Güncel denge incelemesi](Reviews/225-234-Balance-2026-11-07/Review.md).

2026-11-06 · Agent taslağı; kullanıcı henüz oynamadı/onaylamadı.

9×10, 88 aktif hücre; **26 hamle · Normal**. Katalog: `LevelCL_0227`, bölüm10. Asset: [LevelV1_00002270.asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002270.asset).

Hedefler: 5 Lamba çerçevesi, 12 Oyma taşı, 17 İki aşamalı plastik. Dimerdal kutu kotaları: yok.

## Tasarımın dört sorusu

1. **İlk görüntü:** İki köşesi kırpılmış tezgâh; iki aşamalı plastikten üst/sol L konturu, üst sağda dikey, alt solda yatay lamba rafları.
2. **İlk karar:** Üst soldaki 4×4 çalışma yüzünde rafın rengine uygun eşleşme hazırla; dikey rafı mı, yatay rafı mı önce bitireceğini seç.
3. **Açılınca:** Bir lamba çerçevesi tamamlanınca üç hücre birlikte açılır. Oyma bloğunun üst ve sol yüzü açıldıkça alt sağa special ulaştırmak kolaylaşır.
4. **Kalan iş:** Beş çerçeveyi bitir, üç vuruşlu 12 oyma taşını ve 17 iki aşamalı plastiği topla; iki kayan rafın boşluklarını zamanla.

## Yerleşim, katman ve besleme

Üç dikey ve iki yatay LampStrip79, her biri üç lamba: 15 lamba, 5 çerçeve hedefi. İki çerçeve kayar; biri ters yöndedir. Üst/sol kontur17 PlasticTwoStage40; alt sağdaki bitişik3×4 alan12 SculptingStone28. Plastik hareketlidir; konturun bütün oyun boyunca sabit kalacağı varsayılmaz. Ek plastik üretimi yoktur.

Başlangıç normal taş yuvası44; doğal beslenen40, mevcut kaynak kuralıyla başlangıç dolan0, gerçek asset renk pini0. Bilinçli başlangıç boşları: [42, 65, 83, 88]. Pinlerden tek başına zorunlu üçlü veya hazır special kurulmadı. Açılabilir engeller kalkınca bütün aktif hücreler doğal beslenir. Bu geometri kontrolüdür; kaskat/düşüş oyunu simüle edilmedi.

## Seçim ve tekrar incelemesi

173/177/45 ve kırpılmış94 yakın adaylardır.173 ile görsel karşılaştırmada ortogonal raf ritmi ortak; üç dikey/iki yatay çerçeve, L plastik ve bitişik oyma kütlesi yeni iş sırası kuruyor.

Diğer adaylar: Dokuz dikey/yatay rafın tamamını kopyalamak: çalışma yüzünü fazla kapatırdı. Mor-only bütün lambalar: referansın renk eşleştirme kararını sürekli special zorunluluğuna çevirirdi.

Hamle gerekçesi: 227 normal yükseliş: 15 renkli lamba beş ayrı çerçeveye bağlı; iki raf kayar. Plastik ve oyma bloğu farklı vuruş yükleri verir; 26 hamle ilk ayar.

## Kontrol sınırı

Statik YAML/dizi/origin/footprint/katman/hedef/üretim/ikon/dolum/katalog/TR–EN ad kontrolleri geçti. Gerçek sprite PNG açılıp incelendi; renkler pin dışındaki yuvalarda temsilidir. Sağ kesit bir oynanmış durum değildir; gelecekte rastgele oluşacak un ve fenerler yerleştirilmiş gibi çizilmez.

1–225 ve mevcut runtime/art dahil3393 korunan dosya değişmedi. Oyun dengesi, oyuncu ilgisi ve CPU/GC/FPS ölçülmedi. Unity build/Play, bot veya düşüş/kaskat simülasyonu, yayın, release baseline ve commit yapılmadı.

[Gerçek sprite önizlemesi](Level227-Isik-Tezgahi.png) · [Batch ve performans notu](Batches/226-230-Lantern-Wind-Crystal/README.md)
