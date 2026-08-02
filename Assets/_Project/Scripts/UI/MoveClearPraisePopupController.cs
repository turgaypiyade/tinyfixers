using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MoveClearPraisePopupController : MonoBehaviour
{
    [SerializeField] private int minimumClearedTiles = 30;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 72f);
    [SerializeField] private Vector2 badgeSize = new Vector2(390f, 174f);
    [SerializeField] private float holdDuration = 0.58f;
    [SerializeField] private TMP_FontAsset preferredFont;

    private BoardController board;
    private Coroutine activeRoutine;
    private RectTransform activePopup;
    private TMP_FontAsset resolvedFont;

    public void Bind(BoardController target)
    {
        if (board == target)
            return;

        if (board != null)
            board.OnMoveClearPraise -= HandleMoveClearPraise;

        board = target;

        if (board != null)
            board.OnMoveClearPraise += HandleMoveClearPraise;
    }

    private void OnDestroy()
    {
        if (board != null)
            board.OnMoveClearPraise -= HandleMoveClearPraise;

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        activePopup = null;
    }

    private void HandleMoveClearPraise(int clearedTiles)
    {
        if (clearedTiles < minimumClearedTiles)
            return;

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        DestroyActivePopup();

        activeRoutine = StartCoroutine(PlayPopup(clearedTiles));
    }

    private IEnumerator PlayPopup(int clearedTiles)
    {
        RectTransform popup = BuildPopup(clearedTiles);
        activePopup = popup;

        if (!IsAlive(popup))
        {
            ClearRoutineReferences(popup);
            yield break;
        }

        var group = popup.GetComponent<CanvasGroup>();
        if (!IsAlive(popup) || !IsAlive(group))
        {
            ClearRoutineReferences(popup);
            yield break;
        }

        float tilt = Random.Range(-5.5f, 5.5f);
        Vector2 start = anchoredPosition + new Vector2(0f, -24f);
        Vector2 peak = anchoredPosition + new Vector2(0f, 10f);
        Vector2 end = anchoredPosition + new Vector2(0f, 42f);

        const float inDuration = 0.20f;
        const float settleDuration = 0.16f;
        const float outDuration = 0.24f;

        float t = 0f;
        while (t < inDuration)
        {
            if (!IsAlive(popup) || !IsAlive(group))
            {
                ClearRoutineReferences(popup);
                yield break;
            }

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / inDuration);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            group.alpha = e;
            popup.anchoredPosition = Vector2.LerpUnclamped(start, peak, e);
            popup.localScale = Vector3.one * Mathf.LerpUnclamped(0.36f, 1.18f, e);
            popup.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(tilt * 1.8f, tilt, e));
            yield return null;
        }

        t = 0f;
        while (t < settleDuration)
        {
            if (!IsAlive(popup))
            {
                ClearRoutineReferences(popup);
                yield break;
            }

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / settleDuration);
            float e = 1f - Mathf.Pow(1f - k, 2f);
            popup.anchoredPosition = Vector2.LerpUnclamped(peak, anchoredPosition, e);
            popup.localScale = Vector3.one * Mathf.LerpUnclamped(1.18f, 1f, e);
            yield return null;
        }

        yield return new WaitForSeconds(holdDuration);

        t = 0f;
        while (t < outDuration)
        {
            if (!IsAlive(popup) || !IsAlive(group))
            {
                ClearRoutineReferences(popup);
                yield break;
            }

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / outDuration);
            group.alpha = 1f - k;
            popup.anchoredPosition = Vector2.LerpUnclamped(anchoredPosition, end, k);
            popup.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 0.88f, k);
            yield return null;
        }

        DestroyPopup(popup);
        ClearRoutineReferences(popup);
    }

    private void DestroyActivePopup()
    {
        var popup = activePopup;
        activePopup = null;
        DestroyPopup(popup);
    }

    private static void DestroyPopup(RectTransform popup)
    {
        if (!IsAlive(popup))
            return;

        Destroy(popup.gameObject);
    }

    private void ClearRoutineReferences(RectTransform popup)
    {
        if (activePopup == popup)
            activePopup = null;

        activeRoutine = null;
    }

    private static bool IsAlive(UnityEngine.Object obj)
    {
        return obj != null;
    }

    private RectTransform BuildPopup(int clearedTiles)
    {
        PickTier(clearedTiles, out string label, out Color fill, out Color rim, out Color accent);

        var root = new GameObject("MoveClearPraise", typeof(RectTransform), typeof(CanvasGroup));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.SetParent(transform, false);
        rootRt.SetAsLastSibling();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = anchoredPosition;
        rootRt.sizeDelta = badgeSize + new Vector2(44f, 42f);

        var group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        CreateJagged("Shadow", rootRt, badgeSize + new Vector2(34f, 28f), new Vector2(7f, -9f), new Color(0.05f, 0.04f, 0.04f, 0.34f), 13, 0.47f);
        CreateJagged("Rim", rootRt, badgeSize + new Vector2(34f, 30f), Vector2.zero, rim, 13, 0.45f);
        CreateJagged("Badge", rootRt, badgeSize, Vector2.zero, fill, 13, 0.50f);
        CreateJagged("SparkA", rootRt, new Vector2(38f, 38f), new Vector2(-badgeSize.x * 0.45f, badgeSize.y * 0.34f), accent, 6, 0.42f);
        CreateJagged("SparkB", rootRt, new Vector2(30f, 30f), new Vector2(badgeSize.x * 0.45f, -badgeSize.y * 0.34f), accent, 6, 0.42f);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(rootRt, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(34f, 30f);
        textRt.offsetMax = new Vector2(-34f, -30f);

        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        var font = ResolveFont();
        if (font != null)
            text.font = font;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = 58f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 38f;
        text.fontSizeMax = 62f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = new Color(0.04f, 0.035f, 0.02f, 1f);
        text.outlineWidth = 0.12f;
        text.outlineColor = new Color(1f, 0.96f, 0.50f, 0.9f);
        text.raycastTarget = false;

        return rootRt;
    }

    private TMP_FontAsset ResolveFont()
    {
        if (preferredFont != null)
            return preferredFont;

        if (resolvedFont != null)
            return resolvedFont;

        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        TMP_FontAsset fallbackInter = null;

        for (int i = 0; i < fonts.Length; i++)
        {
            var font = fonts[i];
            if (font == null || string.IsNullOrEmpty(font.name) || !font.name.StartsWith("Inter_"))
                continue;

            fallbackInter ??= font;

            if (font.name.Contains("ExtraBold") || font.name.Contains("Bold"))
            {
                resolvedFont = font;
                return resolvedFont;
            }
        }

        resolvedFont = fallbackInter;
        return resolvedFont;
    }

    private static void CreateJagged(
        string name,
        RectTransform parent,
        Vector2 size,
        Vector2 position,
        Color color,
        int spikes,
        float innerRadius)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(JaggedBadgeGraphic));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var graphic = go.GetComponent<JaggedBadgeGraphic>();
        graphic.color = color;
        graphic.Spikes = Mathf.Max(4, spikes);
        graphic.InnerRadius = Mathf.Clamp(innerRadius, 0.35f, 0.95f);
        graphic.raycastTarget = false;
    }

    private static void PickTier(int clearedTiles, out string label, out Color fill, out Color rim, out Color accent)
    {
        if (clearedTiles >= 50)
        {
            label = "GREAT!";
            fill = new Color(1f, 0.84f, 0.12f, 1f);
            rim = new Color(0.94f, 0.10f, 0.08f, 1f);
            accent = new Color(1f, 0.98f, 0.58f, 1f);
            return;
        }

        if (clearedTiles >= 40)
        {
            label = "WOW!";
            fill = new Color(1f, 0.84f, 0.12f, 1f);
            rim = new Color(0.94f, 0.10f, 0.08f, 1f);
            accent = new Color(1f, 0.98f, 0.58f, 1f);
            return;
        }

        label = "GOOD!";
        fill = new Color(1f, 0.84f, 0.12f, 1f);
        rim = new Color(0.94f, 0.10f, 0.08f, 1f);
        accent = new Color(1f, 0.98f, 0.58f, 1f);
    }
}

public sealed class JaggedBadgeGraphic : MaskableGraphic
{
    [SerializeField] private int spikes = 18;
    [SerializeField, Range(0.35f, 0.95f)] private float innerRadius = 0.74f;

    public int Spikes
    {
        get => spikes;
        set
        {
            spikes = Mathf.Max(4, value);
            SetVerticesDirty();
        }
    }

    public float InnerRadius
    {
        get => innerRadius;
        set
        {
            innerRadius = Mathf.Clamp(value, 0.35f, 0.95f);
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var rt = transform as RectTransform;
        if (rt == null)
            return;

        Rect rect = rt.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        Vector2 center = rect.center;
        float rx = rect.width * 0.5f;
        float ry = rect.height * 0.5f;
        int pointCount = Mathf.Max(8, spikes * 2);

        vh.AddVert(center, color, new Vector2(0.5f, 0.5f));

        for (int i = 0; i < pointCount; i++)
        {
            float angle = -Mathf.PI * 0.5f + (Mathf.PI * 2f * i / pointCount);
            float radius = (i & 1) == 0 ? 1f : innerRadius;
            Vector2 point = center + new Vector2(Mathf.Cos(angle) * rx * radius, Mathf.Sin(angle) * ry * radius);
            vh.AddVert(point, color, new Vector2((point.x - rect.xMin) / rect.width, (point.y - rect.yMin) / rect.height));
        }

        for (int i = 1; i <= pointCount; i++)
        {
            int next = i == pointCount ? 1 : i + 1;
            vh.AddTriangle(0, i, next);
        }
    }
}
