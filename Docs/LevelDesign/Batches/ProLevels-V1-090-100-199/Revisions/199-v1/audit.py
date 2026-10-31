"""Static authoring/entry checks only: no match, cascade, bot or Unity execution."""
from pathlib import Path
from collections import Counter,deque
import sys,json,re,hashlib,struct,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import footprint,stage
from export_level_previews import int_field,int_array,parse_entries
from designs import DESIGNS,KEYS

def neighbors(i,w,h):
    x,y=i%w,i//w
    for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
        if 0<=x+dx<w and 0<=y+dy<h:yield (y+dy)*w+x+dx

def groups(indices,w,h):
    left=set(indices);out=[]
    while left:
        start=left.pop();group={start};q=[start]
        while q:
            for j in neighbors(q.pop(),w,h):
                if j in left:left.remove(j);group.add(j);q.append(j)
        out.append(group)
    return out

def surface(d,opened=()):
    obs=d['data']['obstacles'][:];origins=d['data']['obstacleOrigins'][:]
    for s in d['stacks']:obs[s['originCellIndex']]=s['obstacleId'];origins[s['originCellIndex']]=s['originCellIndex']
    for m in d['magnets']:
        for i in m['pathCellIndices']:obs[i]=29;origins[i]=m['pathCellIndices'][0]
    for s in d['ancientSeals']:
        start=s['originCellIndex']
        for dy in range(s['height']):
            for dx in range(s['width']):i=start+dy*9+dx;obs[i]=64;origins[i]=start
    for i in opened:obs[i]=0;origins[i]=-1
    return obs,origins

def entry(d,top):
    w,h=d['w'],d['h'];cells=d['data']['cells'];active={i for i,a in enumerate(cells) if a}
    # Movables are stamped directly, so they are not treated as unknown random tiles.
    fixed={i for i in active if top[i] and stage(top[i],'blocksCells')}
    movable={i for i in active if top[i] and stage(top[i],'behavior')==3}
    slots=active-fixed-movable;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in fixed or i in movable:break
            if i in slots:reached.add(i)
    queue=deque(reached)
    def corner_open(k):
        if k not in active:return False
        return k not in fixed and k not in movable or bool(top[k] and stage(top[k],'allowDiagonal'))
    while queue:
        i=queue.popleft();x,y=i%w,i//w
        if y+1==h:continue
        for dx in (0,-1,1):
            if not 0<=x+dx<w:continue
            j=i+w+dx
            if j not in slots or j in reached:continue
            if dx and not(corner_open(i+dx) or corner_open(i+w)):continue
            reached.add(j);queue.append(j)
    return dict(slots=slots,reached=reached,empty=slots-reached,movable=movable,fixed=fixed)

def check(d):
    w,h=d['w'],d['h'];size=w*h;data=d['data'];top,orig=surface(d)
    assert len(groups((i for i,a in enumerate(data['cells']) if a),w,h))==1
    assert all(len(v)==size for v in data.values())
    assert all(c in (0,1) for c in data['cells'])
    used=set();counts=Counter()
    for i,o in enumerate(data['obstacles']):
        assert not o or data['cells'][i]
        assert data['obstacleOrigins'][i]==(i if o else -1)
        if o:assert footprint(o)==(1,1);counts[o]+=1
        if not data['cells'][i]:assert not data['pinnedTileTypes'][i] and not data['pinnedSpecialTypes'][i]
    for s in d['stacks']:
        i=s['originCellIndex'];assert data['obstacles'][i]==46 and s['obstacleId']==39
        counts[s['obstacleId']]+=1
    for m in d['magnets']:
        path=m['pathCellIndices'];assert len(path)==len(set(path)) and len(path)>=2
        for i in path:assert data['cells'][i] and not data['obstacles'][i] and i not in used;used.add(i)
        assert all(b in neighbors(a,w,h) for a,b in zip(path,path[1:]));counts[29]+=1
    for s in d['ancientSeals']:
        start=s['originCellIndex'];assert start%w+s['width']<=w and start//w+s['height']<=h
        for dy in range(s['height']):
            for dx in range(s['width']):
                i=start+dy*w+dx;assert data['cells'][i] and not data['obstacles'][i] and i not in used;used.add(i)
        counts[64]+=1
    before=entry(d,top);after_top,_=surface(d,d['opening']);after=entry(d,after_top)
    # A deliberately pinned TNT sits on a fixed shelf, never authored floating above slots.
    pins={i:p for i,p in enumerate(data['pinnedSpecialTypes']) if p}
    for i in pins:
        assert i in before['reached'] and data['pinnedTileTypes'][i] in range(1,5)
        lower=[j for j in range(i+w,size,w) if data['cells'][j]]
        assert not lower or lower[0] in before['fixed']
    # Movable presentation is stable: each item either has a fixed shelf below or a
    # contiguous column of authored movable pieces down to its last active cell.
    for i in before['movable']:
        for j in range(i+w,size,w):
            if not data['cells'][j]:continue
            if j in before['fixed']:break
            assert j in before['movable'],(d['n'],'movable would fall at initial settle',i,j)
    clear=[0]*size;fully=entry(d,clear)
    assert fully['reached']==fully['slots'],'Permanent mask/refill trap'
    assert len(after['reached'])>len(before['reached'])
    if d['n']<199:
        assert h<=10 and len(d['waves'])==3 and len(d['goals'])==1
        assert d['goals'][0]['targetType']==2 and d['goals'][0]['collectibleId']==2
        assert set(d['throws'])<={14,30,40} and d['pressureCap']<=7
        assert len(before['reached'])>=30 and not pins
        # The upper endpoint is immediate; clearing a side vein provides access to
        # the intentionally gated lower endpoint (not an invisible free opening).
        for m in d['magnets']:
            assert set(neighbors(m['pathCellIndices'][0],w,h)) & before['reached']
            vein_open=[0 if o==5 else o for o in top]
            assert set(neighbors(m['pathCellIndices'][-1],w,h)) & entry(d,vein_open)['reached']
    else:
        assert counts[28]==17 and counts[39]==17 and counts[45]==4 and counts[64]==1
        assert pins=={13:4} and d['goals'][0]['amount']==sum(data['cells'])==87
        assert all(top[i]==39 and abs(i%w-4)<=1 and abs(i//w-1)<=1 for i in d['opening'])
        assert len(after['reached'])-len(before['reached'])>=10
        assert not any(g['obstacleId'] in (38,43,45) for g in d['goals'])
    result=dict(level=d['n'],title=d['title'],active=sum(data['cells']),holes=size-sum(data['cells']),moves=d['moves'],
        instances=dict(counts),initialRandomTiles=len(before['reached']-set(pins)),initialMovables=len(before['movable']),
        initialEmptyCells=sorted(before['empty']),initialSpawnCells=sorted(before['reached']),
        openingSpawnCells=sorted(after['reached']),afterDeclaredOpening=len(after['reached']),preparedSpecials=pins,
        limits='Static geometry and counterfactual opening only. No matches, gravity settle, cascades, bot, builds or win-rate measurement.')
    return result

def run():
    results=[check(d) for d in DESIGNS]
    if '--assets' in sys.argv:
        catalog=(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').read_text()
        names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())['entries']
        previous_names=json.loads((HERE/'Before/LevelNames.json').read_text())['entries']
        assert [e for e in names if e['level'] not in (90,100,199)]==previous_names
        restored_catalog=catalog
        for d in DESIGNS:
            p=ROOT/f'Assets/_Project/Settings/ProLevels/LevelV1_{d["n"]*10:08d}.asset';t=p.read_text()
            raw=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(p)],text=True))['MonoBehaviour']
            assert (raw['width'],raw['height'],raw['moves'])==(9,d['h'],d['moves'])
            for k,expected in [('goals',d['goals']),('bossWaves',d['waves'])]:
                def flow(v):
                    return {k:float(x) if re.fullmatch(r'-?[0-9.]+',x) else x for k,x in re.findall(r'(\w+): ([^,{}]+)',v)}
                normalized=[{name:flow(v) if isinstance(v,str) and v.startswith('{') else v for name,v in row.items()} for row in expected]
                assert raw[k]==normalized,(d['n'],k)
            paths=[list(struct.unpack('<'+str(len(s)//8)+'i',bytes.fromhex(s))) for s in re.findall(r'^  - pathCellIndices: ([0-9a-f]+)$',t,re.M)]
            assert paths==[m['pathCellIndices'] for m in d['magnets']]
            assert int_array(t,'randomPool',4)==[0,1,2,3]
            for k in ('tubes','waterChannels','safes','tarpStatues','colorCrateInitialHits','paintCanBoxColors','tutorialSteps'):assert raw[k]==[]
            for k in KEYS:assert int_array(t,k,d['w']*d['h'],-1 if k=='obstacleOrigins' else 0)==d['data'][k]
            assert int_field(t,'moves')==d['moves'] and int_field(t,'levelKind')==int(d['n']!=199)
            for k,expected in [('stackedObstacles',d['stacks']),('ancientSeals',d['ancientSeals'])]:assert parse_entries(t,k)==expected
            guid=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1]
            key=re.search(r'    level: '+str(d['n'])+r'\n    levelKey: ([^\n]+)',catalog)[1]
            assert f'    level: {d["n"]}\n    levelKey: {key}\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}' in catalog
            restored_catalog=restored_catalog.replace(f'    level: {d["n"]}\n    levelKey: {key}\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}',f'    level: {d["n"]}\n    levelKey: {key}\n    levelData: {{fileID: 0}}')
            e=next(e for e in names if e['level']==d['n']);assert (e['name'],e['nameEn'],e['levelKey'])==(d['title'],d['nameEn'],key)
        assert restored_catalog==(HERE/'Before/LevelCatalogProV1.asset').read_text(),'Unrequested catalog edit'
        protected=json.loads((HERE/'protected-before.json').read_text())
        assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in protected.items())
        print('PASS:',len(protected),'pre-existing files preserved.')
    (HERE/'validation.json').write_text(json.dumps(dict(levels=results),ensure_ascii=False,indent=2)+'\n')
    for r in results:print({k:v for k,v in r.items() if k not in ('initialSpawnCells','openingSpawnCells','limits','initialEmptyCells')},'empty',len(r['initialEmptyCells']))
    print('PASS: three static layouts, native footprints, honest empty cells, movable stability, refill and opening access.')

if __name__=='__main__':run()
