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

public class JokerFocusOverlayController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool verboseDebugLogs = true;

    [Header("Texts")]
    [SerializeField] private string singleTargetText = "Tek taşı yok etmek istediğin objeyi seç!";
    [SerializeField] private string rowTargetText = "Satırı yok etmek istediğin objeyi seç!";
    [SerializeField] private string columnTargetText = "Sütunu yok etmek istediğin objeyi seç!";
    [SerializeField] private string shuffleText = "Karıştırmak için board üzerinde bir taşı seç!";

    [Header("Selection Highlight")]
    [SerializeField] private bool useProceduralSelectionFrame = false;
    [SerializeField] private string selectedFrameSpriteName = "Top_border_img_v2";
    [SerializeField] private float selectedFramePadding = 10f;
    [SerializeField] private Color selectedFrameOutlineColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private float selectedFrameOutlineThickness = 2f;
    [SerializeField] private float disabledJokerAlpha = 0.35f;
    [SerializeField] private float selectedJokerScale = 1.1f;
    [SerializeField] private string selectedGlowSpriteName = "";
    [SerializeField] private float selectedGlowAlpha = 0.9f;
    [SerializeField] private float selectedGlowScale = 1.3f;
    [SerializeField] private Color selectionOutlineColor = new Color(0.45f, 0.9f, 1f, 1f);
    [SerializeField] private float selectionOutlineDistance = 5f;
    [SerializeField] private float selectedOutlineDistance = 8f;
    [SerializeField] private float selectedOverlayAlpha = 0.55f;

    private const int MaxJokerSlots = 8;

    private readonly Image[] jokerIcons = new Image[MaxJokerSlots];
    private readonly Button[] jokerButtons = new Button[MaxJokerSlots];
    private readonly Image[] jokerSelectionFrames = new Image[MaxJokerSlots];
    private readonly Image[] jokerSelectionGlows = new Image[MaxJokerSlots];
    private readonly Outline[] jokerSelectionOutlines = new Outline[MaxJokerSlots];
    private readonly Canvas[] jokerSelectionCanvases = new Canvas[MaxJokerSlots];
    private readonly Vector3[] jokerBaseScales = new Vector3[MaxJokerSlots];
    private readonly Vector3[] jokerIconBaseScales = new Vector3[MaxJokerSlots];
    private readonly Transform[] jokerSlotTransforms = new Transform[MaxJokerSlots];
    private readonly int[] jokerBoosterIndices = new int[MaxJokerSlots];
    private readonly bool[] jokerIsBoosterSlot = new bool[MaxJokerSlots];
    private int cachedJokerCount;

    private Canvas rootCanvas;
    private BoardController board;
    private Image dimOverlay;
    private TextMeshProUGUI descriptionText;
    private int selectedJokerIndex = -1;
    private Sprite selectionFrameSprite;
    private Sprite selectionGlowSprite;
    private RectTransform boardRectTransform;
    private Transform boardOriginalParent;
    private int boardOriginalSiblingIndex = -1;
    private int lastHandledTapFrame = -1;
    private int lastHandledTapIndex = -1;

    private static bool sceneHookRegistered;
    private static readonly HashSet<string> missingFrameSpriteWarnings = new HashSet<string>();
    private static readonly HashSet<string> invalidFrameBorderWarnings = new HashSet<string>();
    private static readonly HashSet<string> fullRectFrameWarnings = new HashSet<string>();

    private sealed class JokerPointerProxy : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        private JokerFocusOverlayController owner;
        private int visualIndex = -1;

        public void Init(JokerFocusOverlayController ownerController, int index)
        {
            owner = ownerController;
            visualIndex = index;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            owner?.OnJokerPointerDown(visualIndex, gameObject.name);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            owner?.OnJokerPointerClick(visualIndex, gameObject.name);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterInstaller()
    {
        if (sceneHookRegistered)
            return;

        sceneHookRegistered = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        TryInstallController();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallController();
    }

    private static void TryInstallController()
    {
        var jokerGrid = FindJokerGrid();
        if (jokerGrid == null || jokerGrid.GetComponent<JokerFocusOverlayController>() != null)
            return;

        jokerGrid.AddComponent<JokerFocusOverlayController>();
    }

    private static GameObject FindJokerGrid()
    {
        var transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            var tr = transforms[i];
            if (tr == null || tr.name != "JokerGrid")
                continue;

            var go = tr.gameObject;
            if (!go.scene.IsValid() || !go.scene.isLoaded)
                continue;

            if ((go.hideFlags & HideFlags.HideInHierarchy) != 0)
                continue;

            return go;
        }

        return null;
    }

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            rootCanvas = FindFirstObjectByType<Canvas>();

        selectionFrameSprite = ResolveSelectionFrameSprite();
        if (selectionFrameSprite == null)
            useProceduralSelectionFrame = true;

        selectionGlowSprite = ResolveSpriteByName(selectedGlowSpriteName);

        CacheJokerIcons();
        CreateOverlayUi();
        SetOverlayVisible(false);
        SetSelectedJoker(-1);
    }

    private void Start()
    {
        // Bazı sahnelerde JokerGrid çocukları Awake sırasında tam oluşmayabiliyor.
        // Start'ta bir kez daha cacheleyip butonların kesinlikle bağlandığından emin ol.
        CacheJokerIcons();
        SetSelectedJoker(-1);
    }

    private void OnEnable()
    {
        if (board == null)
            board = FindFirstObjectByType<BoardController>();

        if (board != null)
            board.OnBoosterTargetingChanged += HandleBoosterTargetingChanged;
    }

    private void OnDisable()
    {
        if (board != null)
            board.OnBoosterTargetingChanged -= HandleBoosterTargetingChanged;

        SetOverlayVisible(false);
    }

    private void OnTransformChildrenChanged()
    {
        CacheJokerIcons();
        SetSelectedJoker(selectedJokerIndex >= 0 && selectedJokerIndex < cachedJokerCount ? selectedJokerIndex : -1);
    }

    private Sprite ResolveSelectionFrameSprite()
    {
        if (string.IsNullOrEmpty(selectedFrameSpriteName))
            return null;

        var sprite = ResolveSpriteByName(selectedFrameSpriteName);
        if (sprite != null)
            return sprite;

        string normalized = selectedFrameSpriteName.Trim();
        string noAtlasIndex = normalized.EndsWith("_0")
            ? normalized.Substring(0, normalized.Length - 2)
            : normalized;

        string[] candidates =
        {
            normalized,
            $"{normalized}_0",
            noAtlasIndex,
            $"{noAtlasIndex}_0"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrEmpty(candidate))
                continue;

            sprite = ResolveSpriteByName(candidate);
            if (sprite != null)
                return sprite;

            sprite = ResolveSpriteByNameCaseInsensitive(candidate);
            if (sprite != null)
                return sprite;
        }

        if (!useProceduralSelectionFrame && missingFrameSpriteWarnings.Add(normalized))
            Debug.LogWarning($"JokerFocusOverlayController could not find frame sprite '{selectedFrameSpriteName}'. Falling back to procedural frame.");

        return null;
    }

    private Sprite ResolveSpriteByName(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return null;

        var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            if (sprite == null)
                continue;

            if (sprite.name == spriteName)
                return sprite;
        }

        var atlasSprite = ResolveSpriteFromLoadedAtlases(spriteName, false);
        if (atlasSprite != null)
            return atlasSprite;

#if UNITY_EDITOR
        var assetSprite = ResolveSpriteFromAssetDatabase(spriteName, false);
        if (assetSprite != null)
            return assetSprite;
#endif

        return null;
    }



    private Sprite ResolveSpriteByNameCaseInsensitive(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return null;

        var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            if (sprite == null || string.IsNullOrEmpty(sprite.name))
                continue;

            if (string.Equals(sprite.name, spriteName, System.StringComparison.OrdinalIgnoreCase))
                return sprite;
        }

        var atlasSprite = ResolveSpriteFromLoadedAtlases(spriteName, true);
        if (atlasSprite != null)
            return atlasSprite;

#if UNITY_EDITOR
        var assetSprite = ResolveSpriteFromAssetDatabase(spriteName, true);
        if (assetSprite != null)
            return assetSprite;
#endif

        return null;
    }

    private Sprite ResolveSpriteFromLoadedAtlases(string spriteName, bool ignoreCase)
    {
        var atlases = Resources.FindObjectsOfTypeAll<SpriteAtlas>();
        for (int i = 0; i < atlases.Length; i++)
        {
            var atlas = atlases[i];
            if (atlas == null)
                continue;

            if (!ignoreCase)
            {
                var atlasSprite = atlas.GetSprite(spriteName);
                if (atlasSprite != null)
                    return atlasSprite;

                continue;
            }

            int spriteCount = atlas.spriteCount;
            if (spriteCount <= 0)
                continue;

            var sprites = new Sprite[spriteCount];
            atlas.GetSprites(sprites);
            for (int j = 0; j < sprites.Length; j++)
            {
                var atlasSprite = sprites[j];
                if (atlasSprite == null || string.IsNullOrEmpty(atlasSprite.name))
                    continue;

                if (string.Equals(atlasSprite.name, spriteName, System.StringComparison.OrdinalIgnoreCase))
                    return atlasSprite;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    private Sprite ResolveSpriteFromAssetDatabase(string spriteName, bool ignoreCase)
    {
        string[] guids = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int j = 0; j < sprites.Length; j++)
            {
                var sprite = sprites[j] as Sprite;
                if (sprite == null || string.IsNullOrEmpty(sprite.name))
                    continue;

                if (!ignoreCase && sprite.name == spriteName)
                    return sprite;

                if (ignoreCase && string.Equals(sprite.name, spriteName, System.StringComparison.OrdinalIgnoreCase))
                    return sprite;
            }
        }

        return null;
    }
#endif

    private void HandleBoosterTargetingChanged(bool isTargeting)
    {
        if (!isTargeting)
        {
            CancelVisualSelection();
            return;
        }

        RefreshFocusVisualOrder();
    }

    private void CacheJokerIcons()
    {
        for (int i = 0; i < MaxJokerSlots; i++)
        {
            jokerIcons[i] = null;
            jokerButtons[i] = null;
            jokerSelectionFrames[i] = null;
            jokerSelectionGlows[i] = null;
            jokerSelectionOutlines[i] = null;
            jokerSelectionCanvases[i] = null;
            jokerSlotTransforms[i] = null;
            jokerBoosterIndices[i] = -1;
            jokerIsBoosterSlot[i] = false;
        }

        var jokerEntries = new List<(Transform slot, JokerBoosterSlotMapping mapping)>(MaxJokerSlots);
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null)
                continue;

            if (child.name.Contains("SelectionFrame") || child.name.Contains("SelectionGlow"))
                continue;

            if (child.GetComponent<Image>() == null && child.GetComponentInChildren<Image>(true) == null)
                continue;

            var mapping = child.GetComponent<JokerBoosterSlotMapping>();
            if (mapping == null || !mapping.IsBoosterSlot)
            {
                int inferredBoosterIndex;
                if (!TryInferBoosterIndex(child, out inferredBoosterIndex) && !TryInferBoosterIndexBySiblingOrder(child, out inferredBoosterIndex))
                    continue;

                mapping = child.gameObject.AddComponent<JokerBoosterSlotMapping>();
                ForceSetJokerMapping(mapping, true, inferredBoosterIndex);

                DebugLog($"[JokerFocus] Missing JokerBoosterSlotMapping detected on '{child.name}'. Auto-created mapping with inferred boosterIndex={inferredBoosterIndex}.");
            }

            jokerEntries.Add((child, mapping));
            if (jokerEntries.Count >= MaxJokerSlots)
                break;
        }

        jokerEntries.Sort((a, b) => a.mapping.BoosterIndex.CompareTo(b.mapping.BoosterIndex));

        if (jokerEntries.Count == 0)
            Debug.LogWarning("[JokerFocus] No booster slots cached. Joker buttons will not respond. Verify JokerGrid children and JokerBoosterSlotMapping components.");

        cachedJokerCount = jokerEntries.Count;
        int count = cachedJokerCount;
        for (int i = 0; i < count; i++)
        {
            var child = jokerEntries[i].slot;
            var mapping = jokerEntries[i].mapping;
            var icon = child.GetComponent<Image>();
            if (icon == null)
                icon = child.GetComponentInChildren<Image>(true);

            jokerSlotTransforms[i] = child;
            jokerBoosterIndices[i] = mapping.BoosterIndex;
            jokerIsBoosterSlot[i] = true;
            jokerIcons[i] = icon;

            jokerBaseScales[i] = child.localScale;
            jokerSelectionCanvases[i] = EnsureSelectionCanvas(child.gameObject);

            if (icon != null)
            {
                jokerIconBaseScales[i] = icon.rectTransform.localScale;
                jokerSelectionFrames[i] = EnsureSelectionFrame(icon);
                jokerSelectionGlows[i] = EnsureSelectionGlow(icon);
                jokerSelectionOutlines[i] = EnsureSelectionOutline(icon);
            }

            var button = child.GetComponent<Button>();
            if (button == null)
                button = child.gameObject.AddComponent<Button>();

            jokerButtons[i] = button;

            var raycastGraphic = EnsureRaycastSurface(child, icon);

            button.transition = Selectable.Transition.None;
            button.targetGraphic = raycastGraphic;
            button.interactable = true;
            int capturedIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleJokerTap(capturedIndex));

            // Bazı cihaz/Canvas kombinasyonlarında Button.onClick tetiklenmeyebiliyor.
            // Raycast alanına ve slot objesine pointer proxy ekleyerek tıklamayı garanti altına al.
            EnsurePointerProxy(child.gameObject, capturedIndex);
            if (raycastGraphic != null)
                EnsurePointerProxy(raycastGraphic.gameObject, capturedIndex);

           // DebugLog($"[JokerFocus] Slot cached -> slotName='{child.name}', visualIndex={i}, boosterIndex={mapping.BoosterIndex}, hasIcon={(icon != null)}, hasButton={(button != null)}");
        }

       // DebugLog($"[JokerFocus] CacheJokerIcons complete. childCount={transform.childCount}, cachedJokerCount={cachedJokerCount}");
    }

    private void ForceSetJokerMapping(JokerBoosterSlotMapping mapping, bool isBoosterSlot, int boosterIndex)
    {
        var type = typeof(JokerBoosterSlotMapping);
        var isBoosterField = type.GetField("isBoosterSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var boosterIndexField = type.GetField("boosterIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        isBoosterField?.SetValue(mapping, isBoosterSlot);
        boosterIndexField?.SetValue(mapping, boosterIndex);
    }

    private bool TryInferBoosterIndex(Transform child, out int boosterIndex)
    {
        boosterIndex = -1;
        if (child == null)
            return false;

        string n = child.name;
        if (string.IsNullOrEmpty(n))
            return false;

        string lower = n.ToLowerInvariant();
        if (lower.Contains("hammer") || lower.Contains("single"))
        {
            boosterIndex = 0;
            return true;
        }

        if (lower.Contains("row") || lower.Contains("horizontal"))
        {
            boosterIndex = 1;
            return true;
        }

        if (lower.Contains("column") || lower.Contains("vertical"))
        {
            boosterIndex = 2;
            return true;
        }

        if (lower.Contains("shuffle") || lower.Contains("mix"))
        {
            boosterIndex = 3;
            return true;
        }

        return false;
    }

    private bool TryInferBoosterIndexBySiblingOrder(Transform child, out int boosterIndex)
    {
        boosterIndex = -1;
        if (child == null || child.parent != transform)
            return false;

        int sibling = child.GetSiblingIndex();
        if (sibling < 0 || sibling > 3)
            return false;

        boosterIndex = sibling;
        return true;
    }

    private void DebugLog(string message)
    {
        if (!verboseDebugLogs)
            return;

        Debug.Log(message);
    }





    private Image EnsureSelectionGlow(Image icon)
    {
        var existing = icon.transform.Find("SelectionGlow");
        Image glow;

        if (existing != null)
        {
            glow = existing.GetComponent<Image>();
            if (glow == null)
                glow = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            var glowGo = new GameObject("SelectionGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glowGo.transform.SetParent(icon.transform, false);
            glowGo.transform.SetAsFirstSibling();
            glow = glowGo.GetComponent<Image>();
        }

        var glowRect = glow.rectTransform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = Vector2.zero;
        glowRect.offsetMax = Vector2.zero;
        glowRect.localScale = Vector3.one * Mathf.Max(1f, selectedGlowScale);

        glow.sprite = selectionGlowSprite != null ? selectionGlowSprite : selectionFrameSprite;
        glow.type = Image.Type.Sliced;
        glow.preserveAspect = true;
        glow.raycastTarget = false;
        glow.enabled = false;
        glow.color = new Color(1f, 1f, 1f, Mathf.Clamp01(selectedGlowAlpha));

        return glow;
    }

    private Image EnsureSelectionFrame(Image icon)
    {
        var parent = icon.transform;
        if (parent == null)
            return null;

        string frameName = $"{icon.gameObject.name}_SelectionFrame";
        var existing = parent.Find(frameName);

        if (existing == null)
        {
            var legacyParent = icon.transform.parent;
            if (legacyParent != null)
            {
                var legacyFrame = legacyParent.Find(frameName);
                if (legacyFrame != null)
                {
                    existing = legacyFrame;
                    existing.SetParent(parent, false);
                }
            }
        }

        if (existing == null)
        {
            var oldChildFrame = icon.transform.Find("SelectionFrame");
            if (oldChildFrame != null)
            {
                existing = oldChildFrame;
                existing.SetParent(parent, false);
                existing.name = frameName;
            }
        }

        Image frame;

        if (existing != null)
        {
            frame = existing.GetComponent<Image>();
            if (frame == null)
                frame = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            var frameGo = new GameObject("SelectionFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGo.name = frameName;
            frameGo.transform.SetParent(parent, false);
            frame = frameGo.GetComponent<Image>();
        }

        frame.transform.SetAsFirstSibling();

        var frameRect = frame.rectTransform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.offsetMin = new Vector2(-selectedFramePadding, -selectedFramePadding);
        frameRect.offsetMax = new Vector2(selectedFramePadding, selectedFramePadding);

        bool hasValidBorder = selectionFrameSprite != null && HasSlicedBorder(selectionFrameSprite);
        bool shouldUseSpriteFrame = !useProceduralSelectionFrame && hasValidBorder;

        frame.sprite = shouldUseSpriteFrame ? selectionFrameSprite : null;
        frame.type = shouldUseSpriteFrame ? Image.Type.Sliced : Image.Type.Simple;
        frame.fillCenter = false;
        frame.preserveAspect = true;
        frame.raycastTarget = false;
        frame.enabled = false;
        frame.color = shouldUseSpriteFrame
            ? new Color(1f, 1f, 1f, 0.95f)
            : new Color(1f, 1f, 1f, 0f);

        if (!useProceduralSelectionFrame && selectionFrameSprite != null && !hasValidBorder)
        {
            string key = selectionFrameSprite.name;
            if (invalidFrameBorderWarnings.Add(key))
                Debug.LogWarning($"JokerFocusOverlayController frame sprite '{selectionFrameSprite.name}' has no border data. Falling back to procedural outline.");
        }

        var frameOutline = frame.GetComponent<Outline>();
        if (frameOutline == null)
            frameOutline = frame.gameObject.AddComponent<Outline>();

        if (!shouldUseSpriteFrame)
        {
            frameOutline.effectColor = selectedFrameOutlineColor;
            frameOutline.effectDistance = new Vector2(selectedFrameOutlineThickness, selectedFrameOutlineThickness);
            frameOutline.useGraphicAlpha = false;
            frameOutline.enabled = true;
        }
        else
        {
            frameOutline.enabled = false;
        }

        return frame;
    }

    private bool HasSlicedBorder(Sprite sprite)
    {
        if (sprite == null)
            return false;

        Vector4 border = sprite.border;
        return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
    }

    private Outline EnsureSelectionOutline(Image icon)
    {
        var outline = icon.GetComponent<Outline>();
        if (outline == null)
            outline = icon.gameObject.AddComponent<Outline>();

        outline.effectColor = selectionOutlineColor;
        outline.effectDistance = new Vector2(selectionOutlineDistance, selectionOutlineDistance);
        outline.useGraphicAlpha = true;
        outline.enabled = false;
        return outline;
    }

    private Canvas EnsureSelectionCanvas(GameObject jokerObject)
    {
        var canvas = jokerObject.GetComponent<Canvas>();

        if (canvas == null)
            canvas = jokerObject.AddComponent<Canvas>();

        // Slot'a Canvas eklediğimiz için bu seviyede de raycast alındığından emin ol.
        // Aksi halde bazı durumlarda üst Canvas raycaster'ı nested canvas grafiklerini tıklatmayabiliyor.
        var raycaster = jokerObject.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = jokerObject.AddComponent<GraphicRaycaster>();

        canvas.overrideSorting = false;
        return canvas;
    }

    private void EnsurePointerProxy(GameObject target, int index)
    {
        if (target == null)
            return;

        var proxy = target.GetComponent<JokerPointerProxy>();
        if (proxy == null)
            proxy = target.AddComponent<JokerPointerProxy>();

        proxy.Init(this, index);
    }

    private Graphic EnsureRaycastSurface(Transform slot, Image icon)
    {
        if (icon != null)
        {
            icon.raycastTarget = true;
            return icon;
        }

        var existingGraphic = slot.GetComponent<Graphic>();
        if (existingGraphic != null)
        {
            existingGraphic.raycastTarget = true;
            return existingGraphic;
        }

        var hitAreaTransform = slot.Find("JokerHitArea");
        Image hitArea;
        if (hitAreaTransform != null)
        {
            hitArea = hitAreaTransform.GetComponent<Image>();
            if (hitArea == null)
                hitArea = hitAreaTransform.gameObject.AddComponent<Image>();
        }
        else
        {
            var hitAreaGo = new GameObject("JokerHitArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hitAreaGo.transform.SetParent(slot, false);
            hitAreaGo.transform.SetAsFirstSibling();
            hitArea = hitAreaGo.GetComponent<Image>();
        }

        var rect = hitArea.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        hitArea.color = new Color(1f, 1f, 1f, 0.001f);
        hitArea.raycastTarget = true;
        return hitArea;
    }

    private void CreateOverlayUi()
    {
        if (rootCanvas == null)
            return;

        var overlayGo = new GameObject("JokerFocusDimOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGo.transform.SetParent(rootCanvas.transform, false);

        dimOverlay = overlayGo.GetComponent<Image>();
        dimOverlay.color = new Color(0f, 0f, 0f, 0.72f);
        dimOverlay.raycastTarget = false;

        var overlayRect = overlayGo.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var textGo = new GameObject("JokerFocusDescription", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(rootCanvas.transform, false);

        descriptionText = textGo.GetComponent<TextMeshProUGUI>();
        descriptionText.fontSize = 56;
        descriptionText.enableAutoSizing = true;
        descriptionText.fontSizeMin = 28;
        descriptionText.fontSizeMax = 56;
        descriptionText.alignment = TextAlignmentOptions.Center;
        descriptionText.color = Color.white;
        descriptionText.fontStyle = FontStyles.Bold;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.08f, 0.82f);
        textRect.anchorMax = new Vector2(0.92f, 0.96f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void RefreshFocusVisualOrder()
    {
        if (dimOverlay != null)
            dimOverlay.transform.SetAsLastSibling();

        BringBoardInFrontOfOverlay();

        if (descriptionText != null)
            descriptionText.transform.SetAsLastSibling();
    }

    private void BringBoardInFrontOfOverlay()
    {
        if (board == null)
            board = FindFirstObjectByType<BoardController>();

        if (board == null)
            return;

        if (boardRectTransform == null)
            boardRectTransform = board.GetComponent<RectTransform>();

        if (boardRectTransform == null)
            return;

        if (boardOriginalSiblingIndex < 0)
        {
            boardOriginalParent = boardRectTransform.parent;
            boardOriginalSiblingIndex = boardRectTransform.GetSiblingIndex();
        }

        boardRectTransform.SetAsLastSibling();
    }

    private void RestoreBoardOrder()
    {
        if (boardRectTransform == null || boardOriginalParent == null || boardOriginalSiblingIndex < 0)
            return;

        if (boardRectTransform.parent == boardOriginalParent)
        {
            int safeIndex = Mathf.Clamp(boardOriginalSiblingIndex, 0, boardOriginalParent.childCount - 1);
            boardRectTransform.SetSiblingIndex(safeIndex);
        }

        boardOriginalSiblingIndex = -1;
        boardOriginalParent = null;
    }

    private void OnJokerPointerDown(int tappedJokerIndex, string sourceName)
    {
       // DebugLog($"[JokerFocus] PointerDown from '{sourceName}'. visualIndex={tappedJokerIndex}");
    }

    private void OnJokerPointerClick(int tappedJokerIndex, string sourceName)
    {
       // DebugLog($"[JokerFocus] PointerClick from '{sourceName}'. visualIndex={tappedJokerIndex}");
        HandleJokerTap(tappedJokerIndex);
    }

    private void HandleJokerTap(int tappedJokerIndex)
    {
        if (lastHandledTapFrame == Time.frameCount && lastHandledTapIndex == tappedJokerIndex)
        {
            //DebugLog($"[JokerFocus] Duplicate tap ignored. visualIndex={tappedJokerIndex}, frame={Time.frameCount}");
            return;
        }

        lastHandledTapFrame = Time.frameCount;
        lastHandledTapIndex = tappedJokerIndex;

        //DebugLog("[JokerFocus] Jokere tıklandı. visualIndex=" + tappedJokerIndex + ", cachedJokerCount=" + cachedJokerCount);
        if (tappedJokerIndex < 0 || tappedJokerIndex >= cachedJokerCount)
        {
           // Debug.LogWarning("[JokerFocus] Tap ignored because index is out of cached range.");
            return;
        }

        if (!jokerIsBoosterSlot[tappedJokerIndex])
        {
           // Debug.LogWarning($"[JokerFocus] Tap ignored because slot {tappedJokerIndex} is not configured as a booster slot.");
            return;
        }

        int mappedBoosterIndex = jokerBoosterIndices[tappedJokerIndex];
        if (mappedBoosterIndex < 0)
        {
           // Debug.LogWarning($"[JokerFocus] Tap ignored because slot {tappedJokerIndex} mapped booster index is invalid ({mappedBoosterIndex}).");
            return;
        }

        if (selectedJokerIndex == tappedJokerIndex)
        {
            CancelActiveJoker();
            return;
        }

        ActivateFocusFor(mappedBoosterIndex);
        ActivateBooster(mappedBoosterIndex);
        SetSelectedJoker(tappedJokerIndex);
    }

    private void ActivateBooster(int index)
    {
        if (board == null)
            board = FindFirstObjectByType<BoardController>();

        if (board == null)
        {
            Debug.LogWarning("[JokerFocus] BoardController not found. Booster activation request dropped.");
            return;
        }

        if (index < 0 || index > 3)
        {
            board.ActivateBooster(-1);
            return;
        }

        board.ActivateBooster(index);
    }

    private void ActivateFocusFor(int index)
    {
        if (descriptionText == null || dimOverlay == null)
            return;

        SetOverlayVisible(true);

        switch (index)
        {
            case 0:
                descriptionText.text = singleTargetText;
                break;
            case 1:
                descriptionText.text = rowTargetText;
                break;
            case 2:
                descriptionText.text = columnTargetText;
                break;
            case 3:
                descriptionText.text = shuffleText;
                break;
            default:
                descriptionText.text = string.Empty;
                break;
        }

        RefreshFocusVisualOrder();
    }

    private void CancelActiveJoker()
    {
        CancelVisualSelection();
        ActivateBooster(-1);
    }

    private void CancelVisualSelection()
    {
        SetOverlayVisible(false);

        if (descriptionText != null)
            descriptionText.text = string.Empty;

        RestoreBoardOrder();
        SetSelectedJoker(-1);
    }

    private void SetSelectedJoker(int index)
    {
        selectedJokerIndex = index;

        for (int i = 0; i < cachedJokerCount; i++)
        {
            bool isSelected = i == selectedJokerIndex;

            var frame = jokerSelectionFrames[i];
            if (frame != null)
            {
                bool frameReady = frame.sprite != null || frame.GetComponent<Outline>() != null;
                frame.enabled = isSelected && frameReady;

                if (isSelected)
                {
                    frame.transform.SetAsFirstSibling();

                    if (frame.sprite != null && !HasSlicedBorder(frame.sprite))
                    {
                        string key = $"{frame.sprite.name}:{i}";
                        if (fullRectFrameWarnings.Add(key))
                            Debug.LogWarning($"JokerFocusOverlayController selected frame sprite '{frame.sprite.name}' may behave like full-rect (atlas/border issue). Icon+frame visibility is being protected via fallback rules.");
                    }
                }
            }

            var glow = jokerSelectionGlows[i];
            if (glow != null)
            {
                bool hasGlowSprite = glow.sprite != null;
                glow.enabled = isSelected && hasGlowSprite;
                glow.color = new Color(1f, 1f, 1f, Mathf.Clamp01(selectedGlowAlpha));
            }

            var outline = jokerSelectionOutlines[i];
            if (outline != null)
            {
                outline.effectDistance = isSelected
                    ? new Vector2(selectedOutlineDistance, selectedOutlineDistance)
                    : new Vector2(selectionOutlineDistance, selectionOutlineDistance);
                outline.enabled = isSelected;
            }

            var button = jokerButtons[i];
            if (button != null && jokerIsBoosterSlot[i])
                button.interactable = selectedJokerIndex < 0 || isSelected;

            var selectionCanvas = jokerSelectionCanvases[i];
            if (selectionCanvas != null)
            {
                bool liftAboveOverlay = selectedJokerIndex >= 0 && isSelected;
                selectionCanvas.overrideSorting = liftAboveOverlay;

                if (liftAboveOverlay && rootCanvas != null)
                {
                    selectionCanvas.sortingLayerID = rootCanvas.sortingLayerID;
                    selectionCanvas.sortingOrder = rootCanvas.sortingOrder + 10;
                }
                else
                {
                    selectionCanvas.sortingOrder = 0;
                }
            }

            var icon = jokerIcons[i];
            if (icon != null)
            {
                bool shouldStayEnabled = selectedJokerIndex < 0 || isSelected;
                var color = Color.white;

                if (selectedJokerIndex >= 0 && isSelected)
                {
                    color = new Color(1f, 1f, 1f, 1f);
                }
                else if (!shouldStayEnabled)
                {
                    color = new Color(1f, 1f, 1f, disabledJokerAlpha);
                }
                else if (selectedJokerIndex < 0)
                {
                    color = Color.white;
                }

                icon.color = color;
            }

            var child = jokerSlotTransforms[i];
            if (child != null)
            {
                Vector3 baseScale = jokerBaseScales[i] == Vector3.zero ? Vector3.one : jokerBaseScales[i];
                child.localScale = baseScale;
            }

            if (icon != null)
            {
                Vector3 baseIconScale = jokerIconBaseScales[i] == Vector3.zero ? Vector3.one : jokerIconBaseScales[i];
                icon.rectTransform.localScale = (selectedJokerIndex >= 0 && isSelected)
                    ? baseIconScale * Mathf.Max(1f, selectedJokerScale)
                    : baseIconScale;
            }
        }

        if (dimOverlay != null && dimOverlay.gameObject.activeSelf)
            dimOverlay.color = new Color(0f, 0f, 0f, selectedJokerIndex >= 0 ? selectedOverlayAlpha : 0.72f);
    }

    private void SetOverlayVisible(bool visible)
    {
        if (dimOverlay != null)
            dimOverlay.gameObject.SetActive(visible);

        if (descriptionText != null)
            descriptionText.gameObject.SetActive(visible);
    }
}
