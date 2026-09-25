using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Faz 1 decoupled-resolve playback koordinatörü. Amaç: normal cascade fall TAM bitmeden,
/// match'i oluşturan taşlar hücrelerine VARINCA (event-driven, timed sync YOK) clear'ı başlatmak
/// (overlap). Böylece referans oyundaki "son taş gride girerken match başlar" hissi elde edilir.
///
/// Tek-blocking-sequencer kısıtı (bkz. Docs/DecoupledResolve_Plan.md §6): ExecuteClearPass
/// clear'ı ActionSequencer'a MatchClearAction olarak enqueue eder ve FallAction Blocking'tir →
/// düşüş oynarken clear kuyruğa girse düşüşün TAMAMI bitene kadar bekler, overlap olmaz. Çözüm:
/// overlap path'inde fall'u sequencer DIŞINDA (detached StartCoroutine) koştururuz; ana sequencer
/// boş kalır, ExecuteClearPass onu clear için kullanır → clear, fall tail'iyle PARALEL oynar.
///
/// KARAR mantığı (hangi match temizlenir, hangi special oluşur/aktive olur) DEĞİŞMEZ; yalnız
/// görsel başlangıç zamanı bağımlılık kuralına bağlanır. Zor yollar (special aktivasyon, obstacle)
/// çağıran tarafta gate ile bugünkü seri yola düşer.
/// </summary>
public class BoardVisualCoordinator
{
    private readonly BoardController board;
    private readonly ActionSequencer sequencer;

    public BoardVisualCoordinator(BoardController board, ActionSequencer sequencer)
    {
        this.board = board;
        this.sequencer = sequencer;
    }

    /// <summary>
    /// Cascade fall action'larını ana sequencer'ın DIŞINDA koşturur; match taşları hücrelerine
    /// vardıklarında (TileView.FallArrived event'i — timed/polling senkron YOK) runClear'ı paralel
    /// başlatır. Kesintisiz motorda clear bitince refill devam eder; eski yolda fall da beklenir.
    /// </summary>
    public IEnumerator PlayFallWithOverlappedClear(
        List<BoardAction> fallActions,
        HashSet<TileView> matchTiles,
        Func<IEnumerator> runClear)
    {
        // Event-driven arrival: yalnız bu fall pass'te hareket eden match taşlarını bekle.
        // Match grubundaki yerleşik taşlar FallArrived atmaz; onları pending'e koymak overlap'i
        // fallDone'a kadar kilitler ve cascade→clear arasında görünen gecikmeyi geri getirir.
        var pending = new HashSet<TileView>();
        var participants = new Dictionary<TileView, (int lifetime, int x, int y)>();
        if (matchTiles != null)
        {
            foreach (var t in matchTiles)
            {
                if (t == null) continue;
                participants[t] = (t.LifetimeVersion, t.X, t.Y);
                if (t.IsPlannedToMoveThisFallPass
                    || (board.UseContinuousFallMotion && t.RuntimeState == TileRuntimeState.Falling))
                {
                    if (board.UseContinuousFallMotion && (t.HasArrivedForPlannedFall
                        || board.IsTileReadyForContinuousMatch(t))) continue;
                    pending.Add(t);
                }
            }
        }

        bool IsOriginalMatch()
        {
            foreach (var pair in participants)
            {
                var t = pair.Key;
                var original = pair.Value;
                if (t == null || !t.IsCurrentLifetime(original.lifetime)
                    || t.X != original.x || t.Y != original.y
                    || board.GetTileViewAt(original.x, original.y) != t)
                    return false;
            }
            return true;
        }

        Action<TileView> onArrived = null;
        onArrived = (tile) => { pending.Remove(tile); };
        var subscribed = new List<TileView>(pending);
        foreach (var t in subscribed)
            t.FallArrived += onArrived;

        bool fallDone = false;
        try
        {
            board.StartCoroutine(RunActionsDetached(fallActions, () => fallDone = true));

            while (board.UseContinuousFallMotion
                ? pending.Count > 0
                : !fallDone && (pending.Count > 0 || subscribed.Count == 0))
            {
                // An earlier pass may still own a pending match tile after THIS fall action
                // finishes. Wait for its arrival too. Events are a fast path, not the only
                // completion signal: a replacement movement can settle without emitting one.
                // Queued/active/parked motions and off-grid tiles cannot pass the fallback.
                if (board.UseContinuousFallMotion)
                {
                    // A pooled/replaced participant invalidates this match. Return to the
                    // resolver to find current groups, rather than clearing its replacement.
                    if (!IsOriginalMatch()) yield break;
                    pending.RemoveWhere(t => t.HasArrivedForPlannedFall
                        || board.IsTileReadyForContinuousMatch(t));
                }
                if (board.UseContinuousFallMotion && pending.Count == 0) break;
                yield return null;
            }
        }
        finally
        {
            foreach (var t in subscribed)
                if (t != null && t) t.FallArrived -= onArrived;
        }

        if (board.UseContinuousFallMotion && !IsOriginalMatch()) yield break;

        bool clearDone = false;
        board.StartCoroutine(Wrap(runClear != null ? runClear() : null, () => clearDone = true));

        // Once the clear opens space, let ResolveBoard plan the next fall immediately.
        // The detached tail retains a job handle so level-end cannot overtake it.
        while (!clearDone || (!board.UseContinuousFallMotion && !fallDone))
            yield return null;
    }

    // ActionSequencer.PlaySequence'in blocking semantiğini taklit eder ama ANA kuyruktan
    // bağımsız (detached) koşar → resolve loop bunu beklerken sequencer clear için serbest kalır.
    private IEnumerator RunActionsDetached(List<BoardAction> actions, Action onDone)
    {
        using var fallJob = board.UseContinuousFallMotion
            ? board.BeginJob(BoardController.BoardJobKind.DetachedFall) : null;
        try
        {
            if (actions != null)
            {
                foreach (var a in actions)
                {
                    if (a == null) continue;
                    if (a.Blocking)
                        yield return board.StartCoroutine(a.ExecuteVisuals(sequencer));
                    else
                        board.StartCoroutine(a.ExecuteVisuals(sequencer));
                }
            }
        }
        finally { onDone?.Invoke(); }
    }

    // Coroutine'i istisna-güvenli adımlar; bitince onDone çağırır (RunTogether deseni).
    private IEnumerator Wrap(IEnumerator inner, Action onDone)
    {
        if (inner != null)
        {
            while (true)
            {
                bool hasNext;
                try { hasNext = inner.MoveNext(); }
                catch (Exception ex) { Debug.LogException(ex); break; }
                if (!hasNext) break;
                yield return inner.Current;
            }
        }
        onDone?.Invoke();
    }
}
