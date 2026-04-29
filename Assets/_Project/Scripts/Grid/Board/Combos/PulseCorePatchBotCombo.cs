using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PulseCorePatchBotComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public PatchbotComboService PatchbotService;
    public SpecialVisualService VisualService;
    public SpecialEffectOrchestrator Effects;

    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
    public Action<SpecialBoardSignal> EmitBoardSignal;

    public Action<ResolutionContext> EnqueueChainSpecials;
    public Action<ResolutionContext> ProcessQueue;
}

public sealed class PulseCorePatchBotComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PulseCorePatchBotCombo
{
    private readonly int affectedCellCount;
    private readonly PulseCoreSpecial pulseCoreSpecial;

    public PulseCorePatchBotCombo(int affectedCellCount = 9)
    {
        this.affectedCellCount = Mathf.Max(1, affectedCellCount);
        pulseCoreSpecial = new PulseCoreSpecial(this.affectedCellCount);
    }

    public PulseCorePatchBotComboExecutionResult Execute(PulseCorePatchBotComboExecutionRuntime rt)
    {
        var result = new PulseCorePatchBotComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var patchBotTile = GetPatchBotTile(rt);
        var pulseTile = GetPulseTile(rt);

        RegisterComboTiles(rt, patchBotTile, pulseTile);

        var target = rt.PatchbotService.FindTarget(patchBotTile, pulseTile, null);
        if (!target.hasCell)
            return result;

        int initialTx = target.x;
        int initialTy = target.y;

        // Hedef tile'a referans tut. Dash uçarken board değişebilir;
        // varış anında bu referans üzerinden hedef revalidate edilecek.
        TileView trackedTargetTile = target.tile;

        rt.VisualService.PlayTeleportMarkers(patchBotTile, initialTx, initialTy);
        rt.VisualService.PlayTeleportMarkers(pulseTile, initialTx, initialTy);

        rt.Context.Affected.Add(patchBotTile);
        rt.Context.Affected.Add(pulseTile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, patchBotTile, rt.Board);
        SpecialCellUtils.MarkAffectedCell(rt.Context, pulseTile, rt.Board);

        if (rt.FinalizeAtEnd)
        {
            var initialClearAction = new MatchClearAction(
                new HashSet<TileView> { patchBotTile, pulseTile },
                doShake: false,
                animationMode: ClearAnimationMode.Default,
                isSpecialPhase: true
            );
            result.Actions.Add(initialClearAction);
        }

        // Closure capture'ları.
        var capturedRt = rt;
        var capturedPatchBotTile = patchBotTile;
        var capturedPulseTile = pulseTile;
        var capturedExcluded = new HashSet<TileView> { patchBotTile, pulseTile };

        rt.PatchbotService.EnqueueDash(patchBotTile, initialTx, initialTy, pulseTile, null, () =>
        {
            // ── ARRIVAL ─────────────────────────────────────────────────────────────
            // Burada dash hedefe varmış durumda. Board bu süre içinde değişmiş olabilir:
            //   - Hedef hücredeki taş düşmüş olabilir
            //   - Hedef hücreye başka bir taş gelmiş olabilir
            //   - Hedef tile destroy edilmiş olabilir
            // Bu yüzden hedefi yeniden doğrula ve gerekirse en güncel hedefi seç.

            ResolveFinalTarget(
                capturedRt,
                trackedTargetTile,
                capturedPatchBotTile,
                capturedPulseTile,
                capturedExcluded,
                initialTx,
                initialTy,
                out int finalTx,
                out int finalTy);

            Debug.Log(
                $"[PulseCorePatchBotCombo] ARRIVAL initialTarget=({initialTx},{initialTy}) " +
                $"finalTarget=({finalTx},{finalTy}) " +
                $"trackedAlive={(trackedTargetTile != null && trackedTargetTile)}");

            var arrivalCtx = new ResolutionContext();

            var pulseResult = pulseCoreSpecial.ExecuteAtTarget(new PulseCoreExecutionRuntime
            {
                Board = capturedRt.Board,
                Context = arrivalCtx,

                // Kaynak pulse taşı arrival anında artık canlı olmayabilir.
                Origin = capturedPulseTile,
                Partner = capturedPatchBotTile,

                FinalizeAtEnd = true,
                ActivateSpecial = capturedRt.ActivateSpecial,
                ProcessFanout = capturedRt.ProcessFanout,
                CleanupImplantedTiles = capturedRt.CleanupImplantedTiles,
                FireOverrideOverrideSpecialVisuals = capturedRt.FireOverrideOverrideSpecialVisuals,
                EmitBoardSignal = capturedRt.EmitBoardSignal,
                EnqueueChainSpecials = capturedRt.EnqueueChainSpecials,
                ProcessQueue = capturedRt.ProcessQueue,

                SuppressVisualSideEffects = false,
                SkipOriginRegistration = true,
                ForcedOriginSpecial = TileSpecial.PulseCore,
                SignalSourceTile = capturedPulseTile
            }, finalTx, finalTy);

            if (capturedRt.Board != null && pulseResult != null && pulseResult.Actions.Count > 0)
            {
                // Arrival action'ları board'un mevcut sequencer akışına eklenecek.
                // Ancak bunlar bir "background job" olarak işaretlenmeli ki, board bunlar
                // bitmeden ResolveBoard "DONE" demesin. Aksi takdirde SWAP END oluyor ve
                // arkadan PulseCore clear geliyor — sağ taraf boş kalıyor (önceki bug).
                capturedRt.Board.ActiveBackgroundJobs++;
                capturedRt.Board.StartCoroutine(
                    EnqueueArrivalActionsAndRelease(capturedRt.Board, pulseResult.Actions));
            }
        });

        return result;
    }

    /// <summary>
    /// Dash uçarken board değişmiş olabilir. Varış anında hedefi revalidate et.
    /// </summary>
    /// <remarks>
    /// Stratejisi:
    ///   1. Hedef hücrede obstacle varsa → eski hedef geçerli (obstacle düşmez).
    ///   2. Tracked tile hâlâ canlı ve aynı hücredeyse → eski hedef geçerli.
    ///   3. Tracked tile canlı ama farklı hücredeyse → yakın mesafeyse takip et,
    ///      uzaksa eski hedefte bırak (görsel tutarlılık için).
    ///   4. Tracked tile destroy edilmişse:
    ///      a) Eski hücrede yeni bir taş varsa onu hedefle.
    ///      b) Yoksa yeni hedef seç.
    /// </remarks>
    private static void ResolveFinalTarget(
        PulseCorePatchBotComboExecutionRuntime rt,
        TileView trackedTargetTile,
        TileView patchBotTile,
        TileView pulseTile,
        HashSet<TileView> excluded,
        int initialX,
        int initialY,
        out int finalX,
        out int finalY)
    {
        finalX = initialX;
        finalY = initialY;

        if (rt == null || rt.Board == null)
            return;

        // 1) Hedef hücrede hâlâ obstacle var mı? Obstacle'lar düşmez,
        //    o yüzden bunlar için retarget gereksiz.
        if (rt.PatchbotService != null && rt.PatchbotService.HasObstacleAt(initialX, initialY))
            return;

        // Board boyutlarını kontrol et.
        bool initialCellInBounds =
            initialX >= 0 && initialX < rt.Board.Width &&
            initialY >= 0 && initialY < rt.Board.Height;

        // 2) Tracked tile referansı hâlâ canlı mı?
        bool trackedAlive = trackedTargetTile != null && trackedTargetTile;

        if (trackedAlive)
        {
            // Tile hâlâ aynı hücrede mi?
            if (trackedTargetTile.X == initialX && trackedTargetTile.Y == initialY)
            {
                // Hedef hâlâ aynı yerde — eski hedef geçerli.
                return;
            }

            // Tile başka hücreye kaymış (cascade/diagonal). Onu takip et,
            // ama mesafe çok büyük değilse. Çok büyük mesafelerde görsel
            // tutarsızlık olur (PatchBot başka yere uçtu, etki başka yerde patladı).
            float dx = trackedTargetTile.X - initialX;
            float dy = trackedTargetTile.Y - initialY;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);

            const float maxRetargetDistance = 4f;
            if (distance <= maxRetargetDistance)
            {
                finalX = trackedTargetTile.X;
                finalY = trackedTargetTile.Y;
                return;
            }

            // Tile çok uzağa gitmiş — ilk hedefte çalıştırmak daha güvenli.
            return;
        }

        // 3) Tracked tile tamamen yok. Şu seçenekler:
        //    a) Hedef hücrede şu an başka bir tile varsa onu hedefle (yeni taş düşmüş).
        //    b) Yoksa yeni hedef seç.
        if (initialCellInBounds)
        {
            var currentTileAtCell = rt.Board.Tiles[initialX, initialY];
            if (currentTileAtCell != null && currentTileAtCell)
            {
                // Hücrede yeni bir taş var — eski hedef koordinatları zaten kullanılabilir.
                return;
            }
        }

        // 4) Yeni hedef seç. excluded set'i orijinal kombo taşlarını içeriyor;
        //    onları tekrar seçmeyiz (zaten yok edildiler).
        if (rt.PatchbotService != null)
        {
            var freshTarget = rt.PatchbotService.FindTarget(patchBotTile, pulseTile, excluded);
            if (freshTarget.hasCell)
            {
                finalX = freshTarget.x;
                finalY = freshTarget.y;
            }
        }
    }

    /// <summary>
    /// PulseCore arrival action'larını sequencer'a ekler ve bitmesini bekler.
    /// Bu süre boyunca ActiveBackgroundJobs yüksek tutulur, böylece ResolveBoard
    /// idle'a düşmez ve "SWAP END" sonrası arkadan clear gelmesi engellenir.
    /// </summary>
    private static IEnumerator EnqueueArrivalActionsAndRelease(
        BoardController board,
        List<BoardAction> actions)
    {
        if (board == null)
            yield break;

        try
        {
            var sequencer = board.GetComponent<ActionSequencer>();

            // Mevcut sequencer akışı bitsin (idle olsun) — fall/cascade hâlâ oynuyor olabilir.
            // Bu sayede arrival clear, board'un mevcut işiyle çakışmaz.
            // Maksimum bekleme süresi (deadlock koruması).
            const float maxWaitSeconds = 5f;
            float waited = 0f;

            while (sequencer != null && sequencer.IsPlaying && waited < maxWaitSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (board == null)
                yield break;

            sequencer = board.GetComponent<ActionSequencer>();
            if (sequencer == null || actions == null || actions.Count == 0)
                yield break;

            sequencer.Enqueue(actions);

            // Sequencer'ın çalışmaya başlaması için bir frame bekle.
            yield return null;

            // Action'ların bitmesini bekle.
            while (sequencer != null && sequencer.IsPlaying)
                yield return null;
        }
        finally
        {
            if (board != null)
                board.ActiveBackgroundJobs = Mathf.Max(0, board.ActiveBackgroundJobs - 1);
        }
    }

    private bool CanExecute(PulseCorePatchBotComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        bool originIsPatchBot = rt.Origin.GetSpecial() == TileSpecial.PatchBot;
        bool partnerIsPatchBot = rt.Partner.GetSpecial() == TileSpecial.PatchBot;
        bool originIsPulse = rt.Origin.GetSpecial() == TileSpecial.PulseCore;
        bool partnerIsPulse = rt.Partner.GetSpecial() == TileSpecial.PulseCore;

        return (originIsPatchBot && partnerIsPulse) || (partnerIsPatchBot && originIsPulse);
    }

    private void RegisterComboTiles(PulseCorePatchBotComboExecutionRuntime rt, TileView patchBotTile, TileView pulseTile)
    {
        AddPatchBotOrigin(rt, patchBotTile);
        AddPulseReference(rt, pulseTile);
    }

    private void AddPatchBotOrigin(PulseCorePatchBotComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private void AddPulseReference(PulseCorePatchBotComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private TileView GetPatchBotTile(PulseCorePatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.PatchBot ? rt.Origin : rt.Partner;
    }

    private TileView GetPulseTile(PulseCorePatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.PulseCore ? rt.Origin : rt.Partner;
    }
}