"""Sprite-based V2 opening previews. Empty gravity-shadowed cells stay honestly empty."""
from pathlib import Path
from textwrap import wrap
import importlib.util,json,random,sys
from PIL import Image,ImageDraw,ImageEnhance

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];sys.path.insert(0,str(HERE))
from verify_v2 import gravity_graph,top_of
sys.path.insert(0,str(ROOT/'Tools'))
from export_level_previews import int_array
spec=importlib.util.spec_from_file_location('project_sprites',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
designs=json.loads((HERE/'render-data-v2.json').read_text())
checks={d['level']:d for d in json.loads((HERE/'validation.json').read_text())['levels']}
labels={4:'Tek aşamalı sandık',5:'İki aşamalı sandık',6:'Üç aşamalı sandık',25:'Çamur',14:'Yağ (dinamik)'}


def board(im,d,origin,cell,plan=False):
    top=top_of(d);spawned=set(d['initialSpawnCells']);pins=d['data']['pinnedSpecialTypes']
    if plan:
        covered={s['originCellIndex'] for s in d['stacks']}
        for i in d['planOpenCells']:top[i]=25 if i in covered else 0
        spawned=gravity_graph(d,top)['spawned']
    dr=ImageDraw.Draw(im);rng=random.Random(d['n']);colors={}
    for i,active in enumerate(d['data']['cells']):
        if not active:continue
        x=origin[0]+i%9*cell;y=origin[1]+i//9*cell;o=top[i]
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//9),fill='#3b4b66',outline='#7485a1')
        if o in (14,25):r.paste(im,r.oid_art(o),(x,y,cell,cell))
        if o in (4,5,6):r.paste(im,r.oid_art(o),(x,y,cell,cell),cell*.012)
        elif i in spawned:
            special=pins[i] if not plan else 0
            if special:
                r.paste(im,r.SPECIALS[special],(x,y,cell,cell),cell*.03)
                dr.rounded_rectangle((x+2,y+2,x+cell-2,y+cell-2),radius=max(2,cell//9),outline=r.GOLD,width=max(1,round(cell/28)))
            else:
                choices=list(range(4));rng.shuffle(choices)
                for color in choices:
                    if i%9>=2 and colors.get(i-1)==colors.get(i-2)==color:continue
                    if i//9>=2 and colors.get(i-9)==colors.get(i-18)==color:continue
                    break
                colors[i]=color
                art=r.TILES[color] if o!=14 else ImageEnhance.Brightness(r.TILES[color]).enhance(.65)
                r.paste(im,art,(x,y,cell,cell),cell*.055)
        if plan and i in d['planOpenCells']:
            dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//9),outline=r.GOLD,width=2)


for d in designs:
    n=d['n'];v=checks[n];im=Image.new('RGBA',(1280,1180),r.BG);dr=ImageDraw.Draw(im)
    dr.text((32,24),f'V1 / {n} · {d["title"]} · yeni tasarım v2',font=r.font(32,True),fill=r.GOLD)
    dr.text((33,77),f'9 × {d["h"]} · {d["moves"]} hamle · {d["difficulty"]} · hedef geçiş {d["targetPassRate"]}',font=r.font(22),fill=r.INK)
    dr.text((33,119),'Altın çerçeve = hazır special · Boş hücreler, kapak açılınca dolacak gerçek alanlar',font=r.font(18),fill=r.MUTED)
    board(im,d,(32,174),66)
    x,y=680,174
    dr.text((x,y),'BU LEVELİN OLAYI',font=r.font(20,True),fill=r.GOLD);y+=34
    y=r.paragraph(dr,x,y,d['decision'],width=50,size=19);y+=18
    dr.text((x,y),'HEDEFLER',font=r.font(19,True),fill=r.GOLD);y+=34
    for g in d['goals']:
        o=g['obstacleId'];r.paste(im,r.oid_art(o),(x,y,36,36))
        dr.text((x+48,y+7),f'{g["amount"]} {labels[o]}',font=r.font(20,True),fill=r.INK);y+=45
    y+=16;dr.text((x,y),'ŞEKİL VE AÇILMA',font=r.font(19,True),fill=r.GOLD);y+=32
    for line in d['flow']:y=r.paragraph(dr,x,y,line,width=53,size=17)
    y+=18;dr.text((x,y),'İLK AÇILMA PLANI',font=r.font(18,True),fill=r.GOLD);y+=32
    board(im,d,(x,y),19,plan=True)
    tx=x+192;ty=y
    for line in [f"Dolum bağlı hücre: {v['spawnConnectedBefore']} → {v['spawnConnectedAfterDeclaredOpening']}",
                 'Altın kenar: açılması planlanan kapak.',
                 'Statik yerleşim planı; gerçek hamle/kaskat sonucu değil.']:
        ty=r.paragraph(dr,tx,ty,line,width=29,size=17)
    bottom=max(926,174+66*d['h']+24)
    for line in [f"{v['active']} aktif · {v['holes']} hole · {v['initialOrdinaryTiles']} normal taş + {len(v['preparedSpecials'])} hazır special",
                 f"{len(v['intentionallyEmptyCells'])} hücre açılışta boş; taşla doldurulmuş gibi çizilmedi.",
                 'Yalnız mevcut engeller; yeni runtime/VFX veya kural kodu yok.',
                 'Taş renkleri temsili. Unity Play/build veya bot simülasyonu yok.']:
        dr.text((33,bottom),line,font=r.font(17),fill=r.MUTED);bottom+=31
    dr.text((33,1138),'İnceleme taslağı · Geçiş oranı ve cihaz FPS’i henüz ölçülmedi · 11 korundu',font=r.font(18),fill=r.INK)
    im.convert('RGB').save(HERE/f'LevelV1-{n:03d}.png')

contact=Image.new('RGBA',(1580,790),r.BG);dr=ImageDraw.Draw(contact)
dr.text((25,18),'ProLevels V1 · 11–15 / yeni tasarım v2',font=r.font(31,True),fill=r.GOLD)
dr.text((25,65),'11 korundu · Pencere / odalar / yağlı geçiş / üç bent · Boş hücreler bilinçli açılma alanı',font=r.font(18),fill=r.MUTED)
t=(ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00000110.asset').read_text()
e=dict(n=11,h=10,moves=30,title='Yağlı Rampa',difficulty='Korundu',stacks=[],data={k:int_array(t,k,90,-1 if k=='obstacleOrigins' else 0) for k in ['cells','obstacles','obstacleOrigins','pinnedTileTypes','pinnedSpecialTypes']})
e['initialSpawnCells']=sorted(gravity_graph(e,top_of(e))['spawned'])
for j,d in enumerate([e]+designs):
    x=25+j*310
    dr.text((x,110),f'{d["n"]} · {d["title"]}',font=r.font(18,True),fill=r.GOLD)
    dr.text((x,139),f'{d["moves"]} hamle · {d["difficulty"]}',font=r.font(17),fill=r.INK)
    board(contact,d,(x,177),29)
    if d['n']>11:
        yy=519
        for line in wrap(d['cue'],width=29):dr.text((x,yy),line,font=r.font(17,True),fill=r.INK);yy+=25
        v=checks[d['n']]
        dr.text((x,632),f"{v['active']} aktif / {v['holes']} hole",font=r.font(16),fill=r.MUTED)
dr.text((25,694),'Normal hedef %55–85 · Zor hedef %35–55 · İlk hamle bütçeleri; ölçülmüş kazanma oranı değil.',font=r.font(19),fill=r.INK)
dr.text((25,733),'Mevcut sprite’larla statik açılış planı. Taş renkleri temsili; Unity oyun görüntüsü veya simülasyon değildir.',font=r.font(17),fill=r.MUTED)
contact.convert('RGB').save(HERE/'Levels011-015.png')
print('V2 actual-sprite previews rendered; shadowed cells left empty; structural after-opening plans labelled.')
