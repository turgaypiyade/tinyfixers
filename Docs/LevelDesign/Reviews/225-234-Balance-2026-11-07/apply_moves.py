"""Apply only explicitly authorized move tuning; reject concurrent asset edits."""
from pathlib import Path
import hashlib, json, re
from audit import yaml_data

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
MOVES={225:29,226:23,227:26,228:29,229:32,230:30,231:28,232:35,233:21,234:46}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()


def apply():
    assert not (HERE/'applied-moves.json').exists(), 'Already applied; preserve subsequent user edits.'
    manifest=json.loads((HERE/'before-manifest.json').read_text())
    prepared=[]
    for e in manifest:
        p=ROOT/e['asset'];assert sha(p)==e['sha256'], ('Concurrent edit',p)
        before=yaml_data(p);t=p.read_text();n=e['level'];moves=MOVES[n]
        t,count=re.subn(r'^  moves: \d+$',f'  moves: {moves}',t,flags=re.M);assert count==1
        prepared.append((e,p,t,before,moves))
    out=HERE/'After-Overrides';out.mkdir(exist_ok=True)
    results=[]
    for e,p,t,before,moves in prepared:
        old=before['moves']
        if old!=moves:p.write_text(t)
        after=yaml_data(p);assert after['moves']==moves
        assert {k:v for k,v in before.items() if k!='moves'}=={k:v for k,v in after.items() if k!='moves'}
        d=json.loads((HERE/f'L{e["level"]:04d}.json').read_text());d['moves']=moves
        (out/f'L{e["level"]:04d}.json').write_text(json.dumps(d,ensure_ascii=False)+'\n')
        results.append(dict(level=e['level'],asset=e['asset'],before=old,after=moves,
                            beforeSha256=e['sha256'],afterSha256=sha(p),onlyMovesChanged=True))
    (HERE/'applied-moves.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(results,ensure_ascii=False))


if __name__=='__main__':apply()
