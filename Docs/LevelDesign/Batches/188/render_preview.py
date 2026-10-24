"""Project-sprite composition and historical comparison; not a game screenshot."""
from pathlib import Path
import importlib.util,json,math,random,re
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];DOCS=ROOT/'Docs/LevelDesign'
spec=importlib.util.spec_from_file_location('render',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
d=json.loads((HERE/'design.json').read_text())
inventory={e['level']:e for e in json.loads((DOCS/'Reviews/188-Catalog/Catalog-Inventory-Before.json').read_text())}

def sliced_body(width,height,cell):
    source=r.resource('SealSafe','SealBody');sw,sh=source.size
    meta=(ROOT/'Assets/_Project/Resources/SealSafe/SealBody.png.meta').read_text()
    m=re.search(r'spriteBorder: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+), w: ([\d.]+)\}',meta)
    left,bottom,right,top=[round(float(v)) for v in m.groups()]
    factor=cell*1.5/sw
    dx=[0,round(left*factor),width-round(right*factor),width]
    dy=[0,round(top*factor),height-round(bottom*factor),height]
    sx=[0,left,sw-right,sw];sy=[0,top,sh-bottom,sh]
    out=Image.new('RGBA',(width,height))
    for y in range(3):
        for x in range(3):
            if sx[x+1]<=sx[x] or sy[y+1]<=sy[y]:continue
            part=source.crop((sx[x],sy[y],sx[x+1],sy[y+1])).resize((dx[x+1]-dx[x],dy[y+1]-dy[y]),Image.Resampling.LANCZOS)
            out.alpha_composite(part,(dx[x],dy[y]))
    return out

def seal_art(entry,cell):
    w,h=entry['width']*cell,entry['height']*cell
    im=sliced_body(w,h,cell);large=min(entry['width'],entry['height'])>=3
    disk_cells=228.375/105 if large else 145/105
    disk_size=cell*disk_cells;disk_x=w/2;disk_y=h/2+cell*(13.625/105 if large else 6.75/105)
    r.paste(im,r.resource('SealSafe','SealDisk'),(disk_x-disk_size/2,disk_y-disk_size/2,disk_size,disk_size))
    colors=['Red','Yellow','Green'];start=max(0,entry.get('startColor',1)-1)
    for j,angle in enumerate([90,-30,210]):
        radius=disk_size*.3016;size=disk_size*.2569
        x=disk_x+math.cos(math.radians(angle))*radius;y=disk_y-math.sin(math.radians(angle))*radius
        r.paste(im,r.resource('SealSafe','SealGem'+colors[(start+j)%3]),(x-size/2,y-size/2,size,size))
    fu=cell*(1.5 if large else 1);pw=fu*.95
    plate=r.resource('SealSafe','SealCounterPlate'+colors[start]);ph=pw*plate.height/plate.width
    py=fu*.2;r.paste(im,plate,(w/2-pw/2,py-ph/2,pw,ph))
    count=entry[('redCount','yellowCount','greenCount')[start]]
    ImageDraw.Draw(im).text((w/2,py),str(count),font=r.font(max(9,round(fu*.24)),True),fill='white',stroke_width=max(1,cell//35),stroke_fill='#1c0e0a',anchor='mm')
    return im

def board(im,entry,xy,cell,under=False):
    w=entry['width'];data=entry['data'];custom=entry.get('custom',{})
    seals=entry.get('ancientSeals',custom.get('ancientSeals',[]))
    obs=(data['obstacles'] if under else entry['top']);orig=(data['obstacleOrigins'] if under else entry['topOrigins'])
    rng=random.Random(entry.get('n',entry.get('level',0)));dr=ImageDraw.Draw(im);colors={}
    overlay_ids={14,25,42,46,53,58,69,76}
    for i,active in enumerate(data['cells']):
        if not active:continue
        x,y=xy[0]+i%w*cell,xy[1]+i//w*cell
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//8),fill='#777eb5',outline='#989dce')
        oid=obs[i]
        if oid==0 or oid in overlay_ids:
            choices=list(range(4));rng.shuffle(choices)
            color=next((c for c in choices if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==c) and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==c)),choices[0])
            colors[i]=color;r.paste(im,r.TILES[color],(x,y,cell,cell),cell*.04)
        if oid in (0,64,26):continue
        if orig[i]!=i:continue
        m=re.search(r'^    size: \{x: (\d+), y: (\d+)\}$',r.DEFS[oid],re.M);bw,bh=map(int,m.groups())
        if oid in overlay_ids:bw=bh=1
        r.paste(im,r.oid_art(oid),(x,y,bw*cell,bh*cell),cell*.015)
    if not under:
        for s in seals:
            i=s['originCellIndex'];im.alpha_composite(seal_art(s,cell),(xy[0]+i%w*cell,xy[1]+i//w*cell))

im=Image.new('RGBA',(1280,1140),r.BG);dr=ImageDraw.Draw(im)
dr.text((30,25),'188 / Üç Mühürlü Geçit',font=r.font(38,True),fill=r.GOLD)
dr.text((31,78),'9 × 11 · 28 hamle · 4 renk · Normal',font=r.font(24),fill=r.INK)
dr.text((31,119),'Rengi seç → geçidi aç → kargoları çıkışa indir',font=r.font(22),fill=r.MUTED)
board(im,d,(30,175),68)
x,y=700,175;dr.text((x,y),'HEDEFLER',font=r.font(22,True),fill=r.GOLD);y+=37
for oid,label in [(64,'3 antik mühür'),(34,'6 kargo'),(40,'21 iki aşamalı plastik'),(39,'9 pusula · yalnız special')]:
    art=seal_art(d['ancientSeals'][0],24) if oid==64 else r.oid_art(oid)
    r.paste(im,art,(x,y,54,54));dr.text((x+70,y+14),label,font=r.font(23,True),fill=r.INK);y+=68
y+=9;dr.text((x,y),'İLK KARAR',font=r.font(21,True),fill=r.GOLD);y+=35
y=r.paragraph(dr,x,y,'Kırmızı, sarı ve yeşil başlayan üç geçitten hangisini önce açacaksın?',width=43,size=20);y+=16
y=r.paragraph(dr,x,y,'Her mühür: her renkten 7 taş. Açılınca 2 kargo ve 7 plastik ortaya çıkar.',width=43,size=19);y+=18
dr.text((x,y),'MÜHÜRLERİN ALTINDA',font=r.font(20,True),fill=r.GOLD);y+=37
# Only reveal the sealed three-row region in this inset, keeping the two-layer relationship legible.
hidden=Image.new('RGBA',(9*45,11*45),r.BG);board(hidden,d,(0,0),45,under=True)
im.alpha_composite(hidden.crop((0,4*45,9*45,7*45)),(x,y));y+=157
y=r.paragraph(dr,x,y,'Kargolar 2., 5. ve 8. sütunda. Alt pusula hattını special ile aç; plastikleri temizleyip kargoları indir.',width=43,size=18)
dr.text((31,963),'95 aktif hücre · 4 köşe boşluğu · 59 başlangıç taş hücresi · Hazır special yok',font=r.font(20),fill=r.INK)
dr.text((31,1010),'Güncel proje sprite’larıyla yerleşim önizlemesi; normal taş renkleri temsili.',font=r.font(19),fill=r.MUTED)
dr.text((31,1051),'187 önceki level tarandı. Bu bir oyun ekran görüntüsü veya ölçülmüş denge sonucu değildir.',font=r.font(18),fill=r.MUTED)
dr.text((31,1090),'Level içerikleri, ortak köşe çizimi ve çalışma zamanı kodu korundu; Unity Play/build/sim yok.',font=r.font(18),fill=r.MUTED)
im.convert('RGB').save(DOCS/'Level188-Uc-Muhurlu-Gecit.png')

cards=[(143,'Ortada tek büyük mühür; lazer ve çamur açılır.'),(107,'Yakın yatay kütle; çiçekli zemin ve farklı maskeli gövde.'),(123,'Alt yarıda yoğun boya/jel alanı; tek açılma yönü.'),(110,'Kırpılmış kütle benzerliği; üstte kemer, altta dolu alan.'),(149,'Dört köşe mührü; altında yumurta/taş ve jel hedefi.'),(173,'Kargo + plastik; çamurlu taban, mühür yok.'),(186,'Kullanıcı tasarımı: mıknatıs, dolap ve yeşil sandık.'),(187,'Kullanıcı tasarımı: su yolları, çiçek ve boya.'),(188,'Üç bitişik renk kapısı; gizli kargo ve special çıkış hattı.')]
im=Image.new('RGBA',(1350,1660),r.BG);dr=ImageDraw.Draw(im)
dr.text((25,18),'188 / BENZERLİK VE YAKIN LEVEL KONTROLÜ',font=r.font(28,True),fill=r.GOLD)
dr.text((25,64),'Şekil benzerliği yenilik yüzdesi değildir; açılma sırası ve oyuncunun ilk kararı ayrıca incelendi.',font=r.font(18),fill=r.MUTED)
for j,(number,caption) in enumerate(cards):
    entry=d if number==188 else inventory[number];x=25+j%3*445;y=114+j//3*505
    dr.text((x,y),f'{number} · {entry.get("title",entry["name"])}',font=r.font(24,True),fill=r.GOLD)
    board(im,entry,(x,y+40),32)
    r.paragraph(dr,x,y+410,caption,width=38,size=17)
dr.text((25,1631),'Mevcut sprite’larla statik yerleşimler; otomatik renkler temsili. Oynanış testi değildir.',font=r.font(18),fill=r.MUTED)
im.convert('RGB').save(DOCS/'Reviews/188-Catalog/Comparison.png')
print('Rendered level 188 and 9-board historical comparison.')
