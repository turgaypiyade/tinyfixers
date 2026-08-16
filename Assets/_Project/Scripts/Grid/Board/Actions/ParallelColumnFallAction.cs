using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Faz 7B: Bir cascade pass'inin fall hareketlerini sütun action'larına bölüp paralel oynatır.
//
// Dış sözleşme bilerek Blocking kalır: ResolveBoard match aramaya ancak tüm sütun düşüşleri görsel
// olarak bitince devam eder. Böylece 7B'nin ilk dilimi görünür per-column async'i verir ama Faz 8'in
// ReservedFor/TileState altyapısı gelmeden erken match/clear yarışları açılmaz.
public sealed class ParallelColumnFallAction : BoardAction
{
    private readonly List<FallAction> columnFalls;

    public ParallelColumnFallAction(List<FallAction> columnFalls)
    {
        this.columnFalls = columnFalls ?? new List<FallAction>();
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (sequencer == null || columnFalls.Count == 0)
            yield break;

        var board = sequencer.Board;

        int tileCount = 0;
        int maxDist = 0;
        foreach (var fall in columnFalls)
        {
            if (fall == null || !fall.HasMoves)
                continue;

            tileCount += fall.MoveCount;
            maxDist = Mathf.Max(maxDist, fall.GetMaxGridDistanceForSfx());
        }

        if (board != null && tileCount > 0)
            board.PlayTileFallSfx(tileCount, maxDist);

        int inFlight = 0;

        foreach (var fall in columnFalls)
        {
            if (fall == null || !fall.HasMoves)
                continue;

            fall.TryGetSingleTargetColumn(out int column);
            inFlight++;
            sequencer.StartCoroutine(RunColumnFall(fall, sequencer, board, column, () => inFlight--));
        }

        while (inFlight > 0)
            yield return null;

    }

    private static IEnumerator RunColumnFall(
        FallAction fall,
        ActionSequencer sequencer,
        BoardController board,
        int column,
        System.Action onDone)
    {
        if (board != null && column >= 0)
            board.BeginColumnFallVisual(column);

        try
        {
            yield return fall.ExecuteVisuals(sequencer);
        }
        finally
        {
            if (board != null && column >= 0)
                board.EndColumnFallVisual(column);
            onDone?.Invoke();
        }
    }
}
