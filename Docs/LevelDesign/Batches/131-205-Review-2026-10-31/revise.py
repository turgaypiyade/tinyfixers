"""Explicit per-level revision plan. Default writes review drafts; --apply requires baseline hashes."""
from pathlib import Path
from collections import Counter
import copy,hashlib,json,re,struct,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'Tools'))
from check_levels161_162 import array,footprint
from export_level_previews import parse_entries
from review_level_patterns import path_entries
KEYS=('cells','obstacles','obstacleOrigins','wallPieceIds','hamsterSatietyByCell','pinnedTileTypes','pinnedSpecialTypes')
CUSTOM=('stackedObstacles','safes','ancientSeals','tubes','magnets','waterChannels','tarpStatues','colorCrateInitialHits')
BEFORE=json.loads((HERE/'inventory-before.json').read_text())
def rows(text,key):
    m=re.search(r'^  '+key+r':[^\n]*\n((?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*)',text,re.M)
    return re.findall(r'^  - .*?(?=^  - |\Z)',m[1],re.M|re.S) if m else []
def goals(text):
    out=parse_entries(text,'goals')
    for g,raw in zip(out,rows(text,'goals')):
        m=re.search(r'iconOverride: ([^\n]+)',raw);g['iconOverride']=m[1] if m else '{fileID: 0}'
    return out
def goal(o,n):return dict(targetType=1,tileType=0,obstacleId=o,collectibleId=0,iconOverride='{fileID: 0}',amount=n)
def scalar(t,k,v):return re.sub(r'^  '+k+r':[^\n]*',lambda m:f'  {k}: {v}',t,flags=re.M)
def section(t,k,data):
    def val(v):return struct.pack('<'+str(len(v))+'i',*v).hex() if isinstance(v,list) else str(v)
    body=f'  {k}: []\n' if not data else f'  {k}:\n'+''.join(''.join(('  - ' if j==0 else '    ')+kk+': '+val(v)+'\n' for j,(kk,v) in enumerate(r.items())) for r in data)
    pat=r'^  '+k+r':[^\n]*\n(?:(?!  [A-Za-z_]\w*:).*(?:\n|$))*'
    return re.sub(pat,lambda m:body,t,flags=re.M) if re.search(pat,t,re.M) else t+body
def init(row):
    d=copy.deepcopy(row);t=(HERE/'Before'/Path(d['asset']).name).read_text();size=d['width']*d['height']
    d['original']=t;d['data']={k:(array(t,k,size) if re.search(r'^  '+k+r': [0-9a-f]+$',t,re.M) else [0]*size) for k in KEYS};d['goals']=goals(t);d['notes']=[];d['scalars']={}
    d['custom']={k:(path_entries(t,k) if k in ('magnets','waterChannels') else parse_entries(t,k)) for k in CUSTOM}
    return d
def note(d,s):d['notes'].append(s)
def occupied(d):
    w=d['width'];used={i for i,o in enumerate(d['data']['obstacles']) if o}
    for k in ('stackedObstacles','safes','ancientSeals'):
        for s in d['custom'][k]:
            bw,bh=(s['width'],s['height']) if k=='ancientSeals' else footprint(s.get('obstacleId',33))
            if s.get('obstacleId')==68:
                size=next((v['size'] for v in d['custom']['tarpStatues'] if v['originCellIndex']==s['originCellIndex']),bw);bw=bh=size
            used.update(s['originCellIndex']+y*w+x for y in range(bh) for x in range(bw))
    for p in d['custom']['tubes']:
        dx,dy=((0,-1),(0,1),(-1,0),(1,0))[p['direction']];used.update(p['originCellIndex']+j*(dx+dy*w) for j in range(p['length']))
    for k in ('magnets','waterChannels'):
        for p in d['custom'][k]:used.update(p['pathCellIndices'])
    used.update(i for i,v in enumerate(d['data']['pinnedSpecialTypes']) if v)
    used.update(i for i,v in enumerate(d['data']['pinnedTileTypes']) if v)
    return used
def crop(d,left=0,top=0,right=0,bottom=0):
    w,h=d['width'],d['height'];nw,nh=w-left-right,h-top-bottom
    mapping={y*w+x:(y-top)*nw+x-left for y in range(top,h-bottom) for x in range(left,w-right)}
    assert occupied(d)<=mapping.keys(),(d['level'],'crop touches authored content')
    for k,a in d['data'].items():
        new=[a[i] for i in mapping]
        if k=='obstacleOrigins':new=[mapping[v] if v>=0 else -1 for v in new]
        d['data'][k]=new
    for entries in d['custom'].values():
        for p in entries:
            if 'originCellIndex' in p:p['originCellIndex']=mapping[p['originCellIndex']]
            if 'pathCellIndices' in p:p['pathCellIndices']=[mapping[i] for i in p['pathCellIndices']]
    d['width'],d['height']=nw,nh
def cut(d,coords):
    used=occupied(d);w=d['width']
    for x,y in coords:
        i=y*w+x;assert 0<=x<w and 0<=y<d['height'] and i not in used,(d['level'],'cut content',x,y)
        d['data']['cells'][i]=0
def put(d,x,y,o):
    i=y*d['width']+x;assert d['data']['cells'][i] and not d['data']['obstacles'][i],(d['level'],'put',x,y)
    d['data']['obstacles'][i]=o;d['data']['obstacleOrigins'][i]=i
def replace(d,old,new):
    data=d['data'];w=d['width']
    for i,o in enumerate(data['obstacles']):
        if o!=old:continue
        data['obstacles'][i]=new;data['obstacleOrigins'][i]=i if new else -1
        if old in (55,56):data['wallPieceIds'][i]=0
    entries=[]
    for s in d['custom']['stackedObstacles']:
        if s['obstacleId']!=old:entries.append(s);continue
        if not new:continue
        bw,bh=footprint(old)
        for dy in range(bh):
            for dx in range(bw):entries.append(dict(s,obstacleId=new,originCellIndex=s['originCellIndex']+dy*w+dx))
    d['custom']['stackedObstacles']=entries
def counts(d):
    c=Counter(o for i,o in enumerate(d['data']['obstacles']) if o and d['data']['obstacleOrigins'][i]==i)
    for s in d['custom']['stackedObstacles']:c[s['obstacleId']]+=1
    for k,o in (('safes',33),('ancientSeals',64)):c[o]+=len(d['custom'][k])
    for k,o in (('tubes',26),('magnets',29),('waterChannels',76)):
        if d['custom'][k]:c[o]=len(d['custom'][k])
    return +c
def represent(g):
    if g['targetType']==1:return {g['obstacleId']}|({58} if g['obstacleId']==42 else set())
    if g['targetType']==0 and g['tileType']==9:return {44}
    if g['targetType']==2:return {1:{31,22},3:{78}}.get(g['collectibleId'],set())
    return set()
def fix_goals(d):
    c=counts(d);gs=[];known=set()
    for g in d['goals']:
        o=g.get('obstacleId',0)
        if g['targetType']==1 and not(c.get(o) or o==46 or o==42 and c.get(58) or o==53 and c.get(52)):continue
        if g['targetType']==1:
            if o==46:g['amount']=sum(d['data']['cells'])-sum(n for oid,n in c.items() if oid in (31,44,78))
            elif o==42:g['amount']=max(g['amount'],c.get(42,0)+c.get(58,0))
            elif o!=44:g['amount']=max(g['amount'],c.get(o,0))
        gs.append(g);known|=represent(g)
    for o,n in sorted(c.items()):
        if o in known:continue
        assert o not in (31,44,78),('missing producer goal',d['level'],o)
        gs.append(goal(o,n));known|=represent(gs[-1])
    if c.get(58) and 42 not in known:
        gs.append(goal(42,c.get(42,0)+c[58]));known.add(42)
    if c.get(52) or c.get(54):
        if not any(g['targetType']==1 and g['obstacleId']==53 for g in gs):gs.append(goal(53,8*(c.get(52,0)+c.get(54,0))))
    assert len(gs)<=5,(d['level'],'too many goals',[(g['targetType'],g['obstacleId']) for g in gs])
    d['goals']=gs
def design(d):
    n=d['level']
    if n==136:replace(d,30,40);note(d,'İki benzer hareketli tür tek iki-aşamalı şeritte birleşti; taş ve duvar hedefleri için beş slot kaldı.')
    if n==140:replace(d,38,0);replace(d,57,35);replace(d,30,40);cut(d,[(x,y) for y,xs in ((0,(0,1,7,8)),(1,(0,8)),(2,(0,8))) for x in xs]);note(d,'Üst çalışma alanı kademeli kısaldı; sepet/hamster yerine iki aşamalı şerit, kalkan, metal, geri sayımlı alet ve boss.')
    if n==141:replace(d,7,28);replace(d,8,28);replace(d,45,0);note(d,'Kartuş/kapı/oyma açılışı beş hedefte toplandı; üç ayrı yardımcı dolgu ailesi kaldırıldı.')
    if n==150:replace(d,45,12);replace(d,35,12);note(d,'Kalkan/yumurta yardımcıları kırmızı ailede birleşti; köprünün oyma yükü korundu. Çamur/alet ve boss ile beş hedef.')
    if n in (151,152):replace(d,32,77);note(d,'Mühür altındaki hedefsiz bonus altın, mevcut GoldCoin hedef engeline dönüştü; mühür sonrası iş de zorunlu.')
    if n==151:crop(d,bottom=2);cut(d,[(x,y) for y,xs in ((0,(0,1,7,8)),(1,(0,8)),(8,(0,8))) for x in xs]);note(d,'Mühür tanıtımı korundu; gereksiz üst/alt çalışma alanı 99→73 hücreye sıkıştı.')
    if n==152:cut(d,[(x,y) for y,xs in ((0,(0,1,7,8)),) for x in xs]);d['moves']=28;note(d,'İki kemerin açık kenar cepleri kesildi; 30→28 hamle ve mühür sonrası altın toplama.')
    if n==160:replace(d,35,25);replace(d,42,25);note(d,'Beş hedef için mühür altı kalkan ve çim çamurda birleşti; geri sayımlı alet korunurken bossun attığı çamur da aynı hedefte izlenir.')
    if n==162:d['moves']=30;note(d,'Dört zorunlu işi olan yonca silueti korundu; 32→30 hamle.')
    if n==168:crop(d,top=1);cut(d,[(4,y) for y in (4,5,6)]);note(d,'Boya avlusunda üst boş sıra kalktı; merkezdeki yarık iki eşleşme şeridi arasında seçim yaratır.')
    if n==170:crop(d,top=1);replace(d,35,28);note(d,'Boş ilk satır kaldırıldı; Boss board 11→10 satır. Kalkan altlığı oyma tabanına katıldı; çamur/lazer/oyma/alet ve boss hedefleri.')
    if n==172:cut(d,[(x,y) for x in (3,4) for y in (4,5)]);note(d,'Mor akıntıda 2×2 iç boşluk ve kartuş/plastik hedefleri; bedelsiz geniş merkez azaltıldı.')
    if n==173:replace(d,45,8);note(d,'İki yardım ailesi yerine turuncu bölge ve zorunlu şarj kutusu; tüm görünen yük beş hedefte.')
    if n==174:cut(d,[(3,0),(4,0),(5,0),(4,6),(4,7)]);note(d,'Üst orta giriş ve alt orta açıklık daraldı; pusulalar ve sepetler artık bitirilir.')
    if n==176:crop(d,left=1,right=1,top=2,bottom=1);note(d,'İlk renk sandığı dersi aynı iki bantla 9×10→7×7; 80 yerine 39 açık çalışma hücresi.')
    if n==177:replace(d,43,5);replace(d,45,40);cut(d,[(x,y) for y,xs in ((0,(0,1,7,8)),(6,(3,4,5))) for x in xs]);d['moves']=30;note(d,'Beş hedefe sığan dolap/roket atölyesi; bedelsiz toplu patlama yerine bitişik sandık işi, orta geçiş seçimi.')
    if n==178:crop(d,top=3);cut(d,[(x,y) for y,xs in ((0,(0,1,7,8)),(1,(0,8))) for x in xs]);note(d,'Renkli çerçevenin üstteki üç boş sırası çıktı; hedefler/hamleler korundu, başlangıç alanı kısaldı.')
    if n==179:replace(d,57,0);note(d,'Super Hard düzeni/40 hamle korundu; altıncı tür hamster kalktı, lazerler beşinci hedefte açıkça gösterildi.')
    if n==180:replace(d,35,25);replace(d,42,25);note(d,'Tarp altı ve yan çim alanı çamurda birleşti; geri sayımlı aletle boss dahil beş hedef ve uyumlu çamur baskısı.')
    if n==183:replace(d,30,7);note(d,'Orta hareketli bölge tek mor aile oldu; su deposunun ürettiği su dahil tüm yük beş hedefe sığar.')
    if n==184:crop(d,top=2);d['moves']=27;note(d,'Kargo rıhtımının iki boş üst sırası kalktı; kapalı çıkışlar ve iki kargo kanadı korunur.')
    if n==185:replace(d,73,71);note(d,'Hard hamle/maske korundu; alt iki renk bölgesi tek sarı aile, çamur/tarp dahil toplam beş hedef.')
    if n==186:cut(d,[(x,y) for y in (0,1) for x in (7,8)]+[(8,y) for y in (2,3,4,5)]);d['moves']=26;note(d,'Kanal tanıtımının üst sağ boş terası kesildi; kaynak/çıkış ve fener sütunu aynı, 28→26 hamle.')
    if n==188:crop(d,top=3);cut(d,[(0,0),(8,0)]);d['moves']=26;note(d,'Boru orgunun üç boş üst sırası kalktı; anahtar hedefi üreticileri de temsil eder, tüplerin uçları korunur.')
    if n==190:
        crop(d,left=1,right=1);replace(d,35,25)
        for y in (4,5):
            for x in (2,3):put(d,x,y,25)
        note(d,'Düello 7 sütuna indi; kırmızı/yeşil/çamur/geri sayımlı alet ve boss beşlisi, baskı çamur hedefine bağlı.')
    if n==194:
        replace(d,38,0);replace(d,73,72)
        initial=d['custom']['colorCrateInitialHits'];initial[:]=[s for s in initial if d['data']['obstacles'][s['originCellIndex']] not in (72,73)]
        for i,o in enumerate(d['data']['obstacles']):
            if o==72:initial.append(dict(originCellIndex=i,obstacleId=72,remainingHits=2 if i//d['width']<=4 else 1))
        note(d,'Yedi tür beşe indi: kırmızı kapak bölgesi, metal, Rodos, pusula, çamur. Kapaklar 2→1 başlangıç aşamasıyla bitirilebilir yükte.')
    if n==196:crop(d,top=2);cut(d,[(7,0),(8,0),(8,1)]);note(d,'Basamaklı arşivin iki bedelsiz üst sırası ve sağ üst köşesi kesildi; açılan her dolap yeni alan sağlar.')
    if n==200:
        cut(d,[(x,y) for y,xs in ((0,(0,1,7,8)),(1,(0,8))) for x in xs])
        note(d,'Kanyonun üst cepleri küçüldü; boss/mavi/yumurta/kalkan/geri sayımlı alet hedefleri atılan türlerle eşleşir.')
    if n==202:
        cut(d,[(2,0),(6,0),(1,1),(7,1)])
        for y in (1,2):
            for x in range(2,7):put(d,x,y,25)
        d['moves']=25;note(d,'Halka avlunun üst tacı daraldı; iki sıralı zorunlu çamur işi ile kuş ritmi boya kasaları arasında bölünür.')
    if n==203:
        for y in (7,8):
            for x in (2,3,5,6):put(d,x,y,25)
        d['moves']=25;note(d,'Duvar sonrası iki kargo ayağında çamur işi eklendi; kuş/duvar/kargo açılışını tek hamle zinciriyle bitirmek yetmez.')
    if n==204:
        for x in (3,4,5):put(d,x,8,75)
        d['moves']=26;note(d,'S kıyısı korundu; dip fener grubu 3×3 oldu, hedef 6→9 ve 28→26 hamle.')
    if d['kind']:
        # Keep the existing countdown mechanic, but one grey family with one
        # authored target guarantees the goal is reachable even before a later throw.
        x,y={140:(4,8),150:(4,9),160:(4,6),170:(4,9),180:(2,9),190:(3,9),200:(7,9)}[n]
        i=y*d['width']+x
        old=d['data']['obstacles'][i]
        assert old in (0,25,28,35,40) and d['data']['cells'][i],(n,'tool location',x,y,old)
        d['data']['obstacles'][i]=47;d['data']['obstacleOrigins'][i]=i
        pool={140:[40],150:[12,25],160:[25],170:[25],180:[25],190:[25],200:[11]}[n]
        d['scalars']['bossThrownObstacles']=struct.pack('<'+str(len(pool))+'i',*pool).hex()
        d['scalars']['bossToolThreatEnabled']=1
        d['scalars']['bossToolThreatTypes']=struct.pack('<i',47).hex()
        note(d,'Geri sayımlı alet mekaniği korundu: tek gri tür ve başlangıçta bir hedef. Rastgele baskı yalnız gösterilen türlerden. Beş slot için fazla aileler birleştirildi; boss HP/dalgalar/saldırı sayıları aynı.')
    fix_goals(d)
    if n in (160,180,190):
        assert any(g['targetType']==1 and g['obstacleId']==25 for g in d['goals'])
    return d
def serialize(d):
    t=d['original'];w,h=d['width'],d['height'];t=scalar(t,'width',w);t=scalar(t,'height',h);t=scalar(t,'moves',d['moves'])
    for k,v in d['scalars'].items():t=scalar(t,k,v)
    for k,a in d['data'].items():t=scalar(t,k,struct.pack('<'+str(len(a))+'i',*a).hex())
    for k,v in d['custom'].items():
        if v!=((path_entries(d['original'],k) if k in ('magnets','waterChannels') else parse_entries(d['original'],k))):t=section(t,k,v)
    if d['goals']!=goals(d['original']):t=section(t,'goals',d['goals'])
    return t
def main():
    drafts=HERE/'Draft';drafts.mkdir(exist_ok=True);manifest=[]
    for row in BEFORE:
        if row['level']<=130:continue
        d=design(init(row));text=serialize(d);changed=text!=d['original']
        (drafts/Path(d['asset']).name).write_text(text)
        if changed and not d['notes']:note(d,'Hedef dışı türler ve eksik miktarlar görünür hedeflere eklendi; maske/hamle/konumlar korundu.')
        manifest.append(dict(level=d['level'],name=d['name'],asset=d['asset'],changed=changed,notes=d['notes'],before=dict(width=row['width'],height=row['height'],moves=row['moves'],active=sum(row['data']['cells']),goals=row['goals']),after=dict(width=d['width'],height=d['height'],moves=d['moves'],active=sum(d['data']['cells']),goals=d['goals'])))
    (HERE/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
    if '--apply' in sys.argv:
        baseline=json.loads((HERE/'baseline.json').read_text())['hashes']
        for entry in manifest:
            p=ROOT/entry['asset'];assert hashlib.sha256(p.read_bytes()).hexdigest()==baseline[entry['asset']],('changed since snapshot',str(p))
        for entry in manifest:
            if entry['changed']:(ROOT/entry['asset']).write_text((drafts/Path(entry['asset']).name).read_text())
    print('Reviewed',len(manifest),'draft changes',sum(x['changed'] for x in manifest),'applied','--apply' in sys.argv)
if __name__=='__main__':main()
