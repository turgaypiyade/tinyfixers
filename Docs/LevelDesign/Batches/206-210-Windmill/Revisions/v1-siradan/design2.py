"""207–210 · Yel Değirmeni onluğu, devam (2026-11-01). Kullanıcı engel isteği: fener (75), kuş evi (78),
dolap (27), baykuş = HelmetPorcelain (30). 207–208 Normal, 209 Super Hard, 210 Boss Duel.
Normal şablon: LevelV1_00002060 (206; su kanalı silinir). Boss şablonu: LevelV1_00002000 (200; boss ayarları korunur).
Kullanım: python3 design2.py preview|write [lv...]
"""
import re, struct, sys, uuid, json
from pathlib import Path
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]; A = ROOT/'Assets/_Project'; P = A/'Settings/ProLevels'
WINDMILL, FLOUR, WARDROBE, HELMET, LANTERN, BIRDHOUSE, SHIELD = 80, 83, 27, 30, 75, 78, 35
SIZE2 = {WINDMILL, WARDROBE}
def tohex(v): return ''.join(struct.pack('<i', x).hex() for x in v)
def ob(id_, n=None): return ('ob', 1, id_, n)
def bird(n): return ('bird', 2, 0, n)        # Collectible Bird3 (kuş evi ürünü)
def boss(n): return ('boss', 2, 1, n)        # BossDamage

LEVELS = {}
def level(n):
    def deco(f): LEVELS[n] = f; return f
    return deco

@level(207)
def l207():
    # Fener Yağmuru (Normal): tepede 2 sıra fener "tavanı" (10) iki değirmenin arasında asılı; aşağıda taş açıldıkça
    # fenerler tahtaya iner. Alt köşelerde iki dolap. İlk karar: değirmen için special mi, fenerleri indirmek mi.
    W, H = 9, 10
    holes = {(0, 0), (8, 0), (0, 4), (8, 4)}          # kenar değirmen altı: çapraz dolum ulaşmaz (206'daki gibi)
    multi = [(WINDMILL, 0, 2), (WINDMILL, 7, 2), (WARDROBE, 1, 8), (WARDROBE, 6, 8)]
    single = {(x, y): LANTERN for x in range(2, 7) for y in (0, 1)}
    return dict(W=W, H=H, holes=holes, multi=multi, single=single, moves=24,
                goals=[ob(FLOUR, 50), ob(LANTERN, 10), ob(WARDROBE, 2)],
                name=('Fener Yağmuru', 'Lantern Rain'))

@level(208)
def l208():
    # Baykuş Kanadı (Normal): tepede V biçimli baykuş kanadı (10 HelmetPorcelain, düşen tek vuruşluk); değirmenler
    # orta yanlarda; altta 3×2 kuş evi bloğu. Kuşlar yalnız ev açıkken art arda vuruşla toplanır.
    W, H = 9, 10
    holes = {(0, 0), (8, 0), (0, 6), (8, 6)}
    multi = [(WINDMILL, 0, 4), (WINDMILL, 7, 4)]
    single = {(x, 0): HELMET for x in range(1, 8)}
    single.update({(x, 1): HELMET for x in (3, 4, 5)})
    single.update({(x, y): BIRDHOUSE for x in (3, 4, 5) for y in (8, 9)})
    return dict(W=W, H=H, holes=holes, multi=multi, single=single, moves=24,
                goals=[ob(FLOUR, 50), bird(18), ob(HELMET, 10)],
                name=('Baykuş Kanadı', 'Owl Wing'))

@level(209)
def l209():
    # Değirmen Kalesi (Super Hard): iki yanda kale kulesi — tepede değirmen, altında iki dolap (satır 4–9). Orta
    # avlu açık; fenerler orta sütunun tepesinde, kuş evleri alt ortada. Kuleler dolaplar kırılmadan inmez.
    # (v1 dört dolaplık yatay sur alt yan cepleri tamamen kesiyordu → imkânsız dolum; reddedildi.)
    W, H = 9, 11
    holes = {(0, 10), (8, 10)}
    multi = [(WINDMILL, 0, 4), (WINDMILL, 7, 4),
             (WARDROBE, 0, 6), (WARDROBE, 0, 8), (WARDROBE, 7, 6), (WARDROBE, 7, 8)]
    single = {(4, y): LANTERN for y in range(0, 4)}
    single.update({(x, y): BIRDHOUSE for x in (3, 4, 5) for y in (9, 10)})
    return dict(W=W, H=H, holes=holes, multi=multi, single=single, moves=25,
                goals=[ob(FLOUR, 50), ob(WARDROBE, 4), ob(LANTERN, 4), bird(16)],
                name=('Değirmen Kalesi', 'Windmill Keep'))

@level(210)
def l210():
    # Değirmen Düellosu (Boss): 200'ün boss ayarları; iki değirmen yanlarda, ortada fener sütunları, alt sırada
    # yardımcı kalkanlar (hedef değil). Boss fırlatmaları hedefle çakışmaz (fener hedefte → listede fener yok).
    W, H = 9, 10
    holes = {(0, 0), (8, 0), (4, 0), (4, 1), (0, 5), (8, 5)}
    multi = [(WINDMILL, 0, 3), (WINDMILL, 7, 3)]
    single = {(x, y): LANTERN for x in (3, 5) for y in range(0, 3)}
    single.update({(x, 9): SHIELD for x in (2, 3, 5, 6)})
    return dict(W=W, H=H, holes=holes, multi=multi, single=single, moves=30, boss=True,
                goals=[boss(560), ob(FLOUR, 50), ob(LANTERN, 6)],
                name=('Değirmen Düellosu', 'Windmill Duel'))

def build(n):
    d = LEVELS[n](); W, H = d['W'], d['H']; N = W*H
    cells = [0 if (i % W, i // W) in d['holes'] else 1 for i in range(N)]
    obs = [0]*N; org = [-1]*N
    for (x, y), oid in d['single'].items():
        i = y*W+x; assert cells[i] and obs[i] == 0, (n, x, y); obs[i] = oid; org[i] = i
    for oid, mx, my in d['multi']:
        o = my*W+mx
        for dx in (0, 1):
            for dy in (0, 1):
                i = (my+dy)*W+mx+dx; assert mx+dx < W and cells[i] and obs[i] == 0, (n, oid, mx, my); obs[i] = oid; org[i] = o
    return d, cells, obs, org

BLOCKING = {WINDMILL, WARDROBE, BIRDHOUSE}
MOVABLE = {LANTERN, HELMET, SHIELD}

def empties(W, H, cells, obs):
    # Renderer/GridSpawner başlangıç dolumunun aynası: tepeden düz + köşesi açık çapraz erişim.
    active = {i for i, c in enumerate(cells) if c}
    fixed = {i for i in active if obs[i] in BLOCKING}; mov = {i for i in active if obs[i] in MOVABLE}
    slots = active - fixed - mov; reached = set()
    for x in range(W):
        for y in range(H):
            i = y*W+x
            if i in fixed or i in mov: break
            if i in slots: reached.add(i)
    q = list(reached)
    # Kütüphane allowDiagonal: değirmen/dolap/kuş evi/kalkan köşeden çapraz geçirir; fener/baykuş geçirmez.
    corner = lambda i: (i not in fixed and i not in mov) or obs[i] in (WINDMILL, WARDROBE, BIRDHOUSE, SHIELD)
    while q:
        i = q.pop(); x, y = i % W, i // W
        if y+1 == H: continue
        for dx in (0, -1, 1):
            j = i+W+dx
            if not 0 <= x+dx < W or j not in slots or j in reached: continue
            if dx and not (corner(i+dx) or corner(i+W)): continue
            reached.add(j); q.append(j)
    return sorted(slots - reached)

def pins(W, H, cells, obs):
    # Hareketli engel altındaki açılış cebi: taşla dolu başlasın (199'daki pin desteği). Renk (x+2y)%4: yatayda ardışık
    # farklı, dikeyde a-b-a → pinler kendi aralarında üçlü kurmaz.
    p = [0]*(W*H)
    for i in empties(W, H, cells, obs):
        x, y = i % W, i // W; p[i] = (x + 2*y) % 4 + 1
    return p

def check(n):
    d, cells, obs, org = build(n); W = d['W']
    mills = sum(1 for oid, *_ in d['multi'] if oid == WINDMILL)
    flour = next(g[3] for g in d['goals'] if g[2] == FLOUR and g[0] == 'ob')
    assert flour % mills == 0, 'un hedefi değirmen sayısına tam bölünmeli'
    free = sum(1 for i, c in enumerate(cells) if c and obs[i] == 0)
    assert free >= flour + 10, f'{n}: un için boş hücre az ({free} < {flour}+10)'
    assert len(d['goals']) <= 5
    for kind, _, oid, a in d['goals']:
        if kind == 'ob' and oid not in (FLOUR,):
            on = sum(1 for i, o in enumerate(obs) if o == oid and org[i] == i)
            assert on == a, f'{n}: hedef {oid}={a} ama tahtada {on}'
    if d.get('boss'):
        assert d['H'] <= 10
    left = [i for i in empties(W, d['H'], cells, obs) if not any(obs[j] in MOVABLE for j in range(i % W, i, W))]
    assert not left, f'{n}: hareketli engel altı olmayan boş cep {[(i % W, i // W) for i in left]}'
    return dict(level=n, free=free, perSack=flour // mills, active=sum(cells),
                pinned=len(empties(W, d['H'], cells, obs)))

def write(n):
    d, cells, obs, org = build(n); W, H = d['W'], d['H']; N = W*H; info = check(n)
    name = f'LevelV1_0000{n}0'
    src = (P/('LevelV1_00002000.asset' if d.get('boss') else 'LevelV1_00002060.asset')).read_text()
    s = re.sub(r'^  m_Name: .*$', f'  m_Name: {name}', src, count=1, flags=re.M)
    def setline(k, v):
        nonlocal s
        s, c = re.subn(rf'^  {k}:.*$', f'  {k}: {v}', s, count=1, flags=re.M); assert c == 1, k
    setline('width', W); setline('height', H); setline('moves', d['moves'])
    for k, v in (('cells', cells), ('obstacles', obs), ('obstacleOrigins', org), ('wallPieceIds', [0]*N),
                 ('lampColorByCell', [0]*N), ('hamsterSatietyByCell', [0]*N), ('pinnedTileTypes', pins(W, H, cells, obs)),
                 ('pinnedSpecialTypes', [0]*N)):
        setline(k, tohex(v))
    for k in ('lampStripSettings', 'stackedObstacles', 'waterChannels', 'tubes', 'magnets', 'safes', 'ancientSeals',
              'colorCrateInitialHits'):
        s = re.sub(rf'^  {k}:.*\n(?:  - .*\n(?:    .*\n)*)*', f'  {k}: []\n', s, count=1, flags=re.M)
    if re.search(r'^  movableSpawnColumns:', s, re.M): setline('movableSpawnColumns', '00'*W)
    if d.get('boss'):
        # Fener hedefte → boss fener fırlatmasın (kural 5). 200 listesi: 14,12,13,30 (fener yok) aynen kalır.
        assert LANTERN not in [struct.unpack('<i', bytes.fromhex(h))[0]
                               for h in re.findall(r'.{8}', re.search(r'^  bossThrownObstacles: (\w*)', s, re.M)[1])]
    gl = ''.join(f"  - targetType: {t}\n    tileType: 0\n    obstacleId: {o}\n    collectibleId: "
                 f"{3 if k == 'bird' else 2 if k == 'boss' else 0}\n    iconOverride: {{fileID: 0}}\n    amount: {a}\n"
                 for k, t, o, a in [(g[0], g[1], (0 if g[0] == 'bird' else g[2]), g[3]) for g in d['goals']])
    s, c = re.subn(r'^  goals:\n(?:  - .*\n(?:    .*\n)*)*', '  goals:\n' + gl, s, count=1, flags=re.M); assert c == 1
    asset = P/f'{name}.asset'; meta = P/f'{name}.asset.meta'
    if meta.exists(): guid = re.search(r'guid: ([0-9a-f]+)', meta.read_text())[1]
    else:
        guid = uuid.uuid4().hex
        meta.write_text(re.sub(r'guid: [0-9a-f]+', f'guid: {guid}', (P/'LevelV1_00002060.asset.meta').read_text(), count=1))
    asset.write_text(s)
    cat = (P/'LevelCatalogProV1.asset').read_text()
    if f'level: {n}\n' not in cat:
        cat = cat.rstrip('\n') + f"\n  - chapter: 9\n    level: {n}\n    levelKey: LevelCL_0{n}\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n"
        (P/'LevelCatalogProV1.asset').write_text(cat)
    names = A/'Resources/LevelNames.json'; js = names.read_text()
    if f'"level": {n},' not in js:
        tr, en = d['name']
        entry = f'    }},\n    {{\n      "level": {n},\n      "levelKey": "LevelCL_0{n}",\n      "name": "{tr}",\n      "nameEn": "{en}"\n    }}\n  ],'
        js = re.sub(r'    \}\n  \],', entry, js, count=1); json.loads(js); names.write_text(js)
    print('yazıldı', n, name, guid, info)

if __name__ == '__main__':
    cmd, lvs = sys.argv[1], [int(a) for a in sys.argv[2:]] or sorted(LEVELS)
    for n in lvs:
        if cmd == 'check': print(check(n))
        elif cmd == 'write': write(n)
