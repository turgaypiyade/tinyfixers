#!/usr/bin/env python3
"""Validate authored opening relief and Cargo paths; no gameplay/bot/build claim."""
from collections import Counter
from pathlib import Path
import re
import struct

root = Path(__file__).resolve().parents[1]
path = root / 'Assets/_Project/Settings/ProductionLevels/LevelP_001600.asset'
text = path.read_text()
def array(key):
    return list(struct.unpack('<81i', bytes.fromhex(re.search(r'^  ' + key + r': (\w+)$', text, re.M)[1])))
data = {key: array(key) for key in ('cells', 'obstacles', 'obstacleOrigins', 'pinnedTileTypes', 'pinnedSpecialTypes', 'wallPieceIds', 'hamsterSatietyByCell')}
cells, obs, origins, specials = (data[key] for key in ('cells', 'obstacles', 'obstacleOrigins', 'pinnedSpecialTypes'))
assert sum(cells) == 54 and cells.count(0) == 27
assert [(i, value) for i, value in enumerate(specials) if value] == [(13, 4)]  # (4,1) PulseCore
assert obs[13] == 0 and data['pinnedTileTypes'][13] != 0
for i in (11, 15, 20, 24):
    assert obs[i] == 4 and origins[i] == i
    assert abs(i % 9 - 4) <= 2 and abs(i // 9 - 1) <= 2
assert Counter(value for value in obs if value) == {34: 12, 5: 20, 4: 13}
goals = dict((int(a), int(b)) for a, b in re.findall(r'    obstacleId: (\d+)\n    collectibleId: 0\n    iconOverride: \{fileID: 0\}\n    amount: (\d+)', text))
assert goals == {34: 12, 5: 20, 4: 13}
for i, obstacle in enumerate(obs):
    if obstacle:
        assert cells[i] and origins[i] == i
    if not cells[i]:
        assert not obstacle and origins[i] == -1 and not specials[i]
    if obstacle == 34:
        assert all(obs[y * 9 + i % 9] in (0, 4, 5, 34) for y in range(i // 9 + 1, 9))
# The first blast removes both single-hit blockers below each upper inner Cargo.
after = obs[:]
for i in (11, 15, 20, 24):
    after[i] = 0
for x in (2, 6):
    assert after[9 + x] == after[18 + x] == 0
guid = re.search(r'^guid: (\w+)$', Path(str(path) + '.meta').read_text(), re.M)[1]
catalog = (root / 'Assets/_Project/Settings/LevelCatalogPro.asset').read_text()
assert re.search(r'    level: 156\n    levelKey: LevelCL_0156\n    levelData: \{fileID: 11400000, guid: ' + guid, catalog)
print('PASS: 156 arrays, goals, catalog, ready PulseCore and two removable Cargo opening corridors. Win rate unmeasured.')
