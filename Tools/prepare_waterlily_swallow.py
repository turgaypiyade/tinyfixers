"""Slice supplied directional frames without repainting; pad uniformly and import four atlases."""
from pathlib import Path
from PIL import Image,ImageChops,ImageDraw,ImageFont
import re,json,shutil,uuid,hashlib
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Assets/_Project/Resources/GrassWaterLily/Swallow';DOC=ROOT/'Docs/Checks/WaterLily-Directional-Swallow-2026-11-06'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
guid=lambda key:uuid.uuid5(uuid.NAMESPACE_URL,'tinyfixers/waterlily/swallow/'+key).hex
LIB=ROOT/'Assets/_Project/Settings/ObstacleLibrary.asset';SERVICE=ROOT/'Assets/_Project/Scripts/Grid/Grass/GrassWaterLilyService.cs'

def extract(im):
    w,h=im.size;raw=im.getchannel('A').tobytes();a=bytearray(255 if x>180 else 0 for x in raw);parts=[]
    for start in range(w*h):
        if not a[start]:continue
        a[start]=0;q=[start];indices=[];loX=w;hiX=0;loY=h;hiY=0
        while q:
            i=q.pop();indices.append(i);x=i%w;y=i//w;loX=min(loX,x);hiX=max(hiX,x);loY=min(loY,y);hiY=max(hiY,y)
            for j in [i-w,i+w]+([i-1] if x else [])+([i+1] if x<w-1 else []):
                if 0<=j<w*h and a[j]:a[j]=0;q.append(j)
        if len(indices)>2000:parts.append((loX,loY,hiX+1,hiY+1,indices))
    parts.sort(key=lambda p:p[0]);assert len(parts)==5,len(parts)
    # Nearest opaque component owns its antialias/glow pixels, including where X bounds overlap.
    labels=bytearray(w*h);q=[]
    for label,p in enumerate(parts,1):
        for i in p[4]:labels[i]=label;q.append(i)
    at=0
    while at<len(q):
        i=q[at];at+=1;x=i%w
        for j in [i-w,i+w]+([i-1] if x else [])+([i+1] if x<w-1 else []):
            if 0<=j<w*h and raw[j] and not labels[j]:labels[j]=labels[i];q.append(j)
    labelImage=Image.frombytes('L',(w,h),bytes(labels));result=[]
    for label,p in enumerate(parts,1):
        mask=labelImage.point([255 if n==label else 0 for n in range(256)])
        frame=im.copy();frame.putalpha(ImageChops.multiply(im.getchannel('A'),mask))
        cx=(p[0]+p[2])/2;cy=(p[1]+p[3])/2
        square=frame.crop((round(cx-320),round(cy-320),round(cx+320),round(cy+320)))
        assert square.size==(640,640)
        result.append(square.resize((512,512),Image.Resampling.LANCZOS))
    return result,[list(p[:4]) for p in parts]

def meta(name):
    t=(ROOT/'Assets/_Project/Resources/GrassWaterLily/CF1.png.meta').read_text();t=re.sub(r'^guid: .*$',f'guid: {guid(name)}',t,flags=re.M)
    ids=[21300000+j for j in range(1,6)]
    table='  internalIDToNameTable:\n'+''.join(f'  - first:\n      213: {fid}\n    second: {name}_{j}\n' for j,fid in enumerate(ids))
    t=t.replace('  internalIDToNameTable: []\n',table).replace('  spriteMode: 1','  spriteMode: 2')
    t=re.sub(r'maxTextureSize: \d+','maxTextureSize: 4096',t)
    entries='    sprites:\n'
    for j,fid in enumerate(ids):
        entries+=f'''    - serializedVersion: 2
      name: {name}_{j}
      rect:
        serializedVersion: 2
        x: {j*512}
        y: 0
        width: 512
        height: 512
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData: 
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {guid(name+str(j))}
      internalID: {fid}
      vertices: []
      indices: 
      edges: []
      weights: []
'''
    t=t.replace('    sprites: []\n',entries);t=re.sub(r'^    spriteID: .*$', '    spriteID: ',t,flags=re.M)
    t=t.replace('    nameFileIdTable: {}','    nameFileIdTable:\n'+''.join(f'      {name}_{j}: {fid}\n' for j,fid in enumerate(ids)).rstrip())
    return t

def main():
    DOC.mkdir(parents=True,exist_ok=True);OUT.mkdir(exist_ok=True)
    snapshot=DOC/'protected-before.json'
    if not snapshot.exists():
        for p in [LIB,SERVICE]:
            d=DOC/'Before'/p.relative_to(ROOT);d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,d)
        snapshot.write_text(json.dumps({str(p.relative_to(ROOT)):sha(p) for p in (ROOT/'Assets/_Project').rglob('*') if p.is_file() and p not in [LIB,SERVICE]},indent=2)+'\n')
    Path(str(OUT)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid("folder")}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    rows=[];refs=[];info={};font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',25)
    contact=Image.new('RGBA',(1600,4*350),'#19283a');draw=ImageDraw.Draw(contact)
    for row,name in enumerate(['Up','Right','Down','Left']):
        src=Path('/Users/turgayp/Downloads')/('lilly'+name.lower()+'.png');dst=DOC/'Sources'/src.name;dst.parent.mkdir(exist_ok=True);shutil.copy2(src,dst)
        frames,bounds=extract(Image.open(src).convert('RGBA'))
        if name=='Left':frames.reverse()
        atlas=Image.new('RGBA',(2560,512))
        for j,frame in enumerate(frames):
            atlas.alpha_composite(frame,(j*512,0));refs.append(f'    - {{fileID: {21300001+j}, guid: {guid(name)}, type: 3}}\n')
            contact.alpha_composite(frame.resize((300,300),Image.Resampling.LANCZOS),(j*320,row*350+30));draw.text((j*320+12,row*350+4),f'{name} {j}'+(' closed' if j==4 else ''),font=font,fill='white')
        atlas.save(OUT/(name+'.png'));(OUT/(name+'.png.meta')).write_text(meta(name));info[name]=dict(sourceSHA256=sha(src),sourceBounds=bounds,playbackReversed=name=='Left',frames=5,canvas=512)
    original=(DOC/'Before'/LIB.relative_to(ROOT)).read_text();block=re.search(r'^  - id: 69\n.*?(?=^  - id:|\Z)',original,re.M|re.S)[0]
    aux=re.search(r'^    auxiliarySprites:\n(?:    - .*\n){5}',block,re.M)[0];updated=block.replace(aux,aux+''.join(refs));current=LIB.read_text()
    currentBlock=re.search(r'^  - id: 69\n.*?(?=^  - id:|\Z)',current,re.M|re.S)[0]
    assert currentBlock in [block,updated],'Unexpected lily library edits.'
    LIB.write_text(current.replace(currentBlock,updated));contact.convert('RGB').save(DOC/'Frames.png');(DOC/'sources.json').write_text(json.dumps(info,indent=2)+'\n')
    print('Prepared four5-frame directional atlases;originalCF1–CF5 retained,auxiliary5–24 appended. Left reversed. No image repainting.')

if __name__=='__main__':main()
