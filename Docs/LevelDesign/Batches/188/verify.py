"""Static authored-data checks only; no game, bot, or cascade simulation."""
from pathlib import Path
from collections import Counter,deque
import hashlib,json,re,subprocess,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import KEYS,array,DEFS,stage,footprint
from export_level_previews import int_field,parse_entries
d=json.loads((HERE/'design.json').read_text());asset=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset');text=asset.read_text()
a={k:array(text,k,99) for k in KEYS};assert a==d['data']
assert tuple(int_field(text,k) for k in ('width','height','moves','levelKind'))==(9,11,28,0)
assert array(text,'randomPool',4)==[0,1,2,3]
for key in ('wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes'):assert not any(a[key]),key
active={i for i,v in enumerate(a['cells']) if v};assert len(active)==95 and set(range(99))-active=={0,8,90,98}
assert all(a['obstacles'][i]==0 and a['obstacleOrigins'][i]==-1 for i in set(range(99))-active)
base=Counter(o for o in a['obstacles'] if o);assert base=={34:6,40:21,39:9}
assert all(footprint(o)==(1,1) for o in base)
assert all(origin==(i if a['obstacles'][i] else -1) for i,origin in enumerate(a['obstacleOrigins']))
seals=parse_entries(text,'ancientSeals');assert seals==d['ancientSeals']
assert {s['startColor'] for s in seals}=={1,2,3}
covered=set();seals_by_origin={}
for s in seals:
    origin=s['originCellIndex'];ox,oy=origin%9,origin//9
    assert (s['width'],s['height'],s['stackOrder'])==(3,3,1) and oy==4
    region={(oy+y)*9+ox+x for y in range(3) for x in range(3)}
    assert region<=active and not region&covered
    assert Counter(a['obstacles'][i] for i in region)=={34:2,40:7}
    assert {i%9 for i in region if a['obstacles'][i]==34}=={ox+1}
    for color in ('redCount','yellowCount','greenCount'):assert s[color]==7
    covered|=region;seals_by_origin[origin]=sorted(region)
assert covered==set(range(36,63))
goal_counts={g['obstacleId']:g['amount'] for g in parse_entries(text,'goals')}
assert goal_counts=={64:3,34:6,40:21,39:9}
assert not ({38,43,45,59,60,61,62}&set(goal_counts)),'No helper forced into the goal list'
assert stage(39,'damageRule')==1 and stage(39,'blocksCells')==1
assert stage(34,'behavior')==3 and re.search(r'^    exitAtBottom: 1$',DEFS[34],re.M)
assert re.search(r'^    hits: 2$',DEFS[40],re.M) and stage(40,'behavior')==3
assert {i for i,o in enumerate(a['obstacles']) if o==39}==set(range(81,90))
for x in (1,4,7):
    assert all(y*9+x in active for y in range(11)),'Cargo must have a straight exit path'
    assert a['obstacles'][9*9+x]==39
    assert all(a['obstacles'][y*9+x]==0 for y in (7,8,10))
    # The full lower two-row work area can create a special that reaches each outlet.
    assert all(a['obstacles'][y*9+nx]==0 for y in (7,8) for nx in (x-1,x))
opening=active-covered-set(range(81,90));assert len(opening)==59
assert sum(i<36 for i in opening)==34
def components(cells):
    unseen=set(cells);sizes=[]
    while unseen:
        q=deque([min(unseen)]);count=0
        while q:
            i=q.popleft()
            if i not in unseen:continue
            unseen.remove(i);count+=1
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                x,y=i%9+dx,i//9+dy
                if 0<=x<9 and 0<=y<11 and y*9+x in unseen:q.append(y*9+x)
        sizes.append(count)
    return sizes
assert components(active)==[95] and components(opening)==[34,18,7]
diagonal_nodes=0
for x in range(10):
    for y in range(12):
        bits=sum(v for v,(cx,cy) in zip((1,2,4,8),((x-1,y-1),(x,y-1),(x,y),(x-1,y))) if 0<=cx<9 and 0<=cy<11 and cy*9+cx in active)
        diagonal_nodes+=bits in (5,10)
assert diagonal_nodes==0
for key in ('tubes','magnets','waterChannels','safes','stackedObstacles','tarpStatues','paintCanBoxColors','tutorialSteps','colorCrateInitialHits'):assert not parse_entries(text,key),key
prior=json.loads((ROOT/'Docs/LevelDesign/Reviews/188-Catalog/Catalog-Inventory-Before.json').read_text())
assert len(prior)==187 and [v['level'] for v in prior]==list(range(1,188))
for oid in (34,40,39,64):assert any(str(oid) in v['obstacleInstances'] for v in prior)
catalog=ROOT/'Assets/_Project/Settings/LevelCatalogPro.asset'
entry=f'  - chapter: 8\n    level: 188\n    levelKey: LevelCL_0188\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n'
assert catalog.read_text()==(HERE/'catalog-before.txt').read_text()+entry
assert f'guid: {d["guid"]}' in Path(str(asset)+'.meta').read_text()
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())
assert names['entries'][:-1]==json.loads((HERE/'names-before.json').read_text())['entries']
assert names['entries'][-1]==dict(level=188,levelKey='LevelCL_0188',name=d['title'])
assert len({x['levelKey'] for x in names['entries']})==188
protected=json.loads((HERE/'protected-before.json').read_text())
changed=[path for path,h in protected.items() if not (ROOT/path).exists() or hashlib.sha256((ROOT/path).read_bytes()).hexdigest()!=h]
assert not changed,changed
def yaml_load(p):return json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(p)],text=True))['MonoBehaviour']
assert yaml_load(asset)['ancientSeals']==seals
assert len(yaml_load(catalog)['entries'])==188
similarity=json.loads((ROOT/'Docs/LevelDesign/Reviews/188-Catalog/Similarity.json').read_text())
assert similarity['previousLevelsScanned']==187
report=dict(level=188,passed=True,name=d['title'],moves=28,difficulty='Normal',active=95,holes=4,openingTileCells=59,
    initialOpenComponents=[34,18,7],initialTopFedTileCells=34,lowerPrepopulatedTileCells=25,
    sealedRegions=seals_by_origin,buriedCargo=6,buriedTwoStagePlastic=21,sealTotalColorCredits=63,specialOnlyOutlets=9,
    cargoExitColumnsOneBased=[2,5,8],extraMovableRefillQuota=0,previousLevelsScanned=187,
    previousSettingsScriptsResourcesScenesPrefabsUnchanged=len(protected),diagonalOnlyMaskCorners=0,
    unityBuildRun=False,gameplaySimulationRun=False,winRateMeasured=False)
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2))
