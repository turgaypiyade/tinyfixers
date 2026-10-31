"""197 Işık Merdiveni — rahatlatma (2026-11-01). 196 tanıtımından sonra gelen '7' fazla sertti (Usta bot %0):
başlangıçta ~9 serbest hücre + 30 anahtar. Sağ üst fener/plastik bloğu (satır 2–4, sütun 5–9; 1 tabanlı) taş alanına
döner → merdiven silüeti netleşir, ilk hamlelerde nefes alanı açılır. Anahtar kotası 30 → 16. Diğer her şey aynı.
python3 design.py write   (kaynak: before_LevelV1_00001970.asset → asset)
"""
import re, struct, sys
from pathlib import Path
from collections import Counter
HERE = Path(__file__).resolve().parent
ASSET = HERE.parents[3]/'Assets/_Project/Settings/ProLevels/LevelV1_00001970.asset'
KEYS = 16
OPEN = {(x, y) for x in range(4, 9) for y in range(1, 4)}

def unhex(h): return [struct.unpack('<i', bytes.fromhex(h[i:i+8]))[0] for i in range(0, len(h), 8)]
def tohex(v): return ''.join(struct.pack('<i', x).hex() for x in v)

def write():
    s = (HERE/'before_LevelV1_00001970.asset').read_text()
    W = int(re.search(r'^  width: (\d+)', s, re.M)[1])
    get = lambda k: unhex(re.search(rf'^  {k}: ([0-9a-f]*)$', s, re.M)[1])
    ob, org = get('obstacles'), get('obstacleOrigins')
    for x, y in OPEN:
        i = y*W + x; assert ob[i] in (40, 75) and org[i] == i; ob[i] = 0; org[i] = -1
    for k, v in (('obstacles', ob), ('obstacleOrigins', org)):
        s = re.sub(rf'^  {k}: [0-9a-f]*$', f'  {k}: {tohex(v)}', s, count=1, flags=re.M)
    cnt = Counter(ob)
    amounts = {75: cnt[75], 40: cnt[40], 28: cnt[28], 44: KEYS}
    def fix(m):
        oid = int(m[1]); return m[0].replace(f'amount: {m[2]}', f'amount: {amounts[oid]}')
    s = re.sub(r'obstacleId: (\d+)\n    collectibleId: 0\n    amount: (\d+)', fix, s)
    ASSET.write_text(s); print('yazıldı', amounts)

if __name__ == '__main__' and sys.argv[1:] == ['write']: write()
