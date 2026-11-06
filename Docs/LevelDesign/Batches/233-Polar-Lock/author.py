"""Only V1 level233. Authoring and static topology; no game simulation or build."""
from pathlib import Path
from collections import deque
import hashlib, importlib.util, json, re, shutil, sys, uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
A = ROOT / 'Assets/_Project'
PRO = A / 'Settings/ProLevels'
CAT = PRO / 'LevelCatalogProV1.asset'
NAMES = A / 'Resources/LevelNames.json'
ASSET = PRO / 'LevelV1_00002330.asset'
TEMPLATE = PRO / 'LevelV1_00002320.asset'
sys.path.insert(0, str(HERE.parent / '226-230-Lantern-Wind-Crystal'))
import design as shared
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()


def layers(d, removed=(), magnet_interval=None):
    removed = set(removed)
    w = d['width']
    result = [[(o, d['data']['obstacleOrigins'][i])] if o and o not in removed else []
              for i, o in enumerate(d['data']['obstacles'])]
    if 29 not in removed:
        for entry in d['custom']['magnets']:
            path = entry['pathCellIndices']
            a, b = magnet_interval if magnet_interval else (0, len(path))
            for cell in path[a:b]:
                result[cell].append((29, path[0]))
    events = [(s.get('stackOrder',0),s['originCellIndex'],s['obstacleId'],*shared.footprint(s['obstacleId']))
              for s in d['custom'].get('stackedObstacles',[])]
    events += [(s.get('stackOrder',0),s['originCellIndex'],o,s['width'],s['height'])
               for o,key in [(33,'safes'),(64,'ancientSeals')] for s in d['custom'].get(key,[])]
    for order,origin,o,fw,fh in sorted(events):
        if o in removed: continue
        for dy in range(fh):
            for dx in range(fw): result[origin+dy*w+dx].append((o,origin))
    return result


def feed(d, removed=(), magnet_interval=None):
    """Reachability graph only. Explicit Safe blocking matches the runtime override."""
    ls = layers(d, removed, magnet_interval)
    w, h = d['width'], d['height']
    active = {i for i, v in enumerate(d['data']['cells']) if v}
    blockers = {i: [o for o, _ in ls[i] if o in (29,33) or shared.stage(o,'blocksCells')
                   or shared.stage(o,'holdsTile')] for i in active}
    blocked = {i for i in active if blockers[i]}
    movable = {i for i in active if any(shared.stage(o,'behavior') == 3 for o,_ in ls[i])}
    slots = active - blocked - movable
    reached = set()
    for x in range(w):
        for y in range(h):
            i = y*w+x
            if i in blocked: break
            if i in active: reached.add(i)
    def corner(i):
        return i in active and (i not in blocked or all(shared.stage(o,'allowDiagonal') for o in blockers[i]))
    queue = deque(reached)
    while queue:
        i = queue.popleft()
        x, y = i%w, i//w
        if y+1 == h: continue
        for dx in (0,-1,1):
            j = i+w+dx
            if not 0 <= x+dx < w or j not in active or j in blocked or j in reached: continue
            if dx and not (corner(i+dx) or corner(i+w)): continue
            reached.add(j)
            queue.append(j)
    return slots, reached & slots


def make():
    b = shared.Board(233, 'Kutup Kilidi', 'Polar Lock', 'Kutup-Kilidi',
        ['.oooooo.'] + ['oooooooo']*8 + ['.oooooo.']*2, 27,
        'Kilit biçimli tek gövde: üstte çalışma avlusu, altta bitişik iki renkli kasa, yanlarda porselen çerçeve, dipte U biçimli mıknatıs ve pusulalı çıkış bandı.',
        'Kırmızıyla başlayan sol kasayı mı, yeşille başlayan sağ kasayı mı ilerleteceksin; yoksa iki alt mıknatıs ucunu içeri taşıyıp çıkışları mı açacaksın?',
        'Her kasa dört kargoyu açar. Aynı dört sütunun altında special-only pusulalar ve mıknatıs hattı kalır; kasaları açmak teslimatı bitirmez.',
        'Sekiz kargoyu düz sütunlarından alta indir; sekiz pusulayı special ile temizle, mıknatıs çiftini buluştur ve on iki porseleni topla.')
    b.rect(2,6,4,2,34)
    b.d['custom']['safes'].append(dict(originCellIndex=50,width=2,height=2,
        redHits=2,yellowHits=1,greenHits=1,lockHitMode=0,
        firstLock=0,secondLock=1,thirdLock=2,stackOrder=1))
    b.d['custom']['safes'].append(dict(originCellIndex=52,width=2,height=2,
        redHits=1,yellowHits=1,greenHits=2,lockHitMode=0,
        firstLock=2,secondLock=1,thirdLock=0,stackOrder=1))
    for x in (0,7): b.rect(x,3,1,6,30)
    b.rect(2,8,4,2,39)
    path = [y*8+1 for y in range(7,11)] + [10*8+x for x in range(2,7)] + [y*8+6 for y in range(9,6,-1)]
    b.d['custom']['magnets'].append(dict(pathCellIndices=path))
    b.goal(34,8).goal(33,2).goal(29,1).goal(39,8).goal(30,12)
    d = b.d
    d.update(difficulty='Normal', refillSpecialGuard=-1, previewRemoved=[33], previewStage='KASA KALKINCA / İÇERİK KESİTİ',
        mechanics=['Safe / Cargo','Magnet','SculptingSpecial','HelmetPorcelain'],
        budget='27 hamle ilk ayar. İki kasa karşıt sırada2+1+1renkli hit;12 hücrelik mıknatıs yolunda11 uç ilerlemesi;8 special-only çıkış hücresi;12 tek-hit porselen ve8 gerçek kargo teslimatı. Hamleler çoklu vuruşlarla ortak ilerletilebilir; denge ölçülmedi.',
        alternatives=[
            'Üç renk Dimerdal veya yeni elma adası:231–232 akışını tekrar edecekti; iki aile de dinlendirildi.',
            'Jel altında yoğun oyma + alan temizleyen yardımcı:229da reddedilen erken bitiş ilişkisini tekrar ederdi.',
            'İki karşılıklı üretici kanadı: farklı ilk karar yerine aynı koridor şablonunu korurdu.',
            'Uzun dış çevre mıknatısı: uç ilerleme yükü gereksiz uzardı;yalnız alt12hücre seçildi.'],
        userConstraints=['Çim,GrassFlower,GrassWaterLily yok.','Önceki levellar ve kullanıcı232değişiklikleri korunur.'],
        illustration='Gerçek proje sprite’ları; normal taş renkleri temsili. Sağ görünüm yalnız örtü kesitidir; düşüş/oynanmış an değildir.')
    slots, reached = feed(d)
    d['openingGeometry'] = dict(activeCells=sum(d['data']['cells']),tileSlots=len(slots),naturallyFed=len(reached),
        authoredPins=0,intentionallyEmpty=sorted(slots-reached))
    assert slots == reached, slots-reached
    return d


def write():
    baseline = json.loads((HERE/'protected-before.json').read_text())
    owned_path = HERE/'owned-hashes.json'
    owned = json.loads(owned_path.read_text()) if owned_path.exists() else {}
    for p,h in owned.items(): assert sha(ROOT/p) == h, ('Concurrent edit; stop',p)
    allowed = {str(p.relative_to(ROOT)) for p in (CAT,NAMES)}
    external = json.loads((HERE/'external-edits.json').read_text())['files'] if (HERE/'external-edits.json').exists() else {}
    assert not [p for p,h in baseline.items() if p not in allowed and sha(ROOT/p) != external.get(p,h)], 'Unrecorded external asset drift'
    levels = list(map(int,re.findall(r'^    level: (\d+)$',CAT.read_text(),re.M)))
    assert levels == list(range(1,234 if owned else 233))
    if not owned: assert not ASSET.exists()
    d = make()
    guid = re.search(r'^guid: (\w+)$',Path(str(ASSET)+'.meta').read_text(),re.M)[1] if owned else uuid.uuid4().hex
    d['guid'] = guid
    t = (HERE/'Before'/TEMPLATE.relative_to(ROOT)).read_text()
    for k,v in dict(m_Name=ASSET.stem,width=8,height=11,moves=d['moves'],levelKind=0,usesCustomIntro=0,
        hamsterSatietyOverride=0,movableSpawnFallbackMoves=0,movableSpawnFillColumns=1,refillSpecialGuard=-1).items():
        t = shared.scalar(t,k,v)
    for k,v in d['data'].items(): t = shared.scalar(t,k,shared.encode(v))
    for k in list(shared.CUSTOM)+['fruits','bossWaves','tutorialSteps']:
        rows = d['custom'].get(k,[])
        if k == 'magnets': rows = [dict(pathCellIndices=shared.encode(e['pathCellIndices'])) for e in rows]
        t = shared.entries(t,k,rows)
    t = shared.entries(t,'goals',d['goals'])
    t = shared.scalar(t,'movableSpawnColumns','00'*8)
    ASSET.write_text(t)
    Path(str(ASSET)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    cat = (HERE/'Before'/CAT.relative_to(ROOT)).read_text()
    CAT.write_text(cat + f'  - chapter: 10\n    level: 233\n    levelKey: LevelCL_0233\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n')
    names = json.loads((HERE/'Before'/NAMES.relative_to(ROOT)).read_text())
    assert not any(e['level']==233 for e in names['entries'])
    names['entries'].append(dict(level=233,levelKey=d['levelKey'],name=d['name'],nameEn=d['nameEn']))
    NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    (HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    owned_path.write_text(json.dumps({str(p.relative_to(ROOT)):sha(p) for p in (CAT,NAMES,ASSET,Path(str(ASSET)+'.meta'))},indent=2)+'\n')
    print(d['name'],d['openingGeometry'])


if __name__ == '__main__': write()
