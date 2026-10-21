"""Static projection of existing DynamicBoardBorder placement and current scene sprites.
Not a Unity screenshot; validates topology and allows close inspection without changing renderer code.
"""
from pathlib import Path
from collections import Counter
import importlib.util,json,re
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
spec=importlib.util.spec_from_file_location('preview',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
scene=(ROOT/'Assets/_Project/Scenes/01_Game.unity').read_text()
scene=re.search(r'm_EditorClassIdentifier: Assembly-CSharp::DynamicBoardBorder\n(.*?)(?=^---)',scene,re.M|re.S)[1]
def scalar(k):return float(re.search(r'^  '+k+r': ([\d.]+)$',scene,re.M)[1])
tile=scalar('tileSize');thickness=scalar('borderThickness');gap=scalar('outsideGap');ratio=scalar('spriteCoreRatio')
offset=gap+thickness*ratio/2 if scalar('keepBorderOutsideGrid') else 0
sprite_set=(ROOT/'Assets/_Project/Settings/BorderThemeSet/BorderSpriteSet_DarkNavy.asset').read_text()
sprites={}
for key in re.findall(r'^  (\w+Prefab):',sprite_set,re.M):
    ref=r.ref(re.search(r'^  '+key+r': (.*)$',sprite_set,re.M)[1])
    prefab=r.GUIDS[ref['guid']][0].read_text()
    sprites[key]=r.sprite(r.ref(re.search(r'^  m_Sprite: (.*)$',prefab,re.M)[1]))
corners={1:('outerLT',1,1),2:('outerRT',-1,1),4:('outerRB',-1,-1),8:('outerLB',1,-1),14:('innerLT',-1,-1),13:('innerRT',1,-1),11:('innerRB',1,1),7:('innerLB',-1,1)}
def render(d):
    size=(round(9*tile+80),round(11*tile+80));im=Image.new('RGBA',size,'#c6aa84');draw=ImageDraw.Draw(im);pad=40
    def solid(x,y):return 0<=x<9 and 0<=y<11 and d['data']['cells'][y*9+x]
    def mask(x,y):return sum(bit for bit,(cx,cy) in zip((1,2,4,8),((x-1,y-1),(x,y-1),(x,y),(x-1,y))) if solid(cx,cy))
    def trim(m):
        if m in (0,15,3,12,9,6):return 0
        if m in (1,2,4,8):return -gap if scalar('keepBorderOutsideGrid') else 0
        return max(0,thickness/2-offset)
    def paste(key,x,y,w,h):
        art=sprites[key+'Prefab'].resize((max(1,round(w)),max(1,round(h))),Image.Resampling.LANCZOS)
        im.alpha_composite(art,(round(pad+x-w/2),round(pad+y-h/2)))
    for y in range(11):
        for x in range(9):
            if solid(x,y):draw.rectangle((pad+x*tile,pad+y*tile,pad+(x+1)*tile,pad+(y+1)*tile),fill='#9e9cc5')
    counts=Counter()
    for ny in range(12):
        for x in range(9):
            above,below=solid(x,ny-1),solid(x,ny)
            if bool(above)==bool(below):continue
            a,b=trim(mask(x,ny)),trim(mask(x+1,ny));length=tile-a-b
            paste('edgeTop' if below else 'edgeBottom',x*tile+a+length/2,ny*tile+(-offset if below else offset),length,thickness)
    for y in range(11):
        for nx in range(10):
            left,right=solid(nx-1,y),solid(nx,y)
            if bool(left)==bool(right):continue
            a,b=trim(mask(nx,y)),trim(mask(nx,y+1));length=tile-a-b
            paste('edgeLeft' if right else 'edgeRight',nx*tile+(-offset if right else offset),y*tile+a+length/2,thickness,length)
    for ny in range(12):
        for nx in range(10):
            m=mask(nx,ny);counts[m]+=1
            if m in (5,7,10,11,13,14) and scalar('junctionEnabled'):
                x,y=pad+nx*tile,pad+ny*tile;half=scalar('junctionSize')/2**.5
                draw.polygon(((x,y-half),(x+half,y),(x,y+half),(x-half,y)),fill='#9492ca')
            parts=[corners[m]] if m in corners else [('innerRT',1,-1),('innerLB',-1,1)] if m==5 else [('innerLT',-1,-1),('innerRB',1,1)] if m==10 else []
            for key,dx,dy in parts:paste(key,nx*tile+dx*offset,ny*tile+dy*offset,thickness,thickness)
    return im,counts
old=json.loads((HERE/'Revisions/v3/design.json').read_text());new=json.loads((HERE/'design.json').read_text())
before,bmask=render(old);after,amask=render(new)
assert bmask[5]+bmask[10]==8 and amask[5]+amask[10]==0
canvas=Image.new('RGBA',(1500,1510),'#19283a');draw=ImageDraw.Draw(canvas)
draw.text((25,20),'181 / ÇAPRAZ BOŞLUK VE KÖŞE BİRLEŞİMLERİ',font=r.font(32,True),fill=r.GOLD)
for x,label,art in ((25,'v3 · Yalnız köşeden temas eden delikler',before),(780,'v4 · Kenardan bağlı tek boşluk şeridi',after)):
    draw.text((x,79),label,font=r.font(22,True),fill=r.INK)
    canvas.alpha_composite(art.resize((round(art.width*.65),round(art.height*.65)),Image.Resampling.LANCZOS),(x,121))
    crop=art.crop((round(40+tile*.7),round(40+tile*.7),round(40+tile*2.7),round(40+tile*2.7)))
    canvas.alpha_composite(crop.resize((480,480),Image.Resampling.LANCZOS),(x,927))
draw.text((25,1435),'Mevcut DarkNavy sprite’ları ve sahne ölçüleriyle statik yerleşim. Ortak köşe kodu değiştirilmedi.',font=r.font(20),fill=r.MUTED)
draw.text((25,1470),'Unity ekran görüntüsü değildir; oyun içindeki son piksel birleşimi ayrıca doğrulanmadı.',font=r.font(19),fill=r.MUTED)
canvas.convert('RGB').save(ROOT/'Docs/LevelDesign/Level181-Capraz-Kose-Kontrolu.png')
print('Existing corner sprite placement inspected: v3 8 corner-only nodes; v4 0. Renderer/scene untouched.')
