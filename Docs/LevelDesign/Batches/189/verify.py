"""Static asset/layer/path/preservation verification; no gameplay simulation."""
from pathlib import Path
from collections import Counter,deque
import hashlib,json,re,subprocess,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import KEYS,array,DEFS,stage,footprint
from export_level_previews import int_field,parse_entries
from review_level_patterns import path_entries,load
d=json.loads((HERE/'design.json').read_text());asset=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset');text=asset.read_text()
a={k:array(text,k,99) for k in KEYS};assert a==d['data']
assert tuple(int_field(text,k) for k in ('width','height','moves','levelKind'))==(9,11,32,0)
assert d['difficulty']=='Super Hard' and 189%10==9
assert array(text,'randomPool',4)==[0,1,2,3]
for k in ('pinnedTileTypes','pinnedSpecialTypes','hamsterSatietyByCell'):assert not any(a[k])
active={i for i,v in enumerate(a['cells']) if v};assert len(active)==96 and set(range(99))-active=={89,90,98}
base=Counter(o for o in a['obstacles'] if o);assert base=={69:14,45:8,55:16,75:16,62:2,59:2}
paths=path_entries(text,'waterChannels');assert paths==d['waterChannels']
used=set();sources=set()
for entry in paths:
    path=entry['pathCellIndices'];assert len(path)==8 and len(set(path))==8 and not set(path)&used
    assert set(path)<=active and path[-1]%9==0
    assert a['obstacles'][path[0]]==0;sources.add(path[0])
    for first,last in zip(path,path[1:]):
        dx,dy=last%9-first%9,last//9-first//9
        assert abs(dx)+abs(dy)==1 and dy>=0
    for i in path[1:]:assert a['obstacles'][i]==69 and a['obstacleOrigins'][i]==i and footprint(69)==(1,1)
    used.update(path)
assert set(d['lilyCells'])==used-sources and len(used-sources)==14
spawner=(ROOT/'Assets/_Project/Scripts/Grid/GridSpawner.cs').read_text()
assert spawner.index('ConvertGrassFlowersToGrass(resolvedLevel)')<spawner.index('StampWaterChannelCellsIntoLevel(resolvedLevel)')<spawner.index('StampChannelCoversIntoLevel(resolvedLevel)')<spawner.index('StampLayeredEntriesIntoLevel(resolvedLevel)')
assert 'if (cover) channelCovers.Add((cell, authored));' in spawner
for group in d['pockets']:
    x,y=min(i%9 for i in group),min(i//9 for i in group)
    assert set(group)=={(y+dy)*9+x+dx for dx in range(2) for dy in range(2)}
    assert set(group)<=active and not set(group)&used and all(a['obstacles'][i]==45 for i in group)
assert [i+45 for i in d['pockets'][0]]==d['pockets'][1]
assert stage(45,'behavior')==3 and re.search(r'^    hits: 2$',DEFS[45],re.M)
wall_cells=set()
for group in d['walls']:
    origin=group['origin'];x,y=origin%9,origin//9
    expected={(y+dy)*9+x+dx for dx in range(2) for dy in range(2)}
    assert set(group['cells'])==expected and x==5 and not expected&wall_cells
    for i in expected:assert a['obstacles'][i]==55 and a['obstacleOrigins'][i]==origin and a['wallPieceIds'][i]==group['piece']
    wall_cells|=expected
assert {g['piece'] for g in d['walls']}=={1,2,3,4} and len(wall_cells)==16
assert all(v==0 for i,v in enumerate(a['wallPieceIds']) if i not in wall_cells)
stacks=parse_entries(text,'stackedObstacles');assert stacks==d['stacks']
garden={y*9+x for y in range(1,9) for x in (7,8)}
assert {e['originCellIndex'] for e in stacks}==garden
assert all(e['obstacleId']==58 and e['stackOrder']==1 and a['obstacles'][e['originCellIndex']]==75 for e in stacks)
assert stage(75,'behavior')==3 and re.search(r'^    hits: 2$',DEFS[75],re.M)
launcher_cells=set()
for oid,origin in ((62,91),(59,88)):
    bw,bh=footprint(oid);region={(origin//9+y)*9+origin%9+x for y in range(bh) for x in range(bw)}
    assert region<=active and not region&launcher_cells
    for i in region:assert a['obstacles'][i]==oid and a['obstacleOrigins'][i]==origin
    launcher_cells|=region
    assert stage(oid,'damageRule')==1 and re.search(r'^    hits: 2$',DEFS[oid],re.M)
assert footprint(62)==(2,1) and footprint(59)==(1,2)
right_ray={10*9+x for x in range(3,9)}&active
assert 97 in right_ray and a['obstacleOrigins'][97]==88,'Horizontal launcher must reach the vertical launcher'
up_ray={y*9+7 for y in range(9)}
assert len(up_ray&garden)==8 and not up_ray&wall_cells
goals={g['obstacleId']:g['amount'] for g in parse_entries(text,'goals')}
assert goals=={76:2,69:14,55:4,75:16} and not set(goals)&{45,59,62}
assert goals[75]==base[75] and goals[55]==len(d['walls'])
free={i for i in active if a['obstacles'][i]==0}-sources
assert len(free)==36 and all(y*9+x in free for y in range(11) for x in (3,4))
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
assert components(active)==[96] and components(free)==[36]
for key in ('ancientSeals','safes','tubes','magnets','tarpStatues','paintCanBoxColors','colorCrateInitialHits','tutorialSteps'):assert not parse_entries(text,key)
inventory=load();previous=inventory[:-1];current=inventory[-1]
assert current['level']==189 and len(previous)==188 and not current['geometryWarnings']
assert all(current['top'][i]==69 for i in d['lilyCells']),'Review inventory must preserve canal covers'
assert all(current['top'][i]==58 for i in garden)
assert current['obstacleInstances'][55]==4 and current['obstacleInstances'][76]==2
for oid in (69,45,55,58,75,59,76):assert any(oid in p['obstacleInstances'] for p in previous)
# Right-facing launcher is the explicit directional variant requested by the user.
assert 62 in DEFS and any(59 in p['obstacleInstances'] for p in previous)
catalog=ROOT/'Assets/_Project/Settings/LevelCatalogPro.asset'
entry=f'  - chapter: 8\n    level: 189\n    levelKey: LevelCL_0189\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n'
assert catalog.read_text()==(HERE/'catalog-before.txt').read_text()+entry
assert f'guid: {d["guid"]}' in Path(str(asset)+'.meta').read_text()
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())
assert names['entries'][:-1]==json.loads((HERE/'names-before.json').read_text())['entries']
assert names['entries'][-1]==dict(level=189,levelKey='LevelCL_0189',name=d['title'])
baseline=json.loads((HERE/'protected-before.json').read_text())
changed=[path for path,h in baseline.items() if not (ROOT/path).exists() or hashlib.sha256((ROOT/path).read_bytes()).hexdigest()!=h]
assert not changed,changed
yaml=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(asset)],text=True))['MonoBehaviour']
assert yaml['moves']==32 and len(yaml['waterChannels'])==2 and len(yaml['stackedObstacles'])==16
result=dict(level=189,passed=True,name=d['title'],difficulty='Super Hard',moves=32,active=96,authoredEdgeHoles=3,
    freeNormalOpeningTileCells=36,normalTileCellsIncludingLockedLily=50,totalInitialTileViewSlots=74,channelSources=2,channelCells=14,
    lilyAboveChannelCells=14,eggBirdWindows=d['pockets'],helperEggBirds=8,independentWallPieces=4,wallCells=16,
    flowerAboveLanternCells=16,lanterns=16,perpendicularLaunchers={'rightOrigin':91,'upOrigin':88,'rayIntersection':97},
    extraMovableRefillQuota=0,previousCatalogEntriesScanned=188,previousSettingsScriptsResourcesScenesPrefabsUnchanged=len(baseline),
    runtimeCodeChanged=False,unityBuildRun=False,gameplaySimulationRun=False,winRateMeasured=False)
(HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps(result,ensure_ascii=False,indent=2))
