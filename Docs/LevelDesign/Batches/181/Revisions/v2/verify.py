"""Static asset/layer/refill verification; no Unity or gameplay simulation."""
from pathlib import Path
from collections import Counter
import hashlib,json,re,sys,subprocess
ROOT=Path(__file__).resolve().parents[4]
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import array,KEYS,footprint,stage
from export_level_previews import int_field,parse_entries
HERE=Path(__file__).resolve().parent
d=json.loads((HERE/'design.json').read_text())
p=ROOT/'Assets/_Project/Settings/ProductionLevels'/(d['asset']+'.asset')
t=p.read_text();a={k:array(t,k,99) for k in KEYS}
assert int_field(t,'width')==9 and int_field(t,'height')==11 and int_field(t,'moves')==26
assert int_field(t,'levelKind')==0 and a==d['data'] and a['cells']==[1]*99
assert array(t,'randomPool',4)==[0,1,2,3]
assert all(not any(a[k]) for k in ('pinnedTileTypes','pinnedSpecialTypes','wallPieceIds','hamsterSatietyByCell'))
counts=Counter();layers=[[] for _ in range(99)];objects={}
for i,o in enumerate(a['obstacles']):
    if o:assert o==25 and a['obstacleOrigins'][i]==i;layers[i].append((o,i));counts[o]+=1;objects[(o,i)]={i}
    else:assert a['obstacleOrigins'][i]==-1
stacks=parse_entries(t,'stackedObstacles');assert stacks==d['stacks']
for s in sorted(stacks,key=lambda s:s['stackOrder']):
    o,i=s['obstacleId'],s['originCellIndex'];w,h=footprint(o);x,y=i%9,i//9
    assert x+w<=9 and y+h<=11 and o not in (14,25,46,53)
    region={(y+dy)*9+x+dx for dy in range(h) for dx in range(w)}
    for j in region:
        assert layers[j] and a['cells'][j]
        # A larger cover may hide several complete smaller objects, never part of one.
        assert objects[layers[j][-1]]<=region
        assert sum(stage(oid,'behavior')==3 for oid,origin in layers[j])+int(stage(o,'behavior')==3)<=1
    objects[(o,i)]=region;counts[o]+=1
    for j in region:layers[j].append((o,i))
assert counts=={21:6,27:4,28:16,25:40}
assert max(map(len,layers))==3
wardrobe_origins={27,47,67,87}
assert {s['originCellIndex'] for s in stacks if s['obstacleId']==27}==wardrobe_origins
assert {i for i,z in enumerate(layers) if len(z)==3}=={origin+dy*9+dx for origin in wardrobe_origins for dy in range(2) for dx in range(2)}
assert not (set(counts)&{71,72,73,74})
goals=parse_entries(t,'goals');assert all(g['targetType']==1 and g['amount']==counts[g['obstacleId']] for g in goals)
assert {g['obstacleId'] for g in goals}==set(counts)
for k in ('tubes','magnets','safes','ancientSeals','paintCanBoxColors','tarpStatues','tutorialSteps'):assert not parse_entries(t,k)
opening={i for i,z in enumerate(layers) if not z}
assert len(opening)==59
# The solid staircase grows toward the bottom: all initial normal cells have a direct vertical spawn path.
assert all(all(not layers[y*9+i%9] for y in range(i//9+1)) for i in opening)
assert all(any(j in opening for j in (i-9,i+9,i-1,i+1) if 0<=j<99 and (j//9==i//9 or j%9==i%9)) for i in wardrobe_origins)
catalog=(ROOT/'Assets/_Project/Settings/LevelCatalogPro.asset').read_text();before=(HERE/'catalog-before.txt').read_text()
assert catalog==before+f'  - chapter: 7\n    level: 181\n    levelKey: LevelCL_0181\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n'
assert re.search(r'^guid: (\w+)$',Path(str(p)+'.meta').read_text(),re.M)[1]==d['guid']
metas={re.search(r'^guid: (\w+)$',m.read_text(),re.M)[1]:Path(str(m)[:-5]) for m in p.parent.glob('*.asset.meta')}
last={}
for n,g in re.findall(r'    level: (\d+)\n    levelKey: [^\n]+\n    levelData: \{fileID: 11400000, guid: (\w+)',before):
    tx=metas[g].read_text();size=int_field(tx,'width')*int_field(tx,'height')
    used=set(array(tx,'obstacles',size))|{s['obstacleId'] for s in parse_entries(tx,'stackedObstacles')}
    for o in counts:
        if o in used:last[o]=int(n)
assert set(last)==set(counts),'Only previously introduced obstacles'
baseline=json.loads((HERE/'protected-before.json').read_text())
assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in baseline.items())
names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())['entries']
assert len(names)==181 and len({e['name'] for e in names})==181
assert names[-1]==dict(level=181,levelKey='LevelCL_0181',name=d['title'])
parsed=json.loads(subprocess.check_output(['ruby','-rjson','-ryaml','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(p)],text=True))['MonoBehaviour'];assert parsed['moves']==26
assert Path(str(p)+'.meta').read_bytes()==(HERE/'Revisions/v1'/Path(str(p)+'.meta').name).read_bytes()
result=dict(level=181,revision=2,passed=True,yamlParsed=True,active=99,holes=0,openingTileCells=59,directRefillReachable=59,obstacleInstances=dict(counts),maxLayerDepth=3,stackedEntries=len(stacks),previousUse=last,protectedFilesUnchanged=len(baseline),unityBuildRun=False,gameplaySimulationRun=False,winRateMeasured=False)
(HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(result,ensure_ascii=False,indent=2))
