"""Write the delivery notes from the authored model and independent validation."""
import json
from design import HERE,ROOT
from render import goal_label

ds=json.loads((HERE/'designs.json').read_text())
validation=json.loads((HERE/'validation.json').read_text())
summary=['# 222–225 · Şekil ve kristal çalışması','',
    'Kullanıcının 2026-11-05 tarihli açık 222–225 isteği. Önceki 1–221 ve mevcut gameplay/akış kodu korundu.',
    'Tasarımlar statik ve görsel olarak incelendi; kullanıcı tarafından oynanmadı veya onaylanmadı.','',
    '| Level | TR / EN | Hamle | Hedefler |','| --- | --- | ---: | --- |']
for d in ds:
    v=next(v for v in validation['levels'] if v['level']==d['level'])
    goals=' · '.join(f'{g["amount"]} {goal_label(g)}' for g in d['goals'])
    summary.append(f'| {d["level"]}{" Hard" if d["level"]==225 else ""} | {d["name"]} / {d["nameEn"]} | {d["moves"]} | {goals} |')
    text=f'''# {d['level']} · {d['name']} / {d['nameEn']}

İnceleme taslağı · 2026-11-05 · {'Hard' if d['level']==225 else 'Normal'} · {d['moves']} hamle · {d['width']}×{d['height']} · {v['active']} aktif hücre · dört normal renk.

Asset: `{d['asset']}`. Aktif katalog: `LevelCatalogProV1.asset`, `{d['levelKey']}`. TR/EN isim ürün verisine eklendi.

- **İlk bakış:** {d['idea']}
- **İlk karar:** {d['firstDecision']}
- **İlk katman açılınca:** {d['change']}
- **Sonraki iş:** {d['remaining']}

Hedefler: **{goals}**. Hedef sayısı {len(d['goals'])}; kaynak ve ürün aynı anda hedeflenmez.

{d['budget']} Hamle bütçesi erişim, katman sırası, özel güç alanı ve üretim/toplama işiyle seçildi; oynanmış denge değildir.

Başlangıçta {d['openingGeometry']['tileSlots']} taş yuvası, {d['openingGeometry']['naturallyFed']} doğal beslemeli hücre var. Renk pini ve hazır special yok. Başlangıçtaki {len(d['openingGeometry']['intentionallyEmpty'])} boş yuva, açılabilir engel gölgesidir; önizlemede noktalı gösterilir. Son açılabilir katmanlar kalkınca tüm normal yuvalar beslenir. Kalıcı kaynak grupları alt sınıra dayanır.

Elmaslar normal taş katmanındadır: takas, düşüş, yana kayma, kendi rengindeki elmaslarla eşleşme/special ile temizlenme. Komşu normal eşleşme elması kırmaz. Üretim yalnız hareketsiz taşın yerine yerleşir. Dimerdal renkleri ortak elmas kotasını paylaşır; bütün kutuları açmak veya her renkten belirli sayıda üretmek zorunlu değildir.

Alternatiflerden seçim:
{chr(10).join('- '+a for a in d['alternatives'])}

**Statik doğrulama:** Unity YAML, dizi boyu, maske bağlantısı, footprint/origin, sıvı-alt/çim-üst sırası, hedef miktarı/ikon, kalıcı kaynak erişimi, dolum kesitleri, isim/katalog/GUID ve kapsam dışı hash koruması geçti.

**Agent görsel değerlendirmesi:** Güncel proje sprite'larıyla açılış ve kesit PNG'si açılarak incelendi. Bu değerlendirme kullanıcı onayı değildir. Serbest taş renkleri temsili; sağ kesit oynanmış an değildir. Hareketli porselenlerin başlangıç şekli oyun sırasında değişir.

**Oynanmış denge:** Yok. Unity build/Play, cihaz, bot veya gravity/cascade simülasyonu çalıştırılmadı. FPS/GC/kazanma oranı ölçülmedi. Yayın veya release baseline yok. Yeni runtime/VFX kodu eklenmedi.

Önizleme: [Level{d['level']}-{d['slug']}.png](Level{d['level']}-{d['slug']}.png). Batch: [README](Batches/222-225-Crystal-Shapes/README.md).
'''
    if d['level']==223:
        text=text.replace('Elmaslar normal taş katmanındadır:', 'Bu levelde Dimerdal/elmas yok; mevcut KeyGenerator anahtarları kullanılır. Sonraki Dimerdal seviyeleri için geçerli mekanik notu: Elmaslar normal taş katmanındadır:')
    (ROOT/f'Docs/LevelDesign/Level{d["level"]}-{d["slug"]}.md').write_text(text)
summary += ['', '## Tekrar ve görsel kontrol','',
    'Aktif katalogda her adaydan önceki tüm seviyeler tarandı. `Tools/review_level_patterns.py` çıktıları `Reviews/222-Crystal-Shapes` … `225-Crystal-Shapes` altında. Normalize geometri ve crop/ayna skorları yalnız kısa liste içindir; özgünlük veya onay yüzdesi değildir.',
    '', 'Yakın tarihsel örnekler gerçek sprite karşılaştırmalarıyla açılıp incelendi:',
    '', '- 222 ↔ 221/103: alt kaynak dikdörtgeni ortaktır; yaprak maskesi, çapraz çim damarı ve tek yeşil renk üretimi farklıdır.',
    '- 223 ↔ 94/120: küçük yatay anahtar maskesi; 2×3 oyma/çamur başı ve 2×4 hareketli porselen şaft. Eski geniş örtü/iki oda düzeni tekrar edilmedi.',
    '- 224 ↔ 111/170: merkezde gövde ve alt kütle benzerliği var; burada kesintisiz çapayı engeller çizer, heykel üstten açılır ve alt uçlar kalıcı elmas kaynağıdır.',
    '- 225 ↔ 148/101: açık üst alan ve alt iş yükü ortak bir dil. Boya kasası dizisi yerine tek 4×4 sıralı mühür, altında iki kat çamur, yanında iki 2×4 special-only sur ve dipte üretim vardır.',
    '', 'Agent incelemesinde 224’ün iki hücrelik gövde bağlantısı tamamlandı (ahşap 32→34); ayrı bir kullanıcı revizyonu/onayı değildir. PNG metin/hedef çakışmaları teslimden önce giderildi.',
    '', '## Doğrulama ve koruma','',
    f'- `python3 .../verify.py`: PASS; {validation["protectedFiles"]} kapsam dışı dosyanın hash’i aynı. 1–221 ve önceki adlar korundu.',
    '- Önceki katalog/adlar/tasarım kayıtları `Before/`; var olan gameplay kodu, library, meta/GUID ve ayarlar `protected-before.json` ile izlenir.',
    '- `design.py` yeniden çalıştırılacaksa önce `owned-hashes.json` eşitliğini ister; kullanıcının yeni değişikliğini sessizce ezmez.',
    '- `render.py`: dört detay PNG ve `Levels222-225.png`; gerçek sprite’lar, temsili serbest renkler, açıkça işaretlenmiş boş yuvalar/kesit.',
    '- Kaynaklar alttadır; normal yuvaların kalıcı kaynak altında kapanması statik olarak reddedilir. İlk boş yuvalar yalnız açılabilir engel gölgesidir.',
    '- Bu çalışma build/Play/simülasyon/cihaz testi veya yayın içermez. Hamle sayıları ilk tahmin; kullanıcı denemesinden sonra yerel ayarlanır.',
    '', '## Performans incelemesi','',
    'Yeni gameplay/VFX kodu yok; mevcut havuz ve iptal/teardown yolları kullanılıyor. 222’de bir, 224/225’te ikişer Dimerdal görünümü var; üretim/ghost/collection havuzları ve tek servis döngüsü korunuyor. 223’te dört KeyGenerator mevcut ghost havuzunu kullanır; servisin mevcut LateUpdate anahtar sayımı 45 hücreyi tarar. 224’te tek heykelin mevcut toplama/uçuş maliyeti, 225’te mührün mevcut renk uçuştan rezervasyon/teardown yolu geçerlidir. Bu özellikler kaynak kod incelemesidir; yeni CPU/GC/FPS ölçümü veya performans artışı iddiası yok.',
    '', '## Sonraki adım','',
    'Kullanıcı 222–225’i oynayacak. Takılınan hedef/bölge, kalan hamle ve kayıp haklar üzerinden yerel ayar. 226+ yetkilendirilmedi. 221’deki Dimerdal yeni normal-taş davranışı cihazda henüz tekrar denenmedi.',
    '221–230 onluğunda yeni mekanik şu an yalnız Dimerdal’dır; yeşil/kırmızı/sarı kutular aynı ailenin renk varyantlarıdır. İkinci mekanik, 226+ talebi gelince güncel kod/tanıtım havuzuyla ayrıca seçilir; bu işte yeni mekanik/runtime uydurulmadı.', '']
(HERE/'README.md').write_text('\n'.join(summary))
print('Wrote four level notes and batch review/delivery record.')
