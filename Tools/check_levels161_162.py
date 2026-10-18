#!/usr/bin/env python3
"""Authored level, tutorial and geometric flow checks; no Unity/bot simulation."""
from collections import Counter, deque
from pathlib import Path
import json
import re
import struct

from export_level_previews import int_field, parse_entries

ROOT = Path(__file__).resolve().parents[1]
SETTINGS = ROOT / 'Assets/_Project/Settings'
LIBRARY = (SETTINGS / 'ObstacleLibrary.asset').read_text()
DEFS = {int(m[1]): m[2] for m in re.finditer(
    r'^  - id: (\d+)\n(.*?)(?=^  - id:|\Z)', LIBRARY, re.M | re.S)}
CATALOG = (SETTINGS / 'LevelCatalogPro.asset').read_text()
KEYS = ('cells', 'obstacles', 'obstacleOrigins', 'wallPieceIds',
        'hamsterSatietyByCell', 'pinnedTileTypes', 'pinnedSpecialTypes')


def array(text, key, length):
    match = re.search(r'^  ' + key + r': ([0-9a-f]+)$', text, re.M)
    assert match, key
    blob = bytes.fromhex(match[1])
    assert len(blob) == length * 4, (key, len(blob), length)
    return list(struct.unpack(f'<{length}i', blob))


def stage(oid, key):
    return int(re.search(r'^      ' + key + r': (\d+)$', DEFS[oid], re.M)[1])


def footprint(oid):
    match = re.search(r'^    size: \{x: (\d+), y: (\d+)\}$', DEFS[oid], re.M)
    return tuple(map(int, match.groups()))


def check(number, suffix):
    path = SETTINGS / 'ProductionLevels' / f'LevelP_{suffix}.asset'
    text = path.read_text()
    assert (int_field(text, 'width'), int_field(text, 'height')) == (9, 11)
    assert int_field(text, 'levelKind') == 0
    assert int_field(text, 'moves') == (28 if number == 161 else 24)
    a = {key: array(text, key, 99) for key in KEYS}
    assert a['cells'] == [1] * 99, 'Full rectangle; no island/choke-point mask'
    assert not any(a['wallPieceIds']) and not any(a['hamsterSatietyByCell'])
    assert array(text, 'randomPool', 4) == [0, 1, 2, 3]
    obs, origins = a['obstacles'], a['obstacleOrigins']
    counts = Counter()
    for i, oid in enumerate(obs):
        if not oid:
            assert origins[i] == -1
            continue
        assert oid in DEFS and 0 <= origins[i] < 99
        origin = origins[i]
        assert obs[origin] == oid and origins[origin] == origin
        width, height = footprint(oid)
        x, y = origin % 9, origin // 9
        assert x + width <= 9 and y + height <= 11
        expected = {(y + dy) * 9 + x + dx for dy in range(height) for dx in range(width)}
        actual = {j for j, value in enumerate(origins) if value == origin}
        assert expected == actual and all(obs[j] == oid for j in actual)
        if i == origin:
            counts[oid] += 1
    top = obs[:]
    covered = set()
    for s in parse_entries(text, 'stackedObstacles'):
        i, oid = s['originCellIndex'], s['obstacleId']
        assert i not in covered and obs[i] == 25 and oid == 8 and s['stackOrder'] == 1
        assert footprint(oid) == (1, 1)
        assert oid not in (14, 25, 46, 53), 'No liquid above a solid'
        assert stage(obs[i], 'behavior') != 3, 'At most one movable layer'
        covered.add(i)
        top[i] = oid
        counts[oid] += 1
    for key in ('safes', 'ancientSeals', 'tarpStatues', 'paintCanBoxColors', 'tubes', 'magnets', 'tutorialSteps'):
        assert not parse_entries(text, key), (number, key)
    expected = {70: 1, 4: 12} if number == 161 else {70: 4, 25: 20, 5: 14}
    goals = parse_entries(text, 'goals')
    assert {g['obstacleId']: g['amount'] for g in goals} == expected
    assert all(g['targetType'] == 1 and g['amount'] == counts[g['obstacleId']] for g in goals)
    assert all(g['obstacleId'] not in (38, 43, 45) for g in goals), 'Helpers are not goals'
    if number == 161:
        assert counts == {70: 1, 4: 12}
        assert {i for i, o in enumerate(obs) if o == 70} == {58, 59, 67, 68}
        expected_specials = {22: 2, 55: 1}
        # The authored column/row specials each intersect the introductory statue.
        assert 22 % 9 in (58 % 9, 58 % 9 + 1) and 55 // 9 in (58 // 9, 58 // 9 + 1)
    else:
        assert counts == {70: 4, 25: 20, 8: 20, 5: 14, 43: 1, 45: 4}
        assert {origins[i] for i, o in enumerate(obs) if o == 70} == {10, 15, 64, 69}
        assert {i for i, o in enumerate(obs) if o == 45} == {86, 87, 95, 96}
        assert {i for i, o in enumerate(obs) if o == 43} == {84, 85, 93, 94}
        assert len(covered) == 20
        expected_specials = {40: 4, 41: 1}
        assert abs(40 % 9 - 41 % 9) == 1 and 40 // 9 == 41 // 9
        # Both possible swap centers keep the opening inside a five-wide work area.
        assert all(obs[y * 9 + x] == 0 for y in range(3, 7) for x in range(2, 7))
    specials = {i: value for i, value in enumerate(a['pinnedSpecialTypes']) if value}
    assert specials == expected_specials
    assert {i for i, value in enumerate(a['pinnedTileTypes']) if value} == set(specials)
    assert all(obs[i] == 0 and 1 <= a['pinnedTileTypes'][i] <= 4 for i in specials)

    # Geometric spawn routes with all authored blockers in place. This deliberately
    # does not predict cascades, exact opening swaps or win rate.
    blocked = {i for i, oid in enumerate(top) if oid and
               (stage(oid, 'blocksCells') or stage(oid, 'behavior') == 3)}
    playable = set(range(99)) - blocked
    reached = set()
    pending = deque(i for i in range(9) if i not in blocked)
    while pending:
        i = pending.popleft()
        if i in reached:
            continue
        reached.add(i)
        if i // 9 == 10:
            continue
        if i + 9 not in blocked:
            pending.append(i + 9)
        for dx in (-1, 1):
            j = i + 9 + dx
            if not 0 <= i % 9 + dx < 9 or j not in playable:
                continue
            if any(k not in blocked or stage(top[k], 'allowDiagonal') for k in (i + dx, i + 9)):
                pending.append(j)
    assert not (playable - reached), (number, 'Unreachable normal cells', sorted(playable - reached))
    assert len(playable) == (83 if number == 161 else 41)
    guid = re.search(r'^guid: (\w+)$', Path(str(path) + '.meta').read_text(), re.M)[1]
    assert re.search(r'    level: ' + str(number) + r'\n    levelKey: LevelCL_0' + str(number) +
                     r'\n    levelData: \{fileID: 11400000, guid: ' + guid, CATALOG)
    print(f'PASS {number}: 9x11 / 99 active; {counts[70]} statues; {len(playable)} spawn-accessible tile cells; '
          f'{len(goals)} consistent goals; pins, origins and layers valid.')


def check_intro():
    # First production use comes from the catalog, not the numerical asset suffix.
    by_guid = {}
    for meta in (SETTINGS / 'ProductionLevels').glob('*.asset.meta'):
        guid = re.search(r'^guid: (\w+)$', meta.read_text(), re.M)[1]
        by_guid[guid] = Path(str(meta)[:-5])
    first = {}
    for number, guid in re.findall(r'    level: (\d+)\n    levelKey: [^\n]+\n'
                                  r'    levelData: \{fileID: 11400000, guid: (\w+)', CATALOG):
        if guid not in by_guid:
            continue
        text = by_guid[guid].read_text()
        blob = bytes.fromhex(re.search(r'^  obstacles: ([0-9a-f]+)$', text, re.M)[1])
        ids = set(struct.unpack(f'<{len(blob)//4}i', blob))
        ids.update(s['obstacleId'] for s in parse_entries(text, 'stackedObstacles'))
        for oid in ids:
            first[oid] = min(first.get(oid, int(number)), int(number))
    assert first[70] == 161
    assert all(first[o] < 161 for o in (4, 5, 8, 25, 43, 45))
    assert footprint(70) == (2, 2) and len(re.findall(r'^      damageRule: 1$', DEFS[70], re.M)) == 4
    assert int_field(DEFS[70].replace('    hits:', '  hits:'), 'hits') == 4
    hint = (ROOT / 'Assets/_Project/Data/Tutorial/ObstacleHintLibrary.asset').read_text()
    assert re.search(r'  - id: 70\n.*?titleLocKey: hint_obstacle_rhodes_statue_name\n.*?locKey: hint_obstacle_rhodes_statue\n', hint, re.S)
    translations = json.loads((ROOT / 'Assets/_Project/Resources/Localization/tinyfixers_localization.json').read_text())
    for language in translations['languages']:
        if language['code'] not in ('tr', 'en'):
            continue
        for key in ('hint_obstacle_rhodes_statue', 'hint_obstacle_rhodes_statue_name'):
            assert sum(e['key'] == key and bool(e['value']) for e in language['entries']) == 1
    manager = (ROOT / 'Assets/_Project/Scripts/Tutorial/ObstacleHintManager.cs').read_text()
    assert 'foreach (int raw in levelData.obstacles)' in manager and 'AddId((ObstacleId)raw)' in manager
    assert 'MarkHintSeen(id)' in manager and 'ReleasePause();' in manager
    print('PASS: first catalog appearance is 161; four special-only stages and existing TR/EN first-use introduction are wired.')


if __name__ == '__main__':
    check(161, '001650')
    check(162, '001660')
    check_intro()
    print('Static checks only; move counts/difficulty are initial settings. No Unity, bot or win-rate simulation.')
