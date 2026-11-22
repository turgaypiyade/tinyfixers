"""Static actual-sprite board previews; tile colors illustrative, not a game simulation."""
from pathlib import Path
import importlib.util,json,re,random
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
spec=importlib.util.spec_from_file_location('existing_art',HERE.parent/'173-180/render_previews.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
ds=json.loads((HERE/'designs.json').read_text())
skin=(ROOT/'Assets/_Project/Resources/WaterChannelSkin.asset').read_text()
closed=[a.sprite(a.ref(v)) for v in re.findall(r'^  - (.*)$',skin.split('  closed:')[1].split('  open:')[0],re.M)]
source=a.sprite(a.ref(re.search(r'^  source: (.*)$',skin,re.M)[1]))
def sprite(o):return a.obstacle(o)
def drawboard(im,d,ox,oy,c):
 dr=ImageDraw.Draw(im);w=d['width'];path=d['water'];data=d['data'];rng=random.Random(d['level']);colors={}
 def box(i,fw=1,fh=1):return (ox+i%w*c,oy+i//w*c,c*fw,c*fh)
 def side(i,j):return 'L' if j==i-1 else 'R' if j==i+1 else 'T' if j==i-w else 'B'
 for i,on in enumerate(data['cells']):
  if not on:continue
  x,y,_,_=box(i);dr.rounded_rectangle((x+1,y+1,x+c-1,y+c-1),radius=6,fill='#38526b',outline='#9cb7cc',width=2)
  if i in path:
   k=path.index(i)
   if k==0:a.paste(im,source,box(i));continue
   edges={side(i,path[k-1]),side(i,path[k+1]) if k<len(path)-1 else 'B'}
   key='H' if edges=={'L','R'} else 'V' if edges=={'T','B'} else ''.join(t for t in 'LRTB' if t in edges)
   a.paste(im,closed[['H','V','LB','LT','RB','RT'].index(key)],box(i))
  if data['obstacles'][i]:continue
  choices=list(range(4));rng.shuffle(choices)
  for color in choices:
   if i%w>=2 and colors.get(i-1)==colors.get(i-2)==color:continue
   if i//w>=2 and colors.get(i-w)==colors.get(i-2*w)==color:continue
   break
  colors[i]=color;a.paste(im,a.TILES[color],box(i),c*.13)
 for i,o in enumerate(data['obstacles']):
  if not o or data['obstacleOrigins'][i]!=i:continue
  fw,fh=(2,2) if o in(100,102) else (1,1)
  a.paste(im,sprite(o),box(i,fw,fh),c*.025)
  if o==100:
   egg=Image.open(ROOT/'Assets/_Project/Resources/FlyFruit/Egg.png').convert('RGBA');x,y,bw,bh=box(i,2,2)
   for j in range(4):a.paste(im,egg,(x+bw*(.1 if j%2==0 else .54),y+bh*(.1 if j<2 else .54),bw*.36,bh*.36))
 if path:
  x,y,_,_=box(path[-1]);dr.line([(x+c/2,y+c),(x+c/2,y+c+24)],fill='#6ce8fb',width=5)
  dr.polygon([(x+c/2-8,y+c+18),(x+c/2+8,y+c+18),(x+c/2,y+c+30)],fill='#6ce8fb')

for d in ds:
 im=Image.new('RGBA',(850,900),'#17283b');dr=ImageDraw.Draw(im)
 dr.text((30,20),f"{d['level']} · {d['name']}",font=a.font(30,True),fill='#ffce78')
 dr.text((30,64),f"{d['width']}×{d['height']} · {d['moves']} hamle · Normal / ilk tanıtım",font=a.font(21),fill='white')
 drawboard(im,d,50,125,70)
 goal={4:'1 su kanalı · kaynak + 12 açılacak hücre · alt çıkış',13:'12 kuş · 2 sütun × 3 sıra kalıcı kuş evi',16:'2 elma · 1 kutu / 4 yumurta · ayrı 2×2 adacıklar'}[d['level']]
 dr.text((30,805),goal,font=a.font(22,True),fill='#ffce78')
 dr.text((30,855),'Gerçek sprite / temsili taş renkleri. Statik önizleme; oynanış testi değil.',font=a.font(17),fill='#b8cad9')
 im.convert('RGB').save(HERE/f"Level{d['level']:03d}.png")
contact=Image.new('RGB',(1530,540),'#17283b')
for k,d in enumerate(ds):
 im=Image.open(HERE/f"Level{d['level']:03d}.png");im.thumbnail((510,540));contact.paste(im,(k*510,0))
contact.save(HERE/'Overview.png')
print(HERE/'Overview.png')
