"""Only234 layered revision. Preserve the user-liked grid and current other assets."""
from author import *
from collections import Counter

REV=HERE/'Revisions/v2-layered'


def make_v2():
    d=json.loads((REV/'Before/Docs/LevelDesign/Batches/234-Paint-Wave/design.json').read_text())
    old=make()
    assert d['data']['cells']==old['data']['cells']
    cells=[y*8+x for y in range(5,11)for x in(0,1)]+[y*8+x for y in(3,4)for x in(6,7)]
    assert len(cells)==len(set(cells))==16
    for i in cells:
        assert d['data']['cells'][i] and d['data']['obstacles'][i]in(0,11,12)
        d['data']['obstacles'][i]=25;d['data']['obstacleOrigins'][i]=i
        d['custom']['stackedObstacles'].append(dict(originCellIndex=i,obstacleId=40,stackOrder=1,initialRemainingHits=0))
        d['custom']['stackedObstacles'].append(dict(originCellIndex=i,obstacleId=28,stackOrder=2,initialRemainingHits=0))
    d['goals']=[]
    for o,count in[(21,2),(63,2),(28,16),(40,16),(25,16)]:
        d['goals'].append(dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride=shared.base.icon(o),amount=count))
    d.update(idea='Aynı dalga maskesinde iki ayrı çalışma yüzü: solda renkli kapak→boya;alt sol kenar ve sağ omuzda oyma→iki-hit plastik→çamur.',
        firstDecision='Üst soldaki renkli kapakları açıp boya işini mi başlatacaksın,yoksa sağ omuz/alt kenarın üç katmanını mı ilerleteceksin?',
        change='16oyma hücresi kalkınca henüz tamamlanmamış16iki-aşamalı plastik açılır;onlar gidince16iki-hit çamur yuvası görünür. Açılış kombosu işi tek katmanda bitirmez.',
        remaining='İki ColorChest veiki PaintCanBox dışında16oyma,16iki-aşamalı plastik,16çamur zorunludur. Üç katman aynı anda açık sayılmaz.',
        mechanics=['ColorChest → PaintCanBox','SculptingStone → PlasticTwoStage → Mud'],
        previewRemoved=[21,28],previewStage='İLK ÖRTÜLER KALKINCA',
        layeredCells=cells,revision='v2-layered',
        pinReason='Yalnız(0,4)normal yuvası doğal açılış gölgesidir. Altında(0,5)oyma kapağı vealt katmanlar başlangıç desteğidir. Başka taş/special pinlenmez.',
        budget='24hamle korundu;hamle kısarak zorlaştırılmadı. 16×(3oyma+2plastik+2çamur)ve2×(5renkli kapak+9kova)ham hücre-isabet yüküdür;aynı special birden çok hücreyi ilerletebilir,bunlar oyuncu hamlesi sayısı değildir. Yeni ayar henüz oynanmadı.',
        alternatives=d['alternatives']+['Yalnız sol sütuna daha çok HP:tek dikey combo aynı işi sürdürürdü. Sağ omuz da ayrı bir üç-katman yüzü oldu.',
            'Altı hedef:iki renkli tek-hit plastikler dinlendirilip orta katmanda tekID40kullanıldı;hedef listesi beşte kaldı.'])
    slots,reach=feed(d);assert slots-reach=={32}
    d['openingGeometry']=dict(activeCells=74,normalSlots=len(slots),naturallyFed=len(reach),shadowPins=[32],specialPins=0,intentionallyEmpty=[])
    return d


def write_v2():
    baseline=json.loads((REV/'protected-before.json').read_text());rel=str(ASSET.relative_to(ROOT))
    ownpath=REV/'owned-hashes.json';own=json.loads(ownpath.read_text())if ownpath.exists()else{}
    for p,h in baseline.items():assert sha(ROOT/p)==own.get(p,h),('External edit;stop',p)
    d=make_v2();source=(REV/'Before'/rel).read_text()
    for k,v in d['data'].items():source=shared.scalar(source,k,shared.encode(v))
    source=shared.entries(source,'stackedObstacles',d['custom']['stackedObstacles'])
    source=shared.entries(source,'goals',d['goals'])
    ASSET.write_text(source)
    (HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    ownpath.write_text(json.dumps({rel:sha(ASSET)},indent=2)+'\n')
    (HERE/'active-revision.json').write_text(json.dumps(dict(revision='v2-layered',author='revise_v2.py',reason='User playedv1andfound it too easy;grid preserved.'),indent=2)+'\n')
    print(d['revision'],d['openingGeometry'])


if __name__=='__main__':write_v2()
