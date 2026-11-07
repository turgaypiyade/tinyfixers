"""Real-sprite opening/cutaway; only one authored tile, other colors illustrative."""
from author import *
from PIL import Image, ImageDraw
import random, textwrap

spec=importlib.util.spec_from_file_location('paint_wave_art',HERE.parent/'173-180/render_previews.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
D=json.loads((HERE/'design.json').read_text())


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
    im=Image.new('RGBA',(1220,1300),'#172536');dr=ImageDraw.Draw(im)
    dr.text((32,24),'234 · BOYA DALGASI',font=art.font(36,True),fill='#ffcf75')
    dr.text((32,80),'Normal · 24 hamle · 8×11 /74 aktif hücre · 4 renk',font=art.font(23),fill='#e7edf6')
    dr.text((32,134),'AÇILIŞ',font=art.font(23,True),fill='#ffcf75');board(im,D,(32,177),65)
    dr.text((675,134),'KAPAKLAR KALKINCA',font=art.font(23,True),fill='#ffcf75');board(im,D,(675,177),49,[21])
    y=764
    for text in ['Sol kenar: iki renkli sandık → iki boya kasası. Her kasada dokuz kova.','Alt kenar: mavi ve kırmızı plastikler kendi renk eşleşmelerini ister.','Altın nokta: yalnız doğal dolum ulaşmayan yuvaya yerleştirilmiş gerçek başlangıç taşı.']:
        y=paragraph(dr,text,675,y,43,19)+12
    dr.text((32,953),'HEDEFLER',font=art.font(23,True),fill='#ffcf75')
    for n,(goal,label)in enumerate(zip(D['goals'],['Renkli sandık','Boya kasası','Mavi plastik','Kırmızı plastik'])):
        x=32+n*296
        art.paste(im,art.sprite(art.ref(goal['iconOverride'])),(x,997,65,65))
        dr.text((x+78,1013),str(goal['amount']),font=art.font(27,True),fill='#e7edf6')
        dr.text((x,1074),label,font=art.font(20),fill='#e7edf6')
    paragraph(dr,'Önce fikri seç: dalga silueti ve kenar atölyesi. Renkli kapağı açmak, altındaki boya işini bitirmez.',32,1132,105,21)
    dr.text((32,1221),'Gerçek sprite’lar · Serbest taş renkleri temsili · 53 otomatik yuva +1 gerçek başlangıç taşı.',font=art.font(19),fill='#a9bdd1')
    dr.text((32,1258),'Sağ görünüm örtü kesitidir; düşmüş plastikler veya oynanmış bir an değildir.',font=art.font(19),fill='#a9bdd1')
    im.convert('RGB').save(ROOT/'Docs/LevelDesign/Level234-Boya-Dalgasi.png')
    opening=Image.new('RGBA',(600,820),'#172536');board(opening,D,(40,45),65)
    opening.convert('RGB').save(HERE/'Opening.png')
    print('Rendered real project sprites and explicit shadow pin.')


if __name__=='__main__':render()
