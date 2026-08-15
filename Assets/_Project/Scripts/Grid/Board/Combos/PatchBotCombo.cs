using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PatchBotComboExecutionRuntime
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

    // Her PatchBot için solo runtime factory (PatchBotSpecial.Execute → PatchbotDashUI).
    // SpecialResolver tarafından bağlanır; null ise eski OverridePatchBotAirborneGroupAction'a düşer.
    public Func<TileView, PatchBotTargetCoordinator, PatchBotExecutionRuntime> BuildPatchBotRuntime;
}

public sealed class PatchBotComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PatchBotCombo
{
    public PatchBotComboExecutionResult Execute(PatchBotComboExecutionRuntime rt)
    {
        var result = new PatchBotComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var firstPatchBot = rt.Origin.GetSpecial() == TileSpecial.PatchBot ? rt.Origin : rt.Partner;
        var secondPatchBot = firstPatchBot == rt.Origin ? rt.Partner : rt.Origin;

        // Source tiles'ı processed olarak işaretle — cascade/chain tekrar aktive etmesin.
        rt.Context.Processed.Add(new Vector2Int(firstPatchBot.X, firstPatchBot.Y));
        rt.Context.Processed.Add(new Vector2Int(secondPatchBot.X, secondPatchBot.Y));

        RegisterComboTiles(rt, firstPatchBot, secondPatchBot);

        ComboBehaviorEvents.EmitComboTriggered(
            TileSpecial.PatchBot,
            TileSpecial.PatchBot,
            new Vector2Int(firstPatchBot.X, firstPatchBot.Y));

        if (!rt.FinalizeAtEnd)
            return result;

        // BİRLEŞİK AKIŞ: PatchBot+PatchBot combo artık solo PatchBot ile AYNI yolu kullanır.
        // Her iki source PatchBot + 1 bonus (toplam 3 bot) PatchBotSpecial.Execute → PatchbotDashUI
        // ile fırlatılır: yeni uçuş animasyonları (blade spinner/afterimage), shared coordinator ile
        // grup hedef koordinasyonu ve uçuş sırasında canlı yeniden hedefleme. Her bot bağımsız bir
        // solo PatchBot'tur (FinalizeAtEnd=true): initialClearAction origin'i temizler + dash'i
        // pompalar, hedefte solo hit + cascade tetikler.
        if (rt.BuildPatchBotRuntime != null)
        {
            var launchCells = new List<Vector2Int>
            {
                new Vector2Int(firstPatchBot.X, firstPatchBot.Y),
                new Vector2Int(secondPatchBot.X, secondPatchBot.Y)
            };

            // Bonus 3. bot: kaynaklara en yakın uygun normal taşı PatchBot'a çevir (combo gücü korunur).
            var bonusCell = ConvertBonusPatchBot(rt, firstPatchBot, secondPatchBot);
            if (bonusCell.HasValue)
                launchCells.Add(bonusCell.Value);

            // SENKRON solo fırlatma: dash'ler hemen enqueue olur, dönen initialClearAction'lar
            // (pompa) sequencer'a girer → İLK MatchClearAction çalışınca BoardAnimator TÜM dash
            // buffer'ını tek PlayDashParallel ile tüketir → 3 bot neredeyse aynı anda uçar.
            var launchActions = PatchBotSpecial.LaunchGroupSolo(rt.Board, launchCells, rt.BuildPatchBotRuntime);
            if (launchActions != null && launchActions.Count > 0)
                result.Actions.AddRange(launchActions);
        }
        else
        {
            // Fallback (factory bağlanmamışsa): eski toplu senkron dalış.
            result.Actions.Add(new OverridePatchBotAirborneGroupAction(
                rt.Board,
                new List<Vector2Int>
                {
                    new Vector2Int(firstPatchBot.X, firstPatchBot.Y),
                    new Vector2Int(secondPatchBot.X, secondPatchBot.Y)
                },
                bonusPhantomBots: 1));
        }

        if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
            rt.CleanupImplantedTiles?.Invoke(rt.Context);

        return result;
    }

    // Combo'nun 3. (bonus) botu için: iki kaynağın orta noktasına en yakın UYGUN normal taşı
    // (special yok, obstacle yok, hedeflenebilir) PatchBot'a çevirir. Override'ın taş dönüşümüyle
    // aynı desen (SetSpecial + SyncAfterSpecialChange). Uygun taş yoksa null → bonus atlanır.
    private Vector2Int? ConvertBonusPatchBot(
        PatchBotComboExecutionRuntime rt,
        TileView a,
        TileView b)
    {
        var board = rt.Board;
        var obstacleService = board.ObstacleStateService;
        Vector2 mid = new Vector2((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);

        TileView best = null;
        int bestX = -1, bestY = -1;
        float bestDist = float.MaxValue;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y])
                    continue;
                if (obstacleService != null && obstacleService.GetObstacleIdAt(x, y) != ObstacleId.None)
                    continue;
                if (obstacleService != null && obstacleService.IsMovableObstacleAt(x, y))
                    continue;

                var tile = board.Tiles[x, y];
                if (tile == null || tile == a || tile == b)
                    continue;
                if (tile.GetSpecial() != TileSpecial.None)
                    continue;
                if (board.GridData[x, y] == null)
                    continue;
                if (!SpecialUtils.CanTargetTileContent(board, x, y))
                    continue;

                float d = Mathf.Abs(x - mid.x) + Mathf.Abs(y - mid.y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = tile;
                    bestX = x;
                    bestY = y;
                }
            }
        }

        if (best == null)
            return null;

        best.SetSpecial(TileSpecial.PatchBot);
        SpecialCellUtils.SyncAfterSpecialChange(board, best);
        return new Vector2Int(bestX, bestY);
    }

    private bool CanExecute(PatchBotComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.PatchbotService == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        return rt.Origin.GetSpecial() == TileSpecial.PatchBot &&
               rt.Partner.GetSpecial() == TileSpecial.PatchBot;
    }

    private void RegisterComboTiles(
        PatchBotComboExecutionRuntime rt,
        TileView a,
        TileView b)
    {
        if (a != null)
        {
            rt.Context.Affected.Add(a);
            SpecialCellUtils.MarkAffectedCell(rt.Context, a, rt.Board);
        }

        if (b != null)
        {
            rt.Context.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(rt.Context, b, rt.Board);
        }
    }
}
