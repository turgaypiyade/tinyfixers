"""189 Dolaplı Geçit — Super Hard'a çekme (2026-11-01). Usta bot %40, galibiyette ort. 8 hamle artıyor:
ortadaki 5 sütunluk engelsiz alan bedava special üretiyordu. Yan plastik şeritler (PlasticTwoStage 40) içe doğru
bir sütun kalınlaşır (sütun 3 ve 7, satır 5–9; 1 tabanlı) → orta alan 3 sütun; köşe dolaplarına ulaşmak emek ister.
Plastik hedefi 10 → 20. Hamle 30 ve diğer her şey aynı; refillSpecialGuard dokunulmadı (otomatik).
python3 design.py write   (kaynak: before_LevelV1_00001890.asset → asset)
"""
import re, struct, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
ASSET = HERE.parents[3]/'Assets/_Project/Settings/ProLevels/LevelV1_00001890.asset'
W = 9
ADD = [(x, y) for x in (2, 6) for y in range(4, 9)]

def unhex(h): return [struct.unpack('<i', bytes.fromhex(h[i:i+8]))[0] for i in range(0, len(h), 8)]
def tohex(v): return ''.join(struct.pack('<i', x).hex() for x in v)

def write():
    s = (HERE/'before_LevelV1_00001890.asset').read_text()
    get = lambda k: unhex(re.search(rf'^  {k}: ([0-9a-f]*)$', s, re.M)[1])
    ob, org = get('obstacles'), get('obstacleOrigins')
    for x, y in ADD:
        i = y*W + x; assert ob[i] == 0; ob[i] = 40; org[i] = i
    for k, v in (('obstacles', ob), ('obstacleOrigins', org)):
        s = re.sub(rf'^  {k}: [0-9a-f]*$', f'  {k}: {tohex(v)}', s, count=1, flags=re.M)
    n = ob.count(40)
    s, k = re.subn(r'(obstacleId: 40\n    collectibleId: 0\n    iconOverride: \{fileID: 0\}\n    amount: )10', rf'\g<1>{n}', s); assert k == 1
    ASSET.write_text(s); print('yazıldı plastik', n)

if __name__ == '__main__' and sys.argv[1:] == ['write']: write()
