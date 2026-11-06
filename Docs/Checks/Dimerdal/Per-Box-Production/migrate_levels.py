"""Materialize per-source quotas in existing Diamond levels, preserving their totals."""
from pathlib import Path
import hashlib,json,re,struct
HERE=Path(__file__).resolve().parent
ROOT=next(p for p in HERE.parents if (p/'Assets/_Project').is_dir())

def entries_for(text):
    def array(key):
        raw=re.search(r'^  '+key+r': ([0-9a-f]+)$',text,re.M)[1]
        return struct.unpack('<'+'i'*(len(raw)//8),bytes.fromhex(raw))
    ids,origins=array('obstacles'),array('obstacleOrigins')
    boxes=[(i,id) for i,id in enumerate(ids) if 84<=id<=95 and origins[i]==i]
    authored=sum(1 for i,id in enumerate(ids) if 96<=id<=99 and origins[i]==i)
    total=int(re.search(r'    collectibleId: 4\n    iconOverride: [^\n]+\n    amount: (\d+)',text)[1])
    assert boxes and total-authored>=len(boxes)
    return [dict(originCellIndex=o,obstacleId=id,count=(total-authored)//len(boxes)+(i<(total-authored)%len(boxes))) for i,(o,id) in enumerate(boxes)]

def main():
    cat=(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').read_text()
    paths={re.search(r'^guid: (\w+)',p.read_text(),re.M)[1]:str(p.relative_to(ROOT))[:-5] for p in (ROOT/'Assets/_Project/Settings/ProLevels').glob('*.asset.meta')}
    levels=[]
    for n,key,guid in re.findall(r'    level: (\d+)\n    levelKey: (\S+)\n    levelData: \{fileID: 11400000, guid: (\w+), type: 2\}',cat):
        rel=paths[guid];before=HERE/'Before'/rel
        if not before.exists():continue
        old=before.read_text();counts=entries_for(old)
        block='  dimerdals:\n'+''.join(f'  - originCellIndex: {e["originCellIndex"]}\n    obstacleId: {e["obstacleId"]}\n    count: {int(e["count"])}\n' for e in counts)
        text=old+('' if old.endswith('\n') else '\n')+block
        path=ROOT/rel;assert path.read_text() in (old,text),('Concurrent level edit; review required',rel)
        path.write_text(text)
        levels.append(dict(level=int(n),levelKey=key,asset=rel,dimerdals=counts))
    assert {e['level'] for e in levels}=={221,222,224,225}
    (HERE/'levels.json').write_text(json.dumps(levels,ensure_ascii=False,indent=2)+'\n')
    batch=ROOT/'Docs/LevelDesign/Batches/222-225-Crystal-Shapes'
    for p in [ROOT/'Docs/LevelDesign/Batches/221-Dimerdal-Intro/design.json',batch/'designs.json']:
        data=json.loads(p.read_text());ds=data if isinstance(data,list) else [data]
        for d in ds:
            current=next((e for e in levels if e['level']==d['level']),None)
            d.setdefault('custom',{})['dimerdals']=current['dimerdals'] if current else []
        p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
    p=batch/'owned-hashes.json';hashes=json.loads(p.read_text())
    for e in levels:
        if e['asset'] in hashes:hashes[e['asset']]=hashlib.sha256((ROOT/e['asset']).read_bytes()).hexdigest()
    p.write_text(json.dumps(hashes,indent=2)+'\n')
    (batch/'active-revision.json').write_text(json.dumps(dict(revision='v4-per-box-production',author='../../../Checks/Dimerdal/Per-Box-Production/migrate_levels.py',verifier='../../../Checks/Dimerdal/Per-Box-Production/verify.py'),indent=2)+'\n')
    print('Explicit per-box counts:',{e['level']:[int(x['count']) for x in e['dimerdals']] for e in levels})

if __name__=='__main__':main()
