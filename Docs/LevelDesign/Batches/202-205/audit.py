"""Static masks/footprints/goals/refill/support. Does not execute moves, cascades or bots."""
from pathlib import Path
from collections import Counter,deque
import hashlib,json,re,struct,subprocess,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(HERE));sys.path.insert(0,str(ROOT/'Tools'))
from designs import DESIGNS,KEYS
from check_levels161_162 import footprint,stage
from export_level_previews import parse_entries,int_array,int_field

def neighbors(i,w,h):
    x,y=i%w,i//w
    for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
        if 0<=x+dx<w and 0<=y+dy<h:yield (y+dy)*w+x+dx
def components(cells,w,h):
    left=set(cells);out=[]
    while left:
        q=[left.pop()];component=set(q)
        while q:
            for n in neighbors(q.pop(),w,h):
                if n in left:left.remove(n);q.append(n);component.add(n)
        out.append(component)
    return out
def surface(d,opened=(),unseal=False):
    top=d['data']['obstacles'][:];orig=d['data']['obstacleOrigins'][:];sources=set()
    for c in d['channels']:
        p=c['pathCellIndices'];sources.add(p[0]);top[p[0]]=0;orig[p[0]]=-1
        for i in p[1:]:assert top[i]==0;top[i]=76;orig[i]=i
    if not unseal:
        for s in d['seals']:
            start=s['originCellIndex']
            for dy in range(s['height']):
                for dx in range(s['width']):i=start+dy*9+dx;top[i]=64;orig[i]=start
    for i in opened:top[i]=0;orig[i]=-1
    return top,orig,sources
def entry(d,top,sources):
    w,h=d['w'],d['h'];active={i for i,v in enumerate(d['data']['cells']) if v}
    fixed={i for i in active if top[i] and stage(top[i],'blocksCells')}
    movable={i for i in active if top[i] and stage(top[i],'behavior')==3}
    slots=active-fixed-movable-sources;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in fixed or i in movable:break
            if i in slots:reached.add(i)
    q=deque(reached)
    def corner(i):
        # Authored holes and channel source are pass-through voids; current BirdHouse allows diagonal.
        return i not in fixed and i not in movable or bool(top[i] and stage(top[i],'allowDiagonal'))
    while q:
        i=q.popleft();x,y=i%w,i//w
        if y+1==h:continue
        for dx in (0,-1,1):
            if not 0<=x+dx<w:continue
            j=i+w+dx
            if j not in slots or j in reached:continue
            if dx and not(corner(i+dx) or corner(i+w)):continue
            reached.add(j);q.append(j)
    return dict(slots=slots,reached=reached,empty=slots-reached,fixed=fixed,movable=movable)
def check(d):
    w,h=d['w'],d['h'];size=w*h;data=d['data'];active={i for i,v in enumerate(data['cells']) if v}
    assert w==9 and h<=11 and len(components(active,w,h))==1
    assert all(len(data[k])==size for k in KEYS)
    assert not any(data['hamsterSatietyByCell'])
    instances=Counter();used=set()
    for i,o in enumerate(data['obstacles']):
        if not o:assert data['obstacleOrigins'][i]==-1;continue
        assert i in active;origin=data['obstacleOrigins'][i];assert origin in active
        bw,bh=footprint(o);x,y=origin%w,origin//w
        assert x+bw<=w and y+bh<=h
        expected=next(g['cells'] for g in d['walls'] if g['origin']==origin) if o==55 else {(y+dy)*w+x+dx for dy in range(bh) for dx in range(bw)}
        expected=set(expected)
        actual={j for j,v in enumerate(data['obstacleOrigins']) if v==origin}
        assert expected==actual and expected<=active and all(data['obstacles'][j]==o for j in expected)
        if i==origin:instances[o]+=1
    wall_cells={i for g in d['walls'] for i in g['cells']}
    assert {i for i,v in enumerate(data['wallPieceIds']) if v}==wall_cells
    for g in d['walls']:
        assert len(components(g['cells'],w,h))==1
        assert all(data['wallPieceIds'][i]==g['piece'] and data['obstacleOrigins'][i]==g['origin'] for i in g['cells'])
    houses={i for i,o in enumerate(data['obstacles']) if o==78}
    regions=[{(g['y']+dy)*w+g['x']+dx for dy in range(g['height']) for dx in range(g['width'])} for g in d['groups']]
    assert set().union(*regions)==houses and len(houses)==sum(map(len,regions))
    assert {frozenset(g) for g in regions}=={frozenset(g) for g in components(houses,w,h)},'No detached single houses'
    for c in d['channels']:
        p=c['pathCellIndices'];assert len(p)>=2 and len(p)==len(set(p)) and set(p)<=active and not used.intersection(p)
        used.update(p);assert all(b in neighbors(a,w,h) and b//w>=a//w for a,b in zip(p,p[1:]))
        assert p[-1]//w==h-1 or p[-1]%w in (0,w-1)
        assert all(data['obstacles'][i]==0 for i in p)
        instances[76]+=1
    for s in d['seals']:
        start=s['originCellIndex'];region={(start//w+dy)*w+start%w+dx for dy in range(s['height']) for dx in range(s['width'])}
        assert region<=active and all(data['obstacles'][i]==39 for i in region) and not region&used and all(s[k]>0 for k in ('redCount','yellowCount','greenCount'))
        instances[64]+=1;used.update(region)
    assert all(g['obstacleId'] not in (38,43,45,78) for g in d['goals'] if g['targetType']==1)
    assert d['goals'][0]['targetType']==2 and d['goals'][0]['collectibleId']==3 and d['goals'][0]['amount']==d['birds']
    assert {g['obstacleId']:g['amount'] for g in d['goals'][1:]}=={o:n for o,n in instances.items() if o not in (78,38)}
    top,orig,sources=surface(d);before=entry(d,top,sources)
    aftertop,_,_=surface(d,d['opening'],unseal=d['n']==205);after=entry(d,aftertop,sources)
    pins={i:p for i,p in enumerate(data['pinnedSpecialTypes']) if p}
    assert {i for i,p in enumerate(data['pinnedTileTypes']) if p}==set(pins)
    for i,p in pins.items():
        assert i in before['reached'] and data['pinnedTileTypes'][i] in range(1,5)
        assert i+w in before['fixed'],'Prepared combo must rest on a shelf'
    for i in before['movable']:
        for j in range(i+w,size,w):
            if j not in active:continue
            if j in before['fixed']:break
            assert j in before['movable'],'Authored cargo/lantern would move at initial settle'
    eventual=[o if o==78 else 0 for o in data['obstacles']]
    full=entry(d,eventual,sources)
    assert full['slots']==full['reached'],'Permanent refill trap after destructible gates are opened'
    for i in houses:
        assert any(j in full['slots'] for j in neighbors(i,w,h)),'At least one future normal matching face per house'
    if d['n']==202:assert len(houses)==3 and d['birds']==18 and sum(data['cells'])==63 and not before['empty']
    if d['n']==203:
        assert {i%w for i,o in enumerate(data['obstacles']) if o==34}=={3,5}
        assert not any(data['obstacles'][y*w+x]==78 for x in (3,5) for y in range(3,h))
        assert len(before['movable'])==4 and len(after['empty'])==0 and d['birds']==12
        assert len(d['walls'])==1 and len(d['walls'][0]['cells'])==6
    if d['n']==204:assert len(houses)==4 and d['birds']==16 and not before['empty'] and len(before['movable'])==6
    if d['n']==205:
        assert not pins and len(d['opening'])==8 and len(d['seals'])==2 and d['birds']==24
        assert len(after['reached'])>len(before['reached']) and not after['empty']
        unsealed,_,_=surface(d,(),True)
        assert entry(d,unsealed,sources)['slots']==before['slots'],'Removing seals exposes special-only obstacles, not ordinary slots'
        assert {i for i,o in enumerate(top) if o==39}==set(),'All eight compasses initially covered'
    return dict(level=d['n'],title=d['title'],width=w,height=h,moves=d['moves'],difficulty=d['difficulty'],
        active=len(active),holes=size-len(active),instances=dict(instances),goals=d['goals'],
        initialTileCells=sorted(before['reached']),initialNormalTiles=len(before['reached']-set(pins)),
        initialMovables=sorted(before['movable']),initialEmptyCells=sorted(before['empty']),preparedSpecials=pins,
        afterOpeningTileCells=sorted(after['reached']),afterOpeningEmptyCells=sorted(after['empty']),
        limits='Static authored arrays/geometry, support, masks, goals and feed graphs. No moves/cascades/bots/Unity builds, measured balance or device FPS.')

def verify_assets():
    results=[check(d) for d in DESIGNS]
    assert [d['birds'] for d in DESIGNS]==[18,12,16,24]
    assert len({tuple(d['data']['cells']) for d in DESIGNS})==4,'Every revision has a distinct board silhouette'
    catalog=(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').read_text()
    assert hashlib.sha256(catalog.encode()).hexdigest()==json.loads((HERE/'Revisions/v2-before/protected-before.json').read_text())['Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset']
    for d in DESIGNS:
        p=ROOT/f'Assets/_Project/Settings/ProLevels/LevelV1_{d["n"]*10:08d}.asset';t=p.read_text();guid=re.search(r'^guid: (\w+)$',Path(str(p)+'.meta').read_text(),re.M)[1]
        assert f'    level: {d["n"]}\n    levelKey: LevelCL_0{d["n"]}\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}' in catalog
        raw=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(p)],text=True))['MonoBehaviour']
        assert (raw['width'],raw['height'],raw['moves'],raw['levelKind'])==(9,d['h'],d['moves'],0)
        for k in KEYS:
            blob=re.search(r'^  '+k+r': ([0-9a-f]+)$',t,re.M)[1]
            assert len(blob)==9*d['h']*8 and int_array(t,k,9*d['h'])==d['data'][k]
        assert int_array(t,'randomPool',4)==[0,1,2,3]
        assert [{k:v for k,v in g.items() if k!='iconOverride'} for g in d['goals']]==parse_entries(t,'goals')
        assert parse_entries(t,'ancientSeals')==d['seals']
        for k in ('stackedObstacles','safes','tubes','magnets','tarpStatues','colorCrateInitialHits','bossWaves','tutorialSteps'):
            assert not parse_entries(t,k),k
        channel_hex=re.findall(r'^  - pathCellIndices: ([0-9a-f]+)$',t,re.M)
        assert [list(struct.unpack('<'+str(len(b)//8)+'i',bytes.fromhex(b))) for b in channel_hex]==[c['pathCellIndices'] for c in d['channels']]
    names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())['entries'];prior=json.loads((HERE/'Before/LevelNames.json').read_text())['entries']
    assert [e for e in names if e['level']<202]==prior
    for d in DESIGNS:assert next(e for e in names if e['level']==d['n'])==dict(level=d['n'],levelKey=f'LevelCL_0{d["n"]}',name=d['title'],nameEn=d['nameEn'])
    t=(ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00002010.asset').read_text();prior201=(HERE/'Before/LevelV1_00002010.asset').read_text()
    assert t==prior201.replace('    amount: 12\n','    amount: 50\n'),'201 layout and every other field preserved'
    assert parse_entries(t,'goals')[0]['amount']==50
    protected=json.loads((HERE/'Revisions/v2-before/protected-before.json').read_text())
    changed=[p for p,h in protected.items() if hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    assert not changed,changed
    report=dict(revision='v2',levels=results,preservedFiles=len(protected),revision201='201 unchanged, including its user-requested 50 goal. 202–205 use local goals 18/12/16/24.')
    (HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print('PASS: v2 202–205 distinct silhouettes, local quotas, footprints, wall, support/feed/layers; 201, catalog, names, GUIDs and',len(protected),'files preserved.')

if __name__=='__main__':
    if '--assets' in sys.argv:verify_assets()
    else:
        for d in DESIGNS:
            result=check(d);print(d['n'],d['title'],'initial tiles',len(result['initialTileCells']),'empty',result['initialEmptyCells'],'after',len(result['afterOpeningTileCells']))
