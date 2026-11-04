"""Sprite and geometry comparison against each review shortlist; no asset edits."""
import json,re
from PIL import Image,ImageDraw
from render import r,art,ds,board
from common import ROOT,HERE,review
levels=review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')
# Old reference assets can omit unpinned arrays (runtime defaults to zero).
strict_array=r.array
r.array=lambda text,key,size:strict_array(text,key,size) if re.search(r'^  '+key+r': [0-9a-f]+$',text,re.M) else [0]*size
out=HERE/'Comparisons';out.mkdir(exist_ok=True)
for d in ds:
    report=json.loads((ROOT/f'Docs/LevelDesign/Reviews/213-220-Showcase/{d["level"]}/Similarity.json').read_text())
    ns=list(dict.fromkeys([p['level'] for p in report['geometryShortlist'][:2]]+[p['level'] for p in report['croppedShapeShortlist'][:2]]+([217] if d['level']==219 else [210] if d['level']==220 else [])+[d['level']]))
    im=Image.new('RGBA',(len(ns)*248,710),'#172536');draw=ImageDraw.Draw(im)
    for j,n in enumerate(ns):
        p=next(x for x in levels if x['level']==n);x=j*248+8
        draw.text((x,12),f'{n} · {p["name"][:22]}',font=art.font(15,True),fill='#ffcf75')
        if n>=213:board(im,next(z for z in ds if z['level']==n),(x,48),25)
        else:r.board(im,p,(x,48),25)
        review.drawboard(im,p,(x,370),25)
        draw.text((x,665),'Sprite / authored geometry',font=art.font(14),fill='#a9bdd1')
    im.convert('RGB').save(out/f'Level{d["level"]}.png')
print('Eight shortlist sprite/geometry comparisons rendered.')
