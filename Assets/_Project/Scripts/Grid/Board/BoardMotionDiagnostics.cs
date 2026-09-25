using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Temporary, read-only diagnostics. Calls are omitted from non-development players.
internal static class BoardMotionDiagnostics
{
    // Tek hücrenin tam görsel dökümü: (1) gridde kayıtlı taş ve tüm alt objeleri, (2) üst zincir,
    // (3) tahtada o hücrenin ortasını kaplayan TÜM görseller (çizim sırasına göre) — "altta kaldı" /
    // "üstünü bir şey örttü" ayrımı buradan okunur.
    internal static void DumpCell(BoardController board, Vector2Int cell)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[CellDump] cell=({cell.x},{cell.y}) frame={Time.frameCount}");
        var tile = board.GetTileViewAt(cell.x, cell.y);
        if (tile == null)
        {
            sb.AppendLine("  tile=null (veride boş)");
        }
        else
        {
            sb.AppendLine($"  tile={tile.name} id={tile.GetInstanceID()}/{tile.LifetimeVersion} type={tile.GetTileType()} " +
                $"sp={tile.GetSpecial()} state={tile.RuntimeState} coords=({tile.X},{tile.Y}) hiddenBy={tile.HiddenBy ?? "-"}@{tile.HiddenFrame} " +
                $"held={board.IsCellHeld(cell.x, cell.y)}");
            for (var t = tile.transform.parent; t != null; t = t.parent)
                sb.AppendLine($"  parent {t.name} sibling={t.GetSiblingIndex()} layer={t.gameObject.layer} active={t.gameObject.activeSelf} scale={t.localScale}");
            foreach (var t in tile.GetComponentsInChildren<Transform>(true))
                sb.AppendLine("  node " + DescribeNode(t, tile.transform));
        }

        var root = board.TilesRoot != null ? board.TilesRoot.GetComponentInParent<Canvas>()?.rootCanvas : null;
        Vector3 center = tile != null ? tile.IconImage != null ? tile.IconImage.rectTransform.position : tile.transform.position
            : board.TilesRoot != null ? board.TilesRoot.TransformPoint(new Vector3((cell.x + 0.5f) * board.TileSize, -(cell.y + 0.5f) * board.TileSize, 0f)) : Vector3.zero;
        if (root != null)
        {
            var cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, center);
            sb.AppendLine($"  covering graphics at screen={screen} (hiyerarşi sırası; sonraki üstte çizilir):");
            foreach (var g in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
            {
                if (!g.enabled || !RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, screen, cam)) continue;
                var c = g.canvas;
                sb.AppendLine($"    {Path(g.transform)} color={g.color} alpha={g.canvasRenderer.GetAlpha():F2} " +
                    $"sprite={(g is UnityEngine.UI.Image im && im.sprite != null ? im.sprite.name : "-")} layer={g.gameObject.layer} " +
                    $"canvas={(c != null ? $"{c.name}/ovr={c.overrideSorting}/order={c.sortingOrder}" : "-")} culled={g.canvasRenderer.cull}");
            }
        }
        Debug.Log(sb.ToString());
    }

    private static string DescribeNode(Transform t, Transform root)
    {
        var sb = new System.Text.StringBuilder(Path(t, root));
        sb.Append($" active={t.gameObject.activeSelf} layer={t.gameObject.layer} scale={t.localScale} sibling={t.GetSiblingIndex()}");
        if (t is RectTransform rt) sb.Append($" pos={rt.anchoredPosition} size={rt.rect.size} pivot={rt.pivot}");
        if (t.TryGetComponent<CanvasGroup>(out var cg)) sb.Append($" canvasGroup={cg.alpha:F2}");
        if (t.TryGetComponent<Canvas>(out var cv)) sb.Append($" canvas(enabled={cv.enabled} ovr={cv.overrideSorting} order={cv.sortingOrder})");
        if (t.TryGetComponent<UnityEngine.UI.Graphic>(out var g))
            sb.Append($" graphic(enabled={g.enabled} color={g.color} rAlpha={g.canvasRenderer.GetAlpha():F2} cull={g.canvasRenderer.cull} " +
                $"sprite={(g is UnityEngine.UI.Image im && im.sprite != null ? im.sprite.name : "-")} mat={(g.material != null ? g.material.name : "-")})");
        return sb.ToString();
    }

    private static string Path(Transform t, Transform stopAt = null)
    {
        var path = t.name;
        for (var p = t.parent; p != null && p != stopAt; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    private sealed class Session
    {
        public int id, chains, replans, overlaps, interruptedFalls, lineTimeouts, frameHitches, deferredGravity;
        public float started, maxStill, worstFrameMs;
        public readonly HashSet<int> falls = new();
        public readonly Dictionary<(int id, int lifetime), (Vector2 position, float since)> positions = new();
        // Boş hücre takibi: ne zamandan beri boş, bu sürede tutuldu mu (efekt) — uzun boşluk teşhisi.
        public readonly Dictionary<Vector2Int, (float since, bool held)> empties = new();
        public int longEmpties;
        public float longestEmpty;
    }

    private const float LongEmptySeconds = 0.35f;

    // "Ekranda boş, veride dolu" teşhisi: taşın görünmemesine yol açabilecek HER şeyi dener; hepsi
    // normalse null. (Tek bir alpha/scale kontrolü, iç Canvas / üst CanvasGroup / Y ölçeği gibi
    // yolları kaçırıyordu.)
    private static string DescribeInvisibility(BoardController board, TileView tile, float size)
    {
        var reasons = new List<string>();
        var rt = tile.RectTransform;
        if (!tile.gameObject.activeInHierarchy) reasons.Add("inactive");
        if (rt.parent != board.TilesRoot) reasons.Add($"parent={(rt.parent != null ? rt.parent.name : "null")}");
        if (Mathf.Abs(rt.localScale.x) < 0.2f || Mathf.Abs(rt.localScale.y) < 0.2f) reasons.Add($"scale={rt.localScale.x:F2}x{rt.localScale.y:F2}");
        foreach (var group in tile.GetComponentsInParent<CanvasGroup>(true))
            if (group.alpha < 0.05f) reasons.Add($"canvasGroup({group.name})={group.alpha:F2}");

        var icon = tile.IconImage;
        if (icon == null) { reasons.Add("noIcon"); }
        else
        {
            var irt = icon.rectTransform;
            if (!icon.gameObject.activeInHierarchy) reasons.Add("iconInactive");
            if (!icon.enabled) reasons.Add("iconDisabled");
            if (icon.sprite == null) reasons.Add("iconNoSprite");
            if (icon.color.a < 0.05f) reasons.Add($"iconAlpha={icon.color.a:F2}");
            if (icon.canvasRenderer != null && icon.canvasRenderer.GetAlpha() < 0.05f) reasons.Add("iconRendererAlpha=0");
            if (Mathf.Abs(irt.localScale.x) < 0.2f || Mathf.Abs(irt.localScale.y) < 0.2f) reasons.Add($"iconScale={irt.localScale.x:F2}x{irt.localScale.y:F2}");
            if (irt.rect.width < size * 0.1f || irt.rect.height < size * 0.1f) reasons.Add($"iconRect={irt.rect.width:F0}x{irt.rect.height:F0}");
            if (irt.anchoredPosition.magnitude > size * 0.5f) reasons.Add($"iconOffset={irt.anchoredPosition}");
            if (icon.TryGetComponent<Canvas>(out var nested))
            {
                if (!nested.enabled) reasons.Add("iconCanvasDisabled");
                // Kendi sıralaması olan iç Canvas, ana Canvas'tan yüksek değilse ikon CellBG/GameBG altında kalır.
                var rootCanvas = nested.rootCanvas;
                if (nested.overrideSorting && rootCanvas != null && rootCanvas != nested
                    && nested.sortingLayerID == rootCanvas.sortingLayerID && nested.sortingOrder <= rootCanvas.sortingOrder)
                    reasons.Add($"iconCanvasBehind(order={nested.sortingOrder})");
            }
        }
        return reasons.Count > 0 ? string.Join(",", reasons) : null;
    }

    // Her kare: taşsız oynanabilir hücreleri izle. Hücre dolduğunda (veya tur bittiğinde hâlâ boşsa)
    // eşik üstü boş kaldıysa hücre, süre ve sebep adayı (tutuldu mu / şu an tutuluyor mu) yazılır.
    private static void TrackEmptyCells(BoardController board, Session session, float now, bool final)
    {
        if (board.Tiles == null) return;
        for (int x = 0; x < board.Width; x++)
        for (int y = 0; y < board.Height; y++)
        {
            var cell = new Vector2Int(x, y);
            bool open = board.Tiles[x, y] == null && !board.IsMaskHoleCell(x, y) && !board.IsObstacleBlockedCell(x, y)
                && (board.ObstacleStateService == null || !board.ObstacleStateService.HoldsTileAt(x, y));
            bool held = board.IsPendingTriggeredSpecialCell(x, y);
            bool tracked = session.empties.TryGetValue(cell, out var e);

            if (open && !final)
            {
                session.empties[cell] = tracked ? (e.since, e.held || held) : (now, held);
                continue;
            }
            if (!tracked) continue;
            if (!open) session.empties.Remove(cell);

            float duration = now - e.since;
            if (duration < LongEmptySeconds) continue;
            session.longEmpties++;
            session.longestEmpty = Mathf.Max(session.longestEmpty, duration);
            Write(board, session, "EMPTY_LONG",
                $"cell=({x},{y}) empty={duration:F2}s heldDuring={e.held || held} heldNow={held} " +
                $"filled={!open} resolvableNow={board.CascadeLogic != null && board.CascadeLogic.HasAnyResolvableEmptyPlayableCell()}");
        }
    }

    private static readonly Dictionary<BoardController, Session> sessions = new();
    private static int nextId;

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    internal static void ChainBegin(BoardController board, string detail)
    {
        if (board == null) return;
        bool created = !sessions.TryGetValue(board, out var session);
        if (created)
        {
            session = new Session { id = ++nextId, started = Time.realtimeSinceStartup };
            sessions.Add(board, session);
        }
        session.chains++;
        Write(board, session, "CHAIN_BEGIN", detail);
        if (created) board.StartCoroutine(Observe(board, session));
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    internal static void ChainEnd(BoardController board, string detail)
    {
        if (board == null || !sessions.TryGetValue(board, out var session)) return;
        session.chains--;
        Write(board, session, "CHAIN_END", detail);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    internal static void Event(BoardController board, string phase, string detail)
    {
        if (board != null && sessions.TryGetValue(board, out var session))
        {
            if (phase == "MOVE_OVERLAP") session.overlaps++;
            if (phase == "LINE_IMPACT_TIMEOUT") session.lineTimeouts++;
            if (phase == "GRAVITY_DEFERRED") session.deferredGravity++;
            Write(board, session, phase, detail);
        }
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    internal static void FallBegin(BoardController board, int id, int tiles, float estimate)
    {
        if (board == null || !sessions.TryGetValue(board, out var session)) return;
        session.falls.Add(id);
        Write(board, session, "FALL_BEGIN", $"fall={id} tiles={tiles} estimate={estimate:F3}s");
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    internal static void FallEnd(BoardController board, int id, float elapsed, bool completed)
    {
        if (board == null || !sessions.TryGetValue(board, out var session)) return;
        session.falls.Remove(id);
        if (!completed) session.interruptedFalls++;
        Write(board, session, "FALL_END", $"fall={id} elapsed={elapsed:F3}s completed={completed}");
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    internal static void CascadePlan(BoardController board)
    {
        if (board == null || !sessions.TryGetValue(board, out var session)) return;
        if (session.falls.Count > 0) session.replans++;
        if (session.falls.Count > 0)
            Write(board, session, "CASCADE_PLAN", "duringActiveFall=True");
    }

    private static void Write(BoardController board, Session session, string phase, string detail)
    {
        // Per-column starts/ends dominate large bonus rounds. Keep their counters
        // in the final result; print only chain stages and suspicious overlap.
        if (phase == "FALL_BEGIN" || phase == "FALL_END") return;
        Debug.Log($"[MotionDiag] run={session.id} board={board.GetInstanceID()} frame={Time.frameCount} " +
            $"t={Time.realtimeSinceStartup - session.started:F3}s phase={phase} " +
            $"gen={board.FallGeneration} chains={session.chains} falls={session.falls.Count} " +
            $"busy={board.IsBusy} seq={board.IsActionSequencePlaying} jobs={board.ActiveBackgroundJobs} " +
            $"blocking={board.BlockingBackgroundJobs} fx={board.PresentationFxInFlight} dash={board.FlyingPatchBotDashes} {detail}");
    }

    private static IEnumerator Observe(BoardController board, Session session)
    {
        float quietSince = -1f;
        float nextSample = 0f;
        try
        {
            while (board != null)
            {
                float now = Time.realtimeSinceStartup;
                session.worstFrameMs = Mathf.Max(session.worstFrameMs, Time.unscaledDeltaTime * 1000f);
                if (Time.unscaledDeltaTime > 0.05f) session.frameHitches++;
                bool quiet = session.chains == 0 && session.falls.Count == 0
                    && !board.IsBusy && !board.IsActionSequencePlaying && board.ActiveBackgroundJobs == 0;
                if (!quiet) quietSince = -1f;
                else if (quietSince < 0f) quietSince = now;

                // Allow detached landing cosmetics to finish before judging positions.
                bool settled = quietSince >= 0f && now - quietSince >= 0.25f;
                bool timeout = now - session.started >= 60f;
                TrackEmptyCells(board, session, now, settled || timeout);
                if (now >= nextSample || settled || timeout)
                {
                    Sample(board, session, settled, timeout);
                    nextSample = now + 0.3f;
                }
                if (settled || timeout) yield break;
                yield return null;
            }
        }
        finally
        {
            sessions.Remove(board);
        }
    }

    private static void Sample(BoardController board, Session session, bool settled, bool timeout)
    {
        int offGrid = 0, nonIdle = 0, pins = 0, targets = 0, mismatches = 0, still = 0, hidden = 0;
        var hiddenSamples = new List<string>();
        float maxError = 0f, longestStill = 0f;
        var samples = new List<string>();
        var pinSamples = new List<string>();
        float now = Time.realtimeSinceStartup;
        float size = Mathf.Max(1f, board.TileSize);
        var liveIds = new HashSet<(int id, int lifetime)>();
        if (board.Tiles != null)
        for (int x = 0; x < board.Width; x++)
        for (int y = 0; y < board.Height; y++)
        {
            if (board.IsPendingTriggeredSpecialCell(x, y))
            {
                pins++;
                if (pinSamples.Count < 8) pinSamples.Add($"({x},{y})");
            }
            if (board.IsReservedTileTargetCell(x, y)) targets++;
            var tile = board.Tiles[x, y];
            if (tile == null) continue;
            if (tile.X != x || tile.Y != y) mismatches++;
            if (tile.RuntimeState != TileRuntimeState.Idle) nonIdle++;
            // "Ekranda boş, veride dolu": taş gridde ama görünmüyor (combo gizlemesi, yarım kalan
            // clear animasyonu, havuzdaki deaktif view).
            if (settled || timeout)
            {
                string why = DescribeInvisibility(board, tile, size);
                if (why != null)
                {
                    hidden++;
                    if (hiddenSamples.Count < 6)
                        hiddenSamples.Add($"({x},{y}) id={tile.GetInstanceID()}/{tile.LifetimeVersion} type={tile.GetTileType()} " +
                            $"sp={tile.GetSpecial()} state={tile.RuntimeState} why={why} " +
                            $"hiddenBy={tile.HiddenBy ?? "-"}@{tile.HiddenFrame} nowFrame={Time.frameCount}");
                }
            }
            Vector2 position = tile.RectTransform.anchoredPosition;
            float error = Vector2.Distance(position, new Vector2(x * size, -y * size)) / size;
            maxError = Mathf.Max(maxError, error);
            if (error <= 0.1f) continue;
            offGrid++;
            int id = tile.GetInstanceID();
            var identity = (id, tile.LifetimeVersion);
            liveIds.Add(identity);
            float since = now;
            if (session.positions.TryGetValue(identity, out var previous)
                && Vector2.Distance(position, previous.position) / size < 0.01f)
                since = previous.since;
            session.positions[identity] = (position, since);
            float duration = now - since;
            longestStill = Mathf.Max(longestStill, duration);
            if (duration >= 0.3f) still++;
            if (samples.Count < 6)
                samples.Add($"cell=({x},{y}) id={id}/{tile.LifetimeVersion} sp={tile.GetSpecial()} " +
                    $"state={tile.RuntimeState} pos=({position.x / size:F2},{-position.y / size:F2}) " +
                    $"error={error:F2}cell still={duration:F2}s");
        }
        var removed = new List<(int id, int lifetime)>();
        foreach (var id in session.positions.Keys)
            if (!liveIds.Contains(id)) removed.Add(id);
        foreach (var id in removed) session.positions.Remove(id);
        session.maxStill = Mathf.Max(session.maxStill, longestStill);
        bool fillable = board.CascadeLogic != null && board.CascadeLogic.HasAnyResolvableEmptyPlayableCell();
        bool settledIssue = offGrid > 0 || nonIdle > 0 || pins > 0 || targets > 0 || mismatches > 0 || fillable || hidden > 0;
        bool issue = settledIssue || session.overlaps > 0 || session.interruptedFalls > 0 || session.lineTimeouts > 0;
        string phase = timeout ? "RESULT TIMEOUT" : settled
            ? (issue ? "RESULT ISSUE" : session.frameHitches > 0 ? "RESULT PERF" : "RESULT OK") : "SAMPLE";
        if (!settled && !timeout && still == 0) return;
        Write(board, session, phase,
            $"settledClean={!settledIssue} moveOverlaps={session.overlaps} interruptedFalls={session.interruptedFalls} " +
            $"lineTimeouts={session.lineTimeouts} framesOver50ms={session.frameHitches} " +
            $"offGrid={offGrid} maxError={maxError:F2}cell nonIdle={nonIdle} coordMismatch={mismatches} " +
            $"hidden={hidden}[{string.Join("; ", hiddenSamples)}] " +
            $"fillable={fillable} pins={pins}[{string.Join(",", pinSamples)}] reservedTargets={targets} " +
            $"stationaryOffGrid={still} longestStill={longestStill:F2}s maxStillInRun={session.maxStill:F2}s " +
            $"longEmpties={session.longEmpties} longestEmpty={session.longestEmpty:F2}s " +
            $"replansDuringFall={session.replans} deferredGravity={session.deferredGravity} worstFrameMs={session.worstFrameMs:F1} timeScale={Time.timeScale:F2} " +
            $"samples=[{string.Join("; ", samples)}]");
    }
}
