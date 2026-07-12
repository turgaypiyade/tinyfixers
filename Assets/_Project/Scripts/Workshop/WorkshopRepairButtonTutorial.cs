using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// İlk açılışta RepairButton'a dokunmayı öğreten tutorial.
/// Görsel ipucu: el ikonu butonun üstünde pulse yapar + açıklama metni.
/// Oyuncu butona tıklayınca tutorial kapanır, bir daha gösterilmez.
///
/// PlayerPrefs flag: "tutorial_seen_workshop_repair"
/// </summary>
public class WorkshopRepairButtonTutorial : MonoBehaviour
{
    private const string KeyTutorialSeen = "tutorial_seen_workshop_repair";

    [Header("References")]
    [SerializeField] private Button repairButton;
    [Tooltip("Tutorial UI root — dim background + hand + text içeren GameObject.")]
    [SerializeField] private GameObject tutorialOverlay;
    [Tooltip("Buton üstünde gösterilecek el ikonu (RectTransform).")]
    [SerializeField] private RectTransform handIcon;
    [Tooltip("Açıklama metni (opsiyonel).")]
    [SerializeField] private TMP_Text descriptionText;
    [Tooltip("Tutorial overlay'ın CanvasGroup'u (fade animasyon için).")]
    [SerializeField] private CanvasGroup overlayGroup;

    [Header("Localization")]
    [SerializeField] private string descriptionLocalizationKey = "workshop_tutorial_tap_repair";
    [SerializeField] private string descriptionFallback = "Atölyeyi tamir etmeye başla!";

    [Header("Timing")]
    [Tooltip("Sahne yüklendikten kaç saniye sonra tutorial gösterilsin (yükleme/animasyonlar bitsin diye).")]
    [SerializeField, Min(0f)] private float showDelay = 0.6f;
    [SerializeField, Min(0.1f)] private float fadeDuration = 0.3f;

    [Header("Hand Pulse")]
    [Tooltip("El ikonunun tek bir pulse periyodu süresi.")]
    [SerializeField, Min(0.1f)] private float pulseDuration = 0.7f;
    [SerializeField, Range(1f, 2f)] private float pulseScale = 1.25f;
    [Tooltip("El ikonunun buton üzerindeki offset'i (pozitif Y = yukarı). " +
             "El aşağı bakıyorsa 80-120 arası iyidir; el sağa bakıyorsa X'i ayarla.")]
    [SerializeField] private Vector2 handOffset = new Vector2(0f, 100f);

    [Header("Hand Animation Style")]
    [Tooltip("El pulse + tap hareketi yapsın (aşağı yukarı + scale).")]
    [SerializeField] private bool useTapMotion = true;
    [SerializeField, Min(0f)] private float tapDownOffsetY = -15f;

    private Coroutine pulseRoutine;
    private bool dismissed;
    private readonly List<Button> blockedButtons = new();
    private GameObject delayBlocker;   // gecikme boyunca HER girişi yutan tam-ekran bloklayıcı

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        // tutorialOverlay'i SetActive(false) yapMA — eğer script aynı GameObject'te ise
        // Start çalışmaz. Bunun yerine sadece CanvasGroup.alpha = 0 ile gizliyoruz.
        if (overlayGroup != null)
        {
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        Debug.Log($"[Tutorial] Start. IsSeen={IsSeen()}, gameObject.activeSelf={gameObject.activeSelf}, overlay={tutorialOverlay != null}");

        if (IsSeen())
        {
            Debug.Log("[Tutorial] Workshop repair tutorial zaten görüldü, atlanıyor.");
            return;
        }

        // ÖNEMLİ: input'u HEMEN kilitle (gecikme öncesi). Yoksa 0.6 sn içinde kullanıcı
        // başka ekrana geçip RepairButton'ı kaybediyor ve tutorial tamamlanamıyor.
        BlockBackgroundButtons();                        // butonları kapat (tutorial süresince)
        delayBlocker = CreateFullscreenBlocker();        // gecikme boyunca HER şeyi engelle
        if (repairButton != null) repairButton.onClick.AddListener(OnRepairButtonClicked);

        StartCoroutine(ShowTutorialDelayed());
    }

    // Tam ekran görünmez raycast yakalayıcı — pointer-handler'lı objeleri (level butonu vb.)
    // de durdurur. Kendi ScreenSpaceOverlay canvas'ında en üstte durur.
    private GameObject CreateFullscreenBlocker()
    {
        var go = new GameObject("TutorialDelayBlocker",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);   // görünmez ama tıklamayı yutar
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return go;
    }

    private void OnDestroy()
    {
        if (repairButton != null) repairButton.onClick.RemoveListener(OnRepairButtonClicked);
        RestoreBackgroundButtons();
        if (delayBlocker != null) Destroy(delayBlocker);
    }

    // ── Show / Hide ──────────────────────────────────────────────────────────

    private IEnumerator ShowTutorialDelayed()
    {
        yield return new WaitForSeconds(showDelay);
        ShowTutorial();
    }

    private void ShowTutorial()
    {
        Debug.Log("[Tutorial] ShowTutorial() çağrıldı.");

        if (dismissed) return;
        if (repairButton == null)
        {
            Debug.LogWarning("[Tutorial] repairButton atanmamış!");
            return;
        }

        // Gecikme bloklayıcısını kaldır → artık repair tıklanabilir (diğer butonlar hâlâ kapalı).
        if (delayBlocker != null) { Destroy(delayBlocker); delayBlocker = null; }

        if (tutorialOverlay != null && !tutorialOverlay.activeSelf) tutorialOverlay.SetActive(true);
        if (overlayGroup != null) overlayGroup.blocksRaycasts = false;

        // (BlockBackgroundButtons + repairButton aboneliği artık Start'ta yapılıyor.)

        // Description text
        if (descriptionText != null)
        {
            string text = !string.IsNullOrEmpty(descriptionLocalizationKey)
                ? GameLocalization.Get(descriptionLocalizationKey)
                : descriptionFallback;
            if (string.IsNullOrEmpty(text) || text == descriptionLocalizationKey)
                text = descriptionFallback;
            descriptionText.text = text;
        }

        // Position hand above button
        PositionHandAboveButton();

        // Start pulse + fade in
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseHandLoop());

        StartCoroutine(FadeOverlay(0f, 1f));
    }

    private void OnRepairButtonClicked()
    {
        if (dismissed) return;
        DismissTutorial();
    }

    private void DismissTutorial()
    {
        if (dismissed) return;
        dismissed = true;

        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        if (delayBlocker != null) { Destroy(delayBlocker); delayBlocker = null; }

        if (repairButton != null) repairButton.onClick.RemoveListener(OnRepairButtonClicked);

        RestoreBackgroundButtons();
        StartCoroutine(FadeOutAndHide());
        MarkSeen();
    }

    private IEnumerator FadeOutAndHide()
    {
        yield return FadeOverlay(overlayGroup != null ? overlayGroup.alpha : 1f, 0f);
        // Görsel kaybolduktan sonra child GameObject'leri (dim, hand, text) inactive yap
        // ama script'in GameObject'i aktif kalsın.
        if (tutorialOverlay != null && tutorialOverlay != gameObject)
            tutorialOverlay.SetActive(false);
    }

    // ── Background blocking ──────────────────────────────────────────────────

    private void BlockBackgroundButtons()
    {
        blockedButtons.Clear();
        var allButtons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            if (btn == repairButton) continue;
            if (!btn.interactable) continue;
            btn.interactable = false;
            blockedButtons.Add(btn);
        }
    }

    private void RestoreBackgroundButtons()
    {
        foreach (var btn in blockedButtons)
        {
            if (btn != null)
                btn.interactable = true;
        }
        blockedButtons.Clear();
    }

    // ── Position & Animation ────────────────────────────────────────────────

    private void PositionHandAboveButton()
    {
        if (handIcon == null || repairButton == null) return;

        var btnRT = repairButton.GetComponent<RectTransform>();
        var handParent = handIcon.parent as RectTransform;
        if (btnRT == null || handParent == null) return;

        // Canvas type'tan bağımsız doğru hesap: button world pos → screen pos → handIcon parent local pos
        var canvas = handIcon.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, btnRT.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(handParent, screenPoint, cam, out Vector2 localPoint))
        {
            handIcon.anchoredPosition = localPoint + handOffset;
        }
        else
        {
            // Fallback
            handIcon.position = btnRT.position;
            handIcon.anchoredPosition += handOffset;
        }
    }

    private IEnumerator PulseHandLoop()
    {
        if (handIcon == null) yield break;
        Vector3 baseScale = Vector3.one;
        Vector2 baseAnchored = handIcon.anchoredPosition;

        while (true)
        {
            float t = 0f;
            while (t < pulseDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / pulseDuration);
                float sine = Mathf.Sin(k * Mathf.PI); // 0 → 1 → 0

                float scale = Mathf.Lerp(1f, pulseScale, sine);
                handIcon.localScale = baseScale * scale;

                if (useTapMotion)
                    handIcon.anchoredPosition = baseAnchored + new Vector2(0f, tapDownOffsetY * sine);

                yield return null;
            }
            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator FadeOverlay(float from, float to)
    {
        if (overlayGroup == null) yield break;
        overlayGroup.alpha = from;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        overlayGroup.alpha = to;
    }

    // ── PlayerPrefs ──────────────────────────────────────────────────────────

    public static bool IsSeen() => PlayerPrefs.GetInt(KeyTutorialSeen, 0) == 1;

    public static void MarkSeen()
    {
        PlayerPrefs.SetInt(KeyTutorialSeen, 1);
        PlayerPrefs.Save();
    }

    public static void ResetSeen()
    {
        PlayerPrefs.DeleteKey(KeyTutorialSeen);
        PlayerPrefs.Save();
    }
}
