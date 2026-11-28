using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ana menü entegrasyonu (Hediye Şenliği kalıbı): sağ event panelinde ortak şablon ikon + süre plakası
/// (EventIconPlate) + jeton rozeti. Açılış seviyesinden önce kilitli görünür (EventLockOverlay), sezon arasında gizli.
/// Makine ekranı ilk açılışta tembel kurulur. Saniyede bir kontrol; ekran yokken iş yapılmaz.
/// </summary>
public sealed class ClawMachineController : MonoBehaviour
{
    private const string PanelName = "RightEventPanel";
    private static readonly Color PlateColor = new Color(0.16f, 0.28f, 0.68f);   // 2026-11-28: pembe → lacivert (logo diskiyle aynı)
    private ClawMachineConfig cfg;
    private RectTransform canvasRoot, icon;
    private GameObject badge;
    private TMP_Text countdown, tokenCount;
    private ClawMachineScreen screen;
    private float nextCheck;
    private bool focused = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CurrentScene() => Install(SceneManager.GetActiveScene());
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

    private static void Install(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.name, "MainMenu", StringComparison.OrdinalIgnoreCase) || RuntimeSimulationSession.IsActive) return;
        var existing = FindFirstObjectByType<ClawMachineController>(FindObjectsInactive.Include);
        if (existing != null && existing.gameObject.scene == scene) return;
        var cfg = ClawMachineConfig.Shared;
        if (cfg == null || cfg.menuIcon == null) return;
        Transform panel = null;
        foreach (var root in scene.GetRootGameObjects()) { panel = Find(root.transform, PanelName); if (panel != null) break; }
        if (panel == null) return;
        var canvas = panel.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var host = ClawMachineUI.Stretch("ClawMachineEventSystem", canvas.rootCanvas.transform);
        var controller = host.gameObject.AddComponent<ClawMachineController>();
        controller.Initialize((RectTransform)canvas.rootCanvas.transform, panel, cfg);
    }

    private static Transform Find(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = Find(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private void Initialize(RectTransform canvas, Transform panel, ClawMachineConfig config)
    {
        cfg = config; canvasRoot = canvas;
        icon = ClawMachineUI.Rect("ClawMachineEventIcon", panel, Vector2.one * 150, Vector2.zero);
        icon.localScale = Vector3.one * 1.15f;   // diğer event ikonlarıyla aynı ayak izi
        var logo = ClawMachineUI.Picture("Logo", icon, cfg.menuIcon, Vector2.one * 150, Vector2.zero);
        logo.raycastTarget = false;
        var hit = icon.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        var button = icon.gameObject.AddComponent<Button>();
        button.targetGraphic = hit; button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Open);
        icon.gameObject.AddComponent<EventIconAnimator>();
        countdown = ClawMachineUI.Label("Countdown", icon, "", 21, new Vector2(121, 38), new Vector2(0, -45), PlateColor);
        EventIconPlate.Attach(countdown, PlateColor);
        var badgeImage = ClawMachineUI.Picture("TokenBadge", icon, BridgeRepairUI.Pill(), new Vector2(58, 42), new Vector2(58, 64));
        badgeImage.type = Image.Type.Sliced;
        badgeImage.color = new Color(0.82f, 0.1f, 0.12f);
        badge = badgeImage.gameObject;
        tokenCount = ClawMachineUI.Label("Count", badge.transform, "", 25, new Vector2(56, 39), Vector2.zero, badgeImage.color);
        Refresh();
    }

    private void OnEnable() { focused = true; ClawMachineState.OnChanged += Refresh; GameLocalization.OnLanguageChanged += Refresh; }
    private void OnDisable()
    {
        ProgressiveContent.CancelEvent(this);
        ClawMachineState.OnChanged -= Refresh; GameLocalization.OnLanguageChanged -= Refresh;
    }
    private void OnDestroy()
    {
        if (icon != null) Destroy(icon.gameObject);
        if (screen != null) Destroy(screen.gameObject);
    }
    private void OnApplicationFocus(bool value) { focused = value; menuReadyAt = clearSince = -1f; }
    private void OnApplicationPause(bool paused) { focused = !paused; menuReadyAt = clearSince = -1f; }

    private void Update()
    {
        if (cfg == null || !focused || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        ClawMachineState.Sync(cfg, DateTime.UtcNow);
        Refresh();
        TryShowSeasonSummary();
    }

    // ── Sezon sonu özeti: menü sakinken bir kez (Hediye Şenliği davet kuralları) ──
    private float menuReadyAt = -1f, clearSince = -1f;

    private void TryShowSeasonSummary()
    {
        if (RuntimeSimulationSession.IsActive || !ClawMachineState.Data.summaryPending) return;
        if (!FirebaseCloudSaveService.RestoreResolved || LoadingScreenManager.IsVisible || CustomIntroLoadingManager.IsVisible)
        { menuReadyAt = clearSince = -1f; return; }
        if (menuReadyAt < 0f) menuReadyAt = Time.unscaledTime;
        if (Time.unscaledTime - menuReadyAt < 4f) return;
        if (IsMenuBusy()) { clearSince = -1f; return; }
        if (clearSince < 0f) { clearSince = Time.unscaledTime; return; }
        if (Time.unscaledTime - clearSince < 1f) return;
        if (!ClawMachineState.TryTakeSummary(out int plush, out int tokens)) return;
        string body = string.Format(ClawMachineUI.L("end_body", "{0}/{1} peluş topladın."), plush, ClawMachineConfig.PlushCount);
        if (tokens > 0) body += "\n" + string.Format(ClawMachineUI.L("end_tokens", "Kullanılmayan {0} jeton silindi."), tokens);
        body += "\n" + ClawMachineUI.L("end_next", "Yeni sezon yakında başlıyor!");
        RuntimeChoicePopup.Show(ClawMachineUI.L("end_title", "Pençe Makinesi Bitti"), body);
    }

    private readonly System.Collections.Generic.List<PopupEntranceAnimator> popupBuffer = new(16);

    private bool IsMenuBusy()
    {
        if (ContentDownloadOverlay.IsVisible || (screen != null && screen.gameObject.activeInHierarchy)
            || RuntimeChoicePopup.IsShowing || RewardChestRevealOverlay.IsShowing
            || ShopPurchaseCelebration.IsShowing || MarketNavigator.IsMenuMarketOpen()) return true;
        if (FindFirstObjectByType<TutorialOverlayController>() != null
            || WorkshopRepairButtonTutorial.BlocksOpeningPopups()
            || DailySlotEventButton.WheelHasPriority
            || FindFirstObjectByType<EventScreenIris>() != null
            || FindFirstObjectByType<RisingIntroOverlay>() != null) return true;
        canvasRoot.GetComponentsInChildren(false, popupBuffer);
        bool busy = false;
        for (int i = 0; i < popupBuffer.Count; i++)
            if (popupBuffer[i].IsVisible) { busy = true; break; }
        popupBuffer.Clear();
        return busy;
    }

    private void Refresh()
    {
        if (cfg == null || icon == null) return;
        var now = DateTime.UtcNow;
        // Açılış seviyesinden önce kilitli görünür (diğer event ikonları gibi); editör debug kapıyı açabilir.
        if (!ClawMachineState.IsUnlocked(cfg))
        {
            if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
            EventLockOverlay.Apply(icon.gameObject, cfg.unlockLevel, countdown);
            badge.SetActive(false);
            return;
        }
        // Kapalı gün / sezon arası / mola: gizlemek yerine gri "YAKINDA"; dokununca açılışa kalan süre.
        if (!ClawMachineState.CanGrab(cfg, now))
        {
            EventLockOverlay.ApplyWaiting(icon.gameObject, ClawMachineState.NextOpen(cfg, now), countdown);
            badge.SetActive(false);
            return;
        }
        EventLockOverlay.Release(icon.gameObject);
        if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
        // Oturum sürüyorsa kalan oyun, yoksa bugünün kapanışına kalan süre.
        var session = ClawMachineState.SessionLeft(cfg, now);
        countdown.text = TimeFormat.Countdown(session > TimeSpan.Zero ? session : ClawMachineState.OpenDayEnd(cfg, now) - now);
        int tokens = ClawMachineState.Tokens;
        badge.SetActive(ClawMachineState.HasJoined && tokens > 0);
        tokenCount.text = tokens.ToString();
    }

    public void Open()
    {
        if (cfg == null || !ClawMachineState.IsUnlocked(cfg)) return;
        // Makine/pençe/kapsül/peluş görselleri event paketinde (parçalı içerik); ikon yerel kalır.
        if (ProgressiveContent.GateEvent(this, EventContentPolicy.Claw, Open, () => cfg != null && ClawMachineState.IsLive(cfg, DateTime.UtcNow))) return;
        ClawMachineState.Sync(cfg, DateTime.UtcNow);
        if (!ClawMachineState.IsLive(cfg, DateTime.UtcNow)) return;
        if (screen == null)
        {
            var rt = ClawMachineUI.Stretch("ClawMachineScreen", canvasRoot);
            // Kendi sıralama sınırı: animasyonlu event canvas'ları ekranın üstüne çizilmesin.
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 150;
            rt.gameObject.AddComponent<GraphicRaycaster>();
            rt.gameObject.SetActive(false);
            screen = rt.gameObject.AddComponent<ClawMachineScreen>();
            screen.Build(cfg);
        }
        screen.Open(icon);
    }
}
