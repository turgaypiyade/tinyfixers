"""Verify the actual Unity assets, catalog, goals, layers and static feed graph."""
from author import *
sys.path.insert(0,str(ROOT/'Docs/LevelDesign/Reviews/225-234-Balance-2026-11-07'))
from audit import yaml_data
from review_level_patterns import load, compare


def packed(value,count):
    import struct
    assert isinstance(value,str) and len(value)==count*8
    return list(struct.unpack(f'<{count}i',bytes.fromhex(value)))


def verify():
    ds=json.loads((HERE/'designs.json').read_text());names=json.loads(NAMES.read_text())['entries']
    assert list(map(int,re.findall(r'^    level: (\d+)$',CAT.read_text(),re.M)))==list(range(1,251))
    original_cat=(HERE/'Before'/CAT.name).read_text();assert CAT.read_text().startswith(original_cat)
    oldnames=json.loads((HERE/'Before'/NAMES.name).read_text())['entries'];assert names[:len(oldnames)]==oldnames
    baseline=json.loads((HERE/'protected-before.json').read_text())
    assert all(sha(ROOT/p)==h for p,h in baseline.items() if p not in [str(CAT.relative_to(ROOT)),str(NAMES.relative_to(ROOT))])
    boolean_keys=set()
    def discover(v):
        if isinstance(v,dict):
            for k,val in v.items():
                if isinstance(val,bool):boolean_keys.add(k)
                discover(val)
        elif isinstance(v,list):
            for x in v:discover(x)
    for p in (ROOT/'Docs/LevelDesign/Reviews/225-234-Balance-2026-11-07').glob('L0*.json'):discover(json.loads(p.read_text()))
    # The running balance batch reads immutable initial inputs. Later design/move
    # revisions export elsewhere so no sample silently switches versions mid-run.
    overrides=HERE/('SimLevels-Current' if (HERE/'Sim-Balance').exists() else 'SimLevels');overrides.mkdir(exist_ok=True)
    checks=[]
    for d in ds:
        n=d['level'];raw=yaml_data(ROOT/d['asset']);w,h=d['width'],d['height'];size=w*h
        assert (raw['width'],raw['height'],raw['moves'],raw['levelKind'])==(w,h,d['moves'],d['kind'])
        assert raw['m_Name']==Path(d['asset']).stem
        for k,a in d['data'].items():assert packed(raw[k],size)==a,(n,k)
        assert len(raw['goals'])==len(d['goals'])<=5
        for a,b in zip(raw['goals'],d['goals']):
            for k in ['targetType','tileType','obstacleId','collectibleId','amount']:assert a[k]==b[k],(n,k)
            assert a['iconOverride']['fileID']!=0 and a['iconOverride'].get('guid'),(n,'goal icon')
        for key,rows in d['custom'].items():
            actual=raw[key]or[]
            assert len(actual)==len(rows),(n,key)
            for a,b in zip(actual,rows):
                for k,v in b.items():assert (packed(a[k],len(v)) if k=='pathCellIndices' else a[k])==v,(n,key,k)
        assert not any(d['data']['pinnedSpecialTypes'])
        assert not {42,58,69,71,72,73,74}&{o for p in layers(d) for o,_ in p}
        origins=Counter()
        for i,o in enumerate(d['data']['obstacles']):
            if not o:
                assert d['data']['obstacleOrigins'][i]==-1;continue
            origin=d['data']['obstacleOrigins'][i];assert 0<=origin<size and d['data']['cells'][i]
            assert d['data']['obstacles'][origin]==o and d['data']['obstacleOrigins'][origin]==origin
            if i!=origin:continue
            origins[o]+=1
            if o in (76,79):continue
            entry=next((e for e in d['custom']['fruits'] if e['originCellIndex']==i),None) if o==102 else None
            fw,fh=(entry['width'],entry['height']) if entry else shared.footprint(o)
            assert i%w+fw<=w and i//w+fh<=h,(n,o,'footprint')
            wanted={(i//w+y)*w+i%w+x for y in range(fh) for x in range(fw)}
            got={j for j,origin2 in enumerate(d['data']['obstacleOrigins']) if origin2==i}
            assert got==wanted,(n,o,i,wanted,got)
        for e in d['custom']['stackedObstacles']:origins[e['obstacleId']]+=1
        for key,o in [('safes',33),('ancientSeals',64),('tubes',26),('magnets',29),('waterChannels',76)]:
            if d['custom'][key]:origins[o]=len(d['custom'][key])
        if origins[79]:origins[79]=len({pid for pid in d['data']['wallPieceIds'] if pid})
        for e in d['custom']['waterChannels']+d['custom']['magnets']:
            path=e['pathCellIndices'];assert len(path)==len(set(path))>=2
            assert all(0<=i<size and d['data']['cells'][i] for i in path)
            assert all(abs(a%w-b%w)+abs(a//w-b//w)==1 for a,b in zip(path,path[1:]))
            if e in d['custom']['waterChannels']:
                assert all(b//w>=a//w for a,b in zip(path,path[1:]));last=path[-1]
                assert last%w in (0,w-1) or last//w==h-1
        for plane in layers(d):assert sum(shared.stage(o,'behavior')==3 for o,_ in plane)<=1,(n,'multiple movable layers')
        goals={g['obstacleId']:g['amount'] for g in d['goals'] if g['targetType']==1}
        for o,amount in goals.items():
            if o==53:assert origins[52]*8==amount
            elif o==23:assert origins[o]*12==amount
            else:assert origins[o]==amount,(n,o,origins[o],amount)
        represented=set(goals)
        for g in d['goals']:
            if g['targetType']==2:
                represented|={1:{22},2:set(),3:{78},4:set(range(84,100))}[g['collectibleId']]
            if g['targetType']==0 and g['tileType']==9:represented.add(44)
        if 102 in goals:represented.add(100)
        assert not set(o for o,c in origins.items() if c)-represented,(n,'unrepresented obstacle')
        if d['kind']:
            assert h<=10 and not set(goals)&set(d['bossThrownObstacles']+d['bossToolThreatTypes'])
        if d['custom']['dimerdals']:
            amount=sum(e['count'] for e in d['custom']['dimerdals'])
            assert any(g['targetType']==2 and g['collectibleId']==4 and g['amount']==amount for g in d['goals'])
        if d['custom']['fruits']:assert len(d['custom']['fruits'])==goals[102]<=origins[100]*2
        slots,reach=feed(d);pins={i for i,v in enumerate(d['data']['pinnedTileTypes'])if v}
        assert pins==slots-reach
        for i in pins:
            # All active cells immediately below are occupied at full opening;
            # holes directly below would require a separate settled-pin check.
            assert i//w==h-1 or d['data']['cells'][i+w],(n,'unsupported pin',i)
        permanent={22,44,78}
        removed={o for plane in layers(d) for o,_ in plane if o not in permanent}
        after_slots,after_reach=feed(d,removed)
        assert after_slots==after_reach,(n,'unreachable refill after opening',after_slots-after_reach)
        guid=re.search(r'^guid: (\w+)$',Path(str(ROOT/d['asset'])+'.meta').read_text(),re.M)[1]
        assert guid==d['guid'] and f'guid: {guid}' in CAT.read_text()
        assert any(e['level']==n and e['name']==d['name'] and e['nameEn']==d['nameEn'] and e['levelKey']==d['levelKey'] for e in names)
        def export(value,key=''):
            if isinstance(value,dict):return {k:export(v,k) for k,v in value.items() if not k.startswith('m_') and not(isinstance(v,dict)and'fileID'in v)}
            if isinstance(value,list):return [export(v) for v in value]
            if key in boolean_keys and isinstance(value,int):return bool(value)
            if isinstance(value,str)and re.fullmatch('[0-9a-f]+',value)and len(value)%8==0:return packed(value,len(value)//8)
            if value is None:return []
            return value
        exported=export(raw);exported['movableSpawnColumns']=[False]*w
        (overrides/f'L{n:04d}.json').write_text(json.dumps(exported,ensure_ascii=False)+'\n')
        checks.append(dict(level=n,result='PASS',moves=d['moves'],goals=len(d['goals']),obstacleGoalSlots=len(goals),opening=d['openingGeometry'],allLaterSlotsFed=True,permanentSourcesAtBottom=bool(set(origins)&permanent)))
    all_levels=load(CAT);shortlist=[]
    for d in all_levels:
        if d['level']<235:continue
        previous=[compare(d,p) for p in all_levels if p['level']<d['level']]
        shortlist.append(dict(level=d['level'],closest=sorted(previous,key=lambda r:r['blockerOverlap'],reverse=True)[:4],cropped=sorted(previous,key=lambda r:r['croppedShapeOverlap'],reverse=True)[:4]))
    (HERE/'similarity.json').write_text(json.dumps(shortlist,ensure_ascii=False,indent=2)+'\n')
    (HERE/'validation.json').write_text(json.dumps(dict(levels=checks,previousLevelFilesPreserved=len(baseline)-2,simulation=False,limits='Static authored layout and flow; runtime pass rate is separate.'),ensure_ascii=False,indent=2)+'\n')
    print('PASS',len(ds),'levels; preserved',len(baseline)-2,'prior level/meta files; sim JSON exported.')


if __name__=='__main__':verify()
