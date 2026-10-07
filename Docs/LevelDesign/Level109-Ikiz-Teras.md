# Level 109 — İkiz Teras

`LevelCatalogPro` global 109 → `LevelP_001160.asset`. Normal level; 9×11 dış çerçevenin tamamı kullanılır. 6. satırın ortasındaki beş hole çıkarılınca 94 aktif hücre kalır. Başlangıç ayarı 26 hamledir.

## Kompozisyon

Üstte sıcak ahşap tonlarında 7×2 chest3 bloğu, altta açık tonlu 7×2 PlasticTwoStage bloğu. Ortadaki yatay boşluğun üzerinde üç RocketBasket yan yana tek bir grup oluşturur. Boşluk iki yandaki ikişer hücrelik geçişle çevrelenir; board tek bağlantılı alan olarak kalır. Maske, engeller ve açılış taş renkleri sağ-sol simetriktir.

```text
ooooooooo
oCCCCCCCo
oCCCCCCCo
ooooooooo
oooRRRooo
oo.....oo
ooooooooo
ooooooooo
oPPPPPPPo
oPPPPPPPo
ooooooooo
```

`o` normal taş, `.` hole, `C` altında Mud olan chest3, `P` PlasticTwoStage, `R` RocketBasket. Koordinatlar üstten aşağıdır; 6. satır `y=5`.

`Level109-Ikiz-Teras.svg` mevcut sprite'larla yerleşim önizlemesidir; oyun ekran görüntüsü değildir. Taşlar renk şemasıyla gösterilir. Örtü altındaki çamur ayrı görünür katman olarak çizilmez.

## Hedefler ve yardım

- 14 chest3: üç vuruşlu üst blok.
- 14 Mud: sandıkların altındaki iki katmanlı zemin. Sandıkları açtıktan sonra aynı bölge üzerinde çalışmayı sürdürme nedeni.
- 14 PlasticTwoStage: iki vuruşlu, hareketli alt blok; oynanış sırasında düşebilir.
- Üç RocketBasket **goal değildir**. Yan yana grup, kırmızı/sarı/mavi eşleşmelerle toplam dokuz roket yardımı sunar; `rocketBasketFireAllOnHit=0`. Bütün sepetlerin boşalması doğrudan kazanma şartı değildir.

chest3 ilk 14, Mud ilk 4, PlasticTwoStage ilk 47, RocketBasket ilk 71'de kullanılmıştır. Yeni engel veya boss tasarımı yoktur.

## Amaçlanan kararlar

Üst sandıkları açıp çamura geçmek ile alt blokta alan kazanmak arasında seçim yapılır. Yardım grubunu kullanmak eşleşme hazırlığı ister. Ortadaki hole şeridi, orta sütunlarda dikey eşleşmeleri böler; iki yan geçiş ve special kullanımı değer kazanır.

108'in kolay geçtiği geri bildirimi dikkate alındı: yardım paketi 4 yumurta + 2 sepetten yalnız 3 sepete indirildi; hedefler çok vuruşlu iki büyük blok ve sandık altı çamur olarak düzenlendi. Bu zorluk niyetidir; oynanış sonucu doğrulanmadı. 108 asset'i değiştirilmedi.

## Statik kontrol

99 elemanlı diziler, 94 aktif hücre, beş hole, dolu dış sınır, bağlı board, doğru origin'ler, 14 chest-over-Mud katmanı, hedef adetleri ve katalog bağlantısı doğrulandı. 63 açık normal taş hücresi vardır. Açılışta hazır üçlü veya aynı renk 2×2 yoktur; normal taşlar arasında 9 geçerli takas bulunur ve hem üstte hem altta hamle vardır.

Bu takaslar ilk doğrudan eşleşmeyle sepette yüklü bir rengi ateşlemez; sonraki refill/cascade sonuçları hesaplanmadı. Hareketli plastikle takaslar sayılmadı. Hazır special verilmedi.

Unity, bot, build veya oynanış simülasyonu çalıştırılmadı. Kazanma oranı ve 26 hamlenin dengesi kullanıcı tarafından oynanarak değerlendirilecek.
