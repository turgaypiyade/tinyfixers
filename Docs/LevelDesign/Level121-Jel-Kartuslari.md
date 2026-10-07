# Level 121 — Jel Kartuşları (v3)

`LevelCatalogPro` global 121 → `LevelP_001260.asset`. Tam 9×11, 26 hamle. Kolay ama hareketli: jel fırlatıcı (kartuş) tanıtımı + ColorChest ve Wardrobe.

## Kompozisyon

```text
ooooooooo
PoooooooP
PooQQQooP
PoooooooP
PDDoooDDP
PDDoeoDDP
PoooooooP
PoooooooP
ooooooooo
^oCCoCCo^
^oCCoCCo^
```

- `^` alt köşelerde yukarı bakan kartuşlar (GelLauncherUp, 1×2): special ile 2 vuruş → sütunu süpürür, mor şapkaları siler, sütunu jeller.
- `P` iki kenar sütunda 14 mor şapka (plastic, 7).
- `D` iki Wardrobe (27, 2×2): kapı açılır, içindeki eşyalar tek tek çıkar.
- `C` iki ColorChest (21, 2×2): dört rengin eşleşmesiyle açılır; kartuşların arasında, alt satırda (altında beslenecek hücre yok).
- `Q` üç turuncu şapka (vurgu, hedef değil), `e` EggBird (yardımcı), `o` normal taş. Başlangıçta açık jel yok.

## Hedefler

99 hücre jel, 2 ColorChest, 2 Wardrobe, 14 mor şapka.

## Revizyonlar

- v1 yatay kartuşlar + açık jel — sade.
- v2 yukarı bakan kartuşlar + şapka sütunları + kemer — kolay ve tek düze bulundu.
- v3: ColorChest ve Wardrobe eklendi (repoda bu dönemde ilk kullanım), daha çeşitli hedefler.

## Statik kontrol

2×2 origin'leri, kartuş origin'leri, hedef adetleri ve katalog bağlantısı (GUID korunarak) kontrol edildi. Hazır üçlü/2×2 yok, 10 geçerli takas. Unity'de çalıştırılmadı.
