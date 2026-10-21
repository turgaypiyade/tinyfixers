#!/usr/bin/env python3
"""Inspect this batch's rollback; mutate only with an explicit --apply argument.

Refuse later catalog edits or changes to owned files instead of deleting user work.
The snapshots, manifest and this script remain as an archive after rollback.
"""
import argparse
import hashlib
import json
from pathlib import Path


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true', help='Restore catalog/state and remove unchanged new batch files')
    args = parser.parse_args()
    batch = Path(__file__).resolve().parent
    root = batch.parents[3]
    manifest = json.loads((batch/'manifest.json').read_text())

    def resolve(relative):
        path = root/relative
        if Path(relative).is_absolute() or not path.resolve().is_relative_to(root):
            raise SystemExit('Unsafe manifest path: '+relative)
        return path

    catalog = resolve(manifest['catalogPath'])
    state = resolve(manifest['statePath'])
    before_catalog = batch/'catalog-before.txt'
    before_state = batch/'design-state-before.txt'
    for path, expected in ((catalog,manifest['catalogAfterSha256']),
                           (state,manifest['stateAfterSha256']),
                           (before_catalog,manifest['catalogBeforeSha256']),
                           (before_state,manifest['stateBeforeSha256'])):
        if not path.is_file() or sha(path)!=expected:
            raise SystemExit('Rollback refused; file changed: '+str(path))
    owned = [resolve(p) for p in manifest['ownedFiles']]
    for relative, expected in manifest['ownedFiles'].items():
        path = resolve(relative)
        if not path.is_file() or sha(path)!=expected:
            raise SystemExit('Rollback refused; new file changed/missing: '+relative)
        if path in (catalog,state):
            raise SystemExit('Invalid owned file: '+relative)
    if not args.apply:
        print(f'DRY RUN: catalog and state can be restored; {len(owned)} unchanged batch files can be removed.')
        print('No files changed. --apply performs the reviewed rollback.')
        return
    catalog.write_bytes(before_catalog.read_bytes())
    state.write_bytes(before_state.read_bytes())
    for path in owned:
        path.unlink()
    print(f'Restored catalog/state and removed {len(owned)} batch files. Snapshots remain in {batch}.')


if __name__=='__main__':
    main()
