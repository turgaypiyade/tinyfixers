"""v2: distinct play spaces; Bird quota follows accessible houses and other work."""
KEYS=('cells','obstacles','obstacleOrigins','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')
def make(n,title,en,mask,moves,bird_count,idea,beats):
    assert all(len(row)==9 for row in mask)
    h=len(mask)
    return dict(n=n,title=title,nameEn=en,w=9,h=h,moves=moves,birds=bird_count,difficulty='Zor' if n==205 else 'Normal',
        idea=idea,beats=beats,data={k:([int(c=='#') for row in mask for c in row] if k=='cells' else [-1 if k=='obstacleOrigins' else 0]*(9*h)) for k in KEYS},
        groups=[],seals=[],channels=[],walls=[],opening=[],paintColors=[],goals=[])
def put(d,x,y,oid,bw=1,bh=1):
    origin=y*9+x
    for dy in range(bh):
        for dx in range(bw):
            i=(y+dy)*9+x+dx
            assert d['data']['cells'][i] and not d['data']['obstacles'][i],(d['n'],x,y,oid,i)
            d['data']['obstacles'][i]=oid;d['data']['obstacleOrigins'][i]=origin
def birds(d,x,y,bw,bh):
    d['groups'].append(dict(x=x,y=y,width=bw,height=bh))
    for dy in range(bh):
        for dx in range(bw):put(d,x+dx,y+dy,78)
def goal(oid,amount):return dict(targetType=1,tileType=0,obstacleId=oid,collectibleId=0,iconOverride='{fileID: 0}',amount=amount)
def finish(d,extra):
    d['goals']=[dict(targetType=2,tileType=0,obstacleId=0,collectibleId=3,iconOverride='{fileID: 0}',amount=d['birds'])]+extra
    return d

a=make(202,'Boyalı Kuş Avlusu','Painted Bird Courtyard',[
    '..#####..','.#######.','#########','###...###','###...###','#########','#########','.#######.','..#####..'],26,18,
    'Halka avlunun hangi yanını önce açacaksın? İki boya kasasını boşalt; alttaki üç evi açık tutarak kuşları kısa serilerde topla.',[
    'Ortada 3×2 gölet boşluğu; iki yanda geçit, üstte ve altta geniş eşleşme yayları var.',
    'Boya kasaları halkanın iki alt omzunda. Her biri dokuz kutu taşır; tamamen boşalan iki kasa hedeftir.',
    'Alt kenarda tek 3×1 kuş grubu var. Üç üretici için hedef 18; açık evlere tekrar vurmayı diğer kanada geçmeden planla.'])
birds(a,3,8,3,1)
for x in (0,7):
    put(a,x,5,63,2,2)
    a['opening'] += [(5+dy)*9+x+dx for dy in range(2) for dx in range(2)]
finish(a,[goal(63,2)])

b=make(203,'Kuşlu Sevkiyat','Bird Delivery',[
    '#########','#########','#########','.#######.','..#####..','..#####..','..#####..','..#####..','..#####..','...###...'],27,12,
    'Duvarın aynı hücresini ilerletip bendi bir anda yık; dört kargo çatallı çıkışın iki yanından akar, ortadaki kuşlar yerinde kalır.',[
    'Geniş üst alan, altta iki kargo şeridine dönüşür. Yan cepler iki hücre genişliğinde: 2×2 eşleşme fırsatı var. Çıkış sütunları 4 ve 6 açık.',
    '3×2 bent tek duvar parçasıdır. Hücrelerin çatlağı ayrı ilerler; bir hücre beşinci vuruşa ulaşınca bütün bent çöker.',
    'Dört kargo iki dikey çift halinde bende dayanır. Üç evlik alt orta sütunda hedef 12 kuş; ana gösteri kargo akışıdır.'])
birds(b,4,7,1,3)
for y in (3,4):
    for x in (3,5):put(b,x,y,34);b['opening'].append(y*9+x)
region=[y*9+x for y in (5,6) for x in (3,4,5)]
origin=min(region)
for i in region:
    assert not b['data']['obstacles'][i]
    b['data']['obstacles'][i]=55;b['data']['obstacleOrigins'][i]=origin;b['data']['wallPieceIds'][i]=1
b['walls']=[dict(origin=origin,piece=1,obstacleId=55,cells=region)]
b['opening']+=region
finish(b,[goal(34,4),goal(55,1)])

c=make(204,'Kuşlu Akarsu','Birdsong Stream',[
    '..#####..','.#######.','#########','#########','######...','######...','...######','...######','.########','.#######.','..#####..'],28,16,
    'S biçimli kıyıyı dolaş: soldaki kuşları kısa bir seride topla, suyu üç kıvrımdan geçirip sağ kıyıdan çıkar, alt göletteki fenerleri kır.',[
    'Sol dış kıyıda dört evlik tek sütun var. Hedef 16; kanala ilerlemek için kuşları bütün bölüm boyunca açık tutmak gerekmiyor.',
    'Tek su yolu orta dirsekten sağ çıkışa gider. Kaynaktan kopuk açılmış bir parça su gelene kadar kuru kalır.',
    'Alt havzada 3×2 fener grubu dipte durur. Altı fener hedefi açılış adediyle aynı; ek fener üretimi planlanmadı.'])
birds(c,0,2,1,4)
path=[(4,0),(4,1),(4,2),(3,2),(2,2),(2,3),(2,4),(3,4),(4,4),(4,5),(4,6),(5,6),(6,6),(6,7),(6,8),(7,8),(8,8)]
c['channels']=[dict(pathCellIndices=[y*9+x for x,y in path])]
for y in (9,10):
    for x in (3,4,5):put(c,x,y,75)
finish(c,[goal(76,1),goal(75,6)])

d=make(205,'Mühürlü Kuş Parkı','Sealed Bird Park',[
    '###...###','####.####','#########','.#######.','..#####..','..#####..','.#######.','#########','#########','.#######.','..#####..'],27,24,
    'İki mühürden hangisini önce açacaksın? Altlarında pusulalar bekliyor. Merkezdeki roket sepetini erken tüketmek yerine doğru anda kullan.',[
    'Üstte iki kapalı park girişi; kırmızı ve yeşil başlayan mühürler her renkten sırasıyla 5 ve 6 taş ister. Kuş toplarken renk işi de ilerler.',
    'Her 2×2 mührün altında dört pusula var; kapak kalkmadan special isabeti pusulaya ulaşmaz. İki mühür ve sekiz pusula zorunlu hedef.',
    'Alt parkta tek 2×3 kuş grubu, hedef 24. Üstündeki roket sepeti yardımcıdır; hedef yapılmaz ve atışlarının belirli bir pusulayı kesin vuracağı varsayılmaz.'])
birds(d,3,8,2,3)
for x0,color,count in ((0,1,5),(7,3,6)):
    for y in (0,1):
        for x in (x0,x0+1):put(d,x,y,39)
    d['seals'].append(dict(originCellIndex=x0,width=2,height=2,startColor=color,redCount=count,yellowCount=count,greenCount=count,stackOrder=0))
put(d,4,7,38)
d['opening']=[i for i,o in enumerate(d['data']['obstacles']) if o==39]
finish(d,[goal(64,2),goal(39,8)])
DESIGNS=[a,b,c,d]
