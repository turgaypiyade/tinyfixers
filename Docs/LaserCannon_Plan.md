# Lazer Topu (Laser Cannon) — yeni engel planı (2026-10-11)

## Kullanıcının verdiği kurallar
- **Yalnız special ile hasar alır.** 4 vuruş: tabandaki 4 cıvata başta GRİ; her special vuruşunda biri TURUNCU yanar.
  4'ü tamamlanınca engel **parçalanarak** oyundan çıkar.
- **Taret serbestçe döner**; boştayken (idle) de arada kendi kendine döner.
- **Hedefleme:** tahtada bir special "yakaladığında" namluyu ona çevirir, **geniş saydam bir lazer** gönderir;
  special'ın arkasında **çok parlak bir hale** (işaret) oluşur.
- **Kullanılmazsa:** işaretlenen special o hamlede kullanılmazsa, **ikinci hamlede kafese alınır** (Magnet'teki kafes:
  `SpecialLockCoordinator`, `TileView.IsSpecialLocked`).
- **Kafes açma (güncel):** kafesli special'ın 4 komşusunda **normal eşleşme** olursa ya da special'ın **etki alanı** onu kapsarsa kafes **o anda** kalkar (kafes şişip söner). Serbest kalan taşa `TileView.CageGrace` verilir: oyuncunun yeni hamlesine / tahta durulana dek etki alanları onu patlatmaz (aynı zincir tetiklemesin), swap serbest. Tek giriş: `BoardController.NoteCageImpact` (BoardAnimator'ın iki engel-hasarı döngüsü çağırır; booster açmaz).
  (Motorda `ReleaseCoveredBy` var ama AoE'ye BAĞLI DEĞİL — eklenecek; şu anki kural "kilitli special AoE'de patlamaz".)
- Sonra döngü baştan: yeni special arar.

## Görseller (hazır)
`Resources/LaserCannon/`: `LaserCannon_Base.png` (gri cıvatalı taban), `LaserCannon_Turret.png` (dönen taret, %94,
pivot tam merkez), `LaserCannon_Bolt.png` (turuncu cıvata, 61 px). 512×512, dönme merkezi tuval ortası.
Cıvata merkezleri (0–1, sol-üst orijin): sol üst (0.189, 0.216), sağ üst (0.814, 0.216), sağ alt (0.814, 0.780),
sol alt (0.187, 0.779). Kaynak: `~/Downloads/laserLauncher*.png` (kullanıcı çizimi).
Lazer, hale ve kafes: kodla (kafes için Magnet'in `SpecialLocker` sprite'ı yeniden kullanılabilir).

## Kararlar (kullanıcı cevapları, 2026-10-11)
1. **Boyut 2×2** (hem en küçük hem en büyük). Sabit, hücre kapatır.
2. Aynı anda **bir** special seçer ve onu takip eder (düşerse lazer de takip eder). Seçim: topa en yakın special.
3. Zamanlama: hamle N sonunda işaret → hamle N+1 sonunda hâlâ kullanılmamışsa kafes. Ardından aynı hamle sonunda yeni hedef.
4. Kafes açılınca special **serbest kalır, patlamaz**. Açıldığı hamle sonunda yeniden hedeflenmez.
5. İki top **aynı special'ı hedeflemez**.
6. Hedef (goal) değil; ama **PatchBot onu hedef gibi arar** (`PatchBotTargetCoordinator`).
7. **Yığın (167 kullanıcı düzeltmesi):** üstüne örtü gelebilir; ayrıca altına gizli jel konabilir. Lazer 2×2 üst katman olarak yerleştirilir, tek hücrelik jel tabanı tamamen örter. Jel lazer kırılana kadar görünmez, yayılmaz ve kaplama hedefine sayılmaz; kırılınca mevcut beneath/reveal akışı açar. Editörde lazerin üst katman yasağı kaldırıldı; sıvı taban ve tam örtme kuralları korunur. Lazer kendi üstünde örtü varken pasiftir.
8. Tanıtım: **Level 131**.
Ek: top yıkılınca kafesledikleri serbest kalır (patlamaz).

## Uygulama
- `ObstacleId.LaserCannon = 65`; `ObstacleLibrary` kaydı: 2×2, 4 vuruş, 4 stage `damageRule: SpecialOnly`.
  Stage sprite = `LaserCannon_Icon.png` (taban+kule birleşik; editör/hedef/ipucu ikonu).
  `auxiliarySprites`: [0] kule, [1] cıvata, [2] taban (sahnede Image yalnız tabanı gösterir).
- `Grid/LaserCannon/LaserCannonView.cs`: kule + 4 cıvata child'ı; `SetLit` (vuruşta cıvata patlayarak yanar), `SetAngle`.
- `Grid/LaserCannon/LaserCannonService.cs` (board'a runtime eklenir): `OnPlayerMoveResolved`'da kafes + hedef seçimi;
  tek `Update` döngüsünde kule dönüşü (hedefe SmoothDampAngle, boşta 2.5–5 sn'de bir rastgele), lazer (geniş saydam
  lazer yeşili yamuk bant (`LaserBeamGraphic`): namluda 0.32 hücre, ~1.5° açıyla genişler, alpha 0.5, hedefe 0.6 sn'de uzanır; ilk varışta güçlü parlama (hale büyür+beyaza çalar, üstte yeşil halka dalgası) ve special'ın arkasında 4 köşeli L nişangâh (varışta büyükten içe kilitlenir); kule SmoothDamp 0.2 sn / 380°/s, boşta 0.35 sn / 220°/s, parlak çekirdek yalnız kafes atışında çakar, `BreakFxParent`'ta, top başına bir kez kurulur), special'ın arkasında hale (taşa bağlı,
  ikondan önceki kardeş), kafes görseli (`Magnet/SpecialLocker`, havuzlu). Kırılma: 4. cıvata yanar, hücreler kısa
  tutulur (`HoldCells` + `ObstacleCollapse` işi), taban/kule parçalara ayrılıp uçar.
- `SpecialLockCoordinator`: `LockSpecial(..., releaseWhenCovered)`, `NoteCovered`, `ReleaseOwnedBy`. Kafes etki anında açılır; CageGrace aynı zincirden korur.
- `SpecialChainRunner.IsCaged` artık örnek metodu: etki alanına giren kafesli special'ı `NoteCovered` ile bildirir.
  Yalnız special yayılım motoru (runner) bildirir; booster'lar (çekiç vb.) kafesi açmaz.
- `GridSpawner`: `DrawObstacleImage` → view + kayıt; vuruşta `SetLit` + sarsıntı; temizlenince `TryPlayBreak`.
- Editör: overlay modunda reddedilir. Sim: `NotSimulated`. İpucu: `hint_obstacle_lasercannon(_name)` TR/EN.
- Ses: kütüphanede PaintCanBox'ın hit/break sesleri; lazer kilitlenme sesi yok.

## Test edilecek
- Kule hedefe dönüyor mu, lazer special düşerken takip ediyor mu; hale special'ın ARKASINDA mı.
- 2. hamle sonunda kafes; kafesli special'a başka special'ın etki alanı değince hamle sonunda açılıyor mu, patlamıyor mu.
- İki top farklı special seçiyor mu; örtülü top pasif mi.
- 4 special vuruşunda 4 cıvata ve parçalanma; PatchBot topa gidiyor mu.
- Kafes sonrası oynanabilir hamle kalmazsa shuffle (BoardController mevcut kontrolü).
