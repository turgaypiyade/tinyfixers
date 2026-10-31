"""Three deliberate nature compositions; data only, no gameplay execution."""
from collections import Counter

KEYS = ('cells','obstacles','obstacleOrigins','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')

def design(n,title,en,mask,moves,idea,beats):
    assert all(len(row)==9 for row in mask)
    size=9*len(mask)
    return dict(n=n,title=title,nameEn=en,w=9,h=len(mask),moves=moves,
        difficulty='Süper Zor' if n==199 else 'Boss Duel',idea=idea,beats=beats,
        data={k:([int(c=='#') for row in mask for c in row] if k=='cells' else [-1 if k=='obstacleOrigins' else 0]*size) for k in KEYS},
        stacks=[],magnets=[],ancientSeals=[],waves=[],goals=[],opening=[])

def put(d,x,y,oid):
    i=y*9+x
    assert d['data']['cells'][i] and d['data']['obstacles'][i]==0,(d['n'],x,y)
    d['data']['obstacles'][i]=oid;d['data']['obstacleOrigins'][i]=i

def obstacle_goal(oid,amount):
    return dict(targetType=1,tileType=0,obstacleId=oid,collectibleId=0,iconOverride='{fileID: 0}',amount=amount)

def boss(d,hp,player,pressure,throws,waves):
    d.update(hp=hp,playerHp=player,pressureCap=pressure,throws=throws,oilCap=8 if d['n']==90 else 10)
    d['goals']=[dict(targetType=2,tileType=0,obstacleId=1,collectibleId=2,iconOverride='{fileID: 21300000, guid: bd8a2a2aabdf46fcabea5eb683bc76d9, type: 3}',amount=hp)]
    d['waves']=[dict(hpWeight=weight,attackDamageBase=damage,oilCount=count,oilEveryMoves=4,
        characterProfile='{fileID: 0}',bodyTint='{r: 1, g: 1, b: 1, a: 1}') for weight,damage,count in waves]

def make():
    a=design(90,'Dalga Düellosu','Tidal Duel',[
        '...#####.','..#######','.########','#########','#####..##',
        '####...##','####.....','#####....','.#######.','..#######'],30,
        'Kayalık kıvrımı açıp dalganın içini genişlet; erken kalkan mı, daha büyük bir saldırı mı?',[
        'Sol tarafta eşleşme kur; kıvrımın ağzındaki tek roket sepeti komşu kırmızı, sarı ve mavi eşleşmeleri ödüllendirir.',
        'Oyma taşını kırdıkça kıvrım normal taş alanına katılır. Alt dudaktaki altı kalkan savunma fırsatıdır.',
        'İlk rakip engel atmaz; sonraki ikisinde porselen ve yağ baskısı başlar. Tek hedef rakiplerin toplam canıdır.'])
    crest=[(4,2),(5,2),(6,2),(7,2),(8,2),(8,3),(8,4),(8,5),(7,5),(7,4),(7,3),(6,3),(5,3)]
    for x,y in crest:put(a,x,y,28)
    put(a,4,3,38)
    for y in (8,9):
        for x in (5,6,7):put(a,x,y,35)
    a['opening']=[y*9+x for x,y in crest]+[31]
    boss(a,420,180,6,[14,30],[(.30,7,0),(.33,8,1),(.37,8,2)])

    b=design(100,'Yaprak Düellosu','Leaf Duel',[
        '..#####..','.#######.','#########','#########','#########',
        '#########','.#######.','.#######.','..#####..','...###...'],32,
        'Yaprağın iki yüzünde çalış; mıknatıs damarını birleştirerek ortada uzun bir eşleşme hattı kazan.',[
        'Üst mıknatıs ucu hemen erişilir; alt uca yan damarı açarak inilir. Mıknatıs yanında bekletilen special kafeslenebilir; üretip hemen kullanmak değerli.',
        'İki basamaklı yan damar toplam on iki iki-aşamalı sandıktan oluşur. Damar ve mıknatıs açılınca yaprak tek geniş alana dönüşür.',
        'Sapın altı altı kalkanlık savunma bölgesidir. Üç rakip 144 + 158 + 178 can; son rakip daha sert vurur.'])
    branches=[(0,4),(1,4),(1,5),(2,5),(2,6),(3,6)]
    for x,y in branches+[(8-x,y) for x,y in branches]:put(b,x,y,5)
    b['magnets']=[dict(pathCellIndices=[y*9+4 for y in range(1,8)])]
    for y in (8,9):
        for x in (3,4,5):put(b,x,y,35)
    b['opening']=[y*9+4 for y in range(1,8)]
    boss(b,480,210,7,[14,30,40],[(.30,7,1),(.33,8,1),(.37,9,2)])

    c=design(199,'Deniz Kabuğu','Seashell',[
        '..#####..','.#######.','#########','#########','#########','#########',
        '#########','#########','#########','.#######.','..#####..'],31,
        'Jelli merkezden kabuğu çatlat; normal vuruş isteyen dış kabuk ile special isteyen iç kıvrımı farklı planla.',[
        'Jel üzerindeki hazır TNT, hemen altındaki üç pusulalık zayıf ağzı açabilir. Ortadaki boş odalar ancak bir dolum yolu açılınca dolar.',
        'Dış kıvrımda 17 üç-vuruşlu oyma taşı; içte ve ağızda 17 yalnız-special pusula. Renk toplayan tek merkez mühür her renkten 8 taş ister.',
        'Alt dudakta dört yumurta-kuş yardım eder. Bütün 87 aktif hücreyi jele kaplamak gerekir; jelsiz special tek başına kaplama sağlamaz.'])
    spiral=([(x,2) for x in range(1,8)]+[(7,y) for y in range(3,10)]+
        [(x,9) for x in range(6,0,-1)]+[(1,y) for y in range(8,3,-1)]+
        [(x,4) for x in range(2,6)]+[(5,y) for y in range(5,8)]+[(4,7),(3,7)])
    assert len(spiral)==34 and len(set(spiral))==34
    gate={(3,2),(4,2),(5,2)}
    for j,(x,y) in enumerate(spiral):
        oid=39 if j>=20 or (x,y) in gate else 28
        if (x,y) in gate:
            put(c,x,y,46)
            c['stacks'].append(dict(originCellIndex=y*9+x,obstacleId=oid,stackOrder=0,initialRemainingHits=0))
        else:put(c,x,y,oid)
    put(c,4,1,46)
    c['data']['pinnedTileTypes'][13]=3;c['data']['pinnedSpecialTypes'][13]=4
    c['ancientSeals']=[dict(originCellIndex=48,width=2,height=2,startColor=1,redCount=8,yellowCount=8,greenCount=8,stackOrder=0)]
    for x in (2,3,4,5):put(c,x,10,45)
    c['opening']=[y*9+x for x,y in gate]
    c['goals']=[obstacle_goal(46,sum(c['data']['cells'])),obstacle_goal(39,17),obstacle_goal(64,1)]
    c['oilCap']=0
    return [a,b,c]

DESIGNS=make()
