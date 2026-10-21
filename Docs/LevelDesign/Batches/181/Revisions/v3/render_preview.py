"""Real project sprites; illustrative random colors, not a game screenshot."""
from pathlib import Path
import copy,importlib.util,json
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
spec=importlib.util.spec_from_file_location('preview173',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
d=json.loads((HERE/'design.json').read_text())
def project(excluded=()):
    out=copy.deepcopy(d);out['stacks']=[]
    for s in sorted(d['stacks'],key=lambda s:s['stackOrder']):
        o,i=s['obstacleId'],s['originCellIndex']
        if o in excluded:continue
        import re
        w,h=map(int,re.search(r'^    size: \{x: (\d+), y: (\d+)\}$',r.DEFS[o],re.M).groups())
        for dy in range(h):
            for dx in range(w):
                j=(i//9+dy)*9+i%9+dx
                out['data']['obstacles'][j]=o;out['data']['obstacleOrigins'][j]=i
    return out
im=Image.new('RGBA',(1200,1115),r.BG);draw=ImageDraw.Draw(im)
draw.text((32,25),'181 / Basamaklı Arşiv · v3',font=r.font(40,True),fill=r.GOLD)
draw.text((33,82),'9 × 11 · 26 hamle · 4 renk · Normal',font=r.font(24),fill=r.INK)
draw.text((33,122),'1,1 → 9,9 çapraz hole · İki bölge · Dört dolapta üç katman',font=r.font(20),fill=r.MUTED)
r.board(im,project(),(32,175),70)
x,y=718,176
for heading,lines in [('HEDEFLER',['6 eski ColorChest','4 dolap · toplam 40 eşya','16 oyma taşı','40 çamur hücresi']),('OYUN FİKRİ',[d['decision']]),('KATMAN AKIŞI',['Her dolap → 4 oyma taşı → çamur.','Her ColorChest → çamur.'])]:
    draw.text((x,y),heading,font=r.font(20,True),fill=r.GOLD);y+=34
    for line in lines:y=r.paragraph(draw,x,y,line,width=37,size=18)
    y+=19
draw.text((718,620),'GÖVDELER AÇILINCA',font=r.font(17,True),fill=r.GOLD)
draw.text((944,620),'EN ALTTAKİ TABAN',font=r.font(17,True),fill=r.GOLD)
r.board(im,project((21,27)),(718,654),23,base=True)
r.board(im,project((21,27,28)),(944,654),23,base=True)
draw.text((33,983),'90 aktif · 9 hole · 50 başlangıç taşı · 26 hamle ve hedef yükü korundu',font=r.font(21),fill=r.INK)
draw.text((33,1028),'Mevcut sprite’larla tasarım önizlemesi; normal taş renkleri temsili.',font=r.font(19),fill=r.MUTED)
draw.text((33,1061),'26 hamle ilk ayar; kazanma oranı ölçülmedi. Önceki levellar korundu.',font=r.font(19),fill=r.MUTED)
path=ROOT/'Docs/LevelDesign/Level181-Basamakli-Arsiv.png';im.convert('RGB').save(path)
print(path)
