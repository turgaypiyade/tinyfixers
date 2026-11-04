"""Current project sprites; illustrative free tile colours, honest authored pins/layers."""
from pathlib import Path
import copy,json,random,re,textwrap
from PIL import Image,ImageDraw
from common import ROOT,HERE,load_art,layer_model,feed
from design import designs
r=load_art();art=r.art
ds=json.loads((HERE/'designs.json').read_text())
skin=(ROOT/'Assets/_Project/Resources/WaterChannelSkin.asset').read_text()
def skin_list(key):
    section=re.search(r'^  '+key+r':\n((?:  - .*\n)+)',skin,re.M)[1]
    return [art.sprite(art.ref(ref)) for ref in re.findall(r'^  - (.*)$',section,re.M)]
closed=skin_list('closed');opened=skin_list('open');water=skin_list('water')
source=art.sprite(art.ref(re.search(r'^  source: (.*)$',skin,re.M)[1]))
shape_keys=['H','V','LB','LT','RB','RT']
def side(a,b,w):
    dx=b%w-a%w;dy=b//w-a//w
    return 'L' if dx<0 else 'R' if dx>0 else 'T' if dy<0 else 'B'
def shape(a,b):return 'H' if 'L' in(a,b) and 'R' in(a,b) else 'V' if 'T' in(a,b) and 'B' in(a,b) else ('L' if 'L' in(a,b) else 'R')+('T' if 'T' in(a,b) else 'B')
def paste(im,asset,box,inset=0,alpha=1):
    if alpha!=1:
        asset=asset.copy();asset.putalpha(asset.getchannel('A').point(lambda a:round(a*alpha)))
    art.paste(im,asset,box,inset)
def board(im,d,xy,cell,removed=(),cutaway=False):
    draw=ImageDraw.Draw(im);w,h=d['width'],d['height'];ls=layer_model(d,removed);slots,reached=feed(d,removed)
    pins=d['data']['pinnedTileTypes'];filled=reached|{i for i,v in enumerate(pins) if v and i in slots}
    sources={p['pathCellIndices'][0] for p in d['custom']['waterChannels']};channels={i for p in d['custom']['waterChannels'] for i in p['pathCellIndices']}
    def box(i):return xy[0]+i%w*cell,xy[1]+i//w*cell,cell,cell
    for i,c in enumerate(d['data']['cells']):
        if not c:continue
        x,y,_,_=box(i);draw.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(3,cell//10),fill='#354c65',outline='#65839c')
    for p in d['custom']['waterChannels']:
        path=p['pathCellIndices'];key='V' if side(path[0],path[1],w)=='B' else 'H'
        paste(im,opened[shape_keys.index(key)],box(path[0]));paste(im,water[shape_keys.index(key)],box(path[0]))
        for j,i in enumerate(path[1:],1):
            key=shape(side(i,path[j-1],w),side(i,path[j+1],w) if j+1<len(path) else 'B')
            paste(im,closed[shape_keys.index(key)],box(i))
    rng=random.Random(d['level']);colors={}
    for i in sorted(filled):
        choices=list(range(4));rng.shuffle(choices)
        if pins[i]:choices=[pins[i]-1]
        for color in choices:
            if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==color) and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==color):break
        colors[i]=color;paste(im,art.TILES[color],box(i),cell*.07,.20 if cutaway and i in channels else 1)
    for i in slots-filled:
        x,y,_,_=box(i);draw.ellipse((x+cell*.45,y+cell*.45,x+cell*.55,y+cell*.55),fill='#8398aa')
    # Draw solids from retained layer identity, then transparent grass/flower covers.
    drawn=set()
    for i,cell_layers in enumerate(ls):
        for o,origin in cell_layers:
            if o in (42,58,76):continue
            key=(o,origin)
            if key in drawn:continue
            drawn.add(key)
            region=[j for j,other in enumerate(ls) if key in other]
            left=min(j%w for j in region);top=min(j//w for j in region);fw=max(j%w for j in region)-left+1;fh=max(j//w for j in region)-top+1
            paste(im,art.oid_art(o),(xy[0]+left*cell,xy[1]+top*cell,fw*cell,fh*cell),cell*.02)
    for i,cell_layers in enumerate(ls):
        for o,origin in cell_layers:
            if o in (42,58):paste(im,art.oid_art(o),box(i))
    for i in sources:paste(im,source,box(i))
def paragraph(draw,text,xy,size=21,width=48,color='#e7edf6'):
    y=xy[1]
    for line in textwrap.wrap(text,width):draw.text((xy[0],y),line,font=art.font(size),fill=color);y+=size+8
    return y
LABELS={9:'Sarı plastik',11:'Mavi plastik',12:'Kırmızı plastik',13:'Yeşil plastik',21:'Renkli sandık',30:'Porselen',40:'İki aşamalı plastik',42:'Çim',58:'Çiçek',75:'Fener',76:'Su kanalı',83:'Un'}
def goal_label(g):return 'Boss HP' if g['targetType']==2 and g['collectibleId']==2 else 'Kuş' if g['targetType']==2 else LABELS[g['obstacleId']]
def render():
    out=HERE/'Previews';out.mkdir(exist_ok=True)
    limits=[]
    for d in ds:
        n=d['level'];im=Image.new('RGBA',(1360,1100),'#172536');draw=ImageDraw.Draw(im)
        kind='BOSS DUEL' if n==220 else 'SUPER HARD' if n==219 else 'HARD' if n==215 else 'NORMAL'
        draw.text((32,22),f'{n} · {d["name"].upper()}',font=art.font(36,True),fill='#ffcf75')
        draw.text((34,76),f'{kind} · {d["width"]}×{d["height"]} · {sum(d["data"]["cells"])} hücre · {d["moves"]} hamle · 4 renk',font=art.font(23),fill='#e7edf6')
        draw.text((34,124),'AÇILIŞ',font=art.font(23,True),fill='#ffcf75');board(im,d,(34,165),60)
        if d['custom']['waterChannels']:
            removed={58,42,11,12,30};cut=True;caption='KANAL / ÖRTÜ KESİTİ'
        elif n==219:removed={58,42,40};cut=False;caption='ÇİM / PLASTİK KALKINCA'
        elif n==214:removed={58,42};cut=False;caption='ÇİÇEK / ÇİM KALKINCA'
        elif n==215:removed={9,11,12,40};cut=False;caption='PANOLAR KALKINCA'
        else:removed={30,40,13};cut=False;caption='İLK ENGELLER KALKINCA'
        draw.text((654,124),caption,font=art.font(22,True),fill='#ffcf75');board(im,d,(654,165),38,removed,cut)
        draw.text((654,598),'Statik katman kesiti; oynanmış bir an değildir.',font=art.font(17),fill='#a9bdd1')
        if cut:draw.text((654,624),'Kanal üstündeki taşlar rotayı göstermek için saydam.',font=art.font(17),fill='#a9bdd1')
        y=660;y=paragraph(draw,d['idea'],(654,y),21,49)+12
        y=paragraph(draw,d['opening'],(654,y),20,50)+12
        paragraph(draw,d['after'],(654,y),20,50)
        draw.text((34,864),'HEDEFLER',font=art.font(24,True),fill='#ffcf75')
        for j,g in enumerate(d['goals']):
            x=34+j*260;o=78 if g['targetType']==2 and g['collectibleId']==3 else None if g['targetType']==2 else g['obstacleId']
            if o:paste(im,source if o==76 else art.oid_art(o),(x,908,56,56))
            draw.text((x+62,926),f'{g["amount"]} {goal_label(g)}',font=art.font(16,True),fill='#e7edf6')
        pin_count=sum(bool(p) for p in d['data']['pinnedTileTypes'])
        draw.text((34,1001),f'Güncel proje sprite’ları. Serbest taş renkleri temsili; {pin_count} gerçek başlangıç renk pini var.',font=art.font(18),fill='#a9bdd1')
        draw.text((34,1033),'Yerleşim / hedef / dolum kontrolleri statiktir. Oynanış dengesi ve cihaz performansı henüz ölçülmedi.',font=art.font(18),fill='#a9bdd1')
        im.convert('RGB').save(out/f'Level{n}.png');im.convert('RGB').save(ROOT/f'Docs/LevelDesign/Level{n}-{d["slug"]}.png')
    # Larger boards in a 4×2 contact sheet preserve readable silhouettes and sprite grouping.
    sheet=Image.new('RGBA',(1920,1540),'#172536');draw=ImageDraw.Draw(sheet)
    for j,d in enumerate(ds):
        x=24+(j%4)*480;y=24+(j//4)*760
        draw.text((x,y),f'{d["level"]} · {d["name"]}',font=art.font(24,True),fill='#ffcf75')
        draw.text((x,y+36),f'{d["moves"]} hamle'+(' · HARD' if d['level']==215 else ' · SUPER HARD' if d['level']==219 else ' · BOSS' if d['level']==220 else ''),font=art.font(19),fill='#e7edf6')
        board(sheet,d,(x,y+77),47)
        for k,g in enumerate(d['goals']):draw.text((x,y+618+k*22),f'{g["amount"]} {goal_label(g)}',font=art.font(16),fill='#cbd8e5')
    sheet.convert('RGB').save(HERE/'Levels213-220.png')
    print('Rendered eight opening/cutaway previews and 4×2 contact sheet with current sprites.')
if __name__=='__main__':render()
