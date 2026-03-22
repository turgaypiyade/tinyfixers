# Unified Task: Line/Column Obstacle Damage + Obstacle Layer Behavior + Joker Focus UX

Bu task, üç ayrı sorunu tek akışta çözecek şekilde hazırlanmıştır.

## Problem Özeti
1. **Line/Column emitter** (özellikle `UnderTileLayered` obstacle’larda) satır/sütunun tamamını etkilemiyor; sadece belirli tile’lar temizleniyor, bazı special tile’lar kalabiliyor.
2. **Obstacle Library** içinde `Draw under Tiles` seçeneği fiilen kilitli görünüyor; `OverTile` davranışına geçiş zor/çalışmıyor gibi. Obstacle’ların normal görünmesi ve hit sayısı bitince silinmesi beklentisi tutarlı değil.
3. **Bottom area Joker seçimi** sırasında seçim çerçevesi görünürken joker ikonunun kaybolduğu/çok sönük kaldığı durum var; seçili jokerin daha parlak vurgulanması isteniyor.

---

## Hedefler (Single Definition of Done)
- Row/Column etkileri, tile olsun/olmasın hedeflenen hücrelerde obstacle hasarını deterministik uygular.
- Obstacle katman kararı (`UnderTile` / `OverTile`) tek ve net bir kuralla yönetilir; editor tarafında seçim geri alınabilir ve runtime görünüm tutarlı olur.
- Joker seçiminde ikon görünürlüğü korunur; seçili joker çerçeve + parlaklık/vurgu ile net şekilde öne çıkar.

---

## Kapsam

### A) Gameplay / Damage Propagation
**Dosyalar:**
- `Assets/_Project/Scripts/Grid/Board/BoardAnimator.cs`
- `Assets/_Project/Scripts/Grid/Board/BoardController.cs`
- `Assets/_Project/Scripts/Grid/Board/SpecialResolver.cs`

**Yapılacaklar**
- Satır/sütun etkilerinde yalnızca mevcut `TileView` listesine bağlı kalmadan, etkilenen hücre koordinatlarını da işleyebilen bir yol ekle.
- Obstacle hasarını tile bağımlı olmayan hücre hedefleri için de uygula (`TryDamageAt(x,y)` bu durumda da çalışmalı).
- Booster Row/Column akışında special temizleme davranışını normal special activation ile tutarlı hale getir (special tile’ların atlanmaması).
- Aynı hücreye tek resolve turunda duplicate hit verilmemesini garanti et.

**Kabul Kriterleri**
- Row booster, satırdaki obstacle’lı ama tile olmayan hücreleri de hasarlar.
- Column booster, sütunda aynı şekilde tüm geçerli hücreleri etkiler.
- Satır/sütunda bulunan special tile’lar beklenen akışta temizlenir/aktive edilir, “geride kalan” olmaz.

---

### B) Obstacle Layering / Editor-Data Tutarlılığı
**Dosyalar:**
- `Assets/_Project/Scripts/Core/ObstacleLibrary.cs`
- `Assets/_Project/Scripts/Grid/GridSpawner.cs`
- `Assets/_Project/Scripts/Editor/LevelDataEditor.cs`

**Yapılacaklar**
- `drawUnderTiles` ve `behavior` arasındaki karşılıklı zorlayıcı senkronizasyonu kaldır; tek yönlü migration ve tek kaynak kuralı belirle.
- Runtime katman seçimini tek bir source-of-truth üzerinden yap (`behavior` önerilir).
- Level editor info alanında obstacle davranış katmanı net görünsün (Under/Over/Reveal).
- Mevcut obstacle’ların backward-compatibility davranışını koru (legacy veride bozulma olmamalı).

**Kabul Kriterleri**
- Inspector’da Under/Over tercihi değiştirilebilir ve geri alınabilir.
- Runtime’da obstacle görseli seçilen davranış katmanında spawn olur.
- Hit sayısı tamamlandığında obstacle görseli/state’i temizlenir.

---

### C) Joker Focus UI / Selection Feedback
**Dosya:**
- `Assets/_Project/Scripts/UI/JokerFocusOverlayController.cs`

**Yapılacaklar**
- Selection frame’in ikonun üstünü kapatma riskini ortadan kaldır (frame sprite fallback/alternative highlight).
- Seçili joker için görünür bir “aktif” vurgu ekle (parlaklık + opsiyonel scale/pulse).
- Overlay karartması altında seçili öğenin okunurluğunu artır.

**Kabul Kriterleri**
- Seçili jokerde ikon kaybolmaz.
- Çerçeve + parlaklık aynı anda net görünür.
- Seçim iptal edildiğinde tüm görsel durumlar başlangıca döner.

---

## Teknik Kurallar / Guardrails
- Obstacle hasarı için tek otorite `ObstacleStateService.TryDamageAt` kalmalı.
- Bir resolve döngüsünde aynı origin obstacle’a istenmeyen çoklu hit verilmemeli.
- Event bazlı görsel güncelleme (`OnObstacleStageChanged`, `OnObstacleDestroyed`, `ObstacleVisualChanged`) korunmalı; coupling artırılmamalı.

---

## Test Planı (Smoke + Regression)

### Functional
- 2-hit obstacle, row clear ile: 1. tetiklemede stage değişir, 2. tetiklemede temizlenir.
- Aynı senaryo column clear ile doğrulanır.
- Satır/sütunda special tile varken booster kullanımı sonrası “kalan special” regress olmaz.
- Under/Over davranış seçimi değiştirilip Play Mode’da görsel katman doğrulanır.
- Joker seçimi sırasında ikon görünürlüğü ve vurgu doğrulanır.

#### UI Smoke Checklist (Joker Focus)
- [ ] 4 jokerin her biri tek tek seçilir; seçili jokerde glow + outline + scale artışı görünür, diğer 3 jokerde glow/outline kapalı ve ikon alpha `disabledJokerAlpha` olur.
- [ ] Seçili joker varken farklı bir joker seçilerek geçiş yapılır; yeni seçilen jokerde vurgu açılır, önceki seçili joker başlangıç görsel durumuna döner.
- [ ] Seçim iptal edilir (`CancelVisualSelection` yolu); 4 jokerin tamamı başlangıç state’ine döner (ikon alpha normal, glow/outline kapalı, scale normal).

### Regression
- Başlangıç resolve (`resolveInitialOnStart`) obstacle state’i bozmaz.
- Swap revert ve invalid match sonrası obstacle/joker state stabil kalır.
- Level editor’de obstacle paint/erase sonrası runtime görünüm tutarlı kalır.

### Console Hygiene
- Yeni `NullReferenceException`, index out-of-range veya event unsubscribe hatası oluşmamalı.

---

## Teslim Çıktıları
- Kod değişiklikleri (A/B/C kapsamında).
- Kısa teknik not: “davranış kuralı değişiklikleri ve migration etkisi”.
- Gerekirse kısa “Known Limitations” bölümü.
