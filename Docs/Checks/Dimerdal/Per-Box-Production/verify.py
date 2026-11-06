"""Audit per-box data migration and unrelated-file preservation, without builds/simulation."""
from pathlib import Path
import hashlib,json,re,struct,subprocess
HERE=Path(__file__).resolve().parent
ROOT=next(p for p in HERE.parents if (p/'Assets/_Project').is_dir())

def main():
    sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
    levels=json.loads((HERE/'levels.json').read_text())
    protected=json.loads((HERE/'protected-before.json').read_text())
    changed=[rel for rel,h in protected.items() if not(ROOT/rel).exists() or sha(ROOT/rel)!=h]
    assert not changed,('Unrelated changes since per-box work began',changed)
    code_manifest=HERE/'owned-code-hashes.json'
    if code_manifest.exists():
        for rel,h in json.loads(code_manifest.read_text()).items():
            assert sha(ROOT/rel)==h,('Source changed since validated version; review required',rel)
    for e in levels:
        text=(ROOT/e['asset']).read_text();before=(HERE/'Before'/e['asset']).read_text()
        block='  dimerdals:\n'+''.join(f'  - originCellIndex: {d["originCellIndex"]}\n    obstacleId: {d["obstacleId"]}\n    count: {int(d["count"])}\n' for d in e['dimerdals'])
        assert text==before+('' if before.endswith('\n') else '\n')+block,'Only the new per-box settings may be added.'
        def array(k):
            raw=re.search(r'^  '+k+r': ([0-9a-f]+)$',text,re.M)[1]
            return struct.unpack('<'+'i'*(len(raw)//8),bytes.fromhex(raw))
        ids,origins=array('obstacles'),array('obstacleOrigins')
        for d in e['dimerdals']:
            assert d['count']>0 and ids[d['originCellIndex']]==d['obstacleId'] and origins[d['originCellIndex']]==d['originCellIndex']
        total=int(re.search(r'    collectibleId: 4\n    iconOverride: [^\n]+\n    amount: (\d+)',text)[1])
        assert sum(d['count'] for d in e['dimerdals'])==total
    ruby="ARGV.each{|p| t=File.readlines(p).reject{|l|l.start_with?('%','---')}.join;YAML.safe_load(t,aliases:false)}"
    subprocess.run(['ruby','-ryaml','-e',ruby,*[str(ROOT/e['asset']) for e in levels]],check=True)
    data=(ROOT/'Assets/_Project/Scripts/Core/LevelData.cs').read_text()
    assert '[System.Serializable]\npublic struct DimerdalEntry' in data and 'public DimerdalEntry[] dimerdals;' in data
    clone=(ROOT/'Assets/_Project/Scripts/Grid/GridSpawner.cs').read_text()
    assert 'clone.SynchronizeDimerdalGoals();' in clone
    editor=(ROOT/'Assets/_Project/Scripts/Editor/LevelDataEditor.cs').read_text()
    for token in ['Üreteceği elmas','Tahtadaki Dimerdal kutuları','new EditorGUI.DisabledScope(automaticDiamond)','level.SetDimerdalProductionCount','level.SynchronizeDimerdalGoals()']:
        assert token in editor
    result=dict(result='PASS',protectedFiles=len(protected),unrelatedChanges=[],levels={str(e['level']):[int(d['count']) for d in e['dimerdals']] for e in levels},
        preserved=['all level fields except appended source settings','goal amounts and purple icon','catalog/names/meta','other levels including current user edits','art assets','fall/gravity code'],
        limits='YAML/data/source-path audit; actual C# tests use Unity doubles. No Unity build/Play/device, gameplay simulation, publishing or CPU/GC/FPS measurement.')
    (HERE/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print('PASS:',len(protected),'unrelated files preserved; four levels add per-box counts only; goals/layouts/moves unchanged; serialized data and editor/runtime paths checked.')

if __name__=='__main__':main()
