"""201–205'e Lamba Şeridi (2026-11-01, kullanıcı: "lamba kullanımı mümkünse boardu tekrar tasarla; 201 kalabilir
ama onunla birlikte lambayı da ekleyelim yavaş yavaş"). Kaynak her zaman before_*.asset.
python3 variants.py preview [lv...]   → after/L<lv>.png (asset'ler geri yüklenir)
python3 variants.py apply <lv>        → asset'e yazar
"""
import re, struct, sys, shutil
from pathlib import Path
HERE = Path(__file__).resolve().parent
P = HERE.parents[3]/'Assets/_Project/Settings/ProLevels'
sys.path.insert(0, str(HERE)); import lampview
Y, R, B, G, PU = 1, 2, 3, 4, 5

def unhex(h): return [struct.unpack('<i', bytes.fromhex(h[i:i+8]))[0] for i in range(0, len(h), 8)]
def tohex(v): return ''.join(struct.pack('<i', x).hex() for x in v)

class Lv:
    def __init__(self, name):
        self.name = name; self.s = (HERE/f'before_{name}').read_text()
        self.W = int(re.search(r'^  width: (\d+)', self.s, re.M)[1]); self.H = int(re.search(r'^  height: (\d+)', self.s, re.M)[1])
        g = lambda k: unhex(re.search(rf'^  {k}: ([0-9a-f]*)$', self.s, re.M)[1])
        self.cells, self.ob, self.org = g('cells'), g('obstacles'), g('obstacleOrigins')
        self.piece = g('wallPieceIds'); self.col = [0]*(self.W*self.H); self.lamps = []
    def i(self, x, y): return y*self.W + x
    def clear(self, x, y, expect=None):
        i = self.i(x, y); assert expect is None or self.ob[i] == expect, (x, y, self.ob[i]); self.ob[i] = 0; self.org[i] = -1
    def lamp(self, cells, shift=0, reverse=0):
        pid = len(self.lamps) + 1 + max(self.piece)
        idx = [self.i(x, y) for x, y, _ in cells]; o = min(idx)
        for x, y, c in cells:
            i = self.i(x, y); assert self.cells[i] and self.ob[i] == 0, (x, y, self.ob[i])
            self.ob[i] = 79; self.org[i] = o; self.piece[i] = pid; self.col[i] = c
        self.lamps.append((pid, shift, reverse))
    def put(self, x, y, oid, origin=None):
        i = self.i(x, y); assert self.cells[i] and self.ob[i] == 0, (x, y, self.ob[i])
        self.ob[i] = oid; self.org[i] = i if origin is None else origin
    def block(self, x0, y0, w, h, oid):
        o = self.i(x0, y0)
        for y in range(y0, y0+h):
            for x in range(x0, x0+w): self.put(x, y, oid, o)
    def stack(self, cells, oid):
        n = len(re.findall(r'stackOrder', self.s))
        add = ''.join(f"  - originCellIndex: {self.i(x, y)}\n    obstacleId: {oid}\n    stackOrder: {n+k+1}\n    initialRemainingHits: 0\n"
                      for k, (x, y) in enumerate(cells))
        self.s, k = re.subn(r'^  stackedObstacles: \[\]\n', '  stackedObstacles:\n' + add, self.s, count=1, flags=re.M); assert k == 1
    def channel(self, path):
        h = tohex([self.i(x, y) for x, y in path])
        self.s, k = re.subn(r'^(  - pathCellIndices: )[0-9a-f]*$', rf'\g<1>{h}', self.s, count=1, flags=re.M); assert k == 1
    def count(self, oid): return len({self.org[i] for i in range(len(self.ob)) if self.ob[i] == oid})
    def text(self, goals, moves=None):
        s = self.s
        for k, v in (('obstacles', self.ob), ('obstacleOrigins', self.org), ('wallPieceIds', self.piece)):
            s = re.sub(rf'^  {k}: [0-9a-f]*$', f'  {k}: {tohex(v)}', s, count=1, flags=re.M)
        s = re.sub(r'^  lampColorByCell:.*\n', '', s, flags=re.M)
        s = re.sub(r'^  lampStripSettings:.*\n(?:  - .*\n(?:    .*\n)*)*', '', s, flags=re.M)
        st = "  lampStripSettings:\n" + ''.join(f"  - pieceId: {p}\n    shift: {sh}\n    reverse: {rv}\n" for p, sh, rv in self.lamps)
        s = re.sub(r'^(  wallPieceIds: .*\n)', lambda m: m[1] + f"  lampColorByCell: {tohex(self.col)}\n" + st, s, count=1, flags=re.M)
        gl = ''.join(f"  - targetType: {t}\n    tileType: 0\n    obstacleId: {o}\n    collectibleId: {c}\n"
                     f"    iconOverride: {{fileID: 0}}\n    amount: {a}\n" for t, o, c, a in goals)
        s, k = re.subn(r'^  goals:\n(?:  - .*\n(?:    .*\n)*)*', '  goals:\n' + gl, s, count=1, flags=re.M); assert k == 1
        if moves: s, k = re.subn(r'^  moves: \d+$', f'  moves: {moves}', s, count=1, flags=re.M); assert k == 1
        return s

BIRD = lambda n: (2, 0, 3, n)
OBS = lambda oid, n: (1, oid, 0, n)

def v201():   # Kuş evi tanıtımı aynen; sağ kenarda yavaş giriş: 6 lambalı KAYMAYAN dikey şerit (alt kenara iner)
    L = Lv('LevelV1_00002010.asset')
    L.lamp([(8, y, c) for y, c in zip(range(3, 9), [Y, Y, R, R, B, B])])
    return L.text([BIRD(50), OBS(79, 1)])

def v202():   # "2": duvar lamba direkleri + yanlarında 2×2 çim çalılıkları (çim akışı kesmez). (Plastik denendi: hareketli
              # engel, çamur üstünden avluya düşer → reddedildi.)
    L = Lv('LevelV1_00002020.asset')
    L.lamp([(0, 2, Y), (0, 3, Y), (0, 4, R)])
    L.lamp([(8, 2, B), (8, 3, B), (8, 4, G)])
    bush = [(x, y) for y in (3, 4) for x in (1, 2, 6, 7)]
    for x, y in bush: L.put(x, y, 42)
    return L.text([BIRD(18), OBS(63, 2), OBS(25, 10), OBS(42, len(bush)), OBS(79, 2)], moves=24)

def v203():   # "3": üst açık alan çimle örtülü (akışı kesmez, kilitler); bent iki katlı lamba, ortalarında mor (special)
    L = Lv('LevelV1_00002030.asset')
    for y in (5, 6):
        for x in (3, 4, 5): L.clear(x, y, 55)
    L.piece = [0]*len(L.piece)
    L.lamp([(3, 5, R), (4, 5, PU), (5, 5, B)])
    L.lamp([(3, 6, Y), (4, 6, PU), (5, 6, G)])
    for y in (0, 1):
        for x in range(9): L.put(x, y, 42)
    return L.text([BIRD(12), OBS(34, 4), OBS(79, 2), OBS(25, 8), OBS(42, 18)], moves=24)

def v204():   # "4": kanal artık aşağı dönerek dipte biter; dipte KAYAN mini çark (mor dahil); evlerin yanında ve sol altta
              # çiçekli çim (çiçek + altındaki çim). (Sağ üst dolap denendi: altındaki 3 hücrenin beslemesini kesti → reddedildi.)
    L = Lv('LevelV1_00002040.asset')
    for y in (8, 9, 10):
        for x in (3, 4, 5): L.clear(x, y, 75)
    L.cells[L.i(4, 9)] = 0
    ring = [(3,8),(4,8),(5,8),(5,9),(5,10),(4,10),(3,10),(3,9)]
    L.lamp([(x, y, c) for (x, y), c in zip(ring, [Y, PU, R, R, B, B, G, G])], shift=1)
    L.channel([(4,0),(4,1),(4,2),(3,2),(2,2),(2,3),(2,4),(3,4),(4,4),(4,5),(4,6),(5,6),(6,6),(6,7),(6,8),(6,9),(6,10)])
    fl = [(1,2),(1,3),(1,4),(1,5),(1,8),(2,8),(1,9),(2,9)]
    for x, y in fl: L.put(x, y, 58)
    s = L.text([BIRD(16), OBS(76, 1), OBS(79, 1), OBS(58, len(fl)), OBS(42, len(fl))], moves=25)
    return re.sub(r'^  cells: [0-9a-f]*$', f'  cells: {tohex(L.cells)}', s, count=1, flags=re.M)

def v205():   # "5" Zor v3 (kullanıcı: "sol ve sağda fener... ekrandakinden fazla fener hedefi... sütun bazlı döküm aktif,
              # parametre 0"): kenar sütunları baştan sona fener (hareketli, 2 vuruş), yalnız kendi sütunundan dolar
              # (fallback 0); lamba kutuları bir sütun içe; hunide 2 sıra çiçekli çim; roket sepeti yok.
    L = Lv('LevelV1_00002050.asset')
    for x0 in (0, 7):
        for x in (x0, x0+1):
            for y in (0, 1): L.clear(x, y)
    L.s = re.sub(r'^  ancientSeals:\n(?:  - .*\n(?:    .*\n)*)*', '  ancientSeals: []\n', L.s, count=1, flags=re.M)
    L.clear(4, 7, 38)
    for x in (0, 8):
        for y in range(L.H): L.cells[L.i(x, y)] = 1
    for x in (1, 7): L.cells[L.i(x, 2)] = 0   # lamba kutusunun altı beslenmiyordu → hole
    L.lamp([(1,0,PU),(2,0,R),(2,1,PU),(1,1,B)], shift=1)
    L.lamp([(6,0,G),(7,0,PU),(7,1,Y),(6,1,PU)], shift=1, reverse=1)
    for x in (0, 8):
        for y in range(L.H): L.put(x, y, 75)
    flower = [(x, y) for y in (4, 5) for x in range(2, 7)]
    for x, y in flower: L.put(x, y, 58)
    s = L.text([BIRD(24), OBS(79, 2), OBS(75, 30), OBS(58, len(flower)), OBS(42, len(flower))], moves=27)
    s = re.sub(r'^  cells: [0-9a-f]*$', f'  cells: {tohex(L.cells)}', s, count=1, flags=re.M)
    s = re.sub(r'^  movableSpawn.*\n', '', s, flags=re.M)
    cols = ''.join('01' if x in (0, 8) else '00' for x in range(L.W))
    s = re.sub(r'^(  pinnedSpecialTypes: .*\n)', lambda m: m[1] + f"  movableSpawnColumns: {cols}\n  movableSpawnFallbackMoves: 0\n"
               "  movableSpawnFillColumns: 1\n", s, count=1, flags=re.M)
    return s

V = {201: ('LevelV1_00002010.asset', v201), 202: ('LevelV1_00002020.asset', v202), 203: ('LevelV1_00002030.asset', v203),
     204: ('LevelV1_00002040.asset', v204), 205: ('LevelV1_00002050.asset', v205)}

if __name__ == '__main__':
    cmd, lvs = sys.argv[1], [int(a) for a in sys.argv[2:]] or list(V)
    (HERE/'after').mkdir(exist_ok=True)
    for lv in lvs:
        name, fn = V[lv]
        (P/name).write_text(fn())
        if cmd == 'preview':
            try: lampview.render(lv, HERE/f'after/L{lv}.png')
            finally: shutil.copyfile(HERE/f'before_{name}', P/name)
        print(cmd, lv)
