"""251 · Sıcak Hava Balonu tanıtımı. Yalnız 251'i EKLER (1–250 dokunulmaz). Unity/build/sim yok.
Kullanım: python3 -B author.py [--write]"""
from pathlib import Path
import importlib.util, json, re, sys, uuid
HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location('nature_author', HERE.parent/'240-250-Nature/author.py')
nature = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(nature)
late = nature.late
ROOT, PRO, NAMES = late.ROOT, late.PRO, late.NAMES
CAT = PRO/'LevelCatalogProV1.asset'
LEVEL_NAMES_MD = ROOT/'Docs/LevelDesign/Level-Names.md'


def level251():
    b = late.board(251, 'Kapadokya Sabahı', 'Cappadocia Morning', 'Kapadokya-Sabahi', [
        'oo.ooo.oo',
        '.o..o..o.',
        '.o.ooo.o.',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo',
        'ooooooooo'], 24,
        'Kapadokya’da sabah: üstte şapkalı üç peri bacası, vadinin iki alt köşesinde ahşap dolaplarında bekleyen iki sıcak hava balonu. '
        'Balonların arasında, vadi tabanında beş oyma kaya.',
        'Ortada special üretip köşedeki balonlara mı göndereceksin, yoksa önce tabandaki kayaları eşleşmelerle mi oyacaksın?',
        'Balon her special/joker vuruşunda dolabın içinde bir kademe şişer; üçüncüde dolabı ikiye yarar ve uçar. Uçarken kendi iki sütunundaki taşları '
        'sağa sola savurur, yoldaki special’ları tetikler, fenere ve engellere birer vuruş verir.',
        'İki balon, beş kaya ve peri bacalarından düşen üç fener birlikte bitmeli.')
    b.put(0, 9, 103).put(7, 9, 103)                 # alt köşelerde dolaplı balonlar
    for x in range(2, 7):
        b.put(x, 10, 28)                            # balonların arasında taban: __XXXXX__
    for x in (1, 4, 7):
        b.put(x, 0, 75)                             # peri bacası şapkalarında fenerler (düşer)
    b.goal(103, 2).goal(28, 5).goal(75, 3)
    b.d['previewStage'] = ''
    b.d['previewRemoved'] = []
    b.d['maxPins'] = 4
    return b.d


nature.LEVELS[251] = level251


def prepare():
    return nature.prepare(251)


def write(d):
    asset = ROOT/d['asset']; meta = Path(str(asset)+'.meta')
    assert not asset.exists() and not meta.exists(), 'yalnız yeni level ekler'
    cat = CAT.read_text()
    assert list(map(int, re.findall(r'^    level: (\d+)$', cat, re.M))) == list(range(1, 251))
    names = json.loads(NAMES.read_text())
    assert not any(e['level'] == 251 or e['levelKey'] == d['levelKey'] for e in names['entries'])
    before = HERE/'Before'; before.mkdir(exist_ok=True)
    for p in (CAT, NAMES, LEVEL_NAMES_MD): (before/p.name).write_bytes(p.read_bytes())
    d['guid'] = uuid.uuid4().hex
    asset.write_text(late.serialize(d))
    meta.write_text(f'fileFormatVersion: 2\nguid: {d["guid"]}\nNativeFormatImporter:\n  externalObjects: {{}}\n'
                    '  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    cat += (f'  - chapter: {(d["level"]-1)//25+1}\n    level: {d["level"]}\n    levelKey: {d["levelKey"]}\n'
            f'    levelData: {{fileID: 11400000, guid: {d["guid"]}, type: 2}}\n')
    CAT.write_text(cat)
    names['entries'].append(dict(level=d['level'], levelKey=d['levelKey'], name=d['name'], nameEn=d['nameEn']))
    NAMES.write_text(json.dumps(names, ensure_ascii=False, indent=2)+'\n')
    md = LEVEL_NAMES_MD.read_text()
    if not md.endswith('\n'): md += '\n'
    LEVEL_NAMES_MD.write_text(md + f'| {d["level"]} | {d["name"]} |\n')
    (HERE/f'design-{d["level"]}.json').write_text(json.dumps(d, ensure_ascii=False, indent=2)+'\n')
    print('eklendi', asset.name, 'guid', d['guid'])


if __name__ == '__main__':
    d = prepare(); print(d['name'], d['openingGeometry'])
    if '--write' in sys.argv: write(d)
