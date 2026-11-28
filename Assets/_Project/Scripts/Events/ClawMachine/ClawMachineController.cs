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
    private static readonly Color PlateColor = new Color(0.86f, 0.30f, 0.62f);
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
    private void OnApplicationFocus(bool value) => focused = value;
    private void OnApplicationPause(bool paused) => focused = !paused;

    private void Update()
    {
        if (cfg == null || !focused || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        ClawMachineState.Sync(cfg, DateTime.UtcNow);
        Refresh();
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
        bool live = ClawMachineState.IsLive(cfg, now);
        if (!live)
        {
            // Ara dönem: gizlemek yerine gri "YAKINDA" (kullanıcı 2026-11-28); dokununca başlamasına kalan süre.
            var window = ClawMachineState.Window(cfg, now);
            EventLockOverlay.ApplyWaiting(icon.gameObject, window.NextStart, countdown);
            badge.SetActive(false);
            return;
        }
        EventLockOverlay.Release(icon.gameObject);
        if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
        countdown.text = TimeFormat.Countdown(ClawMachineState.Window(cfg, now).End - now);
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
