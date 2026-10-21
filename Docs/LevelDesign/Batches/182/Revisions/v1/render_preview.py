"""Actual project sprites, illustrative normal colors; no Unity/gameplay execution."""
from pathlib import Path
import importlib.util,json,re
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
spec=importlib.util.spec_from_file_location('preview',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
d=json.loads((HERE/'design.json').read_text())
im=Image.new('RGBA',(1200,1115),r.BG);draw=ImageDraw.Draw(im)
draw.text((32,25),'182 / Fener Şenliği',font=r.font(40,True),fill=r.GOLD)
draw.text((33,82),'9 × 11 · 28 hamle · 4 renk · Normal',font=r.font(24),fill=r.INK)
draw.text((33,122),'Yeni Lantern / fener tanıtımı · Daralan alt gövdede iki aşamalı ışık deseni',font=r.font(20),fill=r.MUTED)
r.board(im,d,(32,175),70)
x,y=718,176
draw.text((x,y),'HEDEFLER',font=r.font(21,True),fill=r.GOLD);y+=40
lit=r.sprite(r.ref(re.search(r'^    goalIconSprite: (.*)$',r.DEFS[75],re.M)[1]))
for art,label in ((lit,'30 fener'),(r.oid_art(30),'21 porselen')):
    r.paste(im,art,(x,y,54,54));draw.text((x+70,y+14),label,font=r.font(25,True),fill=r.INK);y+=72
y+=18;draw.text((x,y),'OYUN FİKRİ',font=r.font(20,True),fill=r.GOLD);y+=34
y=r.paragraph(draw,x,y,d['decision'],width=37,size=19);y+=24
draw.text((x,y),'FENERİN İKİ AŞAMASI',font=r.font(20,True),fill=r.GOLD);y+=39
r.paste(im,r.oid_art(75),(x,y,106,106));draw.text((x+129,y+34),'→',font=r.font(43,True),fill=r.GOLD);r.paste(im,lit,(x+196,y,106,106));y+=120
draw.text((x,y),'Sönük',font=r.font(20),fill=r.INK);draw.text((x+198,y),'İlk hit: yanar',font=r.font(20),fill=r.INK);y+=39
y=r.paragraph(draw,x,y,'İkinci hit: fener toplanır. Fener ve porselenler hareketlidir; desen açılış yerleşimidir.',width=37,size=18);y+=22
draw.text((x,y),'AÇILMA AKIŞI',font=r.font(20,True),fill=r.GOLD);y+=34
for line in ('Üstte 30 normal taş hücresi.','İlk tam sıra: 9 fener.','Alt bölge: fener–porselen daması.','Alt genişlik: 9 → 7 → 5 sütun.'):
    y=r.paragraph(draw,x,y,line,width=37,size=18)
draw.text((33,983),'81 aktif · 18 hole · 30 başlangıç taşı · Sabit renk veya hazır special yok',font=r.font(21),fill=r.INK)
draw.text((33,1028),'Mevcut sprite’larla tasarım önizlemesi; normal taş renkleri temsili.',font=r.font(19),fill=r.MUTED)
draw.text((33,1061),'28 hamle ilk ayar; geçiş oranı ölçülmedi. 181 ve önceki levellar korundu.',font=r.font(19),fill=r.MUTED)
path=ROOT/'Docs/LevelDesign/Level182-Fener-Senligi.png';im.convert('RGB').save(path);print(path)
