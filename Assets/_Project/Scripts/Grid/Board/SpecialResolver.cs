using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpecialResolver
{
    private readonly BoardController board;
    private readonly MatchFinder matchFinder;
    private readonly BoardAnimator boardAnimator;
    private readonly PulseCoreImpactService pulseCoreImpactService;
    private readonly PatchbotComboService patchbotComboService;
    private HashSet<Vector2Int> specialAffectedCells;

    // SystemOverride fan-out lightning (single shot) support
    private TileView overrideFanoutOrigin;
    private readonly List<TileView> overrideFanoutTargets = new();
    private bool overrideForceDefaultClearAnim;
    private bool overrideSuppressPerTileClearVfx;
    private bool overrideFanoutNormalSelectionPulse;
    private int overrideFanoutPulseHitCount;
    private readonly List<PendingOverrideImplant> pendingOverrideImplants = new();
    private Dictionary<TileView, float> overrideOverrideRadialClearDelays;
    private float overrideOverrideVfxDuration;
    private readonly HashSet<TileView> overrideImplantedTiles = new();
    private bool deferOverrideImplantVisualRefresh;
    private const float OverrideOverrideRadialClearDuration = 0.45f;

    private readonly struct PendingOverrideImplant
    {
        public readonly TileView target;
        public readonly TileSpecial special;
        public readonly TileView partnerTile;
        public readonly TileView overrideTile;

        public PendingOverrideImplant(TileView target, TileSpecial special, TileView partnerTile, TileView overrideTile)
        {
            this.target = target;
            this.special = special;
            this.partnerTile = partnerTile;
            this.overrideTile = overrideTile;
        }
    }

    public SpecialResolver(BoardController board, MatchFinder matchFinder, BoardAnimator boardAnimator, PulseCoreImpactService pulseCoreImpactService)
    {
        this.board = board;
        this.matchFinder = matchFinder;
        this.boardAnimator = boardAnimator;
        this.pulseCoreImpactService = pulseCoreImpactService;
        patchbotComboService = new PatchbotComboService(board);
    }

    
    private static void HideTileVisualForCombo(TileView t)
    {
        if (t == null) return;

        // Hide only visually so the clear pipeline can still reference transforms for VFX.
        // CanvasGroup should exist on Tile root; if not, add one in prefab.
        if (!t.TryGetComponent<CanvasGroup>(out var cg))
            cg = t.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

public TileView TryCreateSpecial(HashSet<TileView> matches)
    {
        // 1) Eğer bu tur kullanıcı swap’inin ilk resolve turuysa: eski davranış (A/B'den winner seç)
        if (board.LastSwapUserMove && board.LastSwapA != null && board.LastSwapB != null)
        {
            board.LastSwapUserMove = false;

            TileSpecial aSpec = matchFinder.DecideSpecialAt(board.LastSwapA.X, board.LastSwapA.Y);
            TileSpecial bSpec = matchFinder.DecideSpecialAt(board.LastSwapB.X, board.LastSwapB.Y);

            (TileView winner, TileSpecial wSpec) = PickWinner(board.LastSwapA, aSpec, board.LastSwapB, bSpec);

            if (winner == null || wSpec == TileSpecial.None)
                return null;

            winner.SetSpecial(wSpec);
            if (wSpec == TileSpecial.SystemOverride)
                winner.SetOverrideBaseType(winner.GetTileType());

            // Special tile'ın kendisi patlamasın
            matches.Remove(winner);

            return winner;
        }

        // 2) Cascade turu: Eğer kullanıcı swap'ı değilse (yukarıdan düşenlerden special oluşuyorsa)
        TileView bestTile = null;
        TileSpecial bestSpecial = TileSpecial.None;
        int bestScore = 0;

        foreach (var t in matches)
        {
            TileSpecial s = matchFinder.DecideSpecialAt(t.X, t.Y);
            int score = SpecialScore(s);
            if (score > bestScore)
            {
                bestScore = score;
                bestSpecial = s;
                bestTile = t;
            }
        }

        if (bestTile != null && bestSpecial != TileSpecial.None)
        {
            bestTile.SetSpecial(bestSpecial);
            if (bestSpecial == TileSpecial.SystemOverride)
                bestTile.SetOverrideBaseType(bestTile.GetTileType());

            matches.Remove(bestTile);
            return bestTile;
        }

        return null;
    }

    private void ConsumeSwapSourceVisuals(TileView a, TileView b)
    {
        HideTileVisualForCombo(a);
        HideTileVisualForCombo(b);
    }

    
    public int SpecialScore(TileSpecial s)
    {
        switch (s)
        {
            case TileSpecial.SystemOverride: return 60;
            case TileSpecial.PulseCore: return 50;
            case TileSpecial.LineH:
            case TileSpecial.LineV: return 30;
            case TileSpecial.PatchBot: return 20;
            default: return 0;
        }
    }

    public (TileView winner, TileSpecial spec) PickWinner(TileView a, TileSpecial aSpec, TileView b, TileSpecial bSpec)
    {
        int Score(TileSpecial s)
        {
            switch (s)
            {
                case TileSpecial.SystemOverride: return 60;
                case TileSpecial.PulseCore: return 50;
                case TileSpecial.LineH:
                case TileSpecial.LineV: return 30;
                case TileSpecial.PatchBot: return 20;
                default: return 0;
            }
        }

        int ascore = Score(aSpec);
        int bscore = Score(bSpec);

        if (ascore == 0 && bscore == 0) return (null, TileSpecial.None);
        if (ascore >= bscore) return (a, aSpec);
        return (b, bSpec);
    }

    public List<BoardAction> ResolveSpecialSwap(TileView a, TileView b)
    {
        var actions = new List<BoardAction>();
        board.ShakeNextClear = true;
        board.LastSwapUserMove = false;
        board.IsSpecialActivationPhase = true;

        TileSpecial sa = a.GetSpecial();
        TileSpecial sb = b.GetSpecial();

        bool saIsLine  = sa == TileSpecial.LineH || sa == TileSpecial.LineV;
        bool sbIsLine  = sb == TileSpecial.LineH || sb == TileSpecial.LineV;
        bool saIsPulse = sa == TileSpecial.PulseCore;
        bool sbIsPulse = sb == TileSpecial.PulseCore;

        // ── Normal partner'ı sadece combo veya PatchBot durumunda tüket.
        //    Diğer special + normal swap'larda normal taş yerinde kalır
        //    (patlama alanındaysa doğal olarak temizlenir).
        bool bothSpecial = sa != TileSpecial.None && sb != TileSpecial.None;
        bool anyPatchBot = sa == TileSpecial.PatchBot || sb == TileSpecial.PatchBot;
        bool consumeNormalPartner = bothSpecial || anyPatchBot;

        if (consumeNormalPartner)
        {
            // Override tiles stay visible during fan-out; hide only partners.
            // Exception: Override+Override hides both (combo VFX takes over).
            bool aIsOvr = sa == TileSpecial.SystemOverride;
            bool bIsOvr = sb == TileSpecial.SystemOverride;
            bool deferOverrideHide = (aIsOvr || bIsOvr) && !(aIsOvr && bIsOvr);
            if (deferOverrideHide)
            {
                if (aIsOvr) HideTileVisualForCombo(b);
                else        HideTileVisualForCombo(a);
            }
            else
            {
                ConsumeSwapSourceVisuals(a, b);
            }
        }
        else
        {
            // Single special + normal partner: hide special unless it's an Override
            var onlySpecial = (sa != TileSpecial.None) ? a : b;
            if (onlySpecial.GetSpecial() != TileSpecial.SystemOverride)
                HideTileVisualForCombo(onlySpecial);
        }

        // ── Tüm state'i sıfırla (her çağrıda, early return dahil) ──
        specialAffectedCells = new HashSet<Vector2Int>();
        overrideFanoutOrigin = null;
        overrideFanoutTargets.Clear();
        overrideForceDefaultClearAnim = false;
        overrideSuppressPerTileClearVfx = false;
        overrideFanoutNormalSelectionPulse = false;
        overrideFanoutPulseHitCount = 0;
        pendingOverrideImplants.Clear();
        overrideOverrideRadialClearDelays = null;
        overrideOverrideVfxDuration = 0f;
        overrideImplantedTiles.Clear();
        deferOverrideImplantVisualRefresh = false;

        // ✅ PULSE + LINE: toplu ClearMatchesAnimated yapma.
        if ((saIsPulse && sbIsLine) || (sbIsPulse && saIsLine))
        {
            var center = saIsPulse ? a : b;
            int cx = center.X;
            int cy = center.Y;

            // Yeni Action sistemine göre datayı anında silip Action objesini oluştur
            var pulseAction = board.CreatePulseEmitterComboAction(cx, cy);
            actions.Add(pulseAction);

            // ── Chain: combo alanındaki special taşları (ör. SystemOverride) tetikle ──
            var chainAffected = new HashSet<TileView>();
            var chainCells = new HashSet<Vector2Int>();
            for (int r = cy - 1; r <= cy + 1; r++)
                for (int c = 0; c < board.Width; c++)
                {
                    if (r < 0 || r >= board.Height) continue;
                    chainCells.Add(new Vector2Int(c, r));
                    if (board.Tiles[c, r] != null) chainAffected.Add(board.Tiles[c, r]);
                }
            for (int c = cx - 1; c <= cx + 1; c++)
                for (int r = 0; r < board.Height; r++)
                {
                    if (c < 0 || c >= board.Width) continue;
                    chainCells.Add(new Vector2Int(c, r));
                    if (board.Tiles[c, r] != null) chainAffected.Add(board.Tiles[c, r]);
                }

            bool chainHasLine, chainHasAny;
            var chainLightningTargets = new HashSet<TileView>();
            var chainLightningStrikes = new List<LightningLineStrike>();
            ExpandSpecialChain(chainAffected, chainCells,
                out chainHasLine, out chainHasAny,
                chainLightningTargets, chainLightningStrikes);

            if (chainHasAny && chainAffected.Count > 0)
            {
                var chainMode = chainHasLine
                    ? ClearAnimationMode.LightningStrike
                    : ClearAnimationMode.Default;
                
                actions.Add(new MatchClearAction(
                    chainAffected,
                    doShake: true,
                    animationMode: chainMode,
                    affectedCells: chainCells,
                    obstacleHitContext: null,
                    includeAdjacentOverTileBlockerDamage: false,
                    lightningOriginTile: null,
                    lightningOriginCell: null,
                    lightningVisualTargets: chainLightningTargets,
                    lightningLineStrikes: chainLightningStrikes,
                    isSpecialPhase: true
                ));
            }

            board.IsSpecialActivationPhase = false;
            specialAffectedCells = null;
            return actions;
        }

        // ── Special taşlar her zaman affected'e eklenir.
        //    Normal partner yalnızca PB veya combo ise eklenir;
        //    aksi halde sadece patlama alanına girerse doğal olarak eklenir.
        var affected = new HashSet<TileView>();
        if (sa != TileSpecial.None) { affected.Add(a); MarkAffectedCell(a); }
        if (sb != TileSpecial.None) { affected.Add(b); MarkAffectedCell(b); }
        if (consumeNormalPartner)
        {
            affected.Add(a); affected.Add(b);
            MarkAffectedCell(a); MarkAffectedCell(b);
        }
        var processed = new HashSet<TileView>();
        bool hasLineActivation = false;
        var lightningVisualTargets = new HashSet<TileView>(); // lightning only for Line path tiles
        var lightningLineStrikes = new List<LightningLineStrike>();
        var queued = new HashSet<TileView>();
        var queue = new Queue<SpecialActivation>();

        bool saIsOverride = sa == TileSpecial.SystemOverride;
        bool sbIsOverride = sb == TileSpecial.SystemOverride;
        // Override+Override tüm board'u temizler → stagger gereksiz, anında yok et
        bool suppressPulseImpactAnimations = (saIsPulse && sbIsPulse) || (saIsOverride && sbIsOverride);
        bool suppressPerTileClearVfx = (saIsPulse && sbIsLine) || (sbIsPulse && saIsLine);

        // Satır/sütun etkisi üreten tüm özel zincirlerde hedefe lightning gidip ardından tile clear olsun.
        hasLineActivation = hasLineActivation || saIsLine || sbIsLine;


        if (sa != TileSpecial.None && sb != TileSpecial.None)
        {
            ApplyComboEffect(affected, queue, queued, processed, a, b, sa, sb, lightningVisualTargets, lightningLineStrikes);
            processed.Add(a);
            processed.Add(b);
        }
        else
        {
            var specialTile = sa != TileSpecial.None ? a : b;
            var partnerTile = sa != TileSpecial.None ? b : a;
            EnqueueActivation(queue, queued, specialTile, partnerTile);
        }

        EnqueueChainSpecials(affected, queue, queued, processed);

        while (queue.Count > 0)
        {
            var activation = queue.Dequeue();
            queued.Remove(activation.special);
            if (activation.special == null || processed.Contains(activation.special)) continue;

            processed.Add(activation.special);
            ApplySpecialActivation(affected, activation.special, activation.partner, ref hasLineActivation, lightningVisualTargets, lightningLineStrikes);
            EnqueueChainSpecials(affected, queue, queued, processed);
        }

        // SystemOverride: show a single fan-out lightning mark to all targets before clearing/activating.
        if (overrideFanoutOrigin != null && overrideFanoutTargets.Count > 0)
        {
            deferOverrideImplantVisualRefresh = true;
            foreach (var t in overrideFanoutTargets)
            {
                ApplyPendingOverrideImplantForTile(affected, queue, queued, t);
            }

            if (pendingOverrideImplants.Count > 0)
                ApplyPendingOverrideImplants(affected, queue, queued);

            actions.Add(new SystemOverrideFanoutVisualAction(board, overrideFanoutOrigin, new List<TileView>(overrideFanoutTargets), overrideFanoutNormalSelectionPulse));
        }
        else if (pendingOverrideImplants.Count > 0)
        {
            ApplyPendingOverrideImplants(affected, queue, queued);
        }

        // ── Issue 6: Fan-out tamamlandı — override taşını artık gizle ──
        if (overrideFanoutOrigin != null)
            HideTileVisualForCombo(overrideFanoutOrigin);

        if (queue.Count > 0)
        {
            EnqueueChainSpecials(affected, queue, queued, processed);

            while (queue.Count > 0)
            {
                var activation = queue.Dequeue();
                queued.Remove(activation.special);
                if (activation.special == null || processed.Contains(activation.special)) continue;

                processed.Add(activation.special);
                ApplySpecialActivation(affected, activation.special, activation.partner, ref hasLineActivation, lightningVisualTargets, lightningLineStrikes);
                EnqueueChainSpecials(affected, queue, queued, processed);
            }
        }

        // ── Issue 3: Override+Override — VFX ile senkron temizleme ──
        //    pendingOverrideOverrideClearDelay artık 0; VFX zaten başladı,
        //    radial clear delay'leri VFX süresiyle eşleştirildi.
        // Override+Override: dalga sırasında yoluna çıkan special'ların görsel efektini tetikle
        if (overrideOverrideRadialClearDelays != null && overrideOverrideRadialClearDelays.Count > 0)
            FireOverrideOverrideSpecialVisuals(affected, overrideOverrideRadialClearDelays);

        Dictionary<TileView, float> stagger = suppressPulseImpactAnimations
            ? null
            : pulseCoreImpactService.BuildStaggerDelays(affected, processed);
        var animationMode = (hasLineActivation && !overrideForceDefaultClearAnim)
            ? ClearAnimationMode.LightningStrike
            : ClearAnimationMode.Default;
        // Special zincirinde yalnızca gerçekten etkilenen hücreler hasar alsın.
        // Komşu over-tile blocker ek hasarı, satır/sütun special'larda yan hücrelerde
        // beklenmeyen stage düşüşüne neden olabiliyor.
        actions.Add(new MatchClearAction(
            affected,
            doShake: true,
            staggerDelays: stagger,
            staggerAnimTime: board.PulseImpactAnimTime,
            animationMode: animationMode,
            affectedCells: specialAffectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: lightningVisualTargets,
            lightningLineStrikes: lightningLineStrikes,
            suppressPerTileClearVfx: (suppressPerTileClearVfx || overrideSuppressPerTileClearVfx),
            perTileClearDelays: overrideOverrideRadialClearDelays,
            isSpecialPhase: true));
            
        board.IsSpecialActivationPhase = false;
        specialAffectedCells = null;
        return actions;
    }

    /// <summary>
    /// Tek bir special taşı bağımsız olarak aktive eder (partner yok, combo yok).
    /// ProcessSwap'ta normal tarafın match'inden yeni bir special oluştuğunda,
    /// eski special'ı combo'ya sokmadan solo çalıştırmak için kullanılır.
    /// Chain mekanizması aktiftir: etki alanındaki special'lar (yeni oluşanlar dahil)
    /// tetiklenir. SystemOverride fan-out, PatchBot dash vb. tam desteklenir.
    /// </summary>
    public List<BoardAction> ResolveSpecialSolo(TileView specialTile)
    {
        var actions = new List<BoardAction>();
        if (specialTile == null) return actions;

        board.ShakeNextClear = true;
        board.LastSwapUserMove = false;
        board.IsSpecialActivationPhase = true;

        // Override taşı fan-out sırasında görünür kalmalı (Issue 6)
        bool deferHide = specialTile.GetSpecial() == TileSpecial.SystemOverride;
        if (!deferHide)
            HideTileVisualForCombo(specialTile);

        // ── Tüm state'i sıfırla (defensive — cross-call kontaminasyon önlemi) ──
        specialAffectedCells = new HashSet<Vector2Int>();
        overrideFanoutOrigin = null;
        overrideFanoutTargets.Clear();
        overrideForceDefaultClearAnim = false;
        overrideSuppressPerTileClearVfx = false;
        overrideFanoutNormalSelectionPulse = false;
        overrideFanoutPulseHitCount = 0;
        pendingOverrideImplants.Clear();
        overrideOverrideRadialClearDelays = null;
        overrideOverrideVfxDuration = 0f;
        overrideImplantedTiles.Clear();
        deferOverrideImplantVisualRefresh = false;

        var affected = new HashSet<TileView> { specialTile };
        MarkAffectedCell(specialTile);

        var processed = new HashSet<TileView>();
        bool hasLineActivation = false;
        var lightningVisualTargets = new HashSet<TileView>();
        var lightningLineStrikes = new List<LightningLineStrike>();
        var queued = new HashSet<TileView>();
        var queue = new Queue<SpecialActivation>();

        TileSpecial spec = specialTile.GetSpecial();
        bool specIsLine = spec == TileSpecial.LineH || spec == TileSpecial.LineV;
        hasLineActivation = specIsLine;

        // Solo activation: partner = null
        EnqueueActivation(queue, queued, specialTile, null);
        EnqueueChainSpecials(affected, queue, queued, processed);

        while (queue.Count > 0)
        {
            var activation = queue.Dequeue();
            queued.Remove(activation.special);
            if (activation.special == null || processed.Contains(activation.special)) continue;

            processed.Add(activation.special);
            ApplySpecialActivation(affected, activation.special, activation.partner,
                ref hasLineActivation, lightningVisualTargets, lightningLineStrikes);
            EnqueueChainSpecials(affected, queue, queued, processed);
        }

        // ── SystemOverride fan-out: lightning → implant → chain ──
        if (overrideFanoutOrigin != null && overrideFanoutTargets.Count > 0)
        {
            deferOverrideImplantVisualRefresh = true;
            foreach (var t in overrideFanoutTargets)
            {
                ApplyPendingOverrideImplantForTile(affected, queue, queued, t);
            }

            if (pendingOverrideImplants.Count > 0)
                ApplyPendingOverrideImplants(affected, queue, queued);

            actions.Add(new SystemOverrideFanoutVisualAction(board, overrideFanoutOrigin, new List<TileView>(overrideFanoutTargets), overrideFanoutNormalSelectionPulse));
        }
        else if (pendingOverrideImplants.Count > 0)
        {
            ApplyPendingOverrideImplants(affected, queue, queued);
        }

        // ── Issue 6: Fan-out tamamlandı — override taşını artık gizle ──
        if (overrideFanoutOrigin != null)
            HideTileVisualForCombo(overrideFanoutOrigin);
        else if (deferHide)
            HideTileVisualForCombo(specialTile);

        // Fan-out'tan eklenen chain special'ları işle
        if (queue.Count > 0)
        {
            EnqueueChainSpecials(affected, queue, queued, processed);
            while (queue.Count > 0)
            {
                var activation = queue.Dequeue();
                queued.Remove(activation.special);
                if (activation.special == null || processed.Contains(activation.special)) continue;
                processed.Add(activation.special);
                ApplySpecialActivation(affected, activation.special, activation.partner,
                    ref hasLineActivation, lightningVisualTargets, lightningLineStrikes);
                EnqueueChainSpecials(affected, queue, queued, processed);
            }
        }

        // ── Issue 7: implanted special görsellerini temizle ──
        if (overrideImplantedTiles.Count > 0)
        {
            foreach (var tile in overrideImplantedTiles)
            {
                if (tile != null && tile.GetSpecial() != TileSpecial.None)
                    tile.SetSpecial(TileSpecial.None);
            }
            overrideImplantedTiles.Clear();
        }

        Dictionary<TileView, float> stagger =
            pulseCoreImpactService.BuildStaggerDelays(affected, processed);

        var animationMode = (hasLineActivation && !overrideForceDefaultClearAnim)
            ? ClearAnimationMode.LightningStrike
            : ClearAnimationMode.Default;

        actions.Add(new MatchClearAction(
            affected,
            doShake: true,
            staggerDelays: stagger,
            staggerAnimTime: board.PulseImpactAnimTime,
            animationMode: animationMode,
            affectedCells: specialAffectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: lightningVisualTargets,
            lightningLineStrikes: lightningLineStrikes,
            suppressPerTileClearVfx: overrideSuppressPerTileClearVfx,
            perTileClearDelays: overrideOverrideRadialClearDelays,
            isSpecialPhase: true));

        board.IsSpecialActivationPhase = false;
        specialAffectedCells = null;
        return actions;
    }
    public void ExpandSpecialChain(
        HashSet<TileView> affected,
        HashSet<Vector2Int> affectedCells,
        out bool hasLineActivation,
        out bool hasAnySpecialActivation,
        HashSet<TileView> lightningVisualTargets = null,
        List<LightningLineStrike> lightningLineStrikes = null)

    {
        hasLineActivation = false;
        hasAnySpecialActivation = false;
        if (affected == null || affected.Count == 0)
            return;

        bool previousSpecialPhase = board.IsSpecialActivationPhase;
        var previousAffectedCells = specialAffectedCells;

        board.IsSpecialActivationPhase = true;
        specialAffectedCells = affectedCells ?? new HashSet<Vector2Int>();

        var processed = new HashSet<TileView>();
        var queued = new HashSet<TileView>();
        var queue = new Queue<SpecialActivation>();

        if (affectedCells != null)
        {
            foreach (var cell in affectedCells)
            {
                if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) continue;
                var tileAtCell = board.Tiles[cell.x, cell.y];
                if (tileAtCell == null) continue;
                affected.Add(tileAtCell);
            }
        }

        foreach (var tile in affected)
        {
            if (tile == null) continue;
            MarkAffectedCell(tile);
            if (tile.GetSpecial() == TileSpecial.None) continue;
            EnqueueActivation(queue, queued, tile, null);
        }

        while (queue.Count > 0)
        {
            var activation = queue.Dequeue();
            queued.Remove(activation.special);
            if (activation.special == null || processed.Contains(activation.special)) continue;

            processed.Add(activation.special);
            hasAnySpecialActivation = true;
           // ApplySpecialActivation(affected, activation.special, activation.partner, ref hasLineActivation);
            ApplySpecialActivation(
            affected,
            activation.special,
            activation.partner,
            ref hasLineActivation,
            lightningVisualTargets,
            lightningLineStrikes);

            EnqueueChainSpecials(affected, queue, queued, processed);
        }

        board.IsSpecialActivationPhase = previousSpecialPhase;
        specialAffectedCells = previousAffectedCells;
    }

    void EnqueueActivation(Queue<SpecialActivation> queue, HashSet<TileView> queued, TileView special, TileView partner)
    {
        if (special == null || queued.Contains(special)) return;
        if (special.GetSpecial() == TileSpecial.None) return;
        queued.Add(special);
        queue.Enqueue(new SpecialActivation(special, partner));
    }

    void EnqueueChainSpecials(HashSet<TileView> affected, Queue<SpecialActivation> queue, HashSet<TileView> queued, HashSet<TileView> processed)
    {
        foreach (var tile in affected)
        {
            if (tile == null) continue;
            if (tile.GetSpecial() == TileSpecial.None) continue;
            if (processed.Contains(tile)) continue;
            EnqueueActivation(queue, queued, tile, null);
        }
    }

    void ApplySpecialActivation(HashSet<TileView> matches, TileView specialTile, TileView partnerTile, ref bool hasLineActivation, HashSet<TileView> lightningVisualTargets = null, List<LightningLineStrike> lightningLineStrikes = null)
    {
        if (specialTile == null) return;
        switch (specialTile.GetSpecial())
        {
            case TileSpecial.LineH:

            case TileSpecial.LineV:
                hasLineActivation = true;
                ApplyLineAt(matches, specialTile.X, specialTile.Y, specialTile.GetSpecial(), lightningVisualTargets, lightningLineStrikes);
                break;
            case TileSpecial.PulseCore:
                AddSquare(matches, specialTile.X, specialTile.Y, 1); //efected grid 3X3
                break;
            case TileSpecial.SystemOverride:
                {
                    TileType type;
                    if (partnerTile != null) type = partnerTile.GetTileType();
                    else type = specialTile.GetTileType();

                    // Solo (partner=null) veya normal partner → selection pulse aktif
                    var partnerSpecial = partnerTile != null ? partnerTile.GetSpecial() : TileSpecial.None;
                    overrideFanoutNormalSelectionPulse = (partnerTile == null) || (partnerSpecial == TileSpecial.None);
                    overrideFanoutPulseHitCount = 0;

                    // Fan-out lightning: hedefleri işaretle, sonra topluca temizle
                    overrideFanoutOrigin = specialTile;
                    // Special taşları hedefleme — onlar board'da kalsın, chain tetiklenmesin
                    CollectAllOfType(overrideFanoutTargets, type, excludeSpecials: true);
                    overrideForceDefaultClearAnim = true;
                    // Per-tile VFX gösterilsin (shrink/pop) — fan-out lightning zaten ayrı çalışır
                    overrideSuppressPerTileClearVfx = false;
                    AddAllOfType(matches, type, excludeSpecials: true);
                    break;
                }
            case TileSpecial.PatchBot:
                if (partnerTile != null)
                {
                    if (ApplyPatchBotTeleportHit(matches, specialTile, partnerTile, lightningVisualTargets, lightningLineStrikes))
                        hasLineActivation = true;
                }
                else
                    ApplyPatchBotSoloHit(matches, specialTile);   // ✅
                break;
        }
    }

    void AddLineEffect(HashSet<TileView> matches, TileView origin, TileSpecial line)
    {
        if (origin == null) return;
        if (line == TileSpecial.LineH)
            AddRow(matches, origin.Y);
        else if (line == TileSpecial.LineV)
            AddCol(matches, origin.X);
    }

// Centralized helpers so line-style specials (LineH/LineV and any combos that rely on LineTravel)
// don't have their row/col + strike logic copy-pasted across combo branches.
    void ApplyLineAt(
        HashSet<TileView> matches,
        int x, int y,
        TileSpecial line,
        HashSet<TileView> lightningVisualTargets,
        List<LightningLineStrike> lightningLineStrikes)
    {
        if (line == TileSpecial.LineH)
        {
            AddRow(matches, y);
            AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineH);
            if (lightningVisualTargets != null) AddRow(lightningVisualTargets, y);
        }
        else if (line == TileSpecial.LineV)
        {
            AddCol(matches, x);
            AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineV);
            if (lightningVisualTargets != null) AddCol(lightningVisualTargets, x);
        }
    }

    void ApplyFatLineAt(
        HashSet<TileView> matches,
        int x, int y,
        TileSpecial line,
        HashSet<TileView> lightningVisualTargets,
        List<LightningLineStrike> lightningLineStrikes)
    {
        if (line == TileSpecial.LineH)
        {
            AddRow(matches, y - 1);
            AddRow(matches, y);
            AddRow(matches, y + 1);
            AddLineStrike(lightningLineStrikes, x, y - 1, TileSpecial.LineH);
            AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineH);
            AddLineStrike(lightningLineStrikes, x, y + 1, TileSpecial.LineH);

            if (lightningVisualTargets != null)
            {
                AddRow(lightningVisualTargets, y - 1);
                AddRow(lightningVisualTargets, y);
                AddRow(lightningVisualTargets, y + 1);
            }
        }
        else if (line == TileSpecial.LineV)
        {
            AddCol(matches, x - 1);
            AddCol(matches, x);
            AddCol(matches, x + 1);
            AddLineStrike(lightningLineStrikes, x - 1, y, TileSpecial.LineV);
            AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineV);
            AddLineStrike(lightningLineStrikes, x + 1, y, TileSpecial.LineV);

            if (lightningVisualTargets != null)
            {
                AddCol(lightningVisualTargets, x - 1);
                AddCol(lightningVisualTargets, x);
                AddCol(lightningVisualTargets, x + 1);
            }
        }
    }

    void ApplyCrossAt(
        HashSet<TileView> matches,
        int x, int y,
        HashSet<TileView> lightningVisualTargets,
        List<LightningLineStrike> lightningLineStrikes)
    {
        AddRow(matches, y);
        AddCol(matches, x);
        AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineH);
        AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineV);

        if (lightningVisualTargets != null)
        {
            AddRow(lightningVisualTargets, y);
            AddCol(lightningVisualTargets, x);
        }
    }

    void ApplyPulseLineAt(
        HashSet<TileView> matches,
        int cx, int cy,
        HashSet<TileView> lightningVisualTargets,
        List<LightningLineStrike> lightningLineStrikes)
    {
        // 3 rows + 3 cols sweep (LineTravel)
        AddRow(matches, cy - 1);
        AddRow(matches, cy);
        AddRow(matches, cy + 1);
        AddCol(matches, cx - 1);
        AddCol(matches, cx);
        AddCol(matches, cx + 1);

        AddLineStrike(lightningLineStrikes, cx, cy - 1, TileSpecial.LineH);
        AddLineStrike(lightningLineStrikes, cx, cy, TileSpecial.LineH);
        AddLineStrike(lightningLineStrikes, cx, cy + 1, TileSpecial.LineH);
        AddLineStrike(lightningLineStrikes, cx - 1, cy, TileSpecial.LineV);
        AddLineStrike(lightningLineStrikes, cx, cy, TileSpecial.LineV);
        AddLineStrike(lightningLineStrikes, cx + 1, cy, TileSpecial.LineV);

        if (lightningVisualTargets != null)
        {
            AddRow(lightningVisualTargets, cy - 1);
            AddRow(lightningVisualTargets, cy);
            AddRow(lightningVisualTargets, cy + 1);
            AddCol(lightningVisualTargets, cx - 1);
            AddCol(lightningVisualTargets, cx);
            AddCol(lightningVisualTargets, cx + 1);
        }
    }

    bool ApplyPatchBotTeleportHit(HashSet<TileView> matches, TileView patchBotTile, TileView partnerTile, HashSet<TileView> lightningVisualTargets = null, List<LightningLineStrike> lightningLineStrikes = null)
    {
        if (patchBotTile == null || partnerTile == null) return false;

        var target = patchbotComboService.FindTarget(patchBotTile, partnerTile, null);
        if (!target.hasCell) return false;

        patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);

        bool partnerIsSpecial = partnerTile.GetSpecial() != TileSpecial.None;
        PlayTeleportMarkers(patchBotTile, target.x, target.y);

        if (partnerIsSpecial)
        {
            return TriggerPartnerEffectAt(matches, patchBotTile, partnerTile, target.x, target.y, lightningVisualTargets, lightningLineStrikes);
        }

        ApplyPatchBotTeleportToCell(matches, patchBotTile, partnerTile, target.x, target.y);
        return false;
    }

    void ApplyPatchBotTeleportToCell(HashSet<TileView> matches, TileView patchBotTile, TileView partnerTile, int targetX, int targetY)
    {
        if (patchBotTile == null || partnerTile == null) return;
        if (targetX < 0 || targetX >= board.Width || targetY < 0 || targetY >= board.Height) return;

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(targetX, targetY);
        if (board.Holes[targetX, targetY] && !hasObstacleAtTarget) return;

        patchbotComboService.ConsumeSwapSource(matches, patchBotTile, partnerTile, MarkAffectedCell);
        var matchDatas = new HashSet<TileData>();
        patchbotComboService.ResolveTargetImpact(matchDatas, targetX, targetY, hasObstacleAtTarget, MarkAffectedCell, MarkAffectedCell);
        foreach (var data in matchDatas)
        {
            if (board.Tiles[data.X, data.Y] != null) matches.Add(board.Tiles[data.X, data.Y]);
        }
    }

    bool TriggerPartnerEffectAt(HashSet<TileView> matches, TileView patchBotTile, TileView partnerTile, int originX, int originY, HashSet<TileView> lightningVisualTargets = null, List<LightningLineStrike> lightningLineStrikes = null)
    {
        if (partnerTile == null) return false;
        var special = partnerTile.GetSpecial();
        if (special == TileSpecial.None) return false;

        if (special == TileSpecial.LineH || special == TileSpecial.LineV)
        {
            PlayTeleportMarkers(partnerTile, originX, originY);
            PlayTransientSpecialVisualAt(partnerTile, originX, originY);
            ApplyLineAt(matches, originX, originY, special, lightningVisualTargets, lightningLineStrikes);
            return true;
        }

        if (special == TileSpecial.PulseCore)
        {
            PlayTeleportMarkers(partnerTile, originX, originY);
            PlayTransientSpecialVisualAt(partnerTile, originX, originY);
            board.PlayPulsePulseExplosionVfxAtCell(originX, originY);
            AddSquare(matches, originX, originY, 2);
            return false;
        }

        if (special == TileSpecial.SystemOverride)
        {
            PlayTeleportMarkers(partnerTile, originX, originY);
            TriggerSystemOverridePatchBotConversion(matches, patchBotTile, partnerTile);
        }
        return false;
    }

    void TriggerSystemOverridePatchBotConversion(HashSet<TileView> matches, TileView patchBotTile, TileView systemOverrideTile)
    {
        if (systemOverrideTile == null) return;

        TileType baseType = systemOverrideTile.GetOverrideBaseType(out var storedType) ? storedType : systemOverrideTile.GetTileType();

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y]) continue;
                var tile = board.Tiles[x, y];
                if (tile == null) continue;
                if (tile == patchBotTile || tile == systemOverrideTile) continue;
                if (!tile.GetTileType().Equals(baseType)) continue;
                if (tile.GetSpecial() != TileSpecial.None) continue;

                tile.SetSpecial(TileSpecial.PatchBot);
                AutoPatchBotTeleportHitAndVanish(matches, tile, patchBotTile, systemOverrideTile);
            }
    }

    void AutoPatchBotTeleportHitAndVanish(HashSet<TileView> matches, TileView autoPatchBot, TileView patchBotTile, TileView systemOverrideTile)
    {
        if (autoPatchBot == null) return;

        // Fan-out sırasında PatchBot ikonu hedefte kısa süre görünsün.
        if (!deferOverrideImplantVisualRefresh)
            HideTileVisualForCombo(autoPatchBot);
        matches.Add(autoPatchBot);
        MarkAffectedCell(autoPatchBot);

        var target = patchbotComboService.FindTarget(autoPatchBot, patchBotTile, null, systemOverrideTile);
        if (!target.hasCell) return;

        // Dash'i anında ve asenkron başlat (kuyruklamadan)
        // Böylece her PatchBot koyulduğu an harekete geçer, birbirini beklemez
        float dashDelay = deferOverrideImplantVisualRefresh ? 0.10f : 0f;
        FireImmediateDash(autoPatchBot, target.x, target.y, dashDelay);
        
        var matchSetData = new HashSet<TileData>();
        patchbotComboService.HitCellOnce(matchSetData, target.x, target.y, target.tile, MarkAffectedCell, MarkAffectedCell);
        
        foreach (var data in matchSetData) 
        {
            if (board.Tiles[data.X, data.Y] != null) matches.Add(board.Tiles[data.X, data.Y]);
        }
    }

    /// <summary>
    /// PatchBot dash'ini kuyruğa eklemeden anında asenkron başlatır.
    /// Override+PatchBot combo'larında her PatchBot'un hemen harekete geçmesi için kullanılır.
    /// </summary>
    void FireImmediateDash(TileView fromTile, int targetX, int targetY, float delay = 0f)
    {
        if (fromTile == null || board.PatchbotDashUI == null) return;

        IEnumerator CoPlayDash()
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            var req = new BoardController.PatchbotDashRequest
            {
                from = new Vector2Int(fromTile.X, fromTile.Y),
                to   = new Vector2Int(targetX, targetY)
            };
            var singleDash = new List<BoardController.PatchbotDashRequest>(1) { req };
            board.PatchbotDashUI.PlayDashParallel(singleDash, board);
        }

        board.StartCoroutine(CoPlayDash());
    }

    void PlayTeleportMarkers(TileView sourceTile, int targetX, int targetY)
    {
        if (board.BoardVfxPlayer == null || sourceTile == null) return;

        static Vector3 WorldCenter(TileView tv)
        {
            if (tv == null) return Vector3.zero;
            var rt = tv.GetComponent<RectTransform>();
            if (rt != null) return rt.TransformPoint(rt.rect.center);
            return tv.transform.position;
        }

        Vector3 CellWorldCenterVia(Transform reference, int x, int y, float ts)
        {
            var local = new Vector3(x * ts + ts * 0.5f, -y * ts - ts * 0.5f, 0f);
            return reference.TransformPoint(local);
        }

        var fromWorld = WorldCenter(sourceTile);
        var targetTile = board.Tiles[targetX, targetY];
        Vector3 toWorld;
        if (targetTile != null)
            toWorld = WorldCenter(targetTile);
        else
        {
            var reference = sourceTile.transform.parent != null ? sourceTile.transform.parent : sourceTile.transform;
            toWorld = CellWorldCenterVia(reference, targetX, targetY, board.TileSize);
        }

        board.BoardVfxPlayer.PlayTeleportMarkers(toWorld, fromWorld);
    }


    void PlayTransientSpecialVisualAt(TileView sourceTile, int targetX, int targetY)
    {
        if (sourceTile == null) return;

        var sprite = sourceTile.GetIconSprite();
        if (sprite == null) return;

        var parent = board.Parent != null ? board.Parent : sourceTile.transform.parent as RectTransform;
        if (parent == null) return;

        var ghostGo = new GameObject("PatchBotSpecialGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var ghostRt = ghostGo.GetComponent<RectTransform>();
        ghostRt.SetParent(parent, false);
        ghostRt.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRt.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRt.pivot = new Vector2(0.5f, 0.5f);
        ghostRt.sizeDelta = new Vector2(board.TileSize, board.TileSize);

        var image = ghostGo.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.95f);

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(targetX, targetY);
        float yOffset = hasObstacleAtTarget ? board.TileSize * 0.22f : 0f;
        ghostRt.anchoredPosition = new Vector2(targetX * board.TileSize + board.TileSize * 0.5f, -targetY * board.TileSize - board.TileSize * 0.5f + yOffset);
        ghostRt.localScale = hasObstacleAtTarget ? Vector3.one * 1.08f : Vector3.one;

        board.StartCoroutine(FadeAndDestroySpecialGhost(image, ghostRt, 0.24f));
    }

    IEnumerator FadeAndDestroySpecialGhost(UnityEngine.UI.Image image, RectTransform ghostRt, float duration)
    {
        float elapsed = 0f;
        Vector2 startPos = ghostRt != null ? ghostRt.anchoredPosition : Vector2.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            if (image != null)
            {
                var c = image.color;
                c.a = Mathf.Lerp(0.95f, 0f, t);
                image.color = c;
            }

            if (ghostRt != null)
            {
                float rise = board.TileSize * 0.08f * t;
                ghostRt.anchoredPosition = new Vector2(startPos.x, startPos.y + rise);
            }

            yield return null;
        }

        if (ghostRt != null)
            Object.Destroy(ghostRt.gameObject);
    }

    void TeleportTile(TileView tile, int targetX, int targetY)
    {
        
        if (tile == null) return;
        if (targetX < 0 || targetX >= board.Width || targetY < 0 || targetY >= board.Height) return;
        if (board.Holes[targetX, targetY]) return;

        var targetTile = board.Tiles[targetX, targetY];
        int sourceX = tile.X;
        int sourceY = tile.Y;
        PlayTeleportMarkers(tile, targetX, targetY);


        board.Tiles[sourceX, sourceY] = targetTile;
        if (targetTile != null)
        {
            targetTile.SetCoords(sourceX, sourceY);
            targetTile.SnapToGrid(board.TileSize);
        }
        board.SyncTileData(sourceX, sourceY); // Sync Data model

        board.Tiles[targetX, targetY] = tile;
        tile.SetCoords(targetX, targetY);
        tile.SnapToGrid(board.TileSize);
        board.SyncTileData(targetX, targetY); // Sync Data model

        board.RefreshTileObstacleVisual(tile);
        board.RefreshTileObstacleVisual(targetTile);
    }


    bool CanSpecialAffectCell(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (!board.Holes[x, y])
            return true;

        return board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y);
    }

    void ApplyComboEffect(HashSet<TileView> matches, Queue<SpecialActivation> queue, HashSet<TileView> queued, HashSet<TileView> processed, TileView a, TileView b, TileSpecial sa, TileSpecial sb, HashSet<TileView> lightningVisualTargets = null, List<LightningLineStrike> lightningLineStrikes = null)
    {
        bool IsLine(TileSpecial s) => s == TileSpecial.LineH || s == TileSpecial.LineV;
        bool IsPulse(TileSpecial s) => s == TileSpecial.PulseCore;
        bool IsPatchBot(TileSpecial s) => s == TileSpecial.PatchBot;
        bool IsOverride(TileSpecial s) => s == TileSpecial.SystemOverride;

        if (IsOverride(sa) && IsOverride(sb))
        {
            // Only show Override+Override combo VFX when both tiles are real overrides (have stored base type)
            bool aHasBase = a != null && a.GetOverrideBaseType(out _);
            bool bHasBase = b != null && b.GetOverrideBaseType(out _);
            if (aHasBase && bHasBase)
            {
                // Hide the two source tiles immediately so the combo runner is the only visible representation.
                HideTileVisualForCombo(a);
                HideTileVisualForCombo(b);

                overrideOverrideVfxDuration = board.PlaySystemOverrideComboVfxAndGetDuration();
            }

            AddAllTiles(matches);

            // Radial clear süresi VFX ile senkron: VFX genişlerken taşlar yok edilir.
            // pendingOverrideOverrideClearDelay = 0 → VFX'i beklemeden hemen temizlemeye başla.
            float clearDuration = Mathf.Max(OverrideOverrideRadialClearDuration, overrideOverrideVfxDuration * 0.85f);
            overrideOverrideRadialClearDelays = BuildCenterOutClearDelays(matches, clearDuration);
            return;
        }

        if (IsOverride(sa) || IsOverride(sb))
        {
            var overrideTile = IsOverride(sa) ? a : b;
            var otherTile = IsOverride(sa) ? b : a;

            if (otherTile == null || overrideTile == null)
                return;

            // Ensure swap sources are consumed (used)
            matches.Add(overrideTile);
            matches.Add(otherTile);
            MarkAffectedCell(overrideTile);
            MarkAffectedCell(otherTile);

            TileSpecial targetSpecial = otherTile.GetSpecial();
            bool targetIsLine = targetSpecial == TileSpecial.LineH || targetSpecial == TileSpecial.LineV;
            bool targetIsNormal = targetSpecial == TileSpecial.None;
            // Kural: Override taşı HER ZAMAN swaplandığı (partner) taşın rengini hedef almalı.
            TileType baseType = otherTile.GetTileType();

            // Single-shot fan-out lightning from the override tile to all affected targets
            overrideFanoutOrigin = overrideTile;
            // Keep default clear unless partner is a line special; line combos must preserve LineTravel strikes.
            overrideForceDefaultClearAnim = !targetIsLine;
            // Per-tile VFX: normal partner → shrink/pop göster; line partner → LineTravel görseli yeterli
            overrideSuppressPerTileClearVfx = targetIsLine;
            // IMPORTANT: In Override + Normal we want "selected" feedback when the lightning reaches a target.
            // Set this ONCE here so it can't be missed due to any loop filtering.
            overrideFanoutNormalSelectionPulse = targetIsNormal;

            // Build conversion/clear list (only normal tiles of the base type)
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    if (!CanSpecialAffectCell(x, y)) continue;
                    var tile = board.Tiles[x, y];
                    if (tile == null) continue;
                    if (!tile.GetTileType().Equals(baseType)) continue;
                    if (tile.GetSpecial() != TileSpecial.None) continue;

                    overrideFanoutTargets.Add(tile);

                    if (targetSpecial == TileSpecial.None)
                    {
                        // Normal partner: fan-out hedefleri beam ulaştığında kısa bir seçilme pulse
                        // oynatıp ardından normal clear akışına bırak.
                        matches.Add(tile);
                        MarkAffectedCell(tile);
                        continue;
                    }

                    if (targetSpecial == TileSpecial.PatchBot)
                    {
                        pendingOverrideImplants.Add(new PendingOverrideImplant(tile, TileSpecial.PatchBot, otherTile, overrideTile));
                        continue;
                    }

                    // Special partner: beam targetında taşı dönüştür, sonra özel etkiyi zincire ekle.
                    pendingOverrideImplants.Add(new PendingOverrideImplant(tile, targetSpecial, otherTile, overrideTile));
                }

            return;
        }

        if (IsLine(sa) && IsLine(sb))
        {
            ApplyCrossAt(matches, a.X, a.Y, lightningVisualTargets, lightningLineStrikes);
            return;
        }

        if (IsLine(sa) && IsPatchBot(sb) || (IsLine(sb) && IsPatchBot(sa)))
        {
            var lineTile = IsLine(sa) ? a : b;
            var patchBotTile = IsPatchBot(sa) ? a : b;
            var target = patchbotComboService.FindTarget(patchBotTile, lineTile, null);
            if (target.hasCell)
            {
                patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);
                PlayTeleportMarkers(patchBotTile, target.x, target.y);
                PlayTeleportMarkers(lineTile, target.x, target.y);
                ApplyLineAt(matches, target.x, target.y, lineTile.GetSpecial(), lightningVisualTargets, lightningLineStrikes);
            }
            return;
        }

        if (IsLine(sa) && IsPulse(sb) || (IsLine(sb) && IsPulse(sa)))
        {
            // Pulse + Line: sadece 3 satır + 3 sütun taraması (LineTravel). Per-tile pop VFX kapatılacak.
            var center = IsPulse(sa) ? a : b;
            if (center == null) return;

            ApplyPulseLineAt(matches, center.X, center.Y, lightningVisualTargets, lightningLineStrikes);
            return;
        }

        if (IsPatchBot(sa) && IsPatchBot(sb))
        {
            var usedTargets = new HashSet<TileView>();
            var dataMatches = new HashSet<TileData>();

            var firstTarget = patchbotComboService.FindTarget(a, b, usedTargets);
            if (firstTarget.hasCell)
            {
                if (firstTarget.tile != null)
                    usedTargets.Add(firstTarget.tile);
                patchbotComboService.EnqueueDash(a, firstTarget.x, firstTarget.y);
                PlayTeleportMarkers(a, firstTarget.x, firstTarget.y);
                patchbotComboService.HitCellOnce(dataMatches, firstTarget.x, firstTarget.y, firstTarget.tile, MarkAffectedCell, MarkAffectedCell);
            }

            var secondTarget = patchbotComboService.FindTarget(b, a, usedTargets);
            if (secondTarget.hasCell)
            {
                if (secondTarget.tile != null)
                    usedTargets.Add(secondTarget.tile);
                patchbotComboService.EnqueueDash(b, secondTarget.x, secondTarget.y);
                PlayTeleportMarkers(b, secondTarget.x, secondTarget.y);
                patchbotComboService.HitCellOnce(dataMatches, secondTarget.x, secondTarget.y, secondTarget.tile, MarkAffectedCell, MarkAffectedCell);
            }
            
            foreach (var data in dataMatches) 
            {
                if (board.Tiles[data.X, data.Y] != null) matches.Add(board.Tiles[data.X, data.Y]);
            }
            return;
        }

        if ((IsPatchBot(sa) && IsPulse(sb)) || (IsPulse(sa) && IsPatchBot(sb)))
        {
            var pulseTile = IsPulse(sa) ? a : b;
            var patchBotTile = IsPatchBot(sa) ? a : b;
            var target = patchbotComboService.FindTarget(patchBotTile, pulseTile, null);
            if (target.hasCell)
            {
                patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);
                PlayTeleportMarkers(patchBotTile, target.x, target.y);
                PlayTeleportMarkers(pulseTile, target.x, target.y);
                //AddSquareEven(matches, target.x, target.y, board.PatchBotPulseComboSize);
                AddSquare(matches, target.x, target.y, 1); // 3x3
            }
            return;
        }

        if (IsPulse(sa) && IsPulse(sb))
        {
                // Combo patlama VFX
            board.PlayPulsePulseExplosionVfxAtCell(a.X, a.Y);
            AddSquare(matches, a.X, a.Y, 2); //5X5
            return;
        }

        if (IsLine(sa) || IsLine(sb))
        {
            TileSpecial line = IsLine(sa) ? sa : sb;
            // Keep legacy behavior: apply from tile 'a' coords (swap origin),
            // but route through a single helper to avoid copy-paste.
            ApplyFatLineAt(matches, a.X, a.Y, line, lightningVisualTargets, lightningLineStrikes);
            return;
        }

    }

    void ApplyPendingOverrideImplantForTile(HashSet<TileView> matches, Queue<SpecialActivation> queue, HashSet<TileView> queued, TileView target)
    {
        if (target == null)
            return;

        if (overrideFanoutNormalSelectionPulse)
        {
            // NOTE: For override+normal partner we only want a quick "selected" feedback
            // when the lightning reaches the target. TileView.PlaySelectionPulse() was
            // unreliable (tile sizing logic can stomp scale), so we route through BoardAnimator.
            boardAnimator.PlaySelectionPulse(target, delay: 0f, peakScale: 1.30f, upTime: 0.10f, downTime: 0.10f);
            overrideFanoutPulseHitCount++;
        }

        if (pendingOverrideImplants.Count == 0)
            return;

        for (int i = pendingOverrideImplants.Count - 1; i >= 0; i--)
        {
            var pending = pendingOverrideImplants[i];
            if (pending.target != target)
                continue;

            ApplyPendingOverrideImplant(matches, queue, queued, pending);
            pendingOverrideImplants.RemoveAt(i);
        }
    }

    void ApplyPendingOverrideImplants(HashSet<TileView> matches, Queue<SpecialActivation> queue, HashSet<TileView> queued)
    {
        for (int i = 0; i < pendingOverrideImplants.Count; i++)
            ApplyPendingOverrideImplant(matches, queue, queued, pendingOverrideImplants[i]);

        pendingOverrideImplants.Clear();
    }

    void ApplyPendingOverrideImplant(HashSet<TileView> matches, Queue<SpecialActivation> queue, HashSet<TileView> queued, PendingOverrideImplant pending)
    {
        if (pending.target == null)
            return;

        pending.target.SetSpecial(pending.special, deferVisualUpdate: deferOverrideImplantVisualRefresh);
        // İmplant edilen taşı takip et — fan-out boyunca görsel güncellemesi ertelenebilir.
        overrideImplantedTiles.Add(pending.target);

        if (pending.special == TileSpecial.PatchBot)
        {
            AutoPatchBotTeleportHitAndVanish(matches, pending.target, pending.partnerTile, pending.overrideTile);
            return;
        }

        matches.Add(pending.target);
        MarkAffectedCell(pending.target);
        EnqueueActivation(queue, queued, pending.target, pending.partnerTile);
    }


    void AddLineStrike(List<LightningLineStrike> lineStrikes, int x, int y, TileSpecial lineSpecial)
    {
        if (lineStrikes == null)
            return;

        bool isHorizontal = lineSpecial == TileSpecial.LineH;
        bool isVertical = lineSpecial == TileSpecial.LineV;
        if (!isHorizontal && !isVertical)
            return;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        lineStrikes.Add(new LightningLineStrike(new Vector2Int(x, y), isHorizontal));
    }

    void AddRow(HashSet<TileView> matches, int y)
    {
        if (y < 0 || y >= board.Height) return;
        for (int x = 0; x < board.Width; x++)
            if (CanSpecialAffectCell(x, y))
            {
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null)
                    matches.Add(board.Tiles[x, y]);
            }
    }

    void AddCol(HashSet<TileView> matches, int x)
    {
        if (x < 0 || x >= board.Width) return;
        for (int y = 0; y < board.Height; y++)
            if (CanSpecialAffectCell(x, y))
            {
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null)
                    matches.Add(board.Tiles[x, y]);
            }
    }

    void AddSquare(HashSet<TileView> matches, int cx, int cy, int radius)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;
                if (!CanSpecialAffectCell(x, y)) continue;
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null) matches.Add(board.Tiles[x, y]);
            }
    }

    void AddSquareEven(HashSet<TileView> matches, int cx, int cy, int size)
    {
        if (size < 2) return;
        int half = size / 2;
        int startX = cx - (half - 1);
        int startY = cy - (half - 1);
        int endX = startX + size - 1;
        int endY = startY + size - 1;

        for (int x = startX; x <= endX; x++)
            for (int y = startY; y <= endY; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;
                if (!CanSpecialAffectCell(x, y)) continue;
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null) matches.Add(board.Tiles[x, y]);
            }
    }

    void AddAllTiles(HashSet<TileView> matches)
    {
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!CanSpecialAffectCell(x, y)) continue;
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null) matches.Add(board.Tiles[x, y]);
            }
    }

    
    void CollectAllOfType(List<TileView> buffer, TileType type, bool excludeSpecials)
    {
        if (buffer == null) return;
        buffer.Clear();

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!CanSpecialAffectCell(x, y)) continue;
                var t = board.Tiles[x, y];
                if (t == null) continue;
                if (!t.GetTileType().Equals(type)) continue;
                if (excludeSpecials && t.GetSpecial() != TileSpecial.None) continue;
                buffer.Add(t);
            }
    }

void AddAllOfType(HashSet<TileView> matches, TileType type, bool excludeSpecials = false)
    {
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!CanSpecialAffectCell(x, y)) continue;
                var t = board.Tiles[x, y];
                if (t == null) continue;
                if (!t.GetTileType().Equals(type)) continue;
                if (excludeSpecials && t.GetSpecial() != TileSpecial.None) continue;
                MarkAffectedCell(x, y);
                matches.Add(t);
            }
    }

    void MarkAffectedCell(TileView tile)
    {
        if (tile == null) return;
        MarkAffectedCell(tile.X, tile.Y);
    }

    void MarkAffectedCell(int x, int y)
    {
        if (specialAffectedCells == null) return;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return;
        if (!CanSpecialAffectCell(x, y)) return;
        specialAffectedCells.Add(new Vector2Int(x, y));
    }

    void ApplyPatchBotSoloHit(HashSet<TileView> matches, TileView patchBotTile)
    {
        if (patchBotTile == null) return;

        // PatchBot zaten clear edilecek; sadece kendi cell’ini affected olarak işaretlemek yeterli
        matches.Add(patchBotTile);
        MarkAffectedCell(patchBotTile);

        // 1 hedef seç ve vur
        var target = patchbotComboService.FindTarget(patchBotTile, null, null);
        if (!target.hasCell) return;

        patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);

        PlayTeleportMarkers(patchBotTile, target.x, target.y);

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(target.x, target.y);

        var dataMatches = new HashSet<TileData>();
        patchbotComboService.ResolveTargetImpact(dataMatches, target.x, target.y, hasObstacleAtTarget, MarkAffectedCell, MarkAffectedCell);
        
        foreach (var data in dataMatches) 
        {
            if (board.Tiles[data.X, data.Y] != null) matches.Add(board.Tiles[data.X, data.Y]);
        }
    }

    private Dictionary<TileView, float> BuildCenterOutClearDelays(HashSet<TileView> targets, float maxDelay)
    {
        if (targets == null || targets.Count == 0 || maxDelay <= 0f)
            return null;

        float centerX = (board.Width - 1) * 0.5f;
        float centerY = (board.Height - 1) * 0.5f;
        var center = new Vector2(centerX, centerY);
        float maxDistance = 0f;

        foreach (var tile in targets)
        {
            if (tile == null) continue;
            float distance = Vector2.Distance(new Vector2(tile.X, tile.Y), center);
            if (distance > maxDistance)
                maxDistance = distance;
        }

        if (maxDistance <= Mathf.Epsilon)
            return null;

        var delays = new Dictionary<TileView, float>(targets.Count);
        foreach (var tile in targets)
        {
            if (tile == null) continue;
            float distance = Vector2.Distance(new Vector2(tile.X, tile.Y), center);
            float normalized = Mathf.Clamp01(distance / maxDistance);
            // Ease-out quadratic: wave expands quickly from center then decelerates at edges,
            // giving a dramatic shockwave-like feel.
            float eased = 1f - (1f - normalized) * (1f - normalized);
            delays[tile] = eased * maxDelay;
        }

        return delays;
    }

    /// <summary>
    /// Override+Override radial dalga sırasında yoluna çıkan special taşların
    /// görsel efektini (sadece VFX, mantıksal etki yok) ateşler.
    /// Her special'ın efekti, dalganın o hücreye ulaştığı zamana göre tetiklenir.
    /// </summary>
    private void FireOverrideOverrideSpecialVisuals(HashSet<TileView> affected, Dictionary<TileView, float> radialDelays)
    {
        if (affected == null || radialDelays == null || board == null) return;

        foreach (var tile in affected)
        {
            if (tile == null) continue;
            var spec = tile.GetSpecial();
            if (spec == TileSpecial.None) continue;
            if (!radialDelays.TryGetValue(tile, out float delay)) continue;

            int x = tile.X;
            int y = tile.Y;
            board.StartCoroutine(DelayedSpecialVisualTrigger(x, y, spec, delay));
        }
    }

    /// <summary>
    /// Belirtilen gecikme sonrasında hücredeki special'ın görsel efektini çalar.
    /// Mantıksal etki yoktur — sadece Override+Override dalgası sırasında
    /// kullanıcıya special taşların varlığını göstermek içindir.
    /// </summary>
    private IEnumerator DelayedSpecialVisualTrigger(int x, int y, TileSpecial special, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        switch (special)
        {
            case TileSpecial.PulseCore:
                board.PlayPulsePulseExplosionVfxAtCell(x, y);
                board.StartCoroutine(boardAnimator.MicroShake(0.08f, board.ShakeStrength * 0.4f));
                break;
            case TileSpecial.LineH:
            case TileSpecial.LineV:
            {
                var strikes = new List<LightningLineStrike>(1)
                {
                    new LightningLineStrike(new Vector2Int(x, y), special == TileSpecial.LineH)
                };
                board.PlayLightningLineStrikes(strikes, null);
                break;
            }
        }
    }

    
    readonly struct SpecialActivation
    {
        public readonly TileView special;
        public readonly TileView partner;

        public SpecialActivation(TileView special, TileView partner)
        {
            this.special = special;
            this.partner = partner;
        }
    }
}