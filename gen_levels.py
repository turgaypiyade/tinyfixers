#!/usr/bin/env python3
"""Generate 50 LevelCL_XXX.asset files for TinyFixers."""

import os

SCRIPT_GUID = "d85e85dc9bc3a4bd2a8772f074d86c37"
OBSTACLE_LIB_GUID = "542e279c729681244b475a353cdd0e8f"
MUSIC_GUID = "a75116037af37490bb9f00591892d8d6"
OUTPUT_DIR = "/Users/turgayp/Development Projects/Unity/tinyfixers/Assets/_Project/Settings/ClaudeTestLevels"

# ObstacleId enum values
STONE = 1
SHIELD1 = 2
SHIELD2 = 3
CHEST1 = 4
CHEST2 = 5
CHEST3 = 6
PLASTIC = 7
OIL = 14
COLOR_CHEST = 21   # 2×2
ENERGY_CONTAINER = 22
BATTERY_BOX = 23   # 2×2
OMEGA_MODUL = 24

# TileType enum values
GEAR = 0
CORE = 1
BOLT = 2
PLATE = 3

# LevelGoalTargetType
TARGET_TILE = 0
TARGET_OBSTACLE = 1
TARGET_COLLECTIBLE = 2

# 2×2 multi-cell obstacles
TWO_BY_TWO = {COLOR_CHEST, BATTERY_BOX}


def encode_int(v):
    v = v & 0xFFFFFFFF
    return f"{v&0xFF:02x}{(v>>8)&0xFF:02x}{(v>>16)&0xFF:02x}{(v>>24)&0xFF:02x}"


def encode_array(arr):
    return ''.join(encode_int(v) for v in arr)


def goal_tile(tile_type, amount):
    return {'targetType': TARGET_TILE, 'tileType': tile_type, 'obstacleId': 1,
            'collectibleId': 0, 'amount': amount}


def goal_obs(obs_id, amount):
    return {'targetType': TARGET_OBSTACLE, 'tileType': 0, 'obstacleId': obs_id,
            'collectibleId': 0, 'amount': amount}


def goal_collect(amount):
    return {'targetType': TARGET_COLLECTIBLE, 'tileType': 0, 'obstacleId': 1,
            'collectibleId': 1, 'amount': amount}


def build_asset(name, width, height, moves, goals, cell_grid, obs_map,
                energy_per_container=5):
    """
    cell_grid: list[height][width] of 0 (empty) or 1 (normal)
    obs_map: dict {(col, row): obstacle_id}
             For 2×2 obstacles supply top-left corner only.
    """
    size = width * height
    cells = [0] * size
    obstacles = [0] * size
    origins = [-1] * size

    for r in range(height):
        for c in range(width):
            cells[r * width + c] = cell_grid[r][c]

    for (col, row), obs_id in obs_map.items():
        if obs_id in TWO_BY_TWO:
            origin_idx = row * width + col
            for dr in range(2):
                for dc in range(2):
                    r2, c2 = row + dr, col + dc
                    if 0 <= r2 < height and 0 <= c2 < width:
                        idx = r2 * width + c2
                        obstacles[idx] = obs_id
                        origins[idx] = origin_idx
        else:
            idx = row * width + col
            if 0 <= row < height and 0 <= col < width:
                obstacles[idx] = obs_id
                origins[idx] = idx

    goals_yaml = ""
    for g in goals:
        goals_yaml += (
            f"  - targetType: {g['targetType']}\n"
            f"    tileType: {g['tileType']}\n"
            f"    obstacleId: {g['obstacleId']}\n"
            f"    collectibleId: {g['collectibleId']}\n"
            f"    iconOverride: {{fileID: 0}}\n"
            f"    amount: {g['amount']}\n"
        )

    return (
        f"%YAML 1.1\n"
        f"%TAG !u! tag:unity3d.com,2011:\n"
        f"--- !u!114 &11400000\n"
        f"MonoBehaviour:\n"
        f"  m_ObjectHideFlags: 0\n"
        f"  m_CorrespondingSourceObject: {{fileID: 0}}\n"
        f"  m_PrefabInstance: {{fileID: 0}}\n"
        f"  m_PrefabAsset: {{fileID: 0}}\n"
        f"  m_GameObject: {{fileID: 0}}\n"
        f"  m_Enabled: 1\n"
        f"  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}\n"
        f"  m_Name: {name}\n"
        f"  m_EditorClassIdentifier: Assembly-CSharp::LevelData\n"
        f"  width: {width}\n"
        f"  height: {height}\n"
        f"  moves: {moves}\n"
        f"  baseCoinReward: 100\n"
        f"  goals:\n"
        f"{goals_yaml}"
        f"  energyPerContainer: {energy_per_container}\n"
        f"  musicClip: {{fileID: 8300000, guid: {MUSIC_GUID}, type: 3}}\n"
        f"  musicVolume: 0.8\n"
        f"  obstacleLibrary: {{fileID: 11400000, guid: {OBSTACLE_LIB_GUID}, type: 2}}\n"
        f"  cells: {encode_array(cells)}\n"
        f"  obstacles: {encode_array(obstacles)}\n"
        f"  obstacleOrigins: {encode_array(origins)}\n"
    )


# ── Grid helpers ──────────────────────────────

def full(w, h):
    return [[1] * w for _ in range(h)]


def diamond(w, h):
    g = full(w, h)
    for r in range(h):
        for c in range(w):
            nd = abs(r - (h-1)/2) / ((h-1)/2) + abs(c - (w-1)/2) / ((w-1)/2)
            if nd > 1.25:
                g[r][c] = 0
    return g


def plus_shape(w, h):
    g = [[0]*w for _ in range(h)]
    cs, ce = w//2-1, w//2+2
    rs, re = h//2-1, h//2+2
    for r in range(h):
        for c in range(w):
            if cs <= c < ce or rs <= r < re:
                g[r][c] = 1
    return g


def h_shape(w, h):
    g = full(w, h)
    gap_s, gap_e = w//2-1, w//2+1
    mid_s, mid_e = h//3, h - h//3
    for r in range(h):
        if r < mid_s or r >= mid_e:
            for c in range(gap_s, gap_e):
                g[r][c] = 0
    return g


def frame(w, h, bord=2):
    g = [[0]*w for _ in range(h)]
    for r in range(h):
        for c in range(w):
            if r < bord or r >= h-bord or c < bord or c >= w-bord:
                g[r][c] = 1
    return g


def hourglass(w, h):
    g = [[0]*w for _ in range(h)]
    for r in range(h):
        d = min(r, h-1-r)
        half = (w-1) / 2
        shrink = int(half * (1 - d / (h/2)))
        for c in range(shrink, w-shrink):
            g[r][c] = 1
    return g


def star(w, h):
    d = diamond(w, h)
    p = plus_shape(w, h)
    return [[1 if d[r][c] or p[r][c] else 0 for c in range(w)] for r in range(h)]


def wavy(w, h):
    g = full(w, h)
    cut = min(2, w//4)
    for i in range(cut):
        g[0][i] = g[0][w-1-i] = 0
        g[h-1][i] = g[h-1][w-1-i] = 0
    return g


# ── Level definitions ─────────────────────────

levels = []

# ═══════════════════════════════════════════════
# PHASE 1  L01–L10  Pure match, no obstacles
# ═══════════════════════════════════════════════

# L01 – 5×5 full – tutorial
levels.append(build_asset("LevelCL_001", 5, 5, 20,
    [goal_tile(GEAR, 15), goal_tile(CORE, 10)],
    full(5, 5), {}))

# L02 – 6×5 corner cuts
g = full(6, 5)
for r, c in [(0,0),(0,5),(4,0),(4,5)]:
    if 0<=c<6: g[r][c] = 0
levels.append(build_asset("LevelCL_002", 6, 5, 18,
    [goal_tile(GEAR, 15), goal_tile(CORE, 12)],
    g, {}))

# L03 – 7×7 diamond
levels.append(build_asset("LevelCL_003", 7, 7, 22,
    [goal_tile(GEAR, 20), goal_tile(CORE, 18)],
    diamond(7, 7), {}))

# L04 – 6×6 centre hole
g = full(6, 6)
for r in range(2, 4):
    for c in range(2, 4):
        g[r][c] = 0
levels.append(build_asset("LevelCL_004", 6, 6, 24,
    [goal_tile(GEAR, 14), goal_tile(CORE, 12), goal_tile(BOLT, 8)],
    g, {}))

# L05 – 8×6 H-shape  (LineH naturally forms)
levels.append(build_asset("LevelCL_005", 8, 6, 26,
    [goal_tile(GEAR, 18), goal_tile(CORE, 14), goal_tile(BOLT, 10)],
    h_shape(8, 6), {}))

# L06 – 7×7 frame
levels.append(build_asset("LevelCL_006", 7, 7, 28,
    [goal_tile(GEAR, 12), goal_tile(CORE, 12), goal_tile(BOLT, 10), goal_tile(PLATE, 10)],
    frame(7, 7), {}))

# L07 – 9×9 plus  (Bomb naturally forms in 3-wide arm)
levels.append(build_asset("LevelCL_007", 9, 9, 30,
    [goal_tile(GEAR, 20), goal_tile(CORE, 18), goal_tile(BOLT, 16), goal_tile(PLATE, 14)],
    plus_shape(9, 9), {}))

# L08 – 9×7 wavy
levels.append(build_asset("LevelCL_008", 9, 7, 32,
    [goal_tile(GEAR, 22), goal_tile(CORE, 20), goal_tile(BOLT, 16), goal_tile(PLATE, 14)],
    wavy(9, 7), {}))

# L09 – 9×9 hourglass
levels.append(build_asset("LevelCL_009", 9, 9, 35,
    [goal_tile(GEAR, 24), goal_tile(CORE, 20), goal_tile(BOLT, 18), goal_tile(PLATE, 16)],
    hourglass(9, 9), {}))

# L10 – 9×9 star  (phase boss)
levels.append(build_asset("LevelCL_010", 9, 9, 38,
    [goal_tile(GEAR, 24), goal_tile(CORE, 22), goal_tile(BOLT, 20), goal_tile(PLATE, 18)],
    star(9, 9), {}))

# ═══════════════════════════════════════════════
# PHASE 2  L11–L20  Stone + chest1
# ═══════════════════════════════════════════════

# L11 – 6×6 full, Stone×4 centre
levels.append(build_asset("LevelCL_011", 6, 6, 22,
    [goal_tile(GEAR, 18), goal_obs(STONE, 4)],
    full(6, 6),
    {(2,2):STONE,(3,2):STONE,(2,3):STONE,(3,3):STONE}))

# L12 – 7×6 diamond, Stone×6
levels.append(build_asset("LevelCL_012", 7, 6, 24,
    [goal_tile(GEAR, 16), goal_tile(CORE, 12), goal_obs(STONE, 6)],
    diamond(7, 6),
    {(3,1):STONE,(2,2):STONE,(4,2):STONE,(2,3):STONE,(4,3):STONE,(3,4):STONE}))

# L13 – 8×7 H-shape, Stone×4 + chest1×4
levels.append(build_asset("LevelCL_013", 8, 7, 26,
    [goal_tile(GEAR, 18), goal_obs(STONE, 4), goal_obs(CHEST1, 4)],
    h_shape(8, 7),
    {(1,1):STONE,(6,1):STONE,(1,5):STONE,(6,5):STONE,
     (3,3):CHEST1,(4,3):CHEST1,(3,4):CHEST1,(4,4):CHEST1}))

# L14 – 7×7 full, chest1×8 ring
levels.append(build_asset("LevelCL_014", 7, 7, 26,
    [goal_tile(GEAR, 16), goal_tile(CORE, 12), goal_obs(CHEST1, 8)],
    full(7, 7),
    {(2,1):CHEST1,(3,1):CHEST1,(4,1):CHEST1,
     (1,3):CHEST1,(5,3):CHEST1,
     (2,5):CHEST1,(3,5):CHEST1,(4,5):CHEST1}))

# L15 – 7×7 frame, Stone×4 corners + chest1×6
levels.append(build_asset("LevelCL_015", 7, 7, 28,
    [goal_tile(GEAR, 14), goal_tile(CORE, 12), goal_obs(STONE, 4), goal_obs(CHEST1, 6)],
    frame(7, 7),
    {(0,0):STONE,(6,0):STONE,(0,6):STONE,(6,6):STONE,
     (2,2):CHEST1,(4,2):CHEST1,(3,3):CHEST1,(2,4):CHEST1,(4,4):CHEST1,(3,1):CHEST1}))

# L16 – 8×8 full, Stone×8 + chest1×8
levels.append(build_asset("LevelCL_016", 8, 8, 30,
    [goal_tile(GEAR, 18), goal_obs(STONE, 8), goal_obs(CHEST1, 8)],
    full(8, 8),
    {(1,2):STONE,(3,2):STONE,(5,2):STONE,(7,2):STONE,
     (1,5):STONE,(3,5):STONE,(5,5):STONE,(7,5):STONE,
     (0,3):CHEST1,(2,3):CHEST1,(4,3):CHEST1,(6,3):CHEST1,
     (0,4):CHEST1,(2,4):CHEST1,(4,4):CHEST1,(6,4):CHEST1}))

# L17 – 9×8 diamond, Stone fence + chest1 inside
levels.append(build_asset("LevelCL_017", 9, 8, 30,
    [goal_tile(GEAR, 16), goal_tile(CORE, 12), goal_obs(STONE, 8), goal_obs(CHEST1, 6)],
    diamond(9, 8),
    {(2,1):STONE,(6,1):STONE,(1,2):STONE,(7,2):STONE,
     (1,5):STONE,(7,5):STONE,(2,6):STONE,(6,6):STONE,
     (4,2):CHEST1,(3,3):CHEST1,(5,3):CHEST1,
     (4,4):CHEST1,(3,4):CHEST1,(5,4):CHEST1}))

# L18 – 9×9 wavy, Stone+chest1 dense rows
levels.append(build_asset("LevelCL_018", 9, 9, 33,
    [goal_tile(GEAR, 18), goal_tile(CORE, 16), goal_obs(STONE, 5), goal_obs(CHEST1, 7)],
    wavy(9, 9),
    {(1,1):STONE,(4,1):STONE,(7,1):STONE,(2,3):STONE,(6,3):STONE,
     (0,5):CHEST1,(2,5):CHEST1,(4,5):CHEST1,(6,5):CHEST1,(8,5):CHEST1,
     (1,7):CHEST1,(4,7):CHEST1,(7,7):CHEST1}))

# L19 – 9×9 plus, chest1 3×3 centre block
levels.append(build_asset("LevelCL_019", 9, 9, 34,
    [goal_tile(GEAR, 20), goal_tile(CORE, 16), goal_tile(BOLT, 14), goal_obs(CHEST1, 9)],
    plus_shape(9, 9),
    {(3,3):CHEST1,(4,3):CHEST1,(5,3):CHEST1,
     (3,4):CHEST1,(4,4):CHEST1,(5,4):CHEST1,
     (3,5):CHEST1,(4,5):CHEST1,(5,5):CHEST1}))

# L20 – 9×9 star, Stone+chest1 boss
levels.append(build_asset("LevelCL_020", 9, 9, 38,
    [goal_tile(GEAR, 18), goal_tile(CORE, 16), goal_obs(STONE, 8), goal_obs(CHEST1, 8)],
    star(9, 9),
    {(2,2):STONE,(6,2):STONE,(1,4):STONE,(7,4):STONE,
     (2,6):STONE,(6,6):STONE,(4,1):STONE,(4,7):STONE,
     (3,3):CHEST1,(5,3):CHEST1,(4,4):CHEST1,
     (3,5):CHEST1,(5,5):CHEST1,(4,3):CHEST1,
     (4,5):CHEST1,(3,4):CHEST1,(5,4):CHEST1}))

# ═══════════════════════════════════════════════
# PHASE 3  L21–L30  chest2 + chest3 + Plastic + Shield
# ═══════════════════════════════════════════════

# L21 – 6×7 full, chest2×4 (first chest2!)
levels.append(build_asset("LevelCL_021", 6, 7, 24,
    [goal_tile(GEAR, 18), goal_tile(CORE, 14), goal_obs(CHEST2, 8)],
    full(6, 7),
    {(2,2):CHEST2,(3,2):CHEST2,(2,4):CHEST2,(3,4):CHEST2}))

# L22 – 7×7 diamond, chest2×6
levels.append(build_asset("LevelCL_022", 7, 7, 26,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(CHEST2, 12)],
    diamond(7, 7),
    {(3,1):CHEST2,(2,3):CHEST2,(4,3):CHEST2,
     (1,4):CHEST2,(5,4):CHEST2,(3,5):CHEST2}))

# L23 – 8×7 H-shape, chest2×4 + Stone×4
levels.append(build_asset("LevelCL_023", 8, 7, 28,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(STONE, 4), goal_obs(CHEST2, 8)],
    h_shape(8, 7),
    {(0,0):STONE,(7,0):STONE,(0,6):STONE,(7,6):STONE,
     (3,2):CHEST2,(4,2):CHEST2,(3,4):CHEST2,(4,4):CHEST2}))

# L24 – 9×9 plus, chest3×4 (FIRST chest3!)
levels.append(build_asset("LevelCL_024", 9, 9, 30,
    [goal_tile(GEAR, 20), goal_tile(CORE, 16), goal_obs(CHEST3, 12)],
    plus_shape(9, 9),
    {(3,3):CHEST3,(5,3):CHEST3,(3,5):CHEST3,(5,5):CHEST3}))

# L25 – 9×8 frame, chest3×4 + chest2×4
levels.append(build_asset("LevelCL_025", 9, 8, 32,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_tile(BOLT, 12), goal_obs(CHEST3, 12), goal_obs(CHEST2, 8)],
    frame(9, 8),
    {(2,2):CHEST3,(6,2):CHEST3,(2,5):CHEST3,(6,5):CHEST3,
     (4,2):CHEST2,(4,5):CHEST2,(0,3):CHEST2,(8,3):CHEST2}))

# L26 – 7×7 diamond, Plastic×8 (FIRST plastic!)
levels.append(build_asset("LevelCL_026", 7, 7, 28,
    [goal_tile(GEAR, 14), goal_tile(CORE, 12), goal_tile(BOLT, 10), goal_obs(PLASTIC, 8)],
    diamond(7, 7),
    {(0,3):PLASTIC,(6,3):PLASTIC,(3,0):PLASTIC,(3,6):PLASTIC,
     (1,1):PLASTIC,(5,1):PLASTIC,(1,5):PLASTIC,(5,5):PLASTIC}))

# L27 – 9×8 wavy, Plastic×5 + chest2×4
levels.append(build_asset("LevelCL_027", 9, 8, 32,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(CHEST2, 8), goal_obs(PLASTIC, 5)],
    wavy(9, 8),
    {(1,1):PLASTIC,(3,1):PLASTIC,(5,1):PLASTIC,(7,1):PLASTIC,
     (4,4):PLASTIC,
     (0,3):CHEST2,(2,3):CHEST2,(6,3):CHEST2,(8,3):CHEST2}))

# L28 – 9×9 hourglass, chest3+Plastic+Stone
levels.append(build_asset("LevelCL_028", 9, 9, 34,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(STONE, 4), goal_obs(CHEST3, 9), goal_obs(PLASTIC, 3)],
    hourglass(9, 9),
    {(2,2):STONE,(6,2):STONE,(2,6):STONE,(6,6):STONE,
     (4,0):CHEST3,(4,4):CHEST3,(4,8):CHEST3,
     (1,4):PLASTIC,(7,4):PLASTIC,(4,2):PLASTIC}))

# L29 – 9×9 plus, Shield1×4 (FIRST Shield!)
levels.append(build_asset("LevelCL_029", 9, 9, 36,
    [goal_tile(GEAR, 20), goal_tile(CORE, 16), goal_obs(SHIELD1, 4), goal_obs(CHEST2, 8)],
    plus_shape(9, 9),
    {(4,1):SHIELD1,(1,4):SHIELD1,(7,4):SHIELD1,(4,7):SHIELD1,
     (3,4):CHEST2,(5,4):CHEST2,(4,3):CHEST2,(4,5):CHEST2}))

# L30 – 9×9 star, phase boss (chest3+Plastic+Shield)
levels.append(build_asset("LevelCL_030", 9, 9, 40,
    [goal_tile(GEAR, 18), goal_tile(CORE, 16), goal_obs(SHIELD1, 4), goal_obs(CHEST3, 12), goal_obs(PLASTIC, 4)],
    star(9, 9),
    {(2,2):SHIELD1,(6,2):SHIELD1,(2,6):SHIELD1,(6,6):SHIELD1,
     (4,1):CHEST3,(4,7):CHEST3,(1,4):CHEST3,(7,4):CHEST3,
     (0,0):PLASTIC,(8,0):PLASTIC,(0,8):PLASTIC,(8,8):PLASTIC}))

# ═══════════════════════════════════════════════
# PHASE 4  L31–L40  Oil + ColorChest + EnergyContainer
# ═══════════════════════════════════════════════

# L31 – 7×7 full, Oil×4 centre (FIRST Oil!)
levels.append(build_asset("LevelCL_031", 7, 7, 26,
    [goal_tile(GEAR, 18), goal_tile(CORE, 16), goal_tile(BOLT, 12), goal_obs(OIL, 4)],
    full(7, 7),
    {(2,3):OIL,(4,3):OIL,(2,4):OIL,(4,4):OIL}))

# L32 – 8×7 H-shape, Oil×6 + Stone×2
levels.append(build_asset("LevelCL_032", 8, 7, 28,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(STONE, 2), goal_obs(OIL, 6)],
    h_shape(8, 7),
    {(0,0):STONE,(7,0):STONE,
     (3,2):OIL,(4,2):OIL,(3,3):OIL,(4,3):OIL,(3,4):OIL,(4,4):OIL}))

# L33 – 9×8 diamond, Oil×8 + chest2×2
levels.append(build_asset("LevelCL_033", 9, 8, 30,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(CHEST2, 4), goal_obs(OIL, 8)],
    diamond(9, 8),
    {(2,1):OIL,(6,1):OIL,(1,3):OIL,(7,3):OIL,
     (1,4):OIL,(7,4):OIL,(2,6):OIL,(6,6):OIL,
     (3,3):CHEST2,(5,3):CHEST2}))

# L34 – 9×9 plus, ColorChest×1 (FIRST 2×2 CC!)
levels.append(build_asset("LevelCL_034", 9, 9, 30,
    [goal_tile(GEAR, 20), goal_tile(CORE, 18), goal_obs(COLOR_CHEST, 1)],
    plus_shape(9, 9),
    {(4,4):COLOR_CHEST}))   # covers (4,4)(5,4)(4,5)(5,5)

# L35 – 9×9 frame, ColorChest×2
levels.append(build_asset("LevelCL_035", 9, 9, 32,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_tile(BOLT, 12), goal_obs(COLOR_CHEST, 2)],
    frame(9, 9),
    {(2,2):COLOR_CHEST,(6,2):COLOR_CHEST}))

# L36 – 9×9 wavy, ColorChest×2 + Oil×4
levels.append(build_asset("LevelCL_036", 9, 9, 34,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(COLOR_CHEST, 2), goal_obs(OIL, 4)],
    wavy(9, 9),
    {(2,2):COLOR_CHEST,(5,5):COLOR_CHEST,
     (4,1):OIL,(4,2):OIL,(1,6):OIL,(7,6):OIL}))

# L37 – 9×9 hourglass, EnergyContainer×4 (FIRST EC!)
levels.append(build_asset("LevelCL_037", 9, 9, 34,
    [goal_tile(GEAR, 18), goal_tile(CORE, 14), goal_collect(20), goal_obs(ENERGY_CONTAINER, 4)],
    hourglass(9, 9),
    {(3,3):ENERGY_CONTAINER,(5,3):ENERGY_CONTAINER,
     (3,5):ENERGY_CONTAINER,(5,5):ENERGY_CONTAINER},
    energy_per_container=5))

# L38 – 9×9 plus, CC + EC + Oil
levels.append(build_asset("LevelCL_038", 9, 9, 36,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(COLOR_CHEST, 1), goal_obs(ENERGY_CONTAINER, 2), goal_obs(OIL, 3)],
    plus_shape(9, 9),
    {(4,4):COLOR_CHEST,
     (3,3):ENERGY_CONTAINER,(5,6):ENERGY_CONTAINER,
     (4,2):OIL,(3,7):OIL,(5,7):OIL},
    energy_per_container=8))

# L39 – 9×9 diamond, CC + EC + chest3
levels.append(build_asset("LevelCL_039", 9, 9, 38,
    [goal_tile(GEAR, 14), goal_tile(CORE, 14), goal_obs(CHEST3, 12), goal_obs(COLOR_CHEST, 1), goal_obs(ENERGY_CONTAINER, 2)],
    diamond(9, 9),
    {(4,1):CHEST3,(4,7):CHEST3,(1,4):CHEST3,(7,4):CHEST3,
     (3,3):COLOR_CHEST,
     (2,5):ENERGY_CONTAINER,(6,5):ENERGY_CONTAINER},
    energy_per_container=10))

# L40 – 9×9 star, phase boss
levels.append(build_asset("LevelCL_040", 9, 9, 42,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(COLOR_CHEST, 4), goal_obs(ENERGY_CONTAINER, 1), goal_obs(OIL, 4)],
    star(9, 9),
    {(2,2):COLOR_CHEST,(6,2):COLOR_CHEST,
     (2,6):COLOR_CHEST,(6,6):COLOR_CHEST,
     (4,4):ENERGY_CONTAINER,
     (1,4):OIL,(7,4):OIL,(4,1):OIL,(4,7):OIL},
    energy_per_container=12))

# ═══════════════════════════════════════════════
# PHASE 5  L41–L50  BatteryBox + OmegaModul
# ═══════════════════════════════════════════════

# L41 – 8×8 corner cuts, BatteryBox×1 (FIRST BB!)
g41 = full(8, 8)
for i in range(2):
    g41[0][i] = g41[0][7-i] = g41[7][i] = g41[7][7-i] = 0
levels.append(build_asset("LevelCL_041", 8, 8, 35,
    [goal_tile(GEAR, 20), goal_tile(CORE, 18), goal_tile(BOLT, 14), goal_obs(BATTERY_BOX, 12)],
    g41, {(3,3):BATTERY_BOX}))

# L42 – 9×8 diamond, BatteryBox×2
levels.append(build_asset("LevelCL_042", 9, 8, 40,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(BATTERY_BOX, 24)],
    diamond(9, 8),
    {(2,3):BATTERY_BOX,(6,3):BATTERY_BOX}))

# L43 – 9×9 H-shape, BB×1 + chest3×4 + Oil×2
levels.append(build_asset("LevelCL_043", 9, 9, 40,
    [goal_tile(GEAR, 16), goal_tile(CORE, 14), goal_obs(BATTERY_BOX, 12), goal_obs(CHEST3, 12), goal_obs(OIL, 2)],
    h_shape(9, 9),
    {(4,4):BATTERY_BOX,
     (1,2):CHEST3,(7,2):CHEST3,(1,6):CHEST3,(7,6):CHEST3,
     (4,2):OIL,(4,6):OIL}))

# L44 – 9×9 frame, OmegaModul×6 (FIRST OmegaModul!)
levels.append(build_asset("LevelCL_044", 9, 9, 38,
    [goal_tile(GEAR, 18), goal_tile(CORE, 16), goal_obs(OMEGA_MODUL, 6)],
    frame(9, 9),
    {(2,2):OMEGA_MODUL,(4,2):OMEGA_MODUL,(6,2):OMEGA_MODUL,
     (2,6):OMEGA_MODUL,(4,6):OMEGA_MODUL,(6,6):OMEGA_MODUL}))

# L45 – 9×9 diamond, BB×1 + OmegaModul×5
levels.append(build_asset("LevelCL_045", 9, 9, 42,
    [goal_tile(GEAR, 14), goal_tile(CORE, 14), goal_obs(BATTERY_BOX, 12), goal_obs(OMEGA_MODUL, 5)],
    diamond(9, 9),
    {(4,3):BATTERY_BOX,
     (2,2):OMEGA_MODUL,(6,2):OMEGA_MODUL,
     (1,5):OMEGA_MODUL,(7,5):OMEGA_MODUL,(4,7):OMEGA_MODUL}))

# L46 – 9×9 plus, BB×1 + OmegaModul×4 + ColorChest×1
levels.append(build_asset("LevelCL_046", 9, 9, 44,
    [goal_tile(GEAR, 14), goal_tile(CORE, 12), goal_obs(BATTERY_BOX, 12), goal_obs(COLOR_CHEST, 1), goal_obs(OMEGA_MODUL, 4)],
    plus_shape(9, 9),
    {(4,4):BATTERY_BOX,
     (2,2):COLOR_CHEST,
     (3,1):OMEGA_MODUL,(5,1):OMEGA_MODUL,
     (1,4):OMEGA_MODUL,(7,4):OMEGA_MODUL}))

# L47 – 9×9 hourglass, BB×1 + OmegaModul×2 + Shield1×4 + Oil×2
levels.append(build_asset("LevelCL_047", 9, 9, 45,
    [goal_tile(GEAR, 14), goal_tile(CORE, 12), goal_obs(BATTERY_BOX, 12), goal_obs(SHIELD1, 4), goal_obs(OMEGA_MODUL, 2), goal_obs(OIL, 2)],
    hourglass(9, 9),
    {(3,4):BATTERY_BOX,
     (2,2):SHIELD1,(6,2):SHIELD1,(2,6):SHIELD1,(6,6):SHIELD1,
     (4,1):OMEGA_MODUL,(4,7):OMEGA_MODUL,
     (1,4):OIL,(7,4):OIL}))

# L48 – 9×9 star, BB×2 + OmegaModul×4 + ColorChest×1
levels.append(build_asset("LevelCL_048", 9, 9, 46,
    [goal_tile(GEAR, 14), goal_tile(CORE, 12), goal_obs(BATTERY_BOX, 24), goal_obs(OMEGA_MODUL, 4), goal_obs(COLOR_CHEST, 1)],
    star(9, 9),
    {(1,1):BATTERY_BOX,(6,1):BATTERY_BOX,
     (3,4):COLOR_CHEST,
     (0,4):OMEGA_MODUL,(8,4):OMEGA_MODUL,
     (4,0):OMEGA_MODUL,(4,8):OMEGA_MODUL}))

# L49 – 9×9 wavy, everything dense
levels.append(build_asset("LevelCL_049", 9, 9, 48,
    [goal_tile(GEAR, 12), goal_tile(CORE, 12), goal_obs(BATTERY_BOX, 12), goal_obs(COLOR_CHEST, 1), goal_obs(SHIELD1, 2), goal_obs(CHEST3, 6)],
    wavy(9, 9),
    {(1,1):BATTERY_BOX,
     (4,3):COLOR_CHEST,
     (2,3):SHIELD1,(6,3):SHIELD1,
     (1,6):CHEST3,(7,6):CHEST3,
     (3,7):CHEST3,
     (6,1):OMEGA_MODUL,(3,5):OIL,(6,5):OIL}))

# L50 – 9×9 star, FINAL BOSS – all obstacles
levels.append(build_asset("LevelCL_050", 9, 9, 50,
    [goal_tile(GEAR, 14), goal_tile(CORE, 12), goal_obs(BATTERY_BOX, 24), goal_obs(COLOR_CHEST, 1), goal_obs(ENERGY_CONTAINER, 1)],
    star(9, 9),
    {(1,1):BATTERY_BOX,(6,1):BATTERY_BOX,
     (1,6):BATTERY_BOX,(6,6):BATTERY_BOX,
     (3,3):COLOR_CHEST,
     (2,4):SHIELD1,(6,4):SHIELD1,
     (4,2):CHEST3,(4,6):CHEST3,
     (1,4):OMEGA_MODUL,(7,4):OMEGA_MODUL,
     (4,4):ENERGY_CONTAINER,
     (3,1):OIL,(5,1):OIL,(3,7):OIL,(5,7):OIL},
    energy_per_container=15))

# ── Write files ──────────────────────────────

os.makedirs(OUTPUT_DIR, exist_ok=True)
for i, content in enumerate(levels, 1):
    fname = f"LevelCL_{i:03d}.asset"
    fpath = os.path.join(OUTPUT_DIR, fname)
    with open(fpath, 'w') as f:
        f.write(content)
    print(f"  {fname}")

print(f"\n✓ {len(levels)} levels written to {OUTPUT_DIR}")
