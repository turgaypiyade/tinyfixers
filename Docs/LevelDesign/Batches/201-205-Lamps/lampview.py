"""render_one çıktısının üstüne Lamba Şeridi (79) çerçeve + lamba çizer (131-205 çizicisi 79'u bilmiyor).
Kullanım: from lampview import render(level, out)   — asset'i katalogdan okur."""
import re, struct, sys, importlib.util
from pathlib import Path
from PIL import Image, ImageDraw
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
spec = importlib.util.spec_from_file_location('rv', HERE.parent/'131-205-Review-2026-10-31/render.py')
rv = importlib.util.module_from_spec(spec); spec.loader.exec_module(rv)
ART = ROOT/'Assets/_Project/Art/UI/Obstacles/LampStrip'
CELL, X0, Y0 = 53, 28, 95
LAMP = {1: 'Yellow', 2: 'Red', 3: 'Blue', 4: 'Green', 5: 'Mor'}

def ints(s, k):
    m = re.search(rf'^  {k}: ([0-9a-f]*)$', s, re.M)
    return [struct.unpack('<i', bytes.fromhex(m[1][i:i+8]))[0] for i in range(0, len(m[1]), 8)] if m and m[1] else []

def piece_key(cells, c, W):
    x = c % W; l = x > 0 and c-1 in cells; r = x < W-1 and c+1 in cells; u = c-W in cells; d = c+W in cells
    return ('H_Mid' if l and r else 'V_Mid' if u and d else 'C_RightDown' if r and d else 'C_LeftDown' if l and d
            else 'C_RightUp' if r and u else 'C_LeftUp' if l and u else 'H_Left' if r else 'H_Right' if l
            else 'V_Top' if d else 'V_Bottom' if u else 'H_Single')

def render(lv, out):
    d = next(x for x in rv.review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset') if x['level'] == lv)
    s = (ROOT/d['asset']).read_text()
    W = d['width']; ob = ints(s, 'obstacles'); org = ints(s, 'obstacleOrigins'); col = ints(s, 'lampColorByCell')
    im = Image.new('RGBA', (720, 890), '#19283a'); dr = ImageDraw.Draw(im)
    dr.text((24,16), f'{d["level"]} · {d["name"]}', font=rv.art.font(25,True), fill='#ffcf75')
    shifts = re.findall(r'pieceId: \d+\n    shift: (\d)', s)
    dr.text((24,55), f'{d["width"]}×{d["height"]} · {d["moves"]} moves · {sum(d["data"]["cells"])} active'
            + (' · lamba kayar' if '1' in shifts else ''), font=rv.art.font(18), fill='white')
    rv.board(im, d, (X0, Y0), CELL)
    pieces = {}
    for i, o in enumerate(ob):
        if o == 79: pieces.setdefault(org[i], set()).add(i)
    for cells in pieces.values():
        for c in cells:
            x, y = c % W, c // W; box = (X0+x*CELL, Y0+y*CELL)
            dr.rectangle((box[0], box[1], box[0]+CELL-1, box[1]+CELL-1), fill='#19283a')
            im.alpha_composite(Image.open(ART/f'LampStrip_{piece_key(cells, c, W)}.png').convert('RGBA').resize((CELL, CELL), Image.LANCZOS), box)
            if col and col[c]:
                im.alpha_composite(Image.open(ART/f'LampStrip_Lamp_{LAMP[col[c]]}.png').convert('RGBA').resize((CELL, CELL), Image.LANCZOS), box)
    gl = [rv.goal_label(g) if not (g['targetType'] == 1 and g['obstacleId'] == 79) else 'LampStrip' for g in d['goals']]
    for i, (g, lab) in enumerate(zip(d['goals'], gl)): dr.text((28,705+i*25), f'{lab}: {g["amount"]}', font=rv.art.font(18), fill='white')
    dr.text((28,856), 'Static project art · tile colours illustrative · lamba renkleri gerçek', font=rv.art.font(13), fill='#a9bdd1')
    im.convert('RGB').save(out)

if __name__ == '__main__': render(int(sys.argv[1]), sys.argv[2])
