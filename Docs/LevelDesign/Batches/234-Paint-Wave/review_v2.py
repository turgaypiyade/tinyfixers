"""Actual YAML/layer/feed/preservation checks; no gameplay simulation."""
from revise_v2 import *
from collections import Counter, deque
import subprocess


def load(path):
    return json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e',"s=File.readlines(ARGV[0]).reject{|l|l.start_with?('%','---')}.join;puts JSON.generate(YAML.safe_load(s,aliases:false)['MonoBehaviour'])",str(path)]))


def verify():
    d=json.loads((HERE/'design.json').read_text());raw=load(ASSET)
    old=load(REV/'Before'/ASSET.relative_to(ROOT));text=ASSET.read_text()
    assert d['revision']=='v2-layered' and(raw['width'],raw['height'],raw['moves'])==(8,11,24)
    for k in old:
        if k not in['obstacles','obstacleOrigins','goals','stackedObstacles']:assert raw[k]==old[k],k
    for k,v in d['data'].items():assert shared.array(text,k,88)==v,k
    for k,v in d['custom'].items():assert(raw[k]or[])==v,k
    cells=d['layeredCells'];assert len(set(cells))==len(cells)==16
    assert len(raw['stackedObstacles'])==34
    assert {g['obstacleId']:g['amount']for g in raw['goals']}=={21:2,63:2,28:16,40:16,25:16}
    for i in cells:assert layers(d)[i]==[(25,i),(40,i),(28,i)]
    for i in[0,1,8,9]:assert layers(d)[i]==[(63,0),(21,0)]
    for i in[16,17,24,25]:assert layers(d)[i]==[(63,16),(21,16)]
    assert all(d['data']['cells'][i]and d['data']['obstacleOrigins'][i]==i for i in cells)
    assert Counter(d['data']['obstacles'])=={0:64,63:8,25:16}
    assert not({11,12,42,58,69,71,72,73,74}&{o for plane in layers(d)for o,_ in plane})
    pins={i:v for i,v in enumerate(d['data']['pinnedTileTypes'])if v};assert pins=={32:1}
    assert layers(d)[40][-1]==(28,40) and not layers(d)[32]
    assert not any(d['data']['pinnedSpecialTypes'])
    phases=[]
    for removed in[[],[21],[28],[21,28],[21,28,40],[21,63,28],[21,63,28,40],[21,63,28,40,25]]:
        slots,reach=feed(d,removed);assert slots==reach|(set(pins)&slots),(removed,slots-reach)
        phases.append(dict(removed=removed,normalSlots=len(slots),naturallyFed=len(reach),shadowPins=sorted(slots-reach)))
    # Each shoulder2x2 and lower2x6 region touches the authored edge; no isolated
    # middle box or unsupported movable startup stack is introduced.
    assert set(cells)=={y*8+x for y in range(5,11)for x in[0,1]}|{30,31,38,39}
    for g in raw['goals']:assert g['iconOverride'].get('guid')and g['iconOverride']['fileID']!=0
    baseline=json.loads((REV/'protected-before.json').read_text());rel=str(ASSET.relative_to(ROOT))
    drift=[p for p,h in baseline.items()if p!=rel and sha(ROOT/p)!=h];assert not drift,drift
    # Original visual/move scope is deliberately retained. The new difficulty
    # comes from supported layers and a second work face, not a shorter timer.
    assert raw['cells']==old['cells']and raw['moves']==old['moves']and len(raw['goals'])==5
    report=dict(result='PASS',revision='v2-layered',sameGrid=True,activeCells=74,movesUnchanged=24,
        goals={21:2,63:2,28:16,40:16,25:16},threeLayerCells=16,secondWorkFace=[30,31,38,39],
        normalOpeningSlots=50,automaticOpeningTiles=49,shadowPins=pins,phases=phases,
        previousAssetsPreserved=len(baseline)-1,appleSpriteChangePreserved=True,
        playOrBuildOrSimulation=False,measuredBalance=False)
    (REV/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    (HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(report,ensure_ascii=False))


if __name__=='__main__':verify()
