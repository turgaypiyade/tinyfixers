"""Read-only production asset validation and static comparison rendering."""
from author import *
from collections import Counter
import subprocess

D=json.loads((HERE/'design.json').read_text())


def load(path):
    return json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e',
        "s=File.readlines(ARGV[0]).reject{|l|l.start_with?('%','---')}.join;puts JSON.generate(YAML.safe_load(s,aliases:false)['MonoBehaviour'])",str(path)]))


def verify():
    baseline=json.loads((HERE/'protected-before.json').read_text())
    external=json.loads((HERE/'external-edits.json').read_text())['files']
    allowed={str(p.relative_to(ROOT)) for p in (CAT,NAMES)}
    drift=[p for p,h in baseline.items() if p not in allowed and (not(ROOT/p).exists() or sha(ROOT/p)!=external.get(p,h))]
    assert not drift,drift
    raw=load(ASSET);t=ASSET.read_text();size=88
    assert (raw['width'],raw['height'],raw['moves'],raw['levelKind'])==(8,11,27,0)
    assert raw['refillSpecialGuard']==-1
    for k,v in D['data'].items():assert shared.array(t,k,size)==v,k
    for k,v in D['custom'].items():
        if k=='magnets':assert shared.array('  path: '+raw[k][0]['pathCellIndices'],'path',12)==v[0]['pathCellIndices']
        else:assert (raw[k]or[])==v,k
    active={i for i,v in enumerate(D['data']['cells']) if v}
    assert len(active)==82
    group={min(active)};q=deque(group)
    while q:
        i=q.popleft();x,y=i%8,i//8
        for xx,yy in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
            j=yy*8+xx
            if 0<=xx<8 and 0<=yy<11 and j in active and j not in group:group.add(j);q.append(j)
    assert group==active
    assert not any(D['data']['pinnedTileTypes']) and not any(D['data']['pinnedSpecialTypes'])
    assert shared.array(t,'randomPool',4)==[0,1,2,3]
    instances=Counter();forbidden={42,58,69}
    for i,o in enumerate(D['data']['obstacles']):
        origin=D['data']['obstacleOrigins'][i]
        assert o not in forbidden
        if not o:assert origin==-1;continue
        assert i in active and origin==i and shared.footprint(o)==(1,1)
        instances[o]+=1
    assert instances=={34:8,30:12,39:8}
    covered=set()
    for s in raw['safes']:
        assert(s['width'],s['height'])==(2,2) and s['lockHitMode']==0 and s['stackOrder']==1
        assert sorted([s['firstLock'],s['secondLock'],s['thirdLock']])==[0,1,2]
        assert s['redHits']+s['yellowHits']+s['greenHits']==4
        origin=s['originCellIndex'];cells={origin,origin+1,origin+8,origin+9}
        assert not covered&cells;covered|=cells
        assert all(D['data']['obstacles'][i]==34 for i in cells)
        # Two adjacent normal cells above each safe face, within a six-wide workspace.
        assert all(layers(D)[i-8]==[] for i in [origin,origin+1])
    assert covered=={i for i,o in enumerate(D['data']['obstacles'])if o==34}
    assert {s['firstLock'] for s in raw['safes']}=={0,2}
    path=D['custom']['magnets'][0]['pathCellIndices'];assert len(path)==len(set(path))==12
    assert all(i in active and not D['data']['obstacles'][i] for i in path)
    assert all(abs(a%8-b%8)+abs(a//8-b//8)==1 for a,b in zip(path,path[1:]))
    assert set(path)&covered==set()
    assert path==[57,65,73,81,82,83,84,85,86,78,70,62]
    # Cargo has straight-only gravity. All four authored lanes remain active to the real bottom.
    for x in range(2,6):
        assert all(y*8+x in active for y in range(6,11))
        assert all(D['data']['obstacles'][y*8+x]==39 for y in [8,9])
        assert 10*8+x in path
    # The two porcelain stacks rest on each column's lowest active cell; no empty cell below.
    for x in (0,7):
        assert [i//8 for i,o in enumerate(D['data']['obstacles'])if o==30 and i%8==x]==list(range(3,9))
        assert all(y*8+x not in active for y in (9,10))
    goals={g['obstacleId']:g['amount'] for g in raw['goals']}
    assert goals=={34:8,33:2,29:1,39:8,30:12} and len(raw['goals'])==5
    for g in raw['goals']:
        assert g['targetType']==1 and g['obstacleId']not in forbidden
        assert g['iconOverride'].get('guid') and g['iconOverride']['fileID']==21300000
    phases=[]
    for removed in [[],[33],[33,39],[33,39,29],[33,39,29,30],[33,39,29,30,34]]:
        slots,reach=feed(D,removed);assert slots==reach,(removed,slots-reach)
        phases.append(dict(removed=removed,normalSlots=len(slots),naturallyFed=len(reach)))
    # Out-of-order opening is temporary: a closed safe can roof the cleared exit band.
    slots,reach=feed(D,[39]);temporary=sorted(slots-reach)
    assert len(temporary)==8 and feed(D,[33,39])[0]==feed(D,[33,39])[1]
    cat_before=(HERE/'Before'/CAT.relative_to(ROOT)).read_text()
    assert CAT.read_text()==cat_before+f'  - chapter: 10\n    level: 233\n    levelKey: LevelCL_0233\n    levelData: {{fileID: 11400000, guid: {D["guid"]}, type: 2}}\n'
    assert list(map(int,re.findall(r'^    level: (\d+)$',CAT.read_text(),re.M)))==list(range(1,234))
    old_names=json.loads((HERE/'Before'/NAMES.relative_to(ROOT)).read_text())
    names=json.loads(NAMES.read_text());assert names['entries'][:-1]==old_names['entries']
    assert names['entries'][-1]==dict(level=233,levelKey='LevelCL_0233',name='Kutup Kilidi',nameEn='Polar Lock')
    # Verify the user-edited232 independently of the older design model.
    previous=load(TEMPLATE);assert previous['moves']==32 and [d['count']for d in previous['dimerdals']]==[5,5,5]
    assert sha(TEMPLATE)==sha(HERE/'Before'/TEMPLATE.relative_to(ROOT))
    report=dict(result='PASS',level=233,activeCells=82,components=1,goals=goals,phases=phases,
        startingEmpty=0,pins=0,specialOnlyCells=8,magnetPathCells=12,magnetEndpointAdvances=11,
        closedSafeTemporaryRoofIfFloorClearedFirst=temporary,
        priorAssetHashesPreserved=len(baseline)-len(allowed)-len(external),externalEditsPreserved=external,
        previous232UserChangesPreserved=True,grassFamilyAbsent=True,simulationOrBuild=False,
        measuredBalance=False,measuredPerformance=False)
    (HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print('PASS233:',json.dumps(report,ensure_ascii=False))


def comparison():
    spec=importlib.util.spec_from_file_location('polar_comparison',HERE/'render.py')
    visual=importlib.util.module_from_spec(spec);spec.loader.exec_module(visual)
    sys.path.insert(0,str(ROOT/'Tools'))
    import review_level_patterns as patterns
    from PIL import Image,ImageDraw
    candidates=patterns.load(CAT)
    im=Image.new('RGBA',(1530,980),'#142436');dr=ImageDraw.Draw(im)
    for col,n in enumerate([142,198,233]):
        d=next(d for d in candidates if d['level']==n)
        x=30+col*500
        dr.text((x,22),f'{n} · {d["name"]}',font=visual.art.font(23,True),fill='#ffcf75')
        cell=min(48,440/d['width'])
        visual.board(im,d,(x,83),cell)
        description={142:'Benzer: alt kütle ve iki kasa. Fark: çamur/sandık işi; ortak mıknatıslı kargo çıkışı yok.',
            198:'Mıknatıs aile tekrarı; iki ayrı kısa yol, dolap ve renkli sandık işi. 233 tek alt çıkış hattını kilitler.',
            233:'İki kasa → sekiz kargo; pusula bandı → gerçek alt çıkış. Üst çalışma avlusu bütün zorunlu işleri birleştirir.'}[n]
        visual.para(dr,description,x,720,40,20)
    dr.text((30,935),'Statik gerçek sprite karşılaştırması · Benzerlik puanı yenilik veya oynanmış denge yüzdesi değildir.',font=visual.art.font(20),fill='#a5b8cf')
    im.convert('RGB').save(HERE/'Comparison.png')


if __name__=='__main__':verify();comparison()
