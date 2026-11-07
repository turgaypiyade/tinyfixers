"""Only V1 235: data revision, real-sprite preview and static checks. No gameplay run."""
from pathlib import Path
from collections import Counter, deque
import argparse, copy, hashlib, importlib.util, json, re, struct, sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
OLD = HERE.parent / '235-250-Late-Game'
sys.path.insert(0, str(OLD))
import author as shared
art_spec = importlib.util.spec_from_file_location('copper_arch_art', OLD / 'render.py')
art = importlib.util.module_from_spec(art_spec)
art_spec.loader.exec_module(art)
sys.path.insert(0, str(ROOT / 'Docs/LevelDesign/Reviews/225-234-Balance-2026-11-07'))
from audit import yaml_data
from review_level_patterns import load, compare
from PIL import Image, ImageDraw

ASSET = ROOT / 'Assets/_Project/Settings/ProLevels/LevelV1_00002350.asset'
NOTE = ROOT / 'Docs/LevelDesign/Level235-Bakir-Kemer.md'
PNG = NOTE.with_suffix('.png')
MODEL = OLD / 'designs.json'
REL = str(ASSET.relative_to(ROOT))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()


def snapshot():
    if (HERE / 'protected-assets.json').exists():
        return
    HERE.mkdir(parents=True, exist_ok=True)
    files = [ASSET, Path(str(ASSET) + '.meta'), NOTE, PNG, MODEL,
             OLD / 'Contact.png', OLD / 'Previews/235-Bakir-Kemer.png']
    for p in files:
        q = HERE / 'Before' / p.relative_to(ROOT)
        q.parent.mkdir(parents=True, exist_ok=True)
        q.write_bytes(p.read_bytes())
    baseline = {str(p.relative_to(ROOT)): sha(p) for p in (ROOT / 'Assets').rglob('*') if p.is_file()}
    (HERE / 'protected-assets.json').write_text(json.dumps(baseline, indent=2) + '\n')


def make():
    rows = ['..ooooo..', '.ooooooo.'] + ['ooooooooo'] * 6 + ['.ooooooo.'] * 2 + ['..ooooo..']
    b = shared.board(235, 'Bakır Kemer', 'Copper Arch', 'Bakir-Kemer', rows, 28,
        'Alt kenardan yükselen tek bir taş kemer kütlesi; iki heykel aynı yapının ayaklarıdır. Üstte geniş çalışma avlusu vardır.',
        'Kemerin tepesini açıp merkezden aşağı ilerle veya yan açıklıklardan heykel yüzüne special hizala; mavi/yeşil renkler brandaları ilerletir.',
        'Oyma kütlesi açıldıkça çamurlu merkez görünür. Her heykelde 14 renk taşı, ardından üç ayrı special isabeti gerekir; heykelin altında da dört çamur gizlidir.',
        'İki heykel bitince onların altında açılan sekiz çamur da temizlenmelidir. Çamurun tamamı açılışta erişilebilir değildir.')
    sides = shared.rect(3, 6, 3, 1) + shared.rect(2, 7, 5, 1) + shared.rect(3, 8, 3, 2)
    sill = shared.rect(2, 10, 5, 1)
    for x, y in sides + sill:
        b.put(x, y, 25).overlay(x, y, 28)
    for x, color in [(1, 2), (6, 3)]:
        b.rect(x, 8, 2, 2, 25).overlay(x, 8, 68)
        b.d['custom']['tarpStatues'].append(dict(originCellIndex=8*9+x, color=color, count=14, size=2))
    b.goal(68, 2).goal(28, 19).goal(25, 27)
    d = b.d
    previous = next(x for x in json.loads((HERE / 'Before' / MODEL.relative_to(ROOT)).read_text()) if x['level'] == 235)
    d.update(guid=previous['guid'], revision='v2-solid-arch',
        previewRemoved=[28], previewStage='OYMA KATMANI KALDIRILIRSA',
        mechanics=['SculptingStone → Mud', 'TarpStatue: 14 renk → 3 special → 4 Mud'],
        alternatives=[
            'Eski ince sütunlara yalnız ek HP/hamle: açık dikdörtgen ve kopuk iki ayak görünümü sürerdi.',
            '234 gibi oyma→iki-hit plastik→çamur: hemen önceki seviyenin üçlü yığınını tekrar ederdi.',
            'Üstten kapanan taş kemer: altında başlangıç gölgeleri üretirdi; alt kaide geniş doğal dolum bırakır.',
            'İlk kalın U kaidesi: gerçek sprite karşılaştırmasında 176/182 alt çerçevelerine fazla yaklaştı; dikey yan kollar kaldırıldı.',
            'Tüm engelleri alta düz dikdörtgen yığmak: heykellerle maskenin kenar ilişkisini zayıflatırdı.'],
        budget='28 hamle ilk ayar · Hard. Yeni yerleşim oynanmadı; eski 18 hamle / bot %35 bu sürüme ait değildir.',
        botEvidence=None,
        illustration='Gerçek proje sprite’ları; normal renkler temsili. Katman kesiti oynanmış an değildir.')
    slots, reach = shared.feed(d)
    assert slots == reach, sorted(slots-reach)
    d['openingGeometry'] = dict(activeCells=sum(d['data']['cells']), normalSlots=len(slots),
                               naturallyFed=len(reach), shadowPins=[], specialPins=0)
    return d


def render(d):
    im = Image.new('RGBA', (1550, 1230), '#172536'); dr = ImageDraw.Draw(im)
    dr.text((32, 22), '235 · Bakır Kemer — katmanlı kaide', font=art.art.font(36, True), fill='#ffcf75')
    dr.text((32, 73), 'Hard · 9×11 · 85 aktif hücre · 28 hamle (ilk ayar)', font=art.art.font(23), fill='white')
    panels = [(32, 'AÇILIŞ', []), (548, 'OYMA KATMANI KALDIRILIRSA', [28]),
              (1064, 'HEYKELLER DE KALDIRILIRSA', [28, 68])]
    for x, title, removed in panels:
        dr.text((x, 135), title, font=art.art.font(21, True), fill='#ffcf75')
        art.drawboard(im, d, (x, 180), 48, removed)
    art.paragraph(dr, 'Alttan yükselen tek kemer kütlesi. Üst avludan merkez veya yan yüzlere doğru çalışılır.', 32, 746, 48, 21)
    art.paragraph(dr, '19 oyma taşı kalkınca çamurlu merkez görünür. Heykeller kendi renk + special işlerini korur.', 548, 746, 46, 21)
    art.paragraph(dr, 'İki heykelin altında 8 çamur daha açılır. Toplam 27 çamurun tamamı hedefte sayılır.', 1064, 746, 43, 21)
    dr.text((32, 886), 'HEDEFLER', font=art.art.font(23, True), fill='#ffcf75')
    for j, g in enumerate(d['goals']):
        x = 32 + j*505
        art.art.paste(im, art.art.sprite(art.art.ref(g['iconOverride'])), (x, 936, 64, 64))
        dr.text((x+78, 949), f'{g["amount"]} {art.label(g)}', font=art.art.font(23, True), fill='white')
    art.paragraph(dr, 'Açılış: 58/58 normal yuva doğal dolum. Hazır special, taş pini ve başlangıç boş cebi yok.', 32, 1040, 118, 22)
    art.paragraph(dr, 'Sağdaki görünümler katman kesitidir; oynanmış an veya garantili açılma sırası değildir. Normal renkler temsili.', 32, 1100, 126, 19, '#b7c9da')
    art.paragraph(dr, 'Yeni sürümün oyun dengesi ölçülmedi. Gerçek sprite ve statik veri kontrolü, kullanıcı oyun onayından ayrıdır.', 32, 1161, 125, 19, '#b7c9da')
    im.convert('RGB').save(HERE / 'Preview.png')


def render_comparison(d):
    im=Image.new('RGBA',(1760,2240),'#172536'); dr=ImageDraw.Draw(im)
    levels=load(shared.CAT); models=[]
    for n in [2,50,68,126,101,216,48,176,182,205,233,234]:
        a=next(x for x in levels if x['level']==n); raw=yaml_data(ROOT/a['asset'])
        size=a['width']*a['height']
        for key in d['data']:
            a['data'][key]=list(struct.unpack(f'<{size}i',bytes.fromhex(raw[key]))) if raw.get(key) else [0]*size
        for key in d['custom']:
            a['custom'][key]=raw.get(key) or []
            for e in a['custom'][key]:
                if 'pathCellIndices' in e:
                    e['pathCellIndices']=list(struct.unpack(f'<{len(e["pathCellIndices"])//8}i',bytes.fromhex(e['pathCellIndices'])))
        models.append(a)
    old=next(x for x in json.loads((HERE/'Before'/MODEL.relative_to(ROOT)).read_text()) if x['level']==235)
    models.extend([old,d])
    for k,a in enumerate(models):
        x=25+(k%4)*435;y=22+(k//4)*555
        title=f'{a["level"]} · {a["name"]}'
        if k==12:title+=' — eski'
        if k==13:title+=' — yeni'
        dr.text((x,y),title,font=art.art.font(20,True),fill='#ffcf75')
        art.drawboard(im,a,(x,y+46),37)
    im.convert('RGB').save(HERE/'Comparison.png')


def verify(d, applied=False):
    size = d['width'] * d['height']; w = d['width']; h = d['height']
    assert (w, h, d['kind'], len(d['goals'])) == (9, 11, 0, 3)
    assert all(len(a) == size for a in d['data'].values())
    assert not any(d['data']['pinnedTileTypes'] + d['data']['pinnedSpecialTypes'])
    planes = shared.layers(d)
    used = {o for p in planes for o, _ in p}
    assert used == {25, 28, 68}
    counts = Counter()
    for i, o in enumerate(d['data']['obstacles']):
        assert bool(o) == (d['data']['obstacleOrigins'][i] >= 0)
        if o:
            assert o == 25 and d['data']['obstacleOrigins'][i] == i
            counts[o] += 1
    for e in d['custom']['stackedObstacles']:
        o=e['obstacleId']; origin=e['originCellIndex']; fw,fh=shared.shared.footprint(o)
        assert origin%w+fw<=w and origin//w+fh<=h
        for y in range(fh):
            for x in range(fw):
                i=origin+y*w+x
                assert d['data']['cells'][i] and d['data']['obstacles'][i]==25
        counts[o]+=1
    assert counts == Counter({25:27, 28:19, 68:2})
    assert {g['obstacleId']: g['amount'] for g in d['goals']} == dict(counts)
    assert all(len(p) in (0,2) for p in planes)
    statue_cells = {i for i,p in enumerate(planes) if p and p[-1][0]==68}
    assert len(statue_cells)==8
    assert sorted((e['color'], e['count'], e['size']) for e in d['custom']['tarpStatues'])==[(2,14,2),(3,14,2)]
    occupied={i for i,p in enumerate(planes) if p}
    reached={min(occupied)}; q=deque(reached)
    while q:
        i=q.popleft()
        for j in [i-w, i+w] + ([i-1] if i%w else []) + ([i+1] if i%w+1<w else []):
            if j in occupied and j not in reached: reached.add(j); q.append(j)
    assert reached==occupied, 'The whole obstacle plinth must be connected'
    flow=[]
    for removed in [[],[28],[68],[28,68]]:
        slots,reach=shared.feed(d,removed)
        assert slots==reach,(removed,sorted(slots-reach))
        flow.append(dict(removed=removed,normalSlots=len(slots),fed=len(reach)))
    for removed_origins in [{73},{78}]:
        partial=copy.deepcopy(d)
        partial['custom']['stackedObstacles']=[e for e in partial['custom']['stackedObstacles'] if e['originCellIndex'] not in removed_origins or e['obstacleId']!=68]
        slots,reach=shared.feed(partial)
        assert slots==reach
    all_levels=load(shared.CAT)
    assert all_levels[234]['asset']==REL and all_levels[234]['levelKey']==d['levelKey']
    current=next(x for x in all_levels if x['level']==235)
    candidate=copy.deepcopy(current)
    candidate.update(width=w,height=h,data=d['data'],top=[p[-1][0] if p else 0 for p in planes],
                     blocked=[bool(p and (shared.shared.stage(p[-1][0],'blocksCells') or shared.shared.stage(p[-1][0],'behavior')==3)) for p in planes],
                     obstacleInstances=dict(counts))
    comparisons=[compare(candidate,x) for x in all_levels if x['level']<235]
    (HERE/'Similarity.json').write_text(json.dumps(dict(
        geometry=sorted(comparisons,key=lambda r:r['blockerOverlap'],reverse=True)[:12],
        cropped=sorted(comparisons,key=lambda r:r['croppedShapeOverlap'],reverse=True)[:12]),ensure_ascii=False,indent=2)+'\n')
    if applied:
        raw=yaml_data(ASSET)
        for k,a in d['data'].items():
            assert list(struct.unpack(f'<{size}i',bytes.fromhex(raw[k])))==a,k
        for k in ['width','height','moves']:assert raw[k]==d[k]
        assert raw['levelKind']==0
        assert len(raw['goals'])==3
        for k,rows in d['custom'].items(): assert (raw[k] or [])==rows,k
        for a,b in zip(raw['goals'],d['goals']):
            for k in ['targetType','tileType','obstacleId','collectibleId','amount']:assert a[k]==b[k]
            assert a['iconOverride']['fileID'] and a['iconOverride']['guid']
        before=yaml_data(HERE/'Before'/REL)
        changed={k for k in raw if raw[k]!=before[k]}
        assert changed=={'cells','obstacles','obstacleOrigins','stackedObstacles','tarpStatues','goals','moves'}
        baseline=json.loads((HERE/'protected-assets.json').read_text())
        now={str(p.relative_to(ROOT)):sha(p) for p in (ROOT/'Assets').rglob('*') if p.is_file()}
        assert now.keys()==baseline.keys(), 'Asset inventory changed'
        drift=[p for p in baseline if p!=REL and now[p]!=baseline[p]]
        assert not drift, drift
        result=dict(result='PASS',changedAssetFields=sorted(changed),protectedAssets=len(baseline)-1,
                    flow=flow,obstaclePlinthConnected=True,goals=dict(counts),simulation=False,
                    limits='Static feed graph, not runtime cascade or human balance measurement.')
        (HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
        print(json.dumps(result,ensure_ascii=False))
    else:print('Draft checked:',d['openingGeometry'],'flow:',flow)


def apply(d):
    assert not (HERE/'applied.json').exists(), 'One-shot writer; preserve later user edits'
    baseline=json.loads((HERE/'protected-assets.json').read_text())
    assert all(sha(ROOT/p)==h for p,h in baseline.items()),'External asset edit; review before writing'
    assert MODEL.read_bytes()==(HERE/'Before'/MODEL.relative_to(ROOT)).read_bytes()
    t=(HERE/'Before'/REL).read_text()
    for k,v in d['data'].items():t=shared.shared.scalar(t,k,shared.shared.encode(v))
    for k in ['stackedObstacles','tarpStatues']:t=shared.shared.entries(t,k,d['custom'][k])
    t=shared.shared.entries(t,'goals',d['goals'])
    t=shared.shared.scalar(t,'moves',d['moves'])
    ASSET.write_text(t)
    ds=json.loads(MODEL.read_text());ds[0]=d
    assert all(x['level']==235 or x==y for x,y in zip(ds,json.loads((HERE/'Before'/MODEL.relative_to(ROOT)).read_text())))
    MODEL.write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    (HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    PNG.write_bytes((HERE/'Preview.png').read_bytes())
    (OLD/'Previews/235-Bakir-Kemer.png').write_bytes(PNG.read_bytes())
    contact=Image.open(HERE/'Before'/ (OLD/'Contact.png').relative_to(ROOT)).convert('RGBA')
    dr=ImageDraw.Draw(contact);dr.rectangle((0,0,349,539),fill='#172536')
    dr.text((16,12),'235 · Bakır Kemer v2',font=art.art.font(18,True),fill='#ffcf75')
    dr.text((16,40),'Hard / 28 hamle · ilk ayar',font=art.art.font(17),fill='white')
    art.drawboard(contact,d,(16,82),29)
    dr.text((16,428),'Tek kaide / heykel altında çamur',font=art.art.font(15),fill='#b7c9da')
    dr.text((16,458),'3 hedef / 0 pin / oynanmadı',font=art.art.font(16),fill='#b7c9da')
    contact.convert('RGB').save(OLD/'Contact.png')
    (OLD/'active-revisions.json').write_text(json.dumps({'235':dict(revision=d['revision'],
        model='../235-Copper-Arch-v2/design.json',verify='../235-Copper-Arch-v2/revise.py --verify',
        oldSimulationApplies=False)},indent=2)+'\n')
    (HERE/'applied.json').write_text(json.dumps({REL:sha(ASSET)},indent=2)+'\n')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');parser.add_argument('--verify',action='store_true');args=parser.parse_args()
    snapshot();d=make();verify(d);render(d);render_comparison(d)
    if args.apply:apply(d)
    if args.apply or args.verify:verify(d,True)
