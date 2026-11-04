"""Static reference/preservation audit for the two missing boss icons; no Unity build."""
from pathlib import Path
import ast,hashlib,json,re,sys
from PIL import Image
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
ROOT=next(p for p in HERE.parents if(p/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').exists())
sys.path.insert(0,str(ROOT/'Tools'))
import review_level_patterns as review
def main():
    manifest=json.loads((HERE/'manifest.json').read_text());icon=manifest['icon']
    for row in manifest['changes']:
        path=ROOT/row['asset'];old=(HERE/path.name).read_text();current=path.read_text()
        pattern=r'(^  - targetType: 2\n    tileType: \d+\n    obstacleId: \d+\n    collectibleId: 2\n    iconOverride: )\{fileID: 0\}'
        expected,count=re.subn(pattern,lambda m:m[1]+icon,old,flags=re.M)
        assert count==1 and current==expected,'Only the BossDamage icon may change'
    protected=json.loads((HERE/'protected-files.json').read_text())
    assert all((ROOT/p).is_file() and hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==digest for p,digest in protected.items())
    levels=review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset');bosses=[]
    for level in levels:
        if not level['kind']:continue
        text=(ROOT/level['asset']).read_text()
        goals=re.search(r'^  goals:\n(.*?)(?=^  levelKind:)',text,re.M|re.S)[1]
        refs=[re.search(r'^    iconOverride: (.*)$',g,re.M)[1] for g in re.split(r'(?=^  - targetType:)',goals,flags=re.M) if re.search(r'^    collectibleId: 2$',g,re.M)]
        assert refs==[icon],(level['level'],refs)
        bosses.append(level['level'])
    assert len(bosses)==22
    sprite=ROOT/'Assets/_Project/Art/UI/WonderCharacters/Badger/BadgerIdle.png'
    guid=re.search(r'guid: (\w+)',icon)[1]
    assert f'guid: {guid}\n' in Path(str(sprite)+'.meta').read_text()
    assert Image.open(sprite).convert('RGBA').getchannel('A').getbbox()
    for rel in ('Assets/_Project/Scripts/UI/TopHUD/TopHudController.cs','Assets/_Project/Scripts/UI/PreLevelSpecialPopupController.cs'):
        assert re.search(r'if \(goal.iconOverride != null\)\s+return goal.iconOverride;',(ROOT/rel).read_text())
    editor=(ROOT/'Assets/_Project/Scripts/Editor/LevelDataEditor.cs').read_text();assert 'ObjectField("Icon Override", goal.iconOverride, typeof(Sprite), false)' in editor
    for rel in ('Docs/LevelDesign/Batches/206-210-Windmill/design2.py','Docs/LevelDesign/Batches/213-220-Showcase/design.py','Docs/LevelDesign/Batches/213-220-Showcase/render.py'):
        ast.parse((ROOT/rel).read_text())
    model=next(d for d in json.loads((BATCH/'designs.json').read_text()) if d['level']==220)
    assert next(g['iconOverride'] for g in model['goals'] if g['targetType']==2 and g['collectibleId']==2)==icon
    report=dict(result='PASS',bossLevels=bosses,fixedLevels=[210,220],onlyBossIconChanged=True,sprite=str(sprite.relative_to(ROOT)),protectedFiles=len(protected),hudAndPrelevelUseAssignedOverride=True,unityBuildOrPlayRun=False)
    (BATCH/'validation.json').write_text(json.dumps(report,indent=2)+'\n');(HERE/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PASS: all22 active boss levels have valid icons;210/220 differ only by BossDamage icon;',len(protected),'other level/meta/catalog/name files preserved.')
if __name__=='__main__':main()
