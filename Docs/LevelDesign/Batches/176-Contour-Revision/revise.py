"""Scoped176 revision: author, render and static checks; no builds or game simulation."""
from pathlib import Path
from collections import Counter, deque
import importlib.util, json, random, re, subprocess, sys
from PIL import Image, ImageDraw

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
ASSET=ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00001760.asset'
CAT=ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset'
NAMES=ROOT/'Assets/_Project/Resources/LevelNames.json'
sys.path.insert(0,str(HERE.parent/'226-230-Lantern-Wind-Crystal'))
import design as shared
spec=importlib.util.spec_from_file_location('contour_art',HERE.parent/'173-180/render_previews.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)


def before(path):return HERE/'Before'/path.relative_to(ROOT)
def make():
    b=shared.Board(176,'Kızıl ve Yeşil','Crimson and Green','Kizil-ve-Yesil',
        ['.oooooo.']+['oooooooo']*4+['.oooooo.']*2+['..oooo..'],26,
        'Geniş ağızdan daralan tek çanak; kırmızı ve yeşil alt kontur V biçiminde birleşir.',
        'Kırmızı veya yeşil eşleşmeyle ilgili kenarı aşındır; ortak special iki rengi de ilerletebilir.',
        'Dış dirsekler açılınca iç alt kutulara yeni normal eşleşme yüzleri ve daha geniş special hattı açılır.',
        'Beş kırmızı ve beş yeşil sandığı bitir; her biri üç hit ister. Yeni ailenin ilk V1 tanıtımı rahat Normal kalır.')
    red=[(1,5),(1,6),(2,6),(2,7),(3,7)]
    for x,y in red:b.put(x,y,72);b.put(7-x,y,74)
    b.goal(72,5).goal(74,5)
    d=b.d
    d.update(difficulty='Normal introduction',budget='26 hamle ve5+5hedef korunur;yük30kabul edilen hit. Yardımcı,hazırspecial,ekengel veya katman yok.',
        alternatives=['İki bağımsız2×2blok: yine normal taşlar arasında kopuk adacık olacaktı.',
            'Dört renkli tam çerçeve:178pekiştirmesini ve henüz tanıtılmamış renkleri erkene çekerdi.',
            'Sandıkları artırmak/hamleyi azaltmak:176’nın rahat tanıtım işlevini bozardı.'])
    return d


def graph(d,removed=()):
    w,h=d['width'],d['height'];active={i for i,a in enumerate(d['data']['cells'])if a}
    blocked={i for i,o in enumerate(d['data']['obstacles'])if o and o not in removed}
    slots=active-blocked;reach=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in blocked:break
            if i in active:reach.add(i)
    q=deque(reach)
    def corner(i):return i in active and(i not in blocked or shared.stage(d['data']['obstacles'][i],'allowDiagonal'))
    while q:
        i=q.popleft();x,y=i%w,i//w
        if y==h-1:continue
        for dx in (0,-1,1):
            j=i+w+dx
            if not 0<=x+dx<w or j not in slots or j in reach:continue
            if dx and not(corner(i+dx)or corner(i+w)):continue
            reach.add(j);q.append(j)
    return slots,reach&slots


def apply():
    baseline=json.loads((HERE/'protected-before.json').read_text())
    owned=json.loads((HERE/'owned-hashes.json').read_text())if(HERE/'owned-hashes.json').exists()else{}
    for rel,h in baseline.items():
        expected=owned.get(rel,h)
        assert shared.sha(ROOT/rel)==expected,('Concurrent change; preserve and stop',rel)
    d=make();source=before(ASSET).read_text()
    for k,v in dict(width=8,height=8).items():source=shared.scalar(source,k,v)
    for k in ['cells','obstacles','obstacleOrigins','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes']:
        source=shared.scalar(source,k,shared.encode(d['data'][k]))
    # Preserve exact goals,26moves,music,intro and all unrelated serialized settings.
    ASSET.write_text(source)
    (HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    (HERE/'owned-hashes.json').write_text(json.dumps({str(ASSET.relative_to(ROOT)):shared.sha(ASSET)},indent=2)+'\n')


def board(im,d,xy,cell):
    dr=ImageDraw.Draw(im);w=d['width'];slots,reach=graph(d);rng=random.Random(176);colors={}
    for i,a in enumerate(d['data']['cells']):
        if a:
            x,y=xy[0]+i%w*cell,xy[1]+i//w*cell
            dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=5,fill='#354d6a',outline='#819ab6',width=2)
    for i in sorted(reach):
        choices=list(range(4));rng.shuffle(choices)
        for c in choices:
            if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==c)and not(i//w>=2 and colors.get(i-w)==colors.get(i-w*2)==c)and not(i%w and i//w and all(colors.get(j)==c for j in[i-1,i-w,i-w-1])):break
        colors[i]=c;art.paste(im,art.TILES[c],(xy[0]+i%w*cell,xy[1]+i//w*cell,cell,cell),cell*.06)
    for i,o in enumerate(d['data']['obstacles']):
        if o:art.paste(im,art.oid_art(o),(xy[0]+i%w*cell,xy[1]+i//w*cell,cell,cell),cell*.01)


def render():
    d=make();oldtext=before(ASSET).read_text()
    old=dict(width=7,height=7,data={k:shared.array(oldtext,k,49)for k in ['cells','obstacles','obstacleOrigins']})
    im=Image.new('RGBA',(1250,1080),'#172536');dr=ImageDraw.Draw(im)
    dr.text((32,24),'176 · KIZIL VE YEŞİL',font=art.font(35,True),fill='#ffcf75')
    dr.text((32,79),'Normal / renkli sandık tanıtımı · 26 hamle · 5 kırmızı + 5 yeşil',font=art.font(23),fill='#e7edf6')
    dr.text((32,139),'ESKİ · REDDEDİLEN DÜZEN',font=art.font(22,True),fill='#f4a6a1')
    board(im,old,(32,191),66)
    dr.text((670,139),'YENİ · TEK ALT KONTUR',font=art.font(22,True),fill='#ffcf75')
    board(im,d,(670,191),65)
    dr.text((32,729),'7×7 · 49 aktif /39 normal yuva',font=art.font(22),fill='#d7e1ed')
    dr.text((670,746),'8×8 · 54 aktif /44 normal yuva',font=art.font(22),fill='#d7e1ed')
    dr.text((32,808),'İki ayrı şerit, tahta sınırından kopuk.',font=art.font(21),fill='#f4b7b3')
    dr.text((670,808),'İki renk, aynı daralan kenarı tamamlıyor.',font=art.font(21),fill='#e7edf6')
    dr.text((32,880),'Zorluk şişirilmedi: hedefler, üç-hit sandıklar ve26hamle aynı. Yeni engel /çim /hazır special yok.',font=art.font(21),fill='#e7edf6')
    dr.text((32,934),'44/44başlangıç normal yuvası doğal beslenir. Kesit /taş renkleri temsili; oynanış simülasyonu değildir.',font=art.font(19),fill='#a9bdd1')
    dr.text((32,986),'İsim,GUID/meta,katalog,diğer levellar ve runtime aynı; manuel oyun testi kullanıcıda.',font=art.font(20),fill='#a9bdd1')
    im.convert('RGB').save(HERE/'Before-After.png')
    single=Image.new('RGBA',(750,880),'#172536');dr=ImageDraw.Draw(single)
    dr.text((30,22),'176 · Kızıl ve Yeşil',font=art.font(32,True),fill='#ffcf75')
    dr.text((30,70),'Normal · 26hamle · 5 kırmızı /5 yeşil',font=art.font(22),fill='#e7edf6')
    board(single,d,(39,126),84)
    dr.text((30,824),'Gerçek sprite’lar · Serbest taş renkleri temsili',font=art.font(20),fill='#a9bdd1')
    single.convert('RGB').save(ROOT/'Docs/LevelDesign/Level176-Kizil-ve-Yesil.png')


def verify():
    d=make();t=ASSET.read_text();old=before(ASSET).read_text()
    parsed=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e',"s=File.readlines(ARGV[0]).reject{|l|l.start_with?('%','---')}.join;puts JSON.generate(YAML.safe_load(s,aliases:false)['MonoBehaviour'])",str(ASSET)]))
    assert(parsed['width'],parsed['height'],parsed['moves'],parsed['levelKind'])==(8,8,26,0)
    counts=Counter(d['data']['obstacles']);assert counts=={0:54,72:5,74:5}
    for key in ['cells','obstacles','obstacleOrigins','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes']:
        assert shared.array(t,key,64)==d['data'][key]
    assert sum(d['data']['cells'])==54
    for i,o in enumerate(d['data']['obstacles']):
        assert d['data']['obstacleOrigins'][i]==(i if o else-1)
        if o:assert d['data']['cells'][i] and i//8>=5
    def connected(cells):
        q=deque([min(cells)]);seen=set(q)
        while q:
            i=q.popleft();x,y=i%8,i//8
            for xx,yy in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
                j=yy*8+xx
                if 0<=xx<8 and 0<=yy<8 and j in cells and j not in seen:seen.add(j);q.append(j)
        return seen==cells
    assert connected({i for i,a in enumerate(d['data']['cells'])if a})
    assert connected({i for i,o in enumerate(d['data']['obstacles'])if o})
    for o in [72,74]:assert connected({i for i,v in enumerate(d['data']['obstacles'])if v==o})
    phases=[]
    for removed in [[],[72],[74],[72,74]]:
        slots,reach=graph(d,removed);assert slots==reach
        phases.append(dict(removed=removed,normalSlots=len(slots),naturallyFed=len(reach)))
    # Strict text scope: only geometry/dimension array lines may change.
    pattern=r'^  (?:width|height|cells|obstacles|obstacleOrigins|wallPieceIds|hamsterSatietyByCell|pinnedTileTypes|pinnedSpecialTypes):[^\n]*\n'
    assert re.sub(pattern,'',t,flags=re.M)==re.sub(pattern,'',old,flags=re.M)
    assert {g['obstacleId']:g['amount']for g in parsed['goals']}=={72:5,74:5}
    assert shared.sha(Path(str(ASSET)+'.meta'))==shared.sha(before(Path(str(ASSET)+'.meta')))
    baseline=json.loads((HERE/'protected-before.json').read_text());target=str(ASSET.relative_to(ROOT))
    drift=[p for p,h in baseline.items()if p!=target and shared.sha(ROOT/p)!=h];assert not drift,drift
    assert shared.sha(CAT)==shared.sha(before(CAT)) and shared.sha(NAMES)==shared.sha(before(NAMES))
    old_d=dict(width=7,height=7,data={k:shared.array(old,k,49)for k in ['cells','obstacles','obstacleOrigins']})
    old_slots,old_reach=graph(old_d)
    report=dict(result='PASS',changedLevelOnly=176,preservedOtherAssetFiles=len(baseline)-1,
        nameAndGuidAndCatalogPreserved=True,normalIntroduction=True,moves=26,goals={72:5,74:5},
        activeCells=54,connectedObstacleComponents=1,perColorComponents=1,phases=phases,
        previousStaticFeed=dict(normalSlots=len(old_slots),naturallyFed=len(old_reach),unfed=sorted(old_slots-old_reach)),
        pins=0,grass=False,buildOrGameSimulation=False,measuredBalance=False)
    (HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(report,ensure_ascii=False))


if __name__=='__main__':
    if '--apply'in sys.argv:apply()
    render()
    if '--apply'in sys.argv or '--verify'in sys.argv:verify()
