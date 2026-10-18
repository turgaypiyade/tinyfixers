#!/usr/bin/env python3
"""Level 168 authoring, layers and geometric refill checks; no game simulation."""
from collections import Counter,deque
from pathlib import Path
import hashlib,json,re
from export_level_previews import int_field,parse_entries
from check_levels161_162 import ROOT,KEYS,DEFS,array,footprint,stage

settings=ROOT/'Assets/_Project/Settings'
path=settings/'ProductionLevels/LevelP_001720.asset';text=path.read_text()
assert (int_field(text,'width'),int_field(text,'height'),int_field(text,'moves'),int_field(text,'levelKind'))==(9,11,32,0)
data={key:array(text,key,99) for key in KEYS}
assert data['cells']==[1]*99 and array(text,'randomPool',4)==[0,1,2,3]
assert not any(data['pinnedTileTypes']) and not any(data['pinnedSpecialTypes'])
assert not any(data['wallPieceIds']) and not any(data['hamsterSatietyByCell'])
obs,origins=data['obstacles'],data['obstacleOrigins'];counts=Counter()
for i,oid in enumerate(obs):
    if oid==0:assert origins[i]==-1;continue
    origin=origins[i];w,h=footprint(oid);x,y=origin%9,origin//9
    assert oid in DEFS and 0<=origin<99 and x+w<=9 and y+h<=11
    expected={(y+dy)*9+x+dx for dy in range(h) for dx in range(w)}
    assert expected=={j for j,o in enumerate(origins) if o==origin}
    assert all(obs[j]==oid for j in expected)
    if i==origin:counts[oid]+=1
top=obs[:];covered=set()
for s in parse_entries(text,'stackedObstacles'):
    i=s['originCellIndex'];assert s==dict(originCellIndex=i,obstacleId=5,stackOrder=1)
    assert i not in covered and obs[i]==25 and footprint(5)==(1,1)
    assert stage(25,'behavior')!=3 and stage(5,'behavior')!=3, 'No multiple movable layers'
    top[i]=5;covered.add(i);counts[5]+=1
assert len(covered)==26 and counts=={27:2,25:26,5:26,40:6,43:1,45:2,38:2}
goals=parse_entries(text,'goals')
assert [(g['obstacleId'],g['amount']) for g in goals]==[(27,2),(5,26),(25,26),(40,6)]
assert all(g['targetType']==1 and g['amount']==counts[g['obstacleId']] for g in goals)
assert not any(g['obstacleId'] in (38,43,45) for g in goals), 'Helpers are not goals'
assert {origins[i] for i,o in enumerate(obs) if o==27}=={9,16}
assert {i for i,o in enumerate(obs) if o==43}=={84,85,93,94}
assert {i for i,o in enumerate(obs) if o==45}=={86,87}
assert {i for i,o in enumerate(obs) if o==38}=={95,96}
assert int_field(text,'rocketBasketFireAllOnHit')==1, 'Loaded rockets launch together for the intended salvo'
assert all(top[6*9+x]==0 for x in range(9)), 'Full-width middle workspace'
assert all(top[y*9+x]==0 for y in (3,4,7,8) for x in range(2,7)), 'Five-wide special production lanes'
for key in ('tubes','magnets','safes','ancientSeals','paintCanBoxColors','tarpStatues','tutorialSteps'):
    assert not parse_entries(text,key)
# Each authored normal cell must be able to receive an automatic tile while blockers remain.
blocked={i for i,o in enumerate(top) if o and (stage(o,'blocksCells') or stage(o,'behavior')==3)}
playable=set(range(99))-blocked;reached=set();pending=deque(i for i in range(9) if i not in blocked)
while pending:
    i=pending.popleft()
    if i in reached:continue
    reached.add(i)
    if i//9==10:continue
    if i+9 not in blocked:pending.append(i+9)
    for dx in (-1,1):
        j=i+9+dx
        if not 0<=i%9+dx<9 or j not in playable:continue
        if any(k not in blocked or stage(top[k],'allowDiagonal') for k in (i+dx,i+9)):pending.append(j)
assert playable==reached,(sorted(playable-reached),'Opening cells lack a refill route')
assert len(playable)==51
catalog=(settings/'LevelCatalogPro.asset').read_text()
guid=re.search(r'^guid: (\w+)$',Path(str(path)+'.meta').read_text(),re.M)[1]
assert re.search(r'  - chapter: 7\n    level: 168\n    levelKey: LevelCL_0168\n    levelData: \{fileID: 11400000, guid: '+guid,catalog)
assert len(re.findall(r'^    level: 168$',catalog,re.M))==1
by_guid={re.search(r'^guid: (\w+)$',p.read_text(),re.M)[1]:Path(str(p)[:-5]) for p in path.parent.glob('*.asset.meta')}
seen=set()
for number,g in re.findall(r'    level: (\d+)\n    levelKey: [^\n]+\n    levelData: \{fileID: 11400000, guid: (\w+)',catalog):
    if int(number)>=168 or g not in by_guid:continue
    old=by_guid[g].read_text();n=int_field(old,'width')*int_field(old,'height')
    seen.update(array(old,'obstacles',n));seen.update(s['obstacleId'] for s in parse_entries(old,'stackedObstacles'))
assert set(counts)<=seen,'All families must already be introduced'
print('PASS 168: 9x11 full rectangle, 32 moves, 51 refill-accessible automatic tile cells; footprints, 26 liquid-base stacks, goals, helper cluster and prior introductions valid.')
baseline=Path('/private/tmp/tinyfixers_level168_baseline.json');old_catalog=Path('/private/tmp/tinyfixers_catalog_before168.asset')
if baseline.exists() and old_catalog.exists():
    before=json.loads(baseline.read_text())
    assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==value for p,value in before.items())
    assert catalog.startswith(old_catalog.read_text())
    print(f'PASS: {len(before)} existing asset/settings files unchanged; previous catalog prefix retained.')
print('Static checks only; no Unity Play/build, bot, cascade or win-rate simulation.')
