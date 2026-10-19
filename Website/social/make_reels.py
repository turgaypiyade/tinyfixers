"""Telefon ekran kayıtlarından Instagram Reels (1080x1920) üretir.

Kaynak: iPhone ekran kaydı (1320x2868, 60 fps, oyun sesi). Her klip: seçilen aralık, alttaki 40 px
"Development Build" şeridi kırpılır, oyun ortada + yanlar aynı görüntünün bulanık/koyu hali, ilk 3 sn
başlık kartı, köşede logo, ses -14 LUFS. Bildirim/Kontrol Merkezi içeren anlar aralık seçiminde dışarıda
bırakılır (her yeni kayıtta kontrol et — kişisel veri sızmasın). Kontrol Merkezi kaydı durdururken açılır:
bitişi onun açılış karesinden ≥0.4 sn önce tut. Logo sağdaki bulanık kenar boşluğunda (oyun UI'sına binmez).
Kullanım: python3 make_reels.py [anahtar ...]   (anahtarsız = hepsi)
"""
import pathlib, subprocess, sys
from PIL import Image, ImageDraw, ImageFilter
from make_posts import font, DYNA, NUNITO, logo_badge

HERE = pathlib.Path(__file__).resolve().parent
SRC = pathlib.Path.home() / 'Downloads'
OUT = pathlib.Path.home() / 'Movies' / 'WondiReels' / 'reels'
W, H = 1080, 1920
SUN = (255, 214, 64)

# Kaybedilen level'lar 'Hamle Kalmadı' popup'ından (satış teklifi) ÖNCE biter.
# Kurgu: AÇILIŞ (level'ın ilk, el değmemiş hali — tasarımı gösterir; başlık kartı bunun üstünde) + 0.3 sn
# geçiş + FİNAL (kombolar ve level sonu). Kullanıcı: "ilk sahneler levelin güzelliğini gösteriyor".
# anahtar: (kaynak kayıt, açılış sn, final bitiş sn, level, kanca cümlesi)
INTRO_S, FINAL_S, XFADE_S = 4.3, 19.0, 0.3
CLIPS = {
    'L011': ('17-53-48', 0.5, 74.5, 11, 'Clear it with style'),
    'L026': ('17-57-22', 0.3, 62.5, 26, 'Crack the crates!'),
    'L049': ('17-59-28', 3.4, 74.5, 49, 'So close... could YOU finish it?'),
    'L067': ('18-01-27', 4.4, 66.4, 67, 'Rocket party!'),
    'L098': ('18-02-57', 4.4, 78.5, 98, 'Wonder Hammer to the rescue'),
    'L114': ('18-04-55', 7.8, 77.9, 114, 'Flowers, grass and a lot of WOW'),
    'L139': ('18-08-02', 7.2, 63.0, 139, 'Out of moves! Could you do better?'),
    'L151': ('18-10-00', 6.8, 82.1, 151, 'One more move... or not?'),
}


def title_card(level: int, hook: str, path: pathlib.Path):
    img = Image.new('RGBA', (W, 420), (0, 0, 0, 0))
    band = Image.new('RGBA', (W, 420), (0, 0, 0, 0))
    ImageDraw.Draw(band).rounded_rectangle((70, 60, W - 70, 380), 48, fill=(12, 24, 70, 215))
    img.alpha_composite(band.filter(ImageFilter.GaussianBlur(1)))
    d = ImageDraw.Draw(img)

    def center(y, text, f, fill, stroke=0):
        w = d.textlength(text, font=f)
        d.text(((W - w) / 2, y), text, font=f, fill=fill, stroke_width=stroke, stroke_fill=(40, 20, 0))

    center(95, f'LEVEL {level}', font(DYNA, 120), SUN, stroke=5)
    f = font(NUNITO, 54)
    while d.textlength(hook, font=f) > W - 200: f = font(NUNITO, f.size - 2)
    center(265, hook, f, (255, 255, 255))
    img.save(path)


def render(key: str):
    rec, intro, end, level, hook = CLIPS[key]
    start = end - FINAL_S
    src = SRC / f'ScreenRecording_10-18-2026 {rec}_1.MP4'
    OUT.mkdir(parents=True, exist_ok=True)
    card = OUT / f'_{key}_title.png'; title_card(level, hook, card)
    badge = OUT / '_badge.png'; logo_badge(72).save(badge)
    dur = INTRO_S + FINAL_S - XFADE_S
    graph = (
        f'[0:v]fps=60,settb=AVTB[i0];[3:v]fps=60,settb=AVTB[i1];'
        f'[i0][i1]xfade=transition=fade:duration={XFADE_S}:offset={INTRO_S - XFADE_S}[cat];'
        f'[0:a][3:a]acrossfade=d={XFADE_S}[acat];'
        '[cat]crop=1320:2828:0:0,split=2[a][b];'
        f'[a]scale={W}:-2,crop={W}:{H},gblur=sigma=28,eq=brightness=-0.18:saturation=1.1[bg];'
        f'[b]scale=-2:{H}[fg];'
        '[bg][fg]overlay=(W-w)/2:0[v1];'
        "[1:v]format=rgba,fade=t=in:st=0.15:d=0.35:alpha=1,fade=t=out:st=2.7:d=0.4:alpha=1[t];"
        "[v1][t]overlay=0:150:enable='between(t,0,3.2)'[v2];"
        f'[v2][2:v]overlay=W-w-8:H-h-150,format=yuv420p[v];'
        f'[acat]afade=t=in:d=0.3,afade=t=out:st={dur - 0.7:.2f}:d=0.7,loudnorm=I=-14:TP=-1.5:LRA=11[aud]'
    )
    out = OUT / f'reel_{key}.mp4'
    cmd = ['ffmpeg', '-v', 'error', '-y', '-ss', f'{intro}', '-t', f'{INTRO_S}', '-i', str(src),
           '-loop', '1', '-t', '3.3', '-i', str(card), '-i', str(badge),
           '-ss', f'{start}', '-t', f'{FINAL_S}', '-i', str(src),
           '-filter_complex', graph, '-map', '[v]', '-map', '[aud]',
           '-c:v', 'libx264', '-preset', 'slow', '-crf', '19', '-profile:v', 'high', '-r', '60',
           '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-movflags', '+faststart', str(out)]
    subprocess.run(cmd, check=True)
    card.unlink(missing_ok=True)
    print('ok', out.name)


if __name__ == '__main__':
    for k in (sys.argv[1:] or CLIPS):
        render(k)
