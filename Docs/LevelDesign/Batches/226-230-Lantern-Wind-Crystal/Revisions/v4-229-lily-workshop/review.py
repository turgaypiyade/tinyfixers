"""Scoped serialized/static checks and actual sprite diagrams. No game simulation."""
import json,re,subprocess,importlib.util,random,textwrap
from collections import Counter
from PIL import Image,ImageDraw
from author import HERE,BATCH,ROOT,ASSET,shared,sha
spec=importlib.util.spec_from_file_location('lilyWorkshopArt',BATCH.parent/'173-180/render_previews.py');art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
d=json.loads((HERE/'design.json').read_text())

def verify():
    protected=json.loads((HERE/'protected-before.json').read_text());assert not [p for p,h in protected.items() if not(ROOT/p).exists() or sha(ROOT/p)!=h]
    t=(ROOT/ASSET).read_text();parsed=json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e',"t=File.readlines(ARGV[0]).reject{|l|l.start_with?('%','---')}.join;puts JSON.generate(YAML.safe_load(t,aliases:false)['MonoBehaviour'])",str(ROOT/ASSET)]))
    for k,v in d['data'].items():assert shared.array(t,k,99)==v and len(v)==99
    for k in shared.CUSTOM:assert (parsed[k] or [])==d['custom'][k],k
    for a,b in zip(parsed['goals'],d['goals']):
        for k,v in b.items():
            if k!='iconOverride':assert a[k]==v
            else:assert re.search(r'guid: (\w+)',v)[1]==a[k]['guid'] and a[k]['fileID']!=0
    assert len(parsed['goals'])==4 and (parsed['moves'],parsed['width'],parsed['height'])==(28,9,11)
    assert set(d['data']['obstacles'])=={0,69,27,28,40} and not any(d['custom'].values())
    assert not any(d['data']['pinnedTileTypes']) and not any(d['data']['pinnedSpecialTypes'])
    instances=Counter()
    for i,o in enumerate(d['data']['obstacles']):
        origin=d['data']['obstacleOrigins'][i]
        if not o:assert origin==-1;continue
        fw,fh=shared.footprint(o);assert origin<=i and origin%9+fw<=9 and origin//9+fh<=11
        if origin==i:
            instances[o]+=1
            assert all(d['data']['obstacles'][i+dy*9+dx]==o and d['data']['obstacleOrigins'][i+dy*9+dx]==i for dy in range(fh) for dx in range(fw))
    assert instances=={69:12,27:4,28:12,40:12}
    assert {g['obstacleId']:g['amount'] for g in d['goals']}==dict(instances)
    phases=[]
    for removed in [[],[28],[28,40],[28,40,27,69]]:
        slots,reach,seed=shared.feed(d,removed);assert slots==reach and not seed
        phases.append(dict(removed=removed,slots=len(slots),natural=len(reach),unfilled=0))
    assert phases[0]['slots']==59
    # All moving plastics start bottom-packed; lily grass is implicit in GridSpawner.
    assert all(d['data']['obstacles'][y*9+x]==40 for x in range(4,7) for y in range(7,11))
    cat=(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').read_text();assert f'    level: 229\n    levelKey: LevelCL_0229\n    levelData: {{fileID: 11400000, guid: {d["guid"]}' in cat
    names=json.loads((ROOT/'Assets/_Project/Resources/LevelNames.json').read_text());e=next(x for x in names['entries'] if x['level']==229);assert (e['name'],e['nameEn'])==(d['name'],d['nameEn'])
    (HERE/'validation.json').write_text(json.dumps(dict(result='PASS',protectedProjectFiles=len(protected),changedLevels=[229],phases=phases,openingUnderlyingSlots=59,lockedLilyCells=12,exposedMatchSlots=47,buildOrPlay=False,measuredBalance=False),indent=2)+'\n')
    print('PASS229:serialized goals/layers/footprints/feed;other',len(protected),'project files preserved.')

def board(im,model,xy,cell,removed=()):
    dr=ImageDraw.Draw(im);w=model['width'];ls=shared.layers(model,removed);slots,reach,seed=shared.feed(model,removed);rng=random.Random(229);colors={}
    for i,a in enumerate(model['data']['cells']):
        if not a:continue
        x,y=xy[0]+i%w*cell,xy[1]+i//w*cell;dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=5,fill='#354c65',outline='#65839c')
    for i in sorted(reach|seed):
        choices=list(range(4));rng.shuffle(choices)
        for c in choices:
            if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==c) and not(i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==c):break
        colors[i]=c;art.paste(im,art.TILES[c],(xy[0]+i%w*cell,xy[1]+i//w*cell,cell,cell),cell*.07)
    seen=set()
    for i,layers in enumerate(ls):
        for o,origin in layers:
            if (o,origin) in seen:continue
            seen.add((o,origin));fw,fh=shared.footprint(o);box=(xy[0]+origin%w*cell,xy[1]+origin//w*cell,fw*cell,fh*cell)
            if o==69:art.paste(im,art.oid_art(42),box)
            art.paste(im,art.oid_art(o),box,cell*.015)

def render():
    im=Image.new('RGBA',(1450,1370),'#172536');dr=ImageDraw.Draw(im)
    dr.text((30,22),'229 · RÜZGÂR BURGUSU · YENİ TASARIM',font=art.font(33,True),fill='#ffcf75')
    dr.text((30,77),'SUPER HARD · 28 hamle · 9×11 · 4 renk',font=art.font(24),fill='#e7edf6')
    dr.text((30,135),'BAŞLANGIÇ',font=art.font(23,True),fill='#ffcf75');board(im,d,(30,178),65)
    dr.text((740,135),'OYMA KAİDESİ AÇILINCA',font=art.font(23,True),fill='#ffcf75');board(im,d,(740,178),45,[28])
    dr.text((740,703),'Statik kesit; oynanmış bir durum değildir.',font=art.font(18),fill='#a9bdd1')
    y=757
    for text in ['İlk special nilüfer işini bitirmez: yapraklar ve alt çim kalır.','Dolap kapısı açılınca içerik toplama işi başlar.','Plastik bloğu iki aşamalı; ilk vuruşta bitmez.','47 açık eşleşme yuvası; ayrıca nilüferlerin altında 12 kilitli taş. Bütün yuvalar doğal beslenir.']:
        for line in textwrap.wrap(text,49):dr.text((740,y),line,font=art.font(20),fill='#e7edf6');y+=27
        y+=18
    labels={69:'12 Nilüfer',27:'4 Dolap',28:'12 Oyma',40:'12 İki aşamalı plastik'}
    for j,g in enumerate(d['goals']):
        x=30+j*352;art.paste(im,art.sprite(art.ref(g['iconOverride'])),(x,1140,62,62));dr.text((x+70,1160),labels[g['obstacleId']],font=art.font(20,True),fill='#e7edf6')
    dr.text((30,1250),'Gerçek proje sprite’ları · Normal taş renkleri temsili · Hamle ilk ayar; kullanıcı henüz oynamadı/onaylamadı.',font=art.font(20),fill='#a9bdd1')
    dr.text((30,1294),'Jel ve pres yok. Başlangıç pini ve boşu yok. Unity build / Play / denge simülasyonu yapılmadı.',font=art.font(20),fill='#a9bdd1')
    im.convert('RGB').save(HERE/'Level229.png');im.convert('RGB').save(ROOT/'Docs/LevelDesign/Level229-Ruzgar-Burgusu.png')
    print('Rendered229 opening and static cutaway with current art.')

if __name__=='__main__':verify();render()
