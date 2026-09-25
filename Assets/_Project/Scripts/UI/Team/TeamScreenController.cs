using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takım ekranı (sade sohbet). Üst bilgi (amblem, isim, üye sayısı), altında sohbet akışı
/// (gelen mesaj solda, benimki sağda), en altta "Can İste" / "Mesaj". Mesaj'a basınca
/// bottom bar üstünde tek satırlık input açılır. Bot can yardımları akışta kabul edilir.
/// </summary>
public sealed class TeamScreenController : MonoBehaviour
{
    [Header("Tema")]
    [SerializeField] private UITheme theme;

    [Header("Üst bilgi")]
    [SerializeField] private Image emblemImage;
    [SerializeField] private TMP_Text teamNameText;
    [SerializeField] private TMP_Text memberCountText;   // "40/50"

    [Header("Akış")]
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private TeamChatRow chatRowPrefab;
    [SerializeField] private ScrollRect scrollRect;      // yeni mesajda alta kaydırmak için

    [Header("Alt butonlar")]
    [SerializeField] private Button requestLifeButton;
    [SerializeField] private Button messageButton;
    [Tooltip("Takımdan ayrıl butonu (opsiyonel). Boşsa header'a runtime bir buton kurulur.")]
    [SerializeField] private Button leaveButton;
    [Tooltip("Takımdan ayrıl aksiyonlarında kullanılan kare kırmızı buton görseli.")]
    [SerializeField] private Sprite leaveRedSquareButtonSprite;

    [Header("Mesaj input (bottom bar üstü)")]
    [SerializeField] private GameObject messageInputRoot;   // başta kapalı
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private Button messagePostButton;

    [Header("Görsel")]
    [Tooltip("Avatarı olmayan mesajlara isme göre deterministik dağıtılan mock avatar havuzu.")]
    [SerializeField] private Sprite[] avatarPool;
    [Tooltip("Takım amblemi yoksa kullanılacak varsayılan amblem.")]
    [SerializeField] private Sprite defaultEmblem;
    [Tooltip("Amblem havuzu — PlayerTeamState.EmblemIndex buradan sprite'a çevrilir.")]
    [SerializeField] private Sprite[] emblemPool;

    [Header("Takımsız durum (Ara/Oluştur)")]
    [Tooltip("Takım İÇİ görünümün kökü (header + sohbet + butonlar).")]
    [SerializeField] private GameObject inTeamRoot;
    [Tooltip("Takımsızken gösterilen Ara/Oluştur tarayıcısı.")]
    [SerializeField] private TeamBrowserController browser;

    private ITeamService service;
    private readonly List<GameObject> feed = new();
    private bool wired;
    private TMP_Text requestLifeLabel;
    private string defaultRequestLifeLabel;
    private readonly List<TeamChatRow> lifeReplyRows = new();

    private void OnEnable()
    {
        WireButtons();
        LivesTimerService.EnsureExists();
        LivesManager.OnLivesChanged += RefreshLifeControls;
        if (messageInputRoot != null) messageInputRoot.SetActive(false);
        ApplyTeamState();
    }

    // Takım durumuna göre görünüm: takımsız → Ara/Oluştur tarayıcısı; takımlı → sohbet.
    private void ApplyTeamState()
    {
        bool hasTeam = PlayerTeamState.HasTeam;

        if (inTeamRoot != null) inTeamRoot.SetActive(hasTeam);
        if (browser != null) browser.gameObject.SetActive(!hasTeam);

        if (!hasTeam) return;

        // Katılma/kurma sonrası BackendServices.ResetTeam çağrılmış olabilir —
        // her açılışta güncel servisi al (singleton zaten cache'ler).
        if (service != null) service.OnChanged -= OnServiceChanged;
        service = BackendServices.Team;
        service.OnChanged += OnServiceChanged;   // gerçek sohbette canlı mesaj akışı
        Refresh();
    }

    private void OnServiceChanged() => Refresh();

    private void OnDisable()
    {
        LivesManager.OnLivesChanged -= RefreshLifeControls;
        if (service != null) service.OnChanged -= OnServiceChanged;
    }

    // Browser'dan katılma/kurma bitti sinyali (mockup kurulumunda bağlanır).
    private void OnTeamEntered() => ApplyTeamState();

    private void Awake()
    {
        if (browser != null) browser.OnTeamEntered += OnTeamEntered;
    }

    private void OnDestroy()
    {
        if (browser != null) browser.OnTeamEntered -= OnTeamEntered;
    }

    private void WireButtons()
    {
        if (wired) return;
        if (requestLifeButton != null)
        {
            requestLifeButton.onClick.AddListener(OnRequestLife);
            requestLifeLabel = requestLifeButton.GetComponentInChildren<TMP_Text>(true);
            defaultRequestLifeLabel = requestLifeLabel != null ? requestLifeLabel.text : "Can İste";
        }
        if (messageButton != null)     messageButton.onClick.AddListener(ToggleMessageInput);
        if (messagePostButton != null) messagePostButton.onClick.AddListener(OnPostMessage);
        if (messageInput != null)      messageInput.onSubmit.AddListener(_ => OnPostMessage());

        if (leaveButton == null) leaveButton = BuildLeaveButton();
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
        wired = true;
    }

    // Header sağ-üstüne runtime "Ayrıl" butonu (serialized leaveButton yoksa).
    private Button BuildLeaveButton()
    {
        var parent = inTeamRoot != null ? inTeamRoot.transform : transform;
        var go = new GameObject("LeaveTeamButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-16, -16); rt.sizeDelta = new Vector2(92, 92);
        var img = go.AddComponent<Image>();
        ApplyButtonImage(img, leaveRedSquareButtonSprite, new Color(0.75f, 0.25f, 0.25f));
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        txtGo.layer = gameObject.layer;
        var trt = (RectTransform)txtGo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = "Ayrıl"; txt.fontSize = 24; txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center; txt.color = Color.white;
        return btn;
    }

    // ── Takımdan ayrıl (onaylı) ─────────────────────────────────────

    private void OnLeaveClicked() => ShowLeaveConfirm();

    private void ShowLeaveConfirm()
    {
        RuntimeChoicePopup.Show("Takımdan Ayrıl", "Takımdan ayrılmak istiyor musun?",
            new RuntimeChoicePopup.Choice("Ayrıl", () => { if (this != null) DoLeave(); }, primary: true),
            new RuntimeChoicePopup.Choice("Vazgeç", null));
    }

    private void DoLeave()
    {
        if (service != null) service.OnChanged -= OnServiceChanged;
        service = null;
        PlayerTeamState.LeaveTeam();
        BackendServices.ResetTeam();
        ApplyTeamState();   // takımsız görünüme (Ara/Oluştur) döner
    }

    private static void ApplyButtonImage(Image image, Sprite sprite, Color fallbackColor)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.color = sprite != null ? Color.white : fallbackColor;
        image.preserveAspect = sprite != null;
    }

    private void Refresh()
    {
        if (service == null) return;
        var info = service.GetTeamInfo();
        if (info != null)
        {
            if (emblemImage != null)
            {
                // Öncelik: servis ambleminin kendisi → oyuncunun seçtiği havuz amblemi → varsayılan.
                var emblem = info.emblem;
                if (emblem == null && emblemPool != null && emblemPool.Length > 0)
                    emblem = emblemPool[PlayerTeamState.EmblemIndex % emblemPool.Length];
                if (emblem == null) emblem = defaultEmblem;
                emblemImage.sprite  = emblem;
                emblemImage.enabled = emblem != null;
                emblemImage.preserveAspect = true;
            }
            if (teamNameText != null)    teamNameText.text = info.teamName;
            SingleLineText.Fit(teamNameText);
            if (memberCountText != null) memberCountText.text = info.MemberLabel;
        }

        BuildFeed();
        RefreshLifeControls();
    }

    private void BuildFeed()
    {
        foreach (var go in feed) if (go != null) Destroy(go);
        feed.Clear();
        lifeReplyRows.Clear();
        if (contentContainer == null) return;

        if (chatRowPrefab != null)
        {
            foreach (var m in GetFeedMessages().OrderBy(message => message.sentTicks))
            {
                if (m.avatar == null)
                {
                    // Benim mesajım → ProfileScreen'de SEÇTİĞİM avatar; gelen → havuzdan.
                    m.avatar = m.isMine
                        ? (PlayerAvatarProvider.Current ?? PickAvatar(m.senderName))
                        : PickAvatar(m.senderName);
                }
                var row = Instantiate(chatRowPrefab, contentContainer);
                row.Bind(m, theme);
                if (m.lifeReply != null)
                {
                    var inbox = service.LifeInbox;
                    row.BindLifeReply(m.lifeReply, () => inbox.Accept(m.lifeReply.id));
                    lifeReplyRows.Add(row);
                }
                else if (m.botLifeRequest != null)
                {
                    var inbox = service.LifeInbox;
                    row.BindBotLifeRequest(m.botLifeRequest, () => inbox.HelpBot(m.botLifeRequest.id));
                    lifeReplyRows.Add(row);
                }
                feed.Add(row.gameObject);
            }
        }

        SnapToBottom();
    }

    private IEnumerable<TeamChatMessage> GetFeedMessages()
    {
        foreach (var message in service.GetChat()) yield return message;
        foreach (var reply in service.LifeInbox.Replies)
        {
            yield return new TeamChatMessage
            {
                senderName = reply.sender,
                avatar = PickAvatar(reply.sender),
                text = "Sana 1 can gönderdim!",
                timeLabel = new System.DateTime(reply.sentTicks, System.DateTimeKind.Utc).ToLocalTime().ToString("HH:mm"),
                sentTicks = reply.sentTicks,
                lifeReply = reply,
                isMine = false
            };
        }
        foreach (var request in service.LifeInbox.BotRequests)
        {
            yield return new TeamChatMessage
            {
                senderName = request.sender,
                avatar = PickAvatar(request.sender),
                text = request.helped ? "Can için teşekkürler!" : "Can gönderebilir misin?",
                timeLabel = new System.DateTime(request.sentTicks, System.DateTimeKind.Utc).ToLocalTime().ToString("HH:mm"),
                sentTicks = request.sentTicks,
                botLifeRequest = request,
                isMine = false
            };
        }
    }

    private void RefreshLifeControls()
    {
        var inbox = service?.LifeInbox;
        bool full = LivesManager.Current >= LivesManager.MaxLives;
        if (requestLifeButton != null) requestLifeButton.interactable = inbox != null && inbox.CanRequest;
        if (requestLifeLabel != null)
            requestLifeLabel.text = full ? "Can Dolu (10/10)"
                : inbox != null && inbox.Pending > 0 ? "Canların Hazır"
                : inbox != null && inbox.IsWaiting ? "Can Bekleniyor"
                : defaultRequestLifeLabel;
        foreach (var row in lifeReplyRows)
            if (row != null) row.RefreshLifeReply();
    }

    private void SnapToBottom()
    {
        if (scrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;   // en yeni mesaj altta
    }

    // İsme göre deterministik avatar (aynı isim her seferinde aynı robotu alır).
    private Sprite PickAvatar(string name)
    {
        Sprite profileAvatar = PlayerAvatarProvider.PickForSeed(name);
        if (profileAvatar != null)
            return profileAvatar;

        if (avatarPool == null || avatarPool.Length == 0) return null;
        int hash = StableHash(name);
        return avatarPool[hash % avatarPool.Length];
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
            }
            return hash & int.MaxValue;
        }
    }

    // ── Aksiyonlar ──────────────────────────────────────────────────

    private void OnRequestLife()
    {
        if (service == null || !service.LifeInbox.CanRequest) return;
        service.RequestLife();
        RefreshLifeControls();
    }

    // Mesaj butonu: tek satırlık input alanını aç/kapat.
    private void ToggleMessageInput()
    {
        if (messageInputRoot == null) return;
        bool show = !messageInputRoot.activeSelf;
        messageInputRoot.SetActive(show);
        if (show && messageInput != null)
        {
            messageInput.text = "";
            messageInput.ActivateInputField();
        }
    }

    private void OnPostMessage()
    {
        if (messageInput == null) return;
        string text = messageInput.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        service.SendMessage(text);
        messageInput.text = "";
        BuildFeed();
        if (messageInputRoot != null) messageInputRoot.SetActive(false);
    }
}
