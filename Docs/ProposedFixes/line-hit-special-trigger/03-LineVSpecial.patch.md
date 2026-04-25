# LineVSpecial hit-based chain patch

Path: `Assets/_Project/Scripts/Grid/Board/Specials/LineVSpecial.cs`

## 1) Runtime alanları

`LineVExecutionRuntime` sonuna ekle:

```csharp
public bool DeferChainUntilLineHit;
public Func<ResolutionContext, TileView, TileView, List<BoardAction>> ExecuteSpecialActions;
```

## 2) Execute içindeki eager chain'i koşullu yap

Eski:

```csharp
BuildLineVisuals(rt);
ExecuteQueuedChain(rt);
RemoveDeferredOverrideOriginsFromLineClear(rt);
```

Yeni:

```csharp
BuildLineVisuals(rt);

if (!rt.DeferChainUntilLineHit)
    ExecuteQueuedChain(rt);

RemoveDeferredOverrideOriginsFromLineClear(rt);
```

## 3) BuildClearAction MatchClearAction parametresi

Eski son kısım:

```csharp
perTileClearDelays: ctx.OverrideRadialClearDelays,
isSpecialPhase: true,
presentationPlan: null
```

Yeni:

```csharp
perTileClearDelays: ctx.OverrideRadialClearDelays,
isSpecialPhase: true,
presentationPlan: null,
onLineHitSpecialActions: rt.DeferChainUntilLineHit
    ? cell => ExecuteSpecialAtLineHit(rt, cell)
    : null
```

## 4) Helper ekle

```csharp
private List<BoardAction> ExecuteSpecialAtLineHit(LineVExecutionRuntime rt, Vector2Int cell)
{
    var actions = new List<BoardAction>();

    if (rt == null || rt.Board == null || rt.Context == null)
        return actions;

    if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
        return actions;

    var tile = rt.Board.GetTileViewAt(cell.x, cell.y);
    if (tile == null)
        return actions;

    if (tile == rt.Origin || tile == rt.Partner)
        return actions;

    var special = tile.GetSpecial();
    if (special == TileSpecial.None)
        return actions;

    if (rt.Context.Processed.Contains(cell))
        return actions;

    rt.Context.Queued.Remove(cell);

    if (!rt.Context.ChainExecutionOrder.Contains(cell))
        rt.Context.ChainExecutionOrder.Add(cell);

    rt.Context.Processed.Add(cell);
    rt.Context.Processed.Remove(cell);

    var nestedActions = rt.ExecuteSpecialActions?.Invoke(rt.Context, tile, null);

    rt.Context.Processed.Add(cell);

    if (nestedActions != null && nestedActions.Count > 0)
        actions.AddRange(nestedActions);

    return actions;
}
```
