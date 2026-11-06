"""Scoped 222–225 authoring. Static data only; no Unity, gameplay simulation or deployment."""
from pathlib import Path
from collections import Counter, deque
import hashlib, importlib.util, json, re, struct, sys, uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
A = ROOT / 'Assets/_Project'
PRO = A / 'Settings/ProLevels'
CAT = PRO / 'LevelCatalogProV1.asset'
NAMES = A / 'Resources/LevelNames.json'
sys.path.insert(0, str(ROOT / 'Tools'))
from check_levels161_162 import DEFS, footprint, stage

ARRAYS = ('cells', 'obstacles', 'obstacleOrigins', 'wallPieceIds', 'lampColorByCell',
          'hamsterSatietyByCell', 'pinnedTileTypes', 'pinnedSpecialTypes')
CUSTOM = ('stackedObstacles', 'tubes', 'magnets', 'safes', 'ancientSeals', 'waterChannels',
          'tarpStatues', 'lampStripSettings', 'paintCanBoxColors', 'colorCrateInitialHits', 'dimerdals')

def encode(a): return struct.pack('<' + str(len(a)) + 'i', *a).hex()
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def scalar(t, k, value):
    t, n = re.subn(r'^  ' + k + r':[^\n]*$', lambda _: f'  {k}: {value}', t, flags=re.M)
    assert n == 1, k
    return t
def entries(t, k, rows):
    body = f'  {k}: []\n' if not rows else f'  {k}:\n' + ''.join(
        ''.join(('  - ' if j == 0 else '    ') + key + ': ' + str(value) + '\n'
                for j, (key, value) in enumerate(row.items())) for row in rows)
    t, n = re.subn(r'^  ' + k + r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*', lambda _: body, t, flags=re.M)
    assert n == 1, k
    return t
def icon(oid):
    ref=re.search(r'^    goalIconSprite: (.*)$', DEFS[oid], re.M)[1]
    return ref if 'guid:' in ref else re.search(r'^    - sprite: (.*)$',DEFS[oid],re.M)[1]

class Board:
    def __init__(self, n, title, en, slug, rows, moves, idea, first, change, remaining):
        self.d = dict(level=n, name=title, nameEn=en, slug=slug, levelKey=f'LevelCL_{n:04d}',
                      asset=f'Assets/_Project/Settings/ProLevels/LevelV1_{n*10:08d}.asset',
                      width=len(rows[0]), height=len(rows), moves=moves, kind=0, rows=rows,
                      idea=idea, firstDecision=first, change=change, remaining=remaining,
                      data={k: [0] * (len(rows)*len(rows[0])) for k in ARRAYS},
                      custom={k: [] for k in CUSTOM}, goals=[], previewRemoved=[], openedDimerdals=False)
        assert all(len(row) == self.d['width'] for row in rows)
        self.d['data']['cells'] = [int(c == 'o') for row in rows for c in row]
        self.d['data']['obstacleOrigins'] = [-1] * len(self.d['data']['cells'])
    def put(self, x, y, oid):
        d=self.d; w=d['width']; origin=y*w+x; fw,fh=footprint(oid)
        assert x+fw<=w and y+fh<=d['height']
        for yy in range(y,y+fh):
            for xx in range(x,x+fw):
                i=yy*w+xx
                assert d['data']['cells'][i] and not d['data']['obstacles'][i], (d['level'],x,y,oid,i)
                d['data']['obstacles'][i]=oid; d['data']['obstacleOrigins'][i]=origin
        return self
    def rect(self,x,y,w,h,oid):
        assert footprint(oid)==(1,1)
        for yy in range(y,y+h):
            for xx in range(x,x+w): self.put(xx,yy,oid)
        return self
    def overlay(self,x,y,oid,order=1):
        self.d['custom']['stackedObstacles'].append(dict(originCellIndex=y*self.d['width']+x,
            obstacleId=oid,stackOrder=order,initialRemainingHits=0)); return self
    def goal(self,oid,amount):
        self.d['goals'].append(dict(targetType=1,tileType=0,obstacleId=oid,collectibleId=0,
            iconOverride=icon(oid),amount=amount)); return self
    def diamonds(self,amount,oid):
        self.d['goals'].append(dict(targetType=2,tileType=0,obstacleId=0,collectibleId=4,
            iconOverride=icon(oid),amount=amount)); return self
    def keys(self,amount):
        lib=(A/'Resources/TileIconLibrary_Main.asset').read_text()
        key=re.search(r'^  key: (.*)$',lib,re.M)[1]
        self.d['goals'].append(dict(targetType=0,tileType=9,obstacleId=0,collectibleId=0,
            iconOverride=key,amount=amount)); return self

def designs():
    b=Board(222,'Zümrüt Yaprak','Emerald Leaf','Zumrut-Yaprak',[
        '.....oo..','...oooo..','..oooooo.','.oooooooo','ooooooooo','ooooooooo',
        '.oooooooo','..oooooo.','...oooo..','....oo...','....oo...'],24,
        'Sağa uzanan yaprak silueti; ortadaki kesintisiz çapraz çim damarı ve sapı tamamlayan yeşil vitrin.',
        'Çim damarının dış yüzünden mi ilerleyeceksin, sapın üstünde yeşil eşleşme mi hazırlayacaksın?',
        'Çim kalktıkça yaprağın iki yüzü tek eşleşme alanına dönüşür; yeşil kepenk için daha çok hizalama açılır.',
        'Yeşil vitrini aç, toplam 10 elması üret ve aynı renk elmas eşleşmeleri/special ile ayrıca topla.')
    b.rect(2,3,4,2,42).rect(3,5,4,2,42).put(4,9,88).diamonds(10,97).goal(42,16)
    b.d.update(previewRemoved=[42],openedDimerdals=True,
        budget='221’in 8 elmasına karşı 10 elmas + 16 çim. Tek yeni renk varyantı; 24 hamle ilk tahmin.',
        alternatives=['İki renkli simetrik vitrin: 221 kaidesini gereksiz tekrar eder.',
                      'Tam dikdörtgen yeşil bant: yaprak silueti daha belirgin bir ilk bakış sağlar.'])
    result=[b.d]

    b=Board(223,'Uçan Anahtar','Flying Key','Ucan-Anahtar',[
        '.ooo.....','ooooo....','ooooo....','ooooooooo','ooooooooo','ooooo.ooo','.ooo..ooo'],23,
        'Yatay anahtar silueti: solda yuvarlak baş, sağda iki diş ve uçtaki bitişik 2×2 anahtar makineleri.',
        'Sağdaki porselen şaftı indirip makinelerin üst yüzünü mü açacaksın, soldaki oyma bloğunu mu çözeceksin?',
        'Şaft açılınca makinelerle seri üretim başlar; sol baştaki 2×3 oyma bloğu kalkınca çamurlu taban görünür.',
        '14 anahtarı kendi aralarında eşleştir/special ile temizle; oyma bloğunun altındaki 6 çamuru bitir.')
    b.rect(1,4,2,3,25)
    for y in range(4,7):
        for x in (1,2): b.overlay(x,y,28)
    b.rect(5,3,4,2,30).rect(7,5,2,2,44).keys(14).goal(30,8).goal(28,6).goal(25,6)
    b.d.update(previewRemoved=[28,30],
        budget='27 başlangıç taşı yuvası; sınırlı baş ve iki sıra şaftta special hizalama gerekir. 23 hamle ilk tahmin.',
        alternatives=['Yeni bir Dimerdal rengi daha: tekniği tekrar etmek yerine eski anahtar davranışıyla görsel ilişki kuruldu.',
                      'Dik anahtar deliği: 222 ile art arda uzun dar sap tekrarını güçlendirirdi.'])
    result.append(b.d)

    b=Board(224,'Kristal Çapa','Crystal Anchor','Kristal-Capa',['oooooooo']*11,24,
        'Düz tahtanın içinde engeller bir çapa çizer: brandalı tepe halkası, ahşap gövde/kuşak, mavi-kırmızı kristal uçlar.',
        'Üstteki dört çimi kaldırıp heykelin mavi etiketini mi başlatacaksın, altta iki renkli vitrinin yüzlerini mi açacaksın?',
        'Ahşap çapa çözülürken yatay vuruş alanları genişler; çim kalkınca heykel mavi taş toplamaya başlar.',
        'Heykelin 8 mavi taşından sonra üç special isabeti ve ortak hedefte 12 elması ayrıca toplama işi kalır.')
    b.put(3,0,68)
    b.d['custom']['tarpStatues']=[dict(originCellIndex=3,color=2,count=8,size=2)]
    contour={(x,y) for y in range(2,8) for x in (3,4)}
    contour|={(x,4) for x in range(1,7)}
    contour|={(x,7) for x in (0,1,6,7)}
    contour|={(x,8) for x in range(8)}
    contour|={(x,9) for x in (2,3,4,5)}|{(3,10),(4,10)}
    for x,y in sorted(contour,key=lambda p:(p[1],p[0])): b.put(x,y,4)
    b.put(0,9,85).put(6,9,94)
    for y in (0,1):
        for x in (3,4): b.overlay(x,y,42)
    b.diamonds(12,96).goal(4,len(contour)).goal(68,1).goal(42,4)
    b.d.update(previewRemoved=[42,4],openedDimerdals=True,
        budget='34 tek vuruş ahşap, örtü sonrası 8 mavi + 3 special heykel işi, 12 elmas. 24 hamle ilk tahmin.',
        alternatives=['Çapa biçimli dış maske: alt uçların eşleşme alanını gereksiz küçültürdü.',
                      'Üç vuruş oyma çapa: normal 224’ü sadece yüksek vuruş yüküyle Hard’a yaklaştırırdı.'])
    result.append(b.d)

    b=Board(225,'Mühürlü Kristal Kalesi','Sealed Crystal Keep','Muhurlu-Kristal-Kalesi',['oooooooo']*11,25,
        'Geniş tahtada iki 2×4 pusula suru, ortada 4×4 mühürlü avlu, altta bitişik yeşil ve sarı kristal kapıları.',
        'Üst çalışma alanında mührün kırmızı/sarı/yeşil sırasını ilerletirken surlara ulaşacak special çizgilerini sakla.',
        'Mühür açılınca 16 hücrelik çamur avlusu oynanır olur; pusula surlarını yalnız special açar ve yan alt ceplere besleme verir.',
        'Pusula surları ile iki kat çamuru temizlerken kapıları açıp 16 elması üret/topla; açılış tek başına hedefleri bitirmez.')
    b.rect(2,4,4,4,25).rect(0,5,2,4,39).rect(6,5,2,4,39).put(2,9,88).put(4,9,91)
    b.d['custom']['ancientSeals']=[dict(originCellIndex=34,width=4,height=4,startColor=1,
        redCount=9,yellowCount=9,greenCount=9,stackOrder=1)]
    b.diamonds(16,97).goal(64,1).goal(39,16).goal(25,16)
    b.d.update(previewRemoved=[64,39],openedDimerdals=True,
        budget='Hard: 27 sıralı renk toplama, 16 special-only sur hücresi, 16 iki kat çamur ve 16 elmas. 25 hamle ilk tahmin.',
        alternatives=['Dört renkli bütün Dimerdal’lar: hedef elmaslarını aşırı böler, fazladan kaynaklar ayrı karar üretmez.',
                      'Çiçek/çimle beşinci hedef: mühür önüne yeni kapak eklemek yerine dört anlamlı iş korundu.'])
    result.append(b.d)
    return result

def layers(d,removed=()):
    removed=set(removed); w=d['width']; out=[[] for _ in d['data']['cells']]
    for i,o in enumerate(d['data']['obstacles']):
        if o and o not in removed: out[i].append((o,d['data']['obstacleOrigins'][i]))
    events=[(s['stackOrder'],s['originCellIndex'],s['obstacleId'],*footprint(s['obstacleId'])) for s in d['custom']['stackedObstacles']]
    events += [(s['stackOrder'],s['originCellIndex'],64,s['width'],s['height']) for s in d['custom']['ancientSeals']]
    for order,origin,o,fw,fh in sorted(events):
        if o in removed: continue
        for dy in range(fh):
            for dx in range(fw): out[origin+dy*w+dx].append((o,origin))
    return out

def feed(d,removed=()):
    """Static downward/diagonal reachability and authored shadow seed rules; no falling simulation."""
    ls=layers(d,removed); w,h=d['width'],d['height']; active={i for i,v in enumerate(d['data']['cells']) if v}
    blockers={i:[o for o,_ in cell if stage(o,'blocksCells') or stage(o,'behavior')==3] for i,cell in enumerate(ls)}
    blocked={i for i in active if blockers[i]};slots=active-blocked;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in blocked: break
            if i in slots: reached.add(i)
    def corner(i): return i not in blocked or all(stage(o,'allowDiagonal') for o in blockers[i])
    q=deque(reached)
    while q:
        i=q.popleft();x,y=i%w,i//w
        if y+1==h:continue
        for dx in (0,-1,1):
            j=i+w+dx
            if not 0<=x+dx<w or j not in slots or j in reached:continue
            if dx and not(corner(i+dx) or corner(i+w)):continue
            reached.add(j);q.append(j)
    seeded=set()
    for i in slots-reached:
        above=[o for j in range(i%w,i,w) for o in blockers[j]]
        if above and all(re.search(r'^    fillsShadowBeneath: 1$',DEFS[o],re.M) for o in above): seeded.add(i)
        elif any(stage(o,'locksInteraction') for o,_ in ls[i]):seeded.add(i)
    return slots,reached,seeded

def write():
    assert not (HERE/'active-revision.json').exists(), 'A later scoped revision is active; use its author instead of rewriting approved levels.'
    ds=designs(); previous=json.loads((HERE/'designs.json').read_text()) if (HERE/'designs.json').exists() else []
    owned=json.loads((HERE/'owned-hashes.json').read_text()) if (HERE/'owned-hashes.json').exists() else {}
    for rel,digest in owned.items():assert sha(ROOT/rel)==digest,('Concurrent edit, refusing overwrite',rel)
    if not previous:
        assert [int(n) for n in re.findall(r'^    level: (\d+)$',CAT.read_text(),re.M)]==list(range(1,222))
        assert not any((ROOT/d['asset']).exists() for d in ds)
    cat=(HERE/'Before'/CAT.name).read_text(); names=json.loads((HERE/'Before'/NAMES.name).read_text())
    for d in ds:
        old=next((p for p in previous if p['level']==d['level']),None)
        d['guid']=old['guid'] if old else uuid.uuid4().hex
        text=(PRO/'LevelV1_00002210.asset').read_text()
        for k,v in dict(m_Name=Path(d['asset']).stem,width=d['width'],height=d['height'],moves=d['moves'],levelKind=0,
                        refillSpecialGuard=.75,oilSpreadMaxCells=0,movableSpawnFallbackMoves=0,usesCustomIntro=0).items():text=scalar(text,k,v)
        for k,v in d['data'].items():text=scalar(text,k,encode(v))
        for k in CUSTOM:text=entries(text,k,d['custom'][k])
        for k in ('tutorialSteps','bossWaves'):text=entries(text,k,[])
        text=entries(text,'goals',d['goals']);text=scalar(text,'movableSpawnColumns','00'*d['width'])
        p=ROOT/d['asset'];p.write_text(text)
        if not old:Path(str(p)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {d["guid"]}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
        cat+=f'  - chapter: 9\n    level: {d["level"]}\n    levelKey: {d["levelKey"]}\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n'
        names['entries'].append(dict(level=d['level'],levelKey=d['levelKey'],name=d['name'],nameEn=d['nameEn']))
        slots,reached,seeded=feed(d)
        d['openingGeometry']=dict(tileSlots=len(slots),naturallyFed=len(reached),shadowSeeded=len(seeded),
            intentionallyEmpty=sorted(slots-reached-seeded),authoredPins=0)
    CAT.write_text(cat);NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    (HERE/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    paths=[CAT,NAMES]+[ROOT/d['asset'] for d in ds]+[Path(str(ROOT/d['asset'])+'.meta') for d in ds]
    (HERE/'owned-hashes.json').write_text(json.dumps({str(p.relative_to(ROOT)):sha(p) for p in paths},indent=2)+'\n')
    for d in ds:print(d['level'],d['name'],d['moves'],'moves',sum(d['data']['cells']),'active',d['openingGeometry'])

if __name__=='__main__':write()
