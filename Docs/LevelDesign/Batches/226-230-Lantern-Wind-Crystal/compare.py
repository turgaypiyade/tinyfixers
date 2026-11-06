"""Current active-catalog sprite comparison, with current movable/pass-through fill rules."""
import importlib.util
from collections import deque
from PIL import Image,ImageDraw
import render as new
from design import HERE,ROOT,stage,DEFS
import re
spec=importlib.util.spec_from_file_location('historical_current_art',HERE.parent/'131-205-Review-2026-10-31/render.py')
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)

def current_spawn(d):
    w,h=d['width'],d['height'];active={i for i,v in enumerate(d['data']['cells']) if v};top=d['top']
    blocked={i for i in active if top[i] and (stage(top[i],'blocksCells') or stage(top[i],'holdsTile'))}
    movable={i for i in active if top[i] and stage(top[i],'behavior')==3};slots=active-blocked-movable;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in blocked:break
            if i in active:reached.add(i)
    corner=lambda i:i in active and (i not in blocked or stage(top[i],'allowDiagonal'))
    q=deque(reached)
    while q:
        i=q.popleft();x,y=i%w,i//w
        if y+1>=h:continue
        for dx in [0,-1,1]:
            j=i+w+dx
            if not 0<=x+dx<w or j not in active or j in blocked or j in reached:continue
            if dx and not(corner(i+dx) or corner(i+w)):continue
            reached.add(j);q.append(j)
    for i in slots-reached:
        above=[top[j] for j in range(i%w,i,w) if j in blocked]
        if above and all(re.search(r'^    fillsShadowBeneath: 1$',DEFS[o],re.M) for o in above):reached.add(i)
    return slots,reached&slots

def main():
    old.spawn=current_spawn
    levels=old.review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')
    pairs=[(226,111),(227,173),(228,61),(229,169),(230,133)]
    im=Image.new('RGBA',(2300,1220),'#172536');dr=ImageDraw.Draw(im)
    for j,(a,c) in enumerate(pairs):
        x=20+j*460;d=next(d for d in levels if d['level']==c)
        dr.text((x,20),f'Önceki {c}: {d["name"]}',font=new.art.font(21,True),fill='#a9bdd1');old.board(im,d,(x,70),37)
        d=next(d for d in new.DS if d['level']==a)
        dr.text((x,560),f'Yeni {a}: {d["name"]}',font=new.art.font(21,True),fill='#ffcf75');new.board(im,d,(x,610),37)
    dr.text((20,1125),'Aktif V1 katalogdan çözülen yakın adaylar · Statik geometri puanı tekrar/onay yüzdesi değildir.',font=new.art.font(23),fill='#a9bdd1')
    dr.text((20,1163),'228 / 61: iki kütle benzer; gerçek boarddaki çalışma alanı ve üretim/toplama akışı farklı. Renkler temsili.',font=new.art.font(21),fill='#a9bdd1')
    im.convert('RGB').save(HERE/'Comparison.png')
    assert not old.WARNINGS,old.WARNINGS
    print('Rendered five current-catalog comparisons; no missing-art warnings.')
if __name__=='__main__':main()
