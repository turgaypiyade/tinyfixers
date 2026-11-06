"""Real project sprite previews; static layer cutaways, never simulated play frames."""
import importlib.util, json, math, random, re, textwrap, sys
from PIL import Image, ImageDraw
from design import HERE, ROOT, A, DEFS, layers, feed

spec=importlib.util.spec_from_file_location('crystal_preview_art',HERE.parent/'173-180/render_previews.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
DS=json.loads((HERE/'designs.json').read_text())
CACHE={}

def stretch(im,asset,box):
    x,y,w,h=map(round,box)
    im.alpha_composite(asset.resize((max(1,w),max(1,h)),Image.Resampling.LANCZOS),(x,y))

def dimerdal(im,oid,box,opened):
    if oid not in CACHE:
        aux=DEFS[oid].split('auxiliarySprites:',1)[1].split('allowCrossContextFallback:',1)[0]
        CACHE[oid]=[art.sprite(art.ref(ref)) for ref in re.findall(r'^    - (.*)$',aux,re.M)]
    frame,diamond,slat=CACHE[oid];x,y,w,h=box
    stretch(im,frame,box)
    art.paste(im,diamond,(x+w*.22,y+h*.23,w*.56,h*.54))
    if not opened:
        for i in range(4):stretch(im,slat,(x+w*.108,y+h*.115+h*.753*i/4,w*.784,h*.753/4))

def seal(im,entry,box,cell):
    x,y,w,h=box
    # Sprite layers and large-tier disk/counter dimensions from AncientSealView.
    stretch(im,art.resource('SealSafe','SealBody'),box)
    diam=cell*228.375/105;cx=x+w/2;cy=y+h/2+cell*13.625/105
    art.paste(im,art.resource('SealSafe','SealDisk'),(cx-diam/2,cy-diam/2,diam,diam))
    for name,angle in zip(('Red','Yellow','Green'),(90,-30,210)):
        a=math.radians(angle);gx=cx+math.cos(a)*diam*.3016;gy=cy-math.sin(a)*diam*.3016;size=diam*.2569
        art.paste(im,art.resource('SealSafe','SealGem'+name),(gx-size/2,gy-size/2,size,size))
    plate=art.resource('SealSafe','SealCounterPlateRed');pw=cell*1.5*.95;ph=pw*plate.height/plate.width
    art.paste(im,plate,(cx-pw/2,y+cell*.3,pw,ph))
    ImageDraw.Draw(im).text((cx,y+cell*.3+ph/2),str(entry['redCount']),anchor='mm',font=art.font(max(12,round(cell*.36)),True),fill='white',stroke_width=1,stroke_fill='#341b1b')

def board(im,d,xy,cell,removed=(),opened=False):
    dr=ImageDraw.Draw(im);w=d['width'];ls=layers(d,removed);slots,reached,seeded=feed(d,removed);filled=reached|seeded
    def box(i):return xy[0]+i%w*cell,xy[1]+i//w*cell,cell,cell
    for i,active in enumerate(d['data']['cells']):
        if not active:continue
        x,y,_,_=box(i)
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(3,cell//10),fill='#354c65',outline='#65839c')
        for o,_ in ls[i]:
            if o==25:art.paste(im,art.oid_art(o),box(i))
    rng=random.Random(d['level']);colors={}
    for i in sorted(filled):
        choices=list(range(4));rng.shuffle(choices)
        for color in choices:
            if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==color) and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==color):break
        colors[i]=color;art.paste(im,art.TILES[color],box(i),cell*.07)
    for i in slots-filled:
        x,y,_,_=box(i);dr.ellipse((x+cell*.45,y+cell*.45,x+cell*.55,y+cell*.55),fill='#8398aa')
    drawn=set()
    for i,cell_layers in enumerate(ls):
        for o,origin in cell_layers:
            if o in (25,42):continue
            key=o,origin
            if key in drawn:continue
            drawn.add(key)
            region=[j for j,other in enumerate(ls) if key in other]
            l,t=min(j%w for j in region),min(j//w for j in region)
            fw,fh=max(j%w for j in region)-l+1,max(j//w for j in region)-t+1
            rect=(xy[0]+l*cell,xy[1]+t*cell,fw*cell,fh*cell)
            if 84<=o<=95:dimerdal(im,o,rect,opened)
            elif o==68:
                e=next(e for e in d['custom']['tarpStatues'] if e['originCellIndex']==origin)
                art.paste(im,art.tarp_art(e),rect)
            elif o==64:
                e=next(e for e in d['custom']['ancientSeals'] if e['originCellIndex']==origin)
                seal(im,e,rect,cell)
            else:art.paste(im,art.oid_art(o),rect,cell*.02)
    for i,cell_layers in enumerate(ls):
        if any(o==42 for o,_ in cell_layers):art.paste(im,art.oid_art(42),box(i))

LABELS={4:'Ahşap',25:'Çamur',28:'Oyma taşı',30:'Porselen',39:'Pusula taşı',42:'Çim',64:'Mühür',68:'Heykel'}
def goal_label(g):
    return 'Elmas' if g['targetType']==2 else 'Anahtar' if g['targetType']==0 else LABELS[g['obstacleId']]
def paragraph(dr,t,xy,size=21,width=48):
    y=xy[1]
    for line in textwrap.wrap(t,width):dr.text((xy[0],y),line,font=art.font(size),fill='#e7edf6');y+=size+8
    return y
def render():
    for d in DS:
        if '--only225' in sys.argv and d['level']!=225:continue
        im=Image.new('RGBA',(1440,1540),'#172536');dr=ImageDraw.Draw(im)
        dr.text((34,22),f'{d["level"]} · {d["name"].upper()}',font=art.font(35,True),fill='#ffcf75')
        kind='HARD' if d['level']==225 else 'NORMAL'
        dr.text((34,77),f'{kind} · {d["width"]}×{d["height"]} · {sum(d["data"]["cells"])} hücre · {d["moves"]} hamle · 4 taş rengi',font=art.font(23),fill='#e7edf6')
        dr.text((34,129),'AÇILIŞ',font=art.font(23,True),fill='#ffcf75')
        board(im,d,(34,176),64)
        dr.text((718,129),'ÖRTÜ / ENGELLER KALKINCA',font=art.font(23,True),fill='#ffcf75')
        board(im,d,(718,176),42,d['previewRemoved'],d['openedDimerdals'])
        dr.text((718,660),'Statik kesit; oynanmış bir an değildir.',font=art.font(18),fill='#a9bdd1')
        y=710
        for text in (d['idea'],d['firstDecision'],d['change'],d['remaining']):
            y=paragraph(dr,text,(718,y),19,53)+14
        assert y<1100, (d['level'],'right-hand description exceeds its allocated area',y)
        dr.text((34,1110),'HEDEFLER',font=art.font(24,True),fill='#ffcf75')
        for j,g in enumerate(d['goals']):
            x=34+j*340
            art.paste(im,art.sprite(art.ref(g['iconOverride'])),(x,1160,64,64))
            dr.text((x+75,1181),f'{g["amount"]} {goal_label(g)}',font=art.font(22,True),fill='#e7edf6')
        paragraph(dr,d['budget'],(34,1290),21,105)
        dr.text((34,1400),'Güncel proje sprite’ları · Serbest taş renkleri temsili · Renk pini / hazır special yok.',font=art.font(20),fill='#a9bdd1')
        dr.text((34,1440),'Noktalı boş hücreler: üzerlerindeki kapak açılınca beslenir. Hareketli engellerin konumu oyunla değişir.',font=art.font(19),fill='#a9bdd1')
        dr.text((34,1477),'Statik doğrulama ve görsel inceleme yapıldı; oynanış dengesi ölçülmedi.',font=art.font(19),fill='#a9bdd1')
        im.convert('RGB').save(HERE/'Previews'/f'Level{d["level"]}.png')
        im.convert('RGB').save(ROOT/f'Docs/LevelDesign/Level{d["level"]}-{d["slug"]}.png')
    sheet=Image.new('RGBA',(2240,1130),'#172536');dr=ImageDraw.Draw(sheet)
    for j,d in enumerate(DS):
        x=25+j*555
        dr.text((x,24),f'{d["level"]} · {d["name"]}',font=art.font(23,True),fill='#ffcf75')
        dr.text((x,60),f'{d["moves"]} hamle'+(' · HARD' if d['level']==225 else ''),font=art.font(21),fill='#e7edf6')
        board(sheet,d,(x,112),56)
        for k,g in enumerate(d['goals']):dr.text((x,810+k*33),f'{g["amount"]} {goal_label(g)}',font=art.font(20),fill='#e7edf6')
    dr.text((25,1040),'Gerçek proje sprite’ları; taş renkleri temsili. Noktalı cepler kapaklar açılınca beslenir. Denge henüz oynanmadı.',font=art.font(23),fill='#a9bdd1')
    sheet.convert('RGB').save(HERE/'Levels222-225.png')
    print('Rendered four opening/cutaway sheets + contact sheet from current project sprites.')
if __name__=='__main__':render()
