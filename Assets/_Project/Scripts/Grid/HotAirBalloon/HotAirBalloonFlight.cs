using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sıcak Hava Balonu kalkışı (kullanıcı kuralları 2026-11-09): üçüncü special/joker vuruşunda balon tam şişer, dolap
/// ortadan ikiye yarılır (yarılar dış alt köşelerinden dışa devrilip söner), balon titrer ve yükselir. Geçtiği İKİ sütunda (2x2'nin üstü) taşlar sağa-sola savrularak temizlenir (hedeflere sayılır),
/// yoldaki special'lar TETİKLENİR, engeller BİR vuruş alır (genel "bir etki = bir vuruş" kuralı; GelLauncher'ın tam
/// kırma istisnası ona özel). Vuruşlar ortak BoosterCellImpactPass'ten (GelLauncher/Lift ile aynı yol).
/// Akış: ObstacleCollapse işi (tahta-genel girdi kapısı DEĞİL) + footprint ve yol hücreleri HoldForFlow ile tutulur;
/// level-end bitişi bekler. Savrulan taş görselleri paylaşılan havuzdan, tek döngüden sürülür.
/// </summary>
public sealed class HotAirBalloonFlight
{
    private const float TrembleSeconds = 0.28f;
    private const float RiseSeconds = 0.95f;
    private const float FlingSeconds = 0.5f;
    private const float SplitSeconds = 0.6f;     // dolap yarılarının devrilip sönmesi (kalkışla örtüşür)
    private const float SplitAngle = 55f, SplitSlide = 0.25f, SplitDrop = 0.25f;   // derece · alan oranı
    private const string GhostPoolKey = "HotAirBalloon.Ghost";

    private readonly BoardController board;
    private readonly Vector2Int origin;
    private readonly List<Ghost> ghosts = new(16);
    private static readonly Stack<Image> ghostPool = new();
    private RectTransform halfL, halfR;
    private Image halfLImage, halfRImage;
    private Vector3 halfLStart, halfRStart, areaRight, areaUp;

    private struct Ghost
    {
        public Image image;
        public Vector3 start;
        public float dir, t, spin;
    }

    public HotAirBalloonFlight(BoardController board, Vector2Int origin)
    {
        this.board = board;
        this.origin = origin;
    }

    public IEnumerator Run()
    {
        if (board == null || board.LevelData == null) yield break;
        var level = board.LevelData;
        var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(ObstacleId.HotAirBalloon) : null;
        var job = board.BeginJob(BoardController.BoardJobKind.ObstacleCollapse);
        CellHold footHold = null, pathHold = null;
        Image body = null;
        try
        {
            var footprint = new List<Vector2Int>(4);
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                    footprint.Add(origin + new Vector2Int(dx, dy));
            // Yol: 2x2'nin üstündeki iki sütun, alttan yukarı (y=0 tahtanın üstü).
            var rows = new List<int>();
            for (int y = origin.y - 1; y >= 0; y--) rows.Add(y);
            var path = new List<Vector2Int>(rows.Count * 2);
            foreach (int y in rows) { path.Add(new Vector2Int(origin.x, y)); path.Add(new Vector2Int(origin.x + 1, y)); }
            footHold = board.HoldForFlow(footprint);
            pathHold = board.HoldForFlow(path);

            // GridSpawner görseli bu olaydan hemen önce/sonra devreder: birkaç kare bekle.
            int originIndex = origin.y * board.Width + origin.x;
            for (int f = 0; f < 4 && body == null; f++)
            {
                body = HotAirBalloonView.Take(originIndex);
                if (body == null) yield return null;
            }
            if (!IsCurrent(level)) yield break;
            HotAirBalloonView view = body != null ? body.GetComponent<HotAirBalloonView>() : null;
            CanvasGroup fade = null;
            if (body != null && !body.TryGetComponent(out fade)) fade = body.gameObject.AddComponent<CanvasGroup>();
            if (fade != null) { fade.interactable = false; fade.blocksRaycasts = false; }

            // 1) Tam şişer, dolap ikiye yarılır (kırılma sesi def.breakSound merkezi yoldan çalındı), balon titrer.
            if (view != null)
            {
                var corners = new Vector3[4];
                body.rectTransform.GetWorldCorners(corners);   // 0 sol-alt · 1 sol-üst · 3 sağ-alt
                areaRight = corners[3] - corners[0];
                areaUp = corners[1] - corners[0];
                view.ShowFull();
                view.SplitCabinet(board.BreakFxParent, out halfL, out halfR);
                if (halfL != null) { halfLStart = halfL.position; halfLImage = halfL.GetComponent<Image>(); }
                if (halfR != null) { halfRStart = halfR.position; halfRImage = halfR.GetComponent<Image>(); }
                for (float t = 0f; t < TrembleSeconds; t += Time.deltaTime)
                {
                    view.Tremble(Mathf.Sin(t * 70f) * 3f);
                    TipHalves(t / SplitSeconds);
                    yield return null;
                    if (!IsCurrent(level)) yield break;
                }
                view.Tremble(0f);
            }
            if (def != null && def.flightLoopSound != null) board.Audio?.PlayOneShotClip(def.flightLoopSound, def.flightLoopSoundVolume);

            // 2) Yükseliş: balonun tabanı bir satırın merkezini geçince o satırın iki hücresi vurulur.
            var impacts = new BoosterCellImpactPass(board, tileBreakVisual: Fling, ownHold: pathHold);
            var rowY = new float[rows.Count];
            for (int i = 0; i < rows.Count; i++) rowY[i] = board.GetCellWorldCenterPosition(origin.x, rows[i]).y;
            float cellH = Mathf.Abs(board.GetCellWorldCenterPosition(origin.x, 0).y
                                    - board.GetCellWorldCenterPosition(origin.x, Mathf.Min(1, board.Height - 1)).y);
            if (cellH < 0.0001f) cellH = 1f;
            float travel = (origin.y + 3) * cellH;   // tahtanın üstünden bir hücre daha
            Vector3 startPos = body != null ? body.rectTransform.position : board.GetCellWorldCenterPosition(origin.x, origin.y);
            // Görünen sepetin tabanı (2x2 alt kenarı + dolap tabanı yüksekliği): satır, sepet geçince vurulur.
            float baseY = board.GetCellWorldCenterPosition(origin.x, origin.y + 1).y - cellH * 0.5f
                          + HotAirBalloonView.FullBasketLift * 2f * cellH;
            int next = 0;
            for (float t = 0f; t < RiseSeconds || next < rows.Count; t += Time.deltaTime)
            {
                if (!IsCurrent(level)) yield break;
                float p = Mathf.Clamp01(t / RiseSeconds);
                float rise = travel * p * p * (3f - 2f * p) * 0.4f + travel * p * 0.6f;   // yumuşak kalkış, sabit tırmanış
                if (body != null)
                {
                    body.rectTransform.position = startPos + new Vector3(Mathf.Sin(p * 9f) * cellH * 0.06f, rise, 0f);
                    if (fade != null) fade.alpha = p > 0.8f ? 1f - (p - 0.8f) / 0.2f : 1f;
                }
                TipHalves((TrembleSeconds + t) / SplitSeconds);
                float bottom = baseY + rise;
                while (next < rows.Count && (bottom >= rowY[next] || p >= 1f))
                {
                    impacts.HitCell(new Vector2Int(origin.x, rows[next]));
                    impacts.HitCell(new Vector2Int(origin.x + 1, rows[next]));
                    next++;
                }
                TickGhosts(Time.deltaTime);
                yield return null;
            }
            // Savrulanlar bitene dek (kısa) sür.
            for (float t = 0f; t < FlingSeconds && ghosts.Count > 0; t += Time.deltaTime)
            {
                TickGhosts(Time.deltaTime);
                yield return null;
            }
        }
        finally
        {
            for (int i = 0; i < ghosts.Count; i++) Release(ghosts[i].image);
            ghosts.Clear();
            if (body != null) Object.Destroy(body.gameObject);
            if (halfL != null) Object.Destroy(halfL.gameObject);
            if (halfR != null) Object.Destroy(halfR.gameObject);
            footHold?.Dispose();
            pathHold?.Dispose();
            job.Dispose();
            if (IsCurrent(level)) board.RequestResolveAfterActionSequence();
        }
    }

    private bool IsCurrent(LevelData level) => board != null && board.isActiveAndEnabled && board.LevelData == level;

    // Dolap yarıları: dış alt köşe pivotu etrafında dışa devrilir, hafif yana kayar, düşer ve söner.
    private void TipHalves(float k)
    {
        k = Mathf.Clamp01(k);
        float alpha = 1f - k * k;
        Vector3 drop = -areaUp * (SplitDrop * k * k);
        if (halfL != null)
        {
            halfL.position = halfLStart - areaRight * (SplitSlide * k) + drop;
            halfL.localRotation = Quaternion.Euler(0f, 0f, SplitAngle * k);
            halfLImage.color = new Color(1f, 1f, 1f, alpha);
        }
        if (halfR != null)
        {
            halfR.position = halfRStart + areaRight * (SplitSlide * k) + drop;
            halfR.localRotation = Quaternion.Euler(0f, 0f, -SplitAngle * k);
            halfRImage.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    // ── Savrulan taşlar: taşın ikonundan kopya; sol sütun sola, sağ sütun sağa kavisli uçar, döner, söner ──
    private void Fling(TileView tile)
    {
        var parent = board.BreakFxParent;
        var icon = tile != null ? tile.IconImage : null;
        if (parent == null || icon == null || icon.sprite == null) return;
        var img = Rent(parent);
        img.sprite = icon.sprite;
        img.preserveAspect = true;
        var rt = img.rectTransform;
        rt.position = icon.rectTransform.position;
        rt.sizeDelta = icon.rectTransform.rect.size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        img.color = Color.white;
        float dir = tile.X <= origin.x ? -1f : 1f;
        ghosts.Add(new Ghost { image = img, start = rt.position, dir = dir, t = 0f, spin = dir * Random.Range(260f, 420f) });
    }

    private void TickGhosts(float dt)
    {
        if (ghosts.Count == 0) return;
        float cell = Mathf.Max(1f, board.TileSize);
        var parent = board.BreakFxParent;
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            var g = ghosts[i];
            g.t += dt / FlingSeconds;
            if (g.t >= 1f || g.image == null) { Release(g.image); ghosts.RemoveAt(i); continue; }
            float k = g.t;
            // Yerel birimde kavis: yana 2.2 hücre, önce biraz yukarı sonra aşağı.
            Vector3 local = new Vector3(g.dir * cell * 2.2f * k, cell * (1.1f * k - 2.2f * k * k), 0f);
            g.image.rectTransform.position = g.start + (parent != null ? parent.TransformVector(local) : local);
            g.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -g.spin * k);
            g.image.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, k);
            g.image.color = new Color(1f, 1f, 1f, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
            ghosts[i] = g;
        }
    }

    private static Image Rent(RectTransform parent)
    {
        Image img = null;
        while (ghostPool.Count > 0 && img == null) img = ghostPool.Pop();
        if (img == null)
        {
            var go = new GameObject(GhostPoolKey, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            img = go.GetComponent<Image>();
            img.raycastTarget = false;
        }
        img.gameObject.layer = parent.gameObject.layer;
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.SetAsLastSibling();
        img.gameObject.SetActive(true);
        return img;
    }

    private static void Release(Image img)
    {
        if (img == null) return;
        img.gameObject.SetActive(false);
        ghostPool.Push(img);
    }
}
