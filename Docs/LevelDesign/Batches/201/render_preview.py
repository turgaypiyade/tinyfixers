"""Current project sprite preview. Illustrative tile colors; no gameplay/physics execution."""
from pathlib import Path
import importlib.util, random, re, sys
from PIL import Image, ImageDraw
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(ROOT/'Tools'))
from design import DESIGN
from review_level_patterns import load
spec = importlib.util.spec_from_file_location('project_art', HERE.parent/'173-180/render_previews.py')
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)

def board(image, d, xy, cell):
    draw = ImageDraw.Draw(image)
    w, h = d.get('w', d.get('width')), d.get('h', d.get('height'))
    data = d['data']; top = d.get('top', data['obstacles'])
    origins = d.get('topOrigins', data['obstacleOrigins'])
    rng = random.Random(d.get('n', d.get('level', 201)))
    colors = {}
    for i, active in enumerate(data['cells']):
        if not active: continue
        x, y = xy[0]+i%w*cell, xy[1]+i//w*cell
        draw.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1), radius=max(2,cell//8), fill='#485b79', outline='#7587a2')
        if not top[i]:
            choices = list(range(4)); rng.shuffle(choices)
            color = next((c for c in choices if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==c)
                          and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==c)), choices[0])
            colors[i] = color
            r.paste(image, r.TILES[color], (x,y,cell,cell), cell*.055)
    for i, oid in enumerate(top):
        if not oid or not data['cells'][i] or origins[i]!=i: continue
        # Size is resolved from the current library, never guessed from artwork.
        match = re.search(r'^    size: \{x: (\d+), y: (\d+)\}$', r.DEFS[oid], re.M)
        bw,bh = map(int, match.groups())
        r.paste(image, r.oid_art(oid), (xy[0]+i%w*cell,xy[1]+i//w*cell,cell*bw,cell*bh), cell*.015)

def render():
    d=DESIGN
    image=Image.new('RGBA',(1280,1080),r.BG); draw=ImageDraw.Draw(image)
    draw.text((30,22),'201 · Guguk Bahçesi / Cuckoo Garden',font=r.font(34,True),fill=r.GOLD)
    draw.text((30,73),'9 × 10 · 26 hamle · 4 renk · Tanıtım / Normal',font=r.font(23),fill=r.INK)
    draw.text((30,111),'Güncel proje sprite’ları · Taş renkleri temsili · Oyun ekran görüntüsü değildir',font=r.font(17),fill=r.MUTED)
    board(image,d,(30,160),70)
    x,y=700,160
    r.paste(image,r.sprite(r.ref(r.DEFS[78].split('goalIconSprite: ',1)[1])),(x,y,76,82))
    draw.text((x+90,y+17),'50 KUŞ TOPLA',font=r.font(26,True),fill=r.GOLD)
    y+=115
    for heading,body in [
        ('İKİ TOPLU BÖLGE','Sağ üstte ve sol altta 2 sütun × 3 satır. Toplam 12 ev; tek başına dağılmış ev yok.'),
        ('ÖNCE AÇ, SONRA TOPLA','Evin yanında eşleşme yap: kuş dışarı çıkar. Aynı zincirde veya sonraki hamlede tekrar vur: kuş hedefe uçar.'),
        ('BÖLGEYE ODAKLAN','Açtığın gruba tekrar vurmayı planla. Bir sonraki hamlede vuruş gelmezse kuş yuvaya döner.'),
        ('EVLER YERİNDE KALIR','Hedef evleri kırmak değil, kuş toplamak. Evler ortak hedef dolana kadar tekrar kuş üretir.'),
        ('EŞLEŞME ŞERİTLERİ','Her grubun iki yanında açık taş şeridi var. Merkez geniş; 76 normal hücrenin tamamı statik dolum grafına bağlı.')]:
        draw.text((x,y),heading,font=r.font(20,True),fill=r.GOLD); y+=31
        y=r.paragraph(draw,x,y,body,width=42,size=19); y+=25
    draw.text((30,920),'88 aktif / 2 köşe hole · 12 ev / 2 grup · 76 normal yuva · Hazır special / renk pini yok',font=r.font(20),fill=r.INK)
    for j,text in enumerate(['Yeni mekanik tek başına tanıtılır; başka engel/katman veya ek toplama hedefi yok.',
                              'Statik mask, dolum ve komşuluk kontrolü; hamle sayısı ilk ayar, denge ölçülmedi.',
                              'Unity Play/build, bot/kaskat simülasyonu ve remote yayın yapılmadı. Önceki seviyeler korundu.']):
        draw.text((30,966+j*30),text,font=r.font(17),fill=r.MUTED)
    image.convert('RGB').save(HERE/'Level201-Guguk-Bahcesi.png')
    levels=load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')
    refs=[next(l for l in levels if l['level']==n) for n in (20,86,150,200)]
    contact=Image.new('RGBA',(1360,650),r.BG); draw=ImageDraw.Draw(contact)
    draw.text((24,20),'Yerel karşılaştırma · benzer kütleler ve komşu seviyeler',font=r.font(28,True),fill=r.GOLD)
    for j,ref in enumerate(refs):
        x=24+j*335
        draw.text((x,77),f'{ref["level"]} · {ref["name"][:21]}',font=r.font(20,True),fill=r.INK)
        board(contact,ref,(x,120),32)
    draw.text((24,532),'20: dört sandık köşesi · 86: dört üretici bölgesi · 150: katmanlı kanatlar · 200: Boss Duel',font=r.font(18),fill=r.MUTED)
    draw.text((24,568),'201: istenen çapraz iki küçük üretici grubu; kalıcı ev + tekrar vuruş penceresi yeni ana karar.',font=r.font(18),fill=r.INK)
    draw.text((24,606),'Mevcut sprite’larla statik karşılaştırma; taş renkleri temsili. Benzerlik puanı onay/denge değildir.',font=r.font(17),fill=r.MUTED)
    contact.convert('RGB').save(HERE/'Local-References.png')
    print('Rendered candidate and four local references; no level assets changed.')

if __name__=='__main__': render()
