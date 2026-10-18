# Brandalı Heykel (TarpStatue) — kurallar (2026-10-13, kullanıcı onaylı)

Çerçeve içinde brandayla örtülü taş blok. Önce etiket rengindeki taşlar toplanır, branda kalkar; sonra taş vurularak
aşama aşama heykele dönüşür; bitmiş heykel hedefe uçar, engel parçalanıp yok olur. Hedef = heykel sayısı.

## Nesne

| | |
|---|---|
| Id | `TarpStatue = 68` |
| Boyut | **Kare, heykel başına: 2×2 / 3×3 / 4×4** — tek engel (aynı id), boyut `TarpStatueEntry.size`; ayak izi `LevelData.GetObstacleFootprint` |
| Renk | Etiket 4 renkten biri: kırmızı (Core), sarı (Gear), mavi (Bolt), yeşil (Plate). Level'da engel başına seçilir |

## Akış

1. **Branda (toplama):** etiketteki sayı, o renkte tahtanın **herhangi bir yerinde** temizlenen taş başına 1 azalır
   (geri sayım 30 → 0). Toplanan taş etikete uçar (AncientSeal toplama efekti). Bu evrede vuruş hasar vermez.
2. **Branda soyulur** (sayaç 0): branda alttan yukarı doğru soyularak açılır, kalkar → taş blok görünür.
3. **Taş aşamaları:** YALNIZ special 1 aşama ilerletir; normal eşleşme vurmaz (2026-10-13 kullanıcı kararı; SculptingSpecial kuralı, damageRule=SpecialOnly). Her vuruşta taş
   parçaları kopup düşer (taş görsellerindeki çakıllardan kesilen Chip_0..5 sprite'ları).
   Aşamalar: kaba blok → kaba aslan → yarım oyulmuş → bitmiş heykel.
4. **Son:** bitmiş heykel hedef sayacına uçar; çerçeve + zemin parçalanıp savrulur (AncientSeal kırılması), engel kalkar.
   - Varsayılan uygulandı: taş evresi **3 vuruş**, bitmiş heykel ortaya çıkınca hemen uçar (kullanıcı onayı bekliyor).

## Yığın rolü

- **Üstüne çim/saydam örtü gelebilir** → önce örtü kırılır, engel yığın kuralıyla (hit-lock) korunur.
- **Altına engel saklanabilir** (altın, çamur…) → kırılınca açılır. Sıra kuralı (AncientSeal 126 dersi): önce
  altındakini aç, SONRA hedef bildirimi — yoksa altın toplanmadan level kazanılır.

## Yeniden kullanılacak altyapı

| Parça | Kaynak |
|---|---|
| Renk sayacı + taş uçuşu | `AncientSealService` / `AncientSealCollectFx` |
| Aşamalı sprite + bitişik vuruş | `SculptingStone` (ObstacleDef stage'leri) |
| Parçalanma (Mask pencere + savrulma), kırılmada ayak izi tutma | `AncientSealView` + `BeginBreakHold` |
| Hedefe uçuş | `GoalFlyFx` |

## Gereken sprite'lar (aynı kadraj/boyut, şeffaf arka plan, tam karşıdan)

1. Çerçeve — yalnız turuncu çerçeve + kayışlar, ortası boş (dört kenar eşit kalınlık).
2. İç zemin — çerçeve içini dolduran mavi kumaş.
3. Branda örtüsü — yalnız buruşuk mavi branda, çerçevesiz.
4. Taş aşamaları ×4 — yalnız taş/heykel, zemin ve çerçeve yok.
5. Etiket — ip + beşgen plaka, sayısız, tek renk (diğer 3 rengi ton kaydırmayla türetilir).

## Uygulama (2026-10-13 — KOD YAZILDI, Unity testi bekliyor)

Mimari: heykel **sıradan kütüphane engeli** (ObstacleLibrary id 68: 2x2, hits 3, OverTileBlocker, damageRule SpecialOnly, SculptingStone sesi)
→ hasar, aşama, yığın (üstte çim / altta saklı engel), editör paleti, hedef sayımı mevcut sistemden. Üstüne:

| Parça | Yer |
|---|---|
| Renk/sayı/boyut (engel başına) | `LevelData.tarpStatues` (`TarpStatueEntry`); editör paleti: yeni heykel ayarı + tahtadaki heykellerin düzenlenebilir listesi (sonradan renk/sayı/boyut değişir) |
| Branda kilidi | `ObstacleStateService.TarpStatueCoveredQuery` → TryDamageAt vuruşu yutar; PatchBot hedeflemez |
| Toplama (uçuş + sayaç) | `Grid/TarpStatue/TarpStatueService.cs` (AncientSealCollectFx, `ICollectionFxTarget` ile genelleştirildi) |
| Görünüm (katmanlar, soyulma, parça, parçalanma) | `Grid/TarpStatue/TarpStatueView.cs` — GridSpawner engel Image'ına takılır (LaserCannon deseni) |
| Final (ayak izi tutma + heykel uçuşu) | `TarpStatueService.TryPlayFinale` — yok edildi/görsel temizlendi olaylarından hangisi önce gelirse |
| HUD hedef ikonu | `Resources/TarpStatue/Lion_Icon` (TopHudController) |
| Sprite'lar | `Resources/TarpStatue/` — Frame, Background, Tarp, Stone_1..4, Tag_Red/Yellow/Blue/Green, Chip_0..5, Icon_* (palet) |
| Simülasyon | Taş evresi doğru, branda toplaması modellenmiyor (Approximate) |

## 2026-10-13 test düzeltmeleri

- Taşlar çerçeve içini dolduracak şekilde büyütüldü (2. tur: her aşama KENDİ yüksekliğiyle iç yüksekliği doldurur, tepe üst çubuğun hafif arkasına; ölçekler 0.89–0.93 → geçişte boy farkı küçük); katman sırası
  zemin → taş → branda → çerçeve (çerçeve en üstte, taş kenarları çubukların arkasına girebilir).
- Sarı seçilip kırmızı kalması: renk yalnız yerleştirme anında yazılıyordu → palette tahtadaki heykeller düzenlenir.
- Heykel her durumda HUD'a uçar: hedef slotu varsa ona, yoksa HUD ortasına.
- Vuruş parçaları büyütüldü (heykel kenarının %17–27'si) ve daha güçlü saçılır.
- Tanıtım ipucu eklendi (ObstacleHintLibrary id 68 + hint_obstacle_tarpstatue TR/EN); bilardo topu için de (id 66; cep ayrı anlatılmaz).
- Sesler (kütüphane tanımında, level'la önceden yüklenir; taş toplama sesi YOK — oyunun kırılma sesleri yeter, kullanıcı kararı):
  `Audio/SFX/TarpStatue_*.wav` — hitSound = StoneHit (her special vuruşu), breakSound = Reveal (son vuruş, heykel ortaya
  çıkar), followUpBreakSound = FrameShatter (çerçeve parçalanırken, servis), coverRemovedSound (yeni ObstacleDef alanı) =
  TarpOff (branda soyulurken, servis).
