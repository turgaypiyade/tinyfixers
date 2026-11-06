# 226–230 · Fener / rüzgâr / kristal

## Güncel229 — v4 · 2026-11-06

v3jel/pres kullanıcı tarafından reddedildi. [Güncel v4](Revisions/v4-229-lily-workshop/README.md):28hamle;12nilüfer/4dolap/12oyma/12iki aşamalı plastik. Aşağıdaki v3ve ilk batch notları tarihseldir.

## Güncel229 — v3 jel/oyma · 2026-11-06

Yalnız229 yeniden tasarlandı:28hamle SuperHard,99jel/12oyma/2Rodos/12porselen;iki jel presi. [Güncel229 kaydı](Revisions/v3-229-gel-sculpture/README.md) ve [önizleme](Revisions/v3-229-gel-sculpture/Level229.png). ParenttopluPNG/karşılaştırma ve aşağıdaki ilk batch ölçümleri229 için tarihseldir.226 kullanıcı tarafından ayrıca düzenlenmiştir;v2modelini yeniden yazmayın. Güncel229kontrolü scopedverify ile yapılır;eski batchauthor çalıştırılmaz.


## Aktif226 revizyonu — v2 · 2026-11-06

226v1 kullanıcı tarafından reddedildi;[v2 bağlı duvar yolu](Revisions/v2-wall-route/README.md) günceldir. Yeni226:68aktif/28hamle,1duvar/24fener/16un;36doğal başlangıç taşı,pin0. Üç küçük C#katman/origin desteği eklendi;düşüş/akış değiştirilmedi. Aşağıdaki ilk taslak gerekçeleri ve ölçümleri226 için tarihseldir;227–230 değişmedi.

2026-11-06 · Yetkili beş yeni taslak; görsel inceleme agent'a aittir. Kullanıcı onayı ve oynanmış denge yok.

| Level | TR / EN | Hamle / tür | Hedefler |
| --- | --- | --- | --- |
| 226 | Rüzgâr Kemeri / Windlit Arch | 28 · Normal | 1 Duvar, 24 Fener, 16 Un |
| 227 | Işık Tezgâhı / Lightwork Bench | 26 · Normal | 5 Lamba çerçevesi, 12 Oyma taşı, 17 İki aşamalı plastik |
| 228 | Kristal Ünlem / Crystal Exclamation | 26 · Normal | 12 Elmas, 15 Fener |
| 229 | Rüzgâr Burgusu / Crystal Whirl | 28 · Super Hard | 12 Nilüfer, 4 Dolap, 12 Oyma taşı, 12 İki aşamalı plastik |
| 230 | Büyük Işık Düellosu / Grand Light Duel | 30 · Boss Duel | 660 Boss HP, 12 Elmas, 16 Fener, 16 Un |

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
