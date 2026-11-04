"""Static data, layers, feed-route and preservation audit. No Unity, bot or cascade simulation."""
from pathlib import Path
from collections import Counter, deque
import copy, hashlib, importlib.util, json, re, struct, subprocess, sys
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import array, footprint
from export_level_previews import parse_entries, int_field
spec=importlib.util.spec_from_file_location('rv',HERE.parent/'131-205-Review-2026-10-31/render.py')
r=importlib.util.module_from_spec(spec); spec.loader.exec_module(r)
path=ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00002110.asset'; text=path.read_text()
raw=subprocess.check_output(['ruby','-rjson','-ryaml','-e',
    's=File.read(ARGV[0]).gsub(/^%.*\n/, "").gsub(/^--- !u!.*\n/, "---\n"); puts JSON.generate(YAML.safe_load(s))',str(path)],text=True)
assert json.loads(raw)['MonoBehaviour']['moves']==20
d=next(p for p in r.review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset') if p['level']==211)
w,h=d['width'],d['height']; assert (w,h)==(9,11) and d['kind']==0
a={k:array(text,k,w*h) for k in ('cells','obstacles','obstacleOrigins','wallPieceIds','lampColorByCell','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')}
assert sum(a['cells'])==75 and set(a['cells'])=={0,1}
assert array(text,'randomPool',4)==[0,1,2,3]
assert not any(a['pinnedTileTypes']) and not any(a['pinnedSpecialTypes'])
assert not any(a['lampColorByCell']) and not any(a['hamsterSatietyByCell'])
def connected(cells):
    pending=set(cells); seen={next(iter(pending))}; q=deque(seen)
    while q:
        i=q.popleft()
        for j in (i-w,i+w,i-1,i+1):
            if j in pending and j not in seen and abs(j%w-i%w)+abs(j//w-i//w)==1:seen.add(j);q.append(j)
    return seen==pending
assert connected({i for i,v in enumerate(a['cells']) if v})
counts=Counter(); walls=[]
for i,o in enumerate(a['obstacles']):
    if not o:
        assert a['obstacleOrigins'][i]==-1
        continue
    origin=a['obstacleOrigins'][i]; assert a['cells'][i] and a['obstacles'][origin]==o and a['obstacleOrigins'][origin]==origin
    if origin!=i:continue
    region={j for j,v in enumerate(a['obstacleOrigins']) if v==origin}
    assert connected(region); counts[o]+=1
    if o in (55,56):
        assert len(region)>=4 and len({a['wallPieceIds'][j] for j in region})==1 and a['wallPieceIds'][i]>0
        assert len({j%w for j in region})>=2 and len({j//w for j in region})>=2
        walls.append(dict(id=o,origin=origin,cells=sorted(region)))
    else: assert footprint(o)==(1,1) and region=={i} and a['wallPieceIds'][i]==0
assert len(walls)==4
for e in d['custom']['stackedObstacles']:
    o=e['obstacleId']; origin=e['originCellIndex']; bw,bh=footprint(o)
    assert (o,origin,bw,bh,e['stackOrder'])==(27,74,2,2,1)
    region={origin+dy*w+dx for dy in range(bh) for dx in range(bw)}
    assert all(a['cells'][j] and a['obstacles'][j]==25 for j in region)
    counts[o]+=1
assert counts=={55:2,56:2,28:17,27:1,25:4}
assert len(d['goals'])==5 and {g['obstacleId']:g['amount'] for g in d['goals']}==counts
assert all(g['targetType']==1 for g in d['goals'])
assert not any(d['custom'][k] for k in ('ancientSeals','waterChannels','safes','tubes','magnets','tarpStatues'))
assert not d['geometryWarnings']
flow=[]
for label,removed in [('opening',set()),('brick route open',{55}),('metal route open',{56}),('both routes open',{55,56}),('stone keel open',{55,56,28}),('all covers open',{55,56,28,27})]:
    phase=copy.deepcopy(d)
    for i,o in enumerate(phase['top']):
        if o not in removed:continue
        phase['top'][i]=a['obstacles'][i] if o==27 else 0
        phase['topOrigins'][i]=a['obstacleOrigins'][i] if o==27 else -1
    slots,reached=r.spawn(phase)
    assert slots==reached,(label,sorted(slots-reached))
    flow.append(dict(phase=label,tileSlots=len(slots),reachable=len(reached)))
assert flow[0]['tileSlots']==28
catalog=ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset'
before=(HERE/'Before'/catalog.name).read_text()
assert catalog.read_text().startswith(before)
assert list(map(int,re.findall(r'^    level: (\d+)$',catalog.read_text(),re.M)))==list(range(1,212))
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())
old=json.loads((HERE/'Before/LevelNames.json').read_text())
assert names['entries'][:-1]==old['entries']
assert names['entries'][-1]==dict(level=211,levelKey='LevelCL_0211',name='Çelik Arma',nameEn='Steel Crest')
baseline=json.loads((HERE/'baseline.json').read_text())
changed=[f for f,digest in baseline.items() if not (ROOT/f).exists() or hashlib.sha256((ROOT/f).read_bytes()).hexdigest()!=digest]
assert not changed,changed
report=dict(level=211,moves=20,checks='PASS',yaml='Ruby/Psych safe_load',activeCells=75,initialTiles=28,goalCounts=dict(counts),wallPieces=walls,feedRouteChecks=flow,protectedFiles=len(baseline),changedProtectedFiles=changed,
    limits='Static geometry and full-family removal checkpoints, not an exhaustive runtime state search. No Unity/player build, bot/cascade simulation, device FPS or win-rate measurements. Draft not user-approved.')
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('wallPieces','limits')},ensure_ascii=False))
