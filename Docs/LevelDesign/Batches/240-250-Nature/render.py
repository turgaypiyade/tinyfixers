"""Gerçek proje sprite'larıyla tek level önizlemesi (statik; oynanmış an değildir)."""
import sys, importlib.util
from pathlib import Path
from PIL import Image, ImageDraw
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent/'235-250-Late-Game'))
import render as late_render
_spec = importlib.util.spec_from_file_location('nature_author', HERE/'author.py')
nature = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(nature)
art = late_render.art

def render(n):
    d = nature.prepare(n); cell = 64
    W = 40 + d['width']*cell + 40 + 520; H = max(40 + d['height']*cell + 40, 760)
    im = Image.new('RGBA', (W, H), '#18263a'); dr = ImageDraw.Draw(im)
    late_render.drawboard(im, d, (40, 60), cell)
    x = 80 + d['width']*cell
    dr.text((x, 30), f"{n} · {d['name']}", font=art.font(34, True), fill='#ffd27a')
    dr.text((x, 78), f"{d['difficulty']} · {d['width']}×{d['height']} · {d['moves']} hamle", font=art.font(22), fill='#e7edf6')
    y = 130
    for title, text in [('FİKİR', d['idea']), ('İLK KARAR', d['firstDecision']), ('AÇILINCA', d['change']), ('KALAN İŞ', d['remaining'])]:
        dr.text((x, y), title, font=art.font(20, True), fill='#ffd27a'); y += 30
        y = late_render.paragraph(dr, text, x, y, 44, 19) or y + 8
        y += 14
    gy = H - 120
    for k, g in enumerate(d['goals']):
        gx = x + k*150
        icon = art.sprite(art.ref(g['iconOverride']))
        art.paste(im, icon, (gx, gy, 60, 60))
        dr.text((gx + 68, gy + 16), str(g['amount']), font=art.font(26, True), fill='white')
    out = HERE/f'Level{n}.png'; im.convert('RGB').save(out); print(out)

if __name__ == '__main__': render(int(sys.argv[1]))
