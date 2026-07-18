using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuLevelButtonController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string gameSceneName = "01_Game";
    [SerializeField] private string prefsLevelKey = "current_level";
    [SerializeField] private PreLevelSpecialPopupController preLevelSpecialPopup;
    [SerializeField] private ChapterThemeLibrary themeLibrary;
    [Tooltip("LevelData.usesCustomIntro kontrolü için katalog. Boşsa default load her zaman çalışır.")]
    [SerializeField] private LevelCatalog levelCatalog;

    [Header("Can Sistemi")]
    [Tooltip("Can = 0 iken level butonuna basılınca yönlendirilecek can göstergesi.")]
    [SerializeField] private MainMenuLivesDisplay livesDisplay;

    [Header("İlk Açılış")]
    [Tooltip("Oyunu ilk kez açan kullanıcıya verilecek altın miktarı.")]
    [SerializeField] private int initialCoins = 500;

    [Header("Debug Level Selector")]
    [SerializeField] private bool enableDebugLevelSelector = true;
    [SerializeField, Min(0.25f)] private float debugLongPressSeconds = 3f;

    private int currentLevel;
    private Coroutine debugLongPressRoutine;
    private bool suppressNextLevelClick;
    private GameObject debugPanelRoot;
    private TMP_InputField debugLevelInput;
    private TMP_Text debugStatusText;

    private void Start()
    {
        LivesTimerService.InitialCoins = initialCoins;
        LivesTimerService.EnsureExists();
        currentLevel = PlayerPrefs.GetInt(prefsLevelKey, 1);
        UpdateVisual();
    }

    private void OnEnable()
    {
        GameLocalization.OnLanguageChanged += UpdateVisual;
    }

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= UpdateVisual;
        StopDebugLongPressDetection();
    }

    private void UpdateVisual()
    {
        if (levelText != null)
            levelText.text = GameLocalization.GetFormat("prelevel_popup_title_level", currentLevel);
    }

    public void OnLevelButtonClicked()
    {
        if (suppressNextLevelClick)
        {
            suppressNextLevelClick = false;
            return;
        }

        if (!LivesManager.HasLives)
        {
            // Can yoksa reklam akışını lives display üzerinden başlat.
            if (livesDisplay == null)
                livesDisplay = FindFirstObjectByType<MainMenuLivesDisplay>();
            livesDisplay?.OnAreaClicked();
            return;
        }

        if (preLevelSpecialPopup == null)
            preLevelSpecialPopup = FindPreLevelPopupInScene();

        if (preLevelSpecialPopup != null)
        {
            // Pre-level popup kendi içinde scene yüklediğinde LoadingScreen'i kendisi gösterir.
            preLevelSpecialPopup.Open();
            return;
        }

        // Custom intro varsa onu kullan (sahne yüklemesi manager'a ait); aksi halde default.
        if (TryShowCustomIntro())
            return;

        ShowLoadingScreen();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!enableDebugLevelSelector)
            return;

        StopDebugLongPressDetection();
        debugLongPressRoutine = StartCoroutine(CoOpenDebugPanelAfterHold());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopDebugLongPressDetection();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopDebugLongPressDetection();
    }

    private IEnumerator CoOpenDebugPanelAfterHold()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, debugLongPressSeconds));

        debugLongPressRoutine = null;
        suppressNextLevelClick = true;
        ShowDebugLevelPanel();
    }

    private void StopDebugLongPressDetection()
    {
        if (debugLongPressRoutine == null)
            return;

        StopCoroutine(debugLongPressRoutine);
        debugLongPressRoutine = null;
    }

    private void ShowDebugLevelPanel()
    {
        currentLevel = Mathf.Max(1, PlayerPrefs.GetInt(prefsLevelKey, currentLevel > 0 ? currentLevel : 1));

        if (debugPanelRoot == null)
            CreateDebugLevelPanel();

        if (debugPanelRoot == null)
            return;

        debugPanelRoot.SetActive(true);
        debugPanelRoot.transform.SetAsLastSibling();

        if (debugLevelInput != null)
        {
            debugLevelInput.text = currentLevel.ToString();
            debugLevelInput.Select();
            debugLevelInput.ActivateInputField();
        }

        SetDebugStatus("Seviye numarası gir.", false);
    }

    private void HideDebugLevelPanel()
    {
        suppressNextLevelClick = false;

        if (debugPanelRoot != null)
            debugPanelRoot.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void CreateDebugLevelPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[MainMenuLevelButtonController] Canvas bulunamadı; debug level selector açılamadı.");
            return;
        }

        debugPanelRoot = CreateUiObject("DebugLevelSelectorOverlay", canvas.transform, out RectTransform overlayRt);
        debugPanelRoot.layer = canvas.gameObject.layer;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        var overlayImage = debugPanelRoot.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.55f);
        overlayImage.raycastTarget = true;

        GameObject panel = CreateUiObject("LevelSelectorPanel", debugPanelRoot.transform, out RectTransform panelRt);
        panel.layer = debugPanelRoot.layer;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(460f, 330f);

        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.10f, 0.13f, 0.97f);

        var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(24, 24, 22, 22);
        panelLayout.spacing = 12f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        CreateDebugText(panel.transform, "Title", "Level Seç", 32f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.96f, 0.82f, 1f), 44f);
        CreateDebugText(panel.transform, "Hint", "Test etmek istediğin seviye numarasını gir.", 18f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.83f, 0.89f, 0.96f, 1f), 30f);

        debugLevelInput = CreateDebugInput(panel.transform);
        debugLevelInput.onSubmit.AddListener(_ => PlayDebugLevel());

        debugStatusText = CreateDebugText(panel.transform, "Status", "", 17f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.65f, 0.90f, 0.72f, 1f), 28f);

        GameObject row = CreateUiObject("ButtonRow", panel.transform, out _);
        row.layer = panel.layer;
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        var rowElement = row.AddComponent<LayoutElement>();
        rowElement.preferredHeight = 54f;

        CreateDebugButton(row.transform, "Kapat", new Color(0.22f, 0.25f, 0.30f, 1f), HideDebugLevelPanel);
        CreateDebugButton(row.transform, "Ayarla", new Color(0.18f, 0.38f, 0.58f, 1f), ApplyDebugLevelAndClose);
        CreateDebugButton(row.transform, "Oyna", new Color(0.30f, 0.62f, 0.32f, 1f), PlayDebugLevel);

        debugPanelRoot.SetActive(false);
    }

    private TMP_InputField CreateDebugInput(Transform parent)
    {
        GameObject inputObject = CreateUiObject("LevelInput", parent, out _);
        inputObject.layer = debugPanelRoot != null ? debugPanelRoot.layer : gameObject.layer;

        var inputImage = inputObject.AddComponent<Image>();
        inputImage.color = new Color(0.96f, 0.98f, 1f, 1f);

        var inputElement = inputObject.AddComponent<LayoutElement>();
        inputElement.preferredHeight = 62f;

        TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
        input.targetGraphic = inputImage;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.keyboardType = TouchScreenKeyboardType.NumberPad;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 6;
        input.caretColor = new Color(0.08f, 0.10f, 0.13f, 1f);
        input.selectionColor = new Color(0.25f, 0.50f, 0.95f, 0.35f);

        GameObject textArea = CreateUiObject("Text Area", inputObject.transform, out RectTransform textAreaRt);
        textArea.layer = inputObject.layer;
        textAreaRt.anchorMin = Vector2.zero;
        textAreaRt.anchorMax = Vector2.one;
        textAreaRt.offsetMin = new Vector2(18f, 6f);
        textAreaRt.offsetMax = new Vector2(-18f, -6f);

        TMP_Text placeholder = CreateDebugText(textArea.transform, "Placeholder", "örn. 37", 28f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Color(0.45f, 0.50f, 0.58f, 0.75f), -1f);
        TMP_Text text = CreateDebugText(textArea.transform, "Text", "", 28f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.07f, 0.09f, 0.12f, 1f), -1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        input.textViewport = textAreaRt;
        input.placeholder = placeholder;
        input.textComponent = text;

        return input;
    }

    private TMP_Text CreateDebugText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color,
        float preferredHeight)
    {
        GameObject textObject = CreateUiObject(name, parent, out RectTransform rect);
        textObject.layer = parent.gameObject.layer;

        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;

        if (preferredHeight > 0f)
        {
            var layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
        }
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        return label;
    }

    private Button CreateDebugButton(Transform parent, string label, Color color, System.Action onClick)
    {
        GameObject buttonObject = CreateUiObject(label + "Button", parent, out _);
        buttonObject.layer = parent.gameObject.layer;

        var image = buttonObject.AddComponent<Image>();
        image.color = color;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick?.Invoke());

        var layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 52f;
        layout.flexibleWidth = 1f;

        CreateDebugText(buttonObject.transform, "Label", label, 19f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, -1f);
        return button;
    }

    private static GameObject CreateUiObject(string name, Transform parent, out RectTransform rect)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return go;
    }

    private void ApplyDebugLevelAndClose()
    {
        if (!TryApplyDebugLevelFromInput())
            return;

        HideDebugLevelPanel();
    }

    private void PlayDebugLevel()
    {
        if (!TryApplyDebugLevelFromInput())
            return;

        HideDebugLevelPanel();
        suppressNextLevelClick = false;
        OnLevelButtonClicked();
    }

    private bool TryApplyDebugLevelFromInput()
    {
        string raw = debugLevelInput != null ? debugLevelInput.text.Trim() : "";
        if (raw.Length == 0 || !int.TryParse(raw, out int selectedLevel))
        {
            SetDebugStatus("Geçerli bir seviye numarası gir.", true);
            return false;
        }

        selectedLevel = Mathf.Max(1, selectedLevel);
        if (levelCatalog != null && !levelCatalog.TryGetGlobalLevel(selectedLevel, out _))
        {
            SetDebugStatus($"Katalogda level {selectedLevel} yok.", true);
            return false;
        }

        currentLevel = selectedLevel;
        PlayerPrefs.SetInt(prefsLevelKey, currentLevel);
        PlayerPrefs.Save();
        UpdateVisual();
        RefreshMenuThemeAfterDebugLevelChange();
        SetDebugStatus($"Level {currentLevel} seçildi.", false);
        return true;
    }

    private void SetDebugStatus(string message, bool isError)
    {
        if (debugStatusText == null)
            return;

        debugStatusText.text = message;
        debugStatusText.color = isError
            ? new Color(1f, 0.45f, 0.42f, 1f)
            : new Color(0.65f, 0.90f, 0.72f, 1f);
    }

    private static void RefreshMenuThemeAfterDebugLevelChange()
    {
        var applier = FindFirstObjectByType<ChapterThemeApplier>(FindObjectsInactive.Include);
        applier?.Apply();
    }

    private bool TryShowCustomIntro()
    {
        if (levelCatalog == null || !levelCatalog.TryGetGlobalLevel(currentLevel, out LevelData data) || data == null)
            return false;
        if (!data.usesCustomIntro || data.introLeftSprite == null || data.introRightSprite == null)
            return false;

        CustomIntroLoadingManager.Show(
            data.introLeftSprite, data.introRightSprite, gameSceneName,
            data.introSlideInDuration, data.introHoldDuration);
        return true;
    }

    private void ShowLoadingScreen()
    {
        if (themeLibrary == null)
        {
            LoadingScreenManager.Show((Sprite)null);
            return;
        }

        LoadingHintEntry hint = themeLibrary.GetRandomLoadingHint();
        if (hint != null)
        {
            LoadingScreenManager.Show(hint);
            return;
        }

        LoadingScreenManager.Show(themeLibrary.GetRandomLoadingImage());
    }

    private static PreLevelSpecialPopupController FindPreLevelPopupInScene()
    {
        var popups = Resources.FindObjectsOfTypeAll<PreLevelSpecialPopupController>();
        for (int i = 0; i < popups.Length; i++)
        {
            var popup = popups[i];
            if (popup == null || popup.gameObject.scene.name == null)
                continue;

            if (popup.gameObject.scene.isLoaded)
                return popup;
        }

        return null;
    }
}
