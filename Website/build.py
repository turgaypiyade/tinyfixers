#!/usr/bin/env python3
"""Wondi Games sitesinin yayın paketi (wrangler.jsonc build adımı çalıştırır).

Kaynak tek: index.html (İngilizce + TR sözlüğü). Çıktı .wrangler/site:
  index.html      — İngilizce (kök)
  tr/index.html   — aynı sayfa, <html lang="tr" data-lang="tr"> + Türkçe başlık/açıklama/canonical
                    (arama motoru Türkçe içeriği ayrı adreste görsün; hreflang ile eşlenir)
  img/            — görseller
  robots.txt, sitemap.xml
Ham kareler (raw/, IMGStore/) asla kopyalanmaz.
"""
import datetime
import pathlib
import shutil
import sys

ROOT = pathlib.Path(__file__).resolve().parent
OUT = ROOT / ".wrangler" / "site"
SITE = "https://wondigames.com"

TR_TITLE = "Wonder Fixers — Eşleştirme Bulmaca Oyunu | Wondi Games"
TR_DESC = ("Wonder Fixers, Wondi Games'in eşleştirme (match-3) bulmaca oyunu. Taşları eşleştir, yıldız kazan ve "
           "hayvan tamirci ekibiyle Eğik Pati Kulesi'nden Miyav Piramidi'ne dünyanın harikalarını onar.")
TR_OG_TITLE = "Wonder Fixers — Wondi Games'in eşleştirme bulmacası"
TR_OG_DESC = "Taşları eşleştir, yıldız kazan ve hayvan tamirci ekibiyle dünyanın harikalarını onar."


def replace_once(text: str, old: str, new: str) -> str:
    if text.count(old) != 1:
        raise SystemExit(f"build.py: beklenen metin bulunamadı ya da birden fazla: {old[:70]!r}")
    return text.replace(old, new)


def turkish_page(html: str) -> str:
    html = replace_once(html, '<html lang="en">', '<html lang="tr" data-lang="tr">')
    start = html.index('<meta name="description" content="')
    end = html.index('">', start) + 2
    html = html[:start] + f'<meta name="description" content="{TR_DESC}">' + html[end:]
    html = replace_once(html, f'<link rel="canonical" href="{SITE}/">', f'<link rel="canonical" href="{SITE}/tr/">')
    start = html.index('<meta property="og:title" content="')
    end = html.index('">', start) + 2
    html = html[:start] + f'<meta property="og:title" content="{TR_OG_TITLE}">' + html[end:]
    start = html.index('<meta property="og:description" content="')
    end = html.index('">', start) + 2
    html = html[:start] + f'<meta property="og:description" content="{TR_OG_DESC}">' + html[end:]
    html = replace_once(html, f'<meta property="og:url" content="{SITE}/">', f'<meta property="og:url" content="{SITE}/tr/">')
    html = replace_once(html, '<meta property="og:locale" content="en_US">', '<meta property="og:locale" content="tr_TR">')
    html = replace_once(html, '<meta property="og:locale:alternate" content="tr_TR">',
                        '<meta property="og:locale:alternate" content="en_US">')
    start = html.index("<title>")
    end = html.index("</title>") + len("</title>")
    html = html[:start] + f"<title>{TR_TITLE}</title>" + html[end:]
    return html


def main() -> None:
    if OUT.exists():
        shutil.rmtree(OUT)
    (OUT / "tr").mkdir(parents=True)

    html = (ROOT / "index.html").read_text(encoding="utf-8")
    (OUT / "index.html").write_text(html, encoding="utf-8")
    (OUT / "tr" / "index.html").write_text(turkish_page(html), encoding="utf-8")
    shutil.copytree(ROOT / "img", OUT / "img", ignore=shutil.ignore_patterns(".DS_Store"))
    # Instagram görselleri (API herkese açık JPEG adresi ister): yalnız .jpg, betik/metin yayınlanmaz.
    for jpg in (ROOT / "social").glob("*/*.jpg"):
        dst = OUT / "social" / jpg.parent.name / jpg.name
        dst.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(jpg, dst)
    sys.path.insert(0, str(ROOT / "legal"))
    import shell                       # /privacy/ ve /terms/ (TR/EN tek sayfa)
    shell.build(OUT)

    (OUT / "robots.txt").write_text(f"User-agent: *\nAllow: /\n\nSitemap: {SITE}/sitemap.xml\n", encoding="utf-8")

    today = datetime.date.today().isoformat()
    alternates = (f'    <xhtml:link rel="alternate" hreflang="en" href="{SITE}/"/>\n'
                  f'    <xhtml:link rel="alternate" hreflang="tr" href="{SITE}/tr/"/>\n'
                  f'    <xhtml:link rel="alternate" hreflang="x-default" href="{SITE}/"/>\n')
    urls = "".join(f"  <url>\n    <loc>{SITE}{path}</loc>\n    <lastmod>{today}</lastmod>\n{alternates}  </url>\n"
                   for path in ("/", "/tr/"))
    urls += "".join(f"  <url>\n    <loc>{SITE}{path}</loc>\n    <lastmod>{today}</lastmod>\n  </url>\n"
                    for path in ("/privacy/", "/terms/", "/delete-account/"))
    (OUT / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">\n'
        f"{urls}</urlset>\n", encoding="utf-8")

    files = sum(1 for p in OUT.rglob("*") if p.is_file())
    print(f"build.py: {files} dosya → {OUT}")


if __name__ == "__main__":
    main()
