"""Current catalog project-sprite review sheets; illustrative tile colours, no gameplay simulation."""
from pathlib import Path
from collections import deque
import importlib.util,json,re,random,sys
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
import review_level_patterns as review
from check_levels161_162 import stage,footprint,DEFS,array
from export_level_previews import parse_entries
spec=importlib.util.spec_from_file_location('art',HERE.parent/'173-180/render_previews.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
ENUM=re.search(r'public enum ObstacleId[^\{]*\{(.*?)^}',(ROOT/'Assets/_Project/Scripts/Core/LevelData.cs').read_text(),re.M|re.S)[1]
LABELS={int(m[2]):m[1] for m in re.finditer(r'^    (\w+) = (\d+)',ENUM,re.M)}
SOURCE_PHASE='After'
WARNINGS=set()
def flag(o,key,default=0):
    if not o:return default
    m=re.search(r'^    '+key+r': (\d+)',DEFS[o],re.M)
    return int(m[1]) if m else default
def spawn(d):
    w,h=d['width'],d['height'];top=d['top'];active={i for i,v in enumerate(d['data']['cells']) if v}
    sources={p['pathCellIndices'][0] for p in d['custom']['waterChannels']}
    fixed={i for i in active if top[i] and stage(top[i],'blocksCells')}
    movable={i for i in active if top[i] and stage(top[i],'behavior')==3}
    slots=active-fixed-movable-sources;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in fixed or i in movable:break
            if i in slots:reached.add(i)
    q=deque(reached)
    def corner(i):return i not in fixed and i not in movable or bool(top[i] and stage(top[i],'allowDiagonal'))
    while q:
        i=q.popleft();x,y=i%w,i//w
        if y+1==h:continue
        for dx in (0,-1,1):
            j=i+w+dx
            if not 0<=x+dx<w or j not in slots or j in reached:continue
            if dx and not(corner(i+dx) or corner(i+w)):continue
            reached.add(j);q.append(j)
    # Current GridSpawner also seeds a shadow if every blocker above permits filling.
    for i in slots-reached:
        above=[top[j] for j in range(i%w,i,w) if j in fixed or j in movable]
        if above and all(flag(o,'fillsShadowBeneath') for o in above):reached.add(i)
        elif top[i] and stage(top[i],'locksInteraction'):reached.add(i)
    return slots,reached
def artwork(o):
    try:return art.oid_art(o)
    except Exception as exc:
        WARNINGS.add((o,str(exc)));im=Image.new('RGBA',(128,128),'#874f78');ImageDraw.Draw(im).text((10,40),str(o),font=art.font(30,True),fill='white');return im
def board(im,d,xy,cell=50):
    dr=ImageDraw.Draw(im);w,h=d['width'],d['height'];top=d['top'];orig=d['topOrigins'];x0,y0=xy
    text=((ROOT/d['asset']) if SOURCE_PHASE=='After' else HERE/SOURCE_PHASE/Path(d['asset']).name).read_text();size=w*h
    pins=array(text,'pinnedSpecialTypes',size);types=array(text,'pinnedTileTypes',size)
    d['custom'].setdefault('colorCrateInitialHits',parse_entries(text,'colorCrateInitialHits'))
    slots,filled=spawn(d);rng=random.Random(d['level']);colours={}
    for i,a in enumerate(d['data']['cells']):
        if not a:continue
        x=x0+i%w*cell;y=y0+i//w*cell
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(3,cell//10),fill='#354c65',outline='#65839c')
        o=top[i]
        if o in (14,25,46,53,76):art.paste(im,artwork(o),(x,y,cell,cell))
        if i in filled:
            cs=list(range(4));rng.shuffle(cs)
            if types[i]:cs=[types[i]-1]
            for c in cs:
                if not(i%w>=2 and colours.get(i-1)==colours.get(i-2)==c) and not(i//w>=2 and colours.get(i-w)==colours.get(i-2*w)==c):break
            colours[i]=c
            art.paste(im,art.SPECIALS[pins[i]] if pins[i] else art.TILES[min(3,c)],(x,y,cell,cell),cell*.07)
        elif i in slots:dr.ellipse((x+cell*.45,y+cell*.45,x+cell*.55,y+cell*.55),fill='#8398aa')
        if o in (42,58,69):
            art.paste(im,artwork(42),(x,y,cell,cell))
            if o!=42:art.paste(im,artwork(o),(x,y,cell,cell))
    drawn=set();tarps={p['originCellIndex']:p for p in d['custom']['tarpStatues']}
    for i,o in enumerate(top):
        if not o or o in (14,25,42,46,53,58,69,76):continue
        x=x0+i%w*cell;y=y0+i//w*cell
        if o in (55,56):
            prefix='MetalWallQ_' if o==56 else 'WallQ_'
            for dx,dy in ((-1,-1),(1,-1),(-1,1),(1,1)):
                def same(xx,yy):
                    j=yy*w+xx
                    return 0<=xx<w and 0<=yy<h and top[j]==o and orig[j]==orig[i]
                v=same(i%w,i//w+dy);hh=same(i%w+dx,i//w)
                corner=('T' if dy<0 else 'B')+('L' if dx<0 else 'R')
                name='Outer'+corner if not v and not hh else 'Edge'+('Left' if dx<0 else 'Right') if v and not hh else 'Edge'+('Top' if dy<0 else 'Bottom') if hh and not v else 'Inner'
                try:piece=art.resource('Wall',prefix+name)
                except FileNotFoundError:piece=art.resource('Wall','WallQ_'+name)
                art.paste(im,piece,(x+(cell/2 if dx>0 else 0),y+(cell/2 if dy>0 else 0),cell/2,cell/2))
            continue
        if o in (26,29):
            if o==29:art.paste(im,artwork(o),(x,y,cell,cell))
            continue
        key=(o,orig[i])
        if key in drawn:continue
        drawn.add(key);region=[j for j,v in enumerate(top) if v==o and orig[j]==orig[i]]
        left=min(j%w for j in region);up=min(j//w for j in region);bw=max(j%w for j in region)-left+1;bh=max(j//w for j in region)-up+1
        image=art.tarp_art(tarps[orig[i]]) if o==68 and orig[i] in tarps else artwork(o)
        remaining=next((p['remainingHits'] for p in d['custom'].get('colorCrateInitialHits',[]) if p['originCellIndex']==orig[i] and p['obstacleId']==o),None)
        if remaining is not None:image=art.oid_art(o,remaining)
        art.paste(im,image,(x0+left*cell,y0+up*cell,bw*cell,bh*cell),cell*.02)
        if o==64:
            seal=next(p for p in d['custom']['ancientSeals'] if p['originCellIndex']==orig[i])
            dr.text((x0+(left+bw/2)*cell,y0+(up+bh/2)*cell),'/'.join(str(seal[k]) for k in ('redCount','yellowCount','greenCount')),anchor='mm',font=art.font(max(12,cell//3),True),fill='white',stroke_width=1,stroke_fill='#502a32')
    for p in d['custom']['tubes']:
        i=p['originCellIndex'];dx,dy=((0,-1),(0,1),(-1,0),(1,0))[p['direction']];coords=[(i%w+dx*k,i//w+dy*k) for k in range(p['length'])]
        image=art.tube_art(p['length'],cell)
        if p['direction']:image=image.rotate({1:180,2:90,3:270}[p['direction']],expand=True)
        im.alpha_composite(image,(int(x0+min(x for x,y in coords)*cell),int(y0+min(y for x,y in coords)*cell)))
def goal_label(g):
    if g['targetType']==2:return {1:'Energy',2:'Boss HP',3:'Bird'}.get(g['collectibleId'],'Collectible')
    if g['targetType']==0:return 'Key' if g['tileType']==9 else 'Tile '+str(g['tileType'])
    return LABELS.get(g['obstacleId'],str(g['obstacleId']))
def main():
    global SOURCE_PHASE
    phase=sys.argv[1] if len(sys.argv)>1 else 'After';SOURCE_PHASE=phase
    levels=json.loads((HERE/f'inventory-{phase.lower()}.json').read_text()) if phase in ('Before','Draft') else review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset')
    out=HERE/phase;out.mkdir(exist_ok=True)
    for d in levels:
        if d['level']<=130:continue
        im=Image.new('RGBA',(720,890),'#19283a');dr=ImageDraw.Draw(im)
        dr.text((24,16),f'{d["level"]} · {d["name"]}',font=art.font(25,True),fill='#ffcf75')
        dr.text((24,55),f'{d["width"]}×{d["height"]} · {d["moves"]} moves · {sum(d["data"]["cells"])} active · '+('BOSS' if d['kind'] else 'HARD' if d['level']%10==5 else 'SUPER HARD' if d['level']%10==9 else 'NORMAL'),font=art.font(18),fill='white')
        board(im,d,(28,95),53)
        for index,g in enumerate(d['goals']):dr.text((28,705+index*25),f'{goal_label(g)}: {g["amount"]}',font=art.font(18),fill='white')
        dr.text((28,856),'Static project art · tile colours illustrative · dots: unfilled start candidates',font=art.font(13),fill='#a9bdd1')
        im.convert('RGB').save(out/f'Level{d["level"]}.png')
    for start in range(131,206,10):
        ns=list(range(start,min(start+10,206)));sheet=Image.new('RGB',(1250,650),'#101a29')
        for j,n in enumerate(ns):
            tile=Image.open(out/f'Level{n}.png');tile.thumbnail((250,309));sheet.paste(tile,((j%5)*250,(j//5)*325))
        sheet.save(out/f'Levels{start}-{ns[-1]}.png')
    (out/'art-warnings.json').write_text(json.dumps(sorted(WARNINGS),ensure_ascii=False,indent=2))
    print(phase,'rendered 75 boards, 8 sheets; art warnings:',len(WARNINGS))
if __name__=='__main__':main()
