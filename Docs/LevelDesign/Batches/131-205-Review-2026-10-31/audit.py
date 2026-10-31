"""Independent serialized-data audit. Static graph/authoring checks only, no moves/bot/cascade simulation."""
from pathlib import Path
from collections import Counter,deque
import copy,hashlib,json,re,struct,sys,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import array,footprint,stage,DEFS
from export_level_previews import parse_entries,int_field
from review_level_patterns import path_entries
KEYS=('cells','obstacles','obstacleOrigins','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')
CUSTOM=('stackedObstacles','safes','ancientSeals','tubes','magnets','waterChannels','tarpStatues','colorCrateInitialHits')
BEFORE=json.loads((HERE/'inventory-before.json').read_text());BY={d['level']:d for d in BEFORE};PAYLOADS={}
def component(region,w,h):
    left=set(region);sizes=[]
    while left:
        q=[left.pop()];size=0
        while q:
            i=q.pop();size+=1;x,y=i%w,i//w
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                j=(y+dy)*w+x+dx
                if 0<=x+dx<w and 0<=y+dy<h and j in left:left.remove(j);q.append(j)
        sizes.append(size)
    return sorted(sizes,reverse=True)
def read(row,phase):
    d=copy.deepcopy(row);path=ROOT/row['asset'] if phase=='After' else HERE/phase/Path(row['asset']).name;t=path.read_text()
    # Parse Unity payload independently; serialized int arrays are decoded from raw hex below.
    payload=PAYLOADS[str(path)]
    d['width'],d['height'],d['moves'],d['kind']=[payload[k] for k in ('width','height','moves','levelKind')];size=d['width']*d['height']
    d['data']={k:(array(t,k,size) if re.search(r'^  '+k+r': [0-9a-f]+$',t,re.M) else [0]*size) for k in KEYS}
    d['goals']=payload['goals'];d['custom']={k:(path_entries(t,k) if k in ('magnets','waterChannels') else parse_entries(t,k)) for k in CUSTOM}
    d['payload']=payload;d['raw']=t
    return d
def topology(d):
    w,h=d['width'],d['height'];a=d['data'];top=a['obstacles'][:];orig=a['obstacleOrigins'][:];active={i for i,v in enumerate(a['cells']) if v};sources=set()
    counts=Counter(o for i,o in enumerate(top) if o and orig[i]==i)
    for key,o in (('tubes',26),('magnets',29)):
        for p in d['custom'][key]:
            if key=='tubes':
                dx,dy=((0,-1),(0,1),(-1,0),(1,0))[p['direction']];indices=[p['originCellIndex']+j*(dx+dy*w) for j in range(p['length'])]
            else:indices=p['pathCellIndices']
            assert len(indices)>=2 and len(indices)==len(set(indices)) and set(indices)<=active,(d['level'],'path',key,p)
            for i in indices:top[i]=o;orig[i]=indices[0]
        if d['custom'][key]:counts[o]=len(d['custom'][key])
    for p in d['custom']['waterChannels']:
        path=p['pathCellIndices'];assert len(path)>=2 and len(path)==len(set(path)) and set(path)<=active
        for i,j in zip(path,path[1:]):assert abs(i%w-j%w)+abs(i//w-j//w)==1 and j//w>=i//w
        last=path[-1];x,y=last%w,last//w
        assert y==h-1 or x in (0,w-1) or any(0<=x+dx<w and 0<=y+dy<h and (y+dy)*w+x+dx not in active for dx,dy in ((1,0),(-1,0),(0,1))),('channel exit',d['level'])
        sources.add(path[0]);top[path[0]]=0;orig[path[0]]=-1
        for i in path[1:]:
            assert not top[i] or top[i]==76 or footprint(top[i])==(1,1),(d['level'],'large cover on channel')
            if not top[i] or top[i]==76:top[i]=76;orig[i]=i
    if d['custom']['waterChannels']:counts[76]=len(d['custom']['waterChannels'])
    overlays=[(p.get('stackOrder',0),0,p['obstacleId'],p) for p in d['custom']['stackedObstacles']]
    overlays += [(p.get('stackOrder',0),1,33,p) for p in d['custom']['safes']]
    overlays += [(p.get('stackOrder',0),2,64,p) for p in d['custom']['ancientSeals']]
    for _,kind,o,p in sorted(overlays,key=lambda v:v[:2]):
        bw,bh=(p.get('width',2),p.get('height',2)) if kind else footprint(o)
        if o==68:bw=bh=next(q['size'] for q in d['custom']['tarpStatues'] if q['originCellIndex']==p['originCellIndex'])
        region={p['originCellIndex']+dy*w+dx for dy in range(bh) for dx in range(bw)}
        assert region<=active and p['originCellIndex']%w+bw<=w,(d['level'],'overlay footprint',o,p)
        counts[o]+=1
        for i in region:top[i]=o;orig[i]=p['originCellIndex']
    d.update(top=top,topOrigins=orig,obstacleInstances=dict(counts),blocked=[bool(o and (stage(o,'blocksCells') or stage(o,'behavior')==3)) for o in top])
    return +counts,sources
def coverage(goals):
    known=set()
    for g in goals:
        if g['targetType']==1:
            known.add(g['obstacleId'])
            if g['obstacleId']==42:known.add(58)
        elif g['targetType']==0 and g['tileType']==9:known.add(44)
        elif g['targetType']==2:known|={1:{31,22},3:{78}}.get(g['collectibleId'],set())
    return known
def audit(d):
    n=d['level'];w,h=d['width'],d['height'];a=d['data'];active={i for i,v in enumerate(a['cells']) if v};counts,sources=topology(d)
    assert w<=9 and h<=11 and (18<=d['moves']<=40 or n==198 and d['moves']==17),(n,'dimensions/moves')
    assert not d['kind'] or h<=10,(n,'boss >10 rows')
    assert len(d['goals'])<=5 and all(g['amount']>0 for g in d['goals']),(n,'HUD goal limit')
    assert not (set(counts)-coverage(d['goals'])),(n,'unrepresented obstacles',set(counts)-coverage(d['goals']))
    identities=[(g['targetType'],g['obstacleId'] if g['targetType']==1 else g['tileType'] if g['targetType']==0 else g['collectibleId']) for g in d['goals']]
    assert len(identities)==len(set(identities)),(n,'duplicate goals')
    for i,o in enumerate(a['obstacles']):
        if not o:assert a['obstacleOrigins'][i]==-1,(n,'empty origin',i);continue
        origin=a['obstacleOrigins'][i];assert i in active and origin in active and a['obstacles'][origin]==o and a['obstacleOrigins'][origin]==origin,(n,'origin',i)
        if i!=origin:continue
        actual={j for j,v in enumerate(a['obstacleOrigins']) if v==origin and a['obstacles'][j]==o}
        if o in (55,56):assert len(component(actual,w,h))==1,(n,'disconnected wall')
        elif o in (26,29):
            if o==26:
                p=next(p for p in d['custom']['tubes'] if p['originCellIndex']==origin);dx,dy=((0,-1),(0,1),(-1,0),(1,0))[p['direction']]
                expected={origin+j*(dx+dy*w) for j in range(p['length'])}
            else:expected=set(next(p['pathCellIndices'] for p in d['custom']['magnets'] if p['pathCellIndices'][0]==origin))
            assert actual==expected,(n,'authored path footprint',o,origin)
        else:
            bw,bh=footprint(o)
            if o==68:bw=bh=next(p['size'] for p in d['custom']['tarpStatues'] if p['originCellIndex']==origin)
            assert origin%w+bw<=w and origin//w+bh<=h
            expected={origin+dy*w+dx for dy in range(bh) for dx in range(bw)}
            assert actual==expected,(n,'rectangle footprint',o,origin,actual,expected)
    for p in d['custom']['colorCrateInitialHits']:
        i=p['originCellIndex'];assert i in active and a['obstacles'][i] in (71,72,73,74) and p['obstacleId']==a['obstacles'][i] and 1<=p['remainingHits']<=3,(n,'crate initial stage')
    for i,pin in enumerate(a['pinnedSpecialTypes']):
        if pin:assert i in active and not d['blocked'][i],(n,'blocked prepared special',i)
    for g in d['goals']:
        if g['targetType']!=1:continue
        o=g['obstacleId']
        if o==46:
            expected=len(active)-sum(counts.get(p,0) for p in (31,44,78));assert g['amount']==expected,(n,'gel quota',g['amount'],expected)
        elif o==42:assert g['amount']>=counts.get(42,0)+counts.get(58,0)
        elif o not in (44,53):assert g['amount']>=counts.get(o,0),(n,'less than initial targets',o)
    if counts.get(58):assert 42 in coverage(d['goals']),(n,'grass remains after flower goal')
    if counts.get(52) or counts.get(54):assert 53 in coverage(d['goals']),(n,'dynamic puddle creates sixth goal')
    if d['kind']:
        payload=d['payload'];raw=re.search(r'^  bossThrownObstacles: ([0-9a-f]+)$',d['raw'],re.M)
        pool=list(struct.unpack('<'+str(len(raw[1])//8)+'i',bytes.fromhex(raw[1]))) if raw else []
        assert pool and set(pool)<=coverage(d['goals']) and payload['bossToolThreatEnabled'],(n,'unlisted pressure type')
        tools=re.search(r'^  bossToolThreatTypes: ([0-9a-f]+)$',d['raw'],re.M)
        assert tools and struct.unpack('<i',bytes.fromhex(tools[1]))==(47,) and counts[47]==1 and 47 in coverage(d['goals']),(n,'unlisted or unreachable tool threat')
        assert any(g['targetType']==2 and g['collectibleId']==2 for g in d['goals'])
        before=read(BY[n],'Before')['payload']
        for field in ('playerMaxHp','bossWaveCount','bossWaves','enemyAttackBaseDamage','damagePerClearedTile','bossAttackEveryMoves','bossAttackOilCount'):
            assert payload.get(field)==before.get(field),(n,'combat tuning changed',field)
    return dict(level=n,width=w,height=h,active=len(active),maskComponents=component(active,w,h),moves=d['moves'],goals=len(d['goals']),families=dict(counts))
def main():
    phase='After' if '--assets' in sys.argv else 'Draft';reports=[];inventory=[]
    paths=[(ROOT/row['asset'] if ph=='After' else HERE/ph/Path(row['asset']).name) for ph in (phase,'Before') for row in BEFORE if row['level']>130]
    ruby="out={}; ARGV.each { |p| t=File.readlines(p).reject { |l| l.start_with?('%','---') }.join; out[p]=YAML.safe_load(t, aliases: false)['MonoBehaviour'] }; puts JSON.generate(out)"
    PAYLOADS.update(json.loads(subprocess.run(['ruby','-rjson','-ryaml','-e',ruby]+list(map(str,paths)),check=True,text=True,capture_output=True).stdout))
    for old in BEFORE:
        if old['level']<=130:continue
        d=read(old,phase);reports.append(audit(d));d.pop('payload');d.pop('raw');inventory.append(d)
    (HERE/f'inventory-{phase.lower()}.json').write_text(json.dumps(inventory,ensure_ascii=False,indent=2))
    baseline=json.loads((HERE/'baseline.json').read_text());differences=[]
    if phase=='After':
        for rel,sha in baseline['hashes'].items():
            current=hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()
            if current!=sha and rel not in baseline['mutable']:differences.append(rel)
        # Separate user steering while this batch was in progress: actual Rhodes event-order fix.
        authorized=json.loads((HERE/'separate-rhodes-fix.json').read_text())['files']
        assert set(differences)==set(authorized),differences
        for rel,sha in authorized.items():
            assert baseline['hashes'][rel]==sha['before'] and hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()==sha['after'],('unexpected runtime edit',rel)
        for n in range(1,131):
            rel=BY[n]['asset'];assert hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()==baseline['hashes'][rel],('protected early level',n)
    # Revised masks must not introduce additional isolated board regions.
    for row in reports:
        old=BY[row['level']];old_parts=component([i for i,v in enumerate(old['data']['cells']) if v],old['width'],old['height'])
        assert len(row['maskComponents'])<=len(old_parts),(row['level'],'new disconnected region')
    output=dict(phase=phase,levels=reports,protectedFiles=len(baseline['hashes'])-len(baseline['mutable']),separateAuthorizedRuntimeChange=differences,limits='Static authoring/graph checks; no Unity build, play, bot or measured win-rate.')
    (HERE/f'validation-{phase.lower()}.json').write_text(json.dumps(output,ensure_ascii=False,indent=2))
    print('PASS',phase,len(reports),'levels; all authored obstacle families represented; <=5 goals; boss <=10 rows and pressure type coverage')
if __name__=='__main__':main()
