using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Profil açılınca (PlayerProfile.UnlockLevel) bir kez gösterilen öğretici: ekran kararır, parmak üst bardaki
/// avatar butonunu gösterir → oyuncu dokununca profil açılır → parmak isim alanını gösterir → alana dokununca biter.
/// Sahne kurulumu yok: MainMenu yüklenince kendini kurar. Karartmada hedefin etrafı açıktır ve tıklama YALNIZ
/// o delikten geçer (diğer her şey kapalı). Menü boşta değilse (yükleme, popup, ödül uçuşu, başka öğretici,
/// hedefin üstünü örten herhangi bir şey) bekler; araya bir şey girerse geri çekilip tekrar bekler.
/// İsmini zaten değiştirmiş oyuncuya gösterilmez.
/// </summary>
public sealed class ProfileUnlockTutorial : MonoBehaviour
{
    private const string SeenKey = "tutorial_seen_profile_unlock";
    private const string MainMenuSceneName = "MainMenu";
    private const float IdleBeforeShow = 1f;      // menü bu kadar süre sakin kalınca başla
    private const float HolePadding = 14f;
    private static readonly Vector2 HandSize = new Vector2(150f, 159f);   // FingerTutorial 620x656
    private static readonly Vector2 HandTipPivot = new Vector2(0.40f, 0.03f);
    private const float TapTravel = 22f;
    private const float TapPeriod = 0.8f;
    private const float FadeDuration = 0.25f;
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.85f);   // kullanıcı 2026-11-11: biraz daha koyu

    // ── Bootstrap ────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MainMenuSceneName || IsSeen() || RuntimeSimulationSession.IsActive) return;
        if (!PlayerProfile.CustomizationUnlocked) return;
        if (!PlayerProfile.HasDefaultName) { MarkSeen(); return; }   // zaten değiştirmiş
        new GameObject("ProfileUnlockTutorial").AddComponent<ProfileUnlockTutorial>();
    }

    public static bool IsSeen() => PlayerPrefs.GetInt(SeenKey, 0) == 1;

    private static void MarkSeen()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    /// Test: öğreticiyi yeniden göster (isim de varsayılana döner — değiştirilmiş isimde gösterilmez).
    public static void ResetSeen()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerProfile.PlayerName = PlayerProfile.DefaultName;
    }

    // ── Instance ─────────────────────────────────────────────────────

    private RectTransform root;
    private CanvasGroup group;
    private TutorialSpotlightGraphic dim;
    private RectTransform hand;
    private TMP_Text caption;

    private Button avatarButton;
    private ProfilePageController profile;
    private RectTransform target;            // şu an gösterilen (avatar butonu ya da isim alanı)
    private Rect lastHole;
    private readonly Vector3[] corners = new Vector3[4];
    private readonly List<Rect> dimRects = new List<Rect>(4);
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private PointerEventData probe;
    private float tapTime;
    private float nextBusyCheck;
    private float nextFind;
    private bool busy;

    private IEnumerator Start()
    {
        BuildOverlay();
        SetVisible(false);

        while (true)
        {
            // 1) Menü otursun; hedef buton ekranda ve üstü açık olsun.
            yield return WaitUntilIdle();
            if (avatarButton == null) { Destroy(gameObject); yield break; }

            // 2) Avatar butonunu göster; dokununca profil açılır (butonun kendi Open'ı).
            ShowStep((RectTransform)avatarButton.transform, "profile_tutorial_open");
            yield return Fade(1f);
            bool interrupted = false;
            while (!ProfileOpen())
            {
                if (avatarButton == null || !avatarButton.isActiveAndEnabled || Busy()) { interrupted = true; break; }
                yield return null;
            }
            if (interrupted) { yield return Fade(0f); SetVisible(false); continue; }

            // 3) Profil açıldı: isim alanını göster (giriş animasyonu sürerken delik LateUpdate'te izler).
            var input = profile.NameInput;
            if (input == null) break;
            ShowStep((RectTransform)input.transform, "profile_tutorial_name");
            while (ProfileOpen() && !input.isFocused) yield return null;
            break;   // alana dokundu ya da profili kapattı → öğretici biter (tekrar ısrar etmez)
        }

        MarkSeen();
        group.blocksRaycasts = false;   // isim yazılırken solan karartma dokunuşu yutmasın
        yield return Fade(0f);
        Destroy(gameObject);
    }

    private bool ProfileOpen() => profile != null && profile.NameInput != null && profile.NameInput.gameObject.activeInHierarchy;

    private IEnumerator WaitUntilIdle()
    {
        float calm = 0f;
        while (calm < IdleBeforeShow)
        {
            yield return null;
            if (avatarButton == null && Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 0.5f;   // sahne taraması seyrek
                FindTargets();
            }
            calm = avatarButton != null && !Busy() && TargetIsTopmost((RectTransform)avatarButton.transform)
                ? calm + Time.unscaledDeltaTime : 0f;
        }
    }

    // Sahne taraması (FindAnyObjectByType) her kare değil, saniyede 4 kez.
    private bool Busy()
    {
        if (Time.unscaledTime < nextBusyCheck) return busy;
        nextBusyCheck = Time.unscaledTime + 0.25f;
        busy = IsMenuBusy();
        return busy;
    }

    private static bool IsMenuBusy() =>
        IsMenuBusyExceptMissions()
        || WorkshopRepairButtonTutorial.BlocksOpeningPopups();   // görev öğreticisi (level 10 sonrası) sırada

    /// Ana menüde yönlendirme başlatmaya engel bir an var mı (yükleme, onay, seçim/sandık/satın alma penceresi,
    /// reklam, ödül uçuşu)? Görev öğreticisi de kendi başlangıcında bunu kullanır (kendini beklemesin diye ayrı).
    internal static bool IsMenuBusyExceptMissions() =>
        LoadingScreenManager.IsVisible || CustomIntroLoadingManager.IsVisible
        || LegalConsent.NeedsAcceptance || LegalConsent.IsShowing
        || RuntimeChoicePopup.IsShowing || RewardChestRevealOverlay.IsShowing || ShopPurchaseCelebration.IsShowing
        || AdService.IsShowing || FindAnyObjectByType<MainMenuRewardCollectFx>() != null;

    // Profil sayfasını açan buton: onClick'inde ProfilePageController.Open kalıcı çağrısı olan, görünür buton.
    private void FindTargets()
    {
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var e = button.onClick;
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
            {
                if (e.GetPersistentTarget(i) is ProfilePageController page && e.GetPersistentMethodName(i) == "Open")
                {
                    avatarButton = button;
                    profile = page;
                    return;
                }
            }
        }
    }

    // Hedefin ortasına dokunulsa ilk ona mı gider? (popup, başka overlay ya da sekme değişimi örtmüş olabilir)
    private bool TargetIsTopmost(RectTransform t) => IsTopmost(t, transform, hits, ref probe);

    /// <paramref name="t"/>'nin ortasına dokunulsa ilk ona mı gider? <paramref name="ignore"/> altı (kendi karartması) sayılmaz.
    internal static bool IsTopmost(RectTransform t, Transform ignore, List<RaycastResult> hits, ref PointerEventData probe)
    {
        var es = EventSystem.current;
        if (es == null || t == null || !t.gameObject.activeInHierarchy) return false;
        probe ??= new PointerEventData(es);
        probe.position = RectTransformUtility.WorldToScreenPoint(CameraOf(t), t.TransformPoint(t.rect.center));
        hits.Clear();
        es.RaycastAll(probe, hits);
        foreach (var hit in hits)
        {
            if (ignore != null && hit.gameObject.transform.IsChildOf(ignore)) continue;
            return hit.gameObject.transform.IsChildOf(t);
        }
        return false;
    }

    private static Camera CameraOf(RectTransform t)
    {
        var canvas = t.GetComponentInParent<Canvas>();
        canvas = canvas != null ? canvas.rootCanvas : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    // ── Görsel ───────────────────────────────────────────────────────

    private void BuildOverlay()
    {
        gameObject.layer = 5;   // UI
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;   // menü UI üstü, ödül uçuşu (900) ve yükleme (999) altı
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
        dim.raycastTarget = true;   // delik dışı her dokunuşu yutar
        dim.UseRaycastHole = true;

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
        target = newTarget;
        lastHole = default;
        tapTime = 0f;
        caption.text = GameLocalization.Get(captionKey);
        // Oyun anı yazısı → DynaPuff (oyun içi öğreticiyle aynı: beyaz yüz, yumuşak siyah kontur + hafif gölge).
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

    // Delik hedefi izler (popup giriş animasyonu, ölçek); parmak delik kenarına dokunur gibi gidip gelir.
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
            dimRects.Add(Rect.MinMaxRect(full.xMin, full.yMin, full.xMax, hole.yMin));   // alt
            dimRects.Add(Rect.MinMaxRect(full.xMin, hole.yMax, full.xMax, full.yMax));   // üst
            dimRects.Add(Rect.MinMaxRect(full.xMin, hole.yMin, hole.xMin, hole.yMax));   // sol
            dimRects.Add(Rect.MinMaxRect(hole.xMax, hole.yMin, full.xMax, hole.yMax));   // sağ
            dim.SetRects(dimRects);
        }

        // Hedef ekranın üst yarısındaysa parmak altta, yukarıyı gösterir; değilse üstte, aşağıyı.
        bool pointUp = hole.center.y > 0f;
        float away = pointUp ? -1f : 1f;   // parmağın hedeften uzaklaştığı yön (y)
        float edge = pointUp ? hole.yMin : hole.yMax;
        float tap = Mathf.Sin(Mathf.Repeat(tapTime / TapPeriod, 1f) * Mathf.PI);   // 0→1→0: dokun
        hand.localEulerAngles = new Vector3(0f, 0f, pointUp ? 180f : 0f);
        hand.anchoredPosition = new Vector2(hole.center.x, edge + away * TapTravel * (1f - tap));

        var capRt = caption.rectTransform;
        float capY = edge + away * (HandSize.y + TapTravel + capRt.sizeDelta.y * 0.5f + 10f);
        capRt.anchoredPosition = new Vector2(0f, capY);
    }

    private Rect HoleOf(RectTransform t)
    {
        t.GetWorldCorners(corners);
        Camera cam = CameraOf(t);
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
