using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Cached visual-preview invitation. Reuses MissionPopup, its existing palette and hero animation.</summary>
public sealed class GearCupJoinPopup : MonoBehaviour
{
    private GearCupPreviewConfig config;
    private GearCupPreviewScreen owner;
    private RectTransform safeArea;
    private RectTransform panel;
    private TMP_Text title;
    private TMP_Text message;
    private TMP_Text timer;
    private TMP_Text joinLabel;
    private TMP_Text laterLabel;
    private Button joinButton;
    private Button laterButton;
    private Button closeButton;
    private Image dim;
    private bool layoutPending;
    private bool closing;
    private float nextRefresh;
    private Func<string> customTitle, customMessage;
    private Func<TimeSpan> customRemaining;
    private Action<RectTransform> customHero;
    private Action customJoin;
    private Sprite customBackground;
    private TMP_FontAsset customFont;

    // Built once per preview screen and reused after cancel/close. No live event is joined here.
    public static GearCupJoinPopup Create(Transform parent, GearCupPreviewConfig config)
    {
        var popup = CreateRoot(parent, config, "GearCupJoinPopup");
        popup.Build();
        return popup;
    }

    // Reuse the exact Gear Cup shell, entrance, actions and layout for another event's invitation.
    public static GearCupJoinPopup CreateCustom(Transform parent, string name, GearCupPreviewConfig style,
        Sprite background, TMP_FontAsset font, Func<string> title, Func<string> message,
        Func<TimeSpan> remaining, Action<RectTransform> hero, Action join)
    {
        var popup = CreateRoot(parent, style, name);
        popup.customBackground = background;
        popup.customFont = font;
        popup.customTitle = title;
        popup.customMessage = message;
        popup.customRemaining = remaining;
        popup.customHero = hero;
        popup.customJoin = join;
        popup.Build();
        return popup;
    }

    private static GearCupJoinPopup CreateRoot(Transform parent, GearCupPreviewConfig config, string name)
    {
        var rt = CommonPopupView.NewRect(parent, name, Vector2.zero);
        rt.gameObject.SetActive(false);
        rt.gameObject.tag = "Untagged";
        CommonPopupView.Region(rt, new Rect(0, 0, 1, 1));
        var canvas = rt.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 260;
        rt.gameObject.AddComponent<GraphicRaycaster>();
        var popup = rt.gameObject.AddComponent<GearCupJoinPopup>();
        popup.config = config;
        return popup;
    }

    public void Show(GearCupPreviewScreen target)
    {
        if (gameObject.activeSelf) return;
        owner = target;
        closing = false;
        joinButton.interactable = laterButton.interactable = closeButton.interactable = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Fit();
        layoutPending = false;
        Refresh();
        nextRefresh = Time.unscaledTime + 1f;
        PopupEntranceAnimator.Enter(panel, dim);
    }

    private void OnEnable()
    {
        GameLocalization.OnLanguageChanged += Refresh;
        layoutPending = true;
    }

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= Refresh;
        owner = null;
        closing = false;
        layoutPending = false;
    }

    private void OnRectTransformDimensionsChange() => layoutPending = true;

    private void OnApplicationFocus(bool focused)
    {
        if (focused) { layoutPending = true; nextRefresh = 0f; }
    }

    private void Update()
    {
        if (layoutPending) { layoutPending = false; Fit(); }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1f;
        RefreshTimer();
    }

    private void Fit()
    {
        if (safeArea == null || panel == null || Screen.width <= 0 || Screen.height <= 0) return;
        var safe = Screen.safeArea;
        safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        var viewport = safeArea.rect.size;
        // Çerçeve güvenli alanın iki yanına tam dayanır (PreLevel gibi); içerik panel ölçeğiyle aynı oranda büyür.
        // Kısa/geniş ekranda yüksekliğe sığacak kadar küçülür.
        float scale = Mathf.Min(viewport.x / panel.sizeDelta.x, (viewport.y - 48f) / panel.sizeDelta.y);
        panel.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }

    private void Refresh()
    {
        if (title == null) return;
        title.text = customTitle != null ? customTitle() : GameLocalization.GetOr("gearcup_title", "GEAR CUP");
        message.text = customMessage != null ? customMessage() : GameLocalization.GetOr("gearcup_join_message",
            "Collect gears with your team! The top three teams win rewards.");
        joinLabel.text = GameLocalization.GetOr("event_join_button", "Join");
        laterLabel.text = GameLocalization.GetOr("gearcup_join_later", "Not now");
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        if (timer != null && (customRemaining != null || owner != null))
        {
            TimeSpan remaining = customRemaining != null ? customRemaining() : owner.PreviewTimeRemaining;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            timer.text = GameLocalization.GetOr("gearcup_ends_in", "ENDS IN") + "  " +
                TimeFormat.Countdown(remaining);
            if (customRemaining != null) joinButton.interactable = !closing && remaining > TimeSpan.Zero;
        }
    }

    private bool BeginClose()
    {
        if (closing || !gameObject.activeInHierarchy) return false;
        closing = true;
        joinButton.interactable = laterButton.interactable = closeButton.interactable = false;
        return true;
    }

    private void Join()
    {
        if (!BeginClose()) return;
        PopupEntranceAnimator.Exit(panel, CompleteJoin);
    }

    private void CompleteJoin()
    {
        var target = owner;
        var accepted = customJoin;
        // Exit restores the panel's home position before calling us. Mark only a completed
        // acceptance, so an interrupted popup cannot leave a launch point for another event.
        if (target != null || accepted != null) EventScreenIris.MarkLaunchPoint(joinButton);
        gameObject.SetActive(false);
        if (accepted != null) accepted();
        else if (target != null) target.AcceptPreviewJoin();
    }

    private void Later()
    {
        if (!BeginClose()) return;
        PopupEntranceAnimator.Exit(panel, Hide);
    }

    private void Hide() => gameObject.SetActive(false);

    private void Build()
    {
        var skin = CommonPopupSkin.Shared;
        dim = Picture("Dim", transform, null);
        CommonPopupView.Region(dim.rectTransform, new Rect(0, 0, 1, 1));
        dim.color = new Color(0.01f, 0.03f, 0.09f, 0.76f);
        dim.raycastTarget = true;
        safeArea = CommonPopupView.NewRect(transform, "SafeArea", Vector2.zero);
        CommonPopupView.Region(safeArea, new Rect(0, 0, 1, 1));
        float height = config.joinPopupBackground != null
            ? 900f * config.joinPopupBackground.rect.height / config.joinPopupBackground.rect.width : 1317f;
        panel = CommonPopupView.NewRect(safeArea, "MissionPopup", new Vector2(900, height));
        var shell = Picture("Frame", panel, config.joinPopupBackground);
        CommonPopupView.Region(shell.rectTransform, new Rect(0, 0, 1, 1));
        shell.material = config.joinPopupMaterial;
        shell.raycastTarget = true;

        title = Text(panel, "Title", 76, Color.white, customFont != null ? customFont : config.headingFont);
        CommonPopupView.Region(title.rectTransform, new Rect(0.21f, 0.875f, 0.58f, 0.10f));
        title.textWrappingMode = TextWrappingModes.NoWrap;
        // Yazı kuralı: kontur/gölge = başlığın oturduğu bandın koyusu. Bant, popup görselinin bu event için
        // yeniden boyanmış hâli (PopupColorRemap _TargetColor, mavi-mor) — rengi materyalden oku.
        var band = config.joinPopupMaterial != null && config.joinPopupMaterial.HasProperty("_TargetColor")
            ? config.joinPopupMaterial.GetColor("_TargetColor") : TextSurfaceStyle.Base(TextSurface.Blue);
        TextSurfaceStyle.Apply(title, band);
        DisplayUpperCase.Apply(title);
        var titleArc = title.gameObject.AddComponent<TextArcEffect>();
        titleArc.RotateLetters = false;
        titleArc.SetArcAngle(15f);
        // Reuse the event's exact arena sprite. The rounded mask clips only this static
        // backdrop; the animated hero keeps its separate canvas outside the mask subtree.
        var viewport = Picture("ArenaViewport", panel, config.rowBackground);
        CommonPopupView.Region(viewport.rectTransform, new Rect(0.16f, 0.21f, 0.68f, 0.58f));
        viewport.type = Image.Type.Sliced;
        viewport.preserveAspect = false;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Sprite arenaSprite = customBackground != null ? customBackground : config.background;
        var arena = Picture("ArenaBackground", viewport.transform, arenaSprite);
        CommonPopupView.Region(arena.rectTransform, new Rect(0, 0, 1, 1));
        arena.preserveAspect = false;
        if (arenaSprite != null)
        {
            var aspect = arena.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = arenaSprite.rect.width / arenaSprite.rect.height;
        }
        var body = CommonPopupView.NewRect(panel, "Content", Vector2.zero);
        CommonPopupView.Region(body, new Rect(0.17f, 0.235f, 0.66f, 0.54f));
        bool customInvitation = customHero != null;
        if (customInvitation)
        {
            // Keep every animation pose inside the arena, including the wide follow-through.
            body.gameObject.AddComponent<RectMask2D>();
            customHero(body);
        }
        else BuildHero(body);
        message = Text(body, "Message", 38, customInvitation ? new Color32(28, 44, 105, 255) : Color.white,
            customFont != null ? customFont : config.bodyFont);
        if (customInvitation)
            CommonPopupView.Region(message.rectTransform, new Rect(0.03f, 0.81f, 0.94f, 0.18f));
        else Place(message.rectTransform, 0, -225, 570, 150);
        timer = Text(body, "Countdown", 30, customInvitation ? new Color32(59, 40, 126, 255) : new Color32(200, 230, 255, 255),
            customFont != null ? customFont : config.bodyFont);
        if (customInvitation)
            CommonPopupView.Region(timer.rectTransform, new Rect(0.05f, 0.735f, 0.90f, 0.065f));
        else Place(timer.rectTransform, 0, -325, 570, 46);
        timer.textWrappingMode = TextWrappingModes.NoWrap;

        var actions = CommonPopupView.NewRect(panel, "Actions", Vector2.zero);
        CommonPopupView.Region(actions, new Rect(0.12f, 0.035f, 0.76f, 0.16f));
        laterButton = CommonPopupView.Button(actions, "BtnLater", "", Later,
            skin.secondaryButton != null ? skin.secondaryButton : skin.accountButton);
        CommonPopupView.Region((RectTransform)laterButton.transform, CommonPopupView.ActionRegion(0, 2));
        laterLabel = laterButton.GetComponentInChildren<TMP_Text>(true);
        joinButton = CommonPopupView.Button(actions, "BtnJoin", "", Join, skin.continueButton);
        CommonPopupView.Region((RectTransform)joinButton.transform, CommonPopupView.ActionRegion(1, 2));
        joinLabel = joinButton.GetComponentInChildren<TMP_Text>(true);
        // The custom MissionPopup layout does not use CommonPopupView's footer sizing.
        FitButtonLabel(laterLabel);
        FitButtonLabel(joinLabel);
        closeButton = CommonPopupView.Button(panel, "BtnClose", "", Later, config.closeIcon);
        CommonPopupView.Region((RectTransform)closeButton.transform, new Rect(0.85f, 0.89f, 0.10f, 0.07f));
        closeButton.GetComponentInChildren<TMP_Text>(true).text = "";
    }

    private void BuildHero(RectTransform body)
    {
        var hero = CommonPopupView.NewRect(body, "Hero", new Vector2(800, 540));
        hero.anchoredPosition = new Vector2(0, 80);
        hero.localScale = Vector3.one * 0.84f;
        hero.gameObject.AddComponent<Canvas>();
        var ring = Picture("PlatformRing", hero, config.platformRing);
        Place(ring.rectTransform, 0, -178, 680, 255);
        var back = Picture("GearBack", hero, config.gear);
        Place(back.rectTransform, 0, 45, 174, 174);
        var rabbit = Character("Rabbit", hero, -195, config.rabbitFrames, 1508f, out var rabbitBreath);
        var pig = Character("Pig", hero, 195, config.pigFrames, 1497f, out var pigBreath);
        var front = Picture("GearFront", hero, config.gear);
        Place(front.rectTransform, 0, 45, 174, 174);
        var animator = hero.gameObject.AddComponent<GearCupHeroAnimator>();
        animator.Configure(config, rabbit, rabbitBreath, back, front, ring, pig, pigBreath);
        animator.ConfigureOrbit(new Vector2(0, 45), new Vector2(24, 16));
    }

    private static Image Character(string name, RectTransform hero, float x, Sprite[] frames,
        float footY, out RectTransform breathing)
    {
        breathing = CommonPopupView.NewRect(hero, name + "Breathing", new Vector2(800f / 3f, 400));
        breathing.pivot = new Vector2(0.5f, 0);
        breathing.anchoredPosition = new Vector2(x, -160);
        var image = Picture(name, breathing, frames != null && frames.Length > 0 ? frames[0] : null);
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(800f / 3f, 400);
        rt.anchoredPosition = new Vector2(0, -(1536f - footY) / 1536f * 400f);
        return image;
    }

    private static Image Picture(string name, Transform parent, Sprite sprite)
    {
        var rect = CommonPopupView.NewRect(parent, name, Vector2.zero);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, string name, float size, Color color, TMP_FontAsset font)
    {
        var text = CommonPopupView.Text(parent, name, "", size, color);
        if (font != null) text.font = font;
        return text;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private static void FitButtonLabel(TMP_Text text)
    {
        text.fontSize = text.fontSizeMax = 44f;
        text.fontSizeMin = 24f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }
}
