"""Wondi Games Instagram aracı (Instagram API, Instagram girişi). Anahtar ~/.wondi-instagram.env (IG_ACCESS_TOKEN) — ASLA yazdırma.
  python3 ig.py me                       → hesap bilgisi
  python3 ig.py post <jpeg_url> <caption_file>   → tek görsel paylaş (görsel herkese açık JPEG adresi olmalı)
  python3 ig.py carousel <caption_file> <url1> <url2> ...  → kaydırmalı paylaşım (2–10 görsel)
  python3 ig.py reel <mp4_url> <caption_file>   → Reels (akışta da görünür); video herkese açık MP4 adresi olmalı
  python3 ig.py refresh                  → 60 günlük anahtarı yeniler (en az 24 saatlik anahtar), dosyaya yazar
Görseller: Website/social/<gönderi>/ altında, build.py ile wondigames.com/social/... adresinde yayınlanır."""
import json, os, pathlib, sys, time, urllib.parse, urllib.request

ENV = pathlib.Path.home() / '.wondi-instagram.env'
API = 'https://graph.instagram.com/v23.0'

def token():
    for line in ENV.read_text().splitlines():
        if line.startswith('IG_ACCESS_TOKEN='): return line.split('=', 1)[1].strip()
    sys.exit('IG_ACCESS_TOKEN yok')

def call(path, params=None, post=False):
    params = dict(params or {}); params['access_token'] = token()
    data = urllib.parse.urlencode(params)
    req = urllib.request.Request(f'{API}/{path}', data=data.encode() if post else None) if post \
        else urllib.request.Request(f'{API}/{path}?{data}')
    try:
        with urllib.request.urlopen(req, timeout=60) as r: return json.load(r)
    except urllib.error.HTTPError as e:
        sys.exit(f'HTTP {e.code}: {e.read().decode()[:500]}')

def wait_ready(cid, tries=30):
    for _ in range(tries):
        st = call(cid, {'fields': 'status_code'}).get('status_code')
        if st == 'FINISHED': return
        if st == 'ERROR': sys.exit(f'container {cid} ERROR')
        time.sleep(3)
    sys.exit('container zaman aşımı')

def publish(cid):
    wait_ready(cid)
    res = call('me/media_publish', {'creation_id': cid}, post=True)
    media = call(res['id'], {'fields': 'permalink'})
    print('YAYINLANDI', media.get('permalink'))

def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'me'
    if cmd == 'me':
        print(call('me', {'fields': 'user_id,username,account_type,media_count'}))
    elif cmd == 'post':
        url, cap = sys.argv[2], pathlib.Path(sys.argv[3]).read_text(encoding='utf-8')
        publish(call('me/media', {'image_url': url, 'caption': cap}, post=True)['id'])
    elif cmd == 'carousel':
        cap = pathlib.Path(sys.argv[2]).read_text(encoding='utf-8')
        kids = [call('me/media', {'image_url': u, 'is_carousel_item': 'true'}, post=True)['id'] for u in sys.argv[3:]]
        for k in kids: wait_ready(k)
        publish(call('me/media', {'media_type': 'CAROUSEL', 'children': ','.join(kids), 'caption': cap}, post=True)['id'])
    elif cmd == 'reel':
        url, cap = sys.argv[2], pathlib.Path(sys.argv[3]).read_text(encoding='utf-8')
        cid = call('me/media', {'media_type': 'REELS', 'video_url': url, 'caption': cap, 'share_to_feed': 'true'}, post=True)['id']
        wait_ready(cid, tries=120)   # video işleme dakikalar sürebilir (~6 dk tavan)
        publish(cid)
    elif cmd == 'refresh':
        req = urllib.request.Request('https://graph.instagram.com/refresh_access_token?' + urllib.parse.urlencode(
            {'grant_type': 'ig_refresh_token', 'access_token': token()}))
        with urllib.request.urlopen(req, timeout=30) as r: new = json.load(r)
        lines = [l for l in ENV.read_text().splitlines() if not l.startswith('IG_ACCESS_TOKEN=')]
        ENV.write_text('\n'.join(lines + [f"IG_ACCESS_TOKEN={new['access_token']}"]) + '\n'); os.chmod(ENV, 0o600)
        print('yenilendi, gün:', round(new.get('expires_in', 0) / 86400))
    else:
        sys.exit(__doc__)

if __name__ == '__main__': main()
