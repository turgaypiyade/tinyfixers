"""Scoped notes and current checkpoint. Never rewrites earlier level notes/names."""
import json,re
from common import ROOT,HERE,review,DEFS
if (HERE/'active-revision.json').exists():
    raise RuntimeError('A later revision is active; use its update_docs.py rather than rewriting current notes from v1.')
from render import goal_label
ds=json.loads((HERE/'designs.json').read_text());validation=json.loads((HERE/'validation.json').read_text());by={d['level']:d for d in validation['levels']}
lines=['# 213–220 · Sekiz ayrı kompozisyon','',
 'Kullanıcı 2026-11-03 tarihinde sekiz seviye daha istedi. Hedef aralık aktif V1 katalogda **213–220**. 1–212 korunmuştur. Bu taslaklar kullanıcı tarafından henüz oynanıp onaylanmadı.','',
 'Fener, kuş evi, su kanalı ve değirmenler aynı tahtaya doldurulmadı; porselen, iki aşamalı plastik, renkli plastikler, ColorChest ve çiçekli çim set boyunca dağıtıldı. 211–220 yeni mekanik tanıtımı istisnası sürer. Sarı9/yeşil13, önceden açık mavi11/kırmızı12 renkli plastik mekaniğinin istenen renk varyantlarıdır; bağımsız yeni aile sayılmadı.','',
 '| Seviye | TR / EN | Tür | Hamle | Ana kompozisyon |','|---|---|---|---|---|']
for d in ds:
    v=by[d['level']];lines.append(f'| {d["level"]} | {d["name"]} / {d["titleEn"]} | {v["kind"]} | {d["moves"]} | {d["idea"]} |')
lines+=['','## Mekanik ve akış sınırları','',
 '- 213: 8 başlangıç feneri / 18 hedef; 10 ek fener yalnız 2 ve 6 numaralı sütunlarda. 220: 14 başlangıç / 18 hedef; 4 ek fener 1 ve 7 numaralı sütunlarda. Sütunlar sıfır tabanlı, fallback 0. Diğer hareketli hedefler başlangıç adedine eşittir.',
 '- 214/218/220 kuş hedefleri 26/24/28; ev adedini kırma hedefi yapmaz. Kenar L köşelerindeki kapalı iç yüzler special isabeti ister; bunların bulunduğu satırlarda açık saldırı hattı var.',
 '- 216/218 kanal uzunlukları 17/22 hücre (kaynak dahil). Tekrar/up-step yok; her biri aşağı yönlü son adımla en alt satırda biter. Kaynak hücresinde taş/engel yok; kalan kanal üstünde yalnız desteklenen 1×1 örtüler var.',
 '- 217/219: ikişer değirmen ve 40 un hedefi → çuval başına20 konum. Kanatlara yalnız special/joker hasarı gelir. LineV bütün sütunu tarar; hole hücresi dalgayı durdurmaz. Unun örtü/tahta dışı/su yüzünden sığmayan kısmı HUD canlı+gelecek ürün hesabında azalabilir; 40 kesin temizlenecek yerleştirilmiş un hücresi değildir.',
 '- 219: üstte18 çiçek →18 çim; ayrıca iki alt sandığın üstünde8 çim, toplam26. Solda yan yana iki değirmen, sağda yan yana iki çim kaplı sandık; 217’nin iki köşe değirmeni düzeni yinelenmedi.',
 '- 220: 9×10 Boss, 620HP / üç dalga. Başlangıç engelleri hedeflerde var. Baskı havuzu8/12/13 hedef DIŞINDA; kullanıcı 2026-11-01 kuralına uygun. Alet tehditleri de hedeflerle çakışmıyor. 210’daki mevcut karakter/HP/saldırı ayarları şablon olarak korundu; başlangıç kalkanı eklenmedi.',
 '- Açılışlarda tüm normal taş yuvaları doğal besleme veya gerçek authored renk pinleriyle dolu. 213/215/217/218 pin sayıları16/13/35/3. Diğerlerinde0. Sabit başlangıç special yok. Pinler kendi aralarında başlangıç üçlüsü oluşturmaz.',
 '- Statik aile-kaldırma kesitleri hareketli engelleri yerçekimiyle oynatmaz. Yeni açılmış sabit-engel yuvaları üzerinde hâlâ hareketli blok varsa bu hücreler ayrı `newlyExposedSlotsBelowMovables` kaydındadır; bunları ölçülmüş gerçek boşluk veya doğrulanmış yerçekimi sonucu sayma. Açılış ve tüm kaldırılabilir katmanların temizlendiği uç durumda beslenemeyen normal yuva yok.','',
 '## Doğrulama / görsel inceleme','',
 '- `python3 design.py`: yalnız sekiz yeni asset/meta, katalog ekleri ve sekiz TR/EN isim. `Before/` anlık dosya yedekleri ve `protected-before.json` yerel koruma hash’leridir; **app release baseline değildir**.',
 '- `python3 verify.py`: bağımsız Ruby/Psych Unity YAML okuması; tam dizi uzunluğu, mask/origin/footprint, katman, hedef, dört renk, refill kotası/sütunu, hazırlık pinleri, grup boyutları, kanal yönü, boss/tehdit ve GUID/katalog/isim kontrolü.2976 mevcut Settings/Scripts/Resources dosyası hash ile aynı.',
 '- `python3 render.py`: güncel proje sprite’larıyla sekiz büyük açılış + katman kesiti ve `Levels213-220.png`. Serbest taş renkleri temsili; gerçek pinler assette yazılıdır. Kanal kesitlerinde rota için taşlar saydamlaştırılmıştır.',
 '- Tekrar taraması `Reviews/213-220-Showcase/<level>/` içinde aktif V1 katalogla yapıldı. Her seviyenin konum/crop kısa listelerindeki örnekler `Comparisons/` sprite+geometri PNG’lerinde görsel olarak incelendi. Skorlar yenilik yüzdesi veya kullanıcı onayı değildir.',
 '- Unity build, Play Mode, bot/kaskat simülasyonu, cihaz/FPS veya kazanma oranı ölçümü, upload/publish yapılmadı. Hamleler ilk deneme bütçesidir.','',
 '## Mevcut performans maliyetleri','',
 'Runtime/VFX kodu değiştirilmedi. Her seviyede en fazla10 kuş evi, iki değirmen veya bir kanal var; dört yeni ana mekanik aynı tahtada birleşmiyor. BirdHouseService tek Update ve flyerPool kullanır, boşta kapanır; Clear/OnDestroy uçuşları goal yazmadan bitirip abonelikleri çözer. WaterChannelService mevcut damla havuzunu kullanır; Dispose abonelikleri ve maske dışı şelale kökünü temizler. WindmillService tek Update, tekrar kullanılan aday/yayılım listeleri ve değirmen alt Canvas’ları kullanır; Clear/OnDestroy aktif işleri ve state bağlarını temizler. Ancak çuval/bulut görsellerinde mevcut GameObject/Image üretimi, aday taramaları ve sıralama maliyetleri hâlâ vardır. Boss baskısı mevcut listeler ve tahta taramaları kullanır; aynı anda8 baskı objesi sınırı korunur. Bütün sesler mevcut merkezi board audio yolundadır. Bunlar kod incelemesiyle görülen sınırlardır, ölçülmüş FPS kazanımı değildir.','']
# Replace hand-entered path lengths with the audited counts.
lengths=[by[n]['channelLengths'][0] for n in (216,218)]
lines=[line.replace('17/22 hücre',f'{lengths[0]}/{lengths[1]} hücre') for line in lines]
(HERE/'README.md').write_text('\n'.join(lines))
for d in ds:
    n=d['level'];v=by[n]
    note=[f'# {n} · {d["name"]} / {d["titleEn"]}','',f'**{v["kind"]} · {d["width"]}×{d["height"]} · {v["active"]} aktif hücre · {d["moves"]} hamle · 4 renk.**','',d['idea'],'',f'İlk karar: {d["opening"]}','',f'Açıldıktan sonraki iş: {d["after"]}','','| Hedef | Adet |','|---|---|']
    note += [f'| {goal_label(g)} | {g["amount"]} |' for g in d['goals']]
    note+=['','```text',*d['rows'],'```','',
        f'Asset: `{d["asset"]}`. Katalog anahtarı `{d["levelKey"]}`; TR/EN adlar LevelNames.json’da. Hedefler origin/ürün/kanal türüne göre doğrulandı.',
        f'Açılışta {v["checkpoints"][0]["tileSlots"]} normal taş yuvası, {v["realPins"]} gerçek başlangıç renk pini, hazır special yok. Pinler kendi aralarında başlangıç üçlüsü oluşturmuyor; serbest renkleri motor doldurur. Katman ve dolum sınırları batch notundadır.','',
        f'[Proje sprite önizlemesi](Level{n}-{d["slug"]}.png) · [Grup tasarım notu](Batches/213-220-Showcase/README.md)','',
        'Statik veri/katman/maske/hedef/başlangıç dolumu ve PNG görsel incelemesi tamamlandı. Unity Play/build, bot/kaskat simülasyonu ve yayın yapılmadı. Hamle sayısı ilk ayar; kullanıcı oynanışı/onayı veya ölçülmüş denge yok.','']
    (ROOT/f'Docs/LevelDesign/Level{n}-{d["slug"]}.md').write_text('\n'.join(note))
canon=ROOT/'Docs/LevelDesign/Level-Names.md';text=canon.read_text()
for d in ds:
    line=f'| {d["level"]} | {d["name"]} |'
    if line not in text:text+=line+'\n'
canon.write_text(text)
def prepend(path,header,entry):
    p=ROOT/path;text=p.read_text()
    if header not in text:
        first,rest=text.split('\n',1);p.write_text(first+'\n\n'+header+'\n\n'+entry+'\n'+rest.lstrip('\n'))
prepend('Docs/LevelDesign/Design-Feedback.md','## 213–220 · yaratıcı şekiller / dağıtılmış engeller · 2026-11-03',
 '- **Kullanıcı:** “8 tane daha seviye yap”, “fener, bir home, water channel, yel değirmeni ... helmet porcealen, plastic two obstacle, color chest, grass with flower, plastic_xxx ... hepsini bir anda değil uygun şekilde dağıt”, “boardun ortasına ... tek başına duran objeler koyma”, “yelken gibi yaratıcı”, “çok engel koyduysa ... daha düz bir board”.\n'
 '- **Yetki/tercih:** Aktif V1 devamı213–220 açıkça yetkilendirildi. Yeni aile tanıtımı gerekmiyor; mevcut türler set içinde dağıtılır. Okunaklı, güzel siluet; anlamlı blok/bant/sütun/kenar konturu, yoğun işte daha sade maske. Ortada rastgele tekil obje yok.\n'
 '- **Ajan uygulaması (onay değildir):** Balon/çan/vitray/lale/kum saati/kıvrılan nehir/taç/festival.215Hard,219SuperHard,220Boss10satır. Kuş evleri kenarda, fenerler sütunda, kanallar alt çıkışlı;219Grass sandıkların üstünde.2976 önceki dosya hash ile aynı.\n'
 '- **Durum/sınır:** Sekiz taslak statik/görsel olarak incelendi; kullanıcı tarafından henüz oynanıp onaylanmadı.221 veya önceki levelları yeniden tasarlama yetkisi yok.\n')
prepend('Docs/LevelDesign/Design-Principles.md','**213–220 kullanıcı yönü · 2026-11-03:**',
 'Sekiz seviye için fener/kuş evi/su kanalı/değirmen; porselen, iki aşamalı plastik, ColorChest, çiçekli çim ve renkli plastiklerle set boyunca dağıtılır. Kullanıcının örneklerini tek tahtaya yığma. Az/orta türde yelken gibi okunaklı yaratıcı siluet; çok engelli kompozisyonda daha düz maske seçilebilir. Ortaya açıklamasız tekil obje serpiştirme. Bu tur213–220 yetkilidir; önceki onaylı levellar korunur.\n')
checkpoint='- **213–220 tamamlandı — 2026-11-03, kullanıcı denemesi bekliyor:** Açık sekiz seviye isteğiyle213Işıklı Balon22 ·214Guguk Çanı24 ·215Vitray Atölyesi26Hard ·216Sulu Lale22 ·217Kanatlı Kum Saati24 ·218Kıvrılan Nehir25 ·219Çiçekli Taç25SuperHard ·220Büyük Festival29Boss(9×10,620HP/3dalga) eklendi. Fener/kuş evi/kanal/değirmen set boyunca dağıtıldı; porselen/plastik/ColorChest/çiçekli çim grupları tamamlıyor. Sarı/yeşil plastik mevcut renkli plastik ailesinin varyantlarıdır.1–212(kullanıcının211=24hamlesi ve212 onaylı maskesi dahil), mevcut runtime ve2976 korunan dosya aynı. Batch `Batches/213-220-Showcase/` snapshot/yazıcı/statik audit/katman PNG/toplu önizleme/karşılaştırmalar; sekiz asset/meta, katalog ve TR/EN adlar hazır. Statik YAML/dizi/origin/hedef/başlangıç dolumu/kanal/boss/gruplama kontrolleri ve gerçek PNG incelemesi var; hareketli engellerin düşüşü simüle edilmedi. Oynanmış denge/onay, Unity build/Play, bot/kaskat, yayın veya app-release baseline yok. **221 istenmeden eklenmez.**\n'
prepend('Docs/LevelDesign/Design-State.md','<!-- current: 213-220-showcase -->',checkpoint)
prepend('Docs/LevelDesign/Memory.md','## Güncel devam noktası — 213–220 · 2026-11-03',checkpoint+'\nBu kayıt aşağıdaki “213 istenmedi / yalnız211 yetkisi” gibi tarihsel durumların önündedir. Yeni çalışma221’e geçmek değil, kullanıcının213–220 deneme geri bildirimini almaktır.\n')
enum=(ROOT/'Assets/_Project/Scripts/Core/LevelData.cs').read_text();enum=enum[enum.index('public enum ObstacleId'):enum.index('public static class LampStripColor')]
labels={int(m[2]):m[1] for m in re.finditer(r'^    (\w+) = (\d+)',enum,re.M)}
old=review.load(ROOT/'Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset');pool=[]
for o in sorted(DEFS):
    ns=[d['level'] for d in old if d['level']<=212 and str(o) in map(str,d['obstacleInstances'])]
    pool.append(dict(id=o,name=labels.get(o,'legacy'),firstAuthoredV1=min(ns) if ns else None,recentUses=ns[-6:],selectedLevels=[d['level'] for d in ds if any(oid==o for cell in __import__('common').layer_model(d) for oid,_ in cell)]))
(HERE/'Pool-Review.json').write_text(json.dumps(dict(note='All definitions considered; variants, products, producers and stages are not independent new mechanics. Plastic_Yellow9/Green13 are requested colour variants of known Blue11/Red12; goal use does not create a new mechanic. Windmill represented by Flour83; BirdHouse by collectible Bird3.',definitions=pool,alternativesConsidered=['A third sail/ship: rejected internally to preserve212 as its own silhouette.','Four disconnected mill rooms: not chosen;209 already uses that vocabulary.','Repeated wall/metal/stone/cabinet set: not chosen; newest user asked this broader distribution.','One board combining all four newer mechanics: rejected internally for readability and goal-slot cost.']),ensure_ascii=False,indent=2)+'\n')
print('Eight design notes, TR name index, current checkpoint, explicit feedback and pool review written.')
