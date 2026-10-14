# Bilardo Topu + Delik — kurallar (kilitli, 2026-10-13)

> **DURUM (2026-10-13): RAFTA.** Kullanıcı oyunda denedi, fikir oturmadı. Kod kalıyor ama level'larda kullanılmıyor;
> 2. aşama yapılmayacak. Top olmayan level'da maliyet yok (yuvarlanma adımı anında çıkar).

Amaç: tahtadaki bilardo topunu/toplarını, level'da konan deliğe (cebe) ulaştırmak. Hedef = deliğe giren top sayısı.

## Nesneler

| Nesne | Id (ayrıldı) | Ne |
|---|---|---|
| Bilardo topu | `BilliardBall = 66` | Hücrede duran top. Kırılmaz, swap'lanamaz. Yalnız level'da konur (üstten yeni top gelmez). |
| Delik (cep) | `BilliardPocket = 67` | Level editöründe konan hücre (genelde alt sıra; her yere konabilir). Top girince hedef +1. |

Bir level'da birden fazla top ve delik olabilir.

## Yığın rolü (2026-10-13 kararı)

- **Top kargo gibi:** zemin örtülerinin (çamur, su, çim) içine girebilir; örtü top üstündeyken de normal vurulur.
  Taş kaplayan engellerin altına konmaz.
- **Cep yığına girmez:** üstüne de altına da katman konmaz (editör engeller).
- **Cep taşa kapalı engel:** taşlar cebe girmez, yanından çapraz akar (köşe geçişi açık). Kırılmaz. **Sınırsız** top alır.

## Yuvarlanma kuralları

1. **Ne zaman:** topun yanındaki bir hücre boşalınca top oraya yuvarlanır.
2. **Yön önceliği:**
   1. Aşağı (normal taş gibi düşer)
   2. Çapraz aşağı (sol-alt / sağ-alt)
   3. Yana (sol / sağ)
   4. **Yukarı asla.**
3. **Eşitlik:** aynı öncelikte iki yön açıksa deliğe (en yakın deliğe) yakın olan taraf seçilir.
4. **Öncelik taşlardan önce:** boşalan hücreyi önce top alır; yukarıdan düşen taşlar ondan sonra doldurur.
   (Bu olmadan top hiç kıpırdayamaz — boşluk aynı karede dolar.)
5. **Delik:** top delik hücresine girince içine düşer → üstteki hedef sayacına uçar → hedef +1, top tahtadan çıkar.
   Cep, yön önceliğinde boş hücre gibi sayılır (altındaki cep → düşer; çapraz/yandaki cep → yuvarlanır).
6. **Takılma = erken fail:** her hamle bitince, cebe hâlâ ulaşabilen top sayısı kalan top hedefinden azsa
   oyun hamleler bitmeden fail olur. Ulaşılabilirlik yalnız kalıcı topolojiyle hesaplanır (hole + kenar + cep;
   kırılabilir engeller zamanla açılır sayılır; adımlar kural 2 ile aynı, yukarı asla).
   - Fail ekranı "Top Takıldı!" modunda: hamle teklifi yerine **Topu Taşı** (aynı altın/reklam bedeli).
     Takılı top, cebe ulaşabilen en yakın normal taşla yer değiştirir (top yaylanarak uçar). Hamle de 0 ise
     teklif edilen hamle ayrıca eklenir.
   - Editör tasarım anında uyarır: cebe hiç ulaşamayan top / ulaşabilen top < hedef.

## 2. aşama — bilardo vuruşu

6. **Roket topa çarparsa:** roketin temizlediği satır/sütun boyunca top roketin gittiği yönde fırlar; boşalan
   hücrelerden kayar, bir taşa / engele / kenara / deliğe kadar.
7. **Pulse patlaması:** top merkezden uzağa, boşsa 1–2 hücre itilir.
8. **Aynı yönde kayma:** yana yuvarlanmaya başlayan top, önü boş ve altı dolu olduğu sürece aynı yönde kaymayı sürdürür.
   → **1. aşamaya alındı** (geri dönüş yasağı olmadan top iki boş hücre arasında gidip gelirdi).
   Havadayken de geçerli: temizlenen taşlar hücreleri tek tek bırakır; yana yuvarlanan topun önünde hücre
   boşalınca top onu da taşlardan önce alır, yolu uzar (2026-10-13 testi: top boşalan sıranın ortasında kalıyordu).
   Taş düşüş kuralları değişmez — uzatma, havadaki taşı yeniden hedefleyen mevcut motorla akar.

## Uygulama sırası

| Aşama | İçerik |
|---|---|
| 1 | Top + delik + kural 1–6 + 8 — **KOD YAZILDI (2026-10-13), Unity testi bekliyor** |
| 2 | Roket/pulse vuruşu (2. aşama kural 6–7) |

Kod haritası (1. aşama):

| Parça | Yer |
|---|---|
| Yuvarlanma planı (kural 1–5, 8) | `CascadeLogic.Billiard.cs` — planlayıcının ilk adımı; yol `VirtualTile.Path`'e yazılır, düşüş motoru sürer |
| Cebe uzaklık / ulaşılabilirlik | `Core/BilliardReach.cs` (runtime + editör aynı hesap) |
| Cebe giriş, takılma, kurtarma | `Grid/Board/BilliardBallService.cs` (`board.Billiard`) |
| Kırılmazlık (tek kural) | `ObstacleStateService.IsUnbreakableMovableAt` / `IsUnbreakableObstacleAt` — kargo + top + cep |
| Swap yasağı | `ObstacleStateService.IsInteractionLockedAt` |
| Top görseli (3B yuvarlanan numara + sabit parlama) | `Grid/BilliardBallFace.cs` + `Grid/BilliardBallRoll.cs` |
| Cebe batış + hedefe uçuş | `GoalFlyFx.PlayBilliardPocket` (halka ghost'u tahtadaki cebe hizalı) |
| Fail "Topu Taşı" modu | `LevelEndSimplePopupController.RefreshBilliardStuckOfferVisuals` + `board.ContinueWithBilliardRescue` |
| Sprite'lar | `Resources/Billiard/` — top gövdesi, parlama, cep (koyu taban + ağ + halka), yalnız halka |
| Simülasyon | Modellenmiyor (`SimGoalFidelity.NotSimulated`) |

## Görsel (önce görsel — iyi değilse kodlanmaz)

**SEÇİLDİ (2026-10-13): A — bilardo.** Koyu mor gövde (112,40,182; 2026-10-13 "biraz daha koyu") + beyaz "8" ORTADA, ayrı parlama. **3B yuvarlanma** (2026-10-13): düzlemde dönen "8" kayıyormuş gibi görünüyordu ve hız artınca stroboskop gibi geri dönüyordu → numara artık topun yüzeyinde gidiş yönünde kayar, kenarda basıklaşıp arkaya geçer, karşı yüzdeki ikinci "8" öbür kenardan gelir (gerçek topta iki numara var). Fiziksel hız (yol/yarıçap, kare başı ≤0.7 rad), her harekette (yana, çapraz, düz düşüş). Sprite: aux 0 parlama, 1 numarasız gövde, 2 numara çıkartması; stage sprite (numaralı) HUD/editör ikonu. Kod: `Grid/BilliardBallFace.cs` (tahta + cep ghost'u ortak). Cep: üstten
içine bakılan file (huni gibi daralan kahve ip ağ, koyu taban) + pirinç halka. Top cebe girerken halka ghost'u topun
önüne çizilir → top halkanın içinden ağa batıyor görünür. Aşağıdaki A/B karşılaştırması tarihçedir.

İki tema seçeneği (kurallar ikisinde de AYNI; yalnız görsel ve deliğe giriş anı değişir) — ikisi de üretilip
yan yana bakılarak seçilecek:

| | A) Bilardo topu | B) Top şeklinde kıvrılmış kirpi |
|---|---|---|
| Top | Parlak bilardo topu (klasik kırmızı ya da numaralı) | Dikenleri içe kıvrık, top gibi yuvarlanan sevimli kirpi |
| Delik | Bilardo masası cebi: koyu iç, ahşap/deri kenar | Kirpinin yuvası: toprak/yaprak kenarlı yuva deliği |
| Deliğe giriş | Top cebe düşer, küçük parıltı | Kirpi yuvaya düşer, kafasını çıkarıp sevinir, sonra hedefe uçar |
| Artısı | Herkes anında anlar ("deliğe sok") | Hayvan dünyasına oturur, karakter ve sevimlilik katar |

Not: kirpi yuvarlanırken görselin dönmesi (dikenli top) iyi okunur; düşüşte normal taş gibi davranır.

Prompt taslağı A — top:
> Glossy cartoon billiard ball icon for a mobile match-3 game, single ball centered, bright saturated red with a
> small white circle, strong single top-left highlight, soft contact shadow, clean vector-like rendering, polished
> casual game style, transparent background, 512x512.

Prompt taslağı A — delik:
> Top-down cartoon billiard table pocket for a mobile match-3 board cell, round dark hole with a thick polished
> wooden rim and dark leather lining, slight inner depth gradient, single top-left light, polished casual game style,
> fills a square cell, transparent background, 512x512.

Prompt taslağı B — kirpi:
> Cute cartoon hedgehog curled up into a perfect ball for a mobile match-3 game tile, soft brown spines tucked in,
> tiny nose and closed happy eyes peeking out, round silhouette that reads as a rolling ball, glossy polished casual
> game style, single top-left light, soft contact shadow, transparent background, 512x512.

Prompt taslağı B — yuva (delik):
> Top-down cartoon hedgehog burrow entrance for a mobile match-3 board cell, round dark hole with a rim of soft earth,
> small leaves and a tiny twig, slight inner depth gradient, single top-left light, polished casual game style,
> fills a square cell, transparent background, 512x512.
