# Level 107 — Çiçekli Madalyon

`LevelCatalogPro` global 107 → `LevelP_001140.asset`. Normal level, 9×11 zarf içinde 87 aktif hücre; 28 hamle başlangıç tahmini.

## Kompozisyon

Köşeleri kesilmiş, sağ-sol ve üst-alt simetrik madalyon. Dışta 28 hücrelik yeşil/çiçekli çerçeve; içte krem tonlu dört düz Wall şeridi; merkezde açık tonlu 5×3 engel bloğu. Merkezin üst ve alt satırları PlasticTwoStage, ortası HelmetPorcelain. Normal taşların ilk renkleri sağ-sol simetrik sabitlenir; refill normal dört renk havuzundan gelir.

`Level107-Cicekli-Madalyon.svg`, mevcut engel sprite'larını kullanan yerleşim önizlemesidir. Oyun ekran görüntüsü değildir; normal taşlar renk şemasıyla gösterilir. Wall autotile birleşimleri ve runtime çiçek dağılımı farklı olabilir.

```text
..FFFFF..
.FWWWWWF.
FoooooooF
FWoooooWF
FWPPPPPWF
FWHHHHHWF
FWPPPPPWF
FWoooooWF
FoooooooF
.FWWWWWF.
..FFFFF..
```

`.` hole, `o` normal taş, `F` GrassFlower, `W` Wall, `P` PlasticTwoStage, `H` HelmetPorcelain. Her düz Wall şeridi ayrı bir parçadır; toplam dört parça, her parça beş hücre.

## Hedefler ve kararlar

- 4 Wall parçası; parçanın tek bir hücresine odaklanmak bütün şeridi açar.
- 28 Grass; yalnızca çiçek katmanını dökmek yetmez, çim de temizlenir.
- 10 PlasticTwoStage ve 5 HelmetPorcelain; merkezde benzer renkleri farklı dayanıklılıklarla bir araya getirir.

Üst ve alt iç açıklıklarda eşleşme kurulur. Duvarları açıp dış çiçeklere erişimi artırmak veya merkezdeki engel bloğunu eritmek ilk karardır. Duvar şeritlerinin uçlarında geçişler bırakılmıştır. Hareketli merkez engelleri oyun ilerledikçe düşebilir; simetri açılış kompozisyonudur.

Wall 101, GrassFlower 105, PlasticTwoStage 47, HelmetPorcelain 37'de görülmüştür; 107'de yeni engel tanıtılmaz. Katmanlı engel, hazır special veya boss mekaniği eklenmez.

## Doğrulama ve denge sınırı

Asset dizileri, hole hücreleri, bağlantılı board, dört Wall parçasının bağlantıları/origin bilgileri, hedef adetleri ve katalog bağlantısı statik kontrol edildi. 24 açık normal taş hücresi vardır. Sabit açılışta hazır üçlü ya da aynı renk 2×2 yoktur; açık normal taşlar arasında 6 geçerli komşu takası bulunur. Bu sayım hareketli engellerle yapılabilecek takasları kapsamaz.

Unity, bot, build veya oynanış simülasyonu çalıştırılmadı. 28 hamlenin zorluk/kazanma oranı doğrulanmadı; kullanıcı oynayarak ayarlayacak.
