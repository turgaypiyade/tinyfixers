using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Splash/boot: ekranda "loading..." gösterir ve GERÇEKTEN bekler — Firebase auth + cloud restore
/// çözülene kadar (FirebaseCloudSaveService.RestoreResolved). Böylece MainMenu açıldığında altın/
/// yıldız/level bulutla senkronlanmış olur.
///
/// Marka sonrası loading sırasında MainMenu arkada hazırlanır. Restore maxWaitSeconds içinde
/// çözülmezse ağ bekleyişi biter ve YEREL veriyle devam edilir.
/// Restore geç gelirse arka planda tamamlanır ve ekranlar OnRestored ile kendini tazeler.
/// </summary>
public class BootLoader : MonoBehaviour
{
    [Header("Splash Sequence")]
    [SerializeField] Image splashImage;
    [SerializeField] Sprite brandSprite;
    [SerializeField] Sprite loadingSprite;
    [SerializeField, Min(0f)] float brandDisplaySeconds = 2f;

    [Header("Timing")]
    [Tooltip("Markadan sonra loading ekranı en az bu kadar görünür.")]
    [SerializeField, Min(0f)] float minDisplaySeconds = 3f;
    [Tooltip("Loading ekranı başladıktan sonra Firebase için en fazla beklenecek süre. Marka süresi dahil değil.")]
    [SerializeField, Min(0f)] float maxWaitSeconds = 5f;
    [SerializeField] string nextSceneName = "MainMenu";

    [Header("Loading Text")]
    [Tooltip("Boş bırakılırsa runtime'da Canvas altına 'loading...' yazısı oluşturulur.")]
    [SerializeField] TMP_Text loadingText;
    [SerializeField] Canvas canvas;
    [SerializeField] string loadingLabel = "Loading";

    private bool HasBrandSplash => splashImage != null && brandSprite != null && brandDisplaySeconds > 0f;

    void Awake()
    {
        ShowSplash(HasBrandSplash ? brandSprite : loadingSprite);
        if (loadingText != null) loadingText.gameObject.SetActive(!HasBrandSplash);
    }

    private void ShowSplash(Sprite sprite)
    {
        if (splashImage == null || sprite == null) return;
        splashImage.enabled = true;
        splashImage.sprite = sprite;
        LoadingScreenManager.ApplyFitAspect(splashImage, sprite);
    }

    void Start()
    {
        PlayerStats.EnsureInitialized();   // oyuna ilk giriş tarihini bir kez kaydet
        CurrencyLedger.EnsureInit();       // altın/yıldız defteri tabanını hazırla

        // İlk harf büyük olsun ("loading" → "Loading") — inspector'da eski küçük değer serialize
        // edilmiş olsa bile garanti.
        if (!string.IsNullOrEmpty(loadingLabel))
            loadingLabel = char.ToUpper(loadingLabel[0]) + loadingLabel.Substring(1);

        EnsureLoadingText();
        if (loadingText != null) loadingText.gameObject.SetActive(!HasBrandSplash);
        StartCoroutine(LoadNext());
    }

    IEnumerator LoadNext()
    {
        float brandDuration = HasBrandSplash ? brandDisplaySeconds : 0f;
        float timeout = Mathf.Max(0f, maxWaitSeconds);
        float loadingMinimum = Mathf.Clamp(minDisplaySeconds, 0f, timeout);

        float elapsed = 0f;
        float loadingStartedAt = 0f;
        bool loadingShown = !HasBrandSplash;
        float dotTimer = 0f;
        int dots = 0;
        AsyncOperation sceneLoad = null;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            if (!loadingShown && elapsed >= brandDuration)
            {
                ShowSplash(loadingSprite);
                loadingShown = true;
                loadingStartedAt = elapsed;
                if (loadingText != null) loadingText.gameObject.SetActive(true);
                SetLoadingDots(0);
            }

            // Assetleri loading sırasında yükle; MainMenu Awake/Start ancak restore kararı
            // verilip sync gate açıldıktan sonra çalışsın.
            if (loadingShown && sceneLoad == null)
            {
                sceneLoad = SceneManager.LoadSceneAsync(nextSceneName);
                if (sceneLoad != null) sceneLoad.allowSceneActivation = false;
            }

            // "loading" → "loading." → "loading.." → "loading..."
            if (loadingShown) dotTimer += Time.unscaledDeltaTime;
            if (dotTimer >= 0.35f)
            {
                dotTimer = 0f;
                dots = (dots + 1) % 4;
                SetLoadingDots(dots);
            }

            bool restoreDone = FirebaseCloudSaveService.RestoreResolved;
            float loadingElapsed = elapsed - loadingStartedAt;
            bool minDone = loadingShown && loadingElapsed >= loadingMinimum;
            bool timedOut = loadingShown && loadingElapsed >= timeout;

            if (restoreDone && minDone)
                break;

            if (timedOut && minDone)
            {
                if (!restoreDone)
                {
                    bool offline = Application.internetReachability == NetworkReachability.NotReachable;
                    Debug.Log($"[Boot] Cloud restore {(offline ? "offline" : "timeout")} — yerel veriyle devam.");
                }
                break;
            }

            yield return null;
        }

        // Sync kapısını aç: bundan sonra kazanılan altın/yıldız "offline delta" olarak sayılır.
        // Restore hiç çözülmediyse (offline) VE hiç senkron olmamışsa (brand-new): yerel taban
        // benimsensin ki ilk-açılış hibeleri güvenilir değere yazılsın.
        if (!FirebaseCloudSaveService.RestoreResolved && !CurrencyLedger.EverSynced)
            CurrencyLedger.AdoptLocalAsBase();
        CurrencyLedger.OpenSyncGate();

        // Ağ bekleyişi bitti. Yavaş cihazda kalan yerel sahne yüklemesi sırasında görsel
        // ve Loading noktaları yaşamaya devam etsin.
        while (sceneLoad != null && sceneLoad.progress < 0.9f)
        {
            dotTimer += Time.unscaledDeltaTime;
            if (dotTimer >= 0.35f)
            {
                dotTimer = 0f;
                dots = (dots + 1) % 4;
                SetLoadingDots(dots);
            }
            yield return null;
        }

        // Splash sahnesindeki post-FX Volume'ları sahne yüklemeden ÖNCE devre dışı bırak:
        // LoadScene(single) Volume'u yok ederken VolumeManager hâlâ ona erişip
        // "Volume has been destroyed but you are still trying to access it" hatası veriyordu.
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v != null) v.enabled = false;

        if (sceneLoad != null)
            sceneLoad.allowSceneActivation = true;
        else
            SceneManager.LoadScene(nextSceneName);
    }

    private void SetLoadingDots(int dots)
    {
        if (loadingText == null) return;
        loadingText.text = loadingLabel + new string('.', dots);
    }

    // loadingText atanmadıysa Canvas altına ekranın altında ortalı bir TMP yazısı üretir.
    private void EnsureLoadingText()
    {
        if (loadingText != null) return;

        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("LoadingText", typeof(RectTransform));
        // KRİTİK: yeni UI objesi layer 0'da doğar; Screen Space Camera canvas'ı onu culler → görünmez.
        go.layer = canvas.gameObject.layer;

        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 100f);
        rt.sizeDelta = new Vector2(900f, 150f);   // auto-size büyük değerde stabil kalsın

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = loadingLabel;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        // Loading hint ekranındaki "loading" yazısının stilini birebir kopyala (BakBakOne SDF,
        // auto-size 40..84, beyaz). Prefab Resources'ta; instantiate etmeden font/boyut okunur.
        var hintPrefab = Resources.Load<LoadingHintView>("UI/LoadingHintView");
        var src = hintPrefab != null ? hintPrefab.LoadingText : null;
        if (src != null)
        {
            tmp.font = src.font;
            tmp.fontStyle = src.fontStyle;
            tmp.color = src.color;
            tmp.enableAutoSizing = src.enableAutoSizing;
            tmp.fontSizeMin = src.fontSizeMin;
            tmp.fontSizeMax = src.fontSizeMax;
            tmp.fontSize = src.fontSize;
        }
        else
        {
            tmp.fontSize = 72f;   // fallback: yine de belirgin büyük
        }

        loadingText = tmp;
    }
}
