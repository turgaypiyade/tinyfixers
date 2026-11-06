"""Static diagrams from current project sprites; normal colors are illustrative."""
from author import *
from PIL import Image, ImageDraw, ImageOps
import math, random, textwrap

spec = importlib.util.spec_from_file_location('polar_art',HERE.parent/'173-180/render_previews.py')
art = importlib.util.module_from_spec(spec)
spec.loader.exec_module(art)
D = json.loads((HERE/'design.json').read_text())
MAG = (A/'Prefabs/FX/Magnet/MagnetView.prefab').read_text()


def mag_sprite(field):
    return art.sprite(art.ref(re.search(r'^  '+field+r': (.+)$',MAG,re.M)[1]))


def safe(im, s, xy, cell, width):
    i = s['originCellIndex']
    x,y = xy[0]+i%width*cell, xy[1]+i//width*cell
    w,h = s['width']*cell, s['height']*cell
    folder = A/'Art/UI/Obstacles/RobotStyle/Case'
    front = Image.open(folder/'CaseFront.png').convert('RGBA')
    im.alpha_composite(front.resize((round(w),round(h)),Image.Resampling.LANCZOS),(round(x),round(y)))
    dr = ImageDraw.Draw(im)
    for n,(name,key) in enumerate([('Red','redHits'),('Yellow','yellowHits'),('Green','greenHits')]):
        knob = Image.open(folder/f'Case{name}Knob.png').convert('RGBA')
        art.paste(im,knob,(x+w*(.16+n*.26),y+h*.49,w*.14,h*.18))
        text=str(s[key]);font=art.font(max(8,round(cell*.24)),True)
        dr.text((x+w*(.235+n*.26),y+h*.245),text,font=font,fill='#ffffff',anchor='mm')


def magnet(im, path, xy, cell, width):
    for k,i in enumerate(path):
        x,y = xy[0]+i%width*cell, xy[1]+i//width*cell
        if k in (0,len(path)-1):
            other=path[1 if k==0 else -2]
            dx,dy=other%width-i%width,other//width-i//width
            sprite=mag_sprite('magnetSprite').rotate(math.degrees(math.atan2(-dx,-dy)),resample=Image.Resampling.BICUBIC,expand=True)
        else:
            prev,nxt=path[k-1],path[k+1]
            near={(prev%width-i%width,prev//width-i//width),(nxt%width-i%width,nxt//width-i//width)}
            if (0,-1) in near and (0,1) in near: sprite=mag_sprite('straightTubeSprite')
            elif (-1,0) in near and (1,0) in near: sprite=mag_sprite('straightTubeHorizontalSprite')
            else:
                # Runtime Tube style uses flipped copies of the same authored elbow.
                sprite=mag_sprite('elbowTubeSprite')
                if (-1,0) in near: sprite=ImageOps.mirror(sprite)
                if (0,1) in near: sprite=ImageOps.flip(sprite)
        art.paste(im,sprite,(x,y,cell,cell),cell*.015)


def board(im,d,xy,cell,removed=()):
    w=d['width'];dr=ImageDraw.Draw(im);ls=layers(d,removed);slots,reached=feed(d,removed)
    def box(i):return xy[0]+i%w*cell,xy[1]+i//w*cell,cell,cell
    for i,a in enumerate(d['data']['cells']):
        if a:
            x,y,_,_=box(i)
            dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(3,round(cell*.11)),fill='#354d6a',outline='#8aa3bf',width=max(1,round(cell*.02)))
            if ls[i] and ls[i][-1][0] in (14,25,46,53):art.paste(im,art.oid_art(ls[i][-1][0]),box(i))
    rng=random.Random(d['level']);colors={}
    for i in sorted(reached):
        choices=list(range(4));rng.shuffle(choices)
        for c in choices:
            if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==c) and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==c) and not(i%w and i//w and all(colors.get(j)==c for j in [i-1,i-w,i-w-1])):break
        colors[i]=c;art.paste(im,art.TILES[c],box(i),cell*.07)
    seen=set()
    for i,plane in enumerate(ls):
        if not plane:continue
        o,origin=plane[-1]
        if o in (14,25,29,33,46,53) or (o,origin) in seen:continue
        seen.add((o,origin));fw,fh=shared.footprint(o)
        art.paste(im,art.oid_art(o),(xy[0]+origin%w*cell,xy[1]+origin//w*cell,fw*cell,fh*cell),cell*.015)
    if 29 not in removed:
        for entry in d['custom']['magnets']:magnet(im,entry['pathCellIndices'],xy,cell,w)
    if 33 not in removed:
        for entry in d['custom']['safes']:safe(im,entry,xy,cell,w)


def para(dr,text,x,y,width=46,size=20,color='#dce7f3'):
    for line in textwrap.wrap(text,width):
        dr.text((x,y),line,font=art.font(size),fill=color);y+=size+8
    return y


def render():
    im=Image.new('RGBA',(1240,1340),'#142436');dr=ImageDraw.Draw(im)
    dr.text((32,24),'233 · KUTUP KİLİDİ',font=art.font(36,True),fill='#ffd080')
    dr.text((32,78),'Normal · 27 hamle · 8×11 · 82 aktif hücre · 4 renk',font=art.font(23),fill='#e4edf7')
    dr.text((32,128),'AÇILIŞ',font=art.font(23,True),fill='#ffd080');board(im,D,(32,172),69)
    dr.text((680,128),'KASALARIN ALTI',font=art.font(23,True),fill='#ffd080');board(im,D,(690,172),55,[33])
    y=807
    y=para(dr,'İki kasa → sekiz kargo. Sol kilit kırmızıyla, sağ kilit yeşille başlar.',680,y,43,20)+12
    y=para(dr,'Pusulalı çıkış bandı yalnız special ile açılır. Mıknatısın iki ucu alttaki U yolunda buluşmalıdır.',680,y,43,20)+12
    y=para(dr,'Kesit yalnız saklı içeriği gösterir; kargoların düşmüş konumu değildir.',680,y,43,18,'#a5b8cf')
    dr.text((32,976),'HEDEFLER',font=art.font(23,True),fill='#ffd080')
    labels=['Kargo','Kasa','Mıknatıs çifti','Pusula','Porselen']
    for n,(g,label) in enumerate(zip(D['goals'],labels)):
        x=32+n*240;art.paste(im,art.sprite(art.ref(g['iconOverride'])),(x,1020,62,62))
        dr.text((x+73,1037),str(g['amount']),font=art.font(27,True),fill='#ffffff')
        dr.text((x,1092),label,font=art.font(19),fill='#e4edf7')
    para(dr,'Önce renk kilidi mi, alt mıknatıs ucu mu? Kasa açılınca iş bitmez: special bandı ve son çıkış hattı hâlâ çözülmelidir.',32,1154,103,22)
    dr.text((32,1260),'Gerçek proje sprite’ları · Serbest taş renkleri temsili · Başlangıçta boş cep / hazır special yok.',font=art.font(18),fill='#a5b8cf')
    dr.text((32,1294),'Statik veri ve görsel inceleme · Oyun dengesi ölçülmedi; manuel test kullanıcıda.',font=art.font(18),fill='#a5b8cf')
    path=ROOT/'Docs/LevelDesign/Level233-Kutup-Kilidi.png';im.convert('RGB').save(path)
    crop=Image.new('RGBA',(650,900),'#142436');board(crop,D,(49,44),69);crop.convert('RGB').save(HERE/'Opening.png')
    print('Rendered',path.relative_to(ROOT))


if __name__=='__main__':render()
