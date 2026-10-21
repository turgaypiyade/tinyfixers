"""v4: connect the diagonal via upper-side bridge holes; shared rendering stays intact."""
from pathlib import Path
import hashlib,json,re,struct
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
d=json.loads((HERE/'design.json').read_text());assert d['revision']==3
asset=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset')
backup=HERE/'Revisions/v3';assert not backup.exists();backup.mkdir(parents=True)
for p in (asset,Path(str(asset)+'.meta'),HERE/'design.json',HERE/'verify.py',HERE/'validation.json',HERE/'render_preview.py',ROOT/'Docs/LevelDesign/Level181-Basamakli-Arsiv.md',ROOT/'Docs/LevelDesign/Level181-Basamakli-Arsiv.png'):
    (backup/p.name).write_bytes(p.read_bytes())
# Keep all lower-side matching pockets and all existing obstacles. Remove eight upper-side bridges.
extra={k*9+k+1 for k in range(8)}
assert all(d['data']['cells'][i] and not d['data']['obstacles'][i] and d['data']['obstacleOrigins'][i]==-1 for i in extra)
for i in extra:d['data']['cells'][i]=0
d['revision']=4
d['rows']=[''.join('.' if not d['data']['cells'][y*9+x] else c for x,c in enumerate(row)) for y,row in enumerate(d['rows'])]
d['legend']='. kenardan bağlı çapraz hole şeridi; W Wardrobe → dört taş → Mud; C ColorChest → Mud; o normal taş.'
d['notes'].append('v4: Köşeden değen 9 delik, üst-sağ taraftan 8 ara hücre eklenerek kenardan bağlı 17 hücrelik tek boşluk şeridine çevrildi. Ortak köşe/yuvarlatma kodu korunur.')
# Guard scene/art/prefab inputs as well as runtime scripts; no renderer mutation is needed.
paths=[p for folder in ('Assets/_Project/Scripts/Grid','Assets/_Project/Settings/BorderThemeSet','Assets/_Project/Prefabs/Grid','Assets/_Project/Scenes') for p in (ROOT/folder).rglob('*') if p.is_file()]
(HERE/'border-protected-v4.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},indent=2)+'\n')
t=asset.read_text();value=struct.pack('<99i',*d['data']['cells']).hex()
t,n=re.subn(r'^  cells:[^\n]*$',lambda m:f'  cells: {value}',t,flags=re.M);assert n==1
asset.write_text(t);(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
ref=ROOT/'Docs/LevelDesign/References/11-Level181-Ayrik-Capraz-Hole.png'
ref.write_bytes(Path('/var/folders/54/y51scw3j7cj3198kjy1bv4cw0000gn/T/codex-clipboard-NAU8ns.png').read_bytes())
print('v4 mask saved: 17 edge-connected holes, 82 active cells; shared border code and settings preserved.')
