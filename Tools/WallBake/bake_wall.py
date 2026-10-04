# Duvar (Wall) ve Metal Duvar (MetalWall) sprite'larını kiremit.png + kullanıcının detay çizimlerinden
# oyun ölçüsüne pişirir.
# Çıktı: Assets/_Project/Resources/Wall/ — runtime Resources.Load<Sprite>("Wall/<önek><ad>").
# Çalıştır: python3 Tools/WallBake/bake_wall.py   (kiremit / kabartma / çatlak çizimleri değişince)
# Gereken: pip install numpy pillow
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
os.environ.setdefault('R0','160')
from wall_concave import concave, C        # C = 1212 kaynak hücre
from wall_recolor import tint_rgb
from wall_dynamite_mask import keep_mask2

ART="/Users/turgayp/Development Projects/Unity/tinyfixers/Assets/_Project/Art/UI/Obstacles/RobotStyle/Wall/"
OUT="/Users/turgayp/Development Projects/Unity/tinyfixers/Assets/_Project/Resources/Wall/"
os.makedirs(OUT, exist_ok=True)
CELL=512                     # oyun hücresi çözünürlüğü
SC=CELL/C
H=C//2
R0=int(os.environ['R0'])
# kiremit.png kenar bantları (1212 px) ve detay ölçeği — WallPieceView ile aynı olmalı.
RIM_L,RIM_R,RIM_T,RIM_B=106,98,131,94
DETAIL_SCALE=0.85

# Türler: (sprite öneki, renk, parlama eğimi, kullanılan çatlaklar, editör/hedef önizleme dosyası)
#  - Wall: bej taş (kullanıcı 2026-10-04). Parlamalar yumuşak (0.55) → beyaza patlamaz.
#  - MetalWall: metalik gri; parlamalar daha sert (0.9) → metal parlaklığı.
KINDS=[
    ("Wall",      (205,186,160), 0.55, ["Crack1","Crack2","Crack3","Crack4"], "kiremit_bej.png"),
    ("MetalWall", (148,154,163), 0.90, ["Crack1","Crack3"],          "kiremit_metal.png"),
]
CRACK_SRC={"Crack1":"KiremitCatlak1.png","Crack2":"KiremitCatlak2.png","Crack3":"KiremitCatlak3.png",
           "Crack4":"Kiremitcatlak4.png"}

def recolor(img, color, slope, dynamite_name=None):
    a=np.asarray(img.convert("RGBA")).astype(np.float32)/255
    t=tint_rgb(a[...,:3],color,slope)
    if dynamite_name is None:
        a[...,:3]=t
    else:
        k=keep_mask2(dynamite_name,a[...,:3])[...,None]
        a[...,:3]=a[...,:3]*k+t*(1-k)
    return Image.fromarray((a*255).astype(np.uint8),"RGBA")

def save(img, name, size):
    img.resize(size, Image.LANCZOS).save(OUT+name+".png", optimize=True)

kir_src=Image.open(ART+"kiremit.png").convert("RGBA").resize((C,C),Image.LANCZOS)
concaves={k:concave(k,R0) for k in ["TL","TR","BL","BR"]}

for prefix,color,slope,cracks,preview in KINDS:
    kir=recolor(kir_src,color,slope)
    q=lambda x,y: kir.crop((x,y,x+H,y+H))
    Q=(CELL//2, CELL//2)
    save(q(0,0),prefix+"Q_OuterTL",Q); save(q(H,0),prefix+"Q_OuterTR",Q)
    save(q(0,H),prefix+"Q_OuterBL",Q); save(q(H,H),prefix+"Q_OuterBR",Q)
    save(q(H//2,0),prefix+"Q_EdgeTop",Q); save(q(H//2,H),prefix+"Q_EdgeBottom",Q)
    save(q(0,H//2),prefix+"Q_EdgeLeft",Q); save(q(H,H//2),prefix+"Q_EdgeRight",Q)
    save(q(H//2,H//2),prefix+"Q_Inner",Q)
    for k,(img,(vx,vy)) in concaves.items():
        n=img.width; assert (vx,vy)==(n//2,n//2), (k,vx,vy,n)   # köşe noktası tam merkezde
        s=int(round(n*SC)); s+=s%2
        save(recolor(img,color,slope),prefix+"Concave_"+k,(s,s))
    for src,name in [("kiremitkabartma1.png","Kabartma1"),("kiremitkabartma2.png","Kabartma2")]:
        save(recolor(Image.open(ART+src),color,slope),prefix+"Detail_"+name,(CELL,CELL))
    for c in cracks:
        save(recolor(Image.open(ART+CRACK_SRC[c]),color,slope,CRACK_SRC[c]),prefix+"Detail_"+c,(CELL,CELL))
    # Editör paleti / hedef çubuğu / ipucu ikonu (ObstacleLibrary sprite'ı): oyundaki tek hücre gibi —
    # kenarlı kiremit + iç düz alanın %85'i boyutunda, alanın merkezinde kabartma (WallPieceView.DetailRect).
    cell=recolor(Image.open(ART+"kiremit.png"),color,slope).resize((CELL,CELL),Image.LANCZOS)
    kab=recolor(Image.open(ART+"kiremitkabartma1.png"),color,slope)
    u0,u1=RIM_L/C,1-RIM_R/C; v0,v1=RIM_T/C,1-RIM_B/C
    dw=int(round((u1-u0)*DETAIL_SCALE*CELL)); dh=int(round((v1-v0)*DETAIL_SCALE*CELL))
    cx=(u0+u1)/2*CELL; cy=(v0+v1)/2*CELL
    cell.alpha_composite(kab.resize((dw,dh),Image.LANCZOS),(int(round(cx-dw/2)),int(round(cy-dh/2))))
    cell.save(ART+preview,optimize=True)
    print("baked",prefix)
print("ok")
