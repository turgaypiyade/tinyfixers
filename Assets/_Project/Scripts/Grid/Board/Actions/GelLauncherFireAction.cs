using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kırılan jel fırlatıcının (ObstacleId.GelLauncherUp/Down/Left/Right) atışı. Kapak ağızdan fırlar ve
/// tahta kenarına kadar hücre hücre uçar; vardığı her hücre için <see cref="GelLauncherBlastAction"/>
/// sıraya girer (engel kalmayana kadar vurur, sonra taşı kırar / special'ı tetikler). Jel kapağın
/// <see cref="GelLagCells"/> hücre gerisinden gelir: bir hücre ancak kendi patlaması bittikten sonra
/// boyanır. Sonunda kartuşun kendi hücreleri de boyanır.
///
/// WaterTankSpreadAction kalıbı: BoardController.HandleObstacleDestroyed'dan coroutine olarak başlar,
/// async ObstacleSpread job'u tutar (board akmaya devam eder, yalnız level-end bekler).
/// </summary>
public sealed class GelLauncherFireAction : BoardAction
{
    private const float CellSeconds = 0.07f;
    private const int GelLagCells = 2;
    private const float CompletionTimeout = 6f;
    private const string CapSpritePath = "GelLauncher/GelLauncherCap";

    private readonly BoardController board;
    private readonly Vector2Int origin;
    private readonly ObstacleId launcherId;

    public GelLauncherFireAction(BoardController board, Vector2Int origin, ObstacleId launcherId)
    {
        this.board = board;
        this.origin = origin;
        this.launcherId = launcherId;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (board == null || board.LevelData == null)
            yield break;

        var level = board.LevelData;
        var job = board.BeginJob(BoardController.BoardJobKind.ObstacleSpread);
        RectTransform cap = null;
        try
        {
            Vector2Int dir = Direction(launcherId);
            var footprint = Footprint(origin, launcherId);
            Vector2Int front = FrontCell(footprint, dir);
            var path = BuildPath(front, dir);

            var blasts = new GelLauncherBlastAction[path.Count];
            int painted = 0;

            // Hücreler arası dünya adımı (kartuşun iç hücresinden ağız hücresine).
            Vector3 frontWorld = board.GetCellWorldCenterPosition(front.x, front.y);
            Vector2Int back = front - dir;
            Vector3 step = frontWorld - board.GetCellWorldCenterPosition(back.x, back.y);
            if (footprint.Count < 2 || step.sqrMagnitude < 0.0001f)
                step = CellStepFallback(dir);

            cap = CreateCap(dir, step);
            Vector3 from = frontWorld + step * 0.5f;   // ağız
            if (cap != null) cap.position = from;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 to = board.GetCellWorldCenterPosition(path[i].x, path[i].y);
                yield return Fly(cap, from, to, CellSeconds, i == 0);
                if (!IsCurrent(level)) yield break;
                from = to;

                if (!board.IsMaskHoleCell(path[i].x, path[i].y))
                {
                    blasts[i] = new GelLauncherBlastAction(board, level, path[i]);
                    board.EnqueueBoardAction(blasts[i]);
                }

                painted = PaintReady(path, blasts, painted, i - GelLagCells);
            }

            // Kapak tahtadan çıkıp söner.
            yield return FlyOut(cap, from, from + step, CellSeconds * 1.5f);
            if (cap != null) { Object.Destroy(cap.gameObject); cap = null; }

            // Kalan patlamaları bekle; biten hücreleri sırayla boya.
            float waited = 0f;
            while (painted < path.Count && waited < CompletionTimeout && IsCurrent(level))
            {
                painted = PaintReady(path, blasts, painted, path.Count - 1);
                if (painted >= path.Count) break;
                waited += Time.deltaTime;
                yield return null;
            }

            if (!IsCurrent(level)) yield break;
            for (int i = painted; i < path.Count; i++)
                PaintIfOpen(path[i]);
            foreach (var c in footprint)
                PaintIfOpen(c);
        }
        finally
        {
            if (cap != null) Object.Destroy(cap.gameObject);
            job.Dispose();
            if (IsCurrent(level))
                board.RequestResolveAfterActionSequence();
        }
    }

    // Sırayı koruyarak: patlaması biten (ya da hole olan) hücreleri limit'e kadar boyar.
    private int PaintReady(List<Vector2Int> path, GelLauncherBlastAction[] blasts, int painted, int limit)
    {
        while (painted <= limit && painted < path.Count)
        {
            var blast = blasts[painted];
            if (blast != null && !blast.Completed)
                break;
            PaintIfOpen(path[painted]);
            painted++;
        }
        return painted;
    }

    private void PaintIfOpen(Vector2Int c)
    {
        if (board.IsMaskHoleCell(c.x, c.y)) return;
        // Kırılamayan bir engel hâlâ hücreyi kapatıyorsa altına jel sürme (hedef sayacı yanılmasın).
        var obstacles = board.ObstacleStateService;
        if (obstacles != null && obstacles.IsCellBlocked(c.x, c.y)) return;
        board.PaintGelAt(c.x, c.y);
    }

    private bool IsCurrent(LevelData level)
        => board != null && board.isActiveAndEnabled && board.LevelData == level;

    // ── Geometri ──────────────────────────────────────────────────────────────

    // Grid'de y aşağı artar: Up = (0,-1).
    private static Vector2Int Direction(ObstacleId id)
    {
        switch (id)
        {
            case ObstacleId.GelLauncherDown: return new Vector2Int(0, 1);
            case ObstacleId.GelLauncherLeft: return new Vector2Int(-1, 0);
            case ObstacleId.GelLauncherRight: return new Vector2Int(1, 0);
            default: return new Vector2Int(0, -1);
        }
    }

    private static List<Vector2Int> Footprint(Vector2Int origin, ObstacleId id)
    {
        bool vertical = id == ObstacleId.GelLauncherUp || id == ObstacleId.GelLauncherDown;
        return new List<Vector2Int> { origin, vertical ? origin + new Vector2Int(0, 1) : origin + new Vector2Int(1, 0) };
    }

    // Ağzın bulunduğu (atış yönündeki) kartuş hücresi.
    private static Vector2Int FrontCell(List<Vector2Int> footprint, Vector2Int dir)
        => (dir.x + dir.y) < 0 ? footprint[0] : footprint[footprint.Count - 1];

    private List<Vector2Int> BuildPath(Vector2Int front, Vector2Int dir)
    {
        var path = new List<Vector2Int>();
        for (var c = front + dir; c.x >= 0 && c.x < board.Width && c.y >= 0 && c.y < board.Height; c += dir)
            path.Add(c);
        return path;
    }

    private Vector3 CellStepFallback(Vector2Int dir)
    {
        Vector3 a = board.GetCellWorldCenterPosition(0, 0);
        Vector3 bx = board.GetCellWorldCenterPosition(Mathf.Min(1, board.Width - 1), 0);
        Vector3 by = board.GetCellWorldCenterPosition(0, Mathf.Min(1, board.Height - 1));
        return (bx - a) * dir.x + (by - a) * dir.y;
    }

    // ── Kapak görseli ────────────────────────────────────────────────────────

    private RectTransform CreateCap(Vector2Int dir, Vector3 worldStep)
    {
        var sprite = Resources.Load<Sprite>(CapSpritePath);
        var parent = board.BreakFxParent;
        if (sprite == null || parent == null) return null;

        var go = new GameObject("GelLauncherCap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.SetAsLastSibling();
        float cell = parent.InverseTransformVector(worldStep).magnitude;
        if (cell <= 0.01f) cell = board.TileSize;
        rt.sizeDelta = new Vector2(cell * 0.85f, cell * 0.85f);
        // Kapak sprite'ı yukarı bakar; atış yönüne döndür (z pozitif = saat yönü tersi).
        float angle = dir.y < 0 ? 0f : dir.y > 0 ? 180f : dir.x > 0 ? -90f : 90f;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return rt;
    }

    private static IEnumerator Fly(RectTransform cap, Vector3 from, Vector3 to, float seconds, bool launch)
    {
        if (cap == null) yield break;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (cap == null) yield break;
            float k = t / seconds;
            cap.position = Vector3.LerpUnclamped(from, to, k);
            // Fırlama anında küçük bir "pop".
            float s = launch ? Mathf.Lerp(0.6f, 1.15f, k) : 1.15f;
            cap.localScale = Vector3.one * s;
            yield return null;
        }
        if (cap != null) cap.position = to;
    }

    private static IEnumerator FlyOut(RectTransform cap, Vector3 from, Vector3 to, float seconds)
    {
        if (cap == null) yield break;
        var img = cap.GetComponent<Image>();
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (cap == null) yield break;
            float k = t / seconds;
            cap.position = Vector3.LerpUnclamped(from, to, k);
            if (img != null) { var c = img.color; c.a = 1f - k; img.color = c; }
            yield return null;
        }
    }
}

/// <summary>
/// Jel fırlatıcı kapağının tek hücreye çarpması: önce hücredeki engel(ler) kalmayana kadar special
/// vuruşu (kırılamayan engelde durur), sonra <see cref="CellsImpactAction"/> ile taş kırılır / special
/// tetiklenir. Sıraya (sequencer) girer → hasar o anki içeriğe uygulanır.
/// </summary>
public sealed class GelLauncherBlastAction : BoardAction
{
    private const int MaxObstacleHits = 6;

    private readonly BoardController board;
    private readonly LevelData level;
    private readonly Vector2Int cell;
    public bool Completed { get; private set; }

    public GelLauncherBlastAction(BoardController board, LevelData level, Vector2Int cell)
    {
        this.board = board;
        this.level = level;
        this.cell = cell;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        try
        {
            if (board == null || !board.isActiveAndEnabled || board.LevelData != level)
                yield break;

            var obstacles = board.ObstacleStateService;
            for (int i = 0; i < MaxObstacleHits && obstacles != null && obstacles.HasObstacleAt(cell.x, cell.y); i++)
            {
                var hit = board.ApplyObstacleDamageAt(cell.x, cell.y, ObstacleHitContext.SpecialActivation);
                if (!hit.didHit) break;   // kırılamaz (kargo, jel vb.)
                board.TriggerObstacleVisualChange(hit.visualChange);
            }

            yield return new CellsImpactAction(board, level, new List<Vector2Int> { cell }).ExecuteVisuals(sequencer);
        }
        finally
        {
            Completed = true;
        }
    }
}
