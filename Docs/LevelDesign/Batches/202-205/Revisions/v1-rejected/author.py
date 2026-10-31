"""Append four V1 levels only. Existing 201 is already revised by the explicit goal-only request."""
from pathlib import Path
import hashlib,json,re,shutil,struct,sys,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(HERE))
from designs import DESIGNS
from audit import check
LEVELS=ROOT/'Assets/_Project/Settings/ProLevels';CATALOG=LEVELS/'LevelCatalogProV1.asset';NAMES=ROOT/'Assets/_Project/Resources/LevelNames.json'
def scalar(text,key,value):
    line=f'  {key}: {value}'
    return re.sub(r'^  '+key+r':[^\n]*',lambda _:line,text,flags=re.M) if re.search(r'^  '+key+r':',text,re.M) else text+line+'\n'
def section(text,key,rows):
    def val(v):return struct.pack('<'+str(len(v))+'i',*v).hex() if isinstance(v,list) else str(v)
    body=f'  {key}: []\n' if not rows else f'  {key}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+k+': '+val(v)+'\n' for j,(k,v) in enumerate(row.items())) for row in rows)
    pattern=r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*'
    return re.sub(pattern,lambda _:body,text,flags=re.M) if re.search(pattern,text,re.M) else text+body
def main():
    assert '--apply' in sys.argv,'Inspect draft previews before --apply.'
    assert not (HERE/'protected-before.json').exists(),'Already authored; use audit.py --assets.'
    for d in DESIGNS:
        check(d);assert not (LEVELS/f'LevelV1_{d["n"]*10:08d}.asset').exists()
    catalog=CATALOG.read_text();assert catalog==(HERE/'Before/LevelCatalogProV1.asset').read_text()
    assert max(map(int,re.findall(r'^    level: (\d+)$',catalog,re.M)))==201
    names=json.loads(NAMES.read_text());assert names==json.loads((HERE/'Before/LevelNames.json').read_text())
    for d in DESIGNS:assert not any(e['level']==d['n'] or e['name']==d['title'] for e in names['entries'])
    docs=ROOT/'Docs/LevelDesign'
    for p in (docs/'Design-State.md',docs/'Design-Feedback.md',docs/'Level-Names.md',docs/'Obstacle-Design-Guide.md'):
        shutil.copyfile(p,HERE/'Before'/p.name)
    protected={}
    for folder in ('Settings','Scripts','Resources','Scenes','Prefabs','Shaders','Editor','Data'):
        for p in (ROOT/'Assets/_Project'/folder).rglob('*'):
            if p.is_file() and p not in (CATALOG,NAMES) and p.suffix in ('.asset','.meta','.cs','.json','.unity','.prefab','.shader','.xml','.mm','.java'):
                protected[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
    (HERE/'protected-before.json').write_text(json.dumps(protected,indent=2)+'\n')
    manifest=[];template=(LEVELS/'LevelV1_00002010.asset').read_text()
    for d in DESIGNS:
        p=LEVELS/f'LevelV1_{d["n"]*10:08d}.asset';text=template
        for k,v in dict(m_Name=p.stem,width=9,height=d['h'],moves=d['moves'],levelKind=0,baseCoinReward=10,
            usesCustomIntro=0,oilSpreadMaxCells=0,comboTutorial=0,rocketBasketFireAllOnHit=0,refillSpecialGuard=-1).items():text=scalar(text,k,v)
        for k,v in {**d['data'],'randomPool':[0,1,2,3]}.items():text=scalar(text,k,struct.pack('<'+str(len(v))+'i',*v).hex())
        for k,rows in [('goals',d['goals']),('ancientSeals',d['seals']),('waterChannels',d['channels']),('paintCanBoxColors',d['paintColors'])]:text=section(text,k,rows)
        for k in ('tutorialSteps','stackedObstacles','tubes','magnets','safes','tarpStatues','colorCrateInitialHits','bossWaves'):text=section(text,k,[])
        p.write_text(text);guid=uuid.uuid4().hex
        Path(str(p)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
        catalog+=f'  - chapter: 9\n    level: {d["n"]}\n    levelKey: LevelCL_0{d["n"]}\n    levelData: {{fileID: 11400000, guid: {guid}, type: 2}}\n'
        names['entries'].append(dict(level=d['n'],levelKey=f'LevelCL_0{d["n"]}',name=d['title'],nameEn=d['nameEn']))
        manifest.append(dict(level=d['n'],asset=str(p.relative_to(ROOT)),guid=guid,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    CATALOG.write_text(catalog);NAMES.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
    p=docs/'Level-Names.md';text=p.read_text().replace('(1–201)','(1–205)')
    for d in DESIGNS:text+=f'| {d["n"]} | {d["title"]} |\n'
    p.write_text(text)
    (HERE/'manifest.json').write_text(json.dumps(dict(levels=manifest,revision201='Bird collectible amount only: 12 -> 50',scope='V1 202–205 only; no runtime changes, publishing or release baseline.'),indent=2)+'\n')
    print('Created 202–205; existing catalog references and names preserved. Run audit.py --assets.')
if __name__=='__main__':main()
