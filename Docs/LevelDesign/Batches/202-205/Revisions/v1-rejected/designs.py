"""Four distinct BirdHouse reinforcements, using previously introduced obstacle families."""
KEYS=('cells','obstacles','obstacleOrigins','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')
def make(n,title,en,h,moves,birds,idea,beats):
    return dict(n=n,title=title,nameEn=en,w=9,h=h,moves=moves,birds=birds,difficulty='Zor' if n==205 else 'Normal',
        idea=idea,beats=beats,data={k:[-1 if k=='obstacleOrigins' else 1 if k=='cells' else 0]*(9*h) for k in KEYS},
        groups=[],seals=[],channels=[],opening=[],paintColors=[],goals=[])
def put(d,x,y,oid,bw=1,bh=1):
    i=y*9+x
    for dy in range(bh):
        for dx in range(bw):
            j=(y+dy)*9+x+dx
            assert d['data']['cells'][j] and not d['data']['obstacles'][j],(d['n'],x,y,oid,j)
            d['data']['obstacles'][j]=oid;d['data']['obstacleOrigins'][j]=i
def birds(d,x,y,bw,bh):
    d['groups'].append(dict(x=x,y=y,width=bw,height=bh))
    for dy in range(bh):
        for dx in range(bw):put(d,x+dx,y+dy,78)
def goal(oid,amount):return dict(targetType=1,tileType=0,obstacleId=oid,collectibleId=0,iconOverride='{fileID: 0}',amount=amount)
def finish(d,extra):
    d['goals']=[dict(targetType=2,tileType=0,obstacleId=0,collectibleId=3,iconOverride='{fileID: 0}',amount=d['birds'])]+extra
    return d

a=make(202,'Boyalı Kuş Avlusu','Painted Bird Courtyard',10,30,50,
    'Sağ kenardaki kuş sırasını çalıştırırken sol alt taş kapağı kır; iki boya kasasının çevresinde yeni eşleşme yüzleri aç.',[
    'Sağ kenarda tek sütunda altı kuş evi; açık yan şerit sürekli yeniden beslenir.',
    'Sol alttaki 2×4 oyma taşı kapağını kır; altındaki iki bitişik boya kasasına yaklaş.',
    'Boya kasaları dokuzar kutu içerir: normal eşleşme bir, special iki kutu düşürür; hedef iki kasanın tamamen boşalması.'])
a['data']['cells'][0]=a['data']['cells'][8]=0
birds(a,8,2,1,6)
for y in (6,7):
    for x in range(4):put(a,x,y,28);a['opening'].append(y*9+x)
for x in (0,2):put(a,x,8,63,2,2)
finish(a,[goal(28,8),goal(63,2)])

b=make(203,'Kuşlu Sevkiyat','Bird Delivery',11,30,50,
    'Alt kenardaki iki kuş sırasının arasından kargo çıkar: merkezdeki taş rampasını açıp altı sandığı üç açık çıkışa indir.',[
    'Alt sol ve alt sağda birer 3×1 kuş grubu; ortadaki üç sütun kargo çıkışı için açık.',
    'Altı kargo merkezde 3×2 dizilir, üç katlı oyma taşı rampasının üzerinde durur; başlangıç konumları düşüp değişmez.',
    'Kırılan her taş kargoya alan açar. Alt ceplerde dolum kapak açıldıktan sonra gelir; kuşlar çıkış sütunlarını kapatmaz.'])
b['data']['cells'][0]=b['data']['cells'][8]=0
birds(b,0,10,3,1);birds(b,6,10,3,1)
for y in (4,5):
    for x in (3,4,5):put(b,x,y,34);b['opening'].append(y*9+x)
for y in (6,7,8):
    for x in (3,4,5):put(b,x,y,28);b['opening'].append(y*9+x)
finish(b,[goal(34,6),goal(28,9)])

c=make(204,'Kuşlu Akarsu','Birdsong Stream',11,30,50,
    'Solda iki sütunlu kuş bloğunu beslerken sağdaki kıvrımlı su yolunu kaynaktan çıkışa aç; fenerleri alt köşede kır.',[
    'Sol tarafta 2×4 kuş grubu; iki yanında eşleşme şeridi ve altına açık dolum rotası var.',
    'Sağdaki tek kanal üç kıvrımla alta iner. Ortadan açılan hücre, kaynak bağlanana kadar kuru kalır.',
    'Sağ alt 2×4 fener bloğu açılışta dipte durur. Sekiz fener hedefi başlangıç adediyle aynı; ek fener kotası yok.'])
for i in (0,1,9,81,90,91):c['data']['cells'][i]=0
birds(c,1,4,2,4)
path=[(4,0),(4,1),(5,1),(6,1),(6,2),(6,3),(5,3),(4,3),(4,4),(4,5),(5,5),(6,5),(6,6),(6,7),(5,7),(4,7),(4,8),(4,9),(4,10)]
c['channels']=[dict(pathCellIndices=[y*9+x for x,y in path])]
for y in range(7,11):
    for x in (7,8):put(c,x,y,75)
finish(c,[goal(76,1),goal(75,8)])

d=make(205,'Mühürlü Kuş Parkı','Sealed Bird Park',11,28,60,
    'Hazır Pulse + roketle pusula kuşağını yar; açık kuş grubundan toplarken renk mührünü çöz ve ikinci grubu aç.',[
    'Alt bölgede iki 2×4 kuş bloğu; sol grup 2×4 antik mührün altında, sağ grup açık ama üstünde pusula kuşağı var.',
    'İki satırlık 14 pusula yalnız special alır. Üstteki hazır Pulse + roket ilk geçidi açar; kalan pusulalar için yeni special üret.',
    'Mühür her renkten 8 taş ister. Açılınca sekiz kuş evi daha görünür; normal hücre sayısı mührün kalkmasıyla artmaz.'])
for i in (0,8):d['data']['cells'][i]=0
birds(d,1,6,2,4);birds(d,6,6,2,4)
for y in (4,5):
    for x in range(1,8):put(d,x,y,39)
d['seals']=[dict(originCellIndex=55,width=2,height=4,startColor=1,redCount=8,yellowCount=8,greenCount=8,stackOrder=0)]
for x,special,color in ((3,4,1),(4,2,3)):
    i=3*9+x;d['data']['pinnedSpecialTypes'][i]=special;d['data']['pinnedTileTypes'][i]=color
# Pulse+Line centered on the line: three complete rows plus three complete columns.
d['opening']=[i for i,o in enumerate(d['data']['obstacles']) if o==39 and (i//9 in (2,3,4) or i%9 in (3,4,5))]
finish(d,[goal(39,14),goal(64,1)])
DESIGNS=[a,b,c,d]
