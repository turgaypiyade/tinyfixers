"""Only232 authoring. No Unity, builds, playback, simulation or publishing."""
from pathlib import Path
import importlib.util,sys,json,re,hashlib,uuid,shutil
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];A=ROOT/'Assets/_Project';PRO=A/'Settings/ProLevels'
CAT=PRO/'LevelCatalogProV1.asset';NAMES=A/'Resources/LevelNames.json';ASSET=PRO/'LevelV1_00002320.asset';TEMPLATE=PRO/'LevelV1_00002310.asset'
sys.path.insert(0,str(HERE.parent/'226-230-Lantern-Wind-Crystal'))
import design as shared
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def make():
    b=shared.Board(232,'Kristal Hilal','Crystal Crescent','Kristal-Hilal',[
        '..ooooo..','.ooooooo.','oooooo...','oooooo.oo','oooooo.oo',
        'oooooo...','oooooo...','oooooo...','ooooooo..','oooooooo.','..oooooo.'],28,
        'Hilal biçimli ana tahta: dış kenarda mavi/yeşil/kırmızı kristal omurga, iç yayda çiçekli çim bahçesi. Açıklıktaki2×2elma ayrı bir ada.',
        'İç yaydaki çiçekleri açıp dört hamsteri mi serbest bırakacaksın, dış kenardaki renkli kepenkleri aynı special hattında mı ilerleteceksin?',
        'Çiçek→çim→hamster sırası açılır. Hamsterler dörder lokmadan sonra zıplayıp yardım eder; yuvaları normal taş alanına katılır. Alt yumurta kutusu iki elmayı besler.',
        'Üç kaynak kendi6şar elmasını üretip bitirir;18elmasın ayrıca toplanması gerekir. Ada elması dahil iki meyve yalnız sineklerin altışar lokmasıyla tamamlanır.')
    for y,oid in [(4,85),(6,88),(8,94)]:b.source(0,y,oid,6)
    b.put(4,9,100)
    b.d['custom']['fruits']=[]
    for x,y in [(7,3),(6,9)]:
        origin=y*9+x
        for yy in range(y,y+2):
            for xx in range(x,x+2):
                i=yy*9+xx;assert b.d['data']['cells'][i] and not b.d['data']['obstacles'][i]
                b.d['data']['obstacles'][i]=102;b.d['data']['obstacleOrigins'][i]=origin
        b.d['custom']['fruits'].append(dict(originCellIndex=origin,width=2,height=2))
    b.rect(4,4,2,2,57)
    for y in range(3,6):
        for x in (4,5):b.overlay(x,y,58)
    for y in (4,5):
        for x in (4,5):b.d['data']['hamsterSatietyByCell'][y*9+x]=4
    b.diamonds(18,96).goal(102,2).goal(57,4).goal(58,6).goal(42,6)
    d=b.finish([58],
        '232 Normal:231tanıtımından sonraki artış. Üç kaynakta4açılış+6üretim;4specialyumurta salımı;4hamster×4lokma;6çiçek+6çim. Ortak special hatları ve hamster yardımıyla28hamle ilk ayar;oynanmış denge değildir.',
        ['Piyano gibi düz bir üretici duvarı ve yanında ada:231in iki bölge okumasına fazla yaklaşırdı.',
         'Elmayı kapalı bir çemberin içine almak: geniş çalışma alanı ve dolum ilişkisinden fazla götürürdü.',
         'Jel/pres veya çim altında sandık: bu fikirde katman sonrası farklı hareket/yardım işi yerine önceki reddedilen etkileşimleri tekrar ederdi.'])
    d.update(previewStage='ÇİÇEK VE ÇİM KALKINCA',previewRemoved=[58],hamsterSatiety=4,
        flowerCells=[y*9+x for y in range(3,6) for x in (4,5)],
        mechanics=['Dimerdal','FlyFruit','Hamster','GrassFlower / Grass'],
        illustration='Renkler temsili. Sağ kesit katmanları kaldırır;düşüş/sıçrama/oynanmış pozisyon göstermez.')
    return d

def write():
    snapshot=HERE/'protected-before.json'
    if not snapshot.exists():
        for p in [CAT,NAMES,TEMPLATE,Path(str(TEMPLATE)+'.meta')]:
            q=HERE/'Before'/p.relative_to(ROOT);q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
        snapshot.write_text(json.dumps({str(p.relative_to(ROOT)):sha(p) for p in A.rglob('*') if p.is_file()},indent=2)+'\n')
    owned=HERE/'owned-hashes.json';old=json.loads(owned.read_text()) if owned.exists() else {}
    for p,h in old.items():assert sha(ROOT/p)==h,('User edit; stop',p)
    baseline=json.loads(snapshot.read_text());allowed={str(p.relative_to(ROOT)) for p in [CAT,NAMES]}
    assert not[p for p,h in baseline.items() if p not in allowed and sha(ROOT/p)!=h],'Unrelated project drift; stop.'
    current=CAT.read_text();levels=list(map(int,re.findall(r'^    level: (\d+)$',current,re.M)))
    if not old:assert levels==list(range(1,232)) and not ASSET.exists()
    else:assert levels==list(range(1,233))
    d=make();guid=re.search(r'^guid: (\w+)$',Path(str(ASSET)+'.meta').read_text(),re.M)[1] if ASSET.exists() else uuid.uuid4().hex;d['guid']=guid
    t=(HERE/'Before'/TEMPLATE.relative_to(ROOT)).read_text()
    for k,v in dict(m_Name=ASSET.stem,width=9,height=11,moves=d['moves'],levelKind=0,usesCustomIntro=0,hamsterSatietyOverride=0,movableSpawnFallbackMoves=0,movableSpawnFillColumns=1).items():t=shared.scalar(t,k,v)
    for k,v in d['data'].items():t=shared.scalar(t,k,shared.encode(v))
    for k in list(shared.CUSTOM)+['fruits']:t=shared.entries(t,k,d['custom'].get(k,[]))
    for k in ['bossWaves','tutorialSteps']:t=shared.entries(t,k,[])
    t=shared.entries(t,'goals',d['goals']);t=shared.scalar(t,'movableSpawnColumns','00'*9)
    ASSET.write_text(t);Path(str(ASSET)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    cat=(HERE/'Before'/CAT.relative_to(ROOT)).read_text()+f'  - chapter: 10\n    level: 232\n    levelKey: LevelCL_0232\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n';CAT.write_text(cat)
    names=json.loads((HERE/'Before'/NAMES.relative_to(ROOT)).read_text());assert not any(e['level']==232 for e in names['entries']);names['entries'].append(dict(level=232,levelKey=d['levelKey'],name=d['name'],nameEn=d['nameEn']));NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    (HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    owned.write_text(json.dumps({str(p.relative_to(ROOT)):sha(p) for p in [CAT,NAMES,ASSET,Path(str(ASSET)+'.meta')]},indent=2)+'\n')
    print(d['name'],d['openingGeometry'])
if __name__=='__main__':write()
