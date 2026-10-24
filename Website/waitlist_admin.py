#!/usr/bin/env python3
"""Private launch-list administration. Secrets and exports stay in ignored .wrangler/.

python3 Website/waitlist_admin.py export
python3 Website/waitlist_admin.py remove --email address@example.com
No email is sent by this tool.
"""
import argparse
import datetime
import json
import os
from pathlib import Path
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parent


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('export', 'remove'))
    parser.add_argument('--email')
    args = parser.parse_args()
    if args.command == 'remove' and not args.email:
        parser.error('remove requires --email')
    secret = (ROOT / '.wrangler/waitlist-admin.env').read_text().strip().split('=', 1)[1]
    data = json.dumps({'email': args.email}).encode() if args.command == 'remove' else None
    request = urllib.request.Request('https://www.wondigames.com/api/launch-list/' + args.command,
                                    data=data, headers={'Authorization': 'Bearer ' + secret,
                                    'Content-Type': 'application/json', 'User-Agent': 'WondiGames-WaitlistAdmin/1.0'})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            result = json.load(response)
    except urllib.error.HTTPError as error:
        raise SystemExit(f'HTTP {error.code}; no data printed.')
    if args.command == 'export':
        directory = ROOT / '.wrangler/waitlist-exports'
        directory.mkdir(parents=True, exist_ok=True)
        os.chmod(directory, 0o700)
        path = directory / (datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.json')
        path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
        os.chmod(path, 0o600)
        print(f"Exported {len(result.get('entries', []))} registrations to {path}; no emails printed or sent.")
    else:
        if not result.get('ok'):
            raise SystemExit('Removal failed.')
        print('Registration removed. No email sent.')


if __name__ == '__main__':
    main()
