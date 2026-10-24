"""Inventory catalog assets and compare authored layouts; never writes production assets.

Geometry scores shortlist visual review, not a novelty, gameplay or balance verdict.
Custom entries and lower layers are retained in the JSON for separate inspection.
"""
from pathlib import Path
from collections import Counter
import argparse,json,re,struct
from PIL import Image,ImageDraw,ImageFont,ImageOps
from check_levels161_162 import array,footprint,stage
from export_level_previews import int_field,parse_entries

ROOT=Path(__file__).resolve().parents[1]
SETTINGS=ROOT/'Assets/_Project/Settings'
FONT='/System/Library/Fonts/Supplemental/Arial.ttf'
COLORS={0:'#4c5c72',21:'#c899e5',27:'#76a989',25:'#a56c3b',28:'#e6d5ae',14:'#444333',26:'#71bcc8',29:'#dc788f',46:'#a859bf',33:'#86a9d3',64:'#dca35d',75:'#d8ad5c',76:'#439ca8'}

def path_entries(text,key):
    # Magnet and canal paths share a field name. Decode only the requested YAML section.
    section=re.search(r'^  '+key+r':[^\n]*\n((?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*)',text,re.M)
    if not section:return []
    return [dict(pathCellIndices=list(struct.unpack(f'<{len(raw)//8}i',bytes.fromhex(raw))))
            for raw in re.findall(r'^  - pathCellIndices: ([0-9a-f]+)$',section[1],re.M)]

def load():
    metas={re.search(r'^guid: (\w+)$',p.read_text(),re.M)[1]:Path(str(p)[:-5]) for p in (SETTINGS/'ProductionLevels').glob('*.asset.meta')}
    names={e['level']:e['name'] for e in json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text())['entries']}
    catalog=(SETTINGS/'LevelCatalogPro.asset').read_text();levels=[]
    for n,level_key,guid in re.findall(r'    level: (\d+)\n    levelKey: ([^\n]+)\n    levelData: \{fileID: 11400000, guid: (\w+)',catalog):
        n=int(n);p=metas[guid];t=p.read_text();w=int_field(t,'width');h=int_field(t,'height');size=w*h
        data={k:array(t,k,size) for k in ('cells','obstacles','obstacleOrigins')}
        custom={k:parse_entries(t,k) for k in ('stackedObstacles','safes','ancientSeals','tubes','magnets','waterChannels','tarpStatues')}
        # Generic numeric YAML helpers would turn digit-only serialized hex into one integer.
        for key in ('magnets','waterChannels'):custom[key]=path_entries(t,key)
        top=data['obstacles'][:];origins=data['obstacleOrigins'][:];depth=[int(o>0) for o in top]
        counts=Counter(o for i,o in enumerate(top) if o and origins[i]==i);issues=[]
        # The runtime stamps tubes and magnet paths before safes/overlays. Include authored paths.
        for o,key in ((26,'tubes'),(29,'magnets')):
            valid=0
            for s in custom[key]:
                if o==26:
                    origin=s['originCellIndex'];dx,dy=((0,-1),(0,1),(-1,0),(1,0))[s['direction']]
                    coords=[(origin%w+dx*k,origin//w+dy*k) for k in range(s['length'])]
                    if not all(0<=x<w and 0<=y<h for x,y in coords):
                        issues.append(dict(obstacleId=o,origin=origin,reason='invalid tube path; runtime GetCellIndices returns null'))
                        continue
                    region=[y*w+x for x,y in coords]
                else:
                    region=s['pathCellIndices'];origin=region[0]
                    if len(region)<2:continue
                valid+=1
                for j in region:
                    if not 0<=j<size:continue
                    if top[j]!=o or origins[j]!=origin:depth[j]+=1
                    top[j]=o;origins[j]=origin
            if valid:counts[o]=valid
        # Channel goal counts whole paths. The current spawner preserves authored 1x1
        # covers above the channel; multi-cell overlaps are still invalid. Source is pass-through.
        channel_count=0
        for entry in custom['waterChannels']:
            path=entry['pathCellIndices']
            if len(path)<2 or any(i<0 or i>=size or not data['cells'][i] for i in path):
                issues.append(dict(obstacleId=76,reason='invalid canal cell or length'))
                continue
            channel_count+=1
            top[path[0]]=0;origins[path[0]]=-1;depth[path[0]]=0
            for i in path[1:]:
                cover=top[i] if top[i] not in (0,76) and origins[i]==i and footprint(top[i])==(1,1) else 0
                if top[i]!=76:depth[i]+=1
                top[i]=cover or 76;origins[i]=i
        if channel_count:counts[76]=channel_count
        events=[(s.get('stackOrder',0),0,s['obstacleId'],s) for s in custom['stackedObstacles']]
        events += [(s.get('stackOrder',0),1,33,s) for s in custom['safes']]
        events += [(s.get('stackOrder',0),2,64,s) for s in custom['ancientSeals']]
        for order,kind,o,s in sorted(events,key=lambda e:e[:2]):
            origin=s['originCellIndex'];bw,bh=(s.get('width',2),s.get('height',2)) if kind else footprint(o)
            if o==68:
                entry=next((x for x in custom['tarpStatues'] if x['originCellIndex']==origin),{})
                bw=bh=max(2,entry.get('size',2))
            counts[o]+=1
            for dy in range(bh):
                for dx in range(bw):
                    x,y=origin%w+dx,origin//w+dy
                    if not(0<=x<w and 0<=y<h):
                        issues.append(dict(obstacleId=o,origin=origin,reason='overlay footprint outside board; omitted from geometry'))
                        continue
                    j=y*w+x;top[j]=o;origins[j]=origin;depth[j]+=1
        blocked=[bool(data['cells'][i] and o and (stage(o,'blocksCells') or stage(o,'behavior')==3)) for i,o in enumerate(top)]
        levels.append(dict(level=n,name=names.get(n,''),levelKey=level_key,asset=str(p.relative_to(ROOT)),width=w,height=h,moves=int_field(t,'moves'),kind=int_field(t,'levelKind'),data=data,top=top,topOrigins=origins,blocked=blocked,obstacleInstances=dict(counts),custom=custom,maxAuthoredDepth=max(depth),geometryWarnings=issues,goals=parse_entries(t,'goals')))
    assert len({d['level'] for d in levels})==len(levels)
    return levels

def plane(d,key,crop=False):
    seq=d['data']['cells'] if key=='mask' else d['blocked']
    im=Image.new('L',(d['width'],d['height']));im.putdata([255*int(v) for v in seq])
    if crop and im.getbbox():im=im.crop(im.getbbox())
    return im.resize((36,44),Image.Resampling.NEAREST)

def overlap(a,b):
    aa={i for i,v in enumerate(a.tobytes()) if v};bb={i for i,v in enumerate(b.tobytes()) if v}
    return len(aa&bb)/max(1,len(aa|bb))

def compare(target,other):
    a=plane(target,'blocked');b=plane(other,'blocked');am=plane(target,'mask');bm=plane(other,'mask')
    transforms=[('aynı yön',lambda im:im),('yatay ayna',ImageOps.mirror),('dikey ayna',ImageOps.flip),('180 derece',lambda im:ImageOps.flip(ImageOps.mirror(im)))]
    values=[(overlap(a,fn(b)),overlap(am,fn(bm)),label) for label,fn in transforms]
    best=max(values,key=lambda z:(z[0],z[1]))
    ca,cb=plane(target,'blocked',True),plane(other,'blocked',True)
    shape=max((overlap(ca,fn(cb)),label) for label,fn in transforms)
    fa=set(map(int,target['obstacleInstances']));fb=set(map(int,other['obstacleInstances']))
    return dict(level=other['level'],name=other['name'],blockerOverlap=round(best[0],4),maskOverlap=round(best[1],4),orientation=best[2],sameDirectionOverlap=round(values[0][0],4),croppedShapeOverlap=round(shape[0],4),croppedShapeOrientation=shape[1],sharedFamilies=sorted(fa&fb),familyOverlap=round(len(fa&fb)/max(1,len(fa|fb)),4))

def drawboard(im,d,xy,cell=14):
    draw=ImageDraw.Draw(im);x0,y0=xy;w=d['width'];h=d['height']
    draw.rectangle((x0-1,y0-1,x0+w*cell,y0+h*cell),fill='#080f1a',outline='#8898af')
    for i,c in enumerate(d['data']['cells']):
        if not c:continue
        x=x0+i%w*cell;y=y0+i//w*cell;o=d['top'][i]
        color=COLORS.get(o,'#cd9858') if o else COLORS[0]
        draw.rectangle((x+1,y+1,x+cell-1,y+cell-1),fill=color)
        if o and d['topOrigins'][i]==i and cell>=24:
            draw.text((x+2,y+2),str(o),font=ImageFont.truetype(FONT,11),fill='#07101c')

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--candidate',type=int,required=True);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    out=args.output;out.mkdir(parents=True,exist_ok=True);levels=load();target=next(d for d in levels if d['level']==args.candidate)
    previous=[d for d in levels if d['level']<args.candidate];rank=sorted([compare(target,d) for d in previous],key=lambda z:(z['blockerOverlap'],z['maskOverlap']),reverse=True)
    report=dict(candidate=args.candidate,previousLevelsScanned=len(previous),limits='Static authored geometry including valid tube paths, magnet paths, safes, seals and overlays. Custom data retained; no gameplay/dynamic spawning. IoU after normalization/reflection is a shortlist metric, never perceptual novelty percent. Crop score ignores size and placement. Opaque appearance/grass on movable objects require visual review.',geometryShortlist=rank[:24],croppedShapeShortlist=sorted(rank,key=lambda r:r['croppedShapeOverlap'],reverse=True)[:24],chestAndWardrobe=[d['level'] for d in previous if {21,27}<=set(map(int,d['obstacleInstances']))],chestLevels=[d['level'] for d in previous if 21 in d['obstacleInstances']],wardrobeLevels=[d['level'] for d in previous if 27 in d['obstacleInstances']],geometryWarnings=[dict(level=d['level'],issues=d['geometryWarnings']) for d in previous if d['geometryWarnings']])
    (out/'Catalog-Inventory.json').write_text(json.dumps(levels,ensure_ascii=False,indent=2)+'\n')
    (out/'Similarity.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    for start in range(0,len(previous),36):
        group=previous[start:start+36];im=Image.new('RGB',(1380,1350),'#19283a');dr=ImageDraw.Draw(im)
        dr.text((20,12),f'Katalog {group[0]["level"]}–{group[-1]["level"]} / authored yerleşim haritası',font=ImageFont.truetype(FONT,24),fill='#ffcf75')
        for j,d in enumerate(group):
            x=20+j%6*228;y=60+j//6*211
            dr.text((x,y),f'{d["level"]} · {d["name"][:23]}',font=ImageFont.truetype(FONT,15),fill='#ecf2fa')
            drawboard(im,d,(x,y+27),14)
        im.save(out/f'Catalog-{group[0]["level"]:03d}-{group[-1]["level"]:03d}.png')
    print('Scanned',len(previous),'previous catalog entries. Chest + Wardrobe:',report['chestAndWardrobe'])
    print('Board positions:',[(r['level'],r['name'],r['blockerOverlap']) for r in report['geometryShortlist'][:10]])
    print('Cropped silhouette:',[(r['level'],r['name'],r['croppedShapeOverlap']) for r in report['croppedShapeShortlist'][:10]])

if __name__=='__main__':main()
