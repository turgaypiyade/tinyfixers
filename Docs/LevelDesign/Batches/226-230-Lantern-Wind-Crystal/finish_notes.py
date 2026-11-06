"""Finalize this batch's documentation; no Unity execution or runtime changes."""
from pathlib import Path
import hashlib,json,shutil

HERE=Path(__file__).resolve().parent
assert not (HERE/'active-revision.json').exists(), 'Archived writer: use the active revision documentation.'
ROOT=HERE.parents[3]
DOC=ROOT/'Docs/LevelDesign'
ds=json.loads((HERE/'designs.json').read_text())
labels={55:'Duvar',75:'Fener',83:'Un',79:'Lamba çerçevesi',28:'Oyma taşı',40:'İki aşamalı plastik',71:'Sarı renkli sandık',42:'Çim'}
def goal(g):
    label=('Elmas' if g['collectibleId']==4 else 'Boss HP') if g['targetType']==2 else labels[g['obstacleId']]
    return f"{g['amount']} {label}"
extra={
226:'Dört gerçek Fener75, dört ayrı Wall55 kapağının altındadır. Büyük duvar tek parçadır: toplam 16 duvar hücresi, 5 duvar hedefi. Başlangıçta dört fener; yalnız sağdaki iki sütundan (sıfır tabanlı 7/8) 16 ek fener, toplam20. Genel fallback kapalıdır. İki değirmenin çuvalları 8+8 un üretir. Üst kapaklar açılmadan seçili sütunlar fener basmaz.',
227:'Üç dikey ve iki yatay LampStrip79, her biri üç lamba: 15 lamba, 5 çerçeve hedefi. İki çerçeve kayar; biri ters yöndedir. Üst/sol kontur17 PlasticTwoStage40; alt sağdaki bitişik3×4 alan12 SculptingStone28. Plastik hareketlidir; konturun bütün oyun boyunca sabit kalacağı varsayılmaz. Ek plastik üretimi yoktur.',
228:'Üst merkezde3×5=15 Fener75; altta3×3 kırmızı Dimerdal95 ve kendi12 üretim kotası. Arada iki normal taş sırası vardır. Kutu kendi son elması başarıyla yerleşince kalkar. Üretim hedefi azaltmaz; elmas daha sonra eşleşme/special ile toplanır. Ortak mor hedef ikonu kullanılır.',
229:'Ortadaki12 sarı ColorCrate71 üzerinde12 Grass42; çim üst örtüdür. Dört Windmill80 toplam24 un (çuval başına6) sağlar. Yeşil Dimerdal88 kota6, mavi Dimerdal85 kota10; iki kutu bağımsız tükenir, ortak mor Diamond hedefi16’dır. Fener eklenmeyerek dört hedefte kalındı.',
230:'9×10, üç boss dalgası ve toplam660 HP;30 hamle, oyuncu270 HP. Varsayılan BuildWaves dağıtımı kullanılır. Mavi85 ve kırmızı94 kutuların kotası6+6;16 Fener75; iki değirmen8+8 un. Karşı atışlar8/12/13 ve alet tehditleri47/48/51/50/49 hedeflerde yoktur.220’nin mevcut karşı baskı yapısı başlangıç alınmış,220 değiştirilmemiştir.'}
review={
226:'78 ve87 konumsal;204 kırpılmış benzerlik kısa listesine girdi.78 ile gerçek sprite karşılaştırmasında yeni kemerin iki bağlantısı, sağ örtülü fener sütunları ve sol değirmen işi ayrışıyor.',
227:'173/177/45 ve kırpılmış94 yakın adaylardır.173 ile görsel karşılaştırmada ortogonal raf ritmi ortak; üç dikey/iki yatay çerçeve, L plastik ve bitişik oyma kütlesi yeni iş sırası kuruyor.',
228:'Kırpılmış engel siluetinde61 güçlü yakın adaydır; ayrıca159/193, konumsal181 incelendi.61’de tam genişlikte üst plastik/alt sandık-taş bantları ve ortada yatay çalışma yüzü vardır.228’de dar merkez gövdesi, alt üretici noktası ve geniş dikey yan çalışma yüzleri vardır. Benzer dikdörtgen kütleler saklanmadı; puan özgünlük yüzdesi sayılmadı.',
229:'169 konumsal;12/173/117 kırpılmış yakın adaylardır.169 ile görsel karşılaştırma burgu çevresindeki dört değirmen, merkezi örtülü L ve eşit olmayan iki bağımsız üretim işini ayırır.',
230:'133/102 konumsal;67 kırpılmış kısa listededir.133 karşılaştırmasında arena boyutu ortak olsa da üst fener sahnesi, yan bağımsız kaynaklar ve dip değirmenleri farklı açılma işi kurar.'}
for d in ds:
    n=d['level'];o=d['openingGeometry'];sources=d['custom']['dimerdals']
    quota=' + '.join(str(e['count']) for e in sources) or 'yok'
    text=f'''# {n} · {d['name']} / {d['nameEn']}

2026-11-06 · Agent taslağı; kullanıcı henüz oynamadı/onaylamadı.

{d['width']}×{d['height']}, {sum(d['data']['cells'])} aktif hücre; **{d['moves']} hamle · {d['difficulty']}**. Katalog: `{d['levelKey']}`, bölüm10. Asset: [{Path(d['asset']).name}](../../{d['asset']}).

Hedefler: {', '.join(goal(g) for g in d['goals'])}. Dimerdal kutu kotaları: {quota}.

## Tasarımın dört sorusu

1. **İlk görüntü:** {d['idea']}
2. **İlk karar:** {d['firstDecision']}
3. **Açılınca:** {d['change']}
4. **Kalan iş:** {d['remaining']}

## Yerleşim, katman ve besleme

{extra[n]}

Başlangıç normal taş yuvası{o['tileSlots']}; doğal beslenen{o['naturallyFed']}, mevcut kaynak kuralıyla başlangıç dolan{o['shadowSeeded']}, gerçek asset renk pini{o['authoredPins']}. Bilinçli başlangıç boşları: {o['intentionallyEmpty'] or 'yok'}. Pinlerden tek başına zorunlu üçlü veya hazır special kurulmadı. Açılabilir engeller kalkınca bütün aktif hücreler doğal beslenir. Bu geometri kontrolüdür; kaskat/düşüş oyunu simüle edilmedi.

## Seçim ve tekrar incelemesi

{review[n]}

Diğer adaylar: {' '.join(d['alternatives'])}

Hamle gerekçesi: {d['budget']}

## Kontrol sınırı

Statik YAML/dizi/origin/footprint/katman/hedef/üretim/ikon/dolum/katalog/TR–EN ad kontrolleri geçti. Gerçek sprite PNG açılıp incelendi; renkler pin dışındaki yuvalarda temsilidir. Sağ kesit bir oynanmış durum değildir; gelecekte rastgele oluşacak un ve fenerler yerleştirilmiş gibi çizilmez.

1–225 ve mevcut runtime/art dahil3393 korunan dosya değişmedi. Oyun dengesi, oyuncu ilgisi ve CPU/GC/FPS ölçülmedi. Unity build/Play, bot veya düşüş/kaskat simülasyonu, yayın, release baseline ve commit yapılmadı.

[Gerçek sprite önizlemesi](Level{n}-{d['slug']}.png) · [Batch ve performans notu](Batches/226-230-Lantern-Wind-Crystal/README.md)
'''
    (DOC/f"Level{n}-{d['slug']}.md").write_text(text)

table='\n'.join(f"| {d['level']} | {d['name']} / {d['nameEn']} | {d['moves']} · {d['difficulty']} | {', '.join(goal(g) for g in d['goals'])} |" for d in ds)
readme=f'''# 226–230 · Fener / rüzgâr / kristal

2026-11-06 · Yetkili beş yeni taslak; görsel inceleme agent'a aittir. Kullanıcı onayı ve oynanmış denge yok.

| Level | TR / EN | Hamle / tür | Hedefler |
| --- | --- | --- | --- |
{table}

226: kullanıcı referansı13/14, duvarın altında gerçek fener + sağdaki iki sütundan fener üretimi, solda değirmenler.227: referans15’ten dik/yatay LampStrip, oyma taşları ve iki aşamalı plastik.228–230: kullanıcı üç yeni fikri bize bıraktı;230Boss. Önceki225 veya onaylı levellar yeniden tasarlanmadı.231+ için istek yok.

Katalog bölüm sınırı25’tir;226–230 bölüm10’a eklendi. Mevcut ChapterThemeLibrary, ayrı bölüm10 görseli bulunmazsa son mevcut temaya döner; yeni tema veya runtime kodu eklenmedi. TR/EN isimler hem oyun JSON’unda hem notlarda bulunur.

## Çalıştırma ve koruma

`python3 Docs/LevelDesign/Batches/226-230-Lantern-Wind-Crystal/verify.py`

`design.py` yalnız sahip olduğu yeni asset/meta ve katalog/adları hash korumasıyla yazar; kullanıcı değişikliğini ezmez. Eski batch yazıcıları çalıştırılmaz. `render.py` gerçek proje sprite’larıyla açılış ve statik katman kesiti, `compare.py` tarihi karşılaştırma üretir. Başlangıç renk pinleri gerçek asset verisidir. Serbest renkler temsilidir; kesitler gameplay simülasyonu değildir.

`Before/` katalog/ad/hafıza snapshot’ları; `protected-before.json`3393 mevcut oyun dosyası.1–225, mevcut engel kütüphanesi, kod ve art korundu. `validation.json` bağımsız audit; `visual-review.json` açılan PNG ve değerlendirme kaydıdır. Her level için `Reviews/<N>-Lantern-Wind-Crystal` tam katalog envanteri ve benzerlik listeleri vardır. Özellikle228’in61 ile kırpılmış kütle benzerliği görsel olarak incelendi; yüzdelik özgünlük iddiası yok.

## Motor kuralları

Hareketli Fener75 ve Plastic40 dolu hücre sayılır, kalıcı tavan duvarı sayılmaz. Statik besleme kontrolü gerçek CascadeLogic/BoardCellStateQuery erişim kurallarıyla kuruldu; eski renderer'ın hareketli engelleri kalıcı çatı sayması yalnız bu batch'in karşılaştırma aracında düzeltildi. Gameplay akışına dokunulmadı.226’da gizli fenerler başlangıç ve kalan üretim kotasına dahildir; tür75/kolon7–8, fallback kapalı. LineH erişiminin hole boyunca devam etmesi mevcut koddan doğrulandı.

Dimerdal kotası228[12],229[6,10],230[6,6]; her kutu kendi başarılı son yerleşiminden sonra kalkar. Hedef üretim değil toplama sayar; mor ortak sembol kullanır. Değirmenin kendisi hedef değildir; Un83 hedefi çuval başına bölüştürülür. Çuval yerleşimi/unun yayılması dinamik olduğundan önizlemede gelecekteki konumu uydurulmaz. Boss karşı atışları hedef dışında tutuldu.

## Performans ve doğrulama sınırı

Yeni runtime/VFX/audio sistemi eklenmedi. Mevcut Windmill paylaşılan update ve buffer’ları kullanır;229’da dört değirmen/16 dönen kanat vardır. Mevcut tek seferlik puff görsellerinin oluşturulup kaldırılma maliyeti sürer; sıfır allocation iddiası yok. Dimerdal mevcut uçuş/parça havuzlarını ve hareketsiz taş aramasını kullanır; hedef değişiminde0.1sn yeniden deneme yolunda tahta taraması maliyeti vardır. Havuz/iptal/teardown yolları mevcut kodda gözden geçirildi; değiştirilmedi. Ağır özel canvas eklenmedi. CPU/GC/FPS ölçülmedi.

Statik kontrol ve açılmış gerçek PNG incelemesi tamamlandı. Unity build/Play, bot/kaskat/düşüş simülasyonu, cihazda denge testi, canlı telemetri doğrulaması, yayın, baseline veya commit yok. Hamleler ilk tahmindir.

[Toplu önizleme](Levels226-230.png) · [Tarihi karşılaştırma](Comparison.png)
'''
(HERE/'README.md').write_text(readme)
visual={'date':'2026-11-06','reviewer':'agent','userApproved':False,'played':False,'balanceMeasured':False,'opened':[f'Previews/Level{d["level"]}.png' for d in ds]+['Levels226-230.png','Comparison.png'],'assessment':review,'finalCorrection':'230 kota ve boss paragrafı çakışması giderildi; son PNG yeniden açıldı.','limits':'Gerçek sprite; pin dışı renkler temsili. Katman kesiti, gameplay anı değildir.'}
(HERE/'visual-review.json').write_text(json.dumps(visual,ensure_ascii=False,indent=2)+'\n')

def prepend_once(path,heading,body):
    text=path.read_text()
    if heading in text:return
    first,sep,rest=text.partition('\n')
    path.write_text(first+'\n\n'+heading+'\n\n'+body.strip()+'\n\n'+rest.lstrip())
checkpoint='''Son açık istekler226 ve227 için referanslı tasarım, ardından228–229 ve230Boss’un tasarımıdır. Beş taslak tamamlandı:226 Rüzgâr Kemeri28Normal;227 Işık Tezgâhı26Normal;228 Kristal Ünlem26Normal;229 Rüzgâr Burgusu27Super Hard;230 Büyük Işık Düellosu30Boss(9×10,660 toplamHP/3rakip). Bölüm10, aktifV1 katalog1–230 ve TR/EN isimler güncel.226 dört duvar kapağı altında dört gerçek fener ve yalnız sağ iki sütundan16 ek fener/iki değirmen;227 beş üçlü lamba çerçevesi+oyma+plastik;228 ünlem;229 çim üstte sarıL/dört değirmen;230 üst fener sahnesi/yan kristal locaları/dip değirmenleri. Dimerdal kotaları228[12],229[6,10],230[6,6]; bağımsız kırılma, toplam mor hedef.

Kayıt `Batches/226-230-Lantern-Wind-Crystal/README.md`; beş ayrı not/gerçek sprite PNG, toplu PNG ve eski örneklerle karşılaştırma var. Statik audit geçti;3393 kapsam dışı oyun dosyası ve1–225 korundu. Başlangıç pinleri226=20,229=8;227’de dört bilinçli açılabilir gölge boşu var. Açılma sonrası besleme erişimi statik doğrulandı. Görsel inceleme agent değerlendirmesidir; kullanıcı onayı veya oynanmış denge değildir. Hamleler ilk ayar. Build/Play/cihaz/bot/kaskat/düşüş simülasyonu, yayın/baseline/commit yok; runtime/art/akış değiştirilmedi.231+ istenmedi. Alttaki “226+istenmedi” kayıtları tarihseldir.'''
for name in ('Memory.md','Design-State.md','SESSION-LEVEL-DESIGN-NOTES.md'):
    prepend_once(DOC/name,'## Güncel devam — 226–230 taslakları · 2026-11-06',checkpoint)
feedback='''**Kullanıcı kaynağı:**226 için “duvarı, altında fener çıkar ama sütun bazlı dökülür sol tarafta bizim yel değirmenleri olabilir… şeklinde biraz inisiyatif”;227 referansı için “sculpting stones, strip lamp, plastic two obstacle”;228–230 için “diğer3 levelı228-229-230 (boss olacak) sen tasarla… fener, yeldeğirmeni,dimerdal… kullanıyor olabilirsin”.

**Kapsam / ders:** Referansın açılma sırası ve dik/yatay ritmi alınır; başka oyunun bilinmeyen perde/kapı mekaniği kopyalanmaz.226/227 açık tasarım talebidir;228–230’da adı geçen üç engel her levele birlikte konacak zorunlu liste değildir.230Boss ve en fazla10satır.231 için yetki çıkarılmaz.

**Agent uygulaması:**226 örtülü fener+sütun üretimi;227 raflar;228ünlem;229burgu;230arena. Bu beş taslak henüz kullanıcı tarafından değerlendirilmedi. Görsel agent değerlendirmesi batch visual-review kaydındadır; oynanmış denge yok.'''
prepend_once(DOC/'Design-Feedback.md','## 226/227 referansları ve228–230 tasarım yetkisi — kullanıcı · 2026-11-06',feedback)
index=DOC/'References/Index.md'
if '13-Fener-Sutunu-Acilis.png' not in index.read_text():
    with index.open('a') as f:f.write('''\n## 226–227 — açık tasarım referansları · 2026-11-06

[13 — Fener sütunu açılış](13-Fener-Sutunu-Acilis.png), [14 — açıldıktan sonra](14-Fener-Sutunu-Sonra.png):226 için duvar altında fener, seçili sütunlardan düşen ürün ve solda bizim değirmenler. Kullanıcı şekli değiştirme inisiyatifi verdi. Yabancı oyundaki sağ perdelerin davranışı varsayılmadı.

[15 — Dik/yatay şerit tezgâhı](15-Dik-Yatay-Serit-Tezgahi.png):227 için ortogonal LampStrip, SculptingStone ve PlasticTwoStage düşüncesi. Adet/yer/tahta birebir kopyalanmadı.228–230’u bu konseptleri düşünerek tasarlama ayrıca açıkça istendi; referanslar otomatik kullanıcı onayı değildir.
''')
names=DOC/'Level-Names.md';text=names.read_text().replace('LevelCatalogProV1 (1–212)','LevelCatalogProV1 (1–230)')
for d in ds:
    if f"| {d['level']} |" not in text:text+=f"| {d['level']} | {d['name']} |\n"
names.write_text(text)

delivery=HERE/'Delivery';delivery.mkdir(exist_ok=True)
for d in ds:
    for suffix in ('md','png'):
        p=DOC/f"Level{d['level']}-{d['slug']}.{suffix}";shutil.copy2(p,delivery/p.name)
shutil.copy2(HERE/'Levels226-230.png',delivery/'Levels226-230.png')
hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in delivery.iterdir() if p.is_file() and p.name!='sha256.json'}
(delivery/'sha256.json').write_text(json.dumps(hashes,ensure_ascii=False,indent=2)+'\n')
print('Five design notes, batch review, checkpoint, names, references and repository delivery completed.')
