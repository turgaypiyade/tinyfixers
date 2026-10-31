"""Actual project sprites and honest static spawn shadows; no gameplay simulation."""
from pathlib import Path
import ast,importlib.util,json,random,re,math,sys
from PIL import Image,ImageDraw,ImageOps
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(HERE))
from designs import DESIGNS
from audit import surface,entry
spec=importlib.util.spec_from_file_location('project_art',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
# Reuse only the existing seal-art functions, without rerunning that batch's renderer.
source=ast.parse((HERE.parent/'188/render_preview.py').read_text())
functions=[n for n in source.body if isinstance(n,ast.FunctionDef) and n.name in ('sliced_body','seal_art')]
scope=dict(r=r,Image=Image,ImageDraw=ImageDraw,ROOT=ROOT,re=re,math=math)
exec(compile(ast.Module(body=functions,type_ignores=[]),'existing_seal_art','exec'),scope)
seal_art=scope['seal_art']
magnet_prefab=(ROOT/'Assets/_Project/Prefabs/FX/Magnet/MagnetView.prefab').read_text()
magnet=r.sprite(r.ref(re.search(r'^  magnetSprite: (.+)$',magnet_prefab,re.M)[1]))
tube=r.sprite(r.ref(re.search(r'^  straightTubeSprite: (.+)$',magnet_prefab,re.M)[1]))

def board(im,d,xy,cell,opened=False):
    top,orig=surface(d,d['opening'] if opened else ())
    status=entry(d,top);dr=ImageDraw.Draw(im);rng=random.Random(d['n']);colors={}
    for i,a in enumerate(d['data']['cells']):
        if not a:continue
        x,y=xy[0]+i%9*cell,xy[1]+i//9*cell
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//8),fill='#485b79',outline='#7587a2')
        if d['data']['obstacles'][i]==46 and top[i] in (0,46):r.paste(im,r.oid_art(46),(x,y,cell,cell))
    seeded=set(d.get('seededTileCells',[]))
    for i in sorted(status['reached']|seeded):
        x,y=xy[0]+i%9*cell,xy[1]+i//9*cell
        special=d['data']['pinnedSpecialTypes'][i] if not opened else 0
        if special:
            r.paste(im,r.SPECIALS[special],(x,y,cell,cell),cell*.025)
            dr.rounded_rectangle((x+2,y+2,x+cell-2,y+cell-2),radius=max(2,cell//9),outline=r.GOLD,width=2)
            continue
        if i in seeded or i in d.get('colorBoundaryCells',[]):choices=[d['data']['pinnedTileTypes'][i]-1]
        else:choices=list(range(4));rng.shuffle(choices)
        color=next((c for c in choices if not(i%9>=2 and colors.get(i-1)==colors.get(i-2)==c) and not(i//9>=2 and colors.get(i-9)==colors.get(i-18)==c)),choices[0])
        colors[i]=color;r.paste(im,r.TILES[color],(x,y,cell,cell),cell*.05)
    for i,o in enumerate(top):
        if not o or not d['data']['cells'][i] or o in (46,29,64):continue
        if orig[i]!=i:continue
        x,y=xy[0]+i%9*cell,xy[1]+i//9*cell
        r.paste(im,r.oid_art(o),(x,y,cell,cell),cell*.015)
    for m in d['magnets']:
        path=m['pathCellIndices']
        for k,i in enumerate(path):
            if top[i]!=29:continue
            x,y=xy[0]+i%9*cell,xy[1]+i//9*cell
            if k in (0,len(path)-1):
                art=magnet.rotate(90 if k==0 else -90,expand=True)
                r.paste(im,art,(x,y,cell,cell))
            else:r.paste(im,tube,(x,y,cell,cell))
    for s in d['ancientSeals']:
        i=s['originCellIndex'];im.alpha_composite(seal_art(s,cell),(xy[0]+i%9*cell,xy[1]+i//9*cell))
    if opened:
        for i in d['opening']:
            x,y=xy[0]+i%9*cell,xy[1]+i//9*cell
            dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//8),outline=r.GOLD,width=max(1,cell//15))

def para(draw,x,y,text,width=49,size=19):return r.paragraph(draw,x,y,text,width=width,size=size)

def render():
    for d in DESIGNS:
        im=Image.new('RGBA',(1360,1200),r.BG);dr=ImageDraw.Draw(im);g=entry(d,surface(d)[0])
        dr.text((30,22),f'V1 / {d["n"]} · {d["title"]}',font=r.font(36,True),fill=r.GOLD)
        dr.text((31,77),f'9 × {d["h"]} · {d["moves"]} hamle · 4 renk · {d["difficulty"]}',font=r.font(24),fill=r.INK)
        subtitle='Güncel proje sprite’ları · İçteki 18 taş assette tanımlı; dış renkler temsili' if d['n']==199 else 'Güncel proje sprite’ları · Taş renkleri temsili · Boş odalar gerçekten boş gösterildi'
        dr.text((31,119),subtitle,font=r.font(18),fill=r.MUTED)
        board(im,d,(30,175),68)
        x,y=695,175;dr.text((x,y),'OYUN FİKRİ',font=r.font(21,True),fill=r.GOLD);y+=36
        y=para(dr,x,y,d['idea'],width=51,size=20);y+=18
        if d['n']<199:
            dr.text((x,y),f'Toplam rakip canı {d["hp"]} · Oyuncu canı {d["playerHp"]}',font=r.font(20,True),fill=r.INK);y+=33
            dr.text((x,y),'Üç rakip · Hasar yalnız taşlardan · Yan temizlik hedefi yok',font=r.font(17),fill=r.MUTED);y+=42
        else:
            dr.text((x,y),'Hedefler: 87 jel hücresi · 17 pusula · 1 mühür',font=r.font(20,True),fill=r.INK);y+=43
        for j,text in enumerate(d['beats'],1):
            y=para(dr,x,y,f'{j}. {text}',width=56,size=18);y+=15
        y=max(y,650);dr.text((x,y),'AÇILMA PLANI',font=r.font(19,True),fill=r.GOLD);y+=33
        board(im,d,(x,y),26,opened=True)
        yy=y+10
        for text in ['Altın kenar: açılması incelenen hücreler.',
                     f'Dolum bağlı normal yuvalar: {len(g["reached"])} → {len(entry(d,surface(d,d["opening"])[0])["reached"])}.',
                     'Sonraki taş renkleri temsili; gerçek hamle sonucu değil.']:
            yy=para(dr,x+255,yy,text,width=28,size=17);yy+=14
        for j,line in enumerate([
            f'{sum(d["data"]["cells"])} aktif / {9*d["h"]-sum(d["data"]["cells"])} hole · {len(g["empty"]-set(d.get("seededTileCells",[])))} açılışta dolmayan hücre · Yeni runtime/VFX kodu yok.',
            'Altın çerçeveli TNT yalnız 199’da hazır; bosslarda special üretimini oyuncu kurar.',
            'Statik tasarım taslağı. Hamleler ilk ayar; kazanma oranı ve cihaz performansı ölçülmedi.',
            'Unity Play/build, bot veya kaskat simülasyonu çalıştırılmadı. Önceki levellar korundu.']):
            dr.text((31,1046+j*32),line,font=r.font(18),fill=r.MUTED if j else r.INK)
        im.convert('RGB').save(HERE/f'LevelV1-{d["n"]:03d}.png')
    im=Image.new('RGBA',(1610,880),r.BG);dr=ImageDraw.Draw(im)
    dr.text((28,23),'Doğadan üç oyun fikri · ProLevels V1',font=r.font(34,True),fill=r.GOLD)
    dr.text((28,75),'90 dalga / genişleyen kıvrım    ·    100 yaprak / mıknatıs damarı    ·    199 deniz kabuğu / katmanlı spiral',font=r.font(19),fill=r.MUTED)
    captions=['Kayalık kıvrımı kır; alanı genişlet.','Yan damarı aç; mıknatıs uçlarını birleştir.','Önce mührü aç; altındaki dört jelden yayıl.']
    for j,d in enumerate(DESIGNS):
        x=28+j*530
        dr.text((x,126),f'{d["n"]} · {d["title"]}',font=r.font(27,True),fill=r.GOLD)
        dr.text((x,166),f'{d["moves"]} hamle · {d["difficulty"]}',font=r.font(20),fill=r.INK)
        board(im,d,(x,216),48)
        para(dr,x,760,captions[j],width=43,size=19)
    dr.text((28,838),'Statik önizleme · Dış renkler temsili, 199’un iç taşları assetten · Denge henüz ölçülmedi',font=r.font(20),fill=r.MUTED)
    im.convert('RGB').save(HERE/'Levels090-100-199.png')
    print('Rendered three individual previews and one contact sheet; no production assets written.')

if __name__=='__main__':render()
