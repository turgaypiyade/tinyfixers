"""Tek seviye önizlemesi (131-205 inceleme çizicisiyle aynı görünüm). python3 render_one.py 197 out.png"""
import sys, importlib.util
from pathlib import Path
R = Path(__file__).resolve().parents[1]/'131-205-Review-2026-10-31/render.py'
spec = importlib.util.spec_from_file_location('rv', R); rv = importlib.util.module_from_spec(spec)
sys.argv_backup = sys.argv; spec.loader.exec_module(rv)
from PIL import Image, ImageDraw
lv = int(sys.argv[1]); out = sys.argv[2]
d = next(x for x in rv.review.load(rv.ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset') if x['level'] == lv)
im = Image.new('RGBA', (720, 890), '#19283a'); dr = ImageDraw.Draw(im)
dr.text((24,16), f'{d["level"]} · {d["name"]}', font=rv.art.font(25,True), fill='#ffcf75')
dr.text((24,55), f'{d["width"]}×{d["height"]} · {d["moves"]} moves · {sum(d["data"]["cells"])} active', font=rv.art.font(18), fill='white')
rv.board(im, d, (28,95), 53)
for i, g in enumerate(d['goals']): dr.text((28,705+i*25), f'{rv.goal_label(g)}: {g["amount"]}', font=rv.art.font(18), fill='white')
dr.text((28,856), 'Static project art · tile colours illustrative · dots: unfilled start candidates', font=rv.art.font(13), fill='#a9bdd1')
im.convert('RGB').save(out); print(out)
