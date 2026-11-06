"""Prepare supplied assets, separate fly parts and append only IDs100–102."""
from pathlib import Path
from PIL import Image,ImageFilter,ImageChops,ImageOps,ImageDraw
import re,uuid,json,hashlib,shutil
R=Path(__file__).resolve().parents[1];S=R/'Docs/Checks/FlyFruit-2026-11-06/Sources';O=R/'Assets/_Project/Resources/FlyFruit'
guid=lambda n:uuid.uuid5(uuid.NAMESPACE_URL,'tinyfixers/flyfruit/'+n).hex
template=(R/'Assets/_Project/Resources/GrassWaterLily/CF1.png.meta').read_text()
FLY=['FlyFrontBody','FlyFrontLeftWing','FlyFrontRightWing','FlySideBody','FlySideLeftWing','FlySideRightWing']
def save(name,im):
    # Keep a small uniform transparent margin; original colors and proportions retained.
    box=im.getchannel('A').point(lambda x:255 if x>8 else 0).getbbox();assert box
    im=im.crop(box);im=ImageOps.contain(im,(496,496),Image.Resampling.LANCZOS);canvas=Image.new('RGBA',(512,512));canvas.alpha_composite(im,((512-im.width)//2,(512-im.height)//2))
    if name in FLY:
        dest=S.parent/'PreparedParts';dest.mkdir(exist_ok=True);canvas.save(dest/(name+'.png'));return canvas
    canvas.save(O/(name+'.png'))
    t=re.sub(r'^guid: .*$',f'guid: {guid(name)}',template,flags=re.M);t=re.sub(r'^    spriteID: .*$',f'    spriteID: {guid(name+"sprite")}',t,flags=re.M)
    (O/(name+'.png.meta')).write_text(t)
    return canvas
def fly_parts(name):
    im=Image.open(S/(name+'.png')).convert('RGBA');w,h=im.size;a=bytearray(im.getchannel('A').point(lambda x:255 if x>220 else 0).tobytes());parts=[]
    for start in range(w*h):
        if not a[start]:continue
        a[start]=0;q=[start];pixels=[];minx=w
        while q:
            i=q.pop();pixels.append(i);x=i%w;minx=min(minx,x)
            for j in [i-w,i+w]+([i-1]if x else [])+([i+1]if x<w-1 else []):
                if 0<=j<w*h and a[j]:a[j]=0;q.append(j)
        if len(pixels)>2000:parts.append((minx,pixels))
    parts.sort();assert len(parts)==3
    result={}
    for suffix,(_,pixels) in zip(['LeftWing','Body','RightWing'],parts):
        mask=bytearray(w*h)
        for i in pixels:mask[i]=255
        # Bounded edge expansion retains antialias/fur, excluding the side-sheet's broad gray halo.
        mask=Image.frombytes('L',(w,h),bytes(mask)).filter(ImageFilter.MaxFilter(9));out=im.copy();out.putalpha(ImageChops.multiply(im.getchannel('A'),mask));result[name+suffix]=save(name+suffix,out)
    return result
def ref(n):return '{fileID: '+str(21300001+FLY.index(n) if n in FLY else 21300000)+', guid: '+guid('FlyAtlas' if n in FLY else n)+', type: 3}'
def atlas(art):
    image=Image.new('RGBA',(1536,1024));t=re.sub(r'^guid: .*$',f'guid: {guid("FlyAtlas")}',template,flags=re.M)
    t=t.replace('  spriteMode: 1','  spriteMode: 2');t=re.sub(r'maxTextureSize: \d+','maxTextureSize: 2048',t)
    table='  internalIDToNameTable:\n';entries='    sprites:\n';names='    nameFileIdTable:\n'
    for i,name in enumerate(FLY):
        fid=21300001+i;x=i%3*512;y=512-i//3*512;image.alpha_composite(art[name],(x,i//3*512))
        table+=f'  - first:\n      213: {fid}\n    second: {name}\n';names+=f'      {name}: {fid}\n'
        entries+=f'''    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {x}
        y: {y}
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
      spriteID: {guid(name+'atlas')}
      internalID: {fid}
      vertices: []
      indices: 
      edges: []
      weights: []
'''
    t=t.replace('  internalIDToNameTable: []\n',table).replace('    sprites: []\n',entries).replace('    nameFileIdTable: {}\n',names)
    t=re.sub(r'^    spriteID: .*$','    spriteID: ',t,flags=re.M)
    image.save(O/'FlyAtlas.png');(O/'FlyAtlas.png.meta').write_text(t)
def definition(old,source,id,sprite,behavior,blocks,hits,damage,size,aux,icon):
    b=re.search(r'^  - id: '+str(source)+r'\n.*?(?=^  - id:|\Z)',old,re.M|re.S)[0];b=b.replace('  - id: '+str(source),'  - id: '+str(id),1)
    stage=f'''    stages:
    - sprite: {ref(sprite)}
      damageRule: {damage}
      blocksCells: {blocks}
      behavior: {behavior}
      allowDiagonal: {1 if blocks else 0}
      locksInteraction: 0
      holdsTile: 0
      suppressHitParticles: 1
      hitParticleSprites: []
      hitSound: {{fileID: 0}}
      hitSoundVolume: 1
'''
    b=re.sub(r'^    stages:\n.*?(?=^    auxiliarySprites:)',stage,b,flags=re.M|re.S)
    b=re.sub(r'^    auxiliarySprites:[^\n]*\n(?:    - .*\n)*',('    auxiliarySprites:\n'+''.join('    - '+ref(x)+'\n' for x in aux)) if aux else '    auxiliarySprites: []\n',b,flags=re.M)
    for k,v in {'size':f'{{x: {size[0]}, y: {size[1]}}}','hits':str(hits),'fullCellSprite':'0','goalIconSprite':ref(icon),'suppressHitParticles':'1','breakParticleSprites':'[]','hitSound':'{fileID: 0}','breakSound':'{fileID: 0}','legacySprite':ref(sprite),'legacyBlocksCells':str(blocks),'legacyBehavior':str(behavior),'wardrobeItemSprites':'[]'}.items():b=re.sub(r'^    '+k+r':[^\n]*$',f'    {k}: {v}',b,flags=re.M)
    b=re.sub(r'^    wardrobeItemSprites: \[\]\n(?:    - .*\n)+','    wardrobeItemSprites: []\n',b,flags=re.M)
    return b
def main():
    owned=S.parent/'owned-art-hashes.json'
    if owned.exists():
        assert all((R/p).is_file() and hashlib.sha256((R/p).read_bytes()).hexdigest()==h for p,h in json.loads(owned.read_text()).items()),'Artist changes detected; refusing overwrite.'
    owned_defs=S.parent/'owned-definitions.json'
    if owned_defs.exists():
        current=(R/'Assets/_Project/Settings/ObstacleLibrary.asset').read_text()
        for id,digest in json.loads(owned_defs.read_text()).items():
            block=re.search(r'^  - id: '+id+r'\n.*?(?=^  - id:|\Z)',current,re.M|re.S)
            assert block and hashlib.sha256(block[0].encode()).hexdigest()==digest,'Obstacle settings changed; refusing overwrite.'
    O.mkdir(exist_ok=True);Path(str(O)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid("folder")}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    art={}
    for n in ['BoxFrame','Egg','Apple','AppleHalf','BoxReference']:art[n]=save(n,Image.open(S/(n+'.png')).convert('RGBA'))
    art.update(fly_parts('FlyFront'));art.update(fly_parts('FlySide'))
    atlas(art)
    lib=R/'Assets/_Project/Settings/ObstacleLibrary.asset';s=lib.read_text()
    # Append under the definitions sequence, before subsequent root-level settings if any.
    blocks=[definition(s,28,100,'BoxFrame',1,1,4,1,(2,2),['Egg','BoxReference'],'BoxReference'),definition(s,30,101,'Egg',3,0,1,0,(1,1),FLY,'Egg'),definition(s,28,102,'Apple',1,1,6,5,(1,1),['AppleHalf'],'Apple')]
    if not re.search(r'^  - id: 100$',s,re.M):
        end=list(re.finditer(r'^  - id: \d+\n.*?(?=^  - id:|^  [A-Za-z_]|\Z)',s,re.M|re.S))[-1].end();s=s[:end]+''.join(blocks)+s[end:];lib.write_text(s)
    else:
        for id,b in zip([100,101,102],blocks):s=re.sub(r'^  - id: '+str(id)+r'\n.*?(?=^  - id:|\Z)',lambda _:b,s,flags=re.M|re.S)
        lib.write_text(s)
    # Preserve the earlier split outputs outside Assets; runtime uses one shared fly texture.
    for name in FLY:
        for suffix in ['.png','.png.meta']:
            p=O/(name+suffix)
            if p.exists():
                dest=S.parent/'LegacySplitParts';dest.mkdir(exist_ok=True);shutil.move(p,dest/p.name)
    sheet=Image.new('RGBA',(1536,4*400),'#19283a');dr=ImageDraw.Draw(sheet)
    for j,(n,im) in enumerate(art.items()):
        x=(j%3)*512;y=(j//3)*400;sheet.alpha_composite(im.resize((340,340),Image.Resampling.LANCZOS),(x+80,y+30));dr.text((x+20,y+8),n,fill='white')
    sheet.convert('RGB').save(S.parent/'Prepared-Assets.png');(S.parent/'asset-guids.json').write_text(json.dumps({n:guid('FlyAtlas' if n in FLY else n) for n in art},indent=2)+'\n')
    (S.parent/'asset-refs.json').write_text(json.dumps({n:dict(guid=guid('FlyAtlas' if n in FLY else n),fileID=21300001+FLY.index(n)if n in FLY else 21300000) for n in art},indent=2)+'\n')
    print('Prepared11 supplied sprites in6textures;fly parts share one atlas;library100–102 linked.')
if __name__=='__main__':main()
