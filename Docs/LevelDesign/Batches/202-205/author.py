"""Apply only the four rejected level assets' v2 revision; preserve GUIDs/catalog/names/runtime."""
from pathlib import Path
import hashlib,json,re,struct,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(HERE))
from designs import DESIGNS
from audit import check
LEVELS=ROOT/'Assets/_Project/Settings/ProLevels'
def scalar(text,key,value):
    line=f'  {key}: {value}'
    return re.sub(r'^  '+key+r':[^\n]*',lambda _:line,text,flags=re.M) if re.search(r'^  '+key+r':',text,re.M) else text+line+'\n'
def section(text,key,rows):
    def val(v):return struct.pack('<'+str(len(v))+'i',*v).hex() if isinstance(v,list) else str(v)
    body=f'  {key}: []\n' if not rows else f'  {key}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+k+': '+val(v)+'\n' for j,(k,v) in enumerate(row.items())) for row in rows)
    pattern=r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*'
    return re.sub(pattern,lambda _:body,text,flags=re.M) if re.search(pattern,text,re.M) else text+body

def main():
    assert '--apply' in sys.argv,'Inspect v2 sprite previews before --apply.'
    assert not (HERE/'Revisions/v2-applied.json').exists(),'Already applied: use audit.py --assets.'
    for d in DESIGNS:
        check(d);p=LEVELS/f'LevelV1_{d["n"]*10:08d}.asset'
        assert p.read_bytes()==(HERE/'Revisions/v1-rejected'/p.name).read_bytes(),'Source changed since v1 archive; inspect before overwriting.'
    protected=json.loads((HERE/'Revisions/v2-before/protected-before.json').read_text())
    assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in protected.items())
    manifest=[]
    for d in DESIGNS:
        p=LEVELS/f'LevelV1_{d["n"]*10:08d}.asset';text=p.read_text()
        for k,v in dict(width=9,height=d['h'],moves=d['moves'],levelKind=0,baseCoinReward=10,
            usesCustomIntro=0,oilSpreadMaxCells=0,comboTutorial=0,rocketBasketFireAllOnHit=0,refillSpecialGuard=-1).items():text=scalar(text,k,v)
        for k,v in {**d['data'],'randomPool':[0,1,2,3]}.items():text=scalar(text,k,struct.pack('<'+str(len(v))+'i',*v).hex())
        for k,rows in [('goals',d['goals']),('ancientSeals',d['seals']),('waterChannels',d['channels']),('paintCanBoxColors',d['paintColors'])]:text=section(text,k,rows)
        for k in ('tutorialSteps','stackedObstacles','tubes','magnets','safes','tarpStatues','colorCrateInitialHits','bossWaves'):text=section(text,k,[])
        p.write_text(text)
        guid=re.search(r'^guid: (\w+)$',Path(str(p)+'.meta').read_text(),re.M)[1]
        manifest.append(dict(level=d['n'],asset=str(p.relative_to(ROOT)),guid=guid,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    result=dict(revision='v2',levels=manifest,scope='Only 202–205 level content; 201 remains50; no GUID/catalog/name/runtime changes or publication.')
    (HERE/'Revisions/v2-applied.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    (HERE/'manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print('Applied v2 to four assets only. All GUIDs, names, catalog, 201 and runtime preserved.')
if __name__=='__main__':main()
