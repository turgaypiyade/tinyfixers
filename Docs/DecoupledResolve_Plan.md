# Decoupled Resolve Plan — Simülasyon / Sunum Ayrımı

Amaç: Taş akışını referans oyundaki (Royal Kingdom tarzı) hisse yaklaştırmak.
Kullanıcı gözlemi: referans oyunda **son taş daha gride girerken** o sütundaki match
temizlenmeye başlıyor. Bizde ise düşüş %100 bitiyor, sonra ~8 kare ölü boşluk, sonra
match animasyonu başlıyor. Kök fark: referans **mantığı önceden çözüp** sunumu overlap'li
oynatıyor; bizde sunum her fazı bir öncekinin görsel bitişine kilitliyor.

## 1. Mevcut mimari (presentation-gated, seri)

`BoardController.ResolveBoard` (satır ~2257) tek bir `while(true)` döngüsü:

```
her pass:
  1) bottom-exit cargo
  2) STRICT ORDER BARRIER: boş playable hücre varsa → CalculateCascades (fall)
       → actionSequencer.Enqueue → while(IsPlaying) yield → continue
  3) FindAllMatches → varsa ExecuteClearPass → continue
  4) cascades (fall) → Enqueue → while(IsPlaying) yield → continue
  5) oil spread / barrel spread / ... (board tam idle olunca)
  stabil olana dek tekrar
```

Kilit gerçek: **mantık zaten ileriden biliyor** — `CalculateCascades` mantıksal board'u
ANINDA final pozisyona günceller, sonra animasyonu kuyruğa atar. Yani "ne olacağını önceden
bilmek" bizde VAR; kullanılmıyor. Tek engel: `while (actionSequencer.IsPlaying) yield return null;`
alâkasız bir sütun hâlâ düşerken bile match'i / bir sonraki fazı bloklar.

`ActionSequencer` non-blocking action'ı zaten destekliyor (`action.Blocking==false` →
StartCoroutine, beklemez). Ama resolve döngüsü her Enqueue'dan sonra elle bekliyor.

### KRİTİK KISIT (2026-07-25 bulgu): tek-blocking-sequencer
`ExecuteClearPass` clear'ı AYNI sequencer'a `MatchClearAction` olarak enqueue eder ve
`while (actionSequencer.IsPlaying) yield` ile bekler. `FallAction` **Blocking=true** →
`PlaySequence` onu `yield return StartCoroutine(...)` ile bekler, kuyruk durur. Sonuç: düşüş
oynarken clear enqueue edilirse **düşüşün TAMAMI bitene kadar** kuyrukta bekler → overlap YOK.
Gerçek overlap için clear (ve sonraki fall) **sequencer'ın blocking kuyruğu dışında**, doğrudan
`StartCoroutine` ile, per-tile varış tetiğiyle koşmalı. Yani decouple'ın asıl işi: fall/clear
event'lerini tek-blocking-sequencer'dan çıkarıp **playback koordinatörüne** almak. (Koordinatör
sequencer'ı REPLACE eder — yanında ikinci motor olarak KOŞMAZ, desync riski; [[project_tile_fall_flow]].)

### Neden bariyer var (dikkat edilecek edge-case'ler)
- MatchFinder aktif boş hücre varken çalışmamalı (yanlış match / çift-işleme).
- Diagonal / L-path slide (LevelP_0060 regresyon seviyesi).
- Special oluşturma + tetikleme cascade'leri (çift-patlama riski — [[project_override_fixes]]).
- Obstacle hasar sırası (oil/barrel/cargo/safe/magnet), oil/barrel spread yalnız board idle iken.
- Goal-orb / PatchBot uçuşları (non-blocking, [[project_goal_orb_flight_nonblocking]]).
- Boss-duel seviyeleri.

## 2. Hedef mimari (simulate → timeline → playback)

İki fazlı:

**A) Simülasyon (anında, görselsiz):** Tüm resolve'u mantıksal board üzerinde sonuna kadar
koştur. Çıktı: sıralı **event listesi**. Her event = { tip (fall/clear/special-activate/
spawn/obstacle-hit/spread), etkilenen hücreler/taşlar, **başlangıç zamanı (global timeline)** }.
Başlangıç zamanları **bağımlılığa göre overlap'li**:
- Bir clear'ın başlangıcı = kendi taşlarının VARIŞ zamanının max'ı (tüm board'un değil).
- Bir sonraki fall'un başlangıcı = onu besleyen clear'ın "hücre boşaldı" anı (clear kuyruğunun
  tamamı değil).

**B) Playback:** Timeline'ı oynat — her event'i kendi `startTime`'ında görsel olarak tetikle.
Taşlar düşerken, oturan match temizlenir; overlap doğal oluşur.

Bu, YENİ paralel motor DEĞİL: aynı `CalculateCascades` / `MatchFinder` / `ExecuteClearPass`
mantığı; sadece "bekle" kapısı "zamanla" kapısına dönüşür.

## 3. Fazlı yol haritası

Büyük-patlama rewrite yok. Her faz shippable ve tek başına test edilebilir.
Geçmiş ders ([[project_tile_fall_flow]]): paralel sistem açma, mevcut path'i genişlet.

### Faz 1 — Cerrahi overlap (fall→clear sınırı) [EN ÖNCE]
Hedef: 8 karelik ölü boşluğu kapat. En düşük risk, en görünür kazanç.
- `ResolveBoard`'da fall Enqueue'dan sonra "tam idle" beklemek yerine, **match'i oluşturan
  taşların kendi düşüşü bitince** ExecuteClearPass'i başlat; alâkasız taşlar arkada düşmeye
  devam etsin.
- Somut: cascade fall'dan sonra, sequencer tam boşalmadan FindAllMatches'i güvenli çalıştırmak
  için, "mantıksal olarak yerine oturmuş ve artık hareket etmeyecek" taşları işaretle
  (CalculateCascades zaten final pozisyonu biliyor → bir taş final hücresindeyse ve altı
  dolu ise "settled"). Match yalnız settled taşlarda aranır; kalanlar düşerken clear başlar.
- Guard: settled-set boş hücre içermez (bariyerin asıl amacı korunur). Diagonal/spawn
  taşları "settled" sayılmadan match'e girmez.
- Regresyon: LevelP_0060 (diagonal), 4'lü match (special üretimi), special en-altta senaryosu.

### Faz 2 — Clear→sonraki-fall overlap
- Clear tamamlanmadan, boşalan hücrelerin üstündeki taşlar düşmeye başlar (clear'ın kuyruğu
  hâlâ oynarken). "Hücre boşaldı" event'i clear tamamlanmasından ayrılır.

### Faz 3 — Tam timeline (simulate → playback)
- Resolve'u tek geçişte simüle edip event listesi + global timeline üret; playback'i tek
  koordinatörle oynat. Faz 1-2'nin overlap kuralları timeline'a genellenir.
- Special/combo/obstacle event'leri timeline'a taşınır (SpecialChainRunner ile hizalı,
  [[project_special_chain_runner]]).

## 4. Riskler / geri-alma
- Her faz ayrı commit; kırılırsa tek faz geri alınır.
- `activeFallProfile.fps` (şu an 35, ~57 kare hedef) sunum hızını kontrol eder; timeline
  bundan bağımsız — overlap timing'i profille çakışmamalı.
- Çift-patlama / çift-clear en büyük risk: her event bir kez işlenmeli (işlenmiş-set).
- Dev doğrulama: `BoardFlowTraceEnabled` log'ları + LevelP_0060 + boss-duel.

## 6. Koordinatör Tasarımı (detay)

Kullanıcı kararı (2026-07-25): önce tam koordinatörü tasarla, sonra kodla.

### 6.1 Temel model — "mantık önde, görsel bağımlılıkla arkadan"
Tüm cascade'i baştan simüle edip dev timeline pişirmeyiz (special/combo randomness'ı her
alt-sistemi karar/gösterim diye ayırmayı gerektirir → devasa + kırılgan). Bunun yerine:
- **Mantık round-round önde koşar** (mevcut `CalculateCascades`/`FindAllMatches`/`ExecuteClearPass`
  KARAR mantığı aynen kalır; board'u ANINDA final pozisyona günceller — zaten öyle).
- **Görsel katman bağımlılık-tabanlı overlap'le arkadan gelir.** Sınırlı lookahead: mantık en
  fazla ~1 round önde. Bu tam kullanıcının modeli: "GridData oturmuş, TileView 1-2 kare geriden".

### 6.2 BoardVisualCoordinator (tek-blocking-sequencer'ı REPLACE eder)
Sorumluluk: fall/clear görsellerini blocking kuyruk yerine **paralel coroutine'lerle** oynatıp
aralarına bağımlılık-tabanlı başlangıç zamanı koymak. Mevcut `FallAction`/`MatchClearAction`
görsel işini yapmaya devam eder ama:
- **Non-blocking koşabilirler** (Blocking=false) ve per-tile ilerleme sorgulanabilir.
- Coordinator in-flight tüm coroutine'leri izler → "tüm görsel bitti mi" (input-unlock) bilir.

Per-tile varış sorgusu: `Vector2.Distance(rt.anchoredPosition, target) <= arrivalEpsCells*tileSize`.
`arrivalEpsCells` tunable (kullanıcı "son taş ~2 kare önce" → ~0.4-0.8 hücre).

### 6.3 Bağımlılık kuralları (overlap'in kalbi)
1. **Clear[match] başlangıcı:** match'teki TÜM taşlar arrival-eps'e girince. (→ "match düşüş
   bitmeden, taş girerken başlar" — kullanıcının #1 isteği.)
2. **Fall[round N+1] başlangıcı:** doldurduğu hücreler boşalınca. Bir hücre, onu boşaltan
   clear'ın "taş yok oldu" anında (clear animasyonunun ~ortası) serbest sayılır — clear'ın
   TAMAMI değil. (Faz 2 kazancı.)
3. **Lane bağımlılığı (diagonal/chest overlap fix):** C kolonuna YANDAN çapraz giren bir taş
   varsa, C kolonunda o çapraz taşın kesişme satırının ÜSTÜNE inecek düz taş, çapraz taş o
   satırı geçene kadar küçük bir gecikmeyle bekler. (→ kullanıcının #2 isteği, chest senaryosu.)

Kesişme tespiti (kural 3): çapraz record'un girdiği hedef kolon = `toX`, giriş satırı = `toY`;
aynı `toX`'e inen düz record'lardan `toY' < toY` (üstte) olanlara, çapraz record'un
`toY` satırına varış zamanına eşit `startDelay` verilir. Basit, path-kesişim geometrisi gerektirmez.

### 6.4 Zor alt-sistemler → BARRIER (ilk sürümde overlap'siz)
Special aktivasyon, combo, override, PulseCore zinciri, obstacle spread (oil/barrel), goal-orb,
PatchBot, boss-duel: ilk koordinatör sürümünde bunlar **barrier event** — koordinatör bunlardan
önce ve sonra tüm görselleri senkronlar (overlap yok). Böylece çift-patlama/çift-clear riski olan
karmaşık yollar bugünkü seri davranışı korur; yalnız **normal fall/clear overlap kazanır**.
Sonraki fazlarda barrier'lar tek tek gevşetilir (SpecialChainRunner ile hizalı,
[[project_special_chain_runner]]).

### 6.5 Entegrasyon noktası
`ResolveBoard` (BoardController ~2257): `while (actionSequencer.IsPlaying) yield` bekleme
noktaları koordinatör çağrılarıyla değişir. Karar mantığı (barrier sırası, HasAnyEmptyPlayableCell,
FindAllMatches, ExecuteClearPass) DEĞİŞMEZ; yalnız "ne zaman görsel başlasın / ne zaman devam
edeyim" koordinatöre delege edilir. Feature flag: `useDecoupledResolve` (BoardController serialized
+ sahne), false → bugünkü seri yol (anında geri-alma).

### 6.6 Determinizm / güvenlik
- Her tile bir kerede TEK aktif görsel coroutine'e sahip (`CancelActiveSettle`/`FallGeneration`
  guard'ları mevcut, [[project_tile_fall_flow]] non-blocking settle deseni).
- İşlenmiş-set: her clear/fall event bir kez. Mantık board'u önde mutate ettiği için, bir
  TileView re-target edilmeden önce mevcut görsel segmenti bitmeli (per-tile kilit).
- Coordinator "tüm görsel idle" → `OnActionSequenceFinished` eşdeğeri input-unlock tetikler.

### 6.7 Rollout (revize)
- **Faz 1:** BoardVisualCoordinator iskeleti + normal fall→clear overlap (kural 1) + zorlar barrier.
  Flag arkasında. Cihaz testi: normal cascade akışı, "match taş girerken başlıyor mu".
- **Faz 2:** Kural 2 (clear→fall overlap).
- **Faz 3:** Kural 3 (lane/diagonal) → chest overlap fix.
- **Faz 4+:** Barrier gevşetme (special/combo overlap), SpecialChainRunner entegrasyonu.

## 5. Durum
- fps=35 (hız kalibrasyonu tamam, ~57 kare) — sahne 01_Game.
- Koordinatör tasarımı: YAZILDI (§6).
- **FAZ 1 BİTTİ ✅ (cihazda onaylandı 2026-07-25):** `BoardVisualCoordinator.cs` (event-driven,
  detached fall + FallArrived arrival event) + `useDecoupledResolve` + `fallArrivalLeadCells`
  (0.2 ≈ 1 frame erken) + `TryBuildSimpleOverlapMatch` + special-safe sticky guard
  `hadSpecialActivityThisResolve`. TileView.FallArrived (MoveToGridCell x2 + MoveToGridPath;
  MaybeRaiseEarlyFallArrived lead ile erken). Match, taş oturmadan ~1 frame önce başlıyor;
  LineV/Override/PulseCore regression YOK. Timed sync KULLANILMADI. Kapatma: flag=false.
- Commit: kullanıcı isteyince.
- **Faz 2 (clear→fall overlap) ve Faz 3 (lane/chest diagonal) — sonraki.**
