"""Scoped authoring, static checks and backups. No Unity/game execution."""
from pathlib import Path
import re, struct, json, hashlib, shutil
ROOT=Path(__file__).resolve().parents[4]; HERE=Path(__file__).resolve().parent
PRO=ROOT/'Assets/_Project/Settings/ProLevels'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
CAT=PRO/'LevelCatalogProV1.asset'
def block(s,k):
 m=re.search(r'^  '+re.escape(k)+r':[^\n]*(?:\n(?!  [A-Za-z_]\w*:).*)*',s,re.M)
 return m.group(0) if m else ''
def setval(s,k,v):
 old=block(s,k); new='  '+k+':'+(('' if v.startswith('\n') else ' ')+v if v else '')
 return s.replace(old,new,1) if old else s.rstrip()+'\n'+new+'\n'
def ints(a):return b''.join(struct.pack('<i',i) for i in a).hex()
def arr(s,k):
 v=block(s,k).split(':',1)[-1].strip()
 return list(struct.unpack('<'+'i'*(len(v)//8),bytes.fromhex(v))) if v and v!='[]' else []
def asset(n):
 guid=re.search(r'    level: '+str(n)+r'\n.*?levelData: \{fileID: \d+, guid: (\w+)',CAT.read_text(),re.S)[1]
 p=next(p for p in PRO.glob('*.asset.meta') if 'guid: '+guid in p.read_text());return Path(str(p)[:-5])
def board(n,rows,moves,idea):
 w=len(rows[0]);h=len(rows)
 return dict(level=n,width=w,height=h,moves=moves,idea=idea,rows=rows,goals=[],water=[],fruits=[],data={'cells':[int(c!='.') for row in rows for c in row],'obstacles':[0]*(w*h),'obstacleOrigins':[-1]*(w*h),'pinnedTileTypes':[0]*(w*h),'pinnedSpecialTypes':[0]*(w*h)})
def put(d,x,y,o,w=1,h=1):
 origin=y*d['width']+x
 for yy in range(y,y+h):
  for xx in range(x,x+w):
   i=yy*d['width']+xx;assert d['data']['cells'][i] and not d['data']['obstacles'][i]
   d['data']['obstacles'][i]=o;d['data']['obstacleOrigins'][i]=origin
 return origin

def designs():
 a=board(4,['ooooooooo']*9,32,'Kare tahta içinde S biçimli dere; tek kaynak ve aşağı akan tek çıkış.')
 a['water']=[y*9+x for x,y in [(3,0),(3,1),(3,2),(4,2),(5,2),(5,3),(5,4),(5,5),(4,5),(3,5),(3,6),(3,7),(3,8)]]
 a['goals']=[(1,76,1)]
 b=board(13,['..oooo..','.oooooo.']+['oooooooo']*7,30,'Çatılı kuş evi silueti; kapı biçimli 2×3 yuva grubunun iki yanında çalışma alanı.')
 for y in range(6,9):
  for x in (3,4):put(b,x,y,78)
 b['goals']=[(2,3,12)]
 c=board(16,['.ooooooo.','ooooooooo','ooooooooo','.ooooooo.','..ooooo..','..ooooo..','...ooo...','oo.ooo.oo','oo.ooo.oo'],30,'Geniş bahçe tacı, altta sinek kutusu ve ayrı iki elma adacığı.')
 put(c,3,7,100,2,2)
 for x in (0,7):c['fruits'].append(dict(originCellIndex=put(c,x,7,102,2,2),width=2,height=2))
 c['goals']=[(1,102,2)]
 return [a,b,c]

def write():
 paths=[asset(n) for n in (4,13,16)]
 if not (HERE/'Before').exists():
  protected={str(p.relative_to(ROOT)):sha(p) for folder in ('Settings','Scripts','Scenes','Prefabs','Resources') for p in (ROOT/'Assets/_Project'/folder).rglob('*') if p.is_file() and p.suffix in ('.asset','.meta','.cs','.unity','.prefab','.json')}
  (HERE/'protected-before.json').write_text(json.dumps(protected,indent=2))
  for p in paths+[Path(str(p)+'.meta') for p in paths]+[CAT,ROOT/'Assets/_Project/Resources/LevelNames.json',ROOT/'Docs/LevelDesign/Memory.md',ROOT/'Docs/LevelDesign/Design-State.md',ROOT/'Docs/LevelDesign/Design-Feedback.md']:
   dst=HERE/'Before'/p.relative_to(ROOT);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst)
 else:
  # Resume only if no game data has been changed since this baseline was captured.
  assert all(sha(ROOT/p)==h for p,h in json.loads((HERE/'protected-before.json').read_text()).items())
 names_path=ROOT/'Assets/_Project/Resources/LevelNames.json'
 naming=json.loads(names_path.read_text())
 if not any(e['level']==4 for e in naming['entries']):
  naming['entries'].append(dict(level=4,levelKey='LevelCL_004',name='Kıvrımlı Dere',nameEn='Winding Stream'))
  naming['entries'].sort(key=lambda e:e['level'])
  names_path.write_text(json.dumps(naming,indent=2,ensure_ascii=False)+'\n')
 names={e['level']:e for e in json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())['entries']}
 ds=designs()
 for d,p in zip(ds,paths):
  s=p.read_text();assert not re.search(r'\n  - id:',block(s,'tutorialSteps'));assert 'comboTutorial: 0' in s
  d['name']=names[d['level']]['name'];d['nameEn']=names[d['level']]['nameEn'];d['asset']=str(p.relative_to(ROOT))
  for k in ('width','height','moves'):s=setval(s,k,str(d[k]))
  for k,v in d['data'].items():s=setval(s,k,ints(v))
  for k in ('tubes','magnets','waterChannels','safes','ancientSeals','stackedObstacles','colorCrateInitialHits','lampStripSettings','paintCanBoxColors','dimerdals','tarpStatues','fruits','tutorialSteps'):s=setval(s,k,'[]')
  for k in ('wallPieceIds','lampColorByCell','hamsterSatietyByCell'):s=setval(s,k,ints([0]*(d['width']*d['height'])))
  for k in ('oilSpreadMaxCells','hamsterSatietyOverride','movableSpawnFallbackMoves'):s=setval(s,k,'0')
  s=setval(s,'movableSpawnColumns','00'*d['width'])
  s=setval(s,'randomPool',ints([0,1,2,3]))
  goals=[]
  for typ,oid,count in d['goals']:
   goals.append(f'  - targetType: {typ}\n    tileType: 0\n    obstacleId: {oid if typ==1 else 0}\n    collectibleId: {oid if typ==2 else 1}\n    iconOverride: {{fileID: 0}}\n    amount: {count}')
  s=setval(s,'goals','\n'+'\n'.join(goals))
  if d['water']:s=setval(s,'waterChannels','\n  - pathCellIndices: '+ints(d['water']))
  if d['fruits']:s=setval(s,'fruits','\n'+'\n'.join(f"  - originCellIndex: {e['originCellIndex']}\n    width: {e['width']}\n    height: {e['height']}" for e in d['fruits']))
  p.write_text(s)
 (HERE/'designs.json').write_text(json.dumps(ds,indent=2,ensure_ascii=False)+'\n')
 verify()

def verify():
 ds=json.loads((HERE/'designs.json').read_text()); reports=[]
 for d in ds:
  s=(ROOT/d['asset']).read_text();w=d['width'];h=d['height'];data=d['data']
  for k,v in data.items():assert arr(s,k)==v
  active={i for i,a in enumerate(data['cells']) if a};obs=data['obstacles'];origins=data['obstacleOrigins']
  assert len(active)>0 and all((o==0 and origins[i]==-1) or (o>0 and i in active and origins[i] in active and obs[origins[i]]==o) for i,o in enumerate(obs))
  for origin in {origins[i] for i,o in enumerate(obs) if o}:
   o=obs[origin];fw,fh=(2,2) if o in (100,102) else (1,1)
   assert {i for i,v in enumerate(origins) if v==origin}=={origin+y*w+x for y in range(fh) for x in range(fw)}
  path=d['water']
  if path:
   assert len(set(path))==len(path) and set(path)<=active
   assert all((b==a+w or (abs(b-a)==1 and a//w==b//w)) for a,b in zip(path,path[1:]))
   assert path[-1]//w==h-1 and path[-1]-path[-2]==w
  blocked={i for i,o in enumerate(obs) if o in (78,100,102)}|set(path[:1])
  slots=active-blocked
  # Conservative static downward/diagonal supply graph; no gameplay/cascade simulation.
  # Every normal slot in these boards also has a vertical source above its own segment.
  reached=set()
  for x in range(w):
   column=[y*w+x for y in range(h) if y*w+x in active]
   if not column:continue
   first=column[0]
   if first in slots:reached.add(first)
   elif first in path[:1] and first+w in slots:reached.add(first+w)
  changed=True
  while changed:
   changed=False
   for i in sorted(slots-reached):
    if i-w in reached:reached.add(i);changed=True
  assert reached==slots,(d['level'],'unfed',sorted(slots-reached))
  if d['level']==13:
   birds=[i for i,o in enumerate(obs) if o==78];assert len(birds)==6
   assert all((i-1 in slots and i%w>0) or (i+1 in slots and i%w<w-1) for i in birds)
  if d['level']==16:
   assert len(d['fruits'])==2 and sum(o==100 and origins[i]==i for i,o in enumerate(obs))==1
   for e in d['fruits']:
    island={e['originCellIndex']+y*w+x for x in range(2) for y in range(2)}
    for i in island:
     neighbors=[j for j in (i-w,i+w) if 0<=j<w*h]+([i-1] if i%w else [])+([i+1] if i%w<w-1 else [])
     assert not (set(neighbors)&(active-island))
  reports.append(dict(level=d['level'],active=len(active),normalSlots=len(slots),verticallyFed=len(reached),goals=d['goals'],moves=d['moves'],channelBottomExit=bool(path),manualPlayTested=False))
 protected=json.loads((HERE/'protected-before.json').read_text());allowed={d['asset'] for d in ds}|{'Assets/_Project/Resources/LevelNames.json'}
 changes=[p for p,h in protected.items() if sha(ROOT/p)!=h];assert set(changes)==allowed,changes
 result=dict(levels=reports,changedAssets=changes,protectedUnchanged=len(protected)-len(changes),validation='Static data/footprint/feed checks only; no play/build/simulation.')
 (HERE/'validation.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n');print(json.dumps(result,ensure_ascii=False,indent=2))
if __name__=='__main__':
 import sys
 verify() if '--verify' in sys.argv else write()
