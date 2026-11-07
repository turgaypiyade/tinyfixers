"""Finalize only owned 235–250 drafts after completed candidate runs."""
from author import *

MOVES={235:18,236:16,237:26,238:30,239:33,240:24,241:24,242:21,
       243:30,244:24,245:24,246:25,247:16,248:17,249:22,250:36}
EVIDENCE={235:'Sim-Final-Trial',236:'Sim-Retune',237:'Sim-Retune',238:'Sim-Balance',
          239:'Sim-Balance',240:'Sim-Last-Hard',241:'Sim-Retune',242:'Sim-Retune',
          243:'Sim-Retune',244:'Sim-244-24-Validation',245:'Sim-Last-Hard',246:'Sim-Retune',
          247:'Sim-Final-Trial',248:'Sim-Final-Trial',249:'Sim-249-22-Validation',250:'Sim-Balance'}


def selected_games(n,folder):
    games=[]
    for p in (HERE/folder).glob('job*/report.json'):
        r=json.loads(p.read_text(encoding='utf-8-sig'))
        relevant=[g for g in r['games']if g['levelPath']==f'L{n}']
        if not relevant:continue
        assert r['completed'],(n,'candidate still running',p)
        assert len(relevant)==r['config']['gamesPerLevel'],(n,'partial sample')
        games+=relevant
    assert len(games)==20,(n,len(games),'evidence incomplete')
    return games


def finish():
    assert not (HERE/'finalized.json').exists(),'Already finalized; preserve later user edits.'
    ds=json.loads((HERE/'designs.json').read_text());owned=json.loads((HERE/'written.json').read_text())
    evidence={n:selected_games(n,EVIDENCE[n])for n in MOVES}
    for d in ds:assert sha(ROOT/d['asset'])==owned[d['asset']],('Concurrent edit',d['level'])
    rev=HERE/'Revisions/Final-Balance';rev.mkdir(parents=True,exist_ok=True)
    (rev/'before-designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    for d in ds:(rev/Path(d['asset']).name).write_bytes((ROOT/d['asset']).read_bytes())
    rows=[]
    for index,d in enumerate(ds):
        n=d['level'];before=d['moves']
        if n==240:
            model=json.loads((HERE/'Final-Trial-Inputs/240-model.json').read_text())
            model['guid']=d['guid'];d=model;ds[index]=d
        d['moves']=MOVES[n]
        slots,reach=feed(d);pins=slots-reach
        for i in range(len(d['data']['pinnedTileTypes'])):d['data']['pinnedTileTypes'][i]=0
        for i in pins:d['data']['pinnedTileTypes'][i]=1+(i%d['width']+i//d['width'])%4
        d['openingGeometry']=dict(activeCells=sum(d['data']['cells']),normalSlots=len(slots),
            naturallyFed=len(reach),shadowPins=sorted(pins),specialPins=0)
        d['budget']=(f'{d["moves"]} hamle · Boss düellosu. Bot sonucu insan geçiş oranı değildir.' if d['kind']
                     else f'{d["moves"]} hamle. Oyuncu hedefi {d["target"]}; bot sonucu insan geçiş oranı değildir.')
        gs=evidence[n];counts=Counter(g['outcome']for g in gs);valid=counts['Won']+counts['Lost']
        d['botEvidence']=dict(folder=EVIDENCE[n],games=len(gs),won=counts['Won'],lost=counts['Lost'],
            errors=len(gs)-valid,winPercent=round(100*counts['Won']/valid,1)if valid else None)
        if n in(239,250):d['botEvidence']['limit']='Bot Dimerdal üretimine doğrudan öncelik vermiyor; insan oranı için güvenilir ölçü değil.'
        p=ROOT/d['asset'];p.write_text(serialize(d));owned[d['asset']]=sha(p)
        rows.append(dict(level=n,beforeMoves=before,moves=d['moves'],bossHp=900 if n==240 else 680 if n==250 else None,
                         result=d['botEvidence'],sha256=sha(p)))
    (HERE/'designs.json').write_text(json.dumps(ds,ensure_ascii=False,indent=2)+'\n')
    (HERE/'written.json').write_text(json.dumps(owned,indent=2)+'\n')
    (HERE/'finalized.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(rows,ensure_ascii=False))


if __name__=='__main__':finish()
