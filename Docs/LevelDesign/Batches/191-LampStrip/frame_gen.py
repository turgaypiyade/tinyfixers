"""Lamba Şeridi çerçevesi — geometriden ışık hesaplayan prosedürel üretici (premium metal + içe çökük kanal).
Üç kaynak şekil (yatay 4×1, dikey 1×4, halka 3×3) hücre=512 ile çizilir, ışık/gölge şeklin bütününde hesaplanır,
sonra hücrelere kesilir → parçalar her şekilde dikişsiz. Işık sol üstten.
python3 frame_gen.py OUTDIR
"""
import sys, os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

C = 512; M = 32; R_OUT = 56; TRIM = 34; R_IN = 26; SOCK_R = 176; HOLE_R = 146

STEEL = (150, 160, 176); STEEL_HI = (246, 249, 255); STEEL_LO = (40, 46, 58)
CHAN = (30, 36, 60); CHROME = (196, 204, 216)

def rr(size, box, r):
    m = Image.new('L', size, 0); ImageDraw.Draw(m).rounded_rectangle(box, radius=r, fill=255); return m

def shapes(kind):
    if kind == 'H':
        size = (4*C, C); body = rr(size, (M, M, 4*C-M, C-M), R_OUT)
        chan = rr(size, (M+TRIM, M+TRIM, 4*C-M-TRIM, C-M-TRIM), R_IN)
        cells = [(i, 0) for i in range(4)]
    elif kind == 'V':
        size = (C, 4*C); body = rr(size, (M, M, C-M, 4*C-M), R_OUT)
        chan = rr(size, (M+TRIM, M+TRIM, C-M-TRIM, 4*C-M-TRIM), R_IN)
        cells = [(0, i) for i in range(4)]
    else:
        size = (3*C, 3*C)
        body = rr(size, (M, M, 3*C-M, 3*C-M), R_OUT)
        hole = rr(size, (C-M, C-M, 2*C+M, 2*C+M), 24)
        body = ImageChops.subtract(body, hole)
        chan = rr(size, (M+TRIM, M+TRIM, 3*C-M-TRIM, 3*C-M-TRIM), R_IN)
        chan = ImageChops.subtract(chan, rr(size, (C-M-TRIM, C-M-TRIM, 2*C+M+TRIM, 2*C+M+TRIM), 24+TRIM))
        cells = [(x, y) for y in range(3) for x in range(3) if (x, y) != (1, 1)]
    return size, body, chan, cells

def relief(mask, blur, d):
    """Sol-üst ışığa göre yön türevi: >0 aydınlık kenar, <0 gölgeli kenar (L, 128 nötr)."""
    h = mask.filter(ImageFilter.GaussianBlur(blur))
    a = ImageChops.offset(h, d, d); b = ImageChops.offset(h, -d, -d)
    return ImageChops.subtract(a, b, scale=1.0, offset=128)

def tint(size, rgb):
    return Image.new('RGB', size, rgb)

def light_layers(base, rel, hi, lo, gain):
    """rel'deki >128 kısmı hi rengine, <128 kısmı lo rengine doğru karıştır."""
    up = rel.point(lambda v: max(0, min(255, int((v-128)*gain))))
    dn = rel.point(lambda v: max(0, min(255, int((128-v)*gain))))
    out = Image.composite(tint(base.size, hi), base, up)
    return Image.composite(tint(base.size, lo), out, dn)

def render(kind):
    size, body, chan, cells = shapes(kind)
    W, H = size
    # metal gövde: taban + geniş eğim + keskin kenar parıltısı
    metal = tint(size, STEEL)
    metal = light_layers(metal, relief(body, 9, 4), STEEL_HI, STEEL_LO, 5.0)
    metal = light_layers(metal, relief(body, 2, 1), (255, 255, 255), (20, 24, 32), 3.0)
    # kanal: koyu iç + üst-sol içe düşen gölge + alt-sağ ince ışık dönüşü
    inv = ImageChops.invert(chan)
    shade = ImageChops.offset(inv.filter(ImageFilter.GaussianBlur(14)), 9, 12)
    bounce = ImageChops.offset(inv.filter(ImageFilter.GaussianBlur(4)), -4, -5)
    ch = tint(size, CHAN)
    ch = Image.composite(tint(size, (4, 6, 12)), ch, ImageChops.multiply(shade, chan).point(lambda v: int(v*0.85)))
    ch = Image.composite(tint(size, (110, 124, 160)), ch, ImageChops.multiply(bounce, chan).point(lambda v: int(v*0.45)))
    # kanal ağzındaki dudak: metal gövde ile kanal sınırında ince koyu çizgi
    lip = ImageChops.subtract(chan.filter(ImageFilter.MaxFilter(5)), chan)
    img = Image.composite(ch, metal, chan)
    img = Image.composite(tint(size, (12, 14, 20)), img, lip.point(lambda v: int(v*0.7)))
    alpha = body
    # yuvalar + perçinler
    socket = Image.new('L', size, 0); hole = Image.new('L', size, 0); riv = Image.new('L', size, 0)
    ds, dh, dr = ImageDraw.Draw(socket), ImageDraw.Draw(hole), ImageDraw.Draw(riv)
    for (cx, cy) in cells:
        x0, y0 = cx*C + C//2, cy*C + C//2
        ds.ellipse((x0-SOCK_R, y0-SOCK_R, x0+SOCK_R, y0+SOCK_R), fill=255)
        dh.ellipse((x0-HOLE_R, y0-HOLE_R, x0+HOLE_R, y0+HOLE_R), fill=255)
    band = M + TRIM//2
    def rivet(x, y):
        dr.ellipse((x-11, y-11, x+11, y+11), fill=255)
    for (cx, cy) in cells:
        for o in (128, 384):
            for (x, y) in [(cx*C+o, band), (cx*C+o, H-band), (band, cy*C+o), (W-band, cy*C+o)]:
                if body.getpixel((x, y)) > 200 and chan.getpixel((x, y)) < 50: rivet(x, y)
            if kind == 'R':
                for (x, y) in [(cx*C+o, C-M-TRIM//2), (cx*C+o, 2*C+M+TRIM//2), (C-M-TRIM//2, cy*C+o), (2*C+M+TRIM//2, cy*C+o)]:
                    if 0 <= x < W and 0 <= y < H and body.getpixel((x, y)) > 200 and chan.getpixel((x, y)) < 50: rivet(x, y)
    chrome = light_layers(tint(size, CHROME), relief(socket, 6, 3), (255, 255, 255), (60, 66, 78), 5.0)
    chrome = light_layers(chrome, relief(riv, 3, 2), (255, 255, 255), (50, 56, 66), 4.0)
    sock_shadow = ImageChops.offset(socket.filter(ImageFilter.GaussianBlur(8)), 4, 6)
    img = Image.composite(tint(size, (0, 0, 0)), img, ImageChops.multiply(sock_shadow, ImageChops.subtract(chan, socket)).point(lambda v: int(v*0.5)))
    img = Image.composite(chrome, img, socket)
    hinv = ImageChops.invert(hole)
    hshade = ImageChops.offset(hinv.filter(ImageFilter.GaussianBlur(12)), 7, 10)
    hc = Image.composite(tint(size, (0, 0, 0)), tint(size, (14, 16, 24)), ImageChops.multiply(hshade, hole).point(lambda v: int(v*0.9)))
    img = Image.composite(hc, img, hole)
    riv_sh = ImageChops.offset(riv.filter(ImageFilter.GaussianBlur(2)), 1, 2)
    img = Image.composite(tint(size, (20, 22, 28)), img, ImageChops.subtract(riv_sh, riv).point(lambda v: int(v*0.6)))
    img = Image.composite(chrome, img, riv)
    out = img.convert('RGBA'); out.putalpha(alpha)
    # tahtaya düşen kısa gölge (gövdenin altında, 32 px boşluk içinde)
    sh = ImageChops.offset(body.filter(ImageFilter.GaussianBlur(7)), 0, 6).point(lambda v: int(v*0.5))
    shadow = Image.new('RGBA', size, (0, 0, 0, 0)); shadow.putalpha(sh)
    shadow.alpha_composite(out)
    return shadow

def cut(img, x, y): return img.crop((x*C, y*C, x*C+C, y*C+C))

def main(outdir):
    os.makedirs(outdir, exist_ok=True)
    Hs, Vs, Rs = render('H'), render('V'), render('R')
    P = {'H_Left': cut(Hs, 0, 0), 'H_Mid': cut(Hs, 1, 0), 'H_Right': cut(Hs, 3, 0),
         'V_Top': cut(Vs, 0, 0), 'V_Mid': cut(Vs, 0, 1), 'V_Bottom': cut(Vs, 0, 3),
         'C_RightDown': cut(Rs, 0, 0), 'C_LeftDown': cut(Rs, 2, 0), 'C_RightUp': cut(Rs, 0, 2), 'C_LeftUp': cut(Rs, 2, 2)}
    single = Image.new('RGBA', (C, C)); single.paste(P['H_Left'].crop((0, 0, C//2, C)), (0, 0))
    single.paste(P['H_Right'].crop((C//2, 0, C, C)), (C//2, 0)); P['H_Single'] = single
    for n, im in P.items(): im.save(os.path.join(outdir, f'LampStrip_{n}.png'))
    print('ok', outdir)

if __name__ == '__main__':
    main(sys.argv[1])
