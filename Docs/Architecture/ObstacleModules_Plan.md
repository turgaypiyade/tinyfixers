# Engel Modülleri — ortak sınıfların şişmesini durdurma planı (2026-11-07)

## Sorun (ölçüldü, 2026-11-07)

| Dosya | Satır | Engel-özel izler |
|---|---|---|
| BoardController.cs | 6626 | 22 farklı `ObstacleId.X`, `Dimerdals`/`FlyFruits` gibi servis property'leri, `TryPlaceFlyEgg`/`TryConvertDimerdalDiamond` |
| ObstacleStateService.cs | 3181 | 219 `ObstacleId.X` referansı (36 farklı id), **29 public Func/Action interceptor alanı** (`DimerdalHitInterceptor`, `FlyEggBoxRemainingQuery`, …) ve id başına `if` zincirleri (`ApplyHit`, `GetActiveMeaningfulHitsAt`) |
| GridSpawner.cs | 4743 | 155 referans, engel başına `BuildX()` + `ConfigureObstacleContentView` içinde id dalları |
| TileView.cs | 3283 | Dimerdal elmas alanları (`DimerdalCollectionStarted`, `ApplyDimerdalDiamond`) |
| LevelDataEditor.cs | 3147 | 104 referans, `ObstacleDisplayName` switch'i |

Her yeni engel aynı 5 dosyaya dokunuyor → çakışma, gözden kaçan teardown, test yükü.

## Hedef: tek arayüz + kayıt defteri (registry)

```csharp
// Engel ailesinin TEK sözleşmesi. Ortak sınıflar yalnız bunu bilir.
public interface IObstacleModule
{
    bool Owns(ObstacleId id);                                   // Dimerdal: 84..95, Fly: 100..102
    void Build(BoardController board, LevelData level);         // GridSpawner.BuildX() yerine
    bool TryConfigureView(int origin, ObstacleDef def, Image img); // ConfigureObstacleContentView dalları yerine
    ObstacleHitResult? TryHit(in ObstacleHitRequest hit);       // null = varsayılan HP yolu; interceptor alanları yerine
    int MeaningfulHits(int origin);                             // GetActiveMeaningfulHitsAt dalları yerine
    void Teardown();                                            // seviye değişimi / OnDisable
}

public sealed class ObstacleModuleRegistry
{
    private readonly IObstacleModule[] byId = new IObstacleModule[256]; // O(1), sıcak yolda alloc yok
    public void Register(IObstacleModule m) { for (int i = 0; i < byId.Length; i++) if (m.Owns((ObstacleId)i)) byId[i] = m; }
    public IObstacleModule Get(ObstacleId id) => (uint)id < byId.Length ? byId[(int)id] : null;
    public T Find<T>() where T : class, IObstacleModule { … }   // board.Dimerdals/board.FlyFruits property'leri yerine
}
```

- `ObstacleStateService.ApplyHit` başında: `var m = modules.Get(id); if (m != null && m.TryHit(req) is { } r) return r;`
  → 29 delegate alanı ve id `if`'leri zamanla silinir.
- `GridSpawner`: `modules.BuildAll(level)` + `modules.Get(def.id)?.TryConfigureView(...)`.
- `BoardController`: yalnız genel yetenekler kalır (`CanReplaceIdleColorTile`, `SyncTileData`, `BeginJob`).
  `TryPlaceFlyEgg` → FlyFruit modülüne, `TryConvertDimerdalDiamond` → Dimerdal modülüne taşınır (genel API'yi çağırırlar).
- `TileView`: engel durumu tile üstünde alan olarak değil, modülün `Dictionary<TileView, …>`/`ConditionalWeakTable`'ında; ya da
  tek genel `TileView.ObstacleTag` (int) alanı.
- Editör: görünen ad, palet grubu, ayak izi kontrolleri `ObstacleDef` verisine (`displayNameTr`, `paletteGroup`) taşınır;
  `LevelDataEditor` switch'leri kalkar. AGENTS.md editör kuralı modül şablonunun parçası olur.

## Performans

Registry dizisi id ile O(1); arayüz çağrısı delegate çağrısıyla aynı maliyette. Yeni Update yok: modüller mevcut
tek-Update kalıbını korur (FlyFruitService/DimerdalService zaten öyle). Kazanç ölçülmedi; amaç bakım, perf değil.

## Geçiş (strangler — büyük patlama yok)

1. **Pilot (1 iş):** `IObstacleModule` + registry ekle; FlyFruit ve Dimerdal'ı (en yeni, en izole) taşı. Eski delegate
   alanları paralel çalışır; davranış birebir. Kullanıcı L221–232'yi oynar.
2. **Kural:** bundan sonraki HER yeni engel yalnız modül olarak yazılır; ortak 5 dosyaya `ObstacleId.X` eklenmez.
3. **Göç:** eski engeller (KeyGenerator, BirdHouse, Safe, Wall/LampStrip/Windmill, Hamster, RocketBasket, Magnet, Tube…)
   iş arası, ayda 2–3 tane; her biri ayrı commit + cihaz testi.
4. Son adım: boşalan interceptor alanları ve `board.X` property'leri silinir.

Risk: hit sırası/dedup (`emittedOriginsThisMove`) ortak kalmalı — modül yalnız "bu vuruşu kabul ediyor muyum" kararını
verir, sayaç/strike dedup ObstacleStateService'te kalır.
