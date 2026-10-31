using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Can isteği kartı (sohbet akışında): üst bantta isim + "Can İstiyor!", altta büyük kalp, "Alınan Can:" + ilerleme
/// çubuğu (alınan/5 — bir istek en fazla 5 can toplar) ve yeşil Yardım butonu. Çubuk Progress Event barının
/// zemin + yeşil dolgu görselleri (9-slice). Yardım'a basınca controller'a haber verir; karşılanınca satır kaldırılır.
/// Düzen prefab'ın layout gruplarından değil koddan kurulur (EnsureCardLayout, bir kez).
/// </summary>
public sealed class TeamLifeRequestRow : MonoBehaviour
{
    [SerializeField] private Image panel;
    [SerializeField] private Image avatar;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text tagText;        // "Can İstiyor!"
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressText;   // "2/5"
    [SerializeField] private Button helpButton;
    [SerializeField] private TMP_Text helpButtonText;
    [Tooltip("Çubuk zemini — Progress Event barı (BarTrackV2, 9-slice).")]
    [SerializeField] private Sprite barTrackSprite;
    [Tooltip("Çubuk dolgusu — Progress Event yeşili (ProgressGreenV2, 9-slice).")]
    [SerializeField] private Sprite barFillSprite;

    // Referans orana göre (RM): kart biraz daha yüksek; içerik ölçüleri aynı oranda büyüdü.
    private const float CardHeight = 264f;
    private const float HeaderHeight = 74f;
    private static readonly Vector2 BarSize = new Vector2(320f, 64f);
    private const float BarInset = 6f;   // zeminin krem kenarı + iç gölgesi; dolgu bunun içinde
    private static readonly Color HeaderTint = new Color(0.90f, 0.79f, 0.60f, 1f);
    private static readonly Color NameColor = new Color(0.55f, 0.24f, 0.12f, 1f);
    private static readonly Color LabelColor = new Color(0.55f, 0.36f, 0.22f, 1f);
    private static readonly Color BarBack = new Color(0.36f, 0.31f, 0.25f, 0.65f);   // sprite yoksa

    private TeamLifeRequest request;
    private Action<TeamLifeRequest> onHelp;
    private bool laidOut;
    private TMP_Text progressLabel;   // "Alınan Can:"

    /// helped: bu isteğe zaten can gönderdim (buton "Gönderildi", kapalı). mine: kendi isteğim (buton yok).
    public void Bind(TeamLifeRequest r, UITheme theme, Action<TeamLifeRequest> helpHandler,
        bool helped = false, bool mine = false)
    {
        request = r;
        onHelp = helpHandler;
        if (r == null) return;
        EnsureCardLayout();

        if (nameText != null) nameText.text = r.requesterName;
        SingleLineText.Fit(nameText);
        if (tagText != null)  tagText.text  = GameLocalization.Get(mine ? "team_life_request_mine" : "team_life_request_tag");
        if (progressLabel != null) progressLabel.text = GameLocalization.Get("team_life_progress_label");
        // Bir istek en fazla 5 can toplar: pay her zaman 5 (bot/kendi/gerçek istek aynı).
        int max = TeamLifeInbox.DonationLimit;
        int got = Mathf.Clamp(r.current, 0, max);
        if (progressText != null) progressText.text = got + "/" + max;
        SetFill((float)got / max);
        if (helpButtonText != null) helpButtonText.text = GameLocalization.Get(helped ? "team_life_sent" : "team_help");

        if (helpButton != null)
        {
            helpButton.gameObject.SetActive(!mine);
            helpButton.interactable = !helped;
            helpButton.onClick.RemoveAllListeners();
            helpButton.onClick.AddListener(() => onHelp?.Invoke(request));
        }

        if (theme != null)
        {
            UITheme.ApplySurface(panel, theme.cardBackground, theme.creamSurface);
            theme.ApplyText(nameText, NameColor, heading: true);
            theme.ApplyText(tagText, theme.lifeRed, heading: true);
            theme.ApplyText(progressLabel, LabelColor, heading: true);
            theme.ApplyText(progressText, Color.white, heading: true);
            if (barFillSprite == null) UITheme.ApplySurface(progressFill, theme.progressFill, theme.ctaGreen);
            theme.ApplyText(helpButtonText, Color.white, heading: true);
        }
        // Kontur font atandıktan SONRA (TMP font-değişimi tuzağı).
        TmpOutline.Apply(progressText, 0.22f, new Color(0.12f, 0.30f, 0.06f));
        UiButtons.StyleLabel(helpButtonText, UiButtons.Kind.GreenPlain);
    }

    // Referans düzen (RM): kartın üstünde koyu krem bant, solda kalp, ortada etiket + çubuk, sağda buton.
    private void EnsureCardLayout()
    {
        if (laidOut) return;
        laidOut = true;
        var root = (RectTransform)transform;

        // Prefab'ın dikey/yatay layout'u yerine sabit kart: elemanlar köke taşınır, eski kutular kapanır.
        if (TryGetComponent(out VerticalLayoutGroup vlg)) vlg.enabled = false;
        if (TryGetComponent(out LayoutElement le)) { le.minHeight = le.preferredHeight = CardHeight; }
        root.sizeDelta = new Vector2(root.sizeDelta.x, CardHeight);

        var header = NewChild("Header", typeof(Image));
        header.anchorMin = new Vector2(0f, 1f); header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.offsetMin = new Vector2(8f, -8f - HeaderHeight); header.offsetMax = new Vector2(-8f, -8f);
        var headerImg = header.GetComponent<Image>();
        if (panel != null) { headerImg.sprite = panel.sprite; headerImg.type = panel.type; }
        headerImg.color = HeaderTint;
        headerImg.raycastTarget = false;
        header.SetSiblingIndex(0);

        Place(nameText, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(32f, -8f - HeaderHeight * 0.5f), new Vector2(430f, 56f),
            TextAlignmentOptions.MidlineLeft);
        Place(tagText, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-32f, -8f - HeaderHeight * 0.5f), new Vector2(330f, 56f),
            TextAlignmentOptions.MidlineRight);

        float bodyMid = (CardHeight - HeaderHeight - 8f) * 0.5f;
        float heartSize = 132f * CardHeight / 240f;
        var heart = NewChild("Heart", typeof(Image));
        heart.anchorMin = heart.anchorMax = new Vector2(0f, 0f);
        heart.pivot = new Vector2(0.5f, 0.5f);
        heart.anchoredPosition = new Vector2(120f, bodyMid);
        heart.sizeDelta = new Vector2(heartSize, heartSize);
        var heartImg = heart.GetComponent<Image>();
        heartImg.sprite = UiIcons.Heart;
        heartImg.preserveAspect = true;
        heartImg.raycastTarget = false;
        heartImg.enabled = heartImg.sprite != null;

        if (progressFill != null && progressFill.transform.parent is RectTransform bar)
        {
            bar.SetParent(root, false);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = new Vector2(-30f, bodyMid - 26f);
            bar.sizeDelta = BarSize;
            if (bar.TryGetComponent(out LayoutElement barLe)) barLe.ignoreLayout = true;
            if (bar.TryGetComponent(out Image barImg))
            {
                if (barTrackSprite != null) ApplySliced(barImg, barTrackSprite);
                else barImg.color = BarBack;
            }

            // Dolgu: sol kenara yaslı, genişliği SetFill verir (Filled kırpma yerine — yuvarlak uç korunur).
            var fillRt = progressFill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = new Vector2(BarInset, BarInset);
            fillRt.offsetMax = new Vector2(BarInset, -BarInset);
            if (barFillSprite != null) ApplySliced(progressFill, barFillSprite);
            else progressFill.type = Image.Type.Sliced;
            progressFill.raycastTarget = false;
        }

        var labelRt = NewChild("ProgressLabel", typeof(TextMeshProUGUI));
        labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.anchoredPosition = new Vector2(-30f, bodyMid + 36f);
        labelRt.sizeDelta = new Vector2(BarSize.x, 40f);
        progressLabel = labelRt.GetComponent<TextMeshProUGUI>();
        if (tagText != null) progressLabel.font = tagText.font;
        progressLabel.fontSize = 30f;
        progressLabel.alignment = TextAlignmentOptions.Center;
        progressLabel.textWrappingMode = TextWrappingModes.NoWrap;
        progressLabel.raycastTarget = false;

        if (helpButton != null)
        {
            var btn = (RectTransform)helpButton.transform;
            btn.SetParent(root, false);
            btn.anchorMin = btn.anchorMax = new Vector2(1f, 0f);
            btn.pivot = new Vector2(1f, 0.5f);
            btn.anchoredPosition = new Vector2(-28f, bodyMid);
            btn.sizeDelta = new Vector2(240f, 100f);
            if (btn.TryGetComponent(out LayoutElement btnLe)) btnLe.ignoreLayout = true;
            UiButtons.Apply(helpButton, UiButtons.Kind.GreenPlain, styleLabel: false);
            if (helpButtonText != null)
            {
                helpButtonText.enableAutoSizing = true;
                helpButtonText.fontSizeMax = 40f;
                helpButtonText.fontSizeMin = 24f;
                helpButtonText.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        // Eski satır kutuları (avatar + isim / çubuk + buton) artık boş: kapat.
        if (avatar != null)
        {
            var top = avatar.transform;
            while (top.parent != null && top.parent != transform) top = top.parent;
            if (top != transform) top.gameObject.SetActive(false);
        }
        foreach (Transform child in transform)
            if (child.TryGetComponent(out HorizontalLayoutGroup h) && child.childCount == 0)
                child.gameObject.SetActive(false);
    }

    // 0..1 → dolgu genişliği. Boşken gizli; doluyken en az kendi yüksekliği kadar (yuvarlak uçlar ezilmesin).
    private void SetFill(float t)
    {
        if (progressFill == null) return;
        var fillRt = progressFill.rectTransform;
        float innerW = BarSize.x - BarInset * 2f;
        float innerH = BarSize.y - BarInset * 2f;
        progressFill.enabled = t > 0f;
        float w = t > 0f ? Mathf.Max(innerH, innerW * Mathf.Clamp01(t)) : 0f;
        fillRt.offsetMax = new Vector2(BarInset + w, fillRt.offsetMax.y);
    }

    private static void ApplySliced(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.preserveAspect = false;
        // Kenarlar yüksekliğe oranlı kalsın (buton ailesiyle aynı ölçekleyici).
        if (!image.TryGetComponent(out SlicedHeightScaler scaler)) scaler = image.gameObject.AddComponent<SlicedHeightScaler>();
        scaler.Refresh();
    }

    private void Place(TMP_Text text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, TextAlignmentOptions align)
    {
        if (text == null) return;
        var rt = text.rectTransform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        if (rt.TryGetComponent(out LayoutElement le)) le.ignoreLayout = true;
        text.alignment = align;
        text.enableAutoSizing = true;
        text.fontSizeMax = 40f;
        text.fontSizeMin = 24f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private RectTransform NewChild(string childName, Type component)
    {
        var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), component);
        go.layer = gameObject.layer;   // runtime UI: kamera görsün
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        return rt;
    }
}
