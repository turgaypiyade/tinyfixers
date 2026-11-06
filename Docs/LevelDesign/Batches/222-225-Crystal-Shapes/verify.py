"""Independent YAML/geometry/goal/preservation checks. No gameplay or gravity simulation."""
from collections import Counter, deque
import json, re, struct, subprocess
from design import HERE, ROOT, CAT, NAMES, ARRAYS, CUSTOM, DEFS, footprint, stage, sha, layers, feed
from export_level_previews import parse_entries, int_field
from check_levels161_162 import array
import review_level_patterns as review

def components(cells,w,h):
    remaining=set(cells);groups=[]
    while remaining:
        group={remaining.pop()};q=deque(group)
        while q:
            i=q.popleft();x,y=i%w,i//w
            for dx,dy in ((0,1),(0,-1),(1,0),(-1,0)):
                j=(y+dy)*w+x+dx
                if 0<=x+dx<w and 0<=y+dy<h and j in remaining:
                    remaining.remove(j);group.add(j);q.append(j)
        groups.append(group)
    return groups

def main():
    ds=json.loads((HERE/'designs.json').read_text());all_levels=review.load(CAT)
    assert [d['level'] for d in all_levels]==list(range(1,226))
    assert CAT.read_text().startswith((HERE/'Before'/CAT.name).read_text())
    old=json.loads((HERE/'Before'/NAMES.name).read_text());names=json.loads(NAMES.read_text())
    assert names['entries'][:len(old['entries'])]==old['entries']
    assert len(names['entries'])==len(old['entries'])+4
    assert len({d['level'] for d in names['entries']})==len(names['entries'])
    baseline=json.loads((HERE/'protected-before.json').read_text())
    changed=[p for p,h in baseline.items() if not(ROOT/p).exists() or sha(ROOT/p)!=h]
    assert not changed,('Protected files changed; preserve/reconcile concurrent work',changed)
    ruby="out={};ARGV.each{|p| t=File.readlines(p).reject{|l|l.start_with?('%','---')}.join;out[p]=YAML.safe_load(t,aliases:false)['MonoBehaviour']};puts JSON.generate(out)"
    paths=[str(ROOT/d['asset']) for d in ds]
    payloads=json.loads(subprocess.run(['ruby','-rjson','-ryaml','-e',ruby]+paths,check=True,text=True,capture_output=True).stdout)
    unlocked={o for d in all_levels if d['level']<222 for o in map(int,d['obstacleInstances'])}
    allowed=unlocked|set(range(84,96)) # Dimerdal colors/square sizes are variants of family introduced at 221.
    reports=[]
    for d in ds:
        n=d['level'];w,h=d['width'],d['height'];size=w*h;p=ROOT/d['asset'];text=p.read_text();raw=payloads[str(p)]
        assert (int_field(text,'width'),int_field(text,'height'),int_field(text,'moves'))==(w,h,d['moves'])
        assert int_field(text,'levelKind')==0 and 1<=len(d['goals'])<=5
        assert raw['refillSpecialGuard']==.75 and array(text,'randomPool',4)==[0,1,2,3]
        data={k:array(text,k,size) for k in ARRAYS};assert data==d['data']
        active={i for i,v in enumerate(data['cells']) if v}
        assert set(data['cells'])<={0,1} and len(components(active,w,h))==1
        for k in ('pinnedTileTypes','pinnedSpecialTypes','wallPieceIds','lampColorByCell','hamsterSatietyByCell'):
            assert not any(data[k]),(n,k)
        counts=Counter();regions={};origins=[]
        for i,o in enumerate(data['obstacles']):
            if not o:assert data['obstacleOrigins'][i]==-1;continue
            assert o in allowed and i in active and o not in range(71,75)
            origin=data['obstacleOrigins'][i];assert 0<=origin<size
            assert data['obstacles'][origin]==o and data['obstacleOrigins'][origin]==origin
            if i!=origin:continue
            fw,fh=footprint(o);assert origin%w+fw<=w and origin//w+fh<=h
            expected={origin+dy*w+dx for dy in range(fh) for dx in range(fw)}
            actual={j for j,v in enumerate(data['obstacles']) if v==o and data['obstacleOrigins'][j]==origin}
            assert actual==expected and expected<=active
            counts[o]+=1;regions.setdefault(o,set()).update(expected);origins.append((o,origin,expected))
        for e in d['custom']['stackedObstacles']:
            origin=e['originCellIndex'];o=e['obstacleId'];fw,fh=footprint(o)
            expected={origin+dy*w+dx for dy in range(fh) for dx in range(fw)}
            assert expected<=active and e['stackOrder']==1 and e['initialRemainingHits']==0
            if o==28:assert all(data['obstacles'][i]==25 for i in expected),'Mud belongs below the solid'
            elif o==42:assert all(data['obstacles'][i]==68 for i in expected),'Grass is the upper cover'
            else:raise AssertionError((n,'Unexpected authored cover',o))
            counts[o]+=1;regions.setdefault(o,set()).update(expected)
        for e in d['custom']['ancientSeals']:
            origin=e['originCellIndex'];assert e['width']==e['height']==4 and e['stackOrder']==1
            expected={origin+dy*w+dx for dy in range(4) for dx in range(4)}
            assert expected<=active and all(data['obstacles'][i]==25 for i in expected)
            assert [e[k] for k in ('redCount','yellowCount','greenCount')]==[9,9,9]
            counts[64]+=1
        for k in CUSTOM:assert (raw.get(k) or [])==d['custom'][k],(n,k)
        assert raw['goals']==[dict(g,iconOverride=parse_ref(g['iconOverride'])) for g in d['goals']]
        goals=d['goals'];assert all(g['amount']>0 and 'guid:' in g['iconOverride'] for g in goals)
        assert len({(g['targetType'],g['obstacleId'] if g['targetType']==1 else g['collectibleId'] if g['targetType']==2 else g['tileType']) for g in goals})==len(goals)
        quotas={g['obstacleId']:g['amount'] for g in goals if g['targetType']==1}
        diamonds=sum(g['amount'] for g in goals if g['targetType']==2 and g['collectibleId']==4)
        keys=sum(g['amount'] for g in goals if g['targetType']==0 and g['tileType']==9)
        for o,count in counts.items():
            if 84<=o<=95:assert diamonds>0 and o not in quotas
            elif o==44:assert keys>0 and o not in quotas
            else:assert quotas[o]==count,(n,o,quotas,count)
        assert set(quotas)<=set(counts) and not set(quotas)&{35,36,38,45,44,78,*range(84,96)}
        for o,cells in regions.items():
            if footprint(o)==(1,1):assert all(len(c)>=4 for c in components(cells,w,h)),(n,o,'small detached island')
        removable=set(counts)-{44,*range(84,96)}
        phases=[]
        for name,removed in [('opening',set()),('shown cutaway',set(d['previewRemoved'])),('all removable layers cleared',removable)]:
            slots,reached,seeded=feed(d,removed)
            empty=slots-reached-seeded
            if name=='all removable layers cleared':assert not empty,('Permanent producer left a refill pocket',n,empty)
            phases.append(dict(name=name,tileSlots=len(slots),natural=len(reached),shadowSeeded=len(seeded),empty=sorted(empty)))
        post=layers(d,removable);free={i for i in active if not any(stage(o,'blocksCells') for o,_ in post[i])}
        source_access=[]
        for o,origin,region in origins:
            if o!=44 and not 84<=o<=95:continue
            # A source group rests on the lower boundary; an upper 1x1 machine
            # may have another permanent machine directly below it.
            permanent={i for i,cell in enumerate(post) if any(o==44 or 84<=o<=95 for o,_ in cell)}
            bottom=max(i//w for i in region)
            below={yy*w+x for yy in range(bottom+1,h) for x in {i%w for i in region} if yy*w+x in active}
            assert below<=permanent,(n,'normal cells below a permanent source',below-permanent)
            for i in region:
                if any(j//w==i//w or j%w==i%w for j in free):continue
                raise AssertionError((n,'source lacks a line attack surface',o,i))
            source_access.append(dict(obstacleId=o,origin=origin,footprint=sorted(region)))
        assert raw['movableSpawnFallbackMoves']==0 and bytes.fromhex(re.search(r'^  movableSpawnColumns: (\w+)$',text,re.M)[1])==bytes(w)
        assert raw['bossToolThreatEnabled']==0 and not raw['bossWaves'] and not raw['tutorialSteps']
        assert len(d['custom']['tarpStatues'])==(1 if n==224 else 0)
        if n==224:assert d['custom']['tarpStatues'][0]==dict(originCellIndex=3,color=2,count=8,size=2)
        live=next(e for e in all_levels if e['level']==n);assert live['asset']==d['asset'] and not live['geometryWarnings']
        name=next(e for e in names['entries'] if e['level']==n)
        assert name=={k:d[k] for k in ('level','levelKey','name','nameEn')}
        assert re.search(r'^guid: (\w+)$',p.with_suffix('.asset.meta').read_text(),re.M)[1]==d['guid']
        assert (ROOT/f'Docs/LevelDesign/Level{n}-{d["slug"]}.png').exists()
        reports.append(dict(level=n,name=d['name'],moves=d['moves'],difficulty='Hard' if n==225 else 'Normal',active=len(active),
            goals=[dict(label=('Diamond' if g['targetType']==2 else 'Key' if g['targetType']==0 else str(g['obstacleId'])),amount=g['amount']) for g in goals],
            initialInstances=dict(counts),goalCount=len(goals),phases=phases,permanentSources=source_access,pins=0,preparedSpecials=0))
    result=dict(result='PASS',previousCatalogEntriesPreserved=221,protectedFiles=len(baseline),changedProtectedFiles=changed,
        priorNamesPreserved=len(old['entries']),levels=reports,limits='Static independent Unity YAML, layers, footprints, initial feed geometry and source access. No Unity/build/Play/bot/gravity/cascade simulation, publication or release baseline. Balance and device performance unmeasured.')
    (HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print('PASS:',len(baseline),'protected files unchanged; catalog 1–221 and prior names preserved.')
    for d in reports:print(d['level'],d['active'],'active',d['goalCount'],'goals',d['moves'],'moves; final producer refill has no empty pocket')

def parse_ref(t):
    m=re.search(r'fileID: (-?\d+), guid: (\w+), type: (\d+)',t)
    return dict(fileID=int(m[1]),guid=m[2],type=int(m[3]))
if __name__=='__main__':
    if (HERE/'active-revision.json').exists():
        import runpy
        revision=json.loads((HERE/'active-revision.json').read_text())
        runpy.run_path(str(HERE/revision['verifier']),run_name='__main__')
    else:main()
