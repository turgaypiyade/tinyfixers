# Level 152 — Basamaklı Atölye

`LevelCatalogPro` global 152 → `LevelP_001560.asset`, chapter 7. Kullanıcının ilk görselindeki 9×11 basamaklı yerleşim, 30 hamle ve dört renk. Görsel: [önizleme](Level152-Basamakli-Atolye.png).

```text
ooooooSSS
ooooooSSS
ooooooSSS
ooooooSSS
PPPPPPSSS
PPPPPPSSS
PPPPPPSSS
.EEPPPSSS
.EEPPPSSS
...PPPGGG
......GGG
```

`.` hole · `o` rastgele taş · `P` PlasticTwoStage (40) · `S` SculptingStone (28) · `E` EggBird (45) · `G` GelLauncherUp (59, her sütundaki iki hücre tek kartuş).

| Hedef | Adet | Davranış |
| --- | ---: | --- |
| PlasticTwoStage | 27 | İki aşamalı, hareketli; sol blok ve orta alt basamak |
| SculptingStone | 27 | Sağdaki üç sütun, üç aşama; normal/special vuruş alır |
| GelLauncherUp | 3 | Sağ alt kenarda 1×2; iki special vuruşundan sonra yukarı ateşler |

Sol alt makine yerine bitişik 2×2 bölgeye dört EggBird kondu. Yumurtalar mevcut iki aşamalı kuş yardımını kullanır; hedef değildir. Sağ altta, kullanıcının jel kartuşu isteği GelLauncherUp ile karşılandı. Kartuşların üstünde taş sütunları vardır; ateşleri yukarıdaki engelleri açabilir. Jel kaplama ayrı kazanma hedefi değildir; kartuşların mevcut jel yayma davranışı korunur.

Temel `Stone` (1) kaydının sprite GUID’i mevcut dosyalarda çözümlenmediği için sağ şeritte görselleri ve aşamaları mevcut SculptingStone seçildi. Ortak ObstacleLibrary değiştirilmedi.

24 açık başlangıç hücresi vardır. Başlangıç renkleri ve special’lar sabitlenmez; iki pin dizisi tamamen sıfırdır. Taşları mevcut oyun üretir. 88 aktif hücre, 11 hole; yedi hücre dizisi 99 eleman. Kartuş origin’leri (6,9), (7,9), (8,9); kapladıkları alt hücreler aynı origin’i paylaşır. Kalıcı engellerin altında normal taş cebi bırakılmaz.

Maske bağlılığı, engel ayak izleri/origin’leri, hedef adetleri, GUID/katalog bağlantısı ve sprite referansları statik kontrol edildi. Önceki level assetleri ve ortak kütüphane dosyası hash karşılaştırmasında değişmedi. Yeni runtime/VFX kodu, kare başı tarama veya kaynak yükleme yolu eklenmedi; mevcut kuş/kartuş efektleri ve temizliği kullanılır. Cihaz performansı ölçülmedi.

Hamle sayısı ilk oynanış ayarıdır. Unity Play/build, bot veya oyun simülasyonu çalıştırılmadı. Önizleme yerleşim şemasıdır, oyun ekran görüntüsü değildir.
