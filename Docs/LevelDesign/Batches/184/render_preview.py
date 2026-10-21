"""Current sprite composition with PaintCanBoxView's 4-3-2 can geometry."""
from pathlib import Path
import importlib.util,json,re,random
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];OUT=ROOT/'Docs/LevelDesign'
spec=importlib.util.spec_from_file_location('p',HERE.parent/'173-180/render_previews.py');r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
d=json.loads((HERE/'design.json').read_text());art=ROOT/'Assets/_Project/Art/UI/Obstacles/WaterChannel'
def load(name):return Image.open(art/(name+'.png')).convert('RGBA')
def paint_box():
    empty=re.search(r'^    auxiliarySprites:\n    - (.*)$',r.DEFS[63],re.M)
    assert empty,'Use the same empty frame as GridSpawner.AttachPaintCanBoxView'
    im=r.sprite(r.ref(empty[1])).copy();can=r.resource('PaintCanBox','PaintCan');aspect=can.height/can.width
    w=.19;h=w*aspect;step=h*.78;total=2*step+h
    if total>.66:w*=.66/total;h*=.66/total;step*=.66/total
    for row,n in enumerate((4,3,2)):
        for k in range(n):
            cx=.5+(k-(n-1)*.5)*w;by=.152+row*step
            r.paste(im,can,((cx-w*.5)*im.width,(1-by-h)*im.height,w*im.width,h*im.height))
    return im
boxart=paint_box()
def side(a,b):return 'L' if b%9<a%9 else 'R' if b%9>a%9 else 'T' if b//9<a//9 else 'B'
def shape(a,b):
    ss={a,b}
    return 'H' if ss=={'L','R'} else 'V' if ss=={'T','B'} else ('L' if 'L' in ss else 'R')+('T' if 'T' in ss else 'B')
shapes={};sources=set()
for entry in d['waterChannels']:
    p=entry['pathCellIndices'];sources.add(p[0]);shapes[p[0]]='V'
    for k,i in enumerate(p[1:],1):shapes[i]=shape(side(i,p[k-1]),side(i,p[k+1]) if k<len(p)-1 else 'B')
def board(im,xy,cell,map_only=False):
    dr=ImageDraw.Draw(im);rng=random.Random(184);colors={}
    for i,active in enumerate(d['data']['cells']):
        if not active:continue
        x,y=xy[0]+i%9*cell,xy[1]+i//9*cell;o=d['data']['obstacles'][i]
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//9),fill='#485b79',outline='#7587a2')
        if i in shapes:
            key=shapes[i];r.paste(im,load('Channel_'+key+('' if map_only or i in sources else '_Closed')),(x,y,cell,cell))
            if map_only or i in sources:r.paste(im,load('Water_'+key),(x,y,cell,cell))
        if i in sources:r.paste(im,load('ChannelSource'),(x,y,cell,cell));continue
        if o:
            if d['data']['obstacleOrigins'][i]==i:r.paste(im,boxart if o==63 else r.oid_art(o),(x,y,cell*(2 if o==63 else 1),cell*(2 if o==63 else 1)))
        elif not map_only:
            choices=list(range(4));rng.shuffle(choices)
            for c in choices:
                if not(i%9>=2 and colors.get(i-1)==colors.get(i-2)==c or i//9>=2 and colors.get(i-9)==colors.get(i-18)==c):break
            colors[i]=c;r.paste(im,r.TILES[c],(x,y,cell,cell),cell*.05)
        elif i not in shapes:dr.ellipse((x+cell*.46,y+cell*.46,x+cell*.54,y+cell*.54),fill='#b2bfd1')
    # Reapply the multi-cell illustrations over the complete cell background.
    for origin in (38,40,56,58):r.paste(im,boxart,(xy[0]+origin%9*cell,xy[1]+origin//9*cell,cell*2,cell*2))
    if map_only:
        for entry in d['waterChannels']:
            i=entry['pathCellIndices'][-1];x=xy[0]+(i%9+.5)*cell;y=xy[1]+11*cell
            dr.line((x,y,x,y+14),fill='#6bddeb',width=3);dr.polygon(((x-5,y+10),(x+5,y+10),(x,y+19)),fill='#6bddeb')
im=Image.new('RGBA',(1200,1120),r.BG);dr=ImageDraw.Draw(im)
dr.text((32,25),'184 / Boya Terasları',font=r.font(40,True),fill=r.GOLD)
dr.text((33,82),'9 × 11 · 28 hamle · 4 renk · Normal',font=r.font(24),fill=r.INK)
dr.text((33,122),'Bağlı boya terası · İki bağımsız su yolu · Special isteyen pusula şeridi',font=r.font(20),fill=r.MUTED)
board(im,(32,175),66)
x,y=688,175;dr.text((x,y),'HEDEFLER',font=r.font(21,True),fill=r.GOLD);y+=38
for image,label in ((boxart,'4 boya kasası'),(load('ChannelSource'),'2 su kanalı'),(r.oid_art(39),'8 pusula · yalnız special')):
    r.paste(im,image,(x,y,54,54));dr.text((x+69,y+15),label,font=r.font(22,True),fill=r.INK);y+=68
y+=12;dr.text((x,y),'OYUN FİKRİ',font=r.font(20,True),fill=r.GOLD);y+=34
y=r.paragraph(dr,x,y,d['decision'],width=39,size=19);y+=20
dr.text((x,y),'SU YOLLARI',font=r.font(20,True),fill=r.GOLD);y+=34
board(im,(x,y),27,True)
r.paragraph(dr,x+268,y+15,'2 kaynak\n23 hücre\n2 alt çıkış',width=18,size=18)
r.paragraph(dr,x+268,y+135,'Açılan kanal normal oynanır kalır.',width=17,size=17)
y+=337
y=r.paragraph(dr,x,y,'Haritada taşlar gizli, su temsili. Kaynağa bağlı açık yol tamamlanınca bir kanal sayılır.',width=42,size=17,color=r.MUTED)
y+=12;r.paragraph(dr,x,y,'Her kasada 9 kutu: normal komşu hit1, special hit2 kutu düşürür. Toplam 36 kutu.',width=42,size=18)
dr.text((33,953),'73 aktif · 26 hole · 2 geçirgen kaynak · 47 açık başlangıç taşı',font=r.font(21),fill=r.INK)
dr.text((33,1003),'Mevcut proje sprite’ları; normal renkler temsili. Oyun ekran görüntüsü değildir.',font=r.font(18),fill=r.MUTED)
dr.text((33,1042),'Statik yol/dolum/footprint kontrolü · Geçiş oranı ve kazanılabilirlik ölçülmedi',font=r.font(18),fill=r.MUTED)
dr.text((33,1078),'Önceki levellar ve kullanıcı değişiklikleri korundu; Unity Play/build veya simülasyon yok.',font=r.font(18),fill=r.MUTED)
im.convert('RGB').save(OUT/'Level184-Boya-Teraslari.png');print('Rendered 184.')
