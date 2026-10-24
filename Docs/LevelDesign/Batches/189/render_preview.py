"""Current-sprite 189 preview, including channel/lily and lantern/flower layers."""
from pathlib import Path
import importlib.util,json,random,re
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];DOCS=ROOT/'Docs/LevelDesign'
spec=importlib.util.spec_from_file_location('art',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
d=json.loads((HERE/'design.json').read_text());w=d['width']
skin=(ROOT/'Assets/_Project/Resources/WaterChannelSkin.asset').read_text()
def sprite_list(key):
    part=re.search(r'^  '+key+r':\n((?:  - .+\n)+)',skin,re.M)[1]
    return [r.sprite(r.ref(line)) for line in part.splitlines()]
CLOSED,OPEN,WATER=[sprite_list(k) for k in ('closed','open','water')]
SOURCE=r.sprite(r.ref(re.search(r'^  source: (.*)$',skin,re.M)[1]));KEYS=['H','V','LB','LT','RB','RT']
def side(a,b):
    dx,dy=b%w-a%w,b//w-a//w
    return 'L' if dx<0 else 'R' if dx>0 else 'T' if dy<0 else 'B'
def shape(a,b):
    edges={a,b}
    return 'H' if edges=={'L','R'} else 'V' if edges=={'T','B'} else ('L' if 'L' in edges else 'R')+('T' if 'T' in edges else 'B')
channels={};sources=set()
for entry in d['waterChannels']:
    path=entry['pathCellIndices'];sources.add(path[0]);channels[path[0]]='H'
    for k,i in enumerate(path[1:],1):channels[i]=shape(side(i,path[k-1]),side(i,path[k+1]) if k<len(path)-1 else 'L')

def wall_art(group,cell):
    coords={(i%w,i//w) for i in group['cells']};left=min(x for x,y in coords);top=min(y for x,y in coords)
    out=Image.new('RGBA',(cell*2,cell*2));half=cell//2
    for x,y in coords:
        for dx,dy in ((-1,-1),(1,-1),(-1,1),(1,1)):
            vertical=(x,y+dy) in coords;horizontal=(x+dx,y) in coords
            corner=('T' if dy<0 else 'B')+('L' if dx<0 else 'R')
            name='Outer'+corner if not vertical and not horizontal else 'Edge'+('Left' if dx<0 else 'Right') if vertical and not horizontal else 'Edge'+('Top' if dy<0 else 'Bottom') if horizontal and not vertical else 'Inner'
            art=r.resource('Wall','WallQ_'+name).resize((half,half),Image.Resampling.LANCZOS)
            out.alpha_composite(art,((x-left)*cell+(half if dx>0 else 0),(y-top)*cell+(half if dy>0 else 0)))
        variant=1+(y*w+x)%5
        p=ROOT/f'Assets/_Project/Resources/Wall/WallDetail_Kabartma{variant}.png'
        if p.exists():r.paste(out,Image.open(p).convert('RGBA'),((x-left)*cell,(y-top)*cell,cell,cell),cell*.08)
    return out

def board(im,xy,cell,mode='opening'):
    dr=ImageDraw.Draw(im);rng=random.Random(189);colors={}
    # Backgrounds first: a later cell must not paint over a 1x2 / 2x1 launcher.
    for i,active in enumerate(d['data']['cells']):
        if not active:continue
        x,y=xy[0]+i%w*cell,xy[1]+i//w*cell
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//8),fill='#777eb5',outline='#989dce')
    for i,active in enumerate(d['data']['cells']):
        if not active:continue
        x,y=xy[0]+i%w*cell,xy[1]+i//w*cell;oid=d['data']['obstacles'][i]
        if i in channels:
            k=KEYS.index(channels[i]);r.paste(im,OPEN[k] if mode=='channel' or i in sources else CLOSED[k],(x,y,cell,cell))
            if mode=='channel' or i in sources:r.paste(im,WATER[k],(x,y,cell,cell))
        if i in sources:
            r.paste(im,SOURCE,(x,y,cell,cell));continue
        if mode=='channel':continue
        if oid in (0,69):
            choices=list(range(4));rng.shuffle(choices)
            c=next((c for c in choices if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==c) and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==c)),choices[0])
            colors[i]=c;r.paste(im,r.TILES[c],(x,y,cell,cell),cell*.04)
        if oid==69:
            r.paste(im,r.oid_art(42),(x,y,cell,cell))
            grow=.06*cell;r.paste(im,r.oid_art(69),(x-grow,y-grow,cell+2*grow,cell+2*grow))
        elif oid not in (0,55):
            if d['data']['obstacleOrigins'][i]!=i:continue
            m=re.search(r'^    size: \{x: (\d+), y: (\d+)\}$',r.DEFS[oid],re.M);bw,bh=map(int,m.groups())
            r.paste(im,r.oid_art(oid),(x,y,bw*cell,bh*cell),cell*.015)
    if mode!='channel':
        for group in d['walls']:
            i=group['origin'];im.alpha_composite(wall_art(group,cell),(xy[0]+i%w*cell,xy[1]+i//w*cell))
        if mode=='opening':
            for entry in d['stacks']:
                i=entry['originCellIndex'];x,y=xy[0]+i%w*cell,xy[1]+i//w*cell
                r.paste(im,r.oid_art(58),(x,y,cell,cell))

im=Image.new('RGBA',(1440,1220),r.BG);dr=ImageDraw.Draw(im)
dr.text((30,25),'189 / İkiz Gölet Kalesi',font=r.font(38,True),fill=r.GOLD)
dr.text((31,80),'9 × 11 · 32 hamle · 4 renk · SUPER HARD',font=r.font(25,True),fill='#ff888c')
dr.text((31,126),'İki kuş penceresi · Nilüfer altında su yolları · Dört bağımsız duvar parçası',font=r.font(21),fill=r.MUTED)
board(im,(30,187),74)
x,y=760,185;dr.text((x,y),'HEDEFLER',font=r.font(22,True),fill=r.GOLD);y+=37
for art,label in [(SOURCE,'2 su kanalı'),(r.oid_art(69),'14 nilüfer'),(wall_art(d['walls'][0],28),'4 ayrı duvar parçası'),(r.sprite(r.ref(re.search(r'^    goalIconSprite: (.*)$',r.DEFS[75],re.M)[1])),'16 fener')]:
    r.paste(im,art,(x,y,56,56));dr.text((x+73,y+15),label,font=r.font(24,True),fill=r.INK);y+=69
y+=5;dr.text((x,y),'YARDIMCI ZİNCİR',font=r.font(21,True),fill=r.GOLD);y+=35
y=r.paragraph(dr,x,y,'İki 2×2 cepte toplam 8 EggBird. Alt soldaki yatay kartuş, sağ alttaki dik kartuşa bakıyor.',width=48,size=20);y+=18
y=r.paragraph(dr,x,y,'Dikey atış sağdaki çiçekli fener sütununa uzanır. Duvarlar tek blok değil: dört ayrı 2×2 parça.',width=48,size=20);y+=20
dr.text((x,y),'NİLÜFERİN ALTINDA',font=r.font(20,True),fill=r.GOLD)
dr.text((x+325,y),'ÇİÇEKLER AÇILINCA',font=r.font(20,True),fill=r.GOLD);y+=38
small=Image.new('RGBA',(9*32,11*32),r.BG);board(small,(0,0),32,'channel')
im.alpha_composite(small.crop((0,32,3*32,10*32)),(x,y))
hidden=Image.new('RGBA',(9*32,11*32),r.BG);board(hidden,(0,0),32,'under')
im.alpha_composite(hidden.crop((7*32,32,9*32,9*32)),(x+366,y))
dr.multiline_text((x+123,y+68),'2 kaynak\n14 kanal hücresi\n14 nilüfer örtüsü',font=r.font(17),fill=r.INK,spacing=8)
r.paragraph(dr,x+458,y+50,'Üstte çiçekli çim; altta 16 fener.',width=16,size=18)
dr.text((31,1053),'96 aktif hücre · 2 kaynak · İki 2×2 EggBird cebi · Hazır special ve sabit renk yok',font=r.font(20),fill=r.INK)
dr.text((31,1095),'Kanal haritasında örtüler gizli, su temsili. EggBird ve fırlatıcılar yardımcı; doğrudan hedef değiller.',font=r.font(18),fill=r.MUTED)
dr.text((31,1133),'Güncel proje sprite’larıyla statik önizleme; normal taş renkleri temsili. Önceki levellar korundu.',font=r.font(18),fill=r.MUTED)
dr.text((31,1171),'32 hamle ilk denge ayarı; Unity Play/build, bot/cascade simülasyonu ve ölçülmüş geçiş oranı yok.',font=r.font(18),fill=r.MUTED)
im.convert('RGB').save(DOCS/'Level189-Ikiz-Golet-Kalesi.png')
print('Rendered 189 opening, channel map, and lantern layer.')
