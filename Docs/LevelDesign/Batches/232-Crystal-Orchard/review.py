"""Static footprint/feed/preservation audit and real-sprite previews. No gameplay simulation."""
from author import *
from collections import Counter,deque
import subprocess,random,copy
from PIL import Image,ImageDraw
spec=importlib.util.spec_from_file_location('crystalArt',HERE.parent/'226-230-Lantern-Wind-Crystal/render.py');old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old);art=old.art
D=json.loads((HERE/'design.json').read_text())
def load(p):
 return json.loads(subprocess.check_output(['ruby','-ryaml','-rjson','-e',"s=File.readlines(ARGV[0]).reject{|l|l.start_with?('%','---')}.join;puts JSON.generate(YAML.safe_load(s,aliases:false)['MonoBehaviour'])",str(p)]))
def verify():
 baseline=json.loads((HERE/'protected-before.json').read_text());allowed={str(p.relative_to(ROOT)) for p in [CAT,NAMES]}
 drift=[p for p,h in baseline.items() if p not in allowed and (not(ROOT/p).exists() or sha(ROOT/p)!=h)];assert not drift,drift
 t=ASSET.read_text();raw=load(ASSET)
 for k,v in D['data'].items():assert shared.array(t,k,99)==v,k
 for k,v in D['custom'].items():assert (raw[k]or[])==v,k
 assert len(raw['goals'])==5 and raw['moves']==28 and (raw['width'],raw['height'])==(9,11)
 fruit={x['originCellIndex']:(x['width'],x['height']) for x in raw['fruits']};assert list(fruit.values())==[(2,2),(2,2)]
 counts=Counter();occupied=set()
 for i,o in enumerate(D['data']['obstacles']):
  origin=D['data']['obstacleOrigins'][i]
  if not o:assert origin==-1;continue
  assert D['data']['cells'][i] and D['data']['obstacles'][origin]==o and D['data']['obstacleOrigins'][origin]==origin
  if i!=origin:continue
  counts[o]+=1;fw,fh=fruit[i] if o==102 else shared.footprint(o)
  cells={i+dy*9+dx for dy in range(fh) for dx in range(fw)};assert not cells&occupied;occupied|=cells
  for c in cells:assert D['data']['obstacles'][c]==o and D['data']['obstacleOrigins'][c]==i
 assert counts=={85:1,88:1,94:1,100:1,102:2,57:4},counts
 assert all(x['count']==6 for x in raw['dimerdals']) and sum(x['count'] for x in raw['dimerdals'])==18
 goals={(g['targetType'],g['obstacleId'],g['collectibleId']):g['amount'] for g in raw['goals']}
 assert goals=={(2,0,4):18,(1,102,0):2,(1,57,0):4,(1,58,0):6,(1,42,0):6}
 assert sum(o==57 for o in D['data']['obstacles'])==4 and all(D['data']['hamsterSatietyByCell'][i]==4 for i,o in enumerate(D['data']['obstacles']) if o==57)
 assert all(x['obstacleId']==58 and x['stackOrder']==1 for x in raw['stackedObstacles'])
 assert len(raw['stackedObstacles'])==6
 assert all(D['data']['cells'][e['originCellIndex']] for e in raw['stackedObstacles'])
 assert not any(D['data']['pinnedTileTypes']) and not any(D['data']['pinnedSpecialTypes'])
 phases=[]
 for removed in [[],[58],[58,57],[58,57,85,88,94],[58,57,85,88,94,100,102]]:
  slots,reach,seed=shared.feed(D,removed);assert slots==reach and not seed,(removed,slots-reach,seed)
  phases.append(dict(removed=removed,normalSlots=len(slots),naturallyFed=len(reach),empty=0))
 # Exact disconnected fruit island; all main-region obstacles have a normal/special access face.
 active={i for i,v in enumerate(D['data']['cells']) if v};components=[]
 while active:
  group={active.pop()};q=deque(group)
  while q:
   i=q.popleft()
   for j in [i-9,i+9]+([i-1]if i%9 else[])+([i+1]if i%9<8 else[]):
    if j in active:active.remove(j);group.add(j);q.append(j)
  components.append(sorted(group))
 assert sorted(map(len,components))==[4,69]
 island=next(c for c in components if len(c)==4);assert set(island)=={34,35,43,44}
 for origin in [36,54,72,85]:
  fw,fh=shared.footprint(D['data']['obstacles'][origin]);face=[]
  for y in range(origin//9,origin//9+fh):
   for x in range(origin%9,origin%9+fw):
    for nx,ny in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
     if 0<=nx<9 and 0<=ny<11:
      c=ny*9+nx
      if D['data']['cells'][c] and D['data']['obstacles'][c]==0:face.append(c)
  assert face,origin
 beforecat=(HERE/'Before'/CAT.relative_to(ROOT)).read_text();assert CAT.read_text().startswith(beforecat)
 assert re.search(r'    level: 232\n    levelKey: LevelCL_0232\n    levelData: \{fileID: 11400000, guid: '+D['guid'],CAT.read_text())
 before=json.loads((HERE/'Before'/NAMES.relative_to(ROOT)).read_text());names=json.loads(NAMES.read_text());assert names['entries'][:-1]==before['entries']
 assert names['entries'][-1]==dict(level=232,levelKey='LevelCL_0232',name='Kristal Hilal',nameEn='Crystal Crescent')
 for g in raw['goals']:assert g['iconOverride'].get('guid') and g['iconOverride']['fileID']!=0
 report=dict(result='PASS',preservedExistingProjectFiles=len(baseline)-2,changedExisting=['LevelCatalogProV1.asset','LevelNames.json'],changedLevelAssets=[232],user231Preserved=True,activeCells=73,components=list(map(len,components)),goals=raw['goals'],phases=phases,pins=0,simulationOrBuild=False,measuredBalance=False)
 (HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print('PASS232: layers,goals,footprints,feed and all previous levels preserved.',phases)

def foot(d,o,origin):
 if o==102:
  f=next(e for e in d['custom']['fruits'] if e['originCellIndex']==origin);return f['width'],f['height']
 return shared.footprint(o)
def flower(im,box,seed):
 art.paste(im,art.oid_art(42),box)
 x,y,w,h=box;rng=random.Random(seed)
 for n,(rx,ry) in enumerate([(.06,.15),(.47,.47),(.49,.01)]):
  p=art.resource('GrassFlower',f'Pansy_{rng.randrange(1,11):02d}');art.paste(im,p,(x+rx*w,y+ry*h,w*.44,h*.44))
def board(im,d,xy,cell,removed=(),opened=False):
 ls=shared.layers(d,removed);dr=ImageDraw.Draw(im);w=d['width'];slots,reach,seed=shared.feed(d,removed)
 for i,a in enumerate(d['data']['cells']):
  if a:
   x,y=xy[0]+i%w*cell,xy[1]+i//w*cell;dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=5,fill='#394f73',outline='#8fa6d2',width=2)
 rng=random.Random(d['level']);colors={}
 for i in sorted(reach|seed):
  candidates=list(range(4));rng.shuffle(candidates)
  for color in candidates:
   if not(i%w>=2 and colors.get(i-1)==colors.get(i-2)==color) and not(i//w>=2 and colors.get(i-w)==colors.get(i-w*2)==color):break
  colors[i]=color;art.paste(im,art.TILES[color],(xy[0]+i%w*cell,xy[1]+i//w*cell,cell,cell),cell*.08)
 seen=set()
 for i,plane in enumerate(ls):
  for o,origin in plane:
   if (o,origin) in seen:continue
   seen.add((o,origin));fw,fh=foot(d,o,origin);box=(xy[0]+origin%w*cell,xy[1]+origin//w*cell,fw*cell,fh*cell)
   if 84<=o<=95:old.cabinet(im,o,box,opened)
   elif o==100:
    art.paste(im,art.oid_art(o),box)
    for n in range(4):art.paste(im,art.oid_art(101),(box[0]+box[2]*(.10 if n%2==0 else .54),box[1]+box[3]*(.10 if n<2 else .54),box[2]*.36,box[3]*.36))
   elif o==58:flower(im,box,i)
   else:art.paste(im,art.oid_art(o),box,cell*.015)
 if d['level']==232:
  for origin in [36,54,72]:
   x,y=xy[0]+origin%w*cell,xy[1]+origin//w*cell
   dr.rounded_rectangle((x+cell*1.45,y+cell*1.6,x+cell*1.96,y+cell*1.95),radius=4,fill='#19283a')
   dr.text((x+cell*1.59,y+cell*1.62),'6',font=art.font(round(cell*.27),True),fill='#ffdd75')
def para(dr,t,x,y,width=47,size=19):return old.paragraph(dr,t,x,y,size,width)
def render():
 im=Image.new('RGBA',(1210,1270),'#172536');dr=ImageDraw.Draw(im)
 dr.text((30,20),'232 · KRİSTAL HİLAL',font=art.font(35,True),fill='#ffcf75')
 dr.text((30,72),'Normal · 28 hamle · 9×11 / 73 aktif hücre · 4 renk',font=art.font(22),fill='#e7edf6')
 dr.text((30,118),'AÇILIŞ',font=art.font(23,True),fill='#ffcf75');board(im,D,(30,160),65)
 dr.text((704,118),'ÇİÇEK VE ÇİM KALKINCA',font=art.font(20,True),fill='#ffcf75');board(im,D,(704,160),49,[58],True)
 y=728
 for t in ['Dış omurga: mavi / yeşil / kırmızı, kutu başına 6 elmas.','İç bahçe: çiçek → çim → dört hamster. Her biri 4 lokmayla yardım eder.','Ayrı adadaki 2×2 elma + dipteki 2×2 elma: tek kutunun 4 yumurtası yeter.']:
  y=para(dr,t,704,y,42,19)+13
 dr.text((30,928),'HEDEFLER',font=art.font(23,True),fill='#ffcf75')
 labels=['Elmas','Elma','Hamster','Çiçek','Çim']
 for j,(g,label) in enumerate(zip(D['goals'],labels)):
  x=30+j*236
  if label=='Çiçek':flower(im,(x,977,62,62),4)
  else:art.paste(im,art.sprite(art.ref(g['iconOverride'])),(x,977,62,62))
  dr.text((x+69,995),str(g['amount'])+' '+label,font=art.font(20,True),fill='#e7edf6')
 para(dr,'İlk karar: bahçeyi açıp hamster yardımını al veya dış omurgada aynı hatla birden fazla kutuyu ilerlet.',30,1080,113,21)
 dr.text((30,1175),'Gerçek proje sprite’ları · Taş renkleri temsili · Kesit: katman açıklaması, oynanmış an değil.',font=art.font(18),fill='#a9bdd1')
 dr.text((30,1210),'Statik/görsel kontrol ayrı; 28 hamle ilk ayar. Oynanış ve denge testi kullanıcıda.',font=art.font(18),fill='#a9bdd1')
 im.convert('RGB').save(ROOT/'Docs/LevelDesign/Level232-Kristal-Hilal.png')
 # Compare candidate with user231 and the two strongest historical geometry shortlists.
 inv=json.loads((ROOT/'Docs/LevelDesign/Reviews/232-Crystal-Crescent/Catalog-Inventory.json').read_text())
 comp=Image.new('RGBA',(1520,700),'#172536');cd=ImageDraw.Draw(comp)
 cd.text((20,20),'232 / 231 / 73 / 174 · Gerçek sprite karşılaştırması',font=art.font(27,True),fill='#ffcf75')
 for j,n in enumerate([232,231,73,174]):
  model=D if n==232 else copy.deepcopy(next(z for z in inv if z['level']==n))
  if n!=232:
   model['custom']['fruits']=load(ROOT/model['asset']).get('fruits')or[]
  x=20+j*375;cd.text((x,77),f'{n} · '+('Kristal Hilal'if n==232 else model['name']or'Kullanıcı leveli'),font=art.font(19,True),fill='#e7edf6');board(comp,model,(x,120),38)
 cd.text((20,590),'232: üç renkli omurga + çiçek/çim/hamster katmanı + iki 2×2 meyve.',font=art.font(22),fill='#e7edf6')
 cd.text((20,633),'Benzerlik skorları yalnız aday sıralar; benzersizlik veya oynanmış denge iddiası değildir.',font=art.font(20),fill='#a9bdd1')
 comp.convert('RGB').save(HERE/'Comparison.png');print('Rendered232opening/cutaway and231/73/174comparison.')
if __name__=='__main__':verify();render()
