#!/usr/bin/env python3
"""Production asset/goal/layer and possible spawn route checks; no game simulation."""
from collections import Counter, deque
from pathlib import Path
import hashlib
import json
import re
import struct

from export_level_previews import int_field, parse_entries
from check_levels161_162 import ROOT, DEFS, KEYS, array, footprint, stage

SETTINGS = ROOT / 'Assets/_Project/Settings'
CATALOG = (SETTINGS / 'LevelCatalogPro.asset').read_text()
EXPECTED = {
    163: dict(asset='001670', moves=28, goals={63:4,25:24,30:24}, counts={63:4,25:24,30:24}, tiles=59, pins={}),
    164: dict(asset='001680', moves=26, goals={46:99,39:20}, counts={46:20,39:20,7:16,59:2}, tiles=59, pins={85:1,86:2}),
    165: dict(asset='001690', moves=30, goals={64:2,28:22,25:22}, counts={32:18,64:2,28:22,25:22}, tiles=59, pins={}),
    166: dict(asset='001700', moves=24, goals={70:2,25:24,6:12}, counts={70:2,25:24,39:24,6:12,38:2}, tiles=53, pins={40:5,41:2}),
}


def cells_at(origin, width, height):
    x,y = origin % 9,origin // 9
    assert 0 <= origin < 99 and x+width <= 9 and y+height <= 11
    return {(y+dy)*9+x+dx for dy in range(height) for dx in range(width)}


def check(number, expected):
    path = SETTINGS / 'ProductionLevels' / f'LevelP_{expected["asset"]}.asset'
    text = path.read_text()
    assert (int_field(text,'width'),int_field(text,'height')) == (9,11)
    assert int_field(text,'moves') == expected['moves'] and int_field(text,'levelKind') == 0
    assert array(text,'randomPool',4) == [0,1,2,3]
    a = {k:array(text,k,99) for k in KEYS}
    assert a['cells'] == [1]*99 and not any(a['wallPieceIds']) and not any(a['hamsterSatietyByCell'])
    obs,origins = a['obstacles'],a['obstacleOrigins']
    counts = Counter()
    for i,oid in enumerate(obs):
        if not oid:
            assert origins[i] == -1
            continue
        assert oid in DEFS
        origin = origins[i]
        assert obs[origin] == oid and origins[origin] == origin
        assert cells_at(origin,*footprint(oid)) == {j for j,v in enumerate(origins) if v == origin}
        if i == origin: counts[oid] += 1
    top = obs[:]
    covered = set()
    for s in parse_entries(text,'stackedObstacles'):
        i,oid = s['originCellIndex'],s['obstacleId']
        assert i not in covered and s['stackOrder'] == 1 and footprint(oid) == (1,1)
        assert obs[i] in (25,46) and oid not in (14,25,46,53), 'Liquids are always underneath solids'
        assert stage(obs[i],'behavior') != 3, 'Only one movable layer'
        covered.add(i);top[i] = oid;counts[oid] += 1
    seals = parse_entries(text,'ancientSeals')
    seal_cells = set()
    for s in seals:
        cells = cells_at(s['originCellIndex'],s['width'],s['height'])
        assert (s['width'],s['height']) == (3,3) and not cells & seal_cells
        assert s['stackOrder'] == 1 and all(obs[i] == 32 and i not in covered for i in cells)
        assert [s[k] for k in ('redCount','yellowCount','greenCount')] == [6,6,6]
        seal_cells |= cells
        for i in cells:top[i] = 64
        counts[64] += 1
    assert counts == expected['counts'], (number,counts)
    goals = parse_entries(text,'goals')
    assert {g['obstacleId']:g['amount'] for g in goals} == expected['goals']
    assert all(g['targetType'] == 1 and g['obstacleId'] not in (32,38,43,45,59,60,61,62) for g in goals)
    assert all(g['amount'] == (99 if g['obstacleId'] == 46 else counts[g['obstacleId']]) for g in goals)
    pins = {i:v for i,v in enumerate(a['pinnedSpecialTypes']) if v}
    assert pins == expected['pins']
    assert {i for i,v in enumerate(a['pinnedTileTypes']) if v} == set(pins)
    for i in pins:assert top[i] == 0 and 1 <= a['pinnedTileTypes'][i] <= 4
    if pins:
        p,q = pins
        assert p//9 == q//9 and abs(p-q) == 1, 'Ready combo partners are adjacent'
    for key in ('tubes','magnets','safes','tarpStatues','tutorialSteps'):
        assert not parse_entries(text,key)
    paint = parse_entries(text,'paintCanBoxColors')
    if number == 163:
        assert {(p['originCellIndex'],p['color']) for p in paint} == {(27,1),(34,4),(63,2),(70,3)}
        assert all(obs[p['originCellIndex']] == 63 and origins[p['originCellIndex']] == p['originCellIndex'] for p in paint)
        assert all(top[y*9+x] == 0 for y in range(11) for x in range(2,7)), 'Five-wide continuous workspace'
    else:assert not paint
    if number == 164:
        assert {origins[i] for i,o in enumerate(obs) if o == 59} == {82,88}
        assert {i for i,o in enumerate(obs) if o == 46 and i not in covered} == {39,41,57,59}
        assert all(top[y*9+x] in (0,46) for y in range(9) for x in range(2,7))
    if number == 165:
        assert [(s['originCellIndex'],s['startColor']) for s in seals] == [(72,1),(78,3)]
        assert len(seal_cells) == 18 and all(i//9 >= 8 for i in seal_cells)
        assert all(top[y*9+x] == 0 for y in range(8) for x in range(2,7))
    if number == 166:
        assert {i for i,o in enumerate(obs) if o == 38} == {93,94}, 'Helpers grouped on bottom edge'
        assert int_field(text,'rocketBasketFireAllOnHit') == 0
    # Potential refill paths while authored blockers remain. This does not predict
    # exact random opening swaps, cascades, special damage or victory probability.
    blocked = {i for i,oid in enumerate(top) if oid and (stage(oid,'blocksCells') or stage(oid,'behavior') == 3)}
    playable = set(range(99)) - blocked
    reached = set();queue = deque(i for i in range(9) if i not in blocked)
    while queue:
        i = queue.popleft()
        if i in reached:continue
        reached.add(i)
        if i//9 == 10:continue
        if i+9 not in blocked:queue.append(i+9)
        for dx in (-1,1):
            j = i+9+dx
            if not 0 <= i%9+dx < 9 or j not in playable:continue
            if any(k not in blocked or stage(top[k],'allowDiagonal') for k in (i+dx,i+9)):
                queue.append(j)
    assert not playable-reached, (number,'Opening cells lack a spawn/allowed corner route',sorted(playable-reached))
    assert len(playable) == expected['tiles']
    guid = re.search(r'^guid: (\w+)$',Path(str(path)+'.meta').read_text(),re.M)[1]
    assert re.search(r'  - chapter: 7\n    level: '+str(number)+r'\n    levelKey: LevelCL_0'+str(number)+r'\n    levelData: \{fileID: 11400000, guid: '+guid, CATALOG)
    print(f'PASS {number}: 9x11, {expected["moves"]} moves, {len(playable)} spawn-accessible tile cells; goals, layers, footprints, pins and catalog valid.')


def check_introductions():
    paths = {}
    for meta in (SETTINGS/'ProductionLevels').glob('*.asset.meta'):
        paths[re.search(r'^guid: (\w+)$',meta.read_text(),re.M)[1]] = Path(str(meta)[:-5])
    first = {}
    for number,guid in re.findall(r'    level: (\d+)\n    levelKey: [^\n]+\n    levelData: \{fileID: 11400000, guid: (\w+)',CATALOG):
        if guid not in paths:continue
        text = paths[guid].read_text()
        n = int_field(text,'width')*int_field(text,'height')
        used = set(array(text,'obstacles',n)) - {0}
        used.update(s['obstacleId'] for s in parse_entries(text,'stackedObstacles'))
        if parse_entries(text,'ancientSeals'):used.add(64)
        for oid in used:first[oid] = min(first.get(oid,int(number)),int(number))
    required = set().union(*(d['counts'] for d in EXPECTED.values()))
    assert all(first.get(oid,9999) < 163 for oid in required), (first,required)
    print('PASS: all selected obstacle families were introduced before 163; no surprise first-use mechanic.')


def check_optional_baseline():
    baseline = Path('/private/tmp/tinyfixers_levels163_166_baseline.json')
    old_catalog = Path('/private/tmp/tinyfixers_catalog_before163_166.asset')
    if baseline.exists() and old_catalog.exists():
        before = json.loads(baseline.read_text())
        assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest() == digest for p,digest in before.items()), 'Pre-existing level/shared asset changed'
        assert CATALOG.startswith(old_catalog.read_text()), 'Previous catalog entries changed'
        print(f'PASS: {len(before)} pre-existing assets unchanged and previous catalog prefix preserved.')


if __name__ == '__main__':
    for number,expected in EXPECTED.items():check(number,expected)
    check_introductions()
    check_optional_baseline()
    print('Static checks only. Difficulty/moves are initial settings; Unity, bots and game simulation were not run.')
