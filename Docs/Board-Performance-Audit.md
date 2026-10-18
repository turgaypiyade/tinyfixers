# Board performans incelemesi — 2026-10-17

Bu inceleme kaynak kodu ve bağımsız davranış kontrolleridir. Unity Play Mode, build, Profiler veya Frame Debugger çalıştırılmadı; cihazdaki GC Alloc, frame süresi ve batch sayısı ölçülmedi.

| Konu | Mevcut durum / yapılan değişiklik |
| --- | --- |
| Taş havuzu | `01_Game` sahnesinde zaten açık. Varsayılan da açık yapıldı; başlangıç gridinden sonra 24 yedek taş hazırlanıyor (`tilePoolWarmCount`, hücre sayısıyla sınırlı). Havuz biterse dinamik büyüme hâlâ mümkün. |
| Cargo | Ghost sprite/konumu yakaladıktan sonra gerçek taş havuza dönüyor. Eski uçuşun tamamlanması yeniden kullanılan taşı etkilemiyor; lifetime kontrolü ve iptal kontrol edildi. |
| Efekt / yazı | `UiVfxPool`, `UiFxPool`, `BoardBreakFxService` mevcut. GearCup üç yazıyı önceden hazırlıyor ve fazlasını birleştiriyor. Tüm efektler başlangıçta hazırlanmış değil; ilk kullanımda büyüme var. |
| Durağan tahta | `BoardFlowPump`, sakinliği doğruladıktan sonra veri değişimi, varış veya hücre bırakma olmadan yeniden grid taramıyor. Aktif cascade, bekleyen eşleşme ve çalışan işler kesintisiz mevcut pompayı kullanıyor. |
| Grid / fizik | Eşleşme ve düşüş matematiksel grid üzerinde. Grid oyun mantığında Collider2D, Rigidbody2D veya Physics2D sorgusu bulunmadı. |
| Geçici bellek | Senkron yerçekimi/segment sorgularının listeleri yeniden kullanılıyor. MatchFinder enum karşılaştırmalarında boxing kaldırıldı. Gecikmeli action’ların sahip olduğu rota/dizi/listeler paylaşılmadı; yaşam döngüleri farklı. |
| Render | QualitySettings üzerinden URP, SRP Batcher açık. Sprite Packer açık, fakat projede Sprite Atlas asset’i yok. Atlas eklenmedi. |

## Açık kalan işler

- `GoalFlyFx` normal hedef ve Cargo ghost’larını hâlâ oluşturup yok ediyor. Güvenli havuzlama, coroutine sahibi ve iptal/teardown için lease takibiyle birlikte ele alınmalı.
- `ClearCellVisualOnly` / `DestroyOrphanedTileView` gibi bazı special yolları doğrudan Destroy kullanıyor. Bekleyen action’lar ham TileView referansı tuttuğu için bunları doğrudan havuza çevirmek yeniden kullanılan taşa eski callback’in dokunmasına yol açabilir. Önce çağıranların lifetime sahipliği dönüştürülmeli.
- Cascade rota/action nesneleri ve bazı coroutine’ler hâlâ allocation üretiyor. Tüm cascade için “0 GC Alloc” sonucu çıkarılamaz.
- Board, UI Image/CanvasRenderer kullanıyor. Atlas aynı texture kullanımına yardımcı olur; farklı materyal, clipping ve Canvas sınırları batching’i bölebilir. SRP Batcher tek UI draw call garantisi değildir. [Unity uGUI Profiler belgesi](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Documentation~/ProfilerUI.md).
- Bütün yüksek çözünürlüklü art’ı tek atlas sayfasına koymak yerine birlikte kullanılan board görselleri ayrılmalı. Canvas UI için atlas rotation kapalı olmalı. [Unity Sprite Atlas ayarları](https://docs.unity.com/en-us/engine/6000.3/manual/unity2d/sprite/atlas/sprite-atlas-reference).

## Doğrulama

- `Tools/check_board_perf_paths.csx`: 511 kontrol. 10.000 değişmeyen idle tick’inde yalnız ilk grid taraması; veri/varış/kilit bırakma ile uyanma; aktif işlerde pompanın devamı; 500 farklı gridde aynı yerçekimi kararı.
- Aynı bağımsız Mono harness’inde, ısınmış 2.000 senkron yerçekimi sorgusu: önce 1.216.000 bayt, sonra 0 bayt. Bu yalnız bu sorgunun ölçümüdür; Unity veya tüm cascade ölçümü değildir.
- `check_tile_pool_reserve.csx`: 13 reserve, yeniden kullanım, Cargo handoff ve iptal kontrolü. `check_tile_lifetime.csx`: 27 yaşam döngüsü kontrolü. `check_diagonal_cascade.csx`: 3.086 çapraz düşüş kontrolü.
- Kaynak/API incelemesi assembly üretmeden mevcut Unity referanslarıyla yapıldı. Build çalıştırılmadı.

Unity’de cihaz Profiler’ıyla ilk kullanım ve ısınmış uzun cascade ayrı ölçülmeli: GC Alloc/call stack, Instantiate/Destroy, UI rebuild ve idle script CPU. Frame Debugger gerçek texture/material/clipping batch ayrımlarını göstermeli. Mevcut `GC.GetTotalMemory(false)` tanı değeri net heap değişimidir; Profiler’ın GC Alloc metriği değildir.
