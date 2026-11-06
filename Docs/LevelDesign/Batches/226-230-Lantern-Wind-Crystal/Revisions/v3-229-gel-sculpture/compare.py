"""Read-only comparison of current229 and closest catalog boards, with project art."""
import importlib.util,json,re
from collections import deque
from PIL import Image,ImageDraw
import render as current
from author import HERE,BATCH,ROOT,shared

spec=importlib.util.spec_from_file_location('historical229comparison',BATCH.parent/'131-205-Review-2026-10-31/render.py')
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)

def current_spawn(d):
    w,h=d['width'],d['height'];active={i for i,v in enumerate(d['data']['cells']) if v};top=d['top']
    blocked={i for i in active if top[i] and (shared.stage(top[i],'blocksCells') or shared.stage(top[i],'holdsTile'))}
    movable={i for i in active if top[i] and shared.stage(top[i],'behavior')==3};slots=active-blocked-movable;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in blocked:break
            if i in active:reached.add(i)
    corner=lambda i:i in active and (i not in blocked or shared.stage(top[i],'allowDiagonal'))
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
        if above and all(re.search(r'^    fillsShadowBeneath: 1$',shared.DEFS[o],re.M) for o in above):reached.add(i)
    return slots,reached&slots

def main():
    old.spawn=current_spawn
    levels=old.review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')
    im=Image.new('RGBA',(1710,900),'#172536');dr=ImageDraw.Draw(im)
    dr.text((22,18),'229 · Güncel katalogdaki yakın örneklerle görsel karşılaştırma',font=current.art.font(28,True),fill='#ffcf75')
    for j,n in enumerate([229,114,183]):
        x=22+j*560;d=next(d for d in levels if d['level']==n)
        dr.text((x,83),f'{n}: {d["name"]}',font=current.art.font(22,True),fill='#e7edf6')
        if n==229:current.board(im,current.d,(x,135),49)
        else:old.board(im,d,(x,135),49)
    dr.text((22,744),'114: jel / oyma / örtü katmanları · 183: kırpılmış engel silueti yakınlığı. Puanlar yalnız aday seçer.',font=current.art.font(22),fill='#a9bdd1')
    dr.text((22,787),'229: örtülü çapraz jel kaynakları → iki sütunu açan presler → sağdaki Rodos / porselen işi.',font=current.art.font(22),fill='#a9bdd1')
    dr.text((22,832),'Gerçek proje sprite’ları · Serbest taş renkleri temsili · Yeni taslak kullanıcı tarafından oynanmadı/onaylanmadı.',font=current.art.font(20),fill='#a9bdd1')
    assert not old.WARNINGS,old.WARNINGS
    im.convert('RGB').save(HERE/'Comparison.png')
    print('Compared current229,114,183 with current catalog and real sprites; no missing art.')

if __name__=='__main__':main()
