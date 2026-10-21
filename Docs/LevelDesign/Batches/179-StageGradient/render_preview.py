"""Render the current user-edited 179 and refresh only its contact-sheet card."""
from pathlib import Path
import importlib.util,json
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];OUT=ROOT/'Docs/LevelDesign'
spec=importlib.util.spec_from_file_location('preview',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
d=json.loads((HERE/'design.json').read_text())
contact=Image.open(OUT/'Levels173-180-Onizleme.png').convert('RGBA')
r.DESIGNS=[d];r.render()
x,y=30+2*370,119+515;dr=ImageDraw.Draw(contact)
dr.rectangle((x,y,x+355,y+505),fill=r.BG)
dr.text((x,y),f'179 · {d["title"]}',font=r.font(23,True),fill=r.GOLD)
dr.text((x,y+33),f'{d["moves"]} hamle · Super Hard',font=r.font(18),fill=r.INK)
r.board(contact,d,(x,y+73),34)
r.paragraph(dr,x,y+458,' · '.join(r.goal_lines(d)),width=39,size=15,color=r.MUTED)
contact.convert('RGB').save(OUT/'Levels173-180-Onizleme.png')
print('179 initial stages 3/2/1/1 and user special pins rendered; other contact cards retained.')
