"""Static asset, layer, gravity-route and preservation checks; no game/bot/cascade execution."""
from pathlib import Path
from collections import Counter, deque
import hashlib, json, re, struct, sys

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from export_level_previews import int_field, int_array, parse_entries

LEVELS=ROOT/'Assets/_Project/Settings/ProLevels'
designs=json.loads((HERE/'designs.json').read_text())
protected=json.loads((HERE/'protected-before.json').read_text())
assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in protected.items()), 'Pre-existing file changed'
catalog=(LEVELS/'LevelCatalogProV1.asset').read_text()
before=(HERE/'Before/LevelCatalogProV1.asset').read_text()
assert (LEVELS/'LevelV1_00000110.asset').read_bytes()==(HERE/'Before/LevelV1_00000110.asset').read_bytes()
normalized=catalog
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())
old_names=json.loads((HERE/'Before/LevelNames.json').read_text())
assert [{k:v for k,v in e.items()} for e in names['entries'] if e['level'] not in range(12,16)]==old_names['entries']
library=(ROOT/'Assets/_Project/Settings/ObstacleLibrary.asset').read_text()
defs={int(m[1]):m[2] for m in re.finditer(r'^  - id: (\d+)\n(.*?)(?=^  - id:|\Z)',library,re.M|re.S)}
hits={o:int(re.search(r'^    hits: (\d+)',defs[o],re.M)[1]) for o in (4,5,6,14,25)}
report=[]
for d in designs:
    n=d['n'];p=LEVELS/(d['asset']+'.asset');t=p.read_text();size=81
    assert int_field(t,'width')==int_field(t,'height')==9
    assert int_field(t,'moves')==d['moves'] and int_field(t,'levelKind')==0
    a={k:int_array(t,k,size,-1 if k=='obstacleOrigins' else 0) for k in d['data']}
    assert all(len(v)==size for v in a.values()) and all(v==1 for v in a['cells'])
    assert not any(a['pinnedTileTypes']) and not any(a['pinnedSpecialTypes'])
    assert all(v==0 for v in a['wallPieceIds']+a['hamsterSatietyByCell'])
    raw=re.search(r'^  randomPool: (\w+)',t,re.M)[1]
    assert list(struct.unpack('<4i',bytes.fromhex(raw)))==[0,1,2,3]
    assert re.search(r'^  refillSpecialGuard: -1$',t,re.M)
    stacks=parse_entries(t,'stackedObstacles');top=a['obstacles'][:];counts=Counter(o for o in top if o)
    for i,o in enumerate(top):
        assert o in (0,4,5,6,14,25)
        assert a['obstacleOrigins'][i]==(i if o else -1)
    stack_indices=set()
    for s in stacks:
        i=s['originCellIndex'];o=s['obstacleId']
        assert 0<=i<size and i not in stack_indices
        assert a['obstacles'][i]==25 and o in (4,5,6), 'Liquid must be below a solid'
        assert s['initialRemainingHits']==0
        stack_indices.add(i);top[i]=o;counts[o]+=1
    goals=parse_entries(t,'goals')
    assert all(g['targetType']==1 and g['amount']>0 for g in goals)
    assert {g['obstacleId']:g['amount'] for g in goals}==dict(counts)
    for key in ('tutorialSteps','tubes','magnets','waterChannels','safes','ancientSeals','tarpStatues','paintCanBoxColors','colorCrateInitialHits','bossWaves'):
        assert parse_entries(t,key)==[]
    # New boards contain only 1x1 cells and no holes. Trace static spawn routes:
    # down, or diagonal if either intervening corner allows it, matching current CascadeLogic.
    blocked={i for i,o in enumerate(top) if o in (4,5,6,14)}
    free=set(range(size))-blocked
    reachable={i for i in range(9) if i in free};queue=deque(reachable)
    while queue:
        i=queue.popleft();x,y=i%9,i//9
        if y==8:continue
        for dx in (0,-1,1):
            nx=x+dx;j=(y+1)*9+nx
            if not 0<=nx<9 or j not in free or j in reachable:continue
            if dx:
                corners=(i+dx,i+9)
                if not any(top[c] not in (4,5,6,14) or '      allowDiagonal: 1' in defs[top[c]].split('    - sprite: ',2)[1] for c in corners):continue
            reachable.add(j);queue.append(j)
    assert reachable==free,(n,'Unexpected empty starting pocket',sorted(free-reachable))
    assert len(free)>=54
    cap=int_field(t,'oilSpreadMaxCells')
    assert cap==(14 if n==14 else 0)
    if n==14:assert counts[14]==8 and cap>=counts[14]
    guid=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1]
    entry=f'    level: {n}\n    levelKey: LevelCL_{n:03d}\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}'
    assert entry in catalog
    normalized=normalized.replace(entry,f'    level: {n}\n    levelKey: LevelCL_{n:03d}\n    levelData: {{fileID: 0}}')
    name=next(e for e in names['entries'] if e['level']==n)
    assert name==dict(level=n,levelKey=f'LevelCL_{n:03d}',name=d['title'],nameEn=d['nameEn'])
    assert f'| {n} | {d["title"]} |' in (ROOT/'Docs/LevelDesign/Level-Names.md').read_text()
    # Every crate group is deliberately contiguous (not detached rectangular islands).
    solids={i for i,o in enumerate(top) if o in (4,5,6)};seen={next(iter(solids))};q=deque(seen)
    while q:
        i=q.popleft();x,y=i%9,i//9
        for j in (i-9 if y else -1,i+9 if y<8 else -1,i-1 if x else -1,i+1 if x<8 else -1):
            if j in solids and j not in seen:seen.add(j);q.append(j)
    assert seen==solids,(n,'Fragmented crate silhouette')
    summary=dict(level=n,name=d['title'],moves=d['moves'],difficulty=d['difficulty'],targetPassRate=d['targetPassRate'],
                 active=81,openingTileCells=len(free),gravityFedOpeningCells=len(reachable),obstacles=dict(counts),
                 authoredCoveredMud=len(stacks),maxAuthoredDepth=2 if stacks else 1,
                 initialHitBudget=sum(hits[o]*c for o,c in counts.items()),oilCap=cap,
                 limits='Static graph/asset checks only; no gameplay, cascade/bot simulation or measured pass rate.')
    report.append(summary);print(summary)
assert normalized==before,'Catalog entries outside 12–15 changed'
(HERE/'validation.json').write_text(json.dumps(dict(preservedExistingFiles=len(protected),level11Preserved=True,levels=report),ensure_ascii=False,indent=2)+'\n')
print('PASS: V1 12–15 structure/goals/layers/spawn routes/catalog/TR+EN names;',len(protected),'old files unchanged. No Unity build/play/simulation.')
