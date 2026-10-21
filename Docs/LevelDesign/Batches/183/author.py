"""Author 183 and the explicitly requested +5-move revision of catalog 179. No gameplay execution."""
from pathlib import Path
import hashlib, json, re, shutil, struct, uuid

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
SETTINGS=ROOT/'Assets/_Project/Settings'
OUT=ROOT/'Docs/LevelDesign'
asset=SETTINGS/'ProductionLevels/LevelP_001870.asset'
prior=SETTINGS/'ProductionLevels/LevelP_001830.asset'
catalog=SETTINGS/'LevelCatalogPro.asset'
assert not asset.exists() and not (HERE/'protected-before.json').exists()
before=catalog.read_text()
match=re.search(r'(  - chapter: (\d+)\n    level: 183\n    levelKey: LevelCL_0183\n    levelData: \{fileID: 11400000, guid: )(\w+)(, type: 2\})',before)
assert match and match[3]=='742236c7e07594cd080ffc379607fde6'
assert re.search(r'    level: 179\n.*\n    levelData: .*'+re.search(r'^guid: (\w+)',Path(str(prior)+'.meta').read_text(),re.M)[1],before)
assert '  moves: 26\n' in prior.read_text()
(HERE/'catalog-before.txt').write_text(before)
(HERE/'protected-before.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest()
    for folder in (SETTINGS,ROOT/'Assets/_Project/Scripts',ROOT/'Assets/_Project/Resources')
    for p in folder.rglob('*') if p.is_file() and p not in (catalog,prior,ROOT/'Assets/_Project/Resources/LevelNames.json')},indent=2)+'\n')
shutil.copy2(OUT/'Design-State.md',HERE/'design-state-before.md')
archive=HERE/'Before';archive.mkdir()
for p in (prior,Path(str(prior)+'.meta'),OUT/'Level179-Celik-Kilit.md',OUT/'Level179-Celik-Kilit.png',
          SETTINGS/'ProductionLevels/LevelP_testt 1.asset',SETTINGS/'ProductionLevels/LevelP_testt 1.asset.meta'):
    shutil.copy2(p,archive/p.name)
old=prior.read_text();prior.write_text(old.replace('  moves: 26\n','  moves: 31\n',1))

data={k:[0]*99 for k in ('obstacles','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')}
data.update(cells=[1]*99,obstacleOrigins=[-1]*99)
for y in range(11):data['obstacles'][y*9]=75
for y in (9,10):
    for x in range(3,9):data['obstacles'][y*9+x]=28
data['obstacleOrigins']=[i if o else -1 for i,o in enumerate(data['obstacles'])]
coords=[(2,0),(2,1)]+[(x,2) for x in range(2,8)]+[(7,y) for y in range(3,7)]+[(x,6) for x in range(6,1,-1)]+[(2,y) for y in range(7,11)]
path=[y*9+x for x,y in coords]
assert all(not data['obstacles'][i] for i in path)
rows=[''.join('D' if i==path[0] else '~' if i in path else {0:'o',75:'L',28:'S'}[data['obstacles'][i]] for i in range(y*9,y*9+9)) for y in range(11)]
goals=[dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=n) for o,n in ((76,1),(75,24),(28,12))]
d=dict(n=183,title='Fenerli Bent',slug='Fenerli-Bent',moves=28,h=11,size=99,difficulty='Normal',chapter=int(match[2]),rows=rows,
    data=data,stacks=[],tubes=[],tarps=[],walls=[],waterChannels=[dict(pathCellIndices=path)],goals=goals,
    asset=asset.stem,guid=uuid.uuid4().hex,
    decision='Kıvrımlı kanalın kopuk bölümlerini açıp suyu alta ulaştır; fener sütunu ve taş setini de temizle.',
    flow=['Tek kanal: 1 kaynak + 20 açılacak hücre; hedef hücre değil 1 kanal.',
          'Solda kesintisiz 11 fener; toplam 24 hedefin 13 adedi mevcut dolumdan gelir.',
          'Sağ altta 6 × 2 taş seti: 12 üç aşamalı SculptingStone.',
          'Açık kanal hücreleri oynanır kalır; yalnız depo geçirgendir.'],
    notes=['İlk WaterChannel76 production tanıtımı kullanıcı isteğiyle yetkilendirildi.',
           'Kanal verisi yalnız waterChannels[] içinde; runtime 20 hücreyi damgalar. Kanal/depo başka engel veya yığınla çakışmaz.',
           'Tam 9×11 board, hole/sabit renk/hazır special/yardımcı yok. Başlangıçta 75 taş hücresi, bunların 20 tanesi kanal üstünde.',
           '28 hamle ilk ayar; çözüm veya geçiş oranı simüle edilmedi.'])
t=(SETTINGS/'ProductionLevels/LevelP_001860.asset').read_text()
def field(key,value):
    global t
    t,n=re.subn(r'^  '+key+r':[^\n]*$',lambda m:f'  {key}: {value}',t,flags=re.M);assert n==1,key
def entries(key,values):
    global t
    body=f'  {key}: []\n' if not values else f'  {key}:\n'+''.join(''.join(f'  - {k}: {v}\n' if j==0 else f'    {k}: {v}\n' for j,(k,v) in enumerate(e.items())) for e in values)
    if not re.search(r'^  '+key+':',t,re.M):
        assert key=='waterChannels';t=t.replace('  magnets: []\n','  magnets: []\n'+body)
    else:
        t,n=re.subn(r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda m:body,t,flags=re.M);assert n==1,key
field('m_Name',d['asset']);field('width',9);field('height',11);field('moves',28);field('levelKind',0)
for k,v in data.items():field(k,struct.pack('<99i',*v).hex())
entries('goals',goals)
entries('waterChannels',[dict(pathCellIndices=struct.pack('<'+str(len(path))+'i',*path).hex())])
for key in ('tubes','magnets','safes','ancientSeals','stackedObstacles','paintCanBoxColors','tarpStatues','tutorialSteps'):entries(key,[])
asset.write_text(t)
Path(str(asset)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {d["guid"]}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
catalog.write_text(before[:match.start(3)]+d['guid']+before[match.end(3):])
(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
np=ROOT/'Assets/_Project/Resources/LevelNames.json';names=json.loads(np.read_text())
assert len(names['entries'])==182
(HERE/'names-before.json').write_bytes(np.read_bytes())
names['entries'].append(dict(level=183,levelKey='LevelCL_0183',name=d['title']))
np.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
nd=OUT/'Level-Names.md';s=nd.read_text();s=re.sub(r'— 1–182','— 1–183',s,count=1);nd.write_text(s+'| 183 | Fenerli Bent |\n')
print('183 Fenerli Bent: 28 moves, 1 channel / 24 lantern / 12 stone; catalog 179: 26 -> 31 moves.')
print('\n'.join(rows))
