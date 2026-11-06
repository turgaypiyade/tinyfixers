"""Static asset/integration/preservation audit and real-sprite assembly review."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
import re,json,subprocess,hashlib
R=Path(__file__).resolve().parents[1];D=R/'Docs/Checks/FlyFruit-2026-11-06';A=R/'Assets/_Project/Resources/FlyFruit'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
old=json.loads((D/'project-before.json').read_text());changed=[p for p,h in old.items() if not(R/p).is_file() or sha(R/p)!=h]
allowed=['Assets/_Project/'+p for p in ['Scripts/Core/LevelData.cs','Scripts/Grid/GridSpawner.cs','Scripts/Grid/Board/BoardController.cs','Scripts/Grid/Board/Obstacles/ObstacleStateService.cs','Scripts/Editor/LevelDataEditor.cs','Scripts/Grid/Board/Actions/PatchBotTargetCoordinator.cs','Settings/ObstacleLibrary.asset']]
assert set(changed)==set(allowed),changed
assert not any('ProLevels/' in p for p in changed)
lib=R/'Assets/_Project/Settings/ObstacleLibrary.asset';text=lib.read_text();before=(D/'Before/Settings/ObstacleLibrary.asset').read_text()
blocks=lambda s:{int(m[1]):m[0] for m in re.finditer(r'^  - id: (\d+)\n.*?(?=^  - id:|\Z)',s,re.M|re.S)}
b,n=blocks(before),blocks(text);assert set(n)-set(b)=={100,101,102};assert all(b[k]==n[k] for k in b)
parsed=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e',"s=File.readlines(ARGV[0]).reject{|l|l.start_with?('%','---')}.join;puts JSON.generate(YAML.safe_load(s,aliases:false))",str(lib)]))
defs=next(v for v in parsed['MonoBehaviour'].values() if isinstance(v,list) and any(isinstance(x,dict) and x.get('id')==102 for x in v));defs={d['id']:d for d in defs}
assert defs[100]['size']=={'x':2,'y':2} and defs[100]['stages'][0]['damageRule']==1
assert defs[101]['hits']==1 and defs[101]['stages'][0]['behavior']==3 and defs[101]['stages'][0]['blocksCells']==0
assert defs[101]['stages'][0]['damageRule']==0 and not defs[101]['restrictNormalMatchTileType']
assert defs[102]['hits']==6 and defs[102]['size']=={'x':1,'y':1}
guids=json.loads((D/'asset-guids.json').read_text());all_guids={}
refs=json.loads((D/'asset-refs.json').read_text())
for p in (R/'Assets').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)$',p.read_text(errors='replace'),re.M)
    if m:all_guids.setdefault(m[1],[]).append(p)
for name,guid in guids.items():
    p=(D/'PreparedParts' if name.startswith('Fly') else A)/(name+'.png');assert Image.open(p).size==(512,512) and len(all_guids[guid])==1
    meta=all_guids[guid][0].read_text();assert 'enableMipMap: 0' in meta and 'isReadable: 0' in meta and 'alphaIsTransparency: 1' in meta
atlas=Image.open(A/'FlyAtlas.png').convert('RGBA');assert atlas.size==(1536,1024)
meta=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.safe_load(File.read(ARGV[0]),aliases:false))',str(A/'FlyAtlas.png.meta')]))
sprites=meta['TextureImporter']['spriteSheet']['sprites'];assert len(sprites)==6
for sprite in sprites:
    name=sprite['name'];r=sprite['rect'];assert refs[name]['fileID']==sprite['internalID']
    crop=atlas.crop((r['x'],1024-r['y']-r['height'],r['x']+r['width'],1024-r['y']))
    assert crop.tobytes()==Image.open(D/'PreparedParts'/(name+'.png')).convert('RGBA').tobytes()
expected={100:['Egg','BoxReference'],101:['FlyFrontBody','FlyFrontLeftWing','FlyFrontRightWing','FlySideBody','FlySideLeftWing','FlySideRightWing'],102:['AppleHalf']}
for id,names in expected.items():
    assert [r['guid'] for r in defs[id]['auxiliarySprites']]==[guids[n] for n in names]
    assert [r['fileID'] for r in defs[id]['auxiliarySprites']]==[refs[n]['fileID'] for n in names]
    assert defs[id]['goalIconSprite']['guid']==guids[{100:'BoxReference',101:'Egg',102:'Apple'}[id]]
for name,info in json.loads((D/'sources.json').read_text()).items():assert sha(D/'Sources'/name)==info['sha256']
controller=(R/'Assets/_Project/Scripts/Grid/Board/BoardController.cs').read_text();hook=controller.index('if (obstacleId == ObstacleId.FlyEgg) FlyFruits?.Hatch(originIndex);');assert hook<controller.index('OnObstacleDestroyed?.Invoke(originIndex, obstacleId);',hook)
service=(R/'Assets/_Project/Scripts/Grid/Board/Obstacles/FlyFruitService.cs').read_text();assert 'new AudioSource' not in service and '.PlayOneShot(' not in service
spawner=(R/'Assets/_Project/Scripts/Grid/GridSpawner.cs').read_text();assert 'IsSingleCellObstacle(lvl, authored, cell)' in spawner and 'clone.SynchronizeFruitGoals();' in spawner
editor=(R/'Assets/_Project/Scripts/Editor/LevelDataEditor.cs').read_text();assert editor.count('level.SetFruitDimensions(originIdx, selectedFruitWidth, selectedFruitHeight);')==2
assert 'EditorGUI.DisabledScope(automaticDiamond || automaticFruit)' in editor

# Real-sprite illustration of the runtime hierarchy, not a playable level or Unity screenshot.
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',23);bold=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf',29)
im=Image.new('RGBA',(1500,1150),'#19283a');dr=ImageDraw.Draw(im)
def art(name):return Image.open((D/'PreparedParts' if name.startswith('Fly') else A)/(name+'.png')).convert('RGBA')
def paste(name,box):
    x,y,w,h=box;image=ImageOps.contain(art(name),(round(w),round(h)),Image.Resampling.LANCZOS);im.alpha_composite(image,(round(x+(w-image.width)/2),round(y+(h-image.height)/2)))
dr.text((28,22),'SİNEK KUTUSU → YUMURTA → 3 SİNEK → MEYVE',font=bold,fill='#ffcf75')
paste('BoxFrame',(50,100,310,310))
for i in range(4):paste('Egg',(50+310*(.10 if i%2==0 else .54),100+310*(.10 if i<2 else .54),310*.36,310*.36))
paste('Egg',(465,160,145,145));paste('Apple',(830,110,270,270));paste('AppleHalf',(1170,110,270,270))
for x in [390,690,1110]:dr.text((x,220),'→',font=bold,fill='#ffcf75')
for x,text in [(45,'4 special → 4 yumurta'),(445,'1×1, düşen yumurta'),(825,'Başlangıç: bütün elma'),(1170,'İlk sürü: yarım elma')]:dr.text((x,440),text,font=font,fill='white')
def fly(prefix,cx,cy,mirror=False):
    # Same body/wing hierarchy, pivots and sizing as the flight view; static neutral pose.
    c=240;canvas=Image.new('RGBA',(650,450));center=(325,225)
    for part,xsign in [('LeftWing',-1),('RightWing',1)]:
        wing=art(prefix+part);w,h=round(c*.58),round(c*.4)
        wing=ImageOps.contain(wing,(w,h),Image.Resampling.LANCZOS)
        if prefix=='FlySide' and part=='RightWing':wing=wing.transpose(Image.Transpose.FLIP_LEFT_RIGHT).resize((round(wing.width*.72),round(wing.height*.72)),Image.Resampling.LANCZOS)
        rootx=center[0]+c*((-.12 if xsign<0 else -.09)if prefix=='FlySide' else xsign*.19)
        rooty=center[1]-c*.04
        extends_left=xsign<0 or prefix=='FlySide'
        x=round(rootx-wing.width*(.94 if extends_left else .06));y=round(rooty-wing.height*.78)
        canvas.alpha_composite(wing,(x,y))
    body=ImageOps.contain(art(prefix+'Body'),(round(c*.78),round(c*.78)),Image.Resampling.LANCZOS);canvas.alpha_composite(body,(round(center[0]-body.width/2),round(center[1]-body.height/2)))
    if mirror:canvas=canvas.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    im.alpha_composite(canvas,(cx-325,cy-225))
fly('FlyFront',250,680);fly('FlySide',750,680);fly('FlySide',1250,680,True)
for x,text in [(80,'Ön / yükselme ve ayrılma'),(600,'Sağ profil / kavisli uçuş'),(1100,'Sol profil / aynalanmış')]:dr.text((x,835),text,font=font,fill='white')
dr.text((30,895),'İkinci üçlü sürü kalan yarıyı yer; meyve kalkınca hedef bir azalır.',font=font,fill='white')
dr.text((30,945),'Goals / PreLevel: yalnız elma adedi. 1×1 ve2×2 elma aynı hedefe1 ekler.',font=font,fill='#ffcf75')
dr.text((30,1000),'Gerçek kaynak sprite’larıyla statik parça birleşimi; motor ekran görüntüsü değildir.',font=font,fill='#a9bdd1')
dr.text((30,1040),'Kanatlar uçuşta ayrı menteşelerden çırpar. Bu çizim bir level asseti değildir.',font=font,fill='#a9bdd1')
im.convert('RGB').save(D/'Review.png')
new=[str(p.relative_to(R)) for p in (R/'Assets/_Project').rglob('*') if p.is_file() and str(p.relative_to(R)) not in old]
result={'result':'PASS','existingProjectFilesPreserved':len(old)-len(changed),'changedExisting':changed,'newProjectFiles':new,'allLevelCatalogNameAssetsPreserved':True,'oldObstacleDefinitionsPreserved':len(b),'newIds':[100,101,102],'sourceImages':9,'preparedSprites':11,'unityBuildOrPlay':False}
(D/'asset-validation.json').write_text(json.dumps(result,indent=2)+'\n')
(D/'owned-art-hashes.json').write_text(json.dumps({str(p.relative_to(R)):sha(p) for p in A.iterdir() if p.is_file()},indent=2)+'\n')
(D/'owned-definitions.json').write_text(json.dumps({str(id):hashlib.sha256(n[id].encode()).hexdigest() for id in [100,101,102]},indent=2)+'\n')
print('PASS:only7existing project files changed;',len(old)-len(changed),'preserved;all levels/catalog/names and prior obstacle definitions identical. New sprite links valid.')
