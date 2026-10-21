"""Static reference mapping, geometry, targeting and preservation; no gameplay simulation."""
from pathlib import Path
from collections import Counter,deque
import hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import KEYS,array,DEFS,stage,footprint
from export_level_previews import int_field,parse_entries
d=json.loads((HERE/'design.json').read_text());ref=json.loads((HERE/'reference-grid.json').read_text())
p=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset');t=p.read_text()
a={k:array(t,k,99) for k in KEYS};assert a==d['data']
assert (int_field(t,'width'),int_field(t,'height'),int_field(t,'moves'),int_field(t,'levelKind'))==(9,11,32,0)
assert d['difficulty']=='Hard' and array(t,'randomPool',4)==[0,1,2,3]
assert ref['mirroredRows']==[row[::-1] for row in ref['originalRows']]
for y,row in enumerate(ref['originalRows']):
    for x,c in enumerate(row):
        i=y*9+8-x;assert a['cells'][i]==int(c!='.')
        expected=75 if c=='E' and y%2 else 40 if c=='E' else 28 if c=='S' else 39 if c=='G' else 0
        assert a['obstacles'][i]==expected
counts=Counter(o for o in a['obstacles'] if o);assert counts=={75:24,40:21,28:9,39:12}
assert all(footprint(o)==(1,1) for o in counts)
assert all(a['obstacleOrigins'][i]==(i if o else -1) for i,o in enumerate(a['obstacles']))
assert {i:v for i,v in enumerate(a['pinnedSpecialTypes']) if v}=={20:4}
assert all(not any(a[k]) for k in ('wallPieceIds','hamsterSatietyByCell','pinnedTileTypes'))
active={i for i,v in enumerate(a['cells']) if v};assert len(active)==78
normal={i for i in active if not a['obstacles'][i]};assert len(normal)==12 and 20 in normal
assert all(all(not a['obstacles'][y*9+i%9] for y in range(i//9+1)) for i in normal)
lower={i for i,o in enumerate(a['obstacles']) if o==39};assert len(lower)==12 and all(i%9>=4 and i//9>=6 for i in lower)
# Every isolated target has a column also occupied by the main movable region:
# after opening that region a vertical line can cross the holes into the target area.
assert all(any(a['obstacles'][y*9+i%9] in (40,75) for y in range(1,4)) for i in lower)
components=[];unseen=active.copy()
while unseen:
    q=deque([min(unseen)]);c=set()
    while q:
        i=q.popleft()
        if i not in unseen:continue
        unseen.remove(i);c.add(i)
        for dx,dy in ((-1,0),(1,0),(0,-1),(0,1)):
            x,y=i%9+dx,i//9+dy;j=y*9+x
            if 0<=x<9 and 0<=y<11 and j in unseen:q.append(j)
    components.append(len(c))
assert components==[66,6,6]
nodes=Counter(sum(bit for bit,(x,y) in zip((1,2,4,8),((nx-1,ny-1),(nx,ny-1),(nx,ny),(nx-1,ny))) if 0<=x<9 and 0<=y<11 and y*9+x in active) for nx in range(10) for ny in range(12))
assert nodes[5]+nodes[10]==ref['diagonalOnlyNodes']==2,'Preserve intentional corner geometry from reference'
goals=parse_entries(t,'goals');assert goals==[{k:v for k,v in g.items() if k!='iconOverride'} for g in d['goals']]
assert {g['obstacleId']:g['amount'] for g in goals}==counts,'No additional movable refill quota'
for k in ('stackedObstacles','waterChannels','tubes','magnets','safes','ancientSeals','tarpStatues','paintCanBoxColors','tutorialSteps'):assert not parse_entries(t,k)
for o,hits in ((75,2),(40,2),(28,3),(39,1)):assert re.search(r'^    hits: '+str(hits)+'$',DEFS[o],re.M)
assert stage(39,'damageRule')==1
catalog=(ROOT/'Assets/_Project/Settings/LevelCatalogPro.asset').read_text();before=(HERE/'catalog-before.txt').read_text()
assert catalog==before+f'  - chapter: 8\n    level: 185\n    levelKey: LevelCL_0185\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n'
assert f'guid: {d["guid"]}' in Path(str(p)+'.meta').read_text()
baseline=json.loads((HERE/'protected-before.json').read_text());changed=[p for p,h in baseline.items() if hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h];assert not changed,changed
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text());assert names['entries'][:-1]==json.loads((HERE/'names-before.json').read_text())['entries']
assert names['entries'][-1]==dict(level=185,levelKey='LevelCL_0185',name=d['title'])
yaml=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(p)],text=True));assert yaml['MonoBehaviour']['moves']==32
report=dict(level=185,passed=True,horizontalMirrorVerified=True,active=78,holes=21,openingTileCells=12,directRefillReachable=12,obstacleInstances=dict(counts),activeComponents=components,referenceCornerOnlyNodes=2,moves=32,difficulty='Hard',initialSpecial={20:4},additionalMovableQuota=0,previousSettingsScriptsResourcesUnchanged=len(baseline),unityBuildRun=False,gameplaySimulationRun=False,winRateMeasured=False)
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(report,ensure_ascii=False,indent=2))
