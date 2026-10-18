using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialOverlayController : MonoBehaviour
{
    [Header("Dim")]
    [SerializeField] private Image dimImage;
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.6f;
    [Tooltip("Önceden tanımlı öğretici spot ışığında karartma (yalnız eşleşecek taşlar açık).")]
    [SerializeField, Range(0f, 1f)] private float spotlightAlpha = 0.9f;
    [SerializeField] private float fadeSpeed = 8f;

    [Header("Description (Group 1 only)")]
    [SerializeField] private GameObject descriptionRoot;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image illustrationImage;
    [Tooltip("Description alt kenarı ile grid üst kenarı arasındaki boşluk (piksel, pozitif = grid üstünden uzaklaş)")]
    [SerializeField] private float descriptionYOffset = 10f;

    [Header("Obstacle Hint")]
    [SerializeField, Range(0f, 1f)] private float hintDimAlpha = 0.93f;
    [SerializeField] private TMP_FontAsset hintFont;
    [SerializeField] private Material hintTitleMaterial;
    [SerializeField] private Image obstacleIconImage;
    [SerializeField] private Button hintDismissButton;

    [Header("Hand + Tile Swap (synchronized)")]
    [SerializeField] private RectTransform handIcon;
    [Tooltip("Sprite içinde parmak ucunun pivot noktası. Alt-orta = (0.5, 0)")]
    [SerializeField] private Vector2 handTipPivot    = new Vector2(0.5f, 0f);
    [SerializeField] private float swapDuration      = 0.30f;  // ileri hareket
    [SerializeField] private float holdDuration      = 0.20f;  // B'de bekleme
    [SerializeField] private float returnDuration    = 0.20f;  // geri dönüş
    [SerializeField] private float pauseDuration     = 2.00f;  // tekrar başlamadan bekleme

    private BoardController board;
    private TileView tutorialFrom;
    private TileView tutorialTo;
    private Coroutine swapRoutine;
    private Coroutine fadeRoutine;
    private Action pendingHintDismiss;
    private CanvasGroup hintGroup;
    private Image hintBackdrop;
    private Image hintIcon;
    private TMP_Text hintTitle;
    private TMP_Text hintEyebrow;
    private TMP_Text hintDescription;
    private TMP_Text hintContinue;
    private float hintOpenedAt;
    private bool hintClosing;
    private bool keepHintBlocker;

    public bool IsVisible => gameObject.activeInHierarchy;

    // Spot ışığı (önceden tanımlı öğretici): ekran kararır, yalnız odak taşları açık kalır; tahtanın üstünde ikon + yazı.
    private RectTransform spotlightRoot;
    private CanvasGroup spotlightGroup;
    private TutorialSpotlightGraphic spotlightDim;
    private RectTransform captionRoot;
    private Image captionIcon;
    private TMP_Text captionText;
    private readonly List<Rect> spotRects = new();

    private void Awake()
    {
        board = FindFirstObjectByType<BoardController>();

        if (dimImage != null)
        {
            var c = dimImage.color;
            c.a = 0f;
            dimImage.color = c;
            dimImage.raycastTarget = false;

            if (!dimImage.TryGetComponent(out Button dimBtn)) dimBtn = dimImage.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(OnHintDismissClicked);
        }

        if (handIcon != null)
            foreach (var g in handIcon.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;

        if (hintDismissButton != null)
        {
            hintDismissButton.onClick.AddListener(OnHintDismissClicked);
            hintDismissButton.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
    }

    public void Show(TileView from, TileView to, string description, Sprite illustration)
    {
        if (board == null) board = FindFirstObjectByType<BoardController>();

        tutorialFrom = from;
        tutorialTo   = to;

        if (hintGroup != null) hintGroup.gameObject.SetActive(false);
        if (spotlightRoot != null) spotlightRoot.gameObject.SetActive(false);
        if (dimImage != null) dimImage.gameObject.SetActive(true);

        if (dimImage != null) dimImage.raycastTarget = false;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        bool hasText = !string.IsNullOrEmpty(description);
        if (descriptionRoot != null)  descriptionRoot.SetActive(hasText);
        if (descriptionText != null)  descriptionText.text = description;
        if (illustrationImage != null)
        {
            if (illustration != null)
                illustrationImage.sprite = illustration;
            illustrationImage.gameObject.SetActive(hasText);
        }

        if (hasText) RepositionDescriptionAboveGrid();

        StopFade();
        fadeRoutine = StartCoroutine(FadeDim(dimAlpha));

        StopSwap();
        if (from != null && to != null)
            swapRoutine = StartCoroutine(LoopSwapWithHand(from, to));
    }

    // Generic notices (e.g. the boss goals reminder) have no obstacle heading or
    // subsequent board highlight and can close fully as soon as they are dismissed.
    public void ShowHint(Sprite icon, string description, Action onDismiss)
    {
        ShowHint(icon, null, description, onDismiss);
        keepHintBlocker = false;
    }

    public void ShowHint(Sprite icon, string title, string description, Action onDismiss)
    {
        StopSwap();
        StopFade();
        EnsureHintLayout();
        pendingHintDismiss = onDismiss;
        keepHintBlocker = true;
        hintClosing = false;
        hintOpenedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (spotlightRoot != null) spotlightRoot.gameObject.SetActive(false);
        if (dimImage != null) dimImage.gameObject.SetActive(false);
        if (descriptionRoot != null) descriptionRoot.SetActive(false);
        if (illustrationImage != null) illustrationImage.gameObject.SetActive(false);
        if (obstacleIconImage != null) obstacleIconImage.gameObject.SetActive(false);
        if (hintDismissButton != null) hintDismissButton.gameObject.SetActive(false);

        hintIcon.sprite = icon;
        hintIcon.gameObject.SetActive(icon != null);
        hintTitle.text = title;
        bool hasTitle = !string.IsNullOrEmpty(title);
        hintTitle.gameObject.SetActive(hasTitle);
        hintEyebrow.gameObject.SetActive(hasTitle);
        hintEyebrow.text = GameLocalization.Get("hint_obstacle_introduction");
        hintDescription.text = description;
        bool textOnly = !hasTitle && icon == null;
        hintDescription.rectTransform.anchorMin = new Vector2(0.16f, textOnly ? 0.32f : 0.20f);
        hintDescription.rectTransform.anchorMax = new Vector2(0.84f, textOnly ? 0.68f : 0.40f);
        hintContinue.text = GameLocalization.Get("bridge_tap_continue");
        hintBackdrop.color = new Color(0f, 0f, 0f, hintDimAlpha);
        hintGroup.alpha = 0f;
        hintGroup.gameObject.SetActive(true);
        fadeRoutine = StartCoroutine(FadeHint(1f));
    }

    public void Hide()
    {
        StopSwap();
        SnapTilesBack();
        StopFade();
        fadeRoutine = StartCoroutine(FadeOutThenHide());
    }

    private void OnHintDismissClicked()
    {
        // Ignore the tap that opened the screen and repeated taps during fade-out.
        if (pendingHintDismiss == null || hintClosing || Time.unscaledTime - hintOpenedAt < 0.25f) return;
        hintClosing = true;
        StopFade();
        fadeRoutine = StartCoroutine(DismissHint());
    }

    private IEnumerator DismissHint()
    {
        yield return FadeHint(0f);
        var callback = pendingHintDismiss;
        pendingHintDismiss = null;
        // Keep the transparent raycast blocker through the board highlight so taps
        // cannot reach boosters or other HUD buttons before the manager resumes play.
        if (!keepHintBlocker)
        {
            hintGroup.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }
        callback?.Invoke();
    }

    public void CancelHint()
    {
        if (hintGroup == null || !hintGroup.gameObject.activeSelf) return;
        pendingHintDismiss = null;
        StopFade();
        if (hintGroup != null) hintGroup.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private IEnumerator FadeHint(float target)
    {
        while (!Mathf.Approximately(hintGroup.alpha, target))
        {
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, target,
                Mathf.Max(1f, fadeSpeed) * Time.unscaledDeltaTime);
            yield return null;
        }
    }

    private void EnsureHintLayout()
    {
        if (hintGroup != null) return;

        var root = CreateHintRect("ObstacleIntroduction", transform, Vector2.zero, Vector2.one);
        hintGroup = root.gameObject.AddComponent<CanvasGroup>();
        var canvas = root.gameObject.AddComponent<Canvas>();
        var parentCanvas = GetComponentInParent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerID = parentCanvas != null ? parentCanvas.sortingLayerID : 0;
        canvas.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + 100;
        root.gameObject.AddComponent<GraphicRaycaster>();

        hintBackdrop = root.gameObject.AddComponent<Image>();
        var dismiss = root.gameObject.AddComponent<Button>();
        dismiss.targetGraphic = hintBackdrop;
        dismiss.transition = Selectable.Transition.None;
        dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
        dismiss.onClick.AddListener(OnHintDismissClicked);

        // An open, borderless layout with a narrower reading column that wraps
        // naturally in either language. Explicit line breaks in localized copy also work.
        hintEyebrow = CreateHintText("Introduction", root,
            new Vector2(0.10f, 0.85f), new Vector2(0.90f, 0.92f), 68.4f);
        hintEyebrow.color = new Color(0.38f, 0.90f, 0.88f, 1f);
        hintEyebrow.characterSpacing = 5f;
        hintTitle = CreateHintText("ObstacleName", root,
            new Vector2(0.06f, 0.67f), new Vector2(0.94f, 0.84f), 152f);
        var skin = CommonPopupSkin.Shared;
        if (hintFont != null)
        {
            if (hintTitleMaterial != null) hintTitle.fontSharedMaterial = hintTitleMaterial;
        }
        else if (skin.titleFont != null)
        {
            hintTitle.font = skin.titleFont;
            if (skin.titleMaterial != null) hintTitle.fontSharedMaterial = skin.titleMaterial;
        }
        hintTitle.enableVertexGradient = true;
        var titleTop = new Color(1f, 0.92f, 0.62f, 1f);
        var titleBottom = new Color(1f, 0.65f, 0.22f, 1f);
        hintTitle.colorGradient = new VertexGradient(titleTop, titleTop, titleBottom, titleBottom);

        var iconRect = CreateHintRect("Obstacle", root, new Vector2(0.32f, 0.43f), new Vector2(0.68f, 0.64f));
        hintIcon = iconRect.gameObject.AddComponent<Image>();
        hintIcon.preserveAspect = true;
        hintIcon.raycastTarget = false;
        hintDescription = CreateHintText("Description", root,
            new Vector2(0.16f, 0.20f), new Vector2(0.84f, 0.40f), 68.4f);
        hintDescription.lineSpacing = 12f;
        hintDescription.color = new Color(1f, 0.95f, 0.85f, 1f);
        hintContinue = CreateHintText("Continue", root,
            new Vector2(0.10f, 0.09f), new Vector2(0.90f, 0.16f), 45.6f);
        hintContinue.color = new Color(0.65f, 0.88f, 0.90f, 1f);
    }

    private RectTransform CreateHintRect(string objectName, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private TMP_Text CreateHintText(string objectName, Transform parent, Vector2 min, Vector2 max, float size)
    {
        var label = CreateHintRect(objectName, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        var bodyFont = hintFont != null ? hintFont : CommonPopupSkin.Shared.font;
        if (bodyFont != null) label.font = bodyFont;
        else if (descriptionText != null) label.font = descriptionText.font;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.6f;
        label.fontSizeMax = size;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    // ── Spot ışığı ──

    /// Önceden tanımlı öğretici: pencere YOK. Ekran kararır, yalnız <paramref name="focusCells"/> taşları açık kalır
    /// (joker odağı tahtayı açar; burada yalnız taşlar). Tahtanın üstünde special ikonu + kısa yazı (DynaPuff), el hamleyi gösterir.
    public void ShowSpotlight(TileView from, TileView to, ICollection<Vector2Int> focusCells, string caption, Sprite icon)
    {
        if (board == null) board = FindFirstObjectByType<BoardController>();

        tutorialFrom = from;
        tutorialTo   = to;

        if (hintGroup != null) hintGroup.gameObject.SetActive(false);
        if (descriptionRoot != null) descriptionRoot.SetActive(false);
        if (illustrationImage != null) illustrationImage.gameObject.SetActive(false);
        if (dimImage != null) dimImage.gameObject.SetActive(false);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        BuildSpotlight(focusCells, caption, icon);

        StopFade();
        fadeRoutine = StartCoroutine(FadeSpotlight(1f));

        StopSwap();
        if (from != null && to != null)
            swapRoutine = StartCoroutine(LoopSwapWithHand(from, to));
    }

    private void EnsureSpotlight()
    {
        if (spotlightRoot != null) return;
        spotlightRoot = new GameObject("TutorialSpotlight", typeof(RectTransform)).GetComponent<RectTransform>();
        spotlightRoot.gameObject.layer = gameObject.layer;   // UI kamerası layer 0'ı çizmez
        spotlightRoot.SetParent(transform, false);
        spotlightRoot.anchorMin = Vector2.zero;
        spotlightRoot.anchorMax = Vector2.one;
        spotlightRoot.pivot = new Vector2(0.5f, 0.5f);
        spotlightRoot.offsetMin = spotlightRoot.offsetMax = Vector2.zero;
        spotlightGroup = spotlightRoot.gameObject.AddComponent<CanvasGroup>();
        spotlightGroup.blocksRaycasts = false;
        spotlightGroup.interactable = false;

        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialSpotlightGraphic));
        dimGo.layer = gameObject.layer;
        var dimRt = (RectTransform)dimGo.transform;
        dimRt.SetParent(spotlightRoot, false);
        dimRt.anchorMin = dimRt.anchorMax = dimRt.pivot = new Vector2(0.5f, 0.5f);
        dimRt.sizeDelta = Vector2.zero;   // mesh köşeleri spotlightRoot merkezine göre (aynı uzay)
        spotlightDim = dimGo.GetComponent<TutorialSpotlightGraphic>();
        spotlightDim.raycastTarget = false;

        captionRoot = new GameObject("Caption", typeof(RectTransform)).GetComponent<RectTransform>();
        captionRoot.gameObject.layer = gameObject.layer;
        captionRoot.SetParent(spotlightRoot, false);
        captionRoot.anchorMin = captionRoot.anchorMax = captionRoot.pivot = new Vector2(0.5f, 0.5f);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.layer = gameObject.layer;
        iconGo.transform.SetParent(captionRoot, false);
        captionIcon = iconGo.GetComponent<Image>();
        captionIcon.preserveAspect = true;
        captionIcon.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.layer = gameObject.layer;
        textGo.transform.SetParent(captionRoot, false);
        captionText = textGo.GetComponent<TextMeshProUGUI>();
        captionText.raycastTarget = false;
        captionText.alignment = TextAlignmentOptions.MidlineLeft;
        captionText.textWrappingMode = TextWrappingModes.Normal;
        captionText.enableAutoSizing = true;
        captionText.fontSizeMin = 30f;
        captionText.fontSizeMax = 52f;
        captionText.color = Color.white;
        // Oyun anı yazısı → DynaPuff (CommonPopupSkin.celebrationFont: satın alma "HARİKA!" ile aynı stil).
        var skin = CommonPopupSkin.Shared;
        var font = skin != null && skin.celebrationFont != null ? skin.celebrationFont
                 : skin != null && skin.titleFont != null ? skin.titleFont : null;
        if (font != null) captionText.font = font;
        if (skin != null && skin.celebrationFont != null && skin.celebrationMaterial != null)
            captionText.fontSharedMaterial = skin.celebrationMaterial;
    }

    private void BuildSpotlight(ICollection<Vector2Int> focus, string caption, Sprite icon)
    {
        EnsureSpotlight();
        spotlightRoot.gameObject.SetActive(true);
        spotlightRoot.SetAsFirstSibling();   // elin altında
        spotlightGroup.alpha = 0f;
        spotlightDim.color = new Color(0f, 0f, 0f, spotlightAlpha);
        spotRects.Clear();

        float top = 0f, left = 0f, right = 0f;
        bool hasGrid = TryGetCellGrid(out var xEdge, out var yEdge);
        if (hasGrid)
        {
            int w = board.Width, h = board.Height;
            left = xEdge[0]; right = xEdge[w];
            top = yEdge[0];
            float bottom = yEdge[h];
            const float Far = 20000f;   // ekran boyundan bağımsız: dış paneller her zaman ekranı kaplar
            spotRects.Add(Rect.MinMaxRect(left - Far, top, right + Far, top + Far));
            spotRects.Add(Rect.MinMaxRect(left - Far, bottom - Far, right + Far, bottom));
            spotRects.Add(Rect.MinMaxRect(left - Far, bottom, left, top));
            spotRects.Add(Rect.MinMaxRect(right, bottom, right + Far, top));

            // Tahta içi: odak dışı hücreler, satır başına birleşik şeritler. Kenarlar aynı ızgara dizilerinden →
            // komşu şeritler tam ortak kenarda buluşur (binme/boşluk yok).
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    if (focus != null && focus.Contains(new Vector2Int(x, y))) { x++; continue; }
                    int start = x;
                    while (x < w && (focus == null || !focus.Contains(new Vector2Int(x, y)))) x++;
                    spotRects.Add(Rect.MinMaxRect(xEdge[start], yEdge[y + 1], xEdge[x], yEdge[y]));
                }
            }
        }
        else
        {
            spotRects.Add(Rect.MinMaxRect(-20000f, -20000f, 20000f, 20000f));
        }
        spotlightDim.SetRects(spotRects);
        LayoutCaption(caption, icon, hasGrid, left, right, top);
    }

    // Hücre ızgarası (spotlightRoot uzayında): gerçek bir taşın ekrandaki köşelerinden (sol üst + sağ alt) türetilir.
    // xEdge[i] = i. sütunun sol kenarı, yEdge[j] = j. satırın üst kenarı (y aşağı doğru azalır).
    private bool TryGetCellGrid(out float[] xEdge, out float[] yEdge)
    {
        xEdge = yEdge = null;
        if (board == null || board.Tiles == null || board.Width <= 0) return false;
        TileView reference = null;
        for (int y = 0; y < board.Height && reference == null; y++)
            for (int x = 0; x < board.Width && reference == null; x++)
                if (board.Tiles[x, y] != null && board.Tiles[x, y].RectTransform != null) reference = board.Tiles[x, y];
        if (reference == null) return false;

        var corners = new Vector3[4];   // 0 sol alt, 1 sol üst, 2 sağ üst, 3 sağ alt
        reference.RectTransform.GetWorldCorners(corners);
        Vector2 topLeft = spotlightRoot.InverseTransformPoint(corners[1]);
        Vector2 bottomRight = spotlightRoot.InverseTransformPoint(corners[3]);
        float cw = bottomRight.x - topLeft.x;
        float ch = topLeft.y - bottomRight.y;
        if (cw <= 0.01f || ch <= 0.01f) return false;

        int rx = reference.X, ry = reference.Y;
        xEdge = new float[board.Width + 1];
        yEdge = new float[board.Height + 1];
        for (int i = 0; i <= board.Width; i++) xEdge[i] = topLeft.x + (i - rx) * cw;
        for (int j = 0; j <= board.Height; j++) yEdge[j] = topLeft.y - (j - ry) * ch;
        return true;
    }

    // Tahtanın üstünde dikey blok: special ikonu ortada üstte, yazı onun altında ortalı (yan yana sıkışmasın).
    private void LayoutCaption(string caption, Sprite icon, bool hasGrid, float left, float right, float top)
    {
        bool hasText = !string.IsNullOrEmpty(caption);
        captionRoot.gameObject.SetActive(hasText || icon != null);
        if (!captionRoot.gameObject.activeSelf) return;

        float width = hasGrid ? right - left : 900f;
        float iconSize = icon != null ? 120f : 0f;
        float gap = icon != null && hasText ? 10f : 0f;
        float textHeight = hasText ? 110f : 0f;
        float total = iconSize + gap + textHeight;

        captionRoot.sizeDelta = new Vector2(width, total);
        captionRoot.anchoredPosition = new Vector2(hasGrid ? (left + right) * 0.5f : 0f, (hasGrid ? top : 0f) + total * 0.5f + 24f);

        var iconRt = captionIcon.rectTransform;
        captionIcon.gameObject.SetActive(icon != null);
        captionIcon.sprite = icon;
        iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        iconRt.anchoredPosition = new Vector2(0f, total * 0.5f - iconSize * 0.5f);

        var textRt = captionText.rectTransform;
        captionText.gameObject.SetActive(hasText);
        captionText.text = caption ?? string.Empty;
        captionText.alignment = TextAlignmentOptions.Center;
        textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(width - 20f, textHeight);
        textRt.anchoredPosition = new Vector2(0f, -total * 0.5f + textHeight * 0.5f);
    }

    private IEnumerator FadeSpotlight(float target)
    {
        if (spotlightGroup == null) yield break;
        while (!Mathf.Approximately(spotlightGroup.alpha, target))
        {
            spotlightGroup.alpha = Mathf.MoveTowards(spotlightGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }
    }

    // ── Synchronized swap + hand loop ──

    private IEnumerator LoopSwapWithHand(TileView from, TileView to)
    {
        if (board == null) yield break;

        var rtA = from.RectTransform;
        var rtB = to.RectTransform;
        if (rtA == null || rtB == null) yield break;

        // El ikonunu hazırla
        if (handIcon != null)
        {
            handIcon.pivot     = handTipPivot;
            handIcon.anchorMin = new Vector2(0.5f, 0.5f);
            handIcon.anchorMax = new Vector2(0.5f, 0.5f);
            handIcon.gameObject.SetActive(true);
        }

        Vector3 posA = GetTileCenter(from);
        Vector3 posB = GetTileCenter(to);

        while (true)
        {
            Vector2 originA = new Vector2(from.X * board.TileSize, -from.Y * board.TileSize);
            Vector2 originB = new Vector2(to.X   * board.TileSize, -to.Y   * board.TileSize);

            // İleri: taşlar A→B, el A→B
            float t = 0f;
            while (t < swapDuration)
            {
                t += Time.deltaTime;
                float k = Smoothstep(Mathf.Clamp01(t / swapDuration));
                rtA.anchoredPosition = Vector2.LerpUnclamped(originA, originB, k);
                rtB.anchoredPosition = Vector2.LerpUnclamped(originB, originA, k);
                if (handIcon != null)
                    handIcon.position = Vector3.Lerp(posA, posB, k);
                yield return null;
            }
            rtA.anchoredPosition = originB;
            rtB.anchoredPosition = originA;
            if (handIcon != null) handIcon.position = posB;

            // B'de bekle
            yield return new WaitForSeconds(holdDuration);

            // Geri: taşlar B→A, el sabit (veya sönük)
            t = 0f;
            while (t < returnDuration)
            {
                t += Time.deltaTime;
                float k = Smoothstep(Mathf.Clamp01(t / returnDuration));
                rtA.anchoredPosition = Vector2.LerpUnclamped(originB, originA, k);
                rtB.anchoredPosition = Vector2.LerpUnclamped(originA, originB, k);
                yield return null;
            }

            from.SnapToGrid(board.TileSize);
            to.SnapToGrid(board.TileSize);
            if (handIcon != null) handIcon.position = posA;

            // Uzun bekleme — kullanıcı hamle yapabilsin
            yield return new WaitForSeconds(pauseDuration);
        }
    }

    private static float Smoothstep(float k) => k * k * (3f - 2f * k);

    private static Vector3 GetTileCenter(TileView tile)
    {
        var corners = new Vector3[4];
        tile.RectTransform.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    // ── Dim ──

    private IEnumerator FadeDim(float target)
    {
        if (dimImage == null) yield break;
        Color c = dimImage.color;
        while (!Mathf.Approximately(c.a, target))
        {
            c.a = Mathf.MoveTowards(c.a, target, fadeSpeed * Time.unscaledDeltaTime);
            dimImage.color = c;
            yield return null;
        }
        c.a = target;
        dimImage.color = c;
    }

    private IEnumerator FadeOutThenHide()
    {
        if (dimImage != null)
        {
            Color c = dimImage.color;
            while (c.a > 0f)
            {
                c.a = Mathf.MoveTowards(c.a, 0f, fadeSpeed * Time.unscaledDeltaTime);
                dimImage.color = c;
                yield return null;
            }
        }

        if (spotlightGroup != null && spotlightRoot != null && spotlightRoot.gameObject.activeSelf)
        {
            while (spotlightGroup.alpha > 0f)
            {
                spotlightGroup.alpha = Mathf.MoveTowards(spotlightGroup.alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
                yield return null;
            }
            spotlightRoot.gameObject.SetActive(false);
        }

        if (handIcon != null) handIcon.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    // ── Description positioning ──

    private void RepositionDescriptionAboveGrid()
    {
        if (descriptionRoot == null || board == null) return;
        if (board.TilesRoot == null || board.Width == 0 || board.Height == 0) return;

        var rt = descriptionRoot.GetComponent<RectTransform>();
        if (rt == null) return;

        var parentRect = rt.parent as RectTransform;
        if (parentRect == null) return;

        // Grid'in üst kenarını (row-0 top edge) parent local space'e çevir
        float gridCenterX  = (board.Width - 1) * board.TileSize * 0.5f;
        float gridTopEdgeY =  board.TileSize * 0.5f; // TilesRoot local: row-0 center=0, top edge=+TileSize/2

        Vector3 worldPt = board.TilesRoot.TransformPoint(new Vector3(gridCenterX, gridTopEdgeY, 0f));
        Vector3 localPt = parentRect.InverseTransformPoint(worldPt);

        // Description alt kenarı = grid üst kenarı + offset → merkez = alt kenar + yarı yükseklik
        float halfH = rt.rect.height * 0.5f;
        Vector3 lp  = rt.localPosition;
        lp.y = localPt.y + halfH + descriptionYOffset;
        rt.localPosition = lp;
    }

    // ── Helpers ──

    private void SnapTilesBack()
    {
        if (board == null) return;
        if (tutorialFrom != null) tutorialFrom.SnapToGrid(board.TileSize);
        if (tutorialTo   != null) tutorialTo.SnapToGrid(board.TileSize);
    }

    private void StopSwap()
    {
        if (swapRoutine != null) { StopCoroutine(swapRoutine); swapRoutine = null; }
        if (handIcon != null)    handIcon.gameObject.SetActive(false);
    }

    private void StopFade()
    {
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
    }
}
