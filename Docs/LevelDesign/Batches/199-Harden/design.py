"""199 Deniz Kabuğu — Super Hard'a çekme (2026-11-01). Usta bot simde %67 → onluğun en kolay normal levelı idi.
Maske/yerleşim aynı. Jel kaynağı ortadaki mührün ALTINDA: mühür kırılmadan jel yayılmaz. Kaldıraç: mührün renk
sayaçları 8/8/8 → 11/11/11 (jel daha geç başlar, aynı 87 hücreyi daha az hamlede kaplamak gerekir) ve 26 → 24 hamle.
python3 design.py write   (kaynak: before_LevelV1_00001990.asset → asset)
"""
import re, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
ASSET = HERE.parents[3]/'Assets/_Project/Settings/ProLevels/LevelV1_00001990.asset'
SEAL, MOVES = 13, 24   # kullanıcı: 11 ile 2 fail + 3. denemede geçti, "biraz rahat" → 13

def write():
    s = (HERE/'before_LevelV1_00001990.asset').read_text()
    for c in ('red', 'yellow', 'green'):
        s, k = re.subn(rf'^(    {c}Count: )8$', rf'\g<1>{SEAL}', s, flags=re.M); assert k == 1, c
    s, k = re.subn(r'^  moves: 26$', f'  moves: {MOVES}', s, flags=re.M); assert k == 1
    ASSET.write_text(s); print('yazıldı seal', SEAL, 'moves', MOVES)

if __name__ == '__main__' and sys.argv[1:] == ['write']: write()
