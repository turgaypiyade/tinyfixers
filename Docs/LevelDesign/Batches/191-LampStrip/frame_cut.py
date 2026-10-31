"""Lamba Şeridi — Figma çizimlerinden 11 çerçeve parçası.
Girdi: Lamba_Yatay_2048x512, Lamba_Dikey_512x2048, Lamba_Halka_1536x1536, Lamba_Tek_512x512 (hücre 512).
Çelik kenarı tek kurala bağlar: Figma'nın uzunluk boyunca gradyanı silinir (doğrusal eğilim çıkarılır),
sonra kesitine dik, sol-üst ışıklı gri tonlama uygulanır → tüm parçalar dikişsiz.
python3 frame_cut.py SRC_DIR OUT_DIR
"""
import sys, os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

C = 512; TARGET = 186.0

def rim_mask(im, seeds):
    w, h = im.size; px = im.load()
    gold = Image.new('L', im.size, 0); gp = gold.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a > 100 and r > 190 and g > 130 and b < 200 and r - b > 55: gp[x, y] = 255
    gold = gold.filter(ImageFilter.MaxFilter(5))
    fill = Image.new('L', im.size, 0); fill.paste(100, mask=gold)
    for s in seeds: ImageDraw.floodfill(fill, s, 255, thresh=0)
    alpha = im.split()[-1].point(lambda v: 255 if v >= 200 else 0)
    return ImageChops.multiply(fill.point(lambda v: 255 if v == 255 else 0), alpha)

def relief(mask, blur, d):
    h = mask.filter(ImageFilter.GaussianBlur(blur))
    a = ImageChops.offset(h, d, d); b = ImageChops.offset(h, -d, -d)
    return ImageChops.subtract(b, a, scale=1.0, offset=128)

def erode(mask, n):
    for _ in range(n): mask = mask.filter(ImageFilter.MinFilter(3))
    return mask

def shade(im, seeds, rivets):
    """Çelik kenarı Figma tonundan bağımsız yeniden boyar: sabit gri taban + sabit içte parlak çizgi
    + sol-üst ışıklı kesit profili. Perçinler ve ince koyu dış hatlar Figma'dan aynen kalır."""
    im = im.convert('RGBA'); w, h = im.size
    rim = rim_mask(im, seeds); rp = rim.load(); px = im.load()
    body = im.split()[-1].point(lambda v: 255 if v >= 128 else 0)
    hl = ImageChops.subtract(erode(body, 8), erode(body, 10)).load()      # dış kenardan ~17-20 px parlak çizgi
    inner = erode(rim, 2).load()                                         # kenar hattı dışındaki iç bölge
    r1 = relief(rim, 10, 8).load()
    r2 = relief(rim, 30, 20).load()
    out = im.copy(); op = out.load()
    for y in range(h):
        for x in range(w):
            if rp[x, y] < 200: continue
            if any((x - a) ** 2 + (y - b) ** 2 < 17 * 17 for a, b in rivets): continue
            r, g, b, a = px[x, y]
            if inner[x, y] < 200:                                            # 2 px kenar hattı: her parçada aynı koyu çizgi
                op[x, y] = (48, 53, 64, a); continue
            base = (226, 230, 238) if hl[x, y] > 128 else (170, 177, 189)
            m = 0.97 + 0.22 * (r1[x, y] - 128) / 128 + 0.22 * (r2[x, y] - 128) / 128
            op[x, y] = tuple(max(0, min(255, int(c * m))) for c in base) + (a,)
    # kanal: boyuna gradyanı sil (Figma dikeyde uçtan uca açılıyordu)
    m = n = sx = sy = sl = sxx = syy = sxy = sxl = syl = 0.0
    for y in range(0, h, 3):
        for x in range(0, w, 3):
            if rp[x, y] >= 200: continue
            r, g, b, a = px[x, y]; l = (r + g + b) / 3
            if a < 250 or not (16 < l < 40): continue
            n += 1; sx += x; sy += y; sl += l; sxx += x*x; syy += y*y; sxy += x*y; sxl += x*l; syl += y*l
    if n > 100:
        A = [[n, sx, sy, sl], [sx, sxx, sxy, sxl], [sy, sxy, syy, syl]]
        for i in range(3):
            p = A[i][i]
            for j in range(i, 4): A[i][j] /= p
            for k2 in range(3):
                if k2 != i:
                    f = A[k2][i]
                    for j in range(i, 4): A[k2][j] -= f * A[i][j]
        ca, cb, cc = A[0][3], A[1][3], A[2][3]
        for y in range(h):
            for x in range(w):
                if rp[x, y] >= 200: continue
                r, g, b, a = op[x, y]
                if a < 250 or (r + g + b) / 3 > 60: continue
                k = 21.0 / max(8.0, ca + cb * x + cc * y)
                op[x, y] = (min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), a)
    return out

def cut(img, cx, cy): return img.crop((cx*C, cy*C, cx*C + C, cy*C + C))

def main(src, out):
    os.makedirs(out, exist_ok=True)
    L = lambda n: Image.open(os.path.join(src, n + '.png'))
    H = shade(L('Lamba_Yatay_2048x512'), [(0, 0)],
              [(256 + 512 * k, y) for k in range(4) for y in (52, 460)] + [(52, 256), (1996, 256)])
    V = shade(L('Lamba_Dikey_512x2048'), [(0, 0)],
              [(x, 256 + 512 * k) for k in range(4) for x in (52, 460)] + [(256, 52), (256, 1996)])
    R = shade(L('Lamba_Halka_1536x1536'), [(0, 0), (768, 768)],
              [(256 + 512 * k, y) for k in range(3) for y in (52, 1484)] + [(x, 256 + 512 * k) for k in range(3) for x in (52, 1484)]
              + [(768, 460), (768, 1076), (460, 768), (1076, 768)])
    S = shade(L('Lamba_Tek_512x512'), [(0, 0)], [(256, 52), (256, 460), (52, 256), (460, 256)])
    P = {'H_Left': cut(H, 0, 0), 'H_Mid': cut(H, 1, 0), 'H_Right': cut(H, 3, 0), 'H_Single': S,
         'V_Top': cut(V, 0, 0), 'V_Mid': cut(V, 0, 1), 'V_Bottom': cut(V, 0, 3),
         'C_RightDown': cut(R, 0, 0), 'C_LeftDown': cut(R, 2, 0), 'C_RightUp': cut(R, 0, 2), 'C_LeftUp': cut(R, 2, 2)}
    for k, im in P.items(): im.save(os.path.join(out, f'LampStrip_{k}.png'))
    print('ok', len(P))

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
