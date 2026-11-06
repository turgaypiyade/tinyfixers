"""Actual YAML, scoped preservation, authored layers and static reachability only."""
from author import *
from collections import Counter, deque
import subprocess

D=json.loads((HERE/'design.json').read_text())


def load(path):
    return json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e',
        "s=File.readlines(ARGV[0]).reject{|l|l.start_with?('%','---')}.join;puts JSON.generate(YAML.safe_load(s,aliases:false)['MonoBehaviour'])",str(path)]))


def verify():
    baseline=json.loads((HERE/'protected-before.json').read_text())
    allowed={str(p.relative_to(ROOT))for p in(CAT,NAMES)}
    drift=[rel for rel,h in baseline.items()if rel not in allowed and sha(ROOT/rel)!=h]
    assert not drift,('External change;do not overwrite',drift)
    raw=load(ASSET);text=ASSET.read_text()
    assert(raw['width'],raw['height'],raw['moves'],raw['levelKind'],raw['refillSpecialGuard'])==(8,11,24,0,-1)
    for k,v in D['data'].items():assert shared.array(text,k,88)==v,k
    for k,v in D['custom'].items():assert(raw[k]or[])==v,k
    assert not raw['fruits'] and not raw['bossWaves'] and not raw['tutorialSteps']
    active={i for i,a in enumerate(D['data']['cells'])if a};assert len(active)==74
    group={min(active)};q=deque(group)
    while q:
        i=q.popleft();x,y=i%8,i//8
        for xx,yy in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
            j=yy*8+xx
            if 0<=xx<8 and 0<=yy<11 and j in active and j not in group:group.add(j);q.append(j)
    assert group==active
    counts=Counter();occupied=set()
    for i,o in enumerate(D['data']['obstacles']):
        origin=D['data']['obstacleOrigins'][i]
        if not o:assert origin==-1;continue
        assert i in active and D['data']['obstacles'][origin]==o and D['data']['obstacleOrigins'][origin]==origin
        if origin!=i:continue
        counts[o]+=1;fw,fh=shared.footprint(o)
        cells={i+dy*8+dx for dy in range(fh)for dx in range(fw)}
        assert not occupied&cells;occupied|=cells
        assert all(D['data']['obstacles'][j]==o and D['data']['obstacleOrigins'][j]==i for j in cells)
    assert counts=={63:2,11:6,12:6}
    overlays=raw['stackedObstacles'];assert len(overlays)==2
    covered=set()
    for e in overlays:
        assert e['obstacleId']==21 and e['stackOrder']==1
        i=e['originCellIndex'];assert i in(0,16)
        cells={i,i+1,i+8,i+9};assert not covered&cells;covered|=cells
        assert all(D['data']['obstacles'][j]==63 and D['data']['obstacleOrigins'][j]==i for j in cells)
    assert len(covered)==8
    assert{g['obstacleId']:g['amount']for g in raw['goals']}=={21:2,63:2,11:6,12:6}
    assert len(raw['goals'])==4
    for g in raw['goals']:
        assert g['targetType']==1 and g['iconOverride'].get('guid')
    assert not({42,58,69,71,72,73,74}&set(D['data']['obstacles']))
    assert shared.array(text,'randomPool',4)==[0,1,2,3]
    pins={i:v for i,v in enumerate(D['data']['pinnedTileTypes'])if v};assert pins=={32:1}
    assert not any(D['data']['pinnedSpecialTypes'])
    slots,reach=feed(D);assert slots-reach==set(pins) and len(slots)==54 and len(reach)==53
    # Pin is on a true normal slot, and cannot initially fall through its supported stack.
    assert not layers(D)[32] and D['data']['obstacles'][40]==11
    for x,o in[(0,11),(1,12)]:
        assert all(D['data']['obstacles'][y*8+x]==o and y*8+x in active for y in range(5,11))
        assert 10*8+x in active
    # Only these two initial columns are occupied by movable color blockers. All
    # twelve are already present and cover the goal; no additional refill quota.
    assert counts[11]==counts[12]==6
    phases=[]
    for removed in[[],[21],[21,63],[21,63,11],[21,63,12],[21,63,11,12]]:
        slots,reach=feed(D,removed);authored=set(pins)&slots
        assert slots==reach|authored,(removed,slots-reach-authored)
        phases.append(dict(removed=removed,normalSlots=len(slots),naturallyFed=len(reach),
            requiredShadowPins=sorted(slots-reach)))
    assert phases[2]['requiredShadowPins']==[]
    # Removing just the low movable bands leaves the same one-cell roof until
    # the upper paint crates clear. Record this; initial filling is not refill.
    slots,reach=feed(D,[11,12]);assert slots-reach=={32}
    beforecat=(HERE/'Before'/CAT.relative_to(ROOT)).read_text()
    suffix=f'  - chapter: 10\n    level: 234\n    levelKey: LevelCL_0234\n    levelData: {{fileID: 11400000, guid: {D["guid"]}, type: 2}}\n'
    assert CAT.read_text()==beforecat+suffix
    assert list(map(int,re.findall(r'^    level: (\d+)$',CAT.read_text(),re.M)))==list(range(1,235))
    oldnames=json.loads((HERE/'Before'/NAMES.relative_to(ROOT)).read_text());names=json.loads(NAMES.read_text())
    assert names['entries'][:-1]==oldnames['entries']
    assert names['entries'][-1]==dict(level=234,levelKey='LevelCL_0234',name='Boya Dalgası',nameEn='Paint Wave')
    user176=load(PRO/'LevelV1_00001760.asset')
    assert user176['moves']==23 and[g['amount']for g in user176['goals']]==[9,9]
    report=dict(result='PASS',level=234,activeCells=74,boardComponents=1,goals={21:2,63:2,11:6,12:6},
        openingAutomaticTiles=53,openingShadowTiles=1,pinnedCells=pins,pinSupport='six blue movable cells down to bottom',
        phases=phases,temporarilyRoofedCellIfLowerBandsClearFirst=32,paintCanCapacity=18,
        previousAssetsPreserved=len(baseline)-2,catalogAndNamesAppendOnly=True,
        user176Preserved='23moves,9red+9green;exact hash preserved',
        grassFamilyAbsent=True,buildOrGameSimulation=False,measuredBalance=False,measuredPerformance=False)
    (HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(report,ensure_ascii=False))


def comparisons():
    spec=importlib.util.spec_from_file_location('wave_visual',HERE/'render.py')
    visual=importlib.util.module_from_spec(spec);spec.loader.exec_module(visual)
    import sys
    sys.path.insert(0,str(ROOT/'Tools'))
    import review_level_patterns as patterns
    from PIL import Image,ImageDraw
    catalog=patterns.load(CAT)
    im=Image.new('RGBA',(1500,1000),'#172536');dr=ImageDraw.Draw(im)
    for col,n in enumerate([36,176,234]):
        d=next(d for d in catalog if d['level']==n);w=d['width'];h=d['height']
        source=(ROOT/d['asset']).read_text()
        d['data']['pinnedTileTypes']=shared.array(source,'pinnedTileTypes',w*h)
        x=25+col*495
        dr.text((x,22),f'{n} · {d["name"]}',font=visual.art.font(24,True),fill='#ffcf75')
        visual.board(im,d,(x,88),min(48,440/w))
        notes={36:'Benzer: kenara bağlı iki sütun. 234ün farkı aynı footprintte sandık→boya işi ve tek başlangıç gölgesi.',
            176:'Kullanıcının güncel hali:23hamle,9+9sandık. Kenar yaklaşımı beğenildi;V kopyalanmadı.',
            234:'Dalga maskesi,sol kenarda iki aşamalı atölye,mavi/kırmızı alt bant. 53otomatik taş+1gerçek pin.'}[n]
        visual.paragraph(dr,notes,x,725,40,20)
    dr.text((25,945),'Gerçek sprite’lar /temsili serbest taş renkleri · Şekil benzerliği yenilik veya denge yüzdesi değildir.',font=visual.art.font(19),fill='#a9bdd1')
    im.convert('RGB').save(HERE/'Comparison.png')


if __name__=='__main__':verify();comparisons()
