using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLevelButtonController : MonoBehaviour
{
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string gameSceneName = "01_Game";
    [SerializeField] private string prefsLevelKey = "current_level";
    [SerializeField] private PreLevelSpecialPopupController preLevelSpecialPopup;

    private int currentLevel;

    private void Start()
    {
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
        if (preLevelSpecialPopup == null)
            preLevelSpecialPopup = FindPreLevelPopupInScene();

        if (preLevelSpecialPopup != null)
        {
            preLevelSpecialPopup.Open();
            return;
        }

        SceneManager.LoadScene(gameSceneName);
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
