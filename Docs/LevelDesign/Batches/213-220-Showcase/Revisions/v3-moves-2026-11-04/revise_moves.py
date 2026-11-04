"""User-requested move calibration only; all layouts and non-move fields remain identical."""
from pathlib import Path
import hashlib,json,re
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
ROOT=next(p for p in HERE.parents if(p/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').exists())
MOVES={213:21,214:21,215:22,216:20,217:21,218:22,219:24,220:28}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    record=HERE/'revision.json'
    assert not record.exists(),'Already applied; inspect current state before another calibration.'
    ds=json.loads((BATCH/'designs.json').read_text());rows=[];hashes={}
    for d in ds:
        n=d['level'];p=ROOT/d['asset'];old=p.read_text();before=(HERE/p.name).read_text();assert old==before
        previous=int(re.search(r'^  moves: (\d+)$',old,re.M)[1]);target=MOVES[n]
        new,count=re.subn(r'^  moves: \d+$',f'  moves: {target}',old,flags=re.M);assert count==1
        assert re.sub(r'^  moves:.*$','',new,flags=re.M)==re.sub(r'^  moves:.*$','',old,flags=re.M)
        p.write_text(new);hashes[d['asset']]=sha(p)
        d['moves']=target;d['revision']='v3-moves-2026-11-04'
        rows.append(dict(level=n,title=d['name'],before=previous,after=target,delta=target-previous,reason='One-life pass reported; moderate normal reduction' if n not in (215,219,220) else 'Hard passed first try; larger reduction' if n==215 else 'One life lost before winning; one-move adjustment only'))
    (BATCH/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    record.write_text(json.dumps(dict(revision='v3-moves-2026-11-04',feedback='User passed213–218 first try; lost one life on each219 and220. Exact remaining moves and repeat sample are unknown.',levels=rows,levelHashes=hashes),ensure_ascii=False,indent=2)+'\n')
    (BATCH/'active-revision.json').write_text(json.dumps(dict(revision='v3-moves-2026-11-04',author='Revisions/v3-moves-2026-11-04/revise_moves.py',verifier='Revisions/v3-moves-2026-11-04/verify_moves.py'),indent=2)+'\n')
    for d in rows:print(d['level'],str(d['before'])+' -> '+str(d['after']))
if __name__=='__main__':main()
