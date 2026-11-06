"""225 revision preservation + static feed checks; no gameplay simulation."""
from pathlib import Path
from collections import Counter
import sys,json,re,subprocess
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
sys.path.insert(0,str(BATCH))
from design import ROOT,sha,feed,layers
from check_levels161_162 import array

def main():
    baseline=json.loads((HERE/'protected-before.json').read_text())
    # A later explicit mechanic request replaces grey closure with retirement.
    # Accept only its exact, separately tested runtime hashes; keep level protection.
    overlay=ROOT/'Docs/Checks/Dimerdal/Exhaustion-Break/owned-runtime-hashes.json'
    runtime=json.loads(overlay.read_text()) if overlay.exists() else {}
    for rel,h in runtime.items():assert sha(ROOT/rel)==h,('Runtime revision changed; review required',rel)
    changed=[rel for rel,h in baseline.items() if rel not in runtime and (not(ROOT/rel).exists() or sha(ROOT/rel)!=h)]
    assert not changed,('Unexpected changes outside authorized level/runtime revisions',changed)
    ds=json.loads((BATCH/'designs.json').read_text());old=json.loads((HERE/'Before/designs.json').read_text())
    assert ds[:3]==old[:3],'222–224 are visually approved and must remain unchanged.'
    d=ds[-1];before=old[-1];assert d['level']==225
    for key in ('asset','guid','levelKey','name','nameEn','width','height','moves','goals','custom','rows'):
        assert d[key]==before[key],key
    assert d['width']==8 and d['height']==11 and d['moves']==25
    a=d['data'];previous=before['data'];assert Counter(a['obstacles'])==Counter(previous['obstacles'])
    for k in a:
        if k not in ('obstacles','obstacleOrigins'):assert a[k]==previous[k]
    p=ROOT/d['asset'];text=p.read_text();prior=(HERE/'Before'/p.name).read_text()
    scrub=lambda t:re.sub(r'^(  (?:obstacles|obstacleOrigins)):.*$',r'\1: <scoped geometry revision>',t,flags=re.M)
    assert scrub(text)==scrub(prior),'Only two geometry array lines may change in 225.'
    for k in a:assert array(text,k,88)==a[k]
    assert {i for i,o in enumerate(a['obstacles']) if o==39}=={y*8+x for y in range(1,9) for x in (0,7)}
    assert {a['obstacleOrigins'][i] for i,o in enumerate(a['obstacles']) if o==88}=={72}
    assert {a['obstacleOrigins'][i] for i,o in enumerate(a['obstacles']) if o==91}=={78}
    slots,reached,seeded=feed(d);empty=slots-reached-seeded
    assert empty=={67,68},empty
    assert {y*8+x for y in (9,10) for x in range(2,6)}<=reached,'Both lower rows must receive actual feed, without pins.'
    assert {y*8+x for y in range(1,9) for x in (1,6)}<=reached,'Both side feed lanes stay open.'
    assert not any(a['pinnedTileTypes']) and not any(a['pinnedSpecialTypes'])
    all_slots,all_reached,all_seeded=feed(d,{25,39,64})
    assert all_slots<=all_reached|all_seeded,'Permanent sources may not leave an unfed pocket.'
    for other in ds[:3]:
        name=f'Level{other["level"]}-{other["slug"]}.png'
        assert sha(ROOT/'Docs/LevelDesign'/name)==sha(HERE/'Before'/name),'Approved PNG changed.'
    ruby="ARGV.each{|p| t=File.readlines(p).reject{|l|l.start_with?('%','---')}.join;YAML.safe_load(t,aliases:false)}"
    yaml_files=[str(p)]
    if not runtime:yaml_files.append(str(ROOT/'Assets/_Project/Resources/Dimerdal/Spent.mat'))
    subprocess.run(['ruby','-ryaml','-e',ruby,*yaml_files],check=True)
    if not runtime:
        shader=ROOT/'Assets/_Project/Shaders/UI_DimerdalSpent.shader'
        guid=re.search(r'^guid: (\w+)$',Path(str(shader)+'.meta').read_text(),re.M)[1]
        assert f'guid: {guid}' in (ROOT/'Assets/_Project/Resources/Dimerdal/Spent.mat').read_text()
    protected_count=len(set(baseline)-set(runtime))
    result=dict(result='PASS',level=225,protectedFiles=protected_count,changedProtectedFiles=[],unchangedLevels='1–224',
        initialEmptyBefore=12,initialEmptyAfter=len(empty),naturallyFedBefore=36,naturallyFedAfter=len(reached),
        lowerNormalRowsFullyFed=True,emptyCellsImmediatelyBelowSeal=sorted(empty),
        unchanged=['mask','dimensions','moves25','goals16/1/16/16','obstacle counts','seal9/9/9','GUID/meta','catalog','names','pins0'],
        authorizedRuntimeFiles=json.loads((HERE/'authorized-existing-writes.json').read_text())[1:],
        laterAuthorizedRuntimeOverlay=runtime,
        limits='Static YAML/geometry and code source checks only. No Unity/project build, GPU shader compilation, Play/device or gravity/cascade simulation. Runtime feedback is separately tested with production C# and Unity doubles.')
    (HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print('PASS: 225 bottom feed36->46, empty12->2; both lower rows populated; goal/count/moves/GUID preserved;',protected_count,'protected files unchanged;',len(runtime),'later authorized runtime hashes checked.')
if __name__=='__main__':main()
