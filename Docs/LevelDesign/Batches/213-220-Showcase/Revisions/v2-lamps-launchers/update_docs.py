"""Current notes for four authorised revisions; previous state remains beside this script."""
from pathlib import Path
import json,sys
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1]
ROOT=next(p for p in HERE.parents if(p/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset').exists())
sys.path.insert(0,str(BATCH))
if json.loads((BATCH/'active-revision.json').read_text())['revision']!='v2-lamps-launchers':
    raise RuntimeError('A newer revision is active; use its documentation updater.')
from render import goal_label
ds=json.loads((BATCH/'designs.json').read_text());validation=json.loads((BATCH/'validation.json').read_text());reports={d['level']:d for d in validation['levels']}
for d in ds:
    if d['level'] not in (213,215,217,218):continue
    n=d['level'];v=reports[n];kind='Hard' if n==215 else 'Normal'
    note=[f'# {n} · {d["name"]} / {d["titleEn"]}','',f'**{kind} · {d["width"]}×{d["height"]} · {v["active"]} aktif hücre · {d["moves"]} hamle · 4 renk.**','',
        '**Güncel revizyon: v2-lamps-launchers, 2026-11-03.** Önceki şekil/yerleşim bu revizyon klasöründe saklı. İsim ve GUID aynı.','',d['idea'],'',f'İlk karar: {d["opening"]}','',f'Açıldıktan sonraki iş: {d["after"]}','','| Hedef | Adet |','|---|---|']
    note += [f'| {goal_label(g)} | {g["amount"]} |' for g in d['goals']]
    note += ['','```text',*d['rows'],'```','',
        f'Açılışta {v["checkpoints"][0]["tileSlots"]} normal taş yuvası, {v["realPins"]} gerçek renk pini; hazır special yok. Pinler kendi aralarında başlangıç üçlüsü oluşturmuyor. Serbest taş renkleri motor tarafından üretilir.','']
    if n==213:note+=['Sepetin iki yanı Up59 jel kartuşu (1×2). İkisi de mevcut plastik sepet grubuna bitişiktir.6 iki-aşamalı plastik kalır. Fener8 başlangıç/18 hedef ve sütun2/6 refill korunur. Jel hedefi61 aktif hücre; kartuşlar hedef olmayan yardımcıdır. Hamle22→24.','']
    if n==215:note+=['Renkli plastik9/11/12 kaldırıldı. İki2-hücre genişliğinde sütun: porselen üst2 sıra, iki-aşamalı plastik sonraki4 sıra, çim kaplı iki sandık sonraki4 sıra. Dış sütun0/8 ve orta3/4/5 açık. Dipte iki bitişik Right62 ve iki bitişik Left61 kartuş. Gel99, PlasticTwo16, ColorChest4, Grass16, Helmet8 hedefleri; hamle26 aynı. Kartuş veya sandık önce açıldığında dolum yolu statik olarak kontrol edildi.','']
    if n==217:note+=['Yeşil plastik13 kaldırıldı. Omuzlarda iki dallanmayan L şerit; her biri4 lamba, birer mor uç. İki şerit de hamle sonunda kayar; sağ parça reverse1. Goal hücre/lamba8 değil, iki çerçevedir. Değirmen2 / Un40, porselen5, iki-aşamalı plastik13 ve24hamle korundu.','']
    if n==218:note+=['Mavi11/kırmızı12 plastik kaldırıldı. İki C şerit, her biri5 lamba: sol sabit, sağ kayar/ters; birer mor uç. Kanal yeni x5 hattından iki şeridin arasından, örtüşmeden iner ve aşağı biter. Goal2 çerçeve; Su Kanalı1, Kuş24, ColorChest2 ve25hamle korunur.','']
    note += [f'[Güncel proje sprite önizlemesi](Level{n}-{d["slug"]}.png) · [Revizyon notu](Batches/213-220-Showcase/Revisions/v2-lamps-launchers/README-current.md)','',
        'Statik YAML/dizi/origin/katman/hedef/kanal/başlangıç dolumu ve PNG incelemesi var. Hareketli engellerin düşüşü, Unity build/Play, bot/kaskat veya cihaz dengesi test edilmedi. Kullanıcı henüz bu revizyonu oynayıp onaylamadı.','']
    (ROOT/f'Docs/LevelDesign/Level{n}-{d["slug"]}.md').write_text('\n'.join(note))
body=['# 213–220 · Güncel dağılım (v2)','',
 'Kullanıcı219’da çim altındaki ColorChest ampullerinin görünmediğini bildirdi; ayrıca LampStrip’i217 ve bir başka seviyede, GelLauncher’ı iki seviyede istedi. Plastik renk varyantlarının kaldırılmasına ve gerektiğinde maskeye müdahaleye izin verdi.','',
 '| Seviye | Güncel ana değişiklik | Hamle |','|---|---|---|',
 '|213 Işıklı Balon|Sepetin iki kenarında yukarı ateşleyen2 kartuş; Jel61 hedefi|24 (22→24)|',
 '|215 Vitray Atölyesi · Hard|Renkli plastikler kalktı; iki sütun ve çim kaplı4 sandık; dipte4 kartuş; Jel99|26|',
 '|217 Kanatlı Kum Saati|Yeşil plastik yerine iki kayan L lamba çerçevesi|24|',
 '|218 Kıvrılan Nehir|Mavi/kırmızı plastik yerine iki C çerçeve; sol sabit/sağ kayan; kanal aradan|25|','',
 '**214,216,219,220 level assetleri;1–212; bütün meta/GUID’ler, katalog ve TR/EN adlar bu tur korunmuştur.**219’un hata düzeltmesi runtime görünüm kaydındadır; leveli değiştirilmedi. Özgün balon/kum saati/nehir maskeleri korundu.215 aynı9×11 dikdörtgende yeniden düzenlendi.','',
 '## ColorChest görünüm düzeltmesi','',
 'Grass yok edilmesi aynı origin’deki önceden kurulmuş `_chestViews` kaydını kaldırıyordu. Artık yalnız gerçek ColorChest yok edilmesi kaldırır. Açılma ve ampul silme olayları mevcut prefab’a ulaşır; yeni view/child üretimi veya tarama eklenmez. Bkz. `Docs/Checks/ColorChest-Cover-Reveal.md`.Eski hata izole testte üretildi; yeni384 kontrol/48sıra ve önceki413/26/6 regresyon kontrolleri geçti.','',
 '## Veri / mekanik sınırları','',
 '- LampStrip yalnız217/218’de var; iki levelde de2 frame hedeflenir.217’nin iki L’si hareketli;218’in C’lerinden sol sabit, sağ hareketli. Yollar dallanmıyor; her şeritte bir mor lamba special gerektirir. Renkler dört taş rengiyle uyumlu.',
 '- GelLauncher yalnız213/215’te var; sırasıyla2 ve4 kartuş. Hedef olmayan yardımcıdır. Gel46 hedefleri tüm aktif alan61/99; bu iki boardda kalıcı üretici veya su kaynağı yok. Yardım ateşi kendi hücrelerini ve yolu jeller; geri kalanı eşleşme/special ile yayılır. Jel gösterim önizlemede ateşlenmiş gibi uydurulmadı.',
 '- 213’te dikey kartuşlar sepetin iki yanına bitişik; yukarı atış aradaki mask hole’larını geçer. `GelLauncherFireAction.BuildPath` koordinatları board sınırına kadar toplar; hole hücreleri darbede atlanır, yol orada bitmez.',
 '- 215’te dört yatay kartuş tek alt bantta, merkez açıklığına bakar. Üzerlerindeki iki sandık sütunu hem iç hem dış besleme yüzüne sahip. Açılışta51 normal yuva doğal dolar; pin gerekmiyor. Kartuşların tek başına veya sandıkların tek başına kaldırıldığı statik uçlar da kontrol edildi.',
 '- 218 kanal19 hücreden oluşur (kaynak dahil). Lamba çerçevesiyle kesişmiyor, son adımı alt sınırda aşağı. Kalıcı kuş kıyısı /24kuş ve iki sandık korundu.',
 '- Her level en fazla5goal. Plastik9/11/12/13 seçilen dört levelden kalktı; PlasticTwo40 ayrı iki-hit aile olarak uygun yerlerde kaldı. 221 veya sonrası için ek seviye/yayın yetkisi çıkarılmadı.',
 '- Authoring snapshot’ları yerel geri alma/veri koruma kaydıdır; **app release baseline değildir**. V1 yazıcısı/denetçisi aktif revizyonu ezmeyecek şekilde yönlendirildi. `revise.py` seçili assetlerin son yazım hash’i farklıysa durur; sonradan kullanıcı değişikliği sessizce geri alınmaz.','',
 '## Kontroller ve performans','',
 'Bağımsız Ruby/Psych YAML okuması; dizi/mask/footprint/origin/katman/goals; şerit bağlantısı/degree/renk/parça ayarı; jel hedefi ve kartuş yönü; kanal ayrıklığı; gerçek pinler; açılış, kartuş, sandık ve tüm kaldırılabilir katmanların temizlendiği geometrik kontrol noktaları.2989 kapsam dışı Settings/Scripts/Resources dosyası aynı. Hareketli engeller sabitlenmiş statik kesitlerde yeni açılan alt hücrelerin gerçek gravity sonucu olduğu iddia edilmez; modeldeki sınır ayrı raporlanır.','',
 'Yeni runtime/VFX eklenmedi. LampStripService mevcut tek Update ve tekrar kullanılan hücre listelerini kullanır; Clear/OnDestroy shift/fall işleri, hold/job ve state aboneliklerini temizler. GelLauncherPressView mevcut ortak driver/moving listesini, cap/gel-görsel havuzlarını kullanır; FireAction finally blokları alt coroutine’leri durdurur, havuz görüntülerini iade eder ve flow hold/job’ları bırakır. Ateş yolunda mevcut List/impact-pass maliyetleri ve başlangıç görünüm kurulumları vardır. ColorChest düzeltmesi mevcut view’ı korur; kapak efektlerinin mevcut allocation maliyetleri değişmedi. Sesler merkezi board audio yolunda kaldı. Cihaz FPS/denge ölçümü veya performans artışı iddiası yok.','',
 'Güncel proje sprite’larıyla dört revizyon PNG’si ve toplu sayfa açılıp incelendi. Press çizimi güncel Editor preview’ındaki frame/gel/head UV crop ve yönleriyle, LampStrip çizimi güncel auxiliary sprite’larla yapıldı. Temsili serbest renkler ve statik katman kesitleri işaretli. Tekrar kısa listesi `Reviews/213-220-Showcase-v2/` altındadır; skorlar yenilik/onay yüzdesi değildir.','',
 '**Unity build/Play, bot/kaskat/gravity simülasyonu, cihaz oyunu, upload/publish yapılmadı.**24/26/24/25hamle ilk revizyon ayarlarıdır; kullanıcı denemesi beklenir.','']
(HERE/'README-current.md').write_text('\n'.join(body))
main=['# 213–220 · Güncel seviye seti','',
 '**Aktif revizyon: v2-lamps-launchers.** [Güncel revizyon / doğrulama notları](Revisions/v2-lamps-launchers/README-current.md). Önceki set `Revisions/v2-lamps-launchers/` içinde snapshot olarak saklıdır.','',
 '| Seviye | TR / EN | Tür | Hamle | Güncel fikir |','|---|---|---|---|---|']
for d in ds:
    n=d['level'];kind='Boss' if n==220 else 'Super Hard' if n==219 else 'Hard' if n==215 else 'Normal'
    main.append(f'|{n}|{d["name"]} / {d["titleEn"]}|{kind}|{d["moves"]}|{d["idea"]}|')
main+=['','LampStrip:217/218. GelLauncher:213/215.219’daki Grass→ColorChest ampul kaydı runtime’da düzeltildi;219 asseti aynı. Katalog/isim/GUID ve diğer levellar korunur. Güncel `verify.py` revizyon denetçisine gider. Eski `design.py` / `write_docs.py` aktif revizyonu v1 ile ezmez.','',
 'Hamleler ilk deneme ayarıdır; statik/görsel kontroller oynanmış denge yerine geçmez. Unity build/Play, bot/kaskat ve yayın yapılmadı.','']
(BATCH/'README.md').write_text('\n'.join(main))
def prepend(rel,title,text):
    p=ROOT/rel;s=p.read_text()
    if title not in s:
        first,rest=s.split('\n',1);p.write_text(first+'\n\n'+title+'\n\n'+text+'\n'+rest.lstrip('\n'))
checkpoint='- **213–220 v2 / ColorChest görünümü — 2026-11-03:** Kullanıcı219’da çim altındaki sandığın ampullerinin görünmediğini bildirdi.Grass destruction artık yalnız gerçekColorChest yıkımında `_chestViews` kaydını kaldırır; mevcut prefab korunur. Eski hata üretildi,384/48sıra test ve413/26/6 regresyon kontrolleri geçti. Kullanıcı isteğiyle **LampStrip217+218, GelLauncher213+215**:217iki kayanL,218ikiC(sol sabit/sağ kayan), kanal aradan;213sepet yanında ikiUp kartuş+Gel61,215dipte dört yatay kartuş/çim üstü dört sandık+Gel99. Seçilen4levelden renkli plastikler kalktı. Hamle213=24(22→24),215=26,217=24,218=25; isim/GUID/katalog aynı.214/216/219/220assetleri ve1–212, diğer kodlar2989hash ile korundu. Batch `Revisions/v2-lamps-launchers/` aktif author/audit/önceki snapshot; dörtPNG/toplu önizleme güncel. Statik ve görsel kontrol var; Unity build/Play/bot/kaskat/gravity/cihaz dengesi/yayın yok. **Oynanıp onaylanmadı;221 istenmedi.**\n'
prepend('Docs/LevelDesign/Design-State.md','<!-- current: 213-220-v2-lamps-launchers -->',checkpoint)
prepend('Docs/LevelDesign/Memory.md','## Güncel revizyon — LampStrip / GelLauncher · 2026-11-03',checkpoint+'\nÖnceki213–220v1 durumu tarihsel kaldı. Kullanıcı yardımcı jel kartuşlarını iki levelde, sıralı lambaları217 ve bir başka levelde istedi; yerel revizyon yetkisidir.\n')
prepend('Docs/LevelDesign/Design-Feedback.md','## 219 ColorChest hatası + iki LampStrip / iki GelLauncher · 2026-11-03',
 '- **Kullanıcı:** “219 ... grass altında color chest ... boş açıldı ama hit alıyor ... ampulleri göstermiyor”, “217 ve senin seçeceğin bir tanesinde ... lampstrip”, “gellauncherı2oyunda lampstripiiki oyunda”, “plastic_xxx ... kaldırabilirsin ... gerekirse boardu da değiştirebilirsin”.\n'
 '- **Kapsam:**219 görünüm hatasını düzelt;217 dahil ikiLampStrip ve ikiGelLauncher seviyesi seç. Renkli plastikleri kaldırmak/tahtayı uyarlamak yetkili. Açık şekil beğenisi veya tüm seviyeleri yeniden kurma talebi değildir.\n'
 '- **Ajan uygulaması (onay değil):**Lamp217/218, Launcher213/215; maskeler korunup215sütun düzeni kuruldu.219asseti korunarak typed-view cleanup düzeltildi. Statik/PNG ve izole production-method kontrolleri var; kullanıcı yeni yerleşimleri henüz oynamadı/onaylamadı.\n')
print('Current four design notes, batch/revision README, feedback and checkpoints updated.')
