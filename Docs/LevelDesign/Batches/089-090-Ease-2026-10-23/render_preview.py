"""Before/after composition with current project sprites; no Unity or gameplay simulation."""
from pathlib import Path
import importlib.util, random, re, struct, sys
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'Tools'))
from export_level_previews import int_field, int_array, parse_entries

spec = importlib.util.spec_from_file_location('sprites', HERE.parent / '173-180/render_previews.py')
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)

def board(im, text, xy, highlight=False):
    w, h = int_field(text, 'width'), int_field(text, 'height')
    a = {k: int_array(text, k, w*h, -1 if k == 'obstacleOrigins' else 0)
         for k in ('cells', 'obstacles', 'obstacleOrigins', 'pinnedTileTypes', 'pinnedSpecialTypes')}
    top, origins = a['obstacles'][:], a['obstacleOrigins'][:]
    for entry in sorted(parse_entries(text, 'stackedObstacles'), key=lambda s: s['stackOrder']):
        i = entry['originCellIndex']
        top[i], origins[i] = entry['obstacleId'], i
    raw = re.search(r'^  randomPool: *([0-9a-f]*)$', text, re.M)[1]
    pool = list(struct.unpack(f'<{len(raw)//8}i', bytes.fromhex(raw))) if raw else list(range(4))
    dr, cell = ImageDraw.Draw(im), 38
    for i, active in enumerate(a['cells']):
        if not active: continue
        x, y = xy[0] + i % w * cell, xy[1] + i // w * cell
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=5,fill='#777eb5',outline='#989dce')
        oid = top[i]
        if oid in (0, 46):
            if oid == 46: r.paste(im, r.oid_art(46), (x,y,cell,cell))
            r.paste(im, r.TILES[random.Random(89+i).choice(pool)], (x,y,cell,cell), 2)
    # Paint complete multi-cell obstacles after every cell background, avoiding clipped chests.
    for i, oid in enumerate(top):
        if a['cells'][i] and oid not in (0,46) and origins[i] == i:
            x, y = xy[0] + i % w * cell, xy[1] + i // w * cell
            match = re.search(r'^    size: \{x: (\d+), y: (\d+)\}$', r.DEFS[oid], re.M)
            bw, bh = map(int, match.groups())
            r.paste(im, r.oid_art(oid), (x,y,bw*cell,bh*cell))
    for safe in parse_entries(text, 'safes'):
        i = safe['originCellIndex']
        r.paste(im, r.oid_art(33), (xy[0]+i%w*cell,xy[1]+i//w*cell,safe['width']*cell,safe['height']*cell))
    if highlight:
        for i in range(31,36):
            x, y = xy[0]+i%w*cell, xy[1]+i//w*cell
            dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=5,outline='#7affb5',width=2)

im = Image.new('RGBA',(840,1130),r.BG)
dr = ImageDraw.Draw(im)
dr.text((28,20),'89–90 / Oynanış geri bildirimi düzeltmesi',font=r.font(29,True),fill=r.GOLD)
for n, filename, title, y in [(89,'LevelP_00991.asset','Gizli Jel Bahçesi',85),(90,'LevelP_00500.asset','Mor Düello',588)]:
    before = (HERE/'Before'/filename).read_text()
    after = (ROOT/'Assets/_Project/Settings/ProductionLevels'/filename).read_text()
    dr.text((28,y),f'{n} · {title}',font=r.font(25,True),fill=r.INK)
    for x, text, label in [(28,before,'Önce · 28 hamle'),(445,after,'Sonra · 33 hamle')]:
        dr.text((x,y+39),label,font=r.font(20),fill=r.MUTED)
        board(im,text,(x,y+77),highlight=n==89 and x==445)
    if n == 89:
        dr.text((28,y+438),'Yeşil çerçeve: 4. satır, 5–9. sütunlar; 5 yeni oynanabilir hücre.',font=r.font(18),fill='#7affb5')
        dr.text((28,y+467),'Tam tahta jel hedefi: 41 → 46. Diğer engeller ve katmanlar aynı.',font=r.font(17),fill=r.MUTED)
dr.text((28,1077),'Güncel proje sprite’ları; taş renkleri temsili, kasa görseli şematik.',font=r.font(17),fill=r.MUTED)
dr.text((28,1103),'Oyun ekran görüntüsü değildir. Unity Play/build veya bot/kaskat simülasyonu yok.',font=r.font(15),fill=r.MUTED)
im.convert('RGB').save(HERE/'Before-After.png')
print(HERE/'Before-After.png')
