using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takım sohbeti satırı: avatar + baloncuk (gönderen + mesaj + zaman).
/// Gelen mesaj SOLDA (avatar solda), benim mesajım SAĞDA (avatarım sağda).
/// </summary>
public sealed class TeamChatRow : MonoBehaviour
{
    [Tooltip("Kök yatay dizilim — sol/sağ hizalama için reverse edilir.")]
    [SerializeField] private HorizontalLayoutGroup layout;
    [SerializeField] private Image bubble;
    [SerializeField] private Image avatar;
    [SerializeField] private TMP_Text senderText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Sprite acceptLifeGreenSprite;

    private TeamLifeInbox.Reply lifeReply;
    private TeamLifeInbox.BotRequest botRequest;
    private Button acceptLifeButton;
    private TMP_Text acceptLifeLabel;

    public void BindLifeReply(TeamLifeInbox.Reply reply, System.Action accept)
    {
        lifeReply = reply;
        BuildInlineAction(accept);
        RefreshLifeReply();
    }

    public void BindBotLifeRequest(TeamLifeInbox.BotRequest request, System.Action help)
    {
        botRequest = request;
        BuildInlineAction(help);
        RefreshLifeReply();
    }

    private void BuildInlineAction(System.Action onClick)
    {
        if (bubble == null || messageText == null) return;

        // Keep the message and its action together on the same line inside the bubble.
        const float buttonWidth = 240f;
        float aspect = acceptLifeGreenSprite != null
            ? acceptLifeGreenSprite.rect.width / Mathf.Max(1f, acceptLifeGreenSprite.rect.height) : 770f / 172f;
        float buttonHeight = buttonWidth / aspect;
        var slot = new GameObject("LifeMessageAction", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        slot.layer = gameObject.layer;
        slot.transform.SetParent(bubble.transform, false);
        var slotLayout = slot.GetComponent<LayoutElement>();
        slotLayout.minHeight = slotLayout.preferredHeight = buttonHeight;
        var inlineLayout = slot.GetComponent<HorizontalLayoutGroup>();
        inlineLayout.childAlignment = TextAnchor.MiddleLeft;
        inlineLayout.spacing = 12f;
        inlineLayout.childControlWidth = inlineLayout.childControlHeight = true;
        inlineLayout.childForceExpandWidth = inlineLayout.childForceExpandHeight = false;

        messageText.transform.SetParent(slot.transform, false);
        var textLayout = messageText.GetComponent<LayoutElement>() ?? messageText.gameObject.AddComponent<LayoutElement>();
        var bubbleLayout = bubble.GetComponent<LayoutElement>();
        var bubbleGroup = bubble.GetComponent<VerticalLayoutGroup>();
        float bubbleWidth = bubbleLayout != null && bubbleLayout.preferredWidth > 0f ? bubbleLayout.preferredWidth : 660f;
        float padding = bubbleGroup != null ? bubbleGroup.padding.horizontal : 24f;
        textLayout.minWidth = 0f;
        textLayout.preferredWidth = Mathf.Min(messageText.GetPreferredValues().x, bubbleWidth - padding - buttonWidth - inlineLayout.spacing);
        textLayout.flexibleWidth = 0f;
        textLayout.minHeight = textLayout.preferredHeight = buttonHeight;

        var action = new GameObject("LifeAction", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        action.layer = gameObject.layer;
        action.transform.SetParent(slot.transform, false);
        var rect = (RectTransform)action.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        var buttonLayout = action.GetComponent<LayoutElement>();
        buttonLayout.minWidth = buttonLayout.preferredWidth = buttonWidth;
        buttonLayout.minHeight = buttonLayout.preferredHeight = buttonHeight;
        var image = action.GetComponent<Image>();
        image.sprite = acceptLifeGreenSprite;
        image.color = Color.white;
        image.preserveAspect = true;
        acceptLifeButton = action.GetComponent<Button>();
        acceptLifeButton.targetGraphic = image;
        acceptLifeButton.onClick.AddListener(() => onClick?.Invoke());

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = gameObject.layer;
        label.transform.SetParent(action.transform, false);
        acceptLifeLabel = label.GetComponent<TextMeshProUGUI>();
        acceptLifeLabel.font = messageText != null ? messageText.font : TMP_Settings.defaultFontAsset;
        acceptLifeLabel.fontSize = 26f;
        acceptLifeLabel.alignment = TextAlignmentOptions.Center;
        acceptLifeLabel.color = Color.white;
        acceptLifeLabel.raycastTarget = false;
        var labelRect = acceptLifeLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        // Saat balonun en altında kalsın (aksiyon satırı ondan sonra eklendi).
        if (timeText != null && timeText.transform.parent == bubble.transform) timeText.transform.SetAsLastSibling();
        var rowLayout = GetComponent<LayoutElement>();
        if (rowLayout != null) rowLayout.minHeight = 170f;
    }

    public void RefreshLifeReply()
    {
        if (acceptLifeButton == null) return;
        if (botRequest != null)
        {
            acceptLifeButton.interactable = !botRequest.helped;
            acceptLifeLabel.text = GameLocalization.Get(botRequest.helped ? "team_life_sent" : "team_help_out");
            return;
        }
        if (lifeReply == null) return;
        bool full = LivesManager.Current >= LivesManager.MaxLives;
        acceptLifeButton.interactable = !lifeReply.accepted && !full;
        acceptLifeLabel.text = GameLocalization.Get(lifeReply.accepted ? "team_life_received" : full ? "team_lives_full_short" : "team_accept");
    }

    // ── RM sohbet düzeni ────────────────────────────────────────────────
    //   [avatar] ◄[ İsim (renkli)                       ]
    //             [ mesaj, gerektiği kadar satır          ]
    //             [                               3g 9s ]
    // Balon geniş ve beyaz (benimki açık yeşil), avatar balonun altına hizalı, saat sağ altta,
    // satır yüksekliği mesaja göre büyür (sabit 150 değil → uzun mesaj taşmaz).
    private const float BubbleWidth = 720f;
    private const float TailSize = 30f;
    private static readonly Color IncomingFill = Color.white;
    private static readonly Color MineFill = new Color(0.86f, 0.96f, 0.80f, 1f);
    private static readonly Color MessageColor = new Color(0.20f, 0.17f, 0.36f, 1f);   // koyu mor-lacivert
    private static readonly Color TimeColor = new Color(0.55f, 0.52f, 0.68f, 1f);
    // Gönderen adı renkleri (isimden deterministik — aynı kişi hep aynı renk).
    private static readonly Color[] SenderColors =
    {
        new Color(0.55f, 0.27f, 0.85f), new Color(0.13f, 0.47f, 0.86f), new Color(0.90f, 0.42f, 0.10f),
        new Color(0.16f, 0.62f, 0.30f), new Color(0.86f, 0.22f, 0.48f), new Color(0.10f, 0.60f, 0.66f),
    };

    private bool styled;
    private Image tail;

    private void EnsureStyle()
    {
        if (styled) return;
        styled = true;

        // Satır: içeriğe göre yükseklik, avatar alta hizalı.
        if (TryGetComponent(out LayoutElement rowLe)) { rowLe.preferredHeight = -1f; rowLe.minHeight = 120f; }
        if (layout != null)
        {
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.spacing = 14f;
            layout.padding = new RectOffset(16, 16, 10, 10);
        }

        if (bubble != null)
        {
            if (bubble.TryGetComponent(out LayoutElement ble)) ble.preferredWidth = BubbleWidth;
            if (bubble.TryGetComponent(out VerticalLayoutGroup vg))
            {
                vg.padding = new RectOffset(28, 24, 16, 12);
                vg.spacing = 4f;
                vg.childForceExpandHeight = false;
            }
            // Konuşma kuyruğu: balon rengiyle aynı, avatara bakan kenarda döndürülmüş kare.
            var go = new GameObject("Tail", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(bubble.transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            tail = go.AddComponent<Image>();
            tail.raycastTarget = false;
            var rt = tail.rectTransform;
            rt.sizeDelta = new Vector2(TailSize, TailSize);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        if (messageText != null)
        {
            messageText.textWrappingMode = TextWrappingModes.Normal;
            messageText.fontSize = 30f;
            messageText.enableAutoSizing = false;
            messageText.alignment = TextAlignmentOptions.TopLeft;
            if (messageText.TryGetComponent(out LayoutElement mle)) { mle.preferredHeight = -1f; mle.minHeight = 38f; }
        }

        if (senderText != null)
        {
            senderText.fontSize = 32f;
            senderText.alignment = TextAlignmentOptions.MidlineLeft;
            if (senderText.TryGetComponent(out LayoutElement sle)) sle.flexibleWidth = 1f;
            if (senderText.transform.parent != null && senderText.transform.parent.TryGetComponent(out LayoutElement top))
                top.preferredHeight = 40f;
        }

        // Saat isim satırından çıkar → balonun en altında, sağa yaslı.
        if (timeText != null && bubble != null)
        {
            timeText.transform.SetParent(bubble.transform, false);
            timeText.transform.SetAsLastSibling();
            timeText.fontSize = 22f;
            timeText.alignment = TextAlignmentOptions.MidlineRight;
            if (timeText.TryGetComponent(out LayoutElement tle)) { tle.preferredWidth = -1f; tle.preferredHeight = 26f; tle.flexibleWidth = 1f; }
        }
        if (tail != null) tail.transform.SetAsFirstSibling();   // balon içeriklerinin arkasında
    }

    public void Bind(TeamChatMessage m, UITheme theme)
    {
        if (m == null) return;
        EnsureStyle();

        if (senderText != null)  senderText.text  = m.senderName;
        SingleLineText.Fit(senderText);
        if (messageText != null) messageText.text = m.text;
        if (timeText != null)    timeText.text    = m.timeLabel;
        if (avatar != null)
        {
            avatar.sprite  = m.avatar;
            avatar.enabled = m.avatar != null;
            avatar.preserveAspect = true;
        }

        // Sol (gelen) / sağ (benim): dizilimi ters çevir + avatar balonun altına hizalı.
        if (layout != null)
        {
            layout.reverseArrangement = m.isMine;
            layout.childAlignment = m.isMine ? TextAnchor.LowerRight : TextAnchor.LowerLeft;
        }

        Color fill = m.isMine ? MineFill : IncomingFill;
        if (bubble != null)
        {
            if (theme != null && theme.cardBackground != null)
            {
                bubble.sprite = theme.cardBackground;
                bubble.type = Image.Type.Sliced;
            }
            bubble.color = fill;
        }
        if (tail != null)
        {
            tail.color = fill;
            var rt = tail.rectTransform;
            float x = m.isMine ? 1f : 0f;   // avatara bakan kenar
            rt.anchorMin = rt.anchorMax = new Vector2(x, 0f);
            rt.anchoredPosition = new Vector2(m.isMine ? -2f : 2f, 46f);
        }

        if (senderText != null) senderText.color = SenderColor(m.senderName);
        if (messageText != null) messageText.color = MessageColor;
        if (timeText != null) timeText.color = TimeColor;
        if (theme != null)
        {
            if (theme.headingFont != null && senderText != null) senderText.font = theme.headingFont;
            if (theme.bodyFont != null && messageText != null) messageText.font = theme.bodyFont;
        }
    }

    private static Color SenderColor(string name)
    {
        int h = 0;
        if (!string.IsNullOrEmpty(name)) foreach (char c in name) h = h * 31 + c;
        return SenderColors[((h % SenderColors.Length) + SenderColors.Length) % SenderColors.Length];
    }
}
