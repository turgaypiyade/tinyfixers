"""Append V1 level 212 only; snapshot and preserve current assets. No build/simulation."""
from pathlib import Path
import hashlib,json,re,struct,sys,uuid
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[3]; A=ROOT/'Assets/_Project'
sys.path.insert(0,str(ROOT/'Tools'))
import review_level_patterns as review
from check_levels161_162 import DEFS
P=A/'Settings/ProLevels'; ASSET=P/'LevelV1_00002120.asset'; CATALOG=P/'LevelCatalogProV1.asset'; NAMES=A/'Resources/LevelNames.json'
def encode(a):return struct.pack('<'+str(len(a))+'i',*a).hex()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    assert not ASSET.exists() and not (HERE/'baseline.json').exists()
    before=CATALOG.read_text(); assert list(map(int,re.findall(r'^    level: (\d+)$',before,re.M)))==list(range(1,212))
    backup=HERE/'Before';backup.mkdir()
    for p in (CATALOG,NAMES,ROOT/'Docs/LevelDesign/Design-State.md',ROOT/'Docs/LevelDesign/Memory.md',ROOT/'Docs/LevelDesign/Level-Names.md'):(backup/p.name).write_bytes(p.read_bytes())
    protected={str(p.relative_to(ROOT)):sha(p) for p in A.rglob('*') if p.is_file() and p not in (CATALOG,NAMES)}
    (HERE/'baseline.json').write_text(json.dumps(protected,indent=2)+'\n')
    old=review.load(CATALOG)
    enum=(A/'Scripts/Core/LevelData.cs').read_text()
    enum=enum[enum.index('public enum ObstacleId'):enum.index('public static class LampStripColor')]
    labels={int(m[2]):m[1] for m in re.finditer(r'^    (\w+) = (\d+)',enum,re.M)}
    pool=[]
    for o in sorted(DEFS):
        ns=[d['level'] for d in old if str(o) in map(str,d['obstacleInstances'])]
        pool.append(dict(id=o,name=labels.get(o,'legacy'),firstV1Use=min(ns) if ns else None,recentV1Uses=ns[-8:]))
    (HERE/'Pool-Review.json').write_text(json.dumps(dict(note='IDs include colour, direction, stage, producer/product and legacy variants; not independent mechanic-family count. Last use is not proof of novelty.',definitions=pool,candidates=[
        dict(concept='Magnetic Dockyard',families=[29,34,33,42],decision='Considered. Moving endpoints and special cages would make this particular cargo route less readable; keep available for another concept.'),
        dict(concept='Painted Sail',families=[33,34,63,26,42],decision='Selected. Sailboat silhouette; colour-locked cargo, paint crates on the drop route, shrinking hull pipes, grass beneath crates.'),
        dict(concept='Paint Wheel',families=[63,64,65,69],decision='Considered. Four radial chambers would repeat the recent 209 four-room vocabulary.')]),ensure_ascii=False,indent=2)+'\n')
    rows=['....oo...','...ooo...','..oooo...','.ooooo...','oooooo...','oooooo...','ooooooooo','ooooooooo','.ooooooo.','..ooooo..','...ooo...']
    w,h=9,11; n=w*h
    data={k:[0]*n for k in ('cells','obstacles','wallPieceIds','lampColorByCell','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')};data['obstacleOrigins']=[-1]*n
    data['cells']=[int(c=='o') for row in rows for c in row]
    def put(x,y,o):
        i=y*w+x;assert data['cells'][i] and data['obstacles'][i]==0
        data['obstacles'][i]=o;data['obstacleOrigins'][i]=i
    for y in (2,3):
        for x in (3,4):put(x,y,34)
    for y,xs in ((3,range(1,2)),(4,range(0,2)),(5,range(0,3))):
        for x in xs:put(x,y,42)
    for y in (6,7):
        for x in range(2,6):put(x,y,42)
    stacks=[dict(originCellIndex=6*w+x,obstacleId=63,stackOrder=1,initialRemainingHits=0) for x in (2,4)]
    safes=[dict(originCellIndex=21,width=2,height=2,redHits=3,yellowHits=3,greenHits=3,lockHitMode=0,firstLock=0,secondLock=1,thirdLock=2,stackOrder=1)]
    tubes=[dict(originCellIndex=73,direction=3,length=3),dict(originCellIndex=79,direction=2,length=3)]
    # The closed hull is intentionally seeded, not a fictitiously filled preview of an empty pocket.
    seed=[76]+[9*w+x for x in range(2,7)]+[10*w+x for x in range(3,6)]
    for i,c in zip(seed,[1,2,3,1,4,2,4,2,3]):data['pinnedTileTypes'][i]=c
    goals=[dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=c) for o,c in ((33,1),(34,4),(63,2),(26,2),(42,14))]
    text=(P/'LevelV1_00002110.asset').read_text()
    def scalar(k,v):
        nonlocal text
        text,c=re.subn(r'^  '+k+r':[^\n]*$',lambda _:f'  {k}: {v}',text,flags=re.M);assert c==1,k
    def entries(k,rows):
        nonlocal text
        body=f'  {k}: []\n' if not rows else f'  {k}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+key+': '+str(v)+'\n' for j,(key,v) in enumerate(e.items())) for e in rows)
        text,c=re.subn(r'^  '+k+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda _:body,text,flags=re.M);assert c==1,k
    for k,v in dict(m_Name=ASSET.stem,width=w,height=h,moves=24,levelKind=0,usesCustomIntro=0,refillSpecialGuard=.75,oilSpreadMaxCells=0).items():scalar(k,v)
    for k,a in data.items():scalar(k,encode(a))
    for k in ('colorCrateInitialHits','magnets','waterChannels','ancientSeals','lampStripSettings','paintCanBoxColors','tarpStatues','tutorialSteps','bossWaves'):entries(k,[])
    entries('stackedObstacles',stacks);entries('safes',safes);entries('tubes',tubes);entries('goals',goals)
    ASSET.write_text(text);guid=uuid.uuid4().hex
    Path(str(ASSET)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    CATALOG.write_text(before+f'  - chapter: 9\n    level: 212\n    levelKey: LevelCL_0212\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n')
    names=json.loads(NAMES.read_text());assert not any(e['level']==212 for e in names['entries'])
    names['entries'].append(dict(level=212,levelKey='LevelCL_0212',name='Renkli Yelken',nameEn='Painted Sail'))
    NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    design=dict(level=212,title='Renkli Yelken',titleEn='Painted Sail',asset=str(ASSET.relative_to(ROOT)),guid=guid,width=w,height=h,moves=24,active=sum(data['cells']),data=data,stacks=stacks,safes=safes,tubes=tubes,goals=goals,seededHullCells=seed)
    (HERE/'design.json').write_text(json.dumps(design,ensure_ascii=False,indent=2)+'\n')
    print('212 Painted Sail:',design['active'],'active; 24 moves; five existing families; nine authored hull tiles.')
if __name__=='__main__':main()
