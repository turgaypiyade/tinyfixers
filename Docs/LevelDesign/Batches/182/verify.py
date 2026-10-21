"""Static masks, origins, goals, refill, sprites, hint and preservation checks; no simulation."""
from pathlib import Path
from collections import Counter,deque
import hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import KEYS,DEFS,array,footprint,stage
from export_level_previews import int_field,parse_entries
d=json.loads((HERE/'design.json').read_text());p=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset');t=p.read_text()
assert (int_field(t,'width'),int_field(t,'height'),int_field(t,'moves'),int_field(t,'levelKind'))==(9,11,28,0)
a={k:array(t,k,99) for k in KEYS};assert a==d['data']
assert array(t,'randomPool',4)==[0,1,2,3]
active={i for i,v in enumerate(a['cells']) if v};assert len(active)==81
assert all(a['cells'][i] or (not a['obstacles'][i] and a['obstacleOrigins'][i]==-1) for i in range(99))
counts=Counter(o for o in a['obstacles'] if o);assert counts=={75:11,30:21}
v1=json.loads((HERE/'Revisions/v1/design.json').read_text())
assert a['cells']==v1['data']['cells'],'Preserve the complete board silhouette'
assert {i for i,o in enumerate(a['obstacles']) if o==75}=={y*9+3 for y in range(11)},'One continuous full-height Lantern column'
assert {i for i,o in enumerate(a['obstacles']) if o==30}=={y*9+x for y in range(4,11) for x in range(4,7)},'One 3x7 porcelain rectangle immediately right of Lanterns'
assert d['rows']==[''.join('.' if not a['cells'][y*9+x] else {0:'o',75:'L',30:'H'}[a['obstacles'][y*9+x]] for x in range(9)) for y in range(11)]
assert all(a['obstacleOrigins'][i]==(i if a['obstacles'][i] else -1) for i in range(99))
assert all(footprint(o)==(1,1) and stage(o,'behavior')==3 for o in counts)
assert not any(a[k][i] for k in ('wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes') for i in range(99))
assert [sum(a['cells'][y*9:y*9+9]) for y in range(11)]==[5,7,9,9,9,9,9,7,7,5,5]
goals=parse_entries(t,'goals');assert goals==[{k:v for k,v in g.items() if k!='iconOverride'} for g in d['goals']]
assert d['goals']==v1['goals'],'Preserve the goal workload'
assert all(g['targetType']==1 and g['amount']>=counts[g['obstacleId']] for g in goals)
deficit={g['obstacleId']:g['amount']-counts[g['obstacleId']] for g in goals}
assert deficit=={75:19,30:0},'Only Lantern uses the existing goal refill quota'
for k in ('stackedObstacles','tubes','magnets','safes','ancientSeals','tarpStatues','paintCanBoxColors','tutorialSteps'):assert not parse_entries(t,k)
blocked={i for i,o in enumerate(a['obstacles']) if o};normal=active-blocked;assert len(normal)==49
assert all(all(not a['obstacles'][y*9+i%9] for y in range(i//9+1)) for i in normal),'All opening tiles have vertical refill through mask holes'
seen=set();q=deque([min(active)])
while q:
    i=q.popleft()
    if i in seen:continue
    seen.add(i)
    for dx,dy in ((-1,0),(1,0),(0,-1),(0,1)):
        x,y=i%9+dx,i//9+dy
        if 0<=x<9 and 0<=y<11 and y*9+x in active-seen:q.append(y*9+x)
assert seen==active
nodes=Counter()
for ny in range(12):
    for nx in range(10):nodes[sum(bit for bit,(x,y) in zip((1,2,4,8),((nx-1,ny-1),(nx,ny-1),(nx,ny),(nx-1,ny))) if 0<=x<9 and 0<=y<11 and y*9+x in active)]+=1
assert not nodes[5] and not nodes[10]
assert len(re.findall(r'^    - sprite:',DEFS[75],re.M))==2
assert int(re.search(r'^    hits: (\d+)$',DEFS[75],re.M)[1])==2
assert all(g in DEFS[75] for g in ('3764b89e5af4487384cfebfe9cd4e428','127c84f422d442eaa045deb663e1494a'))
for name,guid in (('Lantern_1_Unlit','3764b89e5af4487384cfebfe9cd4e428'),('Lantern_2_Lit','127c84f422d442eaa045deb663e1494a')):
    meta=(ROOT/'Assets/_Project/Art/UI/Obstacles/Lantern'/(name+'.png.meta')).read_text();assert f'guid: {guid}' in meta and 'spriteMode: 1' in meta
hint=(ROOT/'Assets/_Project/Data/Tutorial/ObstacleHintLibrary.asset').read_text();assert re.search(r'^  - id: 75$',hint,re.M)
loc=json.loads((ROOT/'Assets/_Project/Resources/Localization/tinyfixers_localization.json').read_text());assert 'hint_obstacle_lantern' in json.dumps(loc)
before=(HERE/'catalog-before.txt').read_text();catalog=(ROOT/'Assets/_Project/Settings/LevelCatalogPro.asset').read_text()
assert catalog==before+f'  - chapter: 7\n    level: 182\n    levelKey: LevelCL_0182\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n'
assert f'guid: {d["guid"]}' in Path(str(p)+'.meta').read_text()
metas={re.search(r'^guid: (\w+)$',m.read_text(),re.M)[1]:Path(str(m)[:-5]) for m in p.parent.glob('*.asset.meta')}
prior=set()
for guid in re.findall(r'levelData: \{fileID: 11400000, guid: (\w+)',before):
    tt=metas[guid].read_text();prior.update(array(tt,'obstacles',int_field(tt,'width')*int_field(tt,'height')))
assert 30 in prior and 75 not in prior,'Explicit user-authorized Lantern introduction'
baseline=json.loads((HERE/'protected-before.json').read_text())
external=json.loads((HERE/'external-baseline-drift.json').read_text())
assert set(external)=={'Assets/_Project/Settings/ObstacleLibrary.asset'}
for path,entry in external.items():
    assert baseline[path]==entry['originalHash']
    assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==entry['observedHash'],'Preserve existing library changes'
assert all(hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==h for path,h in baseline.items() if path not in external)
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text());assert names['entries'][:-1]==json.loads((HERE/'names-before.json').read_text())['entries']
assert names['entries'][-1]==dict(level=182,levelKey='LevelCL_0182',name=d['title'])
parsed=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(p)],text=True));assert parsed['MonoBehaviour']['moves']==28
report=dict(level=182,revision=2,passed=True,active=81,holes=18,openingTileCells=49,directRefillReachable=49,activeMaskComponents=1,diagonalCornerOnlyNodes=0,obstacleInstances=dict(counts),lanternColumnOneBased=4,lanternColumnLength=11,porcelainBlock=[3,7],boardMaskUnchanged=True,goalsUnchanged=True,moves=28,goalDemandAboveInitial=deficit,previousSettingsAndScriptsMatchingV1Baseline=len(baseline)-len(external),existingLibraryChangePreserved=list(external),firstLanternProductionLevel=True,unityBuildRun=False,gameplaySimulationRun=False,winRateMeasured=False)
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(report,ensure_ascii=False,indent=2))
