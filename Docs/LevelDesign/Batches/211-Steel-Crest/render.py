"""Render current project sprites, including authored wall origins. Static preview, not gameplay."""
from pathlib import Path
import copy, importlib.util, json, sys
from PIL import Image, ImageDraw
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[3]
spec=importlib.util.spec_from_file_location('review_art',HERE.parent/'131-205-Review-2026-10-31/render.py')
r=importlib.util.module_from_spec(spec); spec.loader.exec_module(r)
levels=r.review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')
d=next(x for x in levels if x['level']==211)
def draw_board(target,level,xy,cell):
    r.board(target,level,xy,cell)
    # The shared historical renderer omits the brick wall's raised stage-zero details.
    # Match WallPieceView's current resources; the exact random mirroring is illustrative.
    for i,o in enumerate(level['top']):
        if o!=55: continue
        asset=r.art.resource('Wall','WallDetail_Kabartma'+str(1+i%2))
        r.art.paste(target,asset,(xy[0]+i%level['width']*cell,xy[1]+i//level['width']*cell,cell,cell),cell*.14)
im=Image.new('RGBA',(1360,1080),'#172536'); dr=ImageDraw.Draw(im)
dr.text((32,24),'211 · ÇELİK ARMA / STEEL CREST',font=r.art.font(37,True),fill='#ffcf75')
dr.text((34,78),'NORMAL · 9×11 · 75 aktif hücre · 20 hamle · 4 renk',font=r.art.font(23),fill='#e7edf6')
dr.text((34,128),'AÇILIŞ',font=r.art.font(23,True),fill='#ffcf75')
draw_board(im,d,(34,173),60)
hidden=copy.deepcopy(d)
hidden['top']=hidden['data']['obstacles'][:]; hidden['topOrigins']=hidden['data']['obstacleOrigins'][:]
dr.text((654,128),'DOLABIN ALTINDA',font=r.art.font(23,True),fill='#ffcf75')
draw_board(im,hidden,(654,173),43)
dr.text((654,671),'Dipteki 2×2 dolabın altında 4 çamur.',font=r.art.font(21),fill='#e7edf6')
dr.text((654,709),'Solda kiremit, sağda çapraz metal kuşak.',font=r.art.font(21),fill='#e7edf6')
dr.text((654,747),'Sol yolu aç veya sağda ardışık vuruşa odaklan.',font=r.art.font(21),fill='#e7edf6')
dr.text((654,790),'Sağdaki görsel katman kesitidir; bir oyun hamlesi değildir.',font=r.art.font(17),fill='#a9bdd1')
dr.text((34,862),'HEDEFLER',font=r.art.font(24,True),fill='#ffcf75')
goals=[('2 Duvar',55),('2 Metal Duvar',56),('17 Oyma Taşı',28),('1 Dolap',27),('4 Çamur',25)]
for j,(label,o) in enumerate(goals):
    x=34+j*260
    r.art.paste(im,r.art.oid_art(o),(x,907,57,57))
    dr.text((x+65,922),label,font=r.art.font(19,True),fill='#e7edf6')
dr.text((34,1004),'Güncel proje sprite’larıyla statik önizleme. Taş renkleri temsili; oynanmış denge veya oyun ekranı değildir.',font=r.art.font(18),fill='#a9bdd1')
dr.text((34,1035),'Hazır special / sabit renk / yeni mekanik yok. Önceki 210 level korunur.',font=r.art.font(18),fill='#a9bdd1')
im.convert('RGB').save(ROOT/'Docs/LevelDesign/Level211-Celik-Arma.png')
# Compare both the nearest historical boards and the most recent visual vocabulary.
group=[183,144,114,203,207,208,211]
sheet=Image.new('RGBA',(7*240,700),'#172536'); sd=ImageDraw.Draw(sheet)
for j,n in enumerate(group):
    p=next(x for x in levels if x['level']==n)
    sd.text((j*240+8,15),f'{n} · {p["name"][:21]}',font=r.art.font(15,True),fill='#ffcf75')
    draw_board(sheet,p,(j*240+8,57),24)
    sd.text((j*240+8,349),'Katman / engel silueti',font=r.art.font(15),fill='#a9bdd1')
    r.review.drawboard(sheet,p,(j*240+8,385),23)
sheet.convert('RGB').save(HERE/'Comparison.png')
print('Rendered opening, buried mud and seven-board visual comparison. Art warnings:',sorted(r.WARNINGS))
