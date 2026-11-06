"""Static authoring checks only: no moves, gravity, cascade, bot or Unity execution."""
from pathlib import Path
from collections import deque
import hashlib
import json
import re
import struct
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'Tools'))
from check_levels161_162 import array, footprint, stage, DEFS
from export_level_previews import int_field, parse_entries

model = json.loads((HERE / 'design.json').read_text())
asset = ROOT / model['asset']
text = asset.read_text()
w, h = int_field(text, 'width'), int_field(text, 'height')
assert (w, h, int_field(text, 'moves'), int_field(text, 'levelKind')) == (8, 10, 24, 0)
for key, values in model['data'].items():
    assert array(text, key, w * h) == values, key
a = model['data']
assert sum(a['cells']) == 68 and set(a['obstacles']) == {0, 85}
assert footprint(85) == (2, 2)
for origin in model['origins']:
    region = {origin + dy * w + dx for dy in range(2) for dx in range(2)}
    assert region == {i for i, value in enumerate(a['obstacleOrigins']) if value == origin}
    assert all(a['cells'][i] and a['obstacles'][i] == 85 for i in region)
    ox, oy = origin % w, origin // w
    assert oy == 8 and oy + 2 == h  # integrated bottom plinth, no normal cells trapped below
    faces = {i for i in range(w * h) if a['cells'][i] and not a['obstacles'][i] and any(abs(i % w - j % w) + abs(i // w - j // w) == 1 for j in region)}
    assert faces and any(i // w == 7 for i in faces)
assert model['origins'] == [66, 68] and a['obstacles'].count(85) == 8
goals = parse_entries(text, 'goals')
assert len(goals) == 1 and goals[0]['targetType'] == 2 and goals[0]['collectibleId'] == 4 and goals[0]['amount'] == 8
assert '    hits: 4' in DEFS[85] and 'requiredNormalMatchTileType: 2' in DEFS[85]
assert '    fillsShadowBeneath: 1' in DEFS[85]
assert array(text, 'randomPool', 4) == [0, 1, 2, 3]
assert not any(a['pinnedSpecialTypes']) and sum(bool(p) for p in a['pinnedTileTypes']) == 6
assert all(not a['obstacles'][i] and a['cells'][i] and value in range(1, 5) for i, value in enumerate(a['pinnedTileTypes']) if value)

# Inspect authored pin geometry, not a played/simulated swap. Three pinned blues
# can be aligned by one adjacent swap; both ends stay nonblue, so no forced four-match.
pins = a['pinnedTileTypes'][:]
assert (pins[6 * w + 3], pins[7 * w + 3]) == (3, 1)
pins[6 * w + 3], pins[7 * w + 3] = pins[7 * w + 3], pins[6 * w + 3]
assert [pins[7 * w + x] for x in (1, 2, 3, 4, 5)] == [2, 3, 3, 3, 4]
for origin in model['origins']:
    assert any(a['obstacleOrigins'][8 * w + x] == origin for x in (2, 3, 4))
for i, value in enumerate(a['pinnedTileTypes']):
    if not value:
        continue
    assert not (i % w >= 2 and a['pinnedTileTypes'][i - 1] == a['pinnedTileTypes'][i - 2] == value)
    assert not (i // w >= 2 and a['pinnedTileTypes'][i - w] == a['pinnedTileTypes'][i - 2 * w] == value)

# Geometric vertical source reachability. This board needs no shadow-fill assumption:
# permanent machines finish the bottom silhouette; all normal slots have clear feed.
slots = {i for i, active in enumerate(a['cells']) if active and not a['obstacles'][i]}
reached = set()
for x in range(w):
    for y in range(h):
        cell = y * w + x
        if a['obstacles'][cell]:
            break
        if cell in slots:
            reached.add(cell)
assert slots == reached and len(slots) == 60
for key in ['stackedObstacles', 'tubes', 'magnets', 'safes', 'ancientSeals', 'waterChannels', 'tarpStatues', 'lampStripSettings', 'paintCanBoxColors', 'colorCrateInitialHits', 'tutorialSteps', 'bossWaves']:
    assert not parse_entries(text, key), key
assert re.search(r'^  movableSpawnColumns: 0000000000000000$', text, re.M)

catalog_path = ROOT / 'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset'
catalog = catalog_path.read_text()
old_catalog = (HERE / 'Before' / catalog_path.name).read_text()
assert catalog.startswith(old_catalog)
assert list(map(int, re.findall(r'^    level: (\d+)$', catalog, re.M))) == list(range(1, 222))
assert catalog[len(old_catalog):] == f'  - chapter: 9\n    level: 221\n    levelKey: LevelCL_0221\n    levelData: {{fileID: 11400000, guid: {model["guid"]}, type: 2}}\n'
assert re.search(r'^guid: ' + model['guid'] + '$', Path(str(asset) + '.meta').read_text(), re.M)
names = json.loads((ROOT / 'Assets/_Project/Resources/LevelNames.json').read_text())
old_names = json.loads((HERE / 'Before/LevelNames.json').read_text())
assert names['entries'][:-1] == old_names['entries']
assert names['entries'][-1] == dict(level=221, levelKey='LevelCL_0221', name='Mavi Vitrin', nameEn='Blue Showcase')
icon = model['goals'][0]['iconOverride']
assert f'    iconOverride: {icon}\n' in text
sprite_guid = re.search(r'guid: (\w+)', icon)[1]
meta = ROOT / 'Assets/_Project/Art/UI/Obstacles/Dimerdal/DimerdalDiamondBlue.png.meta'
assert f'guid: {sprite_guid}\n' in meta.read_text() and 'internalID: 21300000' in meta.read_text()
fix_path = HERE / 'authorized-code-fixes.json'
fixes = json.loads(fix_path.read_text()) if fix_path.exists() else {}
concurrent_path = HERE / 'concurrent-changes.json'
concurrent = json.loads(concurrent_path.read_text()) if concurrent_path.exists() else {}
assert not (fixes.keys() & concurrent.keys())
assert set(concurrent) <= {'Assets/_Project/Scripts/Grid/Board/CascadeLogic.cs', 'Assets/_Project/Scripts/Grid/Board/TileFallMotionSystem.cs'}
for path, digest in json.loads((HERE / 'protected-before.json').read_text()).items():
    current = hashlib.sha256((ROOT / path).read_bytes()).hexdigest()
    if current != digest:
        fix = fixes.get(path, concurrent.get(path, {}))
        assert fix.get('beforeSha256') == digest and fix.get('afterSha256') == current, path

report = dict(status='PASS', level=221, activeCells=68, normalSpawnAccessibleCells=60, blueDimerdal2x2=2,
              moves=24, diamondGoal=8, fixedNormalPins=6, fixedSpecials=0,
              firstBlueThreeGeometry='pinned adjacent swap above both boxes', otherLevelsPreserved=True, recordedRuntimeFixes=list(fixes), observedConcurrentChanges=list(concurrent),
              limits='Static data, footprint, source-route and pin geometry checks; no gameplay, gravity, cascade, bot, PlayMode, build, balance measurement or publication.')
(HERE / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
print('PASS 221: blue-only Dimerdal intro; 68 active / 60 naturally fed normal cells; 8 diamonds / 24 moves; existing levels preserved; runtime matches exact recorded Dimerdal fixes plus separately observed concurrent edits.')
