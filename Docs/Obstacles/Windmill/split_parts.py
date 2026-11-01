from PIL import Image, ImageFilter, ImageChops, ImageDraw
import colorsys, math
from collections import deque
src=Image.open('images/59.png').convert('RGBA'); body=Image.open('images/60.png').convert('RGBA')
W,H=src.size; px=src.load(); cx,cy=628,593
panel={k:Image.new('L',(W,H),0) for k in 'BYRG'}; neutral=Image.new('L',(W,H),0); notbrown=Image.new('L',(W,H),0)
pp={k:panel[k].load() for k in panel}; npx=neutral.load(); nb=notbrown.load()
for y in range(H):
  for x in range(W):
    r,g,b,a=px[x,y]
    if a<128: continue
    h,l,s=colorsys.rgb_to_hls(r/255,g/255,b/255)
    if s<0.16 or l>0.9: npx[x,y]=255
    brown = (0.0<=h<0.13 or h>0.97) and s>0.12 and l<0.62 and not (s>0.55 and l>0.35)
    gold = 0.1<h<0.16 and s>0.6 and 0.3<l<0.75
    if not brown and not gold: nb[x,y]=255
    if s>0.45 and 0.12<l<0.92:
      q=('B' if 0.5<h<0.72 else 'Y' if 0.10<h<0.2 else 'R' if (h<0.045 or h>0.95) else 'G' if 0.2<h<0.42 else None)
      # çeyrek kısıtı: B sol-üst, Y sağ-üst, R sağ-alt, G sol-alt
      if q=='B' and not (x<cx and y<cy): q=None
      if q=='Y' and not (x>cx and y<cy-40): q=None
      if q=='R' and not (x>cx and y>cy): q=None
      if q=='G' and not (x<cx and y>cy): q=None
      if q: pp[q][x,y]=255
def largest(m):
    sm=m.resize((W//2,H//2)); p=sm.load(); w,h=sm.size; seen=set(); best=[]
    for y in range(h):
      for x in range(w):
        if p[x,y]>128 and (x,y) not in seen:
          comp=[]; dq=deque([(x,y)]); seen.add((x,y))
          while dq:
            a,b=dq.popleft(); comp.append((a,b))
            for na,nb in ((a+1,b),(a-1,b),(a,b+1),(a,b-1)):
              if 0<=na<w and 0<=nb<h and (na,nb) not in seen and p[na,nb]>128: seen.add((na,nb)); dq.append((na,nb))
          if len(comp)>len(best): best=comp
    out=Image.new('L',(w,h),0); op=out.load()
    for a,b in best: op[a,b]=255
    out=out.resize((W,H)).filter(ImageFilter.MaxFilter(3))
    return ImageChops.multiply(out, m.filter(ImageFilter.MaxFilter(5)))
def dil(m,n):
    for _ in range(n): m=m.filter(ImageFilter.MaxFilter(5))
    return m
hub=Image.new('L',(W,H),0); ImageDraw.Draw(hub).ellipse((cx-160,cy-160,cx+160,cy+160),fill=255)
collar=Image.new('L',(W,H),0); ImageDraw.Draw(collar).ellipse((cx-228,cy-228,cx+228,cy+228),fill=255)
hub=ImageChops.lighter(hub, ImageChops.multiply(collar, neutral))
hub=dil(hub,1).filter(ImageFilter.MinFilter(3))
for k in 'BYRG':
    p=largest(panel[k])
    p=p.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))       # parlama boşluklarını kapat
    # parlak beyaz highlight panel içinde → kapat: panelin dışbükey zarfına yakın doldurma
    p=dil(p,4); p=p.filter(ImageFilter.MinFilter(9)).filter(ImageFilter.MinFilter(9))
    rim=ImageChops.multiply(dil(p,24), notbrown)
    m=ImageChops.lighter(p, rim)
    m=m.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))
    m=ImageChops.subtract(m, hub.filter(ImageFilter.MinFilter(3)))
    m=m.filter(ImageFilter.GaussianBlur(0.8))
    layer=src.copy(); layer.putalpha(ImageChops.multiply(m, src.split()[-1].point(lambda v:255 if v>=200 else v)))
    layer.save(f'wm/Sail_{k}.png')
hl=src.copy(); hl.putalpha(ImageChops.multiply(hub.filter(ImageFilter.GaussianBlur(0.8)), src.split()[-1].point(lambda v:255 if v>=200 else v))); hl.save('wm/Hub.png')
b=body.copy(); b.putalpha(body.split()[-1].point(lambda v:255 if v>=200 else v)); b.save('wm/Body.png')
comp=b.copy()
for k in 'BYRG': comp.alpha_composite(Image.open(f'wm/Sail_{k}.png'))
comp.alpha_composite(hl); comp.save('wm/Composite.png')
broken=b.copy()
for k in 'BYG': broken.alpha_composite(Image.open(f'wm/Sail_{k}.png'))
broken.alpha_composite(hl); broken.save('wm/Composite_RedBroken.png')
sh=Image.new('RGBA',(W*2,H),(60,50,110,255)); sh.alpha_composite(comp,(0,0)); sh.alpha_composite(broken,(W,0)); sh.resize((W,H//2)).save('wm/check.png')
print('ok')
