"""Gece incelemesi önerileri (2026-11-01). Varsayılan: yalnız önizleme üretir, asset'i geri yükler.
python3 propose.py preview        → after/L134.png, after/L143.png, after/L157.png (asset'ler değişmeden kalır)
python3 propose.py apply 143      → yalnız o öneriyi asset'e yazar (kaynak: before_*.asset)
"""
import re, struct, sys, shutil, subprocess
from pathlib import Path
HERE = Path(__file__).resolve().parent
P = HERE.parents[3]/'Assets/_Project/Settings/ProLevels'
def unhex(h): return [struct.unpack('<i', bytes.fromhex(h[i:i+8]))[0] for i in range(0, len(h), 8)]
def tohex(v): return ''.join(struct.pack('<i', x).hex() for x in v)

def clear_cells(s, cells, oid):
    W = int(re.search(r'^  width: (\d+)', s, re.M)[1])
    ob = unhex(re.search(r'^  obstacles: ([0-9a-f]*)$', s, re.M)[1])
    org = unhex(re.search(r'^  obstacleOrigins: ([0-9a-f]*)$', s, re.M)[1])
    for x, y in cells:
        i = y*W + x; assert ob[i] == oid, (x, y, ob[i]); ob[i] = 0; org[i] = -1
    s = re.sub(r'^  obstacles: [0-9a-f]*$', f'  obstacles: {tohex(ob)}', s, count=1, flags=re.M)
    s = re.sub(r'^  obstacleOrigins: [0-9a-f]*$', f'  obstacleOrigins: {tohex(org)}', s, count=1, flags=re.M)
    return s, ob.count(oid)

def set_goal(s, oid, amount):
    s, k = re.subn(rf'(obstacleId: {oid}\n    collectibleId: \d+\n(?:    iconOverride: .*\n)?    amount: )\d+', rf'\g<1>{amount}', s)
    if k == 0:
        s, k = re.subn(rf'(obstacleId: {oid}\n    collectibleId: \d+\n    amount: )\d+', rf'\g<1>{amount}', s)
    assert k == 1, oid
    return s

def p134(s):   # "4" Hard'dan (135) zor çıkıyordu: hamle 24 → 27, tahta aynı
    s, k = re.subn(r'^  moves: 24$', '  moves: 27', s, flags=re.M); assert k == 1; return s
def p143(s):   # "3": ilk hamlede yalnız 1 genişlikli koridor vardı → üst plastik sırası taş alanı olur
    s, n = clear_cells(s, [(x, 0) for x in range(9) if x != 4], 40); return set_goal(s, 40, n)
def p157(s):   # "7" (156 tanıtımından sonra): yalnız special ile kırılan pusula sütunu 8 → 6 sıra
    s, n = clear_cells(s, [(x, y) for x in (0, 1) for y in (0, 1)], 39); return set_goal(s, 39, n)
PROPOSALS = {134: ('LevelV1_00001340.asset', p134), 143: ('LevelV1_00001430.asset', p143), 157: ('LevelV1_00001570.asset', p157)}

def write(lv):
    name, fn = PROPOSALS[lv]
    (P/name).write_text(fn((HERE/f'before_{name}').read_text()))

if __name__ == '__main__':
    if sys.argv[1] == 'apply':
        write(int(sys.argv[2])); print('uygulandı', sys.argv[2])
    elif sys.argv[1] == 'preview':
        (HERE/'after').mkdir(exist_ok=True)
        for lv, (name, _) in PROPOSALS.items():
            try:
                write(lv)
                subprocess.run([sys.executable, str(HERE.parent/'197-Ease/render_one.py'), str(lv), str(HERE/f'after/L{lv}.png')], check=True, capture_output=True)
            finally:
                shutil.copyfile(HERE/f'before_{name}', P/name)
        print('önizlemeler hazır; asset\'ler geri yüklendi')
