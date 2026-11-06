import json,shutil
from author import HERE,BATCH,ROOT,ASSET,sha
D=ROOT/'Docs/LevelDesign'
def put(p,s):p.write_text(s.strip()+'\n')
def prepend(p,e):
    s=p.read_text();title,rest=s.split('\n',1)
    if e.split('\n',1)[0] not in s:put(p,title+'\n\n'+e+'\n'+rest)
note='''# 229 · Rüzgâr Burgusu / Crystal Whirl

2026-11-06 · v4 agent taslağı; kullanıcı henüz oynamadı/onaylamadı.

9×11/99 aktif; **28 hamle Super Hard**,4 renk. Aktif V1 katalog `LevelCL_0229`; isim/GUID/katalog korunur. [Asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset).

Hedefler: **12 GrassWaterLily69,4 Wardrobe27,12 SculptingStone28,12 PlasticTwoStage40**. Çim altında sandık,jel ve jel presi yok. Nilüferin kendi çim tabanı mevcut mekanikle otomatik kurulur; nilüfer hedefi alt çim de temizlenince tamamlanır.

## Dört soru ve kalan iş

1. İlk görüntü: solda2×6 nilüfer,sağda dört dolaplık2×8 kule;altta kıvrılan krem kaide ve ona bitişik3×4 plastik bloğu.
2. İlk karar: nilüferin ilk yaprağı için special üret; yanında bekletirsen yutulabileceği için hemen kullanma/geri alma zamanını seç. Aynı zamanda dolap kulesine vuran hatlar hazırla.
3. İlk alan açılınca: oyma kaidesi alt çalışma alanını genişletir. Dolap kapağı açılınca içerikler hâlâ durur. İlk nilüfer hasarı sonraki yaprak ve çim işini bitirmez.
4. Son iş: kalan dolap içeriği,nilüfer yaprak/çim tabanı ve iki aşamalı plastik. Bir yardımcı atışı bütün zorunlu işi otomatik temizlemez.

Reddedilen v3’te12 gizli jel kaynağı + preslerin tam sütun temizlemesi birbirini kolaylaştırıyordu. Statik besleme ve doğru hedef sayıları bunun iyi bir Super Hard olduğunu göstermiyordu. v4’ün gerekçesi bağımsız aşamalar ve special zamanlamasıdır; yalnız hamle azaltılmadı.28 hamle ilk tahmin,ölçülmüş geçiş oranı değildir.

## Veri ve erişim

47 açık eşleşme yuvası + nilüferlerin altında12 kilitli taş yuvası =59 normal yuva;59/59 doğal beslenir. Pin/hazır special/başlangıç boşu yok. Nilüfer normal swap alanı gibi sayılmaz. Plastik bloğu tabana kadar dolu; başlangıçta havada çizilip ilk düşüşte başka bölgeye taşınacağı varsayılmadı. Dolaplar tabana kadar bitişik. Oyma kalkınca71/71,tüm temizlenebilir parçalar kalkınca99/99 besleme yolu var. Bu statik geometri incelemesidir;oyun simülasyonu değildir.

## Havuz,tekrar ve performans

Önceki tüm havuz incelemesi korundu;jel/presin yardım etkisi nedeniyle seçimi reddedildi. Nilüfer ilk special/yutma/geri alma,oyma alan açma,dolap kapak sonrası içerik,plastik hareketli iki aşama işi sağlar. GrassFlower hatırlatması bu levele zorunlu ekleme değildir. Yakın tarihsel3 ve126 gerçek sprite’larla açıldı: kenar/taban bantları ortak görsel dildir;şekle benzersizlik iddiası yok. Kalan iş ve special kullanımı farklıdır. [Karşılaştırma](Batches/226-230-Lantern-Wind-Crystal/Revisions/v4-229-lily-workshop/Comparison.png).

Mevcut nilüfer servisi olayla komşu kontrolü,sabit petal buffer’ı ve havuzlu special uçuşu kullanır;disable/teardown tutma/job/uçuş temizliği vardır. Dolapta ilk kapak/içerik görselleri oluşturulabilir;çıkan öğeler mevcut ObstacleScatterFx üzerinden gider,çocuk görseller kaldırılır. Mevcut maliyetler sürer;sıfır allocation veya FPS iyileşmesi iddiası yok. Bu level için runtime/VFX/ses/akış kodu değiştirilmedi.

Statik YAML/dizi/origin/footprint/hedef/ikon/katman/besleme/katalog/ad kontrolü PASS;6055diğer proje dosyası ve1–228/230 korundu. Gerçek PNG açıldı;serbest taş renkleri temsili ve kesit statiktir. Kullanıcı denge onayı yok. Unity build/Play,bot/düşüş/kaskat simülasyonu,yayın/baseline/commit yapılmadı.

[Önizleme](Level229-Ruzgar-Burgusu.png) · [Revizyon](Batches/226-230-Lantern-Wind-Crystal/Revisions/v4-229-lily-workshop/README.md)
'''
put(D/'Level229-Ruzgar-Burgusu.md',note)
put(HERE/'README.md','''# 229 · v4 nilüfer / dolap / kaide

Kullanıcı v3jel/pres taslağını kolaylık ve kuralları anlamama gerekçesiyle reddetti. Bu revizyon yalnız229’u değiştirir;reddedilen asset/not/PNG Before altında saklıdır.28hamle aynı kaldı;12nilüfer/4dolap/12oyma/12iki aşamalı plastik hedefleri yeni kalan işi kurar. Tam tasarım notu root Level229-Ruzgar-Burgusu.md dosyasındadır.

`python3 Docs/LevelDesign/Batches/226-230-Lantern-Wind-Crystal/Revisions/v4-229-lily-workshop/review.py` bağımsız Ruby YAML okuması,diziler,hedef/origin/footprint/katman,başlangıç ve açılma sonrası statik besleme,katalog/TR–ENad ve6055diğer proje dosyasının korunmasını kontrol eder. Bu kontrol oyun kolaylığını ölçmez.59normal yuvanın12’si nilüfer altındaki kilitli taş;47açık eşleşme yuvası vardır. Renk pini ve boş yuva yok.

Gerçek sprite başlangıç/kesit PNG ve3/126karşılaştırma PNG açıldı. Kenar ve taban bantları ortak;şekil benzersiz diye sunulmaz. Yeni taslak kullanıcı tarafından oynanmadı/onaylanmadı. Mevcut mekaniklerin havuz/iptal/cleanup maliyetleri kaynakta incelendi;FPS/GC ölçülmedi. Level işi runtime/akış/ses kodunu değiştirmedi.

Önceki parenttopluPNG ve v3jel/pres dosyaları tarihseldir;parentauthor çalıştırılmaz.226 kullanıcı tasarımı eski modele/hash’e döndürülmez. Bu revizyon yazıcısı yalnız229’u değiştirir;mevcut başka dosya drifti varsa durur.231+istenmedi. Unitybuild/Play/bot/düşüş-kaskat simülasyonu,yayın/baseline/commit yok.
''')
feedback='''## 229 — jel/pres taslağı reddi; açılış sonrası gerçek iş · kullanıcı · 2026-11-06

**Kaynak:**“taşın altına jel koymuşsun sonra spreading jel var … diye bitecek bir oyun bu … bazen beni hiç anlamadığını ve kurallara uymadığını düşünüyorum”;ardından“devam eder misin”.

**Durum:**v3 açıkça reddedildi. Statik PASS/PNG düzeni zorluk veya kaliteli oyun fikri kanıtı değildir. Çok sayıda gömülü jel kaynağını presin tam sütun temizlemesiyle birleştirmek bu229’da hedef yükünü erken tüketir. Sorun yalnız hamle veya görsel değildir.

**Ders/kapsam:**En güçlü erken açılış/yardımcı etkisinden sonra hangi zorunlu işin kaldığını mekanik üzerinden değerlendir. Birbirini kolaylaştıran yayılma/alan temizlemeyi bağımsız zorluk katmanı sayma. Bu,her levelde jeli veya örtülü jeli yasaklama talebi değildir. Önceki çim altında sandık kullanmama yönü229 için geçerli;GrassFlower/Lily havuz hatırlatması zorunlu liste değildir.

**Agent revizyonu:**v4nilüfer/dolap/oyma/iki aşamalı plastik;aynı28hamle. İlk special yaprak/alt çimi,dolap kapağı içerikleri bitirmez.47açık eşleşme yuvası ve12kilitli nilüferaltı taş doğal beslenir. Bu taslak henüz kullanıcı tarafından oynanmadı/onaylanmadı;statik/görsel kontrol denge kanıtı diye sunulmaz. v3 karşı örnek olarak arşivlenir.
'''
prepend(D/'Design-Feedback.md',feedback)
checkpoint='''## Güncel — 229 jel/pres reddi ve v4 · 2026-11-06

Kullanıcı229v3jel/presi erken bitecek oyun ve kuralları anlamama gerekçesiyle reddetti;devam istedi. Açılış sonrası kalan gerçek iş değerlendirilmeden statik PASS/PNG ile SuperHard iddia edilmez. Bu yalnız229revizyonudur.

Aktif229v4:9×11/99aktif,28hamle SuperHard;12GrassWaterLily69/4Wardrobe27/12SculptingStone28/12PlasticTwoStage40. Jel/pres/çim-altı-sandık yok.47açık eşleşme yuvası +12kilitli nilüferaltı taş=59/59doğal dolum,pin0,boş0. İlk nilüfer speciali sonraki yaprak/çimi;dolap kapağı içeriği bitirmez. Mevcut isim/GUID/katalog korunur.

Kayıt `Batches/226-230-Lantern-Wind-Crystal/Revisions/v4-229-lily-workshop/`;root229not/PNG güncel.6055diğer proje dosyası/diğer levellar ve225/226kullanıcı düzenlemeleri korundu. Eski parentauthor/226modeli çalıştırılmaz. Statik kontrol PASS;gerçek229PNG ve3/126karşılaştırması açıldı;şekil benzersiz veya denge ölçülmüş diye sunulmaz.28hamle ilkayar,yeni taslak kullanıcı onaylı/oynanmış değil. Build/Play/bot/düşüş-kaskat simülasyonu/yayın/baseline/commit yok.231+istenmedi.

Sıradaki açık iş: kullanıcı 229bitince dört yönlü lillydown/left/right/up sprite sheet adımlarıyla nilüfer special yutma animasyonu istedi. Geri toplanırken kapalı kareden mevcut şişkin görsele geçilecek. Bu ayrı yetkili görsel iştir;level geometrisini değiştirmez.
'''
for name in ['Memory.md','Design-State.md','SESSION-LEVEL-DESIGN-NOTES.md']:prepend(D/name,checkpoint)
skill=D/'Skills/tinyfixers-level-design/SKILL.md';s=skill.read_text();marker='Check the strongest plausible early helper/chain'
if marker not in s:s=s.replace('Early-game simplicity still needs', 'Check the strongest plausible early helper/chain against the remaining mandatory work. A spread goal and an area-clearing helper can accelerate the same task; do not count them as independent difficulty. Static validity and a readable PNG do not establish the intended difficulty. Record the user’s rejection and change the interaction, rather than only reducing moves.\n\nEarly-game simplicity still needs');put(skill,s)
p=BATCH/'README.md';s=p.read_text();title,rest=s.split('\n',1)
if '## Güncel229 — v4' not in s:s=title+'\n\n## Güncel229 — v4 · 2026-11-06\n\nv3jel/pres kullanıcı tarafından reddedildi. [Güncel v4](Revisions/v4-229-lily-workshop/README.md):28hamle;12nilüfer/4dolap/12oyma/12iki aşamalı plastik. Aşağıdaki v3ve ilk batch notları tarihseldir.\n'+rest
s=s.replace('| 229 | Rüzgâr Burgusu / Crystal Whirl | 28 · Super Hard | 99 Jel, 12 Oyma taşı, 2 Rodos, 12 Porselen |','| 229 | Rüzgâr Burgusu / Crystal Whirl | 28 · Super Hard | 12 Nilüfer, 4 Dolap, 12 Oyma taşı, 12 İki aşamalı plastik |');put(p,s)
put(HERE/'visual-review.json',json.dumps(dict(opened=['Level229.png','Comparison.png'],reviewer='agent',userApproved=False,played=False,assessment='Bitişik kenar/kaide grupları okunaklı;3/126ile kenar/taban dili ortak. İlk açılış sonrası nilüfer yaprak/çim ve dolap içerik işi kalır;bu mekanik incelemesi ölçülmüş zorluk değildir.'),ensure_ascii=False,indent=2))
delivery=HERE/'Delivery';delivery.mkdir(exist_ok=True)
for src in [ROOT/ASSET,HERE/'Level229.png']:shutil.copy2(src,delivery/src.name)
put(delivery/'Level229.md',note.replace('../../Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset','LevelV1_00002290.asset').replace('Level229-Ruzgar-Burgusu.png','Level229.png').replace('Batches/226-230-Lantern-Wind-Crystal/Revisions/v4-229-lily-workshop/','../'))
put(delivery/'SHA256.json',json.dumps({x.name:sha(x) for x in delivery.iterdir() if x.name!='SHA256.json'},indent=2))
print('229v4notes,feedback,workflow lesson and checkpoint updated. Ready for requested lily animation work.')
