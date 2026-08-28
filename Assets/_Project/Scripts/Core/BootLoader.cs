using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Splash/boot: ekranda "loading..." gösterir ve GERÇEKTEN bekler — Firebase auth + cloud restore
/// çözülene kadar (FirebaseCloudSaveService.RestoreResolved). Böylece MainMenu açıldığında altın/
/// yıldız/level bulutla senkronlanmış olur.
///
/// OFFLINE GARANTİSİ: internet yoksa ya da restore zamanında çözülmezse, maxWaitSeconds (veya
/// internetsizken minDisplaySeconds) dolunca YEREL veriyle devam edilir — oyun asla takılmaz.
/// Restore geç gelirse arka planda tamamlanır ve ekranlar OnRestored ile kendini tazeler.
/// </summary>
public class BootLoader : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Splash en az bu kadar görünür (ani geçiş/flash olmasın).")]
    [SerializeField] float minDisplaySeconds = 1.5f;
    [Tooltip("Online iken cloud restore için üst bekleme. Dolunca yerelle devam (offline-safe).")]
    [SerializeField] float maxWaitSeconds = 8f;
    [SerializeField] string nextSceneName = "MainMenu";

    [Header("Loading Text")]
    [Tooltip("Boş bırakılırsa runtime'da Canvas altına 'loading...' yazısı oluşturulur.")]
    [SerializeField] TMP_Text loadingText;
    [SerializeField] Canvas canvas;
    [SerializeField] string loadingLabel = "Loading";

    void Start()
    {
        PlayerStats.EnsureInitialized();   // oyuna ilk giriş tarihini bir kez kaydet
        CurrencyLedger.EnsureInit();       // altın/yıldız defteri tabanını hazırla

        // İlk harf büyük olsun ("loading" → "Loading") — inspector'da eski küçük değer serialize
        // edilmiş olsa bile garanti.
        if (!string.IsNullOrEmpty(loadingLabel))
            loadingLabel = char.ToUpper(loadingLabel[0]) + loadingLabel.Substring(1);

        EnsureLoadingText();
        StartCoroutine(LoadNext());
    }

    IEnumerator LoadNext()
    {
        // İnternet yoksa cloud restore hiç çözülmeyecek — boşuna bekleme, kısa tut.
        bool offline = Application.internetReachability == NetworkReachability.NotReachable;
        float timeout = offline ? minDisplaySeconds : maxWaitSeconds;

        float elapsed = 0f;
        float dotTimer = 0f;
        int dots = 0;
        bool loggedTimeout = false;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            // "loading" → "loading." → "loading.." → "loading..."
            dotTimer += Time.unscaledDeltaTime;
            if (dotTimer >= 0.35f)
            {
                dotTimer = 0f;
                dots = (dots + 1) % 4;
                SetLoadingDots(dots);
            }

            bool restoreDone = FirebaseCloudSaveService.RestoreResolved;
            bool minDone = elapsed >= minDisplaySeconds;
            bool timedOut = elapsed >= timeout;

            if (restoreDone && minDone)
                break;

            if (timedOut)
            {
                if (!restoreDone && !loggedTimeout)
                {
                    loggedTimeout = true;
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

        // Splash sahnesindeki post-FX Volume'ları sahne yüklemeden ÖNCE devre dışı bırak:
        // LoadScene(single) Volume'u yok ederken VolumeManager hâlâ ona erişip
        // "Volume has been destroyed but you are still trying to access it" hatası veriyordu.
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v != null) v.enabled = false;

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
