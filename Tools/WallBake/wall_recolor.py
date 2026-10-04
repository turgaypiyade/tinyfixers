# Duvar rengi dönüştürme: kiremit düz zemininin parlaklığına göre oranlayıp hedef renge boyar (gölge/parlama korunur).
import numpy as np
from PIL import Image
# Kiremit (orijinal düz zemin parlaklığı) → hedef renk: parlaklık oranını koruyarak boyar.
REF_RGB=np.array([178,56,25],np.float32)/255
LW=np.array([0.299,0.587,0.114],np.float32)
REF_L=float((REF_RGB*LW).sum())
def tint_rgb(rgb, color, highlight_slope=0.55):
    c=np.array(color,np.float32)/255
    lum=(rgb*LW).sum(-1,keepdims=True)
    x=lum/REF_L
    # Parlamalar beyaza patlamasın: düz zeminden (x=1) parlak tonlar yarı eğimle yükselir.
    x=np.where(x>1, 1+(x-1)*highlight_slope, x)
    return np.clip(x*c,0,1)
def maxf(m,r):
    o=m.copy()
    for dy in range(-r,r+1):
        for dx in range(-r,r+1):
            o|=np.roll(np.roll(m,dy,0),dx,1)
    return o
def minf(m,r): return ~maxf(~m,r)
def dynamite_mask(img, box):
    """img RGBA float; box (x0,y0,x1,y1) normalize — yalnız bu bölgede dinamit aranır."""
    rgb=img[...,:3]; H,W=rgb.shape[:2]
    hsv=np.asarray(Image.fromarray((rgb*255).astype(np.uint8)).convert("HSV")).astype(np.float32)/255
    h=hsv[...,0]*360; s=hsv[...,1]; v=hsv[...,2]
    R,G,B=rgb[...,0],rgb[...,1],rgb[...,2]
    # Dinamit saf kırmızı (B≈G), kiremit turuncu (B<<G): mavi/yeşil oranı ayırır (gölgeler dahil).
    red=(R>G*2.2)&(B>=G*0.75)&(R>0.2)
    metal=(s<0.3)&(v>0.12)&(v<0.75)
    rope=(h>24)&(h<50)&(s>0.15)&(s<0.7)&(v>0.5)
    cand=red|metal|rope
    x0,y0,x1,y1=[int(b*(W if i%2==0 else H)) for i,b in enumerate(box)]
    region=np.zeros_like(cand); region[y0:y1,x0:x1]=True
    cand&=region
    k=max(2,W//160)
    m=minf(maxf(cand,k),k)           # kapama: gövdedeki lekeleri doldur
    m=maxf(minf(m,k//2+1),k//2+1)    # açma: tek tük duvar pikselini at
    return m
def recolor_image(path_in, color, box=None):
    im=Image.open(path_in).convert("RGBA")
    a=np.asarray(im).astype(np.float32)/255
    out=a.copy()
    t=tint_rgb(a[...,:3],color)
    if box is None:
        out[...,:3]=t
    else:
        keep=dynamite_mask(a,box)[...,None]
        out[...,:3]=np.where(keep,a[...,:3],t)
    return Image.fromarray((out*255).astype(np.uint8),"RGBA")
