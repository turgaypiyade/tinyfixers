"""Static mask/layer/goal/exit audit and preservation. No Unity, gravity, bot or cascade execution."""
from pathlib import Path
from collections import Counter,deque
import copy,hashlib,json,re,struct,subprocess,sys
from common import r,ROOT,HERE,feed
from check_levels161_162 import array,footprint,stage
p=ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00002120.asset'; t=p.read_text()
raw=subprocess.check_output(['ruby','-rjson','-ryaml','-e','s=File.read(ARGV[0]).gsub(/^%.*\n/, "").gsub(/^--- !u!.*\n/, "---\n");puts JSON.generate(YAML.safe_load(s))',str(p)],text=True)
assert json.loads(raw)['MonoBehaviour']['moves']==24
catalog=ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset'
old=(HERE/'Before'/catalog.name).read_text(); assert catalog.read_text().startswith(old)
assert list(map(int,re.findall(r'^    level: (\d+)$',catalog.read_text(),re.M)))==list(range(1,213))
levels=r.review.load(catalog);d=next(x for x in levels if x['level']==212)
w,h=d['width'],d['height'];assert (w,h,d['moves'],d['kind'])==(9,11,24,0)
a={k:array(t,k,99) for k in ('cells','obstacles','obstacleOrigins','wallPieceIds','lampColorByCell','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')}
assert sum(a['cells'])==59 and set(a['cells'])=={0,1}
assert array(t,'randomPool',4)==[0,1,2,3]
assert not any(a['wallPieceIds']) and not any(a['pinnedSpecialTypes']) and not any(a['lampColorByCell'])
def neighbours(i):return [j for j in (i-w,i+w,i-1,i+1) if 0<=j<99 and abs(j%w-i%w)+abs(j//w-i//w)==1]
def components(cells):
    todo=set(cells);parts=[]
    while todo:
        found={next(iter(todo))};q=deque(found)
        while q:
            for j in neighbours(q.popleft()):
                if j in todo and j not in found:found.add(j);q.append(j)
        todo-=found;parts.append(found)
    return parts
assert len(components({i for i,v in enumerate(a['cells']) if v}))==1
counts=Counter();covered=set()
for i,o in enumerate(a['obstacles']):
    if not o:assert a['obstacleOrigins'][i]==-1;continue
    assert a['cells'][i] and o in (34,42) and a['obstacleOrigins'][i]==i and footprint(o)==(1,1)
    counts[o]+=1
assert counts=={34:4,42:14}
for e in d['custom']['stackedObstacles']:
    origin=e['originCellIndex'];assert e['obstacleId']==63 and e['stackOrder']==1 and footprint(63)==(2,2)
    region={origin+dy*w+dx for dy in (0,1) for dx in (0,1)}
    assert not region&covered and all(a['cells'][i] and a['obstacles'][i]==42 for i in region)
    covered|=region;counts[63]+=1
assert len(covered)==8
e,=d['custom']['safes'];assert (e['originCellIndex'],e['width'],e['height'],e['redHits'],e['yellowHits'],e['greenHits'],e['lockHitMode'])==(21,2,2,3,3,3,0)
safe={21,22,30,31};assert all(a['obstacles'][i]==34 for i in safe) and not safe&covered;counts[33]=1
tube_cells=set();tube_paths=[]
for e in d['custom']['tubes']:
    dx,dy=((0,-1),(0,1),(-1,0),(1,0))[e['direction']];origin=e['originCellIndex']
    coords=[(origin%w+dx*k,origin//w+dy*k) for k in range(e['length'])]
    assert e['length']==3 and all(0<=x<w and 0<=y<h for x,y in coords)
    path=[y*w+x for x,y in coords];assert not set(path)&(safe|covered|tube_cells)
    assert all(a['cells'][i] and a['obstacles'][i]==0 for i in path)
    tube_paths.append(path);tube_cells.update(path);counts[26]+=1
assert len(d['goals'])==5 and {g['obstacleId']:g['amount'] for g in d['goals']}==counts
assert all(g['targetType']==1 for g in d['goals'])
assert counts=={34:4,42:14,63:2,33:1,26:2}
assert not d['geometryWarnings']
assert all(not d['custom'][k] for k in ('magnets','waterChannels','ancientSeals','tarpStatues'))
pool=json.loads((HERE/'Pool-Review.json').read_text());first={q['id']:q['firstV1Use'] for q in pool['definitions']}
assert all(first[o] is not None and first[o]<212 for o in counts)
pins={i for i,v in enumerate(a['pinnedTileTypes']) if v};assert pins=={76,83,84,85,86,87,93,94,95}
assert all(a['cells'][i] and d['top'][i]==0 and 1<=a['pinnedTileTypes'][i]<=4 for i in pins)
assert set(a['pinnedTileTypes'][i] for i in pins)=={1,2,3,4}
for i in pins:
    c=a['pinnedTileTypes'][i]
    assert not(i%w>=2 and a['pinnedTileTypes'][i-1]==a['pinnedTileTypes'][i-2]==c)
    assert not(i//w>=2 and a['pinnedTileTypes'][i-w]==a['pinnedTileTypes'][i-2*w]==c)
    assert not(i%w and i//w and a['pinnedTileTypes'][i-1]==a['pinnedTileTypes'][i-w]==a['pinnedTileTypes'][i-w-1]==c)
slots,reached=feed(d);assert slots-reached==pins
free={i for i in slots if not d['top'][i] or not stage(d['top'][i],'locksInteraction')}
sizes=sorted(map(len,components(free)));assert min(sizes)>=4,sizes
checkpoints=[]
for label,removed in [('opening',set()),('paint removed',{63}),('safe opened',{33}),('cargo delivered and paint removed',{33,34,63}),('all goals cleared',{33,34,63,26,42})]:
    q=copy.deepcopy(d)
    for i,o in enumerate(q['top']):
        if o not in removed:continue
        base=a['obstacles'][i] if o in (33,63) else 0
        q['top'][i]=0 if base in removed else base
        q['topOrigins'][i]=a['obstacleOrigins'][i] if q['top'][i] else -1
    s,f=feed(q);assert not (s-f-pins),(label,sorted(s-f-pins))
    checkpoints.append(dict(phase=label,tileSlots=len(s),feedReachable=len(f),authoredPinsNeeded=len(s-f)))
exits=[]
for i in safe:
    path=[y*w+i%w for y in range(i//w+1,h)]
    assert all(a['cells'][j] for j in path) and (h-1)*w+i%w in path
    assert all(d['top'][j] in (0,33,34,42,63,26) for j in path)
    exits.append(dict(cargoCell=i,column=i%w,straightPath=path))
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text());before=json.loads((HERE/'Before/LevelNames.json').read_text())
assert names['entries'][:-1]==before['entries'] and names['entries'][-1]==dict(level=212,levelKey='LevelCL_0212',name='Renkli Yelken',nameEn='Painted Sail')
guid=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1]
assert re.search(r'level: 212\n    levelKey: LevelCL_0212\n    levelData: \{fileID: 11400000, guid: '+guid,catalog.read_text())
baseline=json.loads((HERE/'baseline.json').read_text());changes=[f for f,ha in baseline.items() if not(ROOT/f).exists() or hashlib.sha256((ROOT/f).read_bytes()).hexdigest()!=ha]
assert not changes,changes
previousAsset=ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00002110.asset'
previousMoves=int(re.search(r'^  moves: (\d+)$',previousAsset.read_text(),re.M)[1])
report=dict(level=212,result='PASS',moves=24,active=59,yaml='Ruby/Psych safe_load',goalCounts=dict(counts),initialTileSlots=len(slots),naturallyFed=len(reached),authoredHullTiles=len(pins),unlockedMatchArea=len(free),unlockedComponents=sizes,checkpoints=checkpoints,cargoPaths=exits,protectedFiles=len(baseline),changedProtectedFiles=changes,preserved211Moves=previousMoves,limits='Static authored data and full-family removal checkpoints. No move/gravity/cascade simulation, Unity build, device test, measured win-rate or FPS. User review pending.')
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('cargoPaths','limits')},ensure_ascii=False))
