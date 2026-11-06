"""Apply only the explicitly requested shared Diamond goal artwork."""
from pathlib import Path
import hashlib,json,re

HERE=Path(__file__).resolve().parent
ROOT=next(p for p in HERE.parents if (p/'Assets/_Project').is_dir())
BATCH=HERE.parents[1]

def expected_assets():
    m=json.loads((HERE/'revision.json').read_text());ref=m['reference'];out={}
    for level in m['levels']:
        rel=level['asset'];before=(HERE/'Before'/rel).read_text()
        text,n=re.subn(r'(?m)(    collectibleId: 4\n    iconOverride: )[^\n]+',lambda x:x[1]+ref,before)
        assert n==1,(rel,n)
        out[rel]=text
    rel=m['library'];before=(HERE/'Before'/rel).read_text();count=0
    def replace(block):
        nonlocal count
        if not 84<=int(block[1])<=99:return block[0]
        text,n=re.subn(r'(?m)^    goalIconSprite: .+$','    goalIconSprite: '+ref,block[0]);assert n==1
        count+=1
        return text
    out[rel]=re.sub(r'^  - id: (\d+)\n.*?(?=^  - id: |\Z)',replace,before,flags=re.M|re.S)
    assert count==16
    return m,out

def main():
    m,assets=expected_assets()
    for rel,text in assets.items():
        p=ROOT/rel
        assert p.read_text() in ((HERE/'Before'/rel).read_text(),text),('Concurrent change; review required',rel)
    for rel,text in assets.items():(ROOT/rel).write_text(text)
    for p in [ROOT/'Docs/LevelDesign/Batches/221-Dimerdal-Intro/design.json',BATCH/'designs.json']:
        model=json.loads(p.read_text());levels=model if isinstance(model,list) else [model]
        for d in levels:
            for goal in d['goals']:
                if goal['targetType']==2 and goal['collectibleId']==4:goal['iconOverride']=m['reference']
        p.write_text(json.dumps(model,ensure_ascii=False,indent=2)+'\n')
    p=BATCH/'owned-hashes.json';hashes=json.loads(p.read_text())
    for rel in assets:
        if rel in hashes:hashes[rel]=hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()
    p.write_text(json.dumps(hashes,indent=2)+'\n')
    (BATCH/'active-revision.json').write_text(json.dumps(dict(revision='v3-shared-goal-icon',author='Revisions/v3-shared-goal-icon/apply.py',verifier='Revisions/v3-shared-goal-icon/verify.py'),indent=2)+'\n')
    print('Applied shared purple goal icon: four level overrides and sixteen library goal icons only.')

if __name__=='__main__':main()
