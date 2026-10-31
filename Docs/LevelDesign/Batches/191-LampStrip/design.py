"""191 Işıklı Kapı — Lamba Şeridi (LampStrip 79) KAYMAYAN tanıtımı. Tasarım verisi + önizleme.
python3 design.py preview   → preview.png (gerçek sprite'lar; taş renkleri temsili/rastgele)
"""
import sys, random, os
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
A = ROOT / 'Assets/_Project'
W, H = 9, 10
HOLES = set()
Y, R, B, G, P = 1, 2, 3, 4, 5                            # LampStripColor
# Kullanıcı düzeni (2026-11-01): lamba | taş | lamba | 3 taş | lamba | taş | lamba. Dört dikey şerit 3..9. satır,
# üstte 3 sıra açık oyun alanı. Komşu şeritler aynı satırda aynı renk → tek taş sütunundaki dikey eşleşme iki
# şeride birden vurur. Tepede mor lamba (yalnız special).
LEFT  = [P, Y, Y, R, R, G, G]
RIGHT = [P, B, B, G, G, Y, Y]
TOP = 3
PILLARS = {
    1: [(0, TOP+i, c) for i, c in enumerate(LEFT)],
    2: [(2, TOP+i, c) for i, c in enumerate(LEFT)],
    3: [(6, TOP+i, c) for i, c in enumerate(RIGHT)],
    4: [(8, TOP+i, c) for i, c in enumerate(RIGHT)],
}
MUD = {(x,y) for x in range(3,6) for y in range(TOP+1,H)}   # orta yol: şeritlerin arasında çamur 3×6
MOVES = 24
NAME, NAME_EN = 'Işıklı Kapı', 'Gate of Lights'

def lamp_cells():
    return {(x,y):(pid,c) for pid,cells in PILLARS.items() for x,y,c in cells}

def preview(out):
    S = 128
    lamps = lamp_cells()
    parts = A/'Art/UI/Obstacles/LampStrip'
    def ld(n): return Image.open(parts/f'LampStrip_{n}.png').convert('RGBA').resize((S,S), Image.LANCZOS)
    P_ = {n: ld(n) for n in ['H_Left','H_Mid','H_Right','H_Single','V_Top','V_Mid','V_Bottom','C_RightDown','C_LeftDown','C_RightUp','C_LeftUp']}
    LMP = {Y:ld('Lamp_Yellow'),R:ld('Lamp_Red'),B:ld('Lamp_Blue'),G:ld('Lamp_Green'),P:ld('Lamp_Mor')}
    halo = ld('Lamp_Hale')
    HC = {Y:(255,200,40),R:(255,70,60),B:(70,150,255),G:(80,220,100),P:(190,100,255)}
    V3 = A/'Art/Icons/Gems/AnimalStyle/V3'
    tiles = [Image.open(V3/f).convert('RGBA') for f in ['YellowStoneV7.png','RedStoneV5.png','BlueStoneV8.png','GreenStoneV9.png']]
    def fit(im):
        k = S*0.86/max(im.size); return im.resize((int(im.width*k), int(im.height*k)), Image.LANCZOS)
    tiles = [fit(t) for t in tiles]
    mud = Image.open(A/'Art/UI/Obstacles/RobotStyle/MudOverlay/MudOverlaySTG1V1.png').convert('RGBA').resize((S,S), Image.LANCZOS)
    top = 70
    img = Image.new('RGBA', (W*S+40, H*S+top+60), (24,20,44,255)); d = ImageDraw.Draw(img)
    font = ImageFont.truetype(str(A/'Fonts/Rajdhani-Bold.ttf'), 34)
    small = ImageFont.truetype(str(A/'Fonts/Rajdhani-Bold.ttf'), 22)
    d.text((20,14), f'191 · {NAME} / {NAME_EN} — {MOVES} hamle · tanıtım (kaymayan)', font=font, fill=(255,255,255))
    ox, oy = 20, top
    rng = random.Random(191)
    for y in range(H):
        for x in range(W):
            if (x,y) in HOLES: continue
            d.rounded_rectangle((ox+x*S+3, oy+y*S+3, ox+x*S+S-3, oy+y*S+S-3), radius=18,
                                fill=(122,100,190) if (x+y)%2==0 else (108,86,172))
    for (x,y) in MUD: img.alpha_composite(mud, (ox+x*S, oy+y*S))
    for (x,y),(pid,c) in lamps.items():
        same = lambda a,b: (a,b) in lamps and lamps[(a,b)][0]==pid
        l,r,u,dn = same(x-1,y), same(x+1,y), same(x,y-1), same(x,y+1)
        key = ('H_Mid' if l and r else 'V_Mid' if u and dn else 'C_RightDown' if r and dn else 'C_LeftDown' if l and dn
               else 'C_RightUp' if r and u else 'C_LeftUp' if l and u else 'H_Left' if r else 'H_Right' if l
               else 'V_Top' if dn else 'V_Bottom' if u else 'H_Single')
        img.alpha_composite(P_[key], (ox+x*S, oy+y*S))
    for (x,y),(pid,c) in lamps.items():
        img.alpha_composite(LMP[c], (ox+x*S, oy+y*S))   # hale oyunda kapalı (ShowHalo=false)
    for y in range(H):
        for x in range(W):
            if (x,y) in HOLES or (x,y) in lamps: continue
            t = tiles[rng.randrange(4)]
            img.alpha_composite(t, (ox+x*S+(S-t.width)//2, oy+y*S+(S-t.height)//2))
    d.text((20, top+H*S+14), f'Hedef: Lamba Şeridi ×{len(PILLARS)} · Çamur ×{len(MUD)}   |   taş renkleri temsili (rastgele)', font=small, fill=(220,220,235))
    img.convert('RGB').save(out)

if __name__ == '__main__':
    if sys.argv[1:] == ['preview']:
        preview(HERE/'preview.png'); print(HERE/'preview.png')


# ── Asset yazımı ──────────────────────────────────────────────────────────
import re, struct
ASSET = A/'Settings/ProLevels/LevelV1_00001910.asset'

def hexints(vals):
    return ''.join(struct.pack('<i', v).hex() for v in vals)

def build_arrays():
    n = W*H
    cells = [0 if (i%W, i//W) in HOLES else 1 for i in range(n)]
    obstacles = [0]*n; origins = [-1]*n; pieces = [0]*n; colors = [0]*n
    for (x,y) in MUD:
        i = y*W+x; obstacles[i] = 25; origins[i] = i
    for pid, cl in PILLARS.items():
        idx = [y*W+x for x,y,_ in cl]; o = min(idx)
        for (x,y,c) in cl:
            i = y*W+x; obstacles[i] = 79; origins[i] = o; pieces[i] = pid; colors[i] = c
    return cells, obstacles, origins, pieces, colors

def write_asset():
    s = ASSET.read_text()
    cells, obstacles, origins, pieces, colors = build_arrays()
    def setline(key, val):
        nonlocal s
        s, k = re.subn(rf'^  {key}:.*$', f'  {key}: {val}', s, count=1, flags=re.M)
        assert k == 1, key
    setline('width', W)
    setline('height', H)
    setline('moves', MOVES)
    for key in ('hamsterSatietyByCell', 'pinnedTileTypes', 'pinnedSpecialTypes'):
        setline(key, hexints([0]*(W*H)))
    setline('cells', hexints(cells))
    setline('obstacles', hexints(obstacles))
    setline('obstacleOrigins', hexints(origins))
    setline('wallPieceIds', hexints(pieces))
    goals = ("  goals:\n"
             "  - targetType: 1\n    tileType: 0\n    obstacleId: 79\n    collectibleId: 0\n"
             "    iconOverride: {fileID: 0}\n    amount: %d\n"
             "  - targetType: 1\n    tileType: 0\n    obstacleId: 25\n    collectibleId: 0\n"
             "    iconOverride: {fileID: 0}\n    amount: %d\n") % (len(PILLARS), len(MUD))
    s, k = re.subn(r'^  goals:\n(?:  - .*\n(?:    .*\n)*)*', goals, s, count=1, flags=re.M); assert k == 1
    s, k = re.subn(r'^  waterChannels:.*\n(?:  - .*\n(?:    .*\n)*)*', '  waterChannels: []\n', s, count=1, flags=re.M); assert k == 1
    s = re.sub(r'^  lampColorByCell:.*\n', '', s, flags=re.M)
    s = re.sub(r'^  lampStripSettings:.*\n(?:  - .*\n(?:    .*\n)*)*', '', s, flags=re.M)
    settings = "  lampStripSettings:\n" + "".join(
        f"  - pieceId: {pid}\n    shift: 0\n    reverse: 0\n" for pid in PILLARS)
    s = re.sub(r'^(  wallPieceIds: .*\n)', lambda m: m.group(1) + f"  lampColorByCell: {hexints(colors)}\n" + settings,
               s, count=1, flags=re.M)
    ASSET.write_text(s)

if __name__ == '__main__' and sys.argv[1:] == ['write']:
    write_asset(); print('yazıldı', ASSET)
