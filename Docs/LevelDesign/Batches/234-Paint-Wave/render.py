"""Real-sprite opening/cutaway; only one authored tile, other colors illustrative."""
from author import *
from PIL import Image, ImageDraw
import random, textwrap

spec=importlib.util.spec_from_file_location('paint_wave_art',HERE.parent/'173-180/render_previews.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
D=json.loads((HERE/'design.json').read_text())
# Later authorized move tuning keeps the layered layout but changes its budget.
D['moves']=int(re.search(r'^  moves: (\d+)$',ASSET.read_text(),re.M)[1])


def paint_box(im,box):
    art.paste(im,art.oid_art(63),box)
    bucket=Image.open(A/'Resources/PaintCanBox/PaintCan.png').convert('RGBA')
    x,y,bw,bh=box
    # Same normalized4-3-2geometry as PaintCanBoxView.Build.
    w=.19;h=w*bucket.height/bucket.width;step=h*.78;total=2*step+h
    if total>.66:
        scale=.66/total;w*=scale;h*=scale;step*=scale
    for row,n in enumerate([4,3,2]):
        for k in range(n):
            cx=.5+(k-(n-1)*.5)*w;by=.152+row*step
            art.paste(im,bucket,(x+(cx-w/2)*bw,y+(1-by-h)*bh,w*bw,h*bh))


def board(im,d,xy,cell,removed=()):
    w=d['width'];ls=layers(d,removed);dr=ImageDraw.Draw(im);slots,reached=feed(d,removed)
    pins=d['data'].get('pinnedTileTypes',[0]*len(ls))
    filled=reached|{i for i,v in enumerate(pins)if v and i in slots}
    for i,a in enumerate(d['data']['cells']):
        if a:
            x,y=xy[0]+i%w*cell,xy[1]+i//w*cell
            dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(3,cell//10),fill='#354c65',outline='#88a2bd',width=2)
            if ls[i] and ls[i][-1][0]==25:art.paste(im,art.oid_art(25),(x,y,cell,cell))
    rng=random.Random(d['level']);colors={}
    for i in sorted(filled):
        choices=[pins[i]-1]if pins[i]else list(range(4))
        if not pins[i]:rng.shuffle(choices)
        for color in choices:
            if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==color)and not(i//w>=2 and colors.get(i-w)==colors.get(i-w*2)==color)and not(i%w and i//w and all(colors.get(j)==color for j in[i-1,i-w,i-w-1])):break
        colors[i]=color;x,y=xy[0]+i%w*cell,xy[1]+i//w*cell
        art.paste(im,art.TILES[color],(x,y,cell,cell),cell*.07)
        if pins[i]:dr.ellipse((x+cell*.04,y+cell*.04,x+cell*.15,y+cell*.15),fill='#ffcf75')
    seen=set()
    for i,plane in enumerate(ls):
        if not plane:continue
        o,origin=plane[-1]
        if o==25:continue
        if(o,origin)in seen:continue
        seen.add((o,origin));fw,fh=shared.footprint(o)
        box=(xy[0]+origin%w*cell,xy[1]+origin//w*cell,fw*cell,fh*cell)
        if o==63:paint_box(im,box)
        else:art.paste(im,art.oid_art(o),box,cell*.02)


def paragraph(dr,text,x,y,width=45,size=20,color='#e7edf6'):
    for line in textwrap.wrap(text,width):
        dr.text((x,y),line,font=art.font(size),fill=color);y+=size+8
    return y


def render():
    im=Image.new('RGBA',(1220,1500),'#172536');dr=ImageDraw.Draw(im)
    dr.text((32,24),'234 · BOYA DALGASI',font=art.font(36,True),fill='#ffcf75')
    dr.text((32,80),f'Normal · {D["moves"]} hamle · 8×11 /74 aktif hücre · 4 renk',font=art.font(23),fill='#e7edf6')
    dr.text((32,134),'AÇILIŞ',font=art.font(23,True),fill='#ffcf75');board(im,D,(32,177),65)
    dr.text((675,134),'İLK ÖRTÜLER KALKINCA',font=art.font(22,True),fill='#ffcf75');board(im,D,(675,177),49,D['previewRemoved'])
    y=764
    for text in ['Sol üst: iki renkli sandık → iki boya kasası. Her kasada dokuz kova.','Alt sol ve sağ omuz: oyma taşı → iki aşamalı plastik → iki vuruşlu çamur.','Altın nokta: yalnız gölgedeki tek yuvaya yerleştirilmiş başlangıç taşı. Diğer 49 yuva otomatik dolar.']:
        y=paragraph(dr,text,675,y,43,19)+12
    dr.text((32,1080),'HEDEFLER',font=art.font(23,True),fill='#ffcf75')
    for n,(goal,label)in enumerate(zip(D['goals'],['Renkli sandık','Boya kasası','Oyma taşı','İki aşamalı plastik','Çamur'])):
        x=32+n*236
        art.paste(im,art.sprite(art.ref(goal['iconOverride'])),(x,1125,65,65))
        dr.text((x+78,1141),str(goal['amount']),font=art.font(27,True),fill='#e7edf6')
        dr.text((x,1202),label,font=art.font(19),fill='#e7edf6')
    paragraph(dr,f'Güncel bütçe {D["moves"]} hamle. Açılış üst kapakları ilerletse de altta plastik, çamur ve boya işi devam eder.',32,1280,105,21)
    dr.text((32,1395),'Gerçek sprite’lar · Serbest renkler temsili · Kullanıcının beğendiği 74 hücrelik grid aynı.',font=art.font(19),fill='#a9bdd1')
    dr.text((32,1436),'Kesitler oynanmış an değildir. Yeni katmanlı sürümün manuel testi kullanıcıda.',font=art.font(19),fill='#a9bdd1')
    im.convert('RGB').save(ROOT/'Docs/LevelDesign/Level234-Boya-Dalgasi.png')
    opening=Image.new('RGBA',(600,820),'#172536');board(opening,D,(40,45),65)
    opening.convert('RGB').save(HERE/'Opening.png')
    sheet=Image.new('RGBA',(1500,900),'#172536');dr=ImageDraw.Draw(sheet)
    for n,(title,removed)in enumerate([('AÇILIŞ',[]),('OYMA / KAPAKLAR KALKINCA',[21,28]),('PLASTİK DE KALKINCA',[21,28,40])]):
        x=28+n*495;dr.text((x,24),title,font=art.font(22,True),fill='#ffcf75');board(sheet,D,(x,82),49,removed)
    dr.text((28,705),'Aynı gridde gerçek katman kesitleri: oyma → plastik → çamur. Soldaki kapak → boya zinciri ayrı sürer.',font=art.font(21),fill='#e7edf6')
    dr.text((28,814),'Düşüş/oynanış simülasyonu yok · Normal renkler temsili · Altın nokta gerçek başlangıç taşıdır.',font=art.font(20),fill='#a9bdd1')
    sheet.convert('RGB').save(HERE/'Layers.png')
    print('Rendered real project sprites and explicit shadow pin.')


if __name__=='__main__':render()
