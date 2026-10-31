"""Author only starting colors/steps for the relocated V1 tutorials. No gameplay simulation."""
from pathlib import Path
from collections import Counter
import sys,re,struct,json,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];sys.path.insert(0,str(ROOT/'Tools'))
from export_level_previews import int_array,int_field,parse_entries
from check_levels161_162 import stage

def authored_patterns(types,w,h):
    for y in range(h):
        for x in range(w):
            i=y*w+x
            if x+2<w:yield (i,i+1,i+2)
            if y+2<h:yield (i,i+w,i+2*w)
            if x+1<w and y+1<h:yield (i,i+1,i+w,i+w+1)

def no_initial_match(types,w,h):
    return all(not types[cells[0]] or not all(types[i]==types[cells[0]] for i in cells) for cells in authored_patterns(types,w,h))

def fill(w,h,free,predefined):
    types=[0]*(w*h)
    for i,color in predefined.items():assert i in free;types[i]=color
    assert no_initial_match(types,w,h)
    for i in sorted(free):
        if types[i]:continue
        palette=[1+(i%w+2*(i//w)+k)%4 for k in range(4)]
        for color in palette:
            types[i]=color
            if no_initial_match(types,w,h):break
        else:raise AssertionError(('No valid starting color',i))
    assert no_initial_match(types,w,h)
    return types

def replace_entries(text,key,rows):
    block=f'  {key}: []\n' if not rows else f'  {key}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+k+': '+str(v)+'\n' for j,(k,v) in enumerate(row.items())) for row in rows)
    pattern=r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*'
    text,n=re.subn(pattern,lambda _:block,text,flags=re.M);assert n==1
    return text

def main():
    manifest=[]
    for n in (8,9,19,22):
        p=ROOT/f'Assets/_Project/Settings/ProLevels/LevelV1_{n*10:08d}.asset'
        text=p.read_text();assert text==(HERE/'Before'/p.name).read_text(),'Current asset changed since snapshot.'
        w=int_field(text,'width');h=int_field(text,'height');size=w*h
        if n in (8,9):
            cells=int_array(text,'cells',size);top=int_array(text,'obstacles',size)
            for s in parse_entries(text,'stackedObstacles'):top[s['originCellIndex']]=s['obstacleId']
            free={i for i,a in enumerate(cells) if a and (not top[i] or not stage(top[i],'blocksCells'))}
            # Rainbow: one vertical swap makes exactly five horizontal yellows.
            predefined={32:1,39:1,40:1,41:2,42:1,43:1} if n==8 else {30:2,39:3}
            pins=fill(w,h,free,predefined);specials=[0]*size
            if n==8:steps=[dict(id=5,fromCell=32,toCell=41),dict(id=9,fromCell=-1,toCell=-1)]
            else:
                specials[30]=1;specials[39]=2
                steps=[dict(id=6,fromCell=30,toCell=39)]
            for key,values in (('pinnedTileTypes',pins),('pinnedSpecialTypes',specials)):
                blob=struct.pack('<'+str(size)+'i',*values).hex()
                text,count=re.subn(r'^  '+key+r':[^\n]*',lambda _:'  '+key+': '+blob,text,flags=re.M);assert count==1
            text=replace_entries(text,'tutorialSteps',steps)
        else:text=replace_entries(text,'tutorialSteps',[])
        p.write_text(text)
        manifest.append(dict(level=n,asset=str(p.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    (HERE/'manifest.json').write_text(json.dumps(dict(changed=manifest,scope='Tutorial steps and starting tiles only; all obstacles, masks, goals, moves, names and catalog preserved.'),indent=2)+'\n')
    print('Moved rainbow tutorial22→8 and first combo19→9; preserved all other level fields.')

if __name__=='__main__':main()
