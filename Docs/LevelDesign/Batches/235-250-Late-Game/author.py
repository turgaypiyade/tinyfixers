"""235–250 only. Draft first, scoped append only; no builds or publication."""
from pathlib import Path
from collections import Counter, deque
import argparse, hashlib, importlib.util, json, re, sys, uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
PRO = ROOT/'Assets/_Project/Settings/ProLevels'
CAT = PRO/'LevelCatalogProV1.asset'
NAMES = ROOT/'Assets/_Project/Resources/LevelNames.json'
sys.path.insert(0, str(HERE.parent/'226-230-Lantern-Wind-Crystal'))
import design as shared
spec = importlib.util.spec_from_file_location('late_topology', HERE.parent/'233-Polar-Lock/author.py')
topology = importlib.util.module_from_spec(spec); spec.loader.exec_module(topology)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
ENERGY_ICON = '{fileID: 21300000, guid: ceb006989970849ea86f6d7b7da439b0, type: 3}'
BOSS_ICON = '{fileID: 21300000, guid: bd8a2a2aabdf46fcabea5eb683bc76d9, type: 3}'


def board(n, name, en, slug, rows, moves, idea, first, change, remaining):
    b = shared.Board(n, name, en, slug, rows, moves, idea, first, change, remaining)
    b.d['custom']['fruits'] = []
    b.d['extra'] = {}
    b.d['previewStage'] = 'İLK KAPAKLAR KALKINCA'
    b.d['difficulty'] = 'Boss Duel' if n % 10 == 0 else 'Super Hard' if n % 10 == 9 else 'Hard' if n % 10 == 5 else 'Normal'
    b.d['target'] = 'Boss ayrı değerlendirilir' if n % 10 == 0 else '%30 yönü' if n % 10 in (5,9) else '%50–60' if n % 10 in (1,2,3,4) else '%40–50'
    return b


def under(b, cells, base=25, middle=None, cover=28):
    for x, y in cells:
        b.put(x, y, base)
        if middle: b.overlay(x, y, middle, 1)
        if cover: b.overlay(x, y, cover, 2 if middle else 1)


def rect(x, y, w, h): return [(xx, yy) for yy in range(y,y+h) for xx in range(x,x+w)]


def collectible(b, kind, amount, icon):
    b.d['goals'].append(dict(targetType=2,tileType=0,obstacleId=0,collectibleId=kind,iconOverride=icon,amount=amount))


def channel(b, coords):
    w = b.d['width']; path = [y*w+x for x,y in coords]
    b.d['custom']['waterChannels'].append(dict(pathCellIndices=path))
    for i in path[1:]:
        assert not b.d['data']['obstacles'][i]
        b.d['data']['obstacles'][i] = 76; b.d['data']['obstacleOrigins'][i] = i


def cover_cells(b, cells, oid=28):
    for x,y in cells:
        i=y*b.d['width']+x
        if b.d['data']['obstacles'][i]: b.overlay(x,y,oid)
        else: b.put(x,y,oid)


def fruit(b,x,y,w=2,h=2):
    d=b.d; origin=y*d['width']+x
    for xx,yy in rect(x,y,w,h):
        i=yy*d['width']+xx
        assert d['data']['cells'][i] and not d['data']['obstacles'][i]
        d['data']['obstacles'][i]=102;d['data']['obstacleOrigins'][i]=origin
    d['custom']['fruits'].append(dict(originCellIndex=origin,width=w,height=h))


def safe(b,x,y,counts):
    b.d['custom']['safes'].append(dict(originCellIndex=y*b.d['width']+x,width=2,height=2,
        redHits=counts[0],yellowHits=counts[1],greenHits=counts[2],lockHitMode=0,
        firstLock=0,secondLock=1,thirdLock=2,stackOrder=1))


def boss(b,hp,health):
    b.d['kind']=1
    b.d['extra'].update(bossAttackEveryMoves=4,bossAttackOilCount=2,bossMaxPressureObstacles=8,
        bossToolThreatEnabled=1,bossToolThreatEveryCounters=4,bossToolThreatCountdown=3,
        bossToolThreatExtraAttacks=2,bossToolThreatLockSeconds=3,playerMaxHp=health,
        damagePerClearedTile=10,enemyAttackBaseDamage=25,bossWaveCount=3)
    b.d['bossThrownObstacles']=[8,12,13];b.d['bossToolThreatTypes']=[47,48,51,50,49]
    collectible(b,2,hp,BOSS_ICON)


def designs():
    result=[]
    b=board(235,'Bakır Kemer','Copper Arch','Bakir-Kemer',['.ooooooo.']+['ooooooooo']*10,32,
        'İki ince oyma ayağı, altta iki örtülü heykelle geniş bir kemer kurar.',
        'Kenar ayağını açarken mavi/yeşil renkleri topla; hazır specialı hangi heykel için saklayacağını seç.',
        'Oyma kalkınca çamurlu ayaklar açılır; heykellerin renk işi bitse bile special aşamaları kalır.',
        'İki heykeli ve iki ayrı kenardaki çamuru tamamla; tek geniş atış bütün katmanları bitirmez.')
    under(b,[(x,y) for y in range(1,9) for x in (0,8)])
    for x,color in [(0,2),(7,3)]:
        b.put(x,9,68);b.d['custom']['tarpStatues'].append(dict(originCellIndex=9*9+x,color=color,count=8,size=2))
    b.goal(68,2).goal(28,16).goal(25,16);b.d['previewRemoved']=[28];result.append(b.d)

    b=board(236,'Cam Ray','Glass Rails','Cam-Ray',['.ooooooo.']+['ooooooooo']*8+['.ooooooo.'],26,
        'Dikey dış raylar ile alt iki yatay ray, sağdaki porselen eteğini çevreler.',
        'Kayan yatay lambaları mı, sağdaki dik rayı mı önce tamamlayacağını seç.',
        'Bir raf tamamen bittiğinde bütün çerçevesi açılır; porselenin altında çamur işi sürer.',
        'Dört renkli çerçeve ve iki aşamalı zemin kalır; yalnız ilk rafı açmak yeterli olmaz.')
    b.lamp(1,[(0,y) for y in range(1,5)],[1,2,3,4])
    b.lamp(2,[(8,y) for y in range(1,5)],[4,3,2,1],True,True)
    for pid,y in [(3,8),(4,9)]: b.lamp(pid,[(x,y) for x in range(1,5)],[3,1,4,2],True)
    under(b,rect(6,4,2,6),middle=30,cover=None)
    b.goal(79,4).goal(30,12).goal(25,12);b.d['previewRemoved']=[79,30];result.append(b.d)

    b=board(237,'Dört Vana','Four Valves','Dort-Vana',['.ooooooo.']+['ooooooooo']*10,32,
        'Dört kısa su yolu, karşılıklı oyma basamaklarının içinden kenara çıkar; dipte fener havuzu vardır.',
        'Bir su yolunu bütünüyle açmakla yeni eşleşme alanı açan oyma grubunu ilerletmek arasında seçim yap.',
        'Oyma kalkınca örtülü kanal hücreleri de erişilir; kanalın bütün bağlantısını açmak gerekir.',
        'Dört ayrı çıkış, fenerlerin iki vuruşu ve alttaki çamur bağımsız işlerdir.')
    for path in [[(2,1),(2,2),(1,2),(0,2)],[(6,3),(6,4),(7,4),(8,4)],
                 [(2,5),(2,6),(1,6),(0,6)],[(6,7),(6,8),(7,8),(8,8)]]: channel(b,path)
    for x,y in [(0,2),(7,4),(0,6),(7,8)]: cover_cells(b,rect(x,y,2,2))
    for x,y in rect(2,7,4,4):
        if y>=8: under(b,[(x,y)],middle=75,cover=None)
        else: b.put(x,y,75)
    b.goal(76,4).goal(28,16).goal(75,16).goal(25,12);b.d['previewRemoved']=[28];result.append(b.d)

    b=board(238,'Kuşlu Tribün','Songbird Terrace','Kuslu-Tribun',['ooooooooo']*11,30,
        'Sol tribünde kapaklı kuş yuvaları ve plastik sıraları; sağda kesintisiz dolap kulesi.',
        'Kuş kapaklarını açıp düzenli üretim mi kuracaksın, dolap içeriğini erkenden mi azaltacaksın?',
        'Oyma kalkınca altı kalıcı yuva açılır. Dolap kapakları ise içerikleri bitirmez.',
        'Yirmi kuş, üç dolabın içerikleri ve iki vuruşlu plastik aynı açılışla tükenmez.')
    b.rect(0,2,2,6,40)
    for x,y in rect(0,8,2,3): b.put(x,y,78).overlay(x,y,28)
    for y in (5,7,9):b.put(7,y,27)
    collectible(b,3,20,shared.base.icon(78));b.goal(28,6).goal(40,12).goal(27,3)
    b.d['previewRemoved']=[28];result.append(b.d)

    b=board(239,'Baklava Kilidi','Diamond Lock','Baklava-Kilidi',['.oooooo.']+['oooooooo']*10,33,
        'İki kenar kasası kristal kapıları gizler; iki kalın alt ayak hem maskeyi hem kilidi tamamlar.',
        'Renk ihtiyaçları farklı iki kasadan hangisini önce açacağını seç; iki üretim yüzünü tek atışta ilerletmek için hizala.',
        'Kasalar açılınca bitmiş bir hedef alanı değil, dört kepenk vuruşu ve ayrı üretim işi ortaya çıkar.',
        'On iki elması üretip ayrıca topla; ayakların oyma ve çamur aşamaları sürer.')
    b.source(0,5,85,6).source(6,5,91,6);safe(b,0,5,(3,2,3));safe(b,6,5,(2,3,3))
    under(b,rect(0,7,2,4)+rect(6,7,2,4))
    b.goal(33,2).diamonds(12,96).goal(28,16).goal(25,16);b.d['previewRemoved']=[33];result.append(b.d)

    b=board(240,'Yağmur Arenası','Rain Arena','Yagmur-Arenasi',['ooooooooo']*10,28,
        'Geniş düello alanı; iki alt su deposu ve çift katmanlı geniş oyma ayaklar.',
        'Specialı rakibe büyük hasar için mi, su deposunu erkenden açıp kurutma işi başlatmak için mi kullanacaksın?',
        'Depo kırılınca rastgele gerçek su birikintileri doğar; önceki oyma işi onları otomatik bitirmez.',
        'Üç rakip, iki depo, yeni birikintiler ve kenar çamuru birlikte tamamlanır.')
    boss(b,900,320);b.put(0,8,52).put(7,8,52)
    under(b,[(x,y) for y in range(2,6) for x in (0,1,7,8)])
    b.goal(52,2).goal(53,16).goal(28,16).goal(25,16);b.d['previewRemoved']=[28];result.append(b.d)

    b=board(241,'Enerji Rıhtımı','Energy Dock','Enerji-Rihtimi',['.oooooo.']+['oooooooo']*9,28,
        'Tek sol rıhtım: oyma kapakların devamında dört enerji kabı; sağda geniş ilk çalışma alanı.',
        'Rıhtımı açıp aynı kaynağa komşu eşleşmelerle enerji toplamayı öğren.',
        'Kaplar ortak sekiz enerji kotasına çalışır; kabı kırma hedefi yoktur.',
        'Enerji geldikten sonra açılan rıhtım çamuru ayrı bir son iştir.')
    b.rect(0,8,2,2,22);under(b,rect(0,2,2,6));collectible(b,1,8,ENERGY_ICON)
    b.goal(28,12).goal(25,12);b.d['extra']['energyPerContainer']=8;b.d['previewRemoved']=[28];result.append(b.d)

    b=board(242,'Pil Kavşağı','Battery Junction','Pil-Kavsagi',['.ooooooo.']+['ooooooooo']*10,32,
        'Enerji rıhtımı ve dört renkli pil kutusu karşılıklı; oyma omurga alttan ortaya uzanır.',
        'Bir rengin pilini bitirmek yerine dört rengi dengeli ilerlet; büyük specialı çok renge temas edecek yerde hazırla.',
        'Pilin her rengi kendi üç vuruşunu ister. Oyma omurgası kalkınca alttaki çamur açılır.',
        'Pil, on enerji ve omurganın iki aşaması kalır; aynı renkleri sürekli toplamak yetmez.')
    b.rect(0,9,2,2,22).put(7,9,23);under(b,rect(3,6,2,5));collectible(b,1,10,ENERGY_ICON)
    # BatteryBox goals count each accepted charge hit: four colors × three hits.
    b.goal(23,12).goal(28,10).goal(25,10);b.d['extra']['energyPerContainer']=10
    b.d['goals'][-3]['iconOverride']='{fileID: 739935742011162432, guid: 166e933f787f841439861ab88ee9562b, type: 3}'
    b.d['previewRemoved']=[28];result.append(b.d)

    b=board(243,'Anahtar Yelkeni','Key Sail','Anahtar-Yelkeni',
        ['ooooooo..','oooooooo.']+['ooooooooo']*7+['.oooooooo']*2,32,
        'Genişleyen yelkenin alt sol dikişi katmanlıdır; sağ dipte dört anahtar makinesi durur.',
        'Makinelerin yüzünden anahtar üretirken yelken dikişini açıp daha geniş special alanı kazan.',
        'Oyma kalkınca plastikler hareket eder; alt sekiz çamur yerinde kalır.',
        'On altı anahtar ayrıca toplanır. Üretimin bitmesi dikişin üç aşamasını tamamlamaz.')
    b.rect(7,9,2,2,44)
    under(b,rect(1,7,2,4),middle=40)
    for x,y in rect(1,5,2,2):b.put(x,y,40).overlay(x,y,28)
    b.keys(16).goal(28,12).goal(40,12).goal(25,8);b.d['previewRemoved']=[28];result.append(b.d)

    b=board(244,'Çifte Çıkış','Twin Exits','Cifte-Cikis',['.ooooooo.']+['ooooooooo']*10,30,
        'İki dış tüp ve ortadaki kargo yolu üç dikey ritim kurar; çıkış kaidesi dipte doludur.',
        'Tüplerin ucunu kısaltırken ortadaki kargoları durduran kaideye special hizala.',
        'Oyma kaidesi kalkınca kargolar düz sütunlarından aşağı iner; alt çamur ayrıca temizlenir.',
        'Altı gerçek teslimat, iki tüp ve kaidenin çamuru; tüp hücre sayısı hedef sayılmaz.')
    for x in (0,8): b.d['custom']['tubes'].append(dict(originCellIndex=4*9+x,length=7,direction=1))
    b.rect(3,3,3,2,34);under(b,rect(3,7,3,4))
    b.goal(34,6).goal(26,2).goal(28,12).goal(25,12);b.d['previewRemoved']=[28];result.append(b.d)

    b=board(245,'Mermer Çarkı','Marble Wheel','Mermer-Carki',['.ooooooo.']+['ooooooooo']*10,33,
        'Yağlı L kaidesinin bir ucunda sol omuz heykeli, diğer ucunda sağ dip heykeli; üstte tek geniş avlu.',
        'Yağı erken açıp bastırmakla heykellerin ayrı special aşamalarını ilerletmek arasında seçim yap.',
        'Oyma açılınca yağ tehdidi başlar; heykelde tek special yalnız bir aşama ilerletir.',
        'İki heykelin son parçaları ve yeniden yayılan yağ; erken alan temizliği kalıcı bitiş değildir.')
    under(b,rect(0,7,2,4)+rect(3,9,4,2),base=14)
    b.put(0,5,70).put(7,9,70);b.goal(70,2).goal(28,16).goal(14,16)
    b.d['previewRemoved']=[28];result.append(b.d)

    b=board(246,'Modül Köprüsü','Module Bridge','Modul-Koprusu',
        ['..oooo..','.oooooo.']+['oooooooo']*6+['..oooo..']*3,28,
        'Geniş üst avlu alt modül köprüsüne iner; soldaki omuz küçük bir ikinci iş yüzü oluşturur.',
        'İki vuruşlu Omega modülünün ilk/ikinci aşamasını gör; pusula kapaklarını special ile aç.',
        'Kapak kalkınca modüller hareket eder, çamurlu yuvalar geride kalır.',
        'On altı modül ve çamur; sekiz pusula yalnız açılış kapaklarıdır.')
    cells=rect(2,8,4,3)+rect(0,6,2,2);under(b,cells,middle=24,cover=None)
    for x,y in cells[:4]+cells[-4:]:b.overlay(x,y,39,2)
    b.goal(24,16).goal(25,16).goal(39,8);b.d['previewRemoved']=[39];result.append(b.d)

    b=board(247,'Su Saati','Water Clock','Su-Saati',['ooooooooo']*11,30,
        'İki uzun su yolu, dipte iki depo ve orta fener sarkacı; yan ayaklar aynı gövdeyi tamamlar.',
        'Kanal bağlantısını tamamlamakla depoyu kırıp sonradan doğacak suyu başlatmak arasında seçim yap.',
        'Yan oyma kalkınca depo yüzü açılır. Depo suyu kanaldan farklı bir temizleme işidir.',
        'İki kanal, iki depo, gerçek su birikintileri ve fenerler; başlangıçtan en fazla beş hedef.')
    channel(b,[(2,1),(2,2),(2,3),(2,4),(2,5),(1,5),(0,5)])
    channel(b,[(6,2),(6,3),(6,4),(6,5),(6,6),(7,6),(8,6)])
    b.put(0,9,52).put(7,9,52);cover_cells(b,rect(0,7,2,2)+rect(7,7,2,2))
    b.rect(3,5,2,6,75);b.goal(52,2).goal(53,16).goal(76,2).goal(28,8).goal(75,12)
    b.d['previewRemoved']=[28];result.append(b.d)

    b=board(248,'Renkli Sahne','Color Stage','Renkli-Sahne',['oooooooo']*11,30,
        'İki yükseklikte ışık balkonu, dipte iki boya kasası ve aralarındaki porselen sahne.',
        'Kayan lambanın rengini beklemekle boya kutularını çoklu special isabetiyle indirmek arasında seçim yap.',
        'Porselen sahne kalkınca çamur açılır; bir ışık çerçevesi bitince bütün yolu boşalır.',
        'Dört raf, iki kasadaki dokuzar kova ve sahnenin zemini; tek açılış atışı bitirmez.')
    for pid,y in [(1,2),(2,3)]:b.lamp(pid,[(x,y) for x in range(3)],[1,3,2],True)
    for pid,y in [(3,5),(4,6)]:b.lamp(pid,[(x,y) for x in range(5,8)],[4,2,3],True,True)
    b.put(0,9,63).put(6,9,63);under(b,rect(2,7,3,4),middle=30,cover=None)
    b.goal(79,4).goal(63,2).goal(30,12).goal(25,12);b.d['previewRemoved']=[79,30];result.append(b.d)

    b=board(249,'Son Köprü','Last Bridge','Son-Kopru',['.ooooooo.']+['ooooooooo']*10,32,
        'Karşılıklı dolap galerileri altta C biçimli uzun mıknatıs yoluna bağlanır; orta köprü pusulalıdır.',
        'Önce dolap yüzlerini mi açacaksın, mıknatısın hangi ucunu yaklaşma yoluna sokacaksın?',
        'Pusula köprüsü special ile kalkınca çamur açılır; dolap kapaklarının arkasında içerikler kalır.',
        'Dört dolap, uzun mıknatısın iki ucunun buluşması ve köprü zemini ayrı zorunlu işlerdir.')
    for x in (0,7):
        for y in (3,5):b.put(x,y,27)
    under(b,rect(2,5,4,3),cover=39)
    path=[8*9+x for x in range(3,-1,-1)]+[9*9,10*9]+[10*9+x for x in range(1,9)]+[9*9+8,8*9+8]
    b.d['custom']['magnets'].append(dict(pathCellIndices=path))
    b.goal(27,4).goal(29,1).goal(39,12).goal(25,12);b.d['previewRemoved']=[39];result.append(b.d)

    b=board(250,'Ustaların Meydanı','Masters Plaza','Ustalarin-Meydani',['ooooooooo']*10,36,
        'İki dış fener sırası ve oyma sütunları, alttaki meyve/kristal sahnesini geniş bir düello alanına bağlar.',
        'Sinek kutusunu erkenden açıp meyveleri başlat; iki renk kristal kapısını ilerleten special hatlarını kur.',
        'Meyveler yalnız sineklerle biter. Üreticiler kendi beşer elmasını bitirir; elmasları ayrıca toplarsın.',
        'Üç rakibin canı, iki meyve, on elmas ve fener/oyma işleri; yardımcı kutu ayrı kırma hedefi değildir.')
    boss(b,680,340)
    for x in (0,8):b.rect(x,0,1,8,75)
    for x in (1,7):b.rect(x,0,1,6,28)
    fruit(b,2,6);fruit(b,5,6);b.put(3,8,100).source(0,8,85,5).source(7,8,94,5)
    b.diamonds(10,96).goal(102,2).goal(75,16).goal(28,12)
    b.d['previewRemoved']=[28,102];result.append(b.d)
    return result


def layers(d, removed=()):
    result=topology.layers(d,removed)
    if 26 not in removed:
        for e in d['custom']['tubes']:
            dx,dy=[(0,-1),(0,1),(-1,0),(1,0)][e['direction']];o=e['originCellIndex']
            for k in range(e['length']):result[o+k*(dy*d['width']+dx)].append((26,o))
    return result


def feed(d, removed=()):
    ls=layers(d,removed);w,h=d['width'],d['height'];active={i for i,a in enumerate(d['data']['cells']) if a}
    blockers={i:[o for o,_ in ls[i] if o in (26,29,33) or shared.stage(o,'blocksCells') or shared.stage(o,'holdsTile')] for i in active}
    blocked={i for i in active if blockers[i]};movable={i for i in active if any(shared.stage(o,'behavior')==3 for o,_ in ls[i])}
    slots=active-blocked-movable;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in blocked:break
            if i in active:reached.add(i)
    def corner(i):return i in active and (i not in blocked or all(shared.stage(o,'allowDiagonal') for o in blockers[i]))
    q=deque(reached)
    while q:
        i=q.popleft();x,y=i%w,i//w
        if y+1==h:continue
        for dx in (0,-1,1):
            j=i+w+dx
            if not 0<=x+dx<w or j not in active or j in blocked or j in reached:continue
            if dx and not(corner(i+dx) or corner(i+w)):continue
            reached.add(j);q.append(j)
    return slots,reached&slots


def prepare():
    assert not (HERE/'written.json').exists(), 'Draft writer is closed after append; revise owned assets explicitly.'
    ds=designs()
    for d in ds:
        assert 1<=len(d['goals'])<=5 and d['height']<= (10 if d['kind'] else 11)
        assert all(g['amount']>0 and 'guid:' in g['iconOverride'] for g in d['goals'])
        assert not {42,58,69,71,72,73,74}&{o for p in layers(d) for o,_ in p}
        slots,reach=feed(d);pins=slots-reach
        # Ordinary pins only in truly shadowed normal slots. Choose four colors
        # cyclically; never create a fixed special or color the whole board.
        for i in pins:d['data']['pinnedTileTypes'][i]=1+((i%d['width'])+i//d['width'])%4
        d['openingGeometry']=dict(activeCells=sum(d['data']['cells']),normalSlots=len(slots),
            naturallyFed=len(reach),shadowPins=sorted(pins),specialPins=0)
        assert len(pins)<=8,(d['level'],'too many initial shadow pockets',pins)
        d['budget']=f'{d["moves"]} hamle ilk ayarı; oyuncu hedefi {d["target"]}. İnsan geçiş oranı ölçülmüş değildir.'
    HERE.mkdir(parents=True,exist_ok=True)
    (HERE/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    for d in ds:print(d['level'],d['name'],d['openingGeometry'])
    return ds


def serialize(d):
    t=(PRO/'LevelV1_00002340.asset').read_text()
    scalars=dict(m_Name=Path(d['asset']).stem,width=d['width'],height=d['height'],moves=d['moves'],
        levelKind=d['kind'],usesCustomIntro=0,hamsterSatietyOverride=0,movableSpawnFallbackMoves=0,
        movableSpawnFillColumns=1,refillSpecialGuard=-1,energyPerContainer=1,bossWaveCount=1,
        bossAttackEveryMoves=2,bossAttackOilCount=0,bossToolThreatEnabled=0,playerMaxHp=900)
    scalars.update(d['extra'])
    for k,v in scalars.items():t=shared.scalar(t,k,v)
    for k,v in d['data'].items():t=shared.scalar(t,k,shared.encode(v))
    t=shared.scalar(t,'randomPool',shared.encode([0,1,2,3]))
    t=shared.scalar(t,'movableSpawnColumns','00'*d['width'])
    for k in list(shared.CUSTOM)+['fruits','tutorialSteps','bossWaves']:
        rows=[]
        for row in d['custom'].get(k,[]):
            rows.append({key:shared.encode(value) if key=='pathCellIndices' else value for key,value in row.items()})
        t=shared.entries(t,k,rows)
    for k in ['bossThrownObstacles','bossToolThreatTypes']:t=shared.scalar(t,k,shared.encode(d.get(k,[])))
    t=shared.entries(t,'goals',d['goals'])
    return t


def write(ds):
    assert not (HERE/'written.json').exists(),'Append-only author already ran; revise explicitly, never overwrite user edits.'
    assert list(map(int,re.findall(r'^    level: (\d+)$',CAT.read_text(),re.M)))==list(range(1,235))
    assert all(not (ROOT/d['asset']).exists() for d in ds)
    before={str(p.relative_to(ROOT)):sha(p) for p in PRO.glob('LevelV1_*.asset*')}
    before[str(CAT.relative_to(ROOT))]=sha(CAT);before[str(NAMES.relative_to(ROOT))]=sha(NAMES)
    (HERE/'protected-before.json').write_text(json.dumps(before,indent=2)+'\n')
    backup=HERE/'Before';backup.mkdir(exist_ok=True)
    (backup/CAT.name).write_bytes(CAT.read_bytes());(backup/NAMES.name).write_bytes(NAMES.read_bytes())
    names=json.loads(NAMES.read_text());cat=CAT.read_text();rendered=[]
    for d in ds:
        assert not any(e['level']==d['level'] or e['levelKey']==d['levelKey'] for e in names['entries'])
        d['guid']=uuid.uuid4().hex;rendered.append((ROOT/d['asset'],serialize(d),d['guid']))
        names['entries'].append(dict(level=d['level'],levelKey=d['levelKey'],name=d['name'],nameEn=d['nameEn']))
        cat+=f'  - chapter: {(d["level"]-1)//25+1}\n    level: {d["level"]}\n    levelKey: {d["levelKey"]}\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n'
    for p,t,guid in rendered:
        p.write_text(t);Path(str(p)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    CAT.write_text(cat);NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    (HERE/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    (HERE/'written.json').write_text(json.dumps({str(p.relative_to(ROOT)):sha(p) for p,_,_ in rendered},indent=2)+'\n')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--write',action='store_true');args=parser.parse_args()
    ds=prepare()
    if args.write:write(ds)
