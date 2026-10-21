"""Archive v1 and author the requested continuous Lantern column; no runtime changes."""
from pathlib import Path
import json, re, shutil, struct

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
asset = ROOT / 'Assets/_Project/Settings/ProductionLevels/LevelP_001860.asset'
archive = HERE / 'Revisions/v1'
assert not archive.exists(), 'Revision already applied; do not overwrite the original snapshot'
archive.mkdir(parents=True)
for path in [asset, Path(str(asset)+'.meta'), *[HERE/name for name in (
    'author.py', 'design.json', 'verify.py', 'validation.json', 'render_preview.py',
    'repetition-review.json', 'repetition-comparison.png')],
    ROOT/'Docs/LevelDesign/Level182-Fener-Senligi.md',
    ROOT/'Docs/LevelDesign/Level182-Fener-Senligi.png']:
    shutil.copy2(path, archive/path.name)

d = json.loads((HERE/'design.json').read_text())
rows = []
for y in range(11):
    rows.append(''.join('.' if not d['data']['cells'][y*9+x] else
                        'L' if x == 3 else 'H' if y >= 4 and 4 <= x <= 6 else 'o'
                        for x in range(9)))
obs = [{'.':0, 'o':0, 'L':75, 'H':30}[c] for row in rows for c in row]
origins = [i if o else -1 for i, o in enumerate(obs)]
d['revision'] = 2
d['rows'] = rows
d['data']['obstacles'] = obs
d['data']['obstacleOrigins'] = origins
d['decision'] = 'Tek fener sütununu iki aşamada aç; sağındaki porselen bloğunu temizleyerek çalışma alanını genişlet.'
d['flow'] = ['Açılışta dördüncü sütunda kesintisiz 11 fener.',
             'Sağ altta bitişik 3 × 7 porselen bloğu: 21 adet.',
             '30 fener hedefinin 19 adedi mevcut hedef refill sistemiyle gelir.']
d['notes'] = ['Kullanıcının parçalı fener eleştirisi üzerine dama kaldırıldı; board maskesi aynen korundu.',
              'Hedefler 30 fener / 21 porselen ve 28 hamle korundu; başlangıçta 11 fener, 21 porselen, 49 normal taş var.',
              'Sütun açılış kompozisyonudur. Hareket ve mevcut refill sonrasında aynı sütunda kalma garantisi yok.',
              'Lantern75 generic movable refill kullanır; pass başına sınır var, ID40 hamle sınırı uygulanmaz.',
              'Sabit renk, hazır special, katman ve yardımcı yok; oynanış dengesi ölçülmedi.']
text = asset.read_text()
for key, values in (('obstacles', obs), ('obstacleOrigins', origins)):
    encoded = struct.pack('<'+'i'*len(values), *values).hex()
    text, n = re.subn(rf'(?m)^(  {key}: )[^\n]*$', lambda m: m[1]+encoded, text)
    assert n == 1
asset.write_text(text)
(HERE/'design.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print('\n'.join(rows))
