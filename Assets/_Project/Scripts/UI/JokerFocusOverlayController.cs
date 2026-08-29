using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Joker seçimi sırasında:
///   - BoardContent (grid) açık ve tıklanabilir kalır
///   - Grid dışı alan (TopHUD + BottomBar) iki panel ile karartılır ve bloklanır
///   - Seçili joker: JokerGrid'in tüm JokerGrid canvas'ı overlay'in üstüne çıkar
///   - Joker slot'larına ayrı Canvas EKLENMEZ → MissingComponentException yok
///   - board.SetInputLocked() ÇAĞRILMAZ → grid her zaman aktif
/// </summary>
public class JokerFocusOverlayController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool verboseDebugLogs = true;

    [Header("Texts")]
    [SerializeField] private string singleTargetText = "Kırmak istediğin objeye tıkla";
    [SerializeField] private string rowTargetText    = "Yok etmek istediğin satıra tıkla";
    [SerializeField] private string columnTargetText = "Yok etmek istediğin sütuna tıkla";
    [SerializeField] private string shuffleText      = "Herhangi bir yere tıkla";

    [Header("Titles (üstteki büyük başlık)")]
    [SerializeField] private string singleTitle  = "Wonder Çekici!";
    [SerializeField] private string rowTitle     = "Wonder Roketi!";
    [SerializeField] private string columnTitle  = "Wonder Asansör!";
    [SerializeField] private string shuffleTitle = "Wonder Karıştırıcı!";
    [SerializeField] private Color  titleColor   = new Color(1f, 0.85f, 0.2f, 1f);   // referanstaki sarı

    [Header("Selection Highlight")]
    [SerializeField] private bool   useProceduralSelectionFrame   = false;
    [SerializeField] private string selectedFrameSpriteName       = "Top_border_img_v2";
    [SerializeField] private float  selectedFramePadding          = 10f;
    [SerializeField] private Color  selectedFrameOutlineColor     = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private float  disabledJokerAlpha            = 0.35f;
    [SerializeField] private float  selectedJokerScale            = 1.1f;
    [SerializeField] private string selectedGlowSpriteName        = "";
    [SerializeField] private float  selectedGlowAlpha             = 0.9f;
    [SerializeField] private float  selectedGlowScale             = 1.3f;
    [SerializeField] private Color  selectionOutlineColor         = new Color(0.45f, 0.9f, 1f, 1f);
    [SerializeField] private float  selectedOverlayAlpha          = 0.95f;

    // ─────────────────────────────────────────────────────────────────────────
    private const int MaxJokerSlots = 8;

    private readonly Image[]     jokerIcons            = new Image[MaxJokerSlots];
    private readonly Button[]    jokerButtons          = new Button[MaxJokerSlots];
    private readonly Image[]     jokerSelectionFrames  = new Image[MaxJokerSlots];
    private readonly Image[]     jokerSelectionGlows   = new Image[MaxJokerSlots];
    private readonly Vector3[]   jokerBaseScales       = new Vector3[MaxJokerSlots];
    private readonly Vector3[]   jokerIconBaseScales   = new Vector3[MaxJokerSlots];
    private readonly Transform[] jokerSlotTransforms   = new Transform[MaxJokerSlots];
    private readonly int[]       jokerBoosterIndices   = new int[MaxJokerSlots];
    private readonly bool[]      jokerIsBoosterSlot    = new bool[MaxJokerSlots];
    private int cachedJokerCount;

    private Canvas          rootCanvas;
    private BoardController board;
    private RectTransform   boardContentRect;

    // Overlay: iki panel (üst + alt) — BoardContent ortada açık kalır
    private GameObject      overlayRoot;
    private Image           overlayTop;
    private Image           overlayBottom;
    private Image           overlayLeft;
    private Image           overlayRight;
    private readonly System.Collections.Generic.List<Image> holePanels = new System.Collections.Generic.List<Image>();
    private TextMeshProUGUI descriptionText;
    private Image           titleIcon;        // üstteki booster ikonu
    private TextMeshProUGUI titleText;        // üstteki büyük başlık

    private int    selectedJokerIndex  = -1;
    private Sprite selectionFrameSprite;
    private Sprite selectionGlowSprite;
    private int    lastHandledTapFrame = -1;
    private int    lastHandledTapIndex = -1;

    private static bool sceneHookRegistered;
    private static readonly HashSet<string> missingFrameSpriteWarnings = new();
    private static readonly HashSet<string> invalidFrameBorderWarnings = new();
    private static readonly HashSet<string> fullRectFrameWarnings      = new();

    // ── Pointer proxy ─────────────────────────────────────────────────────────
    private sealed class JokerPointerProxy : MonoBehaviour, IPointerClickHandler
    {
        private JokerFocusOverlayController owner;
        private int visualIndex = -1;
        public void Init(JokerFocusOverlayController o, int i) { owner = o; visualIndex = i; }
        public void OnPointerClick(PointerEventData e) => owner?.HandleJokerTap(visualIndex);
    }

    // ── Auto-install ──────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterInstaller()
    {
        if (sceneHookRegistered) return;
        sceneHookRegistered = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install() => TryInstallController();

    private static void HandleSceneLoaded(Scene s, LoadSceneMode m) => TryInstallController();

    private static void TryInstallController()
    {
        var jokerGrid = FindJokerGrid();
        if (jokerGrid == null || jokerGrid.GetComponent<JokerFocusOverlayController>() != null) return;
        jokerGrid.AddComponent<JokerFocusOverlayController>();
    }

    private static GameObject FindJokerGrid()
    {
        foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (tr == null || tr.name != "JokerGrid") continue;
            var go = tr.gameObject;
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
            if ((go.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
            return go;
        }
        return null;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) rootCanvas = FindFirstObjectByType<Canvas>();

        selectionFrameSprite = ResolveSelectionFrameSprite();
        if (selectionFrameSprite == null) useProceduralSelectionFrame = true;
        selectionGlowSprite = ResolveSpriteByName(selectedGlowSpriteName);

        ResolveBoardContentRect();
        CacheJokerIcons();
        CreateOverlayUi();
        SetOverlayVisible(false);
        SetSelectedJoker(-1);
    }

    private void Start()
    {
        ResolveBoardContentRect();
        CacheJokerIcons();
        SetSelectedJoker(-1);
    }

    private void OnEnable()
    {
        if (board == null) board = FindFirstObjectByType<BoardController>();
        if (board != null) board.OnBoosterTargetingChanged += HandleBoosterTargetingChanged;
    }

    private void OnDisable()
    {
        if (board != null) board.OnBoosterTargetingChanged -= HandleBoosterTargetingChanged;
        SetOverlayVisible(false);
    }

    private void OnTransformChildrenChanged()
    {
        CacheJokerIcons();
        SetSelectedJoker(selectedJokerIndex >= 0 && selectedJokerIndex < cachedJokerCount
            ? selectedJokerIndex : -1);
    }

    // ── BoardContent rect ─────────────────────────────────────────────────────
    private void ResolveBoardContentRect()
    {
        boardContentRect = null;

        foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rt == null || !rt.gameObject.scene.IsValid() || !rt.gameObject.scene.isLoaded) continue;
            if (rt.name == "BoardContent")
            {
                boardContentRect = rt;
                DebugLog($"[JokerFocus] BoardContent rect found: {rt.name}, pos={rt.position}");
                return;
            }
        }

        string[] fallbackNames = { "BoardMask", "BoardFrame", "BoardRoot", "BoardArea" };
        foreach (var fname in fallbackNames)
        {
            foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rt == null || !rt.gameObject.scene.IsValid()) continue;
                if (rt.name == fname)
                {
                    boardContentRect = rt;
                    Debug.LogWarning($"[JokerFocus] BoardContent not found. Fallback: '{fname}'");
                    return;
                }
            }
        }

        if (board == null) board = FindFirstObjectByType<BoardController>();
        if (board != null)
        {
            boardContentRect = board.GetComponent<RectTransform>();
            Debug.LogWarning("[JokerFocus] Using BoardController RectTransform as last resort.");
        }
    }

    // ── Overlay UI ────────────────────────────────────────────────────────────
    private void CreateOverlayUi()
    {
        if (rootCanvas == null) return;

        overlayRoot = new GameObject("JokerFocusOverlayRoot", typeof(RectTransform));
        overlayRoot.transform.SetParent(rootCanvas.transform, false);
        StretchFull(overlayRoot.GetComponent<RectTransform>());
        overlayRoot.AddComponent<GraphicRaycaster>();

        overlayTop    = MakeBlockPanel(overlayRoot.transform, "OverlayTop");
        overlayBottom = MakeBlockPanel(overlayRoot.transform, "OverlayBottom");
        overlayLeft   = MakeBlockPanel(overlayRoot.transform, "OverlayLeft");
        overlayRight  = MakeBlockPanel(overlayRoot.transform, "OverlayRight");

        // Üstteki booster ikonu (sol üst).
        var iconGo = new GameObject("JokerFocusIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(overlayRoot.transform, false);
        titleIcon = iconGo.GetComponent<Image>();
        titleIcon.raycastTarget = false;
        titleIcon.preserveAspect = true;
        var ir = iconGo.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.03f, 0.785f);
        ir.anchorMax = new Vector2(0.245f, 0.895f);   // daha büyük + daha aşağı
        ir.offsetMin = ir.offsetMax = Vector2.zero;

        // Büyük başlık (ikonun sağı).
        var titleGo = new GameObject("JokerFocusTitle",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(overlayRoot.transform, false);
        titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.fontSize         = 72;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin      = 22;
        titleText.fontSizeMax      = 80;
        titleText.alignment        = TextAlignmentOptions.Left;
        titleText.color            = titleColor;
        titleText.fontStyle        = FontStyles.Bold;
        titleText.raycastTarget    = false;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;   // tek satır (autosize küçültür)
        titleText.overflowMode     = TextOverflowModes.Overflow;
        var titr = titleGo.GetComponent<RectTransform>();
        titr.anchorMin = new Vector2(0.26f, 0.832f);
        titr.anchorMax = new Vector2(0.98f, 0.898f);
        titr.offsetMin = titr.offsetMax = Vector2.zero;

        // Açıklama (başlığın altı).
        var textGo = new GameObject("JokerFocusDescription",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(overlayRoot.transform, false);

        descriptionText = textGo.GetComponent<TextMeshProUGUI>();
        descriptionText.fontSize         = 44;
        descriptionText.enableAutoSizing = true;
        descriptionText.fontSizeMin      = 14;
        descriptionText.fontSizeMax      = 46;
        descriptionText.alignment        = TextAlignmentOptions.Left;
        descriptionText.color            = Color.white;
        descriptionText.fontStyle        = FontStyles.Bold;
        descriptionText.raycastTarget    = false;
        descriptionText.textWrappingMode = TextWrappingModes.NoWrap;   // tek satır
        descriptionText.overflowMode     = TextOverflowModes.Overflow;

        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.26f, 0.768f);
        tr.anchorMax = new Vector2(0.98f, 0.828f);
        tr.offsetMin = tr.offsetMax = Vector2.zero;

        // KRİTİK: new GameObject UI'ı layer 0'da doğuyor; Screen Space Camera sadece kendi layer'ını
        // render ettiği için overlay cull'lanıp görünmez oluyordu (overlayScale=0 red herring).
        // Tüm overlay alt-ağacını canvas/controller layer'ına al.
        int uiLayer = rootCanvas != null ? rootCanvas.gameObject.layer : gameObject.layer;
        SetLayerRecursive(overlayRoot.transform, uiLayer);
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }

    private Image MakeBlockPanel(Transform parent, string goName)
    {
        var go  = new GameObject(goName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color         = new Color(0f, 0f, 0f, selectedOverlayAlpha);
        img.raycastTarget = true;
        return img;
    }

    private void RefreshOverlayPanels()
    {
        if (overlayTop == null || overlayBottom == null) return;
        ResolveBoardContentRect();

        var overlayRect = overlayRoot != null ? overlayRoot.GetComponent<RectTransform>() : null;
        if (boardContentRect == null || overlayRect == null)
        {
            SetAnchors(overlayTop.rectTransform,    0, 0, 1, 1);
            SetAnchors(overlayBottom.rectTransform, 0, 0, 0, 0);
            return;
        }

        var corners = new Vector3[4];
        boardContentRect.GetWorldCorners(corners);

        Vector2 bl = overlayRect.InverseTransformPoint(corners[0]);
        Vector2 tr = overlayRect.InverseTransformPoint(corners[2]);

        Rect cr        = overlayRect.rect;
        float normBotY = Mathf.Clamp01((bl.y - cr.yMin) / cr.height);
        float normTopY = Mathf.Clamp01((tr.y - cr.yMin) / cr.height);
        float normLeftX  = Mathf.Clamp01((bl.x - cr.xMin) / cr.width);
        float normRightX = Mathf.Clamp01((tr.x - cr.xMin) / cr.width);

        SetAnchors(overlayTop.rectTransform,    0, normTopY, 1, 1);
        SetAnchors(overlayBottom.rectTransform, 0, 0,        1, normBotY);
        // Board bbox'ın YANLARI (band içinde) da karartılır.
        if (overlayLeft  != null) SetAnchors(overlayLeft.rectTransform,  0,          normBotY, normLeftX, normTopY);
        if (overlayRight != null) SetAnchors(overlayRight.rectTransform, normRightX, normBotY, 1,         normTopY);

        // Board İÇİNDEKİ hole hücreleri (staircase/boşluklar) tek tek karartılır.
        RefreshHolePanels(overlayRect);

        DebugLog($"[JokerFocus] Panels normBot={normBotY:F3} normTop={normTopY:F3} L={normLeftX:F3} R={normRightX:F3}");
    }

    // Board içindeki HOLE hücrelerini karartan panelleri (pool) yerleştirir.
    private void RefreshHolePanels(RectTransform overlayRect)
    {
        if (board == null || overlayRect == null) return;

        int w = board.Width, h = board.Height;
        if (w <= 1 || h <= 0) return;

        // Bir hücrenin overlay-local boyutu: iki komşu hücre merkezinin local farkı.
        Vector2 l00 = overlayRect.InverseTransformPoint(board.GetCellWorldCenterPosition(0, 0));
        Vector2 l10 = overlayRect.InverseTransformPoint(board.GetCellWorldCenterPosition(1, 0));
        float cellLocal = Mathf.Abs(l10.x - l00.x);
        if (cellLocal < 1f) cellLocal = board.TileSize;

        // Tüm pool panellerini gizle.
        for (int i = 0; i < holePanels.Count; i++)
            if (holePanels[i] != null) holePanels[i].gameObject.SetActive(false);

        int used = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!board.IsMaskHoleCell(x, y)) continue;

                Image panel = used < holePanels.Count ? holePanels[used] : CreateHolePanel();
                if (used >= holePanels.Count) holePanels.Add(panel);
                used++;

                var rt = panel.rectTransform;
                Vector2 local = overlayRect.InverseTransformPoint(board.GetCellWorldCenterPosition(x, y));
                rt.anchoredPosition = local;
                rt.sizeDelta = new Vector2(cellLocal * 1.03f, cellLocal * 1.03f);   // hafif bindirme (boşluk kalmasın)
                panel.gameObject.SetActive(true);
            }
        }
    }

    private Image CreateHolePanel()
    {
        var go = new GameObject("OverlayHole", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = rootCanvas != null ? rootCanvas.gameObject.layer : gameObject.layer;
        go.transform.SetParent(overlayRoot.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, selectedOverlayAlpha);
        img.raycastTarget = true;
        return img;
    }

    private static void SetAnchors(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── Visibility ────────────────────────────────────────────────────────────
    private void SetOverlayVisible(bool visible)
    {
        // Awake'te rootCanvas bulunamadıysa overlay hiç oluşmamış olabilir → tembel oluştur.
        if (overlayRoot == null && visible)
        {
            if (rootCanvas == null)
                rootCanvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            CreateOverlayUi();
        }

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(visible);
            if (visible)
                overlayRoot.transform.SetAsLastSibling();
        }

        SetJokerGridAboveOverlay(visible);
    }

    private void SetJokerGridAboveOverlay(bool above)
    {
        if (overlayRoot == null) return;
        if (!above) return;

        overlayRoot.transform.SetAsLastSibling();
        var jokerRoot = FindRootCanvasChild(transform);
        if (jokerRoot != null)
            jokerRoot.SetAsLastSibling();
    }

    private Transform FindRootCanvasChild(Transform t)
    {
        if (rootCanvas == null) return null;
        var canvasTr = rootCanvas.transform;
        var current = t;
        while (current != null)
        {
            if (current.parent == canvasTr) return current;
            current = current.parent;
        }
        return null;
    }

    // ── Events ────────────────────────────────────────────────────────────────
    private void HandleBoosterTargetingChanged(bool isTargeting)
    {
        if (!isTargeting) CancelVisualSelection();
        else              RefreshOverlayPanels();
    }

    internal void HandleJokerTap(int tappedIndex)
    {
        if (lastHandledTapFrame == Time.frameCount && lastHandledTapIndex == tappedIndex) return;
        lastHandledTapFrame = Time.frameCount;
        lastHandledTapIndex = tappedIndex;

        if (tappedIndex < 0 || tappedIndex >= cachedJokerCount) return;
        if (!jokerIsBoosterSlot[tappedIndex]) return;

        int boosterIndex = jokerBoosterIndices[tappedIndex];
        if (boosterIndex < 0) return;

        if (selectedJokerIndex == tappedIndex)
        {
            if (board == null || board.IsBoosterModeActive)
            {
                CancelActiveJoker();
                return;
            }
            // Board already consumed the booster but UI didn't reset — desync, fall through to re-activate
            selectedJokerIndex = -1;
        }

        // Kilitli ya da hakkı kalmamış booster seçilemez (free oyunda serbest).
        // İptal (yukarıdaki blok) her durumda çalışır.
        if (!BoosterAccessService.CanUse(boosterIndex))
        {
            DebugLog($"[JokerFocus] Booster {boosterIndex} kullanılamaz (kilitli veya hak yok).");
            return;
        }

        ActivateFocusFor(boosterIndex);
        ActivateBooster(boosterIndex);
        SetSelectedJoker(tappedIndex);
    }

    private void ActivateBooster(int index)
    {
        if (board == null) board = FindFirstObjectByType<BoardController>();
        if (board == null) { Debug.LogWarning("[JokerFocus] BoardController not found."); return; }
        board.ActivateBooster(index < 0 || index > 3 ? -1 : index);
    }

    private void ActivateFocusFor(int index)
    {
        if (descriptionText != null)
        {
            descriptionText.text = index switch
            {
                0 => singleTargetText,
                1 => rowTargetText,
                2 => columnTargetText,
                3 => shuffleText,
                _ => string.Empty
            };
        }

        if (titleText != null)
        {
            titleText.text = index switch
            {
                0 => singleTitle,
                1 => rowTitle,
                2 => columnTitle,
                3 => shuffleTitle,
                _ => string.Empty
            };
        }

        var lib = TileIconLibrary.Shared;
        Sprite iconSprite = lib != null ? lib.GetBoosterIcon(BoosterAccessService.ToMode(index)) : null;
        if (titleIcon != null)
        {
            titleIcon.sprite  = iconSprite;
            titleIcon.enabled = iconSprite != null;
        }

        SetOverlayVisible(true);
        RefreshOverlayPanels();
    }

    private void CancelActiveJoker()   { CancelVisualSelection(); ActivateBooster(-1); }

    private void CancelVisualSelection()
    {
        SetOverlayVisible(false);
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (titleText != null) titleText.text = string.Empty;
        if (titleIcon != null) titleIcon.enabled = false;
        SetSelectedJoker(-1);
    }

    // ── Joker visual state ────────────────────────────────────────────────────
    private void SetSelectedJoker(int index)
    {
        selectedJokerIndex = index;

        for (int i = 0; i < cachedJokerCount; i++)
        {
            bool isSelected = i == selectedJokerIndex;

            var frame = jokerSelectionFrames[i];
            if (frame != null)
                frame.enabled = isSelected;

            var glow = jokerSelectionGlows[i];
            if (glow != null)
            {
                glow.enabled = isSelected && glow.sprite != null;
                glow.color   = new Color(1f, 1f, 1f, Mathf.Clamp01(selectedGlowAlpha));
            }

            // Outline yok — sarı kaplama önlendi

            var icon = jokerIcons[i];
            if (icon != null)
                icon.color = (selectedJokerIndex < 0 || isSelected)
                    ? Color.white
                    : new Color(1f, 1f, 1f, disabledJokerAlpha);

            var child = jokerSlotTransforms[i];
            if (child != null)
            {
                Vector3 bs = jokerBaseScales[i] == Vector3.zero ? Vector3.one : jokerBaseScales[i];
                child.localScale = bs;
            }
            if (icon != null)
            {
                Vector3 bis = jokerIconBaseScales[i] == Vector3.zero ? Vector3.one : jokerIconBaseScales[i];
                icon.rectTransform.localScale = (selectedJokerIndex >= 0 && isSelected)
                    ? bis * Mathf.Max(1f, selectedJokerScale)
                    : bis;
            }
        }
    }

    // ── Joker cache ───────────────────────────────────────────────────────────
    private void CacheJokerIcons()
    {
        for (int i = 0; i < MaxJokerSlots; i++)
        {
            jokerIcons[i]             = null;
            jokerButtons[i]           = null;
            jokerSelectionFrames[i]   = null;
            jokerSelectionGlows[i]    = null;
            jokerSlotTransforms[i]    = null;
            jokerBoosterIndices[i]    = -1;
            jokerIsBoosterSlot[i]     = false;
        }

        var entries = new List<(Transform slot, JokerBoosterSlotMapping mapping)>(MaxJokerSlots);

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null) continue;
            if (child.name.Contains("SelectionFrame") || child.name.Contains("SelectionGlow")) continue;
            if (child.GetComponent<Image>() == null && child.GetComponentInChildren<Image>(true) == null) continue;

            var mapping = child.GetComponent<JokerBoosterSlotMapping>();
            if (mapping == null || !mapping.IsBoosterSlot)
            {
                int inf;
                if (!TryInferBoosterIndex(child, out inf) && !TryInferBoosterIndexBySiblingOrder(child, out inf))
                    continue;
                mapping = child.gameObject.AddComponent<JokerBoosterSlotMapping>();
                ForceSetJokerMapping(mapping, true, inf);
                DebugLog($"[JokerFocus] Auto-mapped '{child.name}' → boosterIndex={inf}");
            }
            else if (mapping.BoosterIndex < 0)
            {
                // Elle eklenmiş ama index'i ayarlanmamış (-1) mapping: index'i yine
                // isim/sıradan çıkar — yoksa slot tap'leri sessizce yutulur.
                int inf;
                if (!TryInferBoosterIndex(child, out inf) && !TryInferBoosterIndexBySiblingOrder(child, out inf))
                    continue;
                ForceSetJokerMapping(mapping, true, inf);
                DebugLog($"[JokerFocus] Mapping index -1 düzeltildi: '{child.name}' → boosterIndex={inf}");
            }

            entries.Add((child, mapping));
            if (entries.Count >= MaxJokerSlots) break;
        }

        entries.Sort((a, b) => a.mapping.BoosterIndex.CompareTo(b.mapping.BoosterIndex));

        if (entries.Count == 0)
            Debug.LogWarning("[JokerFocus] No booster slots found in JokerGrid.");

        cachedJokerCount = entries.Count;

        for (int i = 0; i < cachedJokerCount; i++)
        {
            var child   = entries[i].slot;
            var mapping = entries[i].mapping;
            var icon    = child.GetComponent<Image>() ?? child.GetComponentInChildren<Image>(true);

            jokerSlotTransforms[i]  = child;
            jokerBoosterIndices[i]  = mapping.BoosterIndex;
            jokerIsBoosterSlot[i]   = true;
            jokerIcons[i]           = icon;
            jokerBaseScales[i]      = child.localScale;

            if (icon != null)
            {
                jokerIconBaseScales[i]    = icon.rectTransform.localScale;
                jokerSelectionFrames[i]   = EnsureSelectionFrame(icon);
                jokerSelectionGlows[i]    = EnsureSelectionGlow(icon);
            }

            var button = child.GetComponent<Button>() ?? child.gameObject.AddComponent<Button>();
            jokerButtons[i] = button;

            var raycastGraphic = EnsureRaycastSurface(child, icon);
            button.transition    = Selectable.Transition.None;
            button.targetGraphic = raycastGraphic;
            button.interactable  = true;

            int cap = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleJokerTap(cap));

            EnsurePointerProxy(child.gameObject, cap);
            if (raycastGraphic != null)
                EnsurePointerProxy(raycastGraphic.gameObject, cap);
        }
    }

    // ── Component helpers ─────────────────────────────────────────────────────
    private Image EnsureSelectionGlow(Image icon)
    {
        var existing = icon.transform.Find("SelectionGlow");
        Image glow;
        if (existing != null)
            glow = existing.GetComponent<Image>() ?? existing.gameObject.AddComponent<Image>();
        else
        {
            var go = new GameObject("SelectionGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(icon.transform, false);
            go.transform.SetAsFirstSibling();
            glow = go.GetComponent<Image>();
        }

        var rt = glow.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one * Mathf.Max(1f, selectedGlowScale);

        glow.sprite         = null;
        glow.type           = Image.Type.Simple;
        glow.preserveAspect = false;
        glow.raycastTarget  = false;
        glow.enabled        = false;
        glow.color          = new Color(0f, 0f, 0f, 0f);
        return glow;
    }

    private Image EnsureSelectionFrame(Image icon)
    {
        var parent = icon.transform;
        if (parent == null) return null;

        string frameName = $"{icon.gameObject.name}_SelectionFrame";
        Transform existing = parent.Find(frameName)
                          ?? parent.parent?.Find(frameName)
                          ?? parent.Find("SelectionFrame");

        Image frame;
        if (existing != null)
        {
            if (existing.parent != parent) existing.SetParent(parent, false);
            existing.name = frameName;
            frame = existing.GetComponent<Image>() ?? existing.gameObject.AddComponent<Image>();
        }
        else
        {
            var go = new GameObject(frameName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            frame = go.GetComponent<Image>();
        }

        frame.transform.SetAsFirstSibling();

        var fr = frame.rectTransform;
        fr.anchorMin        = Vector2.zero;
        fr.anchorMax        = Vector2.one;
        fr.pivot            = new Vector2(0.5f, 0.5f);
        fr.anchoredPosition = Vector2.zero;
        fr.offsetMin        = new Vector2(-selectedFramePadding, -selectedFramePadding);
        fr.offsetMax        = new Vector2( selectedFramePadding,  selectedFramePadding);

        // Sprite ve Outline YOK — iPhone fill + sarı kaplama önlendi
        // Seçim sadece scale ile belli olur
        frame.sprite        = null;
        frame.type          = Image.Type.Simple;
        frame.fillCenter    = false;
        frame.color         = new Color(0f, 0f, 0f, 0f);
        frame.preserveAspect = false;
        frame.raycastTarget  = false;
        frame.enabled        = false;

        // Varsa eski Outline'ı kaldır
        var oldOutline = frame.GetComponent<Outline>();
        if (oldOutline != null) DestroyImmediate(oldOutline);

        return frame;
    }

    private void EnsurePointerProxy(GameObject target, int index)
    {
        if (target == null) return;
        var p = target.GetComponent<JokerPointerProxy>() ?? target.AddComponent<JokerPointerProxy>();
        p.Init(this, index);
    }

    private Graphic EnsureRaycastSurface(Transform slot, Image icon)
    {
        if (icon != null) { icon.raycastTarget = true; return icon; }

        var g = slot.GetComponent<Graphic>();
        if (g != null) { g.raycastTarget = true; return g; }

        var hitT = slot.Find("JokerHitArea");
        Image hit;
        if (hitT != null)
            hit = hitT.GetComponent<Image>() ?? hitT.gameObject.AddComponent<Image>();
        else
        {
            var go = new GameObject("JokerHitArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(slot, false);
            go.transform.SetAsFirstSibling();
            hit = go.GetComponent<Image>();
        }

        var rt = hit.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        hit.color = new Color(1f, 1f, 1f, 0.001f);
        hit.raycastTarget = true;
        return hit;
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────
    private void ForceSetJokerMapping(JokerBoosterSlotMapping m, bool isSlot, int bIndex)
    {
        var t = typeof(JokerBoosterSlotMapping);
        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        t.GetField("isBoosterSlot", F)?.SetValue(m, isSlot);
        t.GetField("boosterIndex",  F)?.SetValue(m, bIndex);
    }

    private bool TryInferBoosterIndex(Transform child, out int idx)
    {
        idx = -1;
        if (child == null) return false;
        string lower = child.name.ToLowerInvariant();
        if (lower.Contains("hammer") || lower.Contains("single"))     { idx = 0; return true; }
        if (lower.Contains("row")    || lower.Contains("horizontal")) { idx = 1; return true; }
        if (lower.Contains("column") || lower.Contains("vertical"))   { idx = 2; return true; }
        if (lower.Contains("shuffle")|| lower.Contains("mix"))        { idx = 3; return true; }
        return false;
    }

    private bool TryInferBoosterIndexBySiblingOrder(Transform child, out int idx)
    {
        idx = -1;
        if (child == null || child.parent != transform) return false;
        int s = child.GetSiblingIndex();
        if (s < 0 || s > 3) return false;
        idx = s;
        return true;
    }

    // ── Sprite helpers ────────────────────────────────────────────────────────
    private Sprite ResolveSelectionFrameSprite()
    {
        if (string.IsNullOrEmpty(selectedFrameSpriteName)) return null;
        var s = ResolveSpriteByName(selectedFrameSpriteName);
        if (s != null) return s;

        string n    = selectedFrameSpriteName.Trim();
        string bare = n.EndsWith("_0") ? n[..^2] : n;
        foreach (var c in new[] { n, $"{n}_0", bare, $"{bare}_0" })
        {
            if (string.IsNullOrEmpty(c)) continue;
            s = ResolveSpriteByName(c) ?? ResolveSpriteByNameCaseInsensitive(c);
            if (s != null) return s;
        }

        if (!useProceduralSelectionFrame && missingFrameSpriteWarnings.Add(n))
            Debug.LogWarning($"[JokerFocus] Frame sprite '{selectedFrameSpriteName}' not found. Using procedural frame.");
        return null;
    }

    private Sprite ResolveSpriteByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            if (s != null && s.name == name) return s;
        return ResolveSpriteFromLoadedAtlases(name, false)
#if UNITY_EDITOR
               ?? ResolveSpriteFromAssetDatabase(name, false)
#endif
               ;
    }

    private Sprite ResolveSpriteByNameCaseInsensitive(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            if (s != null && string.Equals(s.name, name, System.StringComparison.OrdinalIgnoreCase)) return s;
        return ResolveSpriteFromLoadedAtlases(name, true)
#if UNITY_EDITOR
               ?? ResolveSpriteFromAssetDatabase(name, true)
#endif
               ;
    }

    private Sprite ResolveSpriteFromLoadedAtlases(string name, bool ignoreCase)
    {
        foreach (var atlas in Resources.FindObjectsOfTypeAll<SpriteAtlas>())
        {
            if (atlas == null) continue;
            if (!ignoreCase) { var s = atlas.GetSprite(name); if (s != null) return s; continue; }
            int cnt = atlas.spriteCount;
            if (cnt <= 0) continue;
            var buf = new Sprite[cnt];
            atlas.GetSprites(buf);
            foreach (var s in buf)
                if (s != null && string.Equals(s.name, name, System.StringComparison.OrdinalIgnoreCase)) return s;
        }
        return null;
    }

#if UNITY_EDITOR
    private Sprite ResolveSpriteFromAssetDatabase(string name, bool ignoreCase)
    {
        foreach (var guid in AssetDatabase.FindAssets($"{name} t:Sprite"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Sprite sprite) continue;
                if (!ignoreCase && sprite.name == name) return sprite;
                if (ignoreCase && string.Equals(sprite.name, name, System.StringComparison.OrdinalIgnoreCase)) return sprite;
            }
        }
        return null;
    }
#endif

    private void DebugLog(string msg) { if (verboseDebugLogs) Debug.Log(msg); }
}
