"""Reuse full v2 topology/goal audit against the new scope, plus strict move-only comparison."""
from pathlib import Path
import importlib.util,json,re
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
ROOT=next(p for p in HERE.parents if(p/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').exists())
def main():
    revision=json.loads((HERE/'revision.json').read_text())
    for row in revision['levels']:
        path=ROOT/f'Assets/_Project/Settings/ProLevels/LevelV1_{row["level"]*10:08d}.asset';old=(HERE/path.name).read_text();current=path.read_text()
        assert re.sub(r'^  moves:.*$','',old,flags=re.M)==re.sub(r'^  moves:.*$','',current,flags=re.M)
        assert int(re.search(r'^  moves: (\d+)$',current,re.M)[1])==row['after']
    spec=importlib.util.spec_from_file_location('v2_shape_audit',BATCH/'Revisions/v2-lamps-launchers/verify_revision.py');audit=importlib.util.module_from_spec(spec);spec.loader.exec_module(audit)
    audit.HERE=HERE;audit.REVISION='v3-moves-2026-11-04';audit.main()
    print('PASS: eight exact move-only edits; masks/layers/goals/pins/colours/names/catalog/GUID unchanged.')
if __name__=='__main__':main()
