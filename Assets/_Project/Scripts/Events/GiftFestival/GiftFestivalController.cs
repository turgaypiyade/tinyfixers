using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>MainMenu integration. The icon follows the existing event layout; the screen is built lazily.</summary>
public sealed class GiftFestivalController : MonoBehaviour
{
    private GiftFestivalConfig cfg;
    private RectTransform canvasRoot, icon;
    private GameObject badge;
    private TMP_Text countdown, ballCount;
    private GiftFestivalScreen screen;
    private GearCupJoinPopup joinPopup;
    private float nextCheck;
    private float menuReadyAt = -1f, clearSince = -1f;
    private bool focused = true;
    private readonly List<PopupEntranceAnimator> popupBuffer = new(16);
    private bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CurrentScene() => Install(SceneManager.GetActiveScene());
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

    public static GiftFestivalController Install(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.name, "MainMenu", StringComparison.OrdinalIgnoreCase) || RuntimeSimulationSession.IsActive) return null;
        var existing = UnityEngine.Object.FindFirstObjectByType<GiftFestivalController>(FindObjectsInactive.Include);
        if (existing != null && existing.gameObject.scene == scene) return existing;
        Transform leftPanel = null;
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length && leftPanel == null; i++) leftPanel = Find(roots[i].transform, "LeftEventPanel");
        if (leftPanel == null) return null;
        var parentCanvas = leftPanel.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return null;
        var host = GiftFestivalUI.Stretch("GiftFestivalEventSystem", parentCanvas.rootCanvas.transform);
        var controller = host.gameObject.AddComponent<GiftFestivalController>();
        controller.Initialize((RectTransform)parentCanvas.rootCanvas.transform, leftPanel);
        return controller;
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

    private void Initialize(RectTransform canvas, Transform panel)
    {
        cfg = GiftFestivalConfig.Shared;
        canvasRoot = canvas;
        if (cfg == null) return;
        // Keep the low-level main menu free of event textures and UI allocations.
        if (GiftFestivalState.IsUnlocked(cfg)) BuildIcon(panel);
        Refresh();
    }

    private void BuildIcon(Transform panel)
    {
        if (built || cfg.Art == null) return;
        built = true;
        icon = GiftFestivalUI.Rect("GiftFestivalEventIcon", panel, Vector2.one * 150, Vector2.zero);
        // Match the authored Gear Cup / Bridge icon footprint before the idle animator captures its scale.
        icon.localScale = Vector3.one * 1.15f;
        var logo = GiftFestivalUI.Picture("Logo", icon, cfg.Art.menuLogo, Vector2.one * 150, Vector2.zero);
        var button = icon.gameObject.AddComponent<Image>();
        button.color = Color.clear;
        GiftFestivalUI.Button(button, Open);
        logo.raycastTarget = false;
        icon.gameObject.AddComponent<EventIconAnimator>();
        countdown = GiftFestivalUI.Label("Countdown", icon, cfg, "", 21, new Vector2(121, 38), new Vector2(0, -45));
        var badgeImage = GiftFestivalUI.Picture("BallBadge", icon, BridgeRepairUI.Pill(), new Vector2(58, 42), new Vector2(58, 64));
        badgeImage.type = Image.Type.Sliced;
        badgeImage.color = new Color(0.82f, 0.1f, 0.12f);
        badge = badgeImage.gameObject;
        ballCount = GiftFestivalUI.Label("Count", badge.transform, cfg, "", 25, new Vector2(56, 39), Vector2.zero);
        icon.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        focused = true;
        menuReadyAt = clearSince = -1f;
        GiftFestivalState.OnChanged += Refresh;
        GameLocalization.OnLanguageChanged += Refresh;
    }
    private void OnDisable()
    {
        GiftFestivalState.OnChanged -= Refresh;
        GameLocalization.OnLanguageChanged -= Refresh;
        menuReadyAt = clearSince = -1f;
        popupBuffer.Clear();
    }
    private void OnDestroy()
    {
        if (icon != null) Destroy(icon.gameObject);
        if (screen != null) Destroy(screen.gameObject);
        if (joinPopup != null) Destroy(joinPopup.gameObject);
    }

    private void Update()
    {
        if (cfg == null || !focused || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        GiftFestivalState.Sync(cfg, DateTime.UtcNow);
        if (!built && GiftFestivalState.IsUnlocked(cfg))
        {
            var panel = Find(canvasRoot, "LeftEventPanel");
            if (panel != null) BuildIcon(panel);
        }
        Refresh();
        TryShowReminder();
    }

    private void OnApplicationFocus(bool value) => SetFocus(value);
    private void OnApplicationPause(bool paused) => SetFocus(!paused);
    private void SetFocus(bool value)
    {
        focused = value;
        menuReadyAt = clearSince = -1f;
    }

    private void TryShowReminder()
    {
        if (RuntimeSimulationSession.IsActive || !cfg.autoShowJoinPopup || !built
            || !GiftFestivalState.CanShowInvitation(cfg, DateTime.UtcNow)) return;
        if (!FirebaseCloudSaveService.RestoreResolved || LoadingScreenManager.IsVisible || CustomIntroLoadingManager.IsVisible)
        {
            menuReadyAt = clearSince = -1f;
            return;
        }
        if (menuReadyAt < 0f) menuReadyAt = Time.unscaledTime;
        // Safari/Bridge start after two seconds; Gear Cup checks after three.
        // Present this invitation after them, with an idle gap rather than stacking modals.
        if (Time.unscaledTime - menuReadyAt < 4f) return;
        if (IsReminderBlocked()) { clearSince = -1f; return; }
        if (clearSince < 0f) { clearSince = Time.unscaledTime; return; }
        if (Time.unscaledTime - clearSince < 1f) return;
        Open();
    }

    private bool IsReminderBlocked()
    {
        if ((screen != null && screen.gameObject.activeInHierarchy)
            || (joinPopup != null && joinPopup.gameObject.activeInHierarchy)
            || RuntimeChoicePopup.IsShowing || RewardChestRevealOverlay.IsShowing
            || ShopPurchaseCelebration.IsShowing || MarketNavigator.IsMenuMarketOpen()) return true;
        if (FindFirstObjectByType<TutorialOverlayController>() != null
            || WorkshopRepairButtonTutorial.BlocksOpeningPopups()
            || FindFirstObjectByType<EventScreenIris>() != null
            || FindFirstObjectByType<RisingIntroOverlay>() != null) return true;
        // Reuse the buffer, and scan only once a second while an invitation is pending.
        canvasRoot.GetComponentsInChildren(false, popupBuffer);
        bool blocked = false;
        for (int i = 0; i < popupBuffer.Count; i++)
            if (popupBuffer[i].IsVisible) { blocked = true; break; }
        popupBuffer.Clear();
        return blocked;
    }

    private void Refresh()
    {
        if (cfg == null || !built) return;
        var now = DateTime.UtcNow;
        bool live = GiftFestivalState.IsLive(cfg, now);
        if (icon.gameObject.activeSelf != live) icon.gameObject.SetActive(live);
        if (!live) return;
        countdown.text = TimeFormat.Countdown(GiftFestivalState.Window(cfg, now).End - now);
        int balls = GiftFestivalState.Balls;
        badge.SetActive(GiftFestivalState.HasJoined && balls > 0 && !GiftFestivalState.IsFinished(cfg));
        ballCount.text = balls.ToString();
    }

    public void Open()
    {
        if (cfg == null) return;
        GiftFestivalState.Sync(cfg, DateTime.UtcNow);
        if (!GiftFestivalState.IsLive(cfg, DateTime.UtcNow) || cfg.Art == null) return;
        if (!GiftFestivalState.HasJoined)
        {
            if (joinPopup == null)
                joinPopup = GearCupJoinPopup.CreateCustom(canvasRoot, "GiftFestivalJoinPopup", GearCupPreviewConfig.Shared,
                    cfg.Art.background, cfg.Art.font, () => GiftFestivalUI.L("title"), () => GiftFestivalUI.L("join_summary"),
                    InvitationRemaining, BuildInvitationHero, Join);
            joinPopup.Show(null);
            if (joinPopup.gameObject.activeInHierarchy)
                GiftFestivalState.MarkInvitationShown(cfg, DateTime.UtcNow);
            return;
        }
        OpenScreen();
    }

    private void Join()
    {
        if (this != null && GiftFestivalState.TryJoin(cfg, DateTime.UtcNow)) OpenScreen();
    }

    private TimeSpan InvitationRemaining()
    {
        var now = DateTime.UtcNow;
        GiftFestivalState.Sync(cfg, now);
        return GiftFestivalState.IsLive(cfg, now) ? GiftFestivalState.Window(cfg, now).End - now : TimeSpan.Zero;
    }

    private void BuildInvitationHero(RectTransform body)
    {
        var hero = GiftFestivalUI.Stretch("BaseballAnimation", body);
        hero.gameObject.AddComponent<GiftFestivalJoinHero>().Configure(cfg.Art);
    }

    private void OpenScreen()
    {
        if (screen == null)
        {
            var rt = GiftFestivalUI.Stretch("GiftFestivalScreen", canvasRoot);
            // Its own sorting boundary keeps animated event canvases from drawing over the screen.
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 150;
            rt.gameObject.AddComponent<GraphicRaycaster>();
            rt.gameObject.SetActive(false);
            screen = rt.gameObject.AddComponent<GiftFestivalScreen>();
            screen.Build(cfg);
        }
        screen.Open(icon);
    }
}
