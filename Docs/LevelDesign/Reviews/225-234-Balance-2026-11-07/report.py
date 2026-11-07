"""Summarize completed runs without treating simulator failures as losses."""
from pathlib import Path
from collections import defaultdict,Counter
import json,statistics,math

HERE=Path(__file__).resolve().parent


def runs(folder):
    levels=defaultdict(list)
    for p in sorted((HERE/folder).glob('job*/report.json')):
        report=json.loads(p.read_text(encoding='utf-8-sig'))
        expected=len(report['config']['levelNumbers'])*report['config']['gamesPerLevel']
        assert report['completed'] and len(report['games'])==expected,p
        for g in report['games']:levels[int(g['levelPath'][1:])].append(g)
    return levels


def stats(gs):
    count=Counter(g['outcome'] for g in gs);n=count['Won']+count['Lost'];w=count['Won']
    p=w/n if n else 0;z=1.96;den=1+z*z/n if n else 1
    center=(p+z*z/(2*n))/den if n else 0
    half=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/den if n else 0
    return dict(games=len(gs),won=w,lost=count['Lost'],errors=len(gs)-n,
                winPercent=round(p*100,1) if n else None,
                bot95Wilson=[round(100*(center-half),1),round(100*(center+half),1)] if n else [],
                avgMovesLeftOnWin=round(statistics.mean(g['movesLeft']for g in gs if g['outcome']=='Won'),1)if w else None)


def report():
    baseline=runs('Sim-Main');final={}
    for folder in ['Sim-Move-Trial-Others','Sim-Move-Trial-233-234','Sim-Move-Trial-234-46']:
        final.update(runs(folder))
    final[227]=baseline[227];final[230]=baseline[230]
    moves=json.loads((HERE/'applied-moves.json').read_text())
    reasons={225:'Elmas üreticisi botta doğrudan öncelik almıyor. İnsan hedefi için temkinli +2; %0 bot oranını kovalamadık.',
        226:'23 hamle denemesi %46,7; tanıtım/rahatlama yönüne uygun.',227:'26 hamlede %40; sayı korundu.',
        228:'Üretici körlüğü var; +3 temkinli destek. İnsan %40–50 doğrulanmış değil.',229:'32 hamlede %36,7; sonu9 için kabul edilen yaklaşık %30 yönünde.',
        230:'34 hamle /10 oyun da sıfır galibiyet. Can bitimi hamle bitiminden önce geliyor; hamle şişirmek sorunu çözmedi.30 korundu; HP/karşı baskı ayrı inceleme ister.',
        231:'28 hamlede %46,7 bot; elma üreticisini önceliklendiremeyen bot için düşük okuma. Tanıtımın insan testi gerekli.',
        232:'Üretici körlüğü baskın;32→35 temkinli. Kullanıcının5/5/5 kotası ve tüm yerleşimi korundu; oran onayı yok.',
        233:'27 hamlede %86,7 olan bot geçişi21 hamlede %60’a indi.',
        234:'Üç katmanlı son sürüm24 ve34 hamlede bitirilemedi.42 hamlede %30,46 hamlede11/20=%55.24→46 büyük farkı katman yükünün sonucu; manuel denemede özellikle kontrol edilmeli.'}
    data={n:dict(before=stats(baseline[n]),after=stats(final[n]),reason=reasons[n])for n in range(225,235)}
    (HERE/'results.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
    lines=['#225–234 · hamle dengesi ·2026-11-07','',
        'Kullanıcı Mac simülasyonunu ve sonuçlara göre yalnız hamle ayarını açıkça istedi. Sonu1–4 için oyuncu hedefi %50–60;5/9 için yaklaşık%30 kabul ediliyor.6–8 için önceki%40–50 yönü çalışma varsayımıdır. Boss ayrı değerlendirilir.',
        '', '**Bu yüzdeler Usta bot ölçümüdür; kalibre insan geçiş oranı değildir.**26/27/32hamleli eski notlar yerine canlı katalog çözüldü.20–30 oyun örneği kaba denge yönü verir;20 oyunda %55’in %95Wilson aralığı yaklaşık%34–74’tür.',
        '', '|Level|Hamle önce→sonra|Bot önce|Bot son|Son W/L/hata|', '|---|---:|---:|---:|---|']
    for row in moves:
        n=row['level'];a=data[n]['before'];b=data[n]['after']
        lines.append(f'|{n}|{row["before"]}→{row["after"]}|%{a["winPercent"]}|%{b["winPercent"]}|{b["won"]}/{b["lost"]}/{b["errors"]}|')
    lines+=['','Son sütun her levelin seçilen bütçede gerçek ayrı koşusudur;227/230 sayıları değişmediği için baseline kullanılır.239 ve sonraki tasarımlar bu raporun dışında.','']
    for n in range(225,235):lines.append(f'- **{n}:** {reasons[n]}')
    lines+=['','## Kaynak ve sınırlar','',
        '- `Sim-Main`:10×30=300oyun;Unity6000.3.9f1,buildGuid929489f4052b46b9b6c41b0ea4ea8a11,Auto→Usta,hız10×. Güncel Macbuild’i kullanıcı aldı;agent build/UI açmadı.',
        '- `Sim-Pilot-Mac`:30oyun. `Sim-229-1x` ve`Sim-234-1x`:birer normal hız oyunu;ana yüzdeye karıştırılmaz.',
        '- Hamle denemeleri180+60+30+10+20=300oyun. Bu klasörde toplam632oyun;başlatılamayan sandbox denemesi sonuç sayılmaz.',
        '- Hata/Timeout kayıplara eklenmedi.229’da durulmuş SystemOverride gizleme kontrolü;234’te sahne geçişindePatchbotImpactSpark parent değiştirme hatası görüldü.234/42koşusunda kalanhedef0olduğu haldeLost dönen bir son-hamle kaydı da ayrıca incelenmeli;sonuç kendiliğindenWon’a çevrilmedi.',
        '- Bot `BuildGoalValueMap/GoalProximity/BuildColorNeed` içinde elmas için Dimerdal üreticisini,un için değirmeni,meyve için yumurta kutusunu doğrudan hedeflemez. Üretici seviyelerinde düşük oranlar büyük hamle artışı için tek başına kanıt değildir.',
        '', '## Koruma ve açılış','',
        '`applied-moves.json` veRuby/Psych tamYAML karşılaştırması:225–234’te yalnızmoves değişti.227/230byteolarakaynı.İsim/meta/GUID/maskeler,232’nin5/5/5kotası ve231’in kullanıcı yerleşimi korundu.176’ya dokunulmadı.',
        '', 'Statik normal açılış gölgeleri:225[67,68],227[42,65,83,88],231[79,80,89].Bunlar kapak açılınca beslenir;bu tur yalnızhamle yetkisi verildiği için yeni pin eklenmedi.234’ün32indexindeki mevcut gerçek pin korundu.230’daki iki gölge Dimerdalın mevcut fillsShadowBeneath kuralıyla başlangıçta dolar.',
        '', '## Üç booster isteği — bekleyen','',
        'Menüdeki LineH/PulseCore/SystemOverride için simconfig/gerçekPreLevelSpecialRuntimeInjector teslimatı/teslimat raporu eklendi.21Pythonorchestrator testi ve56izoleC#kontrolü geçti.Üç öğe verilmeden bot başlamaz;eksikteslimatıbaşarılıoranolarak raporlamaz.Dedicatedinjector envanterharcamaz/seçimkaydetmez;iptal/timeoutta callback/host temizlenir. Kaydedilmişstreak’in ekbooster vermemesi için UFOsimülasyonkoruması eklendi.',
        '', '**Mevcut binary bu yeni seçeneği desteklemiyor.**AGENTS.md açık istek olmadan build’i yasakladığı için yeniden Macsim derleme izni async soruldu;yanıt gelmedi.Üçbooster’lı koşu yapılmadı ve oran uydurulmadı.İzin gelirse önce1oyun smoke ile raporda tam3teslimat doğrulanıp sonra225–234aynıhamlelerle karşılaştırılmalı.',
        '', 'Yayın/releasebaseline/commit yapılmadı.']
    (HERE/'Review.md').write_text('\n'.join(lines)+'\n')
    print('Review written;632 actual games, boosters pending explicit build authorization.')


if __name__=='__main__':report()
