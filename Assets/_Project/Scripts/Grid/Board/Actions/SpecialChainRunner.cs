using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HEDEF: TÜM special/comboların zincir motoru (bkz. docs/SpecialChainRunner_Plan.md).
/// Çekirdek ilke: bir special tetiklenir → gerçek animasyon hücreden hücreye yol alır →
/// vardığı her hücrede ne varsa (taş/obstacle/launcher/special) O AN tetiklenir →
/// tetiklenen special kendi animasyonunu yayar. Sanal/deferred zamanlama yok.
///
/// Bu sınıf o motorun mevcut çekirdeği (eski adı PulseChainSequenceAction). Natively
/// PulseCore (5x5) ve LineV/H'i işliyor; LineV/H beam'i yol üstündeki special'ı
/// VARDIĞI AN eşzamanlı alt-zincir olarak ateşliyor (AddArrivalTrigger →
/// LaunchArrivalSubChain, background job = paralel). Diğer special'lar (PatchBot,
/// Override) resolveOtherSpecial ile GERÇEK sınıflarıyla tetikleniyor.
///
/// FAZ 1: 5 solo special'ın TÜM aktivasyonları (tap + swap) bu motordan geçer —
/// SpecialResolver artık tekli aktivasyonda da runner seed'ler. protectedCells,
/// swap'ın normal tarafında yeni oluşan special'ın emisyonlarca tüketilmemesini sağlar
/// (eski Execute yolundaki ProtectedCells'in karşılığı; yalnız bu instance'ın kendi
/// emisyonlarına uygulanır, alt-zincirlere taşınmaz — gravity sonrası hücre bayatlar).
///
/// Kuyruğa giren special anchor'lanır (pending-triggered → gravity-blocked) ki oyuncunun
/// gördüğü yerde patlasın. Adımdan sonra CalculateCascades + catchOverlap ile düşüş.
///
/// YAPILACAK (plan Faz 2-4): kalan bespoke combo action'lar + deferred-override yolları
/// emekliye, emisyonlar paralel ilerlesin.
/// </summary>
public sealed class SpecialChainRunner : BoardAction
{
    private readonly BoardController board;
    private readonly List<TileView> initialSpecials;
    private readonly int areaHalf;
    private readonly float catchOverlap;
    private readonly Func<TileView, List<BoardAction>> resolveOtherSpecial;

    // Dolu ise: bu runner FUSED cross emisyonuyla başlar (Line+Pulse, Line+Line...).
    // Combo'lar bunu seed eder; null ise normal solo/zincir davranışı (eski).
    private readonly Vector2Int? linePulseCrossCenter;

    // Cross yarıçapı: 1 → 3 satır+3 sütun (Line+Pulse), 0 → 1 satır+1 sütun "+" (Line+Line).
    private readonly int crossHalf;

    // Override+Pulse: implant edilen pulse hücreleri. Hepsi AYNI ANDA patlar (yer değiştirme yok);
    // her merkez radyal dalga yayar, dalga bir special'a VARINCA o an tetiklenir (arrival).
    private readonly List<Vector2Int> simultaneousPulseCells;

    // PatchBot+Pulse payload (DiveBurst): merkezde PulseCore TILE'I YOK — pulse havadan
    // taşınıp geldi. Merkez hücre(ler) sanal patlama noktası; hücre mantığı
    // ExplodePulsesSimultaneously ile AYNI, tek fark merkez doğrulaması (tile aranmaz)
    // ve merkezdeki special de zincirlenir (tüketilmez).
    private readonly List<Vector2Int> virtualPulseBurstCenters;

    // PatchBot+LineV/H payload (DiveBurst): origin hücrede Line TILE'I YOK — beam havadan
    // taşınıp geldi. Her strike kendi satır/sütununu süpürür; hücre mantığı
    // SweepLinesSimultaneously ile AYNI, origin hücredeki special de zincirlenir.
    private readonly List<LightningLineStrike> virtualLineSweeps;

    // Override+Line: implant edilen line hücreleri. Hepsi AYNI ANDA kendi satır/sütununu süpürür;
    // yol üstündeki special dalga varınca arrival ile tetiklenir (pulse'un line karşılığı).
    private readonly List<Vector2Int> simultaneousLineCells;

    // Override+Override: board'daki KARIŞIK special hücreleri. Hepsi anchor'lanıp AYNI ANDA
    // paralel sub-chain olarak ateşlenir (her biri tipine göre: pulse→ExplodePulse, line→SweepLine,
    // override→.Execute, patchbot→.Execute). Tüm-board clear normal taşları ayrı temizler.
    private readonly List<Vector2Int> simultaneousMixedCells;

    // Combo'nun KENDİ tile'ları (örn. Line+Pulse'ın iki origin'i): cross bunları zincir
    // değil TÜKETİLECEK (clear) sayar — yoksa kendilerini tekrar tetiklerlerdi.
    private readonly List<TileView> consumeTiles;

    // Swap'ın normal tarafında AYNI hamlede oluşan yeni special hücreleri: bu instance'ın
    // emisyonları bu hücrelere hiç dokunmaz (clear yok, zincir yok) — oyuncunun yeni
    // kazandığı special patlamanın içinde kalsa da hayatta kalır.
    private readonly HashSet<Vector2Int> protectedCells;

    // Queued specials are anchored via pending-triggered cells (gravity-blocked in
    // CalculateCascades) so they activate where the player saw them instead of
    // falling between steps. Released when the special's own turn comes. The registry
    // lives on the ROOT chain: arrival sub-chains share it, and only the root runs
    // the final wait/release (a sub-chain runs as a background job itself, so waiting
    // on ActiveBackgroundJobs from inside one would deadlock until the safety cap).
    private readonly SpecialChainRunner root;
    private readonly Dictionary<TileView, Vector2Int> anchoredCells = new();
    private readonly Vector2Int[] anchorCellBuffer = new Vector2Int[1];

    public override bool Blocking => true;

    public SpecialChainRunner(
        BoardController board,
        List<TileView> initialSpecials,
        int areaHalf,
        float catchOverlapFraction,
        Func<TileView, List<BoardAction>> resolveOtherSpecial = null,
        SpecialChainRunner root = null,
        Vector2Int? linePulseCrossCenter = null,
        List<TileView> consumeTiles = null,
        int crossHalf = 1,
        List<Vector2Int> simultaneousPulseCells = null,
        List<Vector2Int> simultaneousLineCells = null,
        List<Vector2Int> simultaneousMixedCells = null,
        HashSet<Vector2Int> protectedCells = null,
        List<Vector2Int> virtualPulseBurstCenters = null,
        List<LightningLineStrike> virtualLineSweeps = null)
    {
        this.board = board;
        this.initialSpecials = initialSpecials ?? new List<TileView>();
        this.areaHalf = Mathf.Max(1, areaHalf);
        this.catchOverlap = Mathf.Clamp01(catchOverlapFraction);
        this.resolveOtherSpecial = resolveOtherSpecial;
        this.root = root ?? this;
        this.linePulseCrossCenter = linePulseCrossCenter;
        this.consumeTiles = consumeTiles;
        this.crossHalf = Mathf.Max(0, crossHalf);
        this.simultaneousPulseCells = simultaneousPulseCells;
        this.simultaneousLineCells = simultaneousLineCells;
        this.simultaneousMixedCells = simultaneousMixedCells;
        this.protectedCells = protectedCells;
        this.virtualPulseBurstCenters = virtualPulseBurstCenters;
        this.virtualLineSweeps = virtualLineSweeps;
    }

    private bool IsProtectedCell(int x, int y) =>
        protectedCells != null && protectedCells.Contains(new Vector2Int(x, y));

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        bool hasSimultaneousPulses = simultaneousPulseCells != null && simultaneousPulseCells.Count > 0;
        bool hasSimultaneousLines = simultaneousLineCells != null && simultaneousLineCells.Count > 0;
        bool hasSimultaneousMixed = simultaneousMixedCells != null && simultaneousMixedCells.Count > 0;
        bool hasVirtualBursts = virtualPulseBurstCenters != null && virtualPulseBurstCenters.Count > 0;
        bool hasVirtualLineSweeps = virtualLineSweeps != null && virtualLineSweeps.Count > 0;
        if (board == null || (initialSpecials.Count == 0 && linePulseCrossCenter == null
                              && !hasSimultaneousPulses && !hasSimultaneousLines && !hasSimultaneousMixed
                              && !hasVirtualBursts && !hasVirtualLineSweeps))
            yield break;

        var queue = new Queue<TileView>(initialSpecials);
        var processed = new HashSet<TileView>();

        // Override+Pulse: tüm implant pulse'lar AYNI ANDA patlar (yer değiştirme yok); zincirleri
        // radyal dalga hücreye varınca arrival ile tetiklenir (Line/SweepLine ile aynı model).
        if (hasSimultaneousPulses)
            yield return ExplodePulsesSimultaneously(sequencer, processed, simultaneousPulseCells, centersArePulseTiles: true);

        // PatchBot+Pulse payload (DiveBurst): varış hücresinde sanal pulse patlaması —
        // hedef seçimi hariç her şey pulse yolunun aynısı (alandaki special arrival'da tetiklenir).
        if (hasVirtualBursts)
            yield return ExplodePulsesSimultaneously(sequencer, processed, virtualPulseBurstCenters, centersArePulseTiles: false);

        // Override+Line: tüm implant line'lar AYNI ANDA kendi satır/sütununu süpürür.
        if (hasSimultaneousLines)
            yield return SweepLinesSimultaneously(sequencer, processed, linesAreTiles: true);

        // PatchBot+Line payload (DiveBurst): varış hücresinden sanal beam süpürmesi —
        // hedef seçimi hariç her şey line yolunun aynısı (yoldaki special arrival'da tetiklenir).
        if (hasVirtualLineSweeps)
            yield return SweepLinesSimultaneously(sequencer, processed, linesAreTiles: false);

        // Override+Override: board'daki karışık special'lar aynı anda paralel sub-chain olarak ateşlenir.
        if (hasSimultaneousMixed)
            yield return FireMixedCellsSimultaneously(processed);

        // Line+Pulse fused emisyonu: önce 3x3 cross süpürülür; yol üstündeki special'lar
        // VARDIĞI AN eşzamanlı alt-zincir olur (queue'ya değil). Sonra (varsa) seed zinciri.
        if (linePulseCrossCenter.HasValue)
            yield return SweepCross(sequencer, linePulseCrossCenter.Value, processed);

        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            ReleaseAnchor(t);
            if (t == null || !t) continue;
            if (processed.Contains(t)) continue;

            // The tile may have fallen during a previous step's gravity; read its live
            // cell. If it was consumed/cleared meanwhile, skip it.
            int cx = t.X, cy = t.Y;
            if (cx < 0 || cx >= board.Width || cy < 0 || cy >= board.Height) continue;
            if (board.Tiles[cx, cy] != t) continue;

            var special = t.GetSpecial();
            if (special == TileSpecial.None) continue;

            processed.Add(t);

            if (special == TileSpecial.PulseCore)
            {
                yield return ExplodePulse(sequencer, t, cx, cy, queue, processed);
            }
            else if (special == TileSpecial.LineV || special == TileSpecial.LineH)
            {
                yield return SweepLine(sequencer, t, cx, cy, special == TileSpecial.LineH, queue, processed);
            }
            else if (resolveOtherSpecial != null)
            {
                // Non-pulse special inside the chain — activate its own effect inline.
                var acts = resolveOtherSpecial(t);
                if (acts != null)
                {
                    for (int i = 0; i < acts.Count; i++)
                        if (acts[i] != null)
                            yield return acts[i].ExecuteVisuals(sequencer);
                }
                yield return RunGravityWithOverlap(queue.Count > 0);
            }

            // After this step, any newly-arrived specials are discovered on the next
            // pulse area scan (live board). Non-pulse specials reach the queue only
            // through pulse area scans below.
        }

        // Only the root chain runs the final settle: a sub-chain IS a background job,
        // so it must not wait on ActiveBackgroundJobs (it would see itself).
        if (root == this)
        {
            // Wait for background falls AND arrival sub-chains to finish. Anchors must
            // outlive the sub-chains: a released cell could let a not-yet-blasted
            // special fall away from its sub-chain mid-VFX.
            float safety = 0f;
            // Non-blocking uçuşları (goal orb, PatchBot dash) bekleme; hedefe uçarken
            // special zincirinin final settle'ı donmasın. Gerçek background falls/sub-chain'leri
            // beklemeye devam et (BlockingBackgroundJobs).
            while (board.BlockingBackgroundJobs > 0 && safety < 5f)
            {
                // KRİTİK: background job'lar sürerken board DONMASIN.
                // PatchBot dash artık BlockingBackgroundJobs dışında; burası hâlâ gerçek
                // sub-chain/fall job'ları sırasında boş hücreleri kapatır.
                // Runner Blocking olduğu için ResolveBoard loop'u şu an durmuş; gravity'yi onun yerine
                // burada çalıştır. CalculateCascades board.Tiles'ı senkron günceller → HasAnyEmptyPlayableCell
                // guard'ı kendini sınırlar (boş hücre kalmayınca durur). Patchbot HER yoldan tetiklense de
                // (swap/chain/LineVHPulse) taş düşüşü garanti.
                if (board.CascadeLogic != null && board.CascadeLogic.HasAnyEmptyPlayableCell())
                {
                    var fall = board.CascadeLogic.CalculateCascades();
                    if (fall != null)
                        for (int i = 0; i < fall.Count; i++)
                            board.StartImmediateAction(fall[i]);
                    board.RefreshAllSortingOrders();
                }

                safety += Time.deltaTime;
                yield return null;
            }

            // No anchor may outlive the chain (a leftover pending cell would block
            // gravity in that column forever). Released cells may leave holes → settle.
            if (ReleaseAllAnchors())
                yield return RunGravityWithOverlap(hasNext: false);
        }

        board.RefreshAllSortingOrders();
    }

    private IEnumerator ExplodePulse(
        ActionSequencer sequencer, TileView pulse, int cx, int cy,
        Queue<TileView> queue, HashSet<TileView> processed)
    {
        // 1) Explosion VFX — dalganın GERÇEK yarıçapı callback'le gelir (tek saat):
        //    halkalar görsel dalga o mesafeye VARINCA kırılır, zamanlayıcı tahmini yok.
        float waveRadiusPx = 0f;
        board.PulseCoreImpactService?.PlayPulseCoreExplosionVfxAtCell(
            cx, cy, radiusCells: areaHalf, onRadiusPx: px => waveRadiusPx = Mathf.Max(waveRadiusPx, px));

        // 2) Collect area: normal tiles clear, chained specials queue.
        var clearTiles = new HashSet<TileView> { pulse };
        var affectedCells = new HashSet<Vector2Int>();
        var impactCells = new List<Vector2Int>();
        var ringArrivals = new Dictionary<int, List<TileView>>();

        // Combo'nun kendi origin'leri (örn. Pulse+Pulse'ın ikinci pulse'ı): zincirlenmesin,
        // tüketilsin (clear) — yoksa ikinci pulse ayrı patlardı. (SweepCross ile aynı mantık.)
        if (consumeTiles != null)
            foreach (var ct in consumeTiles)
                if (ct != null && ct) { processed.Add(ct); clearTiles.Add(ct); }

        for (int x = cx - areaHalf; x <= cx + areaHalf; x++)
        {
            for (int y = cy - areaHalf; y <= cy + areaHalf; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;
                if (IsProtectedCell(x, y)) continue; // swap'ta yeni oluşan special'a dokunma

                if (!SpecialUtils.CanAffectCell(board, x, y))
                {
                    SpecialCellUtils.TryAddObstacleImpact(board, x, y, impactCells);
                    continue;
                }

                var cell = new Vector2Int(x, y);
                affectedCells.Add(cell);

                // Movable obstacle (Plastic vb.) tile-clear yoluna GİRMEZ: view'ın yaşam
                // döngüsü obstacle hasarınındır (impactCells hasarı vurur; yıkılırsa
                // HandleObstacleDestroyed view'ı kaldırır). Tile-clear + hücre-bazlı geç
                // hasar, eşzamanlı gravity'de taş kayınca state'i ıskalayıp görünmez
                // (orphan) obstacle bırakabiliyordu.
                if (SpecialCellUtils.TryRouteMovableToImpact(board, x, y, impactCells))
                    continue;

                impactCells.Add(cell);

                var tile = board.Tiles[x, y];
                if (tile == null) continue;

                var sp = tile.GetSpecial();

                // A special already claimed by another step/sub-chain but not yet
                // consumed → leave it alone (it must blast itself, not clear here).
                if (tile != pulse && sp != TileSpecial.None && processed.Contains(tile))
                    continue;

                // Alandaki her special (pulse dahil) KUYRUĞA DEĞİL, dalga hücresine
                // VARDIĞI AN paralel alt-zincire gider (karar #2: emisyonlar paralel;
                // SweepLine'ın arrival modeliyle aynı). Anchor: patlayana dek yerinde.
                if (tile != pulse && sp != TileSpecial.None && !processed.Contains(tile))
                {
                    processed.Add(tile);
                    AnchorQueued(tile, x, y);
                    int ring = Mathf.Max(Mathf.Abs(x - cx), Mathf.Abs(y - cy));
                    if (!ringArrivals.TryGetValue(ring, out var listA)) ringArrivals[ring] = listA = new List<TileView>();
                    listA.Add(tile);
                    continue;
                }

                clearTiles.Add(tile);
            }
        }

        // 3) HALKA-BAZLI ARRIVAL CLEAR (kullanıcı direktifi): dalga animasyonu halkaya
        //    varınca o halkanın taşları O AN kırılır. VFX gelmezse timer fallback
        //    (dist × PulseImpactDelayStep) akışı asla bekletmez.
        yield return RunRadialArrivalClear(
            sequencer,
            cell => Mathf.Max(Mathf.Abs(cell.x - cx), Mathf.Abs(cell.y - cy)),
            () => waveRadiusPx,
            clearTiles, affectedCells, impactCells,
            arrivalSpecials: ringArrivals);

        // 4) Gravity + overlap.
        yield return RunGravityWithOverlap(queue.Count > 0);
    }

    // ── Radyal varış motoru: hücreleri merkez(ler)e uzaklık halkalarına böler; gerçek
    // dalga yarıçapı (getWaveRadiusPx) halkayı geçince o halka ANINDA temizlenir,
    // halkadaki arrival special'lar o an alt-zincir olur. Son halka inline beklenir →
    // adım gravity'si veri temizliği bitmeden koşmaz. Timer fallback VFX'siz kurulumda
    // da aynı tempoyu verir. (Plan §1.1 RadialWave'in gerçek hâli.)
    private IEnumerator RunRadialArrivalClear(
        ActionSequencer sequencer,
        Func<Vector2Int, int> ringOf,
        Func<float> getWaveRadiusPx,
        HashSet<TileView> clearTiles,
        HashSet<Vector2Int> affectedCells,
        List<Vector2Int> impactCells,
        Dictionary<int, List<TileView>> arrivalSpecials)
    {
        int maxRing = 0;

        var ringTiles = new Dictionary<int, HashSet<TileView>>();
        foreach (var t in clearTiles)
        {
            if (t == null) continue;
            int d = Mathf.Max(0, ringOf(new Vector2Int(t.X, t.Y)));
            if (!ringTiles.TryGetValue(d, out var set)) ringTiles[d] = set = new HashSet<TileView>();
            set.Add(t);
            if (d > maxRing) maxRing = d;
        }

        var ringCells = new Dictionary<int, HashSet<Vector2Int>>();
        foreach (var c in affectedCells)
        {
            int d = Mathf.Max(0, ringOf(c));
            if (!ringCells.TryGetValue(d, out var set)) ringCells[d] = set = new HashSet<Vector2Int>();
            set.Add(c);
            if (d > maxRing) maxRing = d;
        }

        var ringImpacts = new Dictionary<int, List<Vector2Int>>();
        foreach (var c in impactCells)
        {
            int d = Mathf.Max(0, ringOf(c));
            if (!ringImpacts.TryGetValue(d, out var listI)) ringImpacts[d] = listI = new List<Vector2Int>();
            listI.Add(c);
            if (d > maxRing) maxRing = d;
        }

        if (arrivalSpecials != null)
            foreach (var kv in arrivalSpecials)
                if (kv.Key > maxRing) maxRing = kv.Key;

        float tileSize = Mathf.Max(1f, board.TileSize);
        float fallbackTimer = 0f;

        MatchClearAction FireRing(int d)
        {
            // Önce arrival special'lar (dalga taşa değdiği an tetiklensin).
            if (arrivalSpecials != null && arrivalSpecials.TryGetValue(d, out var specials))
                foreach (var sp in specials)
                    LaunchArrivalSubChain(sp);

            ringTiles.TryGetValue(d, out var tiles);
            ringCells.TryGetValue(d, out var cells);
            ringImpacts.TryGetValue(d, out var impacts);
            if ((tiles == null || tiles.Count == 0) && (cells == null || cells.Count == 0)
                && (impacts == null || impacts.Count == 0))
                return null;

            var act = new MatchClearAction(
                tiles ?? new HashSet<TileView>(),
                doShake: d == 0,
                animationMode: ClearAnimationMode.Default,
                affectedCells: cells,
                impactCells: impacts,
                includeAdjacentOverTileBlockerDamage: false,
                isSpecialPhase: true);

            if (d < maxRing)
                board.StartImmediateAction(act);   // ara halkalar paralel oynar
            return act;
        }

        int next = 0;
        bool inlineWaited = false;
        while (next <= maxRing)
        {
            fallbackTimer += Time.deltaTime;
            float waveRing = getWaveRadiusPx() / tileSize + 0.35f;   // yarım hücre tolerans
            float timerRing = board.PulseImpactDelayStep > 0f
                ? fallbackTimer / board.ApplySpecialChainTempo(board.PulseImpactDelayStep)
                : maxRing + 1f;
            // KRİTİK: float.MaxValue ("hepsi serbest" sinyali) FloorToInt'te int taşması
            // yapar (negatif çıkar) → Clamp ŞART; yoksa halkalar asla ateşlenmez (donma).
            int reach = Mathf.FloorToInt(Mathf.Clamp(Mathf.Max(waveRing, timerRing), 0f, maxRing + 1f));

            while (next <= maxRing && next <= reach)
            {
                var act = FireRing(next);
                if (next == maxRing && act != null)
                {
                    inlineWaited = true;
                    yield return act.ExecuteVisuals(sequencer);   // son halka inline
                }
                next++;
            }

            if (next <= maxRing)
                yield return null;
        }

        // Son halka boştu → önceki halkalar hâlâ background'da oynuyor olabilir;
        // gravity veri temizliğinden önce koşmasın diye bir clear süresi bekle.
        if (!inlineWaited)
            yield return new WaitForSeconds(board.ApplySpecialChainTempo(board.PulseImpactAnimTime));
    }

    private IEnumerator SweepLine(
        ActionSequencer sequencer, TileView line, int cx, int cy, bool isHorizontal,
        Queue<TileView> queue, HashSet<TileView> processed)
    {
        // 1) Collect the row/column: normal tiles clear; a special on the path fires
        //    THE MOMENT the beam reaches its cell (arrival trigger → sub-chain), not
        //    on a later queue turn — the sweep must never pass over something without
        //    breaking/triggering it. Mirrors LineV/HSpecial.CollectColumn/Row: cells
        //    failing CanAffectCell are skipped entirely (no blocker damage on path).
        var clearTiles = new HashSet<TileView>();
        var visualTargets = new List<TileView>();
        var affectedCells = new HashSet<Vector2Int>();
        var arrivalSpecials = new List<TileView>();

        int len = isHorizontal ? board.Width : board.Height;
        for (int i = 0; i < len; i++)
        {
            int x = isHorizontal ? i : cx;
            int y = isHorizontal ? cy : i;

            if (IsProtectedCell(x, y)) continue; // swap'ta yeni oluşan special'a dokunma

            if (!SpecialUtils.CanAffectCell(board, x, y))
                continue;

            affectedCells.Add(new Vector2Int(x, y));

            var tile = board.Tiles[x, y];
            if (tile == null) continue;

            // Movable obstacle (Plastic vb.) tile-clear yoluna girmez — beam'in
            // obstacle hasarı (TryHit) vurur, yıkılırsa view'ı handler kaldırır.
            // (ExplodePulse'taki orphan-obstacle açıklamasının aynısı.)
            if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(x, y))
                continue;

            if (tile != line && tile.GetSpecial() != TileSpecial.None)
            {
                // Already claimed by another step/sub-chain → leave it alone.
                if (processed.Contains(tile)) continue;

                processed.Add(tile);
                AnchorQueued(tile, x, y); // stays put until its own blast resolves
                arrivalSpecials.Add(tile);
                continue;
            }

            clearTiles.Add(tile);
            visualTargets.Add(tile);
        }

        clearTiles.Add(line);

        // 2) Immediate clear with the line's lightning-strike presentation
        //    (same visuals the inline/combined line path uses).
        var strikes = new List<LightningLineStrike>
        {
            new LightningLineStrike(new Vector2Int(cx, cy), isHorizontal)
        };

        var stepClear = new MatchClearAction(
            clearTiles,
            doShake: true,
            animationMode: ClearAnimationMode.LightningStrike,
            affectedCells: affectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: visualTargets,
            lightningLineStrikes: strikes,
            isSpecialPhase: true);

        // Beam arrival → launch the special's own chain right then (concurrent with
        // the rest of the sweep), same pattern as DrainDeferredLineOverrides.
        var firedArrivals = new HashSet<TileView>();
        foreach (var spTile in arrivalSpecials)
        {
            var captured = spTile;
            stepClear.AddArrivalTrigger(new Vector2Int(captured.X, captured.Y), () =>
            {
                if (firedArrivals.Add(captured))
                    LaunchArrivalSubChain(captured);
            });
        }

        yield return stepClear.ExecuteVisuals(sequencer);

        // Fallback: if the strike VFX couldn't play, arrival callbacks never fire —
        // the special must still trigger rather than silently survive.
        foreach (var spTile in arrivalSpecials)
            if (firedArrivals.Add(spTile))
                LaunchArrivalSubChain(spTile);

        // 3) Gravity + overlap.
        yield return RunGravityWithOverlap(queue.Count > 0);
    }

    // Line+Pulse FUSED emisyonu (karar #3): 3 sütun + 3 satırlık haç, gerçek arrival-driven.
    // Hücre mantığı SweepLine ile AYNI; haçtaki special VARDIĞI AN eşzamanlı alt-zincir olur
    // (LaunchArrivalSubChain = background job = paralel). İki special emisyonunun kompozisyonu
    // DEĞİL — tek emisyon.
    private IEnumerator SweepCross(
        ActionSequencer sequencer, Vector2Int center, HashSet<TileView> processed)
    {
        int cx = center.x, cy = center.y;

        var clearTiles = new HashSet<TileView>();
        var visualTargets = new List<TileView>();
        var affectedCells = new HashSet<Vector2Int>();
        var arrivalSpecials = new List<TileView>();
        var strikes = new List<LightningLineStrike>();

        // Combo'nun kendi origin'leri: zincirlenmesin, tüketilsin (clear). processed'a
        // eklenince CollectCell onları arrival-special saymaz; clearTiles'ta zaten temizlenir.
        if (consumeTiles != null)
            foreach (var ct in consumeTiles)
                if (ct != null && ct) { processed.Add(ct); clearTiles.Add(ct); }

        void CollectCell(int x, int y)
        {
            if (IsProtectedCell(x, y)) return; // swap'ta yeni oluşan special'a dokunma
            if (!SpecialUtils.CanAffectCell(board, x, y)) return;

            var cell = new Vector2Int(x, y);
            if (!affectedCells.Add(cell)) return; // satır+sütun kesişimi: hücre bir kez

            var tile = board.Tiles[x, y];
            if (tile == null) return;

            // Movable obstacle tile-clear yoluna girmez (SweepLine'daki orphan koruması).
            if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(x, y))
                return;

            if (tile.GetSpecial() != TileSpecial.None)
            {
                if (processed.Contains(tile)) return;
                processed.Add(tile);
                AnchorQueued(tile, x, y);   // patlayana kadar yerinde dursun
                arrivalSpecials.Add(tile);
                return;
            }

            clearTiles.Add(tile);
            visualTargets.Add(tile);
        }

        for (int x = cx - crossHalf; x <= cx + crossHalf; x++)
        {
            if (x < 0 || x >= board.Width) continue;
            for (int y = 0; y < board.Height; y++) CollectCell(x, y);
            strikes.Add(new LightningLineStrike(new Vector2Int(x, cy), false));
        }
        for (int y = cy - crossHalf; y <= cy + crossHalf; y++)
        {
            if (y < 0 || y >= board.Height) continue;
            for (int x = 0; x < board.Width; x++) CollectCell(x, y);
            strikes.Add(new LightningLineStrike(new Vector2Int(cx, y), true));
        }

        var stepClear = new MatchClearAction(
            clearTiles,
            doShake: true,
            animationMode: ClearAnimationMode.LightningStrike,
            affectedCells: affectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: visualTargets,
            lightningLineStrikes: strikes,
            isSpecialPhase: true);

        // Beam varışı → special'ın kendi zincirini O AN başlat (sweep'in geri kalanıyla eşzamanlı).
        var fired = new HashSet<TileView>();
        foreach (var spTile in arrivalSpecials)
        {
            var captured = spTile;
            stepClear.AddArrivalTrigger(new Vector2Int(captured.X, captured.Y), () =>
            {
                if (fired.Add(captured))
                    LaunchArrivalSubChain(captured);
            });
        }

        yield return stepClear.ExecuteVisuals(sequencer);

        // Fallback: strike VFX oynamazsa arrival callback gelmez → yine de tetikle.
        foreach (var spTile in arrivalSpecials)
            if (fired.Add(spTile))
                LaunchArrivalSubChain(spTile);

        yield return RunGravityWithOverlap(hasNext: false);
    }

    // Override+Pulse / PatchBot+Pulse: TÜM merkezler AYNI ANDA patlar. Tek MatchClearAction tüm
    // merkezlerin alan birleşimini temizler; her hücrenin gecikmesi en yakın merkeze uzaklık × step
    // (radyal dalga). Yer değiştirme yok — gravity sadece en sonda bir kez. Alandaki special'lar
    // dalga hücreye varınca arrival-trigger ile tetiklenir (LaunchArrivalSubChain = aynı dispatch).
    // centersArePulseTiles: true → merkezde gerçek PulseCore tile'ı aranır ve tüketilir
    // (Override+Pulse implant); false → merkez sanal patlama noktası (PatchBot payload,
    // tile yok), merkez hücredeki special de zincirlenir.
    private IEnumerator ExplodePulsesSimultaneously(
        ActionSequencer sequencer, HashSet<TileView> processed,
        List<Vector2Int> rawCenters, bool centersArePulseTiles)
    {
        var centers = new HashSet<Vector2Int>();
        foreach (var cell in rawCenters)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) continue;
            if (centersArePulseTiles)
            {
                var t = board.Tiles[cell.x, cell.y];
                if (t == null || t.GetSpecial() != TileSpecial.PulseCore) continue;
            }
            centers.Add(cell);
        }
        if (centers.Count == 0)
            yield break;

        float NearestCenterDist(int x, int y)
        {
            int best = int.MaxValue;
            foreach (var c in centers)
            {
                int d = Mathf.Max(Mathf.Abs(c.x - x), Mathf.Abs(c.y - y));
                if (d < best) best = d;
            }
            return best;
        }

        var clearTiles = new HashSet<TileView>();
        var affectedCells = new HashSet<Vector2Int>();
        var impactCells = new List<Vector2Int>();
        var ringArrivals = new Dictionary<int, List<TileView>>();

        // Tüm dalgalar aynı anda aynı hızla büyür → TEK merkezin gerçek yarıçap
        // callback'i hepsini temsil eder (halka = en yakın merkeze uzaklık).
        float waveRadiusPx = 0f;
        bool callbackWired = false;
        foreach (var c in centers)
        {
            if (!callbackWired)
            {
                callbackWired = true;
                board.PulseCoreImpactService?.PlayPulseCoreExplosionVfxAtCell(
                    c.x, c.y, radiusCells: areaHalf,
                    onRadiusPx: px => waveRadiusPx = Mathf.Max(waveRadiusPx, px));
            }
            else
            {
                board.PulseCoreImpactService?.PlayPulseCoreExplosionVfxAtCell(c.x, c.y, radiusCells: areaHalf);
            }

            for (int x = c.x - areaHalf; x <= c.x + areaHalf; x++)
            for (int y = c.y - areaHalf; y <= c.y + areaHalf; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;

                if (!SpecialUtils.CanAffectCell(board, x, y))
                {
                    SpecialCellUtils.TryAddObstacleImpact(board, x, y, impactCells);
                    continue;
                }

                var cell = new Vector2Int(x, y);
                if (!affectedCells.Add(cell)) continue; // alan birleşimi: hücre bir kez

                if (SpecialCellUtils.TryRouteMovableToImpact(board, x, y, impactCells)) continue;

                impactCells.Add(cell);

                var tile = board.Tiles[x, y];
                if (tile == null) continue;

                // Merkez olmayan bir special → zincirlenir: dalga hücreye VARINCA tetiklenir.
                // (Sanal merkezde tile tüketilmez; merkez hücredeki special de zincirlenir.)
                if ((!centersArePulseTiles || !centers.Contains(cell))
                    && tile.GetSpecial() != TileSpecial.None && !processed.Contains(tile))
                {
                    processed.Add(tile);
                    AnchorQueued(tile, x, y);
                    int ring = (int)NearestCenterDist(x, y);
                    if (!ringArrivals.TryGetValue(ring, out var listA)) ringArrivals[ring] = listA = new List<TileView>();
                    listA.Add(tile);
                    continue;
                }

                clearTiles.Add(tile);
            }
        }

        // HALKA-BAZLI ARRIVAL CLEAR: dalga halkaya varınca taşlar kırılır, o halkadaki
        // special'lar o an alt-zincir olur (timer fallback dahil — akış asla takılmaz).
        yield return RunRadialArrivalClear(
            sequencer,
            cell => (int)NearestCenterDist(cell.x, cell.y),
            () => waveRadiusPx,
            clearTiles, affectedCells, impactCells,
            arrivalSpecials: ringArrivals);

        yield return RunGravityWithOverlap(hasNext: false);
    }

    // Override+Line / PatchBot+Line: TÜM line'ları AYNI ANDA süpürür. Her LineH kendi satırını,
    // her LineV kendi sütununu temizler (tek MatchClearAction, LightningStrike). Yol üstündeki
    // special'lar dalga varınca arrival ile tetiklenir (LaunchArrivalSubChain = aynı dispatch).
    // linesAreTiles: true → simultaneousLineCells'ten gerçek Line tile'ları okunur ve tüketilir
    // (Override+Line implant); false → virtualLineSweeps'ten sanal beam'ler (PatchBot payload,
    // origin'de tile yok), origin hücredeki special de zincirlenir.
    private IEnumerator SweepLinesSimultaneously(ActionSequencer sequencer, HashSet<TileView> processed, bool linesAreTiles)
    {
        var lines = new List<(Vector2Int cell, bool horizontal)>();
        var lineCellSet = new HashSet<Vector2Int>();
        if (linesAreTiles)
        {
            foreach (var cell in simultaneousLineCells)
            {
                if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) continue;
                var t = board.Tiles[cell.x, cell.y];
                if (t == null) continue;
                var sp = t.GetSpecial();
                if (sp != TileSpecial.LineH && sp != TileSpecial.LineV) continue;
                lines.Add((cell, sp == TileSpecial.LineH));
                lineCellSet.Add(cell);
            }
        }
        else
        {
            foreach (var strike in virtualLineSweeps)
            {
                var cell = strike.originCell;
                if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) continue;
                lines.Add((cell, strike.isHorizontal));
                // lineCellSet boş kalır: origin'de tile yok; orada special varsa zincirlenir.
            }
        }
        if (lines.Count == 0)
            yield break;

        var clearTiles = new HashSet<TileView>();
        var visualTargets = new List<TileView>();
        var affectedCells = new HashSet<Vector2Int>();
        var arrivalSpecials = new List<TileView>();
        var strikes = new List<LightningLineStrike>();

        void CollectCell(int x, int y)
        {
            if (!SpecialUtils.CanAffectCell(board, x, y)) return; // blocker'lar lightning sweep ile vurulur
            var cell = new Vector2Int(x, y);
            if (!affectedCells.Add(cell)) return; // satır/sütun kesişimi: hücre bir kez

            var tile = board.Tiles[x, y];
            if (tile == null) return;
            if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(x, y)) return;

            // Implant line'ın KENDİSİ → temizlenir (tüketilir). Başka special → arrival ile zincir.
            if (!lineCellSet.Contains(cell) && tile.GetSpecial() != TileSpecial.None && !processed.Contains(tile))
            {
                processed.Add(tile);
                AnchorQueued(tile, x, y);
                arrivalSpecials.Add(tile);
                return;
            }

            clearTiles.Add(tile);
            visualTargets.Add(tile);
        }

        foreach (var (cell, horizontal) in lines)
        {
            if (horizontal)
                for (int x = 0; x < board.Width; x++) CollectCell(x, cell.y);
            else
                for (int y = 0; y < board.Height; y++) CollectCell(cell.x, y);

            strikes.Add(new LightningLineStrike(cell, horizontal));
        }

        var stepClear = new MatchClearAction(
            clearTiles,
            doShake: true,
            animationMode: ClearAnimationMode.LightningStrike,
            affectedCells: affectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: visualTargets,
            lightningLineStrikes: strikes,
            isSpecialPhase: true);

        var fired = new HashSet<TileView>();
        foreach (var spTile in arrivalSpecials)
        {
            var captured = spTile;
            stepClear.AddArrivalTrigger(new Vector2Int(captured.X, captured.Y), () =>
            {
                if (fired.Add(captured))
                    LaunchArrivalSubChain(captured);
            });
        }

        yield return stepClear.ExecuteVisuals(sequencer);

        foreach (var spTile in arrivalSpecials)
            if (fired.Add(spTile))
                LaunchArrivalSubChain(spTile);

        yield return RunGravityWithOverlap(hasNext: false);
    }

    // Override+Override: board'daki KARIŞIK special'ları AYNI ANDA ateşler. Hepsi önce anchor'lanır
    // (tüm-board clear'ı sırasında düşmesinler), sonra her biri PARALEL sub-chain olarak başlatılır
    // (LaunchArrivalSubChain → tipine göre ExplodePulse/SweepLine/.Execute). Normal taşları ayrı
    // tüm-board clear temizler (special tile'lar o clear'dan hariç tutulur; çift-temizleme zaten
    // güvenli). Root final settle anchor'ları bırakıp sub-chain'leri bekler.
    private IEnumerator FireMixedCellsSimultaneously(HashSet<TileView> processed)
    {
        // Anchor'ı çağıran combo PendingTriggeredSpecialScopeAction ile yapıyor (whole-board clear
        // boyunca düşmesinler). Burada sadece hepsini paralel sub-chain olarak ateşliyoruz; her
        // sub-chain kendi tipine göre dispatch eder (ExplodePulse/SweepLine/.Execute).
        foreach (var cell in simultaneousMixedCells)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) continue;
            var t = board.Tiles[cell.x, cell.y];
            if (t == null || t.GetSpecial() == TileSpecial.None || processed.Contains(t)) continue;

            processed.Add(t);
            LaunchArrivalSubChain(t);
        }

        yield break; // sub-chain'leri bekleme root final settle'da (ActiveBackgroundJobs)
    }

    // Runs a chained special as its own SpecialChainRunner in the background
    // (ActiveBackgroundJobs guarded; the root's final wait covers it). It shares the
    // root's anchor registry, released once the whole chain settles.
    private void LaunchArrivalSubChain(TileView tile)
    {
        if (tile == null || !tile) return;
        board.StartImmediateActionSequence(new List<BoardAction>
        {
            new SpecialChainRunner(board, new List<TileView> { tile }, areaHalf, catchOverlap, resolveOtherSpecial, root)
        });
    }

    private void AnchorQueued(TileView tile, int x, int y)
    {
        if (tile is null || root.anchoredCells.ContainsKey(tile)) return;
        var cell = new Vector2Int(x, y);
        root.anchoredCells[tile] = cell;
        root.anchorCellBuffer[0] = cell;
        board.SetPendingTriggeredSpecialCells(root.anchorCellBuffer);
    }

    // "is null" on purpose: a destroyed-but-referenced TileView must still release
    // its cell, and Unity's overloaded == would report it as null.
    private void ReleaseAnchor(TileView tile)
    {
        if (tile is null || !root.anchoredCells.TryGetValue(tile, out var cell)) return;
        root.anchoredCells.Remove(tile);
        root.anchorCellBuffer[0] = cell;
        board.ClearPendingTriggeredSpecialCells(root.anchorCellBuffer);
    }

    private bool ReleaseAllAnchors()
    {
        if (root.anchoredCells.Count == 0) return false;
        board.ClearPendingTriggeredSpecialCells(root.anchoredCells.Values);
        root.anchoredCells.Clear();
        return true;
    }

    // Runs CalculateCascades (board.Tiles updated synchronously) as background fall
    // job(s), then waits catchOverlap × fallDuration so the next step can catch the
    // still-falling tiles.
    private IEnumerator RunGravityWithOverlap(bool hasNext)
    {
        float fallDuration = 0f;
        var cascades = board.CascadeLogic.CalculateCascades();
        if (cascades != null && cascades.Count > 0)
        {
            for (int i = 0; i < cascades.Count; i++)
            {
                if (cascades[i] is FallAction fa)
                    fallDuration = Mathf.Max(fallDuration, fa.GetEstimatedVisualDuration(board));
                board.StartImmediateAction(cascades[i]);
            }
        }
        board.RefreshAllSortingOrders();

        if (hasNext && fallDuration > 0f && catchOverlap > 0f)
            yield return new WaitForSeconds(fallDuration * catchOverlap);
    }
}
