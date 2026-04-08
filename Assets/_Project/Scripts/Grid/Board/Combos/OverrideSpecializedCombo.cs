using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OverrideSpecializedComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public Action<ResolutionContext, TileView, TileView> EnqueueActivation;
    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;

    public Action<ResolutionContext> EnqueueChainSpecials;
    public Action<ResolutionContext> ProcessQueue;
}

public sealed class OverrideSpecializedComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class OverrideSpecializedCombo
{
    public OverrideSpecializedComboExecutionResult Execute(OverrideSpecializedComboExecutionRuntime rt)
    {
        var result = new OverrideSpecializedComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var overrideTile = rt.Origin.GetSpecial() == TileSpecial.SystemOverride ? rt.Origin : rt.Partner;
        var otherTile = overrideTile == rt.Origin ? rt.Partner : rt.Origin;
        var targetSpecial = otherTile.GetSpecial();

        AddOrigin(rt, overrideTile);
        AddOrigin(rt, otherTile);

        PrepareFanout(rt, overrideTile, targetSpecial);
        var deferredSpecials = CollectTargets(rt, overrideTile, otherTile, targetSpecial);

        if (rt.FinalizeAtEnd)
        {
            if (rt.ProcessFanout != null)
            {
                var fanoutActions = rt.ProcessFanout(rt.Context);
                if (fanoutActions != null && fanoutActions.Count > 0)
                    result.Actions.AddRange(fanoutActions);
            }

            // Fanout bitti — aynı renk mevcut special'ları aktive et
            if (deferredSpecials != null && deferredSpecials.Count > 0)
            {
                foreach (var cell in deferredSpecials)
                    rt.Context.Processed.Remove(cell);

                rt.EnqueueChainSpecials?.Invoke(rt.Context);
                rt.ProcessQueue?.Invoke(rt.Context);
            }

            // İmplant edilen PulseCore'ları sırayla tetikle.
            // PulseCoreSpecial.ExecuteQueuedChain etrafındaki special'ları
            // (LineH, LineV, Override vs.) kuyruğa alıp zincirleme tetikler.
            // Processed seti recursive döngüyü önler.
            // VFX bastırılıyor — görsel patlama SystemOverrideFanoutPlacementAction
            // tarafından doğru zamanda (lightningbeam sonrası) oynatılacak.
            if (targetSpecial == TileSpecial.PulseCore && rt.ActivateSpecial != null)
            {
                rt.Context.SuppressOverridePulseSelectionVfx = true;

                foreach (var cell in rt.Context.OverrideDeferredPulseExplosions)
                {
                    if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
                        continue;

                    var tile = rt.Board.Tiles[cell.x, cell.y];
                    if (tile == null || tile.GetSpecial() != TileSpecial.PulseCore)
                        continue;

                    rt.ActivateSpecial(rt.Context, tile, null);
                }

                rt.Context.SuppressOverridePulseSelectionVfx = false;
            }

            if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
                rt.CleanupImplantedTiles?.Invoke(rt.Context);

            if (rt.Context.OverrideRadialClearDelays != null && rt.Context.OverrideRadialClearDelays.Count > 0)
                rt.FireOverrideOverrideSpecialVisuals?.Invoke(rt.Context.Affected, rt.Context.OverrideRadialClearDelays);

            result.Actions.Add(BuildClearAction(rt.Context, targetSpecial));
        }

        return result;
    }

    private bool CanExecute(OverrideSpecializedComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        bool hasOverride = rt.Origin.GetSpecial() == TileSpecial.SystemOverride
            || rt.Partner.GetSpecial() == TileSpecial.SystemOverride;

        if (!hasOverride)
            return false;

        var targetSpecial = rt.Origin.GetSpecial() == TileSpecial.SystemOverride
            ? rt.Partner.GetSpecial()
            : rt.Origin.GetSpecial();

        return targetSpecial == TileSpecial.LineH
            || targetSpecial == TileSpecial.LineV
            || targetSpecial == TileSpecial.PulseCore
            || targetSpecial == TileSpecial.PatchBot;
    }

    private void PrepareFanout(OverrideSpecializedComboExecutionRuntime rt, TileView overrideTile, TileSpecial targetSpecial)
    {
        rt.Context.OverrideFanoutOrigin = overrideTile;
        rt.Context.OverrideForceDefaultClearAnim = !(targetSpecial == TileSpecial.LineH || targetSpecial == TileSpecial.LineV);
        rt.Context.OverrideSuppressPerTileClearVfx = false;
        rt.Context.OverrideFanoutNormalSelectionPulse = false;

        SystemOverrideBehaviorEvents.EmitOverrideFanoutStarted(
            new Vector2Int(overrideTile.X, overrideTile.Y),
            targetSpecial);
    }

    private List<Vector2Int> CollectTargets(OverrideSpecializedComboExecutionRuntime rt, TileView overrideTile, TileView otherTile, TileSpecial targetSpecial)
    {
        TileType baseType = otherTile.GetTileType();
        List<Vector2Int> deferredSpecialCells = null;

        for (int x = 0; x < rt.Board.Width; x++)
        {
            for (int y = 0; y < rt.Board.Height; y++)
            {
                if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
                    continue;

                var tile = rt.Board.Tiles[x, y];
                if (tile == null || !tile.GetTileType().Equals(baseType))
                    continue;

                // Origin ve Partner zaten AddOrigin'de eklendi
                if (tile == overrideTile || tile == otherTile)
                    continue;

                if (tile.GetSpecial() != TileSpecial.None)
                {
                    // Mevcut special tile'lar: Affected'a ekle, Processed'a da ekle
                    // ki fanout sırasında erken aktive olmasınlar.
                    // Fanout bittikten sonra Processed'dan çıkarılıp
                    // EnqueueChainSpecials ile doğal sırayla aktive edilecekler.
                    var cell = new Vector2Int(tile.X, tile.Y);
                    rt.Context.Affected.Add(tile);
                    SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
                    rt.Context.Processed.Add(cell);

                    if (deferredSpecialCells == null)
                        deferredSpecialCells = new List<Vector2Int>();
                    deferredSpecialCells.Add(cell);
                    continue;
                }

                rt.Context.OverrideFanoutTargets.Add(tile);
                rt.Context.PendingOverrideImplants.Add(new ResolutionContext.PendingOverrideImplant(
                    new Vector2Int(tile.X, tile.Y),
                    targetSpecial,
                    new Vector2Int(otherTile.X, otherTile.Y),
                    new Vector2Int(overrideTile.X, overrideTile.Y)));
            }
        }

        return deferredSpecialCells;
    }

    private void AddOrigin(OverrideSpecializedComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private MatchClearAction BuildClearAction(ResolutionContext ctx, TileSpecial targetSpecial)
    {
        // Chain'den gelen line activation varsa lightning sweep animasyonu kullan.
        // OverrideForceDefaultClearAnim sadece override fanout'un kendi clear'ı için geçerli,
        // chain special'lar kendi animasyonlarını kullanmalı.
        bool hasChainLightning = ctx.HasLineActivation
            && ctx.LightningLineStrikes != null
            && ctx.LightningLineStrikes.Count > 0;

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            animationMode: hasChainLightning
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            impactCells: ctx.ImpactCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: hasChainLightning ? ctx.LightningVisualTargets : null,
            lightningLineStrikes: hasChainLightning ? ctx.LightningLineStrikes : null,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null);
    }
}