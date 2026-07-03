using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takım ekranı. Üst bilgi (amblem, isim, hediye ilerleme + sayaç + görev), altında
/// sohbet + can istekleri akışı, en altta "Can İste" / "Mesaj". v1'de MockTeamService.
/// </summary>
public sealed class TeamScreenController : MonoBehaviour
{
    [Header("Tema")]
    [SerializeField] private UITheme theme;

    [Header("Üst bilgi")]
    [SerializeField] private Image emblemImage;
    [SerializeField] private TMP_Text teamNameText;
    [SerializeField] private TMP_Text memberCountText;   // "40/50"
    [SerializeField] private Image giftFill;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text missionText;

    [Header("Akış")]
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private TeamChatRow chatRowPrefab;
    [SerializeField] private TeamLifeRequestRow lifeRequestRowPrefab;

    [Header("Alt butonlar")]
    [SerializeField] private Button requestLifeButton;
    [SerializeField] private Button messageButton;

    [Header("Görsel")]
    [Tooltip("Avatarı olmayan mesaj/isteklere isme göre deterministik dağıtılan mock avatar havuzu.")]
    [SerializeField] private Sprite[] avatarPool;
    [Tooltip("Takım amblemi yoksa kullanılacak varsayılan amblem.")]
    [SerializeField] private Sprite defaultEmblem;

    private ITeamService service;
    private readonly List<GameObject> feed = new();
    private bool wired;

    private void OnEnable()
    {
        service ??= BackendServices.Team;
        WireButtons();
        Refresh();
    }

    private void WireButtons()
    {
        if (wired) return;
        if (requestLifeButton != null) requestLifeButton.onClick.AddListener(OnRequestLife);
        if (messageButton != null)     messageButton.onClick.AddListener(OnMessage);
        wired = true;
    }

    private void Refresh()
    {
        var info = service.GetTeamInfo();
        if (info != null)
        {
            if (emblemImage != null)
            {
                var emblem = info.emblem != null ? info.emblem : defaultEmblem;
                emblemImage.sprite  = emblem;
                emblemImage.enabled = emblem != null;
                emblemImage.preserveAspect = true;
            }
            if (teamNameText != null)   teamNameText.text = info.teamName;
            if (memberCountText != null) memberCountText.text = info.MemberLabel;
            if (giftFill != null)     giftFill.fillAmount = info.GiftProgress01;
            if (timerText != null)    timerText.text = info.timerLabel;
            if (missionText != null)  missionText.text = info.missionText;
        }

        BuildFeed();
    }

    private void BuildFeed()
    {
        foreach (var go in feed) if (go != null) Destroy(go);
        feed.Clear();
        if (contentContainer == null) return;

        if (chatRowPrefab != null)
        {
            foreach (var m in service.GetChat())
            {
                if (m.avatar == null) m.avatar = PickAvatar(m.senderName);
                var row = Instantiate(chatRowPrefab, contentContainer);
                row.Bind(m, theme);
                feed.Add(row.gameObject);
            }
        }

        if (lifeRequestRowPrefab != null)
        {
            foreach (var r in service.GetLifeRequests())
            {
                if (r.avatar == null) r.avatar = PickAvatar(r.requesterName);
                var row = Instantiate(lifeRequestRowPrefab, contentContainer);
                row.Bind(r, theme, HandleHelp);
                feed.Add(row.gameObject);
            }
        }
    }

    // İsme göre deterministik avatar (aynı isim her seferinde aynı robotu alır).
    private Sprite PickAvatar(string name)
    {
        if (avatarPool == null || avatarPool.Length == 0) return null;
        int hash = string.IsNullOrEmpty(name) ? 0 : Mathf.Abs(name.GetHashCode());
        return avatarPool[hash % avatarPool.Length];
    }

    private void HandleHelp(TeamLifeRequest request)
    {
        service.Help(request);
        BuildFeed();
    }

    private void OnRequestLife()
    {
        service.RequestLife();
        BuildFeed();
    }

    private void OnMessage()
    {
        // Mockup: sabit mesaj gönder (gerçekte input alanı açılır).
        service.SendMessage("Merhaba takım!");
        BuildFeed();
    }
}
