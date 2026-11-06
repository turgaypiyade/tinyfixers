"""Actual project sprites and press source textures; static authored diagrams only."""
import importlib.util,json,random,textwrap,math
from PIL import Image,ImageDraw
from author import HERE,BATCH,ROOT,shared
spec=importlib.util.spec_from_file_location('level229_art',BATCH.parent/'173-180/render_previews.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
d=json.loads((HERE/'design.json').read_text())

def press():
    # Same source crops and initial pose as GelLauncherPressView. The striped rod
    # is a static approximation of its shader, not a new game sprite.
    out=Image.new('RGBA',(512,1024));dr=ImageDraw.Draw(out)
    left,right,top,bottom=round(.205*512),round(.805*512),round(.12*1024),round(.865*1024)
    w,h=right-left,bottom-top;liquid=round(.63*h)
    dr.rectangle((left,top,right,bottom),fill=(26,6,54,240))
    p=ROOT/'Assets/_Project/Resources/GelLauncher/Press'
    gel=Image.open(p/'PressGel.png').convert('RGBA');gel=gel.crop((82,gel.height-1094,812,gel.height-99))
    out.alpha_composite(gel.resize((w,liquid),Image.Resampling.LANCZOS),(left,bottom-liquid))
    source=Image.open(p/'PressHead.png').convert('RGBA');head=source.crop((65,source.height-724,825,source.height-339))
    hw=round(w*.96);hh=round(hw*385/760);headBottom=bottom-round(liquid-hh*.025)
    rodBottom=bottom-round(liquid+hh*.92);rodWidth=round(w*.26);rx=round((left+right-rodWidth)/2)
    dr.rectangle((rx,top,rx+rodWidth,rodBottom),fill='#080b12')
    for y in range(top-rodWidth,rodBottom,22):
        dr.polygon([(rx,y),(rx+rodWidth,y+rodWidth*.28),(rx+rodWidth,y+rodWidth*.28+11),(rx,y+11)],fill='#edab09')
    out.alpha_composite(head.resize((hw,hh),Image.Resampling.LANCZOS),(round((left+right-hw)/2),headBottom-hh))
    frame=Image.open(p/'PressFrame.png').convert('RGBA').resize(out.size,Image.Resampling.LANCZOS)
    out.alpha_composite(frame);return out
PRESS=press()

def board(im,model,xy,cell,removed=()):
    dr=ImageDraw.Draw(im);w=model['width'];ls=shared.layers(model,removed);slots,reached,seed=shared.feed(model,removed)
    def box(i):return xy[0]+i%w*cell,xy[1]+i//w*cell,cell,cell
    for i,a in enumerate(model['data']['cells']):
        if not a:continue
        x,y,_,_=box(i);dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(3,cell//10),fill='#354c65',outline='#65839c')
        if ls[i] and ls[i][-1][0]==46:art.paste(im,art.oid_art(46),box(i))
    rng=random.Random(229);colors={}
    for i in sorted(reached|seed):
        choices=list(range(4));rng.shuffle(choices)
        for color in choices:
            if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==color) and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==color):break
        colors[i]=color;art.paste(im,art.TILES[color],box(i),cell*.07)
    for i in slots-reached-seed:
        x,y,_,_=box(i);dr.ellipse((x+cell*.45,y+cell*.45,x+cell*.55,y+cell*.55),fill='#a9bdd1')
    seen=set()
    for i,layers in enumerate(ls):
        for oid,origin in layers:
            if oid==46 or (oid,origin) in seen:continue
            seen.add((oid,origin));fw,fh=shared.footprint(oid)
            image=PRESS if oid==59 else art.oid_art(oid)
            art.paste(im,image,(xy[0]+origin%w*cell,xy[1]+origin//w*cell,fw*cell,fh*cell),cell*.02)
    for x in model.get('launcherColumns',[]):
        xx=xy[0]+(x+.5)*cell;y=xy[1]+9*cell-7
        dr.polygon([(xx,y-7),(xx-4,y),(xx+4,y)],fill='#ffcf75')

def paragraph(dr,text,x,y,size=20,width=51,color='#e7edf6'):
    for line in textwrap.wrap(text,width):dr.text((x,y),line,font=art.font(size),fill=color);y+=size+8
    return y

def render():
    im=Image.new('RGBA',(1480,1630),'#172536');dr=ImageDraw.Draw(im)
    dr.text((34,24),'229 · RÜZGÂR BURGUSU · YENİ TASARIM',font=art.font(34,True),fill='#ffcf75')
    dr.text((34,79),'SUPER HARD · 9×11 · 99 hücre · 28 hamle · 4 renk',font=art.font(24),fill='#e7edf6')
    dr.text((34,134),'BAŞLANGIÇ YERLEŞİMİ',font=art.font(23,True),fill='#ffcf75');board(im,d,(34,180),65)
    dr.text((730,134),'OYMA TAŞLARI AÇILINCA',font=art.font(23,True),fill='#ffcf75');board(im,d,(730,180),45,[28])
    dr.text((730,704),'Statik katman kesiti; oynanmış bir an değildir.',font=art.font(18),fill='#a9bdd1')
    y=755
    for key in ['idea','firstDecision','change','remaining']:y=paragraph(dr,d[key],730,y,19,53)+14
    assert y<1210,y
    y=950
    for text in ['12 oyma taşının altında 12 gerçek jel kaynağı.','İki pres, orta sütunları yukarı doğru açar.','63 taş yuvasının tamamı doğal beslenir. Pin ve başlangıç boşu yok.']:
        y=paragraph(dr,text,34,y,20,48)+16
    labels={46:'Jel kaplama',28:'Oyma taşı',70:'Rodos',30:'Porselen'}
    dr.text((34,1230),'HEDEFLER',font=art.font(24,True),fill='#ffcf75')
    for j,g in enumerate(d['goals']):
        x=34+j*355;art.paste(im,art.sprite(art.ref(g['iconOverride'])),(x,1280,66,66))
        dr.text((x+76,1300),str(g['amount'])+' '+labels[g['obstacleId']],font=art.font(21,True),fill='#e7edf6')
    paragraph(dr,d['budget'],34,1395,21,112)
    dr.text((34,1520),'Gerçek proje sprite’ları / pres dokuları · Serbest taş renkleri temsili · Pres çubuğu statik shader yaklaşımı.',font=art.font(18),fill='#a9bdd1')
    dr.text((34,1555),'Statik veri ve PNG incelemesi; Unity Play / cihaz dengesi ve performansı ölçülmedi.',font=art.font(19),fill='#a9bdd1')
    out=HERE/'Level229.png';im.convert('RGB').save(out)
    im.convert('RGB').save(ROOT/'Docs/LevelDesign/Level229-Ruzgar-Burgusu.png')
    print('Rendered229 opening and actual gel-under-stone cutaway with current project art.')

if __name__=='__main__':render()
