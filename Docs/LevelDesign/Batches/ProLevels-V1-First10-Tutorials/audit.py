"""Static tutorial move/pin audit; never runs Unity, gravity, cascades or a bot."""
from pathlib import Path
import sys,re,json,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];sys.path.insert(0,str(ROOT/'Tools'))
from export_level_previews import int_field,int_array,parse_entries
from check_levels161_162 import stage
from author import no_initial_match

EXPECTED={1:[0],3:[1,3],5:[4,7],7:[2,8],8:[5,9],9:[6]}
RESULT={1:0,3:2,5:4,7:3,8:5,9:0}

def load(n):
    p=ROOT/f'Assets/_Project/Settings/ProLevels/LevelV1_{n*10:08d}.asset';t=p.read_text()
    w,h=int_field(t,'width'),int_field(t,'height');size=w*h
    mask=int_array(t,'cells',size);top=int_array(t,'obstacles',size)
    for s in parse_entries(t,'stackedObstacles'):top[s['originCellIndex']]=s['obstacleId']
    return dict(level=n,width=w,height=h,moves=int_field(t,'moves'),mask=mask,top=top,
        tiles=int_array(t,'pinnedTileTypes',size),specials=int_array(t,'pinnedSpecialTypes',size),steps=parse_entries(t,'tutorialSteps'),expected=RESULT.get(n,0))

def strip(text,keys):
    for key in keys:text=re.sub(r'^  '+key+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*','',text,flags=re.M)
    return text

def run():
    early=[load(n) for n in range(1,11)];fixtures=[]
    for d in early:
        n=d['level'];steps=d['steps']
        assert [s['id'] for s in steps]==EXPECTED.get(n,[]),(n,steps)
        if not steps:continue
        w,h=d['width'],d['height'];free={i for i,a in enumerate(d['mask']) if a and (not d['top'][i] or not stage(d['top'][i],'blocksCells'))}
        assert {i for i,v in enumerate(d['tiles']) if v}==free,'All opening tutorial colors are explicit.'
        assert all(d['tiles'][i] in (1,2,3,4) for i in free)
        assert no_initial_match(d['tiles'],w,h),(n,'opening automatic match')
        assert all(d['specials'][i]==0 for i in set(range(w*h))-free)
        # Fully seeded visible board: no empty slot for initial settling to move a
        # colored pattern or the pair of prepared rockets away from its coordinates.
        assert all(d['tiles'][i] for i in free)
        for s in steps:
            if s['fromCell']<0:continue
            a,b=s['fromCell'],s['toCell'];assert a in free and b in free
            assert abs(a%w-b%w)+abs(a//w-b//w)==1
        if n==8:assert not any(d['specials']) and steps[0]==dict(id=5,fromCell=32,toCell=41)
        if n==9:assert {i:s for i,s in enumerate(d['specials']) if s}=={30:1,39:2}
        fixtures.append(d)
    assert sorted(s['id'] for d in early for s in d['steps'])==list(range(10))
    assert load(19)['steps']==[] and load(22)['steps']==[]
    for n in (8,9,19,22):
        p=ROOT/f'Assets/_Project/Settings/ProLevels/LevelV1_{n*10:08d}.asset';old=(HERE/'Before'/p.name).read_text();new=p.read_text()
        keys=['tutorialSteps']+(['pinnedTileTypes','pinnedSpecialTypes'] if n in (8,9) else [])
        assert strip(old,keys)==strip(new,keys),'Unrequested obstacle/mask/goal/move/name/library change.'
    protected=json.loads((HERE/'protected-before.json').read_text())
    assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in protected.items())
    # Existing hints are driven by actual obstacle IDs, not old level numbers.
    first={}
    for d in early:
        for oid in d['top']:
            if oid:first.setdefault(oid,d['level'])
    assert first=={4:1,5:2,6:4,25:6}
    loc=json.loads((ROOT/'Assets/_Project/Resources/Localization/tinyfixers_localization.json').read_text())
    keys=['tutorial_basic_match_desc','tutorial_emitter_created_desc','tutorial_rocket_v_activated_desc',
        'tutorial_patchbot_created_desc','tutorial_pulsecore_created_desc','tutorial_override_created_desc',
        'tutorial_combo_desc','tutorial_pulsecore_activated_desc','tutorial_patchbot_activated_desc','tutorial_override_activated_desc']
    for code in ('tr','en'):
        entries={e['key']:e['value'] for lang in loc['languages'] if lang['code']==code for e in lang['entries']}
        assert all(entries.get(k) for k in keys)
    (HERE/'tutorial-fixtures.json').write_text(json.dumps(fixtures,indent=2)+'\n')
    summary=dict(schedule={n:[s['id'] for s in load(n)['steps']] for n in range(1,11)},
        obstacleFirstUses=first,preservedFiles=len(protected),allTenBasicStepsByLevel9=True,
        legacyTutorialsRemoved=[19,22],runtimeChange='Activation resolves the taught special kind; no extra board scan or Update loop.',
        limits='Static colors/geometry and isolated production method checks; no Unity Play/build, gravity/cascade/bot simulation or measured balance.')
    (HERE/'validation.json').write_text(json.dumps(summary,indent=2)+'\n')
    print('PASS: all10 basic tutorial steps within first9 levels, opening colors/support, move adjacency, TR/EN, unchanged goals/masks/moves and',len(protected),'protected files.')

if __name__=='__main__':run()
