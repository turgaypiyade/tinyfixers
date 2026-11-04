# Grass üzerindeki PaintCanBox ilk görünümü — 2026-11-03

Kullanıcı 212'de çimle örtülü boya kasası açıldığında tek boya kutusu göründüğünü, ilk isabette bütün kutuların geldiğini bildirdi. Yeni bir PaintCanBox ilk görünümde dokuz kutuyla başlamalı; önceden hasarlı bir katman ise gerçek kalan sayısını korumalı.

## Neden ve düzeltme

`GridSpawner.DrawStampedBeneathVisuals` alt katmanı çizerken paylaşılan level dizilerindeki obstacle kimliğini geçici olarak alt engelin kimliğine çeviriyor. Fakat `remainingHitsByOrigin` hâlâ üst çimin **1** vuruşunu taşıyor. `AttachPaintCanBoxView` bu genel hücre sayacını okuyunca 9 yuvalı piramitte yalnız bir kutu açılıyordu. İlk gerçek boya isabette doğru kalan değer **8** geliyor; `PaintCanBoxView.SetCount` 1→8 artışını bir snapshot geri yüklemesi sanıp piramidi yeniden kuruyordu.

`ObstacleStateService.TryGetLayerRemainingHitsAt` istenen **id + origin** için önce kayıtlı alt katmanın sayacını okur, sonra varsa movable örtü deposunu ve gerçekten eşleşen açık katmanı değerlendirir. Bu sıra geçici render kimliğinin sayacı yanlış yönlendirmesini önler. `GridSpawner.AttachPaintCanBoxView` artık bu sorguyu kullanır; kayıt yoksa kütüphane başlangıç sayısına döner. Bilinen sıfır değer ve önceden azalmış sayaç aynen korunur. Gameplay HP, hedef, hasar veya katman sırası değiştirilmez.

`PaintCanBoxView`'ın piramit ve düşürme kodu değiştirilmedi. İlk isabette 9→8 olduğu için dokuz çocuğu silip yeniden kurma dalına girilmez. Sorgu yalnız görsel kurulurken hücrenin kısa katman listesini dolaşır; yeni liste/buffer, board taraması, update, coroutine, abonelik veya ses kaynağı oluşturmaz. Mevcut vuruş animasyonlarının maliyeti aynıdır. Bu bir cihaz FPS ölçümü değildir.

## Kontroller

```text
csi Tools/check_paintcan_cover_reveal.csx --before
REPRODUCED: Grass HP 1 -> one visible can; first paint hit -> eight visible cans and a nine-child rebuild.

csi Tools/check_paintcan_cover_reveal.csx
PASS: 26 production-method checks; fresh covered/revealed box 9 -> 8 -> 6 -> 0, no extra pyramid build, partial snapshots preserved.
```

Harness, gerçek state sorgu/alt katman kaydı, GridSpawner bağlama/güncelleme ve PaintCanBoxView Ensure/Build/SetCount metotlarını Unity nesne test çiftleriyle çalıştırır. Kutu yerleşimi/görünürlük matematiği üretim kodudur; Build çağrı sayısı yalnız testte sayaçla izlenir. Testler yeni örtülü kasa, geçici render kimliği, örtünün HP'sinin değişmemesi, ilk normal ve special isabet, bitiş, kısmen hasarlı 1/3/5/8/9 kayıtlar, iç içe katmanda id/origin ayrımı, açık kasa, sıfır sayaç ve eksik state fallback'ini kapsar. Coroutine zamanı/fizik ve gerçek Unity renderer çalıştırılmaz. Değişen üç ilgili C# dosyası syntax olarak da ayrıştırılır.

Önceki runtime dosyaları `PaintCanBox-Cover-Reveal/Before/` altında saklandı; GridSpawner'ın kullanıcıya ait önceki değişiklikleri korundu. `settings-before.json` kontrolü bütün mevcut Settings/level assetlerinin aynı kaldığını doğrular. 212'nin onaylı maskesi, çim üstte düzeni ve 24 hamlesi değiştirilmedi.

**Unity Play/build, cihaz testi, bot/kaskat simülasyonu veya yayın yapılmadı.** Cihazda/Play Mode'da çimin kademeli açılması sırasında piramidin dokuz kutuyla hazır görünmesi ve ilk gerçek boya isabetinin yalnız uygun sayıda kutu düşürmesi ayrıca gözlenmeli.
