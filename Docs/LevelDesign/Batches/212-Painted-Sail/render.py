from pathlib import Path
import copy,json
from PIL import Image,ImageDraw
from common import r,ROOT,HERE,install_art,board,phase
install_art()
levels=r.review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')
d=next(p for p in levels if p['level']==212)
im=Image.new('RGBA',(1360,1080),'#172536');dr=ImageDraw.Draw(im)
dr.text((32,24),'212 · RENKLİ YELKEN / PAINTED SAIL',font=r.art.font(36,True),fill='#ffcf75')
dr.text((34,79),f'NORMAL · 9×11 · {sum(d["data"]["cells"])} aktif hücre · {d["moves"]} hamle · 4 renk',font=r.art.font(23),fill='#e7edf6')
dr.text((34,128),'AÇILIŞ',font=r.art.font(23,True),fill='#ffcf75');board(im,d,(34,173),60)
hidden=phase(d,{42})
dr.text((654,128),'ÇİM KALKINCA',font=r.art.font(23,True),fill='#ffcf75');board(im,hidden,(654,173),43)
for y,s in [(671,'Boya kasaları alt gövdede yan yana.'),(709,'8 çim, boya kasalarının ÜST katmanında.'),(747,'Önce çim açılır, ardından boya kutuları temizlenir.')]:dr.text((654,y),s,font=r.art.font(21),fill='#e7edf6')
dr.text((654,790),'Sağdaki görsel statik katman kesitidir; oynanış anı değildir.',font=r.art.font(17),fill='#a9bdd1')
dr.text((34,862),'HEDEFLER',font=r.art.font(24,True),fill='#ffcf75')
for j,(label,o) in enumerate([('1 Renk Kilitli Kasa',33),('4 Kargo',34),('2 Boya Kasası',63),('2 Boru',26),('14 Çim',42)]):
    x=34+j*260;r.art.paste(im,r.art.oid_art(o),(x,907,57,57));dr.text((x+62,922),label,font=r.art.font(17,True),fill='#e7edf6')
dr.text((34,1004),'Güncel proje sprite’larıyla statik önizleme. Serbest taş renkleri temsili; 6 gerçek başlangıç taşı var.',font=r.art.font(18),fill='#a9bdd1')
dr.text((34,1035),'Hazır special ve yeni mekanik yok. Oynanış dengesi henüz denenmedi.',font=r.art.font(18),fill='#a9bdd1')
im.convert('RGB').save(ROOT/'Docs/LevelDesign/Level212-Renkli-Yelken.png')
report=json.loads((ROOT/'Docs/LevelDesign/Reviews/212-Painted-Sail/Similarity.json').read_text())
near=[p['level'] for p in report['geometryShortlist'][:3]]
cropped=[p['level'] for p in report['croppedShapeShortlist'][:2]]
group=list(dict.fromkeys(near+cropped+[203,209,211,212]))
sheet=Image.new('RGBA',(len(group)*230,690),'#172536');sd=ImageDraw.Draw(sheet)
for j,n in enumerate(group):
    p=next(x for x in levels if x['level']==n)
    sd.text((j*230+8,15),f'{n} · {p["name"][:21]}',font=r.art.font(15,True),fill='#ffcf75')
    board(sheet,p,(j*230+8,57),23);r.review.drawboard(sheet,p,(j*230+8,385),23)
sheet.convert('RGB').save(HERE/'Comparison.png')
print('Rendered 212 opening, lower layers and',group,'comparison. Art warnings:',sorted(r.WARNINGS))
