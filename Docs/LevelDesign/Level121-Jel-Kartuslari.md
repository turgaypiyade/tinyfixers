# Level 121 — Jel Kartuşları (v2)

`LevelCatalogPro` global 121 → `LevelP_001260.asset`. Tam 9×11, 28 hamle. Yeni jel fırlatıcının (kartuş) tanıtım/test levelı; zor değil, eğlenceli.

## Kompozisyon

```text
ooooooooo
RoooooooB
RoPPPPPoB
RoToooToB
RoTeoeToB
RoToooToB
RooQQQooB
RoooooooB
ooooooooo
^ooooooo^
^ookkkoo^
```

- `^` alt köşelerde **yukarı bakan** iki kartuş (GelLauncherUp, 59, 1×2). Special ile 2 vuruşta kırılınca kapak kendi sütununu baştan sona süpürür, jel arkasından sütunu boyar.
- `R` (sol sütun) 7 Plastic_Red, `B` (sağ sütun) 7 Plastic_Blue: kartuş atışının tek seferde sileceği renkli şapka sütunları.
- Ortada gökkuşağı kemeri: `P` mor şapka tepe (5), `T` PlasticTwoStage kenarlar (6), `Q` turuncu şapka taban (3); kemerin içinde iki EggBird `e` (yardımcı).
- `k` altta üç RocketBasket (yardımcı). `o` normal taş.
- Başlangıçta açık jel yok: jel yalnız kartuşlardan gelir, sonra bulaşık taşlarla yayılır.

## Hedefler

99 hücre jel, 7 kırmızı şapka, 7 mavi şapka, 5 mor şapka. Kartuşlar, EggBird, RocketBasket, PlasticTwoStage ve turuncu şapka hedef değil.

## Revizyon

v1 (yatay kartuşlar alt satırda, ortada açık jel) sade bulundu. v2: kartuşlar yukarı bakıyor, sütunları silinecek renkli şapkalarla dolu, ortada renkli kemer ve yardımcılar.

## Statik kontrol

Diziler, kartuş origin'leri (0,9) ve (8,9), hedef adetleri ve katalog bağlantısı (GUID korunarak) kontrol edildi. Hazır üçlü/2×2 yok, 8 geçerli takas. Unity'de çalıştırılmadı.
