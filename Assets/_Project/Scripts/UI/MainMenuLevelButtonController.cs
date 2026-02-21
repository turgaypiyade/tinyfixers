using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLevelButtonController : MonoBehaviour
{
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string gameSceneName = "01_Game";
    [SerializeField] private string prefsLevelKey = "current_level";

    private int currentLevel;

    private void Start()
    {
        currentLevel = PlayerPrefs.GetInt(prefsLevelKey, 1);
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (levelText != null)
            levelText.text = "Seviye " + currentLevel;
    }

    public void OnLevelButtonClicked()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}
