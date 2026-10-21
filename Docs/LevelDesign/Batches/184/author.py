"""One-time production authoring; no existing level/runtime changes or simulation."""
from pathlib import Path
import hashlib,json,re,struct,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];SETTINGS=ROOT/'Assets/_Project/Settings';DOCS=ROOT/'Docs/LevelDesign'
asset=SETTINGS/'ProductionLevels/LevelP_001880.asset';catalog=SETTINGS/'LevelCatalogPro.asset';namespath=ROOT/'Assets/_Project/Resources/LevelNames.json'
assert not asset.exists() and not (HERE/'protected-before.json').exists()
before=catalog.read_text();assert '    level: 184\n' not in before
(HERE/'catalog-before.txt').write_text(before);(HERE/'names-before.json').write_bytes(namespath.read_bytes())
(HERE/'protected-before.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest()
    for folder in ('Settings','Scripts','Resources') for p in (ROOT/'Assets/_Project'/folder).rglob('*')
    if p.is_file() and p not in (catalog,namespath)},indent=2)+'\n')
(HERE/'state-before.md').write_bytes((DOCS/'Design-State.md').read_bytes())
data={k:[0]*99 for k in ('obstacles','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')}
data['cells']=[int(y<4 or y<8 and x>=2 or y>=8 and x>=6) for y in range(11) for x in range(9)]
data['obstacleOrigins']=[-1]*99
for y in range(4):
    for x in (0,1):i=y*9+x;data['obstacles'][i]=39;data['obstacleOrigins'][i]=i
for origin in (38,40,56,58):
    for dy in (0,1):
        for dx in (0,1):i=origin+dy*9+dx;data['obstacles'][i]=63;data['obstacleOrigins'][i]=origin
paths=[[3,12,13,14,15]+[y*9+6 for y in range(2,11)],[y*9+8 for y in range(11)]]
assert len(set(paths[0])&set(paths[1]))==0
allpath=set().union(*map(set,paths));sources={p[0] for p in paths}
rows=[''.join('.' if not data['cells'][i] else 'D' if i in sources else '~' if i in allpath else {0:'o',39:'S',63:'B'}[data['obstacles'][i]] for i in range(y*9,y*9+9)) for y in range(11)]
goals=[dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=n) for o,n in ((63,4),(76,2),(39,8))]
d=dict(n=184,title='Boya Terasları',slug='Boya-Teraslari',moves=28,h=11,size=99,difficulty='Normal',chapter=8,rows=rows,data=data,
    stacks=[],tubes=[],tarps=[],walls=[],waterChannels=[dict(pathCellIndices=p) for p in paths],goals=goals,asset=asset.stem,guid=uuid.uuid4().hex,
    decision='Üst boya kasalarını açıp alt terasa eriş; iki su yolunu tamamlarken soldaki special hedeflerini bitir.',
    flow=['Kenara bağlı dört boya kasası; toplam 36 kutu. Normal komşu hit1, special hit2 kutu düşürür.',
          'İki bağımsız kanal: toplam 23 açılacak hücre; goal 2 kanal.',
          'Solda 2 × 4 special-only pusula şeridi; hazır special yok.'],
    notes=['73 aktif / 26 hole; 2 depo runtime geçirgen, 47 açık başlangıç taşı.',
           'Boya kasaları alt terasın kenarını tamamlar; örtü altında authored normal taş cebi yok.',
           'Fener ve ColorCrate yok; önceki levellar ve kullanıcı değişiklikleri korunur.',
           '28 hamle ilk denge ayarı; oynanış veya kazanma oranı ölçülmedi.'])
t=(SETTINGS/'ProductionLevels/LevelP_001870.asset').read_text()
def field(key,value):
    global t
    t,n=re.subn(r'^  '+key+r':[^\n]*$',lambda m:f'  {key}: {value}',t,flags=re.M);assert n==1,key
def entries(key,values):
    global t
    body=f'  {key}: []\n' if not values else f'  {key}:\n'+''.join(''.join(f'  - {k}: {v}\n' if j==0 else f'    {k}: {v}\n' for j,(k,v) in enumerate(e.items())) for e in values)
    t,n=re.subn(r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda m:body,t,flags=re.M);assert n==1,key
for k,v in dict(m_Name=d['asset'],width=9,height=11,moves=28,levelKind=0).items():field(k,v)
for k,v in data.items():field(k,struct.pack('<99i',*v).hex())
entries('goals',goals);entries('waterChannels',[dict(pathCellIndices=struct.pack('<'+str(len(p))+'i',*p).hex()) for p in paths])
for key in ('tubes','magnets','safes','ancientSeals','stackedObstacles','paintCanBoxColors','tarpStatues','tutorialSteps'):entries(key,[])
asset.write_text(t);Path(str(asset)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {d["guid"]}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
catalog.write_text(before+f'  - chapter: 8\n    level: 184\n    levelKey: LevelCL_0184\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n')
names=json.loads(namespath.read_text());assert len(names['entries'])==183 and d['title'] not in {e['name'] for e in names['entries']}
names['entries'].append(dict(level=184,levelKey='LevelCL_0184',name=d['title']));namespath.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
np=DOCS/'Level-Names.md';np.write_text(np.read_text().replace('— 1–183','— 1–184',1)+'| 184 | Boya Terasları |\n')
(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print('184 Boya Terasları: 28 moves; 4 paint boxes / 2 water channels / 8 special-only targets.')
print('\n'.join(rows))
