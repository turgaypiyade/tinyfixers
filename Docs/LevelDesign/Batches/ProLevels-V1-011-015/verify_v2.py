"""Static V2 layout/entry/reveal-graph audit; no Unity, bot, cascade or win-rate simulation."""
from pathlib import Path
from collections import Counter,deque
import hashlib,json,re,struct,sys

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from export_level_previews import int_field,int_array,parse_entries
LEVELS=ROOT/'Assets/_Project/Settings/ProLevels'


def top_of(d):
    top=d['data']['obstacles'][:]
    for s in d['stacks']:top[s['originCellIndex']]=s['obstacleId']
    return top


def gravity_graph(d,top):
    # Only 1x1 crates, oil and non-blocking Mud. Crates allow diagonal; non-blockers
    # pass corners regardless of stage allowDiagonal (current BoardCellStateQuery).
    cells=d['data']['cells'];w=9;h=d['h'];blocked={i for i,o in enumerate(top) if o in (4,5,6,14)}
    free={i for i,a in enumerate(cells) if a and i not in blocked};reachable=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in blocked:break
            if i in free:reachable.add(i)
    queue=deque(reachable)
    while queue:
        i=queue.popleft();x,y=i%w,i//w
        if y+1==h:continue
        for dx in (0,-1,1):
            nx=x+dx;j=(y+1)*w+nx
            if not 0<=nx<w or j not in free or j in reachable:continue
            if dx and not (cells[i+dx] or cells[i+w]):continue
            reachable.add(j);queue.append(j)
    # GridSpawner exceptions: Oil holds a tile; an Oil-only shadow is seeded.
    held={i for i,o in enumerate(top) if cells[i] and o==14};shadow=set()
    for i in free-reachable:
        above=[top[y*w+i%w] for y in range(i//w) if top[y*w+i%w] in (4,5,6,14)]
        if above and all(o==14 for o in above):shadow.add(i)
    pins={i for i,p in enumerate(d['data']['pinnedSpecialTypes']) if p}
    return dict(free=free,reachable=reachable,held=held,shadow=shadow,
                spawned=reachable|held|shadow|pins,empty=free-reachable-shadow-pins)


def components(indices,w=9):
    remaining=set(indices);groups=[]
    while remaining:
        seed=remaining.pop();group={seed};q=deque([seed])
        while q:
            i=q.popleft();x=i%w
            for j in (i-w,i+w,i-1 if x else -1,i+1 if x+1<w else -1):
                if j in remaining:remaining.remove(j);group.add(j);q.append(j)
        groups.append(group)
    return groups


def audit():
    protected=json.loads((HERE/'protected-v2-before.json').read_text())
    assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in protected.items()), 'Existing unrelated file changed'
    assert (LEVELS/'LevelCatalogProV1.asset').read_bytes()==(HERE/'Revisions/v1/LevelCatalogProV1.asset').read_bytes()
    assert (ROOT/'Assets/_Project/Resources/LevelNames.json').read_bytes()==(HERE/'Revisions/v1/LevelNames.json').read_bytes()
    assert (LEVELS/'LevelV1_00000110.asset').read_bytes()==(HERE/'Before/LevelV1_00000110.asset').read_bytes()
    names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())['entries']
    catalog=(LEVELS/'LevelCatalogProV1.asset').read_text()
    for d in json.loads((HERE/'designs.json').read_text()):
        n=d['n'];h=d['h'];size=9*h;p=LEVELS/(d['asset']+'.asset');text=p.read_text()
        assert int_field(text,'width')==9 and int_field(text,'height')==h and h<=11
        assert int_field(text,'moves')==d['moves'] and int_field(text,'levelKind')==0
        data={k:int_array(text,k,size,-1 if k=='obstacleOrigins' else 0) for k in d['data']}
        assert data==d['data'] and all(len(a)==size for a in data.values())
        d['data']=data;d['stacks']=parse_entries(text,'stackedObstacles')
        assert len(components(i for i,a in enumerate(data['cells']) if a))==1, 'Board must be edge-connected'
        for i,(active,o) in enumerate(zip(data['cells'],data['obstacles'])):
            assert active in (0,1) and o in (0,4,5,6,14,25)
            assert active or (o==0 and not data['pinnedSpecialTypes'][i])
            assert data['obstacleOrigins'][i]==(i if o else -1)
        assert not any(data['pinnedTileTypes'])
        assert not any(data['wallPieceIds']+data['hamsterSatietyByCell'])
        assert struct.unpack('<4i',bytes.fromhex(re.search(r'^  randomPool: (\w+)',text,re.M)[1]))==(0,1,2,3)
        top=top_of(d);counts=Counter(o for o in data['obstacles'] if o);stacked=set()
        for s in d['stacks']:
            i=s['originCellIndex'];assert i not in stacked and data['obstacles'][i]==25 and s['obstacleId'] in (4,5,6)
            assert s['stackOrder']==0 and s['initialRemainingHits']==0
            counts[s['obstacleId']]+=1;stacked.add(i)
        goals=parse_entries(text,'goals');assert all(g['targetType']==1 and g['amount']>0 for g in goals)
        assert {g['obstacleId']:g['amount'] for g in goals}==dict(counts)
        pins={i:v for i,v in enumerate(data['pinnedSpecialTypes']) if v}
        assert sorted(pins.values())=={12:[2],13:[],14:[1],15:[2,4]}[n]
        assert all(top[i]==0 and data['cells'][i] for i in pins)
        # No later tile-slot in a special's unblocked segment: it stays in position at settle.
        for i in pins:
            for y in range(i//9+1,h):
                j=y*9+i%9
                if top[j] in (4,5,6,14):break
                assert not data['cells'][j], (n,'special would fall before use',i,j)
        initial=gravity_graph(d,top)
        d['initialEmptyCells']=sorted(initial['empty']);d['initialSpawnCells']=sorted(initial['spawned'])
        assert len(initial['reachable']-set(pins))>=20
        # Counterfactual mask checkpoints, not a hit/cascade engine. Prove the declared
        # opening connects useful tile slots, rather than a permanently dead pocket.
        opened=top[:]
        for i in d['planOpenCells']:
            assert top[i] in ((14,) if n==14 else (4,))
            opened[i]=25 if i in stacked else 0
        next_graph=gravity_graph(d,opened)
        if n in (12,13,15):assert len(next_graph['reachable'])>len(initial['reachable'])
        if n==12:
            assert pins=={22:2} and d['planOpenCells']==[49]
            assert all(not data['cells'][y*9+4] for y in (3,4))
            assert len(next_graph['reachable'])-len(initial['reachable'])>=10
        if n==13:
            assert len(d['planOpenCells'])==9
            assert all(not data['cells'][y*9+x] for y in range(6,9) for x in range(3,6))
        if n==14:
            assert pins=={47:1} and counts[14]==12 and int_field(text,'oilSpreadMaxCells')==18
            assert len(components(initial['free']))==2 and len(components(next_graph['free']))==1
            assert all(i//9==5 for i in d['planOpenCells'])
        if n==15:
            assert pins=={22:4,23:2} and len(d['planOpenCells'])==6
            # Real Line+Pulse: 3 rows + 3 columns. Either swap direction includes gate row3.
            for cx in (4,5):assert all(abs(i//9-2)<=1 or abs(i%9-cx)<=1 for i in d['planOpenCells'])
            assert len(next_graph['reachable'])-len(initial['reachable'])>=12
            assert any(o in (5,6) for o in opened)
        uncovered=[25 if i in stacked else 0 if o in (4,5,6,14) else o for i,o in enumerate(top)]
        fully_open=gravity_graph(d,uncovered)
        assert fully_open['reachable']==fully_open['free'], 'Permanent refill trap after covers removed'
        guid=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1];assert guid==d['guid']
        assert f'    level: {n}\n    levelKey: LevelCL_{n:03d}\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}' in catalog
        name=next(e for e in names if e['level']==n);assert name['name']==d['title'] and name['nameEn']==d['nameEn']
        result=dict(level=n,revision=2,name=d['title'],width=9,height=h,moves=d['moves'],
                    active=sum(data['cells']),holes=size-sum(data['cells']),obstacles=dict(counts),
                    initialOrdinaryTiles=len(initial['spawned']-initial['held']-set(pins)),preparedSpecials=pins,
                    intentionallyEmptyCells=sorted(initial['empty']),spawnConnectedBefore=len(initial['reachable']),
                    spawnConnectedAfterDeclaredOpening=len(next_graph['reachable']),firstBeat=d['cue'],
                    maxAuthoredDepth=2 if stacked else 1,limits='Static topology only; no actual hit/board simulation or measured win rate.')
        print(result);d['goals']=goals
        yield d,result
    print('PASS: four V2 designs, special stability/entry graphs, original11 and 3007 old files preserved; names/GUIDs/catalog unchanged.')


if __name__=='__main__':
    audited=list(audit())
    (HERE/'render-data-v2.json').write_text(json.dumps([d for d,r in audited],ensure_ascii=False,indent=2)+'\n')
    (HERE/'validation.json').write_text(json.dumps(dict(revision=2,levels=[r for d,r in audited],preservedExistingFiles=3007,level11Preserved=True),ensure_ascii=False,indent=2)+'\n')
