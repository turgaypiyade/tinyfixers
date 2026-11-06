"""Import approved PNG layers without changing pixels; append new obstacle definitions only."""
from pathlib import Path
from PIL import Image, ImageFilter
import argparse
import hashlib
import json
import re
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--source', type=Path, required=True)
args = parser.parse_args()
dest = ROOT / 'Assets/_Project/Art/UI/Obstacles/Dimerdal'
dest.mkdir(exist_ok=True)

def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'tinyfixers/dimerdal/' + name).hex

def folder_meta(path):
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid(str(path.relative_to(ROOT)))}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

folder_meta(dest)
template = (ROOT / 'Assets/_Project/Art/UI/Obstacles/RobotStyle/PaintCanBox/PaintCanBox_9.png.meta').read_text()
refs = {}
records = []
for tr, en in [('Mavi', 'Blue'), ('Yesil', 'Green'), ('Sari', 'Yellow'), ('Kirmizi', 'Red')]:
    for filename, kind in [('Cerceve-ve-IcZemin.png', 'Frame'), ('Elmas.png', 'Diamond'), ('Kepenk_01_Ust.png', 'Slat')]:
        src = args.source / tr / filename
        out = dest / f'Dimerdal{kind}{en}.png'
        if out.exists():
            assert out.read_bytes() == src.read_bytes(), f'Refusing to overwrite changed asset: {out}'
        else:
            shutil.copy2(src, out)
        with Image.open(src) as im:
            assert im.mode == 'RGBA'
            alpha = im.getchannel('A')
            assert alpha.getextrema() == (0, 255)
            # Analysis only: strip transparent padding via importer rect, preserve PNG bytes.
            core = alpha.point(lambda a: 255 if a >= 200 else 0).filter(ImageFilter.MinFilter(9))
            l, t, r, b = core.getbbox()
            l, t, r, b = max(0, l - 5), max(0, t - 5), min(im.width, r + 5), min(im.height, b + 5)
            width, height = r - l, b - t
            y = im.height - b
        name = out.stem
        asset_guid = guid(name)
        meta = re.sub(r'guid: [0-9a-f]+', 'guid: ' + asset_guid, template, count=1)
        meta = meta.replace('internalIDToNameTable: []', f'internalIDToNameTable:\n  - first:\n      213: 21300000\n    second: {name}')
        meta = meta.replace('spriteMode: 1', 'spriteMode: 2')
        meta = re.sub(r'maxTextureSize: \d+', 'maxTextureSize: 512', meta)
        sprite_sheet = f'''  spriteSheet:
    serializedVersion: 2
    sprites:
    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {l}
        y: {y}
        width: {width}
        height: {height}
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData: 
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {guid(name + '/sprite')}
      internalID: 21300000
      vertices: []
      indices: 
      edges: []
      weights: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: {guid(name + '/sheet')}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable:
      {name}: 21300000
'''
        meta = re.sub(r'  spriteSheet:.*?(?=  mipmapLimitGroupName:)', sprite_sheet, meta, flags=re.S)
        Path(str(out) + '.meta').write_text(meta)
        refs[(en, kind)] = f'{{fileID: 21300000, guid: {asset_guid}, type: 3}}'
        records.append({'asset': str(out.relative_to(ROOT)), 'sha256': hashlib.sha256(out.read_bytes()).hexdigest(), 'spriteRect': [l, y, width, height]})

library_path = ROOT / 'Assets/_Project/Settings/ObstacleLibrary.asset'
library = library_path.read_text()
for index, (en, tile) in enumerate([('Blue', 2), ('Green', 3), ('Yellow', 0), ('Red', 1)]):
    for size in [1, 2, 3, 0]:
        diamond = size == 0
        obstacle_id = 96 + index if diamond else 84 + index * 3 + size - 1
        if re.search(rf'^  - id: {obstacle_id}$', library, re.M):
            continue
        hits = 1 if diamond else 4
        sprite = refs[(en, 'Diamond' if diamond else 'Frame')]
        stages = ''.join(f'''    - sprite: {sprite}
      damageRule: 0
      blocksCells: {0 if diamond else 1}
      behavior: {3 if diamond else 1}
      allowDiagonal: {1 if diamond else 0}
      locksInteraction: 0
      holdsTile: 0
      suppressHitParticles: 1
      hitParticleSprites: []
      hitSound: {{fileID: 0}}
      hitSoundVolume: 1
''' for _ in range(hits))
        aux = ' []\n' if diamond else '\n' + ''.join('    - ' + refs[(en, kind)] + '\n' for kind in ['Frame', 'Diamond', 'Slat'])
        library += f'''  - id: {obstacle_id}
    stages:
{stages}    auxiliarySprites:{aux}    allowCrossContextFallback: 0
    restrictNormalMatchTileType: {0 if diamond else 1}
    requiredNormalMatchTileType: {tile}
    size: {{x: {size or 1}, y: {size or 1}}}
    hits: {hits}
    fullCellSprite: 0
    exitAtBottom: 0
    spawnFillsColumn: 0
    fillsShadowBeneath: 1
    goalIconSprite: {refs[(en, 'Diamond')]}
    coversCellFully: {0 if diamond else 1}
    suppressHitParticles: 1
    hitParticleSprites: []
    breakParticleSprites: []
    particleScale: 1
    particleCount: 0
    wardrobeItemSprites: []
    hamsterSatiety: 7
'''
library_path.write_text(library)
for name in ['DimerdalRules', 'DimerdalService', 'DimerdalView']:
    path = ROOT / f'Assets/_Project/Scripts/Grid/Board/Obstacles/{name}.cs.meta'
    if not path.exists():
        path.write_text(f'fileFormatVersion: 2\nguid: {guid(name + "/script")}\n')
checks = ROOT / 'Docs/Checks/Dimerdal'
checks.mkdir(exist_ok=True)
(checks / 'assets.json').write_text(json.dumps(records, indent=2) + '\n')
print('12 shared texture assets and 16 Dimerdal definitions prepared; source PNG pixels preserved.')
