# Level 162 — Büyük Restorasyon

`LevelCatalogPro` global 162 → `LevelP_001660.asset`, chapter 7. **9×11 tam dikdörtgen, 24 hamle, dört renk.** 161'in rahat tanıtımından sonra, engel/katman yüküyle zorlaşan ve bol kırılma amaçlayan ilk tasarım. [Yerleşim ve taban önizlemesi](Level162-Buyuk-Restorasyon.png).

```text
ooooooooo
CRRoooRRC
CRRoooRRC
PPoooooPP
PPooQHoPP
PPoooooPP
PPoooooPP
PRRoooRRP
PRRoooRRP
CCCBBEECC
CCCBBEECC
```

`o` otomatik taş · `R` 2×2 RhodesStatue (70) · `C` chest2 (5) · `P` Mud (25) → plastic_orange (8) · `Q` hazır PulseCore · `H` hazır LineH · `B` tek 2×2 OverrideBatteryBox (43) · `E` yardımcı EggBird (45). Hole yoktur.

| Hedef | Adet | Yük |
| --- | ---: | --- |
| RhodesStatue | 4 | Dört heykelin her biri dört special isabeti ister |
| Mud | 20 | Önce tek vuruşlu turuncu örtü, sonra iki aşamalı çamur |
| chest2 | 14 | Üst yan çerçeveler ve alt tabanda iki vuruşlu sandıklar |

Dört heykel (1,1), (6,1), (1,7), (6,7); origin'ler **10, 15, 64, 69**. Her 2×2 dört hücreyle aynı origin'i paylaşır; goal 16 hücre değil dört heykeldir. Altın/turkuaz Rodos çerçeveleri, turuncu örtü sütunları ve ahşap taban toplu renk bölgeleri kurar. Maskenin kendisi geniş ve kesintisizdir: üstte tam dokuz hücre, ortada dört sıra boyunca beş hücre genişliğinde açık üretim alanı vardır.

Ortada (4,4) PulseCore ve (5,4) LineH yan yanadır; indeks 40/41. Oyuncu isterse ilk hamlede combo kullanabilir. Üç satır/üç sütun etkisi yanlardaki turuncu örtü bölgeleriyle kesişir; tek vuruşlu örtüler geniş kırılma alanı açar. Örtü altında çamur ve dört aşamalı heykeller kaldığından açılış gösterisi asıl hedefleri tek seferde tüketmek üzere kurulmadı. Gerçek kaskat ve toplam hasar miktarı oynanışta doğrulanmalıdır.

Yirmi turuncu örtü **Mud tabanı üzerinde**, `stackOrder=1`'dir; hiçbir sıvı sandığın/örtünün üstünde değildir. Plastic hareketlidir; kompozisyon gravity ile değişebilir. Plastic doğrudan goal yapılmadı: altındaki çamuru bitirmek örtülü alanları açmayı zaten gerektirir. Çamur yayılma hedefi değildir; ilave varil/su üretimi yoktur.

Alt kenarda bitişik yardım bölgesi: **bir 2×2 OverrideBatteryBox**, origin 84, ve yanındaki 2×2 grupta **dört EggBird**, indeks 86/87/95/96. Yardımcılar doğrudan hedef değildir. Batarya dört renk şarjıyla mevcut bir board dalgasını açar; dört aşamalı bütün heykelleri tek darbede sileceği varsayılmaz. Yumurtalar ikişer vuruşla üçer rastgele uygun hedefe kuş gönderir; belirli bir heykeli vuracakları garanti edilmez. Kalıcı batarya en alt iki sıradadır; altında dolması gereken normal hücre yoktur.

99 aktif hücre: **58 engelli + 41 taş hücresi**. Hazır combo dahil 41 taş hücresinin tamamına olası başlangıç spawn/izinli köşe yolu vardır. Diğer 39 taş rastgele, yalnız iki hazır special ve alt renkleri sabittir. Yoğunluk fiziksel hücreye göre 58/99'dur; toplam örtü/engel sayısı ile aynı ölçü değildir. Bir hücrede en fazla bir hareketli engel bulunur.

`python3 Tools/check_levels161_162.py` boyutları, hedefleri, bütün footprint/origin'leri, katmanları, sıvı tabanını, yardımcı bölgesini, hazır combo komşuluğunu, geniş orta alanı ve katalog bağlantısını kontrol eder. Rodos ilk kez katalog 161'de görülür; diğer bütün engeller daha önce tanıtılmıştır. Önceki level/test assetleri ve ortak library/hint/localization dosyaları korunur.

Yeni runtime/VFX kodu eklenmedi. Dört heykelin parça görünümleri kurulumda oluşturulup vuruşlar arasında kullanılır; mevcut `OnDisable` iptali ve paylaşılan taş parçacık havuzu korunur. Tek batarya ve dört kuş kaynağı mevcut servisleri kullanır. Kuşlar mevcut hatch başına hedef tarama/listelerini ve geçici uçuş görsellerini kullanmaya devam eder; bunlar havuzlanmış veya ölçülmüş bir iyileşme olarak sunulmaz. Yeni frame taraması/efekt üretim yolu eklenmedi; cihaz performansı ölçülmedi.

**24 hamle zor ilk ayardır; kazanılabilirlik/kazanma oranı ölçülmedi.** Unity Play/build, bot veya oyun simülasyonu çalıştırılmadı. Önizleme oyun ekran görüntüsü değildir; normal taş renkleri temsili, engeller ve iki hazır special assetteki gerçek konumlarındadır.
