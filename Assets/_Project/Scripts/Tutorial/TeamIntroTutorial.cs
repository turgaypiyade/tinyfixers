using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Takım tanıtımı (kullanıcı 2026-11-17; D1 ~%15, takımlar baştan açık ama fark edilmiyordu): ilk boss (level 10)
/// kazanılınca, takımı olmayan oyuncuya bir kez. (1) Karartma + parmak alt bardaki TAKIM sekmesini gösterir
/// → dokununca takım ekranı açılır, öğretici çekilir (oyuncu serbestçe takım arar). (2) Bu oturumda bir takıma
/// katılırsa parmak "Can İste" butonunu gösterir; herhangi bir dokunuşla biter. İlk takımda can isteği hemen
/// açıktır (PlayerTeamState: takım DEĞİŞTİREN 24 saat bekler).
/// ProfileUnlockTutorial ile aynı düzen: menü sakin + hedef en üstte olunca başlar, araya pencere girerse çekilir.
/// </summary>
public sealed class TeamIntroTutorial : MonoBehaviour
{
    private const string SeenKey = "tutorial_seen_team_intro";
    private const string MainMenuSceneName = "MainMenu";
    /// current_level ≥ bu (= level 10, ilk boss kazanıldı). Açılış sırası: görev 6 · profil 9 · TAKIM 11 · Görevler 12.
    public const int UnlockLevel = 11;
    private const float IdleBeforeShow = 1f;
    private const float JoinWaitSeconds = 180f;     // takım ekranında katılma beklemesi (sonra sessizce biter)
    private const float HolePadding = 14f;
    private static readonly Vector2 HandSize = new Vector2(150f, 159f);
    private static readonly Vector2 HandTipPivot = new Vector2(0.40f, 0.03f);
    private const float TapTravel = 22f;
    private const float TapPeriod = 0.8f;
    private const float FadeDuration = 0.25f;
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.85f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MainMenuSceneName || IsSeen() || RuntimeSimulationSession.IsActive) return;
        if (CurrentLevel.Global < UnlockLevel) return;
        if (PlayerTeamState.HasTeam) { MarkSeen(); return; }   // zaten takımda
        new GameObject("TeamIntroTutorial").AddComponent<TeamIntroTutorial>();
    }

    public static bool IsSeen() => PlayerPrefs.GetInt(SeenKey, 0) == 1;

    private static void MarkSeen()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    // ── Instance ─────────────────────────────────────────────────────

    private RectTransform root;
    private CanvasGroup group;
    private TutorialSpotlightGraphic dim;
    private RectTransform hand;
    private TMP_Text caption;

    private BottomTabController tabs;
    private int teamTab = -1;
    private Button tabButton;
    private RectTransform target;
    private Rect lastHole;
    private bool tapAnywhere;
    private bool tapped;
    private readonly Vector3[] corners = new Vector3[4];
    private readonly List<Rect> dimRects = new List<Rect>(4);
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private PointerEventData probe;
    private float tapTime;
    private float nextFind;

    private IEnumerator Start()
    {
        BuildOverlay();
        SetVisible(false);

        // 1) Takım sekmesi.
        while (true)
        {
            yield return WaitUntilIdle();
            if (tabButton == null) { Destroy(gameObject); yield break; }
            ShowStep((RectTransform)tabButton.transform, "team_tutorial_open", anywhere: false);
            yield return Fade(1f);
            bool interrupted = false;
            while (tabs != null && tabs.CurrentIndex != teamTab)
            {
                if (tabButton == null || !tabButton.isActiveAndEnabled || Busy()) { interrupted = true; break; }
                yield return null;
            }
            if (!interrupted) break;
            yield return Fade(0f);
            SetVisible(false);
        }

        MarkSeen();   // sekmeyi açtı: tekrar ısrar edilmez
        group.blocksRaycasts = false;
        yield return Fade(0f);
        SetVisible(false);

        // 2) Bu ziyarette takıma katılırsa "Can İste"yi göster (katılmazsa sessizce biter).
        float until = Time.unscaledTime + JoinWaitSeconds;
        while (!PlayerTeamState.HasTeam)
        {
            if (Time.unscaledTime > until || tabs == null || tabs.CurrentIndex != teamTab) { Destroy(gameObject); yield break; }
            yield return null;
        }
        var screen = FindAnyObjectByType<TeamScreenController>();
        var request = screen != null ? screen.RequestLifeButton : null;
        float wait = Time.unscaledTime + 5f;
        while (request != null && !IsTopmostTarget((RectTransform)request.transform))
        {
            if (Time.unscaledTime > wait) { Destroy(gameObject); yield break; }
            yield return new WaitForSecondsRealtime(0.25f);
        }
        if (request == null) { Destroy(gameObject); yield break; }

        ShowStep((RectTransform)request.transform, "team_tutorial_request", anywhere: true);
        yield return Fade(1f);
        tapped = false;
        while (!tapped && request != null && request.isActiveAndEnabled) yield return null;
        group.blocksRaycasts = false;
        yield return Fade(0f);
        Destroy(gameObject);
    }

    private IEnumerator WaitUntilIdle()
    {
        float calm = 0f;
        while (calm < IdleBeforeShow)
        {
            yield return null;
            if (tabButton == null && Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 0.5f;
                FindTargets();
            }
            calm = tabButton != null && !Busy() && IsTopmostTarget((RectTransform)tabButton.transform)
                ? calm + Time.unscaledDeltaTime : 0f;
        }
    }

    // Diğer menü anları (yükleme, pencere, görev/profil öğreticisi, bildirim izni) önce biter.
    // Sahne taraması (FindAnyObjectByType) her kare değil, saniyede 4 kez.
    private float nextBusyCheck;
    private bool busy;

    private bool Busy()
    {
        if (Time.unscaledTime < nextBusyCheck) return busy;
        nextBusyCheck = Time.unscaledTime + 0.25f;
        busy = BusyNow();
        return busy;
    }

    private static bool BusyNow()
        => ProfileUnlockTutorial.IsMenuBusyExceptMissions() || WorkshopRepairButtonTutorial.BlocksOpeningPopups()
           || NotificationPermissionPrompt.WillShowThisSession && FindAnyObjectByType<NotificationPermissionPrompt>() != null
           || FindAnyObjectByType<ProfileUnlockTutorial>() != null;

    private bool IsTopmostTarget(RectTransform t) => ProfileUnlockTutorial.IsTopmost(t, transform, hits, ref probe);

    private void FindTargets()
    {
        tabs = FindAnyObjectByType<BottomTabController>();
        if (tabs == null) return;
        teamTab = tabs.FindTabIndex<TeamScreenController>();
        tabButton = tabs.TabButton(teamTab);
    }

    // ── Görsel (ProfileUnlockTutorial ile aynı) ─────────────────────

    private void BuildOverlay()
    {
        gameObject.layer = 5;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();
        root = (RectTransform)transform;

        var dimGo = NewChild("Dim", typeof(TutorialSpotlightGraphic), typeof(Button));
        Stretch((RectTransform)dimGo.transform);
        dim = dimGo.GetComponent<TutorialSpotlightGraphic>();
        dim.color = DimColor;
        dim.raycastTarget = true;
        dim.UseRaycastHole = true;
        var dimButton = dimGo.GetComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(() => { if (tapAnywhere) tapped = true; });

        var handGo = NewChild("Finger", typeof(Image));
        hand = (RectTransform)handGo.transform;
        hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
        hand.pivot = HandTipPivot;
        hand.sizeDelta = HandSize;
        var handImg = handGo.GetComponent<Image>();
        handImg.sprite = UiIcons.Finger;
        handImg.preserveAspect = true;
        handImg.raycastTarget = false;

        var capGo = NewChild("Caption", typeof(TextMeshProUGUI));
        var capRt = (RectTransform)capGo.transform;
        capRt.anchorMin = capRt.anchorMax = new Vector2(0.5f, 0.5f);
        capRt.sizeDelta = new Vector2(860f, 150f);
        caption = capGo.GetComponent<TextMeshProUGUI>();
        caption.alignment = TextAlignmentOptions.Center;
        caption.textWrappingMode = TextWrappingModes.Normal;
        caption.enableAutoSizing = true;
        caption.fontSizeMax = 54f;
        caption.fontSizeMin = 34f;
        caption.color = Color.white;
        caption.raycastTarget = false;
    }

    private GameObject NewChild(string childName, params System.Type[] components)
    {
        var types = new System.Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        components.CopyTo(types, 1);
        var go = new GameObject(childName, types);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // anywhere: delik de dokunuşu yutar (bilgi adımı; ekrana dokununca biter), değilse yalnız delik geçirir.
    private void ShowStep(RectTransform newTarget, string captionKey, bool anywhere)
    {
        target = newTarget;
        lastHole = default;
        tapTime = 0f;
        tapAnywhere = anywhere;
        dim.UseRaycastHole = !anywhere;
        caption.text = GameLocalization.Get(captionKey);
        var skin = CommonPopupSkin.Shared;
        var font = skin != null ? skin.celebrationFont : null;
        if (font != null && caption.font != font)
        {
            caption.font = font;
            caption.fontSharedMaterial = TutorialOverlayController.CaptionMaterial(font);
        }
        SetVisible(true);
        UpdateLayout();
    }

    private void SetVisible(bool visible)
    {
        group.blocksRaycasts = visible;
        if (!visible) { group.alpha = 0f; target = null; }
    }

    private IEnumerator Fade(float to)
    {
        float from = group.alpha;
        for (float t = 0f; t < FadeDuration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / FadeDuration);
            yield return null;
        }
        group.alpha = to;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        tapTime += Time.unscaledDeltaTime;
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        if (target == null) return;
        Rect hole = HoleOf(target);
        if (hole != lastHole)
        {
            lastHole = hole;
            dim.RaycastHole = hole;
            Rect full = root.rect;
            dimRects.Clear();
            dimRects.Add(Rect.MinMaxRect(full.xMin, full.yMin, full.xMax, hole.yMin));
            dimRects.Add(Rect.MinMaxRect(full.xMin, hole.yMax, full.xMax, full.yMax));
            dimRects.Add(Rect.MinMaxRect(full.xMin, hole.yMin, hole.xMin, hole.yMax));
            dimRects.Add(Rect.MinMaxRect(hole.xMax, hole.yMin, full.xMax, hole.yMax));
            dim.SetRects(dimRects);
        }

        bool pointUp = hole.center.y > 0f;
        float away = pointUp ? -1f : 1f;
        float edge = pointUp ? hole.yMin : hole.yMax;
        float tap = Mathf.Sin(Mathf.Repeat(tapTime / TapPeriod, 1f) * Mathf.PI);
        hand.localEulerAngles = new Vector3(0f, 0f, pointUp ? 180f : 0f);
        hand.anchoredPosition = new Vector2(hole.center.x, edge + away * TapTravel * (1f - tap));

        var capRt = caption.rectTransform;
        float capY = edge + away * (HandSize.y + TapTravel + capRt.sizeDelta.y * 0.5f + 10f);
        capRt.anchoredPosition = new Vector2(0f, capY);
    }

    private Rect HoleOf(RectTransform t)
    {
        t.GetWorldCorners(corners);
        var canvas = t.GetComponentInParent<Canvas>();
        canvas = canvas != null ? canvas.rootCanvas : null;
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        return Rect.MinMaxRect(min.x - HolePadding, min.y - HolePadding, max.x + HolePadding, max.y + HolePadding);
    }
}
