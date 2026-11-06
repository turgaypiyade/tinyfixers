# 229 · Rüzgâr Burgusu / Crystal Whirl

2026-11-06 · Agent taslağı; kullanıcı henüz oynamadı/onaylamadı.

9×11, 87 aktif hücre; **27 hamle · Super Hard**. Katalog: `LevelCL_0229`, bölüm10. Asset: [LevelV1_00002290.asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset).

Hedefler: 16 Elmas, 24 Un, 12 Sarı renkli sandık, 12 Çim. Dimerdal kutu kotaları: 6 + 10.

## Tasarımın dört sorusu

1. **İlk görüntü:** Dört değirmenin çevirdiği çapraz burgu; ortada çim örtülü L biçimli sarı sandık kütlesi, iki farklı kıvrımda kristal kutuları.
2. **İlk karar:** Ortadaki çimi açıp sarı sandık yüzü mü kuracaksın, çevredeki değirmenlere special gönderip un işini mi başlatacaksın?
3. **Açılınca:** Çim altından üç vuruşlu sarı sandıklar çıkar; sandık L’si kalkınca değirmenler ve mavi/yeşil kutular arasında geniş special hatları açılır.
4. **Kalan iş:** Yeşil kutunun 6 ve mavi kutunun 10 elmasını ayrı üret/topla; dört çuvaldan toplam 24 unu,12 sandığı ve 12 çimi bitir.

## Yerleşim, katman ve besleme

Ortadaki12 sarı ColorCrate71 üzerinde12 Grass42; çim üst örtüdür. Dört Windmill80 toplam24 un (çuval başına6) sağlar. Yeşil Dimerdal88 kota6, mavi Dimerdal85 kota10; iki kutu bağımsız tükenir, ortak mor Diamond hedefi16’dır. Fener eklenmeyerek dört hedefte kalındı.

Başlangıç normal taş yuvası51; doğal beslenen43, mevcut kaynak kuralıyla başlangıç dolan0, gerçek asset renk pini8. Bilinçli başlangıç boşları: yok. Pinlerden tek başına zorunlu üçlü veya hazır special kurulmadı. Açılabilir engeller kalkınca bütün aktif hücreler doğal beslenir. Bu geometri kontrolüdür; kaskat/düşüş oyunu simüle edilmedi.

## Seçim ve tekrar incelemesi

169 konumsal;12/173/117 kırpılmış yakın adaylardır.169 ile görsel karşılaştırma burgu çevresindeki dört değirmen, merkezi örtülü L ve eşit olmayan iki bağımsız üretim işini ayırır.

Diğer adaylar: Aynalı iki büyük kanat: önceki seviyelerin varsayılan simetrisini tekrar ederdi. Beşinci bir fener hedefi eklemek: mevcut renk/katman/üretim kararlarına anlamlı yeni iş katmadan hedef listesini uzatırdı.

Hamle gerekçesi: 229 Super Hard: dört değirmen, iki katmanlı sarı L ve 6+10 bağımsız kaynak; 27 hamle ilk tahmin. Erişim sırası ve special birleştirme kritik.

## Kontrol sınırı

Statik YAML/dizi/origin/footprint/katman/hedef/üretim/ikon/dolum/katalog/TR–EN ad kontrolleri geçti. Gerçek sprite PNG açılıp incelendi; renkler pin dışındaki yuvalarda temsilidir. Sağ kesit bir oynanmış durum değildir; gelecekte rastgele oluşacak un ve fenerler yerleştirilmiş gibi çizilmez.

1–225 ve mevcut runtime/art dahil3393 korunan dosya değişmedi. Oyun dengesi, oyuncu ilgisi ve CPU/GC/FPS ölçülmedi. Unity build/Play, bot veya düşüş/kaskat simülasyonu, yayın, release baseline ve commit yapılmadı.

[Gerçek sprite önizlemesi](Level229-Ruzgar-Burgusu.png) · [Batch ve performans notu](Batches/226-230-Lantern-Wind-Crystal/README.md)
