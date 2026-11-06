"""Static positional and cropped geometry shortlist; does not predict gameplay."""
from pathlib import Path
import sys
from PIL import Image, ImageDraw, ImageFont, ImageOps

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'Tools'))
import review_level_patterns as review

levels = {d['level']: d for d in review.load(ROOT / 'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')}
review.COLORS[85] = '#257eff'
numbers = [221, 216, 170, 153, 85, 201]
out = Image.new('RGB', (1500, 830), '#19283a')
dr = ImageDraw.Draw(out)
font = lambda n: ImageFont.truetype(review.FONT, n)
dr.text((24, 18), '221 — konumlu tahta ve kırpılmış engel silueti karşılaştırması', font=font(26), fill='#ffcf75')
for column, number in enumerate(numbers):
    d = levels[number]
    x = 24 + column * 248
    dr.text((x, 72), f'{number} · {d["name"][:20]}', font=font(17), fill='#e7edf6')
    dr.text((x, 101), f'{d["width"]}×{d["height"]}', font=font(16), fill='#a9bdd1')
    review.drawboard(out, d, (x, 139), 22)
    dr.text((x, 421), 'Konumsuz engel silueti', font=font(16), fill='#a9bdd1')
    crop = review.plane(d, 'blocked', True).resize((180, 220), Image.Resampling.NEAREST)
    out.paste(ImageOps.colorize(crop, '#0c1420', '#86bfff'), (x, 458))
    origins = len({origin for oid, origin in zip(d['top'], d['topOrigins']) if oid})
    dr.text((x, 704), f'{origins} engel origin', font=font(16), fill='#e7edf6')
dr.text((24, 760), 'Geometrik envanter: renkler mekanik aile çizimi değildir. Aynı dikdörtgen siluet, aynı oyun fikri veya onay anlamına gelmez.', font=font(18), fill='#a9bdd1')
out.save(ROOT / 'Docs/LevelDesign/Reviews/221-Dimerdal-Intro/Comparison.png')
print('Wrote 221 /216 /170 /153 /85 /201 static geometry comparison.')
