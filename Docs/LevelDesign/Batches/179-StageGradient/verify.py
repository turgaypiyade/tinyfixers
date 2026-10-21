"""Static authoring and preservation checks; no Unity build or gameplay simulation."""
from pathlib import Path
from collections import Counter
import hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import array,KEYS,DEFS
from export_level_previews import parse_entries,int_field
rel='Assets/_Project/Settings/ProductionLevels/LevelP_001830.asset'
t=(ROOT/rel).read_text();before=(HERE/'Before'/rel).read_text()
assert re.sub(r'^    initialRemainingHits: \d+\n','',t,flags=re.M)==before,'Preserve ALL user edits; only add per-stack HP'
assert all(array(t,k,99)==array(before,k,99) for k in KEYS)
assert int_field(t,'moves')==int_field(before,'moves')==31
assert parse_entries(t,'goals')==parse_entries(before,'goals')
stacks=parse_entries(t,'stackedObstacles');original=parse_entries(before,'stackedObstacles')
assert [{k:v for k,v in e.items() if k!='initialRemainingHits'} for e in stacks]==original
assert len(stacks)==24
assert Counter(e['initialRemainingHits'] for e in stacks)=={3:6,2:6,1:12}
for e in stacks:
    row=e['originCellIndex']//9
    assert e['initialRemainingHits']==(3 if row==3 else 2 if row==4 else 1)
    assert e['obstacleId'] in (72,73)
    definition=DEFS[e['obstacleId']]
    assert re.search(r'^    hits: 3$',definition,re.M),'Do not change shared crate defaults'
    assert len(re.findall(r'^    - sprite:',definition,re.M))==3
assert {i:v for i,v in enumerate(array(t,'pinnedSpecialTypes',99)) if v}=={20:4,21:2}
assert not any(array(t,'pinnedTileTypes',99))
baseline=json.loads((HERE/'protected-before.json').read_text())
changed=[p for p,h in baseline.items() if hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
assert not changed,changed
catalog=(ROOT/'Assets/_Project/Settings/LevelCatalogPro.asset').read_text()
guid=re.search(r'^guid: (\w+)$',Path(str(ROOT/rel)+'.meta').read_text(),re.M)[1]
assert re.search(r'    level: 179\n    levelKey: LevelCL_0179\n    levelData: .*'+guid,catalog)
parsed=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(ROOT/rel)],text=True))
assert [e['initialRemainingHits'] for e in parsed['MonoBehaviour']['stackedObstacles']]==[e['initialRemainingHits'] for e in stacks]
design=json.loads((HERE/'design.json').read_text())
assert design['data']=={k:array(t,k,99) for k in KEYS} and design['stacks']==stacks
report=dict(level=179,passed=True,userAssetPreservedExceptInitialHits=True,moves=31,specialPins={20:4,21:2},
    stageRows=[3,2,1,1],crateInstances=24,crateHitBudgetBefore=72,crateHitBudgetAfter=42,
    openingOpenTileCells=18,coveredHeldTileCells=12,
    protectedSettingsScriptsResourcesUnchanged=len(baseline),sharedLibraryUnchanged=True,
    unityBuildRun=False,unityPlayOrGameplaySimulationRun=False,csharpCompileRun=False,winRateMeasured=False)
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(report,ensure_ascii=False,indent=2))
