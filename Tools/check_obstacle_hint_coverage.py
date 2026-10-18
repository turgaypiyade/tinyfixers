#!/usr/bin/env python3
"""Asset/localization coverage. No Unity or project build."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
library = (root / 'Assets/_Project/Settings/ObstacleLibrary.asset').read_text()
hints = (root / 'Assets/_Project/Data/Tutorial/ObstacleHintLibrary.asset').read_text()
definitions = set(map(int, re.findall(r'^  - id: (\d+)$', library, re.M))) - {0}
entries = {}
for block in re.split(r'^  - id: ', hints, flags=re.M)[1:]:
    oid = int(block.splitlines()[0])
    assert oid not in entries, f'Duplicate hint: {oid}'
    entries[oid] = dict(re.findall(r'^    (\w+): (.*)$', block, re.M))
assert definitions <= entries.keys(), f'Missing obstacle hints: {definitions - entries.keys()}'
localization = json.loads((root / 'Assets/_Project/Resources/Localization/tinyfixers_localization.json').read_text())
languages = {lang['code']: {e['key']: e['value'] for e in lang['entries']} for lang in localization['languages']}
for oid in definitions:
    for key in ('titleLocKey', 'locKey'):
        loc_key = entries[oid].get(key)
        assert loc_key, (oid, key)
        for code in ('tr', 'en'):
            assert languages[code].get(loc_key, '').strip(), (oid, code, loc_key)
scene = (root / 'Assets/_Project/Scenes/01_Game.unity').read_text()
hint_guid = re.search(r'^guid: (\w+)$', (root / 'Assets/_Project/Data/Tutorial/ObstacleHintLibrary.asset.meta').read_text(), re.M)[1]
assert re.search(r'enableHints: 1\n  hintLibrary: \{fileID: 11400000, guid: ' + hint_guid, scene)
print(f'PASS: all {len(definitions)} obstacle definitions have unique hints, TR/EN titles/descriptions, and the game scene enables the correct library.')
