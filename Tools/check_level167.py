#!/usr/bin/env python3
"""Reference layout, goal semantics and automatic-fill checks; no Unity/bot simulation."""
from collections import Counter, deque
from pathlib import Path
import hashlib,json,re

from export_level_previews import int_field,parse_entries
from check_levels161_162 import ROOT,KEYS,DEFS,array,footprint,stage

SETTINGS=ROOT/'Assets/_Project/Settings'
PATH=SETTINGS/'ProductionLevels/LevelP_001710.asset'
text=PATH.read_text()
assert (int_field(text,'width'),int_field(text,'height')) == (9,11)
assert int_field(text,'levelKind') == 0 and int_field(text,'moves') == 40
data={key:array(text,key,99) for key in KEYS}
assert array(text,'randomPool',4) == [0,1,2,3]
assert not any(data['pinnedTileTypes']) and not any(data['pinnedSpecialTypes']), 'Normal colors/specials must fill automatically'
assert not any(data['hamsterSatietyByCell']) and int_field(text,'hamsterSatietyOverride') == 4
active={i for i,v in enumerate(data['cells']) if v}
holes=set(range(99))-active
assert len(active)==95 and holes == {45,54,63,72}
obs,origins=data['obstacles'],data['obstacleOrigins']
assert all(obs[i]==0 and origins[i]==-1 for i in holes)
expected_tubes=[(0,3,6),(9,3,6),(18,3,6),(96,0,11)]
tubes=parse_entries(text,'tubes')
assert [(t['originCellIndex'],t['direction'],t['length']) for t in tubes] == expected_tubes
claimed=set()
counts=Counter()
for origin,direction,length in expected_tubes:
    dx,dy={0:(0,-1),1:(0,1),2:(-1,0),3:(1,0)}[direction]
    coords=[(origin%9+k*dx,origin//9+k*dy) for k in range(length)]
    assert all(0<=x<9 and 0<=y<11 for x,y in coords)
    indices={y*9+x for x,y in coords}
    assert not indices&claimed and indices<=active
    assert all(obs[i]==26 and origins[i]==origin for i in indices)
    assert {i for i,v in enumerate(origins) if v==origin} == indices
    claimed|=indices;counts[26]+=1
assert len(claimed)==29
# The bottom-left laser is a 2x2 overlay, with exactly one gel cell buried beneath it.
assert parse_entries(text,'stackedObstacles') == [dict(originCellIndex=81,obstacleId=65,stackOrder=1)]
assert {i for i,v in enumerate(obs) if v==46} == {90} and origins[90]==90
assert all(obs[i]==0 and origins[i]==-1 for i in (81,82,91))
top,top_origins=obs[:],origins[:]
for i in (81,82,90,91):top[i]=65;top_origins[i]=81
multi=[(65,27,2,2),(65,31,2,2),(65,81,2,2),(65,85,2,2),
       (70,46,2,2),(70,48,2,2),(70,64,2,2),(70,66,2,2)]
for oid,origin,w,h in multi:
    assert footprint(oid)==(w,h)
    x,y=origin%9,origin//9
    indices={(y+dy)*9+x+dx for dy in range(h) for dx in range(w)}
    assert x+w<=9 and y+h<=11 and indices<=active and not indices&claimed
    assert all(top[i]==oid and top_origins[i]==origin for i in indices)
    assert {i for i,v in enumerate(top_origins) if v==origin} == indices
    claimed|=indices;counts[oid]+=1
metal={29,30,38,39,83,84,92,93}
hamsters={50,59,68,77}
assert {i for i,v in enumerate(obs) if v==56} == metal
assert {i for i,v in enumerate(obs) if v==57} == hamsters
for piece,(origin,indices) in enumerate(((29,{29,30,38,39}),(83,{83,84,92,93})),1):
    assert all(data['wallPieceIds'][i]==piece and origins[i]==origin for i in indices), 'Each 2x2 metal region must be one shared piece'
    assert {i for i,v in enumerate(origins) if v==origin} == indices, 'Metal pieces stay independent of each other'
assert all(v==0 for i,v in enumerate(data['wallPieceIds']) if i not in metal)
assert not claimed&metal
claimed|=metal;counts[56]=2
for oid,indices in ((57,hamsters),):
    assert not claimed&indices
    assert all(origins[i]==i for i in indices)
    claimed|=indices;counts[oid]+=len(indices)
assert claimed == {i for i,v in enumerate(top) if v}
assert counts == {26:4,65:4,70:4,56:2,57:4}
assert not set(obs)&{59,60,61,62} and not set(top)&{59,60,61,62}, 'No gel launcher remains'
normal=active-claimed
assert normal == {y*9+x for y in range(11) for x in (7,8)}
assert all(origins[i]==-1 for i in normal)
# Both normal columns have direct spawn, without authored/pinned starting tiles.
assert all(all(obs[row*9+i%9]==0 and data['cells'][row*9+i%9] for row in range(i//9+1)) for i in normal)
goals=parse_entries(text,'goals')
assert [(g['obstacleId'],g['amount']) for g in goals] == [(46,95),(56,2),(26,4),(70,4),(57,4)]
assert all(g['targetType']==1 for g in goals)
assert all(g['obstacleId'] not in (59,60,61,62,65) for g in goals), 'Gel cabinet/laser are not explicit goals'
assert goals[0]['amount']==len(active), 'Full coverage includes cells initially occupied by all obstacles'
assert stage(65,'blocksCells')==1 and re.search(r'^    coversCellFully: 1$',DEFS[65],re.M), 'Laser must hide the buried gel'
for key in ('safes','magnets','ancientSeals','paintCanBoxColors','tarpStatues','tutorialSteps'):
    assert not parse_entries(text,key)
# After blockers clear the active board is a single gel-reachable region.
reached=set();pending=deque([98])
while pending:
    i=pending.popleft()
    if i in reached:continue
    reached.add(i)
    x,y=i%9,i//9
    for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
        nx,ny=x+dx,y+dy;j=ny*9+nx
        if 0<=nx<9 and 0<=ny<11 and j in active and j not in reached:pending.append(j)
assert reached==active
assert int_field((ROOT/'Assets/_Project/Prefabs/UI/PreLevelSpecialPopup.prefab').read_text(),'maxPreviewGoals')>=5
hud=(ROOT/'Assets/_Project/Scripts/UI/TopHUD/TopHudController.cs').read_text()
assert 'bool compactRow = count > 4' in hud and 'compactRow ? count' in hud
catalog=(SETTINGS/'LevelCatalogPro.asset').read_text()
guid=re.search(r'^guid: (\w+)$',Path(str(PATH)+'.meta').read_text(),re.M)[1]
assert re.search(r'  - chapter: 7\n    level: 167\n    levelKey: LevelCL_0167\n    levelData: \{fileID: 11400000, guid: '+guid,catalog)
assert len(re.findall(r'^    level: 167$',catalog,re.M))==1
for p in (ROOT/'Assets/_Project/Scripts/Core/LevelData.cs',):
    source=p.read_text();assert re.search(r'MaxHeight = 11;',source)
print('PASS 167: 9x11 / 95 active; two shared 2x2 metal pieces; three upper tubes + one vertical; 22 automatic tile cells; one buried gel; no launcher; goals valid.')
baseline=Path('/private/tmp/tinyfixers_level167_hidden_gel_baseline.json')
if baseline.exists():
    before=json.loads(baseline.read_text())
    for p,value in before.items():
        payload=(ROOT/p).read_bytes()
        if p=='Assets/_Project/Settings/LevelCatalogPro.asset':
            # Later levels may append catalog entries; the original prefix must stay exact.
            blocks=re.split(r'(?=^  - chapter:)',payload.decode(),flags=re.M)
            payload=''.join(block for block in blocks if not (m:=re.search(r'^    level: (\d+)$',block,re.M)) or int(m[1])<=167).encode()
        assert hashlib.sha256(payload).hexdigest()==value,p
    print(f'PASS: {len(before)} protected assets/settings and catalog through 167 unchanged; later catalog additions allowed.')
print('Static geometry/configuration checks only; no Unity Play/build, bot, cascade or win-rate simulation.')
