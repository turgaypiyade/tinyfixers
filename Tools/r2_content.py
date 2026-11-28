#!/usr/bin/env python3
"""R2 content publisher. No Unity build, release baseline, billing change, or embedded credentials.
All remote mutations require an explicit --apply. plan/verify never upload.
"""
import argparse
import glob
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.error
import urllib.parse
import urllib.request

from level_release import credentials, ReleaseError

ROOT = Path(__file__).resolve().parents[1]


class CloudflareError(ValueError):
    def __init__(self, status):
        self.status = status
        super().__init__(f'Cloudflare HTTP {status}; R2 etkinliğini ve token yetkilerini kontrol et.')


def digest(path):
    with path.open('rb') as stream:
        value = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
        return value.hexdigest()


def safe_command_error(result, secrets):
    """Keep actionable CLI diagnostics while stripping credentials and account IDs."""
    output = (result.stderr or result.stdout or '').strip()
    for secret in secrets:
        if secret:
            output = output.replace(secret, '[redacted]')
    output = re.sub(r'(?i)(bearer\s+)[^\s\"\']+', r'\1[redacted]', output)
    lines = [line.strip() for line in output.splitlines() if line.strip()]
    return '\n'.join(lines[-8:])[:1600] or 'Wrangler ayrıntı vermeden başarısız oldu.'


def read_plan(directory):
    directory = Path(directory)
    manifest_path = directory / 'manifest.json'
    manifest = json.loads(manifest_path.read_text())
    plan = json.loads((directory / 'plan.json').read_text())
    if digest(manifest_path) != plan['manifestSha256']:
        raise ValueError('Manifest değişmiş; Unity içerik paketlerini yeniden hazırla.')
    url = urllib.parse.urlsplit(manifest['baseUrl'])
    if url.scheme != 'https' or not url.hostname or url.username or url.password or url.query or url.fragment:
        raise ValueError('Geçerli bir HTTPS içerik adresi gerekli.')
    if manifest.get('schema') not in (2, 3) or manifest.get('platform') not in ('Android', 'iOS'):
        raise ValueError('Uyumsuz içerik şeması/platform.')
    if url.path != '/' + manifest['platform']:
        raise ValueError('İçerik adresi /Android veya /iOS ile bitmeli.')
    if manifest.get('bundledLevels') != 50 or manifest.get('packLevels') != 50:
        raise ValueError('Başlangıç/paket sınırı 50 olmalı.')
    ids = set()
    for bundle in manifest['bundles']:
        name = bundle['file']
        if not re.fullmatch(r'[0-9a-f]{64}\.bundle', name) or name != bundle['sha256'] + '.bundle':
            raise ValueError('Geçersiz paket yolu/hash.')
        if bundle['id'] in ids or not isinstance(bundle['bytes'], int) or bundle['bytes'] <= 0:
            raise ValueError('Geçersiz paket boyutu/kimliği.')
        ids.add(bundle['id'])
        path = directory / 'publish' / name
        if path.stat().st_size != bundle['bytes'] or digest(path) != bundle['sha256']:
            raise ValueError('Yerel paket doğrulanamadı: ' + name)
        if bundle['bytes'] > 300 * 1024 * 1024:
            raise ValueError('Wrangler yükleme sınırı için paket 300 MiB altında olmalı; paketleri böl.')
    def validate_assets(pack):
        if not set(pack['bundles']) <= ids:
            raise ValueError('Eksik paket bağımlılığı.')
        if any(a['bundle'] not in pack['bundles'] for a in pack['assets']):
            raise ValueError('Asset bağımlılığı eksik.')

    previous = 50
    level_bundles = set()
    guids = {}
    for pack in manifest['packs']:
        if pack.get('cityId') or pack['firstLevel'] != previous + 1 or not pack['firstLevel'] <= pack['lastLevel'] <= previous + 50:
            raise ValueError('Paket seviye aralıklarında boşluk/çakışma var.')
        validate_assets(pack)
        level_bundles.update(pack['bundles'])
        for asset in pack['assets']:
            address = (asset['bundle'], asset['path'])
            if not asset.get('guid') or not asset.get('path') or guids.get(asset['guid'], address) != address:
                raise ValueError('Geçersiz/çakışan asset kimliği.')
            guids[asset['guid']] = address
        previous = pack['lastLevel']
    city_ids, city_bundles = set(), set()
    cities = manifest.get('cities')
    if not isinstance(cities, list):
        raise ValueError('Şehir paket listesi eksik.')
    for city in cities:
        city_id = city.get('cityId', '')
        if not re.fullmatch(r'[a-z0-9_-]+', city_id) or city_id in city_ids:
            raise ValueError('Geçersiz/tekrarlı şehir kimliği.')
        city_ids.add(city_id)
        if city.get('firstLevel') != 0 or city.get('lastLevel') != 0 or len(city['bundles']) != 1:
            raise ValueError('Şehir paketi seviyelerden bağımsız olmalı.')
        validate_assets(city)
        bundle = city['bundles'][0]
        if bundle in city_bundles or bundle in level_bundles:
            raise ValueError('Şehir paketi başka tüketiciyle paylaşılamaz.')
        city_bundles.add(bundle)
        expected = {'WorldTour/City_' + city_id, 'WorldTour/City_' + city_id + '_Site'}
        if len(city['assets']) != 2 or {a.get('resourcePath') for a in city['assets']} != expected:
            raise ValueError('Şehrin iki panorama sürümü gerekli.')
        for asset in city['assets']:
            if not asset.get('guid') or asset['guid'] in guids or not asset.get('path'):
                raise ValueError('Şehir asset kimliği eksik veya paylaşılmış.')
            guids[asset['guid']] = (bundle, asset['path'])
    event_ids = set()
    unlock_fields = {
        'safari': 'minLevelGate',
        'bridge-repair': 'minLevelGate',
        'harvest': 'minLevelGate',
        'gift-festival': 'unlockAfterLevel',
        'gear-cup': 'minLevelGate',
        'claw-machine': 'unlockLevel',
        'mission-map': 'on-demand',
    }
    events = manifest.get('events', [])
    if not isinstance(events, list) or (manifest['schema'] == 2 and events):
        raise ValueError('Geçersiz event paket listesi/şeması.')
    for event in events:
        event_id = event.get('eventId')
        mission = isinstance(event_id, str) and re.fullmatch(r'mission-[a-z0-9_-]+', event_id)
        if (event_id not in unlock_fields and not mission) or event_id in event_ids or not event.get('configGuid'):
            raise ValueError('Geçersiz/tekrarlı event kimliği veya eksik config kaynağı.')
        expected_unlock = 'on-demand' if mission else unlock_fields[event_id]
        if event.get('unlockField') != expected_unlock:
            raise ValueError('Event kapı config alanı eşleşmiyor.')
        event_ids.add(event_id)
        if event.get('cityId') or event.get('firstLevel') != 0 or event.get('lastLevel') != 0:
            raise ValueError('Event paketleri şehir/seviye aralıklarından bağımsız olmalı.')
        validate_assets(event)
        if not event['bundles'] or not event['assets']:
            raise ValueError('Boş event paketi.')
        if set(event['bundles']) & (level_bundles | city_bundles) or not set(event['bundles']) <= {'event-' + event_id, 'event-shared'}:
            raise ValueError('Event paketi yalnız kendi paketi ve event-shared bağımlılığını kullanabilir.')
        keys = set()
        for asset in event['assets']:
            key = asset.get('guid', '')
            if not re.fullmatch(r'[0-9a-f]{32}:-?[0-9]+', key) or key in keys or key.split(':')[0] != asset.get('sourceGuid'):
                raise ValueError('Geçersiz/tekrarlı event alt-asset kimliği.')
            keys.add(key)
            if not asset.get('path') or not asset.get('subAssetName') or asset.get('assetType') not in ('Sprite', 'Material', 'Texture2D'):
                raise ValueError('Event asset adresi/türü eksik.')
            address = (asset['bundle'], asset['path'], asset['assetType'], asset['subAssetName'])
            if guids.get(key, address) != address:
                raise ValueError('Event ortak asset adresi çakışıyor.')
            guids[key] = address
    return manifest, plan


def api(method, path, data=None, *, envelope=False):
    auth = credentials()
    body = None if data is None else json.dumps(data).encode()
    request = urllib.request.Request('https://api.cloudflare.com/client/v4' + path.format(account=auth['CLOUDFLARE_ACCOUNT_ID']),
        data=body, method=method, headers={'Authorization': 'Bearer ' + auth['CLOUDFLARE_API_TOKEN'], 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            result = json.load(response)
    except urllib.error.HTTPError as error:
        # Never echo request headers, environment or arbitrary response text.
        raise CloudflareError(error.code) from None
    if not result.get('success'):
        raise ValueError('Cloudflare işlemi başarısız; token değerleri gösterilmiyor.')
    return result if envelope else result['result']


def setup(bucket, domain, apply):
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]{1,61}[a-z0-9]', bucket):
        raise ValueError('Geçersiz bucket adı.')
    if not re.fullmatch(r'[a-z0-9.-]+', domain) or domain.count('.') < 2:
        raise ValueError('Özel bir alt alan adı gerekli.')
    if not apply:
        return {'apply': False, 'bucket': bucket, 'domain': domain, 'billingChanged': False}
    buckets = api('GET', '/accounts/{account}/r2/buckets')['buckets']
    if bucket not in {b['name'] for b in buckets}:
        api('POST', '/accounts/{account}/r2/buckets', {'name': bucket, 'storageClass': 'Standard'})
    domains = api('GET', '/accounts/{account}/r2/buckets/' + bucket + '/domains/custom')['domains']
    if domain not in {d['domain'] for d in domains}:
        # Use the longest matching existing zone; never register a domain or change unrelated DNS.
        zones = api('GET', '/zones?per_page=50')
        matches = sorted((z for z in zones if domain.endswith('.' + z['name'])), key=lambda z: len(z['name']), reverse=True)
        if not matches:
            raise ValueError('Tokenın erişebildiği zone bulunamadı; alan adını R2 panelinden bağla.')
        api('POST', '/accounts/{account}/r2/buckets/' + bucket + '/domains/custom',
            {'domain': domain, 'zoneId': matches[0]['id'], 'enabled': True, 'minTLS': '1.2'})
    return {'bucket': bucket, 'domain': domain, 'billingChanged': False}



def cache_rule(domain, apply):
    if not re.fullmatch(r'[a-z0-9.-]+', domain) or domain.count('.') < 2:
        raise ValueError('Geçersiz özel alan adı.')
    if not apply:
        return {'apply': False, 'cacheHost': domain}
    zones = api('GET', '/zones?per_page=50')
    matches = sorted((z for z in zones if domain.endswith('.' + z['name'])), key=lambda z: len(z['name']), reverse=True)
    if not matches:
        raise ValueError('Zone bulunamadı.')
    zone = matches[0]['id']
    endpoint = '/zones/' + zone + '/rulesets/phases/http_request_cache_settings/entrypoint'
    rule = {'ref': 'wonder_fixers_content_bundles', 'description': 'Wonder Fixers immutable content bundles',
        'expression': '(http.host eq "' + domain + '" and ends_with(http.request.uri.path, ".bundle"))',
        'action': 'set_cache_settings', 'action_parameters': {'cache': True}, 'enabled': True}
    try:
        entrypoint = api('GET', endpoint)
    except CloudflareError as error:
        if error.status != 404:
            raise
        api('POST', '/zones/' + zone + '/rulesets', {'name': 'Content cache', 'kind': 'zone',
            'phase': 'http_request_cache_settings', 'rules': [rule]})
    else:
        existing = next((r for r in entrypoint.get('rules', []) if r.get('ref') == rule['ref']), None)
        if existing is None:
            api('POST', '/zones/' + zone + '/rulesets/' + entrypoint['id'] + '/rules', rule)
        elif existing.get('expression') != rule['expression'] or not existing.get('enabled'):
            raise ValueError('Mevcut içerik cache kuralı farklı; diğer kuralları değiştirmedim.')
    return {'cacheHost': domain, 'cacheRuleReady': True}


def wrangler(directory):
    env = os.environ.copy()
    env.update(credentials())
    env['WRANGLER_SEND_METRICS'] = 'false'
    # Unity launched from Finder/Dock does not inherit the interactive shell PATH.
    # Resolve npx explicitly so Homebrew and per-user Node installs work there too.
    npx = shutil.which('npx', path=env.get('PATH'))
    if not npx:
        home = Path.home()
        candidates = [
            '/opt/homebrew/bin/npx', '/usr/local/bin/npx',
            str(home / '.volta/bin/npx'),
        ]
        candidates += glob.glob(str(home / '.nvm/versions/node/*/bin/npx'))
        candidates += glob.glob(str(home / '.fnm/node-versions/*/installation/bin/npx'))
        npx = next((candidate for candidate in reversed(candidates) if os.path.isfile(candidate)), None)
    if not npx:
        raise ValueError("npx bulunamadı. Terminalde 'which npx' çalıştır; çıktıyı Unity'nin PATH'ine eklemek için araca ilet.")
    tool_dirs = [str(Path(npx).parent), '/opt/homebrew/bin', '/usr/local/bin', '/usr/bin', '/bin']
    env['PATH'] = os.pathsep.join(dict.fromkeys(tool_dirs + env.get('PATH', '').split(os.pathsep)))
    # Avoid a damaged/root-owned ~/.npm cache when Unity is launched from the GUI.
    # Keep npx's Wrangler cache beside generated content, inside its ignored export folder.
    env['npm_config_cache'] = str((Path(directory).resolve() / '.npm-cache'))
    return npx, env


def prune(directory, bucket, keep, apply, confirmed):
    manifest, plan = read_plan(directory)
    platform = manifest['platform']
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]{1,61}[a-z0-9]', bucket):
        raise ValueError('Geçersiz bucket adı.')
    protected = {b['file'] for b in manifest['bundles']}
    active = ROOT / 'Assets/_Project/Resources/ProgressiveContentManifest.json'
    archive = directory / 'protected-manifests' / bucket
    paths = list(archive.glob('*.json')) + keep
    if active.exists():
        paths.append(active)
    for path in paths:
        data = json.loads(path.read_text())
        if data['platform'] == platform:
            if not data['bundles']:
                raise ValueError('Boş koruma manifesti: ' + str(path))
            protected.update(b['file'] for b in data['bundles'])
    candidates = sorted(p for p in (directory / 'publish').glob('*.bundle')
                        if re.fullmatch(r'[0-9a-f]{64}\.bundle', p.name) and p.name not in protected)
    keys = [platform + '/' + p.name for p in candidates]
    signature = hashlib.sha256(json.dumps([bucket, plan['manifestSha256'], keys]).encode()).hexdigest()
    summary = {'bucket': bucket, 'candidateKeys': keys, 'localCandidateBytes': sum(p.stat().st_size for p in candidates),
               'planId': signature, 'deleted': 0}
    if not apply or not keys:
        return summary
    if confirmed != signature or (directory / 'verified.sha256').read_text().strip() != plan['manifestSha256']:
        raise ValueError('Güncel verify ve önizlemedeki --confirm-plan değeri gerekli.')
    npx, env = wrangler(directory)
    for key in keys:
        result = subprocess.run([npx, '--yes', 'wrangler@4', 'r2', 'object', 'delete',
                                 bucket + '/' + key, '--remote'], cwd=ROOT, env=env, capture_output=True)
        if result.returncode:
            raise ValueError('Silme başarısız: ' + key)
        summary['deleted'] += 1
        print('Deleted ' + key, flush=True)
    return summary


def missing_bundles(bucket, manifest):
    prefix = manifest['platform'] + '/'
    query = {'prefix': prefix, 'per_page': 1000}
    existing = {}
    cursors = set()
    while True:
        path = '/accounts/{account}/r2/buckets/' + bucket + '/objects?' + urllib.parse.urlencode(query)
        page = api('GET', path, envelope=True)
        for obj in page['result']:
            existing[obj['key']] = obj['size']
        rows = page['result']
        if not rows:
            break
        info = page.get('result_info') or {}
        cursor = info.get('cursor')
        if cursor:
            if cursor in cursors:
                raise ValueError('R2 listeleme tamamlanamadı.')
            cursors.add(cursor)
            query.pop('start_after', None)
            query['cursor'] = cursor
        else:
            # Some responses omit pagination metadata. Confirm the end with a
            # key-based continuation before treating unlisted bundles as missing.
            last_key = max(obj['key'] for obj in rows)
            if last_key <= query.get('start_after', ''):
                raise ValueError('R2 listelemesi ilerlemedi; yükleme durduruldu.')
            query.pop('cursor', None)
            query['start_after'] = last_key

    missing = {}
    for bundle in manifest['bundles']:
        name = bundle['file']
        if prefix + name not in existing:
            missing[name] = bundle
        elif existing[prefix + name] != bundle['bytes']:
            raise ValueError('R2 nesnesinin boyutu uyuşmuyor: ' + name)
    return list(missing.values())


def publish(directory, bucket, apply):
    manifest, plan = read_plan(directory)
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]{1,61}[a-z0-9]', bucket):
        raise ValueError('Geçersiz bucket adı.')
    summary = {'apply': apply, 'platform': manifest['platform'], 'files': len(manifest['bundles']),
        'bytes': sum(b['bytes'] for b in manifest['bundles']), 'manifestSha256': plan['manifestSha256']}
    if not apply:
        return summary
    pending = missing_bundles(bucket, manifest)
    summary.update(uploadedFiles=len(pending), uploadedBytes=sum(b['bytes'] for b in pending),
                   skippedFiles=len({b['file'] for b in manifest['bundles']}) - len(pending))
    print(f"R2: {len(pending)} yeni paket, {summary['uploadedBytes']} bayt.", flush=True)
    if pending:
        npx, env = wrangler(directory)
    archive = Path(directory) / 'protected-manifests' / bucket
    archive.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(Path(directory) / 'manifest.json', archive / (plan['manifestSha256'] + '.json'))
    # Upload immutable objects only. No latest-pointer or release-baseline mutation.
    for bundle in pending:
        path = Path(directory) / 'publish' / bundle['file']
        command = [npx, '--yes', 'wrangler@4', 'r2', 'object', 'put',
            bucket + '/' + manifest['platform'] + '/' + bundle['file'], '--remote', '--file', str(path),
            '--content-type', 'application/octet-stream', '--cache-control', 'public,max-age=31536000,immutable']
        result = subprocess.run(command, cwd=ROOT, env=env, capture_output=True, text=True)
        if result.returncode:
            detail = safe_command_error(result, [env.get('CLOUDFLARE_API_TOKEN'), env.get('CLOUDFLARE_ACCOUNT_ID')])
            raise ValueError('Paket yüklenemedi: ' + bundle['file'] + '\nWrangler: ' + detail)
        print('Uploaded ' + bundle['file'], flush=True)
    return summary


def verify(directory):
    manifest, plan = read_plan(directory)
    receipt = Path(directory) / 'verified.sha256'
    # An old success receipt must not survive a failed verification attempt.
    if receipt.exists():
        receipt.unlink()
    # Use the same public HTTP client as the successful manual download. No API token
    # is sent to the content host. Keep only one temporary bundle on disk at a time.
    with tempfile.TemporaryDirectory(prefix='tinyfixers-verify-') as temporary:
        downloaded = Path(temporary) / 'bundle'
        for index, bundle in enumerate(manifest['bundles'], 1):
            url = manifest['baseUrl'].rstrip('/') + '/' + bundle['file']
            command = ['curl', '--disable', '--silent', '--show-error', '--fail',
                '--proto', '=https', '--connect-timeout', '30', '--max-time', '600',
                '--max-filesize', str(bundle['bytes']), '--header', 'Accept-Encoding: identity',
                '--output', str(downloaded), '--write-out', '%{http_code}', url]
            result = subprocess.run(command, capture_output=True, text=True, timeout=620)
            if result.returncode != 0 or result.stdout.strip() != '200':
                raise ValueError('İndirme doğrulanamadı: ' + bundle['file']
                    + ' (HTTP ' + result.stdout.strip() + ', curl ' + str(result.returncode) + ').')
            if downloaded.stat().st_size != bundle['bytes'] or digest(downloaded) != bundle['sha256']:
                raise ValueError('Yayın hash/boyut doğrulaması başarısız: ' + bundle['file'])
            downloaded.unlink()
            print(f"Verified {index}/{len(manifest['bundles'])}: {bundle['file']}", flush=True)
    receipt.write_text(plan['manifestSha256'] + '\n')
    return {'verified': True, 'files': len(manifest['bundles']), 'releaseRecorded': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['status', 'setup', 'cache', 'plan', 'publish', 'verify', 'prune'])
    parser.add_argument('--directory', type=Path)
    parser.add_argument('--bucket', default='wonder-fixers-content')
    parser.add_argument('--domain', default='content.wondigames.com')
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--keep', type=Path, action='append', default=[], help='Desteklenen eski build manifesti')
    parser.add_argument('--confirm-plan')
    args = parser.parse_args()
    if args.command == 'status':
        result = {'buckets': [b['name'] for b in api('GET', '/accounts/{account}/r2/buckets')['buckets']]}
        if args.bucket in result['buckets']:
            result['domains'] = api('GET', '/accounts/{account}/r2/buckets/' + args.bucket + '/domains/custom')['domains']
    elif args.command == 'cache':
        result = cache_rule(args.domain, args.apply)
    elif args.command == 'setup':
        result = setup(args.bucket, args.domain, args.apply)
    else:
        if args.directory is None:
            parser.error('--directory gerekli')
        if args.command == 'prune':
            print(json.dumps(prune(args.directory, args.bucket, args.keep, args.apply, args.confirm_plan), indent=2))
            return
        result = verify(args.directory) if args.command == 'verify' else publish(args.directory, args.bucket, args.apply and args.command == 'publish')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, OSError, ReleaseError, subprocess.TimeoutExpired) as error:
        # Network exception strings can contain URLs; none include tokens here.
        print(str(error), file=sys.stderr)
        sys.exit(1)
