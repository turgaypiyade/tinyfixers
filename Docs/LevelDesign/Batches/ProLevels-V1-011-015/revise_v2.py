"""Revise rejected V1 12–15 with authored board shapes and functional reveal beats.
Retains names, GUIDs, catalogue links, 11 and all unrelated assets. No Unity/gameplay execution.
"""
from pathlib import Path
from collections import Counter
import json,re,struct

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
LEVELS=ROOT/'Assets/_Project/Settings/ProLevels'
previous={d['n']:d for d in json.loads((HERE/'designs.json').read_text())}
assert (HERE/'Revisions/v1/REJECTED.md').exists()
assert not (HERE/'v2-authored.json').exists(), 'Already revised; use current assets rather than overwriting user edits.'


def new(n,h,moves,idea,flow):
    old=previous[n]
    return dict(n=n,w=9,h=h,moves=moves,title=old['title'],nameEn=old['nameEn'],guid=old['guid'],
                asset=old['asset'],difficulty=old['difficulty'],targetPassRate=old['targetPassRate'],
                decision=idea,flow=flow,stacks=[],tarps=[],tubes=[],walls=[],oilCap=0,revision=2,
                data={k:[v]*(9*h) for k,v in [('cells',0),('obstacles',0),('obstacleOrigins',-1),
                       ('wallPieceIds',0),('hamsterSatietyByCell',0),('pinnedTileTypes',0),('pinnedSpecialTypes',0)]})


def area(d,xs,ys):
    for y in ys:
        for x in xs:d['data']['cells'][y*9+x]=1


def put(d,x,y,o,cover=False):
    i=y*9+x
    assert d['data']['cells'][i] and d['data']['obstacles'][i]==0,(d['n'],x,y)
    d['data']['obstacles'][i]=25 if cover else o;d['data']['obstacleOrigins'][i]=i
    if cover:d['stacks'].append(dict(originCellIndex=i,obstacleId=o,stackOrder=0,initialRemainingHits=0))


def special(d,x,y,kind):
    i=y*9+x;assert d['data']['cells'][i] and not d['data']['obstacles'][i]
    d['data']['pinnedSpecialTypes'][i]=kind


d12=new(12,10,31,
        'Üst avludaki dikey roketi ateşle: ortadaki gerçek boşluğu geçip alttaki kapağı açar. Taşlar boşluktan geçerek çamurlu avluya dolmaya başlar.',
        ['Merkezde 3×2 gerçek pencere boşluğu; onu saran tek-hit sandık kemeri.',
         'Pencerenin üstünde 1 dikey roket: aşağıdaki tek-hit kapakla aynı sütunda.',
         'İlk dar akış açılır; kapağı genişlettikçe alttaki 7×4 avlu oyuna katılır.'])
area(d12,range(9),range(5));area(d12,range(1,8),range(5,10))
for y in (3,4):
    for x in range(3,6):d12['data']['cells'][y*9+x]=0
for y in range(3,6):
    for x in (1,2,6,7):put(d12,x,y,4)
for x in (2,3,5,6):put(d12,x,2,4)
for x in range(3,6):put(d12,x,5,4)
for y in range(6,10):
    for x in range(1,8):put(d12,x,y,25)
special(d12,4,2,2)
d12['cue']='Dikey roket → pencere arkasındaki kapak → dolmaya başlayan avlu'
d12['planOpenCells']=[5*9+4]

d13=new(13,9,30,
        'İki alt odayı farklı sandık aşamaları kapatıyor. Tek-hit sol odayı erken açıp üç sütunu tam boy çalışma alanına dönüştür; burada hazırladığın yatay roketlerle boşluğun ötesindeki iki-hit odaya da ulaşabilirsin.',
        ['Dört 3×3 oda, ortadaki üç tam sıra ile bağlı; üst/alt ortada gerçek boşluklar.',
         'Sol alt oda 9 tek-hit, sağ alt oda 9 iki-hit sandık; her ikisinin altında çamur.',
         'Sol oda erken açılan ikinci çalışma alanı olur; yatay special boşluğu geçebilir.'])
area(d13,range(9),range(3,6))
for xs in (range(3),range(6,9)):
    area(d13,xs,range(3));area(d13,xs,range(6,9))
for y in range(6,9):
    for x in range(3):put(d13,x,y,4,True)
    for x in range(6,9):put(d13,x,y,5,True)
d13['cue']='Önce sol odayı aç → yeni çalışma alanı → sağ odaya yatay vuruş'
d13['planOpenCells']=[y*9+x for y in range(6,9) for x in range(3)]

d14=new(14,10,29,
        'Üst-sol ve alt-sağ çalışma alanlarını yağlı bir geçiş birleştiriyor. Yatay roketi doğru anda kullanıp köprüyü aç; sonra yağa vurarak tekrar yayılmasını engelle.',
        ['İki kaymış 6 sütunluk alan, ortadaki tam sıra ile Z biçiminde birleşir.',
         'Geçiş üzerinde 3×4 yağ şeridi; iki dış köşede farklı dayanıklılıkta sandıklar.',
         'Hazır yatay roket üç yağ hücresini aynı sırada hedefler; yağ tavanı 18.'])
area(d14,range(6),range(5));area(d14,range(9),[5]);area(d14,range(3,9),range(6,10))
for y in range(6):
    for x in (0,1):put(d14,x,y,5)
for y in range(7,10):
    for x in (7,8):put(d14,x,y,4)
for y in range(4,8):
    for x in range(3,6):put(d14,x,y,14)
special(d14,2,5,1);d14['oilCap']=18
d14['cue']='Yatay roket → yağlı geçiş → iki alan arasında açık sıra'
d14['planOpenCells']=[5*9+x for x in range(3,6)]

d15=new(15,11,30,
        'Hazır PulseCore + dikey roketi birleştirerek ilk bendi aç. Taşlar ilk odaya dolar; ikinci ve üçüncü bentlerde kombo sonrası kalan dayanıklılığı ve alttaki çamuru bitir.',
        ['Geniş üst alandan aşağı üç kaymış hazne; her bendin altına çamur saklı.',
         'Birinci bent 6 tek-hit, ikinci 6 iki-hit, üçüncü 6 üç-hit sandık.',
         'Hazır combo ilk açılışı başlatır; alt iki bendin tamamını veya çamur hedefini bitirmez.'])
area(d15,range(9),range(3));area(d15,range(6),range(3,6))
area(d15,range(3,9),range(6,9));area(d15,range(1,7),range(9,11))
for y,xs,o in [(3,range(6),4),(6,range(3,9),5),(9,range(1,7),6)]:
    for x in xs:put(d15,x,y,o,True)
for x in range(1,7):put(d15,x,10,25)
special(d15,4,2,4);special(d15,5,2,2)
d15['cue']='Pulse + roket → ilk bent → ikinci hazne → üçüncü bent'
d15['planOpenCells']=[3*9+x for x in range(6)]

designs=[d12,d13,d14,d15]
for d in designs:
    counts=Counter(o for o in d['data']['obstacles'] if o)
    counts.update(s['obstacleId'] for s in d['stacks'])
    d['goals']=[dict(targetType=1,tileType=0,obstacleId=o,collectibleId=1,iconOverride='{fileID: 0}',amount=counts[o])
                for o in (4,5,6,14,25) if counts[o]]
    p=LEVELS/(d['asset']+'.asset');text=p.read_text()
    def field(k,v):
        global text
        text,count=re.subn(r'^  '+k+r':[^\n]*',lambda _:f'  {k}: {v}',text,flags=re.M);assert count==1,k
    def entries(k,rows):
        global text
        out=f'  {k}: []\n' if not rows else f'  {k}:\n'+''.join(''.join(('  - ' if i==0 else '    ')+key+': '+str(v)+'\n' for i,(key,v) in enumerate(row.items())) for row in rows)
        text,count=re.subn(r'^  '+k+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*',lambda _:out,text,flags=re.M);assert count==1,k
    for k,v in dict(height=d['h'],moves=d['moves'],oilSpreadMaxCells=d['oilCap']).items():field(k,v)
    for k,a in d['data'].items():field(k,struct.pack('<'+'i'*len(a),*a).hex())
    entries('goals',d['goals']);entries('stackedObstacles',d['stacks'])
    p.write_text(text)
(HERE/'designs.json').write_text(json.dumps(designs,ensure_ascii=False,indent=2)+'\n')
(HERE/'v2-authored.json').write_text(json.dumps(dict(revision=2,rejectedVersion='Revisions/v1/',levels=[12,13,14,15],namesAndGuidsRetained=True),indent=2)+'\n')
print('V2 written: window/reveal, four rooms, oil bridge, three offset gates. Existing names/GUIDs/catalog retained.')
