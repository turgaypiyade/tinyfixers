"""One-time authoring of the user-requested Lantern introduction. No runtime mutations."""
from pathlib import Path
from collections import Counter
import hashlib,json,re,struct,uuid
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
SETTINGS=ROOT/'Assets/_Project/Settings';asset=SETTINGS/'ProductionLevels/LevelP_001860.asset'
assert not asset.exists() and not (HERE/'protected-before.json').exists()
catalog=SETTINGS/'LevelCatalogPro.asset';before=catalog.read_text();assert '    level: 182\n' not in before
(HERE/'catalog-before.txt').write_text(before)
(HERE/'protected-before.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for folder in (SETTINGS,ROOT/'Assets/_Project/Scripts') for p in folder.rglob('*') if p.is_file() and p!=catalog},indent=2)+'\n')
rows=['..ooooo..','.ooooooo.','ooooooooo','ooooooooo','LLLLLLLLL','LHLHLHLHL','HLHLHLHLH','.LHLHLHL.','.HLHLHLH.','..LHLHL..','..HLHLH..']
flat=''.join(rows);data={k:[0]*99 for k in ('obstacles','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')}
data['cells']=[int(c!='.') for c in flat];data['obstacleOrigins']=[-1]*99
for i,c in enumerate(flat):
    if c in 'LH':data['obstacles'][i]=75 if c=='L' else 30;data['obstacleOrigins'][i]=i
counts=Counter(o for o in data['obstacles'] if o);assert counts=={75:30,30:21}
goals=[dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=counts[o]) for o in (75,30)]
d=dict(n=182,title='Fener Şenliği',slug='Fener-Senligi',moves=28,h=11,size=99,difficulty='Normal',rows=rows,data=data,stacks=[],tubes=[],tarps=[],walls=[],goals=goals,asset='LevelP_001860',guid=uuid.uuid4().hex,decision='Üstte special kur; ilk fener sırasını yakıp açarak alttaki fener–porselen desenine ilerle.',flow=['İlk vuruş feneri yakar; ikinci vuruş toplar.','30 fener ve 21 porselenin tamamı hedef.','Daralan alt gövde 9 → 7 → 5 sütun.'],notes=['Lantern 75 ilk kez bu production levelinde tanıtılır; yeni davranış değil, PlasticTwoStage ile aynı veri temelli iki-stage movable.','Goal miktarı başlangıç adedine eşit; ek refill toplama kotası eklenmedi.','Sabit renk, hazır special, katman ve yardımcı yok.'])
t=(SETTINGS/'ProductionLevels/LevelP_001750.asset').read_text()
def field(t,k,v):
    t,n=re.subn(r'^  '+k+r':[^\n]*$',lambda m:f'  {k}: {v}',t,flags=re.M);assert n==1,k
    return t
def entries(t,k,values):
    body=f'  {k}: []\n' if not values else f'  {k}:\n'+''.join(''.join(f'  - {a}: {b}\n' if j==0 else f'    {a}: {b}\n' for j,(a,b) in enumerate(row.items())) for row in values)
    t,n=re.subn(r'^  '+k+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda m:body,t,flags=re.M);assert n==1,k
    return t
for k,v in dict(m_Name=d['asset'],width=9,height=11,moves=28,levelKind=0,oilSpreadMaxCells=0).items():t=field(t,k,v)
for k,v in data.items():t=field(t,k,struct.pack('<99i',*v).hex())
for k,v in dict(goals=goals,stackedObstacles=[],tubes=[],magnets=[],safes=[],ancientSeals=[],paintCanBoxColors=[],tarpStatues=[],tutorialSteps=[]).items():t=entries(t,k,v)
asset.write_text(t);Path(str(asset)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {d["guid"]}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
catalog.write_text(before+f'  - chapter: 7\n    level: 182\n    levelKey: LevelCL_0182\n    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n')
(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
namespath=ROOT/'Assets/_Project/Resources/LevelNames.json';names=json.loads(namespath.read_text())
assert len(names['entries'])==181 and d['title'] not in {e['name'] for e in names['entries']}
(HERE/'names-before.json').write_bytes(namespath.read_bytes())
names['entries'].append(dict(level=182,levelKey='LevelCL_0182',name=d['title']));namespath.write_text(json.dumps(names,ensure_ascii=False,indent=2)+'\n')
name_doc=ROOT/'Docs/LevelDesign/Level-Names.md';name_doc.write_text(name_doc.read_text().replace('— 1–181','— 1–182',1)+'| 182 | Fener Şenliği |\n')
print('182 Fener Şenliği created: 28 moves; 30 Lantern, 21 HelmetPorcelain; 81 active/18 holes.')
