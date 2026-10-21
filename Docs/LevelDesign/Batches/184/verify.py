"""Static geometry, goals, resource binding and preservation. No gameplay execution."""
from pathlib import Path
from collections import Counter,deque
import hashlib,json,re,struct,subprocess,sys
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import KEYS,array,DEFS,stage,footprint
from export_level_previews import int_field,parse_entries
from review_level_patterns import path_entries
d=json.loads((HERE/'design.json').read_text());asset=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset');t=asset.read_text()
assert (int_field(t,'width'),int_field(t,'height'),int_field(t,'moves'),int_field(t,'levelKind'))==(9,11,28,0)
a={k:array(t,k,99) for k in KEYS};assert a==d['data'] and array(t,'randomPool',4)==[0,1,2,3]
active={i for i,v in enumerate(a['cells']) if v};assert len(active)==73
assert [sum(a['cells'][y*9:y*9+9]) for y in range(11)]==[9]*4+[7]*4+[3]*3
assert all(i in active or a['obstacles'][i]==0 and a['obstacleOrigins'][i]==-1 for i in range(99))
counts=Counter()
for i,o in enumerate(a['obstacles']):
    if not o:assert a['obstacleOrigins'][i]==-1;continue
    origin=a['obstacleOrigins'][i];w,h=footprint(o)
    expected={origin+dy*9+dx for dy in range(h) for dx in range(w)}
    actual={j for j,v in enumerate(a['obstacleOrigins']) if v==origin}
    assert actual==expected and expected<=active and all(a['obstacles'][j]==o for j in expected)
    if origin==i:counts[o]+=1
assert counts=={39:8,63:4}
assert {i for i,o in enumerate(a['obstacles']) if o==63 and a['obstacleOrigins'][i]==i}=={38,40,56,58}
assert not any(a[k][i] for k in ('wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes') for i in range(99))
paths=path_entries(t,'waterChannels');assert paths==d['waterChannels'] and len(paths)==2
seen=set();sources=set();canal=set()
for entry in paths:
    p=entry['pathCellIndices'];assert len(p)>=2 and len(p)==len(set(p))
    assert not (set(p)&seen);seen.update(p);sources.add(p[0]);canal.update(p[1:])
    assert set(p)<=active and all(not a['obstacles'][i] for i in p)
    assert all(abs(x%9-y%9)+abs(x//9-y//9)==1 and y//9>=x//9 for x,y in zip(p,p[1:]))
    assert p[-1]//9==10 and p[-1]-p[-2]==9,'Two bottom-facing outlets'
assert sources=={3,8} and len(canal)==23
blocked={i for i,o in enumerate(a['obstacles']) if o and (stage(o,'blocksCells') or stage(o,'behavior')==3)}
normal=active-blocked-sources;assert len(normal)==47 and canal<=normal
assert all(all(not a['obstacles'][y*9+i%9] for y in range(i//9+1)) for i in normal),'Every authored normal tile has a direct vertical route; source/mask holes pass through'
q=deque([min(active)]);visited=set()
while q:
    i=q.popleft()
    if i in visited:continue
    visited.add(i)
    for dx,dy in ((-1,0),(1,0),(0,-1),(0,1)):
        x,y=i%9+dx,i//9+dy;j=y*9+x
        if 0<=x<9 and 0<=y<11 and j in active-visited:q.append(j)
assert visited==active
nodes=Counter(sum(bit for bit,(x,y) in zip((1,2,4,8),((nx-1,ny-1),(nx,ny-1),(nx,ny),(nx-1,ny))) if 0<=x<9 and 0<=y<11 and y*9+x in active) for nx in range(10) for ny in range(12))
assert not nodes[5] and not nodes[10]
goals=parse_entries(t,'goals');assert goals==[{k:v for k,v in g.items() if k!='iconOverride'} for g in d['goals']]
assert {g['obstacleId']:g['amount'] for g in goals}=={63:4,76:2,39:8}
for key in ('stackedObstacles','tubes','magnets','safes','ancientSeals','tarpStatues','paintCanBoxColors','tutorialSteps'):assert not parse_entries(t,key)
assert re.search(r'^    hits: 9$',DEFS[63],re.M) and footprint(63)==(2,2) and stage(39,'damageRule')==1
catalog=(ROOT/'Assets/_Project/Settings/LevelCatalogPro.asset').read_text();before=(HERE/'catalog-before.txt').read_text()
assert catalog==before+f'  - chapter: 8\n    level: 184\n    levelKey: LevelCL_0184\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n'
assert f'guid: {d["guid"]}' in Path(str(asset)+'.meta').read_text()
metas={re.search(r'^guid: (\w+)$',p.read_text(),re.M)[1]:Path(str(p)[:-5]) for p in asset.parent.glob('*.asset.meta')}
prior=set();prior_channels=False
for guid in re.findall(r'levelData: \{fileID: 11400000, guid: (\w+)',before):
    tt=metas[guid].read_text();prior.update(array(tt,'obstacles',int_field(tt,'width')*int_field(tt,'height')));prior_channels|=bool(path_entries(tt,'waterChannels'))
assert {39,63}<=prior and prior_channels
baseline=json.loads((HERE/'protected-before.json').read_text());changed=[p for p,h in baseline.items() if hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h];assert not changed,changed
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text());assert names['entries'][:-1]==json.loads((HERE/'names-before.json').read_text())['entries']
assert names['entries'][-1]==dict(level=184,levelKey='LevelCL_0184',name=d['title'])
parsed=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(asset)],text=True));assert len(parsed['MonoBehaviour']['waterChannels'])==2
report=dict(level=184,passed=True,active=73,holes=26,runtimeSourceHoles=2,openingTileCells=47,directRefillReachable=47,paintBoxes=4,paintCans=36,specialOnlyTargets=8,channels=2,channelCellsToOpen=23,moves=28,activeMaskComponents=1,diagonalCornerOnlyNodes=0,previousSettingsScriptsResourcesUnchanged=len(baseline),unityBuildRun=False,gameplaySimulationRun=False,winRateMeasured=False)
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(report,ensure_ascii=False,indent=2))
