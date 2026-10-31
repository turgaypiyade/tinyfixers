"""Static authored geometry, quota, catalog and preservation checks. No gameplay simulation."""
from pathlib import Path
from collections import deque
import hashlib, json, re, struct, subprocess, sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(HERE));sys.path.insert(0,str(ROOT/'Tools'))
from design import DESIGN,KEYS
from export_level_previews import int_field,int_array,parse_entries
from check_levels161_162 import stage,footprint
ASSET=ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00002010.asset'
CATALOG=ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset'
NAMES=ROOT/'Assets/_Project/Resources/LevelNames.json'

def neighbors(i,w,h):
    x,y=i%w,i//w
    for dx,dy in ((-1,0),(1,0),(0,-1),(0,1)):
        if 0<=x+dx<w and 0<=y+dy<h:yield (y+dy)*w+x+dx

def components(indices,w,h):
    remaining=set(indices);out=[]
    while remaining:
        start=remaining.pop();group={start};queue=[start]
        while queue:
            for n in neighbors(queue.pop(),w,h):
                if n in remaining:remaining.remove(n);group.add(n);queue.append(n)
        out.append(group)
    return out

def check_design(d=DESIGN):
    w,h=d['w'],d['h'];data=d['data'];size=w*h
    assert all(len(data[k])==size for k in KEYS)
    active={i for i,v in enumerate(data['cells']) if v};houses={i for i,v in enumerate(data['obstacles']) if v}
    assert houses<=active and len(active)==88 and len(houses)==12
    assert {data['obstacles'][i] for i in houses}=={78} and footprint(78)==(1,1)
    assert all(data['obstacleOrigins'][i]==(i if i in houses else -1) for i in range(size))
    assert all(not any(data[k]) for k in ('wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes'))
    assert len(components(active,w,h))==1 and len(components(active-houses,w,h))==1
    groups=components(houses,w,h);assert sorted(map(len,groups))==[6,6]
    expected=[{(y0+dy)*w+x0+dx for dy in range(bh) for dx in range(bw)} for x0,y0,bw,bh in d['groups']]
    assert {frozenset(g) for g in groups}=={frozenset(g) for g in expected}
    assert all((set(neighbors(i,w,h)) & (active-houses)) for i in houses),'Every house has a normal matching face'
    # Same static spawn-edge/down/diagonal reachability used by CascadeLogic.ComputeGravityReachableMask.
    # Holes pass vertical feed; BirdHouse blocks cells but allows diagonal corner passage.
    assert stage(78,'blocksCells')==1 and stage(78,'allowDiagonal')==1
    slots=active-houses;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in houses:break
            if i in slots:reached.add(i)
    q=deque(reached)
    def passable(i):return i in slots or i in houses and bool(stage(78,'allowDiagonal'))
    while q:
        i=q.popleft();x,y=i%w,i//w
        if y+1==h:continue
        for dx in (0,-1,1):
            if not 0<=x+dx<w:continue
            j=i+w+dx
            if j not in slots or j in reached:continue
            if dx and not(passable(i+dx) or passable(i+w)):continue
            reached.add(j);q.append(j)
    assert reached==slots and len(reached)==76,'No permanent empty shadow pockets under the upper group'
    assert d['birdGoal']==50 and d['moves']==26
    return dict(level=201,name=d['title'],width=w,height=h,moves=26,active=88,holes=2,
        birdHouseCount=12,groups=d['groups'],collectibleBirdGoal=50,normalSlots=76,
        spawnConnectedSlots=sorted(reached),emptySlots=[],preparedSpecials=0,
        limits='Static arrays, footprint, matching faces and refill graph only. No moves/cascades/bots/builds or measured win rate.')

def verify_assets():
    result=check_design();text=ASSET.read_text()
    # Parse Unity's YAML payload too; serialized numeric arrays are compared as hex separately.
    raw=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e',
        'puts JSON.generate(YAML.safe_load(File.readlines(ARGV[0]).drop(3).join))',str(ASSET)],text=True))['MonoBehaviour']
    assert (raw['width'],raw['height'],raw['moves'],raw['levelKind'])==(9,10,26,0)
    for k in KEYS:
        blob=re.search(r'^  '+k+r': ([0-9a-f]+)$',text,re.M)[1]
        assert len(blob)==9*10*8 and int_array(text,k,90)==DESIGN['data'][k],k
    assert int_array(text,'randomPool',4)==[0,1,2,3]
    goals=parse_entries(text,'goals')
    assert len(goals)==1 and goals[0]['targetType']==2 and goals[0]['collectibleId']==3 and goals[0]['amount']==50
    for k in ('tutorialSteps','stackedObstacles','ancientSeals','waterChannels','magnets','safes','tubes','paintCanBoxColors','tarpStatues','colorCrateInitialHits','bossWaves'):
        assert not parse_entries(text,k),k
    assert int_field(text,'usesCustomIntro')==0 and int_field(text,'oilSpreadMaxCells')==0
    guid=re.search(r'^guid: (\w+)$',Path(str(ASSET)+'.meta').read_text(),re.M)[1]
    catalog=CATALOG.read_text();prior=(HERE/'Before/LevelCatalogProV1.asset').read_text()
    suffix=f'  - chapter: 9\n    level: 201\n    levelKey: LevelCL_0201\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n'
    assert catalog==prior+suffix,'Only append 201; preserve every earlier catalog reference'
    names=json.loads(NAMES.read_text());old=json.loads((HERE/'Before/LevelNames.json').read_text())
    assert [e for e in names['entries'] if e['level']!=201]==old['entries']
    assert [e for e in names['entries'] if e['level']==201]==[dict(level=201,levelKey='LevelCL_0201',name=DESIGN['title'],nameEn=DESIGN['nameEn'])]
    hints=(ROOT/'Assets/_Project/Data/Tutorial/ObstacleHintLibrary.asset').read_text()
    assert re.search(r'^  - id: 78\n.*?locKey: hint_obstacle_birdhouse',hints,re.M|re.S)
    protected=json.loads((HERE/'protected-before.json').read_text())
    changed=[p for p,h in protected.items() if not (ROOT/p).is_file() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    assert not changed,changed
    result['preservedExistingFiles']=len(protected)
    result['asset']=str(ASSET.relative_to(ROOT));result['guid']=guid
    (HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print('PASS: 201 arrays, grouped houses, collectible goal, all 76 refill routes, catalog, TR/EN names and',len(protected),'preserved files.')

if __name__=='__main__':
    if '--assets' in sys.argv:verify_assets()
    else:print(json.dumps(check_design(),ensure_ascii=False,indent=2))
