"""Deterministic project-sprite previews. Empty starting pockets are shown honestly."""
from pathlib import Path
import ast,importlib.util,math,random,re,sys
from PIL import Image,ImageDraw
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sys.path.insert(0,str(HERE));sys.path.insert(0,str(ROOT/'Tools'))
from designs import DESIGNS
from audit import surface,entry,check
spec=importlib.util.spec_from_file_location('project_art',HERE.parent/'173-180/render_previews.py')
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
# Reuse the existing repo's deterministic Seal and 4-3-2 PaintCanBox artwork assembly.
scope=dict(r=r,Image=Image,ImageDraw=ImageDraw,ROOT=ROOT,re=re,math=math)
for file,names in [('188/render_preview.py',('sliced_body','seal_art')),('184/render_preview.py',('paint_box',))]:
    nodes=[n for n in ast.parse((HERE.parent/file).read_text()).body if isinstance(n,ast.FunctionDef) and n.name in names]
    exec(compile(ast.Module(body=nodes,type_ignores=[]),file,'exec'),scope)
seal_art=scope['seal_art'];paint_art=scope['paint_box']()
skin=(ROOT/'Assets/_Project/Resources/WaterChannelSkin.asset').read_text()
def sprites(key):return [r.sprite(r.ref(line)) for line in re.search(r'^  '+key+r':\n((?:  - .+\n)+)',skin,re.M)[1].splitlines()]
CLOSED,OPEN,WATER=[sprites(k) for k in ('closed','open','water')]
SOURCE=r.sprite(r.ref(re.search(r'^  source: (.*)$',skin,re.M)[1]));SHAPES=['H','V','LB','LT','RB','RT']
def side(a,b):return 'L' if b%9<a%9 else 'R' if b%9>a%9 else 'T' if b//9<a//9 else 'B'
def shape(a,b):
    edges={a,b}
    return 'H' if edges=={'L','R'} else 'V' if edges=={'T','B'} else ('L' if 'L' in edges else 'R')+('T' if 'T' in edges else 'B')
def paths(d):
    out={}
    for c in d['channels']:
        p=c['pathCellIndices'];out[p[0]]='V'
        for k,i in enumerate(p[1:],1):out[i]=shape(side(i,p[k-1]),side(i,p[k+1]) if k<len(p)-1 else 'B')
    return out
def board(im,d,xy,cell,mode='opening'):
    opened=d['opening'] if mode=='after' else ()
    top,orig,sources=surface(d,opened);flow=entry(d,top,sources);dr=ImageDraw.Draw(im);rng=random.Random(d['n']);colors={};channels=paths(d)
    for i,a in enumerate(d['data']['cells']):
        if not a:continue
        x,y=xy[0]+i%9*cell,xy[1]+i//9*cell
        dr.rounded_rectangle((x+1,y+1,x+cell-1,y+cell-1),radius=max(2,cell//8),fill='#485b79',outline='#7587a2')
        if i in channels:
            k=SHAPES.index(channels[i]);isopen=mode=='channel' or i in sources
            r.paste(im,OPEN[k] if isopen else CLOSED[k],(x,y,cell,cell))
            if isopen:r.paste(im,WATER[k],(x,y,cell,cell))
        if i in sources:r.paste(im,SOURCE,(x,y,cell,cell));continue
        if i not in flow['reached'] or mode=='channel':continue
        special=d['data']['pinnedSpecialTypes'][i] if mode=='opening' else 0
        if special:
            r.paste(im,r.SPECIALS[special],(x,y,cell,cell),cell*.025)
            dr.rounded_rectangle((x+2,y+2,x+cell-2,y+cell-2),radius=max(2,cell//8),outline=r.GOLD,width=max(1,cell//25));continue
        choices=list(range(4));rng.shuffle(choices)
        c=next((c for c in choices if not(i%9>=2 and colors.get(i-1)==colors.get(i-2)==c) and not(i//9>=2 and colors.get(i-9)==colors.get(i-18)==c)),choices[0])
        colors[i]=c;r.paste(im,r.TILES[c],(x,y,cell,cell),cell*.05)
    if mode=='channel':return
    for i,o in enumerate(top):
        if not o or o in (64,76) or orig[i]!=i:continue
        bw,bh=map(int,re.search(r'^    size: \{x: (\d+), y: (\d+)\}$',r.DEFS[o],re.M).groups())
        r.paste(im,paint_art if o==63 else r.oid_art(o),(xy[0]+i%9*cell,xy[1]+i//9*cell,cell*bw,cell*bh),cell*.015)
    for s in d['seals']:
        i=s['originCellIndex'];im.alpha_composite(seal_art(s,cell),(xy[0]+i%9*cell,xy[1]+i//9*cell))

LABELS={28:'oyma taşı',34:'kargo',39:'pusula · yalnız special',63:'boya kasası',64:'antik mühür',75:'fener',76:'su kanalı'}
def goal_art(d,g):
    if g['targetType']==2:return r.sprite(r.ref(r.DEFS[78].split('goalIconSprite: ',1)[1]))
    oid=g['obstacleId']
    return paint_art if oid==63 else seal_art(d['seals'][0],24) if oid==64 else SOURCE if oid==76 else r.oid_art(oid)
def goal_label(g):return f'{g["amount"]} kuş' if g['targetType']==2 else f'{g["amount"]} {LABELS[g["obstacleId"]]}'
def render():
    for d in DESIGNS:
        stats=check(d);im=Image.new('RGBA',(1360,1240),r.BG);dr=ImageDraw.Draw(im)
        dr.text((30,22),f'{d["n"]} · {d["title"]} / {d["nameEn"]}',font=r.font(33,True),fill=r.GOLD)
        dr.text((30,72),f'9 × {d["h"]} · {d["moves"]} hamle · 4 renk · {d["difficulty"]}',font=r.font(24),fill='#ff9992' if d['n']==205 else r.INK)
        dr.text((30,116),'Güncel proje sprite’ları · Normal taş renkleri temsili · Boş başlangıç cepleri boş gösterildi',font=r.font(17),fill=r.MUTED)
        board(im,d,(30,176),70)
        x,y=720,176
        for g in d['goals']:
            r.paste(im,goal_art(d,g),(x,y,56,56));dr.text((x+75,y+14),goal_label(g),font=r.font(24,True),fill=r.INK);y+=69
        y+=16;dr.text((x,y),'İLK KARAR',font=r.font(21,True),fill=r.GOLD);y+=35
        y=r.paragraph(dr,x,y,d['idea'],width=47,size=21);y+=24
        for j,beat in enumerate(d['beats'],1):y=r.paragraph(dr,x,y,f'{j}. {beat}',width=54,size=18);y+=15
        y=max(y+18,865);dr.text((x,y),'KANAL HARİTASI' if d['n']==204 else 'AÇILMA PLANI',font=r.font(20,True),fill=r.GOLD);y+=34
        board(im,d,(x,y),18,'channel' if d['n']==204 else 'after')
        note='Kaynak dışındaki kanal hücreleri tek vuruşla açılır. Kanalın hedefi hücre değil, tamamlanan yol sayısı.' if d['n']==204 else 'Altı kargo ve taş rampa kalkmış varsayımı; gerçek bir hamle sonucu değil.' if d['n']==203 else 'İlk kapak kaldırılmış varsayımı; gerçek hamle sonucu veya fizik simülasyonu değil.'
        r.paragraph(dr,x+188,y+24,note,width=32,size=17)
        dr.text((30,1000),f'{stats["active"]} aktif / {stats["holes"]} hole · {stats["instances"][78]} kuş evi · {len(stats["initialEmptyCells"])} boş başlangıç yuvası',font=r.font(20),fill=r.INK)
        dr.text((30,1040),f'Statik normal dolum: {len(stats["initialTileCells"])} → {len(stats["afterOpeningTileCells"])} hücre.',font=r.font(19),fill=r.MUTED)
        dr.text((30,1158),'Statik taslak; denge/FPS ölçülmedi. Önceki levellar korundu; 201 yalnız hedef50 revizyonu.',font=r.font(18),fill=r.MUTED)
        dr.text((30,1195),'Unity Play/build, bot/kaskat simülasyonu ve remote yayın yapılmadı.',font=r.font(18),fill=r.MUTED)
        im.convert('RGB').save(HERE/f'LevelV1-{d["n"]}.png')
    im=Image.new('RGBA',(1440,1640),r.BG);dr=ImageDraw.Draw(im)
    dr.text((28,22),'202–205 · Kuş evleri ve eski mekanikler',font=r.font(34,True),fill=r.GOLD)
    dr.text((28,75),'201 hedefi50 · Yan tek sütun / alt tek sıra / iki sütunlu gruplar · 205 Zor',font=r.font(20),fill=r.MUTED)
    for j,d in enumerate(DESIGNS):
        x=28+(j%2)*710;y=130+(j//2)*720
        dr.text((x,y),f'{d["n"]} · {d["title"]}',font=r.font(27,True),fill=r.GOLD)
        dr.text((x,y+39),f'{d["moves"]} hamle · '+ ' / '.join(goal_label(g) for g in d['goals']),font=r.font(17),fill=r.INK)
        board(im,d,(x,y+86),46)
        r.paragraph(dr,x+430,y+100,d['idea'],width=25,size=18)
        r.paragraph(dr,x+430,y+330,d['beats'][0],width=25,size=17)
    dr.text((28,1586),'Güncel sprite’larla statik önizleme · Renkler temsili · Açılışta boş kalan cepler saklanmadı · Denge ölçülmedi',font=r.font(18),fill=r.MUTED)
    im.convert('RGB').save(HERE/'Levels202-205.png')
    print('Rendered four individual previews and a contact sheet; no production level files changed.')
if __name__=='__main__':render()
