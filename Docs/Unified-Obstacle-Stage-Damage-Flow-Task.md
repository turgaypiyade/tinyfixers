# Unified Task: Obstacle Stage Bazlı Kırılma Kuralı (Normal/Special/Booster)

## Amaç
Obstacle’ların her stage için farklı hasar kaynağı kuralı tanımlayabilmesini sağlamak:
- Sadece special ile kırılma,
- Sadece normal match ile kırılma,
- Booster ile kırılma,
- Hepsiyle kırılma,
- Ve karışık akışlar (ör. 1. hit special, 2. hit normal).

Bu task tek seferde veri modeli + runtime hasar değerlendirme + board akışı + doğrulama kapsamını içerir.

---

## Kapsam

### 1) Veri Modeli (ObstacleDef)
**Dosya:** `Assets/_Project/Scripts/Core/ObstacleLibrary.cs`

- [ ] Stage bazlı kırılma kuralı enum’u ekle:
  - [ ] `Any`
  - [ ] `SpecialOnly`
  - [ ] `NormalOnly`
  - [ ] `BoosterOnly`
- [ ] `ObstacleDef` içine `stageDamageRules` listesi ekle.
- [ ] `hits` ile `stageSprites` gibi, `stageDamageRules` için de slot senkronizasyonu ekle.
- [ ] Legacy migration’da yeni alan default `Any` olacak şekilde normalize et.
- [ ] Geriye dönük uyumluluk: eski asset’lerde davranış değişmesin.

**Kabul Kriteri**
- [ ] Inspector’da her stage için kırılma kuralı seçilebilir.
- [ ] Var olan obstacle asset’leri migration sonrası çalışmaya devam eder.

---

### 2) Hasar Servisi (Rule-aware TryDamageAt)
**Dosya:** `Assets/_Project/Scripts/Grid/Board/ObstacleStateService.cs`

- [ ] Hasar context enum’u tanımla:
  - [ ] `NormalMatch`
  - [ ] `SpecialActivation`
  - [ ] `Booster`
  - [ ] `Scripted` (opsiyonel/future-proof)
- [ ] `TryDamageAt` imzasını context alacak şekilde genişlet (gerekirse overload ile geriye uyumluluk).
- [ ] Aktif stage’i `remaining hits` üzerinden hesapla ve stage kuralını oku.
- [ ] Context-stage kuralı uyuşmuyorsa hit tüketme (`didHit=false`, `consumedHit=false`).
- [ ] Uyuşuyorsa mevcut akışı sürdür (remaining azalt, stage sprite güncelle, destroy).
- [ ] Gerekirse debug kolaylığı için sonuç yapısına `rejectedByRule` vb. alan ekle.

**Kabul Kriteri**
- [ ] Kural dışı hasar kaynağı obstacle hit’ini düşürmez.
- [ ] Kural uygun hasar mevcut davranışla aynı şekilde stage ilerletir.

---

### 3) Board Akışı (Context taşıma)
**Dosyalar:**
- `Assets/_Project/Scripts/Grid/Board/BoardController.cs`
- `Assets/_Project/Scripts/Grid/Board/BoardAnimator.cs`
- (Gerekirse) `Assets/_Project/Scripts/Grid/Board/SpecialResolver.cs`

- [ ] `ApplyObstacleDamageAt` çağrı zinciri context alacak şekilde güncelle.
- [ ] Normal clear döngüsünde context = `NormalMatch` gönder.
- [ ] Special activation fazında context = `SpecialActivation` gönder.
- [ ] Booster satır/sütun/tekli etkilerinde context = `Booster` gönder.
- [ ] `affectedCells` ve komşu blocker damage toplama akışında aynı context’i koru.

**Kabul Kriteri**
- [ ] Aynı obstacle, farklı kaynaklardan gelen hasara stage kuralına göre tepki verir.
- [ ] Row/Column booster ve special zincirlerinde kural tutarlılığı korunur.

---

### 4) Örnek Senaryolar (Definition tarafı)
- [ ] **Senaryo A:** `hits=2`, rules = `[SpecialOnly, NormalOnly]`
  - 1. hit yalnız special ile düşer, 2. hit yalnız normal ile düşer.
- [ ] **Senaryo B:** `hits=2`, rules = `[SpecialOnly, SpecialOnly]`
  - İki hit de yalnız special ile düşer.
- [ ] **Senaryo C:** `hits=3`, rules = `[Any, BoosterOnly, NormalOnly]`
  - Karma akış desteklenir.

**Kabul Kriteri**
- [ ] Yukarıdaki üç senaryo QA’da birebir doğrulanır.

---

### 5) Test ve Doğrulama

#### Unit/Service seviyesinde
- [ ] `ObstacleStateService` için context + stage rule kombinasyon testleri ekle.
- [ ] Rule mismatch durumunda hit tüketilmediğini doğrula.
- [ ] Rule match durumunda stage geçişi ve destroy davranışını doğrula.

#### Oyun içi (Manual QA)
- [ ] Normal match ile sadece `NormalOnly` stage’in düştüğünü doğrula.
- [ ] Special tetiklemede sadece `SpecialOnly` stage’in düştüğünü doğrula.
- [ ] Booster etkisinde sadece `BoosterOnly` stage’in düştüğünü doğrula.
- [ ] Hit bitince obstacle görsel/state temizliğinin doğru olduğunu doğrula.

---

## Teknik Notlar
- Tek hasar otoritesi `ObstacleStateService.TryDamageAt` olarak kalmalı.
- Görsel güncelleme event akışı (`OnObstacleStageChanged`, `OnObstacleDestroyed`, `ObstacleVisualChanged`) korunmalı.
- Varsayılan kural `Any` olacağı için mevcut level’larda davranış değişikliği minimumda kalmalı.

---

## Done Definition
- [ ] Veri modeli + migration tamam.
- [ ] Runtime context-rule kontrolü tamam.
- [ ] Board/special/booster çağrı zinciri context-aware tamam.
- [ ] Örnek senaryolar doğrulandı.
- [ ] Testler geçti, regression gözlenmedi.

---

## Iteration Notes (StageRule Runtime Consistency)
- Hole otoritesi yalnızca LevelEditor Mask verisidir (`LevelData.cells[idx] == CellType.Empty`).
- `blocksCells` sadece runtime obstacle etkileşim kuralıdır; mask hole verisini override etmez.
- Bu iterasyonda `DynamicBoardBorder` / `BoardBorderDrawer` için runtime redraw değişikliği kapsam dışıdır.
