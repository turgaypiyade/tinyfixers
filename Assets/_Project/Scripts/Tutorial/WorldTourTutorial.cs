using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dünya turu tanıtımı (kullanıcı 2026-11-19): dünya butonu ilk açıldığında (ilk görev adımı ya da level 8,
/// WorldTourGlobeButton.IsUnlocked) bir kez. (1) Karartma + parmak dünya butonunu gösterir: "Görevleri tamamla,
/// dünyayı güzelleştir!" → dokununca harita açılır. (2) Parmak sıradaki harikanın pinini gösterir → dokununca
/// şehir açılır, öğretici biter. Yıkık şehirdeki "Tamir Et!" oyuncuyu Görevler'e götürür.
/// TeamIntroTutorial ile aynı düzen: menü sakin + hedef en üstte olunca başlar, araya pencere girerse çekilir.
/// Delik hedef kadar YUVARLAK (köşeli kesik kenarı görünmesin; kullanıcı 2026-11-19): dörtgen karartma + delik
/// karesine yuvarlak oyuklu maske; hedef hafifçe büyüyüp küçülür, çevresinde sıcak bir hale nabız atar.
/// </summary>
public sealed class WorldTourTutorial : MonoBehaviour
{
    private const string SeenKey = "tutorial_seen_world_tour";
    private const string MainMenuSceneName = "MainMenu";
    private const int MissionsLevel = 6;            // görev adımı bundan önce yapılamaz → daha erken izlemeye gerek yok
    private const float IdleBeforeShow = 1f;
    private const float MapSettleSeconds = 1.3f;    // harita açılış/uçak animasyonu oturana kadar
    private const float PinWaitSeconds = 30f;
    private const float HolePadding = 10f;
    private const float PulsePeriod = 1.1f, PulseScale = 0.08f;   // hedef: 1 → 1.08 → 1
    private const float HaloSize = 1.7f;                          // hale çapı / delik çapı
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
        if (CurrentLevel.Global < MissionsLevel || !WorldTourMap.HasArt) return;
        new GameObject("WorldTourTutorial").AddComponent<WorldTourTutorial>();
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
    private RectTransform holeMask, halo;
    private Image haloImg;
    private Vector3 targetBaseScale = Vector3.one;
    private static Sprite s_holeSprite, s_haloSprite;

    private WorldTourGlobeButton globe;
    private RectTransform target;
    private Rect lastHole;
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

        // 1) Dünya butonu (kilit açılınca görünür; açılana kadar seyrek yoklama).
        while (true)
        {
            yield return WaitUntilIdle();
            ShowStep((RectTransform)globe.transform, "worldtour_tutorial_open");
            yield return Fade(1f);
            bool interrupted = false;
            while (!WorldTourMap.IsFullScreenOpen)
            {
                if (globe == null || !globe.isActiveAndEnabled || Busy()) { interrupted = true; break; }
                yield return null;
            }
            if (!interrupted) break;
            yield return Fade(0f);
            SetVisible(false);
        }

        MarkSeen();   // haritayı açtı: tekrar ısrar edilmez
        SetVisible(false);
        yield return new WaitForSecondsRealtime(MapSettleSeconds);

        // 2) Sıradaki harikanın pini → şehir.
        var pin = WorldTourMap.ActiveBadge;
        if (!WorldTourMap.IsFullScreenOpen || pin == null) { Destroy(gameObject); yield break; }
        ShowStep(pin, "worldtour_tutorial_pin");
        yield return Fade(1f);
        float until = Time.unscaledTime + PinWaitSeconds;
        while (!CityTourView.IsOpen && WorldTourMap.IsFullScreenOpen && pin != null && Time.unscaledTime < until)
            yield return null;
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
            if (globe == null && Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 0.5f;
                globe = FindAnyObjectByType<WorldTourGlobeButton>();   // yalnız aktif (açık + HOME) olan bulunur
            }
            calm = globe != null && globe.isActiveAndEnabled && !Busy() && IsTopmostTarget((RectTransform)globe.transform)
                ? calm + Time.unscaledDeltaTime : 0f;
        }
    }

    private float nextBusyCheck;
    private bool busy;

    private bool Busy()
    {
        if (Time.unscaledTime < nextBusyCheck) return busy;
        nextBusyCheck = Time.unscaledTime + 0.25f;
        busy = ProfileUnlockTutorial.IsMenuBusyExceptMissions() || WorkshopRepairButtonTutorial.BlocksOpeningPopups()
               || NotificationPermissionPrompt.WillShowThisSession && FindAnyObjectByType<NotificationPermissionPrompt>() != null
               || FindAnyObjectByType<ProfileUnlockTutorial>() != null || FindAnyObjectByType<TeamIntroTutorial>() != null;
        return busy;
    }

    private bool IsTopmostTarget(RectTransform t) => ProfileUnlockTutorial.IsTopmost(t, transform, hits, ref probe);

    // ── Görsel (TeamIntroTutorial ile aynı) ─────────────────────────

    private void BuildOverlay()
    {
        gameObject.layer = 5;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 855;   // tam ekran harita (850) üstü, şehir (860) altı
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();
        root = (RectTransform)transform;

        var dimGo = NewChild("Dim", typeof(TutorialSpotlightGraphic));
        Stretch((RectTransform)dimGo.transform);
        dim = dimGo.GetComponent<TutorialSpotlightGraphic>();
        dim.color = DimColor;
        dim.raycastTarget = true;
        dim.UseRaycastHole = true;   // yalnız delik (hedef buton) dokunuşu geçirir

        // Yuvarlak delik: kare deliğin köşelerini karartma renginde örter (ortası saydam, kenarı yumuşak).
        var maskGo = NewChild("HoleMask", typeof(Image));
        holeMask = (RectTransform)maskGo.transform;
        holeMask.anchorMin = holeMask.anchorMax = new Vector2(0.5f, 0.5f);
        var maskImg = maskGo.GetComponent<Image>();
        maskImg.sprite = HoleSprite();
        maskImg.color = DimColor;
        maskImg.raycastTarget = false;

        var haloGo = NewChild("Halo", typeof(Image));
        halo = (RectTransform)haloGo.transform;
        halo.anchorMin = halo.anchorMax = new Vector2(0.5f, 0.5f);
        haloImg = haloGo.GetComponent<Image>();
        haloImg.sprite = HaloSprite();
        haloImg.color = new Color(1f, 0.86f, 0.35f, 0.9f);   // sıcak altın parıltı
        haloImg.raycastTarget = false;

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

    private void ShowStep(RectTransform newTarget, string captionKey)
    {
        RestoreTargetScale();
        target = newTarget;
        targetBaseScale = newTarget.localScale;
        lastHole = default;
        tapTime = 0f;
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
        if (!visible) { RestoreTargetScale(); group.alpha = 0f; target = null; }
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

    private void RestoreTargetScale()
    {
        if (target != null) target.localScale = targetBaseScale;
    }

    private void OnDestroy() => RestoreTargetScale();

    private void LateUpdate()
    {
        if (target == null) return;
        tapTime += Time.unscaledDeltaTime;
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        if (target == null) return;
        target.localScale = targetBaseScale;          // delik büyümeden ölçülür (nabızla titremesin)
        Rect box = HoleOf(target);
        float radius = Mathf.Max(box.width, box.height) * 0.5f;
        Rect hole = new Rect(box.center.x - radius, box.center.y - radius, radius * 2f, radius * 2f);
        float pulse = 0.5f - 0.5f * Mathf.Cos(Mathf.Repeat(tapTime / PulsePeriod, 1f) * Mathf.PI * 2f);
        target.localScale = targetBaseScale * (1f + PulseScale * pulse);
        holeMask.anchoredPosition = hole.center;
        holeMask.sizeDelta = hole.size;
        halo.anchoredPosition = hole.center;
        halo.sizeDelta = hole.size * (HaloSize + 0.12f * pulse);
        haloImg.canvasRenderer.SetAlpha(0.55f + 0.45f * pulse);
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

    // Kare içinde yuvarlak oyuk: dışı opak (karartma rengiyle boyanır), içi saydam, 2 px yumuşak kenar.
    internal static Sprite HoleSprite()
    {
        if (s_holeSprite != null) return s_holeSprite;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TutorialRoundHole" };
        var px = new Color32[n * n];
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
            float a = Mathf.Clamp01((d - (r - 2f)) / 2f);
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s_holeSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return s_holeSprite;
    }

    // Hale: delik kenarında en parlak, dışa doğru sönen halka; ortası saydam (hedef görünür kalır).
    internal static Sprite HaloSprite()
    {
        if (s_haloSprite != null) return s_haloSprite;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TutorialHalo" };
        var px = new Color32[n * n];
        float r = n * 0.5f, inner = r / HaloSize;     // delik kenarı
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
            float a = d < inner ? Mathf.Clamp01((d - inner + 3f) / 3f) : 1f - Mathf.Clamp01((d - inner) / (r - inner));
            a = a * a;
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s_haloSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return s_haloSprite;
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
