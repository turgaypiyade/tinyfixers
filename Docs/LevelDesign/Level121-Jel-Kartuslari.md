# Level 121 — Jel Kartuşları (v4)

`LevelCatalogPro` global 121 → `LevelP_001260.asset`. Tam 9×11, 28 hamle. Jel fırlatıcı (kartuş) tanıtımı; kullanıcı tarifiyle Wardrobe ve SculptingStone bantları.

## Kompozisyon

```text
ooooooooo
PoooooooP
PooQQQooP
PeoeoeoeP
DDDDoDDDD
DDDDoDDDD
SSSSoSSSS
SSSSoSSSS
ooooooooo
^oCCoCCo^
^oCCoCCo^
```

- `^` alt köşelerde yukarı bakan kartuşlar (GelLauncherUp): sütunu süpürür (taş, dolap, şapka), sütunu jeller.
- `D` 4 Wardrobe (2×2) yan yana, ortadaki 5. sütun boş.
- `S` alttan 4. ve 5. satırlarda 16 SculptingStone, ortada aynı boşluk.
- `e` dolapların üstünde 4 EggBird (yardımcı) — dolap ve taşların getirdiği zorluğu dengeler.
- `C` alt satırda 2 ColorChest. `P` 6 mor şapka, `Q` 3 turuncu şapka (vurgu), `o` normal taş. Başlangıçta açık jel yok.

## Hedefler

99 hücre jel, 2 ColorChest, 4 Wardrobe, 16 SculptingStone.

## Akış notu

Dolap ve taş bantları yalnız orta sütunda geçit bırakır. 8. satırın iki yanı ve kartuş yanındaki hücreler, taşlar kırılana kadar yukarıdan beslenmez; buradaki taşlar temizlenirse hücre boş kalır. Kartuş atışı ve taş kırılması bölgeyi açtıkça normal akış başlar.

## Revizyonlar

v1 sade; v2 tek düze; v3 ColorChest + 2 Wardrobe; v4 kullanıcı tarifi: 4 Wardrobe bandı, 2 sıra SculptingStone, 4 EggBird.

## Statik kontrol

2×2 origin'leri, kartuş origin'leri, hedef adetleri ve katalog bağlantısı (GUID korunarak) kontrol edildi. Hazır üçlü/2×2 yok, 9 geçerli takas. Unity'de çalıştırılmadı.
