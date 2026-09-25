using System;
using System.Collections;
using System.Collections.Generic;

public partial class CascadeLogic
{
    private int activeFallVisuals;
    private bool deferredCascadeRunning;

    // Counts complete FallActions, including their staggered starts. Tile runtime
    // state alone misses queued moves that have not started moving yet.
    internal bool IsGravityBusy => activeFallVisuals > 0 || deferredCascadeRunning || board.HasContinuousFallWork;

    internal IDisposable BeginFallVisual() => new FallVisualScope(this);

    // Kesintisiz düşüşte havadaki taş güvenle yeniden hedeflenir (ışınlanma/çift sürüş yok), bu yüzden
    // yeni boşluk "tüm düşüşler bitsin" diye beklemeden HEMEN planlanır. Eski yolda bu kapı şart:
    // eski coroutine hareketi yeniden hedeflenen taşı başlangıç hücresine ışınlıyordu.
    internal bool CanPlanGravityNow => !IsGravityBusy || board.UseContinuousFallMotion;

    public List<BoardAction> CalculateCascades()
    {
        if (CanPlanGravityNow)
            return CalculateCascadesNow();

        // All callers (Line, PatchBot arrivals, combos and ResolveBoard) share this
        // gate. Do not calculate destinations now and merely delay their visuals:
        // by execution time new impacts may have changed the board again.
        BoardMotionDiagnostics.Event(board, "GRAVITY_DEFERRED",
            $"activeFalls={activeFallVisuals} deferredRunning={deferredCascadeRunning}");
        return new List<BoardAction> { new DeferredCascadeAction(this) };
    }

    private sealed class FallVisualScope : IDisposable
    {
        private CascadeLogic owner;

        public FallVisualScope(CascadeLogic owner)
        {
            this.owner = owner;
            owner.activeFallVisuals++;
        }

        public void Dispose()
        {
            if (owner == null) return;
            owner.activeFallVisuals--;
            owner = null;
        }
    }

    private sealed class DeferredCascadeAction : BoardAction
    {
        private readonly CascadeLogic owner;

        public DeferredCascadeAction(CascadeLogic owner) => this.owner = owner;
        public override bool Blocking => true;

        public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
        {
            // Await only gravity ownership, never background-job counts: this
            // action may itself be a background job, and PatchBot flights may
            // legitimately continue while tiles fall.
            while (owner.IsGravityBusy)
                yield return null;

            // Claim before the first yield so simultaneous waiters cannot both
            // calculate against the same unfinished movement, even before a
            // nested FallAction has started its visual coroutine.
            owner.deferredCascadeRunning = true;
            try
            {
                var actions = owner.CalculateCascadesNow();
                foreach (var action in actions)
                    yield return action.ExecuteVisuals(sequencer);
                owner.board.RefreshAllSortingOrders();
            }
            finally
            {
                owner.deferredCascadeRunning = false;
            }
        }
    }
}
