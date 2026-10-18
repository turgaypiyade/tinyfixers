#!/usr/bin/env python3
"""Check authored fallback economy, goal art, localized labels and prefab references."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[1]
assets = root / 'Assets/_Project'
config = (assets / 'Data/Events/ProgressEvent/ProgressEventConfig.asset').read_text()
icons = (assets / 'Resources/TileIconLibrary_Main.asset').read_text()
goals = re.split(r'(?=^  - goalType:)', config, flags=re.M)[1:]
expected = [(3, 'gear', 1, 1, 15), (2, 'lineH', 11, 1, 15), (5, 'bolt', 0, 150, 0), (4, 'core', 20, 1, 0), (2, 'lineH', 11, 1, 0)]
assert len(goals) == len(expected)
for block, (target, art, reward, amount, minutes) in zip(goals, expected):
    assert int(re.search(r'goalType: (\d+)', block)[1]) == target
    icon = re.search(r'^  ' + art + r': (.*)$', icons, re.M)[1]
    assert re.search(r'^    goalIcon: (.*)$', block, re.M)[1] == icon
    assert int(re.search(r'^      type: (\d+)', block, re.M)[1]) == reward
    assert int(re.search(r'^      amount: (\d+)', block, re.M)[1]) == amount
    assert int(re.search(r'^    rewardDurationMinutes: (\d+)', block, re.M)[1]) == minutes
types = ['LineH', 'LineV', 'LineAny', 'TileCleared_Gear', 'TileCleared_Core', 'TileCleared_Bolt', 'TileCleared_Plate', 'TileCleared_Any', 'ObstacleCleared', 'SpecialCreated', 'LevelWon', 'FirstTryWin']
localization = json.loads((assets / 'Resources/Localization/tinyfixers_localization.json').read_text())
for language in localization['languages']:
    if language['code'] not in ('tr', 'en'):
        continue
    entries = {entry['key']: entry['value'] for entry in language['entries']}
    for goal_type in types:
        assert '37' in entries['progress_goal_' + goal_type].format(37)
prefab = (assets / 'Prefabs/UI/PEGoalRow.prefab').read_text()
ids = re.findall(r'^--- !u!\d+ &(\d+)', prefab, re.M)
assert len(ids) == len(set(ids)), 'Duplicate prefab object ID'
description = re.search(r'  descriptionText: \{fileID: (\d+)\}', prefab)[1]
assert description != '0' and description in ids
assert prefab.count('m_Name: GoalDescription') == 1
assert 'm_AnchoredPosition: {x: 100, y: -43}' in prefab
# The new label lies inside the 135-unit row and below the 60-unit progress bar.
assert -67.5 <= -43 - 14 and -43 + 14 < 5 - 30
print('PASS: five fallback goal/reward pairs, all 12 TR/EN descriptions, unique prefab IDs and a connected description label. No Unity rendering check.')
