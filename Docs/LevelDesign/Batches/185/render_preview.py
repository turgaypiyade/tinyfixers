"""Actual sprite preview of the horizontally mirrored, row-striped reference design."""
from pathlib import Path
import importlib.util,json,re
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];OUT=ROOT/'Docs/LevelDesign'
spec=importlib.util.spec_from_file_location('p',HERE.parent/'173-180/render_previews.py');r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
d=json.loads((HERE/'design.json').read_text());im=Image.new('RGBA',(1220,1130),r.BG);dr=ImageDraw.Draw(im)
dr.text((32,25),'185 / Işık Merdiveni',font=r.font(40,True),fill=r.GOLD)
dr.text((33,82),'9 × 11 · 32 hamle · 4 renk · Hard',font=r.font(24),fill=r.INK)
dr.text((33,122),'Referansın yatay aynası · Fener / PlasticTwoStage satırları · Oyma taşı şeritleri',font=r.font(19),fill=r.MUTED)
r.board(im,d,(32,175),68)
x,y=706,176;dr.text((x,y),'HEDEFLER',font=r.font(21,True),fill=r.GOLD);y+=36
lit=r.sprite(r.ref(re.search(r'^    goalIconSprite: (.*)$',r.DEFS[75],re.M)[1]))
for art,label in ((lit,'24 fener'),(r.oid_art(40),'21 PlasticTwoStage'),(r.oid_art(28),'9 oyma taşı'),(r.oid_art(39),'12 pusula · yalnız special')):
    r.paste(im,art,(x,y,53,53));dr.text((x+67,y+13),label,font=r.font(23,True),fill=r.INK);y+=67
y+=13;dr.text((x,y),'OYUN FİKRİ',font=r.font(20,True),fill=r.GOLD);y+=33
y=r.paragraph(dr,x,y,d['decision'],width=39,size=19);y+=22
dr.text((x,y),'YATAY ŞERİTLER',font=r.font(20,True),fill=r.GOLD);y+=35
for oid,label in ((75,'Fener: 2 vuruş'),(40,'PlasticTwoStage: 2 vuruş'),(28,'Oyma taşı: 3 vuruş')):
    for j in range(3):r.paste(im,r.oid_art(oid),(x+j*40,y,38,38))
    dr.text((x+137,y+8),label,font=r.font(17),fill=r.INK);y+=52
y+=13;dr.text((x,y),'AYNALAMA',font=r.font(20,True),fill=r.GOLD);y+=34
y=r.paragraph(dr,x,y,'Ana merdiven sola iner; kıvrımlı küçük bölge sağ altta. Boşluklar ve üç taş şeridi aynalandı.',width=39,size=18);y+=15
y=r.paragraph(dr,x,y,'Açılışta 1 TNT. Alt bölge için geçici seçim: special isteyen pusulalar.',width=39,size=18)
dr.text((33,966),'78 aktif · 21 hole · 12 başlangıç taş hücresi · 1 hazır TNT',font=r.font(21),fill=r.INK)
dr.text((33,1012),'Mevcut proje sprite’ları; normal renkler temsili. Açılış şeritleri gravity ile değişebilir.',font=r.font(18),fill=r.MUTED)
dr.text((33,1050),'Görsel taslak; ortak board köşe/yuvarlatma kodu ve eski levellar korundu.',font=r.font(18),fill=r.MUTED)
dr.text((33,1086),'32 hamle ilk ayar; Unity Play/build, bot veya kaskat simülasyonu yapılmadı.',font=r.font(18),fill=r.MUTED)
im.convert('RGB').save(OUT/'Level185-Isik-Merdiveni.png');print('Rendered 185 Işık Merdiveni.')
