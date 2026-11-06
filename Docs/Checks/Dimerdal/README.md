# Dimerdal — current implementation


## Güncel — kutu başına adet ve otomatik hedef · 2026-11-05

Kullanıcı her kutuya kendi sayısını girmek istedi: 8+8 veya6+10. Ortak üretim kotası/toplu kırılma yerine kutuların kendi limitleri vardır; yalnız kendi son ürünü başarıyla yerleşince kırılırlar. Goals miktarı toplamdan otomatik hesaplanır, mor ortak ikon sürer. Editörde yeni kutu adedi ve mevcut kutuları ayrı düzenleyen liste vardır. [Güncel uygulama ve kontroller](Per-Box-Production/README.md). Aşağıdaki ortak kota/toplu kırılma açıklamaları tarihseldir.


## Ortak mor hedef ikonu · 2026-11-05

Goals/HUD, PreLevel ve sonuç ekranındaki ortak Diamond hedefi artık kullanıcının `modimerdal.png` görselidir. Dört renkli ürün sprite'ı korunur; 221/222/224/225 override ve 16 kütüphane hedef ikonu eşitlendi. Kayıt: `Docs/LevelDesign/Batches/222-225-Crystal-Shapes/Revisions/v3-shared-goal-icon/`.


## Güncel elmas çizimleri · 2026-11-05

Kullanıcının dört yeni renkli PNG'si tahta, kutu içi ve üretim uçuşunun ortak elmas assetleriyle değiştirildi. PNG bytes ve sprite kimlikleri korundu; yalnız importer kesim dikdörtgenleri yeni çizimlere uyarlandı. Eski görseller proje dışında `Wondi Paylaşım/codex/Dimerdal-Elmas-Yedek-2026-11-05` altında doğrulanmış yedekte. [Görsel ve kayıt](Diamond-Art-Replacement/README.md).

## Güncel — kota tamamlanınca kırılıp kalkma · 2026-11-05

Kullanıcı gri kapanışı KeyGenerator'a benzetti. Son gerekli elmas başarıyla yerleşince bütün görünür kaynaklar gerçek çerçevesinden büyük parçalara ayrılır ve normal `ClearObstacleFromLevel` yoluyla kaldırılır. Alt katman/örtü korunur; örtülü kaynak açılınca kırılır. Hücreler açılır ve mevcut resolve istenir. Önceki gri/%45 alfa davranışı ve shader/material kaldırıldı. Vuruş tepkisi, elmas eşleşmesi, üretim uçuşu ve toplama aynı. Güncel uygulama, testler ve performans sınırları: [Exhaustion-Break/README.md](Exhaustion-Break/README.md). Aşağıdaki gri kapanış bölümü tarihsel teslim kaydıdır.

Four colored slats open after four matching-color or special/tool hits. An already-open box produces one diamond from an adjacent matching-color group, or from a special/tool strike regardless of stone color. The fourth opening hit only opens the box. One connected normal group touching several boxes still produces one diamond. Existing strike/origin deduplication remains authoritative for specials.

The diamond inherits the box's color. It stays on the board and must be collected separately. Every color advances the common `CollectibleId.Diamond` goal. Production includes authored diamonds and in-flight commitments and stops at the authored goal amount. Open boxes remain useful special targets while production quota remains.

## Production and movement

`MatchClearAction` snapshots eligible groups before opening damage, then launches committed production only after the clear completes. All original matching stones stay in the normal clear flow. Interrupted clears return their commitments; stale batches cannot launch after disable/rebuild.

`DimerdalProductionFx` follows KeyGenerator's nonblocking pattern: rise about 2.5 cells, briefly gather, then arc onto an ordinary **idle** colored TileView. Falling, planned, pending-motion, landing-response, dragged, reserved, clearing, special and obstacle targets are ineligible. A target that starts moving during flight is reselected. Searches are throttled to 0.1 seconds with a finite 3-second lifetime. Local view claims prevent duplicate targeting without holding cells or gravity reservations.

Replacement changes the model to one of four appended `TileType.Diamond*` values and swaps the icon in place. It does **not** stamp an obstacle. Existing normal-tile systems handle swaps, hints/playable moves, gravity/diagonal flow, same-color matches, special creation and special/booster clears. Diamonds cannot mix with ordinary color stones or other diamond colors. An adjacent match does not clear a diamond because it has no obstacle layer to damage. The queued-clear guard compares the captured type and lifetime, preserving new diamond matches while rejecting an old normal match that now points at a converted stone. Type and lifetime share one snapshot dictionary.

Box, flight and board use the same authored sprite. The board icon and flight endpoint share an aspect-preserving square sized at 0.85 cells, avoiding inherited normal-tile stretch/layout bounds. Placement waits for landing cosmetics to finish and restores neutral icon scale without moving the root. Original PNGs and sprite references are unchanged. Existing authored obstacle IDs 96–99 are promoted to normal diamond tiles on spawn/reveal without collection events; initial settling preserves them. Keys and other destructive producers cannot overwrite diamonds. Normal shuffle permutations can move them; the recolor fallback preserves their counts.

## Collection

`BoardController.ClearAndDestroyTile` captures the live diamond before release. `TileView.SetSpecial` also collects the diamond consumed when a special is formed or implanted. A per-lifetime flag prevents a later special clear from crediting it again and resets on initialization/pool release. `DimerdalCollectionFx` runs **the same `AncientSealCollectFx` used by TarpStatue** with a diamond sprite override: pop, orbit/gather, stagger, wind-up, curved flight and arrival glow/ring/sparks. Diamonds collected in the same frame share a gather batch. The HUD count decreases on arrival, not when produced or destroyed.

A nonblocking GoalOrbFlight job keeps level completion waiting while gravity continues. Jobs and copied images are released on completion, early exit, exceptions, disable and level changes. A same-level interrupted collection credits the already-destroyed diamond once; old-level flights never credit a new HUD. Missing presentation/HUD targets fall back to immediate counting.

## Authoring and assets

Use `DimerdalBlue/Green/Yellow/Red` square definitions, sizes 1x1, 2x2 or 3x3. The user allowed retaining these variants to avoid stretching the artwork; arbitrary rectangular resizing was not added. Existing IDs84–99 and level221 remain compatible. Add `Collectible / Diamond` as the goal; box-breaking goals are impossible for permanent sources.

Twelve shared transparent textures provide frame, diamond and one reused slat per color. Stable sprite GUID/file IDs and cropped import rects are preserved. Door panels animate one quarter per hit in 0.22 seconds. Cover reveal retains opening state and the existing layered view.

## Performance and limits

Production flights/requests, graph buffers and view claims are reused. Production ghosts use UiVfxPool and one update loop. Collection records, root containers and principal images are pooled; the reused TarpStatue trail still has its existing temporary-dot/coroutine allocation path. No per-frame full-board target scan, per-flight board hold, local audio source or new audio path was introduced. These are code properties; device CPU/GC/frame time was not profiled.

Checks (no project build):

- `csi Tools/check_dimerdal_collection.csx`: 409 production rule/lifecycle checks, then the same checks with the actual shared TarpStatue collection routine. Covers idle-only placement, falling-target rejection/reroute, quota, stale and valid diamond clear guards, HUD timing, pool returns, cancellation and teardown.
- `csi Tools/check_dimerdal_matching.csx`: the actual full MatchFinder, normal conversion and special-formation collection methods; 39 checks covering every diamond color, mixed-color rejection, ordinary stones/keys, swaps, playable moves, 4/5 matches and collection deduplication.
- `csi Tools/check_dimerdal_fall.csx`: 125 existing real-motor checks plus 5632 comparisons between normal stones and diamonds placed while idle, covering vertical/spawn/diagonal routes, route extension, follower spacing, arrival and reservation cleanup.
- `csi Tools/check_shuffle_booster.csx`: 9920 shuffle regression assertions, including preservation of diamond identity/count in the forced recolor fallback.
- `python3 Tools/check_dimerdal_assets.py`: transparent imports, nonblocking legacy definitions, identical box/board diamond sprites, shared icons and preservation of pre-existing library/levels.

No Unity/project build, PlayMode/device replay, measured balance, publication or release baseline was performed. During this task separate CascadeLogic/TileFallMotionSystem edits appeared in the shared workspace; this task did not author or revert them. Preservation records distinguish them from Dimerdal changes.

## 2026-11-05 — donma düzeltmesi + KeyGenerator kuralı (kullanıcı kararı)

- **Kök sebep (donma):** elmas tanımları 96–99 `blocksCells: 1` idi; `ObstacleStateService.IsObstacleLayerBlockingCell`
  bunu duvar sayıyor → elmas yerçekimiyle DÜŞMÜYOR, altındaki hücreler yukarıdan beslenemiyordu (ekran: elmasın altında
  3 boş hücre, akış bitmiyor → giriş kilitli). Bütün diğer hareketli engellerde (WolfEgg, kargo…) 0. → **96–99 `blocksCells: 0`.**
  Kutular 84–95 duvar olarak kalır (doğru).
- **Yerleştirme = KeyGenerator kuralı** (kullanıcı: "aynen keygenerator gibi"): elmas yalnız YERİNDE DURAN, hareketsiz
  (IsRuntimeIdle, bekleyen düşüşü yok, rezerve hedef değil) renkli taşın yerine geçer. Havadaki/düşen taşa yazma KALDIRILDI
  (`BoardController.CanConvertDimerdalDiamond`). Hedef uçuşta düşmeye başlarsa `DimerdalProductionFx` yeniden hedef seçer.
  Elmas sonra normal taş gibi düşer; toplama değişmedi.
- Önceki havada dönüştürme testleri yeni kurala göre güncellendi; artık test hatası beklenmiyor.

## 2026-11-05 — üretilen elmas = normal taş (son kullanıcı düzeltmesi)

Kullanıcı: “burada üretilen elmaslar normal taş gibi çalışacak”. Önceki hareketli-engel modeli kaldırıldı. Aynı renkteki elmaslar kendi aralarında eşleşir, normal taşlarla takas edilir, düşer/yana kayar ve special oluşturabilir. Komşu normal eşleşme elması kırmaz; special ve booster doğrudan temizler. Toplama TarpStatue uçuşuyla, hedef sayımı varışta ve yalnız bir kez gerçekleşir. Dar görünümü gidermek için uçuş ve tahta ikonlarının oranı/ölçüsü ortaklaştırıldı. Bu bir oyun kodu değişikliğidir; üretim seviyeleri yeniden tasarlanmadı, build veya yayın yapılmadı.

## 2026-11-05 — vuruş tepkisi ve üretim tamamlanma görünümü

Kabul edilen açma ve üretim vuruşları0.26sn görsel büyüme/sarsıntı ve yeniden kullanılan aperture beyazflash ile gösterilir. HostRectTransform/flightorigin sabittir. Kepenk açılması hâlâ hit başına bir dilimdir.

`DimerdalService` tamamlanmayı `committed >= capacity && pendingReservations == 0 && !production.HasActiveFlights` ile belirler. Yalnız son başarılı yerleşimden sonra bütün renklerin ortak kaynakları0.30sn'de kapanır; ürün resmi gizlenir,aynıasset-backedgrayUI material ve45%alpha kullanılır. Uçuştaki/rezervasyonlu sonürün erken kapatmaz. Başarısız/iptal uçuşun kotası geri gelir;boşta/taze/reveal kutu güncelgrup durumuyla eşitlenir. Kalan anlamlı hedefquery'si kota ayrılmışken0olur; başarısızlıkta geri döner.

Yeni efekt objelerihostbaşına bir kez kurulur;perhitGameObject/texture/material/Coroutine üretimi yok. Aynıservis yalnız hareketlianimasyonları tickeder. Kutu taraması yalnız completionboolgeçişinde;boardscan eklenmedi. Disable/build flash,transformpose ve aktiflisteyi temizler. Material oyunassetidir,clonelanmaz;UIclip/stencil desteklenir. Ses kaynağı/yeni ses yolu yok.

`csi Tools/check_dimerdal_feedback.csx`:46gerçekview/service/flightkuralı;100tekrarhitGameObjectreuse,kapatma,45%alpha,paylaşılanmaterial,sonürünlandingi,iptal/timeoutquota,reveal,teardown,authored ürünler.409mevcutüretim/lifecycle ve39MatchFinderregresyonu da geçti;ortakTarpStatuecollectrutiniyle tekrarlandı(Unitydoubles). Yeni shadergrayscale/alfa/stencil/clip kaynak kontratı kontrol edildi;Unity/GPUcompile/Play/cihazperformans ölçümü yok.
