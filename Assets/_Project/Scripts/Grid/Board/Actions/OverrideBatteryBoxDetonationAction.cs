using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class OverrideBatteryBoxDetonationAction : BoardAction
{
    private const float DetonationDelay = 1.78f;

    private readonly BoardController board;
    private readonly int originIndex;

    public OverrideBatteryBoxDetonationAction(BoardController board, int originIndex)
    {
        this.board = board;
        this.originIndex = originIndex;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        var activeBoard = board != null ? board : sequencer?.Board;
        if (activeBoard == null)
            yield break;

        // Kutunun basınç/şişme animasyonuyla senkron: patlama tam burst anında başlar.
        yield return new WaitForSeconds(DetonationDelay);

        var impactCells = new List<Vector2Int>();
        var specialTiles = new List<TileView>();
        var impactedOrigins = new HashSet<int>();

        for (int y = 0; y < activeBoard.Height; y++)
        {
            for (int x = 0; x < activeBoard.Width; x++)
            {
                var obstacles = activeBoard.ObstacleStateService;
                if (obstacles != null
                    && obstacles.HasObstacleAt(x, y)
                    && !obstacles.IsExitAtBottomAt(x, y))
                {
                    int obstacleOrigin = obstacles.GetObstacleOriginAt(x, y);
                    if (obstacleOrigin >= 0 && obstacleOrigin != originIndex && impactedOrigins.Add(obstacleOrigin))
                        impactCells.Add(new Vector2Int(x, y));
                }

                if (!SpecialUtils.CanTargetTileContent(activeBoard, x, y))
                    continue;

                var tile = activeBoard.Tiles[x, y];
                if (tile == null)
                    continue;

                if (tile.GetSpecial() != TileSpecial.None)
                    specialTiles.Add(tile);
            }
        }

        if ((impactCells.Count == 0 && specialTiles.Count == 0) || sequencer == null)
        {
            activeBoard.RequestResolveAfterActionSequence();
            yield break;
        }

        // Obstacle merkezi = dalga başlangıcı. originIndex row-major (y*width + x).
        int width = Mathf.Max(1, activeBoard.Width);
        var originCell = new Vector2Int(originIndex % width, originIndex / width);

        float waveDuration = activeBoard.GetSystemOverrideComboWaveDuration();
        float duration = Mathf.Max(0.7f, waveDuration);   // toz bulutunun rahat büyümesi için taban

        // Duman cephesi radius yayınlar; hasar/tetiklemeler aynı lineer radius matematiğiyle zamanlanır.
        float maxRadiusPx = MaxWaveRadiusPx(activeBoard, originCell);
        RectTransform dustRoot = ResolveDustRoot(activeBoard);
        bool canPlayDust = dustRoot != null;
        if (canPlayDust)
            activeBoard.StartCoroutine(DustCloudRoutine(activeBoard, originCell, maxRadiusPx, duration));
        else if (activeBoard.BoardFlowTraceEnabled)
            LogDustRootUnavailable(activeBoard);

        if (activeBoard.BoardFlowTraceEnabled)
        {
            Debug.Log(
                $"[OBBDust] detonate origin=({originCell.x},{originCell.y}) " +
                $"impacts={impactCells.Count} specials={specialTiles.Count} " +
                $"duration={duration:0.000} maxRadiusPx={maxRadiusPx:0.0} " +
                $"eventDriven={canPlayDust}");
        }

        int pending = 0;
        void MarkDone() => pending = Mathf.Max(0, pending - 1);

        for (int i = 0; i < impactCells.Count; i++)
        {
            Vector2Int cell = impactCells[i];
            float delay = WaveDelayForCell(activeBoard, originCell, cell, maxRadiusPx, duration);
            pending++;
            activeBoard.StartCoroutine(ApplyObstacleHitAfterDelay(activeBoard, cell, delay, MarkDone));
        }

        for (int i = 0; i < specialTiles.Count; i++)
        {
            TileView tile = specialTiles[i];
            if (tile == null)
                continue;

            float delay = WaveDelayForCell(activeBoard, originCell, new Vector2Int(tile.X, tile.Y), maxRadiusPx, duration);
            pending++;
            activeBoard.StartCoroutine(TriggerSpecialAfterDelay(activeBoard, tile, delay, MarkDone));
        }

        float waited = 0f;
        float maxWait = duration + 0.35f;
        while (pending > 0 && waited < maxWait)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        activeBoard.RequestResolveAfterActionSequence();
    }

    private static IEnumerator ApplyObstacleHitAfterDelay(
        BoardController b,
        Vector2Int cell,
        float delay,
        System.Action onDone)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (b != null
            && cell.x >= 0 && cell.x < b.Width
            && cell.y >= 0 && cell.y < b.Height
            && b.ObstacleStateService != null
            && b.ObstacleStateService.HasObstacleAt(cell.x, cell.y)
            && !b.ObstacleStateService.IsExitAtBottomAt(cell.x, cell.y))
        {
            var hit = b.ApplyObstacleDamageAt(cell.x, cell.y, ObstacleHitContext.SpecialActivation, null);
            if (hit.didHit)
                b.TriggerObstacleVisualChange(hit.visualChange);
        }

        onDone?.Invoke();
    }

    private static IEnumerator TriggerSpecialAfterDelay(
        BoardController b,
        TileView tile,
        float delay,
        System.Action onDone)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (b != null
            && tile != null
            && tile.X >= 0 && tile.X < b.Width
            && tile.Y >= 0 && tile.Y < b.Height
            && b.Tiles[tile.X, tile.Y] == tile
            && tile.GetSpecial() != TileSpecial.None)
        {
            b.TriggerSpecialTileFromBoardEffect(tile);
        }

        onDone?.Invoke();
    }

    // ── Radyal toz bulutu ────────────────────────────────────────────────────
    // Merkezden dışa doğru büyüyen bir disk dolduran yumuşak toz pufları. Cephe
    // yarıçapı R(t)=t/duration·maxRadius; hit delay'iyle aynı normalizasyon.
    private static IEnumerator DustCloudRoutine(BoardController b, Vector2Int originCell, float maxRadius, float duration)
    {
        RectTransform root = ResolveDustRoot(b);
        if (root == null)
        {
            if (b != null && b.BoardFlowTraceEnabled)
                Debug.LogWarning("[OBBDust] yield break: no active RectTransform root for dust UI.");
            yield break;
        }

        LogDustDiagnostics(b, root, originCell, maxRadius, duration);

        // StartCoroutine aynı frame içinde ilk yield'e kadar koşar. Bir frame beklemek
        // MatchClearAction'ın OnSystemOverrideWaveProgress listener'ını bağlamasına izin verir.
        yield return null;

        Vector2 center = CellAnchored(b, originCell, root);
        float tileSize = LocalTileSize(b, originCell, root);
        b.InvokeSystemOverrideWaveProgress(0f);

        SpawnDustFront(b, root, center, tileSize, maxRadius, duration);

        // Merkez patlama çekirdeği
        for (int i = 0; i < 10; i++)
            SpawnDustPuff(b, root, center + Random.insideUnitCircle * tileSize * 0.35f, tileSize * Random.Range(1.1f, 1.85f));

        float t = 0f;
        float spawnAcc = 0f;
        float clusterAcc = 0f;
        const float spawnEvery = 0.022f;
        const float clusterEvery = 0.052f;
        while (t < duration)
        {
            float front = (t / duration) * maxRadius;
            b.InvokeSystemOverrideWaveProgress(front);
            spawnAcc += Time.deltaTime;
            clusterAcc += Time.deltaTime;
            while (spawnAcc >= spawnEvery)
            {
                spawnAcc -= spawnEvery;
                for (int i = 0; i < 5; i++)
                {
                    float ang = Random.value * Mathf.PI * 2f;
                    // Cepheye yakın ama biraz da geriyi doldur: halka okunur, içi de boş kalmaz.
                    float r = Mathf.Lerp(0.48f, 1.03f, Random.value) * front;
                    Vector2 pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    SpawnDustPuff(b, root, pos, tileSize * Random.Range(0.75f, 1.45f));
                }
            }

            while (clusterAcc >= clusterEvery && front > tileSize * 0.7f)
            {
                clusterAcc -= clusterEvery;
                SpawnWakeCluster(b, root, center, tileSize, front);
            }

            t += Time.deltaTime;
            yield return null;
        }

        b.InvokeSystemOverrideWaveProgress(maxRadius);

        // Dış halka son savurma
        for (int i = 0; i < 34; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            Vector2 pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * maxRadius * Random.Range(0.82f, 1.02f);
            SpawnDustPuff(b, root, pos, tileSize * Random.Range(0.95f, 1.65f));
        }
    }

    private static void SpawnWakeCluster(
        BoardController b,
        RectTransform root,
        Vector2 center,
        float tileSize,
        float frontRadius)
    {
        float ang = Random.value * Mathf.PI * 2f;
        float radius = frontRadius * Random.Range(0.52f, 0.88f);
        Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        Vector2 basePos = center + dir * radius;
        int count = Random.Range(5, 8);

        for (int i = 0; i < count; i++)
        {
            Vector2 tangent = new Vector2(-dir.y, dir.x);
            Vector2 scatter =
                tangent * Random.Range(-0.35f, 0.35f) * tileSize +
                dir * Random.Range(-0.25f, 0.20f) * tileSize;
            float size = tileSize * Random.Range(1.25f, 2.25f);
            SpawnLingeringDustPuff(b, root, basePos + scatter, size, dir);
        }
    }

    private static void SpawnLingeringDustPuff(
        BoardController b,
        RectTransform root,
        Vector2 pos,
        float size,
        Vector2 outward)
    {
        var go = new GameObject("OBBDustWake", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = root.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one * Random.Range(0.62f, 0.92f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        rt.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = Random.value < 0.65f ? GetDustNoiseSprite() : GetSoftSmokeSprite();
        img.preserveAspect = true;
        float shade = Random.Range(0.48f, 0.64f);
        img.color = new Color(shade, shade * 0.84f, shade * 0.58f, Random.Range(0.50f, 0.72f));

        b.StartCoroutine(LingeringDustRoutine(img, size, outward));
    }

    private static IEnumerator LingeringDustRoutine(Image img, float baseSize, Vector2 outward)
    {
        if (img == null)
            yield break;

        var rt = img.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 tangent = new Vector2(-outward.y, outward.x);
        Vector2 drift =
            outward * baseSize * Random.Range(0.08f, 0.22f) +
            tangent * baseSize * Random.Range(-0.18f, 0.18f) +
            Vector2.up * baseSize * 0.08f;
        float duration = Random.Range(0.95f, 1.32f);
        Vector3 startScale = rt.localScale;
        Vector3 endScale = startScale * Random.Range(1.7f, 2.35f);
        float startA = img.color.a;
        float spinSpeed = Random.Range(-12f, 12f);
        float elapsed = 0f;

        while (elapsed < duration && img != null)
        {
            float u = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - u, 2f);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, startPos + drift, eased);
            rt.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            rt.localRotation *= Quaternion.Euler(0f, 0f, Time.deltaTime * spinSpeed);

            var c = img.color;
            c.a = startA * (1f - Mathf.Clamp01((u - 0.18f) / 0.82f));
            img.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (img != null)
            Object.Destroy(img.gameObject);
    }

    private static void SpawnDustFront(
        BoardController b,
        RectTransform root,
        Vector2 center,
        float tileSize,
        float maxRadius,
        float duration)
    {
        var go = new GameObject("OBBDustFront", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = root.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = Vector2.one * Mathf.Max(tileSize, maxRadius * 2.4f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.SetAsLastSibling();

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image flash = CreateDustFrontImage(
            rt,
            "Flash",
            GetSoftSmokeSprite(),
            new Color(1.00f, 0.70f, 0.32f, 0.52f));
        Image haze = CreateDustFrontImage(
            rt,
            "Haze",
            GetDustNoiseSprite(),
            new Color(0.54f, 0.46f, 0.32f, 0.50f));
        Image shock = CreateDustFrontImage(
            rt,
            "ShockRing",
            GetSharpRingSprite(),
            new Color(1.00f, 0.82f, 0.48f, 0.62f));
        Image dustRing = CreateDustFrontImage(
            rt,
            "DustRing",
            GetSoftRingSprite(),
            new Color(0.72f, 0.56f, 0.34f, 0.92f));

        b.StartCoroutine(AnimateDustFront(
            go,
            group,
            flash.rectTransform,
            haze.rectTransform,
            shock.rectTransform,
            dustRing.rectTransform,
            tileSize,
            maxRadius,
            duration));
    }

    private static Image CreateDustFrontImage(RectTransform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = color;
        return img;
    }

    private static IEnumerator AnimateDustFront(
        GameObject go,
        CanvasGroup group,
        RectTransform flash,
        RectTransform haze,
        RectTransform shock,
        RectTransform dustRing,
        float tileSize,
        float maxRadius,
        float duration)
    {
        if (go == null || group == null || flash == null || haze == null || shock == null || dustRing == null)
            yield break;

        float startDiameter = Mathf.Max(tileSize * 0.35f, 8f);
        float endDiameter = Mathf.Max(startDiameter, maxRadius * 2.18f);
        float elapsed = 0f;

        while (elapsed < duration && go != null)
        {
            float u = Mathf.Clamp01(elapsed / duration);
            float dustEase = 1f - Mathf.Pow(1f - u, 2.35f);
            float shockEase = 1f - Mathf.Pow(1f - Mathf.Clamp01(u * 1.55f), 2.7f);
            float flashEase = 1f - Mathf.Pow(1f - Mathf.Clamp01(u * 4.2f), 2f);
            float frontDiameter = Mathf.Lerp(startDiameter, endDiameter, dustEase);
            float shockDiameter = Mathf.Lerp(tileSize * 0.45f, endDiameter * 1.05f, shockEase);
            float flashDiameter = Mathf.Lerp(tileSize * 0.7f, tileSize * 3.2f, flashEase);

            dustRing.sizeDelta = Vector2.one * frontDiameter;
            shock.sizeDelta = Vector2.one * shockDiameter;
            flash.sizeDelta = Vector2.one * flashDiameter;
            haze.sizeDelta = Vector2.one * (frontDiameter * 0.96f);

            float wobbleA = Mathf.Sin(u * Mathf.PI * 4.2f) * 0.014f;
            float wobbleB = Mathf.Cos(u * Mathf.PI * 3.6f) * 0.011f;
            dustRing.localScale = new Vector3(1f + wobbleA, 1f - wobbleB, 1f);
            haze.localScale = new Vector3(1f - wobbleB * 0.8f, 1f + wobbleA * 0.8f, 1f);
            shock.localScale = new Vector3(1f + wobbleB * 0.5f, 1f - wobbleA * 0.5f, 1f);
            dustRing.localRotation = Quaternion.Euler(0f, 0f, u * 7f);
            haze.localRotation = Quaternion.Euler(0f, 0f, -u * 4f);

            var hazeImg = haze.GetComponent<Image>();
            if (hazeImg != null)
            {
                var c = hazeImg.color;
                c.a = Mathf.Lerp(0.52f, 0.08f, u);
                hazeImg.color = c;
            }

            var flashImg = flash.GetComponent<Image>();
            if (flashImg != null)
            {
                var c = flashImg.color;
                c.a = Mathf.Lerp(0.58f, 0f, Mathf.Clamp01(u * 4.5f));
                flashImg.color = c;
            }

            var shockImg = shock.GetComponent<Image>();
            if (shockImg != null)
            {
                var c = shockImg.color;
                c.a = Mathf.Lerp(0.78f, 0f, Mathf.Clamp01((u - 0.06f) / 0.42f));
                shockImg.color = c;
            }

            var dustRingImg = dustRing.GetComponent<Image>();
            if (dustRingImg != null)
            {
                var c = dustRingImg.color;
                c.a = Mathf.Lerp(0.95f, 0.16f, u);
                dustRingImg.color = c;
            }

            group.alpha = Mathf.Lerp(1f, 0.15f, Mathf.Clamp01((u - 0.78f) / 0.22f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (go != null)
            Object.Destroy(go);
    }

    private static void SpawnDustPuff(BoardController b, RectTransform root, Vector2 pos, float size)
    {
        var go = new GameObject("OBBDust", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = root.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        rt.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = GetSoftSmokeSprite();
        img.preserveAspect = true;
        // Sıcak gri-kahve patlama tozu, hafif tonlama
        float shade = Random.Range(0.58f, 0.74f);
        img.color = new Color(shade, shade * 0.86f, shade * 0.62f, Random.Range(0.72f, 0.9f));

        b.StartCoroutine(DustPuffRoutine(img, size));
    }

    private static IEnumerator DustPuffRoutine(Image img, float baseSize)
    {
        if (img == null)
            yield break;

        var rt = img.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 drift = Random.insideUnitCircle * baseSize * 0.35f + Vector2.up * baseSize * 0.1f;
        float duration = Random.Range(0.42f, 0.68f);
        Vector3 startScale = Vector3.one * Random.Range(0.5f, 0.7f);
        Vector3 endScale = startScale * Random.Range(1.9f, 2.6f);
        float startA = img.color.a;
        float elapsed = 0f;

        while (elapsed < duration && img != null)
        {
            float u = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - u, 2f);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, startPos + drift, eased);
            rt.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            var c = img.color;
            c.a = startA * (1f - eased);
            img.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (img != null)
            Object.Destroy(img.gameObject);
    }

    private static Vector2 CellAnchored(BoardController b, Vector2Int cell, RectTransform space)
    {
        // Board'ın kendi kanıtlanmış dönüşümü (override combo VFX ile aynı).
        Vector3 worldPos = b.GetCellWorldCenterPosition(cell.x, cell.y);
        return b.WorldToAnchoredIn(space, worldPos);
    }

    private static RectTransform ResolveDustRoot(BoardController b)
    {
        if (b == null)
            return null;

        RectTransform breakRoot = b.BreakFxParent;
        if (breakRoot != null && breakRoot.gameObject.activeInHierarchy)
            return breakRoot;

        RectTransform vfxRoot = b.BoardVfxPlayer != null ? b.BoardVfxPlayer.VfxRoot : null;
        if (vfxRoot != null && vfxRoot.gameObject.activeInHierarchy)
            return vfxRoot;

        RectTransform parent = b.Parent;
        if (parent != null && parent.gameObject.activeInHierarchy)
            return parent;

        return null;
    }

    private static float LocalTileSize(BoardController b, Vector2Int originCell, RectTransform root)
    {
        if (b == null || root == null)
            return 1f;

        Vector2 a = CellAnchored(b, originCell, root);
        Vector2Int sampleCell;
        if (originCell.x + 1 < b.Width)
            sampleCell = originCell + Vector2Int.right;
        else if (originCell.x - 1 >= 0)
            sampleCell = originCell + Vector2Int.left;
        else if (originCell.y + 1 < b.Height)
            sampleCell = originCell + Vector2Int.up;
        else if (originCell.y - 1 >= 0)
            sampleCell = originCell + Vector2Int.down;
        else
            return Mathf.Max(1f, b.TileSize);

        Vector2 bPos = CellAnchored(b, sampleCell, root);

        float size = Vector2.Distance(a, bPos);
        return size >= 1f ? size : Mathf.Max(1f, b.TileSize);
    }

    private static float MaxWaveRadiusPx(BoardController b, Vector2Int originCell)
    {
        if (b == null)
            return 0f;

        float maxCells = SpecialVisualService.FarthestCornerDistanceCells(
            b.Width, b.Height, originCell.x, originCell.y);
        return (maxCells + 0.5f) * b.TileSize;
    }

    private static float WaveDelayForCell(
        BoardController b,
        Vector2Int originCell,
        Vector2Int targetCell,
        float maxRadiusPx,
        float duration)
    {
        if (b == null || maxRadiusPx <= Mathf.Epsilon || duration <= 0f)
            return 0f;

        float distCells = Vector2.Distance(
            new Vector2(originCell.x, originCell.y),
            new Vector2(targetCell.x, targetCell.y));
        float distPx = Mathf.Max(0f, distCells * b.TileSize - b.TileSize * 0.25f);
        return Mathf.Clamp01(distPx / maxRadiusPx) * duration;
    }

    private static void LogDustDiagnostics(
        BoardController b,
        RectTransform root,
        Vector2Int originCell,
        float maxRadius,
        float duration)
    {
        if (b == null || root == null || !b.BoardFlowTraceEnabled)
            return;

        Canvas canvas = root.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null ? canvas.worldCamera : null;
        Vector3 world = b.GetCellWorldCenterPosition(originCell.x, originCell.y);
        Vector2 anchored = b.WorldToAnchoredIn(root, world);

        Debug.DrawLine(world + Vector3.left * 0.25f, world + Vector3.right * 0.25f, Color.yellow, duration + 0.5f);
        Debug.DrawLine(world + Vector3.down * 0.25f, world + Vector3.up * 0.25f, Color.yellow, duration + 0.5f);

        Debug.Log(
            $"[OBBDust] root={root.name} activeSelf={root.gameObject.activeSelf} " +
            $"activeInHierarchy={root.gameObject.activeInHierarchy} layer={LayerMask.LayerToName(root.gameObject.layer)} " +
            $"rect={root.rect.size} scale={root.lossyScale} sibling={root.GetSiblingIndex()}/{root.parent?.childCount ?? 0} " +
            $"canvas={(canvas != null ? canvas.renderMode.ToString() : "null")} " +
            $"canvasActive={(canvas != null && canvas.gameObject.activeInHierarchy)} " +
            $"camera={(canvasCamera != null ? canvasCamera.name : "null")} " +
            $"cameraMask={(canvasCamera != null ? canvasCamera.cullingMask.ToString() : "n/a")} " +
            $"originWorld={world} originAnchored={anchored} maxRadius={maxRadius:0.0} duration={duration:0.000}");
    }

    private static void LogDustRootUnavailable(BoardController b)
    {
        if (b == null || !b.BoardFlowTraceEnabled)
            return;

        RectTransform vfxRoot = b.BoardVfxPlayer != null ? b.BoardVfxPlayer.VfxRoot : null;
        RectTransform breakRoot = b.BreakFxParent;
        RectTransform parent = b.Parent;

        Debug.LogWarning(
            "[OBBDust] event-driven dust disabled: no active root. " +
            $"BoardVfxPlayer.VfxRoot={RootState(vfxRoot)} " +
            $"BreakFxParent={RootState(breakRoot)} " +
            $"Parent={RootState(parent)}");
    }

    private static string RootState(RectTransform root)
    {
        if (root == null)
            return "null";

        Canvas canvas = root.GetComponentInParent<Canvas>();
        return $"{root.name}(activeSelf={root.gameObject.activeSelf}, " +
               $"activeInHierarchy={root.gameObject.activeInHierarchy}, " +
               $"canvas={(canvas != null ? canvas.renderMode.ToString() : "null")})";
    }

    // Prosedürel yumuşak radyal puf (roket dumanıyla aynı). Bir kez üretilip cache'lenir.
    private static Sprite _softSmoke;
    private static Sprite _softRing;
    private static Sprite _sharpRing;
    private static Sprite _dustNoise;

    private static Sprite GetSoftSmokeSprite()
    {
        if (_softSmoke != null)
            return _softSmoke;

        const int res = 64;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
        float radius = res * 0.48f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center) / radius;
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply(false, true);
        _softSmoke = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        _softSmoke.name = "GeneratedOBBDust";
        return _softSmoke;
    }

    private static Sprite GetSoftRingSprite()
    {
        if (_softRing != null)
            return _softRing;

        const int res = 96;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
        float outer = res * 0.49f;
        float inner = res * 0.28f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center);
            float ang = Mathf.Atan2(y - center.y, x - center.x);
            float warp =
                Mathf.Sin(ang * 5.0f + 0.7f) * 0.024f +
                Mathf.Sin(ang * 9.0f - 1.3f) * 0.014f +
                Mathf.Sin(ang * 15.0f + 2.1f) * 0.008f;
            float localOuter = outer * (1f + warp);
            float localInner = inner * (1f + warp * 1.35f);
            float outerFade = Mathf.Clamp01((localOuter - d) / (outer * 0.18f));
            float innerFade = Mathf.Clamp01((d - localInner) / (outer * 0.22f));
            float band = Mathf.Clamp01(outerFade * innerFade);
            float breakup =
                0.90f +
                Mathf.Sin(ang * 11.0f + d * 0.12f) * 0.055f +
                Mathf.Sin(ang * 21.0f - d * 0.08f) * 0.035f;
            band = band * band * (3f - 2f * band);
            band = Mathf.Clamp01(band * breakup);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, band));
        }

        tex.Apply(false, true);
        _softRing = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        _softRing.name = "GeneratedOBBDustRing";
        return _softRing;
    }

    private static Sprite GetSharpRingSprite()
    {
        if (_sharpRing != null)
            return _sharpRing;

        const int res = 96;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
        float ringRadius = res * 0.39f;
        float width = res * 0.045f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center);
            float ang = Mathf.Atan2(y - center.y, x - center.x);
            float warp =
                Mathf.Sin(ang * 6.0f - 0.4f) * 0.018f +
                Mathf.Sin(ang * 14.0f + 1.7f) * 0.010f;
            float localRadius = ringRadius * (1f + warp);
            float band = Mathf.Clamp01(1f - Mathf.Abs(d - localRadius) / width);
            float breakup = 0.90f + Mathf.Sin(ang * 17.0f + d * 0.2f) * 0.06f;
            band = band * band * (3f - 2f * band);
            band = Mathf.Clamp01(band * breakup);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, band));
        }

        tex.Apply(false, true);
        _sharpRing = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        _sharpRing.name = "GeneratedOBBDustShockRing";
        return _sharpRing;
    }

    private static Sprite GetDustNoiseSprite()
    {
        if (_dustNoise != null)
            return _dustNoise;

        const int res = 96;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
        float radius = res * 0.48f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            var p = new Vector2(x, y);
            float d = Vector2.Distance(p, center) / radius;
            float baseAlpha = Mathf.Clamp01(1f - d);
            baseAlpha = baseAlpha * baseAlpha * (3f - 2f * baseAlpha);

            float ang = Mathf.Atan2(p.y - center.y, p.x - center.x);
            float radialNoise =
                Mathf.Sin(ang * 7.0f + d * 10.0f) * 0.16f +
                Mathf.Sin(ang * 13.0f - d * 17.0f) * 0.10f +
                Mathf.Sin((x * 0.19f) + (y * 0.31f)) * 0.08f;

            float alpha = Mathf.Clamp01(baseAlpha * (0.82f + radialNoise));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        tex.Apply(false, true);
        _dustNoise = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        _dustNoise.name = "GeneratedOBBDustNoise";
        return _dustNoise;
    }
}
