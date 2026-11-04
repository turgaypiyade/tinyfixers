"""Before/after project sprites, plus historical authored geometry for the shortlist."""
from pathlib import Path
import json,sys
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
ROOT=next(p for p in HERE.parents if(p/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').exists())
sys.path.insert(0,str(BATCH))
from render import art,board,ds
from common import review
old={d['level']:d for d in json.loads((HERE/'designs.json').read_text())};levels=review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')
sheet=Image.new('RGBA',(1600,1700),'#172536');draw=ImageDraw.Draw(sheet)
for row,n in enumerate((213,215,217,218)):
    y=20+row*420;d=next(d for d in ds if d['level']==n)
    draw.text((20,y),f'{n} · ÖNCE',font=art.font(23,True),fill='#ffcf75');board(sheet,old[n],(20,y+43),29)
    draw.text((330,y),f'{n} · GÜNCEL',font=art.font(23,True),fill='#ffcf75');board(sheet,d,(330,y+43),29)
    report=json.loads((ROOT/f'Docs/LevelDesign/Reviews/213-220-Showcase-v2/{n}/Similarity.json').read_text())
    ns=list(dict.fromkeys([p['level'] for p in report['geometryShortlist'][:2]]+[p['level'] for p in report['croppedShapeShortlist'][:1]]+([191,196] if n in(217,218) else [])))[:4]
    for col,ref in enumerate(ns):
        p=next(p for p in levels if p['level']==ref);x=660+col*235
        draw.text((x,y),f'{ref} · {p["name"][:20]}',font=art.font(16,True),fill='#e7edf6')
        review.drawboard(sheet,p,(x,y+43),24)
        draw.text((x,y+335),'Tarihsel geometri',font=art.font(15),fill='#a9bdd1')
    draw.text((20,y+383),'Önce/sonra: güncel proje artı, serbest taş renkleri temsili; sağ: yakın geçmiş geometri.',font=art.font(17),fill='#a9bdd1')
sheet.convert('RGB').save(HERE/'Before-After-Comparison.png')
print('Four before/after sprite and historical geometry comparisons rendered.')
