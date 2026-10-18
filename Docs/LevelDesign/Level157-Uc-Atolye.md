# Level 157 — Üç Atölye

`LevelCatalogPro` global 157 → `LevelP_001610.asset`, chapter 7. [Yerleşim önizlemesi](Level157-Uc-Atolye.png). Kullanıcının ikinci referansındaki üç sütun, iki dar kanal ve üç ayrı alt cep kullanıldı: **8×9, 36 hamle, dört renk**.

```text
oooooooo
PPoPPoPP
PPoPPoPP
AAoAAoAA
AAoAAoAA
AAoAAoAA
TToTToTT
TToTToTT
HH.HH.HH
```

`P` 2×2 PaintCanBox (63), `A` HelmetPorcelain (30), `T` 2×2 TarpStatue (68), `H` HatLauncher (31), `o` normal taş, `.` hole. Çok hücreli engellerin dört hücresi aynı origin'i paylaşır.

Hedefler **3 boya kasası + 3 brandalı heykel + 18 porselen + 24 enerji**. Şapkalar doğrudan temizleme hedefi değildir. Altı şapka ortak 24 enerji hedefini besler; miktar şapka başına 24 değildir. HUD/prelevel enerji ikonu HotContainerV1 olarak atanmıştır.

- Üstte üç boya kasası: kırmızı, sarı, mavi. Kasalar dokuzar kutu içerir; normal komşu vuruş bir, special vuruş iki kutu çıkarır. Goal kasa başına bir kez ilerler.
- Ortada üç bitişik 2×3 porselen grubu: referanstaki saydam blokların yerine tek vuruşlu, gravity ile hareket eden açık renkli engeller. Böylece aynı tasarımda yine PlasticTwoStage tekrarlanmadı; ilk büyük kasalar açıldıkça alan genişler.
- Altta üç 2×2 brandalı heykel. Soldan sağa **12 kırmızı, 12 sarı, 12 mavi** taş toplamak brandayı açar; ardından üç special vuruşu gerekir. Üçünün de varsayılan 30 kırmızı istememesi için `tarpStatues[]` açıkça yazıldı.
- En altta 2+2+2 HatLauncher; iki kanalın altı hole olduğundan kalıcı şapkaların altında doldurulamayan normal hücre bırakılmaz.

Diğer kütüphane aileleri de yerleşim açısından değerlendirildi: Cargo düz çıkış rotası ister, bilardo topu cep ister; yağ/çamur ve yayılan jel bu dar kanallı tasarımda ayrı bir yayılma hedefi eklerdi. Mıknatıs çift uç/bağlantı alanı, nilüfer ise yutma/geri atma için açık komşuluk ister. Bu levelde renk toplama → special üretme → enerji çıkarma ilişkisi seçildi; yardımcı RocketBasket veya OverrideBatteryBox hedef eklemek için kullanılmadı.

22 sabit normal başlangıç taşı, otomatik eşleşme yok, **altı geçerli açılış takası** ve en az bir special üreten eşleşme fırsatı var. Hazır special yok; refill rastgele. İki normal kanal üstten aşağı kesintisiz açık. Kasalar ve heykeller çapraz geçişe izin veren mevcut kütüphane ayarını kullanır. Porselenlerin hareketli olmasıyla görüntü ilk hamlelerden itibaren değişebilir.

70 aktif hücre, iki hole. Yedi dizi 72 eleman; footprint/origin'ler, üç renk/sayaç kaydı, hedefler, ikonlar, katalog/GUID ve önceki assetlerin korunması statik denetlendi. Katman yok; sıvı üst katman eklenmedi. Yeni gameplay/VFX kodu eklenmedi. 36 hamle ilk denge ayarı; Unity/build/bot çalıştırılmadı, kazanma oranı ve cihaz performansı ölçülmedi. Önizleme yerleşim şemasıdır.
