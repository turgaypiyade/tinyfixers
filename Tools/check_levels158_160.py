#!/usr/bin/env python3
"""Static authored-data checks only; does not run Unity or simulate gameplay."""
from collections import Counter, deque
from pathlib import Path
import re
import struct

from export_level_previews import int_field, parse_entries

ROOT = Path(__file__).resolve().parents[1]
SETTINGS = ROOT / 'Assets/_Project/Settings'
CATALOG = (SETTINGS / 'LevelCatalogPro.asset').read_text()
LIBRARY = (SETTINGS / 'ObstacleLibrary.asset').read_text()
DEFS = {int(m[1]): m[2] for m in re.finditer(
    r'^  - id: (\d+)\n(.*?)(?=^  - id:|\Z)', LIBRARY, re.M | re.S)}
ARRAYS = ('cells', 'obstacles', 'obstacleOrigins', 'wallPieceIds',
          'hamsterSatietyByCell', 'pinnedTileTypes', 'pinnedSpecialTypes')


def array(text, key, count):
    match = re.search(r'^  ' + key + r': ([0-9a-f]+)$', text, re.M)
    assert match, key
    blob = bytes.fromhex(match[1])
    assert len(blob) == count * 4, (key, len(blob), count * 4)
    return list(struct.unpack(f'<{count}i', blob))


def first_stage(obstacle, key):
    return int(re.search(r'^      ' + key + r': (\d+)', DEFS[obstacle], re.M)[1])


def introduced_before_158():
    by_guid = {}
    for meta in (SETTINGS / 'ProductionLevels').glob('*.asset.meta'):
        guid = re.search(r'^guid: (\w+)$', meta.read_text(), re.M)[1]
        by_guid[guid] = Path(str(meta)[:-5])
    known = set()
    for number, guid in re.findall(
            r'    level: (\d+)\n    levelKey: [^\n]+\n'
            r'    levelData: \{fileID: 11400000, guid: (\w+)', CATALOG):
        if int(number) >= 158:
            continue
        # Earlier tutorial assets may live outside ProductionLevels. All families
        # used here also have authored production appearances before this batch.
        if guid not in by_guid:
            continue
        text = by_guid[guid].read_text()
        m = re.search(r'^  obstacles: ([0-9a-f]+)$', text, re.M)
        if m:
            blob = bytes.fromhex(m[1])
            known.update(struct.unpack(f'<{len(blob)//4}i', blob))
        known.update(s['obstacleId'] for s in parse_entries(text, 'stackedObstacles'))
        if parse_entries(text, 'safes'):
            known.add(33)
    return known


KNOWN = introduced_before_158()


def check(level, suffix):
    path = SETTINGS / 'ProductionLevels' / f'LevelP_{suffix}.asset'
    text = path.read_text()
    w, h = int_field(text, 'width'), int_field(text, 'height')
    n = w * h
    a = {key: array(text, key, n) for key in ARRAYS}
    cells, obs, origins = (a[key] for key in ARRAYS[:3])
    assert w == 9 and h == (10 if level == 160 else 11)
    assert all(c in (0, 1) for c in cells)
    assert not any(a['pinnedTileTypes']) and not any(a['pinnedSpecialTypes'])
    assert not any(a['wallPieceIds']) and not any(a['hamsterSatietyByCell'])
    assert array(text, 'randomPool', 4) == [0, 1, 2, 3]
    assert int_field(text, 'moves') == (30 if level == 160 else 32)
    for i, obstacle in enumerate(obs):
        assert origins[i] == (i if obstacle else -1), (level, i, 'origin')
        if obstacle:
            assert cells[i] and obstacle in DEFS
            assert re.search(r'^    size: \{x: 1, y: 1\}', DEFS[obstacle], re.M)
    layers = [[o] if o else [] for o in obs]
    for s in parse_entries(text, 'stackedObstacles'):
        i, obstacle = s['originCellIndex'], s['obstacleId']
        assert 0 <= i < n and cells[i] and obstacle in DEFS
        assert s['stackOrder'] == 1 and len(layers[i]) == 1
        assert obs[i] in (25, 46) and obstacle not in (14, 25, 46, 53)
        assert re.search(r'^    size: \{x: 1, y: 1\}', DEFS[obstacle], re.M)
        layers[i].append(obstacle)
    safes = parse_entries(text, 'safes')
    safe_cells = set()
    for s in safes:
        origin = s['originCellIndex']
        assert s['width'] == s['height'] == 2 and s['stackOrder'] == 1
        assert s['redHits'] == s['yellowHits'] == s['greenHits'] == 2
        assert s['lockHitMode'] == 0
        assert sorted(s[k] for k in ('firstLock', 'secondLock', 'thirdLock')) == [0, 1, 2]
        x, y = origin % w, origin // w
        assert x + 2 <= w and y + 2 <= h
        for dy in range(2):
            for dx in range(2):
                i = (y + dy) * w + x + dx
                assert cells[i] and obs[i] == 25 and i not in safe_cells
                assert len(layers[i]) == 1
                safe_cells.add(i)
                layers[i].append(33)
    for stack in layers:
        assert sum(first_stage(o, 'behavior') == 3 for o in stack) <= 1
    top = [stack[-1] if stack else 0 for stack in layers]
    assert {o for stack in layers for o in stack} <= KNOWN, (level, 'unintroduced obstacle')
    # Count orthogonally connected mask components. Isolated obstacle rooms are
    # intentional in 159; an accidental tile bridge would remove that puzzle.
    remaining = {i for i, c in enumerate(cells) if c}
    components = 0
    while remaining:
        components += 1
        queue = deque([remaining.pop()])
        while queue:
            i = queue.popleft()
            x, y = i % w, i // w
            for nx, ny in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
                j = ny*w+nx
                if 0 <= nx < w and 0 <= ny < h and j in remaining:
                    remaining.remove(j)
                    queue.append(j)
    assert components == (5 if level == 159 else 1), (level, components)
    blocked = {i for i, o in enumerate(top) if o and
               (o == 33 or first_stage(o, 'blocksCells') or first_stage(o, 'behavior') == 3)}
    playable = {i for i, c in enumerate(cells) if c and i not in blocked}

    # Possible spawn paths, with all authored blockers still in place. Voids pass
    # vertically; diagonal edges require two active endpoints and a valid corner.
    # This is geometric reachability, not a temporal gravity or win simulation.
    reached = set()
    pending = deque(i for i in range(w) if i not in blocked)
    while pending:
        i = pending.popleft()
        if i in reached:
            continue
        reached.add(i)
        x, y = i % w, i // w
        if y + 1 == h:
            continue
        j = i + w
        if j not in blocked:
            pending.append(j)
        if i not in playable:
            continue
        for dx in (-1, 1):
            if not 0 <= x + dx < w:
                continue
            target = j + dx
            corners = (i + dx, j)
            corner_ok = any(cells[k] and (k not in blocked or
                            (top[k] and first_stage(top[k], 'allowDiagonal')))
                            for k in corners)
            if target in playable and corner_ok:
                pending.append(target)
    assert not (playable - reached), (level, 'unreachable normal cells', sorted(playable - reached))

    counts = Counter(o for o in obs if o)
    counts.update(s['obstacleId'] for s in parse_entries(text, 'stackedObstacles'))
    counts[33] += len(safes)
    goals = parse_entries(text, 'goals')
    if level == 160:
        assert len(goals) == 1 and goals[0]['targetType'] == 2
        assert goals[0]['collectibleId'] == 2 and goals[0]['amount'] == 540
        assert int_field(text, 'levelKind') == 1 and h <= 10
        assert int_field(text, 'playerMaxHp') == 250
        assert int_field(text, 'bossWaveCount') == 3
        assert int_field(text, 'damagePerClearedTile') == 1
        assert int_field(text, 'bossMaxPressureObstacles') == int_field(text, 'oilSpreadMaxCells') == 8
        thrown = array(text, 'bossThrownObstacles', 4)
        assert thrown == [14, 11, 30, 40] and all(counts[o] == 0 for o in thrown)
        assert counts[35] == 6 and counts[45] == 2 and counts[12] == 12
    else:
        assert int_field(text, 'levelKind') == 0
        expected = {33: 2, 5: 22, 25: 24} if level == 158 else {46: 69, 39: 24, 7: 12}
        assert {g['obstacleId']: g['amount'] for g in goals} == expected
        for g in goals:
            assert g['targetType'] == 1
            assert g['amount'] == (sum(cells) if g['obstacleId'] == 46 else counts[g['obstacleId']])
        if level == 159:
            assert counts[45] == 2 and counts[46] == 14
            # Every isolated corner has vertical special access from the middle band.
            assert all(cells[5*w + i % w] for i, o in enumerate(obs) if o == 39)
    assert all(g['obstacleId'] not in (38, 43, 45) for g in goals)
    # Masks are mirror-symmetric. Safe lock order and 159's small bird pair are
    # intentional variations within those symmetric silhouettes.
    for y in range(h):
        assert cells[y*w:(y+1)*w] == list(reversed(cells[y*w:(y+1)*w]))
    guid = re.search(r'^guid: (\w+)$', Path(str(path) + '.meta').read_text(), re.M)[1]
    assert re.search(r'    level: ' + str(level) + r'\n    levelKey: LevelCL_0' + str(level) +
                     r'\n    levelData: \{fileID: 11400000, guid: ' + guid, CATALOG)
    print(f'PASS {level}: {sum(cells)} active / {n-sum(cells)} holes; '
          f'{len(playable)} normal cells have geometric spawn access; '
          f'{len(goals)} consistent goals; layers, origins, catalog valid.')


if __name__ == '__main__':
    for number, suffix in ((158, '001620'), (159, '001630'), (160, '001640')):
        check(number, suffix)
    print('Static checks only. Random opening moves, win rate and device performance are unmeasured.')
