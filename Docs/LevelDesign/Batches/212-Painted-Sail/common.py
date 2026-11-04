"""Static level artwork/geometry helpers. Never advances moves, gravity or cascades."""
from pathlib import Path
import copy,importlib.util,re,sys
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
spec=importlib.util.spec_from_file_location('rv212',HERE.parent/'131-205-Review-2026-10-31/render.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
original_spawn=r.spawn
def layers(d):
    out=[[(o,d['data']['obstacleOrigins'][i])] if o else [] for i,o in enumerate(d['data']['obstacles'])]
    w=d['width']
    for e in d['custom']['tubes']:
        origin=e['originCellIndex'];dx,dy=((0,-1),(0,1),(-1,0),(1,0))[e['direction']]
        for k in range(e['length']):out[origin+k*(dx+dy*w)].append((26,origin))
    events=[(e.get('stackOrder',0),0,e) for e in d['custom']['stackedObstacles']]+[(e.get('stackOrder',0),1,e) for e in d['custom']['safes']]
    from check_levels161_162 import footprint
    for _,kind,e in sorted(events,key=lambda v:v[:2]):
        origin=e['originCellIndex'];o=33 if kind else e['obstacleId'];bw,bh=(e['width'],e['height']) if kind else footprint(o)
        for dy in range(bh):
            for dx in range(bw):out[origin+dy*w+dx].append((o,origin))
    removed=set(d.get('_removedFamilies',()))
    return [[v for v in cell if v[0] not in removed] for cell in out]
def phase(d,removed):
    q=copy.deepcopy(d);q['_removedFamilies']=tuple(removed)
    ls=layers(q);q['top']=[c[-1][0] if c else 0 for c in ls];q['topOrigins']=[c[-1][1] if c else -1 for c in ls]
    return q
def feed(d):
    q=copy.deepcopy(d)
    # Safe's blocker state is service-owned; its library stage alone says blocksCells=0.
    # Stone has the same blocking/diagonal flags required for this geometric checkpoint.
    if d['level']==212:
        ls=layers(d)
        # A see-through grass cap doesn't turn its still-live 2x2 paint box into a tile slot.
        q['top']=[28 if any(o==33 for o,_ in cell) else 63 if any(o==63 for o,_ in cell) else cell[-1][0] if cell else 0 for cell in ls]
    else:q['top']=[28 if o==33 else o for o in q['top']]
    slots,reached=original_spawn(q)
    return slots,reached
def seeded_feed(d):
    slots,reached=feed(d)
    from check_levels161_162 import array
    text=(ROOT/d['asset']).read_text();pins=array(text,'pinnedTileTypes',d['width']*d['height'])
    return slots,reached|{i for i,v in enumerate(pins) if v and i in slots}
def paint_art():
    im=r.art.oid_art(63).copy().resize((800,800),Image.Resampling.LANCZOS)
    can=r.art.resource('PaintCanBox','PaintCan')
    w=.19;h=w*can.height/can.width;step=h*.78
    total=2*step+h
    if total>.66:
        factor=.66/total;w*=factor;h*=factor;step*=factor
    for row,n in enumerate((4,3,2)):
        for j in range(n):
            cx=.5+(j-(n-1)*.5)*w;by=.152+row*step
            r.art.paste(im,can,((cx-w/2)*800,(1-by-h)*800,w*800,h*800))
    return im
def safe_art():
    text=(ROOT/'Assets/_Project/Prefabs/FX/Safe/SafeObstacleView.prefab').read_text()
    blocks={m[2]:(int(m[1]),m[3]) for m in re.finditer(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
    images={}
    for _,(typ,b) in blocks.items():
        if typ!=1:continue
        name=re.search(r'^  m_Name: (.*)',b,re.M)[1]
        for comp in re.findall(r'component: \{fileID: (\d+)\}',b):
            c=blocks[comp][1]
            if '  m_Sprite: {fileID: 21300000, guid:' in c:images[name]=r.art.sprite(r.art.ref(re.search(r'^  m_Sprite: (.*)$',c,re.M)[1]))
    im=Image.new('RGBA',(800,800));r.art.paste(im,images['Body'],(0,0,800,800));r.art.paste(im,images['lockPanel'],(100,100,600,600))
    for j,(name,x) in enumerate([('knobred',-82.0998),('knobyellow',-3.35628),('knobgreen',71.7728)]):
        icon=images[name].copy()
        if j:icon.putalpha(icon.getchannel('A').point(lambda a:round(a*.38)))
        r.art.paste(im,icon,((200+x-25)*2,(200-2.88462-20)*2,100,80))
        ImageDraw.Draw(im).text(((200+x)*2,250),'3',anchor='mm',font=r.art.font(91,True),fill=['#ff6666','#ad9646','#4c9555'][j],stroke_width=2,stroke_fill='#29193d')
    return im
def install_art():
    r.art.ART_CACHE[63]=paint_art();r.art.ART_CACHE[33]=safe_art();r.spawn=seeded_feed
def board(im,d,xy,cell):
    if d['level']!=212:return r.board(im,d,xy,cell)
    under=phase(d,set(d.get('_removedFamilies',()))|{42})
    r.board(im,under,xy,cell)
    # Current runtime draws see-through Grass above the retained, visible lower layer.
    for i,o in enumerate(d['top']):
        if o==42:r.art.paste(im,r.art.oid_art(42),(xy[0]+i%d['width']*cell,xy[1]+i//d['width']*cell,cell,cell))
