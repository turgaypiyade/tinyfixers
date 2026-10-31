"""Append only V1 level 201. Refuse overwrites. Preserve the current workspace, not just Git HEAD."""
from pathlib import Path
import hashlib,json,re,shutil,struct,sys,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(HERE))
from design import DESIGN,KEYS
from verify import ASSET,CATALOG,NAMES,check_design

def scalar(text,key,value):
    line=f'  {key}: {value}'
    return re.sub(r'^  '+key+r':[^\n]*',lambda _:line,text,flags=re.M) if re.search(r'^  '+key+r':',text,re.M) else text+line+'\n'

def section(text,key,lines):
    body=f'  {key}: []\n' if not lines else f'  {key}:\n'+lines
    pattern=r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*'
    return re.sub(pattern,lambda _:body,text,flags=re.M) if re.search(pattern,text,re.M) else text+body

def main():
    assert '--apply' in sys.argv,'Inspect draft/preview, then --apply.'
    check_design();assert not ASSET.exists() and not Path(str(ASSET)+'.meta').exists()
    assert not (HERE/'protected-before.json').exists(),'Already authored; use verify.py --assets.'
    catalog=CATALOG.read_text();assert catalog.endswith('\n') and not re.search(r'^    level: 201$',catalog,re.M)
    assert max(map(int,re.findall(r'^    level: (\d+)$',catalog,re.M)))==200
    names=json.loads(NAMES.read_text());assert not any(e['level']==201 or e['name']==DESIGN['title'] for e in names['entries'])
    before=HERE/'Before';before.mkdir(exist_ok=False)
    docs=ROOT/'Docs/LevelDesign'
    for p in (CATALOG,NAMES,docs/'Level-Names.md',docs/'Design-State.md',docs/'Design-Feedback.md',docs/'Obstacle-Design-Guide.md'):
        shutil.copyfile(p,before/p.name)
    protected={}
    for folder in ('Settings','Scripts','Resources','Scenes','Prefabs','Shaders','Editor','Data'):
        for p in (ROOT/'Assets/_Project'/folder).rglob('*'):
            if p.is_file() and p not in (CATALOG,NAMES) and p.suffix in ('.asset','.meta','.cs','.json','.unity','.prefab','.shader','.xml','.mm','.java'):
                protected[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
    (HERE/'protected-before.json').write_text(json.dumps(protected,indent=2)+'\n')
    text=(ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00001930.asset').read_text()
    for key,value in dict(m_Name=ASSET.stem,width=9,height=10,moves=26,baseCoinReward=10,levelKind=0,
        usesCustomIntro=0,introLeftSprite='{fileID: 0}',introRightSprite='{fileID: 0}',
        bossAttackOilCount=0,bossThrownObstacles='',bossToolThreatEnabled=0,bossToolThreatTypes='',
        bossWaveCount=0,oilSpreadMaxCells=0,refillSpecialGuard=-1,comboTutorial=0,
        energyPerContainer=1,rocketBasketFireAllOnHit=0,hamsterSatietyOverride=0).items():text=scalar(text,key,value)
    for key,values in {**DESIGN['data'],'randomPool':[0,1,2,3]}.items():
        text=scalar(text,key,struct.pack('<'+str(len(values))+'i',*values).hex())
    text=section(text,'goals','  - targetType: 2\n    tileType: 0\n    obstacleId: 0\n    collectibleId: 3\n    iconOverride: {fileID: 0}\n    amount: 50\n')
    for key in ('tutorialSteps','stackedObstacles','ancientSeals','waterChannels','magnets','safes','tubes','paintCanBoxColors','tarpStatues','colorCrateInitialHits','bossWaves'):
        text=section(text,key,'')
    ASSET.write_text(text);guid=uuid.uuid4().hex
    Path(str(ASSET)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    CATALOG.write_text(catalog+f'  - chapter: 9\n    level: 201\n    levelKey: LevelCL_0201\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n')
    names['entries'].append(dict(level=201,levelKey='LevelCL_0201',name=DESIGN['title'],nameEn=DESIGN['nameEn']))
    NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    p=docs/'Level-Names.md';s=p.read_text().replace('(1–200)','(1–201)');p.write_text(s+'| 201 | Guguk Bahçesi |\n')
    (HERE/'manifest.json').write_text(json.dumps(dict(level=201,asset=str(ASSET.relative_to(ROOT)),guid=guid,
        assetSha256=hashlib.sha256(ASSET.read_bytes()).hexdigest(),scope='Only V1 201; no runtime, existing level, release or publication changes.'),indent=2)+'\n')
    print('Authored only V1 201; catalog and TR/EN name linked. Run verify.py --assets.')

if __name__=='__main__':main()
