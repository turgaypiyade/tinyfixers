using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OverrideOverrideComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public SpecialVisualService VisualService;
    public SpecialEffectOrchestrator Effects;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;

    // Board'daki karışık special hücrelerini tek-motor (SpecialChainRunner) ile paralel ateşleyen
    // action'ı üretir. Set ise dispatcher zinciri yerine bu kullanılır.
    public Func<List<Vector2Int>, BoardAction> BuildMixedChainForCells;
}

public sealed class OverrideOverrideComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class OverrideOverrideCombo
{
    public OverrideOverrideComboExecutionResult Execute(OverrideOverrideComboExecutionRuntime rt)
    {
        var result = new OverrideOverrideComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        RegisterOrigins(rt);
        var specialCells = CollectAllTargets(rt);
        PreparePresentation(rt);

        if (rt.FinalizeAtEnd)
        {
            if (rt.ProcessFanout != null)
            {
                var fanoutActions = rt.ProcessFanout(rt.Context);
                if (fanoutActions != null && fanoutActions.Count > 0)
                    result.Actions.AddRange(fanoutActions);
            }

            if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
                rt.CleanupImplantedTiles?.Invoke(rt.Context);

            if (rt.Context.OverrideRadialClearDelays != null && rt.Context.OverrideRadialClearDelays.Count > 0)
                rt.FireOverrideOverrideSpecialVisuals?.Invoke(rt.Context.Affected, rt.Context.OverrideRadialClearDelays);

            bool useRunner = specialCells != null && specialCells.Count > 0;

            // Sıra: special'ları anchor'la (clear sırasında düşmesinler) → tüm-board clear (special'lar
            // hariç) → runner special'ları paralel ateşler (boş board'a, obstacle hit'leri uygulanır) →
            // anchor release. Çift-temizleme güvenli (ClearAndDestroyTile idempotent).
            if (useRunner)
                result.Actions.Add(new PendingTriggeredSpecialScopeAction(specialCells, true));

            result.Actions.Add(BuildClearAction(rt.Context));

            if (useRunner)
            {
                result.Actions.Add(rt.BuildMixedChainForCells(specialCells));
                result.Actions.Add(new PendingTriggeredSpecialScopeAction(specialCells, false));
            }
        }

        return result;
    }

    private bool CanExecute(OverrideOverrideComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        return rt.Origin.GetSpecial() == TileSpecial.SystemOverride
            && rt.Partner.GetSpecial() == TileSpecial.SystemOverride;
    }

    private void RegisterOrigins(OverrideOverrideComboExecutionRuntime rt)
    {
        AddAffected(rt, rt.Origin);
        AddAffected(rt, rt.Partner);

        // The two override origins must NOT be re-enqueued as chain specials (they'd re-run
        // OverrideSpecial). Mark only THEM processed; every other covered special stays chainable.
        if (rt.Origin != null)
            rt.Context.Processed.Add(new Vector2Int(rt.Origin.X, rt.Origin.Y));
        if (rt.Partner != null)
            rt.Context.Processed.Add(new Vector2Int(rt.Partner.X, rt.Partner.Y));
    }

    // Board'daki special hücrelerini döner (tek motora verilecek). Factory yoksa null döner →
    // eski dispatcher zinciri (special'lar Affected'ta kalır, ProcessFanout ile chain).
    private List<Vector2Int> CollectAllTargets(OverrideOverrideComboExecutionRuntime rt)
    {
        SpecialCellUtils.AddAllTiles(rt.Context.Affected, rt.Context, rt.Board);

        if (rt.BuildMixedChainForCells == null)
            return null; // fallback: dispatcher zinciri

        // Board'daki special'ları topla → dispatcher zincirinden çıkar (Processed) ve tüm-board
        // clear'ından çıkar (Affected'tan sil). Runner bunları paralel sub-chain ile ateşleyecek.
        // İki override origin'i RegisterOrigins zaten Processed; onları alma.
        var specialCells = new List<Vector2Int>();
        var toRemove = new List<TileView>();
        foreach (var tile in rt.Context.Affected)
        {
            if (tile == null || tile == rt.Origin || tile == rt.Partner) continue;
            if (tile.GetSpecial() == TileSpecial.None) continue;
            specialCells.Add(new Vector2Int(tile.X, tile.Y));
            rt.Context.Processed.Add(new Vector2Int(tile.X, tile.Y));
            toRemove.Add(tile);
        }
        foreach (var tile in toRemove)
            rt.Context.Affected.Remove(tile);

        return specialCells;
    }

    private void PreparePresentation(OverrideOverrideComboExecutionRuntime rt)
    {
        rt.Context.OverrideFanoutOrigin = rt.Origin;
        rt.Context.OverrideFanoutNormalSelectionPulse = false;
        rt.Context.OverrideForceDefaultClearAnim = true;
        rt.Context.OverrideSuppressPerTileClearVfx = false;

        Vector2Int originCell = new Vector2Int(rt.Origin.X, rt.Origin.Y);
        float comboVfxDuration = rt.Effects != null
            ? rt.Effects.PlayOverrideComboVfxAndQueue(TileSpecial.SystemOverride, TileSpecial.SystemOverride, originCell)
            : 0f;

        rt.Context.OverrideVfxDuration = comboVfxDuration;

        float preClearDelay = rt.Board.GetSystemOverrideComboPreClearDuration();
        float waveDuration   = rt.Board.GetSystemOverrideComboWaveDuration();
        float maxDelay       = preClearDelay + waveDuration;

        if (maxDelay <= 0f)
            maxDelay = ResolutionContext.OverrideRadialClearDuration;

        rt.Context.OverrideRadialClearDelays = rt.VisualService != null
            ? rt.VisualService.BuildCenterOutClearDelays(rt.Context.Affected, maxDelay, preClearDelay)
            : null;
    }

    private void AddAffected(OverrideOverrideComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private MatchClearAction BuildClearAction(ResolutionContext ctx)
    {
        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            animationMode: ctx.HasLineActivation && !ctx.OverrideForceDefaultClearAnim
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            impactCells: ctx.ImpactCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: ctx.LightningVisualTargets,
            lightningLineStrikes: ctx.LightningLineStrikes,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null);
    }
}
