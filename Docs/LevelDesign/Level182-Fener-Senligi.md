# Level 182 — Fener Şenliği · v2

Katalog 182 → `LevelP_001860.asset`, chapter 7. **9×11, 81 aktif / 18 hole, 28 hamle, dört renk, Normal.** Kullanıcının parçalı kandil düzeni yerine tek sütun isteği uygulandı; v2 inceleme bekliyor. [Görsel önizleme](Level182-Fener-Senligi.png).

```text
..oLooo..
.ooLoooo.
oooLooooo
oooLooooo
oooLHHHoo
oooLHHHoo
oooLHHHoo
.ooLHHHo.
.ooLHHHo.
..oLHHH..
..oLHHH..
```

`o` otomatik normal taş; `.` hole; `L` **Lantern · 75**; `H` **HelmetPorcelain · 30**. Koordinatlar soldan ve üstten bir tabanlıdır: **4. sütunun tamamında 11 kandil**, hemen sağında **5–7. sütun / 5–11. satırda 3×7 porselen bloğu**. Alt blok boardun alt kenarını tamamlar. Boardun dış silueti ve 18 hole v1 ile birebir aynı; dama kaldırıldı. İsim, GUID ve katalog değişmedi.

| Hedef | Toplam | Açılış | Dolumdan ek kota |
| --- | ---: | ---: | ---: |
| Lantern · 75 | **30** | 11 | 19 |
| HelmetPorcelain · 30 | **21** | 21 | 0 |

Hedef sayıları ve 28 hamle korundu. Tek sütuna başlangıçtaki 30 kandil sığmadığı için 11 yerleştirildi; kalan 19 mevcut hedef dolumuyla gelir. **49 normal taş hücresi** açılışta dolum alabilir. Oyuncu sütunun yanındaki alanlarda special kurabilir; tek vuruşlu porselen bloğu açıldıkça çalışma alanı genişler. Açılış daha ferah, kandil toplama işinin bir kısmı devam eden doluma taşındı. Bu, aynı zorluk veya %40–50 geçiş garantisi değildir; oynanış dengesi ölçülmedi.

Lantern ilk hitte sönükten yanana geçer, ikinci hitte toplanır. İki stage de 1×1 hareketlidir ve renk kısıtı yoktur. Normal renkler rastgele; pin/hazır special/yardımcı/katman yok. ColorCrate 71–74 kullanılmadı. Goal ikonu yanan fener; mevcut sprite/hint kayıtları korunur. Yeni bir ışın/yayılma mekaniği eklenmedi.

**Tek sütun açılış düzenidir.** Hareketli engeller gravity ve swap ile yer değiştirebilir; yeni gelen kandiller aynı sütuna sabitlenmez. `CascadeLogic.TryPickMovableGoalToSpawn` kalan hedef ile yaşayan/gömülü ve planlanmış sayıları karşılaştırır. Lantern genel movable yolunda, pass başına tek movable sınırındadır; ID40'a özel hamle başına spawn kilidi ID75'i kapsamaz. Bu nedenle 19 yeni kandil, en az 19 oyuncu hamlesi demek değildir. Refill hızı ve bitişte kalan hedef riski Play ile doğrulanmadı; runtime bu görsel revizyon için değiştirilmedi.

Tekrar incelemesi 1–181 üzerinde yenilendi. Konum karşılaştırmasında 31 Boru Hattı ve 57 Katmanlı Depo'nun dikey kütleleri, kırpılmış siluette 48 Çamurlu Fenerler'in köşe kütlesi yakın çıktı. [Karşılaştırma](Reviews/182-Column/Comparison.png) ve [kayıt](Reviews/182-Column/Review.md). Dikey şerit yeni bir motif diye sunulmaz; bu revizyonun önceliği kullanıcının kandilleri tek sütunda görme isteğidir. Önceki dama incelemesi v1'e aittir ve arşivdedir.

Statik kontrol: yedi dizi, maske, 32 tek hücreli origin/footprint, tam sütun, bitişik 3×7 blok, hedef/refill farkı, sprite/hint, katalog/GUID ve isim kontrol edildi. 49 başlangıç taş hücresinin tamamının üstten dikey dolum yolu açık. Aktif maske tek bileşen; köşeden tek başına temas eden 5/10 düğümü yok. 181 dahil önceki levellar korundu. v1 baseline'ındaki 1.861 ayar/script dosyasının 1.860'ı aynı; mevcut ObstacleLibrary çalışma kopyası eski snapshot'tan farklı bulundu ve aynen korundu (`external-baseline-drift.json`). Bu revizyon library veya runtime dosyası yazmaz.

Performans incelemesi: yeni runtime/VFX kodu, update döngüsü veya tarama eklenmedi. `useTilePool` sahnede açık; mevcut `AcquireTileObject/ReleaseTile` yolları kullanılır. `TileView.Init/OnDisable` hareket token'larını, aktif settle'ı ve görsel durumu sıfırlar; level kurulumunda `ClearTilePool` eski referansları temizler. Genel kırılma FX havuzu kullanım anahtarı başına yeniden kullanır, normal lifetime sonunda parçacıkları durdurup objeyi geri alır; kapasite 24/key. Mevcut spawn hedef taraması ve `CountAliveOrStampedOrigins` board taramaları sürer; Lantern 75 için ayrı servis/nesne ağı eklenmedi. Genel `GoalFlyFx` uçuşlarında ghost GameObject/Image/Shadow tahsisleri ve normal uçuş sonunda Destroy vardır; bütün uçuşların havuzlandığı veya iptal yolunun kusursuz olduğu iddia edilmez. Scene teardown kök/çocuk ömrüne dayanır; cihaz profili veya Play doğrulaması yapılmadı. Bu tasarımda performans artışı iddia edilmez.

Önceki taslak asset/not/PNG ve doğrulama araçları `Batches/182/Revisions/v1/` içinde saklı. Yeni düzen `design.json`, tek seferlik `revise_column.py`, `verify.py` ve `validation.json` ile kayıtlı. Unity Play/build ve oyun/bot/kaskat simülasyonu çalıştırılmadı. 183 başlatılmadı.
