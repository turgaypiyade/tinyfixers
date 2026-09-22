"""Validate authored boss progression; print a damage-budget sensitivity table.

No Unity build, board simulation, or claimed win rates. The 20/30/40 clear scenarios
omit shields, thrown obstacles, cascades and player skill; they expose HP/move outliers.
"""
from pathlib import Path
import math
import re
import struct

ROOT = Path(__file__).resolve().parents[1]
SETTINGS = ROOT / "Assets/_Project/Settings"


def field(text, key, default=None):
    match = re.search(r"^  " + re.escape(key) + r": (.*)$", text, re.M)
    return match[1] if match else default


def array(text, key):
    raw = bytes.fromhex(field(text, key, ""))
    return list(struct.unpack("<" + "i" * (len(raw) // 4), raw))


def audit_first_boss():
    text = (SETTINGS / "ProductionLevels/LevelP_00050.asset").read_text()
    total = int(re.search(r"    amount: (\d+)", text)[1])
    power = int(field(text, "damagePerClearedTile"))
    hp = int(field(text, "playerMaxHp"))
    counter = int(field(text, "enemyAttackBaseDamage"))
    # Illustrative move trace anchored to the user's ~5-move result at 200 HP / 5 power.
    # Actual board swaps/cascades are not simulated here.
    trace = [6, 9, 3, 12, 10, 6, 9, 3, 12, 10, 6, 9, 6]
    def turns(enemy_hp, per_tile):
        for i, clears in enumerate(trace, 1):
            enemy_hp -= clears * per_tile
            if enemy_hp <= 0:
                return i
        return None
    old_turns, new_turns = turns(200, 5), turns(total, power)
    assert old_turns == 5 and new_turns is not None and 10 <= new_turns <= 14
    assert total > 200 and power == 5
    assert new_turns > turns(80, 1), "Revised boss must also outlast the previous 80 HP / 1 power setting"
    remaining_hp = hp - (new_turns - 1) * counter
    assert remaining_hp >= 40 and new_turns <= int(field(text, "moves"))
    assert set(array(text, "obstacles")) == {0} and int(field(text, "bossAttackOilCount")) == 0
    print(f"First-boss illustrative trace {trace}: before {old_turns} moves; now {new_turns}, player HP {remaining_hp}.")
    for clears in (6, 8, 10, 12, 20):
        moves = math.ceil(total / (power * clears))
        print(f"  {clears} clears/move => {moves} moves, {hp - (moves - 1) * counter} HP remaining")


def audit():
    paths = {}
    for meta in SETTINGS.rglob("*.asset.meta"):
        guid = re.search(r"^guid: (\w+)$", meta.read_text(), re.M)[1]
        paths[guid] = Path(str(meta)[:-5])
    catalog = (SETTINGS / "LevelCatalogPro.asset").read_text()
    entries = re.findall(r"    level: (\d+)\n.*?    levelData: \{fileID: \d+, guid: (\w+)", catalog, re.S)
    enum = (ROOT / "Assets/_Project/Scripts/Core/LevelData.cs").read_text().split("public enum ObstacleId")[1].split("\n}")[0]
    names = {int(v): n for n, v in re.findall(r"^    (\w+) = (\d+),?$", enum, re.M)}
    lib = (SETTINGS / "ObstacleLibrary.asset").read_text()
    definitions = {int(b.splitlines()[0]): b for b in re.split(r"(?m)^  - id: ", lib)[1:]}
    allowed = {7, 8, 9, 11, 12, 13, 14, 25, 30, 40, 46}
    seen = {}
    bosses = []
    print("Budget scenarios: constant clears per move; no shields or obstacle-pressure cost.")
    print("Level | Enemy HP | Player HP | Waves | Power/tile | 20 clears: moves / HP | 30 clears: moves / HP | 40 clears: moves / HP | Throws")
    for number, guid in entries:
        number = int(number)
        if number > 50:
            continue
        path = paths[guid]
        text = path.read_text()
        obstacles = array(text, "obstacles")
        if field(text, "levelKind") == "1":
            bosses.append(number)
            width, height = int(field(text, "width")), int(field(text, "height"))
            cells, origins = array(text, "cells"), array(text, "obstacleOrigins")
            assert len(cells) == len(obstacles) == len(origins) == width * height, path
            for i, obstacle in enumerate(obstacles):
                assert not obstacle or cells[i] != 0, (path, "obstacle in hole", i)
                assert obstacle or origins[i] == -1, (path, "stale origin", i)
            pool = array(text, "bossThrownObstacles")
            count = int(field(text, "bossAttackOilCount"))
            if number <= 10:
                assert count == 0 and not pool, "First two bosses must not throw obstacles"
            else:
                assert count > 0 and pool, path
                for obstacle in pool:
                    assert obstacle in allowed, (path, "unsafe projectile", obstacle)
                    assert obstacle in seen and seen[obstacle] < number, (path, "not introduced", obstacle)
                    assert "size: {x: 1, y: 1}" in definitions[obstacle], (path, obstacle)
                    assert re.search(r"sprite: \{fileID: -?[1-9]\d*, guid:", definitions[obstacle]), (path, obstacle)
            assert "bd8a2a2aabdf46fcabea5eb683bc76d9" in text, "Badger goal icon"
            total = int(re.search(r"    amount: (\d+)", text)[1])
            hp, power = int(field(text, "playerMaxHp")), int(field(text, "damagePerClearedTile"))
            waves, damage = int(field(text, "bossWaveCount")), int(field(text, "enemyAttackBaseDamage"))
            assert 1 <= waves <= 3 and field(text, "bossWaves") == "[]", path
            weights = {1: [1], 2: [.45, .55], 3: [.30, .33, .37]}[waves]
            wave_hp = [round(total * weight) for weight in weights[:-1]]
            wave_hp.append(total - sum(wave_hp))
            assert sum(wave_hp) == total and min(wave_hp) > 0
            scenarios = []
            for clears in (20, 30, 40):
                attacks = [math.ceil(value / (power * clears)) for value in wave_hp]
                loss = sum((turns - 1) * round(damage * (1 + .25 * i)) for i, turns in enumerate(attacks))
                scenarios.append(f"{sum(attacks)} / {hp - loss}")
                if clears == 30:
                    assert sum(attacks) <= int(field(text, "moves")) and loss < hp, (path, "30-clear budget")
            throws = "off" if not count else f"{count} every {field(text, 'bossAttackEveryMoves')}; cap {field(text, 'bossMaxPressureObstacles')}; " + ", ".join(names[o] for o in pool)
            print(f"{number} | {total} | {hp} | {waves} | {power} | " + " | ".join(scenarios) + " | " + throws)
        for obstacle in obstacles:
            if obstacle:
                seen.setdefault(obstacle, number)
    assert bosses == [5, 10, 15, 20, 25, 30, 35, 40, 45], bosses
    print("PASS: 50 catalog entries audited; 9 bosses; all thrown obstacles introduced earlier.")
    print("Level 50 is LevelP_00510 (normal); no new boss has been inserted.")
    audit_first_boss()


if __name__ == "__main__":
    audit()
