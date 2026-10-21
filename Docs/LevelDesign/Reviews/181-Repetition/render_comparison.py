"""Selected historical boards with current sprites; normal colors illustrative."""
from pathlib import Path
import importlib.util,json,re,sys
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import array
spec=importlib.util.spec_from_file_location('render',ROOT/'Docs/LevelDesign/Batches/173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
inventory={d['level']:d for d in json.loads((HERE/'Catalog-Inventory.json').read_text())}
cards=[(7,'Sol kenardan büyüyen çapraz basamak.'),(23,'Üst açık alan + sol/alt engel kütlesi.'),(48,'Açık köşe + katmanlı L bölgesi.'),(68,'Sol kenar basamağı; sağda borular.'),(121,'ColorChest + Wardrobe + oyma taşı.'),(181,'REDDEDİLEN TASLAK: aynı fikir ailesi.')]
im=Image.new('RGBA',(1380,1250),r.BG);draw=ImageDraw.Draw(im)
draw.text((25,18),'181 / MEVCUT LEVELLARLA TEKRAR İNCELEMESİ',font=r.font(29,True),fill=r.GOLD)
draw.text((25,61),'Birebir kopya iddiası değil: tekrar eden siluet, çalışma alanı ve engel ilişkileri.',font=r.font(20),fill=r.MUTED)
for j,(n,caption) in enumerate(cards):
    d=inventory[n];x=25+j%3*455;y=112+j//3*548
    draw.text((x,y),f'{n} · {d["name"]}',font=r.font(24,True),fill=r.GOLD)
    source=ROOT/d['asset']
    snapshot=ROOT/'Docs/LevelDesign/Batches/181/Revisions/v1/LevelP_001850.asset'
    if n==181 and snapshot.exists():source=snapshot
    text=source.read_text();data=dict(cells=d['data']['cells'],obstacles=d['top'],obstacleOrigins=d['topOrigins'])
    data.update({k:array(text,k,d['width']*d['height']) for k in ('pinnedTileTypes','pinnedSpecialTypes')})
    projected=dict(n=n,data=data,stacks=[],tarps=d['custom']['tarpStatues'],tubes=d['custom']['tubes'],walls=[])
    r.board(im,projected,(x,y+40),37)
    r.paragraph(draw,x,y+462,caption,width=39,size=18)
draw.text((25,1220),'Mevcut sprite’larla şema; otomatik taş renkleri temsili. Oyun/build/simülasyon çalıştırılmadı.',font=r.font(18),fill=r.MUTED)
im.convert('RGB').save(HERE/'Comparison.png')
print(HERE/'Comparison.png')
