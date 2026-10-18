# Bostan Hasadı — Görsel Prompt'ları

Tema kararı (2026-10-10, kullanıcı — son hali): **Bostan Hasadı** — "daha renkli ve keyifli". Topraktan sebze
ve meyve çıkar. Hikâye bağı: tamirci hayvanlar harikayı onarırken yanına bostan kurmuş; hasat ekibin ödülü.
(Önce arkeoloji, sonra alet teması düşünüldü; ikisi de bırakıldı.) Kazıcı **Ayı** (hasır şapkalı).
Arka plan: oyuncunun açtığı harikanın arka planı (yeni arka plan çizilmez). Robot DEĞİL.

Prompt'lar İngilizce, açıklamalar Türkçe. Obje prompt'larının sonuna **Stil bloğu**nu ekle.

---

## 0. Stil bloğu (obje/ikon prompt'larının sonuna)
```
Style: premium, high-quality mobile puzzle game asset, polished glossy finish, crisp and razor-sharp
rendering, clean edges, vivid saturated colors, rich depth with smooth shading and a clean bevel.
Lighting: ONE single key light from the top-left with a soft fill; one clean highlight per surface;
no multiple light sources, no scattered specular spots. Big, simple, high-contrast shapes readable at
about 100x100 pixels. Camera: front view, slightly from above (about 20 degrees), centered, no
perspective distortion. Background: fully transparent (PNG with alpha). No floor, no cast shadow outside
the object, no text, no watermark, no frame.
```
Saydam vermiyorsa düz **#FF00FF magenta** zemin iste, ben temizlerim.

---

## 1. Bostan toprağı karesi (kare)
İlk kareyi referanssız üret; B/C varyantlarını ve kazılmış kareyi bundan düzenleme ile.
```
Create a new image from scratch, no reference image. A single square block of rich dark-brown garden
soil seen from the top at a slight angle, like a game tile: soft crumbly tilled earth on top with gentle
furrow lines, two tiny green sprout leaves and a couple of small pebbles, all kept inside the tile,
slightly rounded beveled edges, a darker layered soil face visible on the front edge. Fills the whole
square edge to edge with a thin even margin. Fresh, fertile, inviting to tap.
```
Varyant B/C (ilk kareyi referans ver):
```
Keep everything EXACTLY identical: same size, same shape, same edges, same lighting, same colors, same
camera. Only change the arrangement of the sprouts and pebbles (keep them inside the tile).
```
Kazılmış kare (ilk kareyi referans ver):
```
The same square soil tile as the reference, but dug open: a shallow rounded pit in the center showing
darker, moist soil, a few loose soil crumbs around the rim, no sprouts, the beveled tile edge unchanged.
Keep size, shape, edges, lighting and camera EXACTLY identical to the reference tile.
```

## 2. Sebze ve meyveler (8 parça) — tuval oranı = ızgaradaki ayak izi
Her prompt'a ek: `Fresh and juicy, a little soil on it as if just harvested, but clean, bright and readable. Cute, plump cartoon proportions.`

| # | Ürün | Ayak izi | Tuval |
|---|---|---|---|
| H1 | Havuç (yapraklı) | 1x2 dikey | 1024×2048 |
| H2 | Patates | 1x1 | 1024×1024 |
| H3 | Turp (kırmızı, yapraklı) | 1x1 | 1024×1024 |
| H4 | Mısır koçanı | 1x3 dikey | 1024×3072 |
| H5 | Karpuz | 2x1 yatay | 2048×1024 |
| H6 | Kabak (balkabağı) | 2x2 | 2048×2048 |
| H7 | Patlıcan | 2x1 yatay | 2048×1024 |
| H8 | Çilek salkımı | 1x1 | 1024×1024 |

```
H1: A big bright orange carrot with lush green leafy top, standing vertically.
H2: A round golden-brown potato with a few small eyes.
H3: A round shiny red radish with a white tip and fresh green leaves.
H4: A tall corn cob with bright yellow kernels, green husk leaves peeled back, standing vertically.
H5: A plump striped green watermelon lying horizontally.
H6: A big round orange pumpkin with deep ribs and a curly green stem with one leaf.
H7: A glossy deep-purple eggplant with a green cap, lying horizontally.
H8: Three plump red strawberries on one green stem with a leaf.
```
**[İsteğe bağlı] Altın ürün:** nadir çıkan, booster/joker veren özel parça (ör. altın havuç). İstersen H1'i
"golden, shining, sparkling" diye bir kez daha ürettiririz.

## 3. Hasat alanı (koleksiyon, üst bant ~1080×600)
Bulunan ürünler kasalara/sepete dolar; boşken gölge görünür.
```
A wide horizontal wooden farm market stand with a small striped red-and-white awning, three empty wooden
crates and one empty wicker basket on the counter, warm wood, cheerful and clean. Front view, centered,
transparent background, no produce, no text.
```

## 4. Bahçe küreği ikonu — event parası (kare)
```
A shiny cartoon garden trowel with a chunky green wooden handle and a polished steel blade with a tiny
pile of brown soil on it, tilted 30 degrees, bold and simple, instantly readable as a garden shovel.
```

## 5. Bahçıvan ayı — 3 poz (kare; `Art/UI/Characters/Bear_Worried.png` karakter referansı)
Önce A; B ve C'de A'yı referans ver. Hepsine ek: `Full body, feet at the same baseline in every pose,
character fills about 85% of the canvas height, transparent background.`
```
A: The same bear character as the reference (same fur color, face, red bandana, green overalls, tool
belt), now wearing a straw sun hat, holding a garden trowel, standing happily, full body, friendly smile,
looking slightly to the right.
B: Same bear, same outfit and size, digging: kneeling forward and digging the soil with the trowel,
focused happy face, a little soil flying.
C: Same bear, same outfit and size, cheering: holding up a big orange carrot in triumph, big open-mouth
smile, eyes closed with joy.
```

## 6. Event logosu / sol panel ikonu (kare)
Diğer event logolarıyla aynı aile (yuvarlak altın çerçeve + alt isim bandı).
```
A round game event badge with a thick golden frame: inside, a cheerful vegetable garden patch with a
big carrot, a pumpkin and a watermelon popping out of rich soil, a garden trowel stuck in the ground,
small green sprouts, blue sky. At the bottom an empty dark teal rounded banner (no text). Bright,
glossy, colorful, premium.
```

## 7. (İsteğe bağlı) Toprak topağı — kazı parçacığı (kare, 256)
```
A single small clump of dark brown soil with a tiny green leaf, simple shape, for a particle effect.
```

---

**Kullanılmayanlar:** piramit kazı arka planı, kum karesi, eser/alet prompt'ları (tema değişti).
**Kontrol (ben):** 100×100 okunabilirlik, ürünlerin toprak üstünde ayrışması, ayı pozlarının hizası.

---

## 8. Üst HUD (kullanıcı referansı, 2026-10-10) — asılı tabela + 2 hap
Referans: iplerle asılı ahşap tabela (yapraklar, papatyalar, sağda bahçe küreği), altında iki hap:
fide ikonu + ilerleme, kronometre + kalan süre. **Yazılar görsele GÖMÜLMEZ** — başlık ve sayılar
TMP ile yazılır (TR/EN yerelleştirme + sayaç canlı değişiyor). Hepsi saydam PNG; sona Stil bloğu
(§0) eklenir, ama bu bölümde kamera **tam önden** (20° üstten değil).

### 8a. Asılı tabela — `Harvest_HudSign.png` (1536×768)
```
Create a new image from scratch, no reference image. A wide hanging wooden sign for a cheerful garden
event in a mobile game: one thick horizontal plank of warm honey-brown wood with soft rounded corners,
gentle wood grain, a darker beveled rim and a clean front face. Two thick twisted jute ropes go straight
up from the top-left and top-right of the plank and continue to the very top edge of the image, as if
the sign hangs from above. Lush green leaves and a few small white daisies with yellow centers grow
around the top-left corner and the right end of the plank; a small garden trowel with an orange wooden
handle is tucked behind the right end, its metal blade pointing down-right, overlapping the plank edge.
The central front face of the plank (about 70% of its width and height) is EMPTY and smooth, reserved
for a two-line title. Plank fills about 85% of the image width. Front view, no perspective.
```

### 8b. Hap çerçevesi — `Harvest_HudPill.png` (1024×256)
İki hap AYNI görsel; genişlik Unity'de 9-slice ile ayarlanır (ortası düz olmalı).
```
Create a new image from scratch, no reference image. A horizontal capsule-shaped UI badge for a mobile
game: a thick cream / light-beige rounded outer rim with a soft bevel, and a recessed dark chocolate-
brown inner field running along most of its length for white text. On the LEFT end, a large circular
socket of the same cream rim overlapping the capsule (slightly taller than the capsule), with an empty
light-cream inner circle reserved for an icon. The straight middle section is perfectly uniform from left
to right (it will be stretched). No icon, no text. Front view, centered, fills the width.
```

### 8c. Fide ikonu — `Harvest_HudSprout.png` (512×512)
```
Create a new image from scratch, no reference image. A small mound of rich dark-brown garden soil with a
fresh bright-green two-leaf sprout growing from its top, rounded friendly shapes, a cheerful game icon.
Centered, fills about 85% of the canvas.
```

### 8d. Kronometre ikonu — `Harvest_HudTimer.png` (512×512)
```
Create a new image from scratch, no reference image. A round golden-orange stopwatch with a small top
button and a side button, a clean cream clock face with a few hour ticks and two dark-blue hands, thick
friendly outlines, a cheerful game icon. Centered, fills about 85% of the canvas.
```
(Kronometre başka event'lerde de kullanılabilir — `Art/UI/Shared/` altına koymak da olur.)
