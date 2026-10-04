# Çatlak dokularında dinamit + fitil + metal bandı KORUMA maskesi (renk dönüşümü yalnız duvara uygulanır).
# POLY: dinamit sınırı (kaynak çizim piksel koordinatı); EXCLUDE: sınıra düşen duvar taşları.
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
# Dinamit + fitil sınırları (KiremitCatlak*.png 1268x1240 piksel koordinatı). İç = orijinal renk korunur.
POLY={
 "KiremitCatlak1.png":[(430,560),(520,450),(585,335),(600,245),(690,200),(800,205),(835,265),(900,255),(965,290),(975,345),(1005,420),(1012,520),(955,560),(940,640),(850,720),(760,780),(700,805),(600,790),(500,725),(430,660)],
 "KiremitCatlak3.png":[(560,255),(740,245),(800,215),(960,150),(1010,118),(1065,128),(1060,200),(1100,300),(1210,325),(1220,390),(1245,470),(1248,555),(1150,555),(1070,565),(1000,640),(950,720),(900,790),(850,860),(790,930),(720,975),(560,995),(420,1005),(330,985),(280,950),(280,600),(380,515),(480,415),(500,330)],
 "Kiremitcatlak4.png":[(470,290),(560,230),(640,225),(700,200),(790,150),(830,115),(860,130),(860,180),(790,215),(720,250),(760,265),(860,270),(950,280),(985,300),(985,330),(900,335),(820,345),(860,380),(940,400),(1005,410),(1010,470),(940,455),(860,450),(850,480),(800,550),(740,600),(700,660),(640,760),(580,835),(520,885),(430,885),(305,835),(205,735),(195,630),(230,550),(295,480),(390,365)],
 "KiremitCatlak2.png":[(590,370),(600,255),(700,245),(790,215),(980,140),(1030,175),(1000,230),(900,252),(885,290),(1080,268),(1160,288),(1170,330),(1060,352),(1052,378),(1150,368),(1252,478),(1242,532),(1140,482),(1062,445),(1060,560),(960,640),(880,720),(860,780),(720,850),(650,900),(520,900),(470,840),(480,640),(530,530)],
}
# Sınır içine düşen duvar taşları: her durumda bej'e boyanır.
EXCLUDE={
 "Kiremitcatlak4.png":[
   [(900,262),(1015,262),(1015,500),(900,500)],
   [(722,20),(1015,20),(1015,255),(865,255),(790,212)],
 ],
 "KiremitCatlak3.png":[
   [(880,232),(990,226),(1002,292),(900,302)],
   [(1065,318),(1238,318),(1242,470),(1065,470)],
   [(1000,20),(1240,20),(1240,312),(1062,312),(1000,215)],
   [(1060,540),(1252,540),(1252,600),(1060,600)],
 ],
 "KiremitCatlak1.png":[
   [(695,740),(735,676),(805,688),(824,730),(805,806),(745,810),(698,782)],
   [(848,660),(870,638),(908,650),(908,697),(870,703),(848,690)],
 ],
}

def keep_mask(name, size=(1268,1240), feather=4):
    m=Image.new("L",size,0)
    ImageDraw.Draw(m).polygon(POLY[name],fill=255)
    return np.asarray(m.filter(ImageFilter.GaussianBlur(feather))).astype(np.float32)/255

def dynamite_like(rgb):
    R,G,B=rgb[...,0],rgb[...,1],rgb[...,2]
    v=np.maximum(np.maximum(R,G),B); mn=np.minimum(np.minimum(R,G),B); s=(v-mn)/np.maximum(v,1e-3)
    d=np.maximum(v-mn,1e-6)
    hue=np.where(v==R,((G-B)/d)%6,np.where(v==G,(B-R)/d+2,(R-G)/d+4))*60
    crimson=(B>=G*0.6)&(R>G*1.8)&(v>0.18)
    metal=(s<0.3)&(v>0.1)&(v<0.85)
    rope=(hue>22)&(hue<55)&(s>0.12)&(s<0.72)&(v>0.38)
    return crimson|metal|rope

def rope_like(rgb):
    R,G,B=rgb[...,0],rgb[...,1],rgb[...,2]
    v=np.maximum(np.maximum(R,G),B); mn=np.minimum(np.minimum(R,G),B); s=(v-mn)/np.maximum(v,1e-3)
    d=np.maximum(v-mn,1e-6)
    hue=np.where(v==R,((G-B)/d)%6,np.where(v==G,(B-R)/d+2,(R-G)/d+4))*60
    return (hue>22)&(hue<55)&(s>0.12)&(s<0.72)&(v>0.38)

def keep_mask2(name, rgb, band=30, feather=3):
    H,W=rgb.shape[:2]
    full=Image.new("L",(W,H),0); ImageDraw.Draw(full).polygon(POLY[name],fill=255)
    poly=np.asarray(full)>127
    core=np.asarray(full.filter(ImageFilter.MinFilter(2*band+1)))>127   # sınırdan band px içerisi
    keep=core | (poly & dynamite_like(rgb))
    if name in EXCLUDE:
        ex=Image.new("L",(W,H),0); dr=ImageDraw.Draw(ex)
        for p in EXCLUDE[name]: dr.polygon(p,fill=255)
        # Taş bölgesinde yalnız taş boyanır; üstünden geçen ip fitil kendi renginde kalır.
        keep &= ~((np.asarray(ex)>127) & ~rope_like(rgb))
    m=Image.fromarray((keep*255).astype(np.uint8)).filter(ImageFilter.MedianFilter(5)).filter(ImageFilter.GaussianBlur(feather))
    return np.asarray(m).astype(np.float32)/255
