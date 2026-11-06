"""Actual project sprites, static opening/layer diagrams; no gameplay simulation."""
import importlib.util,json,random,re,textwrap
from PIL import Image,ImageDraw
from design import HERE,ROOT,DEFS,layers,feed,footprint
spec=importlib.util.spec_from_file_location('lantern_wind_art',HERE.parent/'173-180/render_previews.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
DS=json.loads((HERE/'designs.json').read_text());CACHE={}

def aux(oid):
    key=('aux',oid)
    if key not in CACHE:
        section=DEFS[oid].split('auxiliarySprites:',1)[1].split('allowCrossContextFallback:',1)[0]
        CACHE[key]=[art.sprite(art.ref(s)) for s in re.findall(r'^    - (.*)$',section,re.M)]
    return CACHE[key]
def stretch(im,img,box):
    x,y,w,h=map(round,box);im.alpha_composite(img.resize((max(1,w),max(1,h)),Image.Resampling.LANCZOS),(x,y))
def windmill():
    if 'mill' not in CACHE:
        img=Image.new('RGBA',(512,512))
        for asset in aux(80):stretch(img,asset,(0,0,512,512))
        CACHE['mill']=img
    return CACHE['mill']
def cabinet(im,oid,box,opened):
    frame,gem,slat=aux(oid);x,y,w,h=box
    stretch(im,frame,box);art.paste(im,gem,(x+w*.22,y+h*.23,w*.56,h*.54))
    if not opened:
        for i in range(4):stretch(im,slat,(x+w*.108,y+h*(.115+.753*i/4),w*.784,h*.753/4))
def wall(im,d,i,xy,cell,ls):
    w=d['width'];x,y=i%w,i//w;o,origin=next((o,origin) for o,origin in ls[i] if o==55)
    def same(xx,yy):return 0<=xx<w and 0<=yy<d['height'] and (55,origin) in ls[yy*w+xx]
    for dx,dy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
        v,h=same(x,y+dy),same(x+dx,y);corner=('T' if dy<0 else 'B')+('L' if dx<0 else 'R')
        name='Outer'+corner if not v and not h else 'Edge'+('Left' if dx<0 else 'Right') if v and not h else 'Edge'+('Top' if dy<0 else 'Bottom') if h and not v else 'Inner'
        art.paste(im,art.resource('Wall','WallQ_'+name),(xy[0]+x*cell+(cell/2 if dx>0 else 0),xy[1]+y*cell+(cell/2 if dy>0 else 0),cell/2,cell/2))
    art.paste(im,art.resource('Wall','WallDetail_Kabartma'+str(1+i%2)),(xy[0]+x*cell,xy[1]+y*cell,cell,cell),cell*.14)
def board(im,d,xy,cell,removed=(),opened=False):
    dr=ImageDraw.Draw(im);w=d['width'];ls=layers(d,removed);slots,reached,seeded=feed(d,removed)
    pins=d['data']['pinnedTileTypes'];filled=reached|seeded|{i for i,v in enumerate(pins) if v and i in slots}
    def box(i):return xy[0]+i%w*cell,xy[1]+i//w*cell,cell,cell
    for i,c in enumerate(d['data']['cells']):
        if c:
            x,y,_,_=box(i);dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(3,cell//10),fill='#354c65',outline='#65839c')
    rng=random.Random(d['level']);colors={}
    for i in sorted(filled):
        choices=list(range(4));rng.shuffle(choices)
        if pins[i]:choices=[pins[i]-1]
        for color in choices:
            if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==color) and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==color):break
        colors[i]=color;art.paste(im,art.TILES[color],box(i),cell*.07)
        if pins[i] and not removed:
            x,y,_,_=box(i);dr.ellipse((x+cell*.03,y+cell*.03,x+cell*.10,y+cell*.10),fill='#ffcf75')
    for i in slots-filled:
        x,y,_,_=box(i);dr.ellipse((x+cell*.45,y+cell*.45,x+cell*.55,y+cell*.55),fill='#9aabbb')
    drawn=set()
    for i,plane in enumerate(ls):
        for oid,origin in plane:
            if oid in (42,79):continue
            if oid==55:wall(im,d,i,xy,cell,ls);continue
            key=(oid,origin)
            if key in drawn:continue
            drawn.add(key);fw,fh=footprint(oid);rect=(xy[0]+origin%w*cell,xy[1]+origin//w*cell,fw*cell,fh*cell)
            if 84<=oid<=95:cabinet(im,oid,rect,opened)
            else:art.paste(im,windmill() if oid==80 else art.oid_art(oid),rect,cell*.025)
    for i,plane in enumerate(ls):
        if not any(o==79 for o,_ in plane):continue
        origin=next(origin for o,origin in plane if o==79);x,y=i%w,i//w
        same=lambda xx,yy:0<=xx<w and 0<=yy<d['height'] and (79,origin) in ls[yy*w+xx]
        left,right,up,down=same(x-1,y),same(x+1,y),same(x,y-1),same(x,y+1)
        index=1 if left and right else 5 if up and down else 7 if right and down else 8 if left and down else 9 if right and up else 10 if left and up else 0 if right else 2 if left else 4 if down else 6 if up else 3
        art.paste(im,aux(79)[index],box(i));color=d['data']['lampColorByCell'][i]
        if color:art.paste(im,aux(79)[10+color],box(i))
    for i,plane in enumerate(ls):
        if any(o==42 for o,_ in plane):art.paste(im,art.oid_art(42),box(i))
    if d['refill']:
        for x in d['movableColumns']:
            xx=xy[0]+(x+.5)*cell
            dr.polygon([(xx-5,xy[1]-12),(xx+5,xy[1]-12),(xx,xy[1]-3)],fill='#ffcf75')
def paragraph(dr,t,x,y,size=20,width=52,color='#e7edf6'):
    for line in textwrap.wrap(t,width):dr.text((x,y),line,font=art.font(size),fill=color);y+=size+8
    return y
LABELS={28:'Oyma taşı',40:'İki aşamalı plastik',42:'Çim',55:'Duvar parçası',71:'Sarı sandık',75:'Fener',79:'Lamba çerçevesi',83:'Un'}
def label(g):return 'Boss HP' if g['targetType']==2 and g['collectibleId']==2 else 'Elmas' if g['targetType']==2 else LABELS[g['obstacleId']]
def render():
    out=HERE/'Previews';out.mkdir(exist_ok=True)
    for d in DS:
        im=Image.new('RGBA',(1480,1630),'#172536');dr=ImageDraw.Draw(im)
        dr.text((34,22),f'{d["level"]} · {d["name"].replace("i","İ").upper()}',font=art.font(37,True),fill='#ffcf75')
        dr.text((34,80),f'{d["difficulty"].upper()} · {d["width"]}×{d["height"]} · {sum(d["data"]["cells"])} hücre · {d["moves"]} hamle · 4 renk',font=art.font(24),fill='#e7edf6')
        dr.text((34,134),'AÇILIŞ',font=art.font(23,True),fill='#ffcf75');board(im,d,(34,180),65)
        dr.text((730,134),d['previewStage'],font=art.font(21,True),fill='#ffcf75');board(im,d,(730,180),45,d['previewRemoved'],d.get('openedDimerdals',False))
        dr.text((730,700),'Statik katman kesiti; oynanmış bir an değildir.',font=art.font(18),fill='#a9bdd1')
        y=755
        for key in ['idea','firstDecision','change','remaining']:y=paragraph(dr,d[key],730,y,19,53)+14
        assert y<1220,(d['level'],'text overflow',y)
        aside=960
        if d['custom']['dimerdals']:
            counts=' + '.join(str(e['count']) for e in d['custom']['dimerdals'])
            quota='Kutu üretimi: '+counts
            if len(d['custom']['dimerdals'])>1:quota+=' = '+str(sum(e['count'] for e in d['custom']['dimerdals']))
            aside=paragraph(dr,quota+' elmas. Her kutu kendi adedini tamamlar.',34,aside,20,47)+18
        if d['refill']:
            aside=paragraph(dr,'Altın oklar: yalnız bu iki sütundan fener refill. 4 örtülü başlangıç + 16 yeni = 20 fener.',34,aside,20,47)+18
        if d['kind']:
            aside=paragraph(dr,'Boss: 660 toplam HP / 3 rakip. Hedef dışı plastik ve geri sayımlı alet baskısı.',34,aside,20,47)+18
        assert aside<1220,(d['level'],'aside overflow',aside)
        dr.text((34,1230),'HEDEFLER',font=art.font(24,True),fill='#ffcf75')
        for j,g in enumerate(d['goals']):
            x=34+j*355;art.paste(im,art.sprite(art.ref(g['iconOverride'])),(x,1280,66,66))
            dr.text((x+76,1300),f'{g["amount"]} {label(g)}',font=art.font(21,True),fill='#e7edf6')
        paragraph(dr,d['budget'],34,1395,21,112)
        dr.text((34,1520),'Gerçek proje sprite’ları · Serbest taş renkleri temsili · Altın noktalar gerçek başlangıç pinleri.',font=art.font(19),fill='#a9bdd1')
        dr.text((34,1555),'Statik veri / erişim ve görsel inceleme; oyun dengesi ve cihaz performansı ölçülmedi.',font=art.font(19),fill='#a9bdd1')
        im.convert('RGB').save(out/f'Level{d["level"]}.png');im.convert('RGB').save(ROOT/f'Docs/LevelDesign/Level{d["level"]}-{d["slug"]}.png')
    sheet=Image.new('RGBA',(2450,1040),'#172536');dr=ImageDraw.Draw(sheet)
    for j,d in enumerate(DS):
        x=25+j*485;dr.text((x,24),f'{d["level"]} · {d["name"]}',font=art.font(23,True),fill='#ffcf75')
        dr.text((x,64),f'{d["moves"]} hamle · {d["difficulty"]}',font=art.font(21),fill='#e7edf6');board(sheet,d,(x,117),47)
        for k,g in enumerate(d['goals']):
            art.paste(sheet,art.sprite(art.ref(g['iconOverride'])),(x,680+k*56,42,42))
            dr.text((x+52,692+k*56),f'{g["amount"]} {label(g)}',font=art.font(19),fill='#e7edf6')
    dr.text((25,970),'Gerçek sprite’lar / statik tasarım · 226 sütun dolumu · 227 iki yönlü raf · 228 ünlem · 229 burgu · 230 Boss Duel',font=art.font(23),fill='#a9bdd1')
    sheet.convert('RGB').save(HERE/'Levels226-230.png')
    print('Rendered five opening/cutaway previews and contact sheet from current project sprites.')
if __name__=='__main__':render()
