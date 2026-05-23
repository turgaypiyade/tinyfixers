using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameQuickSettings : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button vibrationButton;
    [SerializeField] private Button musicButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button exitButton;

    [Header("Toggle Icons")]
    [SerializeField] private Image vibrationIcon;
    [SerializeField] private Sprite vibrationOn;
    [SerializeField] private Sprite vibrationOff;

    [SerializeField] private Image musicIcon;
    [SerializeField] private Sprite musicOn;
    [SerializeField] private Sprite musicOff;

    [SerializeField] private Image soundIcon;
    [SerializeField] private Sprite soundOn;
    [SerializeField] private Sprite soundOff;

    [Header("Animation")]
    [SerializeField] private float itemSpacing = 90f;
    [SerializeField] private float staggerDelay = 0.06f;
    [SerializeField] private float animDuration = 0.18f;

    [Header("Scene")]
    [SerializeField] private string mainMenuScene = "00_MainMenu";

    private RectTransform[] items;
    private CanvasGroup[] itemGroups;
    private bool isOpen;
    private Coroutine menuAnim;

    private void Awake()
    {
        items = new RectTransform[]
        {
            vibrationButton.GetComponent<RectTransform>(),
            musicButton.GetComponent<RectTransform>(),
            soundButton.GetComponent<RectTransform>(),
            exitButton.GetComponent<RectTransform>()
        };

        itemGroups = new CanvasGroup[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            itemGroups[i] = items[i].GetComponent<CanvasGroup>();
            if (itemGroups[i] == null)
                itemGroups[i] = items[i].gameObject.AddComponent<CanvasGroup>();
        }

        vibrationButton.onClick.AddListener(OnVibrationClicked);
        musicButton.onClick.AddListener(OnMusicClicked);
        soundButton.onClick.AddListener(OnSoundClicked);
        exitButton.onClick.AddListener(OnExitClicked);

        SetInstant(false);
        RefreshIcons();
    }

    private void OnEnable()
    {
        GameSettings.OnMusicChanged += OnMusicChanged;
        GameSettings.OnSoundChanged += OnSoundChanged;
        GameSettings.OnVibrationChanged += OnVibrationChanged;
    }

    private void OnDisable()
    {
        GameSettings.OnMusicChanged -= OnMusicChanged;
        GameSettings.OnSoundChanged -= OnSoundChanged;
        GameSettings.OnVibrationChanged -= OnVibrationChanged;
    }

    private void OnMusicChanged(bool _) => RefreshIcons();
    private void OnSoundChanged(bool _) => RefreshIcons();
    private void OnVibrationChanged(bool _) => RefreshIcons();

    public void ToggleMenu()
    {
        isOpen = !isOpen;
        if (menuAnim != null) StopCoroutine(menuAnim);
        menuAnim = StartCoroutine(AnimateMenu(isOpen));
    }

    private void OnVibrationClicked()
    {
        GameSettings.VibrationEnabled = !GameSettings.VibrationEnabled;
        RefreshIcons();
    }

    private void OnMusicClicked()
    {
        GameSettings.MusicEnabled = !GameSettings.MusicEnabled;
        RefreshIcons();
    }

    private void OnSoundClicked()
    {
        GameSettings.SoundEnabled = !GameSettings.SoundEnabled;
        RefreshIcons();
    }

    private void OnExitClicked()
    {
        SceneManager.LoadScene(mainMenuScene);
    }

    private void RefreshIcons()
    {
        if (vibrationIcon != null)
            vibrationIcon.sprite = GameSettings.VibrationEnabled ? vibrationOn : vibrationOff;
        if (musicIcon != null)
            musicIcon.sprite = GameSettings.MusicEnabled ? musicOn : musicOff;
        if (soundIcon != null)
            soundIcon.sprite = GameSettings.SoundEnabled ? soundOn : soundOff;
    }

    private void SetInstant(bool open)
    {
        for (int i = 0; i < items.Length; i++)
        {
            float targetY = open ? itemSpacing * (i + 1) : 0f;
            var p = items[i].anchoredPosition;
            p.y = targetY;
            items[i].anchoredPosition = p;
            itemGroups[i].alpha = open ? 1f : 0f;
            itemGroups[i].blocksRaycasts = open;
            itemGroups[i].interactable = open;
        }
    }

    private IEnumerator AnimateMenu(bool open)
    {
        for (int i = 0; i < items.Length; i++)
        {
            int idx = i;
            StartCoroutine(AnimateItem(idx, open));
            yield return new WaitForSecondsRealtime(staggerDelay);
        }
    }

    private IEnumerator AnimateItem(int idx, bool open)
    {
        RectTransform rt = items[idx];
        CanvasGroup cg = itemGroups[idx];

        float fromY = rt.anchoredPosition.y;
        float toY = open ? itemSpacing * (idx + 1) : 0f;
        float fromAlpha = cg.alpha;
        float toAlpha = open ? 1f : 0f;

        cg.blocksRaycasts = open;
        cg.interactable = open;

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / animDuration));
            var p = rt.anchoredPosition;
            p.y = Mathf.Lerp(fromY, toY, k);
            rt.anchoredPosition = p;
            cg.alpha = Mathf.Lerp(fromAlpha, toAlpha, k);
            yield return null;
        }

        var fp = rt.anchoredPosition;
        fp.y = toY;
        rt.anchoredPosition = fp;
        cg.alpha = toAlpha;
    }
}
