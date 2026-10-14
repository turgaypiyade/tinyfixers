using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kırılan jel fırlatıcının (ObstacleId.GelLauncherUp/Down/Left/Right) atışı. Gövde sıkışıp esner, kapak
/// ağızdan fırlar ve tahta kenarına kadar hücre hücre uçar; vardığı her hücre için
/// <see cref="GelLauncherBlastAction"/> sıraya girer (engel kalmayana kadar vurur, sonra taşı kırar /
/// special'ı tetikler). Kapak <see cref="GelLagCells"/> hücre çıkınca jel kartuşun TABANINDAN yola çıkar,
/// önce kartuşun iki hücresini sonra yolu kapaktan biraz hızlı boyar; o an engelle kapalı hücreler
/// patlamalar bitince boyanır. Tüp kaybolurken parçalar ve buhar saçılır.
///
/// WaterTankSpreadAction kalıbı: BoardController.HandleObstacleDestroyed'dan coroutine olarak başlar,
/// async ObstacleSpread job'u tutar (board akmaya devam eder, yalnız level-end bekler).
/// </summary>
public sealed class GelLauncherFireAction : BoardAction
{
    // Kapak giderek hızlanır: ilk hücre StartCellSeconds, her hücrede ×CellAccel, en hızlı MinCellSeconds.
    private const float StartCellSeconds = 0.085f;  // ilk hücre okunur olsun; sonra hızlanır
    private const float CellAccel = 0.88f;
    private const float MinCellSeconds = 0.03f;
    private const int GelLagCells = 2;              // jel, kapak bu kadar hücre çıkınca başlar
    private const float GelCellFactor = 0.7f;       // jel aynı eğriyle, kapaktan biraz hızlı (11 hücrede onu geçmez)
    private const float CompletionTimeout = 6f;
    private const string CapSpritePath = "GelLauncher/GelLauncherCap";
    // Basınç: kapaklı gövde yay gibi boyca %40 sıkışır (taban sabit, ağız tarafı içeri çekilir), yanlardan
    // %20 şişer ve giderek titrer; sonra serbest kalır → ileri atılır, kapak fırlar, gövde geri seker.
    private const float PressureSeconds = 0.4f;
    private const float PressureBulge = 0.20f;
    private const float PressureShorten = 0.40f;
    private const float PressureTremorDeg = 3f;
    private const float ReleaseSeconds = 0.18f;
    // İleri atılma: sıkışmadan bir anda ağız yönüne fırlar (kapak bu anın tepesinde kopar), sonra geri seker.
    private const float LungeSeconds = 0.07f;
    private const float LungeCells = 0.22f;
    private const float LungeAxis = 1.08f;
    private const float LungePerp = 0.92f;
    // Kapak ağızda bir an "patlar" (büyüyüp dışarı itilir), sonra yola çıkar.
    private const float CapPopSeconds = 0.09f;
    private const float CapPopScale = 1.35f;

    private readonly BoardController board;
    private readonly Vector2Int origin;
    private readonly ObstacleId launcherId;
    // Kartuşun iki hücresi: gövde görseli sönene dek taş girmez (yoksa şişme düşen taşların altında kalır).
    private CellHold bodyHold;

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
            Vector2Int dir = GelLauncherFx.Direction(launcherId);
            var footprint = Footprint(origin, launcherId);
            bodyHold = board.HoldForFlow(footprint);
            Vector2Int front = FrontCell(footprint, dir);
            var path = BuildPath(front, dir);

            var blasts = new GelLauncherBlastAction[path.Count];
            var deferred = new List<Vector2Int>();
            bool gelStarted = false, gelDone = false;

            // Hücreler arası dünya adımı (kartuşun iç hücresinden ağız hücresine).
            Vector3 frontWorld = board.GetCellWorldCenterPosition(front.x, front.y);
            Vector2Int back = front - dir;
            Vector3 step = frontWorld - board.GetCellWorldCenterPosition(back.x, back.y);
            if (footprint.Count < 2 || step.sqrMagnitude < 0.0001f)
                step = CellStepFallback(dir);

            // Atış öncesi: kapaklı gövde gazla şişer (taban sabit), sonra kapak fırlar ve kapaksız gövdeye geçer.
            Vector3 bodyCenter = (frontWorld + board.GetCellWorldCenterPosition(back.x, back.y)) * 0.5f;
            if (footprint.Count < 2) bodyCenter = frontWorld;
            // Gerçek kartuş görseli: veri kırılımı (bu aksiyonu başlatan) görsel devrinden ÖNCE gelir →
            // GridSpawner'ın devretmesini birkaç kare bekle (görsel o sırada yerinde durur). Yoksa yedek kopya.
            RectTransform body = null;
            for (int f = 0; f < 4 && body == null; f++)
            {
                body = TakeRealBody();
                if (body == null) yield return null;
            }
            if (!IsCurrent(level)) { if (body != null) Object.Destroy(body.gameObject); yield break; }
            if (body != null) bodyCenter = body.position;
            else body = CreateBody(level, step, bodyCenter);
            if (body != null)
            {
                yield return Pressure(body, bodyCenter, step);
                yield return Lunge(body, bodyCenter, step);
            }
            if (!IsCurrent(level))
            {
                if (body != null) Object.Destroy(body.gameObject);
                yield break;
            }

            Vector3 from = frontWorld + step * (0.5f + (body != null ? LungeCells : 0f));   // ağız (atılmış hâlde)
            var parent = board.BreakFxParent;
            if (parent != null)
            {
                float cellLocal = parent.InverseTransformVector(step).magnitude;
                GelLauncherFx.SpawnSteam(board, parent, parent.InverseTransformPoint(from),
                    GelLauncherFx.UiDirection(launcherId), cellLocal, 3, 0.7f);
            }
            if (body != null)
            {
                var openSprite = GelLauncherFx.LoadOpenSprite(launcherId);
                if (openSprite != null) body.GetComponent<Image>().sprite = openSprite;
                board.StartCoroutine(ReleaseAndFade(body, bodyCenter, step));
            }
            else
            {
                ReleaseBodyCells();   // gövde görseli yok: tutacak bir şey kalmadı
            }

            cap = CreateCap(dir, step);
            if (cap != null)
            {
                cap.position = from;
                yield return CapPop(cap, from, step);
                if (cap != null) from = cap.position;
            }

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 to = board.GetCellWorldCenterPosition(path[i].x, path[i].y);
                yield return Fly(cap, from, to, CellSeconds(i), i == 0);
                if (!IsCurrent(level)) yield break;
                from = to;

                if (!board.IsMaskHoleCell(path[i].x, path[i].y))
                {
                    blasts[i] = new GelLauncherBlastAction(board, level, path[i]);
                    board.EnqueueBoardAction(blasts[i]);
                }

                // Kapak ağızdan GelLagCells hücre çıkınca jel tabandan yola çıkar.
                if (!gelStarted && i + 1 >= GelLagCells)
                {
                    gelStarted = true;
                    board.StartCoroutine(RunGel(level, GelCells(footprint, front, path), deferred, () => gelDone = true));
                }
            }
            if (!gelStarted)
            {
                gelStarted = true;
                board.StartCoroutine(RunGel(level, GelCells(footprint, front, path), deferred, () => gelDone = true));
            }

            // Kapak tahtadan çıkıp söner.
            yield return FlyOut(cap, from, from + step, MinCellSeconds * 1.5f);
            if (cap != null) { Object.Destroy(cap.gameObject); cap = null; }

            // Jel ve kapak patlamalarının bitmesini bekle; o an engel altında kalan hücreleri sonra boya.
            float waited = 0f;
            while (waited < CompletionTimeout && IsCurrent(level) && (!gelDone || !AllCompleted(blasts)))
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (!IsCurrent(level)) yield break;
            foreach (var c in deferred)
                PaintIfOpen(c);
        }
        finally
        {
            if (cap != null) Object.Destroy(cap.gameObject);
            ReleaseBodyCells();
            job.Dispose();
            if (IsCurrent(level))
                board.RequestResolveAfterActionSequence();
        }
    }

    // Jel sırası: kartuşun tabanı → ağız hücresi → yol.
    private static List<Vector2Int> GelCells(List<Vector2Int> footprint, Vector2Int front, List<Vector2Int> path)
    {
        var cells = new List<Vector2Int>();
        foreach (var c in footprint)
            if (c != front) cells.Add(c);
        cells.Add(front);
        cells.AddRange(path);
        return cells;
    }

    // Jel kapak hızında ilerler; hücre o an hâlâ engelle kapalıysa sonra boyanmak üzere ayrılır.
    private IEnumerator RunGel(LevelData level, List<Vector2Int> cells, List<Vector2Int> deferred, System.Action done)
    {
        try
        {
            for (int j = 0; j < cells.Count; j++)
            {
                var c = cells[j];
                if (!IsCurrent(level)) yield break;
                if (!board.IsMaskHoleCell(c.x, c.y))
                {
                    var obstacles = board.ObstacleStateService;
                    if (obstacles != null && obstacles.IsCellBlocked(c.x, c.y))
                        deferred.Add(c);
                    else
                        board.PaintGelAt(c.x, c.y);
                }
                yield return new WaitForSeconds(CellSeconds(j) * GelCellFactor);
            }
        }
        finally
        {
            done?.Invoke();
        }
    }

    private static float CellSeconds(int index)
        => Mathf.Max(MinCellSeconds, StartCellSeconds * Mathf.Pow(CellAccel, index));

    private static bool AllCompleted(GelLauncherBlastAction[] blasts)
    {
        foreach (var b in blasts)
            if (b != null && !b.Completed) return false;
        return true;
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

    private static List<Vector2Int> Footprint(Vector2Int origin, ObstacleId id)
    {
        bool vertical = GelLauncherFx.IsVertical(id);
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

    // ── Gövde (atış animasyonu) ──────────────────────────────────────────────

    // Gerçek engel görseli yıkıldığı anda yerine konan geçici gövde: çatlak sprite ile başlar.
    private RectTransform CreateBody(LevelData level, Vector3 worldStep, Vector3 worldCenter)
    {
        var parent = board.BreakFxParent;
        var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(launcherId) : null;
        Sprite cracked = def != null && def.stages != null && def.stages.Count > 1 ? def.stages[1].sprite : null;
        if (parent == null || cracked == null) return null;

        var go = new GameObject("GelLauncherBody", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        float cell = parent.InverseTransformVector(worldStep).magnitude;
        if (cell <= 0.01f) cell = board.TileSize;
        rt.sizeDelta = GelLauncherFx.IsVertical(launcherId) ? new Vector2(cell, cell * 2f) : new Vector2(cell * 2f, cell);
        rt.position = worldCenter;

        var img = go.GetComponent<Image>();
        img.sprite = cracked;
        img.raycastTarget = false;
        return rt;
    }

    // axis: boy ölçeği, perp: en ölçeği. Taban (ağzın tersi) sabit kalır.
    // push: ağız yönüne kayma (hücre cinsinden; ileri atılma).
    private void SetShape(RectTransform body, Vector3 center, Vector3 step, float axis, float perp, float tiltDeg = 0f, float push = 0f)
    {
        body.localScale = GelLauncherFx.IsVertical(launcherId) ? new Vector3(perp, axis, 1f) : new Vector3(axis, perp, 1f);
        body.position = center - step * (1f - axis) + step * push;
        body.localRotation = Quaternion.Euler(0f, 0f, tiltDeg);
    }

    // Sıkışık hâlden bir anda ileri: boy uzar, en daralır, gövde ağız yönüne fırlar.
    private IEnumerator Lunge(RectTransform body, Vector3 center, Vector3 step)
    {
        for (float t = 0f; t < LungeSeconds; t += Time.deltaTime)
        {
            if (body == null) yield break;
            float k = t / LungeSeconds;
            float e = 1f - (1f - k) * (1f - k);
            SetShape(body, center, step,
                Mathf.Lerp(1f - PressureShorten, LungeAxis, e),
                Mathf.Lerp(1f + PressureBulge, LungePerp, e), 0f, LungeCells * e);
            yield return null;
        }
        if (body != null) SetShape(body, center, step, LungeAxis, LungePerp, 0f, LungeCells);
    }

    // GridSpawner'ın devrettiği gerçek görsel: bekleme nabzı durdurulur, pivot merkeze alınır (konum korunur).
    private RectTransform TakeRealBody()
    {
        var img = GelLauncherFx.TakeBody(origin.y * board.Width + origin.x);
        if (img == null) return null;
        var idle = img.GetComponent<GelLauncherIdleFx>();
        if (idle != null) Object.Destroy(idle);
        var rt = img.rectTransform;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = worldCenter;
        rt.SetAsLastSibling();
        return rt;
    }

    // Gaz birikimi: yavaş başlayıp hızlanan şişme, sona doğru artan titreme.
    private IEnumerator Pressure(RectTransform body, Vector3 center, Vector3 step)
    {
        for (float t = 0f; t < PressureSeconds; t += Time.deltaTime)
        {
            if (body == null) yield break;
            float k = t / PressureSeconds;
            float e = k * k * (3f - 2f * k);                     // baştan görünür, sonda dolar
            float tremor = Mathf.Sin(t * 70f) * PressureTremorDeg * k * k;
            SetShape(body, center, step, 1f - PressureShorten * e, 1f + PressureBulge * e, tremor);
            yield return null;
        }
        if (body != null) SetShape(body, center, step, 1f - PressureShorten, 1f + PressureBulge);
    }

    private IEnumerator ReleaseAndFade(RectTransform body, Vector3 center, Vector3 step)
    {
        // Kapak koptu: atılmış hâlden geri seker (hafif geriye taşar), yerine oturur.
        for (float t = 0f; t < ReleaseSeconds; t += Time.deltaTime)
        {
            if (body == null) yield break;
            float k = t / ReleaseSeconds;
            float axis, perp, push;
            if (k < 0.5f)
            {
                float u = k / 0.5f;
                axis = Mathf.Lerp(LungeAxis, 0.96f, u);
                perp = Mathf.Lerp(LungePerp, 1.05f, u);
                push = Mathf.Lerp(LungeCells, -0.05f, u);
            }
            else
            {
                float u = (k - 0.5f) / 0.5f;
                axis = Mathf.Lerp(0.96f, 1f, u);
                perp = Mathf.Lerp(1.05f, 1f, u);
                push = Mathf.Lerp(-0.05f, 0f, u);
            }
            SetShape(body, center, step, axis, perp, 0f, push);
            yield return null;
        }
        if (body == null) { ReleaseBodyCells(); yield break; }
        SetShape(body, center, step, 1f, 1f);

        yield return new WaitForSeconds(0.3f);
        var img = body != null ? body.GetComponent<Image>() : null;
        SpawnBreakShards(body, img != null ? img.sprite : null);
        const float fade = 0.25f;
        for (float t = 0f; t < fade && body != null; t += Time.deltaTime)
        {
            if (img != null) { var c = img.color; c.a = 1f - t / fade; img.color = c; }
            body.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, t / fade);
            yield return null;
        }
        if (body != null) Object.Destroy(body.gameObject);
        ReleaseBodyCells();   // gövde söndü: kartuş hücrelerine taş akabilir
    }

    private void ReleaseBodyCells()
    {
        bodyHold?.Dispose();
        bodyHold = null;
    }

    // Kapak ağızdan dışarı itilip büyür (basıncın boşaldığı an), yola bu noktadan çıkar.
    private static IEnumerator CapPop(RectTransform cap, Vector3 mouth, Vector3 step)
    {
        Vector3 pushed = mouth + step * 0.3f;
        for (float t = 0f; t < CapPopSeconds; t += Time.deltaTime)
        {
            if (cap == null) yield break;
            float k = t / CapPopSeconds;
            float e = 1f - (1f - k) * (1f - k);
            cap.position = Vector3.Lerp(mouth, pushed, e);
            cap.localScale = Vector3.one * Mathf.Lerp(0.7f, CapPopScale, e);
            yield return null;
        }
        if (cap == null) yield break;
        cap.position = pushed;
        cap.localScale = Vector3.one * CapPopScale;
    }

    // Tüp kaybolurken gövdeden parçalar saçılır + son bir buhar (WaterTank parça efektiyle aynı).
    private void SpawnBreakShards(RectTransform body, Sprite source)
    {
        if (body == null || source == null) return;
        var root = board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null
            ? board.BoardVfxPlayer.VfxRoot : board.BreakFxParent;
        if (root == null) return;

        var corners = new Vector3[4];
        body.GetWorldCorners(corners);
        Vector2 a = WaterTankFx.WorldToLocal(corners[0], root);
        Vector2 b = WaterTankFx.WorldToLocal(corners[2], root);
        Vector2 center = (a + b) * 0.5f;
        float size = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        // Kullanıcı: parçalar iri olsun → varsayılanın ~1.8 katı, daha az ama tok parça.
        WaterTankFx.SpawnShards(board, root, center, size, source, 8, 1.8f);
        GelLauncherFx.SpawnSteam(board, root, center, GelLauncherFx.UiDirection(launcherId), size * 0.5f, 2, 0.8f);
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
            float s = launch ? Mathf.Lerp(CapPopScale, 1.15f, k) : 1.15f;
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
