"""Serialized authoring, source geometry and preservation; no gameplay simulation."""
import json,re,subprocess
from collections import Counter
from pathlib import Path
from author import HERE,BATCH,ROOT,ASSET,shared,sha

def section(t,k):
    return re.search(r'^  '+k+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',t,re.M)[0]

def main():
    hashes=json.loads((HERE/'protected-before.json').read_text());changed=[p for p,h in hashes.items() if not(ROOT/p).is_file() or sha(ROOT/p)!=h]
    assert not changed,changed
    for p,h in json.loads((HERE/'owned-hashes.json').read_text()).items():assert sha(ROOT/p)==h,('User edit',p)
    d=json.loads((HERE/'design.json').read_text());t=(ROOT/ASSET).read_text();w,h=d['width'],d['height'];n=w*h
    assert d['level']==229 and d['difficulty']=='Super Hard' and d['moves']==28 and (w,h)==(9,11)
    assert re.search(r'^  moves: 28$',t,re.M) and re.search(r'^  levelKind: 0$',t,re.M)
    for k,a in d['data'].items():assert len(a)==99 and shared.array(t,k,n)==a,k
    assert all(d['data']['cells']) and not any(d['data']['pinnedTileTypes']) and not any(d['data']['pinnedSpecialTypes'])
    forbidden={4,5,6,21,42,58,69,71,72,73,74}
    assert not forbidden & set(d['data']['obstacles'])
    assert not any(e['obstacleId'] in forbidden for e in d['custom']['stackedObstacles'])
    for k in shared.CUSTOM:assert section(t,k)==shared.entries('  '+k+': []\n',k,d['custom'][k]),k
    assert section(t,'goals')==shared.entries('  goals: []\n','goals',d['goals'])
    assert not d['custom']['dimerdals'] and not d['custom']['ancientSeals'] and not d['custom']['safes']
    origins={}
    for i,oid in enumerate(d['data']['obstacles']):
        if not oid:assert d['data']['obstacleOrigins'][i]==-1;continue
        origin=d['data']['obstacleOrigins'][i];origins.setdefault((oid,origin),set()).add(i)
    for (oid,origin),region in origins.items():
        fw,fh=shared.footprint(oid);assert origin==min(region)
        assert region=={origin+dy*w+dx for dy in range(fh) for dx in range(fw)}
    counts=Counter(oid for oid,_ in origins)
    assert counts=={46:12,30:12,59:2,70:2}
    covers=d['custom']['stackedObstacles'];assert len(covers)==12 and {e['obstacleId'] for e in covers}=={28}
    assert {e['originCellIndex'] for e in covers}==set(d['spine'])
    assert all(d['data']['obstacles'][i]==46 for i in d['spine'])
    goal={g['obstacleId']:g['amount'] for g in d['goals']};assert goal=={46:99,28:12,70:2,30:12}
    assert len(d['goals'])<=5 and all('guid:' in g['iconOverride'] for g in d['goals'])
    ls=shared.layers(d);assert all(ls[i]==[(46,i),(28,i)] for i in d['spine'])
    phases=[]
    for removed in [[],[28],[28,59],[28,59,70,30]]:
        slots,reached,seeded=shared.feed(d,removed)
        assert slots==reached and not seeded,(removed,sorted(slots-reached))
        phases.append(dict(removed=removed,normalSlots=len(slots),naturallyFed=len(reached),unfilled=[]))
    assert phases[0]['normalSlots']==63 and phases[-1]['normalSlots']==99
    # Both presses face upwards, touch the bottom and have a real stone/seed in their column.
    assert {(o%9,o//9) for oid,o in origins if oid==59}=={(3,9),(4,9)}
    assert all(any(i%9==x for i in d['spine']) for x in d['launcherColumns'])
    assert {(o%9,o//9) for oid,o in origins if oid==70}=={(5,9),(7,9)}
    assert {(oid,origin) for oid,origin in origins if oid in (59,70)}=={(59,84),(59,85),(70,86),(70,88)}
    assert '  movableSpawnColumns: '+'00'*9+'\n' in t
    cat=(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').read_text()
    assert f'    level: 229\n    levelKey: LevelCL_0229\n    levelData: {{fileID: 11400000, guid: {d["guid"]}' in cat
    names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())
    name=next(e for e in names['entries'] if e['level']==229)
    assert (name['name'],name['nameEn'])==(d['name'],d['nameEn'])
    subprocess.run(['ruby','-ryaml','-e',"t=File.readlines(ARGV[0]).reject{|l|l.start_with?('%','---')}.join;YAML.safe_load(t,aliases:false)",str(ROOT/ASSET)],check=True)
    (HERE/'validation.json').write_text(json.dumps(dict(result='PASS',protectedProjectFiles=len(hashes),changedLevelAssets=[229],unchangedLevels='1–228 and230 including user edits to225/226',goals=goal,phases=phases,limits='Static authored geometry/layers only. No Unity build/Play/device, bot/gravity/cascade simulation, measured balance/performance or publishing.'),ensure_ascii=False,indent=2)+'\n')
    print('PASS:229 YAML/arrays/footprints/layers/goals/all-phase feed;',len(hashes),'other project files and all other levels preserved.')

if __name__=='__main__':main()
