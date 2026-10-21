"""Author the user's horizontally mirrored reference; no older level/runtime mutations."""
from pathlib import Path
import hashlib,json,re,struct,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];SETTINGS=ROOT/'Assets/_Project/Settings';DOCS=ROOT/'Docs/LevelDesign'
asset=SETTINGS/'ProductionLevels/LevelP_001890.asset';catalog=SETTINGS/'LevelCatalogPro.asset';np=ROOT/'Assets/_Project/Resources/LevelNames.json'
assert not asset.exists() and not (HERE/'protected-before.json').exists()
before=catalog.read_text();assert '    level: 185\n' not in before
(HERE/'catalog-before.txt').write_text(before);(HERE/'names-before.json').write_bytes(np.read_bytes())
(HERE/'protected-before.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for folder in ('Settings','Scripts','Resources') for p in (ROOT/'Assets/_Project'/folder).rglob('*') if p.is_file() and p not in (catalog,np)},indent=2)+'\n')
(HERE/'state-before.md').write_bytes((DOCS/'Design-State.md').read_bytes())
reference=json.loads((HERE/'reference-grid.json').read_text())
rows=[''.join('L' if c=='E' and y%2 else 'P' if c=='E' else c for c in row) for y,row in enumerate(reference['mirroredRows'])]
flat=''.join(rows);mapping={'L':75,'P':40,'S':28,'G':39,'o':0,'.':0}
data={k:[0]*99 for k in ('wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')}
data['cells']=[int(c!='.') for c in flat];data['obstacles']=[mapping[c] for c in flat]
data['obstacleOrigins']=[i if o else -1 for i,o in enumerate(data['obstacles'])]
data['pinnedSpecialTypes'][20]=4  # one PulseCore on the front edge of the 3x4 opening
goals=[dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=n) for o,n in ((75,24),(40,21),(28,9),(39,12))]
d=dict(n=185,title='Işık Merdiveni',slug='Isik-Merdiveni',moves=32,h=11,size=99,difficulty='Hard',chapter=8,rows=rows,data=data,
    stacks=[],tubes=[],tarps=[],walls=[],waterChannels=[],goals=goals,asset=asset.stem,guid=uuid.uuid4().hex,
    decision='TNT ve eşleşmelerle soldaki dar açılışı genişlet; yatay ışık şeritlerini açıp sağ alttaki pusulalara special gönder.',
    flow=['Yumurta alanı: satır satır 24 Lantern ve 21 PlasticTwoStage.',
          'Bej blok şeritleri: 9 adet üç vuruşlu SculptingStone.',
          'Sağ altta 12 special-only pusula; bir başlangıç TNTsi (20).'],
    notes=['Kullanıcının görseli 9x11 olarak okundu ve x -> 8-x ile yatay aynalandı.',
           'Sarı yerler bej blok şeritleri olarak yorumlandı; üstteki normal sarı taşlar renk havuzunda kaldı.',
           'Alt kıvrımlı bölge için cevap gelmeyen opsiyonel soruda açıklanan varsayım: SculptingSpecial39.',
           'Referanstaki iki köşe-temas düğümü korundu; ortak border/yuvarlatma kodu değiştirilmedi.',
           '78 aktif, 21 hole, 12 normal başlangıç hücresi. 32 hamle ilk ayar, ölçülmüş kazanılabilirlik yok.'])
t=(SETTINGS/'ProductionLevels/LevelP_001880.asset').read_text()
def field(k,v):
    global t
    t,n=re.subn(r'^  '+k+r':[^\n]*$',lambda m:f'  {k}: {v}',t,flags=re.M);assert n==1,k
def entries(k,values):
    global t
    body=f'  {k}: []\n' if not values else f'  {k}:\n'+''.join(''.join(f'  - {a}: {b}\n' if j==0 else f'    {a}: {b}\n' for j,(a,b) in enumerate(e.items())) for e in values)
    t,n=re.subn(r'^  '+k+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda m:body,t,flags=re.M);assert n==1,k
for k,v in dict(m_Name=d['asset'],width=9,height=11,moves=32,levelKind=0).items():field(k,v)
for k,v in data.items():field(k,struct.pack('<99i',*v).hex())
entries('goals',goals)
for k in ('waterChannels','tubes','magnets','safes','ancientSeals','stackedObstacles','paintCanBoxColors','tarpStatues','tutorialSteps'):entries(k,[])
asset.write_text(t);Path(str(asset)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {d["guid"]}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
catalog.write_text(before+f'  - chapter: 8\n    level: 185\n    levelKey: LevelCL_0185\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n')
names=json.loads(np.read_text());assert len(names['entries'])==184 and d['title'] not in {e['name'] for e in names['entries']}
names['entries'].append(dict(level=185,levelKey='LevelCL_0185',name=d['title']));np.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
p=DOCS/'Level-Names.md';p.write_text(p.read_text().replace('— 1–184','— 1–185',1)+'| 185 | Işık Merdiveni |\n')
(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print('185 Işık Merdiveni: horizontal mirror, 32 moves Hard; 24 Lantern /21 PlasticTwoStage /9 Stone /12 Compass.')
print('\n'.join(rows))
