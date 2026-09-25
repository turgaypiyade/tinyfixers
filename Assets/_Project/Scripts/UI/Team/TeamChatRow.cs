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

        var rowLayout = GetComponent<LayoutElement>();
        if (rowLayout != null) rowLayout.minHeight = rowLayout.preferredHeight = 170f;
    }

    public void RefreshLifeReply()
    {
        if (acceptLifeButton == null) return;
        if (botRequest != null)
        {
            acceptLifeButton.interactable = !botRequest.helped;
            acceptLifeLabel.text = botRequest.helped ? "Gönderildi" : "Yardım Et";
            return;
        }
        if (lifeReply == null) return;
        bool full = LivesManager.Current >= LivesManager.MaxLives;
        acceptLifeButton.interactable = !lifeReply.accepted && !full;
        acceptLifeLabel.text = lifeReply.accepted ? "Alındı" : full ? "Can Dolu" : "Kabul Et";
    }

    public void Bind(TeamChatMessage m, UITheme theme)
    {
        if (m == null) return;

        if (senderText != null)  senderText.text  = m.senderName;
        SingleLineText.Fit(senderText);
        if (messageText != null) messageText.text = m.text;
        if (timeText != null)    timeText.text    = m.timeLabel;
        if (avatar != null)
        {
            avatar.sprite  = m.avatar;
            avatar.enabled = m.avatar != null;
        }

        // Sol (gelen) / sağ (benim): dizilimi ters çevir + hizala.
        if (layout != null)
        {
            layout.reverseArrangement = m.isMine;
            layout.childAlignment = m.isMine ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
        }

        if (theme == null) return;
        // Baloncuk rengi: benimki farklı (mavi tonu), gelen krem.
        UITheme.ApplySurface(bubble, theme.cardBackground, m.isMine ? theme.infoBlue : theme.creamSurface);
        theme.ApplyText(senderText, m.isMine ? theme.textLight : theme.headerBand, heading: true);
        theme.ApplyText(messageText, m.isMine ? theme.textLight : theme.textOnCream);
        theme.ApplyText(timeText, m.isMine ? new Color(1f, 1f, 1f, 0.7f) : theme.textSub);
    }
}
