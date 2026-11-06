"""Independent YAML/origin/goal/layer/source/preservation checks, no gameplay simulation."""
from collections import Counter,deque
from pathlib import Path
import json,re,subprocess
from design import HERE,ROOT,CAT,NAMES,DEFS,CUSTOM,sha,array,footprint,stage,layers,feed,entries

def connected(cells,w):
    left=set(cells)
    if not left:return False
    q=[left.pop()]
    for i in q:
        for j in (i-w,i+w,i-1 if i%w else -1,i+1 if i%w<w-1 else -1):
            if j in left:left.remove(j);q.append(j)
    return not left
def entry_text(text,key):
    m=re.search(r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',text,re.M)
    assert m,key
    return m[0]
def main():
    baseline=json.loads((HERE/'protected-before.json').read_text())
    active=json.loads((HERE/'active-revision.json').read_text()) if (HERE/'active-revision.json').exists() else {}
    allowed=set(active.get('runtimeAllowed',[]))
    if active:
        revision=HERE/active['path']
        for rel,h in json.loads((revision/'protected-before.json').read_text()).items():assert sha(ROOT/rel)==h,('Revision preservation',rel)
        for rel,h in json.loads((revision/'owned-hashes.json').read_text()).items():assert sha(ROOT/rel)==h,('Revision drift',rel)
    changed=[rel for rel,h in baseline.items() if rel not in allowed and (not(ROOT/rel).exists() or sha(ROOT/rel)!=h)]
    assert not changed,('Outside requested226–230 scope',changed)
    owned=json.loads((HERE/'owned-hashes.json').read_text())
    for rel,h in owned.items():assert sha(ROOT/rel)==h,('Current asset/model drift',rel)
    ds=json.loads((HERE/'designs.json').read_text());assert [d['level'] for d in ds]==list(range(226,231))
    oldcat=(HERE/'Before'/CAT.name).read_text();cat=CAT.read_text();assert cat.startswith(oldcat)
    assert [int(n) for n in re.findall(r'^    level: (\d+)$',cat,re.M)]==list(range(1,231))
    assert len(re.findall(r'^  - chapter:',cat[len(oldcat):],re.M))==5
    oldnames=json.loads((HERE/'Before'/NAMES.name).read_text());names=json.loads(NAMES.read_text())
    assert names['entries'][:len(oldnames['entries'])]==oldnames['entries']
    assert len(names['entries'])==len(oldnames['entries'])+5
    oldtitles={e['name'] for e in oldnames['entries']};rows=[]
    for d in ds:
        p=ROOT/d['asset'];text=p.read_text();w,h=d['width'],d['height'];n=w*h;a=d['data'];active={i for i,v in enumerate(a['cells']) if v}
        assert len(active)>0 and 1<=len(d['goals'])<=5 and d['name'] not in oldtitles
        assert all(len(v)==n for v in a.values())
        for key,values in a.items():assert array(text,key,n)==values,(d['level'],key)
        for key,value in [('width',w),('height',h),('moves',d['moves']),('levelKind',d['kind'])]:assert f'  {key}: {value}\n' in text
        assert re.search(r'^guid: '+d['guid']+'$',Path(str(p)+'.meta').read_text(),re.M)
        assert d['chapter']==10
        assert f'  - chapter: {d["chapter"]}\n    level: {d["level"]}\n    levelKey: {d["levelKey"]}\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}' in cat
        name=next(e for e in names['entries'] if e['level']==d['level']);assert (name['levelKey'],name['name'],name['nameEn'])==(d['levelKey'],d['name'],d['nameEn'])
        assert '  tutorialSteps: []\n' in text and '  comboTutorial: 0\n' in text
        assert not any(a['pinnedSpecialTypes']) and all(0<=v<=4 for v in a['pinnedTileTypes'])
        for key in CUSTOM:
            body=entry_text(text,key)
            assert len(re.findall(r'^  - ',body,re.M))==len(d['custom'][key]),(d['level'],key)
            assert body==entries(f'  {key}: []\n',key,d['custom'][key]),(d['level'],'serialized custom entries',key)
        assert entry_text(text,'goals')==entries('  goals: []\n','goals',d['goals'])
        # Variable-shape wall/lamp pieces are one origin, not one goal per cell.
        pieces={}
        for i,o in enumerate(a['obstacles']):
            if not o:assert a['obstacleOrigins'][i]==-1;continue
            assert i in active and 0<=a['obstacleOrigins'][i]<n
            pieces.setdefault((o,a['obstacleOrigins'][i]),set()).add(i)
        for (o,origin),cells in pieces.items():
            assert origin==min(cells) and connected(cells,w),(d['level'],o,origin)
            if o in (55,79):
                assert len({a['wallPieceIds'][i] for i in cells})==1 and a['wallPieceIds'][origin]>0
            else:
                fw,fh=footprint(o);assert cells=={origin+dy*w+dx for dy in range(fh) for dx in range(fw)}
            if o==79:
                degrees=[sum(j in cells for j in (i-w,i+w,i-1 if i%w else -1,i+1 if i%w<w-1 else -1)) for i in cells]
                assert max(degrees)<=2 and degrees.count(1)==2
                assert all(1<=a['lampColorByCell'][i]<=4 for i in cells)
        for i,o in enumerate(a['obstacles']):
            if o!=79:assert a['lampColorByCell'][i]==0
        counts=Counter(o for o,_ in pieces)
        stackedPieces=set()
        for e in d['custom']['stackedObstacles']:
            o,origin=e['obstacleId'],e['originCellIndex'];fw,fh=footprint(o)
            region={origin+dy*w+dx for dy in range(fh) for dx in range(fw)}
            assert region<=active
            runtimeOrigin=e.get('wallPieceOriginPlusOne',0)-1 if o in (55,56) and e.get('wallPieceOriginPlusOne',0)>0 else origin
            if (o,runtimeOrigin) not in stackedPieces:counts[o]+=1;stackedPieces.add((o,runtimeOrigin))
            assert o in (42,55)
            if o==55:assert a['obstacles'][origin]==75
            if o==42:assert a['obstacles'][origin]==71
        ls=layers(d);slots,reached,seeded=feed(d)
        pins={i for i,v in enumerate(a['pinnedTileTypes']) if v}
        assert pins<=slots-reached-seeded,'Pins must be real, justified shadow starters.'
        for i in pins:
            for dx in (1,w):
                if dx==1 and i%w>w-3:continue
                assert not all(j in pins and a['pinnedTileTypes'][j]==a['pinnedTileTypes'][i] for j in (i,i+dx,i+2*dx)),'No authored three-match.'
        expected=d['openingGeometry']
        assert expected==dict(tileSlots=len(slots),naturallyFed=len(reached),shadowSeeded=len(seeded),authoredPins=len(pins),intentionallyEmpty=sorted(slots-reached-seeded-pins))
        alltypes={o for plane in ls for o,_ in plane}
        finalslots,finalreach,finalseed=feed(d,alltypes)
        assert finalslots==active and finalslots<=finalreach|finalseed,'Every final cell has a natural feed route.'
        stages=[]
        for removed in [[],d['previewRemoved'],sorted(alltypes)]:
            ss,rr,seed=feed(d,removed)
            stages.append(dict(removed=removed,slots=len(ss),naturallyFed=len(rr),shadowSeeded=len(seed),withoutFeed=sorted(ss-rr-seed-pins)))
        sourceCounts=d['custom']['dimerdals'];assert len({(s['originCellIndex'],s['obstacleId']) for s in sourceCounts})==len(sourceCounts)
        for e in sourceCounts:assert (e['obstacleId'],e['originCellIndex']) in pieces and 84<=e['obstacleId']<=95 and e['count']>0
        for g in d['goals']:
            assert g['amount']>0 and 'guid:' in g['iconOverride']
            if g['targetType']==2 and g['collectibleId']==4:assert g['amount']==sum(e['count'] for e in sourceCounts)
            elif g['targetType']==2:assert d['kind']==1 and g['collectibleId']==2 and g['amount']==d['boss']['hp']
            elif g['obstacleId']==83:
                assert counts[80]>0 and g['amount']==counts[80]*d['flourPerSack']
                assert len(slots)>=g['amount']
                sackPairs=sum(i+1 in slots and i%w<w-1 for i in slots)+sum(i+w in slots for i in slots)
                assert sackPairs>0,'At least one real normal 1x2 sack target exists.'
            else:
                refill=next((r for r in d['refill'] if r['obstacleId']==g['obstacleId']),None)
                assert g['amount']==counts[g['obstacleId']]+(refill['extra'] if refill else 0),(d['level'],g,counts)
        if d['refill']:
            assert d['level']==226 and d['movableColumns']==[7,8] and counts[75]==4 and counts[55]==5
            assert len(d['custom']['stackedObstacles'])==4
            assert all(any(o==55 for o,_ in ls[x]) for x in d['movableColumns']),'Spawn heads stay covered until a cap opens.'
            assert all(any(a['obstacles'][y*w+x]==75 for y in range(h)) for x in d['movableColumns'])
            after=layers(d,{55});assert all(any(o==75 for o,_ in after[e['originCellIndex']]) for e in d['custom']['stackedObstacles'])
        if d.get('revision')=='v2-wall-route':
            route=set(d['wallRoute']);island=set(d['millIsland'])
            assert len(route)==24 and connected(route,w) and counts[55]==1 and counts[75]==24 and counts[80]==2
            assert len(d['custom']['stackedObstacles'])==24 and {e['originCellIndex'] for e in d['custom']['stackedObstacles']}==route
            assert {e['wallPieceOriginPlusOne'] for e in d['custom']['stackedObstacles']}=={min(route)+1}
            assert all(ls[i][-1]==(55,min(route)) and ls[i][0]==(75,i) for i in route)
            assert expected['authoredPins']==0 and not expected['intentionallyEmpty'] and expected['naturallyFed']==expected['tileSlots']==36
            assert len(island)==8 and all(a['obstacles'][i]==80 for i in island)
            outside=active-island
            assert all(j not in outside for i in island for j in (i-w,i+w,i-1 if i%w else -1,i+1 if i%w<w-1 else -1)), 'Mill island must have a real hole moat.'
            assert all(any(i//w==j//w for j in slots) for i in island), 'Every mill cell has an existing horizontal special line from the work area.'
            assert all((i+w not in active or i+w in route) for i in route if i%w in (4,5)), 'No normal pocket below the wall arm.'
        assert f'  movableSpawnColumns: {"".join("01" if x in d["movableColumns"] else "00" for x in range(w))}\n' in text
        assert '  movableSpawnFallbackMoves: 0\n' in text
        if d['kind']:
            assert d['level']==230 and h<=10 and '  bossWaveCount: 3\n' in text
            targets={g['obstacleId'] for g in d['goals'] if g['targetType']==1}
            assert not targets&set(d['boss']['thrown']+d['boss']['toolThreats'])
            assert len(d['goals'])>1
        rows.append(dict(level=d['level'],active=len(active),moves=d['moves'],difficulty=d['difficulty'],goals=d['goals'],originCounts=dict(counts),opening=expected,phases=stages))
    ruby="ARGV.each{|p| t=File.readlines(p).reject{|l|l.start_with?('%','---')}.join;YAML.safe_load(t,aliases:false)}"
    subprocess.run(['ruby','-ryaml','-e',ruby,str(CAT),*[str(ROOT/d['asset']) for d in ds]],check=True)
    result=dict(result='PASS',protectedFiles=len(baseline)-len(allowed),intentionalRuntimeFiles=sorted(allowed),changedProtectedFiles=[],unchangedLevels='1–225 and227–230 during226v2',levels=rows,
        limits='Static YAML/geometry/layer/feed-source checks only; no Unity/project build, Play/device, bot, gravity/cascade simulation, publishing or measured balance/performance.')
    (HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print('PASS: five levels, goals/origins/layers/columns/boss/YAML;',len(baseline)-len(allowed),'original protected files; active revision preservation passed.')
if __name__=='__main__':main()
