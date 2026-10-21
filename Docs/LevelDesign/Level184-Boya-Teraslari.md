# Level 184 — Boya Terasları

Katalog184 → **LevelP_001880.asset**, chapter8. **9×11, 73 aktif/26 hole, 28 hamle, 4 renk, Normal.** Kullanıcının “yeni bir level daha” isteğiyle hazırlandı; henüz onaylanmadı. [Önizleme](Level184-Boya-Teraslari.png).

```text
SSoDooooD
SSo~~~~o~
SSoooo~o~
SSoooo~o~
..BBBB~o~
..BBBB~o~
..BBBB~o~
..BBBB~o~
......~o~
......~o~
......~o~
```

`o` rastgele normal taş; `.` hole; `S` special-only SculptingSpecial39; `B` 2×2 PaintCanBox63; `D` kanal kaynağı; `~` taş altında WaterChannel76. Haritada su temsili, normal taşlar gizlidir. Ana önizleme mevcut proje sprite'larıyla oluşturuldu; renkler temsilidir.

| Hedef | Adet | İş |
| --- | ---: | --- |
| PaintCanBox63 | **4 kasa** | Her kasada9 kutu; normal komşu isabet1, special isabet2 kutu düşürür; toplam36 kutu |
| WaterChannel76 | **2 kanal** | İki bağımsız kaynaktan alta çıkış; toplam23 açılacak yol hücresi |
| SculptingSpecial39 | **8** | Solda2×4 şerit, yalnız special isabeti |

Kompozisyon: üstte9 sütunluk geniş çalışma alanı, ortada sol kenarı tamamlayan bitişik dört boya kasası, sağda aşağı devam eden3 sütunluk su geçidi. Kasa originleri38/40/56/58; 4×4 terasın dış kenarı hole konturuna oturur, normal taş ortasında kopuk adacık değildir. İlk iki kasa üstten, sağdaki kasalar yandaki taşlardan vurulabilir. Sol alttaki kasa üstü veya sağı açılınca daha rahat erişilir; special'lar da bu yükü paylaşır. Yeni kasa alanı boşaldıkça çalışma alanı büyür; bütün kasaların sırayla kırılmasının kodla zorunlu olduğu iddia edilmez.

Birinci kanal bir tabanlı `(4,1)→(4,2)→(7,2)→(7,11)`; ikinci `(9,1)→(9,11)`. Birinci kaynak+13, ikinci kaynak+10 açılacak hücre içerir. Yollar kesişmez, yukarı çıkmaz, kasaların/pusulaların altına girmez. İki bitiş de alt kenardır. Açılan kanal hücreleri normal oynanır kalır; yalnız2kaynak geçirgendir. Hedef23hücre değil2tamamlanmış kanal. Bitmiş kanalda alttan mevcut şelale gösterimi çalışır.

47 açık başlangıç taş hücresinin tamamı doğrudan üstten dolum alabilir; kaynak hücreleri dikey geçirgendir, authored hole'lar taş tutmaz. Kapalı kutuların altında veya altında kalan normal taş cebi tasarlanmadı; maskede tek aktif bileşen ve köşeden tek başına bağlanan5/10düğümü yok. Alt3sütun dar olsa da iki kanal hücresi açıldıktan sonra da oynanır kalır; yalnız orta sütunla oynandığı varsayılmaz.

Fener, ColorCrate71–74, dolap, çamur/katı katman, başlangıç pini veya hazır special kullanılmadı. Bu aileler yasaklanmış sayılmaz; son iki levelin fener sütunu/bant düzeni bu levelde dinlendirilir. Yeni obstacle tanıtılmadı:39,63ve76 önceki gerçek katalogda mevcut. **28hamle ilk ayardır; %40–50 yönü ölçülmüş kazanma veya çözüm kanıtı değildir.**

Tekrar kontrolü1–183 üzerinde yapıldı. Konumda126 Antik Mühür, kırpılmış engel siluetinde48 Çamurlu Fenerler yakın çıktı. 135 Bölmeli Pencere aynı39+63 ailesini kullanır; orada hole ile ayrılmış dört oda ve tek boya kasası vardır. Burada tek bağlı board, dört bitişik kasa ve iki oynanabilir kanal yolu var. Basamak/L kütlesinin ilk kez kullanıldığı iddia edilmez. [İnceleme](Reviews/184-Terraces/Review.md), [sprite karşılaştırması](Reviews/184-Terraces/Comparison.png).

Statik kontrol:7dizi, maske, bütün origin/footprint'ler, normal dolum, kanal komşuluk/yön/kesişme/çıkış, hedef adedi, önceki tanıtımlar, katalog/GUID/isim ve korunma geçti; UnityYAML Ruby ile ayrıştırıldı. Mevcut sprite'lı PNG incelendi; PaintCanBoxView gibi auxiliary boş kasa üstüne4–3–2dizili9kutunun projedeki sprite'ı çizildi. Önceki **2.401 ayar/script/Resources dosyası**,179sonkullanıcıdüzeni ve183 dahil hash ile korundu. Katalog yalnız184eklendi,1–183isimleri korundu.

Performans: yeni runtime/VFX yok. İki kanalın23parçasında46Image,2depoda6Image,2köpükImage kurulumu vardır; akış yalnız aktifken kanal başına coroutine, sürekli board taraması yok. Tamamlanmada kanal başına bir şelaleRawImage; su shader zamanıyla akar. Damla Image'ları servis havuzunda yeniden kullanılır; eş zamanlı iki çıkışta7'şer damla gerekebilir. Her Splash'ta iki7elemanlıdizi maliyeti kalır. Flow finally goal-flight işini bırakır; Dispose abonelik/çıkış kökünü temizler, fakat Board coroutine'leri için ayrı iptal tanıtıcısı yoktur; canlı rebuild/teardown ölçülmedi.

Dört boya kasası36kutuImage kurar; PaintCanBoxView mevcut wobble/joltcoroutine'lerini yeniden başlatır. Düşen kutu için yeniGameObject/Image kopyası oluşturup host üzerinde uçurur ve normal bitişte Destroy yapar; bu uçuşların havuzlu veya iptal yolunun tam doğrulanmış olduğu söylenmez. Bu mevcut maliyet yeni efekt koduyla artırılmadı; sahne/cihaz profili yapılmadı. Standart taş/special havuzları korunur.

Authoring/snapshot/hash/doğrulama `Batches/184/`. UnityPlay/build, C#build, bot/kaskat simülasyonu çalıştırılmadı. 185 başlatılmadı.
