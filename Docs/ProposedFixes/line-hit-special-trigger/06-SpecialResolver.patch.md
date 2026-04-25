# SpecialResolver hit-based chain patch

Path: `Assets/_Project/Scripts/Grid/Board/Resolver/SpecialResolver.cs`

LineH/LineV'nin normal solo/swap aktivasyonları da event-based chain timing kullanmalı.

Aşağıdaki runtime initializer'lara ekle:

```csharp
DeferChainUntilLineHit = true,
ExecuteSpecialActions = (resolution, tile, partner) =>
    ExecuteSpecialActions(resolution, tile, partner),
```

## Eklenecek yerler

- `ResolveSpecialSwap` içinde `originalSpecial == TileSpecial.LineV`
- `ResolveSpecialSwap` içinde `originalSpecial == TileSpecial.LineH`
- `ResolveSpecialSolo` içinde `spec == TileSpecial.LineV`
- `ResolveSpecialSolo` içinde `spec == TileSpecial.LineH`

## Örnek LineH

```csharp
var result = lineHSpecial.Execute(new LineHExecutionRuntime
{
    Board = board,
    Context = ctx,
    Origin = specialTile,
    Partner = null,
    FinalizeAtEnd = true,
    ActivateSpecial = dispatcher.ApplySpecialActivation,
    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
    FireOverrideOverrideSpecialVisuals = (affected, delays) =>
        visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
    EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
    ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution),

    DeferChainUntilLineHit = true,
    ExecuteSpecialActions = (resolution, tile, partner) =>
        ExecuteSpecialActions(resolution, tile, partner)
});
```

## Örnek LineV

```csharp
var result = lineVSpecial.Execute(new LineVExecutionRuntime
{
    Board = board,
    Context = ctx,
    Origin = specialTile,
    Partner = null,
    FinalizeAtEnd = true,
    ActivateSpecial = dispatcher.ApplySpecialActivation,
    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
    FireOverrideOverrideSpecialVisuals = (affected, delays) =>
        visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
    EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
    ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution),

    DeferChainUntilLineHit = true,
    ExecuteSpecialActions = (resolution, tile, partner) =>
        ExecuteSpecialActions(resolution, tile, partner)
});
```
