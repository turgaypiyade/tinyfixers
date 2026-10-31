"""Fill V1 slots 12–15 only; preserve existing 11 and all other levels. No gameplay execution."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re
import struct
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
LEVELS = ROOT / 'Assets/_Project/Settings/ProLevels'
CATALOG = LEVELS / 'LevelCatalogProV1.asset'
NAMES = ROOT / 'Assets/_Project/Resources/LevelNames.json'
DOCS = ROOT / 'Docs/LevelDesign'
assert not (HERE / 'protected-before.json').exists(), 'Already authored; use verify.py / render_preview.py.'
catalog_before = CATALOG.read_text()
template_path = LEVELS / 'LevelV1_00000110.asset'
template = template_path.read_text()
names_before = json.loads(NAMES.read_text())
assert not any(e['level'] in (12, 13, 14, 15) for e in names_before['entries'])
for n in range(12, 16):
    assert not (LEVELS / f'LevelV1_{n*10:08d}.asset').exists()
    assert re.search(r'    level: '+str(n)+r'\n    levelKey: LevelCL_0'+str(n)+r'\n    levelData: \{fileID: 0\}', catalog_before)
before = HERE / 'Before'
before.mkdir()
for path in (CATALOG, NAMES, template_path, DOCS/'Level-Names.md', DOCS/'Design-State.md'):
    (before / path.name).write_bytes(path.read_bytes())
protected = {}
for directory in ('Settings', 'Scripts', 'Resources', 'Scenes', 'Prefabs', 'Shaders'):
    for path in (ROOT/'Assets/_Project'/directory).rglob('*'):
        if path.is_file() and path not in (CATALOG, NAMES) and path.suffix in ('.asset', '.meta', '.cs', '.json', '.unity', '.prefab', '.shader'):
            protected[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
(HERE/'protected-before.json').write_text(json.dumps(protected, indent=2)+'\n')


def design(n, title, en, moves, decision, flow):
    return dict(n=n, title=title, nameEn=en, difficulty='Zor' if n == 15 else 'Normal',
                targetPassRate='35–55%' if n == 15 else '55–85%', w=9, h=9, moves=moves,
                decision=decision, flow=flow, stacks=[], tarps=[], tubes=[], walls=[], oilCap=0,
                data={k: [v]*81 for k,v in [('cells',1), ('obstacles',0), ('obstacleOrigins',-1),
                                           ('wallPieceIds',0), ('hamsterSatietyByCell',0),
                                           ('pinnedTileTypes',0), ('pinnedSpecialTypes',0)]})


def put(d, x, y, oid):
    i=y*9+x
    assert d['data']['obstacles'][i] == 0, (d['n'], x, y)
    d['data']['obstacles'][i]=oid
    d['data']['obstacleOrigins'][i]=i


def covered(d, x, y, oid):
    put(d,x,y,25)
    d['stacks'].append(dict(originCellIndex=y*9+x, obstacleId=oid, stackOrder=0, initialRemainingHits=0))


d12=design(12,'Açık Avlu','Open Courtyard',29,
           'Üstte geniş alanda eşleş; alt avlunun çamurunu temizlerken onu çevreleyen tek-hit sandık konturunu aç.',
           ['Üst dört sıra tamamen açık; yağ baskısı yok.',
            'Alt U konturu tek vuruşlu sandık; çamurun üstünde örtü yok.',
            'Ortadaki 7×3 çamurlu alan doğrudan taş alır.'])
for y in range(4,9):
    for x in (0,8):put(d12,x,y,4)
for x in range(1,8):put(d12,x,8,4)
for y in range(5,8):
    for x in range(1,8):put(d12,x,y,25)

d13=design(13,'Katmanlı Geçit','Layered Passage',30,
           'Sol ve sağ çalışma alanları arasındaki iki-hit sandık bölmesini aç; altındaki çamurla sağdaki açık çamuru birlikte temizle.',
           ['Üst iki sıra ve iki yandaki çalışma alanları açık.',
            'İki-hit dikey bölme, tek-hit alt kenarla tek bir L oluşturur.',
            '12 sandığın altı çamur; 9 çamur açılıştan doğrudan erişilebilir.'])
for y in range(2,9):covered(d13,3,y,5)
for x in range(4,9):covered(d13,x,8,4)
for y in range(5,8):
    for x in range(4,7):put(d13,x,y,25)

d14=design(14,'Yağlı Köşe','Oily Corner',28,
           'Geniş sol alanda eşleş; sağ alt yağ cebinin ince sandık kapağını açıp yağa düzenli vurarak yayılmayı durdur.',
           ['İki-hit alt sıra ve ince tek-hit dirsek tek bağlı kontur.',
            'Sağ altta yalnız 8 başlangıç yağı; ilk kapağı açana kadar çevresi sandık.',
            'Bir hamlede herhangi bir yağa isabet yayılmayı durdurur; toplam yağ tavanı 14.'])
d14['oilCap']=14
for x in range(9):put(d14,x,8,5)
for y in range(3,8):put(d14,6,y,4)
for x in (7,8):put(d14,x,3,4)
for y in range(4,8):
    for x in (7,8):put(d14,x,y,14)

d15=design(15,'Üç Katlı Bent','Three-Tier Dam',29,
           'Üstteki altı sırada special üret; alt bentte 1→2→3 vuruşlu sandık sıralarını açarken her sıranın altındaki çamuru da bitir.',
           ['54 hücrelik kesintisiz üst çalışma alanı.',
            'Önden arkaya 9 tek-hit, 9 iki-hit ve 9 üç-hit sandık.',
            'Bütün 27 sandık altında çamur var; zorluk yükten gelir, dar boğazdan değil.'])
for y,oid in ((6,4),(7,5),(8,6)):
    for x in range(9):covered(d15,x,y,oid)

designs=[d12,d13,d14,d15]
for d in designs:
    counts=Counter(o for o in d['data']['obstacles'] if o)
    counts.update(s['obstacleId'] for s in d['stacks'])
    # Front-facing covers before the underlayer in the goal strip.
    order=[o for o in (4,5,6,14,25) if counts[o]]
    d['goals']=[dict(targetType=1,tileType=0,obstacleId=o,collectibleId=1,
                     iconOverride='{fileID: 0}',amount=counts[o]) for o in order]
    d['guid']=uuid.uuid4().hex
    d['asset']=f'LevelV1_{d["n"]*10:08d}'
    text=template

    def scalar(key,value):
        global text
        line=f'  {key}: {value}'
        if re.search(r'^  '+key+r':',text,re.M):
            text=re.sub(r'^  '+key+r':[^\n]*',lambda _:line,text,flags=re.M)
        else:text+=line+'\n'

    def entries(key,values):
        global text
        lines=f'  {key}: []\n' if not values else f'  {key}:\n'+''.join(
            ''.join(('  - ' if i==0 else '    ')+k+': '+str(v)+'\n' for i,(k,v) in enumerate(row.items())) for row in values)
        pattern=r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*'
        if re.search(pattern,text,re.M):text=re.sub(pattern,lambda _:lines,text,flags=re.M)
        else:text+=lines

    for key,value in dict(m_Name=d['asset'],width=9,height=9,moves=d['moves'],levelKind=0,
                          baseCoinReward=10,oilSpreadMaxCells=d['oilCap'],refillSpecialGuard=-1,
                          randomPool=struct.pack('<4i',0,1,2,3).hex()).items():scalar(key,value)
    for key,values in d['data'].items():scalar(key,struct.pack('<81i',*values).hex())
    entries('goals',d['goals']);entries('stackedObstacles',d['stacks'])
    for key in ('tutorialSteps','tubes','magnets','waterChannels','safes','ancientSeals','tarpStatues','paintCanBoxColors','colorCrateInitialHits','bossWaves'):
        entries(key,[])
    path=LEVELS/(d['asset']+'.asset')
    path.write_text(text)
    Path(str(path)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {d["guid"]}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    pattern=r'(    level: '+str(d['n'])+r'\n    levelKey: LevelCL_0'+str(d['n'])+r'\n    levelData: )\{fileID: 0\}'
    catalog_before,count=re.subn(pattern,lambda m:m[1]+f'{{fileID: 11400000, guid: {d["guid"]}, type: 2}}',catalog_before)
    assert count==1
CATALOG.write_text(catalog_before)
names=json.loads(NAMES.read_text())
for d in designs:
    assert not any(e['name']==d['title'] for e in names['entries'])
    row=dict(level=d['n'],levelKey=f'LevelCL_{d["n"]:03d}',name=d['title'],nameEn=d['nameEn'])
    index=next((i for i,e in enumerate(names['entries']) if e['level']>d['n']),len(names['entries']))
    names['entries'].insert(index,row)
NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
path=DOCS/'Level-Names.md';note=path.read_text().replace('4, 8, 12–15, 71','4, 8, 71')
note=note.replace('| 11 | Yağlı Rampa |\n','| 11 | Yağlı Rampa |\n'+''.join(f'| {d["n"]} | {d["title"]} |\n' for d in designs))
path.write_text(note)
(HERE/'designs.json').write_text(json.dumps(designs,ensure_ascii=False,indent=2)+'\n')
assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in protected.items())
print('Created V1 12–15 and filled their catalog/name entries; existing 11 and',len(protected),'existing assets/code files preserved.')
