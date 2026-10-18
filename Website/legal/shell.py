"""Gizlilik / Kullanım Şartları sayfalarını ortak kabukla üretir (build.py çağırır). İçerik: privacy_*.html, terms_*.html."""
import pathlib
HERE = pathlib.Path(__file__).resolve().parent
HEAD = '''<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>{title}</title>
<meta name="description" content="{desc}">
<link rel="canonical" href="https://wondigames.com/{slug}/">
<link rel="icon" type="image/png" href="/img/favicon.png">
<meta name="theme-color" content="#1f5bff">
<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Baloo+2:wght@700&family=Nunito:wght@500;700;800&display=swap">
<link rel="stylesheet" href="/legal/legal.css">
</head>
<body>
<header class="bar"><div class="in">
  <a class="mark" href="/" aria-label="Wondi Games"><b>Wondi<i></i></b><span>Games</span></a>
  <nav aria-label="Legal">
    <a href="/privacy/" {pcur}><span data-en>Privacy</span><span data-tr hidden>Gizlilik</span></a>
    <a href="/terms/" {tcur}><span data-en>Terms</span><span data-tr hidden>Şartlar</span></a>
    <div class="lang" role="group" aria-label="Language"><button type="button" data-lang="tr" aria-pressed="false">TR</button><button type="button" data-lang="en" aria-pressed="true">EN</button></div>
  </nav>
</div></header>
<main>
<article class="doc" lang="en" data-doc="en">
{en}
</article>
<article class="doc" lang="tr" data-doc="tr" hidden>
{tr}
</article>
</main>
<footer>© <span id="y">2026</span> Julvera Teknoloji A.Ş. · Wondi Games</footer>
<script>
(() => {
  const set = (l) => {
    document.documentElement.lang = l;
    document.querySelectorAll('[data-doc]').forEach(a => a.hidden = a.dataset.doc !== l);
    document.querySelectorAll('[data-en]').forEach(e => e.hidden = l !== 'en');
    document.querySelectorAll('[data-tr]').forEach(e => e.hidden = l !== 'tr');
    document.querySelectorAll('.lang button').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.lang === l)));
    try { localStorage.setItem('wondi_lang', l); } catch (e) {}
  };
  let l = location.hash === '#tr' ? 'tr' : location.hash === '#en' ? 'en' : '';
  if (!l) { try { l = localStorage.getItem('wondi_lang') || ''; } catch (e) {} }
  if (!l) l = (navigator.language || 'en').toLowerCase().startsWith('tr') ? 'tr' : 'en';
  set(l);
  document.querySelectorAll('.lang button').forEach(b => b.addEventListener('click', () => set(b.dataset.lang)));
  document.getElementById('y').textContent = new Date().getFullYear();
})();
</script>
</body>
</html>
'''
# Oyundaki kabul ekranı bu sürümü okur (/legal/version.json). Metinde KABUL GEREKTİREN bir değişiklik olunca
# tarihi artır → oyuncu bir sonraki açılışta yeniden onaylar. Yazım düzeltmesi için değiştirme.
LEGAL_VERSION = "2026-10-17"

def to_rich_text(html: str) -> str:
    """Hukuki sayfa HTML'ini oyun içi TextMeshPro zengin metnine çevirir (başlık, kalın, madde, tablo satırı)."""
    import re, html as H
    s = html
    s = re.sub(r'<h1>(.*?)</h1>', r'<size=150%><b>\1</b></size>\n', s, flags=re.S)
    s = re.sub(r'<h2>(.*?)</h2>', r'\n<size=120%><b>\1</b></size>\n', s, flags=re.S)
    s = re.sub(r'<h3>(.*?)</h3>', r'\n<b>\1</b>\n', s, flags=re.S)
    s = re.sub(r'<p class="updated">(.*?)</p>', r'<i>\1</i>\n', s, flags=re.S)
    s = re.sub(r'<tr><th>.*?</tr>', '', s, flags=re.S)
    s = re.sub(r'<tr><td>(.*?)</td><td>(.*?)</td></tr>', r'• <b>\1</b>: \2\n', s, flags=re.S)
    s = re.sub(r'<li>(.*?)</li>', r'• \1\n', s, flags=re.S)
    s = re.sub(r'<br\s*/?>\s*', '\n', s)
    s = re.sub(r'</p>', '\n', s)
    s = re.sub(r'</?strong>', lambda m: '</b>' if m.group(0).startswith('</') else '<b>', s)
    s = re.sub(r'<a [^>]*>(.*?)</a>', r'\1', s, flags=re.S)
    s = re.sub(r'<(?!/?(b|i|size)[ =>/])[^>]+>', '', s)          # kalan etiketler (b/i/size hariç)
    s = H.unescape(s)
    s = re.sub(r'[ \t]+', ' ', s)
    s = re.sub(r' *\n *', '\n', s)
    s = re.sub(r'\n{3,}', '\n\n', s)
    return s.strip() + '\n'


def build(out_dir: pathlib.Path):
    import json
    (out_dir / 'legal').mkdir(parents=True, exist_ok=True)
    texts = {}
    unity_dir = HERE.parent.parent / 'Assets' / '_Project' / 'Resources' / 'Legal'
    unity_dir.mkdir(parents=True, exist_ok=True)
    for slug in ('terms', 'privacy'):
        for lang in ('tr', 'en'):
            rich = to_rich_text((HERE / f'{slug}_{lang}.html').read_text(encoding='utf-8'))
            name = f'{slug}_{lang}.txt'
            (out_dir / 'legal' / name).write_text(rich, encoding='utf-8')
            (unity_dir / name).write_text(rich, encoding='utf-8')     # oyunla gelen kopya (çevrimdışı)
            texts[f'{slug}_{lang}'] = f'https://wondigames.com/legal/{name}'
    (unity_dir / 'version.txt').write_text(LEGAL_VERSION, encoding='utf-8')
    (out_dir / 'legal' / 'version.json').write_text(json.dumps({
        "version": LEGAL_VERSION,
        "terms": "https://wondigames.com/terms/",
        "privacy": "https://wondigames.com/privacy/",
        "texts": texts,
    }), encoding='utf-8')
    pages = {
        'privacy': ('Privacy Policy — Wonder Fixers | Wondi Games',
                    'How Wondi Games (Julvera Teknoloji A.Ş.) collects and uses data in Wonder Fixers.'),
        'terms': ('Terms of Service — Wonder Fixers | Wondi Games',
                  'The terms for playing Wonder Fixers by Wondi Games (Julvera Teknoloji A.Ş.).'),
        'delete-account': ('Delete Account — Wonder Fixers | Wondi Games',
                           'How to delete your Wonder Fixers account and data.'),
    }
    (out_dir / 'legal').mkdir(parents=True, exist_ok=True)
    (out_dir / 'legal' / 'legal.css').write_text((HERE / 'legal.css').read_text(encoding='utf-8'), encoding='utf-8')
    for slug, (title, desc) in pages.items():
        src = 'delete' if slug == 'delete-account' else slug
        en = (HERE / f'{src}_en.html').read_text(encoding='utf-8')
        tr = (HERE / f'{src}_tr.html').read_text(encoding='utf-8')
        # str.format DEĞİL: sayfadaki JS/CSS süslü parantezleri şablon alanı sanılırdı.
        html = HEAD
        for key, val in (('title', title), ('desc', desc), ('slug', slug),
                         ('pcur', 'aria-current="page"' if slug == 'privacy' else ''),
                         ('tcur', 'aria-current="page"' if slug == 'terms' else ''),
                         ('en', en), ('tr', tr)):
            html = html.replace('{' + key + '}', val)
        d = out_dir / slug; d.mkdir(parents=True, exist_ok=True)
        (d / 'index.html').write_text(html, encoding='utf-8')
