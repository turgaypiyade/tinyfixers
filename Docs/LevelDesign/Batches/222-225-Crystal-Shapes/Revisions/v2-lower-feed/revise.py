"""225 only: preserve counts/goals/moves while opening bottom feed lanes."""
from pathlib import Path
import sys,json,re
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
sys.path.insert(0,str(BATCH))
from design import ROOT,encode,scalar,sha,feed

def main():
    ds=json.loads((BATCH/'designs.json').read_text());d=next(d for d in ds if d['level']==225)
    p=ROOT/d['asset'];before=(HERE/'Before'/p.name).read_text()
    assert p.read_text()==before,'225 changed since snapshot; rebase the revision first.'
    a=d['data'];w=d['width']
    for i,o in enumerate(a['obstacles']):
        if o==39 or 84<=o<=95:a['obstacles'][i]=0;a['obstacleOrigins'][i]=-1
    for x in (0,7):
        for y in range(1,9):i=y*w+x;a['obstacles'][i]=39;a['obstacleOrigins'][i]=i
    for x,o in ((0,88),(6,91)):
        origin=9*w+x
        for dy in range(2):
            for dx in range(2):i=origin+dy*w+dx;a['obstacles'][i]=o;a['obstacleOrigins'][i]=origin
    slots,reached,seeded=feed(d)
    assert slots-reached-seeded=={67,68}
    assert {y*w+x for y in (9,10) for x in range(2,6)}<=reached
    d['openingGeometry']=dict(tileSlots=len(slots),naturallyFed=len(reached),shadowSeeded=len(seeded),intentionallyEmpty=sorted(slots-reached-seeded),authoredPins=0)
    d['idea']='İki uzun pusula suru, ortada 4×4 mühürlü avlu, alt köşelerde kristal kapıları ve açık besleme yolları.'
    d['firstDecision']='Yan şeritlerden alt çalışma alanına in; mührün renk sırasını ilerletirken surlar için special çizgileri hazırla.'
    d['change']='Alt iki sıra başlangıçtan beslenir. Mühür açılınca 16 çamur hücresi ve altındaki son iki boş yuva da açılır.'
    d['remaining']='16 pusula taşını ve iki kat çamuru temizle; köşelerdeki kapılardan toplam 16 elması üretip ayrıca topla.'
    text=before
    for k in ('obstacles','obstacleOrigins'):text=scalar(text,k,encode(a[k]))
    p.write_text(text)
    (BATCH/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    hashes=json.loads((BATCH/'owned-hashes.json').read_text());hashes[d['asset']]=sha(p)
    (BATCH/'owned-hashes.json').write_text(json.dumps(hashes,indent=2)+'\n')
    (BATCH/'active-revision.json').write_text(json.dumps(dict(revision='v2-lower-feed',author='Revisions/v2-lower-feed/revise.py',verifier='Revisions/v2-lower-feed/verify.py'),indent=2)+'\n')
    print('225 only: 16 compasses /16 mud /1 seal /16 diamonds /25 moves unchanged; 12 empty slots ->2; both bottom rows fed.')
if __name__=='__main__':main()
