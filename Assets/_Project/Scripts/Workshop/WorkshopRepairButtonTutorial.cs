using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü yönlendirmesi, iki aşama (kullanıcı kararı 2026-11-10: yeni oyuncu önce oynasın, görevler sonra):
///  • Hiç level bitirmemiş oyuncu (current_level 1): karartma + yalnız LEVEL butonu açık + el (metin yok; kullanıcı
///    2026-11-10 "zorla bastıralım"). Butona basınca kapanır; seen işaretlenmez (her açılışta level 1'deyse tekrar).
///  • Level 10 (ilk boss) bitince (current_level ≥ MissionsUnlockLevel; kullanıcı 2026-11-10 "önce oyun oynasın")
///    ilk ana menü ziyaretinde, kazanılan yıldızlar cüzdana uçtuktan sonra: RepairButton'a dokunmayı öğreten görev
///    öğreticisi (el + metin + diğer butonlar kilitli).
///  • İkisi de menü SAKİNKEN ve hedef gerçekten en üstteyken başlar (ProfileUnlockTutorial ile aynı denetim); sırasında
///    pencere/sandık/ödül uçuşu araya girerse kilit kalkar, bitince döner (eski tam-ekran gecikme blokajı popup'la
///    birleşince oyunu kilitliyordu — kaldırıldı).
///    Oyuncu butona tıklayınca kapanır, bir daha gösterilmez.
///
/// PlayerPrefs flag: "tutorial_seen_workshop_repair" (yalnız görev aşaması işaretler).
/// </summary>
public class WorkshopRepairButtonTutorial : MonoBehaviour
{
    private const string KeyTutorialSeen = "tutorial_seen_workshop_repair";
    /// Görev öğreticisi bu current_level'den itibaren açılır (= level 5 kazanıldı).
    public const int MissionsUnlockLevel = 11;
    private const float StarFlyWaitSeconds = 5f;   // kazanılan yıldızların cüzdana uçuşunu bekleme sınırı
    private const float IdleBeforeShow = 0.8f;     // menü bu kadar süre sakin + hedef en üstte kalınca başla

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
    private readonly List<UnityEngine.EventSystems.RaycastResult> hits = new();
    private UnityEngine.EventSystems.PointerEventData probe;
    private Button guidedButton;          // yönlendirilen buton (level ya da tamir)
    private bool markSeenOnDismiss, showDescription;
    private Canvas liftedButtonCanvas, liftedHandCanvas;
    private GraphicRaycaster liftedRaycaster;

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

    private IEnumerator Start()
    {
        if (IsSeen()) yield break;

        // İlk açılışta önce Şartlar/Gizlilik kabulü: öğretici tüm düğmeleri kapattığı için kabul ekranının
        // düğmeleri de kapanıyor, iki ekran birbirini kilitliyordu. Kabul bitene dek bekle.
        while (LegalConsent.NeedsAcceptance || LegalConsent.IsShowing) yield return null;
        if (IsSeen()) yield break;

        if (CurrentLevel.Global < MissionsUnlockLevel)
        {
            if (CurrentLevel.Global > 1) yield break;
            // Yeni oyuncu: LEVEL butonuna yönlendir (karartma + tek açık buton). Açılış penceresi önce kapansın;
            // buton bulunamazsa hiçbir şey kilitlenmez.
            var level = FindFirstObjectByType<MainMenuLevelButtonController>();
            if (level == null || !level.TryGetComponent(out Button levelButton)) yield break;
            yield return WaitUntilIdle(levelButton);
            if (this == null || levelButton == null) yield break;
            BeginGuide(levelButton, markSeen: false, showText: false);
            ShowTutorial();
            StartCoroutine(HideWhilePopupOpen());
            yield break;
        }

        // Level sonu yıldızları önce cüzdana uçsun: oyuncu "kazandım" anını görür, sonra harcamayı öğrenir.
        for (float waited = 0f; waited < StarFlyWaitSeconds
             && StarFlyToWalletAnimator.TryGetPendingReward(out _, out _, out _); waited += Time.unscaledDeltaTime)
            yield return null;
        if (IsSeen()) yield break;

        // Menü sakin + tamir butonu en üstte olana dek bekle; sonra kilitle ve göster (arada başka ekrana geçilirse
        // buton en üstte olmaz → beklemeye devam; eski "hemen kilitle" blokajı popup'la birleşince oyunu kilitliyordu).
        if (repairButton == null) yield break;
        yield return WaitUntilIdle(repairButton);
        if (this == null || IsSeen()) yield break;
        BeginGuide(repairButton, markSeen: true, showText: true);
        ShowTutorial();
        StartCoroutine(HideWhilePopupOpen());
    }

    // Menü IdleBeforeShow boyunca sakin (yükleme/onay/pencere/sandık/ödül uçuşu/market yok) ve hedef dokunulabilir en
    // üst öğe kalana dek bekler. Sahne taramaları seyrek (0.25 sn).
    private IEnumerator WaitUntilIdle(Button target)
    {
        yield return new WaitForSeconds(showDelay);   // sahne açılış animasyonları otursun
        var wait = new WaitForSecondsRealtime(0.25f);
        float calm = 0f;
        while (calm < IdleBeforeShow)
        {
            yield return wait;
            if (this == null || target == null) yield break;
            bool idle = target.isActiveAndEnabled && !MenuInterrupted()
                        && ProfileUnlockTutorial.IsTopmost((RectTransform)target.transform, transform, hits, ref probe);
            calm = idle ? calm + 0.25f : 0f;
        }
    }

    // Yönlendirmeyi bekletmesi/geri çekmesi gereken an: açık pencere, market ya da menü meşguliyeti.
    private static bool MenuInterrupted()
        => AnyPopupVisible() || MarketNavigator.IsMenuMarketOpen() || ProfileUnlockTutorial.IsMenuBusyExceptMissions();

    /// Açılış pencerelerinin (etkinlik, profil, Gear Cup önizleme) beklemesi gereken görev öğreticisi bu sahnede
    /// sırada mı? Level 1–10'da öğretici beklemediği için pencereleri susturmaz.
    public static bool BlocksOpeningPopups()
        => !IsSeen() && CurrentLevel.Global >= MissionsUnlockLevel
           && FindFirstObjectByType<WorkshopRepairButtonTutorial>() != null;

    // Yönlendirilen buton dışındaki her butonu kapatır, tıklamayı dinler.
    private void BeginGuide(Button target, bool markSeen, bool showText)
    {
        guidedButton = target;
        markSeenOnDismiss = markSeen;
        showDescription = showText;
        BlockBackgroundButtons();
        if (guidedButton != null) guidedButton.onClick.AddListener(OnGuidedButtonClicked);
    }

    // Yönlendirme sırasında bir pencere/sandık/ödül uçuşu araya girerse (geç gelen bildirim izni vb.) tamamen çekilir:
    // kilit kalkar (pencerenin butonları da kilitlenmiş olabilir → tuzak olmasın), karartma + el gizlenir.
    // Pencere kapanınca kilit ve görünüm geri gelir.
    private IEnumerator HideWhilePopupOpen()
    {
        var wait = new WaitForSecondsRealtime(0.4f);
        bool hidden = false;
        while (!dismissed && this != null)
        {
            bool popup = MenuInterrupted();
            if (popup != hidden)
            {
                hidden = popup;
                if (popup) { RestoreBackgroundButtons(); Unlift(immediate: true); }
                else { BlockBackgroundButtons(); Lift(); }
                if (overlayGroup != null) overlayGroup.alpha = popup ? 0f : 1f;
            }
            yield return wait;
        }
    }

    private static bool AnyPopupVisible()
    {
        var popups = FindObjectsByType<PopupEntranceAnimator>(FindObjectsSortMode.None);
        for (int i = 0; i < popups.Length; i++) if (popups[i].IsVisible) return true;
        return false;
    }

    private void OnDestroy()
    {
        if (guidedButton != null) guidedButton.onClick.RemoveListener(OnGuidedButtonClicked);
        RestoreBackgroundButtons();
        Unlift(immediate: false);
    }

    // ── Show / Hide ──────────────────────────────────────────────────────────

    private void ShowTutorial()
    {
        if (dismissed) return;
        if (guidedButton == null)
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning("[Tutorial] yönlendirilecek buton yok (repairButton atanmamış?)");
            RestoreBackgroundButtons();
            return;
        }

        if (tutorialOverlay != null && !tutorialOverlay.activeSelf) tutorialOverlay.SetActive(true);
        if (overlayGroup != null) overlayGroup.blocksRaycasts = false;

        // Description text (level yönlendirmesinde yok: el yeterli)
        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(showDescription);
            if (showDescription)
            {
                string text = !string.IsNullOrEmpty(descriptionLocalizationKey)
                    ? GameLocalization.Get(descriptionLocalizationKey)
                    : descriptionFallback;
                if (string.IsNullOrEmpty(text) || text == descriptionLocalizationKey)
                    text = descriptionFallback;
                descriptionText.text = text;
            }
        }

        PositionHand(guidedButton.GetComponent<RectTransform>());
        Lift();

        // Start pulse + fade in
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseHandLoop());

        StartCoroutine(FadeOverlay(0f, 1f));
    }

    private void OnGuidedButtonClicked()
    {
        if (dismissed) return;
        DismissTutorial();
    }

    private void DismissTutorial()
    {
        if (dismissed) return;
        dismissed = true;

        if (pulseRoutine != null) StopCoroutine(pulseRoutine);

        if (guidedButton != null) guidedButton.onClick.RemoveListener(OnGuidedButtonClicked);

        RestoreBackgroundButtons();
        Unlift(immediate: true);
        StartCoroutine(FadeOutAndHide());
        if (markSeenOnDismiss) MarkSeen();
    }

    private IEnumerator FadeOutAndHide()
    {
        yield return FadeOverlay(overlayGroup != null ? overlayGroup.alpha : 1f, 0f);
        // Görsel kaybolduktan sonra child GameObject'leri (dim, hand, text) inactive yap
        // ama script'in GameObject'i aktif kalsın.
        if (tutorialOverlay != null && tutorialOverlay != gameObject)
            tutorialOverlay.SetActive(false);
    }

    // ── Spot ışığı: karartma ana Canvas'ta butonlardan SONRA çizilir (hedef de kararıyordu). Hedef butona ve ele
    // geçici override-sorting Canvas eklenir → karartmanın üstünde, tıklanabilir. Kapanınca kaldırılır. ──

    private void Lift()
    {
        if (guidedButton == null) return;
        var root = guidedButton.GetComponentInParent<Canvas>();
        int baseOrder = root != null ? root.rootCanvas.sortingOrder : 0;
        if (!guidedButton.TryGetComponent<Canvas>(out _))
        {
            liftedButtonCanvas = guidedButton.gameObject.AddComponent<Canvas>();
            liftedButtonCanvas.overrideSorting = true;
            liftedButtonCanvas.sortingOrder = baseOrder + 1;
            liftedRaycaster = guidedButton.gameObject.AddComponent<GraphicRaycaster>();
        }
        if (handIcon != null && !handIcon.TryGetComponent<Canvas>(out _))
        {
            liftedHandCanvas = handIcon.gameObject.AddComponent<Canvas>();
            liftedHandCanvas.overrideSorting = true;
            liftedHandCanvas.sortingOrder = baseOrder + 2;
        }
    }

    // GraphicRaycaster Canvas'a bağımlı: önce o kaldırılır.
    private void Unlift(bool immediate)
    {
        void Kill(Component c)
        {
            if (c == null) return;
            if (immediate) DestroyImmediate(c); else Destroy(c);
        }
        Kill(liftedRaycaster); liftedRaycaster = null;
        Kill(liftedButtonCanvas); liftedButtonCanvas = null;
        Kill(liftedHandCanvas); liftedHandCanvas = null;
    }

    // ── Background blocking ──────────────────────────────────────────────────

    private void BlockBackgroundButtons()
    {
        blockedButtons.Clear();
        var allButtons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            if (btn == guidedButton) continue;
            if (!btn.interactable) continue;
            if (btn.GetComponentInParent<LegalConsent>(true) != null || btn.GetComponentInParent<LegalDocumentView>(true) != null)
                continue;   // kabul / belge pencereleri her zaman kullanılabilir
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

    private void PositionHand(RectTransform btnRT)
    {
        if (handIcon == null) return;
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
