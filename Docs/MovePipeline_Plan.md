# Hamle Hattı (Move Pipeline) Planı — special oynarken hamle + tek senkron akış

2026-09-26. `UnifiedSpecialFlow_Plan.md` (Faz 0-8) ve dynamic board denetiminin (2026-09-25) devamı.
Hedef: **special/combo oynarken de boş bölgede hamle yapılabilsin** ve special + combo + match temizliği
**tek bir senkron akış** kuralıyla yürüsün (cihazdaki takılma/bekleme şikâyetlerinin kökü).

## 0. KAPSAM KARARI (2026-09-26, kullanıcı)

"Special'lar patlarken izlemek keyifli, orada hemen hareket istemiyorum. Ama normal akışta ya da tahtanın
farklı yerlerinde hareket olurken başka yerinde hareket etmek istiyorum."
→ **Special/combo PATLAMASI sırasında genel kilit BİLİNÇLİ olarak kalır (P5 İPTAL).** Hedef: normal akışta
(eşleşme, kaskat, special OLUŞUMU, düşüş) tahtanın başka yerinde kesintisiz oynamak.

**N1 YAPILDI (2026-09-26):** Kök: pompanın kaskat temizlikleri "yeri belirsiz" (unlocalized) kaydediliyordu
→ `HasUnlocalizedClear` her kaskat temizliğinde TÜM tahtanın girişini kilitliyordu; ayrıca hamle
`WaitForDynamicInputCommit`'te HER temizliği (pompa dahil) bekliyordu — bekleme swap animasyonundan ÖNCE
olduğu için "kaydırdım, bir şey olmadı" hissi. Special doğuran eşleşme de unlocalized'dı (roket oluşurken
tahta kilitli). Düzeltme: `BuildClearPassAction` temizliği her zaman localized (special PATLAMASI =
isSpecialPhase yine unlocalized/genel kilit); `BoardFlowScheduler.MoveClearCount` = Clear − pompa
temizlikleri; yeni hamle yalnız önceki HAMLENİN temizliğini bekler, kaskatları beklemez.
Kalan normal-akış kilidi: barrel mud saçılımı (`SpreadingObstacles`) — gerekirse hedef hücre tutmaya çevrilir.

## 1. Bugünkü durum (koddan doğrulandı)

Çalışan: düşüş sırasında hamle (dynamic input), hücre bazlı tutma (CellHold), akış pompası (her kare
hücre bazlı yerçekimi + grup eşleşme), kesintisiz düşüş motoru.

Special sırasında hamleyi engelleyen **iki katman** var:

1. **Giriş kapısı** (`BoardController.CanTileUseDynamicInputCell`):
   - `Flow.IsSpecialVisualInFlight` → herhangi bir special görseli/zinciri varken HİÇBİR hücre oynanmaz.
   - `IsDynamicInputBlockedByBoardFlow` → SpecialSweep / ComboStep / ObstacleSpread activity'si veya
     ayak izi bilinmeyen (unlocalized) temizlik varken tüm tahta kilitli.
2. **Hamle park'ı** (`ProcessDynamicSwap` → `WaitForDynamicInputCommit`): kapı açık olsa bile hamle,
   o an süren TÜM temizlik/zincir/combo bitene dek bekletilir. Sebep: her hamle kendi `ResolveBoard`
   döngüsünü çalıştırır ve hamleye özel GENEL durumu değiştirir (lastSwapA/B → special'ın doğacağı hücre,
   jel/yağ yayılım bayrakları, hamle övgüsü sayacı). İki döngü aynı anda = çift shuffle, yanlış hücrede
   special, erken level-end riski. `dynamicSwapLogicDepth` aynı anda tek hamleye izin verir.

Yani kapıyı kaldırmak TEK BAŞINA işe yaramaz: hamle kabul edilir ama special bitene dek park eder.
**Asıl iş hamle çözümünü yeniden tasarlamak.**

Doğrulanan güvenli noktalar:
- `SpecialResolver` aksiyon listesini SENKRON kurar (yield yok) → `IsSpecialActivationPhase` bayrağı iki
  special'ın kurulumu arasında iç içe geçemez. Her `MatchClearAction` special bağlamını AÇIKÇA taşır
  (genel bayrağı miras almaz).
- Special'ların ayak izi zaten tutuluyor: normal temizlik (matches+affected+impact), LineH/V (tüm hat,
  `LineSweepGravityScope`), PulseCore karesi ve alanı (`SpecialChainRunner`), Override fanout hedefleri
  (`ceremonyHold`), booster, swap hücreleri, tetiklenmeyi bekleyen special hücreleri (pending = held).

## 2. Hedef model — "hamle = kısa, bağımsız bir iş"

- **Hamle kabulü tek kural:** iki hücre de oynanabilir (taş Idle, hareket etmiyor, tutulmuyor, rezervsiz,
  kilitli obstacle yok). Genel kapı yok.
- **Hamle kendi bağlamını taşır (MoveContext):** swap hücreleri (special doğum yeri), jel/yağ kaynağı,
  övgü sayacı. Genel lastSwap/per-move alanları kalkar.
- **Hamle kendi ResolveBoard'unu çalıştırmaz:** swap görseli → ilk temizlik (special oluşumlu) ya da
  special aksiyonları → bu işler akışa bağımsız (detached) verilir → hamle biter. Sonraki kaskatları
  pompa yapar (zaten yapıyor).
- **Hamle-sonu işleri TEK sahipte:** yağ/jel yayılımı, roket sepeti, mıknatıs kafesi, deadlock/shuffle,
  level-end değerlendirmesi → "tahta sakinleşti" anında, o ana dek tüketilen hamle sayısıyla BİR KEZ
  (`movesConsumedSinceIdle` zaten var). Çift shuffle yapısal olarak imkânsız.
- **Special fazı türevdir:** "special oynuyor mu" = kayıtlı SpecialSweep/ComboStep activity var mı.
  Yapışkan (sticky) bayraklar ve self-count baseline hack'i silinir.

## 3. Fazlar (her faz ayrı; cihazda onaylanmadan sonrakine geçilmez)

- **P1 — Güvenlik (YAPILDI, 2026-09-26):** kayma halindeki taş (`TileRuntimeState.Swapping`) hiçbir
  temizliğe/special hedefine giremez (`BoardAnimator.ClearMatchesAnimated` listesi +
  `SpecialUtils.CanTargetTileContent`). Swap veriyi animasyondan ÖNCE değiştirdiği için, genel kapı
  kalkınca bir patlama kayan taşı silip swap mantığını ölü/havuza dönmüş taşla bırakabilirdi. Bugünkü
  akışta davranış değişikliği yok (special sırasında zaten swap yapılamıyor).
- **P2 — Hamle-sonu tek sahip:** `ResolveBoard` içindeki hamle-sonu işleri (yağ/jel/roket/kafes/deadlock/
  shuffle/level-end) "tahta sakin + tüketilmiş hamle ≥ 1" kenarında tek yerden çalışır. Sıralı oyunda
  davranış birebir aynı olmalı. Önce envanter: ResolveBoard'da hangi adım neyi okuyor.
- **P3 — MoveContext tamamlama:** lastSwapA/B, jel kaynağı, oil hit, övgü sayacı hamle nesnesine taşınır;
  special oluşum yeri swap'ın kendi bağlamından okunur.
- **P4 — Hamle kendi ResolveBoard'unu bırakır:** `ProcessSwap`/`ProcessSpecialTap` swap + ilk temizlik /
  special aksiyonlarını detached başlatır ve döner. `WaitForDynamicInputCommit` ve
  `dynamicSwapLogicDepth` kalkar.
- **P5 — Genel giriş kapıları kalkar:** yalnız hücre kuralı. Önce "hedefi sonradan belli olan" etkilerin
  hücre tutması doğrulanır: PatchBot hedefi (uçuşta), Override+X dönüştürülen taşlar, Pulse+Pulse şarjı,
  ObstacleSpread (yağ/jel, per-move state P3'le çözülünce).
- **P6 — Temizlik:** sticky special-phase, self-count baseline, `IsSpecialVisualInFlight`'ın eski
  sinyalleri, 11 `CalculateCascades` çağıranın pompaya indirilmesi (tek yerçekimi sahibi).

## 4. Regresyon seti (her fazda)

Normal eşleşme + kaskat · 4'lü/5'li/L special oluşumu (doğru hücrede) · LineH/V · PulseCore · PatchBot ·
Override · her combo (Line+Line, Line+Pulse, Pulse+Pulse, Override+X, Override+Override, PatchBot+X) ·
booster'lar · shuffle (tek sefer) · yağ/jel yayılımı · barrel/mud · KeyGenerator · magnet · boss düellosu ·
son hamlede kazanma/kaybetme (popup zamanında) · bonus turu.
P4/P5 sonrası ek: special oynarken uzak köşede hamle, special alanına hamle (reddedilmeli), iki hamle üst üste.
