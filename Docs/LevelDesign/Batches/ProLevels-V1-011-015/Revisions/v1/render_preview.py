"""Current asset sprites; illustrative random normal colors. Not gameplay or a simulation."""
from pathlib import Path
import importlib.util, json, re, sys
from PIL import Image,ImageDraw

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from export_level_previews import int_array,parse_entries
spec=importlib.util.spec_from_file_location('sprites',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
designs=json.loads((HERE/'designs.json').read_text())
validation={v['level']:v for v in json.loads((HERE/'validation.json').read_text())['levels']}
labels={4:'Tek aşamalı sandık',5:'İki aşamalı sandık',6:'Üç aşamalı sandık',25:'Çamur',14:'Yağ (dinamik)'}
for d in designs:
    text=(ROOT/'Assets/_Project/Settings/ProLevels'/(d['asset']+'.asset')).read_text()
    d['data']={k:int_array(text,k,81,-1 if k=='obstacleOrigins' else 0) for k in d['data']}
    d['stacks']=parse_entries(text,'stackedObstacles')
    d['goals']=parse_entries(text,'goals')
    im=Image.new('RGBA',(1240,1000),r.BG);draw=ImageDraw.Draw(im)
    draw.text((32,25),f'V1 / {d["n"]} · {d["title"]}',font=r.font(34,True),fill=r.GOLD)
    draw.text((33,80),f'9 × 9 · {d["moves"]} hamle · 4 renk · {d["difficulty"]} · hedef geçiş {d["targetPassRate"]}',font=r.font(23),fill=r.INK)
    draw.text((33,123),'Mevcut proje sprite’ları · Taş renkleri temsili · Geçiş oranı ölçülmedi',font=r.font(18),fill=r.MUTED)
    r.board(im,d,(32,177),70)
    x,y=710,177
    draw.text((x,y),'HEDEFLER',font=r.font(20,True),fill=r.GOLD);y+=37
    for g in d['goals']:
        oid=g['obstacleId']
        r.paste(im,r.oid_art(oid),(x,y,44,44))
        draw.text((x+58,y+9),f'{g["amount"]} {labels[oid]}',font=r.font(22,True),fill=r.INK);y+=55
    y+=12;draw.text((x,y),'OYUN FİKRİ',font=r.font(19,True),fill=r.GOLD);y+=32
    y=r.paragraph(draw,x,y,d['decision'],width=46,size=19);y+=15
    draw.text((x,y),'AÇILMA AKIŞI',font=r.font(19,True),fill=r.GOLD);y+=32
    for line in d['flow']:y=r.paragraph(draw,x,y,line,width=48,size=17)
    if d['stacks']:
        y+=15;draw.text((x,y),'SANDIKLARIN ALTINDAKİ TABAN',font=r.font(17,True),fill=r.GOLD)
        r.board(im,d,(x,y+32),17,base=True)
    v=validation[d['n']]
    for y,line in [(842,f'81 aktif hücre · {v["openingTileCells"]} açık başlangıç taşı · Hepsi dolum alabilir'),
                   (880,'Hazır special, renk pini veya ilave engel ailesi yok.'),
                   (922,'Statik tasarım önizlemesi; oyun ekran görüntüsü değildir.'),
                   (955,'11 korundu · Unity Play/build, bot veya kaskat simülasyonu çalıştırılmadı.')]:
        draw.text((33,y),line,font=r.font(18),fill=r.MUTED if y>=920 else r.INK)
    im.convert('RGB').save(HERE/f'LevelV1-{d["n"]:03d}.png')

contact=Image.new('RGBA',(1580,650),r.BG);draw=ImageDraw.Draw(contact)
draw.text((25,18),'ProLevels V1 · yalnız 11–15 grubu',font=r.font(31,True),fill=r.GOLD)
draw.text((25,63),'11 mevcut haliyle korundu · 12–14 yeni Normal · 15 yeni Zor · Taş renkleri temsili',font=r.font(19),fill=r.MUTED)
eleven_text=(ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00000110.asset').read_text()
eleven=dict(n=11,title='Yağlı Rampa',h=10,moves=30,difficulty='Korundu',stacks=[],tarps=[],tubes=[],walls=[],data={k:int_array(eleven_text,k,90,-1 if k=='obstacleOrigins' else 0) for k in ['cells','obstacles','obstacleOrigins','pinnedTileTypes','pinnedSpecialTypes']})
for j,d in enumerate([eleven]+designs):
    x=25+j*310
    draw.text((x,109),f'{d["n"]} · {d["title"]}',font=r.font(18,True),fill=r.GOLD)
    draw.text((x,138),f'{d["moves"]} hamle · {d["difficulty"]}',font=r.font(17),fill=r.INK)
    r.board(contact,d,(x,174),29)
    if d['n']>11:
        v=validation[d['n']]
        draw.text((x,492),f'{v["openingTileCells"]} açık taş',font=r.font(18,True),fill=r.INK)
        draw.text((x,523),'Örtü altında çamur' if d['stacks'] else 'Yağ tavanı: 14' if d['n']==14 else 'Geniş, açık avlu',font=r.font(16),fill=r.MUTED)
draw.text((25,580),'Normal hedef %55–85 · Zor hedef %35–55 · Gerçek kazanma oranları henüz ölçülmedi.',font=r.font(19),fill=r.INK)
draw.text((25,614),'Mevcut sprite’larla statik yerleşim; oyun kaydı, bot veya kaskat simülasyonu değildir.',font=r.font(18),fill=r.MUTED)
contact.convert('RGB').save(HERE/'Levels011-015.png')
print('Rendered four detailed V1 previews and 11–15 contact sheet.')
