from pathlib import Path
import json
import re
import random
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[4]
BATCH = Path(__file__).resolve().parent
OUT = ROOT / 'Docs/LevelDesign'
DESIGNS = json.loads((BATCH/'designs.json').read_text())
VALIDATION = {d['level']:d for d in json.loads((BATCH/'validation.json').read_text())['levels']}
NAMES = {e['level']:e['name'] for e in json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())['entries']}
for d in DESIGNS: d['title'] = NAMES[d['n']]
LIB = (ROOT / 'Assets/_Project/Settings/ObstacleLibrary.asset').read_text()
DEFS = {int(m[1]): m[2] for m in re.finditer(r'^  - id: (\d+)\n(.*?)(?=^  - id:|\Z)', LIB, re.M | re.S)}
GUIDS = {}
for path in (ROOT / 'Assets/_Project').rglob('*.meta'):
    text = path.read_text(errors='replace')
    m = re.search(r'^guid: (\w+)$', text, re.M)
    if m:
        GUIDS[m[1]] = (Path(str(path)[:-5]), text)
CACHE = {}
BG, INK, MUTED, GOLD = '#19283a', '#e7edf6', '#a9bdd1', '#ffcf75'
FONT = '/System/Library/Fonts/Supplemental/Arial.ttf'
BOLD = '/System/Library/Fonts/Supplemental/Arial Bold.ttf'

def font(size, bold=False):
    return ImageFont.truetype(BOLD if bold else FONT, size)

def sprite(ref):
    key = ref['guid'], int(ref['fileID'])
    if key not in CACHE:
        path, meta = GUIDS[key[0]]
        im = Image.open(path).convert('RGBA')
        if re.search(r'^  spriteMode: 2$', meta, re.M):
            match = None
            for block in re.split(r'(?=^    - serializedVersion:)', meta, flags=re.M):
                if re.search(r'^      internalID: '+str(key[1])+r'$', block, re.M):
                    match = block
                    break
            assert match, key
            rect = re.search(r'      rect:\n        serializedVersion: \d+\n        x: ([\d.]+)\n        y: ([\d.]+)\n        width: ([\d.]+)\n        height: ([\d.]+)', match)
            x,y,w,h = [int(float(v)) for v in rect.groups()]
            im = im.crop((x,im.height-y-h,x+w,im.height-y))
        CACHE[key] = im
    return CACHE[key]

def ref(text):
    m = re.search(r'fileID: (-?\d+), guid: (\w+)', text)
    assert m, text
    return dict(fileID=int(m[1]), guid=m[2])

def obstacle(oid):
    return sprite(ref(DEFS[oid].split('sprite: ',1)[1]))

TILE_LIBRARY = (ROOT / 'Assets/_Project/Resources/TileIconLibrary_Main.asset').read_text()
TILES = [sprite(ref(re.search(r'^  '+name+r': (.*)$', TILE_LIBRARY, re.M)[1]))
         for name in ('gear','core','bolt','plate')]

def paste(im, asset, box, inset=0):
    x,y,w,h = box
    scaled = ImageOps.contain(asset, (max(1,int(w-2*inset)),max(1,int(h-2*inset))), Image.Resampling.LANCZOS)
    im.alpha_composite(scaled,(int(x+(w-scaled.width)/2),int(y+(h-scaled.height)/2)))


from textwrap import wrap

def resource(folder,name):
    return Image.open(ROOT/'Assets/_Project/Resources'/folder/(name+'.png')).convert('RGBA')

RHODES={}
def rhodes(stage=1):
    if stage not in RHODES:
        im=resource('RhodesStatue',f'S{stage}_Base')
        parts=[]
        if stage<=3:parts.append(('Sword',[(.7392,.1970,.2193,.3884),(.7392,.1970,.2193,.3907),(.7392,.2169,.2193,.3804)][stage-1]))
        if stage<=2:parts.append(('Shield',[(.0598,.3453,.4394,.4458),(.0582,.3493,.4386,.4450)][stage-1]))
        for name,(x,y,w,h) in parts:
            art=resource('RhodesStatue',f'S{stage}_{name}').resize((round(w*im.width),round(h*im.height)),Image.Resampling.LANCZOS)
            im.alpha_composite(art,(round(x*im.width),round(y*im.height)))
        RHODES[stage]=im
    return RHODES[stage]

def tarp_art(entry):
    im=Image.new('RGBA',(512,512))
    color=['Red','Yellow','Blue','Green'][entry['color']]
    for name in ('Background','Stone_1','Tarp','Frame','Tag_'+color):paste(im,resource('TarpStatue',name),(0,0,512,512))
    ImageDraw.Draw(im).text((256,512*.4365),str(entry['count']),font=font(63,True),fill='white',stroke_width=3,stroke_fill='#3d1524',anchor='mm')
    return im

prefab=(ROOT/'Assets/_Project/Prefabs/FX/TubeViewFX/TubeView.prefab').read_text()
TUBE_PARTS={k:sprite(ref(re.search(r'^  '+k+r': (.*)$',prefab,re.M)[1])) for k in ('bodySprite','baseSprite','openEndCapSprite','sawSprite')}
def tube_art(length,cell):
    im=Image.new('RGBA',(cell,cell*length))
    for y in range(1,length-1):paste(im,TUBE_PARTS['bodySprite'],(0,y*cell,cell,cell))
    paste(im,TUBE_PARTS['openEndCapSprite'],(0,0,cell,cell))
    paste(im,TUBE_PARTS['baseSprite'],(0,(length-1)*cell,cell,cell))
    paste(im,TUBE_PARTS['sawSprite'],(cell*.2,(length-.96)*cell,cell*.6,cell*.6))
    return im

def metal_art(cells):
    coords={(i%9,i//9) for i in cells};left=min(x for x,y in coords);top=min(y for x,y in coords)
    width=max(x for x,y in coords)-left+1;height=max(y for x,y in coords)-top+1
    im=Image.new('RGBA',(width*256,height*256))
    for x,y in coords:
        for dx,dy in ((-1,-1),(1,-1),(-1,1),(1,1)):
            v=(x,y+dy) in coords;h=(x+dx,y) in coords
            corner=('T' if dy<0 else 'B')+('L' if dx<0 else 'R')
            name='Outer'+corner if not v and not h else 'Edge'+('Left' if dx<0 else 'Right') if v and not h else 'Edge'+('Top' if dy<0 else 'Bottom') if h and not v else 'Inner'
            art=resource('Wall','MetalWallQ_'+name).resize((128,128),Image.Resampling.LANCZOS)
            im.alpha_composite(art,((x-left)*256+(128 if dx>0 else 0),(y-top)*256+(128 if dy>0 else 0)))
    return im,width,height

SPECIALS={k:sprite(ref(re.search(r'^  '+key+r': (.*)$',TILE_LIBRARY,re.M)[1])) for k,key in ((1,'lineH'),(2,'lineV'),(3,'patchBotAlone'),(4,'pulseCore'),(5,'overrideRed'))}
ART_CACHE={}
def oid_art(oid,remaining_hits=None):
    key=oid if remaining_hits is None else (oid,remaining_hits)
    if key not in ART_CACHE:
        if remaining_hits is None:ART_CACHE[key]=rhodes() if oid==70 else obstacle(oid)
        else:
            max_hits=int(re.search(r'^    hits: (\d+)$',DEFS[oid],re.M)[1])
            stages=re.findall(r'^    - sprite: (.*)$',DEFS[oid],re.M)
            stage=max(0,min(len(stages)-1,max_hits-max(1,min(max_hits,remaining_hits))))
            ART_CACHE[key]=sprite(ref(stages[stage]))
    return ART_CACHE[key]

def board(im,d,origin,cell,base=False):
    data=d['data'];draw=ImageDraw.Draw(im);rng=random.Random(d['n']);colors={}
    top=data['obstacles'][:];origins=data['obstacleOrigins'][:]
    if not base:
        for s in d['stacks']:top[s['originCellIndex']]=s['obstacleId']
    tarps={s['originCellIndex']:s for s in d['tarps']}
    initial_hits={s['originCellIndex']:s['initialRemainingHits'] for s in d['stacks'] if s.get('initialRemainingHits',0)>0} if not base else {}
    for i,active in enumerate(data['cells']):
        if not active:continue
        x,y=origin[0]+i%9*cell,origin[1]+i//9*cell
        exposed=top[i] if top[i] in (14,25,46) else 0
        fill={14:'#536e39',25:'#99592f',46:'#873fba'}.get(exposed,'#485b79')
        draw.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//9),fill=fill,outline='#7587a2')
        if exposed:paste(im,oid_art(exposed),(x,y,cell,cell))
    for i,oid in enumerate(top):
        if not data['cells'][i]:continue
        x,y=origin[0]+i%9*cell,origin[1]+i//9*cell
        if oid in (0,14,25,46,69):
            if base:
                if oid==0:draw.ellipse((x+cell*.44,y+cell*.44,x+cell*.56,y+cell*.56),fill='#aabbd0')
                if oid!=69:continue
            else:
                special=data['pinnedSpecialTypes'][i]
                if special:
                    paste(im,SPECIALS[special],(x,y,cell,cell),cell*.03)
                    draw.rounded_rectangle((x+2,y+2,x+cell-2,y+cell-2),radius=max(3,cell//9),outline=GOLD,width=2)
                    continue
                choices=list(range(4));rng.shuffle(choices)
                if data['pinnedTileTypes'][i]:choices=[data['pinnedTileTypes'][i]-1]
                for c in choices:
                    horizontal=i%9>=2 and colors.get(i-1)==colors.get(i-2)==c
                    vertical=i//9>=2 and colors.get(i-9)==colors.get(i-18)==c
                    square=i%9>0 and i//9>0 and all(colors.get(j)==c for j in (i-1,i-9,i-10))
                    if not(horizontal or vertical or square):break
                colors[i]=c;paste(im,TILES[c],(x,y,cell,cell),cell*.05)
            if oid==69:
                paste(im,oid_art(42),(x,y,cell,cell))
                paste(im,oid_art(69),(x,y,cell,cell))
            continue
        if oid in (26,56):continue
        if origins[i]!=i:continue
        match=re.search(r'^    size: \{x: (\d+), y: (\d+)\}$',DEFS[oid],re.M)
        w,h=map(int,match.groups())
        if oid==68:
            entry=tarps[i];w=h=entry['size'];art=tarp_art(entry)
        else:art=oid_art(oid,initial_hits.get(i))
        paste(im,art,(x,y,w*cell,h*cell),max(0,int(cell*.015)))
    for t in d['tubes']:
        i=t['originCellIndex'];x=origin[0]+i%9*cell;y=origin[1]+(i//9-t['length']+1)*cell
        im.alpha_composite(tube_art(t['length'],cell),(int(x),int(y)))
    for wall in d['walls']:
        art,w,h=metal_art(wall['cells']);i=wall['origin'];x=origin[0]+i%9*cell;y=origin[1]+i//9*cell
        paste(im,art,(x,y,w*cell,h*cell))

LABELS={7:'Mor plastik',14:'Yağ (dinamik)',25:'Çamur',26:'Boru',27:'Dolap',30:'Porselen',34:'Kargo',39:'Special-only pusula',40:'İki aşamalı fener',46:'Jel kaplama',56:'Metal parçası',68:'Brandalı heykel',69:'Nilüfer',70:'Rodos heykeli'}
def goal_lines(d):
    lines=[]
    for g in d['goals']:
        name=LABELS.get(g['obstacleId'],'Engel') if g['targetType']==1 else 'Anahtar' if g['targetType']==0 else 'Boss hasarı' if g['collectibleId']==2 else 'Enerji'
        lines.append(f'{name}: {g["amount"]}')
    return lines

def paragraph(draw,x,y,text,width=43,size=17,color=INK):
    for line in wrap(text,width):draw.text((x,y),line,font=font(size),fill=color);y+=25
    return y

def render():
    for d in DESIGNS:
        im=Image.new('RGBA',(1100,1130),BG);draw=ImageDraw.Draw(im)
        title=f'{d["n"]} / {d["title"]}'
        draw.text((34,25),title,font=font(35,True),fill=GOLD)
        draw.text((35,79),f'9 × {d["h"]} · {d["moves"]} hamle · 4 renk · {d["difficulty"]}',font=font(23),fill=INK)
        draw.text((35,119),'Altın çerçeve: hazır special · Normal taş renkleri temsili · İlk denge ayarı',font=font(17),fill=MUTED)
        board(im,d,(34,172),64)
        sx,sy=650,174
        for heading,lines in [('HEDEFLER',goal_lines(d)),('OYUN FİKRİ',[d['decision']]),('AÇILMA AKIŞI',d['flow'])]:
            draw.text((sx,sy),heading,font=font(19,True),fill=GOLD);sy+=33
            for line in lines:sy=paragraph(draw,sx,sy,line)
            sy+=19
        if d['stacks']:
            draw.text((sx,sy),'ÖRTÜLER AÇILINCA: TABAN',font=font(16,True),fill=GOLD)
            board(im,d,(sx,sy+33),18,base=True)
        active=sum(d['data']['cells']);normal=VALIDATION[d['n']]['openingTileCells']
        held=sum(1 for s in d['stacks'] if s.get('initialRemainingHits')==1 and s['obstacleId'] in (71,72,73,74))
        opening=f'{normal} açık + {held} örtü altında tutulan taş hücresi' if held else f'{normal} başlangıç taş hücresi (dolum kontrolü geçti)'
        draw.text((35,923),f'{active} aktif hücre · {9*d["h"]-active} hole · {opening}.',font=font(18),fill=INK)
        draw.text((35,961),'Tasarım taslağı; kullanıcı incelemesini bekliyor. Önceki levellar korundu.',font=font(18),fill=INK)
        draw.text((35,1011),'Mevcut sprite’larla yerleşim önizlemesi; oyun ekran görüntüsü değildir.',font=font(17),fill=MUTED)
        draw.text((35,1044),'Geçiş oranı ve cihaz performansı ölçülmedi; hamleler ilk denge ayarıdır.',font=font(17),fill=MUTED)
        draw.text((35,1077),'Unity Play/build, bot veya kaskat simülasyonu çalıştırılmadı.',font=font(17),fill=MUTED)
        path=OUT/f'Level{d["n"]}-{d["slug"]}.png';im.convert('RGB').save(path);print(path.name)
    contact=Image.new('RGBA',(1500,1190),BG);draw=ImageDraw.Draw(contact)
    draw.text((30,20),'173–180 / YENİ TASARIMLAR',font=font(32,True),fill=GOLD)
    draw.text((30,65),'175 Hard · 179 Super Hard · 180 Boss Duel · Geçiş oranları ölçülmedi',font=font(20),fill=MUTED)
    for j,d in enumerate(DESIGNS):
        x=30+(j%4)*370;y=119+(j//4)*515
        draw.text((x,y),f'{d["n"]} · {d["title"]}',font=font(23,True),fill=GOLD)
        draw.text((x,y+33),f'{d["moves"]} hamle · {d["difficulty"]}',font=font(18),fill=INK)
        board(contact,d,(x,y+73),34)
        notes=' · '.join(goal_lines(d))
        paragraph(draw,x,y+458,notes,width=39,size=15,color=MUTED)
    draw.text((30,1160),'Yerleşim şemaları; taş renkleri temsili. Onaylı 171 ve mevcut 172 değişmedi.',font=font(17),fill=MUTED)
    contact.convert('RGB').save(OUT/'Levels173-180-Onizleme.png')

if __name__=='__main__':render()
