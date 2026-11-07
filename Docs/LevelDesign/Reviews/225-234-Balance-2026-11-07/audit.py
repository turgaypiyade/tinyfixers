"""Read-only asset/export checks for this review; no authoring or Unity calls."""
from pathlib import Path
import hashlib, json, math, re, subprocess, struct

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]


def yaml_data(path):
    # Unity packed arrays must stay strings even when the hex contains only digits.
    ruby = r'''require 'yaml'; require 'json'
text = File.read(ARGV[0]).lines.reject { |l| l.start_with?('%') }.join.sub(/^---.*$/, '---')
text = text.gsub(/^(\s+\w+:) ([0-9a-f]{8,})$/) { "#{$1} '#{$2}'" }
puts JSON.generate(YAML.safe_load(text)['MonoBehaviour'])'''
    return json.loads(subprocess.check_output(['ruby', '-e', ruby, str(path)], text=True))


def differences(actual, exported, trail=''):
    if isinstance(actual, dict):
        if 'fileID' in actual:  # Object references intentionally omitted by exporter.
            return []
        return [x for k, v in actual.items() if k in exported
                for x in differences(v, exported[k], trail + '.' + k)]
    if isinstance(exported, list):
        if isinstance(actual, str) and re.fullmatch('[0-9a-f]+', actual):
            raw = bytes.fromhex(actual)
            actual = list(struct.unpack(f'<{len(raw)//4}i', raw)) if len(raw) == len(exported)*4 else list(raw)
        if actual is None:
            actual = []
        if len(actual) != len(exported):
            return [trail + ': length mismatch']
        return [x for i, (a, b) in enumerate(zip(actual, exported))
                for x in differences(a, b, trail + f'[{i}]')]
    if isinstance(actual, (int, float)) and isinstance(exported, (int, float)):
        return [] if math.isclose(actual, exported, rel_tol=1e-6, abs_tol=1e-6) else [trail]
    return [] if actual == exported else [trail]


def main():
    manifest = json.loads((HERE/'before-manifest.json').read_text())
    results = []
    for entry in manifest:
        n = entry['level']
        path = ROOT/entry['asset']
        d = json.loads((HERE/f'L{n:04d}.json').read_text())
        mismatch = differences(yaml_data(path), d)
        assert not mismatch, (n, mismatch)
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], (n, 'asset changed during review')
        size = d['width']*d['height']
        for key in ('cells', 'obstacles', 'obstacleOrigins', 'wallPieceIds',
                    'lampColorByCell', 'hamsterSatietyByCell', 'pinnedTileTypes', 'pinnedSpecialTypes'):
            assert len(d[key]) == size, (n, key)
        assert 0 < len(d['goals']) <= 5
        assert all(g['amount'] > 0 for g in d['goals'])
        for key in ('dimerdals', 'fruits', 'safes', 'ancientSeals', 'stackedObstacles'):
            for obj in d[key]:
                origin = obj['originCellIndex']
                assert 0 <= origin < size and d['cells'][origin], (n, key, origin)
        sources = sum(s['count'] for s in d['dimerdals'])
        # Collectible enum is recorded explicitly; compare source quota against the
        # named goal in runtime reports as well, rather than guessing enum values.
        if sources:
            candidates = [g['amount'] for g in d['goals'] if g['targetType'] == 2]
            assert sources in candidates, (n, sources, candidates)
        if n == 230:
            assert d['levelKind'] == 1 and d['height'] <= 10
            goal_ids = {g['obstacleId'] for g in d['goals'] if g['targetType'] == 1}
            assert not goal_ids.intersection(d['bossThrownObstacles'] + d['bossToolThreatTypes'])
        results.append(dict(level=n, unchanged=True, exportMatchesAllSerializedFields=True,
                            moves=d['moves'], activeCells=sum(d['cells']), goals=len(d['goals']),
                            dimerdalQuota=sources, fruitCount=len(d['fruits']),
                            ordinaryPins=sum(bool(x) for x in d['pinnedTileTypes']),
                            specialPins=sum(bool(x) for x in d['pinnedSpecialTypes'])))
    (HERE/'asset-checks.json').write_text(json.dumps(results, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(results, ensure_ascii=False))


if __name__ == '__main__':
    main()
