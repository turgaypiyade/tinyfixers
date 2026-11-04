"""Static level artwork/geometry helpers. Never advances moves, gravity or cascades."""
from pathlib import Path
import copy,importlib.util,re,sys
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
spec=importlib.util.spec_from_file_location('rv212',HERE.parent/'131-205-Review-2026-10-31/render.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
original_spawn=r.spawn
def feed(d):
    q=copy.deepcopy(d)
    # Safe's blocker state is service-owned; its library stage alone says blocksCells=0.
    # Stone has the same blocking/diagonal flags required for this geometric checkpoint.
    q['top']=[28 if o==33 else o for o in q['top']]
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
def board(im,d,xy,cell):r.board(im,d,xy,cell)
