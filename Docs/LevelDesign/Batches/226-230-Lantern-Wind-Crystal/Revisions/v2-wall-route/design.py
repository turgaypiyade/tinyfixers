"""Only level226 and its three required layered-wall support files are in scope."""
from pathlib import Path
import sys,json,hashlib,re
HERE=Path(__file__).resolve().parent
BATCH=HERE.parents[1]
sys.path.insert(0,str(BATCH))
import design as shared
ROOT=shared.ROOT
sha=shared.sha
ASSET='Assets/_Project/Settings/ProLevels/LevelV1_00002260.asset'
RUNTIME=['Assets/_Project/Scripts/Core/LevelData.cs','Assets/_Project/Scripts/Grid/GridSpawner.cs','Assets/_Project/Scripts/Editor/LevelDataEditor.cs']

def make():
    b=shared.Board(226,'Rüzgâr Kemeri','Windlit Arch','Ruzgar-Kemeri',[
        'oooooo...','oooooo...','oooooo...','ooo.oo...',
        'ooo.oo.oo','ooo.oo.oo','ooo.oo.oo','ooo.oo.oo',
        'ooo.oo...','oooooo...','oooooo...'],28,
        'Tahtanın sağ kolundan tabana dönen tek duvar yolu; sağdaki ayrı adada iki değirmen. Üstte geniş bir başlangıç alanı var.',
        'Yolun üst ağzındaki veya sol alt ucundaki tek bir duvar hücresine odaklan; değirmen adasına uzanacak yatay special hattını hazırla.',
        'Bir hücre beşinci vuruşu alınca tek parça duvar boyunca zincirleme yıkım ilerler. Yolun bütün 24 hücresinden gerçek fenerler açılır.',
        '24 feneri iki aşamada temizle; yalnız special ile erişilen iki değirmenin çuvallarından toplam 16 unu bitir.')
    cells=sorted({y*9+x for y in range(3,11) for x in (4,5)}|{y*9+x for y in (9,10) for x in range(4)})
    origin=min(cells)
    for i in cells:
        b.put(i%9,i//9,75).overlay(i%9,i//9,55)
        b.d['custom']['stackedObstacles'][-1]['wallPieceOriginPlusOne']=origin+1
        b.d['data']['wallPieceIds'][i]=1
    b.put(7,4,80).put(7,6,80).goal(55,1).goal(75,24).goal(83,16)
    b.d.update(walls=[dict(origin=origin,obstacleId=55,pieceId=1,cells=cells)],
        movableColumns=[4,5],previewStage='TEK DUVAR YOLU YIKILINCA',flourPerSack=8,
        reference='13/14 + 16 kullanıcı düzeltmesi: zincirleme yol, altında gerçek fenerler ve special ile erişilen ayrı değirmen adası.',
        revision='v2-wall-route',wallRoute=cells,millIsland=[y*9+x for y in range(4,8) for x in (7,8)])
    return b.finish([55],
        '226 rahatlama: tek duvar parçasında 5 odaklı vuruş, 24 örtülü fener ve adadaki iki değirmen. 28 hamle ilk ayar; oynanmadı.',
        ['Sağ üstte tavan bloğu: kullanıcı reddetti; altında boş ve beslenmeyen alan bırakıyordu.',
         'Tek hücrelik uzun yol: altındaki fenerlere eşleşme erişimini daraltırdı; yol iki hücre genişliğinde kuruldu.',
         'Sol alt değirmenleri ana taş alanına bitiştirmek: kullanıcı special ile ulaşılacak ayrı alan istedi.'])

def write():
    protected=json.loads((HERE/'protected-before.json').read_text())
    assert not [r for r,h in protected.items() if sha(ROOT/r)!=h], 'Unrelated files changed since this revision snapshot.'
    ds=json.loads((BATCH/'designs.json').read_text());old=next(d for d in ds if d['level']==226)
    own=HERE/'owned-hashes.json'
    if own.exists():
        for rel,h in json.loads(own.read_text()).items():assert sha(ROOT/rel)==h,('User edit; refusing overwrite',rel)
    else:
        assert sha(ROOT/ASSET)==sha(HERE/'Before'/ASSET),'Current226 differs from the saved revision input.'
    d=make();d['guid']=old['guid'];ds=[d if x['level']==226 else x for x in ds]
    t=(ROOT/ASSET).read_text()
    for k,v in dict(width=d['width'],height=d['height'],moves=d['moves'],movableSpawnFallbackMoves=0,movableSpawnFillColumns=1).items():t=shared.scalar(t,k,v)
    for k,v in d['data'].items():t=shared.scalar(t,k,shared.encode(v))
    for k in shared.CUSTOM:t=shared.entries(t,k,d['custom'][k])
    t=shared.entries(t,'goals',d['goals']);t=shared.scalar(t,'movableSpawnColumns',''.join('01' if x in d['movableColumns'] else '00' for x in range(d['width'])))
    (ROOT/ASSET).write_text(t)
    (BATCH/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    previous=json.loads((BATCH/'owned-hashes.json').read_text());previous[ASSET]=sha(ROOT/ASSET)
    (BATCH/'owned-hashes.json').write_text(json.dumps(previous,indent=2)+'\n')
    own.write_text(json.dumps({r:sha(ROOT/r) for r in [ASSET]+RUNTIME},indent=2)+'\n')
    (BATCH/'active-revision.json').write_text(json.dumps(dict(revision='v2-wall-route',path=str(HERE.relative_to(BATCH)),levels=[226],runtimeAllowed=RUNTIME),indent=2)+'\n')
    print('226 v2:',sum(d['data']['cells']),'active, one24-cell wall,24 buried lanterns, isolated8-cell mill island;',d['openingGeometry'])

if __name__=='__main__':write()
