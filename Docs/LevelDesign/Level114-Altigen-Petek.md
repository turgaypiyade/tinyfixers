# Level 114 — Altıgen Petek

`LevelCatalogPro` global 114 → `LevelP_001200.asset`. Normal level, 9×11 zarf içinde sivri uçlu altıgen maske (75 aktif hücre), başlangıç ayarı 27 hamle.

## Kompozisyon

Altıgen petek içinde mücevher: merkezde EggBird, çevresinde turuncu PlasticTwoStage elmas, onun dışında mor şapka halkası. Altıgenin iki dik kenarı MetalWall, üst/alt uçları ve köşeleri GrassFlower.

```text
...FFF...
..ooooo..
.oooPooo.
FooPTPooF
1oPTTTPo2
1PTTeTTP2
1oPTTTPo2
FooPTPooF
.oooPooo.
..ooooo..
...FFF...
```

`.` hole, `o` normal taş, `P` plastic (mor şapka, 7), `T` PlasticTwoStage (40), `e` EggBird (45), `F` GrassFlower (58), `1`/`2` MetalWall (56) parçaları (3+3).

Önizleme: `Level114-Altigen-Petek.png` (yerleşim şeması, oyun ekran görüntüsü değil).

## Hedefler ve kararlar

- 2 MetalWall parçası (yan kenarlar).
- 12 PlasticTwoStage (iki vuruş) + 12 mor şapka (tek vuruş): içe doğru katman katman açılan mücevher.
- 10 Grass (GrassFlower).
- EggBird yardımcıdır, **hedef değildir**; mücevherin ortasına ulaşınca kuşlar üç rastgele hedefe yardım eder.
- Chest kullanılmadı. Mor/turuncu tamamlayıcı renkler, kenarlarda gri metal ve yeşil çiçek.

## Statik kontrol

Diziler, 24 hole, iki MetalWall parçası ve origin'leri, EggBird origin'i, hedef adetleri ve katalog bağlantısı kontrol edildi. 34 açık normal taş + 10 örtü altı taş sağ-sol simetrik sabitlendi. Açılışta hazır üçlü/2×2 yok; normal taşlar arasında 10 geçerli takas var (üst ve alt yarıda). Hareketli engellerle yapılabilecek takaslar sayılmadı.

Unity, bot, build veya simülasyon çalıştırılmadı. 27 hamle ilk tahmindir.
