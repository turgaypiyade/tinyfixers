using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLevelButtonController : MonoBehaviour
{
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string gameSceneName = "01_Game";
    [SerializeField] private string prefsLevelKey = "current_level";
    [SerializeField] private PreLevelSpecialPopupController preLevelSpecialPopup;
    [SerializeField] private ChapterThemeLibrary themeLibrary;

    [Header("Can Sistemi")]
    [Tooltip("Can = 0 iken level butonuna basılınca yönlendirilecek can göstergesi.")]
    [SerializeField] private MainMenuLivesDisplay livesDisplay;

    [Header("İlk Açılış")]
    [Tooltip("Oyunu ilk kez açan kullanıcıya verilecek altın miktarı.")]
    [SerializeField] private int initialCoins = 500;

    private int currentLevel;

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
    }

    private void UpdateVisual()
    {
        if (levelText != null)
            levelText.text = GameLocalization.GetFormat("prelevel_popup_title_level", currentLevel);
    }

    public void OnLevelButtonClicked()
    {
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

        ShowLoadingScreen();
        SceneManager.LoadScene(gameSceneName);
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
