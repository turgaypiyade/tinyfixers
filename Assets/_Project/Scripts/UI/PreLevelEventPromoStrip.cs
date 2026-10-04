using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level öncesi popup'ın altındaki event şeridi: etkin olup oyuncunun katılmadığı event'ler
/// (<see cref="PreLevelEventPromoRegistry"/>) ekranın altında, kırmızı alt alanın üstünde gösterilir.
/// Her sayfa: kulpçuk (event adı) + ikon + geri sayım + kısa çağrı + "Katıl". Birden fazla event varsa
/// <see cref="HorizontalPager"/> ile kendiliğinden döner, parmakla kaydırılır.
///
/// Şerit event tanımaz; kart verisi sağlayıcılardan gelir. Görseller (alt alan, kulpçuk, yeşil buton)
/// çağırandan verilir — kodla çizim yok.
/// </summary>
public sealed class PreLevelEventPromoStrip : MonoBehaviour
{
    [Serializable]
    public struct Visuals
    {
        public Sprite background;        // alt alan (BottomareaBGV3)
        public Sprite tab;               // kulpçuk (9-slice, ActiveTab)
        public Sprite joinButton;        // yeşil buton (popup'ın Oyna butonu görseli)
        public TMP_Text joinTextStyle;   // "Katıl" yazısı bu TMP'nin font/materyal/rengini alır (Oyna yazısı)
        public Sprite dot;               // sayfa noktası (opsiyonel)
        public float height;             // alt alan yüksekliği
        public float autoAdvanceSeconds;
    }

    private const float TabHeight = 84f;
    private const float TabOverlap = 14f;       // kulpçuğun alt alana gömülen kısmı (köşeler alanın arkasında)
    private const float TabMinWidth = 340f;
    private const float TabPadding = 90f;
    private const float EnterDuration = 0.3f;

    private Visuals visuals;
    private Action<PreLevelEventPromo> onJoin;
    private HorizontalPager pager;
    private readonly List<(PreLevelEventPromo promo, TMP_Text timer)> timers = new();
    private int lastShownSecond = -1;

    public static PreLevelEventPromoStrip Create(RectTransform parent, Visuals visuals, Action<PreLevelEventPromo> onJoin)
    {
        var go = new GameObject("EventPromoStrip", typeof(RectTransform));
        go.layer = parent.gameObject.layer;   // Screen Space Camera culling guard
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, visuals.height + TabHeight - TabOverlap);

        var strip = go.AddComponent<PreLevelEventPromoStrip>();
        strip.visuals = visuals;
        strip.onJoin = onJoin;

        strip.pager = HorizontalPager.Create(rt, "Pager");
        var pagerRt = (RectTransform)strip.pager.transform;
        pagerRt.anchorMin = Vector2.zero;
        pagerRt.anchorMax = Vector2.one;
        pagerRt.offsetMin = pagerRt.offsetMax = Vector2.zero;
        strip.pager.AutoAdvanceSeconds = visuals.autoAdvanceSeconds;
        strip.pager.DotSprite = visuals.dot;
        strip.pager.DotSize = 16f;
        strip.pager.DotsBottomOffset = 14f;
        return strip;
    }

    /// <summary>Kartları kurar; boşsa şerit gizlenir. Görünürse alttan kayarak girer.</summary>
    public void Show(IReadOnlyList<PreLevelEventPromo> promos)
    {
        pager.Clear();
        timers.Clear();
        lastShownSecond = -1;

        bool any = promos != null && promos.Count > 0;
        gameObject.SetActive(any);
        if (!any) return;

        Canvas.ForceUpdateCanvases();
        float width = ((RectTransform)transform).rect.width;
        pager.PageSpacing = Mathf.Max(1f, width);   // sayfa = tam ekran genişliği (tüm şerit kayar)

        foreach (var promo in promos)
            BuildPage(pager.AddPage(promo.Title), promo, width);

        pager.Finish();
        RefreshTimers(force: true);
        StopAllCoroutines();
        StartCoroutine(SlideIn());
    }

    private void Update() => RefreshTimers(force: false);

    // ── Sayfa ──────────────────────────────────────────────────────────────

    private void BuildPage(RectTransform page, PreLevelEventPromo promo, float width)
    {
        float h = visuals.height;

        // Kulpçuk ÖNCE (arkada): alt köşeleri alt alanın arkasında kalır.
        var tab = NewImage("Tab", page, visuals.tab);
        tab.type = visuals.tab != null && visuals.tab.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        var tabRt = tab.rectTransform;
        tabRt.anchorMin = tabRt.anchorMax = new Vector2(0.5f, 0f);
        tabRt.pivot = new Vector2(0.5f, 0f);
        tabRt.anchoredPosition = new Vector2(0f, h - TabOverlap);

        var title = NewText("Title", tabRt, promo.Title, 44f);
        Stretch(title.rectTransform, new Vector2(0f, TabOverlap * 0.5f), Vector2.zero);
        float tabWidth = Mathf.Max(TabMinWidth, title.GetPreferredValues(promo.Title).x + TabPadding);
        tabRt.sizeDelta = new Vector2(Mathf.Min(tabWidth, width * 0.8f), TabHeight);

        // Alt alan.
        var bg = NewImage("Background", page, visuals.background);
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = new Vector2(0f, 0f);
        bgRt.anchorMax = new Vector2(1f, 0f);
        bgRt.pivot = new Vector2(0.5f, 0f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(0f, h);

        // Sol: ikon + altında geri sayım.
        var icon = NewImage("Icon", bgRt, promo.Icon);
        icon.preserveAspect = true;
        icon.enabled = promo.Icon != null;
        var iconRt = icon.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.2f, 0.58f);
        iconRt.sizeDelta = Vector2.one * h * 0.6f;

        var timer = NewText("Timer", bgRt, "", 34f);
        var timerRt = timer.rectTransform;
        timerRt.anchorMin = timerRt.anchorMax = new Vector2(0.2f, 0.2f);
        timerRt.sizeDelta = new Vector2(width * 0.34f, 50f);
        timers.Add((promo, timer));

        // Sağ: çağrı + Katıl.
        var tagline = NewText("Tagline", bgRt, promo.Tagline, 42f);
        var taglineRt = tagline.rectTransform;
        taglineRt.anchorMin = taglineRt.anchorMax = new Vector2(0.64f, 0.7f);
        taglineRt.sizeDelta = new Vector2(width * 0.6f, 64f);

        BuildJoinButton(bgRt, promo, h);
    }

    private void BuildJoinButton(RectTransform parent, PreLevelEventPromo promo, float h)
    {
        var image = NewImage("JoinButton", parent, visuals.joinButton);
        image.raycastTarget = true;
        image.preserveAspect = true;
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.64f, 0.34f);
        float buttonHeight = h * 0.4f;
        float aspect = visuals.joinButton != null && visuals.joinButton.rect.height > 0f
            ? visuals.joinButton.rect.width / visuals.joinButton.rect.height
            : 2.6f;
        rt.sizeDelta = new Vector2(buttonHeight * aspect, buttonHeight);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onJoin?.Invoke(promo));

        var label = NewText("Label", rt, GameLocalizationText("event_join_button", "Katıl"), buttonHeight * 0.5f);
        Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
        var style = visuals.joinTextStyle;
        if (style != null)
        {
            // Oyna butonunun yazı stilini birebir al (font + outline materyali + renk).
            label.font = style.font;
            label.fontSharedMaterial = style.fontSharedMaterial;
            label.color = style.color;
            label.fontStyle = style.fontStyle;
            label.enableVertexGradient = style.enableVertexGradient;
            label.colorGradient = style.colorGradient;
        }
    }

    // ── Geri sayım ─────────────────────────────────────────────────────────

    private void RefreshTimers(bool force)
    {
        if (timers.Count == 0) return;
        DateTime now = DateTime.UtcNow;
        int second = (int)(now.Ticks / TimeSpan.TicksPerSecond);
        if (!force && second == lastShownSecond) return;
        lastShownSecond = second;

        foreach (var (promo, text) in timers)
        {
            if (text == null) continue;
            TimeSpan remaining = promo.WindowEndUtc == DateTime.MinValue ? TimeSpan.Zero : promo.WindowEndUtc - now;
            text.gameObject.SetActive(remaining > TimeSpan.Zero);
            if (remaining > TimeSpan.Zero) text.text = TimeFormat.Countdown(remaining);
        }
    }

    private IEnumerator SlideIn()
    {
        var rt = (RectTransform)transform;
        float from = -rt.rect.height;
        float t = 0f;
        while (t < EnterDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / EnterDuration);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            rt.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(from, 0f, e));
            yield return null;
        }
        rt.anchoredPosition = Vector2.zero;
    }

    // ── Yardımcılar ────────────────────────────────────────────────────────

    private static string GameLocalizationText(string key, string fallback)
    {
        string value = GameLocalization.Get(key);
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    private Image NewImage(string name, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text NewText(string name, RectTransform parent, string value, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var text = go.AddComponent<TextMeshProUGUI>();
        RewardTextStyle.Apply(text, fontSize);   // Safari altın outline stili (projede ortak)
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
