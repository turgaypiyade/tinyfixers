"""Static authoring/refill geometry and preservation checks. Not a gameplay simulation."""
from pathlib import Path
from collections import Counter, deque
import hashlib,json,re,struct,subprocess,sys
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import KEYS,DEFS,array,stage,footprint
from export_level_previews import int_field,parse_entries

d=json.loads((HERE/'design.json').read_text());p=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset');t=p.read_text()
assert (int_field(t,'width'),int_field(t,'height'),int_field(t,'moves'),int_field(t,'levelKind'))==(9,11,28,0)
a={k:array(t,k,99) for k in KEYS};assert a==d['data']
assert a['cells']==[1]*99 and array(t,'randomPool',4)==[0,1,2,3]
assert Counter(o for o in a['obstacles'] if o)=={75:11,28:12}
assert all(a['obstacleOrigins'][i]==(i if o else -1) for i,o in enumerate(a['obstacles']))
assert all(footprint(o)==(1,1) for o in (75,28,76))
assert stage(75,'behavior')==3 and stage(76,'behavior')==0
assert all(not any(a[k]) for k in ('wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes'))
assert {i for i,o in enumerate(a['obstacles']) if o==75}==set(range(0,99,9))
assert {i for i,o in enumerate(a['obstacles']) if o==28}=={y*9+x for y in (9,10) for x in range(3,9)}
for key in ('stackedObstacles','tubes','magnets','safes','ancientSeals','tarpStatues','paintCanBoxColors','tutorialSteps'):
    assert not parse_entries(t,key)
raws=re.findall(r'^  - pathCellIndices: ([0-9a-f]+)$',t,re.M)
paths=[list(struct.unpack('<'+str(len(raw)//8)+'i',bytes.fromhex(raw))) for raw in raws]
assert paths==[entry['pathCellIndices'] for entry in d['waterChannels']]
assert len(paths)==1 and len(paths[0])==21
path=paths[0];assert len(set(path))==len(path)
assert all(0<=i<99 and a['cells'][i] and not a['obstacles'][i] for i in path),'No canal/depot overlap with solids'
for prev,nxt in zip(path,path[1:]):
    assert abs(prev%9-nxt%9)+abs(prev//9-nxt//9)==1
    assert nxt//9>=prev//9,'Water cannot flow uphill'
assert path[0]==2 and path[-1]==92 and path[-2]==83,'Bottom-facing outlet at (3,11)'
assert all(all(i not in other for other in paths[j+1:]) for j,pp in enumerate(paths) for i in pp)
runtime=a['obstacles'][:]
for i in path[1:]:runtime[i]=76
assert Counter(o for o in runtime if o)=={75:11,28:12,76:20}
goals=parse_entries(t,'goals')
assert goals==[{k:v for k,v in g.items() if k!='iconOverride'} for g in d['goals']]
assert {g['obstacleId']:g['amount'] for g in goals}=={76:1,75:24,28:12}
assert len(paths)==next(g['amount'] for g in goals if g['obstacleId']==76),'Goal counts channels, not cells'
assert next(g['amount'] for g in goals if g['obstacleId']==75)-11==13
# Source alone is runtime pass-through. All 20 opened channel cells stay ordinary playable cells.
depot={path[0]};blocked={i for i,o in enumerate(runtime) if o and (stage(o,'blocksCells') or stage(o,'behavior')==3)}
normal=set(range(99))-blocked-depot
assert len(normal)==75 and set(path[1:])<=normal
assert all(all(y*9+i%9 not in blocked for y in range(i//9+1)) for i in normal),'No hidden normal-tile refill pocket'
assert all(any(j in normal for j in (i-9,i+9) if 0<=j<99) or any(j in normal for j in (i-1,i+1) if 0<=j<99 and j//9==i//9) for i in path[1:])
assert len(re.findall(r'^    - sprite:',DEFS[75],re.M))==2
assert re.search(r'^    hits: 2$',DEFS[75],re.M) and re.search(r'^    hits: 1$',DEFS[76],re.M)
skin=(ROOT/'Assets/_Project/Resources/WaterChannelSkin.asset').read_text()
skinrefs=re.findall(r'guid: (\w+), type: [23]',skin)
metas={re.search(r'^guid: (\w+)',m.read_text(errors='replace'),re.M)[1]:m for m in (ROOT/'Assets/_Project').rglob('*.meta') if re.search(r'^guid: (\w+)',m.read_text(errors='replace'),re.M)}
assert all(g in metas for g in skinrefs)
hint=(ROOT/'Assets/_Project/Data/Tutorial/ObstacleHintLibrary.asset').read_text();assert re.search(r'^  - id: 76$',hint,re.M)
loc=(ROOT/'Assets/_Project/Resources/Localization/tinyfixers_localization.json').read_text();assert 'hint_obstacle_waterchannel' in loc
catalog=(ROOT/'Assets/_Project/Settings/LevelCatalogPro.asset').read_text();before=(HERE/'catalog-before.txt').read_text()
oldguid='742236c7e07594cd080ffc379607fde6'
assert catalog==before.replace(oldguid,d['guid'],1),'Only catalog 183 may change its reference'
assert f'guid: {d["guid"]}' in Path(str(p)+'.meta').read_text()
for n,guid in re.findall(r'    level: (\d+)\n    levelKey: [^\n]+\n    levelData: \{fileID: 11400000, guid: (\w+)',before):
    if int(n)>=183:continue
    prior=Path(str(metas[guid])[:-5]);tt=prior.read_text();sz=int_field(tt,'width')*int_field(tt,'height')
    assert 76 not in array(tt,'obstacles',sz)
    assert not re.search(r'^  waterChannels:\n  -',tt,re.M),'First production canal introduction'
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())
assert names['entries'][:-1]==json.loads((HERE/'names-before.json').read_text())['entries']
assert names['entries'][-1]==dict(level=183,levelKey='LevelCL_0183',name=d['title'])
baseline=json.loads((HERE/'protected-before.json').read_text())
changed=[path for path,h in baseline.items() if hashlib.sha256((ROOT/path).read_bytes()).hexdigest()!=h]
assert not changed,changed
old179=(HERE/'Before/LevelP_001830.asset').read_text();new179=(ROOT/'Assets/_Project/Settings/ProductionLevels/LevelP_001830.asset').read_text()
assert new179==old179.replace('  moves: 26\n','  moves: 31\n',1),'179 preserves all masks, goals, colors and layers'
for file in (p,ROOT/'Assets/_Project/Settings/ProductionLevels/LevelP_001830.asset'):
    parsed=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(file)],text=True));assert parsed['MonoBehaviour']['width']==9
report=dict(level=183,passed=True,active=99,authoredHoles=0,runtimeSourcePassThroughCells=1,openingTileCells=75,directRefillReachable=75,
    initialLantern=11,lanternGoal=24,additionalLanternQuota=13,stone=12,channelCount=1,channelCellsToOpen=20,moves=28,
    catalog179MovesBefore=26,catalog179MovesAfter=31,catalog179OnlyMovesChanged=True,previousTestAssetPreserved=True,
    protectedSettingsScriptsResourcesUnchanged=len(baseline),unityBuildRun=False,gameplaySimulationRun=False,winRateMeasured=False)
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(report,ensure_ascii=False,indent=2))
