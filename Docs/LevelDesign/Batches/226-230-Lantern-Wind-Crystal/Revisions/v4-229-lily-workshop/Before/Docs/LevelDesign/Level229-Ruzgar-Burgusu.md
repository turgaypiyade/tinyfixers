# 229 · Rüzgâr Burgusu / Crystal Whirl

2026-11-06 · Yeni agent taslağı; kullanıcı henüz oynamadı/onaylamadı.

9×11,99 aktif hücre; **28 hamle · Super Hard · 4 renk**. Aktif V1 katalog `LevelCL_0229`, bölüm10. [Level asseti](../../Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset). Mevcut TR/EN isim ve GUID korunur.

| Hedef | Adet |
| --- | ---: |
| Jel kaplama | 99 |
| Oyma taşı | 12 |
| Rodos heykeli | 2 |
| Porselen | 12 |

## Tasarımın dört sorusu

1. **İlk görüntü:** Soldan merkeze kıvrılan bitişik oyma omurgası; sağda iki porselen sütunu, tabanda iki jel presi ve iki Rodos.
2. **İlk karar:** Oyma omurgasının hangi yüzünden jeli açacaksın; iki presi birlikte vurabilecek special hattını nasıl kuracaksın?
3. **Açılınca:** Her oyma taşı altındaki gerçek jel hücresini açar. Yukarı bakan presler ortadaki iki sütunun açılmasını ve jelle boyanmasını sağlar.
4. **Kalan iş:** Jeli99 hücreye taşı; special isteyen Rodosları ve sağdaki12 porseleni bitir. Örtü altından sandık toplama döngüsü kullanılmadı.

## Katman, erişim ve hamle

12 SpreadingGel46 taban hücresinin üzerinde12 SculptingStone28 var; oyma üç vuruşlu. Çim ve sandık yok. GrassFlower58 / GrassWaterLily69 havuzda tutuldu, bu levele zorunlu eklenmedi. İki GelLauncherUp59 tabanda bitişik1×2 gövdeler; special ile iki vuruşta çalışır, kendi hücrelerini de jelle kaplayarak kalkar. Mevcut yardımcı kuralıyla pres için ayrı hedef konmaz.99 jel hedefi preslerin ve heykellerin kapladığı hücreleri de kapsar.

İki2×2 RhodesStatue70 tabana oturur; heykel başına dört special vuruşu gerekir.12 hareketli HelmetPorcelain30 sağda2×6 sütun oluşturur; gövdelerin altı Rodoslarla desteklenir, açılış yerleşimi havada bırakılmadı. Porselen hedefi gerçek başlangıç adedi12’dir; ek üretim kotası yok.

Başlangıçta63 normal taş yuvasının63’ü doğal beslenir; pin,hazır special,boş yuva yok. Oyma kalkınca75/75; presler de kalkınca79/79; bütün temizlenebilir parçalar kalkınca99/99 beslenir. Bunlar seri oyun hamleleri değil, bağımsız statik katman/geometri kontrolleridir. Taş renkleri temsilidir.

28 hamle ilk tahmin: üç vuruşlu omurgaya erişim, iki presi çalıştırmak, iki Rodos için special hazırlamak ve jeli tüm alana taşımak birlikte düşünülmüştür. Geçiş oranı veya oynanmış denge iddiası yok.

## Havuz ve tekrar incelemesi

Güncel ObstacleLibrary ve aktif katalog üzerinden tüm havuz değerlendirildi. Dimerdal/değirmen son228/230 ve eski229’daki kaynak toplama fikrini tekrar ettirecekti. Boru/kanal/mıknatıs yolları, diğer örtüler/sıvılar ve yumurta/roket/pil yardımcıları da değerlendirildi; bu fikri dağıtmamak için eklenmedi. Yeni mekanik tanıtımı yok.

Statik kısa listenin konumsal en yakını114, kırpılmış en yakını183 gerçek sprite’larla açıldı:114’te solda kasa bandı, üstte yumurtalar ve altta çim örtülü geniş oyma alanı;183’te aynalı iki taş basamağı ve ortada plastik/yanlarda boya kutuları var.229 tek çapraz omurga ve tabandaki pres–Rodos ilişkisiyle farklı açılır. Basamak ritmi ortak bir görsel ögedir; benzerlik puanları özgünlük/onay yüzdesi değildir. [Karşılaştırma](Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/Comparison.png).

## Kontrol sınırı

Bağımsız YAML/dizi/katman/origin/footprint/hedef/ikon/besleme/katalog/TR–EN ad kontrolü geçti.6055 diğer proje dosyası,1–228 ve230 korundu;225/226’daki mevcut kullanıcı düzenlemeleri korunur. Gerçek sprite PNG ve katman kesiti açılıp incelendi; görsel değerlendirme agent’a aittir. CPU/GC/FPS ve oynanmış denge ölçülmedi. Unity build/Play,bot/düşüş/kaskat simülasyonu,yayın,baseline veya commit yapılmadı.

[Önizleme](Level229-Ruzgar-Burgusu.png) · [Revizyon kaydı ve performans incelemesi](Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/README.md)
