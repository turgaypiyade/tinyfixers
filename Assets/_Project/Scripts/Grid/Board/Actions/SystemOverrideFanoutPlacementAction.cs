using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SystemOverrideFanoutPlacementAction : BoardAction
{
    private const float PersistentBeamFadeOutSeconds = 0.045f;
    private const float ImplantBreathPeakScale = 1.16f;
    private const float ImplantBreathCycleSeconds = 0.46f;
    // (Eski sway sabitleri ImplantBreathTiltDegrees/WigglePixels/BobPixels kaldırıldı — marking artık sadece nefes.)
    private const float ImplantHighlightAlpha = 0.66f;   // halo parlaklığı (çok az düşürüldü)
    private const float ImplantHighlightScale = 1.25f;   // halo genişliği (silüetten dışa taşma)
    private const float ImplantHighlightPulseScale = 1.16f;   // nefeste daha belirgin parla/genişle
    // Fanout toplam ritmi: az hedefte ışınlar daha uzun okunur, çok hedefte sekans sıkışır.
    private const int OverrideMaxVisibleBeams = 3;
    private const float OverrideFanoutTargetSeconds = 0.75f;
    private const float OverrideBeamMinLifeSeconds = 0.052f;
    private const float OverrideBeamMaxLifeSeconds = 0.180f;
    // "Yanan değnek / floresan tüp": eş-merkezli katmanlar — geniş yumuşak parıltı + sıcak parlak
    // çekirdek. Genişlik = prefab taban kalınlığının çarpanı; alpha = katman opaklığı.
    private const float OverrideGlowOuterTailWidth = 2.3f;
    private const float OverrideGlowOuterHeadWidth = 2.65f;
    private const float OverrideGlowOuterAlpha = 0.26f;
    private const float OverrideGlowMidTailWidth = 1.45f;
    private const float OverrideGlowMidHeadWidth = 1.7f;
    private const float OverrideGlowMidAlpha = 0.58f;
    private const float OverrideCoreTailWidth = 0.98f;
    private const float OverrideCoreHeadWidth = 1.2f;
    private const float OverrideCoreAlpha = 0.98f;
    // Çubuk (rod) DÜZ kalır (yanan değnek düz; kaynayan his alpha flicker'dan). Kenar çizgileri
    // ise hafif tırtıklı — aşağıdaki OverrideEdgeLineJaggedness.
    private const float OverrideRodJaggedness = 0f;
    // Partner renkli KESKİN kenar çizgileri (rim): kalın çubuğun iki YANINA oturur, net partner-renk.
    // Offset kalın gövdenin dışına taşacak kadar; teller de dolgun (gövdeye gömülmesin).
    private const float OverrideEdgeSideOffsetCells = 0.105f;
    private const float OverrideEdgeLineTailWidth = 0.42f;
    private const float OverrideEdgeLineHeadWidth = 0.50f;
    private const float OverrideEdgeLineAlpha = 0.95f;
    // Kenar (rim) çizgileri düz değil, hafif TIRTIKLI olsun (canlı/elektrik hissi). Rod düz kalır.
    private const float OverrideEdgeLineJaggedness = 0.11f;
    // TEK çizgi yaklaşımı: ayrı offset kenar çizgileri yerine, çizginin partner-renkli katmanlarının
    // KENARINA "edge shimmer" (ışık gibi pürüzlü/çok ufak tırtıklı kenar) uygulanır → tek çizgi,
    // partner renkli tırtıklı kenar, boşluk yok. amount küçük = çok ufak tırtık.
    private const float OverrideEdgeShimmerAmount = 0.06f;
    private const float OverrideEdgeShimmerFrequency = 26f;
    private const float OverrideEdgeShimmerSpeed = 11f;
    // Lazerin origin→target UZAMA süresi (tail origin'de kalır, head hedefe uzanır → tam bağlı ışın).
    private const float OverrideBeamReachSeconds = 0.058f;
    // Taşa değince tam-bağlı ışının kısa bekleyişi (çarpma parlaması otursun, sonra kopsun).
    private const float OverrideBeamReachHoldSeconds = 0.045f;
    // Kopuş süresi: kuyruk origin'den ayrılıp hedefe çekilerek söner (override bağı kopar).
    private const float OverrideBeamDetachSeconds = 0.055f;
    private const float OverrideBeamOverlapFadeLeadSeconds = 0.040f;
    private const float MinTargetPlacementBeatSeconds = 0.015f;
    private const float PostPlacementBreathHoldSeconds = 0.22f;
    // Origin override fanout sırasında "kanallama" hissi: origin taşın görseli tahtadan hafif YUKARI
    // kalkar ve ışınlar/yerleşim boyunca yukarıda durur. DÖNME, taşın kendi idle/creation spin'idir
    // (OverrideSpecialView.PlayCreation) — burada tetiklenir, ayrı bir Z-spin UYGULANMAZ.
    private const float OriginLiftCells = 0.16f;        // tile boyutunun oranı = yukarı kalkma miktarı
    private const float OriginLiftRiseSeconds = 0.14f;  // kalkış easing süresi
    private const float OriginBobPixels = 0.05f;        // yukarıdayken hafif salınım (tile oranı)
    private static Sprite laserImpactSprite;
    // Kaynak ikon sprite'ı → beyaz silüet (RGB=beyaz, alpha=ikon) cache'i. Halo bunu kullanır.
    private static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> whiteSilhouetteCache
        = new System.Collections.Generic.Dictionary<Sprite, Sprite>();
    private static readonly Color OverrideLaserCoreColor = new Color(0.20f, 0.82f, 1f, 0.96f);
    // Çubuğun sıcak parlak çekirdeği: beyaza yakın (floresan/kor his); dış parıltı partner rengi.
    private static readonly Color OverrideRodCoreColor = new Color(0.86f, 0.98f, 1f, 1f);

    private readonly BoardController board;
    private readonly Vector2Int origin;
    private readonly List<Vector2Int> targets;
    private readonly List<Vector2Int> deferredPulseExplosionCells;
    private readonly List<Vector2Int> deferredPatchBotCells;
    private readonly System.Action<List<Vector2Int>> launchPatchBots;

    public SystemOverrideFanoutPlacementAction(
        BoardController board,
        Vector2Int origin,
        List<Vector2Int> targets,
        bool doPulse,
        List<Vector2Int> deferredPulseExplosionCells = null,
        List<Vector2Int> deferredPatchBotCells = null,
        System.Action<List<Vector2Int>> launchPatchBots = null)
    {
        this.board = board;
        this.origin = origin;
        this.targets = targets;
        this.deferredPulseExplosionCells = deferredPulseExplosionCells ?? new List<Vector2Int>();
        this.deferredPatchBotCells = deferredPatchBotCells ?? new List<Vector2Int>();
        this.launchPatchBots = launchPatchBots;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (targets == null || targets.Count == 0)
            yield break;

        TileView originTile = null;
        if (origin.x >= 0 && origin.x < board.Width && origin.y >= 0 && origin.y < board.Height)
            originTile = board.Tiles[origin.x, origin.y];

        var implantBreaths = new List<ImplantBreathHandle>();

        // Origin override: ışınlar çıkarken tahtadan hafif yukarı kalkıp döner (kanallama).
        var originLift = StartOriginLift(originTile);

        // Geçerli hedefleri süz.
        var validTargets = new List<Vector2Int>(targets.Count);
        foreach (var pos in targets)
        {
            if (pos.x < 0 || pos.x >= board.Width || pos.y < 0 || pos.y >= board.Height)
                continue;
            if (board.Tiles[pos.x, pos.y] == null)
                continue;
            validTargets.Add(pos);
        }

        var beamTiming = BuildBeamTiming(validTargets.Count);
        var pending = new int[1];

        for (int i = 0; i < validTargets.Count; i++)
        {
            while (pending[0] >= beamTiming.MaxConcurrent)
                yield return null;

            pending[0]++;
            board.StartCoroutine(RunAndSignal(
                CoFireBeamAtTarget(originTile, validTargets[i], implantBreaths, beamTiming), pending));

            if (beamTiming.LaunchIntervalSeconds > 0f && i < validTargets.Count - 1)
            {
                float wait = board.ApplySpecialChainTempo(beamTiming.LaunchIntervalSeconds);
                if (wait > 0f)
                    yield return new WaitForSeconds(wait);
            }
        }

        while (pending[0] > 0)
            yield return null;

        yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.002f));
        if (beamTiming.PostPlacementFadeLeadSeconds > 0f)
            yield return new WaitForSeconds(board.ApplySpecialChainTempo(beamTiming.PostPlacementFadeLeadSeconds));

        if (implantBreaths.Count > 0)
        {
            float hold = board.ApplySpecialChainTempo(beamTiming.PostPlacementBreathHoldSeconds);
            if (hold > 0f)
                yield return new WaitForSeconds(hold);
        }

        StopImplantBreaths(implantBreaths);

        if (deferredPulseExplosionCells != null && deferredPulseExplosionCells.Count > 0)
        {
            // Yerleşim bitti → tetikleme başlıyor: tam 1 vuruşluk nefes (okunabilir geçiş).
            yield return new WaitForSeconds(board.CellTime);

            for (int i = 0; i < deferredPulseExplosionCells.Count; i++)
            {
                var cell = deferredPulseExplosionCells[i];

                if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                    continue;

                var tile = board.Tiles[cell.x, cell.y];
                if (tile == null)
                    continue;

                // Sadece görsel patlama — chain tetikleme OverrideSpecializedCombo
                // tarafından PulseCoreSpecial üzerinden zaten yapıldı.
                PlayPulseCoreExplosionVfx(tile);

                // Patlamalar arası da metronom (¾ vuruş) — yerleşimle aynı ritim.
                yield return new WaitForSeconds(board.CellTime * 0.75f);
            }
        }

        if (deferredPatchBotCells != null && deferredPatchBotCells.Count > 0)
        {
            if (launchPatchBots != null)
            {
                // PatchBotSpecial.Execute akışını kullan — yeni animasyonlar (blade spinner,
                // body separation, afterimage) PatchbotDashUI üzerinden otomatik çalışır.
                launchPatchBots(deferredPatchBotCells);
                // Uçuşlar arka planda (PatchbotDashUI background dash) çalışır;
                // board resolve sonrası cascade'i PatchBotSpecial arrival callback'i tetikler.
            }
            else
            {
                // Fallback: eski toplu uçuş.
                var airborne = new OverridePatchBotAirborneGroupAction(board, deferredPatchBotCells);
                yield return airborne.ExecuteVisuals(sequencer);
            }
        }

        // Kanallama biter → görseli sıfırla, sonra tile'ı gizle.
        StopOriginLift(originLift);

        if (originTile != null)
            SpecialVisualService.HideTileVisualForCombo(originTile);
    }

    private sealed class BeamTiming
    {
        public int MaxConcurrent;
        public float LaunchIntervalSeconds;
        public float ReachSeconds;
        public float ReachHoldSeconds;
        public float DetachSeconds;
        public float OverlapFadeLeadSeconds;
        public float PostPlacementFadeLeadSeconds;
        public float PostPlacementBreathHoldSeconds;
    }

    private static BeamTiming BuildBeamTiming(int targetCount)
    {
        int count = Mathf.Max(1, targetCount);
        int maxConcurrent = Mathf.Clamp(OverrideMaxVisibleBeams, 1, 8);
        float launchInterval = count <= 1 ? 0f : OverrideFanoutTargetSeconds / Mathf.Max(1, count - 1);
        float life = Mathf.Clamp(
            launchInterval > 0f ? launchInterval * maxConcurrent * 0.92f : OverrideBeamMaxLifeSeconds,
            OverrideBeamMinLifeSeconds,
            OverrideBeamMaxLifeSeconds);

        return new BeamTiming
        {
            MaxConcurrent = maxConcurrent,
            LaunchIntervalSeconds = launchInterval,
            ReachSeconds = life * 0.44f,
            ReachHoldSeconds = life * 0.22f,
            DetachSeconds = life * 0.40f,
            OverlapFadeLeadSeconds = Mathf.Min(OverrideBeamOverlapFadeLeadSeconds, life * 0.34f),
            PostPlacementFadeLeadSeconds = Mathf.Min(OverrideBeamOverlapFadeLeadSeconds, life * 0.25f),
            PostPlacementBreathHoldSeconds = Mathf.Lerp(0.10f, PostPlacementBreathHoldSeconds, Mathf.Clamp01(6f / count))
        };
    }

    // Inner coroutine bitince aktif ışın sayacını düşürür (sliding window bekleyebilmek için).
    private IEnumerator RunAndSignal(IEnumerator inner, int[] pending)
    {
        yield return inner;
        pending[0]--;
    }

    // Tek bir hedefe ışın: uzat → taşa değ (kıvılcım + special) → kısa bekle → kop.
    private IEnumerator CoFireBeamAtTarget(
        TileView originTile,
        Vector2Int pos,
        List<ImplantBreathHandle> implantBreaths,
        BeamTiming timing)
    {
        TileView target = board.Tiles[pos.x, pos.y];
        if (target == null)
            yield break;

        Vector3 lastOriginWorld = ResolveTrackedWorldCenter(originTile, origin, Vector3.zero);
        Vector3 lastTargetWorld = ResolveTrackedWorldCenter(target, pos, Vector3.zero);

        System.Func<Vector3> originProvider = () =>
        {
            lastOriginWorld = ResolveTrackedWorldCenter(originTile, origin, lastOriginWorld);
            return lastOriginWorld;
        };

        System.Func<Vector3> targetProvider = () =>
        {
            TileView liveTarget = board.Tiles[pos.x, pos.y] != null
                ? board.Tiles[pos.x, pos.y]
                : target;

            lastTargetWorld = ResolveTrackedWorldCenter(liveTarget, pos, lastTargetWorld);
            return lastTargetWorld;
        };

        var targetBeams = new List<LightningBeam>(5);
        Color edgeColor = ResolveBeamEdgeColor(target);

        // Lazer önce taşa UZANIR (tail origin'de bağlı, head hedefe), taşa değince kuyruk KOPAR.
        var bolt = new BoltProgress();
        System.Func<Vector3> tailProvider = () =>
            Vector3.LerpUnclamped(originProvider(), targetProvider(), Mathf.Clamp01(bolt.Tail));
        System.Func<Vector3> headProvider = () =>
            Vector3.LerpUnclamped(originProvider(), targetProvider(), Mathf.Clamp01(bolt.Head));

        // TEK çizgi: eş-merkezli katmanlar — partner renkli dış+orta parıltı (kenar = partner renk) →
        // sıcak beyaz çekirdek. Ayrı offset kenar çizgisi YOK (boşluk sorunu biter). Partner katmanların
        // KENARI "edge shimmer" ile çok ufak tırtıklı olur; beyaz çekirdek pürüzsüz kalır.
        AddOverrideRodBeam(targetBeams, tailProvider, headProvider, edgeColor,
            OverrideGlowOuterAlpha, OverrideGlowOuterTailWidth, OverrideGlowOuterHeadWidth,
            OverrideEdgeShimmerAmount);
        AddOverrideRodBeam(targetBeams, tailProvider, headProvider, edgeColor,
            OverrideGlowMidAlpha, OverrideGlowMidTailWidth, OverrideGlowMidHeadWidth,
            OverrideEdgeShimmerAmount);
        AddOverrideRodBeam(targetBeams, tailProvider, headProvider, OverrideRodCoreColor,
            OverrideCoreAlpha, OverrideCoreTailWidth, OverrideCoreHeadWidth,
            0f);

        // 1) UZAMA — ışın origin'den taşa kadar uzanır (tam bağlı).
        float reachSeconds = timing != null ? timing.ReachSeconds : OverrideBeamReachSeconds;
        yield return ExtendBolt(bolt, board.ApplySpecialChainTempo(reachSeconds));

        // 2) TAŞA DEĞDİ — çarpma kıvılcımı + su sıçraması + special yerleşir.
        PlayOverrideLaserImpact(targetProvider(), edgeColor);
        board.SyncTileData(target.X, target.Y);
        target.RefreshIcon();
        StartImplantBreath(target, implantBreaths);

        // Kısa bekleyiş: tam-bağlı çubuk bir an görünsün, çarpma otursun.
        float reachHoldSeconds = timing != null ? timing.ReachHoldSeconds : OverrideBeamReachHoldSeconds;
        yield return new WaitForSeconds(board.ApplySpecialChainTempo(reachHoldSeconds));

        // 3) KOPUŞ — kuyruk origin'den ayrılıp hedefe çekilerek söner.
        float detachSeconds = timing != null ? timing.DetachSeconds : OverrideBeamDetachSeconds;
        float fadeLeadSeconds = timing != null ? timing.OverlapFadeLeadSeconds : OverrideBeamOverlapFadeLeadSeconds;
        float detach = board.ApplySpecialChainTempo(detachSeconds);
        FadeBeams(targetBeams, detach);
        board.StartCoroutine(DetachBolt(bolt, detach));
        yield return new WaitForSeconds(Mathf.Max(
            MinTargetPlacementBeatSeconds,
            detach - board.ApplySpecialChainTempo(fadeLeadSeconds)));
    }

    private sealed class ImplantBreathHandle
    {
        public TileView Tile;
        public Transform[] Visuals;
        public Vector3[] BaseScales;
        public Quaternion[] BaseRotations;
        public Vector3[] BasePositions;
        public Image Highlight;
        public Vector3 HighlightBaseScale;
        public Color HighlightBaseColor;
        public bool ManualGlowActive;
        public float Phase;
        public Coroutine Routine;
    }

    private void StartImplantBreath(TileView tile, List<ImplantBreathHandle> activeBreaths)
    {
        if (tile == null || activeBreaths == null)
            return;

        for (int i = 0; i < activeBreaths.Count; i++)
        {
            if (activeBreaths[i].Tile == tile)
                return;
        }

        Transform[] visuals = GetImplantBreathVisuals(tile);
        if (visuals == null || visuals.Length == 0)
            return;

        var baseScales = new Vector3[visuals.Length];
        var baseRotations = new Quaternion[visuals.Length];
        var basePositions = new Vector3[visuals.Length];
        for (int i = 0; i < visuals.Length; i++)
        {
            baseScales[i] = visuals[i] != null ? visuals[i].localScale : Vector3.one;
            baseRotations[i] = visuals[i] != null ? visuals[i].localRotation : Quaternion.identity;
            basePositions[i] = visuals[i] != null ? visuals[i].localPosition : Vector3.zero;
        }

        var highlight = CreateImplantHighlight(tile);
        BoardIdleHintAndComboGlowController.SetManualSpecialGlow(tile, true, board != null ? board.TileSize : 0);
        var handle = new ImplantBreathHandle
        {
            Tile = tile,
            Visuals = visuals,
            BaseScales = baseScales,
            BaseRotations = baseRotations,
            BasePositions = basePositions,
            Highlight = highlight,
            HighlightBaseScale = highlight != null ? highlight.rectTransform.localScale : Vector3.one,
            HighlightBaseColor = highlight != null ? highlight.color : Color.white,
            ManualGlowActive = true,
            Phase = (tile.X * 0.73f + tile.Y * 1.17f) * Mathf.PI
        };

        handle.Routine = board.StartCoroutine(ImplantBreathRoutine(handle));
        activeBreaths.Add(handle);

        // Pulse implant: marking sırasında PulseCore'un oluşum DÖNME efektini de oynat.
        if (tile.GetSpecial() == TileSpecial.PulseCore)
        {
            tile.SetPulseFuseIntensity(2.35f);
            tile.PlayPulseCreationSpin(board != null ? board.TileSize : 0);
        }
        // LineH/LineV implant: 2 tam tur silindir dönüşü oynatılır (kullanıcı isteği — sallanma yok, 2 tur dönme).
        else if (tile.GetSpecial() == TileSpecial.LineH || tile.GetSpecial() == TileSpecial.LineV)
        {
            tile.PlayLineCreationSpin(tile.GetSpecial(), 0.36f);
        }
    }

    private IEnumerator ImplantBreathRoutine(ImplantBreathHandle handle)
    {
        if (handle == null || handle.Visuals == null || handle.Visuals.Length == 0)
            yield break;

        float cycle = Mathf.Max(0.12f, board.ApplySpecialChainTempo(ImplantBreathCycleSeconds));
        float elapsed = 0f;

        while (handle.Tile != null && HasAnyLiveVisual(handle))
        {
            elapsed += Time.deltaTime;
            float p = (elapsed / cycle) * Mathf.PI * 2f + handle.Phase;
            float breath = (Mathf.Sin(p - Mathf.PI * 0.5f) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, ImplantBreathPeakScale, Smooth01(breath));

            // Marking artık YALNIZCA nefes (scale büyü/küçül). Sağa-sola wiggle + tilt + bob
            // KALDIRILDI (kullanıcı: taş sallanmasın, nefes alıp versin). Rotasyon/pozisyona
            // DOKUNMUYORUZ → override+pulse'ta pulse'ın kendi oluşum dönmesi serbestçe döner + nefes alır.
            ApplyImplantBreathScale(handle, scale);
            ApplyImplantHighlight(handle, Smooth01(breath));
            yield return null;
        }
    }

    private void StopImplantBreaths(List<ImplantBreathHandle> activeBreaths)
    {
        if (activeBreaths == null)
            return;

        for (int i = 0; i < activeBreaths.Count; i++)
        {
            var handle = activeBreaths[i];
            if (handle == null)
                continue;

            if (handle.Routine != null)
                board.StopCoroutine(handle.Routine);

            if (handle.Tile != null && handle.Tile.GetSpecial() == TileSpecial.PulseCore)
                handle.Tile.SetPulseFuseIntensity(1f);

            ResetImplantWiggle(handle);
            DestroyImplantHighlight(handle);
        }

        activeBreaths.Clear();
    }

    // ── Origin override lift + idle-spin (fanout kanallama) ───────────────────
    // Lift = görseli tahtadan hafif yukarı taşır (SADECE localPosition). Dönme = taşın kendi
    // idle/creation spin'i (OverrideSpecialView.PlayCreation) — biz rotation'a DOKUNMAYIZ.
    private sealed class OriginLiftHandle
    {
        public Transform[] Visuals;
        public Vector3[] BasePositions;
        public OverrideSpecialView OverrideView;
        public Coroutine Routine;
    }

    private OriginLiftHandle StartOriginLift(TileView originTile)
    {
        if (originTile == null || board == null)
            return null;

        Transform[] visuals = GetImplantBreathVisuals(originTile);
        if (visuals == null || visuals.Length == 0)
            return null;

        var handle = new OriginLiftHandle
        {
            Visuals = visuals,
            BasePositions = new Vector3[visuals.Length],
            OverrideView = originTile.OverrideSpecialView
        };
        for (int i = 0; i < visuals.Length; i++)
            handle.BasePositions[i] = visuals[i] != null ? visuals[i].localPosition : Vector3.zero;

        // Dönme: taşın idle/creation spin-in animasyonunu tetikle (küre yarıları döner → birleşir).
        handle.OverrideView?.PlayCreation();

        handle.Routine = board.StartCoroutine(OriginLiftRoutine(handle));
        return handle;
    }

    private IEnumerator OriginLiftRoutine(OriginLiftHandle handle)
    {
        if (handle == null || handle.Visuals == null || handle.Visuals.Length == 0)
            yield break;

        float tileSize = board != null && board.TileSize > 0 ? board.TileSize : 96f;
        float liftPixels = tileSize * OriginLiftCells;
        float bobPixels = tileSize * OriginBobPixels;
        float elapsed = 0f;

        // Işınlar/yerleşim sürerken yukarıda kalır; StopOriginLift durdurur. Rotation'a dokunmaz.
        while (HasAnyLiveOriginVisual(handle))
        {
            elapsed += Time.deltaTime;
            float rise = Smooth01(elapsed / Mathf.Max(0.0001f, OriginLiftRiseSeconds));
            float bob = rise * Mathf.Sin(elapsed * 5f) * bobPixels;
            float lift = liftPixels * rise + bob;
            ApplyOriginLift(handle, lift);
            yield return null;
        }
    }

    private static bool HasAnyLiveOriginVisual(OriginLiftHandle handle)
    {
        if (handle == null || handle.Visuals == null)
            return false;
        for (int i = 0; i < handle.Visuals.Length; i++)
            if (handle.Visuals[i] != null)
                return true;
        return false;
    }

    private static void ApplyOriginLift(OriginLiftHandle handle, float liftPixels)
    {
        if (handle == null || handle.Visuals == null)
            return;

        int count = Mathf.Min(handle.Visuals.Length, handle.BasePositions.Length);
        for (int i = 0; i < count; i++)
        {
            var v = handle.Visuals[i];
            if (v == null)
                continue;
            // SADECE yukarı taşı — rotation/scale idle-spin'e (PlayCreation) bırakılır.
            v.localPosition = handle.BasePositions[i] + Vector3.up * liftPixels;
        }
    }

    private void StopOriginLift(OriginLiftHandle handle)
    {
        if (handle == null)
            return;

        if (handle.Routine != null && board != null)
            board.StopCoroutine(handle.Routine);

        // Idle-spin'in kendini tekrar oynatmasını (idleWatch) durdur ki gizlenen taş yeniden belirmesin.
        handle.OverrideView?.Stop();

        int count = handle.Visuals != null ? handle.Visuals.Length : 0;
        for (int i = 0; i < count; i++)
        {
            var v = handle.Visuals[i];
            if (v == null)
                continue;
            v.localPosition = handle.BasePositions[i];
        }
    }

    private Transform[] GetImplantBreathVisuals(TileView tile)
    {
        var visuals = new List<Transform>(3);

        if (tile.IconImage != null)
            visuals.Add(tile.IconImage.transform);

        if (tile.GetSpecial() == TileSpecial.PatchBot && tile.PropellerView != null)
            visuals.Add(tile.PropellerView.transform);

        if (tile.GetSpecial() == TileSpecial.SystemOverride && tile.OverrideSpecialView != null)
            visuals.Add(tile.OverrideSpecialView.transform);

        if (visuals.Count == 0 && tile.transform != null)
            visuals.Add(tile.transform);

        return visuals.ToArray();
    }

    private static bool HasAnyLiveVisual(ImplantBreathHandle handle)
    {
        if (handle == null || handle.Visuals == null)
            return false;

        for (int i = 0; i < handle.Visuals.Length; i++)
        {
            if (handle.Visuals[i] != null)
                return true;
        }

        return false;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // Marking nefesi: YALNIZCA scale uygular. Rotation ve position'a DOKUNMAZ — taşın kendi
    // oluşum animasyonu (örn. pulse creation spin / line 2-tur spin) serbest kalır; taş sağa-sola sallanmaz.
    private static void ApplyImplantBreathScale(ImplantBreathHandle handle, float scaleMultiplier)
    {
        if (handle == null || handle.Visuals == null || handle.BaseScales == null)
            return;

        bool isLineSpinning = handle.Tile != null && handle.Tile.IsLineCreationSpinning;

        int count = Mathf.Min(handle.Visuals.Length, handle.BaseScales.Length);
        for (int i = 0; i < count; i++)
        {
            if (handle.Visuals[i] != null)
            {
                if (isLineSpinning && handle.Tile != null && handle.Tile.IconImage != null && handle.Visuals[i] == handle.Tile.IconImage.transform)
                    continue; // 1.5x pop + 2 tur silindir dönüşünü CoLineCreationSpin yönetsin

                handle.Visuals[i].localScale = handle.BaseScales[i] * scaleMultiplier;
            }
        }
    }

    private Image CreateImplantHighlight(TileView tile)
    {
        if (tile == null || tile.IconImage == null)
            return null;

        var iconRt = tile.IconImage.rectTransform;
        var parent = iconRt.parent as RectTransform;
        if (parent == null)
            return null;

        var go = new GameObject("OverrideImplantHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = iconRt.anchorMin;
        rt.anchorMax = iconRt.anchorMax;
        rt.pivot = iconRt.pivot;
        rt.anchoredPosition = iconRt.anchoredPosition;
        // Şekle-UYGUN halo: taşın KENDİ ikon sprite'ını (silüet) kullan, ikon rect'inden birkaç px
        // büyüt → şeklin arkasından etrafını saran, birkaç piksel taşan bir parıltı. (Kare/çizgi DEĞİL.)
        Vector2 iconSize = iconRt.sizeDelta;
        if (iconSize.x <= 0f || iconSize.y <= 0f)
        {
            float s = board != null && board.TileSize > 0 ? board.TileSize : 96f;
            iconSize = new Vector2(s, s);
        }
        rt.sizeDelta = iconSize * ImplantHighlightScale;
        rt.localRotation = iconRt.localRotation;   // pulse dönerken hizalı (ApplyImplantHighlight her frame günceller)
        rt.localScale = Vector3.one;
        rt.SetSiblingIndex(Mathf.Max(0, iconRt.GetSiblingIndex()));   // ikonun ARKASINDA

        var image = go.GetComponent<Image>();
        // BEYAZ şekle-uygun halo: ikonun RGB'sini beyaza çeken silüet (alpha korunur). Düz ikon
        // sprite'ı + color tint, ikonun renklerini çarptığı için halo TAŞIN RENGİNDE görünüyordu;
        // beyaz silüet kesin beyaz yapar. Texture okunamazsa (Read/Write kapalı) ikona düşer (fallback).
        image.sprite = GetWhiteSilhouette(tile.IconImage.sprite) ?? tile.IconImage.sprite;
        image.preserveAspect = tile.IconImage.preserveAspect;
        image.color = new Color(1f, 1f, 1f, ImplantHighlightAlpha);   // beyaz
        image.raycastTarget = false;
        return image;
    }

    // Kaynak sprite'ın ŞEKLİNİ (alpha) koruyup RGB'yi beyaza çeken silüet sprite üretir (cache'li).
    // Halo bununla ŞEKLE-UYGUN + BEYAZ görünür (ikon renkleri sızmaz; armutsa halo da armut).
    // Texture Read/Write GEREKTİRMEZ: kaynak texture GPU'da RenderTexture'a blit edilip oradan
    // ReadPixels ile okunur (import ayarına dokunmadan çalışır). Başarısızsa null → çağıran ikona düşer.
    private static Sprite GetWhiteSilhouette(Sprite src)
    {
        if (src == null)
            return null;
        if (whiteSilhouetteCache.TryGetValue(src, out var cached))
            return cached;   // null da cache'lenir (tekrar denemeyelim)

        Sprite result = null;
        var tex = src.texture;
        if (tex != null)
        {
            Rect r = src.textureRect;
            int px = Mathf.RoundToInt(r.x), py = Mathf.RoundToInt(r.y);
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            if (w > 0 && h > 0)
            {
                RenderTexture rt = null;
                var prevActive = RenderTexture.active;
                try
                {
                    rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tex, rt);           // kaynağı GPU'da RT'ye kopyala (readable olması şart değil)
                    RenderTexture.active = rt;

                    var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                    {
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };
                    outTex.ReadPixels(new Rect(px, py, w, h), 0, 0);   // RT'den (GPU) oku → readable outTex

                    // ŞEKLİ koru (alpha), RGB'yi beyaza çevir → şekle-uygun BEYAZ silüet.
                    var pixels = outTex.GetPixels();
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = new Color(1f, 1f, 1f, pixels[i].a);
                    outTex.SetPixels(pixels);
                    outTex.Apply(false, true);

                    result = Sprite.Create(outTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), src.pixelsPerUnit);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[OverrideImplantHalo] Beyaz silüet üretilemedi ({src.name}): {e.Message}");
                    result = null;   // fallback (çağıran ikonu kullanır — kare değil, renkli şekil)
                }
                finally
                {
                    RenderTexture.active = prevActive;
                    if (rt != null) RenderTexture.ReleaseTemporary(rt);
                }
            }
        }

        whiteSilhouetteCache[src] = result;
        return result;
    }

    private static void ApplyImplantHighlight(ImplantBreathHandle handle, float breath01)
    {
        if (handle == null || handle.Highlight == null)
            return;

        float scale = Mathf.Lerp(1f, ImplantHighlightPulseScale, breath01);
        if (handle.Tile != null && handle.Tile.IconImage != null && handle.Tile.IsLineCreationSpinning)
        {
            float iconScale = handle.Tile.IconImage.rectTransform.localScale.x;
            handle.Highlight.rectTransform.localScale = handle.HighlightBaseScale * iconScale;
        }
        else
        {
            handle.Highlight.rectTransform.localScale = handle.HighlightBaseScale * scale;
        }

        // Şekle-uygun halo taşın dönmesini (pulse creation spin / line rotation) takip etsin → silüet hizalı kalır.
        if (handle.Tile != null && handle.Tile.IconImage != null)
            handle.Highlight.rectTransform.localRotation = handle.Tile.IconImage.rectTransform.localRotation;

        var color = handle.HighlightBaseColor;
        color.a = Mathf.Lerp(ImplantHighlightAlpha * 0.60f, ImplantHighlightAlpha, breath01);
        handle.Highlight.color = color;
    }

    private void DestroyImplantHighlight(ImplantBreathHandle handle)
    {
        if (handle == null)
            return;

        if (handle.ManualGlowActive)
        {
            BoardIdleHintAndComboGlowController.SetManualSpecialGlow(
                handle.Tile,
                false,
                board != null ? board.TileSize : 0);
            handle.ManualGlowActive = false;
        }

        if (handle.Highlight != null)
        {
            Object.Destroy(handle.Highlight.gameObject);
            handle.Highlight = null;
        }
    }

    private static void ResetImplantWiggle(ImplantBreathHandle handle)
    {
        if (handle == null || handle.Visuals == null || handle.BaseScales == null
            || handle.BaseRotations == null || handle.BasePositions == null)
            return;

        int count = Mathf.Min(handle.Visuals.Length, handle.BaseScales.Length);
        for (int i = 0; i < count; i++)
        {
            // Yalnız scale'i sıfırla; rotation/position'a dokunma (taşın kendi spin'i sürebilir).
            if (handle.Visuals[i] != null)
                handle.Visuals[i].localScale = handle.BaseScales[i];
        }
    }

    private Vector3 ResolveTrackedWorldCenter(TileView preferredTile, Vector2Int cell, Vector3 fallback)
    {
        if (cell.x >= 0 && cell.x < board.Width && cell.y >= 0 && cell.y < board.Height)
        {
            var liveTile = board.Tiles[cell.x, cell.y];
            if (liveTile != null)
                return board.GetTileWorldCenter(liveTile);
        }

        if (preferredTile != null && preferredTile.X == cell.x && preferredTile.Y == cell.y)
            return board.GetTileWorldCenter(preferredTile);

        if (board.Parent != null)
        {
            var local = new Vector3(
                cell.x * board.TileSize + board.TileSize * 0.5f,
                -cell.y * board.TileSize - board.TileSize * 0.5f,
                0f);
            return board.Parent.TransformPoint(local);
        }

        return fallback;
    }

    // Lazer segmentinin origin→target hattı üzerindeki ilerlemesi (0=origin, 1=target).
    // Beam uçları bu iki değere bağlı; coroutine'ler değeri her frame günceller.
    private sealed class BoltProgress
    {
        public float Head;
        public float Tail;
    }

    // UZAMA: tail origin'de SABİT kalır, head hedefe uzanır → origin↔hedef tam bağlı ışın.
    private IEnumerator ExtendBolt(BoltProgress bolt, float duration)
    {
        float d = Mathf.Max(0.0001f, duration);
        float e = 0f;
        bolt.Head = 0f;
        bolt.Tail = 0f;

        while (e < d)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / d);
            bolt.Head = 1f - (1f - k) * (1f - k); // EaseOutQuad → hızlı uzayıp taşta oturur
            bolt.Tail = 0f;                        // kuyruk origin'de bağlı kalır
            yield return null;
        }

        bolt.Head = 1f;
        bolt.Tail = 0f;
    }

    // KOPUŞ: kuyruk origin'den ayrılıp hedefe çekilir (head=1 sabit) → override bağı kopar, söner.
    private IEnumerator DetachBolt(BoltProgress bolt, float duration)
    {
        float d = Mathf.Max(0.0001f, duration);
        float e = 0f;
        float startTail = bolt.Tail;

        while (e < d)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / d);
            bolt.Head = 1f;
            bolt.Tail = Mathf.Lerp(startTail, 1f, k * k); // EaseInQuad → önce yapışık, sonra sıyrılır
            yield return null;
        }

        bolt.Tail = 1f;
    }

    // Eş-merkezli tek çubuk katmanı (yan offset YOK): floresan/yanan değnek görünümü için
    // start→end aynı hat, farklı genişlik+alpha ile üst üste bindirilir.
    private void AddOverrideRodBeam(
        List<LightningBeam> persistentBeams,
        System.Func<Vector3> startProvider,
        System.Func<Vector3> endProvider,
        Color color,
        float alpha,
        float tailWidth,
        float headWidth,
        float edgeShimmerAmount = 0f)
    {
        color.a = alpha;
        var beam = board.BeginPersistentLightning(startProvider, endProvider, color);
        if (beam == null)
            return;

        // Uç SİVRİLMESİN: taşa varan uç dolgun kalsın (prefab'ın ince baseEndWidth'ini kullanma).
        beam.SetNoTaper(true);
        // Çubuk gövdesi DÜZ (jaggedness 0). "Yanma" hissi persistentPulse alpha flicker'ından.
        beam.ConfigureRopeStyle(OverrideRodJaggedness, tailWidth, headWidth);
        // Kenar pürüzü (çok ufak tırtık) — partner katmanlarına uygulanır, beyaz çekirdeğe 0.
        if (edgeShimmerAmount > 0f)
            beam.ConfigureEdgeShimmer(edgeShimmerAmount, OverrideEdgeShimmerFrequency, OverrideEdgeShimmerSpeed);
        beam.SetInnerColoredCoreVisible(false);
        persistentBeams.Add(beam);
    }

    // Çubuğun bir yanında, hattı perpendikülerde sideOffsetCells kadar kaydırılmış ince kenar çizgisi.
    private void AddOverrideEdgeLine(
        List<LightningBeam> persistentBeams,
        System.Func<Vector3> startProvider,
        System.Func<Vector3> endProvider,
        float sideOffsetCells,
        Color color,
        float alpha,
        float tailWidth,
        float headWidth)
    {
        float worldOffset = EstimateWorldTileSize() * sideOffsetCells;
        System.Func<Vector3> start = () => OffsetBeamEndpoint(startProvider(), endProvider(), worldOffset);
        System.Func<Vector3> end = () => OffsetBeamEndpoint(endProvider(), startProvider(), -worldOffset);

        color.a = alpha;
        var beam = board.BeginPersistentLightning(start, end, color);
        if (beam == null)
            return;

        beam.SetNoTaper(true);
        // Kenar çizgileri hafif tırtıklı (rod'dan farklı olarak).
        beam.ConfigureRopeStyle(OverrideEdgeLineJaggedness, tailWidth, headWidth);
        beam.SetInnerColoredCoreVisible(false);
        persistentBeams.Add(beam);
    }

    private Vector3 OffsetBeamEndpoint(Vector3 point, Vector3 other, float offset)
    {
        if (Mathf.Abs(offset) < 0.0001f)
            return point;

        Vector3 delta = other - point;
        delta.z = 0f;
        if (delta.sqrMagnitude < 0.0001f)
            return point;

        Vector3 dir = delta.normalized;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        return point + side * offset;
    }

    private float EstimateWorldTileSize()
    {
        if (board.Parent == null)
            return 0.5f;

        return board.Parent.TransformVector(new Vector3(board.TileSize, 0f, 0f)).magnitude;
    }

    private static void FadeBeams(List<LightningBeam> beams, float duration)
    {
        if (beams == null)
            return;

        for (int i = 0; i < beams.Count; i++)
        {
            if (beams[i] != null)
                beams[i].FadeOutAndDestroy(duration);
        }
    }

    private Color ResolveBeamEdgeColor(TileView target)
    {
        if (target == null)
            return Color.white;

        return Color.Lerp(ColorForTileType(target.GetTileType()), Color.white, 0.48f);
    }

    private static Color ColorForTileType(TileType type)
    {
        switch (type)
        {
            case TileType.Gear:  return new Color(1f, 0.82f, 0.18f, 0.92f);
            case TileType.Core:  return new Color(1f, 0.28f, 0.26f, 0.92f);
            case TileType.Bolt:  return new Color(0.22f, 0.55f, 1f, 0.92f);
            case TileType.Plate: return new Color(0.28f, 0.92f, 0.46f, 0.92f);
            case TileType.Key:   return new Color(0.50f, 0.96f, 1f, 0.92f);
            default:             return Color.white;
        }
    }

    private void PlayOverrideLaserImpact(Vector3 worldPos, Color edgeColor)
    {
        if (board == null)
            return;

        RectTransform parent = board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null
            ? board.BoardVfxPlayer.VfxRoot
            : board.Parent;

        if (parent == null)
            return;

        board.StartCoroutine(CoPlayOverrideLaserImpact(parent, worldPos, edgeColor));
        board.StartCoroutine(CoPlaySplashDroplets(parent, worldPos, edgeColor));
    }

    private sealed class SplashDroplet
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Pos;
        public Vector2 Vel;
    }

    // Lazer taşa değince "suya çarpma" gibi damla sıçraması: uçtan yukarı+dışa fırlayan,
    // yerçekimiyle düşen, sönen küçük partner-renkli damlacıklar (çelenk/crown hissi).
    private IEnumerator CoPlaySplashDroplets(RectTransform parent, Vector3 worldPos, Color color)
    {
        if (parent == null)
            yield break;

        var rootGo = new GameObject("OverrideLaserSplash", typeof(RectTransform));
        var root = rootGo.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        root.anchoredPosition = board != null
            ? board.WorldToAnchoredIn(parent, worldPos)
            : Vector2.zero;
        root.SetAsLastSibling();

        float tileSize = Mathf.Max(24f, board != null ? board.TileSize : 96f);
        const int count = 11;
        var drops = new List<SplashDroplet>(count);
        for (int i = 0; i < count; i++)
        {
            // Yukarı ağırlıklı yelpaze (çelenk): çoğu yukarı+dışa, birkaçı yana savrulur.
            float angleDeg = 90f + UnityEngine.Random.Range(-70f, 70f);
            float speed = tileSize * UnityEngine.Random.Range(2.4f, 5.0f);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 vel = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
            float size = tileSize * UnityEngine.Random.Range(0.06f, 0.14f);

            var img = CreateLaserImpactImage(root, "SplashDrop", color, size);
            var c = color; c.a = 0.95f; img.color = c;

            drops.Add(new SplashDroplet { Rect = img.rectTransform, Image = img, Pos = Vector2.zero, Vel = vel });
        }

        float gravity = tileSize * 12f;
        const float duration = 0.42f;
        float e = 0f;
        while (e < duration)
        {
            if (root == null)
                yield break;

            float dt = Time.deltaTime;
            e += dt;
            float k = Mathf.Clamp01(e / duration);

            for (int i = 0; i < drops.Count; i++)
            {
                var dp = drops[i];
                if (dp.Rect == null || dp.Image == null)
                    continue;

                dp.Vel.y -= gravity * dt;      // yerçekimi (UI'da +y yukarı)
                dp.Pos += dp.Vel * dt;
                dp.Rect.anchoredPosition = dp.Pos;
                dp.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, k);

                var c = dp.Image.color;
                c.a = 0.95f * (1f - k * k);     // sona doğru hızlanan sönüm
                dp.Image.color = c;
            }

            yield return null;
        }

        if (rootGo != null)
            Object.Destroy(rootGo);
    }

    private IEnumerator CoPlayOverrideLaserImpact(RectTransform parent, Vector3 worldPos, Color edgeColor)
    {
        if (parent == null)
            yield break;

        var rootGo = new GameObject("OverrideLaserImpact", typeof(RectTransform));
        var root = rootGo.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        root.anchoredPosition = board != null
            ? board.WorldToAnchoredIn(parent, worldPos)
            : Vector2.zero;
        root.SetAsLastSibling();

        float tileSize = Mathf.Max(24f, board != null ? board.TileSize : 96f);
        var flash = CreateLaserImpactImage(root, "ImpactFlash", OverrideLaserCoreColor, tileSize * 0.72f);
        var core = CreateLaserImpactImage(root, "ImpactCore", Color.white, tileSize * 0.28f);

        var shards = new List<LaserImpactShard>(7);
        for (int i = 0; i < 7; i++)
        {
            float angle = (360f / 7f) * i + UnityEngine.Random.Range(-12f, 12f);
            float distance = tileSize * UnityEngine.Random.Range(0.28f, 0.48f);
            float length = tileSize * UnityEngine.Random.Range(0.18f, 0.28f);
            Color color = i % 2 == 0 ? edgeColor : OverrideLaserCoreColor;
            color.a = 0.94f;

            var img = CreateLaserImpactImage(root, "ImpactShard", color, length);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(length, Mathf.Max(3f, tileSize * 0.035f));
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            shards.Add(new LaserImpactShard
            {
                Rect = rt,
                Image = img,
                End = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)) * distance
            });
        }

        const float duration = 0.18f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (root == null)
                yield break;

            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float outEase = 1f - (1f - k) * (1f - k);
            float alpha = 1f - k;

            if (flash != null)
            {
                flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.35f, outEase);
                var c = OverrideLaserCoreColor;
                c.a = 0.72f * alpha;
                flash.color = c;
            }

            if (core != null)
            {
                core.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.85f, 0.35f, k);
                var c = Color.white;
                c.a = 0.95f * alpha;
                core.color = c;
            }

            for (int i = 0; i < shards.Count; i++)
            {
                var shard = shards[i];
                if (shard.Rect == null || shard.Image == null)
                    continue;

                shard.Rect.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, shard.End, outEase);
                shard.Rect.localScale = new Vector3(Mathf.Lerp(1.15f, 0.35f, k), 1f, 1f);
                var c = shard.Image.color;
                c.a = 0.95f * alpha;
                shard.Image.color = c;
            }

            yield return null;
        }

        if (rootGo != null)
            Object.Destroy(rootGo);
    }

    private static Image CreateLaserImpactImage(RectTransform parent, string name, Color color, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;

        var image = go.GetComponent<Image>();
        image.sprite = GetLaserImpactSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private sealed class LaserImpactShard
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 End;
    }

    private static Sprite GetLaserImpactSprite()
    {
        if (laserImpactSprite != null)
            return laserImpactSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float max = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / max;
                float core = 1f - Mathf.Clamp01(d / 0.32f);
                float glow = 1f - Mathf.Clamp01(d);
                float a = Mathf.Clamp01(core * core + glow * glow * 0.46f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply(false, true);
        laserImpactSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return laserImpactSprite;
    }

    private void PlayPulseCoreExplosionVfx(TileView tile)
    {
        if (tile == null)
            return;

        if (board.PulseCoreImpactService != null)
        {
            board.PulseCoreImpactService.PlayPulseCoreExplosionVfxAtTile(tile, radiusCells: 2);
            return;
        }

        if (board.BoardVfxPlayer != null)
            board.BoardVfxPlayer.PlayPulseVfx(GetTileAnchoredPos(tile), radiusCells: 2, tileSize: board.TileSize);

        if (GameSettings.SoundEnabled && board.SfxSource != null)
        {
            if (board.SfxPulseCoreBoom != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreBoom);
            if (board.SfxPulseCoreWave != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreWave);
        }

        if (board.EnablePulseMicroShake && board.PulseMicroShakeDuration > 0f && board.PulseMicroShakeStrength > 0f)
            board.StartCoroutine(board.boardAnimatorRef.MicroShake(board.PulseMicroShakeDuration, board.PulseMicroShakeStrength));

        PulseBehaviorEvents.EmitPulseExplosionPlayed(new Vector2Int(tile.X, tile.Y));
    }

    private Vector2 GetTileAnchoredPos(TileView tile)
    {
        if (tile == null)
            return Vector2.zero;

        RectTransform tileRect = null;
        try
        {
            tileRect = tile.GetComponent<RectTransform>();
        }
        catch (MissingReferenceException)
        {
            return Vector2.zero;
        }

        if (tileRect == null)
            return Vector2.zero;

        var vfxRoot = board.BoardVfxPlayer != null ? board.BoardVfxPlayer.VfxRoot : null;
        if (vfxRoot != null)
        {
            try
            {
                var worldPos = tileRect.TransformPoint(tileRect.rect.center);
                var localPos = vfxRoot.InverseTransformPoint(worldPos);
                return (Vector2)localPos;
            }
            catch (MissingReferenceException)
            {
                return Vector2.zero;
            }
        }

        var tilesRoot = board.Parent;
        var rootOffset = tilesRoot != null ? tilesRoot.anchoredPosition : Vector2.zero;
        try
        {
            return rootOffset + tileRect.anchoredPosition;
        }
        catch (MissingReferenceException)
        {
            return Vector2.zero;
        }
    }

    private HashSet<TileView> BuildPulseClearSet(
        Vector2Int centerCell,
        HashSet<Vector2Int> futurePulseCells)
    {
        var result = new HashSet<TileView>();
        const int half = 1;

        for (int x = centerCell.x - half; x <= centerCell.x + half; x++)
        {
            for (int y = centerCell.y - half; y <= centerCell.y + half; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
                    continue;

                if (!SpecialUtils.CanAffectCell(board, x, y))
                    continue;

                var cell = new Vector2Int(x, y);
                if (futurePulseCells.Contains(cell))
                    continue;

                var tile = board.Tiles[x, y];
                if (tile == null)
                    continue;

                result.Add(tile);
            }
        }

        return result;
    }
}
