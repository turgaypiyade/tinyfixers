"""User revision: preserve the sail mask/safe, move paint boxes to the lowest paired 2x2 fit, grass on top."""
from pathlib import Path
import hashlib,json,re,struct
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];A=ROOT/'Assets/_Project'
REV=HERE/'Revisions/v2-grass-over-bottom';P=A/'Settings/ProLevels/LevelV1_00002120.asset'
def enc(a):return struct.pack('<'+str(len(a))+'i',*a).hex()
def main():
    assert not REV.exists(),'Revision already exists: inspect current assets before changing them.'
    REV.mkdir(parents=True)
    for p in (P,Path(str(P)+'.meta'),HERE/'design.json',HERE/'common.py',HERE/'render.py',HERE/'verify.py',HERE/'validation.json',ROOT/'Docs/LevelDesign/Level212-Renkli-Yelken.png',ROOT/'Docs/LevelDesign/Level212-Renkli-Yelken.md'):
        (REV/p.name).write_bytes(p.read_bytes())
    baseline={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in A.rglob('*') if p.is_file() and p!=P}
    (REV/'protected-before.json').write_text(json.dumps(baseline,indent=2)+'\n')
    d=json.loads((HERE/'design.json').read_text());data=d['data'];old=json.loads(json.dumps(d));t=P.read_text()
    # Only the prior eight buried grass cells move. Six upper-sail grass cells stay in place.
    old_paint={56,57,58,59,65,66,67,68}
    for i in old_paint:
        assert data['obstacles'][i]==42;data['obstacles'][i]=0;data['obstacleOrigins'][i]=-1
    new_paint=set()
    for origin in (74,76):
        for dy in (0,1):
            for dx in (0,1):
                i=origin+dy*9+dx;assert data['cells'][i] and data['obstacles'][i]==0
                data['obstacles'][i]=63;data['obstacleOrigins'][i]=origin;new_paint.add(i)
    stacks=[dict(originCellIndex=i,obstacleId=42,stackOrder=1,initialRemainingHits=0) for i in sorted(new_paint)]
    tubes=[dict(originCellIndex=54,direction=3,length=3),dict(originCellIndex=62,direction=2,length=3)]
    data['pinnedTileTypes']=[0]*99
    for i,c in {63:2,64:3,70:1,71:4,93:4,94:2}.items():data['pinnedTileTypes'][i]=c
    def field(k,v):
        nonlocal t
        t,n=re.subn(r'^  '+k+r':[^\n]*$',lambda _:f'  {k}: {v}',t,flags=re.M);assert n==1
    def entries(k,values):
        nonlocal t
        body=f'  {k}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+kk+': '+str(v)+'\n' for j,(kk,v) in enumerate(e.items())) for e in values)
        t,n=re.subn(r'^  '+k+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda _:body,t,flags=re.M);assert n==1
    for k in ('obstacles','obstacleOrigins','pinnedTileTypes'):field(k,enc(data[k]))
    entries('stackedObstacles',stacks);entries('tubes',tubes)
    assert data['cells']==old['data']['cells'] and d['safes']==old['safes'] and d['goals']==old['goals']
    P.write_text(t)
    d.update(stacks=stacks,tubes=tubes,seededHullCells=[i for i,c in enumerate(data['pinnedTileTypes']) if c],revision='v2-grass-over-bottom')
    (HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    print('Preserved sail mask, safe, 24 moves and five goals. Paint origins 74/76, grass above all eight cells; tubes row 7 (1-based).')
if __name__=='__main__':main()
