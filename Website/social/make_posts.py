"""İlk Instagram paylaşımlarının görsellerini üretir (1080×1350 JPEG). Kaynak: ../img (site görselleri)."""
import pathlib, textwrap
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import numpy as np

HERE = pathlib.Path(__file__).resolve().parent
IMG = HERE.parent / 'img'
FONTS = HERE.parent.parent / 'Assets'
DYNA = FONTS / 'TextMesh Pro/Fonts/DynaPuff/DynaPuff-Bold.ttf'
NUNITO = FONTS / '_Project/Fonts/Nunito/Nunito-Black.ttf'
W, H = 1080, 1350
BLUE = (31, 91, 255); DEEP = (14, 34, 110); SUN = (255, 204, 61)

def font(p, s): return ImageFont.truetype(str(p), s)

def text_c(img, y, s, f, fill, shadow=True, stroke=0):
    d = ImageDraw.Draw(img)
    w = d.textlength(s, font=f); x = (W - w) / 2
    if shadow:
        sh = Image.new('RGBA', img.size, 0); ImageDraw.Draw(sh).text((x, y + 6), s, font=f, fill=(0, 0, 40, 150))
        img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)))
    ImageDraw.Draw(img).text((x, y), s, font=f, fill=fill, stroke_width=stroke, stroke_fill=DEEP)

def wrap_c(img, y, s, f, fill, width_chars, gap=1.25):
    for line in textwrap.wrap(s, width_chars):
        text_c(img, y, line, f, fill, shadow=False); y += int(f.size * gap)
    return y

def gradient(top, bottom):
    t = np.linspace(0, 1, H)[:, None, None]
    a = (np.array(top) * (1 - t) + np.array(bottom) * t).astype('uint8')
    return Image.fromarray(np.repeat(a, W, axis=1), 'RGB').convert('RGBA')

def logo_badge(size):
    src = Image.open(IMG / 'org-logo.png').convert('RGBA').resize((size, size), Image.LANCZOS)
    m = Image.new('L', (size * 4, size * 4), 0); ImageDraw.Draw(m).ellipse((0, 0, size * 4, size * 4), fill=255)
    src.putalpha(m.resize((size, size), Image.LANCZOS)); return src

def shadowed(base, im, xy, blur=18, off=14, alpha=110):
    sh = Image.new('RGBA', base.size, 0)
    a = im.split()[-1].point(lambda v: v * alpha // 255)
    sh.paste((0, 0, 30, 255), (xy[0], xy[1] + off), a)
    base.alpha_composite(sh.filter(ImageFilter.GaussianBlur(blur))); base.alpha_composite(im, xy)

def save(img, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    img.convert('RGB').save(path, 'JPEG', quality=92, optimize=True, progressive=True)

# ── 1) Merhaba ──────────────────────────────────────────────
def post_hello():
    hero = Image.open(IMG / 'hero.webp').convert('RGBA')
    crop = hero.crop((0, 110, 900, 110 + 1125)).resize((W, H), Image.LANCZOS)
    t = np.clip((np.linspace(0, 1, H) - 0.58) / 0.42, 0, 1) ** 1.4
    band = np.zeros((H, W, 4), 'uint8'); band[..., :3] = DEEP; band[..., 3] = (t * 235)[:, None].astype('uint8')
    crop.alpha_composite(Image.fromarray(band, 'RGBA'))
    crop.alpha_composite(logo_badge(150), (40, 40))
    text_c(crop, 1035, 'WONDER FIXERS', font(DYNA, 112), (255, 255, 255))
    text_c(crop, 1180, 'Coming soon on iOS & Android', font(NUNITO, 46), SUN)
    save(crop, HERE / '01-hello' / '1.jpg')

# ── 2) Ekip ─────────────────────────────────────────────────
CREW = [  # dosya, arka plan, rol, ad, cümle
    ('ram', (207, 224, 255), 'CREW CHIEF', 'The Ram', 'Reads the blueprints, wins the duels and never leaves a job half done.'),
    ('bear', (255, 227, 176), 'HEAVY LIFTING', 'The Bear', 'Carries the stone blocks. Also grows the best carrots in the garden.'),
    ('pig', (255, 214, 214), 'MASTER MECHANIC', 'The Pig', 'If it is bolted, riveted or stuck, the hammer comes out.'),
    ('rabbit', (216, 240, 220), 'SCOUT', 'The Rabbit', 'First on site. Finds the cracks before anyone else does.'),
    ('monkey', (230, 220, 255), 'WELDER', 'The Monkey', 'Patches up cracked screens and iron beams with the same steady hand.'),
]

def post_crew():
    out = HERE / '02-crew'
    cover = gradient((52, 120, 255), DEEP)
    text_c(cover, 110, 'MEET THE CREW', font(DYNA, 104), (255, 255, 255))
    text_c(cover, 250, 'Five fixers. One tool for everything.', font(NUNITO, 44), SUN)
    # iki sıra: arkada ayı + domuz, önde tavşan, koç, maymun (alt kenarları hizalı)
    rows = [(('bear', 330), ('pig', 750)), (('rabbit', 185), ('ram', 545), ('monkey', 895))]
    for bottom, row in ((960, rows[0]), (1300, rows[1])):
        for n, cx in row:
            im = Image.open(IMG / f'crew-{n}.webp').convert('RGBA')
            s = 430 / im.height; im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
            shadowed(cover, im, (cx - im.width // 2, bottom - im.height))
    save(cover, out / '1.jpg')
    for i, (n, bg, role, name, line) in enumerate(CREW, start=2):
        light = tuple(min(255, c + 30) for c in bg)
        img = gradient(light, bg)
        glow = Image.new('RGBA', (W, H), 0); ImageDraw.Draw(glow).ellipse((90, 160, 990, 1060), fill=(255, 255, 255, 140))
        img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(90)))
        im = Image.open(IMG / f'crew-{n}.webp').convert('RGBA')
        s = 780 / im.height; im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
        shadowed(img, im, ((W - im.width) // 2, 150), blur=24, off=20, alpha=70)
        d = ImageDraw.Draw(img); f = font(NUNITO, 34); w = d.textlength(role, font=f)
        d.rounded_rectangle(((W - w) / 2 - 28, 60, (W + w) / 2 + 28, 118), 29, fill=DEEP)
        d.text(((W - w) / 2, 64), role, font=f, fill=SUN)
        text_c(img, 975, name, font(DYNA, 104), DEEP, shadow=False)
        wrap_c(img, 1115, line, font(NUNITO, 42), (40, 52, 96), 36)
        img.alpha_composite(logo_badge(84), (W - 84 - 34, H - 84 - 34))
        save(img, out / f'{i}.jpg')

# ── 3) Harikalar ────────────────────────────────────────────
WONDERS = [
    ('pisa', 'Leaning Paw Tower', 'Pisa, Italy · First job'),
    ('goldengate', 'Golden Tail Bridge', 'San Francisco, USA'),
    ('eiffel', 'Eiffur Tower', 'Paris, France'),
    ('pyramid', 'Purramid', 'Giza, Egypt'),
    ('canyon', 'A new wonder', 'Somewhere in Arizona… · Up next'),
]

def post_wonders():
    out = HERE / '03-wonders'
    for i, (n, name, where) in enumerate(WONDERS, start=1):
        src = Image.open(IMG / f'wonder-{n}.webp').convert('RGBA')
        bg = src.resize((W, int(src.height * W / src.width)), Image.LANCZOS).crop((0, 0, W, H)).filter(ImageFilter.GaussianBlur(40))
        bg.alpha_composite(Image.new('RGBA', (W, H), DEEP + (120,)))
        ch = 1060; cw = int(src.width * ch / src.height)
        card = src.resize((cw, ch), Image.LANCZOS)
        m = Image.new('L', (cw, ch), 0); ImageDraw.Draw(m).rounded_rectangle((0, 0, cw - 1, ch - 1), 44, fill=255)
        card.putalpha(m)
        shadowed(bg, card, ((W - cw) // 2, 56), blur=26, off=18, alpha=160)
        text_c(bg, 1142, name, font(DYNA, 76), (255, 255, 255))
        text_c(bg, 1250, where, font(NUNITO, 38), SUN)
        save(bg, out / f'{i}.jpg')

# ── 4) Ekip v2: Characters/ klasöründeki yüksek çözünürlüklü görseller (2026-10-18) ──
CHARS = HERE.parent.parent / 'Characters'
CHAR_FILES = {
    'ram': 'Adventurous Ram Explorer with Map.png', 'bear': 'Cheerful Bear Adventurer Explorer.png',
    'pig': 'Cheerful Pig Engineer with Wrench.png', 'rabbit': 'Goggled Bunny Map Adventurer.png',
    'monkey': 'Cheerful Monkey Engineer with Welding Torch.png', 'bird': 'Cheerful Aviator Bird in Flight.png',
}

NAMES = {'ram': 'Bram', 'bear': 'Brumo', 'pig': 'Tinko', 'rabbit': 'Pemi', 'monkey': 'Fiko', 'bird': 'Pilo'}

def char(n, height):
    im = Image.open(CHARS / CHAR_FILES[n]).convert('RGBA')
    im = im.crop(im.getchannel('A').getbbox())
    s = height / im.height
    return im.resize((max(1, int(im.width * s)), height), Image.LANCZOS)

def post_crew_v2():
    out = HERE / '04-crew-v2'
    cover = gradient((52, 120, 255), DEEP)
    glow = Image.new('RGBA', (W, H), 0); ImageDraw.Draw(glow).ellipse((60, 380, 1020, 1340), fill=(140, 190, 255, 110))
    cover.alpha_composite(glow.filter(ImageFilter.GaussianBlur(110)))
    text_c(cover, 96, 'MEET THE CREW', font(DYNA, 104), (255, 255, 255))
    text_c(cover, 236, 'Five fixers. One tool for everything.', font(NUNITO, 44), SUN)
    bird = char('bird', 230)
    shadowed(cover, bird, (W - bird.width - 40, 300), blur=16, off=12, alpha=80)
    # arka sıra: uzun boylular (ayı, koç) — alt kenarları hizalı; ön sıra: tavşan, domuz, maymun
    for n, cx, h, bottom in (('bear', 330, 560, 1000), ('ram', 760, 560, 1000)):
        im = char(n, h); shadowed(cover, im, (cx - im.width // 2, bottom - im.height), blur=18, off=12, alpha=90)
    for n, cx, h in (('rabbit', 190, 500), ('pig', 545, 480), ('monkey', 895, 500)):
        im = char(n, h); shadowed(cover, im, (cx - im.width // 2, 1310 - im.height), blur=18, off=14, alpha=110)
    save(cover, out / '1.jpg')
    for i, (n, bg, role, _, line) in enumerate(CREW, start=2):
        name = NAMES[n]
        light = tuple(min(255, c + 30) for c in bg)
        img = gradient(light, bg)
        glow = Image.new('RGBA', (W, H), 0); ImageDraw.Draw(glow).ellipse((90, 160, 990, 1060), fill=(255, 255, 255, 140))
        img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(90)))
        im = char(n, 790)
        if im.width > 900: im = im.resize((900, int(im.height * 900 / im.width)), Image.LANCZOS)
        shadowed(img, im, ((W - im.width) // 2, 140 + (790 - im.height)), blur=24, off=20, alpha=70)
        d = ImageDraw.Draw(img); f = font(NUNITO, 34); w = d.textlength(role, font=f)
        d.rounded_rectangle(((W - w) / 2 - 28, 60, (W + w) / 2 + 28, 118), 29, fill=DEEP)
        d.text(((W - w) / 2, 64), role, font=f, fill=SUN)
        text_c(img, 975, name, font(DYNA, 104), DEEP, shadow=False)
        wrap_c(img, 1115, line, font(NUNITO, 42), (40, 52, 96), 36)
        img.alpha_composite(logo_badge(84), (W - 84 - 34, H - 84 - 34))
        save(img, out / f'{i}.jpg')

# ── 5) Ekip (orijinal düzen) — yalnız koç görseli yeni (Characters/), gerisi aynı (2026-10-18) ──
def crew_img(n):
    if n == 'ram':
        im = Image.open(CHARS / CHAR_FILES['ram']).convert('RGBA')
        return im.crop(im.getchannel('A').getbbox())
    return Image.open(IMG / f'crew-{n}.webp').convert('RGBA')

# Onaylı karakter isimleri (2026-10-18) — düzen 05-crew-newram ile birebir aynı.
CREW_NAMED = [(n, bg, role, name, line) for (n, bg, role, _, line), name in
              zip(CREW, ('Bram', 'Brumo', 'Tinko', 'Pemi', 'Fiko'))]

def post_crew_newram(out=None, crew=None):
    out = out or HERE / '05-crew-newram'
    crew = crew or CREW
    cover = gradient((52, 120, 255), DEEP)
    text_c(cover, 110, 'MEET THE CREW', font(DYNA, 104), (255, 255, 255))
    text_c(cover, 250, 'Five fixers. One tool for everything.', font(NUNITO, 44), SUN)
    rows = [(('bear', 330), ('pig', 750)), (('rabbit', 185), ('ram', 545), ('monkey', 895))]
    for bottom, row in ((960, rows[0]), (1300, rows[1])):
        for n, cx in row:
            im = crew_img(n)
            s = 430 / im.height; im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
            shadowed(cover, im, (cx - im.width // 2, bottom - im.height))
    save(cover, out / '1.jpg')
    for i, (n, bg, role, name, line) in enumerate(crew, start=2):
        light = tuple(min(255, c + 30) for c in bg)
        img = gradient(light, bg)
        glow = Image.new('RGBA', (W, H), 0); ImageDraw.Draw(glow).ellipse((90, 160, 990, 1060), fill=(255, 255, 255, 140))
        img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(90)))
        im = crew_img(n)
        s = 780 / im.height; im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
        shadowed(img, im, ((W - im.width) // 2, 150), blur=24, off=20, alpha=70)
        d = ImageDraw.Draw(img); f = font(NUNITO, 34); w = d.textlength(role, font=f)
        d.rounded_rectangle(((W - w) / 2 - 28, 60, (W + w) / 2 + 28, 118), 29, fill=DEEP)
        d.text(((W - w) / 2, 64), role, font=f, fill=SUN)
        text_c(img, 975, name, font(DYNA, 104), DEEP, shadow=False)
        wrap_c(img, 1115, line, font(NUNITO, 42), (40, 52, 96), 36)
        img.alpha_composite(logo_badge(84), (W - 84 - 34, H - 84 - 34))
        save(img, out / f'{i}.jpg')

if __name__ == '__main__':
    import sys
    if 'crew2' in sys.argv: post_crew_v2()
    elif 'newram' in sys.argv: post_crew_newram()
    elif 'names' in sys.argv: post_crew_newram(HERE / '07-crew-names', CREW_NAMED)
    else: post_hello(); post_crew(); post_wonders()
    print('ok')
