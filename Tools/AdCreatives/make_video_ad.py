#!/usr/bin/env python3
"""Wonder Fixers dikey reklam videosu (1080x1920, ~17 sn) — oynanış kesitleri + görev (harika onarımı) anı + kapanış.

python3 Tools/AdCreatives/make_video_ad.py <çıktı klasörü>   (proje kökünden; ffmpeg + Pillow gerekir)
Ham kayıtlar: Videos/*.MP4 (1320x2868, iPhone ekran kaydı). Kişisel bildirim içeren bölümler seçilmez.
TR ve EN sürüm + statik kapak (thumbnail) üretir. Oyun müziği kayıttaki gibi kalır; lisans reklam kullanımına uygun olmalı.
"""
import glob, os, shutil, subprocess, sys, tempfile
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser("~/Desktop/Wondi Paylaşım/Reklam_Video"))
# Kullanım: make_video_ad.py <çıktı> [Kombo] [Boss] [IlkSeviyeler]  (varyasyon verilmezse hepsi)
W, H, FPS = 1080, 1920, 30
os.makedirs(OUT, exist_ok=True)

# make_ads.py yardımcıları (logo, yazı, buton, cover) — aynı görsel dil.
os.chdir(os.path.join(ROOT, "Assets/_Project"))
src = open(os.path.join(ROOT, "Tools/AdCreatives/make_ads.py"), encoding="utf-8").read()
src = src[:src.index("for (W,H) in")].replace("OUT=sys.argv[1]; ", "")
helpers = {}
exec(compile(src, "make_ads_helpers", "exec"), helpers)
logo, text, cta, cover, L = helpers["logo"], helpers["text"], helpers["cta"], helpers["cover"], helpers["L"]

OLD = sorted(glob.glob(os.path.join(ROOT, "Videos", "*.MP4")))
NEW = sorted(glob.glob(os.path.expanduser("~/Desktop/Wondi Paylaşım/Reklam_Video/ScreenRecording*.MP4")))
COMMON = {
    "TR": dict(wonder="Harikaları Onar!", sub="Görevleri tamamla", cta="ÜCRETSİZ OYNA", note="İnternetsiz de oynanır!"),
    "EN": dict(wonder="Restore the Wonders!", sub="Complete the missions", cta="PLAY FREE", note="Play offline, anytime!"),
}
# Varyasyon: (kaynak listesi, video no, başlangıç sn, süre, TR yazı, EN yazı). None yazı = üstüne yazı yok (VS girişi).
# Hareketli anlar ölçülüp elle doğrulandı; bildirim/popup/test reklamı içeren bölümler seçilmez.
# Ritim (kullanıcı “çok seri, nefes yok”): 3,5–4 sn sahneler, crossfade geçiş, büyük anda ağır çekim (speed).
# Klip: (kaynak, video no, başlangıç sn, KAYNAK süre, TR, EN, {isteğe bağlı: speed, zoom})
VARIANTS = {
    # Kombo: kuş yumurtası çatlıyor → kuşlar uçuyor (sesleriyle), ardından TNT zinciri.
    "Kombo": dict(mission=True, clips=[
        (NEW, 4, 14.2, 4.0, "Kuşları Kurtar!", "Free the Birds!"),
        (NEW, 4, 20.8, 3.0, "Patlat!", "Blast!", dict(speed=0.7)),
        (OLD, 1, 115.2, 3.6, "Zincirle!", "Chain it!")]),
    # Boss: tek kesintisiz düello anı — hamle → eşleşme → güç dolar → vuruş → dalga (kullanıcı: süreç tam görünsün).
    "Boss": dict(mission=False, clips=[
        (NEW, 7, 2.9, 2.4, None, None),
        (NEW, 7, 11.2, 7.6, "Eşleştir, Güç Topla, Vur!", "Match, Charge, Strike!"),
        (NEW, 7, 31.6, 3.4, "Sinekler Elmaya!", "Flies Attack!")]),
    "IlkSeviyeler": dict(mission=True, clips=[
        (NEW, 8, 12.8, 4.0, "Eşleştir!", "Match!"), (NEW, 8, 63.2, 3.0, "Patlat!", "Blast!", dict(speed=0.75)),
        (NEW, 8, 85.6, 4.4, "Kazan!", "Win!")]),
}
# 1320x2868 kayıtta arena (karakterler) ~y 450–950: 1000x1778 pencere → 1080x1920 (~1.3x yakınlaşma).
ZOOMS = {"ARENA_ZOOM": (160, 300, 1000, 1778)}
EYFEL = "Art/UI/Missions/ND_M001/1/EyfelV1.png"
EYFEL_BLUR = "Art/UI/Missions/BlurredPreviews/mis3-blur.png"
END_BG = "Art/UI/Missions/ND_M001/1/GoldenGateV1.png"


def run(*cmd):
    subprocess.run(cmd, check=True)


def overlay_png(path, caption=None, with_logo=False):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    if with_logo:
        lg = logo(int(W * 0.46)); im.alpha_composite(lg, ((W - lg.width) // 2, int(H * 0.015)))
    if caption:
        text(im, (W // 2, int(H * 0.86)), caption, int(W * 0.13), maxw=int(W * 0.9))
    im.save(path)


XFADE = 0.35   # sahneler arası yumuşak geçiş (sn)


def gameplay(i, path, start, dur, png, tmp, zoom=None, speed=1.0):
    out = os.path.join(tmp, f"g{i}.mp4")
    # 1320x2868 → genişlik 1080 (yükseklik 2347); durum çubuğunu ve alttaki joker barını keserek 1920'lik pencere.
    frame = f"scale={W}:-2,crop={W}:{H}:0:90"
    if zoom:
        x, y, w, h = ZOOMS[zoom]; frame = f"crop={w}:{h}:{x}:{y},scale={W}:{H}"
    out_dur = dur / speed
    vf = f"{frame},setpts=PTS/{speed},fps={FPS},format=yuv420p[v];[v][1:v]overlay=0:0"
    af = (f"atempo={speed}," if speed != 1.0 else "") + f"aresample=44100,afade=t=out:st={out_dur-0.2:.3f}:d=0.2"
    run("ffmpeg", "-v", "error", "-y", "-ss", str(start), "-t", str(dur), "-i", path, "-i", png,
        "-filter_complex", vf, "-af", af,
        "-c:v", "libx264", "-preset", "medium", "-crf", "19", "-c:a", "aac", "-ac", "2", out)
    return out


def frames_to_clip(frames_dir, n, name, tmp):
    out = os.path.join(tmp, name)
    run("ffmpeg", "-v", "error", "-y", "-framerate", str(FPS), "-i", os.path.join(frames_dir, "%04d.png"),
        "-f", "lavfi", "-t", f"{n / FPS:.3f}", "-i", "anullsrc=r=44100:cl=stereo",
        "-c:v", "libx264", "-preset", "medium", "-crf", "19", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", out)
    return out


def mission(t, tmp):
    """Bulanık/paslı harika aşağıdan yukarı onarılıyor — görev tamamlama anı."""
    d = os.path.join(tmp, "mission"); os.makedirs(d, exist_ok=True)
    full = cover(L(EYFEL), W, H, (0.5, 0.35)).convert("RGB")
    blur = cover(L(EYFEL_BLUR).resize((L(EYFEL).width, L(EYFEL).height), Image.BICUBIC), W, H, (0.5, 0.35)).convert("RGB")
    n = int(FPS * 2.8)
    for k in range(n):
        p = min(1.0, k / (FPS * 2.0)); p = p * p * (3 - 2 * p)
        edge = int(H * (1.08 - 1.16 * p))          # onarım cephesi alttan yukarı
        mask = Image.new("L", (1, H))
        for y in range(H):
            mask.putpixel((0, y), max(0, min(255, int((y - edge) * 255 / 140))))
        frame = Image.composite(full, blur, mask.resize((W, H))).convert("RGBA")
        text(frame, (W // 2, int(H * 0.13)), t["wonder"], int(W * 0.10), maxw=int(W * 0.92))
        text(frame, (W // 2, int(H * 0.20)), t["sub"], int(W * 0.06), fill=(255, 226, 120), maxw=int(W * 0.9))
        frame.convert("RGB").save(os.path.join(d, f"{k:04d}.png"))
    return frames_to_clip(d, n, "mission.mp4", tmp)


def end_card(t, tmp):
    d = os.path.join(tmp, "end"); os.makedirs(d, exist_ok=True)
    bg = L(END_BG); n = int(FPS * 2.6)
    for k in range(n):
        z = 1.0 + 0.04 * k / n                       # hafif yakınlaşma
        im = cover(bg, int(W * z), int(H * z), (0.5, 0.4)).crop((int(W * (z - 1) / 2), int(H * (z - 1) / 2),
                                                               int(W * (z - 1) / 2) + W, int(H * (z - 1) / 2) + H))
        im = im.convert("RGBA")
        shade = Image.new("RGBA", (W, H), (10, 22, 60, 0)); shade.putalpha(Image.linear_gradient("L").resize((W, H)).point(lambda v: int(v * 0.55)))
        im.alpha_composite(shade)
        lg = logo(int(W * 0.66)); im.alpha_composite(lg, ((W - lg.width) // 2, int(H * 0.06)))
        text(im, (W // 2, int(H * 0.79)), t["note"], int(W * 0.06), fill=(255, 226, 120), maxw=int(W * 0.9))
        cta(im, W // 2, int(H * 0.88), int(W * 0.62), t["cta"])
        im.convert("RGB").save(os.path.join(d, f"{k:04d}.png"))
    return frames_to_clip(d, n, "end.mp4", tmp)


def build(name, variant, lang):
    t = COMMON[lang]
    with tempfile.TemporaryDirectory(dir=OUT) as tmp:
        parts = []
        for i, (src, v, s, dur, tr, en, *opt) in enumerate(variant["clips"]):
            o = opt[0] if opt else {}
            caption = tr if lang == "TR" else en
            png = os.path.join(tmp, f"o{i}.png"); overlay_png(png, caption, with_logo=(i == 0 and caption is not None))
            parts.append(gameplay(i, src[v - 1], s, dur, png, tmp, o.get("zoom"), o.get("speed", 1.0)))
        if variant["mission"]:
            parts.append(mission(t, tmp))
        parts.append(end_card(t, tmp))
        out = os.path.join(OUT, f"WonderFixers_Ad_{name}_{lang}_1080x1920.mp4")
        durs = [float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", p],
                                     capture_output=True, text=True).stdout) for p in parts]
        args = ["ffmpeg", "-v", "error", "-y"]
        for p in parts: args += ["-i", p]
        fc, v, a_, tt = [], "[0:v]", "[0:a]", durs[0]
        for k in range(1, len(parts)):
            off = tt - XFADE
            fc.append(f"{v}[{k}:v]xfade=transition=fade:duration={XFADE}:offset={off:.3f}[v{k}]")
            fc.append(f"{a_}[{k}:a]acrossfade=d={XFADE}[a{k}]")
            v, a_, tt = f"[v{k}]", f"[a{k}]", tt + durs[k] - XFADE
        args += ["-filter_complex", ";".join(fc), "-map", v, "-map", a_,
                 "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-r", str(FPS),
                 "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", out]
        run(*args)
        # Kapak / thumbnail: harika + logo + slogan
        cov = cover(L(EYFEL), W, H, (0.5, 0.35)).convert("RGBA")
        lg = logo(int(W * 0.62)); cov.alpha_composite(lg, ((W - lg.width) // 2, int(H * 0.05)))
        text(cov, (W // 2, int(H * 0.80)), t["wonder"], int(W * 0.10), maxw=int(W * 0.92))
        cta(cov, W // 2, int(H * 0.89), int(W * 0.60), t["cta"])
        cov.convert("RGB").save(os.path.join(OUT, f"WonderFixers_Cover_{lang}_1080x1920.jpg"), quality=92)
        print("ok", out)


only = sys.argv[2:] or list(VARIANTS)
for name in only:
    for lang in ("TR", "EN"):
        build(name, VARIANTS[name], lang)
