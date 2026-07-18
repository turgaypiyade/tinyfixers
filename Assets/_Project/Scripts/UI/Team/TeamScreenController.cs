using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takım ekranı (sade sohbet). Üst bilgi (amblem, isim, üye sayısı), altında sohbet akışı
/// (gelen mesaj solda, benimki sağda), en altta "Can İste" / "Mesaj". Mesaj'a basınca
/// bottom bar üstünde tek satırlık input açılır. v1'de MockTeamService. (Event/hediye YOK.)
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

    private void OnEnable()
    {
        WireButtons();
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
        service = BackendServices.Team;
        Refresh();
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
        if (requestLifeButton != null) requestLifeButton.onClick.AddListener(OnRequestLife);
        if (messageButton != null)     messageButton.onClick.AddListener(ToggleMessageInput);
        if (messagePostButton != null) messagePostButton.onClick.AddListener(OnPostMessage);
        if (messageInput != null)      messageInput.onSubmit.AddListener(_ => OnPostMessage());
        wired = true;
    }

    private void Refresh()
    {
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
            if (memberCountText != null) memberCountText.text = info.MemberLabel;
        }

        BuildFeed();
    }

    private void BuildFeed()
    {
        foreach (var go in feed) if (go != null) Destroy(go);
        feed.Clear();
        if (contentContainer == null || chatRowPrefab == null) return;

        foreach (var m in service.GetChat())
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
            feed.Add(row.gameObject);
        }

        SnapToBottom();
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
        if (avatarPool == null || avatarPool.Length == 0) return null;
        int hash = string.IsNullOrEmpty(name) ? 0 : Mathf.Abs(name.GetHashCode());
        return avatarPool[hash % avatarPool.Length];
    }

    // ── Aksiyonlar ──────────────────────────────────────────────────

    private void OnRequestLife()
    {
        service.RequestLife();
        BuildFeed();
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
