# Level 167 — Lazerli Restorasyon

`LevelCatalogPro` 167 → `LevelP_001710.asset`, chapter 7. **9×11, 95 aktif hücre / 4 hole, 40 hamle, dört renk.** Kullanıcının `IMG_7545.PNG` referansı. [Güncel önizleme](Level167-Lazerli-Restorasyon.png).

```text
TTTTTTVoo
TTTTTTVoo
TTTTTTVoo
LLMMLLVoo
LLMMLLVoo
.RRRRHVoo
.RRRRHVoo
.RRRRHVoo
.RRRRHVoo
LLMMLLVoo
LLMMLLVoo
```

`o` otomatik normal taş; `.` hole; T/V yatay/dikey Tube (26); L dört 2×2 LaserCannon (65); M üstte ve altta birer 2×2 MetalWall bloğu (56); R dört 2×2 RhodesStatue (70); H dört Hamster (57). Koordinatlar sıfır tabanlı, y üstten aşağıdır.

**Üstte üç yatay tube vardır.** Altlarındaki lazer, metal, Rodos ve hamster düzeni bir satır aşağı kaydırıldı. Üç yatay tube altışar, sağdaki dikey tube on bir hücredir; dört tüp toplam 29 hücre kaplar. Alt sırada yatay tube veya gel cabinet yoktur. Tahta yüksekliği 11 ve motor sınırı korunur.

## Tek gizli jel

Sol alt lazer origin'i **81**, yani (0,9), ayak izi **81/82/90/91**. Yalnız **90 = (0,10)** hücresinin tabanı `SpreadingGel (46)`; diğer üç taban hücresi boştur. Lazer `stackedObstacles` içinde `originCellIndex=81, obstacleId=65, stackOrder=1` olarak bu 2×2 alanı örter. Diğer üç lazer normal base obstacle'dır.

Jel lazerin altında **gizli ve mühürlüdür**: başlangıçta görünmez, yayılma kaynağı olmaz ve kaplama sayacına sayılmaz. Lazer kırılınca mevcut generic beneath akışı tek jel hücresini geri yükler; gel view oluşturulup UnsealCell çağrıldığında yayılmaya kaynak olur. Ardından oyuncu bu jeli eşleşme ve special zincirleriyle bütün tahtaya bulaştırır. Başlangıçta açık jel veya gel fırlatıcı yoktur.

Editörde LaserCannon'ı üst katmanda yerleştirmeyi engelleyen eski yasak kaldırıldı; zemin sıvıları hâlâ üst katmana konamaz ve tam örtme kontrolü korunur. Yeni runtime/VFX kodu gerekmez: mevcut StampStackedEntry / ClearObstacleFromLevel / RevealGelCell yolları kullanılır.

## Hedefler ve dolum

| Hedef | Adet |
| --- | ---: |
| SpreadingGel | **95 aktif hücrenin tamamı** |
| MetalWall | 2 blok (her biri 4 hücre) |
| Tube | 4 tüp |
| RhodesStatue | 4 heykel |
| Hamster | 4 hamster |

Başlangıçta engel olan yerler de 95'e dahildir; dört hole sayılmaz. Lazerler ayrı goal değildir, ancak tam jel kaplama için açılmaları gerekir. Beş hedef mevcut kompakt HUD / PreLevel desteğini kullanır; Rodos ikonu `Resources/RhodesStatue/S1_Base` referansıdır.

Lazer origin'leri **27/31/81/85**; Rodos origin'leri **46/48/64/66**. Üst metal bloğu **29/30/38/39** hücrelerinde ortak origin **29**, parça ID **1**; alt blok **83/84/92/93** hücrelerinde ortak origin **83**, parça ID **2** taşır. Her 2×2 tek parçadır ve hedefe bir kez sayılır. Blok içindeki bir hücre dört isabetle yıkım eşiğine ulaşınca dört hücrenin tamamı birlikte kalkar; hamlede vurulmayan hücre bir aşama geri gider. Rodos ve lazerler dörder special isabeti ister.

Tube entry'leri **0/Right/6**, **9/Right/6**, **18/Right/6**, **96/Up/11**. Hamster'lar **50/59/68/77** indekslerinde, x=5 sütunundadır; `hamsterSatietyOverride=4`. Her biri dört lokmayla doyar, mevcut iniş etkisini oynatıp ayrılır. Hedef 16 lokma değil dört hamsterdır; hareketli oldukları için başlangıç dizilimi zamanla değişebilir.

Sağdaki x=7/8 sütunlarında **22 normal taş hücresi** üstten doğrudan otomatik dolar. `pinnedTileTypes` / `pinnedSpecialTypes` tamamen sıfırdır; hazır special veya sabit renk eklenmedi. Lazer altındaki üç boş taban başlangıçta runtime örtüsüyle kapalıdır; erken normal taş doğmaz. Engeller açılınca alan genişler.

## Doğrulama ve performans

`python3 Tools/check_level167.py`: 99 elemanlı diziler, üç üst tube + dikey tube, mask, sıfır pinler, footprint/origin'ler, iki bağımsız 2×2 metal bloğu, beş goal, gizli tek jel ve 95 hücre kaplama, 22 hücrenin spawn erişimi, engeller açılınca aktif alanın bağlantısı, katalog/GUID doğrulanır. Revizyon öncesi hash listesi **345 korunan dosyanın**, katalog/meta ve diğer levellar dahil, aynı kaldığını doğrular.

`csi Tools/check_level167_hidden_gel.csx`: üretimdeki stamp, sealed-gel çizim dalı, beneath kayıt/pop, cover clear ve reveal metotları Unity doubles ile çalıştırılır. Lazer 2×2 alanı örter; tek jel başta görünmez/sayıma kapalı kalır; kırılmada yalnız (0,10) jel olur, diğer üç hücre boşalır, önce cover kaldırma olayı sonra jel unseal gelir. Komşu lazer korunur. Unity build/emit yapılmaz.

Yeni gameplay update döngüsü, efekt veya kare başı tarama eklenmedi. Tek mevcut gel overlay hücresi ve dört beneath kaydı kurulumda kullanılır; reveal mevcut jel renderer'ının yaşam döngüsündedir. Dört lazer ortak hedef/dönüş ve kafes havuzunu; dört Rodos mevcut parça view'lerini kullanır. Tek jel fırlatıcının kapak/gövde animasyonu ve alt yatay tüp artık bu levelde yoktur; bunun cihaz maliyetine etkisi ölçülmedi.

TubeView'in mevcut kesim efekti her hitte array/chip GameObject'leri üretir; bu task'ta havuz refaktörü yapılmadı. Dört tube'un toplam uzunluğu 29 hücredir; mevcut maliyet devam eder. Servislerin Clear/OnDestroy ve view DestroyView StopAllCoroutines yolları korunur. Canlı sahne çıkışı ve cihaz kare süreleri ölçülmedi.

**40 hamle ilk denge ayarıdır.** Gerçek kazanılabilirlik/kazanma oranı ölçülmedi; Unity Play/build, bot ve oyun/kaskat simülasyonu çalıştırılmadı. PNG mevcut görsellerle yerleşim şemasıdır, normal renkler temsili; yanındaki mini şema yalnız sol alt lazer kırılınca açılan tabanı gösterir.
