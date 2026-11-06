"""Five authorized V1 levels. Authoring data only; never builds, simulates or publishes."""
from pathlib import Path
import hashlib,importlib.util,json,re,struct,sys,uuid
from collections import deque
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];A=ROOT/'Assets/_Project';PRO=A/'Settings/ProLevels'
CAT=PRO/'LevelCatalogProV1.asset';NAMES=A/'Resources/LevelNames.json'
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import DEFS,footprint,stage,array
spec=importlib.util.spec_from_file_location('crystal_author_base',HERE.parent/'222-225-Crystal-Shapes/design.py')
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
CUSTOM=base.CUSTOM
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def layers(d,removed=()):
    result=base.layers(d,removed)
    for entry in d['custom']['stackedObstacles']:
        if entry['obstacleId'] in (55,56) and entry.get('wallPieceOriginPlusOne',0)>0:
            cell=entry['originCellIndex'];origin=entry['wallPieceOriginPlusOne']-1
            result[cell]=[(o,origin if o==entry['obstacleId'] and old==cell else old) for o,old in result[cell]]
    return result
def feed(d,removed=()):
    """Static reachability from current BoardCellState/Cascade rules. Movable tiles
    occupy their initial cell but DO NOT block an entire spawn-connected column."""
    ls=layers(d,removed);w,h=d['width'],d['height'];active={i for i,v in enumerate(d['data']['cells']) if v}
    blockers={i:[o for o,_ in ls[i] if stage(o,'blocksCells') or stage(o,'holdsTile')] for i in active}
    blocked={i for i in active if blockers[i]};movable={i for i in active if any(stage(o,'behavior')==3 for o,_ in ls[i])}
    slots=active-blocked-movable;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in blocked:break
            if i in active:reached.add(i)
    def corner(i):return i in active and (i not in blocked or all(stage(o,'allowDiagonal') for o in blockers[i]))
    q=deque(reached)
    while q:
        i=q.popleft();x,y=i%w,i//w
        if y+1>=h:continue
        for dx in (0,-1,1):
            j=i+w+dx
            if not 0<=x+dx<w or j not in active or j in blocked or j in reached:continue
            if dx and not(corner(i+dx) or corner(i+w)):continue
            reached.add(j);q.append(j)
    seeded=set()
    for i in slots-reached:
        above=[o for j in range(i%w,i,w) for o in blockers.get(j,[])]
        if above and all(re.search(r'^    fillsShadowBeneath: 1$',DEFS[o],re.M) for o in above):seeded.add(i)
        elif any(stage(o,'locksInteraction') for o,_ in ls[i]):seeded.add(i)
    return slots,reached&slots,seeded

class Board(base.Board):
    def __init__(self,*args):
        super().__init__(*args)
        self.d.update(chapter=(self.d['level']-1)//25+1,walls=[],movableColumns=[],refill=[],previewRemoved=[],previewStage='',pinReason='',difficulty='Normal')
    def wall(self,x,y,w,h,pid):
        cells=[yy*self.d['width']+xx for yy in range(y,y+h) for xx in range(x,x+w)]
        origin=min(cells)
        for i in cells:
            assert self.d['data']['cells'][i] and not self.d['data']['obstacles'][i]
            self.d['data']['obstacles'][i]=55;self.d['data']['obstacleOrigins'][i]=origin;self.d['data']['wallPieceIds'][i]=pid
        self.d['walls'].append(dict(origin=origin,obstacleId=55,pieceId=pid,cells=cells))
        return self
    def lamp(self,pid,points,colors,shift=False,reverse=False):
        assert len(points)==len(colors)
        cells=[y*self.d['width']+x for x,y in points];origin=min(cells)
        for i,c in zip(cells,colors):
            assert self.d['data']['cells'][i] and not self.d['data']['obstacles'][i]
            self.d['data']['obstacles'][i]=79;self.d['data']['obstacleOrigins'][i]=origin
            self.d['data']['wallPieceIds'][i]=pid;self.d['data']['lampColorByCell'][i]=c
        self.d['custom']['lampStripSettings'].append(dict(pieceId=pid,shift=int(shift),reverse=int(reverse)))
        return self
    def source(self,x,y,oid,count):
        self.put(x,y,oid);self.d['custom']['dimerdals'].append(dict(originCellIndex=y*self.d['width']+x,obstacleId=oid,count=count));return self
    def collectible(self,id,count,icon):
        self.d['goals'].append(dict(targetType=2,tileType=0,obstacleId=0,collectibleId=id,iconOverride=icon,amount=count));return self
    def finish(self,preview,budget,alternatives):
        d=self.d;d.update(previewRemoved=preview,budget=budget,alternatives=alternatives)
        slots,reached,seeded=feed(d)
        # Honest authored starting stones, only where a static roof prevents initial
        # natural fill. They do not change the fall motor or guarantee a prepared match.
        empty=slots-reached-seeded
        if len(empty)>4:
            for i in sorted(empty):d['data']['pinnedTileTypes'][i]=(i%d['width']+2*(i//d['width']))%4+1
            d['pinReason']='Kapak/şerit gölgesindeki başlangıç normal taşları gerçekten assete pinlendi; hazır special veya zorunlu eşleşme kurulmadı.'
        d['openingGeometry']=dict(tileSlots=len(slots),naturallyFed=len(reached),shadowSeeded=len(seeded),
            authoredPins=sum(bool(v) for v in d['data']['pinnedTileTypes']),intentionallyEmpty=sorted(empty-{i for i,v in enumerate(d['data']['pinnedTileTypes']) if v}))
        return d

def designs():
    ds=[]
    b=Board(226,'Rüzgâr Kemeri','Windlit Arch','Ruzgar-Kemeri',
        ['ooooooooo']*3+['ooo..oooo']*4+['ooooooooo']*2+['.ooooooo.']*2,28,
        'İki açıklığı bağlayan asimetrik kemer; solda iki bitişik değirmen, sağda tek büyük duvar ve dört küçük kapağın altında fenerler.',
        'Geniş üst sıradan sağ duvarın bir hücresine yoğunlaş veya solda değirmenlere uzanan special hazırla.',
        'Dört küçük duvar kapağı kırıldıkça altlarındaki fenerler görünür. Sağdaki iki seçili sütun kalan16 feneri üretir; büyük duvar da orta çalışma yüzünü açar.',
        '20 feneri yakıp topla; değirmen çuvallarından gelen toplam16 unu temizle. Sol ve sağ işler aynı hamlede ilerletilebilir.')
    b.wall(5,0,2,6,1)
    for pid,(x,y) in enumerate([(7,0),(8,0),(7,1),(8,1)],2):
        b.put(x,y,75).overlay(x,y,55)
        origin=y*9+x;b.d['data']['wallPieceIds'][origin]=pid
        b.d['walls'].append(dict(origin=origin,obstacleId=55,pieceId=pid,cells=[origin]))
    b.put(0,5,80).put(0,7,80).goal(55,5).goal(75,20).goal(83,16)
    b.d.update(movableColumns=[7,8],refill=[dict(obstacleId=75,initial=4,goal=20,extra=16)],previewStage='DUVAR KAPAKLARI AÇILINCA',
        flourPerSack=8,reference='13/14 — katman açılır, seçili sütundan hareketli ürün gelir; maske ve parça yerleri yeniden kuruldu.')
    ds.append(b.finish([55], '226 rahatlama: bir büyük parça + dört fener kapağı, iki değirmen ve20 fener. Katmanlara erişim için28 hamle ilk ayar.',
        ['Referansı birebir uzun S olarak kopyalamak: iki kısa köprü ve üst ortak çalışma sırası seçildi.',
         'Bütün fener kotasını tek-hücre beş-hit duvarlarla örtmek: aşırı vuruş yükü olurdu; yalnız dört gerçek içerik kapağı ve bir büyük duvar parçası seçildi.']))

    b=Board(227,'Işık Tezgâhı','Lightwork Bench','Isik-Tezgahi',
        ['.oooooooo']+['ooooooooo']*8+['oooooooo.'],26,
        'İki köşesi kırpılmış tezgâh; iki-hit plastikten üst/sol L konturu, üst sağda dikey, alt solda yatay lamba rafları.',
        'Üst soldaki4×4 çalışma yüzünde rafın rengine uygun eşleşme hazırla; dikey rafı mı, yatay rafı mı önce bitireceğini seç.',
        'Bir lamba çerçevesi tamamlanınca üç hücre birlikte açılır. Oyma bloğunun üst ve sol yüzü açıldıkça alt sağa special ulaştırmak kolaylaşır.',
        'Beş çerçeveyi bitir, üç-hit12 oyma taşını ve17 iki-hit plastiği topla; iki kayan rafın boşluklarını zamanla.')
    for x in range(1,9):b.put(x,0,40)
    for y in range(1,10):b.put(0,y,40)
    b.lamp(1,[(5,y) for y in range(1,4)],[3,1,3])
    b.lamp(2,[(6,y) for y in range(1,4)],[2,4,2],True)
    b.lamp(3,[(7,y) for y in range(1,4)],[1,3,1])
    b.lamp(4,[(x,6) for x in range(1,4)],[4,2,4])
    b.lamp(5,[(x,8) for x in range(1,4)],[3,1,3],True,True)
    b.rect(6,5,3,4,28).goal(79,5).goal(28,12).goal(40,17)
    b.d.update(previewStage='LAMBA RAFLARI TAMAMLANINCA',reference='15 — ortogonal lamba rafları ve bitişik malzeme bloğu; çerçeve adedi/konumları/maske yeniden kuruldu.')
    ds.append(b.finish([79],'227 normal yükseliş: 15 renkli lamba beş ayrı çerçeveye bağlı; iki raf kayar. Plastik ve oyma bloğu farklı vuruş yükleri verir;26 hamle ilk ayar.',
        ['Dokuz dikey/yatay rafın tamamını kopyalamak: çalışma yüzünü fazla kapatırdı.',
         'Mor-only bütün lambalar: referansın renk eşleştirme kararını sürekli special zorunluluğuna çevirirdi.']))

    b=Board(228,'Kristal Ünlem','Crystal Exclamation','Kristal-Unlem',['ooooooooo']*10,26,
        'Düz tahtanın içinde büyük bir ünlem: üstte3×5 fener gövdesi, iki sıra nefes boşluğu, dipte3×3 kırmızı kristal noktası.',
        'Fener gövdesini yan yüzlerinden açarken alt kristal noktasına kırmızı eşleşme/special hazırla; geniş yan alanlarda kombinasyon kur.',
        'Fener gövdesi kalkınca orta sütunlar açılır. Kırmızı kutu kendi12 ürününü yerleştirince parçalanarak geniş bir alt alan bırakır.',
        '15 fener ve12 kırmızı elması bitir. Üretim hedefe sayılmaz; elmasların ayrıca kendi renkleriyle eşleşmesi veya special ile toplanması gerekir.')
    b.rect(3,0,3,5,75).source(3,7,95,12).diamonds(12,96).goal(75,15)
    b.d.update(previewStage='FENER GÖVDESİ AÇILINCA',openedDimerdals=True)
    ds.append(b.finish([75],'228: iki okunaklı iş, geniş special alanı; tek kaynağın12 ürününü toplama yüküyle26 hamle ilk ayar.',
        ['Üç renkli dağınık kaynaklar: ünlem okunurluğunu ve kendi-rengi elmas eşleşmesini dağıtırdı.',
         'Yeni bir çapa veya yelken: onaylı224/212 görsel fikirlerini tekrar ederdi.']))

    b=Board(229,'Rüzgâr Burgusu','Crystal Whirl','Ruzgar-Burgusu',
        ['..ooooo..','.ooooooo.']+['ooooooooo']*7+['.ooooooo.','..ooooo..'],27,
        'Dört değirmenin çevirdiği çapraz burgu; ortada çim örtülü L biçimli sarı sandık kütlesi, iki farklı kıvrımda kristal kutuları.',
        'Ortadaki çimi açıp sarı sandık yüzü mü kuracaksın, çevredeki değirmenlere special gönderip un işini mi başlatacaksın?',
        'Çim altından üç-hit sarı sandıklar çıkar; sandık L’si kalkınca değirmenler ve mavi/yeşil kutular arasında geniş special hatları açılır.',
        'Yeşil kutunun6 ve mavi kutunun10 elmasını ayrı üret/topla; dört çuvaldan toplam24 unu,12 sandığı ve12 çimi bitir.')
    for x,y in [(1,2),(5,1),(6,6),(2,8)]:b.put(x,y,80)
    crate={(x,y) for x in (3,4) for y in range(3,7)}|{(x,y) for x in (3,4,5) for y in (6,7)}
    for x,y in sorted(crate,key=lambda p:(p[1],p[0])):b.put(x,y,71).overlay(x,y,42)
    b.source(1,5,88,6).source(5,8,85,10).diamonds(16,96).goal(83,24).goal(71,12).goal(42,12)
    b.d.update(difficulty='Super Hard',previewStage='ÇİM VE SARI SANDIKLAR KALKINCA',openedDimerdals=True,flourPerSack=6)
    ds.append(b.finish([42,71],'229 Super Hard: dört değirmen, iki katmanlı sarı L ve6+10 bağımsız kaynak;27 hamle ilk tahmin. Erişim sırası ve special birleştirme kritik.',
        ['Aynalı iki büyük kanat: önceki seviyelerin varsayılan simetrisini tekrar ederdi.',
         'Beşinci bir fener hedefi eklemek: mevcut renk/katman/üretim kararlarına anlamlı yeni iş katmadan hedef listesini uzatırdı.']))

    b=Board(230,'Büyük Işık Düellosu','Grand Light Duel','Buyuk-Isik-Duellosu',['ooooooooo']*10,30,
        '9×10 düello arenası; üst ortada4×4 fener sahnesi, iki yanda kristal locaları, dipte iki değirmen ve aralarında atış koridoru.',
        'Fener sahnesinin kenarında büyük eşleşme kur; değirmenleri aynı special hattında yakalarken iki kristal locasını aç.',
        'Sahne açılınca geniş orta alan birleşir. Her loca6 elmas üretince kendi başına kalkar; değirmenler toplam16 un için çuval yollar.',
        'Üç rakibin toplam660 canı boyunca12 elmas,16 fener ve16 unu tamamla. Hedef dışı plastik/alet atışları karşı baskıdır.')
    b.rect(3,1,4,4,75).source(0,4,85,6).source(7,4,94,6).put(2,7,80).put(5,7,80)
    boss_icon='{fileID: 21300000, guid: bd8a2a2aabdf46fcabea5eb683bc76d9, type: 3}'
    b.collectible(2,660,boss_icon).diamonds(12,96).goal(75,16).goal(83,16)
    b.d.update(kind=1,difficulty='Boss Duel',previewStage='FENER SAHNESİ AÇILINCA',openedDimerdals=True,flourPerSack=8,
        boss=dict(hp=660,waves=3,playerHp=270,counterDamage=7,volleyEvery=4,volleyCount=2,thrown=[8,12,13],toolThreats=[47,48,51,50,49]))
    ds.append(b.finish([75],'230 Boss Duel: önceki220’nin620HP/28hamlesine karşı660HP/30hamle; dört hedef,9×10 açık arena. Kaynaklar6+6; un8+8. İlk tahmin, oynanmış ayar değildir.',
        ['Dar/uzun boss maskesi: 10 satır sınırı ve büyük special ihtiyacıyla uyumsuzdu.',
         'Yalnız boss HP hedefi: başlangıçtaki fener/elmas/un işini görünmez bırakırdı.']))
    letters='A-Za-zÇĞİÖŞÜçğıöşü'
    for d in ds:
        for key in ['idea','firstDecision','change','remaining','budget']:
            t=d[key].replace('iki-hit','iki aşamalı').replace('üç-hit','üç vuruşlu')
            t=re.sub(r'(?<=['+letters+r'])(?=\d)|(?<=\d)(?=['+letters+r'])',' ',t)
            d[key]=re.sub(r';(?=\S)','; ',t)
    return ds

def encode(a):return struct.pack('<'+'i'*len(a),*a).hex()
def scalar(t,k,v):return base.scalar(t,k,v)
def entries(t,k,rows):
    if not re.search(r'^  '+k+r':',t,re.M):t+='  '+k+': []\n'
    return base.entries(t,k,rows)

def write():
    assert not (HERE/'active-revision.json').exists(), 'Use the active revision author; the original batch is archived.'
    ds=designs();owned_path=HERE/'owned-hashes.json';previous=json.loads((HERE/'designs.json').read_text()) if (HERE/'designs.json').exists() else []
    owned=json.loads(owned_path.read_text()) if owned_path.exists() else {}
    for rel,h in owned.items():assert sha(ROOT/rel)==h,('User edit; refusing overwrite',rel)
    if not previous:
        assert list(map(int,re.findall(r'^    level: (\d+)$',CAT.read_text(),re.M)))==list(range(1,226))
        assert all(not(ROOT/d['asset']).exists() for d in ds)
    cat=(HERE/'Before'/CAT.name).read_text();names=json.loads((HERE/'Before'/NAMES.name).read_text())
    for d in ds:
        old=next((p for p in previous if p['level']==d['level']),None);d['guid']=old['guid'] if old else uuid.uuid4().hex
        template=PRO/('LevelV1_00002200.asset' if d['kind'] else 'LevelV1_00002250.asset');t=template.read_text()
        values=dict(m_Name=Path(d['asset']).stem,width=d['width'],height=d['height'],moves=d['moves'],levelKind=d['kind'],baseCoinReward=15 if d['kind'] else 10,
            refillSpecialGuard=.75,usesCustomIntro=0,oilSpreadMaxCells=0,movableSpawnFallbackMoves=0,movableSpawnFillColumns=1,
            bossWaveCount=3 if d['kind'] else 1,bossAttackOilCount=2 if d['kind'] else 0,bossToolThreatEnabled=d['kind'])
        for k,v in values.items():t=scalar(t,k,v)
        for k,a in d['data'].items():t=scalar(t,k,encode(a))
        for k in CUSTOM:t=entries(t,k,d['custom'][k])
        for k in ['tutorialSteps','bossWaves']:t=entries(t,k,[])
        t=entries(t,'goals',d['goals']);t=scalar(t,'movableSpawnColumns',''.join('01' if x in d['movableColumns'] else '00' for x in range(d['width'])))
        p=ROOT/d['asset'];p.write_text(t)
        if not old:Path(str(p)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {d["guid"]}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
        cat+=f'  - chapter: {d["chapter"]}\n    level: {d["level"]}\n    levelKey: {d["levelKey"]}\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n'
        names['entries'].append(dict(level=d['level'],levelKey=d['levelKey'],name=d['name'],nameEn=d['nameEn']))
    CAT.write_text(cat);NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n');(HERE/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    files=[CAT,NAMES]+[ROOT/d['asset'] for d in ds]+[Path(str(ROOT/d['asset'])+'.meta') for d in ds]
    owned_path.write_text(json.dumps({str(p.relative_to(ROOT)):sha(p) for p in files},indent=2)+'\n')
    for d in ds:print(d['level'],d['name'],d['moves'],'moves',sum(d['data']['cells']),'active',d['openingGeometry'])

if __name__=='__main__':write()
