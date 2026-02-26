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

    void SpawnAfterImage()
    {
        // Afterimage'lar da VFXRoot altında (aynı coordinate space)
        var go = new GameObject("PatchbotAfterImage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(vfxRoot, false);

        var img = go.GetComponent<Image>();
        img.sprite = runnerImage.sprite;
        img.raycastTarget = false;
       // img.color = afterColor;
        img.color = new Color(1f, 1f, 1f, 0.9f);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = runnerImage.rectTransform.anchorMin;
        rt.anchorMax = runnerImage.rectTransform.anchorMax;
        rt.pivot = runnerImage.rectTransform.pivot;

        // boyut + konum + scale runner ile aynı
        rt.sizeDelta = runnerImage.rectTransform.sizeDelta;
        rt.anchoredPosition = runnerImage.rectTransform.anchoredPosition;
        rt.localScale = runnerImage.rectTransform.localScale;

        // AfterImage, runner'ın hemen arkasında kalsın
        int runnerIndex = runnerImage.rectTransform.GetSiblingIndex();
        rt.SetSiblingIndex(Mathf.Max(0, runnerIndex - 1));

        StartCoroutine(FadeAndDestroy(img, afterLife));
        var afterRT = (RectTransform)go.transform;

        afterRT.SetAsLastSibling(); // EN ÜSTE
        img.color = new Color(1f, 0f, 0f, 1f); // KIRMIZI, kesin seçilir
    }

    IEnumerator FadeAndDestroy(Image img, float life)
    {
        float t = 0f;
        var c0 = img.color;

        while (t < life)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(c0.a, 0f, t / life);
            img.color = new Color(c0.r, c0.g, c0.b, a);
            yield return null;
        }

        if (img != null) Destroy(img.gameObject);
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