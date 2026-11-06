"""Scoped229 documentation/delivery only. Does not write game assets."""
import json,hashlib,shutil
from pathlib import Path
HERE=Path(__file__).resolve().parent;BATCH=HERE.parents[1];ROOT=HERE.parents[5];DOCS=ROOT/'Docs/LevelDesign'
def put(p,s):p.write_text(s.strip()+'\n')
def prepend(p,entry):
    s=p.read_text();heading,rest=s.split('\n',1)
    if entry.split('\n',1)[0] not in s:put(p,heading+'\n\n'+entry.strip()+'\n'+rest)

note='''# 229 · Rüzgâr Burgusu / Crystal Whirl

2026-11-06 · Yeni agent taslağı; kullanıcı henüz oynamadı/onaylamadı.

9×11,99 aktif hücre; **28 hamle · Super Hard · 4 renk**. Aktif V1 katalog `LevelCL_0229`, bölüm10. [Level asseti](../../Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset). Mevcut TR/EN isim ve GUID korunur.

| Hedef | Adet |
| --- | ---: |
| Jel kaplama | 99 |
| Oyma taşı | 12 |
| Rodos heykeli | 2 |
| Porselen | 12 |

## Tasarımın dört sorusu

1. **İlk görüntü:** Soldan merkeze kıvrılan bitişik oyma omurgası; sağda iki porselen sütunu, tabanda iki jel presi ve iki Rodos.
2. **İlk karar:** Oyma omurgasının hangi yüzünden jeli açacaksın; iki presi birlikte vurabilecek special hattını nasıl kuracaksın?
3. **Açılınca:** Her oyma taşı altındaki gerçek jel hücresini açar. Yukarı bakan presler ortadaki iki sütunun açılmasını ve jelle boyanmasını sağlar.
4. **Kalan iş:** Jeli99 hücreye taşı; special isteyen Rodosları ve sağdaki12 porseleni bitir. Örtü altından sandık toplama döngüsü kullanılmadı.

## Katman, erişim ve hamle

12 SpreadingGel46 taban hücresinin üzerinde12 SculptingStone28 var; oyma üç vuruşlu. Çim ve sandık yok. GrassFlower58 / GrassWaterLily69 havuzda tutuldu, bu levele zorunlu eklenmedi. İki GelLauncherUp59 tabanda bitişik1×2 gövdeler; special ile iki vuruşta çalışır, kendi hücrelerini de jelle kaplayarak kalkar. Mevcut yardımcı kuralıyla pres için ayrı hedef konmaz.99 jel hedefi preslerin ve heykellerin kapladığı hücreleri de kapsar.

İki2×2 RhodesStatue70 tabana oturur; heykel başına dört special vuruşu gerekir.12 hareketli HelmetPorcelain30 sağda2×6 sütun oluşturur; gövdelerin altı Rodoslarla desteklenir, açılış yerleşimi havada bırakılmadı. Porselen hedefi gerçek başlangıç adedi12’dir; ek üretim kotası yok.

Başlangıçta63 normal taş yuvasının63’ü doğal beslenir; pin,hazır special,boş yuva yok. Oyma kalkınca75/75; presler de kalkınca79/79; bütün temizlenebilir parçalar kalkınca99/99 beslenir. Bunlar seri oyun hamleleri değil, bağımsız statik katman/geometri kontrolleridir. Taş renkleri temsilidir.

28 hamle ilk tahmin: üç vuruşlu omurgaya erişim, iki presi çalıştırmak, iki Rodos için special hazırlamak ve jeli tüm alana taşımak birlikte düşünülmüştür. Geçiş oranı veya oynanmış denge iddiası yok.

## Havuz ve tekrar incelemesi

Güncel ObstacleLibrary ve aktif katalog üzerinden tüm havuz değerlendirildi. Dimerdal/değirmen son228/230 ve eski229’daki kaynak toplama fikrini tekrar ettirecekti. Boru/kanal/mıknatıs yolları, diğer örtüler/sıvılar ve yumurta/roket/pil yardımcıları da değerlendirildi; bu fikri dağıtmamak için eklenmedi. Yeni mekanik tanıtımı yok.

Statik kısa listenin konumsal en yakını114, kırpılmış en yakını183 gerçek sprite’larla açıldı:114’te solda kasa bandı, üstte yumurtalar ve altta çim örtülü geniş oyma alanı;183’te aynalı iki taş basamağı ve ortada plastik/yanlarda boya kutuları var.229 tek çapraz omurga ve tabandaki pres–Rodos ilişkisiyle farklı açılır. Basamak ritmi ortak bir görsel ögedir; benzerlik puanları özgünlük/onay yüzdesi değildir. [Karşılaştırma](Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/Comparison.png).

## Kontrol sınırı

Bağımsız YAML/dizi/katman/origin/footprint/hedef/ikon/besleme/katalog/TR–EN ad kontrolü geçti.6055 diğer proje dosyası,1–228 ve230 korundu;225/226’daki mevcut kullanıcı düzenlemeleri korunur. Gerçek sprite PNG ve katman kesiti açılıp incelendi; görsel değerlendirme agent’a aittir. CPU/GC/FPS ve oynanmış denge ölçülmedi. Unity build/Play,bot/düşüş/kaskat simülasyonu,yayın,baseline veya commit yapılmadı.

[Önizleme](Level229-Ruzgar-Burgusu.png) · [Revizyon kaydı ve performans incelemesi](Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/README.md)
'''
put(DOCS/'Level229-Ruzgar-Burgusu.md',note)
readme='''# 229 · v3 jel / oyma revizyonu

2026-11-06. Kullanıcı yalnız229 için yeni tasarım istedi; çim altında sandık istemedi. Grass Flower ve Grass Lily hatırlatması bu levele zorunlu ekleme talebi değildir. Önceki229, Before altında korunur. Eski batch226 modelini güncel kullanıcı assetine benimsetmeyin; diğer levelları yeniden yazmayın.

9×11/99aktif,28hamle SuperHard.12 jel tabanı üstünde12 oyma taşı; sağda12 porselen,tabanda iki yukarı jel presi ve iki Rodos. Hedefler99Jel/12Oyma/2Rodos/12Porselen. Pres yardımcıdır; ayrı hedef yok. Bütün taş yuvaları doğal beslenir; başlangıç63/63,pin0,boş0. Yeni mekanik tanıtımı,refill kotası veya isim değişikliği yok.

## Tekrar ve görsel kontrol

`Pool-Review.json` güncel kütüphane/havuz seçimini kaydeder. `Reviews/229-Gel-Sculpture-v2` bütün aktif katalog tekrar envanteridir.114 konumsal,183 kırpılmış en yakın aday olarak gerçek sprite karşılaştırmasında açıldı.114’ün sol kasa bandı/yumurta üst alanı/çim-oyma alt alanı ile183’ün iki aynalı taş basamağı incelendi.229’daki tek çapraz omurga,alttaki iki pres ve sağdaki porselen/Rodos işi farklı açılma sırası kurar; basamak görsel ritmi ortaktır. Skorlar özgünlük yüzdesi veya kullanıcı onayı değildir.

`Level229.png` ve `Comparison.png` açılıp incelendi. Normal taş renkleri temsili; kesit,oyma katmanı kaldırılmış statik gösterimdir. Gelecekteki jel yayılımı uydurulmadı. Pres,mevcut PressFrame/PressGel/PressHead dokuları ve başlangıç pozu ile çizildi; çubuğun shader şeridi yalnız statik yaklaşımdır. Önizleme motor ekran görüntüsü değildir.

## Mekanik ve performans incelemesi

Seçim güncel kütüphane,LevelData,GridSpawner,ObstacleStateService,GelLauncherFireAction ve ilgili görsel servisler üzerinden kontrol edildi. Oyma üçhit;pres special-only ikihit;Rodos special-only dörthit. Pres yukarı yönde mevcut hücre etki geçişini kullanır; kalan engel katmanları ve gizli jel için yeni runtime kuralı yazılmadı.

Mevcut SpreadingGelOverlayService hücre görsellerini bir kez oluşturup sözlükte tutar; yeni jel hücresi ve köşe yamaları GameObject yaratabilir,ClearAll temizler.99 hücreye yayılmanın mevcut görsel maliyeti sürer; havuzlu/sıfır allocation iddiası yok. Pres paylaşılan hareket sürücüsü ve kaynak dokularını kullanır; hareket iptali,board nesil kontrolü,disable temizliği mevcut kodda incelendi. Atış başına mevcut yol/deferred listeleri ve coroutine maliyeti sürer; finally alt işleri durdurur,kapağı havuza verir,gövdeyi temizler ve hücre tutmalarını bırakır. Buhar UiVfxPool üzerinden kiralanıp paylaşılan sürücüde geri bırakılır; Rodos parçaları mevcut ObstacleScatterFx yolunu kullanır,ilk parça görsellerinde oluşturma maliyeti vardır. Yeni per-frame tarama,coroutine,ses kaynağı veya VFX sistemi eklenmedi. Oyun açılışındaki mevcut taramalar ve efekt maliyetleri ölçülmedi; CPU/GC/FPS iyileşmesi iddia edilmez.

## Kapsam ve yeniden kontrol

`python3 Docs/LevelDesign/Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/verify.py`

Bağımsız YAML okuması/diziler/origin/footprint/katman/hedef/ikon ve dört besleme evresi PASS.6055 diğer Assets/_Project dosyası SHA ile aynı; bütün diğer levellar,katalog,GUID/meta,TR/EN adlar,mevcut runtime/art ve225/226 kullanıcı değişiklikleri korundu. Yalnız229 oyun asseti değişti. Parentmodelde yalnız229 güncellendi;parentactive-revision ek229revizyonuna işaret eder. Eski parent author/verify kullanıcı226 driftini aşmamalıdır; bu revizyon için bu scopedverify kullanılır.

Parent `Levels226-230.png` ve eski parent karşılaştırması tarihsel229 gösterir; güncel229 için bu klasördeki PNG/not kullanılır. Diğer levelları eski modelden render/author etme. `Delivery/` güncel229 not/PNG/asset kopyası ve SHA manifestidir; dış servise yükleme değildir.

Statik kontrol ve agent görsel değerlendirmesi tamamlandı.28 hamle ilk ayardır;kullanıcı henüz oynamadı/onaylamadı. Unity build/Play/cihaz,bot/düşüş/kaskat simülasyonu,ölçülmüş denge, yayın,releasebaseline veya commit yok.231+ istenmedi.
'''
put(HERE/'README.md',readme)
checkpoint='''## Güncel — yalnız229 yeni tasarım · 2026-11-06

Kullanıcı229 için yeni level istedi;“çim altında sandık kullanma” dedi. GrassFlower58 ve GrassWaterLily69 bütün havuzda değerlendirilecek seçeneklerdir; bu levele hemen eklenmeleri gerekmiyor. Bu açık istek,önceki “levelı ben tasarlayacağım” kapsamını **yalnız229 için** değiştirir;diğer levelları yeniden tasarlama yetkisi vermez.

229 Rüzgâr Burgusu / Crystal Whirl yeni taslak:9×11/99aktif,28hamle SuperHard;12jel tabanı üstünde12oyma,sağda12porselen,tabanda iki yukarı jel presi ve ikiRodos. Hedef99jel/12oyma/2Rodos/12porselen. Çim/sandık yok;63/63başlangıç yuvası doğal beslenir,pin0,boş0. Güncel kayıt `Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/README.md`; root229not/gerçekPNG güncel. Eski parenttopluPNG229 için tarihseldir.

6055diğer proje dosyası ve1–228/230 aynı;225=27hamle,226=20hamle ve mevcut kullanıcı geometri değişiklikleri korundu. Eski226model/hash’ini kullanıcı assetine benimsetme veya eski batchauthor’ı çalıştırma.229isim/GUID/meta/katalog korundu. Statik YAML/katman/hedef/footprint/besleme kontrolleri PASS;114/183gerçek sprite karşılaştırması ve yeniPNG açıldı. Bu agent görsel değerlendirmesidir;28hamle ilkayar,yeni229henüz oynanmadı/onaylanmadı. Runtime/art/akış kodu değişmedi;build/Play/bot/düşüş-kaskat simülasyonu,yayın/baseline/commit yok.231+istenmedi. Alttaki eski229çim/sandık/27hamle kayıtları tarihseldir.
'''
for name in ['Memory.md','Design-State.md','SESSION-LEVEL-DESIGN-NOTES.md']:prepend(DOCS/name,checkpoint)
feedback='''## 229 — yeni tasarım / çim-sandık tekrarı / havuz hatırlatması · kullanıcı · 2026-11-06

**Kaynak:**“229 için yeni bir level tasarla lütfen çim altında sandık kullanma farklı birşeyler kullanabilirsin… şu anda eklemene gerek yok ama grass flower ve grass lilly bazen pas mı geçiyorsun hatırlatma için yazıyorum”.

**Durum/kapsam:**Eski229yerine yeni tasarım açıkça istenir;bu229’da çim altında sandık kullanılmaz. Diğer level tasarımlarını değiştirme yetkisi yoktur. GrassFlower58 ve GrassWaterLily69 gözden kaçırılmayacak havuz seçenekleridir;229’a zorunlu ekleme değildir. Bütün levellarda çim veya sandık yasaklandı diye genellenmez.

**Tekrar kullanılacak ders:**Yalnız son eklenen kaynakları veya çim-sandık döngüsünü tekrarlama;mevcut havuzdaki farklı açılma/yayılma/yardımcı mekaniklerini değerlendir. Kullanıcının hatırlatması bir zorunlu bileşen listesi değildir.

**Agent uygulaması/sınır:**229’da oyma altıjel,iki pres,ikiRodos ve porselen sütunu seçildi;28hamle SuperHard. Flower/Lily bu seçime zorlanmadı. Statik/görsel kontrol var;yeni taslağın kullanıcı onayı veya oynanmış dengesi yok. Önceki229not/asset/PNG revizyonBeforeklasöründe saklı.
'''
prepend(DOCS/'Design-Feedback.md',feedback)
p=BATCH/'README.md';s=p.read_text();header='''## Güncel229 — v3 jel/oyma · 2026-11-06

Yalnız229 yeniden tasarlandı:28hamle SuperHard,99jel/12oyma/2Rodos/12porselen;iki jel presi. [Güncel229 kaydı](Revisions/v3-229-gel-sculpture/README.md) ve [önizleme](Revisions/v3-229-gel-sculpture/Level229.png). ParenttopluPNG/karşılaştırma ve aşağıdaki ilk batch ölçümleri229 için tarihseldir.226 kullanıcı tarafından ayrıca düzenlenmiştir;v2modelini yeniden yazmayın. Güncel229kontrolü scopedverify ile yapılır;eski batchauthor çalıştırılmaz.
'''
if '## Güncel229 — v3' not in s:
    title,rest=s.split('\n',1);s=title+'\n\n'+header+'\n'+rest
s=s.replace('| 229 | Rüzgâr Burgusu / Crystal Whirl | 27 · Super Hard | 16 Elmas, 24 Un, 12 Sarı renkli sandık, 12 Çim |','| 229 | Rüzgâr Burgusu / Crystal Whirl | 28 · Super Hard | 99 Jel, 12 Oyma taşı, 2 Rodos, 12 Porselen |')
put(p,s)
visual={'reviewer':'agent','userApproved':False,'played':False,'opened':['Level229.png','Comparison.png'],'assessment':'Bitişik çapraz omurga, sağ porselen sütunları ve tabanda bağlı pres/Rodos grubu okunaklı. Açılışta gerçek boş yuva yok.114/183ile karşılaştırmada katman açılma ve special işi farklı;basamak ritmi ortak.','limits':'Real project sprites; representative normal colors, static cutaway and press rod approximation. No measured balance/performance.'}
put(HERE/'visual-review.json',json.dumps(visual,ensure_ascii=False,indent=2))
delivery=HERE/'Delivery';delivery.mkdir(exist_ok=True)
for src,dst in [(HERE/'Level229.png',delivery/'Level229.png'),(DOCS/'Level229-Ruzgar-Burgusu.md',delivery/'Level229.md'),(ROOT/'Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset',delivery/'LevelV1_00002290.asset')]:shutil.copy2(src,dst)
delivery_note=note.replace('../../Assets/_Project/Settings/ProLevels/LevelV1_00002290.asset','LevelV1_00002290.asset').replace('Level229-Ruzgar-Burgusu.png','Level229.png').replace('Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/Comparison.png','../Comparison.png').replace('Batches/226-230-Lantern-Wind-Crystal/Revisions/v3-229-gel-sculpture/README.md','../README.md')
put(delivery/'Level229.md',delivery_note)
put(delivery/'SHA256.json',json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(delivery.iterdir()) if p.name!='SHA256.json'},indent=2))
print('Updated229 notes/checkpoints/feedback and scoped delivery; no game assets written.')
