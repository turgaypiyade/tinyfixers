"""213–220 authored compositions. No Unity execution, move/cascade simulation or publishing."""
from pathlib import Path
import hashlib,json,re,struct,sys,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];A=ROOT/'Assets/_Project';P=A/'Settings/ProLevels'
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import footprint
CAT=P/'LevelCatalogProV1.asset';NAMES=A/'Resources/LevelNames.json'
BOSS_ICON='{fileID: 21300000, guid: bd8a2a2aabdf46fcabea5eb683bc76d9, type: 3}'
def encode(a):return struct.pack('<'+str(len(a))+'i',*a).hex()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

class Board:
    def __init__(self,n,title,en,slug,rows,moves,idea,opening,after):
        self.n=n;self.title=title;self.en=en;self.slug=slug;self.rows=rows;self.w=len(rows[0]);self.h=len(rows)
        assert all(len(row)==self.w for row in rows)
        self.moves=moves;self.idea=idea;self.opening=opening;self.after=after
        size=self.w*self.h
        self.data={k:[0]*size for k in ('cells','obstacles','wallPieceIds','lampColorByCell','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')}
        self.data['obstacleOrigins']=[-1]*size;self.data['cells']=[int(c=='o') for row in rows for c in row]
        self.stacks=[];self.paths=[];self.goals=[];self.movableColumns={};self.refill=[]
    def put(self,x,y,o):
        origin=y*self.w+x;fw,fh=footprint(o)
        assert x+fw<=self.w and y+fh<=self.h,(self.n,o,x,y)
        for dy in range(fh):
            for dx in range(fw):
                i=(y+dy)*self.w+x+dx
                assert self.data['cells'][i] and not self.data['obstacles'][i],(self.n,o,x,y,i)
                self.data['obstacles'][i]=o;self.data['obstacleOrigins'][i]=origin
        return self
    def rect(self,x,y,w,h,o):
        assert footprint(o)==(1,1)
        for yy in range(y,y+h):
            for xx in range(x,x+w):self.put(xx,yy,o)
        return self
    def cover(self,x,y,w,h,o):
        for yy in range(y,y+h):
            for xx in range(x,x+w):
                i=yy*self.w+xx;assert self.data['cells'][i]
                self.stacks.append(dict(originCellIndex=i,obstacleId=o,stackOrder=1,initialRemainingHits=0))
        return self
    def channel(self,points):
        path=[]
        for a,b in zip(points,points[1:]):
            assert a[0]==b[0] or a[1]==b[1]
            dx=(b[0]>a[0])-(b[0]<a[0]);dy=(b[1]>a[1])-(b[1]<a[1]);assert dy>=0
            steps=abs(b[0]-a[0])+abs(b[1]-a[1]);assert steps>0
            path.extend((a[1]+k*dy)*self.w+a[0]+k*dx for k in range(steps))
        path.append(points[-1][1]*self.w+points[-1][0]);assert len(path)==len(set(path))
        assert all(self.data['cells'][i] for i in path)
        assert not self.data['obstacles'][path[0]]
        self.paths.append(dict(pathCellIndices=path));return self
    def goal(self,o,count):self.goals.append(dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=count));return self
    def collectible(self,id,count):self.goals.append(dict(targetType=2,tileType=0,obstacleId=0,collectibleId=id,iconOverride=BOSS_ICON if id==2 else '{fileID: 0}',amount=count));return self

def designs():
    result=[]
    b=Board(213,'Işıklı Balon','Lantern Balloon','Isikli-Balon',[
        '...ooo...','..ooooo..','.ooooooo.','ooooooooo','ooooooooo','.ooooooo.','..ooooo..','...ooo...','...ooo...','..ooooo..','..ooooo..'],22,
        'Balon kubbesi, iki fener ipi ve iki aşamalı plastikten tek parça sepet.',
        'Kubbedeki porselen tacı aç; iki fener sütunundan hangisini önce yakacağına karar ver.',
        'Kubbe açılınca geniş eşleşmeler, dar boyundan sepetin iki aşamalı son işine bağlanır.')
    b.rect(3,0,3,1,30).rect(2,1,5,1,30)
    for x in (2,6):b.rect(x,2,1,4,75)
    b.rect(2,9,5,2,40).goal(75,18).goal(30,8).goal(40,10)
    b.movableColumns={2:75,6:75};b.refill=[dict(obstacleId=75,initial=8,goal=18,extra=10)]
    result.append(b)

    b=Board(214,'Guguk Çanı','Cuckoo Bell','Guguk-Cani',[
        '...ooo...','..ooooo..','.ooooooo.','.ooooooo.','.ooooooo.','ooooooooo','ooooooooo','ooooooooo','ooooooooo','...ooo...','...ooo...'],24,
        'Çan gövdesinin kenarlarında L/ters L kuş evleri; alt bordürde iki renkli sandık, çiçekli dil.',
        'Açık üst gövdeden kuş evlerinin iç yüzüne seri isabet kur veya çiçekli orta hattı aç.',
        'Renkli sandıkların kapakları açılınca dört renk işi sürer; kuş kotası tek açılışla bitmez.')
    for x in (0,8):b.rect(x,5,1,4,78)
    b.put(1,8,78).put(7,8,78).put(1,6,21).put(6,6,21)
    b.rect(3,7,3,4,58).collectible(3,26).goal(21,2).goal(58,12).goal(42,12);result.append(b)

    b=Board(215,'Vitray Atölyesi','Stained Glass Workshop','Vitray-Atolyesi',['ooooooooo']*11,26,
        'Yoğun iş yükünü taşıyan düz vitray: sarı lento, mavi/kırmızı dikey panolar ve sandıklı alt kaide.',
        'Sarı üst bandı kırarak geniş alanı besle; açık merkezden renk panolarını iki yandan çöz.',
        'İki aşamalı plastik alt kuşağı açılır, dört 2×2 sandığın renk işi son perdeyi oluşturur.')
    b.rect(0,2,9,1,9).rect(3,3,3,1,9).rect(0,3,3,4,11).rect(6,3,3,4,12)
    for x in (0,5):b.rect(x,7,4,2,40)
    for x in (0,2,5,7):b.put(x,9,21)
    b.goal(9,12).goal(11,12).goal(12,12).goal(40,16).goal(21,4);result.append(b)

    b=Board(216,'Sulu Lale','Water Tulip','Sulu-Lale',[
        'oo..o..oo','ooo.o.ooo','ooooooooo','ooooooooo','.ooooooo.','..ooooo..','...ooo...','...ooo...','...ooo...','..ooooo..','.ooooooo.'],22,
        'Üç uçlu lale başı, kanalın kıvrıldığı çiçekli taç ve porselen kök tabanı.',
        'Çiçekli taçtaki katmanı kaldırarak soldaki kanal kıvrımını kaynağa bağla.',
        'Su çiçeğin sapından aşağı iner; tabandaki porselenleri açıp aşağı bakan çıkışı tamamla.')
    b.rect(1,3,7,1,58).rect(2,4,5,1,58).rect(2,9,5,1,30).rect(1,10,7,1,30)
    b.channel([(4,0),(4,2),(2,2),(2,4),(4,4),(4,10)])
    b.goal(76,1).goal(58,12).goal(42,12).goal(30,12);result.append(b)

    b=Board(217,'Kanatlı Kum Saati','Winged Hourglass','Kanatli-Kum-Saati',[
        'ooooooooo','ooooooooo','.ooooooo.','..ooooo..','...ooo...','...ooo...','..ooooo..','.ooooooo.','ooooooooo','ooooooooo','ooooooooo'],24,
        'Kum saati siluetinde yukarıdaki yeşil cam, dar bel ve alt ayaklara oturan iki değirmen.',
        'Porselen üst tacı ve yeşil çift panoyu açarken belde special üretip alt değirmen satırlarına taşı.',
        'Değirmenlerin renkli kanatlarını special ile indir; çuval açılışından sonra un temizliği sürer.')
    b.rect(2,0,5,1,30)
    for x in (1,6):b.rect(x,1,2,2,13)
    for x in (0,7):b.put(x,8,80)
    b.rect(2,8,5,2,40).rect(3,10,3,1,40)
    b.goal(83,40).goal(13,8).goal(30,5).goal(40,13);result.append(b)

    b=Board(218,'Kıvrılan Nehir','Winding River','Kivrilan-Nehir',[
        'ooooooo..','oooooooo.','.oooooooo','..ooooooo','...oooooo','..ooooooo','.oooooooo','oooooooo.','ooooooo..','.ooooooo.','..ooooooo'],25,
        'Dalga silueti içinde uzun kıvrılan kanal; bir kıyıda mavi, öbüründe kırmızı plastik ve iki kıyı sandığı.',
        'Kaynaktan gelen üst kıvrımı aç veya sağ üst kuş kıyısında seri isabet kur.',
        'Kanal kırmızı plastiğin altından geçer; mavi kıyı ve alt sandıklar açıldıktan sonra alt şelaleyi bağla.')
    b.rect(8,2,1,3,78).rect(7,3,1,2,78)
    b.rect(2,5,2,3,11).rect(6,5,2,3,12).put(1,8,21).put(6,9,21)
    b.channel([(0,0),(4,0),(4,3),(6,3),(6,7),(4,7),(4,10)])
    b.goal(76,1).collectible(3,24).goal(11,6).goal(12,6).goal(21,2);result.append(b)

    b=Board(219,'Çiçekli Taç','Blossom Crown','Cicekli-Tac',[
        'o...o...o','oo.ooo.oo','ooooooooo','ooooooooo','ooooooooo','.ooooooo.','.ooooooo.','ooooooooo','ooooooooo','ooooooooo','ooooooooo'],25,
        'Üç uçlu taç; çiçekli geniş üst bant, çift plastik panel ve çim kaplı sandıklarla tamamlanan değirmen kaidesi.',
        'Çiçekleri döküp alttaki çimi kaldır; orta beşli çalışma alanından kaideye special yollarını kur.',
        'Sandıkların üst çimi ayrı bir tur ister; iki değirmenin kanatlarından sonra un ve sandık renkleri işi kalır.')
    b.rect(0,2,9,2,58)
    for x in (1,5):b.rect(x,6,3,3,40)
    for x in (0,2):b.put(x,9,80)
    for x in (5,7):b.put(x,9,21).cover(x,9,2,2,42)
    b.goal(83,40).goal(40,18).goal(21,2).goal(58,18).goal(42,26);result.append(b)

    b=Board(220,'Büyük Festival','Grand Festival','Buyuk-Festival',[
        '..ooooo..','.ooooooo.','ooooooooo','ooooooooo','ooooooooo','ooooooooo','ooooooooo','ooooooooo','ooooooooo','ooooooooo'],29,
        'Boss sahnesi: kubbeli üst alan, iki uzun fener sütunu, yan kuş tribünleri ve porselen alt bant.',
        'Fenerleri yakarken iç koridorda güçlü eşleşmeler kur; kuş tribünlerinden kesintisiz toplama planla.',
        '620 HP / üç dalga sürerken porselen tabanı aç; hedef dışı plastik fırlatmaları ek baskı oluşturur.')
    for x in (0,8):b.rect(x,6,1,4,78)
    for x in (1,7):b.rect(x,3,1,7,75)
    b.rect(2,8,5,2,30).collectible(2,620).collectible(3,28).goal(75,18).goal(30,10)
    b.movableColumns={1:75,7:75};b.refill=[dict(obstacleId=75,initial=14,goal=18,extra=4)]
    result.append(b)
    return result

def scalar(text,key,value):
    text,count=re.subn(r'^  '+key+r':[^\n]*$',lambda _:f'  {key}: {value}',text,flags=re.M);assert count==1,key
    return text
def entries(text,key,rows):
    body=f'  {key}: []\n' if not rows else f'  {key}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+k+': '+str(v)+'\n' for j,(k,v) in enumerate(e.items())) for e in rows)
    text,count=re.subn(r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda _:body,text,flags=re.M);assert count==1,key
    return text
def model(b):
    from common import layer_model,feed
    d=dict(level=b.n,name=b.title,levelKey=f'LevelCL_{b.n:04d}',asset=f'Assets/_Project/Settings/ProLevels/LevelV1_{b.n*10:08d}.asset',width=b.w,height=b.h,moves=b.moves,kind=int(b.n==220),data=b.data,custom={k:[] for k in ('tubes','magnets','safes','ancientSeals','tarpStatues')},goals=b.goals)
    d['custom'].update(stackedObstacles=b.stacks,waterChannels=b.paths)
    layers=layer_model(d);d['top']=[ls[-1][0] if ls else 0 for ls in layers];d['topOrigins']=[ls[-1][1] if ls else -1 for ls in layers]
    slots,reached=feed(d)
    # Real authored starting tiles for movable/fixed shadows, never fictitious preview filling.
    for i in sorted(slots-reached):b.data['pinnedTileTypes'][i]=(i%b.w+2*(i//b.w))%4+1
    d.update(titleEn=b.en,slug=b.slug,rows=b.rows,idea=b.idea,opening=b.opening,after=b.after,refill=b.refill,pinReason='Authored start tiles in geometry shadows; colours do not form a pinned-only starting match.')
    return d
def write():
    if (HERE/'active-revision.json').exists():
        raise RuntimeError('A later revision is active. Use the author recorded in active-revision.json; do not overwrite it with v1.')
    bs=designs();first=not(HERE/'protected-before.json').exists()
    if first:
        assert list(map(int,re.findall(r'^    level: (\d+)$',CAT.read_text(),re.M)))==list(range(1,213))
        assert not any((P/f'LevelV1_{n*10:08d}.asset').exists() for n in range(213,221))
        before=HERE/'Before';before.mkdir()
        for p in (CAT,NAMES,ROOT/'Docs/LevelDesign/Design-State.md',ROOT/'Docs/LevelDesign/Memory.md',ROOT/'Docs/LevelDesign/Design-Feedback.md',ROOT/'Docs/LevelDesign/Design-Principles.md',ROOT/'Docs/LevelDesign/Level-Names.md'):(before/p.name).write_bytes(p.read_bytes())
        protected={str(p.relative_to(ROOT)):sha(p) for root in (A/'Settings',A/'Scripts',A/'Resources') for p in root.rglob('*') if p.is_file() and p not in (CAT,NAMES)}
        (HERE/'protected-before.json').write_text(json.dumps(protected,indent=2)+'\n')
    else:
        owned=json.loads((HERE/'designs.json').read_text());assert [d['level'] for d in owned]==list(range(213,221))
    ds=[model(b) for b in bs]
    cat=(HERE/'Before'/CAT.name).read_text();names=json.loads((HERE/'Before'/NAMES.name).read_text())
    for b,d in zip(bs,ds):
        asset=ROOT/d['asset'];meta=Path(str(asset)+'.meta');template=P/('LevelV1_00002100.asset' if b.n==220 else 'LevelV1_00002120.asset')
        text=template.read_text()
        for key,value in dict(m_Name=asset.stem,width=b.w,height=b.h,moves=b.moves,levelKind=d['kind'],usesCustomIntro=0,refillSpecialGuard=.75,oilSpreadMaxCells=0,movableSpawnFallbackMoves=0,movableSpawnFillColumns=1).items():text=scalar(text,key,value)
        for key,value in b.data.items():text=scalar(text,key,encode(value))
        for key in ('colorCrateInitialHits','tubes','magnets','safes','ancientSeals','lampStripSettings','paintCanBoxColors','tarpStatues','tutorialSteps','bossWaves'):text=entries(text,key,[])
        text=entries(text,'goals',b.goals);text=entries(text,'stackedObstacles',b.stacks)
        text=entries(text,'waterChannels',[dict(pathCellIndices=encode(p['pathCellIndices'])) for p in b.paths])
        # LevelData stores bool[] column flags, not per-column obstacle IDs.
        text=scalar(text,'movableSpawnColumns',bytes(int(x in b.movableColumns) for x in range(b.w)).hex())
        if b.n==220:
            text=scalar(text,'bossThrownObstacles',encode([8,12,13]))
            text=scalar(text,'bossAttackOilCount',2) # Current controller uses the pressure volley count, not an Oil-only type.
            text=scalar(text,'bossMaxPressureObstacles',8)
        guid=re.search(r'^guid: (\w+)',meta.read_text(),re.M)[1] if meta.exists() else uuid.uuid4().hex
        asset.write_text(text)
        if not meta.exists():meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
        cat+=f'  - chapter: 9\n    level: {b.n}\n    levelKey: LevelCL_{b.n:04d}\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n'
        names['entries'].append(dict(level=b.n,levelKey=f'LevelCL_{b.n:04d}',name=b.title,nameEn=b.en));d['guid']=guid
    CAT.write_text(cat);NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    (HERE/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    for d in ds:print(d['level'],d['name'],sum(d['data']['cells']),'active;',d['moves'],'moves;',sum(bool(p) for p in d['data']['pinnedTileTypes']),'real start pins')
if __name__=='__main__':write()
