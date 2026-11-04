"""Append only V1 level 211. Existing content is snapshotted and hash-protected; no build/simulation."""
from pathlib import Path
import hashlib, json, re, struct, uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
PROJECT = ROOT / 'Assets/_Project'
CATALOG = PROJECT / 'Settings/ProLevels/LevelCatalogProV1.asset'
NAMES = PROJECT / 'Resources/LevelNames.json'
ASSET = PROJECT / 'Settings/ProLevels/LevelV1_00002110.asset'

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def encode(a): return struct.pack('<' + str(len(a)) + 'i', *a).hex()

def main():
    assert not ASSET.exists() and not (HERE/'baseline.json').exists(), 'Never overwrite an existing revision'
    before = CATALOG.read_text()
    assert list(map(int, re.findall(r'^    level: (\d+)$', before, re.M))) == list(range(1, 211))
    protected = {str(p.relative_to(ROOT)): sha(p) for p in PROJECT.rglob('*') if p.is_file() and p not in (CATALOG, NAMES)}
    (HERE/'baseline.json').write_text(json.dumps(protected, indent=2)+'\n')
    backup = HERE/'Before'; backup.mkdir()
    for p in (CATALOG, NAMES, ROOT/'Docs/LevelDesign/Level-Names.md', ROOT/'Docs/LevelDesign/Memory.md', ROOT/'Docs/LevelDesign/Design-State.md'):
        (backup/p.name).write_bytes(p.read_bytes())
    width, height = 9, 11
    spans = [(2,6), (1,7)] + [(0,8)]*4 + [(1,7)]*2 + [(2,6)]*2 + [(3,5)]
    cells = [int(a <= x <= b) for a,b in spans for x in range(width)]
    size = len(cells)
    data = {k:[0]*size for k in ('obstacles','wallPieceIds','lampColorByCell','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')}
    data['cells'] = cells; data['obstacleOrigins'] = [-1]*size
    stacks, walls = [], []
    def single(x,y,o):
        i=y*width+x; assert cells[i] and data['obstacles'][i] == 0
        data['obstacles'][i]=o; data['obstacleOrigins'][i]=i
    def wall(x,y,w,h,o,piece):
        region=[(y+dy)*width+x+dx for dy in range(h) for dx in range(w)]
        origin=min(region)
        for i in region:
            assert cells[i] and data['obstacles'][i] == 0
            data['obstacles'][i]=o; data['obstacleOrigins'][i]=origin; data['wallPieceIds'][i]=piece
        walls.append(dict(obstacleId=o,origin=origin,cells=region))
    # Paired 2x2 cabinets cap the crest's shoulders; the mud beneath is a second task.
    for x in (0,7):
        for y in (2,3):
            for xx in (x,x+1): single(xx,y,25)
        stacks.append(dict(originCellIndex=2*width+x,obstacleId=27,stackOrder=1,initialRemainingHits=0))
    # Broad brick shoulders, then two separate metal clasps; neither is one giant easy piece.
    wall(0,4,3,2,55,1); wall(6,4,3,2,55,2)
    wall(1,6,2,2,56,3); wall(6,6,2,2,56,4)
    # One coherent stone keel, not detached singletons: 3-wide neck -> 5-wide body -> 3-wide tip.
    for y,xs in [(6,range(3,6)),(7,range(3,6)),(8,range(2,7)),(9,range(2,7)),(10,range(3,6))]:
        for x in xs: single(x,y,28)
    goals=[dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=n)
           for o,n in ((27,2),(55,2),(56,2),(28,19),(25,8))]
    text=(PROJECT/'Settings/ProLevels/LevelV1_00002060.asset').read_text()
    def scalar(k,v):
        nonlocal text
        text,n=re.subn(r'^  '+k+r':[^\n]*$',lambda _:f'  {k}: {v}',text,flags=re.M); assert n==1,k
    def entries(k,rows):
        nonlocal text
        body=f'  {k}: []\n' if not rows else f'  {k}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+key+': '+str(v)+'\n' for j,(key,v) in enumerate(row.items())) for row in rows)
        pat=r'^  '+k+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*'
        text,n=re.subn(pat,lambda _:body,text,flags=re.M); assert n==1,k
    for k,v in dict(m_Name=ASSET.stem,width=width,height=height,moves=28,levelKind=0,usesCustomIntro=0,refillSpecialGuard=0.75,rocketBasketFireAllOnHit=0,comboTutorial=0,hamsterSatietyOverride=0,movableSpawnFallbackMoves=0,movableSpawnFillColumns=0).items(): scalar(k,v)
    for k,v in data.items(): scalar(k,encode(v))
    scalar('randomPool',encode([0,1,2,3])); scalar('movableSpawnColumns','00'*width)
    for k in ('colorCrateInitialHits','tubes','magnets','waterChannels','safes','ancientSeals','lampStripSettings','paintCanBoxColors','tarpStatues','tutorialSteps','bossWaves'): entries(k,[])
    entries('stackedObstacles',stacks); entries('goals',goals)
    ASSET.write_text(text)
    guid=uuid.uuid4().hex
    Path(str(ASSET)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    CATALOG.write_text(before+f'  - chapter: 9\n    level: 211\n    levelKey: LevelCL_0211\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n')
    names=json.loads(NAMES.read_text()); assert not any(e['level']==211 for e in names['entries'])
    names['entries'].append(dict(level=211,levelKey='LevelCL_0211',name='Çelik Arma',nameEn='Steel Crest'))
    NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    design=dict(level=211,title='Çelik Arma',titleEn='Steel Crest',asset=str(ASSET.relative_to(ROOT)),guid=guid,width=width,height=height,moves=28,refillSpecialGuard=.75,data=data,walls=walls,stacks=stacks,goals=goals,active=sum(cells))
    (HERE/'design.json').write_text(json.dumps(design,ensure_ascii=False,indent=2)+'\n')
    print('Added 211 Steel Crest:',sum(cells),'active cells; 28 moves; 5 existing families; no new mechanics.')

if __name__=='__main__': main()
