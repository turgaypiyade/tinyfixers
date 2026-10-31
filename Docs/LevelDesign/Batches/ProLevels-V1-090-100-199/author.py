"""Fill only three empty V1 slots; refuse overwriting any existing design."""
from pathlib import Path
import hashlib,json,re,struct,sys,uuid,shutil
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(HERE))
from designs import DESIGNS
from audit import check
LEVELS=ROOT/'Assets/_Project/Settings/ProLevels';CATALOG=LEVELS/'LevelCatalogProV1.asset'
NAMES=ROOT/'Assets/_Project/Resources/LevelNames.json';DOCS=ROOT/'Docs/LevelDesign'

def main():
    assert '--apply' in sys.argv,'Use --apply after draft layout inspection.'
    assert not (HERE/'protected-before.json').exists(),'Already authored; use audit.py --assets.'
    for d in DESIGNS:check(d)
    catalog=CATALOG.read_text();names=json.loads(NAMES.read_text())
    for d in DESIGNS:
        n=d['n'];assert not (LEVELS/f'LevelV1_{n*10:08d}.asset').exists()
        assert re.search(r'    level: '+str(n)+r'\n    levelKey: [^\n]+\n    levelData: \{fileID: 0\}',catalog)
        assert not any(e['level']==n or e['name']==d['title'] for e in names['entries'])
    before=HERE/'Before';before.mkdir()
    for p in (CATALOG,NAMES,DOCS/'Level-Names.md',DOCS/'Design-State.md',DOCS/'Design-Feedback.md',ROOT/'Tools/review_level_patterns.py'):
        shutil.copyfile(p,before/p.name)
    protected={}
    for folder in ('Settings','Scripts','Resources','Scenes','Prefabs','Shaders','Editor'):
        for p in (ROOT/'Assets/_Project'/folder).rglob('*'):
            if p.is_file() and p not in (CATALOG,NAMES) and p.suffix in ('.asset','.meta','.cs','.json','.unity','.prefab','.shader','.xml','.mm','.java'):
                protected[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
    (HERE/'protected-before.json').write_text(json.dumps(protected,indent=2)+'\n')
    def scalar(text,key,value):
        line=f'  {key}: {value}'
        return re.sub(r'^  '+key+r':[^\n]*',lambda _:line,text,flags=re.M) if re.search(r'^  '+key+r':',text,re.M) else text+line+'\n'
    def entries(text,key,values):
        def serialize(value):
            return struct.pack('<'+str(len(value))+'i',*value).hex() if isinstance(value,list) else str(value)
        lines=f'  {key}: []\n' if not values else f'  {key}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+k+': '+serialize(v)+'\n' for j,(k,v) in enumerate(row.items())) for row in values)
        pattern=r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*'
        return re.sub(pattern,lambda _:lines,text,flags=re.M) if re.search(pattern,text,re.M) else text+lines
    manifest=[]
    for d in DESIGNS:
        n=d['n'];asset=f'LevelV1_{n*10:08d}';guid=uuid.uuid4().hex
        text=(LEVELS/('LevelV1_00000800.asset' if n!=199 else 'LevelV1_00001930.asset')).read_text()
        values=dict(m_Name=asset,width=9,height=d['h'],moves=d['moves'],baseCoinReward=10,levelKind=int(n!=199),
            usesCustomIntro=0,introLeftSprite='{fileID: 0}',introRightSprite='{fileID: 0}',
            bossAttackEveryMoves=4,bossAttackOilCount=0 if n==199 else 1,
            bossThrownObstacles=struct.pack('<'+str(len(d.get('throws',[])))+'i',*d.get('throws',[])).hex(),
            bossMaxPressureObstacles=d.get('pressureCap',6),bossToolThreatEnabled=0,bossToolThreatTypes='',
            playerMaxHp=d.get('playerHp',100),damagePerClearedTile=1,shieldProtectionPerPickup=2,
            enemyAttackBaseDamage=7,bossWaveCount=len(d['waves']),oilSpreadMaxCells=d['oilCap'],
            randomPool=struct.pack('<4i',0,1,2,3).hex(),refillSpecialGuard=-1,
            comboTutorial=0,rocketBasketFireAllOnHit=0,hamsterSatietyOverride=0)
        for k,v in values.items():text=scalar(text,k,v)
        for k,v in d['data'].items():text=scalar(text,k,struct.pack('<'+str(len(v))+'i',*v).hex())
        for k,v in [('goals',d['goals']),('stackedObstacles',d['stacks']),('magnets',d['magnets']),('ancientSeals',d['ancientSeals']),('bossWaves',d['waves'])]:text=entries(text,k,v)
        for k in ('tutorialSteps','tubes','waterChannels','safes','tarpStatues','paintCanBoxColors','colorCrateInitialHits'):text=entries(text,k,[])
        path=LEVELS/(asset+'.asset');path.write_text(text)
        Path(str(path)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
        level_key=re.search(r'    level: '+str(n)+r'\n    levelKey: ([^\n]+)',catalog)[1]
        pattern=r'(    level: '+str(n)+r'\n    levelKey: '+re.escape(level_key)+r'\n    levelData: )\{fileID: 0\}'
        catalog,count=re.subn(pattern,lambda m:m[1]+f'{{fileID: 11400000, guid: {guid}, type: 2}}',catalog);assert count==1
        names['entries'].append(dict(level=n,levelKey=level_key,name=d['title'],nameEn=d['nameEn']))
        manifest.append(dict(level=n,asset=str(path.relative_to(ROOT)),guid=guid,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    CATALOG.write_text(catalog);names['entries'].sort(key=lambda e:e['level']);NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    md=DOCS/'Level-Names.md';text=md.read_text().replace('(4, 8, 71, 90, 100, 106, 199)','(4, 8, 106)')
    for d in DESIGNS:
        n=d['n'];pattern=r'(^\| '+str(n+1)+r' \|)'
        if re.search(pattern,text,re.M):text=re.sub(pattern,lambda m:f'| {n} | {d["title"]} |\n'+m[1],text,flags=re.M)
        else:text+=f'| {n} | {d["title"]} |\n'
    md.write_text(text)
    (HERE/'manifest.json').write_text(json.dumps(dict(levels=manifest,scope='Only empty V1 90, 100, 199; no release, publication or build.'),indent=2)+'\n')
    assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in protected.items())
    print('Created only V1 90, 100, 199; preserved',len(protected),'existing files. Catalog and TR/EN names linked.')

if __name__=='__main__':main()
