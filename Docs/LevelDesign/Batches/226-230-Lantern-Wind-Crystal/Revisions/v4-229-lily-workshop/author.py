"""Only229 data authoring. No Unity build or gameplay simulation."""
import sys,json,re,shutil,hashlib
from pathlib import Path
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
sys.path.insert(0,str(BATCH))
import design as shared
ROOT=shared.ROOT;ASSET='Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset';sha=shared.sha

def make():
    b=shared.Board(229,'Rüzgâr Burgusu','Crystal Whirl','Ruzgar-Burgusu',['ooooooooo']*11,28,
        'Solda iki sütun nilüfer, sağda dolap kulesi; altta oyma taşından kıvrılan kaideye bitişik plastik bloğu.',
        'Nilüferlerin ilk yaprağı için special hazırla; beklettiğin special yutulabileceği için üretme ve kullanma zamanını seç.',
        'Oyma kaidesi açılınca alt çalışma alanı büyür. Dolap kapağı açılması içeriği bitirmez; nilüferin ilk yaprağı da sonraki yaprak ve çim işini bırakır.',
        'Dolapların içeriğini çıkar, nilüferlerin kalan yaprakları ve alt çimini bitir; iki aşamalı plastik bloğunu temizle.')
    b.rect(0,3,2,6,69).rect(2,7,2,4,28).rect(0,9,2,2,28).rect(4,7,3,4,40)
    for y in [3,5,7,9]:b.put(7,y,27)
    for o,n in [(69,12),(27,4),(28,12),(40,12)]:b.goal(o,n)
    b.d.update(difficulty='Super Hard',revision='v4-229-lily-workshop',selectedFamilies=[69,27,28,40],previewStage='OYMA KAİDESİ KALKINCA')
    d=b.finish([28],
        '28 hamle ilk ayar. Nilüferde ilk special, kalan yapraklar ve alt çim; dolapta kapak ve içerik; oyma ve iki aşamalı plastik ayrı yüklerdir.',
        ['Jel + pres reddedildi: açılış yardımının yayılmayla birleşmesi kalan işi aşırı azaltır.',
         'Bir başka üretici/elmas boardu komşu228/230 fikrini tekrar ederdi.',
         'Hatırlatma diye GrassFlower eklemek bağımsız beşinci işi artırırdı; bu fikirde nilüferin special zamanlaması seçildi.'])
    assert d['openingGeometry']==dict(tileSlots=59,naturallyFed=59,shadowSeeded=0,authoredPins=0,intentionallyEmpty=[])
    return d

def write():
    HERE.mkdir(parents=True,exist_ok=True);snapshot=HERE/'protected-before.json';p=ROOT/ASSET
    if not snapshot.exists():
        for src in [p,ROOT/'Docs/LevelDesign/Level229-Ruzgar-Burgusu.md',ROOT/'Docs/LevelDesign/Level229-Ruzgar-Burgusu.png',BATCH/'designs.json',BATCH/'active-revision.json']:
            dst=HERE/'Before'/src.relative_to(ROOT);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
        snapshot.write_text(json.dumps({str(x.relative_to(ROOT)):sha(x) for x in sorted((ROOT/'Assets/_Project').rglob('*')) if x.is_file() and x!=p},indent=2)+'\n')
    protected=json.loads(snapshot.read_text());assert not [x for x,h in protected.items() if not(ROOT/x).exists() or sha(ROOT/x)!=h],'Unrelated asset drift; stop.'
    owned=HERE/'owned-hashes.json';before=HERE/'Before'/ASSET
    expected=json.loads(owned.read_text())[ASSET] if owned.exists() else sha(before)
    assert sha(p)==expected,'229 user edit; stop.'
    d=make();d['guid']=re.search(r'^guid: (\w+)$',Path(str(p)+'.meta').read_text(),re.M)[1];t=p.read_text()
    for k,v in dict(width=9,height=11,moves=28,levelKind=0,movableSpawnFallbackMoves=0,movableSpawnFillColumns=1).items():t=shared.scalar(t,k,v)
    for k,v in d['data'].items():t=shared.scalar(t,k,shared.encode(v))
    for k in shared.CUSTOM:t=shared.entries(t,k,d['custom'][k])
    t=shared.entries(t,'goals',d['goals']);t=shared.scalar(t,'movableSpawnColumns','00'*9);p.write_text(t)
    (HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    models=json.loads((BATCH/'designs.json').read_text());(BATCH/'designs.json').write_text(json.dumps([d if a['level']==229 else a for a in models],ensure_ascii=False,indent=2)+'\n')
    own=json.loads((BATCH/'owned-hashes.json').read_text());own[ASSET]=sha(p);(BATCH/'owned-hashes.json').write_text(json.dumps(own,indent=2)+'\n');owned.write_text(json.dumps({ASSET:sha(p)},indent=2)+'\n')
    active=json.loads((BATCH/'active-revision.json').read_text());active.setdefault('additionalLevelRevisions',{})['229']='Revisions/v4-229-lily-workshop';(BATCH/'active-revision.json').write_text(json.dumps(active,indent=2)+'\n')
    print('229:12 lily,4wardrobe,12sculpting,12two-stage plastic;28moves.59/59 natural including12 locked lily cells;47 exposed match cells.')

if __name__=='__main__':write()
