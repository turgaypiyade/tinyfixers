# 226 · Rüzgâr Kemeri / Windlit Arch

2026-11-06 · Agent taslağı; kullanıcı henüz oynamadı/onaylamadı.

9×11, 87 aktif hücre; **28 hamle · Normal**. Katalog: `LevelCL_0226`, bölüm10. Asset: [LevelV1_00002260.asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002260.asset).

Hedefler: 5 Duvar, 20 Fener, 16 Un. Dimerdal kutu kotaları: yok.

## Tasarımın dört sorusu

1. **İlk görüntü:** İki açıklığı bağlayan asimetrik kemer; solda iki bitişik değirmen, sağda tek büyük duvar ve dört küçük kapağın altında fenerler.
2. **İlk karar:** Geniş üst sıradan sağ duvarın bir hücresine yoğunlaş veya solda değirmenlere uzanan special hazırla.
3. **Açılınca:** Dört küçük duvar kapağı kırıldıkça altlarındaki fenerler görünür. Sağdaki iki seçili sütun kalan 16 feneri üretir; büyük duvar da orta çalışma yüzünü açar.
4. **Kalan iş:** 20 feneri yakıp topla; değirmen çuvallarından gelen toplam 16 unu temizle. Sol ve sağ işler aynı hamlede ilerletilebilir.

## Yerleşim, katman ve besleme

Dört gerçek Fener75, dört ayrı Wall55 kapağının altındadır. Büyük duvar tek parçadır: toplam 16 duvar hücresi, 5 duvar hedefi. Başlangıçta dört fener; yalnız sağdaki iki sütundan (sıfır tabanlı 7/8) 16 ek fener, toplam20. Genel fallback kapalıdır. İki değirmenin çuvalları 8+8 un üretir. Üst kapaklar açılmadan seçili sütunlar fener basmaz.

Başlangıç normal taş yuvası63; doğal beslenen43, mevcut kaynak kuralıyla başlangıç dolan0, gerçek asset renk pini20. Bilinçli başlangıç boşları: yok. Pinlerden tek başına zorunlu üçlü veya hazır special kurulmadı. Açılabilir engeller kalkınca bütün aktif hücreler doğal beslenir. Bu geometri kontrolüdür; kaskat/düşüş oyunu simüle edilmedi.

## Seçim ve tekrar incelemesi

78 ve87 konumsal;204 kırpılmış benzerlik kısa listesine girdi.78 ile gerçek sprite karşılaştırmasında yeni kemerin iki bağlantısı, sağ örtülü fener sütunları ve sol değirmen işi ayrışıyor.

Diğer adaylar: Referansı birebir uzun S olarak kopyalamak: iki kısa köprü ve üst ortak çalışma sırası seçildi. Bütün fener kotasını tek-hücre beş-hit duvarlarla örtmek: aşırı vuruş yükü olurdu; yalnız dört gerçek içerik kapağı ve bir büyük duvar parçası seçildi.

Hamle gerekçesi: 226 rahatlama: bir büyük parça + dört fener kapağı, iki değirmen ve 20 fener. Katmanlara erişim için 28 hamle ilk ayar.

## Kontrol sınırı

Statik YAML/dizi/origin/footprint/katman/hedef/üretim/ikon/dolum/katalog/TR–EN ad kontrolleri geçti. Gerçek sprite PNG açılıp incelendi; renkler pin dışındaki yuvalarda temsilidir. Sağ kesit bir oynanmış durum değildir; gelecekte rastgele oluşacak un ve fenerler yerleştirilmiş gibi çizilmez.

1–225 ve mevcut runtime/art dahil3393 korunan dosya değişmedi. Oyun dengesi, oyuncu ilgisi ve CPU/GC/FPS ölçülmedi. Unity build/Play, bot veya düşüş/kaskat simülasyonu, yayın, release baseline ve commit yapılmadı.

[Gerçek sprite önizlemesi](Level226-Ruzgar-Kemeri.png) · [Batch ve performans notu](Batches/226-230-Lantern-Wind-Crystal/README.md)
