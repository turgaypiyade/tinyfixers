"""206–210 · Yel Değirmeni onluğu (2026-11-01, kullanıcı: "206-210'u tasarla, hedefim UN olsun"). Yeni seviye asset'i +
katalog girişi + TR/EN ad üretir. Şablon: LevelV1_00001910 (temiz yapı). python3 design.py preview | write <lv...>
"""
import re, struct, sys, uuid, json, random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]; A = ROOT/'Assets/_Project'; P = A/'Settings/ProLevels'
WINDMILL, FLOUR = 80, 83
def tohex(v): return ''.join(struct.pack('<i', x).hex() for x in v)

LEVELS = {}
def level(n): 
    def deco(f): LEVELS[n] = f; return f
    return deco

@level(206)
def l206():
    # Tanıtım (kolay): tek değirmen tepede, etrafı açık; çuvalın düşeceği geniş taş alanı. Yalnız Un hedefi.
    W, H = 8, 10
    holes = {(0,0),(1,0),(6,0),(7,0),(0,1),(7,1)}
    obs = {}
    mills = [(3, 2)]
    return dict(W=W, H=H, holes=holes, mills=mills, obs=obs, moves=25, extra_goals=[],
                name=('Değirmen Tepesi', 'Windmill Hill'))

def build(n):
    d = LEVELS[n](); W, H = d['W'], d['H']; N = W*H
    cells = [0 if (i % W, i // W) in d['holes'] else 1 for i in range(N)]
    ob = [0]*N; org = [-1]*N
    for (x, y), (oid, o) in d['obs'].items(): ob[y*W+x] = oid; org[y*W+x] = o
    for mx, my in d['mills']:
        o = my*W+mx
        for dx in (0,1):
            for dy in (0,1):
                i = (my+dy)*W+mx+dx; assert cells[i] and ob[i] == 0; ob[i] = WINDMILL; org[i] = o
    return d, cells, ob, org

def write(n):
    d, cells, ob, org = build(n); W, H = d['W'], d['H']; N = W*H
    name = f'LevelV1_0000{n}0'
    src = (P/'LevelV1_00001910.asset').read_text()
    s = re.sub(r'^  m_Name: .*$', f'  m_Name: {name}', src, count=1, flags=re.M)
    def setline(k, v):
        nonlocal s
        s, c = re.subn(rf'^  {k}:.*$', f'  {k}: {v}', s, count=1, flags=re.M); assert c == 1, k
    setline('width', W); setline('height', H); setline('moves', d['moves'])
    for k, v in (('cells', cells), ('obstacles', ob), ('obstacleOrigins', org), ('wallPieceIds', [0]*N),
                 ('lampColorByCell', [0]*N), ('hamsterSatietyByCell', [0]*N), ('pinnedTileTypes', [0]*N), ('pinnedSpecialTypes', [0]*N)):
        setline(k, tohex(v))
    s = re.sub(r'^  lampStripSettings:.*\n(?:  - .*\n(?:    .*\n)*)*', '  lampStripSettings: []\n', s, count=1, flags=re.M)
    s = re.sub(r'^  stackedObstacles:.*\n(?:  - .*\n(?:    .*\n)*)*', '  stackedObstacles: []\n', s, count=1, flags=re.M)
    flour = 25 * len(d['mills'])
    goals = [(1, FLOUR, flour)] + d['extra_goals']
    gl = ''.join(f"  - targetType: {t}\n    tileType: 0\n    obstacleId: {o}\n    collectibleId: 0\n    iconOverride: {{fileID: 0}}\n    amount: {a}\n" for t, o, a in goals)
    s, c = re.subn(r'^  goals:\n(?:  - .*\n(?:    .*\n)*)*', '  goals:\n' + gl, s, count=1, flags=re.M); assert c == 1
    asset = P/f'{name}.asset'; meta = P/f'{name}.asset.meta'
    if meta.exists(): guid = re.search(r'guid: ([0-9a-f]+)', meta.read_text())[1]
    else:
        guid = uuid.uuid4().hex
        meta.write_text(re.sub(r'guid: [0-9a-f]+', f'guid: {guid}', (P/'LevelV1_00001910.asset.meta').read_text(), count=1))
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
    print('yazıldı', n, name, guid)

def preview(n, out):
    d, cells, ob, org = build(n); W, H = d['W'], d['H']; S = 96
    art = A/'Art/UI/Obstacles'
    mill = Image.open(art/'Windmill/Windmill_Preview.png').convert('RGBA').resize((2*S, 2*S), Image.LANCZOS)
    V3 = A/'Art/Icons/Gems/AnimalStyle/V3'
    tiles = [Image.open(V3/f).convert('RGBA') for f in ['YellowStoneV7.png','RedStoneV5.png','BlueStoneV8.png','GreenStoneV9.png']]
    tiles = [t.resize((int(t.width*S*.84/max(t.size)), int(t.height*S*.84/max(t.size))), Image.LANCZOS) for t in tiles]
    img = Image.new('RGBA', (W*S+40, H*S+130), (24,20,44,255)); dr = ImageDraw.Draw(img)
    font = ImageFont.truetype(str(A/'Fonts/Rajdhani-Bold.ttf'), 30)
    dr.text((20,14), f'{n} · {d["name"][0]} / {d["name"][1]} — {d["moves"]} hamle', font=font, fill='white')
    ox, oy = 20, 64; rng = random.Random(n)
    for y in range(H):
        for x in range(W):
            if not cells[y*W+x]: continue
            dr.rounded_rectangle((ox+x*S+3, oy+y*S+3, ox+x*S+S-3, oy+y*S+S-3), 16, fill=(122,100,190) if (x+y)%2 else (108,86,172))
            if ob[y*W+x]: continue
            t = tiles[rng.randrange(4)]; img.alpha_composite(t, (ox+x*S+(S-t.width)//2, oy+y*S+(S-t.height)//2))
    for mx, my in d['mills']: img.alpha_composite(mill, (ox+mx*S, oy+my*S))
    dr.text((20, oy+H*S+16), f'Hedef: Un ×{25*len(d["mills"])} (çuval rastgele iner, 5×5) · taş renkleri temsili', font=ImageFont.truetype(str(A/'Fonts/Rajdhani-Bold.ttf'), 22), fill=(220,220,235))
    img.convert('RGB').save(out)

if __name__ == '__main__':
    cmd, lvs = sys.argv[1], [int(a) for a in sys.argv[2:]] or sorted(LEVELS)
    for n in lvs:
        if cmd == 'preview': preview(n, HERE/f'L{n}.png'); print(HERE/f'L{n}.png')
        elif cmd == 'write': write(n)
