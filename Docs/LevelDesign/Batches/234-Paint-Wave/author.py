"""Only234: concept first, scoped asset authoring and static feed analysis."""
from pathlib import Path
import hashlib, importlib.util, json, re, uuid

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
A=ROOT/'Assets/_Project'
PRO=A/'Settings/ProLevels'
CAT=PRO/'LevelCatalogProV1.asset'
NAMES=A/'Resources/LevelNames.json'
ASSET=PRO/'LevelV1_00002340.asset'
TEMPLATE=PRO/'LevelV1_00002330.asset'
spec=importlib.util.spec_from_file_location('polar_topology',HERE.parent/'233-Polar-Lock/author.py')
topology=importlib.util.module_from_spec(spec);spec.loader.exec_module(topology)
shared=topology.shared
layers=topology.layers
feed=topology.feed
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()


def make():
    b=shared.Board(234,'Boya Dalgası','Paint Wave','Boya-Dalgasi',[
        'oooooo..','ooooooo.','oooooooo','oooooooo','oooooooo',
        'oooooo..','ooooo...','ooooo...','oooooo..','ooooooo.','oooooooo'],24,
        'Sağ kenarı dalga gibi içeri girip yeniden açılan tek tahta; sol kıyıda renkli kapaklar ve altta mavi/kırmızı hareketli bantlar.',
        'Sol renkli sandıkları açıp boya işini mi başlatacaksın,yoksa mavi/kırmızı kenarı indirerek alt çalışma alanını mı genişleteceksin?',
        'İki renkli sandığın her biri altında dokuz kovalı bir boya kasası saklar. Kapak kırılması boyayı bitirmez;aynı kenarda ikinci iş başlar.',
        'İki ColorChest,iki PaintCanBox vealtışar mavi/kırmızı plastik tamamlanır. Normal vuruşlar renk seçer;specialiki boya kovası düşürür.')
    for y in (0,2):b.put(0,y,63).overlay(0,y,21)
    b.rect(0,5,1,6,11).rect(1,5,1,6,12)
    b.goal(21,2).goal(63,2).goal(11,6).goal(12,6)
    d=b.d
    slots,reach=feed(d)
    assert slots-reach=={32},slots-reach
    d['data']['pinnedTileTypes'][32]=1  # Gear: ordinary tile, never a prepared special.
    d.update(difficulty='Normal',refillSpecialGuard=-1,previewRemoved=[21],
        previewStage='RENKLİ KAPAKLAR KALKINCA',
        mechanics=['ColorChest → PaintCanBox','Plastic_Blue / Plastic_Red'],
        pinReason='Yalnız açılışta doğal dolum ulaşmayan(0,4)normal yuvasına Gear taşı. Altındaki kesintisiz mavi plastik sütunu başlangıç desteğidir;diğer53normal yuva otomatik dolar.',
        openingGeometry=dict(activeCells=sum(d['data']['cells']),normalSlots=len(slots),naturallyFed=len(reach),
            shadowPins=[32],specialPins=0,intentionallyEmpty=[]),
        budget='234 Normal yükseliş:24hamle ilkayar;iki kapağın5er hitinden sonra2×9kova ayrı iş. Mavi/kırmızı sütunlar12tek-hitmovable;normalmatchkendi rengini ister. Geniş çalışma alanı multi-hit special kurmayı destekler;dengeölçülmedi.',
        alternatives=[
            '176daki V/çanak veya233teki altU: yaklaşım korunur ama kompozisyon tekrarlanırdı.',
            'Bir Dimerdal/elma adası daha:231–232nin kaynak-ürün/ayrıada kararını yinelerdi.',
            'Tam yatay üst çatı:birçok alt yuvayı kalıcı dolumdan koparırdı. Kapaklar yalnız iki kenar sütunuyla sınırlandı.',
            'Tüm taşları elle döşemek:son açık kullanıcı kuralı yalnız doğal dolum ulaşmayan açılış hücreleri içindir.'],
        illustration='Gerçek proje sprite’ları;serbest normal renkler temsili. Altın nokta tek gerçek başlangıç pinidir. Sağ kesit yalnız üstkatmanı kaldırır;düşmüş plastik veya oynanmış an değildir.')
    return d


def write():
    assert not (HERE/'active-revision.json').exists(), 'A later234revision is active;use its scoped author.'
    baseline=json.loads((HERE/'protected-before.json').read_text())
    ownedpath=HERE/'owned-hashes.json'
    owned=json.loads(ownedpath.read_text())if ownedpath.exists()else{}
    for rel,h in owned.items():assert sha(ROOT/rel)==h,('Concurrent edit;stop',rel)
    allowed={str(p.relative_to(ROOT))for p in(CAT,NAMES)}
    drift=[rel for rel,h in baseline.items()if rel not in allowed and sha(ROOT/rel)!=h]
    assert not drift,('Unrelated user edit;preserve and recheck',drift)
    numbers=list(map(int,re.findall(r'^    level: (\d+)$',CAT.read_text(),re.M)))
    assert numbers == list(range(1, 235 if owned else 234))
    if not owned:assert not ASSET.exists()
    d=make();guid=re.search(r'^guid: (\w+)$',Path(str(ASSET)+'.meta').read_text(),re.M)[1]if owned else uuid.uuid4().hex
    d['guid']=guid
    t=(HERE/'Before'/TEMPLATE.relative_to(ROOT)).read_text()
    for k,v in dict(m_Name=ASSET.stem,width=8,height=11,moves=24,levelKind=0,usesCustomIntro=0,
        hamsterSatietyOverride=0,movableSpawnFallbackMoves=0,movableSpawnFillColumns=1,refillSpecialGuard=-1).items():
        t=shared.scalar(t,k,v)
    for k,v in d['data'].items():t=shared.scalar(t,k,shared.encode(v))
    for k in list(shared.CUSTOM)+['fruits','bossWaves','tutorialSteps']:t=shared.entries(t,k,d['custom'].get(k,[]))
    t=shared.entries(t,'goals',d['goals']);t=shared.scalar(t,'movableSpawnColumns','00'*8)
    ASSET.write_text(t)
    Path(str(ASSET)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    cat=(HERE/'Before'/CAT.relative_to(ROOT)).read_text()
    CAT.write_text(cat+f'  - chapter: 10\n    level: 234\n    levelKey: LevelCL_0234\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n')
    names=json.loads((HERE/'Before'/NAMES.relative_to(ROOT)).read_text());assert not any(e['level'] == 234 for e in names['entries'])
    names['entries'].append(dict(level=234,levelKey=d['levelKey'],name=d['name'],nameEn=d['nameEn']))
    NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    (HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    ownedpath.write_text(json.dumps({str(p.relative_to(ROOT)):sha(p)for p in(CAT,NAMES,ASSET,Path(str(ASSET)+'.meta'))},indent=2)+'\n')
    print(d['name'],d['openingGeometry'])


if __name__=='__main__':write()
