"""Shortlist visual review using the active catalog, including historical 1–170 references."""
import importlib.util,json
from PIL import Image,ImageDraw
from design import HERE,ROOT,CAT
import review_level_patterns as review
spec=importlib.util.spec_from_file_location('historical_board_renderer',HERE.parent/'131-205-Review-2026-10-31/render.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
levels={d['level']:d for d in review.load(CAT)}
chosen={222:[221,103],223:[94,120],224:[111,170],225:[148,101]}
for n,neighbors in chosen.items():
    im=Image.new('RGBA',(1700,1060),'#172536');dr=ImageDraw.Draw(im)
    for j,number in enumerate([n]+neighbors):
        d=levels[number];x=24+j*560
        dr.text((x,24),f'{number} · {d["name"]}',font=r.art.font(24,True),fill='#ffcf75')
        dr.text((x,62),('ADAY' if j==0 else 'TARİHSEL BENZER'),font=r.art.font(19),fill='#cbd8e5')
        r.board(im,d,(x,112),49)
    dr.text((24,930),'Gerçek proje sprite’ları; serbest renkler temsili. Karşılaştırma: şekil, açık alan, ilk karar ve sonraki iş.',font=r.art.font(22),fill='#cbd8e5')
    dr.text((24,970),'Eski genel renderer özel Dimerdal kepenk katmanlarını çizmez; yeni adayın ayrıntılı PNG’si teslim önizlemesidir.',font=r.art.font(20),fill='#a9bdd1')
    im.convert('RGB').save(HERE/f'Comparison{n}.png')
(HERE/'comparison-shortlist.json').write_text(json.dumps(chosen,indent=2)+'\n')
print('Four candidate/history sprite comparison sheets created.')
