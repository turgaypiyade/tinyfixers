"""Final unapproved draft: asymmetric crest, removing the repetitive paired-wing interior."""
from pathlib import Path
import json, re, struct
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[3]
d=json.loads((HERE/'design.json').read_text()); p=ROOT/d['asset']
archive=HERE/'Revisions/v1-paired-wings'; archive.mkdir(parents=True,exist_ok=True)
if not (archive/p.name).exists():
    (archive/p.name).write_bytes(p.read_bytes())
    for f in ('design.json','Comparison.png'): (archive/f).write_bytes((HERE/f).read_bytes())
    (archive/'Preview.png').write_bytes((ROOT/'Docs/LevelDesign/Level211-Celik-Arma.png').read_bytes())
w=9; size=99; data=d['data']; cells=data['cells']
for k in data:
    if k!='cells': data[k]=[-1 if k=='obstacleOrigins' else 0]*size
walls=[]
def put(x,y,o):
    i=y*w+x; assert cells[i] and not data['obstacles'][i]
    data['obstacles'][i]=o; data['obstacleOrigins'][i]=i
def wall(x,y,bw,bh,o,piece):
    region=[(y+dy)*w+x+dx for dy in range(bh) for dx in range(bw)]; origin=min(region)
    for i in region:
        assert cells[i] and not data['obstacles'][i]
        data['obstacles'][i]=o; data['obstacleOrigins'][i]=origin; data['wallPieceIds'][i]=piece
    walls.append(dict(obstacleId=o,origin=origin,cells=region))
wall(0,2,3,2,55,1); wall(0,4,4,2,55,2)
wall(6,4,3,2,56,3); wall(5,6,3,2,56,4)
for y,xs in [(6,range(1,5)),(7,range(1,5)),(8,range(4,7)),(9,range(4,7)),(10,range(3,6))]:
    for x in xs: put(x,y,28)
for y in (8,9):
    for x in (2,3): put(x,y,25)
stacks=[dict(originCellIndex=74,obstacleId=27,stackOrder=1,initialRemainingHits=0)]
goals=[dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=n)
       for o,n in ((55,2),(56,2),(28,17),(27,1),(25,4))]
t=p.read_text()
for k,a in data.items():
    t,n=re.subn(r'^  '+k+r':[^\n]*$',lambda _:f'  {k}: '+struct.pack('<'+str(len(a))+'i',*a).hex(),t,flags=re.M); assert n==1
for key,entries in [('stackedObstacles',stacks),('goals',goals)]:
    body=f'  {key}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+k+': '+str(v)+'\n' for j,(k,v) in enumerate(e.items())) for e in entries)
    t,n=re.subn(r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda _:body,t,flags=re.M); assert n==1
p.write_text(t)
d.update(walls=walls,stacks=stacks,goals=goals,revision='v2-asymmetric-sash')
(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print('211 v2: asymmetric sash; 2 brick / 2 metal / 17 stone / 1 cabinet / 4 buried mud.')
