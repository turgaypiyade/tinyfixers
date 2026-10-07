# Level 118 — Su Kuleleri

`LevelCatalogPro` global 118 → `LevelP_001240.asset`. Normal level, tam 9×11 board, başlangıç ayarı 26 hamle. Çiçek/çim yok.

## Kompozisyon

Dört kırmızı bidon (WaterTank 2×2) simetrik bir kare oluşturur; her bidonun dış köşesinde 3'lü mavi yıldızlı şapka grubu, bidon çiftleri arasında iki sıra baykuş, merkezde EggBird. Kırmızı/mavi/krem kontrastı; orta sütun ve kenar sütunlar açık eşleşme koridorları.

```text
ooooooooo
oBBoooBBo
oBNNoNNBo
ooNNoNNoo
oHHHoHHHo
ooooeoooo
oHHHoHHHo
ooNNoNNoo
oBNNoNNBo
oBBoooBBo
ooooooooo
```

`o` normal taş, `N` WaterTank (52, 2×2; dört hücre ortak sol-üst origin), `B` Plastic_Blue (11), `H` HelmetPorcelain (30), `e` EggBird (45).

Önizleme: `Level118-Su-Kuleleri.png`.

## Hedefler ve kararlar

- 4 WaterTank: iki vuruş, **yalnız special** ile; kırılınca ~8 su birikintisi saçar. Birikinti hedefi HUD'da dinamik olarak eklenir, bu yüzden authored hedef sayısı 3'te tutuldu (HUD 4 sütun).
- 12 Plastic_Blue: yalnız mavi (Bolt) komşu eşleşmeyle kırılır; açılışta şapkaların yanında mavi taş bolca var.
- 12 HelmetPorcelain.
- EggBird yardımcıdır, hedef değildir.
- 58 açık hücre special üretimi için alan bırakır. Bidonlar altlarındaki sütunları kapatır; orta ve kenar sütunlar ile çapraz akış besler.

## Statik kontrol

Diziler, 2×2 bidonların ortak origin'leri, EggBird origin'i, hedef adetleri ve katalog bağlantısı kontrol edildi. 58 açık taş sağ-sol simetrik sabitlendi; hazır üçlü/2×2 yok, 9 geçerli takas. Unity, bot, build veya simülasyon çalıştırılmadı; 26 hamle ilk tahmin.
