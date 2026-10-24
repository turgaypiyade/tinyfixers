"""Append level 188 only. Preserve all pre-existing level/runtime data."""
from pathlib import Path
import hashlib,json,re,struct,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
SETTINGS=ROOT/'Assets/_Project/Settings';DOCS=ROOT/'Docs/LevelDesign'
asset=SETTINGS/'ProductionLevels/LevelP_001920.asset'
catalog=SETTINGS/'LevelCatalogPro.asset';names_path=ROOT/'Assets/_Project/Resources/LevelNames.json'
assert not asset.exists() and not (HERE/'protected-before.json').exists()
before=catalog.read_text();assert re.findall(r'^    level: (\d+)$',before,re.M)==list(map(str,range(1,188)))
(HERE/'catalog-before.txt').write_text(before)
for source,target in ((names_path,'names-before.json'),(DOCS/'Design-State.md','state-before.md'),(DOCS/'Level-Names.md','level-names-before.md')):
    (HERE/target).write_bytes(source.read_bytes())
protected={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest()
    for folder in ('Settings','Scripts','Resources','Scenes','Prefabs')
    for p in (ROOT/'Assets/_Project'/folder).rglob('*')
    if p.is_file() and p not in (catalog,names_path)}
(HERE/'protected-before.json').write_text(json.dumps(protected,indent=2)+'\n')
d=json.loads((HERE/'design.json').read_text());d.update(asset=asset.stem,guid=uuid.uuid4().hex)
text=(SETTINGS/'ProductionLevels/LevelP_001910.asset').read_text()
def field(key,value):
    global text
    text,n=re.subn(r'^  '+key+r':[^\n]*$',lambda _:f'  {key}: {value}',text,flags=re.M);assert n==1,key
def entries(key,values):
    global text
    body=f'  {key}: []\n' if not values else f'  {key}:\n'+''.join(
        ''.join(f'  - {k}: {v}\n' if i==0 else f'    {k}: {v}\n' for i,(k,v) in enumerate(e.items())) for e in values)
    text,n=re.subn(r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda _:body,text,flags=re.M);assert n==1,key
for k,v in dict(m_Name=d['asset'],width=9,height=11,moves=d['moves'],levelKind=0,usesCustomIntro=0,refillSpecialGuard=-1,energyPerContainer=1,rocketBasketFireAllOnHit=0,comboTutorial=0,hamsterSatietyOverride=0).items():field(k,v)
field('randomPool',struct.pack('<4i',0,1,2,3).hex())
for k,v in d['data'].items():field(k,struct.pack('<99i',*v).hex())
entries('goals',d['goals']);entries('ancientSeals',d['ancientSeals'])
for k in ('colorCrateInitialHits','tubes','magnets','waterChannels','safes','stackedObstacles','paintCanBoxColors','tarpStatues','tutorialSteps','bossWaves'):entries(k,[])
asset.write_text(text)
Path(str(asset)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {d["guid"]}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
catalog.write_text(before+f'  - chapter: 8\n    level: 188\n    levelKey: LevelCL_0188\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n')
names=json.loads(names_path.read_text());assert len(names['entries'])==187 and d['title'] not in {e['name'] for e in names['entries']}
names['entries'].append(dict(level=188,levelKey='LevelCL_0188',name=d['title']))
names_path.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
p=DOCS/'Level-Names.md';p.write_text(p.read_text().replace('— 1–187','— 1–188',1)+'| 188 | Üç Mühürlü Geçit |\n')
(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print('188 Üç Mühürlü Geçit: 28 moves, 3 seals, 6 cargo, 21 two-stage plastics, 9 special-only compasses.')
