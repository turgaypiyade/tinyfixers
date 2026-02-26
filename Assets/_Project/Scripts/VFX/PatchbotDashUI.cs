using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PatchbotDashUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform boardContent;   // Tile'ların bulunduğu root (sadece test/path bulma için)
    [SerializeField] private RectTransform vfxRoot;        // VFXRoot (runner + afterimage burada)
    [SerializeField] private Image runnerImage;            // PatchbotRunner Image
    [SerializeField] private TileIconLibrary tileIcons;    // TileIconLibrary asset

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 2600f;      // UI units per second (anchored space)
    [SerializeField] private float arriveEps = 2f;

    [Header("AfterImage")]
    [SerializeField] private float spawnEvery = 0.04f;
    [SerializeField] private float afterLife = 0.18f;
    [SerializeField] private Color afterColor = new Color(0.55f, 0.85f, 1f, 0.75f);

    private Coroutine co;

    void Reset()
    {
        runnerImage = GetComponent<Image>();
    }

    public void PlayDash(List<RectTransform> pathTiles)
    {
        // runner kapalıysa aç (Coroutine için şart)
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(DashRoutine(pathTiles));
    }

    public Coroutine PlayDashSequence(List<BoardController.PatchbotDashRequest> requests, BoardController board)
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(DashSequenceRoutine(requests, board));
        return co;
    }

    private IEnumerator DashSequenceRoutine(List<BoardController.PatchbotDashRequest> requests, BoardController board)
    {
        if (runnerImage == null || vfxRoot == null || board == null)
            yield break;

        if (requests == null || requests.Count == 0)
            yield break;

        // Runner her zaman VFXRoot altında kalsın (koord mantığı için)
        if (transform.parent != vfxRoot)
            transform.SetParent(vfxRoot, false);

        runnerImage.raycastTarget = false;
        runnerImage.enabled = true;
        runnerImage.color = new Color(runnerImage.color.r, runnerImage.color.g, runnerImage.color.b, 1f);

        // Eğer patchbot sprite'ı atıyorsan:
        if (tileIcons != null) runnerImage.sprite = tileIcons.patchBot;

        // Boyut fallback (tile size 0 problemine karşı)
        runnerImage.rectTransform.sizeDelta = new Vector2(90f, 90f);
        runnerImage.rectTransform.SetAsLastSibling();

        // sırayla oyna (throttle)
        for (int i = 0; i < requests.Count; i++)
        {
            var req = requests[i];

            Vector3 fromWorld = board.GetCellWorldPosition(req.from.x, req.from.y);
            Vector3 toWorld   = board.GetCellWorldPosition(req.to.x, req.to.y);

            runnerImage.rectTransform.anchoredPosition = WorldToAnchoredIn(vfxRoot, fromWorld);
            Vector2 target = WorldToAnchoredIn(vfxRoot, toWorld);

            float tAfter = 0f;

            while (Vector2.Distance(runnerImage.rectTransform.anchoredPosition, target) > arriveEps)
            {
                runnerImage.rectTransform.anchoredPosition =
                    Vector2.MoveTowards(runnerImage.rectTransform.anchoredPosition, target, dashSpeed * Time.deltaTime);

                tAfter += Time.deltaTime;
                if (tAfter >= spawnEvery)
                {
                    tAfter = 0f;
                    SpawnAfterImage();
                }

                yield return null;
            }

            // küçük boşluk: çoklu patchbot okunabilir olsun
            yield return null;
        }

        co = null;
        runnerImage.enabled = false;
        runnerImage.color = new Color(runnerImage.color.r, runnerImage.color.g, runnerImage.color.b, 0f);
       // gameObject.SetActive(false);
    }
    IEnumerator DashRoutine(List<RectTransform> path)
    {
        if (runnerImage == null || tileIcons == null || boardContent == null || vfxRoot == null) yield break;
        if (path == null || path.Count == 0) yield break;

        // Runner her zaman VFXRoot altında kalsın (senin pipeline)
        if (transform.parent != vfxRoot)
            transform.SetParent(vfxRoot, false);

        // Sprite
        runnerImage.sprite = tileIcons.patchBot;
        runnerImage.raycastTarget = false;
        runnerImage.enabled = true;

        // 1 tile boyutu
        Vector2 tileSize = new Vector2(90f, 90f); // fallback

        // Tile üzerinde Image varsa onun rect'ini kullan
        var tileImage = path[0].GetComponent<Image>();
        if (tileImage != null)
        {
            var tileRT = tileImage.rectTransform;
            if (tileRT.rect.width > 1f && tileRT.rect.height > 1f)
                tileSize = tileRT.rect.size;
        }

        runnerImage.rectTransform.sizeDelta = tileSize;
        runnerImage.rectTransform.SetAsLastSibling();

        // Başlangıç: tile world pos -> vfxRoot local anchored
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
                    SpawnAfterImage(); // VFXRoot altında, aynı space
                }

                yield return null;
            }
        }

        // küçük pop
        var rt = runnerImage.rectTransform;
        Vector3 baseScale = rt.localScale;
        rt.localScale = baseScale * 1.15f;
        yield return new WaitForSeconds(0.06f);
        rt.localScale = baseScale;

        co = null;

        // İstersen testte kapatma. Prod'da kapatmak OK.
        runnerImage.enabled = false;
        gameObject.SetActive(false);
    }

    private void SpawnAfterImageAt(RectTransform source)
    {
        var go = new GameObject("PatchbotAfterImage",
            typeof(RectTransform), typeof(UnityEngine.UI.Image));

        go.transform.SetParent(vfxRoot, false);

        var img = go.GetComponent<UnityEngine.UI.Image>();
        var rt = (RectTransform)go.transform;

        img.sprite = runnerImage.sprite;
        img.raycastTarget = false;
        img.color = afterColor;

        rt.sizeDelta = source.sizeDelta;
        rt.anchoredPosition = source.anchoredPosition;
        rt.localScale = source.localScale;

        Destroy(go, afterLife);
    }

    void SpawnAfterImage()
    {
        if (runnerImage == null) return;
        SpawnAfterImageAt(runnerImage.rectTransform);
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

    public Coroutine PlayDashParallel(
        List<BoardController.PatchbotDashRequest> requests,
        BoardController board)
    {
        return StartCoroutine(DashParallelRoutine(requests, board));
    }

    private IEnumerator DashParallelRoutine(
        List<BoardController.PatchbotDashRequest> requests,
        BoardController board)
    {
        if (requests == null || requests.Count == 0)
            yield break;

        List<Coroutine> running = new List<Coroutine>();

        const float stagger = 0.02f; // küçük fark

        for (int i = 0; i < requests.Count; i++)
        {
            var req = requests[i];

            running.Add(
                StartCoroutine(SingleDashRoutine(req, board))
            );

            yield return new WaitForSeconds(stagger);
        }

        // Hepsinin bitmesini bekle
        while (running.Count > 0)
        {
            running.RemoveAll(c => c == null);
            yield return null;
        }
    }

    private IEnumerator SingleDashRoutine(
        BoardController.PatchbotDashRequest req,
        BoardController board)
    {
        if (runnerImage == null || vfxRoot == null)
            yield break;

        // Her patchbot için ayrı instance yaratıyoruz
        var go = new GameObject("PatchbotRunnerInstance",
            typeof(RectTransform), typeof(UnityEngine.UI.Image));

        go.transform.SetParent(vfxRoot, false);

        var img = go.GetComponent<UnityEngine.UI.Image>();
        var rt = (RectTransform)go.transform;

        img.sprite = runnerImage.sprite;
        img.raycastTarget = false;
        img.color = Color.white;

        rt.sizeDelta = runnerImage.rectTransform.sizeDelta;

        Vector3 fromWorld = board.GetCellWorldPosition(req.from.x, req.from.y);
        Vector3 toWorld = board.GetCellWorldPosition(req.to.x, req.to.y);

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
                SpawnAfterImageAt(rt);
            }

            yield return null;
        }

        Destroy(go);
    }    
    #if UNITY_EDITOR
    [ContextMenu("TEST DASH")]
    void TestDash()
    {
        if (boardContent == null)
        {
            Debug.LogError("[PatchbotDashUI] boardContent missing");
            return;
        }

        // Sadece Image olanları tile gibi kabul et
        var images = boardContent.GetComponentsInChildren<Image>(true);

        List<RectTransform> path = new List<RectTransform>();

        for (int i = 0; i < images.Length; i++)
        {
            var rt = images[i].rectTransform;
            if (rt == boardContent) continue;

            // çok küçük/nokta gibi objeleri ele (opsiyonel ama faydalı)
            if (rt.rect.width < 5f || rt.rect.height < 5f) continue;

            path.Add(rt);
            if (path.Count >= 2) break;
        }

        Debug.Log($"[PatchbotDashUI] TEST DASH pathCount={path.Count}");
        PlayDash(path);
    }
    #endif
}