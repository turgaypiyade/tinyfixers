#!/usr/bin/env python3
"""Play Store tanıtım videosu (yatay 1920x1080) — dikey reklam videosunu ortaya koyar.

python3 Tools/AdCreatives/make_landscape_promo.py [Kombo|Boss|IlkSeviyeler] ...   (proje kökünden; ffmpeg + Pillow)
Girdi: ~/Desktop/Wondi Paylaşım/Reklam_Video/WonderFixers_Ad_{name}_{TR|EN}_1080x1920.mp4 (make_video_ad.py çıktısı).
Düzen: arka plan = aynı videonun bulanık geniş hali; orta = dikey video; sol = logo + slogan; sağ = harika kartı.
Ses aynen korunur. Çıktı: WonderFixers_Promo_{name}_{lang}_1920x1080.mp4 (aynı klasör).
"""
import os, subprocess, sys, tempfile
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VID = os.path.expanduser("~/Desktop/Wondi Paylaşım/Reklam_Video")
W, H = 1920, 1080
os.chdir(os.path.join(ROOT, "Assets/_Project"))
src = open(os.path.join(ROOT, "Tools/AdCreatives/make_ads.py"), encoding="utf-8").read()
src = src[:src.index("for (W,H) in")].replace("OUT=sys.argv[1]; ", "")
helpers = {}
exec(compile(src, "make_ads_helpers", "exec"), helpers)
logo, text, L = helpers["logo"], helpers["text"], helpers["L"]

SLOGAN = {"TR": ("Harikaları Onar!", "Eşleştir · Patlat · Onar"), "EN": ("Restore the Wonders!", "Match · Blast · Restore")}
# Varyasyon başına sağdaki harika kartı: (görsel, kaynakta kırpma kutusu x0,y0,x1,y1)
CARD = {"Kombo": ("EyfelV1.png", (0, 380, 852, 1600)),
        "Boss": ("GoldenGateV1.png", (0, 560, 852, 1650)),
        "IlkSeviyeler": ("PissaV2.png", (60, 200, 940, 1400))}
FG_W = int(1080 * 1080 / 1920)   # dikey videonun genişliği (yükseklik 1080) ≈ 607
SIDE = (W - FG_W) // 2           # yan sütun genişliği ≈ 656


def rounded(im, r):
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, im.width - 1, im.height - 1), r, fill=255)
    out = Image.new("RGBA", im.size, (0, 0, 0, 0)); out.paste(im, (0, 0), mask); return out


def side_panels(name, lang, path):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # Yanları hafif koyulaştır: orta video öne çıksın, yazı okunsun.
    shade = Image.new("RGBA", (SIDE, H), (10, 22, 60, 110))
    im.alpha_composite(shade, (0, 0)); im.alpha_composite(shade, (W - SIDE, 0))
    # Sol: logo + slogan
    lw = int(SIDE * 0.80); lg = logo(lw)
    ly = int(H * 0.20); im.alpha_composite(lg, ((SIDE - lw) // 2, ly))
    head, sub = SLOGAN[lang]
    ty = ly + lg.height + int(H * 0.08)
    text(im, (SIDE // 2, ty), head, int(H * 0.068), maxw=int(SIDE * 0.88))
    text(im, (SIDE // 2, ty + int(H * 0.085)), sub, int(H * 0.040), fill=(255, 226, 120), maxw=int(SIDE * 0.88))
    # Sağ: harika kartı (yuvarlak köşe + beyaz çerçeve)
    f, box = CARD[name]
    art = L("Art/UI/Missions/ND_M001/1/" + f).crop(box)
    ch = int(H * 0.74); cw = int(art.width * ch / art.height)
    if cw > int(SIDE * 0.82):
        cw = int(SIDE * 0.82); ch = int(art.height * cw / art.width)
    card = rounded(art.resize((cw, ch), Image.LANCZOS), 36)
    frame = rounded(Image.new("RGBA", (cw + 16, ch + 16), (255, 255, 255, 235)), 44)
    cx = W - SIDE + (SIDE - frame.width) // 2; cy = (H - frame.height) // 2
    im.alpha_composite(frame, (cx, cy)); im.alpha_composite(card, (cx + 8, cy + 8))
    im.save(path)


def build(name, lang):
    src = os.path.join(VID, f"WonderFixers_Ad_{name}_{lang}_1080x1920.mp4")
    out = os.path.join(VID, f"WonderFixers_Promo_{name}_{lang}_1920x1080.mp4")
    with tempfile.TemporaryDirectory() as tmp:
        png = os.path.join(tmp, "side.png"); side_panels(name, lang, png)
        fc = (f"[0:v]scale={W}:-2,crop={W}:{H},boxblur=28:2,eq=brightness=-0.06[bg];"
              f"[0:v]scale=-2:{H}[fg];[bg][fg]overlay=(W-w)/2:0[v1];[v1][1:v]overlay=0:0,format=yuv420p[v]")
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", src, "-i", png, "-filter_complex", fc,
                        "-map", "[v]", "-map", "0:a?", "-c:v", "libx264", "-preset", "medium", "-crf", "19",
                        "-c:a", "copy", "-movflags", "+faststart", out], check=True)
    print("ok", out)


for n in (sys.argv[1:] or ["Kombo"]):
    for lang in ("EN", "TR"):
        build(n, lang)
