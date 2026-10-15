# Level 151 — Nilüfer Göleti

[Yerleşim önizlemesi](Level151-Nilufer-Goleti.png) · Asset: `Assets/_Project/Settings/ProductionLevels/LevelP_001550.asset` · Katalog: `LevelCatalogPro`, chapter 7 / level 151 / `LevelCL_0151`.

**9×11, 73 aktif hücre / 26 hole, 32 hamle, dört renk.** Normal level; GrassWaterLily'nin ilk üretim kullanımıdır. Kullanıcı ilk tasarımı kolay buldu: soldaki iki nilüfer ve sağdaki iki nilüfer kendi aralarında dikey birleştirildi. Başlangıçta taş renkleri sabitlenmez; oyun rastgele üretir. Hamle sayısı oynanarak ayarlanır, kazanma oranı ölçülmedi.

## Kompozisyon

Solda ve sağda beşer pembe nilüferden oluşan **iki kesintisiz sütun** vardır. Aralarındaki üç sütun special üretme alanıdır. Krem baykuş kasklar üst/alt kemerleri, çamur iki yan kıyıyı oluşturur. Board maskesi ve engeller sağ-sol simetriktir; önceki dış siluet korunur.

```text
...ooo...
..KKoKK..
.KoooooK.
MoLoooLoM
MMLoooLMM
.oLoooLo.
MMLoooLMM
MoLoooLoM
.KoooooK.
..KKoKK..
...RoR...
```

`.` hole · `o` rastgele normal taş alanı · `L` GrassWaterLily (69) · `M` Mud (25) · `K` HelmetPorcelain (30) · `R` RocketBasket (38). Koordinatlar sıfır tabanlı, üstten aşağıdır. Nilüfer ve çamur altında da oyun tarafından taş üretilir.

| Hedef | Adet | Yerleşim / amaç |
|---|---:|---|
| GrassWaterLily | 10 | x=2 ve x=6, y=3–7. Her sütunda bitişik beş çiçek; dört yaprak ve alttaki çim tamamlanınca bir nilüfer sayılır. |
| Mud | 12 | İki simetrik kıyı, y=3–4 ve y=6–7; mevcut iki aşamalı çamur. |
| HelmetPorcelain | 12 | Üst/alt kemerler, y=1–2 ve y=8–9; bir vuruşlu hareketli kasklar. |

İki RocketBasket (3,10) ve (5,10) alt kenarda yardımcıdır, hedef değildir. Normal renk başına bir roket kuralı korunur; `rocketBasketFireAllOnHit=0`. Yardımcıların nilüferleri kesin hedefleyeceği varsayılmaz. Toplam dört authored engel türü ve üç hedef vardır; nilüfer altındaki Grass ayrıca yerleştirilmez veya ayrı hedef yapılmaz.

## Oynanış

- Nilüfer sayısı **4 → 10**; başlangıçtaki iki hazır LineH ve bütün sabit taş renkleri kaldırıldı. Oyuncu special'larını kendisi oluşturur. Hedef yükü arttığı için hamle 32'de tutuldu; aynı anda ayrıca azaltılmadı.
- x=3–5 orta alanı, iki nilüfer sütununun arasındaki y=3–7 satırlarında açıktır. Yatay special aynı sıradaki iki nilüfere; nilüfer sütununa hizalanan dikey special ise bir sütundaki beş nilüfere birden erişebilir. Bu, tek tek hedefleme ile sütun boyunca hasar verme arasında tercih yaratır.
- Komşu special ilk görüldüğünde yutulmaz. Bir sonraki gerçek oyuncu hamlesi çözülünce aynı taş aynı komşu hücrede hâlâ duruyorsa alınır. Düşüş/cascade special'ı yerinden oynatırsa bekleme baştan başlar.
- İçeride special varken ilk special vuruşu onu tahtaya geri yollar; otomatik patlamaz. Sonraki special vuruşu ilk yaprağı düşürüp yutmayı kalıcı durdurur. Karnı boşken ilk vuruş doğrudan yaprak düşürür.
- Kasklar ve çamur normal eşleşmelerle de ilerler; nilüfer yaprakları special gerektirir. Nilüferler açıldıkça yan kıyılarla orta alan arasındaki yatay eşleşme yolları genişler.

## Statik doğrulama ve performans

- Yedi hücre dizisinin her biri 99 eleman; hole'larda engel veya origin yoktur. Her 1×1 engelin origin'i kendi indeksidir; üst üste katman yoktur.
- `pinnedTileTypes` ve `pinnedSpecialTypes` dizileri tamamen sıfırdır. Başlangıç ve refill dört renk havuzundan oyun tarafından üretilir; sabit açılış takası veya belirli bir ilk special garantisi verilmez. Önizlemedeki `?` hücreleri rastgele taş alanını gösterir.
- Başlangıçta 59 normal taş hücresi vardır: 10'u nilüfer altında kilitli, 49'u normal eşleşmeye açıktır. Ayrıca 12 hareketli kask ve iki sabit sepet vardır.
- Maske bağlı ve simetriktir. Mud/GrassWaterLily dikey dolumu kesmez; kasklar hareketli taşlardır. Sabit sepetler yalnız en alt sıradadır, altında dolum bekleyen cep bırakmaz. İki nilüfer sütunu arasında 3×5 açık special üretme alanı korunur.
- Değişiklik yalnız altı birleştirme hücresine nilüfer/origin ekler, nilüfer hedefini 10 yapar ve iki pin dizisini sıfırlar. Maske, diğer hedefler, yardımcılar, 32 hamle, meta GUID, katalog ve diğer level asset'leri korunur.
- 25 seviye/chapter kuralıyla 151 chapter 7'dir. Yeni chapter görseli eklenmedi; mevcut ChapterThemeLibrary eksik chapter için son mevcut temayı kullanır.

Yeni gameplay/VFX kodu eklenmedi. Nilüfer sayısı arttığı için mevcut servis 4 yerine 10 çiçeği ve bekleyen komşularını işler; komşuluk kontrolü tahta durulması/hamle olaylarıyla yapılır. Uçan special görseli çiçek başına tekrar kullanılır; yaprak efektlerinin mevcut 32 yuvalı ortak havuz sınırı korunur. Yeni kare başı tahta taraması, coroutine veya texture üretme yolu eklenmez. Mevcut kapanış/iptal temizliği kullanılır; cihaz maliyeti ölçülmedi.

Unity Play, build, bot veya oynanış simülasyonu çalıştırılmadı. İlk oyunda 32 hamle, iki nilüfer sütununa special ulaştırma ve kenar çamurlarını bitirme yükü değerlendirilmelidir. Önizleme yerleşim şemasıdır; oyun ekran görüntüsü veya sabit taş dizilimi değildir.
