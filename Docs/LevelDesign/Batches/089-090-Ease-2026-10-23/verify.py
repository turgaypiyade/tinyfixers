"""Static authoring/preservation checks only. No Unity or board/cascade simulation."""
from pathlib import Path
import hashlib, json, re, struct, sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT/'Tools'))
from export_level_previews import int_field, parse_entries

paths = {89: ROOT/'Assets/_Project/Settings/ProductionLevels/LevelP_00991.asset',
         90: ROOT/'Assets/_Project/Settings/ProductionLevels/LevelP_00500.asset'}
def array(text, key, count):
    raw = re.search(r'^  '+key+r': ([0-9a-f]+)$', text, re.M)[1]
    return list(struct.unpack(f'<{count}i', bytes.fromhex(raw)))

report = {}
for number, path in paths.items():
    old, new = (HERE/'Before'/path.name).read_text(), path.read_text()
    assert int_field(old,'moves') == 28 and int_field(new,'moves') == 33
    if number == 90:
        assert new == old.replace('  moves: 28\n','  moves: 33\n',1)
        assert int_field(new,'height') == 10 and int_field(new,'levelKind') == 1
    else:
        before, cells = array(old,'cells',81), array(new,'cells',81)
        added = [i for i,(a,b) in enumerate(zip(before,cells)) if a != b]
        assert added == [31,32,33,34,35] and all(cells[i] == 1 for i in added)
        def normalize(text):
            return re.sub(r'^  cells: [0-9a-f]+$','  cells: MASK',text,flags=re.M).replace(
                '  moves: 33\n','  moves: 28\n',1).replace('    amount: 46\n','    amount: 41\n',1)
        assert normalize(new) == normalize(old)
        goals = parse_entries(new,'goals')
        assert goals[0]['obstacleId'] == 46 and goals[0]['amount'] == sum(cells) == 46
        assert int_field(new,'width') == int_field(new,'height') == 9
        obs, origins = array(new,'obstacles',81), array(new,'obstacleOrigins',81)
        for i, oid in enumerate(obs):
            assert (not oid and origins[i] == -1) or (oid == 46 and origins[i] == i and cells[i])
        for key in ('wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes'):
            assert len(array(new,key,81)) == 81
        assert all(cells[e['originCellIndex']] for e in parse_entries(new,'stackedObstacles'))
        safe = parse_entries(new,'safes')[0]
        assert all(cells[(safe['originCellIndex']//9+y)*9+safe['originCellIndex']%9+x]
                   for y in range(safe['height']) for x in range(safe['width']))
        assert all(obs[i] == 0 and origins[i] == -1 for i in added)
    report[number] = {'asset': str(path.relative_to(ROOT)), 'movesBefore':28, 'movesAfter':33,
                      'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}

protected = json.loads((HERE/'protected-before.json').read_text())
changed = [p for p,h in protected.items() if hashlib.sha256((ROOT/p).read_bytes()).hexdigest() != h]
assert set(changed) == {str(p.relative_to(ROOT)) for p in paths.values()}
report['preservation'] = {'protectedFiles': len(protected), 'changed': changed,
                          'otherFilesUnchanged':len(protected)-2}
report['limits'] = 'Static mask/field/layer/preservation checks; no Unity Play/build or bot/cascade simulation.'
(HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(f'PASS: requested scopes, 89 gel/mask/layers, 90 moves only; {len(protected)-2} other protected files unchanged.')
