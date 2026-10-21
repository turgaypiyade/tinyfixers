"""Project-sprite authoring preview and explicit canal map, not a Unity screenshot."""
from pathlib import Path
import importlib.util,json,random,re
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];OUT=ROOT/'Docs/LevelDesign'
spec=importlib.util.spec_from_file_location('preview',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
d=json.loads((HERE/'design.json').read_text());path=d['waterChannels'][0]['pathCellIndices']
art=ROOT/'Assets/_Project/Art/UI/Obstacles/WaterChannel'
def load(name):return Image.open(art/(name+'.png')).convert('RGBA')
def side(a,b):
    dx,dy=b%9-a%9,b//9-a//9
    return 'L' if dx<0 else 'R' if dx>0 else 'T' if dy<0 else 'B'
def shape(a,b):
    s={a,b}
    return 'H' if s=={'L','R'} else 'V' if s=={'T','B'} else ('L' if 'L' in s else 'R')+('T' if 'T' in s else 'B')
shapes={path[0]:'V'}
for k,i in enumerate(path[1:],1):shapes[i]=shape(side(i,path[k-1]),side(i,path[k+1]) if k<len(path)-1 else 'B')
def drawboard(im,origin,cell,map_only=False):
    dr=ImageDraw.Draw(im);rng=random.Random(183);colors={}
    for i in range(99):
        x,y=origin[0]+i%9*cell,origin[1]+i//9*cell
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//9),fill='#485b79',outline='#7587a2')
        oid=d['data']['obstacles'][i]
        if i in shapes:
            r.paste(im,load('Channel_'+shapes[i]+('' if map_only or i==path[0] else '_Closed')),(x,y,cell,cell))
            if map_only or i==path[0]:r.paste(im,load('Water_'+shapes[i]),(x,y,cell,cell))
        if i==path[0]:r.paste(im,load('ChannelSource'),(x,y,cell,cell));continue
        if oid:
            r.paste(im,r.oid_art(oid),(x,y,cell,cell),cell*.015)
        elif not map_only:
            choices=list(range(4));rng.shuffle(choices)
            for color in choices:
                if not (i%9>=2 and colors.get(i-1)==colors.get(i-2)==color or i//9>=2 and colors.get(i-9)==colors.get(i-18)==color):break
            colors[i]=color;r.paste(im,r.TILES[color],(x,y,cell,cell),cell*.05)
        elif i not in shapes:dr.ellipse((x+cell*.46,y+cell*.46,x+cell*.54,y+cell*.54),fill='#9aacc2')
    if map_only:
        i=path[-1];x=origin[0]+(i%9+.5)*cell;y=origin[1]+11*cell
        dr.line((x,y,x,y+20),fill='#75deed',width=4);dr.polygon(((x-8,y+13),(x+8,y+13),(x,y+25)),fill='#75deed')
im=Image.new('RGBA',(1280,1130),r.BG);dr=ImageDraw.Draw(im)
dr.text((32,25),'183 / Fenerli Bent',font=r.font(40,True),fill=r.GOLD)
dr.text((33,82),'9 × 11 · 28 hamle · 4 renk · Normal',font=r.font(24),fill=r.INK)
dr.text((33,122),'Kıvrımlı su yolu · Kesintisiz fener sütunu · Alt kenarı tamamlayan taş set',font=r.font(20),fill=r.MUTED)
drawboard(im,(32,175),70)
x,y=720,175;dr.text((x,y),'HEDEFLER',font=r.font(21,True),fill=r.GOLD);y+=36
for image,label in ((load('ChannelSource'),'1 su kanalı'),(r.sprite(r.ref(re.search(r'^    goalIconSprite: (.*)$',r.DEFS[75],re.M)[1])),'24 fener'),(r.oid_art(28),'12 oyma taşı')):
    r.paste(im,image,(x,y,51,51));dr.text((x+65,y+12),label,font=r.font(24,True),fill=r.INK);y+=62
y+=9;dr.text((x,y),'OYUN FİKRİ',font=r.font(20,True),fill=r.GOLD);y+=33
y=r.paragraph(dr,x,y,d['decision'],width=43,size=19);y+=18
dr.text((x,y),'KANAL HARİTASI',font=r.font(20,True),fill=r.GOLD);y+=34
map_y=y;drawboard(im,(x,map_y),30,True)
dr.text((x+300,map_y+18),'1 kaynak',font=r.font(18,True),fill=r.INK)
dr.text((x+300,map_y+50),'20 hücre',font=r.font(18,True),fill=r.INK)
r.paragraph(dr,x+300,map_y+97,'Su yalnız açık ve bağlı yoldan ilerler.',width=17,size=17)
r.paragraph(dr,x+300,map_y+210,'Açılan hücreler normal oynanır kalır.',width=17,size=17)
y=map_y+373
y=r.paragraph(dr,x,y,'Haritada taşlar gizli, su temsili. Açılışta kanal taşların altındadır.',width=46,size=17,color=r.MUTED)
y=r.paragraph(dr,x,y,'11 fener başlangıçta; 13 ek fener dolumdan gelir. Taş set 3 vuruşludur.',width=46,size=18)
dr.text((33,986),'99 aktif · 0 authored hole · 1 geçirgen depo · 75 başlangıç taş hücresi',font=r.font(21),fill=r.INK)
dr.text((33,1030),'Mevcut proje sprite’ları; normal renkler temsili. Oyun ekran görüntüsü değildir.',font=r.font(18),fill=r.MUTED)
dr.text((33,1065),'Statik dolum/yol kontrolü · Unity Play/build veya bot simülasyonu yok · Denge ölçülmedi',font=r.font(18),fill=r.MUTED)
p=OUT/'Level183-Fenerli-Bent.png';im.convert('RGB').save(p);print(p)

# Reuse the established 173–180 renderer for 179 only, then refresh its contact-sheet caption.
r.DESIGNS=[entry for entry in r.DESIGNS if entry['n']==179]
r.DESIGNS[0]['moves']=31
original_contact=Image.open(OUT/'Levels173-180-Onizleme.png').convert('RGB')
r.render()
x,y=30+2*370,119+515
ImageDraw.Draw(original_contact).rectangle((x,y+31,x+350,y+60),fill=r.BG)
ImageDraw.Draw(original_contact).text((x,y+33),'31 hamle · Super Hard',font=r.font(18),fill=r.INK)
original_contact.save(OUT/'Levels173-180-Onizleme.png')
