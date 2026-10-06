# Yeni Engeller — Görsel & Animasyon Prompt'ları

Kapsam: **Bukalemun**, **Bozuk Makine**, **Aç Hamster**, **Tamir Borusu (Portal)**.
Kural (kullanıcı, 2026-10-05): önce görseller çizilir; görsel + animasyon iyi olmazsa kodlanmaz.

Prompt'lar İngilizce (görsel üreticiler İngilizceyi daha iyi anlıyor), açıklamalar Türkçe.

---

## 0. Ortak kurallar — HER prompt'un sonuna ekle

### Stil bloğu (kopyala-yapıştır)
Kullanıcı tercihi (2026-10-05): ekranda **parlak** = çok kaliteli, NET, keskin, canlı çizim. İstenmeyen:
birçok yönden ışık alan, dağınık parlamalı görünüm. → Cilalı ve canlı, ama TEK ışık kaynağı.
```
Style: premium, high-quality mobile puzzle game asset, polished glossy finish, crisp and razor-sharp
rendering, clean edges, vivid saturated colors, rich depth with smooth shading and a clean bevel.
Lighting: ONE single key light from the top-left with a soft fill; one clean, well-placed highlight per
surface; no multiple light sources, no scattered or random specular spots, no rim lights from several sides.
Clean surfaces, no noise, no grain, no blur, no muddy colors. Big, simple, high-contrast shapes that stay
readable at about 100x100 pixels on a phone screen.
Camera: front view, slightly from above (about 20 degrees), centered, orthographic feel, no perspective distortion.
Composition: single object centered, filling about 85% of a square canvas, small even margin on all sides.
Background: fully transparent (PNG with alpha). No floor, no cast shadow outside the object, no text,
no watermark, no frame, no border.
```

**Okunabilirlik testi (her görselde):** görseli 100×100'e küçült → ne olduğu hâlâ hemen anlaşılıyor mu,
ana renkler birbirinden ayrışıyor mu? Anlaşılmıyorsa detay azalt, formu büyüt.

### Teknik kurallar
- **Kare (1:1)**, 1024×1024 üret → oyuna **512×512** girer (çok hücreli engel istisna, aşağıda yazıyor).
- Arka plan **gerçekten saydam** olmalı. Araç saydam vermiyorsa düz **#FF00FF magenta** zemin iste, ben temizlerim.
- Mevcut taşlarla yan yana koyup bak (`Art/Icons/Gems`): aynı ışık yönü (sol üst), aynı "kalınlık" hissi.

### Tutarlılık yöntemi (EN ÖNEMLİSİ)
Duvarda yaşadık: aşamaları ayrı ayrı ürettik → tuğla dizilimi her karede farklı çıktı, oyunda "zıplıyor".
Bunu önlemek için:
1. Önce **ANA görseli** üret, beğenene kadar tekrarla.
2. Diğer kareleri **sıfırdan üretme** — ana görseli aracın **düzenleme / inpaint / "edit image"** özelliğiyle değiştir:
   yalnız değişmesi gereken bölgeyi seç, prompt'a şunu ekle:
   ```
   Keep everything else EXACTLY identical: same size, same position, same outline, same lighting,
   same colors, same camera. Only change: <değişen şey>.
   ```
3. Karakter karelerinde (hamster) ana görseli **referans görsel** olarak ver ("character reference").
4. Her kare aynı tuvalde aynı noktaya oturmalı (ayak hizası / merkez sabit). Kontrolü ben yaparım.

### Animasyon nasıl kurulacak
Kare kare çizgi film çizmiyoruz. **Birkaç anahtar kare + kodla hareket** (Koç saldırısında olduğu gibi):
kodla yapılanlar → esneme/zıplama (squash & stretch), dönme, büyüme, solma, titreme, parçacık, renk geçişi.
Görsel üretimden sonra ben karelerden **hareketli önizleme (GIF)** çıkarırım; beğenmezsen kodlamayız.

---

## 1. Bukalemun Taş 🦎

**Ne:** Normal taş gibi eşleşir ama her hamle sonunda rengini değiştirir (sarı → mavi → yeşil → kırmızı → …).
Taşın kendisi, kıvrılıp yuvarlak taş şekli almış minik bir bukalemun.

**Gereken görseller**

| Dosya | Ne |
|---|---|
| `Chameleon_Base.png` | ANA görsel — YEŞİL bukalemun, gözler açık |
| `Chameleon_Blink.png` | Aynı bukalemun, gözler kapalı (renk değişirken göz kırpar) |
| Diğer renkler | **Çizme** — sarı/mavi/kırmızıyı ben ana görselden renk kaydırmayla üretirim (gözler korunur). Beğenmezsen 1.3'teki prompt'la çizersin |

### 1.1 Chameleon_Base (ANA)
```
A tiny adorable chameleon curled up into a round, compact, tile-like ball shape (like a cinnamon roll),
its spiral tail tucked in front, two big shiny googly eyes on turret-like eye sockets looking at the viewer,
small happy smile. Bright green glossy skin with subtle lighter green scale pattern and a soft belly stripe.
It must read clearly as a single match-3 game piece at small size: simple, bold shape, round overall
silhouette, no thin details, no tools or accessories.
```
+ Stil bloğu.

**Kabul kriteri:** 120 px'e küçültünce hâlâ "yeşil taş + sevimli bukalemun" diye okunmalı;
yanındaki yeşil taşla (şişe) aynı yeşil tonunda olmalı.

### 1.2 Chameleon_Blink (ana görselden DÜZENLEME)
Seç: yalnız iki göz.
```
Close both eyes in a happy, relaxed blink (curved closed eyelids). Keep everything else EXACTLY identical:
same size, same position, same outline, same lighting, same colors, same camera. Only change: the eyes are closed.
```

### 1.3 (Gerekirse) elle renkli versiyonlar — ana görselden DÜZENLEME
```
Change ONLY the skin color to <warm golden yellow | bright royal blue | candy red>, keeping the same
lighter scale pattern and belly stripe in a matching lighter tone. Eyes stay exactly the same.
Keep everything else EXACTLY identical: same size, same position, same outline, same lighting, same camera.
```

**Animasyon (kod):** hamle bitince → göz kırp (Blink) → küçük esneme + renk geçişi (0.25 sn) → gözler açılır, minik parıltı.

---

## 2. Bozuk Makine ⚙️💨 (Sihirli Kazan karşılığı)

**Ne:** 2×2 hücrelik, eski, yamalı bir tamir makinesi. Kırılana (tamir edilene) kadar her 2–3 hamlede
tahtaya hurda püskürtür (tahtadaki bir taşın üstüne çamur/cıvata engeli düşer). 3 vuruşta tamir olur.
"Wonder Fixers" adının tam karşılığı: oyuncu bozuk makineyi tamir ediyor.

**Boyut:** 2×2 hücre → **1024×1024 üret, oyuna 512×512** (ekranda ~290 px).

**Gereken görseller**

| Dosya | Ne |
|---|---|
| `BrokenMachine_Idle.png` | ANA — kapalı, hafif dumanlı, yamalı bozuk makine |
| `BrokenMachine_Spit.png` | Aynı makine, üst kapağı açık, ağzından hurda fırlarken (püskürtme anı) |
| `BrokenMachine_Hit1.png` | 1. vuruş: bir panel düşmüş, kıvılcım |
| `BrokenMachine_Hit2.png` | 2. vuruş: daha çok hasar, bir dişli dışarı fırlamış, yoğun duman |
| `BrokenMachine_Junk.png` | Fırlayan hurda parçaları seti (ayrı küçük parçalar, 3–4 adet, aralıklı): cıvata, somun, küçük dişli, yay |
| `BrokenMachine_Fixed.png` | (İsteğe bağlı) tamir anında bir an görünen parlak/temiz hali — yok olmadan hemen önce |

### 2.1 BrokenMachine_Idle (ANA)
```
A chunky, cute but grumpy old workshop machine that is clearly broken: a rounded boxy body made of
dented teal-blue painted metal with brass rivets, a small chimney on top puffing a little grey smoke,
a round porthole window on the front with a tired cartoon "face" made of two cracked gauge dials as eyes
and a crooked vent as a frowning mouth, mismatched patches held with duct tape and bolts, a loose spring
sticking out, one stubby little leg shorter than the other. Friendly, toy-like proportions, not scary.
Square overall silhouette that fits a 2x2 game tile area.
```
+ Stil bloğu.

### 2.2 BrokenMachine_Spit (DÜZENLEME — üst kapak + ağız bölgesi)
```
The top hatch pops open and the machine angrily spits out a burst of junk (bolts, nuts, a small gear)
upward with a puff of dark smoke; the gauge-eyes squint. Keep everything else EXACTLY identical:
same size, same position, same outline, same lighting, same colors, same camera.
```

### 2.3 BrokenMachine_Hit1 (ana görselden DÜZENLEME — sağ ön panel)
```
One front side panel has fallen off revealing tangled wires and small sparks; one rivet is missing.
Keep everything else EXACTLY identical: same size, same position, same outline, same lighting, same colors, same camera.
```

### 2.4 BrokenMachine_Hit2 (Hit1'den DÜZENLEME)
```
More damage: the chimney is bent, a big gear is sticking out of the side, thicker smoke and more sparks,
one gauge-eye glass is cracked. Keep everything else EXACTLY identical to the previous image:
same size, same position, same outline, same lighting, same colors, same camera.
```

### 2.5 BrokenMachine_Junk (ayrı üretim)
```
A sprite sheet of 4 separate small junk pieces on a transparent background, evenly spaced in a 2x2 grid,
not touching each other: a chunky steel bolt, a hex nut, a small brass gear, a coiled spring.
Same polished, crisp art style and single top-left light as the machine.
```
+ Stil bloğu (Composition satırı hariç).

### 2.6 BrokenMachine_Fixed (isteğe bağlı, ana görselden DÜZENLEME)
```
The machine is fully repaired and happy: shiny clean teal paint, no patches, no smoke, the gauge-eyes
sparkle and the vent mouth is a big smile, small sparkle highlights. Keep the same size, position,
silhouette, lighting and camera.
```

**Animasyon (kod):** Idle'da hafif nefes alma (esneme) + duman; püskürtmeden önce 0.3 sn titreme →
Spit karesi + hurda parçaları kavisle hedef hücreye uçar → Idle. Vuruşta sarsıntı + kıvılcım.
Tamir: Fixed karesi bir an parlar → parçalara ayrılıp dağılır.

---

## 3. Aç Hamster 🐹 (Aç Hayvan) — 2×2

**Karar (2026-10-05, kullanıcı):** 2×2. Ana görsel üretildi ve OLDUĞU GİBİ kullanılıyor (turuncu hamster,
alında havacı gözlüğü, kırmızı bandana, saydam zemin) — 2×2'de taşlarla karışmaz; alet çantası vb. EKLENMEZ.
Tüm kareler ana görselle aynı kare tuval, aynı ölçek, aynı ayak hizası. 1024×1024 → oyuna 512×512.

### 3.1 Hamster_Idle (ANA) — mevcut görsel
Tuvali kareye tamamla (hamster ortada, ayaklar altta, ~%85 doluluk). Yeni üretim gerekmez.

### 3.2 Yüz kareleri — ana görseli DÜZENLE (yalnız baş/yüz seçili)
Sonuna: `Keep everything else EXACTLY identical: same body, same size, same position, same outline, same lighting, same colors, same camera. Only the face/head area changes.`
- **Hamster_Chomp:** `Mouth wide open in an excited, eager chomp, eyes wide and sparkling, empty cheeks, leaning the head slightly forward.`
- **Hamster_Cheeks1:** `Cheeks half stuffed and round, chewing happily with a closed smiling mouth, eyes curved in a happy smile.`
- **Hamster_Cheeks2:** `Cheeks hugely stuffed and puffed out to almost the width of his body, eyes squeezed shut in bliss, tiny pink blush on the cheeks, closed smiling mouth.`

### 3.3 Gövde kareleri — ana görsel KARAKTER REFERANSI, yeni üretim
Başına: `Same hamster character as the reference image: identical design, colors, fur, goggles, bandana, size and proportions, same camera and lighting, same square canvas framing and scale, feet on the same ground line.` + sonuna stil bloğu.
- **Hamster_Crouch:** `Crouching low and squashed, ready to leap, paws on the ground, cheeks fully stuffed, determined focused face.`
- **Hamster_Jump:** `Mid-air jump, body stretched upward, arms and legs spread out happily, ears flapping, cheeks fully stuffed, joyful face. The hamster is in the upper part of the canvas.`
- **Hamster_Land:** `Ground pound landing, squashed wide with paws slammed down, a small puff of dust at his feet, cheeks now empty, big satisfied grin, little dizzy swirl above his head.`

**Animasyon (kod):** Idle (nefes) → yandaki eşleşmede taşlar uçar → Chomp → doluluğa göre Cheeks1/Cheeks2 →
dolunca Crouch → Jump (kavisle yeni 2×2 alana) → Land (alttaki taşlar patlar, sarsıntı) → Idle.

---

## 4. Tamir Borusu (Portal) 🌀

**Ne:** Taşlar bir hücrenin altındaki boru ağzından girip tahtanın başka bir yerindeki boru ağzından
çıkar. Tahtada zaten boru görsellerimiz var (mavi boru, kırmızı halkalar) — aynı dünyadan.
Birden fazla boru çifti olabilir → çiftler RENKLE ayrılır (A: mavi, B: turuncu, C: mor).

**Boyut:** Boru ağzı hücrenin KENARINA oturur → **1024×512 üret (2:1), oyuna 512×256**.

**Gereken görseller** (her renk çifti için)

| Dosya | Ne |
|---|---|
| `Pipe_In_Blue.png` | Giriş ağzı: hücrenin ALT kenarına oturan, yukarıya açık yarım boru ağzı (içi karanlık) |
| `Pipe_Out_Blue.png` | Çıkış ağzı: hücrenin ÜST kenarına oturan, aşağıya açık yarım boru ağzı |
| `Pipe_Swirl.png` | Boru ağzının İÇİNE konan dönen girdap (ayrı katman — kod döndürür), renksiz/beyaz-mavi, saydam zemin, KARE 512 |
| Turuncu/mor çiftler | Mavi'yi beğenince ana görselden renk DÜZENLEMESİ ile (ya da ben renk kaydırırım) |

### 4.1 Pipe_In_Blue (ANA)
```
A short, wide, rounded industrial pipe opening seen from the front and slightly above, lying horizontally
like a mail slot, its dark round mouth facing upward into the cell above. Glossy royal-blue painted metal
pipe with a thick red rubber rim ring and chunky brass bolts around the rim, a tiny riveted brass plate
with an arrow pointing down into the pipe. The opening shows a dark inner tunnel with a soft blue glow deep inside.
Wide horizontal composition: the object spans the full width and the lower half of a 2:1 canvas.
```
+ Stil bloğu (Composition satırı yerine yukarıdaki yatay kompozisyon).

### 4.2 Pipe_Out_Blue (DÜZENLEME — ya da 4.1'i dikey çevirip ben üretirim)
```
The same pipe opening flipped so its mouth faces downward and it spans the upper half of the canvas,
the brass plate arrow pointing down out of the pipe. Keep the same style, colors, size and lighting.
```

### 4.3 Pipe_Swirl (ayrı üretim, kare)
```
A stylized swirling vortex seen straight on, spiral arms of soft white and light cyan glowing energy
with tiny sparkles, fading to transparent at the outer edge, perfectly round and centered,
seamless so it can be rotated in place. Transparent background.
```

**Animasyon (kod):** girdap sürekli döner + hafif nabız; taş girişte küçülerek boruya çekilir,
çıkışta büyüyerek fırlar; giriş/çıkış anında ağızda minik halka parlaması (TeleportMarkerAnim zaten var).

---

## 5. Teslim & değerlendirme

1. Görselleri `Art/UI/Obstacles/RobotStyle/<EngelAdı>/` altına koy (isimler yukarıdaki gibi).
2. Ben her engel için: arka plan temizliği, 512'ye indirme, hizalama kontrolü, **hareketli önizleme (GIF)**
   ve mevcut taşlarla yan yana tahta önizlemesi çıkarırım.
3. Önizleme iyi değilse → prompt'u düzeltip yeniden üretiriz. İyiyse → kodlama.

Önerilen çizim sırası: Bukalemun (en az görsel) → Bozuk Makine → Aç Hamster (en çok kare) → Portal.
