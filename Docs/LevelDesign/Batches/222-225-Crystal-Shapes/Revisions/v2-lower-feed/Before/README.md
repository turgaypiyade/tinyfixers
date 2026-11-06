# 222–225 · Şekil ve kristal çalışması

Kullanıcının 2026-11-05 tarihli açık 222–225 isteği. Önceki 1–221 ve mevcut gameplay/akış kodu korundu.
Tasarımlar statik ve görsel olarak incelendi; kullanıcı tarafından oynanmadı veya onaylanmadı.

| Level | TR / EN | Hamle | Hedefler |
| --- | --- | ---: | --- |
| 222 | Zümrüt Yaprak / Emerald Leaf | 24 | 10 Elmas · 16 Çim |
| 223 | Uçan Anahtar / Flying Key | 23 | 14 Anahtar · 8 Porselen · 6 Oyma taşı · 6 Çamur |
| 224 | Kristal Çapa / Crystal Anchor | 24 | 12 Elmas · 34 Ahşap · 1 Heykel · 4 Çim |
| 225 Hard | Mühürlü Kristal Kalesi / Sealed Crystal Keep | 25 | 16 Elmas · 1 Mühür · 16 Pusula taşı · 16 Çamur |

## Tekrar ve görsel kontrol

Aktif katalogda her adaydan önceki tüm seviyeler tarandı. `Tools/review_level_patterns.py` çıktıları `Reviews/222-Crystal-Shapes` … `225-Crystal-Shapes` altında. Normalize geometri ve crop/ayna skorları yalnız kısa liste içindir; özgünlük veya onay yüzdesi değildir.

Yakın tarihsel örnekler gerçek sprite karşılaştırmalarıyla açılıp incelendi:

- 222 ↔ 221/103: alt kaynak dikdörtgeni ortaktır; yaprak maskesi, çapraz çim damarı ve tek yeşil renk üretimi farklıdır.
- 223 ↔ 94/120: küçük yatay anahtar maskesi; 2×3 oyma/çamur başı ve 2×4 hareketli porselen şaft. Eski geniş örtü/iki oda düzeni tekrar edilmedi.
- 224 ↔ 111/170: merkezde gövde ve alt kütle benzerliği var; burada kesintisiz çapayı engeller çizer, heykel üstten açılır ve alt uçlar kalıcı elmas kaynağıdır.
- 225 ↔ 148/101: açık üst alan ve alt iş yükü ortak bir dil. Boya kasası dizisi yerine tek 4×4 sıralı mühür, altında iki kat çamur, yanında iki 2×4 special-only sur ve dipte üretim vardır.

Agent incelemesinde 224’ün iki hücrelik gövde bağlantısı tamamlandı (ahşap 32→34); ayrı bir kullanıcı revizyonu/onayı değildir. PNG metin/hedef çakışmaları teslimden önce giderildi.

## Doğrulama ve koruma

- `python3 .../verify.py`: PASS; 3409 kapsam dışı dosyanın hash’i aynı. 1–221 ve önceki adlar korundu.
- Önceki katalog/adlar/tasarım kayıtları `Before/`; var olan gameplay kodu, library, meta/GUID ve ayarlar `protected-before.json` ile izlenir.
- `design.py` yeniden çalıştırılacaksa önce `owned-hashes.json` eşitliğini ister; kullanıcının yeni değişikliğini sessizce ezmez.
- `render.py`: dört detay PNG ve `Levels222-225.png`; gerçek sprite’lar, temsili serbest renkler, açıkça işaretlenmiş boş yuvalar/kesit.
- Kaynaklar alttadır; normal yuvaların kalıcı kaynak altında kapanması statik olarak reddedilir. İlk boş yuvalar yalnız açılabilir engel gölgesidir.
- Bu çalışma build/Play/simülasyon/cihaz testi veya yayın içermez. Hamle sayıları ilk tahmin; kullanıcı denemesinden sonra yerel ayarlanır.

## Performans incelemesi

Yeni gameplay/VFX kodu yok; mevcut havuz ve iptal/teardown yolları kullanılıyor. 222’de bir, 224/225’te ikişer Dimerdal görünümü var; üretim/ghost/collection havuzları ve tek servis döngüsü korunuyor. 223’te dört KeyGenerator mevcut ghost havuzunu kullanır; servisin mevcut LateUpdate anahtar sayımı 45 hücreyi tarar. 224’te tek heykelin mevcut toplama/uçuş maliyeti, 225’te mührün mevcut renk uçuştan rezervasyon/teardown yolu geçerlidir. Bu özellikler kaynak kod incelemesidir; yeni CPU/GC/FPS ölçümü veya performans artışı iddiası yok.

## Sonraki adım

Kullanıcı 222–225’i oynayacak. Takılınan hedef/bölge, kalan hamle ve kayıp haklar üzerinden yerel ayar. 226+ yetkilendirilmedi. 221’deki Dimerdal yeni normal-taş davranışı cihazda henüz tekrar denenmedi.
221–230 onluğunda yeni mekanik şu an yalnız Dimerdal’dır; yeşil/kırmızı/sarı kutular aynı ailenin renk varyantlarıdır. İkinci mekanik, 226+ talebi gelince güncel kod/tanıtım havuzuyla ayrıca seçilir; bu işte yeni mekanik/runtime uydurulmadı.
