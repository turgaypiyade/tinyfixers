"""Project-sprite authoring preview, not a game screenshot or a simulated playthrough."""
from pathlib import Path
import importlib.util
import json
import random
import re
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
spec = importlib.util.spec_from_file_location('dimerdal_project_art', HERE.parent / '173-180/render_previews.py')
art = importlib.util.module_from_spec(spec)
spec.loader.exec_module(art)
d = json.loads((HERE / 'design.json').read_text())
lib = (ROOT / 'Assets/_Project/Settings/ObstacleLibrary.asset').read_text()
block = re.search(r'^  - id: 85\n(.*?)(?=^  - id:|\Z)', lib, re.M | re.S)[1]
aux = block.split('auxiliarySprites:', 1)[1].split('allowCrossContextFallback:', 1)[0]
frame, diamond, slat = [art.sprite(art.ref(ref)) for ref in re.findall(r'^    - (.*)$', aux, re.M)]

def stretch(canvas, sprite, box):
    x, y, w, h = map(round, box)
    canvas.alpha_composite(sprite.resize((max(1, w), max(1, h)), Image.Resampling.LANCZOS), (x, y))

def shutter(canvas, box, remaining=4):
    x, y, w, h = box
    stretch(canvas, frame, box)
    art.paste(canvas, diamond, (x + w * .22, y + h * .23, w * .56, h * .54))
    left, top, width, height = x + w * .108, y + h * .115, w * .784, h * .753
    for i in range(remaining):
        stretch(canvas, slat, (left, top + height * i / 4, width, height / 4))

def colors():
    rng = random.Random(221)
    pins = {i: value - 1 for i, value in enumerate(d['data']['pinnedTileTypes']) if value}
    result = dict(pins)
    for i, active in enumerate(d['data']['cells']):
        if not active or d['data']['obstacles'][i] or i in pins:
            continue
        options = list(range(4))
        rng.shuffle(options)
        for value in options:
            invalid = False
            for dx, dy in [(1, 0), (0, 1)]:
                for offset in [-2, -1, 0]:
                    coords = [(i % 8 + (offset + step) * dx, i // 8 + (offset + step) * dy) for step in range(3)]
                    if not all(0 <= x < 8 and 0 <= y < 10 for x, y in coords):
                        continue
                    others = [y * 8 + x for x, y in coords if y * 8 + x != i]
                    if all(result.get(other) == value for other in others):
                        invalid = True
            if not invalid:
                result[i] = value
                break
        else:
            raise AssertionError('No illustrative color available')
    return result

COLORS = colors()

def board(canvas, xy, cell, remaining=4, arrow=False):
    dr = ImageDraw.Draw(canvas)
    x0, y0 = xy
    for i, active in enumerate(d['data']['cells']):
        if not active:
            continue
        x, y = x0 + i % 8 * cell, y0 + i // 8 * cell
        dr.rounded_rectangle((x + 1, y + 1, x + cell - 1, y + cell - 1), radius=cell // 10, fill='#354c65', outline='#65839c')
        if i in COLORS:
            art.paste(canvas, art.TILES[COLORS[i]], (x, y, cell, cell), cell * .07)
            if d['data']['pinnedTileTypes'][i]:
                r = max(2, cell // 22)
                dr.ellipse((x + 4, y + 4, x + 4 + 2 * r, y + 4 + 2 * r), fill='#ffcf75')
    for origin in d['origins']:
        shutter(canvas, (x0 + origin % 8 * cell, y0 + origin // 8 * cell, 2 * cell, 2 * cell), remaining)
    if arrow:
        x, y1, y2 = x0 + 3.5 * cell, y0 + 6.5 * cell, y0 + 7.5 * cell
        dr.line((x, y1 + cell * .1, x, y2 - cell * .15), fill='#ffdc75', width=max(2, cell // 16))
        dr.polygon([(x, y2), (x - cell * .12, y2 - cell * .18), (x + cell * .12, y2 - cell * .18)], fill='#ffdc75')

def paragraph(dr, text, xy, width=47, size=21):
    from textwrap import wrap
    y = xy[1]
    for line in wrap(text, width):
        dr.text((xy[0], y), line, font=art.font(size), fill='#e7edf6')
        y += size + 8
    return y

def render():
    canvas = Image.new('RGBA', (1360, 1280), '#172536')
    dr = ImageDraw.Draw(canvas)
    dr.text((34, 22), '221 · MAVİ VİTRİN / BLUE SHOWCASE', font=art.font(34, True), fill='#ffcf75')
    dr.text((34, 73), 'DIMERDAL TANITIMI · 8×10 · 68 hücre · 24 hamle · 4 taş rengi', font=art.font(23), fill='#e7edf6')
    dr.text((34, 125), 'BAŞLANGIÇ — YALNIZ MAVİ KUTULAR', font=art.font(23, True), fill='#ffcf75')
    board(canvas, (34, 171), 64, arrow=True)
    dr.text((650, 125), 'KEPENKLER AÇILDIĞINDA', font=art.font(23, True), fill='#ffcf75')
    board(canvas, (650, 171), 42, remaining=0)
    dr.text((650, 607), 'Açık görünüm kesiti; oynanmış bir an değildir.', font=art.font(17), fill='#a9bdd1')
    art.paste(canvas, art.sprite(art.ref(d['goals'][0]['iconOverride'])), (650, 650, 66, 66))
    dr.text((728, 663), 'HEDEF: 8 ELMAS', font=art.font(25, True), fill='#ffcf75')
    y = paragraph(dr, '1. Mavi taşı aşağı kaydır: üçlü, iki kepengin üst yüzüne ulaşır.', (650, 735), size=20)
    y = paragraph(dr, '2. Dört uygun renk / special vuruşuyla kepenkleri aç.', (650, y + 10), size=20)
    paragraph(dr, '3. Açık kutunun yanındaki mavi eşleşmeyle bir elmas üret; sonra elması ayrıca topla.', (650, y + 10), size=20)
    dr.text((34, 940), 'ALTTAN AÇILAN DÖRT DİLİM', font=art.font(22, True), fill='#ffcf75')
    for hit in range(5):
        x = 34 + hit * 205
        shutter(canvas, (x, 980, 128, 128), 4 - hit)
        dr.text((x + 64, 1125), f'{hit} hit', anchor='mt', font=art.font(20, True), fill='#e7edf6')
    dr.text((34, 1204), 'Güncel proje sprite’ları. Serbest taş renkleri temsili; altın noktalar 6 gerçek başlangıç renk pinini gösterir.', font=art.font(18), fill='#a9bdd1')
    dr.text((34, 1238), 'İlk deneme ayarıdır; oynanış dengesi ölçülmedi. Statik veri / şekil / besleme kontrolü yapıldı.', font=art.font(18), fill='#a9bdd1')
    out = ROOT / 'Docs/LevelDesign/Level221-Mavi-Vitrin.png'
    canvas.convert('RGB').save(out)
    (HERE / 'illustrative-colors.json').write_text(json.dumps(COLORS, indent=2) + '\n')
    print(out)

if __name__ == '__main__':
    render()
