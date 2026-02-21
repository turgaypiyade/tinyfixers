# Ana Task: Staged Under-Tile Obstacle End-to-End

Bu task, obstacle akışını tek yerden takip edip implementasyonu modüler tutmak için hazırlanmıştır.

## 0) Scope / Hedef
- Oynanabilir hücrede yaşayan obstacle desteği.
- Aynı obstacle için aşama bazlı görsel (ör. 1. hitte sprite değişimi, 2. hitte temizlenme).
- Match ve special clear etkilerinin obstacle hasarıyla uyumlu çalışması.
- Level editor tarafında görsel ve ayarların kolay yönetimi.

## 1) Data / Editor
**Dosyalar:**
- `Assets/_Project/Scripts/Core/ObstacleLibrary.cs`
- `Assets/_Project/Scripts/Editor/LevelDataEditor.cs`

### Checklist
- [ ] `ObstacleDef` içinde stage sprite akışı netleştirildi (`stageSprites` fallback davranışı dahil).
- [ ] `hits` ve stage sprite sayısı ilişkisi açık kurala bağlandı (dokümante edildi).
- [ ] `drawUnderTiles` editor/inspector’da anlaşılır isim ve tooltip ile görünüyor.
- [ ] `LevelDataEditor` palette preview ilk stage sprite’ını doğru gösteriyor.
- [ ] `LevelDataEditor` seçili obstacle info alanında `hits`, `blocksCells`, `drawUnderTiles` net yazıyor.
- [ ] Level_001 için obstacle yerleştirme akışı (paint/erase/mask) regresyon kontrolünden geçti.

**DoD (Definition of Done):**
- Editor’de yeni obstacle tanımı tek geçişte yapılabiliyor, preview tutarlı, mevcut obstacle’lar bozulmuyor.

---

## 2) Runtime State
**Dosya:**
- `Assets/_Project/Scripts/Grid/Board/ObstacleStateService.cs`

### Checklist
- [ ] Runtime state, level data tanımından bağımsız ama deterministic initialize ediliyor.
- [ ] Origin tabanlı hit takibi (multi-cell obstacle dahil) doğru çalışıyor.
- [ ] `TryDamageAt(x,y)` tek kaynak olarak kullanılıyor.
- [ ] Hit sonrası state transition kuralları açık (decrement, stage change, clear).
- [ ] Clear olduğunda obstacle cleanup origin + footprint için doğru uygulanıyor.
- [ ] Null/array bounds güvenlikleri ve fallback davranışları doğrulandı.

**DoD:**
- Aynı hücreden gelen ardışık clear’larda obstacle state beklenen şekilde 1->2->clear ilerliyor.

---

## 3) Gameplay Integration
**Dosyalar:**
- `Assets/_Project/Scripts/Grid/Board/BoardController.cs`
- `Assets/_Project/Scripts/Grid/Board/BoardAnimator.cs`

### Checklist
- [ ] `BoardController` obstacle state servisinin yaşam döngüsünü yönetiyor (`SetLevelData` vb.).
- [ ] `ObstacleVisualChanged` event’i gameplay ve görsel katmanı ayıracak şekilde kullanılıyor.
- [ ] Tile clear anında obstacle hasarı yalnızca bir kez uygulanıyor.
- [ ] Normal match ve special clear yollarında obstacle hasarı tutarlı.
- [ ] Resolve/cascade akışında duplicate hasar veya race condition yok.

**DoD:**
- Gameplay akışında obstacle hit davranışı deterministik ve side-effect’siz.

---

## 4) Visual Layering (Under/Over)
**Dosya:**
- `Assets/_Project/Scripts/Grid/GridSpawner.cs`

### Checklist
- [ ] Root hiyerarşisi net: `CellBGs < UnderTiles < Tiles < OverTiles`.
- [ ] `drawUnderTiles` true/false’a göre obstacle görseli doğru layer’a spawn oluyor.
- [ ] Origin-index görüntü cache’i doğru güncelleniyor (update/remove).
- [ ] Runtime event ile stage sprite geçişi frame-safe şekilde uygulanıyor.
- [ ] Destroy/rebuild (level reload) sonrası orphan image kalmıyor.

**DoD:**
- Obstacle görseli gameplay state’e birebir uyumlu; layer karışması gözlenmiyor.

---

## 5) QA / Smoke Test Checklist

### Functional
- [ ] 2-hit obstacle: 1. clear sonrası stage2 sprite, 2. clear sonrası obstacle remove.
- [ ] Special taşla temizleme de aynı kuralı izliyor.
- [ ] `blocksCells=false` obstacle’da tile spawn/fall/match normal çalışıyor.
- [ ] `blocksCells=true` obstacle’larda mevcut bloklayıcı davranış bozulmuyor.
- [ ] Multi-cell obstacle için origin ve footprint davranışı doğru.

### Regression
- [ ] Başlangıç resolve akışı (`resolveInitialOnStart`) obstacle state’i bozmaz.
- [ ] Swap revert, invalid match, cascade senaryolarında obstacle state stabil.
- [ ] Level_001 editorde placement/erase sonrası runtime görünüm tutarlı.

### Tech
- [ ] Console’da yeni null ref/index error yok.
- [ ] PR açıklamasına kısa “known limitations” eklendi (varsa).

---

## Önerilen Çalışma Sırası
1. Data/Editor
2. Runtime State
3. Gameplay Integration
4. Visual Layering
5. QA/Smoke

## Önerilen PR Split (istersen tek PR içinde commit bazlı)
- Commit 1: Data/Editor
- Commit 2: Runtime State
- Commit 3: Gameplay Integration
- Commit 4: Visual Layering
- Commit 5: QA fixes + cleanup
