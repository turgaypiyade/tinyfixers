"""Static audit of the user's approved-silhouette revision. No Unity or gameplay simulation."""
from collections import Counter,deque
from pathlib import Path
import hashlib,json,re,subprocess
from common import r,ROOT,HERE,feed,layers,phase
from check_levels161_162 import array,footprint
from export_level_previews import parse_entries
REV=HERE/'Revisions/v2-grass-over-bottom';P=ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00002120.asset'
t=P.read_text();old=(REV/P.name).read_text()
raw=subprocess.check_output(['ruby','-rjson','-ryaml','-e','s=File.read(ARGV[0]).gsub(/^%.*\n/, "").gsub(/^--- !u!.*\n/, "---\n");puts JSON.generate(YAML.safe_load(s))',str(P)],text=True)
assert json.loads(raw)['MonoBehaviour']['moves']==24
allowed=('obstacles','obstacleOrigins','tubes','stackedObstacles','pinnedTileTypes')
def scrub(s):
    for key in allowed:s=re.sub(r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*','',s,flags=re.M)
    return s
assert scrub(t)==scrub(old),'Unexpected level field changed'
d=next(x for x in r.review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset') if x['level']==212)
a={k:array(t,k,99) for k in ('cells','obstacles','obstacleOrigins','pinnedTileTypes','pinnedSpecialTypes')}
assert a['cells']==array(old,'cells',99) and sum(a['cells'])==59
assert (d['width'],d['height'],d['moves'],d['kind'])==(9,11,24,0)
assert parse_entries(t,'goals')==parse_entries(old,'goals') and d['custom']['safes']==parse_entries(old,'safes')
counts=Counter();paint=set();origins=set()
for i,o in enumerate(a['obstacles']):
    if not o:assert a['obstacleOrigins'][i]==-1;continue
    assert a['cells'][i] and o in (34,42,63)
    origin=a['obstacleOrigins'][i]
    assert a['obstacles'][origin]==o and a['obstacleOrigins'][origin]==origin
    if o==63:
        assert origin in (74,76);paint.add(i);origins.add(origin)
    else:assert origin==i;counts[o]+=1
assert counts=={34:4,42:6} and origins=={74,76}
for origin in origins:
    expected={origin+dy*9+dx for dy in (0,1) for dx in (0,1)}
    assert {i for i,o in enumerate(a['obstacles']) if o==63 and a['obstacleOrigins'][i]==origin}==expected
    assert all(a['cells'][i] for i in expected)
assert footprint(63)==(2,2) and paint=={74,75,76,77,83,84,85,86}
# A four-column paired footprint cannot move one row farther down without changing the approved mask.
shared={x for x in range(9) if a['cells'][9*9+x] and a['cells'][10*9+x]}
assert len(shared)==3
stacks=d['custom']['stackedObstacles'];assert len(stacks)==8
assert {e['originCellIndex'] for e in stacks}==paint
assert all(e['obstacleId']==42 and e['stackOrder']==1 for e in stacks)
counts[42]+=8;counts[63]=2;counts[33]=1
paths=[]
for e in d['custom']['tubes']:
    dx,dy=((0,-1),(0,1),(-1,0),(1,0))[e['direction']]
    p=[e['originCellIndex']+k*(dx+dy*9) for k in range(e['length'])]
    assert e['length']==3 and not set(p)&paint and all(a['cells'][i] and not a['obstacles'][i] for i in p)
    paths.append(p);counts[26]+=1
assert paths==[[54,55,56],[62,61,60]]
assert counts=={34:4,42:14,63:2,33:1,26:2}
assert len(d['goals'])==5 and {g['obstacleId']:g['amount'] for g in d['goals']}==counts
ls=layers(d)
for i in paint:assert [o for o,_ in ls[i]]==[63,42]
assert all(ls[i][-1][0]==33 and ls[i][0][0]==34 for i in (21,22,30,31))
assert not d['geometryWarnings'] and not any(a['pinnedSpecialTypes'])
pins={i for i,v in enumerate(a['pinnedTileTypes']) if v};assert pins=={63,64,70,71,93,94}
assert all(a['cells'][i] and not ls[i] for i in pins)
for i in pins:
    c=a['pinnedTileTypes'][i];assert 1<=c<=4
    assert not(i%9>=2 and a['pinnedTileTypes'][i-1]==a['pinnedTileTypes'][i-2]==c)
    assert not(i//9>=2 and a['pinnedTileTypes'][i-9]==a['pinnedTileTypes'][i-18]==c)
assert set(a['pinnedTileTypes'][i] for i in pins)=={1,2,3,4}
checkpoints=[]
for label,removed in [('opening',set()),('grass cleared',{42}),('safe opened',{33}),('grass and paint cleared',{42,63}),('hull and tubes open',{42,63,26}),('all goals cleared',{33,34,42,63,26})]:
    q=phase(d,removed);slots,reached=feed(q)
    assert not(slots-reached-pins),(label,sorted(slots-reached-pins))
    checkpoints.append(dict(phase=label,tileSlots=len(slots),naturallyFed=len(reached),authoredPinsNeeded=len(slots-reached)))
assert checkpoints[0]['tileSlots']==41 and checkpoints[0]['naturallyFed']==35
assert all(a['cells'][y*9+x] for x in (3,4) for y in range(2,11))
baseline=json.loads((REV/'protected-before.json').read_text());changes=[p for p,digest in baseline.items() if not(ROOT/p).exists() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=digest]
assert not changes,changes
report=dict(level=212,revision='v2-grass-over-bottom',result='PASS',moves=24,active=59,goalCounts=dict(counts),sailMaskUnchanged=True,safeUnchanged=True,paintCells=sorted(paint),topGrassCells=sorted(paint),checkpoints=checkpoints,protectedFiles=len(baseline),changedProtectedFiles=changes,limits='Static mask/layer/goal/straight cargo exit and full-family removal checkpoints. No runtime gravity/cascade simulation, Unity build, device test, FPS or win-rate measurement. User approved the silhouette; revised play balance is untested.')
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False))
