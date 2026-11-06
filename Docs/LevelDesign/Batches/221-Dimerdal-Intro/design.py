"""221 only: single-color Dimerdal introduction. No gameplay simulation or publication."""
from pathlib import Path
import hashlib
import json
import re
import struct
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
PROJECT = ROOT / 'Assets/_Project'
PRO = PROJECT / 'Settings/ProLevels'
CATALOG = PRO / 'LevelCatalogProV1.asset'
NAMES = PROJECT / 'Resources/LevelNames.json'
ASSET = PRO / 'LevelV1_00002210.asset'
NAME, NAME_EN = 'Mavi Vitrin', 'Blue Showcase'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def encode(values):
    return struct.pack('<' + 'i' * len(values), *values).hex()

def scalar(text, key, value):
    text, count = re.subn(r'^  ' + key + r':[^\n]*$', lambda _: f'  {key}: {value}', text, flags=re.M)
    assert count == 1, key
    return text

def entries(text, key, rows):
    value = f'  {key}: []\n' if not rows else f'  {key}:\n' + ''.join(''.join(('  - ' if i == 0 else '    ') + k + ': ' + str(v) + '\n' for i, (k, v) in enumerate(row.items())) for row in rows)
    text, count = re.subn(r'^  ' + key + r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*', lambda _: value, text, flags=re.M)
    assert count == 1, key
    return text

def write():
    assert not ASSET.exists(), '221 already exists; preserve it and make an explicit revision instead.'
    assert [int(v) for v in re.findall(r'^    level: (\d+)$', CATALOG.read_text(), re.M)] == list(range(1, 221))
    before = HERE / 'Before'
    before.mkdir(parents=True, exist_ok=False)
    editable = [CATALOG, NAMES, ROOT / 'Docs/LevelDesign/Level-Names.md', ROOT / 'Docs/LevelDesign/Design-State.md', ROOT / 'Docs/LevelDesign/Memory.md', ROOT / 'Docs/LevelDesign/Design-Feedback.md', ROOT / 'Docs/LevelDesign/Obstacle-Design-Guide.md']
    for path in editable:
        shutil_path = before / path.name
        shutil_path.write_bytes(path.read_bytes())
    protected = {}
    for folder in [PROJECT / 'Settings', PROJECT / 'Scripts', PROJECT / 'Resources', PROJECT / 'Art/UI/Obstacles/Dimerdal', PROJECT / 'Data/Tutorial']:
        for path in folder.rglob('*'):
            if path.is_file() and path not in (CATALOG, NAMES):
                protected[str(path.relative_to(ROOT))] = sha(path)
    (HERE / 'protected-before.json').write_text(json.dumps(protected, indent=2) + '\n')

    rows = ['..oooo..', '.oooooo.'] + ['oooooooo'] * 6 + ['.oooooo.', '..oooo..']
    w, h = 8, 10
    arrays = {key: [0] * (w * h) for key in ['cells', 'obstacles', 'wallPieceIds', 'lampColorByCell', 'hamsterSatietyByCell', 'pinnedTileTypes', 'pinnedSpecialTypes']}
    arrays['cells'] = [int(c == 'o') for row in rows for c in row]
    arrays['obstacleOrigins'] = [-1] * (w * h)
    origins = [8 * w + 2, 8 * w + 4]
    for origin in origins:
        for dy in range(2):
            for dx in range(2):
                i = origin + dy * w + dx
                assert arrays['cells'][i]
                arrays['obstacles'][i] = 85  # DimerdalBlue2x2
                arrays['obstacleOrigins'][i] = origin
    # One exact horizontal three-match opportunity across both shutters' upper faces.
    pins = {(2, 7): 3, (3, 7): 1, (4, 7): 3, (3, 6): 3, (1, 7): 2, (5, 7): 4}
    for (x, y), value in pins.items():
        arrays['pinnedTileTypes'][y * w + x] = value
    diamond_meta = (PROJECT / 'Art/UI/Obstacles/Dimerdal/DimerdalDiamondBlue.png.meta').read_text()
    icon_guid = re.search(r'^guid: (\w+)$', diamond_meta, re.M)[1]
    icon = f'{{fileID: 21300000, guid: {icon_guid}, type: 3}}'
    goals = [dict(targetType=2, tileType=0, obstacleId=0, collectibleId=4, iconOverride=icon, amount=8)]
    text = (PRO / 'LevelV1_00002160.asset').read_text()
    for key, value in dict(m_Name=ASSET.stem, width=w, height=h, moves=24, levelKind=0, usesCustomIntro=0,
                           refillSpecialGuard=.75, oilSpreadMaxCells=0, movableSpawnFallbackMoves=0, movableSpawnFillColumns=1).items():
        text = scalar(text, key, value)
    for key, values in arrays.items():
        text = scalar(text, key, encode(values))
    for key in ['colorCrateInitialHits', 'tubes', 'magnets', 'safes', 'ancientSeals', 'lampStripSettings', 'paintCanBoxColors', 'tarpStatues', 'tutorialSteps', 'bossWaves', 'stackedObstacles', 'waterChannels']:
        text = entries(text, key, [])
    text = entries(text, 'goals', goals)
    text = scalar(text, 'movableSpawnColumns', '00' * w)
    assert re.search(r'^  randomPool: 00000000010000000200000003000000$', text, re.M)
    level_guid = uuid.uuid4().hex
    ASSET.write_text(text)
    Path(str(ASSET) + '.meta').write_text(f'fileFormatVersion: 2\nguid: {level_guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    key = 'LevelCL_0221'
    CATALOG.write_text(CATALOG.read_text() + f'  - chapter: 9\n    level: 221\n    levelKey: {key}\n    levelData: {{fileID: 11400000, guid: {level_guid}, type: 2}}\n')
    names = json.loads(NAMES.read_text())
    assert not any(e['level'] == 221 for e in names['entries'])
    names['entries'].append(dict(level=221, levelKey=key, name=NAME, nameEn=NAME_EN))
    NAMES.write_text(json.dumps(names, ensure_ascii=False, indent=2) + '\n')
    model = dict(level=221, levelKey=key, name=NAME, nameEn=NAME_EN, guid=level_guid,
                 asset=str(ASSET.relative_to(ROOT)), width=w, height=h, moves=24, rows=rows, data=arrays,
                 goals=goals, origins=origins, openingSwap=[[3, 6], [3, 7]], pinReason='Six authored normal-color pins make an exact blue three-match across the cabinet faces; free colors remain random.',
                 idea='Üstte eşleşme alanı, altta bitişik iki mavi Dimerdal ile tamamlanan vitrin kaidesi.',
                 firstDecision='Mavi üçlüyle iki kepengi ilerlet; sarı/kırmızı/yeşil normal eşleşmeler kepenkleri açmaz.',
                 after='Dört hitten sonra aynı renk komşu eşleşmesiyle bir elmas üret; elmasları ayrıca topla.')
    (HERE / 'design.json').write_text(json.dumps(model, ensure_ascii=False, indent=2) + '\n')
    print('221 Mavi Vitrin: 8x10 / 68 active, 2 adjacent blue 2x2 Dimerdals, 24 moves, Diamond8, 6 normal-color pins.')

if __name__ == '__main__':
    write()
