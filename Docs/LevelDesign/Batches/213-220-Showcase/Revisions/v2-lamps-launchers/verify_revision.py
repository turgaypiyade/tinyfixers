"""Current serialized levels + supported lamp paths/launcher rays. Static, no move simulation."""
from pathlib import Path
from collections import Counter
import hashlib,json,re,struct,subprocess,sys
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
ROOT=next(p for p in HERE.parents if(p/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').exists())
sys.path.insert(0,str(BATCH))
from common import review,layer_model,feed,footprint,stage,array
from export_level_previews import parse_entries
REVISION='v2-lamps-launchers'
def groups(cells,w,h):
    left=set(cells);out=[]
    while left:
        group={left.pop()};q=list(group)
        while q:
            i=q.pop();x,y=i%w,i//w
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                j=(y+dy)*w+x+dx
                if 0<=x+dx<w and 0<=y+dy<h and j in left:left.remove(j);group.add(j);q.append(j)
        out.append(group)
    return out
def main():
    baseline=json.loads((HERE/'protected-before.json').read_text())
    changed=[p for p,digest in baseline.items() if not(ROOT/p).exists() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=digest]
    observed=HERE/'concurrent-changes.json'
    concurrent={e['path'] for e in json.loads(observed.read_text())['files']} if observed.exists() else set()
    assert not set(changed)-concurrent,('unexplained protected-file edits',set(changed)-concurrent)
    authored=json.loads((BATCH/'designs.json').read_text());levels=review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')
    paths=[str(ROOT/d['asset']) for d in authored]
    ruby="out={}; ARGV.each { |p| t=File.readlines(p).reject { |l| l.start_with?('%','---') }.join; out[p]=YAML.safe_load(t, aliases: false)['MonoBehaviour'] }; puts JSON.generate(out)"
    payloads=json.loads(subprocess.run(['ruby','-rjson','-ryaml','-e',ruby]+paths,check=True,text=True,capture_output=True).stdout)
    reports=[];lamp_levels=[];launcher_levels=[]
    for d in authored:
        n=d['level'];actual=next(p for p in levels if p['level']==n);t=(ROOT/d['asset']).read_text();w,h=d['width'],d['height'];size=w*h;p=payloads[str(ROOT/d['asset'])]
        assert not actual['geometryWarnings']
        assert (p['width'],p['height'],p['moves'],p['levelKind'])==(w,h,d['moves'],d['kind'])
        assert w==9 and h==(10 if n==220 else 11)
        a={key:array(t,key,size) for key in d['data']};assert a==d['data']
        active={i for i,c in enumerate(a['cells']) if c};assert len(groups(active,w,h))==1
        assert not any(a['pinnedSpecialTypes']);assert array(t,'randomPool',4)==[0,1,2,3]
        counts=Counter();regions={};lamp_origins={};launcher_origins={}
        for i,o in enumerate(a['obstacles']):
            if not o:assert a['obstacleOrigins'][i]==-1;continue
            origin=a['obstacleOrigins'][i]
            assert i in active and origin in active and a['obstacles'][origin]==o and a['obstacleOrigins'][origin]==origin
            regions.setdefault((o,origin),set()).add(i)
        for (o,origin),region in regions.items():
            counts[o]+=1
            if o==79:
                assert origin==min(region) and len(region)>=4 and len(groups(region,w,h))==1
                neighbors={i:{j for j in region if abs(i%w-j%w)+abs(i//w-j//w)==1} for i in region}
                assert all(1<=len(v)<=2 for v in neighbors.values()) and sum(len(v)==1 for v in neighbors.values())==2
                ids={a['wallPieceIds'][i] for i in region};assert len(ids)==1 and min(ids)>0
                assert all(1<=a['lampColorByCell'][i]<=5 for i in region)
                lamp_origins[origin]=dict(pieceId=min(ids),cells=sorted(region),colors=[a['lampColorByCell'][i] for i in sorted(region)])
            else:
                fw,fh=footprint(o);assert origin%w+fw<=w and origin//w+fh<=h
                assert region=={origin+dy*w+dx for dy in range(fh) for dx in range(fw)}
            if o in (59,60,61,62):
                launcher_origins[origin]=o
                dx,dy={59:(0,-1),60:(0,1),61:(-1,0),62:(1,0)}[o]
                front=max(region,key=lambda i:(i%w)*dx+(i//w)*dy);x,y=front%w+dx,front//w+dy;ray=[]
                while 0<=x<w and 0<=y<h:ray.append(y*w+x);x+=dx;y+=dy
                assert len(set(ray)&active)>=3,(n,o,'launcher must point into board')
                assert not any(a['obstacles'][i]==78 for i in ray),'No permanent birdhouse blocks a launcher ray'
                assert any(j in active and a['obstacles'][j] for i in region for j in active-region if abs(i%w-j%w)+abs(i//w-j//w)==1),'Helper is attached to obstacle composition'
        for i in range(size):
            assert (a['obstacles'][i]==79)==(a['wallPieceIds'][i]>0)==(a['lampColorByCell'][i]>0)
        settings=parse_entries(t,'lampStripSettings')
        assert settings==d['custom'].get('lampStripSettings',[])
        assert {e['pieceId'] for e in settings}=={e['pieceId'] for e in lamp_origins.values()}
        if lamp_origins:
            lamp_levels.append(n);assert len(lamp_origins)==2
            assert sum(a['lampColorByCell'][i]==5 for i in active)==2
            assert all(e['shift']==1 for e in settings) if n==217 else [e['shift'] for e in settings]==[0,1]
        if launcher_origins:launcher_levels.append(n)
        assert parse_entries(t,'stackedObstacles')==d['custom']['stackedObstacles']
        for e in d['custom']['stackedObstacles']:
            i=e['originCellIndex'];o=e['obstacleId'];assert i in active and a['obstacles'][i]==21 and o==42 and e['stackOrder']==1
            counts[o]+=1
        counts[42]+=counts.get(58,0)
        if not counts[42]:del counts[42]
        for e in actual['custom']['waterChannels']:
            path=e['pathCellIndices'];assert len(path)==len(set(path)) and set(path)<=active and not a['obstacles'][path[0]]
            assert all(abs(i%w-j%w)+abs(i//w-j//w)==1 and j//w>=i//w for i,j in zip(path,path[1:]))
            assert path[-1]//w==h-1 and path[-1]-path[-2]==w
            assert all(not a['obstacles'][i] or footprint(a['obstacles'][i])==(1,1) and a['obstacles'][i]!=79 for i in path)
            counts[76]+=1
        assert actual['custom']['waterChannels']==d['custom']['waterChannels']
        assert p['goals']==[{**g,'iconOverride':{'fileID':0}} for g in d['goals']]
        assert len(p['goals'])<=5
        quotas={g['obstacleId']:g['amount'] for g in d['goals'] if g['targetType']==1};collectibles={g['collectibleId']:g['amount'] for g in d['goals'] if g['targetType']==2}
        for o,count in counts.items():
            if o in (59,60,61,62):assert o not in quotas;continue
            if o==78:assert collectibles.get(3,0)>0;continue
            if o==80:assert quotas[83]==20*count;continue
            extra=sum(e['extra'] for e in d['refill'] if e['obstacleId']==o)
            assert quotas[o]==count+extra,(n,o,count,quotas)
        assert set(quotas)<=set(counts)|({83} if 80 in counts else set())|({46} if launcher_origins else set())
        if launcher_origins:
            assert quotas[46]==len(active) and not counts.get(78) and not counts.get(76)
        if n in (213,215,217,218):assert not set(a['obstacles'])&{9,11,12,13}
        cols=list(bytes.fromhex(re.search(r'^  movableSpawnColumns: (\w+)$',t,re.M)[1]));assert len(cols)==w and set(cols)<={0,1}
        if n==213:assert cols==[0,0,1,0,0,0,1,0,0]
        if n==220:assert cols==[0,1,0,0,0,0,0,1,0]
        pins={i for i,v in enumerate(a['pinnedTileTypes']) if v}
        initial,_=feed(d);assert pins<=initial
        for i in pins:
            c=a['pinnedTileTypes'][i];assert 1<=c<=4
            assert not(i%w>=2 and a['pinnedTileTypes'][i-1]==a['pinnedTileTypes'][i-2]==c)
            assert not(i//w>=2 and a['pinnedTileTypes'][i-w]==a['pinnedTileTypes'][i-2*w]==c)
        checkpoints=[]
        phases=[('opening',set()),('movables cleared',{9,11,12,13,30,40,75}),('launchers cleared',{59,60,61,62}),('chests cleared',{21,42}),('frames and solid covers cleared',{21,59,60,61,62,79,80}),('all removable layers cleared',set(counts)-{78})]
        for label,removed in phases:
            slots,reached=feed(d,removed);unfed=slots-reached-pins;ls=layer_model(d,removed)
            for i in unfed:assert i not in initial and any(stage(o,'behavior')==3 for j in range(i%w,i,w) for o,_ in ls[j]),(n,label,'fixed-only shadow',i)
            if label in ('opening','all removable layers cleared'):assert not unfed
            checkpoints.append(dict(phase=label,tileSlots=len(slots),naturallyFed=len(reached),authoredPinsPresent=len((slots-reached)&pins),newlyExposedBelowFrozenMovables=sorted(unfed)))
        if 80 in counts:assert len(initial)>=quotas[83]
        if n==220:
            thrown=array(t,'bossThrownObstacles',3);assert not set(thrown)&set(quotas)
            assert collectibles[2]==620 and h<=10 and p['bossWaveCount']==3
        reports.append(dict(level=n,title=d['name'],moves=d['moves'],active=len(active),goals=len(p['goals']),counts=dict(counts),lampPieces=lamp_origins,launcherOrigins=launcher_origins,realPins=len(pins),checkpoints=checkpoints))
    assert lamp_levels==[217,218] and launcher_levels==[213,215]
    report=dict(result='PASS',revision=REVISION,levels=reports,lampStripLevels=lamp_levels,gelLauncherLevels=launcher_levels,protectedFiles=len(baseline),unchangedProtectedFiles=len(baseline)-len(changed),changedProtectedFiles=changed,observedConcurrentChanges=sorted(set(changed)&concurrent),limits='Independent serialized YAML and authored static geometry/layer/goal checks. Concurrent edits recorded separately were not made by this task. No Unity build/Play, bot/cascade/gravity simulation, device test or measured balance/FPS. Runtime GridSpawner fix is separately covered by production-method tests.')
    (BATCH/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');(HERE/'validation-current.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print('PASS: LampStrip217/218, GelLauncher213/215; all eight serialized layouts/goals/paths/pins checked;',len(baseline)-len(changed),'protected files unchanged;',len(changed),'concurrent edits recorded. Level meta/GUIDs, catalog and names preserved.')
    for d in reports:
        if d['level'] in (213,215,217,218):print(d['level'],d['moves'],'moves;',d['goals'],'goals;',d['realPins'],'real pins;',d['checkpoints'][0]['tileSlots'],'tile slots')
if __name__=='__main__':main()
