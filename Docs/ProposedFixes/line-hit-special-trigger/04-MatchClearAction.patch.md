# MatchClearAction hit-based chain patch

Path: `Assets/_Project/Scripts/Grid/Board/Actions/MatchClearAction.cs`

## 1) using ekle

```csharp
using System;
```

## 2) Field ekle

```csharp
private Func<Vector2Int, List<BoardAction>> onLineHitSpecialActions;
```

## 3) Constructor sonuna parametre ekle

Eski:

```csharp
IReadOnlyList<Vector2Int> impactCells = null,
bool isBlocking = true,
bool enqueueCascadeOnComplete = false)
```

Yeni:

```csharp
IReadOnlyList<Vector2Int> impactCells = null,
bool isBlocking = true,
bool enqueueCascadeOnComplete = false,
Func<Vector2Int, List<BoardAction>> onLineHitSpecialActions = null)
```

## 4) Constructor içinde ata

```csharp
this.onLineHitSpecialActions = onLineHitSpecialActions;
```

## 5) ClearMatchesAnimated çağrısını değiştir

Eski:

```csharp
yield return sequencer.Animator.ClearMatchesAnimated(
    matches, doShake, staggerDelays, staggerAnimTime,
    animationMode, affectedCells, impactCells, obstacleHitContext,
    includeAdjacentOverTileBlockerDamage, lightningOriginTile,
    lightningOriginCell, lightningVisualTargets, lightningLineStrikes,
    suppressPerTileClearVfx, perTileClearDelays);
```

Yeni:

```csharp
yield return sequencer.Animator.ClearMatchesAnimated(
    matches, doShake, staggerDelays, staggerAnimTime,
    animationMode, affectedCells, impactCells, obstacleHitContext,
    includeAdjacentOverTileBlockerDamage, lightningOriginTile,
    lightningOriginCell, lightningVisualTargets, lightningLineStrikes,
    suppressPerTileClearVfx, perTileClearDelays,
    onLineHitSpecialActions,
    sequencer);
```
