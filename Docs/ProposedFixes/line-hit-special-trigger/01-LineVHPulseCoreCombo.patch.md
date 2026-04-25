# Line-hit special trigger fix

Bu dosya `fix/line-hit-special-trigger-v2` branch'i için hazırlanan uygulanacak kod parçasıdır.

Hedef:
- `LineVHPulseCoreCombo` işi `LineHSpecial` / `LineVSpecial` virtual origin akışına bırakır.
- `LineH/LineV` special chain tetiklerini logic phase'de değil, line sweep hücreye vardığında çalıştırır.

## Assets/_Project/Scripts/Grid/Board/Combos/LineVHPulseCoreCombo.cs

### 1) `Execute(...)` metodunu bununla değiştir

```csharp
public LineVHPulseCoreComboExecutionResult Execute(LineVHPulseCoreComboExecutionRuntime rt)
{
    var result = new LineVHPulseCoreComboExecutionResult();

    if (!CanExecute(rt))
        return result;

    var pulseTile = GetPulseTile(rt);
    var lineTile = GetLineTile(rt);
    var comboCenterTile = rt.Partner != null ? rt.Partner : pulseTile;
    var comboCenterCell = new Vector2Int(comboCenterTile.X, comboCenterTile.Y);

    rt.EmitComboTriggered?.Invoke(lineTile.GetSpecial(), pulseTile.GetSpecial(), comboCenterCell);

    RegisterComboTiles(rt, lineTile, pulseTile);

    // PulseCore + LineV/H kendi özel travel/clear action'ını çalıştırmaz.
    // 3 satır + 3 sütun LineH/LineV special virtual-origin akışına bırakılır.
    return ExecuteLineHVCrossCombo(rt, comboCenterCell);
}
```

### 2) `ExecuteLineVCombo(...)` metodunun üstüne bunu ekle

```csharp
private LineVHPulseCoreComboExecutionResult ExecuteLineHVCrossCombo(
    LineVHPulseCoreComboExecutionRuntime rt,
    Vector2Int comboCenterCell)
{
    var result = new LineVHPulseCoreComboExecutionResult();

    var hOrigins = BuildLineHVirtualOrigins(rt.Board, comboCenterCell);
    var vOrigins = BuildLineVVirtualOrigins(rt.Board, comboCenterCell);

    int total = hOrigins.Count + vOrigins.Count;
    int index = 0;

    rt.DebugLog?.Invoke(
        $"[LineVHPulseCoreCombo] Cross delegate hOrigins={hOrigins.Count} vOrigins={vOrigins.Count} center={comboCenterCell}");

    foreach (var originCell in hOrigins)
    {
        index++;
        bool finalizeAtEnd = rt.FinalizeAtEnd && index == total;

        rt.DebugLog?.Invoke(
            $"[LineVHPulseCoreCombo] delegate LineH origin=virtual({originCell.x},{originCell.y}) finalize={finalizeAtEnd}");

        var lineResult = ExecuteLineHAtVirtualOrigin(rt, originCell, finalizeAtEnd);
        if (lineResult != null && lineResult.Actions != null && lineResult.Actions.Count > 0)
            result.Actions.AddRange(lineResult.Actions);
    }

    foreach (var originCell in vOrigins)
    {
        index++;
        bool finalizeAtEnd = rt.FinalizeAtEnd && index == total;

        rt.DebugLog?.Invoke(
            $"[LineVHPulseCoreCombo] delegate LineV origin=virtual({originCell.x},{originCell.y}) finalize={finalizeAtEnd}");

        var lineResult = ExecuteLineVAtVirtualOrigin(rt, originCell, finalizeAtEnd);
        if (lineResult != null && lineResult.Actions != null && lineResult.Actions.Count > 0)
            result.Actions.AddRange(lineResult.Actions);
    }

    return result;
}
```

### 3) `ExecuteLineVAtVirtualOrigin(...)` runtime initializer içine ekle

```csharp
DeferChainUntilLineHit = true,
ExecuteSpecialActions = rt.ExecuteSpecialActions,
```

Örnek konum:

```csharp
var result = lineV.Execute(new LineVExecutionRuntime
{
    Board = rt.Board,
    Context = rt.Context,
    Origin = null,
    Partner = null,
    VirtualOriginCell = virtualOriginCell,
    FinalizeAtEnd = finalizeAtEnd,
    SuppressVisualSideEffects = false,

    DeferChainUntilLineHit = true,
    ExecuteSpecialActions = rt.ExecuteSpecialActions,

    ActivateSpecial = (resolution, tile, partner) =>
    {
        rt.ExecuteSpecialActions?.Invoke(resolution, tile, partner);
    },
    EnqueueChainSpecials = resolution => EnqueueNewlyAffectedSpecials(rt, pending),
    ProcessQueue = resolution => ProcessPendingChainQueue(rt, pending, nestedResult, "LineV")
});
```

### 4) `ExecuteLineHAtVirtualOrigin(...)` runtime initializer içine ekle

```csharp
DeferChainUntilLineHit = true,
ExecuteSpecialActions = rt.ExecuteSpecialActions,
```

Örnek konum:

```csharp
var result = lineH.Execute(new LineHExecutionRuntime
{
    Board = rt.Board,
    Context = rt.Context,
    Origin = null,
    Partner = null,
    VirtualOriginCell = virtualOriginCell,
    FinalizeAtEnd = finalizeAtEnd,
    SuppressVisualSideEffects = false,

    DeferChainUntilLineHit = true,
    ExecuteSpecialActions = rt.ExecuteSpecialActions,

    ActivateSpecial = (resolution, tile, partner) =>
    {
        rt.ExecuteSpecialActions?.Invoke(resolution, tile, partner);
    },
    EnqueueChainSpecials = resolution => EnqueueNewlyAffectedSpecials(rt, pending),
    ProcessQueue = resolution => ProcessPendingChainQueue(rt, pending, nestedResult, "LineH")
});
```

> Not: `LineVHPulseCoreComboAction` ilk testte silinmek zorunda değil. `Execute(...)` artık `CreatePulseEmitterComboAction(...)` çağırmadığı için devre dışı kalır.
