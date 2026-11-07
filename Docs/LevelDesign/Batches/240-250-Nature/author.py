"""240–250 doğa siluetleri — birer birer revizyon. Mevcut GUID/katalog/isim anahtarı korunur.
Unity/build/sim yok. Kullanım: python3 -B author.py <level> [--write]"""
from pathlib import Path
import importlib.util, json, sys, hashlib, re
HERE = Path(__file__).resolve().parent
LATE = HERE.parent/'235-250-Late-Game'
sys.path.insert(0, str(LATE)); sys.path.insert(0, str(HERE.parent/'226-230-Lantern-Wind-Crystal'))
import author as late
shared = late.shared
ROOT = late.ROOT; PRO = late.PRO; NAMES = late.NAMES
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
BIRD_ICON = shared.base.icon(78)


def level242():
    b = late.board(242, 'Elma Ağacı', 'Apple Tree', 'Elma-Agaci', [
        'o.ooooo.o',
        '.ooooooo.',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        '.ooooooo.',
        '..ooooo..',
        'oo.ooo.oo',
        'oo.ooo.oo',
        '.ooooooo.',
        '.ooooooo.'], 26,
        'Yuvarlak taç, üç taşlık gövde ve yayılan köklerle bir elma ağacı. Elmalar ağaçtan sarkan ayrı adacıklarda; '
        'taç tamamen oyun alanı. İki guguk kuşu evi dalların ucunda, sinek yuvaları köklerin arasında.',
        'Taçtaki kuş evlerini mi besleyeceksin, yoksa special üretip köklerdeki sinek yuvalarına mı indireceksin?',
        'Köklerdeki yuvaya her special vuruşu tahtaya bir yumurta bırakır; yumurta eşleşmeyle patlayınca üç sinek '
        'açık bir elmayı ısırır. Kuş evleri her vuruşta bir kuş uçurur.',
        'Dört elmanın her biri altı ısırık (iki yumurta) ister; iki yuvanın sekiz yumurtası tam yeter, hiçbiri boşa gitmemeli. 12 kuş ayrıca toplanır.')
    # Elmalar düşmez ve normal taşla kırılmaz → oyun alanında yer kaplamaz, ayrı adacıklara asılır
    # (kullanıcı 2026-11-07). Kural: 2 kutu = 4 elma.
    late.fruit(b, 0, 0, 1, 1); late.fruit(b, 8, 0, 1, 1)      # tacın üst köşelerinde
    late.fruit(b, 0, 7); late.fruit(b, 7, 7)                  # gövdenin iki yanında sarkan iri elmalar
    for x in (0, 8):
        b.put(x, 3, 78).put(x, 4, 78)       # dal uçlarında ikişerli kuş evi grupları
    b.put(1, 9, 100).put(6, 9, 100)      # kökler arasında sinek yuvaları
    b.goal(102, 4)
    late.collectible(b, 3, 12, BIRD_ICON)
    b.d['previewStage'] = 'İLK ELMA YENİNCE'
    b.d['previewRemoved'] = []
    return b.d


def level241():
    b = late.board(241, 'Lale Tarlası', 'Tulip Field', 'Lale-Tarlasi', [
        '.o..o..o.',
        '.oo.o.oo.',
        '.ooooooo.',
        '.ooooooo.',
        '.ooooooo.',
        '..ooooo..',
        '...ooo...',
        '.o..o..o.',
        'oo..o..oo',
        'ooooooooo',
        'ooooooooo'], 28,
        'Üç taç yapraklı bir lale, ince sapı ve iki yaprağıyla toprağa iniyor. Sapın iki yanında, toprakta iki yel değirmeni dönüyor.',
        'Lalenin kırmızı taşlarını mı toplayacaksın, yoksa special üretip sapın dibindeki değirmen kanatlarını mı kıracaksın?',
        'Dört kanadı kırılan değirmen yıkılır ve tarlaya un çuvalı fırlatır; çuval patlayınca çevresine un saçar, un taşların altında kalır.',
        'Unu üstündeki taşları eşleştirerek temizle; iki değirmenin unu ve kırmızı lale taşları birlikte bitmeli.')
    b.put(2, 9, 80).put(5, 9, 80)        # sapın iki yanında, toprakta değirmenler
    b.goal(83, 16)
    core = re.search(r'^  core: (.*)$', (ROOT/'Assets/_Project/Resources/TileIconLibrary_Main.asset').read_text(), re.M)[1]
    b.d['goals'].append(dict(targetType=0, tileType=1, obstacleId=0, collectibleId=0, iconOverride=core, amount=40))
    b.d['previewStage'] = ''
    b.d['previewRemoved'] = []
    return b.d

_ICONLIB = None
def tile_goal(b, tile_type, amount):
    global _ICONLIB
    _ICONLIB = _ICONLIB or (ROOT/'Assets/_Project/Resources/TileIconLibrary_Main.asset').read_text()
    key = ['gear', 'core', 'bolt', 'plate'][tile_type]
    icon = re.search(r'^  ' + key + r': (.*)$', _ICONLIB, re.M)[1]
    b.d['goals'].append(dict(targetType=0, tileType=tile_type, obstacleId=0, collectibleId=0, iconOverride=icon, amount=amount))


def walled_lantern(b, x, y, pid):
    """Mağara duvarı (Wall55) arkasında fener (Lantern75): duvar kırılınca fener görünür."""
    b.put(x, y, 75).overlay(x, y, 55)
    origin = y*b.d['width'] + x
    b.d['data']['wallPieceIds'][origin] = pid
    b.d['walls'].append(dict(origin=origin, obstacleId=55, pieceId=pid, cells=[origin]))


def level243():
    b = late.board(243, 'Yusufçuk', 'Dragonfly', 'Yusufcuk', [
        '...ooo...',
        '...ooo...',
        'ooooooooo',
        'ooooooooo',
        '..ooooo..',
        'ooooooooo',
        'ooooooooo',
        '...ooo...',
        '...ooo...',
        '....o....',
        '....o....'], 26,
        'Bir yusufçuk: başı, ince gövdesi ve dört kanadı. Kanatların uçlarında, kenarları boyunca renkli ışık damarları parlıyor.',
        'Hangi kanadın ışık damarını önce söndüreceksin? Kayan damarlarda rengin yanına geldiği anı kollamak gerekir.',
        'Her lamba kendi rengindeki komşu eşleşmeyle söner; mor lamba yalnız special ister. Bir damarın bütün lambaları sönünce çerçeve kalkar.',
        'Dört kanat damarı ve gövdenin mavi taşları birlikte bitmeli.')
    for pid, pts, cols, shift in [
            (1, [(2, 3), (1, 3), (0, 3), (0, 2)], [2, 3, 1, 4], False),
            (2, [(6, 3), (7, 3), (8, 3), (8, 2)], [4, 1, 3, 2], True),
            (3, [(2, 6), (1, 6), (0, 6), (0, 5)], [1, 5, 2, 3], False),
            (4, [(6, 6), (7, 6), (8, 6), (8, 5)], [3, 2, 5, 1], True)]:
        b.lamp(pid, pts, cols, shift)
    b.goal(79, 4)
    tile_goal(b, 2, 30)
    return b.d


def level244():
    b = late.board(244, 'Dağ Deresi', 'Mountain Brook', 'Dag-Deresi', [
        '....o....',
        '...ooo...',
        '..ooooo..',
        '..ooooo..',
        '.ooooooo.',
        '.ooooooo.',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo'], 27,
        'Tek büyük dağ: sivri zirveden eteklere genişleyen yamaçlar. Zirvede doğan dere kıvrıla kıvrıla vadiye iner; yamaçtaki ikinci kaynak sağ yamaçtan dağın eteğine kadar akar. Dere yataklarını kayalar tıkamış.',
        'Önce kayaları mı kıracaksın, yoksa açık dere hücrelerinin üstündeki taşları eşleştirip suyu mu ilerleteceksin?',
        'Kaya kırılınca altındaki dere hücresi görünür; üstündeki taş eşleşince hücre açılır ve su kaynağa bağlı açık hücrelerden akar.',
        'İki derenin tamamı açılıp su kenara ulaşmalı; yataktaki bütün kayalar da temizlenmeli.')
    main = [(4, 1), (4, 2), (4, 3), (3, 3), (3, 4), (3, 5), (2, 5), (2, 6), (2, 7), (3, 7), (4, 7), (4, 8), (4, 9), (4, 10)]
    side = [(5, 2), (5, 3), (6, 3), (6, 4), (7, 4), (7, 5), (7, 6), (7, 7), (7, 8), (7, 9), (7, 10)]
    late.channel(b, main); late.channel(b, side)
    late.cover_cells(b, [(4, 3), (3, 4), (2, 6), (4, 8), (6, 4), (7, 6), (7, 9)])
    b.goal(76, 2).goal(28, 7)
    return b.d


def level245():
    b = late.board(245, 'Mantar Ormanı', 'Mushroom Grove', 'Mantar-Ormani', [
        '.ooooooo.',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'o..ooo..o',
        'oooo.oooo',
        'ooo.o.ooo',
        'ooo.o.ooo',
        '..o.o.o..',
        '.ooooooo.',
        'ooooooooo'], 27,
        'Orman zemininden yükselen üç mantar: ortada geniş kırmızı şapkalı büyük mantar, iki yanda sarı şapkalı küçükler. Şapkaların alt kenarındaki benekler 2×2 renkli kepenkli kutular.',
        'Büyük şapkanın kırmızı kepenklerini mi açacaksın, yoksa yan mantarların sarı kutularına mı yöneleceksin? Her kutu yalnız kendi rengindeki komşu eşleşmeyle açılır.',
        'Dört dilimi açılan kutunun yanındaki her aynı renk eşleşme tahtaya bir elmas bırakır; elmaslar normal taş gibi düşer ve eşleşir.',
        'Dört kutunun bütün elmasları üretilip toplanmalı; tek kutuyu bitirmek yetmez.')
    # Kutular şapka kenarında ama üç yandan açık (≥4 komşu taş); altları hole → kuru cep yok.
    b.source(1, 2, 94, 3).source(6, 2, 94, 3).source(0, 6, 91, 3).source(7, 6, 91, 3)
    b.diamonds(12, 96)
    return b.d


def level246():
    b = late.board(246, 'Kuş Yuvası', 'Bird Nest', 'Kus-Yuvasi', [
        '.ooo.ooo.',
        'ooooooooo',
        '..ooooo..',
        '.ooooooo.',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        '.ooooooo.',
        '..ooooo..',
        '...ooo...',
        '....o....'], 26,
        'Yapraklı bir dal ve altında sarkan yuva. Dalın iki ucunda guguk kuşu evleri, yuvanın ortasında altı benekli yumurta.',
        'Yumurtaları çatlatıp yavru kuşların yardımını mı alacaksın, yoksa dal uçlarındaki kuş evlerini mi besleyeceksin?',
        'Yumurta iki vuruşta çatlar; çıkan kuşlar tahtadaki hedeflere uçup vurur. Kuş evleri her vuruşta bir kuş uçurur.',
        'Altı yumurtanın hepsi çatlamalı ve evlerden 12 kuş uçmalı.')
    for x, y in [(3, 5), (4, 5), (5, 5), (3, 6), (4, 6), (5, 6)]:
        b.put(x, y, 45)
    b.put(0, 1, 78).put(1, 1, 78).put(7, 1, 78).put(8, 1, 78)   # dal uçlarında ikişerli kuş evleri
    b.goal(45, 6)
    late.collectible(b, 3, 12, BIRD_ICON)
    return b.d


def level247():
    b = late.board(247, 'Şelale', 'Waterfall', 'Selale', [
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        '...ooo...',
        '...ooo...',
        '...ooo...',
        '...ooo...',
        '...ooo...',
        '...ooo...',
        '..ooooo..',
        '..ooooo..'], 27,
        'Geniş bir yamaçtan dar bir boğaza dökülen şelale ve dipteki gölet. Yamacın iki ucundaki mağaraların ağzı duvarla örülmüş; içeride fenerler var.',
        'Şelale yatağındaki kayaları mı açacaksın, yoksa mağara duvarlarını kırıp fenerlere mi ulaşacaksın?',
        'Mağara duvarı kırılınca arkasındaki fener çıkar. Kaya kalkınca şelale yatağı görünür; açılan yataktan su gölete akar.',
        'Şelale yatağının tamamı açılmalı, dört duvar kırılmalı ve dört fener yakılıp toplanmalı.')
    late.channel(b, [(4, y) for y in range(0, 11)])
    late.cover_cells(b, [(4, 3), (4, 4), (4, 5), (4, 6), (4, 7), (4, 8)])
    for pid, (x, y) in enumerate([(0, 2), (1, 2), (7, 2), (8, 2)], 1):
        walled_lantern(b, x, y, pid)
    b.goal(76, 1).goal(55, 4).goal(75, 4).goal(28, 6)
    return b.d


def level248():
    b = late.board(248, 'Ayçiçeği', 'Sunflower', 'Aycicegi', [
        '..o.o.o..',
        'ooooooooo',
        '.ooooooo.',
        'ooooooooo',
        '.ooooooo.',
        'ooooooooo',
        '..o.o.o..',
        '....o....',
        '...oo....',
        '....oo...',
        '....o....'], 24,
        'Taç yaprakları hole’larla ayrılmış bir ayçiçeği; ortasında ışıl ışıl dokuz tohum feneri, göbeği saran on iki sarı taç yaprağı, altında yapraklı sapı.',
        'Fenerlere ulaşmak için önce sarı taç yapraklarını açmalısın: yapraklar yalnız yanlarındaki SARI eşleşmeyle kırılır. Sarıyı nerede biriktireceksin?',
        'Fener ilk vuruşta yanar, ikinci vuruşta toplanır; fenerler hareketlidir ve boşalan yere düşer.',
        'On iki taç yaprağı, dokuz fener ve 25 sarı taş birlikte bitmeli.')
    for y in range(2, 5):
        for x in range(3, 6):
            b.put(x, y, 75)
    # Göbeği çevreleyen sarı taç yaprakları: yalnız SARI komşu eşleşmeyle kırılan renkli plastik.
    petals = [(3, 1), (4, 1), (5, 1), (3, 5), (4, 5), (5, 5), (2, 2), (2, 3), (2, 4), (6, 2), (6, 3), (6, 4)]
    for x, y in petals:
        b.put(x, y, 9)
    b.goal(75, 9).goal(9, len(petals))
    tile_goal(b, 0, 25)
    return b.d


def level249():
    b = late.board(249, 'Meyve Bahçesi', 'Orchard', 'Meyve-Bahcesi', [
        '.ooo.ooo.',
        'oooo.oooo',
        'oooo.oooo',
        'oooo.oooo',
        '.o.o.o.o.',
        '..o...o..',
        '..o...o..',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo'], 26,
        'Aralarında boşluk olan iki elma ağacı; her tacın altından iki elma sarkıyor, dallarda birer guguk kuşu evi, bahçe toprağının iki köşesinde sinek yuvaları.',
        'İki yuvanın sekiz yumurtası dört elmaya ancak yetiyor: special’ları yuvalara mı harcayacaksın, kuş evlerine mi?',
        'Yuvaya her special vuruşu bir yumurta bırakır; yumurta patlayınca üç sinek bir elmayı ısırır. Kuş evleri her vuruşta bir kuş uçurur.',
        'Dört elmanın her biri iki yumurta ister; hiçbir yumurta boşa gitmemeli. 16 kuş ayrıca toplanır.')
    for x in (1, 3, 5, 7):                   # taçların altından sarkan elmalar (ayrı adacık)
        late.fruit(b, x, 4, 1, 1)
    for x in (1, 7):
        b.put(x, 2, 78).put(x, 3, 78)       # her taçta ikişerli kuş evi
    b.put(0, 9, 100).put(7, 9, 100)
    b.goal(102, 4)
    late.collectible(b, 3, 16, BIRD_ICON)
    return b.d


def level240():
    b = late.board(240, 'Gökkuşağı', 'Rainbow', 'Gokkusagi', [
        'oo.ooo.oo',
        'oo.ooo.oo',
        '..ooooo..',
        '.ooooooo.',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo'], 30,
        'Yağmurdan sonra: iki köşede bulut adacıkları, tahtanın ortasında iki bantlı kalıcı bir gökkuşağı kemeri — dışta kırmızı, içte sarı. Kemerin altında üç sütunluk kuytu bir bahçe.',
        'Rakibe special mı atacaksın, yoksa gökkuşağının bantlarını kendi renkleriyle mi açacaksın? Her bant yalnız kendi rengindeki komşu eşleşmeyle (ya da special ile) kırılır.',
        'Kırmızı bant tek, sarı iki vuruşta kalkar; kemer açıldıkça altındaki kuytu bahçe tahtaya katılır.',
        'Üç rakibin canı ve gökkuşağının iki bandı birlikte bitmeli. Kemer kalkınca altındaki kuytu alan serbest kalır.')
    late.boss(b, 650, 320)   # kullanıcı “kazanılamaz” → depo/birikinti çıkarıldı, can 650, 30 hamle
    w = b.d['width']
    bands = [
        (72, 1, [(1, y) for y in range(5, 10)] + [(2, 4), (3, 3), (4, 3), (5, 3), (6, 4)] + [(7, y) for y in range(5, 10)]),
        (71, 2, [(2, y) for y in range(5, 10)] + [(3, 4), (4, 4), (5, 4)] + [(6, y) for y in range(5, 10)]),
    ]
    for oid, hits, cells in bands:
        for x, y in cells:
            b.put(x, y, oid)
            if hits < 3:  # 3 = kütüphane varsayılanı
                b.d['custom']['colorCrateInitialHits'].append(dict(originCellIndex=y * w + x, obstacleId=oid, remainingHits=hits))
    b.d['maxPins'] = 15                     # kemerin altındaki kuytu: açılışta gerçek başlangıç taşı (dolum ulaşmaz)
    b.goal(72, 15).goal(71, 13)
    return b.d


def level250():
    b = late.board(250, 'Hasat Bayramı', 'Harvest Festival', 'Hasat-Bayrami', [
        'oo.ooo.oo',
        'oo.ooo.oo',
        '..ooooo..',
        '.ooooooo.',
        'ooooooooo',
        'ooooooooo',
        '.ooooooo.',
        '.ooooooo.',
        '.ooooooo.',
        '..ooooo..'], 24,
        'Bölümün finali: köy meydanında hasat bayramı. Ortada yukarı uzanan fener direği, iki yanda değirmenler dönüyor, iki üst köşede dallardan iri elmalar sarkıyor, meydanın dibinde sinek yuvası.',
        'Rakibe vuracak büyük special’ı mı kuracaksın, yoksa yuvaya ve değirmenlere special gönderip hasadı mı başlatacaksın?',
        'Değirmen kanatları kırılınca un çuvalı meydana düşer; fenerler ilk vuruşta yanar, ikincide toplanır; yuvaya her special bir yumurta bırakır ve sinekler köşe elmalarını yer.',
        'Üç rakibin canı, iki elma, dokuz fener ve değirmenlerin unu birlikte bitmeli.')
    late.boss(b, 950, 340)   # kullanıcı iki kez “kolay” dedi: 680/32 → 820/25 → 950/24, saldırı 3 hamlede bir
    b.d['extra']['bossAttackEveryMoves'] = 3
    late.fruit(b, 0, 0); late.fruit(b, 7, 0)                  # köşe adacıklarında 2×2 elmalar (1 kutu = 2 elma)
    for y in range(0, 3):
        for x in range(3, 6):
            b.put(x, y, 75)                                     # fener direği
    b.put(0, 4, 80).put(7, 4, 80)                               # iki yanda değirmen
    b.put(3, 8, 100)                                            # meydanın dibinde sinek yuvası
    b.goal(102, 2).goal(75, 9).goal(83, 12)
    return b.d


LEVELS = {240: level240, 241: level241, 242: level242, 243: level243, 244: level244, 245: level245,
          246: level246, 247: level247, 248: level248, 249: level249, 250: level250}


def prepare(n):
    d = LEVELS[n]()
    assert 1 <= len(d['goals']) <= 5 and d['height'] <= (10 if d['kind'] else 11)
    boxes = sum(1 for i, o in enumerate(d['data']['obstacles']) if o == 100 and d['data']['obstacleOrigins'][i] == i)
    assert boxes * 2 == len(d['custom'].get('fruits', [])), (n, 'kural: 1 sinek kutusu = 4 yumurta = 2 elma')
    banned = {23, 24, 42, 58, 69}
    assert not banned & {o for p in late.layers(d) for o, _ in p}, 'yasaklı engel'
    slots, reach = late.feed(d); pins = slots - reach
    for i in pins: d['data']['pinnedTileTypes'][i] = 1 + ((i % d['width']) + i // d['width']) % 4
    d['openingGeometry'] = dict(activeCells=sum(d['data']['cells']), normalSlots=len(slots),
                                naturallyFed=len(reach), shadowPins=sorted(pins), specialPins=0)
    assert len(pins) <= d.get('maxPins', 4), (n, 'başlangıçta beslenmeyen cep', sorted(pins))
    return d


def write(d):
    asset = ROOT/d['asset']; meta = Path(str(asset)+'.meta')
    assert asset.exists(), 'yalnız mevcut leveli revize eder'
    before = HERE/'Before'/str(d['level']); before.mkdir(parents=True, exist_ok=True)
    if not (before/asset.name).exists():
        (before/asset.name).write_bytes(asset.read_bytes()); (before/NAMES.name).write_bytes(NAMES.read_bytes())
    asset.write_text(late.serialize(d))          # meta/GUID ve katalog satırı dokunulmaz
    names = json.loads(NAMES.read_text())
    entry = next(e for e in names['entries'] if e['level'] == d['level'] and e['levelKey'] == d['levelKey'])
    entry['name'], entry['nameEn'] = d['name'], d['nameEn']
    NAMES.write_text(json.dumps(names, ensure_ascii=False, indent=2)+'\n')
    (HERE/f'design-{d["level"]}.json').write_text(json.dumps(d, ensure_ascii=False, indent=2)+'\n')
    print('yazıldı', asset.name, 'guid korunuyor:', meta.read_text().split('guid: ')[1].split()[0])


if __name__ == '__main__':
    n = int(sys.argv[1]); d = prepare(n); print(d['name'], d['openingGeometry'])
    if '--write' in sys.argv: write(d)
