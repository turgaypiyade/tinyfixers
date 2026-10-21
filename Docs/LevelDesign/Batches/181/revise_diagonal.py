"""One-time v3 mask edit requested by the user; retain v2 and all obstacle work."""
from pathlib import Path
import json,re,struct
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
d=json.loads((HERE/'design.json').read_text());assert d['revision']==2
asset=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset')
backup=HERE/'Revisions/v2';assert not backup.exists();backup.mkdir(parents=True)
for p in (asset,Path(str(asset)+'.meta'),HERE/'design.json',HERE/'verify.py',HERE/'validation.json',HERE/'render_preview.py',ROOT/'Docs/LevelDesign/Level181-Basamakli-Arsiv.md',ROOT/'Docs/LevelDesign/Level181-Basamakli-Arsiv.png'):
    (backup/p.name).write_bytes(p.read_bytes())
holes={k*9+k for k in range(9)}
assert all(d['data']['obstacles'][i]==0 and d['data']['obstacleOrigins'][i]==-1 for i in holes)
# Every existing 2x2 footprint is strictly below the diagonal.
assert all(s['originCellIndex']//9 > s['originCellIndex']%9+1 for s in d['stacks'] if s['obstacleId'] in (21,27))
for i in holes:d['data']['cells'][i]=0
d['revision']=3
d['rows']=[''.join('.' if y*9+x in holes else c for x,c in enumerate(row)) for y,row in enumerate(d['rows'])]
d['legend']='. çapraz hole (kullanıcı koordinatı 1,1 → 9,9); W Wardrobe → dört taş → Mud; C ColorChest → Mud; o normal taş.'
d['decision']='Üst sağ üçgende special kurup çapraz boşluğun ötesindeki dolapları aç; alt sol bölümde dolap → taş → çamur ve altı sandığın renk hedeflerini ilerlet.'
d['notes'].append('Kullanıcının açık isteği: sol üstten (1,1), (2,2), ... (9,9) hücrelerini hole yap; board normal komşulukta ikiye ayrılır. 9×11 ölçüsü ve engel/hamle yükü korunur.')
t=asset.read_text();value=struct.pack('<99i',*d['data']['cells']).hex()
t,n=re.subn(r'^  cells:[^\n]*$',lambda m:f'  cells: {value}',t,flags=re.M);assert n==1
asset.write_text(t);(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print('181 v3: 9 diagonal holes; goals, moves, footprints, layers, GUID/name/catalog unchanged.')
