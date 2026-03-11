# TinyFixers – AI Project Handoff (Unity)

> Bu doküman, projeyi ilk kez gören bir yapay zekânın hızlıca bağlam kazanması için hazırlanmıştır.
> Kod/akış odaklıdır; özellikle **grid/match/special** sistemi için pratik referans verir.

---

## 1) Proje özeti

- Oyun tipi: Match-3 + special/combo sistemi + obstacle/goal sistemi.
- Motor: Unity (C#).
- Ana oyun döngüsü: swap → match tespiti → clear animasyonu → cascade/fall → yeniden match kontrolü.

Ana giriş noktaları:
- `GridSpawner` sahne kurulumu, level yükleme, board oluşturma.
- `BoardController` runtime oyun akışı, resolve/cascade/special yönetimi.

---

## 2) Klasör / kod organizasyonu

### Core
- `Assets/_Project/Scripts/Core/LevelData.cs`
  - Level grid boyutu, moves, goals, cell/obstacle katmanları.
- `Assets/_Project/Scripts/Core/LevelCatalog.cs`
  - Chapter/level veya key ile `LevelData` seçimi.
- `Assets/_Project/Scripts/Core/LevelRuntimeSelector.cs`
  - Runtime level seçim stratejisi.
- `Assets/_Project/Scripts/Core/BootLoader.cs`
  - Splash/boot sonrası oyun sahnesine geçiş.

### Grid / Board
- `Assets/_Project/Scripts/Grid/GridSpawner.cs`
  - LevelData’yı resolve eder, board’u init eder, initial grid’i spawn eder.
  - `SimulateInitialTypes()` ile başlangıçta otomatik match’siz board üretimi.
- `Assets/_Project/Scripts/Grid/Board/BoardController.cs`
  - Swap, resolve döngüsü, action sequencer, special çağrıları, booster akışı.
- `Assets/_Project/Scripts/Grid/Board/MatchFinder.cs`
  - Match bulma, run hesapları, special karar yardımı.
- `Assets/_Project/Scripts/Grid/Board/CascadeLogic.cs`
  - Clear sonrası düşme ve spawn.
- `Assets/_Project/Scripts/Grid/Board/SpecialResolver.cs`
  - Special swap/solo aktivasyonları, combo ve zincir yönetimi.

### Specials
- `Assets/_Project/Scripts/Grid/Board/Specials/SpecialBehaviorRegistry.cs`
  - Special ve combo davranış registry’si.
- `Assets/_Project/Scripts/Grid/Board/Specials/ISpecialBehavior.cs`
  - Solo special etki arayüzü.
- `Assets/_Project/Scripts/Grid/Board/Specials/IComboBehavior.cs`
  - Combo arayüzü (`Priority` ile).
- Çekirdek davranışlar:
  - `LineBehavior.cs`
  - `PulseCoreBehavior.cs`
  - `PatchBotBehavior.cs`
  - `SystemOverrideBehavior.cs`
- Combo davranışları:
  - `LineCrossCombo.cs`
  - `PulseLineCombo.cs`
  - `PulsePulseCombo.cs`
  - `PulseLineCrossCombo.cs`

### UI / VFX
- `Assets/_Project/Scripts/UI/*`
- `Assets/_Project/Scripts/VFX/*`

---

## 3) Beklenen temel gameplay kuralları (ürün beklentisi)

Bu proje için istenen temel akış şu şekilde tanımlanmış durumda:

1. Board ilk yüklendiğinde special olmayacak.
2. Board ilk yüklendiğinde otomatik match olmayacak.
3. 3’lüler normal match.
4. 4’lüler line (LineH/LineV).
5. 5’li düz run -> SystemOverride.
6. L/T veya 5-cluster -> PulseCore.
7. 2x2 -> PatchBot (bağlamsal olarak special üretimi için değerlendirilmeli).
8. Match sonrası yukarıdan taş düşer/spawn olur, doğal cascade devam eder.

> Not: Son dönemde hatalar bu temel akışı bozduğu için özellikle `MatchFinder`, `ResolveBoard`, `TryCreateSpecial`, `CascadeLogic` kritik noktalardır.

---

## 4) Mevcut kritik teknik noktalar

### 4.1 Resolve döngüsü
- `BoardController.ResolveBoard(bool allowSpecial = true)`
  - `FindAllMatches()` ile eşleşmeler toplanır.
  - Special üretimi `TryCreateSpecial(nonSpecialMatchTiles)` üzerinden yapılır.
  - Üretilen special aynı pass’te temizlenmesin diye clear set’inden çıkarılır.

### 4.2 MatchFinder davranışı
- `FindAllMatches()` şu an globalde 3+ run odaklıdır.
- 2x2’nin global auto-match olarak resolve’a zorla katılması kaldırılmıştır.
  - 2x2 hâlâ `DecideSpecialAt` / bağlamsal candidate akışında kullanılabilir.

### 4.3 Spawn sırasında immediate match azaltma
- `CascadeLogic` spawn type seçiminde `GetRandomTypeAvoidingImmediateMatch(x,y)` kullanır.
- Amaç: refill kaynaklı gereksiz uzun resolve zincirlerini azaltmak.

### 4.4 Special/combo mimarisi
- Special efektleri `SpecialBehaviorRegistry` + behavior sınıfları üzerinden hesaplanır.
- `SpecialResolver` combo/special zincir yönetimini yapar.
- Event hub’lar görsel orkestrasyon için eklenmiştir (`ComboBehaviorEvents`, `LineBehaviorEvents`, `PulseBehaviorEvents`, `SystemOverrideBehaviorEvents`).

---

## 5) Debug / teşhis araçları

### Match debug
- `MatchFinder.FindAllMatches()` development/editor build’lerde:
  - GridData snapshot
  - TileView snapshot
  - GridData vs TileView mismatch scan
  - Matched cells listesi

### Special trace
- `SpecialResolver.TraceSpecialChain(...)` ve `BoardController` üzerindeki trace toggle ile chain debug yapılır.

Önerilen log okuma sırası:
1. `FindAllMatches` sayısı ve matched cells
2. Special swap logu (`ResolveSpecialSwap`)
3. Resolve pass artışı (anormal uzun zincir var mı)
4. GridData/TileView mismatch count (sync sorunu var mı)

---

## 6) Sık görülen regresyonlar (yakın geçmiş)

1. **Yeni üretilen special’ın aynı pass’te silinmesi**
   - Nedeni: created tile clear set’inden çıkarılmıyordu.
2. **2x2’nin global auto-match gibi davranması**
   - Nedeni: `FindAllMatches()` içine global `Add2x2Matches` eklenmesi.
3. **Refill sonrası aşırı cascade**
   - Nedeni: spawn’da tamamen random type seçimi.
4. **Special chain’in global resolve’da yanlış tetiklenmesi**
   - Nedeni: generic pass’te kontrolsüz chain expansion çağrıları.

---

## 7) Yeni bir AI için çalışma rehberi (öneri)

1. **Önce temel akış test et** (special olmayan düz board, basit 3’lü/4’lü/5’li).
2. Sonra tek tek special senaryoları aç:
   - Line+Line
   - Pulse+Line
   - Pulse+Pulse
   - Override + X
3. Eğer beklenmedik clear zinciri varsa:
   - `FindAllMatches` çıktısında 2x2 auto-collect var mı kontrol et.
   - Resolve pass’te created special korunuyor mu kontrol et.
4. Eğer görsel-logic farklıysa mismatch scan’i temel al.

---

## 8) Önemli dosyalar hızlı listesi

- `Assets/_Project/Scripts/Grid/Board/BoardController.cs`
- `Assets/_Project/Scripts/Grid/Board/MatchFinder.cs`
- `Assets/_Project/Scripts/Grid/Board/CascadeLogic.cs`
- `Assets/_Project/Scripts/Grid/Board/SpecialResolver.cs`
- `Assets/_Project/Scripts/Grid/Board/Specials/SpecialBehaviorRegistry.cs`
- `Assets/_Project/Scripts/Grid/Board/Specials/*.cs`
- `Assets/_Project/Scripts/Grid/GridSpawner.cs`
- `Assets/_Project/Scripts/Core/LevelData.cs`

---

## 9) Not

Bu doküman canlı bir "handoff" notudur. Yeni değişikliklerde özellikle şu 3 şeyi güncelle:
- temel gameplay kuralları,
- resolve/match davranış değişiklikleri,
- bilinen regresyon listesi.
