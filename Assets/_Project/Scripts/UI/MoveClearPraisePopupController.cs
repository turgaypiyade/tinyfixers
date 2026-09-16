using System.Collections;
using System.Collections.Generic;
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

    [Header("Praise Lettering")]
    [SerializeField, Range(0f, 32f)] private float textArcHeight = 13f;
    [SerializeField, Range(48f, 120f)] private float maxPraiseFontSize = 92f;
    [SerializeField, Range(0f, 12f)] private float textDepth = 5f;
    [SerializeField] private Vector2 letteringOffset = new Vector2(8f, 0f);

    private BoardController board;
    private Coroutine activeRoutine;
    private RectTransform activePopup;
    private TMP_FontAsset resolvedFont;
    private readonly List<Material> popupMaterials = new List<Material>(3);

    public void Bind(BoardController target)
    {
        // The controller is a persistent host; only its generated popup is temporary.
        // Authored hosts may be saved inactive in the scene.
        if (target != null && !gameObject.activeSelf)
            gameObject.SetActive(true);

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

        StopActivePopup();
    }

    private void OnDisable()
    {
        // Unity stops host coroutines on deactivation. Also remove the unfinished
        // popup so it cannot reappear frozen when the Canvas is enabled again.
        StopActivePopup();
    }

    private void HandleMoveClearPraise(int clearedTiles)
    {
        // C# events can still reach us while a parent Canvas or this component is
        // disabled. Do not start a coroutine or reopen a hidden screen in that case.
        if (!isActiveAndEnabled || clearedTiles < minimumClearedTiles)
            return;

        StopActivePopup();
        activeRoutine = StartCoroutine(PlayPopup(clearedTiles));
    }

    private void StopActivePopup()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        DestroyActivePopup();
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

        float tilt = Random.Range(-2.5f, 2.5f);
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
            popup.localScale = Vector3.one * Mathf.LerpUnclamped(0.48f, 1.08f, e);
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
            popup.localScale = Vector3.one * Mathf.LerpUnclamped(1.08f, 1f, e);
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

    private void DestroyPopup(RectTransform popup)
    {
        foreach (var material in popupMaterials)
            if (material != null) Destroy(material);
        popupMaterials.Clear();

        if (!IsAlive(popup))
            return;

        popup.gameObject.SetActive(false);
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

        Vector2 burstSize = new Vector2(badgeSize.x * 1.18f, badgeSize.y * 1.45f);
        CreateJagged("Shadow", rootRt, burstSize + new Vector2(54f, 48f), new Vector2(3f, -8f), new Color(0.25f, 0.08f, 0.015f, 0.24f), 9, 0.51f);
        CreateJagged("RedRim", rootRt, burstSize + new Vector2(48f, 44f), Vector2.zero, new Color(0.92f, 0.13f, 0.015f, 1f), 9, 0.51f);
        CreateJagged("OrangeRim", rootRt, burstSize + new Vector2(24f, 22f), Vector2.zero, rim, 9, 0.51f);
        CreateJagged("Badge", rootRt, burstSize, Vector2.zero, fill, 9, 0.51f);
        CreateJagged("SparkA", rootRt, new Vector2(34f, 34f), new Vector2(-burstSize.x * 0.44f, burstSize.y * 0.55f), accent, 5, 0.48f);
        CreateJagged("SparkB", rootRt, new Vector2(26f, 26f), new Vector2(burstSize.x * 0.46f, burstSize.y * 0.44f), accent, 5, 0.48f);
        CreateJagged("SparkC", rootRt, new Vector2(22f, 22f), new Vector2(burstSize.x * 0.31f, -burstSize.y * 0.55f), accent, 5, 0.48f);

        var font = ResolveFont();
        Color darkBrown = new Color(0.22f, 0.085f, 0.025f, 1f);
        Color orange = new Color(1f, 0.27f, 0.005f, 1f);
        float extrusion = textDepth * 1.4f;

        // All three layers use the exact same font size and glyph layout. Their
        // independent SDF materials keep this styling off the shared font asset.
        var outline = CreatePraiseText("LetterOutline", rootRt, label, font,
            letteringOffset + new Vector2(0f, -extrusion - 2f), darkBrown, darkBrown, darkBrown, 0.36f);
        var depth = CreatePraiseText("LetterDepth", rootRt, label, font,
            letteringOffset + new Vector2(0f, -extrusion), new Color(1f, 0.49f, 0.015f), orange, orange, 0.22f);
        var face = CreatePraiseText("Label", rootRt, label, font, letteringOffset,
            new Color(1f, 0.99f, 0.84f), new Color(1f, 0.66f, 0.06f),
            new Color(1f, 0.42f, 0.01f), 0.11f);

        face.enableAutoSizing = true;
        face.fontSizeMin = 38f;
        face.fontSizeMax = maxPraiseFontSize;
        face.ForceMeshUpdate();
        outline.fontSize = depth.fontSize = face.fontSize;
        outline.ForceMeshUpdate();
        depth.ForceMeshUpdate();

        return rootRt;
    }

    private TextMeshProUGUI CreatePraiseText(
        string name, RectTransform parent, string label, TMP_FontAsset font,
        Vector2 offset, Color top, Color bottom, Color outlineColor, float outlineWidth)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(22f, 12f) + offset;
        rt.offsetMax = new Vector2(-22f, -12f) + offset;

        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = label;
        text.fontStyle = FontStyles.Normal;
        text.fontSize = maxPraiseFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(top, top, bottom, bottom);
        text.raycastTarget = false;
        text.extraPadding = true;

        // Own these three materials explicitly; the installed TMP version does
        // not dispose font-material instances when a popup text is destroyed.
        ShaderUtilities.GetShaderPropertyIDs();
        var material = new Material(text.fontSharedMaterial);
        popupMaterials.Add(material);
        text.fontSharedMaterial = material;
        material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        material.DisableKeyword("UNDERLAY_ON");
        material.DisableKeyword("UNDERLAY_INNER");
        text.UpdateMeshPadding();
        float arc = textArcHeight;
        text.OnPreRenderText += info => ApplyTextArc(info, arc);
        return text;
    }

    private static void ApplyTextArc(TMP_TextInfo info, float height)
    {
        if (info.characterCount == 0 || height <= 0f) return;

        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            left = Mathf.Min(left, c.origin);
            right = Mathf.Max(right, c.xAdvance);
        }
        if (float.IsInfinity(left) || right - left < 1f) return;

        float center = (left + right) * 0.5f;
        float halfWidth = (right - left) * 0.5f;
        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            var vertices = info.meshInfo[c.materialReferenceIndex].vertices;
            int first = c.vertexIndex;
            float x = (c.origin + c.xAdvance) * 0.5f;
            float normalized = (x - center) / halfWidth;
            float lift = height * (1f - normalized * normalized) - height * 0.5f;
            float angle = Mathf.Atan(-2f * height * normalized / halfWidth) * Mathf.Rad2Deg;
            Vector3 pivot = new Vector3(x, c.baseLine, 0f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            for (int v = 0; v < 4; v++)
                vertices[first + v] = rotation * (vertices[first + v] - pivot)
                    + pivot + Vector3.up * lift;
        }
    }

    [ContextMenu("Preview GREAT (Play Mode)")]
    private void PreviewGreat()
    {
        if (Application.isPlaying)
            HandleMoveClearPraise(Mathf.Max(50, minimumClearedTiles));
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
            rim = new Color(1f, 0.27f, 0.015f, 1f);
            accent = new Color(1f, 0.98f, 0.58f, 1f);
            return;
        }

        if (clearedTiles >= 40)
        {
            label = "WOW!";
            fill = new Color(1f, 0.84f, 0.12f, 1f);
            rim = new Color(1f, 0.27f, 0.015f, 1f);
            accent = new Color(1f, 0.98f, 0.58f, 1f);
            return;
        }

        label = "GOOD!";
        fill = new Color(1f, 0.84f, 0.12f, 1f);
        rim = new Color(1f, 0.27f, 0.015f, 1f);
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
