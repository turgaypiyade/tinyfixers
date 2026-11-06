"""229 replacement only: static authoring, never builds/simulates/publishes."""
from pathlib import Path
from collections import Counter
import json,sys,re
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
sys.path.insert(0,str(BATCH))
import design as shared
ROOT=shared.ROOT;ASSET='Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset'
sha=shared.sha

def make():
    b=shared.Board(229,'Rüzgâr Burgusu','Crystal Whirl','Ruzgar-Burgusu',['ooooooooo']*11,28,
        'Soldan merkeze kıvrılan bitişik oyma omurgası; sağda iki porselen sütunu, tabanda iki jel presi ve iki Rodos heykeli.',
        'Oyma omurgasının hangi yüzünü açıp jeli oyuna katacağına karar ver; iki presi birlikte vuracak special hattını hazırla.',
        'Oyma taşları kalkınca altındaki 12 jel kaynağı açılır. Presler çalıştığında merkezdeki iki sütunu açıp jelle boyar.',
        'Jeli bütün 99 hücreye taşı; iki Rodos heykelini special ile bitir ve sağdaki 12 porseleni temizle.')
    spine=[]
    for x,y in [(1,2),(2,4),(3,6)]:
        for yy in range(y,y+2):
            for xx in range(x,x+2):b.put(xx,yy,46).overlay(xx,yy,28);spine.append(yy*9+xx)
    b.rect(6,3,2,6,30).put(3,9,59).put(4,9,59).put(5,9,70).put(7,9,70)
    b.goal(46,99).goal(28,12).goal(70,2).goal(30,12)
    b.d.update(difficulty='Super Hard',revision='v3-229-gel-sculpture',spine=sorted(spine),
        launcherColumns=[3,4],previewStage='OYMA TAŞLARININ ALTINDAKİ JEL',
        reminder='GrassFlower58 ve GrassWaterLily69 bütün açık engel havuzunun parçasıdır; bu229 için zorunlu değildir.',
        selectedFamilies=[46,28,59,70,30])
    d=b.finish([28],
        'Super Hard: 12 üç vuruşlu oyma, iki special isteyen Rodos, iki presi çalıştırma ve 99 hücre jel kaplama. 28 hamle ilk tahmin.',
        ['Çim altında farklı bir sandık: kullanıcı aynı katman fikrini değiştirmek istedi; bu yönde ilerlenmedi.',
         'Bir başka Dimerdal/değirmen düzeni: önceki229 ve komşu228/230 ile üretim işini tekrarlardı.',
         'GrassFlower veya Grass Lily’yi hatırlatma üzerine zorunlu eklemek: bu229’da beşinci bir bağımsız işi gereksiz eklerdi.',
         'Uzun kapalı C çerçevesi: tavan altında beslenmeyen iç cepler oluşturuyordu; iki hücre kalınlığında çapraz omurga seçildi.'])
    assert d['openingGeometry']==dict(tileSlots=63,naturallyFed=63,shadowSeeded=0,authoredPins=0,intentionallyEmpty=[])
    return d

def write():
    protected=json.loads((HERE/'protected-before.json').read_text())
    assert not [p for p,h in protected.items() if sha(ROOT/p)!=h], 'Unrelated project files changed since snapshot.'
    p=ROOT/ASSET;owned=HERE/'owned-hashes.json'
    expected=json.loads(owned.read_text())[ASSET] if owned.exists() else sha(HERE/'Before'/ASSET)
    assert sha(p)==expected,'229 was edited after snapshot; refusing overwrite.'
    d=make();d['guid']=re.search(r'^guid: (\w+)$',Path(str(p)+'.meta').read_text(),re.M)[1]
    t=p.read_text()
    for k,v in dict(width=9,height=11,moves=d['moves'],levelKind=0,movableSpawnFallbackMoves=0,movableSpawnFillColumns=1).items():t=shared.scalar(t,k,v)
    for k,v in d['data'].items():t=shared.scalar(t,k,shared.encode(v))
    for k in shared.CUSTOM:t=shared.entries(t,k,d['custom'][k])
    t=shared.entries(t,'goals',d['goals']);t=shared.scalar(t,'movableSpawnColumns','00'*9)
    p.write_text(t);(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    models=json.loads((BATCH/'designs.json').read_text());models=[d if x['level']==229 else x for x in models]
    (BATCH/'designs.json').write_text(json.dumps(models,ensure_ascii=False,indent=2)+'\n')
    own=json.loads((BATCH/'owned-hashes.json').read_text());own[ASSET]=sha(p)
    (BATCH/'owned-hashes.json').write_text(json.dumps(own,indent=2)+'\n')
    owned.write_text(json.dumps({ASSET:sha(p)},indent=2)+'\n')
    active=json.loads((BATCH/'active-revision.json').read_text());active.setdefault('additionalLevelRevisions',{})['229']='Revisions/v3-229-gel-sculpture'
    (BATCH/'active-revision.json').write_text(json.dumps(active,indent=2)+'\n')
    print('229 replaced:',d['moves'],'moves,99 active,63/63 natural,0 pins; gel99/stone12/Rhodes2/porcelain12. Catalog/meta/names preserved.')

if __name__=='__main__':write()
