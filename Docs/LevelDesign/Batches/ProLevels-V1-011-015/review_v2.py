"""Read-only historical geometry shortlist using the shared review_level_patterns comparator."""
from pathlib import Path
from collections import Counter
import sys,json
from PIL import Image,ImageDraw,ImageFont
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];sys.path.insert(0,str(ROOT/'Tools'))
import review_level_patterns as pattern
old=pattern.load()
for d in old:d['name']=Path(d['asset']).stem  # historic numbers must not use current V1 names
reports=[];im=Image.new('RGB',(1340,1120),'#19283a');dr=ImageDraw.Draw(im)
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',18)
for row,d in enumerate(json.loads((HERE/'designs.json').read_text())):
    top=d['data']['obstacles'][:]
    for s in d['stacks']:top[s['originCellIndex']]=s['obstacleId']
    target=dict(level=d['n'],name=d['title'],width=9,height=d['h'],data=d['data'],top=top,
                topOrigins=[i if o else -1 for i,o in enumerate(top)],blocked=[o in (4,5,6) for o in top],
                obstacleInstances=dict(Counter(o for o in top if o)))
    ranks=sorted([pattern.compare(target,x) for x in old],key=lambda r:(r['blockerOverlap'],r['maskOverlap']),reverse=True)
    chosen=ranks[:4];reports.append(dict(v1Level=d['n'],historicCatalogueCount=len(old),shortlist=chosen,
        limits='Geometry/reflections shortlist, not originality/quality/pass-rate scores. Historic numbers differ from V1 numbers.'))
    group=[target]+[next(x for x in old if x['level']==r['level']) for r in chosen]
    for col,item in enumerate(group):
        x=18+col*265;y=15+row*277
        dr.text((x,y),f'V1 {d["n"]} / v2' if col==0 else f'Eski {item["level"]} / {item["name"]}',font=font,fill='white')
        pattern.drawboard(im,item,(x,y+32),19)
(HERE/'Repetition-v2.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2)+'\n')
im.save(HERE/'Repetition-v2.png')
print('Read-only geometry shortlist:',len(old),'historic boards per candidate. No level writes.')
