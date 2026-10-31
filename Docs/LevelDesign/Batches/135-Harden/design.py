"""135 Kristal Kalp — Hard'a çekme (2026-11-01). Usta bot simde %73 (Hard için kolay; kayıpta jel 1.8/68 kalıyordu).
Kalp ve yerleşim aynı. Kolaylığın kaynağı: kalbin alt ucundaki 4 jel AÇIK başlıyordu → jel ilk hamleden yayılıyordu.
Değişiklik: uçtaki 4 jel tohumu da loblardaki gibi kum taşı (28, 3 vuruş) altına alınır → jel ancak bir taş
kırılınca başlar. Taş hedefi 14 → 18. Hamle 27 aynı (önce tasarım; gerekirse en son).
python3 design.py write   (kaynak: before_LevelV1_00001350.asset → asset)
"""
import re, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
ASSET = HERE.parents[3]/'Assets/_Project/Settings/ProLevels/LevelV1_00001350.asset'
W = 9
TIP = [(3, 9), (4, 9), (5, 9), (4, 10)]
MOVES = 22

def write():
    s = (HERE/'before_LevelV1_00001350.asset').read_text()
    m = re.search(r'^  stackedObstacles:\n((?:  - .*\n(?:    .*\n)*)*)', s, re.M)
    orders = [int(x) for x in re.findall(r'stackOrder: (\d+)', m[1])]
    add = ''.join(f"  - originCellIndex: {y*W+x}\n    obstacleId: 28\n    stackOrder: {max(orders)+1+k}\n"
                  for k, (x, y) in enumerate(TIP))
    s = s[:m.end()] + add + s[m.end():]
    s, k = re.subn(r'(obstacleId: 28\n    collectibleId: 1\n    amount: )14', r'\g<1>18', s); assert k == 1
    # Kullanıcı 3 oyun (2026-11-01): 1 fail, 2 galibiyette ≥12 hamle arttı → hamle bütçesi gevşek: 27 → 22.
    s, k = re.subn(r'^  moves: 27$', f'  moves: {MOVES}', s, flags=re.M); assert k == 1
    (ASSET).write_text(s); print('yazıldı: uç taşları', len(TIP), 'taş hedefi 18', 'hamle', MOVES)

if __name__ == '__main__' and sys.argv[1:] == ['write']: write()
