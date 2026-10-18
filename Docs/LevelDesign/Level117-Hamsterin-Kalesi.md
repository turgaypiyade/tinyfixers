# Level 117 — Hamster'ın Kalesi

`LevelCatalogPro` global 117 → `LevelP_001230.asset`. Normal level, tam 9×11 board, başlangıç ayarı 28 hamle. **Hamster (57) ilk kez kullanılır.** Çiçek/çim yok.

## Kompozisyon

Solda 2 sütun baykuş/turuncu dama şeridi; ortada açık oyun alanı ve tek Hamster; sağa dayalı 2 sütun genişliğinde, parçalı MetalWall kalesi. Kalenin önünde (x=6) tuğla Wall kalkanları, parçalar arasında ve uçlarda SculptingStone.

```text
oooooooSS
THoooow11
HToooow11
THoooow11
HToooowSS
THoohoS22
HToooox22
THoooox22
HTooooxSS
ooooooo33
ooooooS33
```

`o` normal taş, `T` PlasticTwoStage (40), `H` HelmetPorcelain (30), `h` Hamster (57), `S` SculptingStone (28, hedef değil), `w`/`x` tuğla Wall (55) kalkan parçaları (4 + 3 hücre, hedef değil), `1`/`2`/`3` MetalWall (56) parçaları (6 + 6 + 4).

Önizleme: `Level117-Hamsterin-Kalesi.png`.

## Hedefler ve kararlar

- **Hamster'ı 3 kez doyur** (doyma eşiği library varsayılanı 7 lokma; `hamsterSatietyOverride = 0`). Her doyumda Hamster hedef önceliğiyle bir hedefin yanına zıplar ve 3×3 vurur; kalenin içine erişmenin bir yolu budur.
- 3 MetalWall: üst ve orta parça tuğla kalkanların arkasında; ilk hamlelerde yalnız alt parçaya `(6,9)` hücresinden doğrudan ulaşılır. Ardışık vuruş kuralı nedeniyle kalkan kırıldıktan sonra da parçaya üst üste odaklanmak gerekir.
- 8 HelmetPorcelain + 8 PlasticTwoStage: sol şerit, hareketli.
- Hedef sayısı 4 (HUD sınırı); tuğla kalkanlar ve taşlar hedef değildir ama duvara ulaşmak için kırılmaları gerekir.

## Statik kontrol

Diziler, 5 duvar parçası (2 tuğla + 3 metal) ve origin'leri, Hamster origin'i, hedef adetleri ve katalog bağlantısı kontrol edildi. 51 açık taş sabitlendi (asimetrik tasarım, simetri uygulanmadı); hazır üçlü/2×2 yok, 12 geçerli takas. Unity, bot, build veya simülasyon çalıştırılmadı; 28 hamle ve 3 doyum ilk tahmin.
