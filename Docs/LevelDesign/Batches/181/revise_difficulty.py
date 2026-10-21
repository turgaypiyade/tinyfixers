"""One-time v2 authoring; retain v1 review evidence and the asset GUID/name."""
from pathlib import Path
from collections import Counter
import copy,json,re,struct,sys
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import footprint
d=json.loads((HERE/'design.json').read_text());assert d.get('revision',1)==1
backup=HERE/'Revisions/v1';assert not backup.exists();backup.mkdir(parents=True)
asset=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset')
for p in (asset,Path(str(asset)+'.meta'),HERE/'design.json',HERE/'verify.py',HERE/'validation.json',HERE/'render_preview.py',ROOT/'Docs/LevelDesign/Level181-Basamakli-Arsiv.md',ROOT/'Docs/LevelDesign/Level181-Basamakli-Arsiv.png'):
    (backup/p.name).write_bytes(p.read_bytes())
d.update(revision=2,moves=26,rows=['ooooooooo']*3+['WWooooooo']*2+['CCWWooooo']*2+['CCCCWWooo']*2+['CCCCCCWWo']*2)
d['data']={k:([1]*99 if k=='cells' else [-1]*99 if k=='obstacleOrigins' else [0]*99) for k in d['data']}
d['stacks']=[];d['goals']=[]
for y in (3,5,7,9):
    edge=y-3
    for x in range(0,edge+1,2):
        oid=27 if x==edge else 21;origin=y*9+x
        for dy in range(2):
            for dx in range(2):
                i=(y+dy)*9+x+dx
                assert not d['data']['obstacles'][i]
                d['data']['obstacles'][i]=25;d['data']['obstacleOrigins'][i]=i
                if oid==27:d['stacks'].append(dict(originCellIndex=i,obstacleId=28,stackOrder=1))
        d['stacks'].append(dict(originCellIndex=origin,obstacleId=oid,stackOrder=2 if oid==27 else 1))
counts=Counter(o for o in d['data']['obstacles'] if o)
counts.update(s['obstacleId'] for s in d['stacks'])
assert counts=={21:6,27:4,28:16,25:40}
for oid in (21,27,28,25):d['goals'].append(dict(targetType=1,tileType=0,obstacleId=oid,collectibleId=0,iconOverride='{fileID: 0}',amount=counts[oid]))
d['decision']='Dört dolabın önünü açacak special hatları kur; dolap → oyma taşı → çamur zincirini ilerletirken içteki altı sandığın kalan renklerini tamamla.'
d['flow']=['4 dolap: kapak, ardından toplam 40 eşya.','6 eski ColorChest: kapak, ardından toplam 24 renk.','Dört dolabın altında 16 üç aşamalı oyma taşı.','Bütün basamağın altında 40 iki aşamalı çamur.']
d['legend']='W Wardrobe → dört SculptingStone → dört Mud; C eski ColorChest → dört Mud; o otomatik normal taş. Hole yok.'
d['notes']=['Kullanıcı şekle büyük itirazı olmadığını, ana sorunun 181 için kolaylık olduğunu açıkladı. Şekil ailesi korunup yük artırıldı.','59 açık başlangıç taşı; pin/hazır special/yardımcı yok. 26 hamle ilk denge ayarı, oran ölçülmedi.']
t=asset.read_text()
for key,value in dict(moves=26,**{k:struct.pack('<99i',*a).hex() for k,a in d['data'].items()}).items():
    t,n=re.subn(r'^  '+key+r':[^\n]*$',lambda m:f'  {key}: {value}',t,flags=re.M);assert n==1
for key,values in dict(goals=d['goals'],stackedObstacles=d['stacks']).items():
    body=f'  {key}:\n'+''.join(''.join(f'  - {k}: {v}\n' if j==0 else f'    {k}: {v}\n' for j,(k,v) in enumerate(row.items())) for row in values)
    t,n=re.subn(r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda m:body,t,flags=re.M);assert n==1
asset.write_text(t);(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print('181 v2 authored. Asset GUID, name and catalog unchanged. v1 snapshot:',backup)
