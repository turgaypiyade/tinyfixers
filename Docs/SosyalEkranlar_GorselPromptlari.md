# Sosyal Ekranlar (Liderlik / Takım / Sohbet) — Görsel Prompt'ları

Tarih: 2026-10-06. Referans: Royal Match / Royal Kingdom liderlik, takım ve sohbet ekranları.
Kod tarafı (hatalar, ortak stil, satır düzeni) görsellerden bağımsız ilerliyor; görseller geldikçe
yer tutucuların yerine takılacak.

Prompt'lar İngilizce, açıklamalar Türkçe. Öncelik sırası: **1 → 2 → 3** önce (ekranı en çok değiştirenler).

---

## 0. Ortak kurallar — HER prompt'un sonuna ekle

```
Style: premium, high-quality mobile puzzle game UI asset, polished glossy finish, crisp and razor-sharp
rendering, clean edges, vivid saturated colors, rich depth with smooth shading and a clean bevel,
thick dark outline around the whole silhouette (like Royal Match UI icons).
Lighting: ONE single key light from the top-left with a soft fill; one clean, well-placed highlight per
surface; no multiple light sources, no scattered specular spots, no rim lights from several sides.
Clean surfaces, no noise, no grain, no blur. Big, simple, high-contrast shapes that stay readable at
about 64x64 pixels on a phone screen.
Camera: straight front view, centered, no perspective distortion.
Composition: single object centered, filling about 88% of a square canvas, small even margin.
Background: fully transparent (PNG with alpha). No cast shadow outside the object, no text unless
asked, no watermark, no frame.
```

- **Kare 1:1**, 1024×1024 üret → oyuna 256 veya 512 girer (ben küçültürüm).
- Saydam vermiyorsa düz **#FF00FF magenta** zemin iste, ben temizlerim.
- **Seri görsellerde (madalyalar, amblemler)** önce ANA görseli üret, beğen; diğerlerini sıfırdan
  değil **edit/inpaint** ile türet ve şunu ekle:
  ```
  Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting,
  same camera. Only change: <değişen şey>.
  ```
- Sayı/yazı **çizdirme** (madalyadaki 1-2-3 hariç) — metinleri oyun yazıyor (yerelleştirme).

---

## 1. Sıralama madalyaları (1., 2., 3.) — ÖNCELİK 1

Bugün ilk 3 satırda sıra numarası hediye kutusunun arkasında kayboluyor. Referanstaki gibi kurdeleli
madalya. 3 adet, aynı kalıp, yalnız metal rengi değişir.

**ANA (altın, 1):**
```
A round game UI rank medal: thick polished gold coin-shaped medal with a raised inner rim, a big bold
number "1" embossed in the center in white with a dark outline, and two short ribbon tails hanging
below the medal (deep red ribbon). Chunky, toy-like, cute cartoon proportions, glossy gold.
```
**Gümüş (2) — edit:** `Only change: the medal metal becomes polished silver, ribbon becomes royal blue, number becomes "2".`
**Bronz (3) — edit:** `Only change: the medal metal becomes polished bronze/copper, ribbon becomes dark orange-brown, number becomes "3".`

Teslim: `RankMedal_1.png`, `RankMedal_2.png`, `RankMedal_3.png`.

---

## 2. Skor / kupa ikonu — ÖNCELİK 1

"Score" yazısının yerine skor kutusunun solunda duracak ikon.
```
A shiny golden trophy cup game icon with two curled handles, a short stem and a wide base,
a small star emblem on the cup front. Chunky cute cartoon proportions, glossy gold.
```
Teslim: `TrophyIcon.png`.

---

## 3. Takım amblemleri (kalkan seti) — ÖNCELİK 1

Bugün tüm takımlar aynı amblemi kullanıyor. Kalkan çerçevesi SABİT, içindeki sembol ve zemin rengi
değişir. Önce boş kalkanı (ANA) üret, sonra her amblemi **edit** ile türet.

**ANA (boş kalkan):**
```
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold
side tabs at the top corners, the inner face of the shield is a smooth glossy deep red enamel, empty
in the middle. Chunky cute cartoon proportions.
```
**Amblemler — edit:** `Only change: the inner enamel color becomes <RENK> and a big white glossy <SEMBOL>
icon with a dark outline is placed in the center of the shield.`

| # | RENK | SEMBOL |
|---|------|--------|
| 1 | deep red | wrench |
| 2 | royal blue | animal paw print |
| 3 | emerald green | gear cog |
| 4 | purple | hammer |
| 5 | orange | lightning bolt |
| 6 | teal | star |
| 7 | black | flame |
| 8 | pink | heart |
| 9 | navy blue | anchor |
| 10 | lime green | leaf |
| 11 | gold-yellow | crown |
| 12 | sky blue | rocket |

Teslim: `TeamEmblem_01.png` … `TeamEmblem_12.png` (+ boş kalkan `TeamEmblem_Base.png`).

---

## 4. Buton ikonları — ÖNCELİK 2

Hepsi aynı seri: beyaz/renkli, kalın koyu kontur, buton üstüne oturacak. Bugün bazı butonlarda "□"
görünüyor (ikon/glyph eksik) — bunlar onun yerine gelecek.

| Dosya | Prompt (stil bloğundan önce) |
|-------|------------------------------|
| `Icon_Chat.png` | `A speech bubble game icon, white glossy bubble with three small blue dots inside, short tail at the bottom left.` |
| `Icon_Search.png` | `A magnifying glass game icon, white glossy handle and rim, light blue glass lens.` |
| `Icon_Close.png` | `A bold white X (cross) game icon with rounded ends, glossy, thick dark outline.` |
| `Icon_AddFriend.png` | `A bold white plus sign game icon with rounded ends, glossy, thick dark outline.` |
| `Icon_Invite.png` | `A magenta gift box game icon with a gold ribbon and bow, glossy, slightly tilted.` |
| `Icon_Info.png` | `A round white glossy info badge game icon with a bold blue lowercase "i" in the center.` |

Kalp ikonu var (üst bardaki kalp) — "Can İste" butonunda onu kullanacağım.

---

## 5. Alt menü "Team" ikonu — ÖNCELİK 2

Hâlâ iki robot. Diğer alt menü ikonlarıyla (harita, kupa, ev, market arabası) aynı stilde olmalı:
```
A game navigation bar icon showing two cute cartoon animal heads side by side, a brown bear cub and
a white bunny with aviator goggles, both smiling, slightly overlapping, in front of a small gold
heraldic shield. Same rendering style as a cartoon game menu icon set.
```
Teslim: `NavIcon_Team.png`. Karşılaştırma için mevcut ikonlar: `Art/UI/ScreenImages/Chapter001/NewUI/`
(`JourneyIcon`, `RanksIcon`, `HomeIcon`, `MarketIcon`).

---

## 6. Arka plan deseni — ÖNCELİK 3

Liste arkasındaki düz lacivert boş duruyor. Hafif, döşenebilir (tileable) desen; ben koyu zemin üstüne
düşük opaklıkla bindireceğim.
```
A seamless tileable pattern texture for a mobile game menu background: small, evenly spaced, simple
white line-art icons (paw prints, wrenches, gears, small stars) arranged in a diagonal grid on a flat
transparent background. Flat, minimal, single color white, no shading, no gradient. Perfectly seamless
on all four edges.
```
Not: bu görselde stil bloğunu **kullanma** (düz/flat olmalı). 1024×1024, saydam.
Teslim: `SocialBgPattern.png`.

---

## Teslim

Dosyaları `~/Downloads` içine at, adını söyle. Ben: arka plan temizliği, boyutlandırma, import
ayarları (UI sprite, mipmap kapalı), ilgili ekrana takma.

---

## 7. Tek tek prompt'lar (kopyala-yapıştır hazır)

Her blok tek başına tam prompt'tur (stil dahil). "Edit" olanlarda ilk görseli (altın madalya / boş kalkan) ekle; aracın düzenleme yoksa alttaki tam prompt'u kullan.

### RankMedal_1.png
```
A round game UI rank medal: a thick polished gold coin-shaped medal with a raised inner rim, a big bold number "1" embossed in the center in white with a thick dark outline, and two short deep red ribbon tails hanging below the medal. Chunky, toy-like, cute cartoon proportions. No other text besides the number 1. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame.
```

### RankMedal_2.png
```
Edit the attached gold medal. Only change: the medal metal becomes polished silver, the ribbon becomes royal blue, the number becomes "2". Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A round game UI rank medal: a thick polished silver coin-shaped medal with a raised inner rim, a big bold number "2" embossed in the center in white with a thick dark outline, and two short royal blue ribbon tails hanging below the medal. Chunky, toy-like, cute cartoon proportions. No other text besides the number 2. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame.
```

### RankMedal_3.png
```
Edit the attached gold medal. Only change: the medal metal becomes polished bronze/copper, the ribbon becomes dark orange-brown, the number becomes "3". Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A round game UI rank medal: a thick polished bronze-copper coin-shaped medal with a raised inner rim, a big bold number "3" embossed in the center in white with a thick dark outline, and two short dark orange-brown ribbon tails hanging below the medal. Chunky, toy-like, cute cartoon proportions. No other text besides the number 3. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame.
```

### TrophyIcon.png
```
A shiny golden trophy cup game icon with two curled handles, a short stem and a wide stepped base, a small star emblem on the front of the cup. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_Base.png
```
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy deep red enamel, empty in the middle. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_01.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes deep red and a big white glossy wrench symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy deep red enamel, with a big white glossy wrench symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_02.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes royal blue and a big white glossy animal paw print symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy royal blue enamel, with a big white glossy animal paw print symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_03.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes emerald green and a big white glossy gear cog symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy emerald green enamel, with a big white glossy gear cog symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_04.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes purple and a big white glossy hammer symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy purple enamel, with a big white glossy hammer symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_05.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes orange and a big white glossy lightning bolt symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy orange enamel, with a big white glossy lightning bolt symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_06.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes teal and a big white glossy star symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy teal enamel, with a big white glossy star symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_07.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes black and a big white glossy flame symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy black enamel, with a big white glossy flame symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_08.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes pink and a big white glossy heart symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy pink enamel, with a big white glossy heart symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_09.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes navy blue and a big white glossy anchor symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy navy blue enamel, with a big white glossy anchor symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_10.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes lime green and a big white glossy leaf symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy lime green enamel, with a big white glossy leaf symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_11.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes golden yellow and a big white glossy crown symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy golden yellow enamel, with a big white glossy crown symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### TeamEmblem_12.png
```
Edit the attached empty shield. Only change: the inner enamel color becomes sky blue and a big white glossy rocket symbol with a dark outline is placed in the center of the shield. Keep everything else EXACTLY identical: same size, same position, same outline thickness, same lighting, same camera.

(If your tool cannot edit an image, use this full prompt instead:)
A heraldic game UI team badge: a rounded shield with a thick polished gold frame and two small gold side tabs at the top corners. The inner face of the shield is smooth glossy sky blue enamel, with a big white glossy rocket symbol with a dark outline in the center. Chunky cute cartoon proportions. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### Icon_Chat.png
```
A speech bubble game icon: a white glossy rounded speech bubble with three small blue dots inside and a short tail at the bottom left. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### Icon_Search.png
```
A magnifying glass game icon: white glossy handle and rim, light blue glass lens with one clean highlight. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### Icon_Close.png
```
A bold white X (cross) game icon with rounded ends, glossy, thick dark outline. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### Icon_AddFriend.png
```
A bold white plus sign game icon with rounded ends, glossy, thick dark outline. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### Icon_Invite.png
```
A magenta gift box game icon with a gold ribbon and a big gold bow on top, glossy, slightly tilted. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### Icon_Info.png
```
A round white glossy info badge game icon with a bold blue lowercase letter i in the center. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame.
```

### NavIcon_Team.png
```
A game navigation bar icon: two cute cartoon animal heads side by side, a brown bear cub and a white bunny with aviator goggles, both smiling, slightly overlapping, in front of a small gold heraldic shield. Same rendering style as a cartoon game menu icon set. Style: premium mobile puzzle game UI icon, polished glossy finish, crisp razor-sharp rendering, vivid saturated colors, smooth shading with a clean bevel, thick dark outline around the whole silhouette. ONE key light from the top-left, one clean highlight per surface, no scattered specular spots, no rim lights. Big simple shapes readable at 64x64 pixels. Straight front view, centered, the object fills about 88% of a square 1:1 canvas. Fully transparent background (PNG with alpha), no shadow outside the object, no watermark, no frame. No text.
```

### SocialBgPattern.png
```
A seamless tileable pattern texture for a mobile game menu background: small, evenly spaced, simple white line-art icons (paw prints, wrenches, gears, small stars) arranged in a diagonal grid on a fully transparent background. Flat, minimal, single color white, no shading, no gradient, no outline, no text. Perfectly seamless on all four edges. Square 1:1, 1024x1024.
```

---

## 8. Butonlar & satır kartları (9-slice) — tek tek prompt'lar

Hepsi YAZISIZ üretilir (yazıyı oyun koyar). Ortası düz/tekdüze olmalı ki Unity'de esnetilince bozulmasın. Önce Btn_Orange'ı üret, beğenince diğer renkleri aynı görseli edit ederek türet ("Only change the color to ...").

### Btn_Orange.png
```
A wide pill-shaped game button, color warm orange-yellow (top) to deep orange (bottom). Canvas aspect ratio 3:1 (for example 1536x512). Style: cute premium mobile puzzle game UI button (Royal Match / Candy Crush quality), chunky and toy-like, polished glossy candy finish, crisp razor-sharp rendering, vivid saturated colors. Construction: a rounded-rectangle capsule with fully rounded short ends, a slightly darker thick 3D bottom lip (pressable depth), a soft glossy highlight band along the top inner edge, and a thin dark outline around the whole shape. ONE key light from the top-left, no scattered specular spots, no noise. IMPORTANT for 9-slice stretching: the middle section must be perfectly uniform horizontally (the same from left to right), all decoration only near the edges, no text, no icon, no pattern in the middle. Straight front view, the button centered and filling about 90% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the button, no watermark.
```

### Btn_Green.png
```
A wide pill-shaped game button, color bright lime green (top) to deep green (bottom). Canvas aspect ratio 3:1 (for example 1536x512). Style: cute premium mobile puzzle game UI button (Royal Match / Candy Crush quality), chunky and toy-like, polished glossy candy finish, crisp razor-sharp rendering, vivid saturated colors. Construction: a rounded-rectangle capsule with fully rounded short ends, a slightly darker thick 3D bottom lip (pressable depth), a soft glossy highlight band along the top inner edge, and a thin dark outline around the whole shape. ONE key light from the top-left, no scattered specular spots, no noise. IMPORTANT for 9-slice stretching: the middle section must be perfectly uniform horizontally (the same from left to right), all decoration only near the edges, no text, no icon, no pattern in the middle. Straight front view, the button centered and filling about 90% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the button, no watermark.
```

### Btn_Blue.png
```
A wide pill-shaped game button, color sky blue (top) to royal blue (bottom). Canvas aspect ratio 3:1 (for example 1536x512). Style: cute premium mobile puzzle game UI button (Royal Match / Candy Crush quality), chunky and toy-like, polished glossy candy finish, crisp razor-sharp rendering, vivid saturated colors. Construction: a rounded-rectangle capsule with fully rounded short ends, a slightly darker thick 3D bottom lip (pressable depth), a soft glossy highlight band along the top inner edge, and a thin dark outline around the whole shape. ONE key light from the top-left, no scattered specular spots, no noise. IMPORTANT for 9-slice stretching: the middle section must be perfectly uniform horizontally (the same from left to right), all decoration only near the edges, no text, no icon, no pattern in the middle. Straight front view, the button centered and filling about 90% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the button, no watermark.
```

### Btn_Red.png
```
A wide pill-shaped game button, color bright red (top) to dark crimson (bottom). Canvas aspect ratio 3:1 (for example 1536x512). Style: cute premium mobile puzzle game UI button (Royal Match / Candy Crush quality), chunky and toy-like, polished glossy candy finish, crisp razor-sharp rendering, vivid saturated colors. Construction: a rounded-rectangle capsule with fully rounded short ends, a slightly darker thick 3D bottom lip (pressable depth), a soft glossy highlight band along the top inner edge, and a thin dark outline around the whole shape. ONE key light from the top-left, no scattered specular spots, no noise. IMPORTANT for 9-slice stretching: the middle section must be perfectly uniform horizontally (the same from left to right), all decoration only near the edges, no text, no icon, no pattern in the middle. Straight front view, the button centered and filling about 90% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the button, no watermark.
```

### Btn_Purple.png
```
A wide pill-shaped game button, color bright violet (top) to deep purple (bottom). Canvas aspect ratio 3:1 (for example 1536x512). Style: cute premium mobile puzzle game UI button (Royal Match / Candy Crush quality), chunky and toy-like, polished glossy candy finish, crisp razor-sharp rendering, vivid saturated colors. Construction: a rounded-rectangle capsule with fully rounded short ends, a slightly darker thick 3D bottom lip (pressable depth), a soft glossy highlight band along the top inner edge, and a thin dark outline around the whole shape. ONE key light from the top-left, no scattered specular spots, no noise. IMPORTANT for 9-slice stretching: the middle section must be perfectly uniform horizontally (the same from left to right), all decoration only near the edges, no text, no icon, no pattern in the middle. Straight front view, the button centered and filling about 90% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the button, no watermark.
```

### BtnSq_Red.png
```
A small square game button with generously rounded corners, color bright red to dark crimson. Square canvas 1:1 (1024x1024). Style: cute premium mobile puzzle game UI button (Royal Match / Candy Crush quality), chunky and toy-like, polished glossy candy finish, crisp razor-sharp rendering, vivid saturated colors. Construction: a rounded-rectangle square with generously rounded corners, a slightly darker thick 3D bottom lip (pressable depth), a soft glossy highlight band along the top inner edge, and a thin dark outline around the whole shape. ONE key light from the top-left, no scattered specular spots, no noise. IMPORTANT for 9-slice stretching: the middle section must be perfectly uniform horizontally (the same from left to right), all decoration only near the edges, no text, no icon, no pattern in the middle. Straight front view, the button centered and filling about 90% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the button, no watermark.
```

### BtnSq_Green.png
```
A small square game button with generously rounded corners, color bright lime green to deep green. Square canvas 1:1 (1024x1024). Style: cute premium mobile puzzle game UI button (Royal Match / Candy Crush quality), chunky and toy-like, polished glossy candy finish, crisp razor-sharp rendering, vivid saturated colors. Construction: a rounded-rectangle square with generously rounded corners, a slightly darker thick 3D bottom lip (pressable depth), a soft glossy highlight band along the top inner edge, and a thin dark outline around the whole shape. ONE key light from the top-left, no scattered specular spots, no noise. IMPORTANT for 9-slice stretching: the middle section must be perfectly uniform horizontally (the same from left to right), all decoration only near the edges, no text, no icon, no pattern in the middle. Straight front view, the button centered and filling about 90% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the button, no watermark.
```

### BtnSq_Blue.png
```
A small square game button with generously rounded corners, color sky blue to royal blue. Square canvas 1:1 (1024x1024). Style: cute premium mobile puzzle game UI button (Royal Match / Candy Crush quality), chunky and toy-like, polished glossy candy finish, crisp razor-sharp rendering, vivid saturated colors. Construction: a rounded-rectangle square with generously rounded corners, a slightly darker thick 3D bottom lip (pressable depth), a soft glossy highlight band along the top inner edge, and a thin dark outline around the whole shape. ONE key light from the top-left, no scattered specular spots, no noise. IMPORTANT for 9-slice stretching: the middle section must be perfectly uniform horizontally (the same from left to right), all decoration only near the edges, no text, no icon, no pattern in the middle. Straight front view, the button centered and filling about 90% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the button, no watermark.
```

### Row_Normal.png
```
A wide leaderboard row card: inside fill warm cream (#F8E9C4), frame light tan-brown. Canvas aspect ratio 5:1 (for example 2000x400). Style: cute premium mobile puzzle game UI list-row card (Royal Match leaderboard quality), polished glossy finish, crisp razor-sharp rendering, vivid colors. Construction: a wide rounded rectangle with softly rounded corners, a thick 3D bottom lip, a thin dark outline. ONE key light from the top-left, no noise. IMPORTANT for 9-slice stretching: the frame must have the same thickness on all sides and the inside must be a perfectly uniform flat fill (no gradient spots, no texture, no decoration in the middle); all decoration only on the border. No text, no icons. Straight front view, the card centered and filling about 94% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the card, no watermark.
```

### Row_Gold.png
```
A wide leaderboard row card: inside fill warm cream (#F8E9C4), frame shiny polished GOLD with a few tiny sparkles on the frame only, slightly thicker frame (1st place podium card). Canvas aspect ratio 5:1 (for example 2000x400). Style: cute premium mobile puzzle game UI list-row card (Royal Match leaderboard quality), polished glossy finish, crisp razor-sharp rendering, vivid colors. Construction: a wide rounded rectangle with softly rounded corners, a thick 3D bottom lip, a thin dark outline. ONE key light from the top-left, no noise. IMPORTANT for 9-slice stretching: the frame must have the same thickness on all sides and the inside must be a perfectly uniform flat fill (no gradient spots, no texture, no decoration in the middle); all decoration only on the border. No text, no icons. Straight front view, the card centered and filling about 94% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the card, no watermark.
```

### Row_Silver.png
```
A wide leaderboard row card: inside fill cool light cream (#F2EEE6), frame shiny polished SILVER, slightly thicker frame (2nd place podium card). Canvas aspect ratio 5:1 (for example 2000x400). Style: cute premium mobile puzzle game UI list-row card (Royal Match leaderboard quality), polished glossy finish, crisp razor-sharp rendering, vivid colors. Construction: a wide rounded rectangle with softly rounded corners, a thick 3D bottom lip, a thin dark outline. ONE key light from the top-left, no noise. IMPORTANT for 9-slice stretching: the frame must have the same thickness on all sides and the inside must be a perfectly uniform flat fill (no gradient spots, no texture, no decoration in the middle); all decoration only on the border. No text, no icons. Straight front view, the card centered and filling about 94% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the card, no watermark.
```

### Row_Bronze.png
```
A wide leaderboard row card: inside fill warm cream (#F6E4C8), frame shiny polished BRONZE/copper, slightly thicker frame (3rd place podium card). Canvas aspect ratio 5:1 (for example 2000x400). Style: cute premium mobile puzzle game UI list-row card (Royal Match leaderboard quality), polished glossy finish, crisp razor-sharp rendering, vivid colors. Construction: a wide rounded rectangle with softly rounded corners, a thick 3D bottom lip, a thin dark outline. ONE key light from the top-left, no noise. IMPORTANT for 9-slice stretching: the frame must have the same thickness on all sides and the inside must be a perfectly uniform flat fill (no gradient spots, no texture, no decoration in the middle); all decoration only on the border. No text, no icons. Straight front view, the card centered and filling about 94% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the card, no watermark.
```

### Row_Self.png
```
A wide leaderboard row card: inside fill fresh lime green (#7CC23A), frame brighter yellow-green with a light top rim (the player's own row). Canvas aspect ratio 5:1 (for example 2000x400). Style: cute premium mobile puzzle game UI list-row card (Royal Match leaderboard quality), polished glossy finish, crisp razor-sharp rendering, vivid colors. Construction: a wide rounded rectangle with softly rounded corners, a thick 3D bottom lip, a thin dark outline. ONE key light from the top-left, no noise. IMPORTANT for 9-slice stretching: the frame must have the same thickness on all sides and the inside must be a perfectly uniform flat fill (no gradient spots, no texture, no decoration in the middle); all decoration only on the border. No text, no icons. Straight front view, the card centered and filling about 94% of the canvas width. Fully transparent background (PNG with alpha), no shadow outside the card, no watermark.
```

### Chip_Dark.png
```
A small rounded pill-shaped value chip for numbers like 21/50: inside fill soft warm brown-gray (#B9A58C) recessed into the surface (inner shadow at the top), thin darker outline, no 3D lip. Canvas aspect ratio 3:1. IMPORTANT for 9-slice: perfectly uniform middle, no text. Polished, crisp, cute mobile puzzle game style, ONE key light from the top-left. Fully transparent background (PNG with alpha), no watermark.
```
