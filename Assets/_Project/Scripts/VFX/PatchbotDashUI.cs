using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PatchbotDashUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform boardContent;   // Tile'ların bulunduğu root (sadece test/path bulma için)
    [SerializeField] private RectTransform vfxRoot;        // VFXRoot (runner + afterimage burada)
    [SerializeField] private Image runnerImage;            // PatchbotRunner Image (template only)
    [SerializeField] private TileIconLibrary tileIcons;    // TileIconLibrary asset

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 100f;       // UI units per second (anchored space)
    [SerializeField] private float arriveEps = 2f;

    [Header("AfterImage")]
    [SerializeField] private float spawnEvery = 0.02f;
    [SerializeField] private float afterLife = 0.28f;
    [SerializeField] private Color afterColor = new Color(0.55f, 0.85f, 1f, 0.85f);

    private Coroutine co;

    void Reset()
    {
        runnerImage = GetComponent<Image>();
    }

    /// <summary>
    /// Legacy/test: runs a single runnerImage along a UI-RectTransform path.
    /// </summary>
    public void PlayDash(List<RectTransform> pathTiles)
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(DashRoutine(pathTiles));
    }

    /// <summary>
    /// Main: launches MANY patchbots in parallel (tiny stagger) using per-dash instances,
    /// so multi-patchbot cases don't take 30 seconds.
    /// </summary>
    public Coroutine PlayDashParallel(List<BoardController.PatchbotDashRequest> requests, BoardController board)
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(DashParallelRoutine(requests, board));
        return co;
    }

    private IEnumerator DashParallelRoutine(List<BoardController.PatchbotDashRequest> requests, BoardController board)
    {
        if (vfxRoot == null || board == null) yield break;
        if (requests == null || requests.Count == 0) yield break;

        // IMPORTANT: keep this GameObject active so Fade coroutines can run.
        // Template image is not used for movement in parallel mode.
        if (runnerImage != null) runnerImage.enabled = false;

        // Reliable sprite source
        Sprite patchbotSprite = null;
        if (tileIcons != null && tileIcons.patchBot != null) patchbotSprite = tileIcons.patchBot;
        if (patchbotSprite == null && runnerImage != null) patchbotSprite = runnerImage.sprite;

        const float stagger = 0.02f; // tiny visual offset
        int remaining = 0;

        for (int i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            remaining++;
            StartCoroutine(SingleDashRoutine(req, board, patchbotSprite, () => remaining--));

            if (stagger > 0f)
                yield return new WaitForSeconds(stagger);
        }

        while (remaining > 0)
            yield return null;

        co = null;
    }

    private IEnumerator SingleDashRoutine(
        BoardController.PatchbotDashRequest req,
        BoardController board,
        Sprite sprite,
        System.Action onComplete)
    {
        // Per-patchbot instance
        var go = new GameObject("PatchbotRunnerInstance", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(vfxRoot, false);

        var img = go.GetComponent<Image>();
        var rt = (RectTransform)go.transform;

        img.sprite = sprite;
        img.raycastTarget = false;
        img.enabled = true;
        img.color = Color.white;

        // Ensure visible above board UI
        rt.SetAsLastSibling();

        // Safe anchors/pivot for anchoredPosition motion
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Size fallback
        Vector2 size = new Vector2(90f, 90f);
        if (runnerImage != null && runnerImage.rectTransform != null && runnerImage.rectTransform.sizeDelta.sqrMagnitude > 1f)
            size = runnerImage.rectTransform.sizeDelta;
        rt.sizeDelta = size;

        Vector3 fromWorld = board.GetCellWorldPosition(req.from.x, req.from.y);
        Vector3 toWorld   = board.GetCellWorldPosition(req.to.x, req.to.y);

        rt.anchoredPosition = WorldToAnchoredIn(vfxRoot, fromWorld);
        Vector2 target = WorldToAnchoredIn(vfxRoot, toWorld);

        float tAfter = 0f;

        while (Vector2.Distance(rt.anchoredPosition, target) > arriveEps)
        {
            rt.anchoredPosition =
                Vector2.MoveTowards(rt.anchoredPosition, target, dashSpeed * Time.deltaTime);

            tAfter += Time.deltaTime;
            if (tAfter >= spawnEvery)
            {
                tAfter = 0f;
                SpawnAfterImageAt(rt, sprite);
            }

            yield return null;
        }

        Destroy(go);
        onComplete?.Invoke();
    }

    private IEnumerator DashRoutine(List<RectTransform> path)
    {
        if (runnerImage == null || tileIcons == null || boardContent == null || vfxRoot == null) yield break;
        if (path == null || path.Count == 0) yield break;

        if (transform.parent != vfxRoot)
            transform.SetParent(vfxRoot, false);

        runnerImage.sprite = tileIcons.patchBot;
        runnerImage.raycastTarget = false;
        runnerImage.enabled = true;
        runnerImage.color = Color.white;

        Vector2 tileSize = new Vector2(90f, 90f);
        var tileImage = path[0].GetComponent<Image>();
        if (tileImage != null)
        {
            var tileRT = tileImage.rectTransform;
            if (tileRT.rect.width > 1f && tileRT.rect.height > 1f)
                tileSize = tileRT.rect.size;
        }

        runnerImage.rectTransform.sizeDelta = tileSize;
        runnerImage.rectTransform.SetAsLastSibling();
        runnerImage.rectTransform.anchoredPosition = WorldToAnchoredIn(vfxRoot, path[0].position);

        float tAfter = 0f;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2 target = WorldToAnchoredIn(vfxRoot, path[i].position);

            while (Vector2.Distance(runnerImage.rectTransform.anchoredPosition, target) > arriveEps)
            {
                runnerImage.rectTransform.anchoredPosition =
                    Vector2.MoveTowards(runnerImage.rectTransform.anchoredPosition, target, dashSpeed * Time.deltaTime);

                tAfter += Time.deltaTime;
                if (tAfter >= spawnEvery)
                {
                    tAfter = 0f;
                    SpawnAfterImageAt(runnerImage.rectTransform, runnerImage.sprite);
                }

                yield return null;
            }
        }

        // tiny pop
        var rt = runnerImage.rectTransform;
        Vector3 baseScale = rt.localScale;
        rt.localScale = baseScale * 1.15f;
        yield return new WaitForSeconds(0.06f);
        rt.localScale = baseScale;

        runnerImage.enabled = false;
        co = null;

        // Don't deactivate the GameObject: we want afterimage Fade coroutines to complete.
        // gameObject.SetActive(false);
    }

    private void SpawnAfterImageAt(RectTransform source, Sprite sprite)
    {
        if (vfxRoot == null || source == null) return;

        var go = new GameObject("PatchbotAfterImage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(vfxRoot, false);

        var img = go.GetComponent<Image>();
        var rt = (RectTransform)go.transform;

        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = afterColor;

        rt.anchorMin = source.anchorMin;
        rt.anchorMax = source.anchorMax;
        rt.pivot = source.pivot;

        rt.sizeDelta = source.sizeDelta;
        rt.anchoredPosition = source.anchoredPosition;
        rt.localScale = source.localScale;

        // Keep it behind the runner
        rt.SetSiblingIndex(Mathf.Max(0, source.GetSiblingIndex() - 1));

        StartCoroutine(FadeAndDestroy(go, img, afterLife));
    }

    private IEnumerator FadeAndDestroy(GameObject go, Image img, float life)
    {
        float t = 0f;
        Color start = img.color;

        while (t < life)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(start.a, 0f, t / life);
            img.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }

        Destroy(go);
    }

    static Vector2 WorldToAnchoredIn(RectTransform targetSpace, Vector3 worldPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetSpace,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint
        );
        return localPoint;
    }
}
