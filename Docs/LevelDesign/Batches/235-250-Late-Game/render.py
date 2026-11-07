"""Actual project sprites with explicit static phase/ordinary-color labels."""
from author import *
from PIL import Image,ImageDraw,ImageOps
import ast,math,random,textwrap

A=ROOT/'Assets/_Project'
spec=importlib.util.spec_from_file_location('late_art',HERE.parent/'173-180/render_previews.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
MAG=(A/'Prefabs/FX/Magnet/MagnetView.prefab').read_text()
old=ast.parse((HERE.parent/'233-Polar-Lock/render.py').read_text())
exec(compile(ast.Module(body=[n for n in old.body if isinstance(n,ast.FunctionDef) and n.name in ['safe','mag_sprite','magnet']],type_ignores=[]),'existing_safe_magnet_art','exec'))
CACHE={}
LABEL={14:'Yağ',22:'Enerji kabı',23:'Pil vuruşu',24:'Omega modülü',25:'Çamur',26:'Tüp',27:'Dolap',28:'Oyma taşı',29:'Mıknatıs',30:'Porselen',33:'Kasa',34:'Kargo',39:'Pusula',40:'İki aşamalı plastik',52:'Su deposu',53:'Su birikintisi',63:'Boya kasası',68:'Örtülü heykel',70:'Rodos',75:'Fener',76:'Su kanalı',79:'Lamba çerçevesi',102:'Elma'}


def aux(o):
    if o not in CACHE:
        s=shared.DEFS[o].split('auxiliarySprites:',1)[1].split('allowCrossContextFallback:',1)[0]
        CACHE[o]=[art.sprite(art.ref(v)) for v in re.findall(r'^    - (.*)$',s,re.M)]
    return CACHE[o]


def stretch(im,img,box):
    x,y,w,h=map(round,box);im.alpha_composite(img.resize((w,h),Image.Resampling.LANCZOS),(x,y))


def cabinet(im,o,box):
    frame,gem,slat=aux(o);x,y,w,h=box;stretch(im,frame,box)
    art.paste(im,gem,(x+w*.22,y+h*.23,w*.56,h*.54))
    for k in range(4):stretch(im,slat,(x+w*.108,y+h*(.115+.753*k/4),w*.784,h*.753/4))


def paint(im,box):
    art.paste(im,art.oid_art(63),box);x,y,bw,bh=box
    bucket=Image.open(A/'Resources/PaintCanBox/PaintCan.png').convert('RGBA');w=.19;h=w*bucket.height/bucket.width;step=h*.78
    for row,count in enumerate([4,3,2]):
        for k in range(count):art.paste(im,bucket,(x+(.5+(k-(count-1)*.5)*w-w/2)*bw,y+(1-.152-row*step-h)*bh,w*bw,h*bh))


def battery(im,box):
    art.paste(im,art.oid_art(23),box);x,y,w,h=box
    prefab=(A/'Prefabs/FX/BatteryBox/BatteryBoxView.prefab').read_text()
    for k,key in enumerate(['gearSprites','coreSprites','boltSprites','plateSprites']):
        ref=re.search(r'^  '+key+r':\n  - (.*)$',prefab,re.M)[1]
        art.paste(im,art.sprite(art.ref(ref)),(x+w*(.06+(k%2)*.47),y+h*(.04+(k//2)*.48),w*.41,h*.44))


def drawboard(im,d,xy,cell,removed=()):
    w=d['width'];dr=ImageDraw.Draw(im);ls=layers(d,removed);slots,reach=feed(d,removed)
    pins={i for i,v in enumerate(d['data']['pinnedTileTypes'])if v};filled=reach|(pins&slots)
    def box(i,fw=1,fh=1):return xy[0]+i%w*cell,xy[1]+i//w*cell,fw*cell,fh*cell
    for i,a in enumerate(d['data']['cells']):
        if not a:continue
        x,y,_,_=box(i);dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(3,round(cell*.1)),fill='#354d6a',outline='#89a0b6')
        if ls[i]and ls[i][-1][0]in(14,25,53):art.paste(im,art.oid_art(ls[i][-1][0]),box(i))
    rng=random.Random(d['level']);colors={}
    for i in sorted(filled):
        choices=list(range(4));rng.shuffle(choices)
        if i in pins:choices=[d['data']['pinnedTileTypes'][i]-1]
        for c in choices:
            if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==c)and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==c)and not(i%w and i//w and all(colors.get(j)==c for j in[i-1,i-w,i-w-1])):break
        colors[i]=c;art.paste(im,art.TILES[c],box(i),cell*.07)
        if i in pins and not removed:
            x,y,_,_=box(i);dr.ellipse((x+2,y+2,x+cell*.12,y+cell*.12),fill='#ffcf75')
    for e in d['custom']['waterChannels']:
        coords=[(xy[0]+(i%w+.5)*cell,xy[1]+(i//w+.5)*cell)for i in e['pathCellIndices']]
        dr.line(coords,fill='#72e6ed',width=max(2,round(cell*.045)))
        x,y=coords[0];dr.ellipse((x-3,y-3,x+3,y+3),fill='#72e6ed')
    drawn=set()
    for i,plane in enumerate(ls):
        if not plane:continue
        o,origin=plane[-1]
        if o in(14,25,26,29,33,53,76,79)or(o,origin)in drawn:continue
        drawn.add((o,origin));fw,fh=shared.footprint(o)
        if o==102:
            e=next(e for e in d['custom']['fruits']if e['originCellIndex']==origin);fw,fh=e['width'],e['height']
        target=box(origin,fw,fh)
        if 84<=o<=95:cabinet(im,o,target)
        elif o==68:art.paste(im,art.tarp_art(next(e for e in d['custom']['tarpStatues']if e['originCellIndex']==origin)),target)
        elif o==63:paint(im,target)
        elif o==23:battery(im,target)
        else:art.paste(im,art.oid_art(o),target,cell*.015)
    for i,plane in enumerate(ls):
        if not plane or plane[-1][0]!=79:continue
        origin=plane[-1][1];x,y=i%w,i//w
        def same(xx,yy):return 0<=xx<w and 0<=yy<d['height']and(79,origin)in ls[yy*w+xx]
        l,r,u,v=same(x-1,y),same(x+1,y),same(x,y-1),same(x,y+1)
        idx=1 if l and r else 5 if u and v else 7 if r and v else 8 if l and v else 9 if r and u else 10 if l and u else 0 if r else 2 if l else 4 if v else 6 if u else 3
        art.paste(im,aux(79)[idx],box(i));c=d['data']['lampColorByCell'][i]
        if c:art.paste(im,aux(79)[10+c],box(i))
    if 26 not in removed:
        for e in d['custom']['tubes']:art.paste(im,art.tube_art(e['length'],round(cell)),box(e['originCellIndex'],1,e['length']))
    if 29 not in removed:
        for e in d['custom']['magnets']:magnet(im,e['pathCellIndices'],xy,cell,w)
    if 33 not in removed:
        for e in d['custom']['safes']:safe(im,e,xy,cell,w)


def paragraph(dr,s,x,y,width=46,size=20,color='#e7edf6'):
    for line in textwrap.wrap(s,width):dr.text((x,y),line,font=art.font(size),fill=color);y+=size+7
    return y


def label(g):
    if g['targetType']==2:return {1:'Enerji',2:'Boss HP',3:'Kuş',4:'Elmas'}[g['collectibleId']]
    if g['targetType']==0:return 'Anahtar'
    return LABEL[g['obstacleId']]


def render():
    ds=json.loads((HERE/'designs.json').read_text());out=HERE/'Previews';out.mkdir(exist_ok=True)
    contact=Image.new('RGBA',(1400,2160),'#172536');cd=ImageDraw.Draw(contact)
    for k,d in enumerate(ds):
        im=Image.new('RGBA',(1260,1510),'#172536');dr=ImageDraw.Draw(im)
        dr.text((32,24),f'{d["level"]} · {d["name"]}',font=art.font(37,True),fill='#ffcf75')
        dr.text((32,80),f'{d["difficulty"]} · {d["width"]}×{d["height"]} · {d["moves"]} hamle · 4 renk',font=art.font(24),fill='white')
        dr.text((32,136),'AÇILIŞ',font=art.font(23,True),fill='#ffcf75');drawboard(im,d,(32,180),55)
        dr.text((715,136),d['previewStage'],font=art.font(22,True),fill='#ffcf75');drawboard(im,d,(715,180),42,d['previewRemoved'])
        y=paragraph(dr,'FİKİR: '+d['idea'],32,835,49,21)
        y=paragraph(dr,'Altın noktalar yalnız gerçek gölge yuvası pinleridir. Diğer normal renkler temsili.',32,y+24,49,20,'#b7c9da')
        y=760
        for key in ['firstDecision','change','remaining']:y=paragraph(dr,d[key],715,y,43,20)+15
        assert y<1160,(d['level'],'description overflow',y)
        dr.text((32,1160),'HEDEFLER',font=art.font(23,True),fill='#ffcf75')
        for j,g in enumerate(d['goals']):
            x=32+j*242;art.paste(im,art.sprite(art.ref(g['iconOverride'])),(x,1210,57,57))
            dr.text((x+66,1221),str(g['amount']),font=art.font(25,True),fill='white')
            paragraph(dr,label(g),x,1282,20,18)
        paragraph(dr,d['budget'],32,1360,102,20)
        dr.text((32,1435),'Gerçek sprite’lar · Kanal izi şematik · Sağ kesit statik; düşüş sonrası oynanmış an değildir.',font=art.font(19),fill='#b7c9da')
        dr.text((32,1472),'Manuel oyun/editor onayı kullanıcıda. Bot sonucu insan geçiş oranı değildir.',font=art.font(19),fill='#b7c9da')
        target=out/f'{d["level"]}-{d["slug"]}.png';im.convert('RGB').save(target)
        (ROOT/f'Docs/LevelDesign/Level{d["level"]}-{d["slug"]}.png').write_bytes(target.read_bytes())
        x=(k%4)*350+16;y=(k//4)*540+12
        cd.text((x,y),f'{d["level"]} · {d["name"]}',font=art.font(18,True),fill='#ffcf75')
        cd.text((x,y+28),f'{d["difficulty"]} / {d["moves"]} hamle',font=art.font(17),fill='white')
        drawboard(contact,d,(x,y+70),29)
        cd.text((x,y+414),' → '.join(map(str,d['previewRemoved']))or'Kaynak / içerik işi',font=art.font(16),fill='#b7c9da')
        cd.text((x,y+444),f'{len(d["goals"])} hedef / {len(d["openingGeometry"]["shadowPins"])} gölge pini',font=art.font(16),fill='#b7c9da')
    contact.convert('RGB').save(HERE/'Contact.png');print('Rendered 16 real-sprite previews + contact sheet.')


if __name__=='__main__':render()
