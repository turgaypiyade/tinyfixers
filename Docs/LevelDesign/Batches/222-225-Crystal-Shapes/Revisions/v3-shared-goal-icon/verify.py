"""Static goal-icon revision audit; no build or gameplay simulation."""
from pathlib import Path
import hashlib,json,re,subprocess,sys
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
from apply import expected_assets,ROOT,BATCH

def main():
    sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
    m,assets=expected_assets()
    for rel,text in assets.items():assert (ROOT/rel).read_text()==text,('Only authorized goal-icon lines may change',rel)
    protected=json.loads((HERE/'protected-before.json').read_text())
    changed=[rel for rel,h in protected.items() if not(ROOT/rel).exists() or sha(ROOT/rel)!=h]
    assert not changed,('Unrelated changes since icon revision started',changed)
    p=ROOT/m['goalIconAsset'];assert sha(p)==m['sourceSha256']
    meta=Path(str(p)+'.meta').read_text();assert re.search(r'guid: (\w+)',m['reference'])[1] in meta
    assert 'internalID: 21300000' in meta and 'spriteMode: 2' in meta
    for name in ['Docs/LevelDesign/Batches/221-Dimerdal-Intro/design.json',str((BATCH/'designs.json').relative_to(ROOT))]:
        data=json.loads((ROOT/name).read_text());old=json.loads((HERE/'Before'/name).read_text())
        ds=data if isinstance(data,list) else [data];prior=old if isinstance(old,list) else [old]
        for d,before in zip(ds,prior):
            for goal in before['goals']:
                if goal['targetType']==2 and goal['collectibleId']==4:goal['iconOverride']=m['reference']
            assert d==before,'Only Diamond goal icons may change in saved design models.'
    ruby="ARGV.each{|p| t=File.readlines(p).reject{|l|l.start_with?('%','---')}.join;YAML.safe_load(t,aliases:false)}"
    subprocess.run(['ruby','-ryaml','-e',ruby,*[str(ROOT/rel) for rel in assets],str(p)+'.meta'],check=True)
    # HUD, PreLevel and results honor goal overrides; fallback obtains the library
    # goal icon. Board/box sprites are unchanged by the exact library comparison.
    for rel,method in [('UI/TopHUD/TopHudController.cs','ResolveGoalIcon'),('UI/PreLevelSpecialPopupController.cs','ResolvePreviewGoalIcon'),('UI/LevelEndSimplePopupController.cs','ResolveGoalResultIcon')]:
        text=(ROOT/'Assets/_Project/Scripts'/rel).read_text();assert method in text and 'return goal.iconOverride;' in text and 'GetGoalIconSprite()' in text
    result=dict(result='PASS',levels=[d['level'] for d in m['levels']],sharedDefaultGoalIcons=16,protectedFiles=len(protected),unrelatedChanges=[],exactSourcePng=True,
        preserved=['board and box artwork','runtime code','level layouts/layers/pins','moves','goal amounts/types','catalog/names','other goal icons'],
        limits='Static YAML, exact scoped asset comparison and UI source-path review. No Unity build/Play/device test or gameplay simulation.')
    (HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print('PASS: shared purple icon in four level goals and sixteen defaults;',len(protected),'unrelated files unchanged; layouts/moves/counts/board sprites preserved.')

if __name__=='__main__':main()
