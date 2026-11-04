"""Revise only213/215/217/218 from this revision's current-state snapshots."""
from pathlib import Path
import copy,hashlib,json,re,sys
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
ROOT=next(p for p in HERE.parents if(p/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').exists())
sys.path.insert(0,str(BATCH))
from design import scalar,entries,encode
from common import layer_model,feed,footprint
SELECTED=(213,215,217,218)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def put(d,x,y,o):
    w=d['width'];origin=y*w+x;fw,fh=footprint(o)
    assert x+fw<=w and y+fh<=d['height']
    for dy in range(fh):
        for dx in range(fw):
            i=origin+dy*w+dx;assert d['data']['cells'][i] and not d['data']['obstacles'][i],(d['level'],o,x,y)
            d['data']['obstacles'][i]=o;d['data']['obstacleOrigins'][i]=origin
def clear(d,ids):
    for i,o in enumerate(d['data']['obstacles']):
        if o in ids:d['data']['obstacles'][i]=0;d['data']['obstacleOrigins'][i]=-1
def rect(d,x,y,w,h,o):
    for yy in range(y,y+h):
        for xx in range(x,x+w):put(d,xx,yy,o)
def goal(o,n):return dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=n)
def lamp(d,pid,points,colors,shift,reverse):
    w=d['width'];cells=[y*w+x for x,y in points];origin=min(cells)
    assert len(points)==len(colors)
    for i,c in zip(cells,colors):
        assert d['data']['cells'][i] and not d['data']['obstacles'][i]
        d['data']['obstacles'][i]=79;d['data']['obstacleOrigins'][i]=origin;d['data']['wallPieceIds'][i]=pid;d['data']['lampColorByCell'][i]=c
    d['custom']['lampStripSettings'].append(dict(pieceId=pid,shift=shift,reverse=reverse))
def path(d,points):
    indices=[];w=d['width']
    for a,b in zip(points,points[1:]):
        dx=(b[0]>a[0])-(b[0]<a[0]);dy=(b[1]>a[1])-(b[1]<a[1]);assert dx==0 or dy==0
        assert dy>=0
        for k in range(abs(b[0]-a[0])+abs(b[1]-a[1])):indices.append((a[1]+dy*k)*w+a[0]+dx*k)
    indices.append(points[-1][1]*w+points[-1][0]);return dict(pathCellIndices=indices)
def models():
    ds=json.loads((HERE/'designs.json').read_text())
    for d in ds:
        n=d['level']
        if n not in SELECTED:continue
        d['custom']['lampStripSettings']=[]
        if n==213:
            # Two upright cartridges form the basket sides; no isolated helper in the centre.
            for x in (2,6):
                for y in (9,10):
                    i=y*9+x;assert d['data']['obstacles'][i]==40
                    d['data']['obstacles'][i]=0;d['data']['obstacleOrigins'][i]=-1
                put(d,x,9,59)
            d['goals']=[goal(75,18),goal(30,8),goal(40,6),goal(46,61)];d['moves']=24
            d['idea']='Balon kubbesi ve fener ipleri korunur; sepetin iki yanı yukarı ateşleyen jel kartuşlarıdır.'
            d['opening']='Kubbeden special üretip sepet kartuşlarını çalıştır; kapaklar fener hatlarını açarak yukarı ilerler.'
            d['after']='Jel sepetten gövdeye yayılır; 18 fener kotası, porselen taç ve altı iki-hit plastik bittiğinde tüm61 hücreyi kapla.'
        elif n==215:
            clear(d,{9,11,12,40,21})
            # Keep both outside lanes and the three middle columns open from top to
            # bottom. Every two-cell-wide chest has a feed face on both sides.
            for x in (1,6):
                rect(d,x,0,2,2,30);rect(d,x,2,2,4,40)
                put(d,x,6,21);put(d,x,8,21)
            for x in (0,2):put(d,x,10,62)
            for x in (5,7):put(d,x,10,61)
            d['custom']['stackedObstacles']=[]
            for i,o in enumerate(d['data']['obstacles']):
                if o==21:d['custom']['stackedObstacles'].append(dict(originCellIndex=i,obstacleId=42,stackOrder=1,initialRemainingHits=0))
            d['goals']=[goal(46,99),goal(40,16),goal(21,4),goal(42,16),goal(30,8)]
            d['idea']='Düz atölyede porselen ve iki-hit plastik sütunlar, çim kaplı dört sandık ve dipte karşılıklı jel kartuşları.'
            d['opening']='Üst geniş alanda special kur; alt kenardaki kartuşlara dikey atış ulaştırıp jel yayılmasını başlat.'
            d['after']='Jeli99 hücreye yayarken sütunları ve çimi aç; dört sandığın kapak ve ampul işi orta koridorun iki yanında sürer.'
        elif n==217:
            clear(d,{13})
            lamp(d,1,[(3,1),(2,1),(1,1),(1,2)],[1,1,2,5],1,0)
            lamp(d,2,[(5,1),(6,1),(7,1),(7,2)],[3,3,4,5],1,1)
            d['goals']=[goal(83,40),goal(79,2),goal(30,5),goal(40,13)]
            d['idea']='Kum saati omuzlarında iki hareketli L lamba şeridi; dar bel ve alt ayaklarda iki değirmen.'
            d['opening']='Porselen tacı açarken her hamlede kayan renkleri izle; mor uçlar için special hazırla.'
            d['after']='İki çerçeve bitince omuzlar açılır; belden alt değirmenlere special taşı ve çuvallardan gelen unu temizle.'
        else:
            clear(d,{11,12})
            lamp(d,1,[(3,5),(2,5),(2,6),(2,7),(3,7)],[5,1,1,3,3],0,0)
            lamp(d,2,[(6,5),(7,5),(7,6),(7,7),(6,7)],[2,2,4,4,5],1,1)
            d['custom']['waterChannels']=[path(d,[(0,0),(4,0),(4,3),(5,3),(5,8),(4,8),(4,10)])]
            d['goals']=[goal(76,1),dict(targetType=2,tileType=0,obstacleId=0,collectibleId=3,iconOverride='{fileID: 0}',amount=24),goal(79,2),goal(21,2)]
            d['idea']='Kıvrılan nehrin iki kıyısında C biçimli lamba şeritleri; sağ üstte kuş kıyısı ve altta iki sandık.'
            d['opening']='Soldaki sabit renkleri çöz veya sağdaki kayan şeridin rengini yakala; kuş kıyısına seri isabet kur.'
            d['after']='Kanal şeritlere değmeden aralarından iner; iki çerçeve ve kıyı sandıkları açıldıktan sonra alt şelaleyi bağla.'
        d['data']['pinnedTileTypes']=[0]*(d['width']*d['height'])
        ls=layer_model(d);d['top']=[c[-1][0] if c else 0 for c in ls];d['topOrigins']=[c[-1][1] if c else -1 for c in ls]
        slots,reached=feed(d)
        for i in slots-reached:d['data']['pinnedTileTypes'][i]=(i%d['width']+2*(i//d['width']))%4+1
        d['revision']='v2-lamps-launchers'
    return ds
def main():
    record=HERE/'revision.json'
    if record.exists():
        prior=json.loads(record.read_text())
        for rel,digest in prior['levelHashes'].items():assert sha(ROOT/rel)==digest,('Level edited since this revision; inspect before rewriting',rel)
    ds=models();hashes={}
    for d in ds:
        if d['level'] not in SELECTED:continue
        asset=ROOT/d['asset'];text=(HERE/asset.name).read_text()
        for key,value in d['data'].items():text=scalar(text,key,encode(value))
        text=scalar(text,'moves',d['moves']);text=entries(text,'goals',d['goals'])
        for key in ('stackedObstacles','lampStripSettings'):text=entries(text,key,d['custom'][key])
        text=entries(text,'waterChannels',[dict(pathCellIndices=encode(p['pathCellIndices'])) for p in d['custom']['waterChannels']])
        asset.write_text(text);hashes[d['asset']]=sha(asset)
        print(d['level'],d['moves'],'moves;',sum(bool(p) for p in d['data']['pinnedTileTypes']),'pins;',len(d['goals']),'goals')
    (BATCH/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    record.write_text(json.dumps(dict(revision='v2-lamps-launchers',lampStripLevels=[217,218],gelLauncherLevels=[213,215],levelHashes=hashes),indent=2)+'\n')
    (BATCH/'active-revision.json').write_text(json.dumps(dict(revision='v2-lamps-launchers',author='Revisions/v2-lamps-launchers/revise.py',verifier='Revisions/v2-lamps-launchers/verify_revision.py'),indent=2)+'\n')
if __name__=='__main__':main()
