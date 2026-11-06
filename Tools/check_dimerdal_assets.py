from pathlib import Path
from PIL import Image
import hashlib
import json
import re
import subprocess

root = Path(__file__).resolve().parents[1]
library_path = 'Assets/_Project/Settings/ObstacleLibrary.asset'
library = (root / library_path).read_text()
before = subprocess.check_output(['git', '-c', 'core.fsmonitor=false', 'show', 'HEAD:' + library_path], cwd=root).decode()
assert library.startswith(before), 'Pre-existing obstacle definitions changed.'
parts = re.split(r'^  - id: (\d+)\n', library, flags=re.M)
definitions = {}
for i in range(1, len(parts), 2):
    obstacle_id = int(parts[i])
    assert obstacle_id not in definitions, f'Duplicate obstacle id {obstacle_id}'
    definitions[obstacle_id] = parts[i + 1]

records = json.loads((root / 'Docs/Checks/Dimerdal/assets.json').read_text())
assert len(records) == 12
guids = set()
for record in records:
    path = root / record['asset']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == record['sha256']
    meta = Path(str(path) + '.meta').read_text()
    asset_guid = re.search(r'^guid: ([0-9a-f]+)$', meta, re.M).group(1)
    assert asset_guid not in guids
    guids.add(asset_guid)
    assert 'spriteMode: 2' in meta and 'internalID: 21300000' in meta
    assert 'maxTextureSize: 512' in meta and 'enableMipMap: 0' in meta and 'isReadable: 0' in meta
    with Image.open(path) as im:
        assert im.mode == 'RGBA' and im.getchannel('A').getextrema() == (0, 255)
        x, y, w, h = record['spriteRect']
        assert min(x, y) >= 0 and min(w, h) > 0 and x + w <= im.width and y + h <= im.height

for obstacle_id in range(84, 100):
    block = definitions[obstacle_id]
    diamond = obstacle_id >= 96
    if diamond:
        assert '      allowDiagonal: 1' in block, 'Diamonds must retain normal diagonal flow'
        assert '      blocksCells: 0' in block, 'Legacy authored diamonds must not block gravity before promotion'
        color = obstacle_id - 96
        shutter = definitions[84 + color * 3]
        auxiliary = shutter.split('    auxiliarySprites:', 1)[1].split('    allowCrossContextFallback:', 1)[0]
        box_diamond = re.findall(r'^    - (.*)', auxiliary, re.M)[1]
        board_diamond = re.search(r'^    - sprite: (.*)', block, re.M).group(1)
        assert board_diamond == box_diamond, 'Box, flight and board must use the same diamond artwork'
    expected_hits = 1 if diamond else 4
    assert len(re.findall(r'^    - sprite:', block, re.M)) == expected_hits
    assert f'    hits: {expected_hits}\n' in block
    expected_size = 1 if diamond else (obstacle_id - 84) % 3 + 1
    assert f'    size: {{x: {expected_size}, y: {expected_size}}}' in block
    assert f'      behavior: {3 if diamond else 1}' in block
    if not diamond:
        assert 'restrictNormalMatchTileType: 1' in block
        color = (obstacle_id - 84) // 3
        assert f'requiredNormalMatchTileType: {[2, 3, 0, 1][color]}' in block
        auxiliary = block.split('    auxiliarySprites:', 1)[1].split('    allowCrossContextFallback:', 1)[0]
        assert len(re.findall(r'^    - ', auxiliary, re.M)) == 3
    references = re.findall(r'\{fileID: (-?\d+), guid: ([0-9a-f]+), type: 3\}', block)
    assert references and all(file_id == '21300000' and asset_guid in guids for file_id, asset_guid in references)
    assert re.search(r'goalIconSprite: \{fileID: 21300000, guid:', block)

level = (root / 'Assets/_Project/Scripts/Core/LevelData.cs').read_text()
ids = dict(re.findall(r'\b(Dimerdal\w+)\s*=\s*(\d+)', level))
assert sorted(map(int, ids.values())) == list(range(84, 100))
assert 'Diamond = 4' in level and 'CollectibleId.Diamond => ObstacleId.DimerdalDiamondBlue' in level
hint_path = 'Assets/_Project/Data/Tutorial/ObstacleHintLibrary.asset'
hints = (root / hint_path).read_text()
old_hints = subprocess.check_output(['git', '-c', 'core.fsmonitor=false', 'show', 'HEAD:' + hint_path], cwd=root).decode()
assert hints.startswith(old_hints)
for obstacle_id in range(84, 96):
    assert len(re.findall(rf'^  - id: {obstacle_id}$', hints, re.M)) == 1
locale_path = 'Assets/_Project/Resources/Localization/tinyfixers_localization.json'
old_locale = json.loads(subprocess.check_output(['git', '-c', 'core.fsmonitor=false', 'show', 'HEAD:' + locale_path], cwd=root))
locale = json.loads((root / locale_path).read_text())
for language, old_language in zip(locale['languages'], old_locale['languages']):
    assert language['code'] == old_language['code']
    assert [entry for entry in language['entries'] if not entry['key'].startswith('hint_obstacle_dimerdal')] == old_language['entries']
    assert {entry['key']: entry['value'] for entry in language['entries']}['hint_obstacle_dimerdal_name'] == 'Dimerdal'
catalog_path = 'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset'
changed_levels = subprocess.check_output(['git', '-c', 'core.fsmonitor=false', 'diff', '--name-only', '--', 'Assets/_Project/Settings/ProLevels'], cwd=root).decode().splitlines()
assert set(changed_levels) <= {catalog_path}, 'Pre-existing production level assets changed.'
old_catalog = (root / 'Docs/LevelDesign/Batches/221-Dimerdal-Intro/Before/LevelCatalogProV1.asset').read_text()
catalog = (root / catalog_path).read_text()
assert catalog.startswith(old_catalog)
addition = catalog[len(old_catalog):]
assert re.fullmatch(r'  - chapter: 9\n    level: 221\n    levelKey: LevelCL_0221\n    levelData: \{fileID: 11400000, guid: [0-9a-f]+, type: 2\}\n', addition)
result = {'status': 'PASS', 'sharedTextures': 12, 'shutterDefinitions': 12, 'diamondDefinitions': 4, 'oldLibraryPreserved': True, 'preExistingLevelAssetsPreserved': True, 'separatelyAuthorizedLevelAddition': 221, 'diamondsAllowExistingDiagonalFlow': True}
(root / 'Docs/Checks/Dimerdal/asset-validation.json').write_text(json.dumps(result, indent=2) + '\n')
print('PASS: 12 transparent sprite imports, 12 shutters + 4 diamonds, goals/icons, original obstacle library and pre-existing levels preserved; authorized221 catalog addition retained.')
