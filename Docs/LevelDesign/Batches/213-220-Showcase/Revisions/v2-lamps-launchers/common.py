"""Authored layer planes + static top/down refill reachability; never advances gameplay."""
from pathlib import Path
from collections import deque
import copy,importlib.util,re,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import DEFS,stage,footprint,array
import review_level_patterns as review

def layer_model(d,removed=()):
    removed=set(removed);w=d['width'];layers=[[] for _ in d['data']['cells']]
    def add(i,o,origin):
        if o==58:
            if 42 not in removed:layers[i].append((42,origin))
            if 58 not in removed and 42 not in removed:layers[i].append((58,origin))
        elif o and o not in removed:layers[i].append((o,origin))
    for i,o in enumerate(d['data']['obstacles']):add(i,o,d['data']['obstacleOrigins'][i])
    for path in d['custom']['waterChannels']:
        for i in path['pathCellIndices'][1:]:
            if 76 not in removed:layers[i].insert(0,(76,i))
    for s in sorted(d['custom']['stackedObstacles'],key=lambda e:e['stackOrder']):
        origin=s['originCellIndex'];fw,fh=footprint(s['obstacleId'])
        for dy in range(fh):
            for dx in range(fw):add(origin+dy*w+dx,s['obstacleId'],origin)
    return layers
def feed(d,removed=()):
    w,h=d['width'],d['height'];ls=layer_model(d,removed);active={i for i,c in enumerate(d['data']['cells']) if c}
    sources={p['pathCellIndices'][0] for p in d['custom']['waterChannels']}
    blockers={i:[o for o,_ in cell if stage(o,'blocksCells') or stage(o,'behavior')==3] for i,cell in enumerate(ls)}
    blocked={i for i in active if blockers[i]};slots=active-blocked-sources;reached=set()
    for x in range(w):
        for y in range(h):
            i=y*w+x
            if i in blocked:break
            if i in slots:reached.add(i)
    q=deque(reached)
    def corner(i):return i not in blocked or all(stage(o,'allowDiagonal') for o in blockers[i])
    while q:
        i=q.popleft();x,y=i%w,i//w
        if y+1==h:continue
        for dx in (0,-1,1):
            j=i+w+dx
            if not 0<=x+dx<w or j not in slots or j in reached:continue
            if dx and not(corner(i+dx) or corner(i+w)):continue
            reached.add(j);q.append(j)
    for i in slots-reached:
        above=[o for j in range(i%w,i,w) for o in blockers[j]]
        flags=[bool(re.search(r'^    fillsShadowBeneath: 1$',DEFS[o],re.M)) for o in above]
        if flags and all(flags):reached.add(i)
        elif any(stage(o,'locksInteraction') for o,_ in ls[i]):reached.add(i)
    return slots,reached
def phase(d,removed):
    q=copy.deepcopy(d);q['_removed']=set(removed);ls=layer_model(q,removed)
    q['top']=[s[-1][0] if s else 0 for s in ls];q['topOrigins']=[s[-1][1] if s else -1 for s in ls]
    return q
def load_art():
    spec=importlib.util.spec_from_file_location('showcase_renderer',HERE.parent/'131-205-Review-2026-10-31/render.py')
    r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r);return r
