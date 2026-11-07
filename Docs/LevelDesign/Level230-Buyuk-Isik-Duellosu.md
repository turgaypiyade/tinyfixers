# 230 · Büyük Işık Düellosu / Grand Light Duel

**2026-11-07 güncel hamle: 30.** Bu tur yalnız hamle ayarı yapıldı (30→30); aşağıdaki önceki tasarım notu ve eski görseller tarihsel değerler içerebilir. [Güncel denge incelemesi](Reviews/225-234-Balance-2026-11-07/Review.md).

2026-11-06 · Agent taslağı; kullanıcı henüz oynamadı/onaylamadı.

9×10, 90 aktif hücre; **30 hamle · Boss Duel**. Katalog: `LevelCL_0230`, bölüm10. Asset: [LevelV1_00002300.asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002300.asset).

Hedefler: 660 Boss HP, 12 Elmas, 16 Fener, 16 Un. Dimerdal kutu kotaları: 6 + 6.

## Tasarımın dört sorusu

1. **İlk görüntü:** 9×10 düello arenası; üst ortada 4×4 fener sahnesi, iki yanda kristal locaları, dipte iki değirmen ve aralarında atış koridoru.
2. **İlk karar:** Fener sahnesinin kenarında büyük eşleşme kur; değirmenleri aynı special hattında yakalarken iki kristal locasını aç.
3. **Açılınca:** Sahne açılınca geniş orta alan birleşir. Her loca 6 elmas üretince kendi başına kalkar; değirmenler toplam 16 un için çuval yollar.
4. **Kalan iş:** Üç rakibin toplam 660 canı boyunca 12 elmas,16 fener ve 16 unu tamamla. Hedef dışı plastik/alet atışları karşı baskıdır.

## Yerleşim, katman ve besleme

9×10, üç boss dalgası ve toplam660 HP;30 hamle, oyuncu270 HP. Varsayılan BuildWaves dağıtımı kullanılır. Mavi85 ve kırmızı94 kutuların kotası6+6;16 Fener75; iki değirmen8+8 un. Karşı atışlar8/12/13 ve alet tehditleri47/48/51/50/49 hedeflerde yoktur.220’nin mevcut karşı baskı yapısı başlangıç alınmış,220 değiştirilmemiştir.

Başlangıç normal taş yuvası58; doğal beslenen56, mevcut kaynak kuralıyla başlangıç dolan2, gerçek asset renk pini0. Bilinçli başlangıç boşları: yok. Pinlerden tek başına zorunlu üçlü veya hazır special kurulmadı. Açılabilir engeller kalkınca bütün aktif hücreler doğal beslenir. Bu geometri kontrolüdür; kaskat/düşüş oyunu simüle edilmedi.

## Seçim ve tekrar incelemesi

133/102 konumsal;67 kırpılmış kısa listededir.133 karşılaştırmasında arena boyutu ortak olsa da üst fener sahnesi, yan bağımsız kaynaklar ve dip değirmenleri farklı açılma işi kurar.

Diğer adaylar: Dar/uzun boss maskesi: 10 satır sınırı ve büyük special ihtiyacıyla uyumsuzdu. Yalnız boss HP hedefi: başlangıçtaki fener/elmas/un işini görünmez bırakırdı.

Hamle gerekçesi: 230 Boss Duel: önceki 220’nin 620 HP/28 hamlesine karşı 660 HP/30 hamle; dört hedef,9×10 açık arena. Kaynaklar 6+6; un 8+8. İlk tahmin, oynanmış ayar değildir.

## Kontrol sınırı

Statik YAML/dizi/origin/footprint/katman/hedef/üretim/ikon/dolum/katalog/TR–EN ad kontrolleri geçti. Gerçek sprite PNG açılıp incelendi; renkler pin dışındaki yuvalarda temsilidir. Sağ kesit bir oynanmış durum değildir; gelecekte rastgele oluşacak un ve fenerler yerleştirilmiş gibi çizilmez.

1–225 ve mevcut runtime/art dahil3393 korunan dosya değişmedi. Oyun dengesi, oyuncu ilgisi ve CPU/GC/FPS ölçülmedi. Unity build/Play, bot veya düşüş/kaskat simülasyonu, yayın, release baseline ve commit yapılmadı.

[Gerçek sprite önizlemesi](Level230-Buyuk-Isik-Duellosu.png) · [Batch ve performans notu](Batches/226-230-Lantern-Wind-Crystal/README.md)
