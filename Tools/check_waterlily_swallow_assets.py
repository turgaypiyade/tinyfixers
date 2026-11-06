"""Independent metadata/library/preservation audit and static authored motion sheet."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,re,hashlib,subprocess
ROOT=Path(__file__).resolve().parents[1];DOC=ROOT/'Docs/Checks/WaterLily-Directional-Swallow-2026-11-06';ART=ROOT/'Assets/_Project/Resources/GrassWaterLily/Swallow';LIB=ROOT/'Assets/_Project/Settings/ObstacleLibrary.asset';SERVICE=ROOT/'Assets/_Project/Scripts/Grid/Grass/GrassWaterLilyService.cs'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def yaml(p):return json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e',"puts JSON.generate(YAML.safe_load(File.read(ARGV[0]),aliases:false))",str(p)]))
old=(DOC/'Before'/LIB.relative_to(ROOT)).read_text();now=LIB.read_text()
pattern=r'^  - id: 69\n.*?(?=^  - id:|\Z)';ob=re.search(pattern,old,re.M|re.S)[0];nb=re.search(pattern,now,re.M|re.S)[0]
assert old.replace(ob,'')==now.replace(nb,''),'Other obstacle definitions changed.'
aux=lambda b:re.search(r'^    auxiliarySprites:\n(?:    - .*\n)+',b,re.M)[0]
assert ob.replace(aux(ob),'')==nb.replace(aux(nb),''),'Lily mechanics/audio/default art changed.'
oldrefs=re.findall(r'fileID: (\d+), guid: (\w+)',aux(ob));refs=re.findall(r'fileID: (\d+), guid: (\w+)',aux(nb));assert len(refs)==25 and refs[:5]==oldrefs
snapshot=json.loads((DOC/'protected-before.json').read_text());assert not [p for p,h in snapshot.items() if not(ROOT/p).exists() or sha(ROOT/p)!=h],'Other project files changed.'
sources=json.loads((DOC/'sources.json').read_text());loaded={};guids=set()
for side,name in enumerate(['Up','Right','Down','Left']):
    path=ART/(name+'.png');im=Image.open(path).convert('RGBA');assert im.size==(2560,512)
    meta=yaml(Path(str(path)+'.meta'));importer=meta['TextureImporter'];sprites=importer['spriteSheet']['sprites'];assert importer['spriteMode']==2 and len(sprites)==5
    assert importer['mipmaps']['enableMipMap']==0 and importer['maxTextureSize']>=2560
    assert not importer['isReadable'] and importer['alphaIsTransparency']==1
    assert meta['guid'] not in guids;guids.add(meta['guid']);loaded[name]=im
    for j,sprite in enumerate(sprites):
        assert sprite['rect']==dict(serializedVersion=2,x=j*512,y=0,width=512,height=512)
        assert sprite['pivot']==dict(x=.5,y=.5)
        assert refs[5+side*5+j]==(str(sprite['internalID']),meta['guid'])
        assert importer['spriteSheet']['nameFileIdTable'][sprite['name']]==sprite['internalID']
        assert im.crop((j*512,0,(j+1)*512,512)).getchannel('A').getbbox()
    src=DOC/'Sources'/('lilly'+name.lower()+'.png');assert sha(src)==sources[name]['sourceSHA256']
    assert sources[name]['playbackReversed']==(name=='Left')
    assert all(max(b[2]-b[0],b[3]-b[1])<640 for b in sources[name]['sourceBounds']),'Opaque frame clipped.'
# Static timeline: same production timing/scale; marked as illustrative, not a Unity capture.
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',20);times=[0,.25,.40,.49,.58,.68,.84,.96,1.0]
preview=Image.new('RGBA',(1800,1100),'#19283a');dr=ImageDraw.Draw(preview)
smooth=lambda t:t*t*(3-2*t)
clamp=lambda x:max(0,min(1,x))
for side,name in enumerate(['Up','Right','Down','Left']):
    for j,t in enumerate(times):
        cx=j*200+100;cy=side*250+145;dx,dy=[(0,-1),(1,0),(0,1),(-1,0)][side]
        dr.text((j*200+8,side*250+7),f'{name} {t:.2f}',font=font,fill='white')
        dr.ellipse((cx-3,cy-3,cx+3,cy+3),fill='#6b8195')
        reach=smooth(clamp(t/.45))*(1-smooth(clamp((t-.66)/.34)))
        if t>=1:frame=Image.open(ROOT/'Assets/_Project/Resources/GrassWaterLily/CF5.png').convert('RGBA');size=88
        else:
            n=min(4,int(clamp((t-.28)/.36)*5));frame=loaded[name].crop((n*512,0,(n+1)*512,512));size=round(88*1.44)
        frame=frame.resize((size,size),Image.Resampling.LANCZOS);x=round(cx+dx*40*.9*reach-size/2);y=round(cy+dy*40*.9*reach-size/2)
        preview.alpha_composite(frame,(x,y))
dr.text((18,1020),'Static production-timing illustration · closed-only return → existing CF5 at home · not a Unity render.',font=font,fill='white')
preview.convert('RGB').save(DOC/'Timeline.png')
result=dict(result='PASS',protectedFiles=len(snapshot),allLevelAssetsPreserved=True,changedExistingProjectFiles=[str(SERVICE.relative_to(ROOT)),str(LIB.relative_to(ROOT))],directionalFrames=20,originalFramesRetained=5,sourceSheets=4,otherObstacleDefinitionsPreserved=True,unityBuildOrPlay=False)
(DOC/'assets-validation.json').write_text(json.dumps(result,indent=2)+'\n');(DOC/'owned-hashes.json').write_text(json.dumps({str(p.relative_to(ROOT)):sha(p) for p in [SERVICE,LIB]},indent=2)+'\n')
print('PASS:20 sprite links/canvases/source sheets;5 old frames and all levels retained;',len(snapshot),'other project files unchanged.')
