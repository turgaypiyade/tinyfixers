# Wonder Fixers reklam görselleri (Google App kampanyası: 1200x628, 1200x1200, 1200x1500) — oyun art'ından birleştirme.
import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
OUT=sys.argv[1]; A="Art/UI/"
FONT="Fonts/TinyFixersPop/TinyFixersPop-Regular.ttf"
def L(p): return Image.open(p).convert("RGBA")
def crop(im,box): return im.crop(box)
wonder=crop(L(A+"Logos/LogoParts/wonderlogo.png"),(0,258,604,516))
fixers=crop(L(A+"Logos/LogoParts/FixersLogo.png"),(97,443,509,611))
def logo(width):
    w=wonder.resize((width,int(wonder.height*width/wonder.width)),Image.LANCZOS)
    fw=int(width*0.78); f=fixers.resize((fw,int(fixers.height*fw/fixers.width)),Image.LANCZOS)
    c=Image.new("RGBA",(width,w.height+f.height-int(f.height*0.22)),(0,0,0,0))
    c.alpha_composite(w,(0,0)); c.alpha_composite(f,((width-fw)//2,w.height-int(f.height*0.22)))
    return c
CH={"rabbit":(A+"Logos/LogoParts/RabitLogo.png",(117,0,491,404)),"pig":(A+"Logos/LogoParts/Piglogo.png",(83,6,509,433)),
    "bear":(A+"Logos/LogoParts/BearLogo.png",(97,12,579,426)),"monkey":(A+"WonderCharacters/WDImgs/MW_1.png",(76,19,1183,1232))}
def char(name,h):
    p,b=CH[name]; im=crop(L(p),b); return im.resize((int(im.width*h/im.height),h),Image.LANCZOS)
def cover(bg,W,H,focus=(0.5,0.45)):
    s=max(W/bg.width,H/bg.height); im=bg.resize((int(bg.width*s)+1,int(bg.height*s)+1),Image.LANCZOS)
    x=int((im.width-W)*focus[0]); y=int((im.height-H)*focus[1]); return im.crop((x,y,x+W,y+H))
def shade(im,top=True,bottom=True,strength=150):
    W,H=im.size; g=Image.new("L",(1,H))
    for y in range(H):
        t=0
        if bottom and y>H*0.62: t=max(t,(y-H*0.62)/(H*0.38))
        if top and y<H*0.22: t=max(t,(H*0.22-y)/(H*0.22)*0.7)
        g.putpixel((0,y),int(strength*t))
    dark=Image.new("RGBA",(W,H),(12,24,60,255)); dark.putalpha(g.resize((W,H)))
    out=im.copy(); out.alpha_composite(dark); return out
def text(im,xy,s,size,anchor="mm",fill=(255,255,255),stroke=(14,34,92),maxw=None):
    d=ImageDraw.Draw(im); f=ImageFont.truetype(FONT,size)
    while maxw and size>12 and d.textlength(s,font=f)+size*0.2>maxw:   # kutuya sığdır
        size-=2; f=ImageFont.truetype(FONT,size)
    d.text((xy[0],xy[1]+size*0.07),s,font=f,anchor=anchor,fill=(8,18,50),stroke_width=max(4,size//9),stroke_fill=(8,18,50))  # gölge
    d.text(xy,s,font=f,anchor=anchor,fill=fill,stroke_width=max(3,size//11),stroke_fill=stroke)
def cta(im,cx,cy,w,label):
    btn=L("Art/UI/ScreenImages/Chapter001/NewUI/GreenButonwoStroke.png"); h=int(btn.height*w/btn.width)
    b=btn.resize((w,h),Image.LANCZOS); im.alpha_composite(b,(cx-w//2,cy-h//2))
    text(im,(cx,cy-int(h*0.04)),label,int(h*0.40),stroke=(24,82,10),maxw=int(w*0.80))
def make(name,bgpath,W,H,head,sub,chars,focus=(0.5,0.45),lang="TR"):
    im=shade(cover(L(bgpath),W,H,focus))
    if W>H:   # yatay: solda logo+yazı+buton, sağda görsel
        col=int(W*0.40); x0=int(W*0.03); lw=col
        lg=logo(lw); im.alpha_composite(lg,(x0,int(H*0.05)))
        text(im,(x0+col//2,int(H*0.05)+lg.height+int(H*0.08)),head,int(H*0.085),maxw=col)
        cta(im,x0+col//2,int(H*0.84),int(col*0.80),sub)
    else:
        square = H <= W*1.05
        lw=int(W*(0.50 if square else 0.56)); lg=logo(lw)
        im.alpha_composite(lg,((W-lw)//2,int(H*0.03)))
        base = 0.70 if square else 0.74           # karakterlerin ayak hizası
        hh=int(H*(0.26 if square else 0.28))
        n=len(chars)
        for i,c in enumerate(chars):
            ci=char(c,hh if i in (1,2) else int(hh*0.86))
            px=int(W*0.02)+i*int(W*0.96/max(1,n))+(int(W*0.96/max(1,n))-ci.width)//2
            im.alpha_composite(ci,(px,int(H*base)-ci.height))
        text(im,(W//2,int(H*(base+0.06))),head,int(W*0.075),maxw=int(W*0.90))
        cta(im,W//2,int(H*(0.89 if square else 0.905)),int(W*0.42),sub)
    im.convert("RGB").save(f"{OUT}/{name}_{W}x{H}.jpg",quality=92)
for (W,H) in [(1200,1200),(1200,1500),(1200,628)]:
    make("A_Ayasofya","Art/UI/Missions/ND_M001/1/Ayasofya.png",W,H,"Harikaları Onar!","ÜCRETSİZ OYNA",[],focus=(0.5,0.30))
    make("A_Eyfel","Art/UI/Missions/ND_M001/1/EyfelV1.png",W,H,"Harikaları Onar!","ÜCRETSİZ OYNA",[],focus=(0.5,0.30))
    # Tinko (domuz) dini yapı sahnesinde kullanılmaz → ekip görseli Golden Gate önünde.
    make("B_Ustalar","Art/UI/Missions/ND_M001/1/GoldenGateV1.png",W,H,"Eşleştir, Patlat, Onar!","ÜCRETSİZ OYNA",["pig","rabbit","bear","monkey"],focus=(0.5,0.25))
print("ok")
