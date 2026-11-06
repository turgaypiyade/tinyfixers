"""Scoped226 revision notes, feedback and reviewed-art delivery."""
from pathlib import Path
import json,hashlib,shutil,sys
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1];ROOT=BATCH.parents[3];DOC=ROOT/'Docs/LevelDesign'
sys.path.insert(0,str(BATCH));import design as shared
ds=json.loads((BATCH/'designs.json').read_text());d=next(x for x in ds if x['level']==226)

def before(p):
    q=HERE/'Before'/p.relative_to(ROOT)
    if p.exists() and not q.exists():q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)

def prepend(p,title,body):
    before(p);t=p.read_text()
    if title in t:return
    first,_,rest=t.partition('\n');p.write_text(first+'\n\n'+title+'\n\n'+body.strip()+'\n\n'+rest.lstrip())

checkpoint='''Kullanıcı226 v1’i açıkça reddetti: duvar referanstaki gibi yol boyunca bağlı olmalı, bir hücresi yıkılınca domino gibi ilerlemeli ve altından fenerler çıkmalı. Sağ üstte tavan bloğu altı boşluk/akış sorunu yarattı; değirmenler ana normal alanın yanında kalıp farklı bir oyun kurmadı. Değirmenler special ile erişilecek ayrı hücre grubunda istenir. Ekran kanıtı `References/16-Level226-Akis-Ve-Oyun-Fikri-Reddi.png`. Önceki226 için agent görsel değerlendirmesi kullanıcı onayı değildir.

**226 v2 Rüzgâr Kemeri:**9×11/68aktif,28hamleNormal; tek24hücre duvar yolu, tamamının altında24 gerçek Fener75; sağdaki ayrı2×4adada iki Windmill80. Hedefler1Duvar/24Fener/16Un. Duvar sağ koldan tabana döner, altına normal taş cebi bırakmaz.36 başlangıç taş yuvasının hepsi doğal beslenir; pin/hazırspecial/başlangıçboşu yok. Bir hücrede mevcut beşinci vuruş tek parçanın zincirini başlatır. Değirmen adası gerçek hole ile ayrılır; bütün değirmen satırlarına ana çalışma alanından yatay special erişimi vardır.24 gizli fener hedefi karşılar; fazladan fener üretim kotası yok.

Mevcut yığın damgalama her Wall hücresini ayrı origin yaptığı için gerekli küçük destek eklendi: StackedObstacleEntry.wallPieceOriginPlusOne(0eski davranış), GridSpawner ortak origin ile üst duvar/alt içerik kaydı, editörde tek parça hedef sayımı. Düşüş/akış,duvar animasyonu/ses/FX kodu değişmedi; yeni tarama veya per-hit allocation yok.265 gerçek yöntem katman/origin/hedef/beneath/dalga kontrolü ve1356 mevcut zincir/sırayla açılma/havuz/iptal kontrolü geçti. Eski damgalama ile24 ayrı parça hatası yeniden üretildi. Üç eski genel harness mevcut Dimerdal doubles eksikliğiyle çalışmıyor; bu revizyonun öncesiyle aynı yedi yöntem doğrulandı ve sınırlama kaydedildi.

Aktif kayıt `Batches/226-230-Lantern-Wind-Crystal/Revisions/v2-wall-route/`; parentauthor/notyazıcısı eski226’yı geri basamaz. Yeni gerçek PNG ve katman kesiti açıldı; görsel değerlendirme agent’a aittir.5278 diğer oyun dosyası,1–225ve227–230 assetleri,katalog/TR–EN adlar korundu. Yeni taslak kullanıcı tarafından henüz oynanmadı/onaylanmadı;28hamle ilk ayar. Unitybuild/Play/cihaz/bot/düşüş-kaskat simülasyonu,yayın/baseline/commit yok.231+istenmedi.'''
for name in ('Memory.md','Design-State.md','SESSION-LEVEL-DESIGN-NOTES.md'):
    prepend(DOC/name,'## Güncel — 226 v1 reddi / bağlı duvar yolu v2 · 2026-11-06',checkpoint)

feedback='''**Kullanıcı kaynağı:** “226 olmamış… duvarın verdiğim referans gibi bir yolun içerisinde olmasını… bir yerden yıkılınca domino taşları gibi yıkılması… altından da fenerler çıkacaktı… sağ üst köşeye… bu tür konumlamalar altta boşluk oluşturduğu için taş akışını bozuyor… değirmenler için ayrılmış biraz bağımsız duran ve specialla ulaşılması gereken hücrelerde… bu şekilde farklı bir oyun olmamış”.

**Durum:**226 v1 reddedildi. Bu geri bildirim yalnız226’nın yeniden kurulmasını yetkilendirir;227–230 için onay veya revizyon isteği değildir.

**Tekrar kullanılacak ders:** Referansın yalnız engel ailesini veya genel siluetini almak yeterli değildir. Bağlı bir yolun tek olayla açılması, altındaki içerik ve bağımsız alana special erişimi ilk kararın kendisidir. Bir tavan bloğunu sağ üste taşımak bu ilişkiyi kaybedip altında boş cepler yaratabilir. Önizlemede pinle dolu gösterilen bir başlangıç, açılan ilk taşlardan sonra aynı bölgenin beslenebileceğini kanıtlamaz. Katmanlar açılmadan önce de çalışma alanının fiziksel beslemesini değerlendir. Ayrı değirmen adası bu226 fikrine özeldir; bütün levellara zorunlu şablon yapılmaz.

**Agent uygulaması / sınır:** Tek24hücre duvar yolu,24 gerçek gizli fener ve hole ile ayrılmış2×4değirmen adası. Başlangıç36/36 doğal dolum,pin0. Statik ve gerçek sprite görsel kontrolü var;v2 kullanıcı onayı/oynanmış denge yok. Önceki v1 görünümü ve asseti karşı örnek olarak revizyon Before klasöründe saklandı.'''
prepend(DOC/'Design-Feedback.md','## 226 — oyun fikri ve akış reddi · kullanıcı · 2026-11-06',feedback)

note='''# 226 · Rüzgâr Kemeri / Windlit Arch — v2

2026-11-06 · V1 kullanıcı tarafından reddedildi. Bu revizyon henüz oynanmadı/onaylanmadı.

**9×11,68 aktif hücre;28 hamle Normal. Hedef:1 duvar parçası,24 fener,16 un.** Katalog `LevelCL_0226`,bölüm10;isim/GUID/katalog/hamle korundu. Asset [LevelV1_00002260.asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002260.asset).

## Tasarımın dört sorusu

1. **İlk görüntü:** Sağ koldan tabana dönen iki hücre genişliğinde bağlı duvar yolu; yukarıda6×3 çalışma yüzü,sağda ayrı değirmen adası.
2. **İlk karar:** Yolun üst ağzındaki veya sol alt ucundaki bir duvar hücresine odaklanırken adaya yatay special hizala.
3. **Açılınca:** Aynı hücre beşinci vuruşu alınca tek24hücre parça mevcut dalgayla yıkılır;her hücrenin altından kendi gerçek feneri çıkar.
4. **Kalan iş:**24 iki aşamalı fener ve iki değirmenin çuvallarından gelen8+8un. Değirmenlere yalnızspecial ile uzanılır;unun gelecekteki rastgele yeri önceden çizilmez.

## Akış ve katman

Duvar sıfır tabanlıx4–5/y3–10 dik kolu ilex0–3/y9–10 tabanını birleştirir. Bütün duvar hücreleri ortak origin31 kullanır;her birinin altında ayrı originli Fener75 vardır. Yolun dik kolu tabana dayanır;altında engellenmiş normal alan yoktur.36 normal başlangıç yuvasının36’sı doğal beslenir. Renk pini,hazırspecial ve boş başlangıç yuvası yoktur.24 gizli fener toplam hedefi karşılar;ek/refill kotası0’dır. Var olan sütun seçimi4/5 ekstra fener gerektirmez.

İki Windmill80,x7/y4 vex7/y6’dadır;ada yalnız bu sekiz hücreyi içerir. Ana tahtayla ortogonal veya çapraz normal taş teması yoktur;x6hole şeridi ayırır. Ana çalışma alanında her değirmen satırına yatay special hattı bulunur. Mevcut LineH kodu hole’lar ve duvar hücreleri üzerinden ilerler;akış koduna dokunulmadı. Ada normal taş çalışma alanı değildir;değirmenlerin altında beslenmesi gereken boş hücre bırakılmadı.

## Gerekli küçük katman desteği

Eski stacked Wall damgalaması her kapak hücresini ayrı parça yapıyordu. İsteğe bağlı `wallPieceOriginPlusOne` ortak origin bilgisini taşır;0eski davranışı korur. GridSpawner yerleştirme hücresini değiştirmeden gerçek duvar origin’ini ve beneath kaydını ortak kurar. Editör hedef önerisi bunu tekparça sayar. Düşüş/kaskat,animasyon,efekt ve ses kodu değişmedi. Alan okumasıO(1);yeni tahta taraması,efekt veya per-hit allocation yok. Mevcut duvar dalgası tek coroutine ve mevcut FX havuzlarını kullanır;1356 sıra/iptal/havuz kontrolü geçti. CPU/GC/FPS ölçülmedi.

## Kontroller ve sınırlar

Gerçek yeni stamp/origin/editör hedef/beneath/dalga yöntemleriyle265kontrol geçti. Eski yöntemle24ayrıorigin sorunu tekrar üretildi. YAML/dizi/katman/bağlılık/hedef/başlangıç besleme/special erişim ve5278dosya koruma kontrolü geçti;1–225ve227–230assetleri,katalog,adlar aynı. Üç eski genel test betiğinin Dimerdal test doubles eksikleri bu değişiklikten önce de vardır;[teknik kayıt](Batches/226-230-Lantern-Wind-Crystal/Revisions/v2-wall-route/legacy-harness-status.json).

Gerçek sprite PNG ve referans açılıp karşılaştırıldı. Yakın örnekler111konumsal,84kırpılmışsilüet;U/J benzeri kütle tek başına yenilik iddiası değildir. Buradaki oyun işi tekparça yolun açılması ve ayrı adaya special erişimidir. Sağ kesit statiktir,oynanmış an değildir;serbest taş renkleri temsilidir.28hamle ilk tahmin. Unitybuild/Play/cihaz/bot/düşüş-kaskat simülasyonu,yayın/baseline/commit yok.

[Yeni önizleme](Level226-Ruzgar-Kemeri.png) · [V2 teknik kayıt](Batches/226-230-Lantern-Wind-Crystal/Revisions/v2-wall-route/README.md)
'''
(DOC/'Level226-Ruzgar-Kemeri.md').write_text(note)

readme='''# 226 v2 · bağlı duvar yolu ve ayrı değirmen adası

V1 kullanıcı tarafından reddedildi; v2 henüz kullanıcı tarafından oynanmadı/onaylanmadı. Önceki asset, kod, model ve PNG `Before/` altında saklanır.

Yeni 226: 9×11 / 68 aktif, 28 hamle; tek 24 hücre Wall55 (ortak origin31), altında24 Fener75, sağda hole ile ayrılmış iki Windmill80. Hedefler1/24/16; 36/36 doğal başlangıç taşı, pin0, boş0. Gizli24 fener hedefi karşılar; ek üretim0. Değirmenlere ana oyun alanından yalnız special ile erişilir. Yıkım mevcut beşinci hit ve komşuluk dalgasını kullanır.

## Yetkili değişiklikler

Yalnız226 asseti ve üç gerekli C# dosyası: LevelData.StackedObstacleEntry isteğe bağlı ortak duvar origin'i (0 eski davranış); GridSpawner.StampStackedEntry ortak kapak origin'i ve alt fener kaydı; LevelDataEditor.CountGoalObstacles tek parça sayımı. `Tools/review_level_patterns.py` aynı metadata'yı okur. Düşüş/akış, duvar animasyonu, audio ve FX kodu değişmedi. Ortak origin okuması O(1); yeni tarama, efekt veya per-hit allocation yok. Editör HashSet'i yalnız editör sayımındadır. CPU/GC/FPS ölçülmedi.

## Tekrarlanabilir kontroller

- `python3 Docs/LevelDesign/Batches/226-230-Lantern-Wind-Crystal/verify.py`: YAML/diziler/origin/katman/hedef, 36/36 doğal dolum, ada ayrılığı/special hizası, 5278 kapsam dışı oyun dosyası ve diğer levelların korunması.
- `csi Tools/check_stacked_wall_route.csx --before`: eski üretim stamp'inin24 bağımsız duvar parçası yarattığı tekrar üretildi.
- `csi Tools/check_stacked_wall_route.csx`: 265 gerçek yöntem kontrolü; ters sırayla stamp, 24 ayrı alt fener/origin/HP koruma, bütün hücrelerden dalga bağlılığı, eski alansız kayıtlar/geçersiz değerler ve editör hedefi.
- `csi Tools/check_wall_progressive_collapse.csx`: 1356 mevcut duvar zamanlayıcı/sırayla hücre bırakma/FX havuzu/iptal/hold ownership kontrolü.

Üç eski genel harness, DimerdalRules/Service/View doubles güncel olmadığı için derlenmedi. İlgili yedi üretim Spawner yöntemi Before ile byte-byte aynıdır; bu226 revizyonundan kaynaklanan bir regresyon değildir. `legacy-harness-status.json` tam kapsamı listeler. Bu kayıt genel regresyonların geçtiği iddiasını taşımaz.

Gerçek sprite önizlemesi ve kullanıcı referansı açıldı; v1 reddi ile v2 agent değerlendirmesi ayrı kayıtlıdır. `Docs/LevelDesign/Reviews/226-Wall-Route-v2/` tekrar envanteri;111 ve84 gerçek sprite karşılaştırması incelendi. Yakınlık puanı özgünlük veya onay yüzdesi değildir. Parent author ve not yazıcısı eski226'yı geri basamaz; aktif v2 author bu dizindedir.

Unity build/Play/cihaz, bot, düşüş/kaskat simülasyonu, yayın, release baseline ve commit yok. 1–225/227–230, katalog/TR–EN adlar ve GUID korundu. 28 hamle oynanmış denge ayarı değildir.
'''
# Space the technical note's prose, retaining paths and names above as exact commands.
(HERE/'README.md').write_text(readme)

for name in ('README.md','visual-review.json'):
    p=BATCH/name;before(p)
parent=BATCH/'README.md';text=parent.read_text()
if '## Aktif226 revizyonu' not in text:
    first,_,rest=text.partition('\n');text=first+'\n\n## Aktif226 revizyonu — v2 · 2026-11-06\n\n226v1 kullanıcı tarafından reddedildi;[v2 bağlı duvar yolu](Revisions/v2-wall-route/README.md) günceldir. Yeni226:68aktif/28hamle,1duvar/24fener/16un;36doğal başlangıç taşı,pin0. Üç küçük C#katman/origin desteği eklendi;düşüş/akış değiştirilmedi. Aşağıdaki ilk taslak gerekçeleri ve ölçümleri226 için tarihseldir;227–230 değişmedi.\n\n'+rest.lstrip()
text=text.replace('| 226 | Rüzgâr Kemeri / Windlit Arch | 28 · Normal | 5 Duvar, 20 Fener, 16 Un |','| 226 | Rüzgâr Kemeri / Windlit Arch | 28 · Normal | 1 Duvar, 24 Fener, 16 Un |')
parent.write_text(text)
v=json.loads((BATCH/'visual-review.json').read_text());v['assessment']['226']='V1 kullanıcı tarafından reddedildi. V2 gerçek PNG açıldı: tek24hücreyol/24altfener,ayrıdeğirmenadası,36doğalbaşlangıç,pin0. Referans13 ve yakın örnek111/84 ile karşılaştırıldı. Kullanıcı henüzv2oynamadı/onaylamadı.'
v['active226Revision']='v2-wall-route';(BATCH/'visual-review.json').write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
(HERE/'visual-review.json').write_text(json.dumps(dict(reviewer='agent',userApproved=False,played=False,opened=['../../../../Level226-Ruzgar-Kemeri.png','../../../../References/13-Fener-Sutunu-Acilis.png','Comparison226.png'],assessment=v['assessment']['226'],limits='Statik katman kesiti; serbest renkler temsili. Oynanmış denge veya cihaz performansı yok.'),ensure_ascii=False,indent=2)+'\n')
(HERE/'feedback-pair.json').write_text(json.dumps(dict(level=226,before='Before/Docs/LevelDesign/Batches/226-230-Lantern-Wind-Crystal/designs.json',after='../../designs.json',beforeUserStatus='rejected',afterUserStatus='not_reviewed',source='References/16-Level226-Akis-Ve-Oyun-Fikri-Reddi.png',reasons=['Referanstaki bağlı domino yolu kaybedildi','Tavan bloğu altında dolum boşluğu oluştu','Değirmenler ayrı special işi kurmadı'],scope='Only226; not approval or redesign of227–230'),ensure_ascii=False,indent=2)+'\n')

index=DOC/'References/Index.md';before(index)
if '16-Level226-Akis-Ve-Oyun-Fikri-Reddi.png' not in index.read_text():
    with index.open('a') as f:f.write('\n## 226 v1 reddi — kullanıcı oyun ekranı · 2026-11-06\n\n[16 — Tavan altı boşluk ve oyun fikri reddi](16-Level226-Akis-Ve-Oyun-Fikri-Reddi.png).226v1reddedildi:referanstaki duvar yol boyunca tekparça açılıp altındaki fenerleri göstermeli;değirmenler ayrı,special ileerişilecek hücrelerde olmalı. Bu ekran başarılı örnek değildir;v2agenttaslağı da kullanıcı onayı sayılmaz.\n')
guide=DOC/'Obstacle-Design-Guide.md';before(guide)
if 'wallPieceOriginPlusOne' not in guide.read_text():
    with guide.open('a') as f:f.write('\n## Örtülü içerik üstünde tek duvar yolu · 2026-11-06\n\nStackedObstacleEntry.wallPieceOriginPlusOne,Wall/MetalWall yığın hücrelerini tekorigin altında birleştirir;değer ortak en küçük origin+1,0eski bağımsız hücre davranışıdır. originCellIndex her kapağın gerçek yerleştirme hücresi olarak kalır. Aynı bağlı parçanın tüm Wallentryleri aynı değeri taşır;altındaki Lantern75 kendi origin/HP’sini korur. Tekhücre beşinci vuruş bütün Wallparçasının mevcut komşuluk dalgasını başlatır;hedef bir parçadır.226v2bu alanı kullanır;eski assetler otomatik değiştirilmedi. Düşüş,animasyon ve ses kuralları aynı.\n')

delivery=BATCH/'Delivery'
for suffix in ('md','png'):
    p=DOC/f'Level226-Ruzgar-Kemeri.{suffix}';shutil.copy2(p,delivery/p.name)
shutil.copy2(BATCH/'Levels226-230.png',delivery/'Levels226-230.png')
for p in delivery.glob('*.md'):
    t=p.read_text().replace('](../../Assets/','](../../../../../Assets/').replace('](Batches/226-230-Lantern-Wind-Crystal/','](../')
    p.write_text(t)
(delivery/'sha256.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in delivery.iterdir() if p.is_file() and p.name!='sha256.json'},indent=2)+'\n')
print('226 revision notes/feedback/checkpoint and repository delivery updated; old drafts retained.')
