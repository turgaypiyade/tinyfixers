"""Independent Unity YAML/array audit and geometric checkpoints. No gameplay simulation."""
from pathlib import Path
from collections import Counter
import hashlib,json,re,struct,subprocess
from common import ROOT,HERE,review,footprint,stage,array,layer_model,feed,DEFS
from export_level_previews import int_field,parse_entries
def components(region,w,h):
    left=set(region);groups=[]
    while left:
        group={left.pop()};q=list(group)
        while q:
            i=q.pop();x,y=i%w,i//w
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                j=(y+dy)*w+x+dx
                if 0<=x+dx<w and 0<=y+dy<h and j in left:left.remove(j);group.add(j);q.append(j)
        groups.append(group)
    return groups
def main():
    records=json.loads((HERE/'designs.json').read_text());levels=review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset');names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())
    assert [d['level'] for d in levels]==list(range(1,221))
    beforecat=(HERE/'Before/LevelCatalogProV1.asset').read_text();cat=(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').read_text();assert cat.startswith(beforecat)
    oldnames=json.loads((HERE/'Before/LevelNames.json').read_text());assert names['entries'][:len(oldnames['entries'])]==oldnames['entries']
    assert {k:v for k,v in names.items() if k!='entries'}=={k:v for k,v in oldnames.items() if k!='entries'}
    assert len(names['entries'])==len(oldnames['entries'])+8
    assert len({e['levelKey'] for e in names['entries']})==len(names['entries'])
    assert len({e['name'] for e in names['entries']})==len(names['entries'])
    paths=[str(ROOT/d['asset']) for d in records]
    ruby="out={}; ARGV.each { |p| t=File.readlines(p).reject { |l| l.start_with?('%','---') }.join; out[p]=YAML.safe_load(t, aliases: false)['MonoBehaviour'] }; puts JSON.generate(out)"
    payloads=json.loads(subprocess.run(['ruby','-rjson','-ryaml','-e',ruby]+paths,check=True,text=True,capture_output=True).stdout)
    baseline=json.loads((HERE/'protected-before.json').read_text())
    changed=[p for p,digest in baseline.items() if not(ROOT/p).exists() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=digest]
    assert not changed,('protected files changed',changed)
    reports=[];unlocked={o for d in levels if d['level']<=212 for o in map(int,d['obstacleInstances'])}
    # Requested Plastic_xxx colours share the already introduced colour-keyed plastic mechanic.
    if unlocked&{9,11,12,13}:unlocked|={9,11,12,13}
    for authored in records:
        n=authored['level'];d=next(p for p in levels if p['level']==n);w,h=d['width'],d['height'];size=w*h;t=(ROOT/d['asset']).read_text();payload=payloads[str(ROOT/d['asset'])]
        assert not d['geometryWarnings'];assert (w,h,d['moves'],d['kind'])==(9,10 if n==220 else 11,authored['moves'],int(n==220))
        assert payload['refillSpecialGuard']==.75 and array(t,'randomPool',4)==[0,1,2,3]
        a={key:array(t,key,size) for key in authored['data']};assert a==authored['data']
        assert len(components([i for i,v in enumerate(a['cells']) if v],w,h))==1
        assert all(v in (0,1) for v in a['cells'])
        assert not any(a['pinnedSpecialTypes']) and not any(a['wallPieceIds']) and not any(a['hamsterSatietyByCell'])
        assert not any(a['lampColorByCell'])
        counts=Counter();family_cells={}
        for i,o in enumerate(a['obstacles']):
            if not o:assert a['obstacleOrigins'][i]==-1;continue
            origin=a['obstacleOrigins'][i];assert a['cells'][i] and 0<=origin<size
            assert a['obstacles'][origin]==o and a['obstacleOrigins'][origin]==origin
            if i!=origin:continue
            fw,fh=footprint(o);assert origin%w+fw<=w and origin//w+fh<=h
            region={origin+dy*w+dx for dy in range(fh) for dx in range(fw)}
            actual={j for j,v in enumerate(a['obstacles']) if v==o and a['obstacleOrigins'][j]==origin}
            assert actual==region and all(a['cells'][j] for j in region)
            counts[o]+=1;family_cells.setdefault(o,set()).update(region)
        stacks=d['custom']['stackedObstacles'];assert stacks==authored['custom']['stackedObstacles']
        for e in stacks:
            assert e['obstacleId']==42 and e['stackOrder']==1 and e['initialRemainingHits']==0
            i=e['originCellIndex'];assert a['obstacles'][i]==21 and a['cells'][i]
            counts[42]+=1;family_cells.setdefault(42,set()).add(i)
        for key in ('tubes','magnets','safes','ancientSeals','tarpStatues','colorCrateInitialHits','paintCanBoxColors','lampStripSettings','tutorialSteps','bossWaves'):
            assert not parse_entries(t,key),(n,key)
        for e in d['custom']['waterChannels']:
            path=e['pathCellIndices'];assert len(path)>=2 and len(path)==len(set(path)) and all(0<=i<size and a['cells'][i] for i in path)
            assert not a['obstacles'][path[0]]
            for i,j in zip(path,path[1:]):assert abs(i%w-j%w)+abs(i//w-j//w)==1 and j//w>=i//w
            assert path[-1]//w==h-1 and path[-1]-path[-2]==w,'Waterfall exit points down'
            assert all(not a['obstacles'][i] or footprint(a['obstacles'][i])==(1,1) for i in path)
            counts[76]+=1
        assert d['custom']['waterChannels']==authored['custom']['waterChannels']
        assert set(counts)<=unlocked,'No new mechanics in 211–220'
        flower=counts.get(58,0);counts[42]+=flower
        if not counts[42]:del counts[42]
        if flower:family_cells.setdefault(42,set()).update(family_cells[58])
        goals=d['goals'];assert goals==[{k:v for k,v in g.items() if k!='iconOverride'} for g in authored['goals']] and 1<=len(goals)<=5
        assert all(g['iconOverride']=={'fileID':0} for g in payload['goals'])
        ids=[(g['targetType'],g['obstacleId'] if g['targetType']==1 else g['collectibleId']) for g in goals];assert len(ids)==len(set(ids))
        quotas={g['obstacleId']:g['amount'] for g in goals if g['targetType']==1};collectibles={g['collectibleId']:g['amount'] for g in goals if g['targetType']==2}
        assert all(g['amount']>0 for g in goals)
        for o,count in counts.items():
            if o==78:assert collectibles.get(3,0)>0;continue
            if o==80:
                assert quotas[83]%count==0 and quotas[83]//count==20;continue
            refill=next((r for r in authored['refill'] if r['obstacleId']==o),None)
            assert quotas[o]==count+(refill['extra'] if refill else 0),(n,o,count,quotas)
        assert set(quotas)<=set(counts)|({83} if 80 in counts else set())
        for o,cells in family_cells.items():
            if footprint(o)!=(1,1):continue
            assert all(len(group)>=4 for group in components(cells,w,h)),(n,o,'small detached group')
        for i,v in enumerate(a['pinnedTileTypes']):
            if not v:continue
            slots,_=feed(d);assert i in slots and 1<=v<=4
            assert not(i%w>=2 and a['pinnedTileTypes'][i-1]==a['pinnedTileTypes'][i-2]==v)
            assert not(i//w>=2 and a['pinnedTileTypes'][i-w]==a['pinnedTileTypes'][i-2*w]==v)
        phases=[('opening',set()),('movables removed',{9,11,12,13,30,40,75}),('flowers shed',{58}),('grass cleared',{42,58}),('removable solids cleared',{21,80}),('all removable layers cleared',set(counts)-{78})]
        checkpoints=[];pins={i for i,v in enumerate(a['pinnedTileTypes']) if v}
        initial_slots,_=feed(d)
        for label,removed in phases:
            slots,reached=feed(d,removed);unfed=slots-reached-pins
            # Frozen-family planes do not move falling obstacles. A newly opened solid
            # footprint can be filled by the movable column above it, which then admits
            # refill. Record that limit rather than treating the frozen plane as gravity.
            live_layers=layer_model(d,removed)
            for i in unfed:
                assert i not in initial_slots and any(stage(o,'behavior')==3 for j in range(i%w,i,w) for o,_ in live_layers[j]),(n,label,'unfed fixed-only pocket',i)
            if label in ('opening','all removable layers cleared'):assert not unfed
            checkpoints.append(dict(phase=label,tileSlots=len(slots),naturallyFed=len(reached),authoredPinsPresent=len((slots-reached)&pins),newlyExposedSlotsBelowMovables=sorted(unfed)))
        # Every permanent house has a row/column attack surface after removable layers clear.
        houses=family_cells.get(78,set());free={i for i,c in enumerate(a['cells']) if c}-houses
        special_only=[]
        for i in houses:
            x,y=i%w,i//w
            neighbors={yy*w+xx for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)) if 0<=xx<w and 0<=yy<h}
            if not neighbors&free:
                assert any(j//w==y for j in free),'Enclosed house needs a line attack surface'
                special_only.append(i)
        if 80 in counts:
            initial_slots,_=feed(d);assert len(initial_slots)>=quotas[83]
            for origin in {a['obstacleOrigins'][i] for i,o in enumerate(a['obstacles']) if o==80}:
                # LineV collects a whole column across mask holes; no prepared special is assumed.
                assert any(i%w in (origin%w,origin%w+1) for i in initial_slots)
        if n==220:
            assert h<=10 and collectibles[2]==620 and collectibles[3]==28
            thrown=array(t,'bossThrownObstacles',3);assert thrown==[8,12,13] and not set(thrown)&set(quotas)
            tools=bytes.fromhex(re.search(r'^  bossToolThreatTypes: (\w+)$',t,re.M)[1]);tool_ids=set(struct.unpack('<'+str(len(tools)//4)+'i',tools));assert not tool_ids&set(quotas)
            assert payload['bossToolThreatEnabled']==1 and payload['bossWaveCount']==3 and not payload['bossWaves']
        column_hex=re.search(r'^  movableSpawnColumns: (\w+)$',t,re.M)[1];cols=list(bytes.fromhex(column_hex));assert len(cols)==w and set(cols)<={0,1}
        assert int_field(t,'movableSpawnFallbackMoves')==0
        for refill in authored['refill']:
            assert counts[refill['obstacleId']]==refill['initial'] and quotas[refill['obstacleId']]==refill['goal']
            assert sum(cols)==2
        if n==213:assert cols==[0,0,1,0,0,0,1,0,0]
        if n==220:assert cols==[0,1,0,0,0,0,0,1,0]
        if not authored['refill']:assert not any(cols)
        actualname=next(e for e in names['entries'] if e['level']==n)
        assert actualname==dict(level=n,levelKey=d['levelKey'],name=authored['name'],nameEn=authored['titleEn'])
        guid=re.search(r'^guid: (\w+)$',Path(str(ROOT/d['asset'])+'.meta').read_text(),re.M)[1];assert guid==authored['guid']
        assert f'    level: {n}\n    levelKey: {d["levelKey"]}\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}' in cat
        reports.append(dict(level=n,title=d['name'],active=sum(a['cells']),moves=d['moves'],kind='Boss' if n==220 else 'Super Hard' if n==219 else 'Hard' if n==215 else 'Normal',goalCount=len(goals),initialInstances=dict(counts),refill=authored['refill'],realPins=len(pins),channelLengths=[len(p['pathCellIndices']) for p in d['custom']['waterChannels']],specialOnlyBirdHouseCells=special_only,checkpoints=checkpoints))
    report=dict(result='PASS',levels=reports,protectedFiles=len(baseline),changedProtectedFiles=changed,previousCatalogEntriesPreserved=212,previousNamesPreserved=len(oldnames['entries']),limits='Independent Unity YAML and authored geometry/layer/goal checks, not gravity/cascade/bot simulation. No Unity build/Play, device test, measured balance or FPS, upload, or app-release baseline.')
    (HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print('PASS: 213–220 arrays, footprints, layers, goals, mask connectivity, refill checkpoints, real pins, grouping, downward channels, boss/pressure, catalog/names and',len(baseline),'protected files.')
    for d in reports:print(d['level'],d['active'],'active;',d['goalCount'],'goals;',d['realPins'],'pins;',d['checkpoints'][0]['tileSlots'],'tile slots')
if __name__=='__main__':main()
