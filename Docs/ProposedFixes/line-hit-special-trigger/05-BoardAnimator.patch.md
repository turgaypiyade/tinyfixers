# BoardAnimator hit-based chain patch

Path: `Assets/_Project/Scripts/Grid/Board/BoardAnimator.cs`

## 1) ClearMatchesAnimated signature sonuna parametre ekle

Eski son parametre:

```csharp
Dictionary<TileView, float> perTileClearDelays = null)
```

Yeni:

```csharp
Dictionary<TileView, float> perTileClearDelays = null,
Func<Vector2Int, List<BoardAction>> onLineHitSpecialActions = null,
ActionSequencer sequencer = null)
```

`BoardAnimator.cs` zaten `using System;` içeriyor.

## 2) Local helper ekle

`bool lineHitWindowOpen = false;` satırının hemen altına ekle:

```csharp
int runningLineHitActions = 0;

IEnumerator RunLineHitAction(BoardAction action)
{
    if (action == null || sequencer == null)
        yield break;

    runningLineHitActions++;

    yield return action.ExecuteVisuals(sequencer);

    runningLineHitActions--;
}

void StartLineHitActions(List<BoardAction> actions)
{
    if (actions == null || actions.Count == 0 || sequencer == null)
        return;

    foreach (var action in actions)
    {
        if (action != null)
            board.StartCoroutine(RunLineHitAction(action));
    }
}
```

## 3) TryClearTileOnLineSweepHit içinde special hit'i önce çalıştır

Method içindeki mevcut final kısmı bul:

```csharp
lineHitClearedTiles.Add(tileAtCell);
FinalizeTileClear(tileAtCell);
```

Bunun yerine şunu koy:

```csharp
if (tileAtCell.GetSpecial() != TileSpecial.None && onLineHitSpecialActions != null)
{
    lineHitClearedTiles.Add(tileAtCell);

    var nestedActions = onLineHitSpecialActions.Invoke(cell);
    StartLineHitActions(nestedActions);

    return;
}

lineHitClearedTiles.Add(tileAtCell);
FinalizeTileClear(tileAtCell);
```

## 4) Lightning bitişinden sonra nested actions'ları bekle

Şu bloktan hemen sonra:

```csharp
if (lightningDuration > 0f)
{
    var __w = Wait(lightningDuration);
    if (__w != null) yield return __w;
}
```

şunu ekle:

```csharp
while (runningLineHitActions > 0)
    yield return null;
```
