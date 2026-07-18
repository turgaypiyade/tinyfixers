using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takımsız durum ekranı (Team tab): üstte Ara/Oluştur sekmeleri.
/// — Ara: takım adı arama + takım listesi; "Takım Bilgisi" → popup (amblem, kapasite,
///   açıklama, gereken bölüm) → Katıl.
/// — Oluştur: ad, amblem (Gözat = havuzda gezin), açıklama, gereken bölüm (◀ N ▶),
///   Oluştur (100 coin).
/// Katılınca/kurulunca PlayerTeamState kalıcılaşır, OnTeamEntered tetiklenir —
/// TeamScreenController sohbet görünümüne geçer.
/// </summary>
public sealed class TeamBrowserController : MonoBehaviour
{
    public const int CreateCostCoins = 100;

    [Header("Tema")]
    [SerializeField] private UITheme theme;

    [Header("Sekmeler")]
    [SerializeField] private Button searchTabButton;
    [SerializeField] private Image searchTabBg;
    [SerializeField] private Button createTabButton;
    [SerializeField] private Image createTabBg;
    [SerializeField] private GameObject searchRoot;
    [SerializeField] private GameObject createRoot;

    [Header("Ara")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button searchButton;
    [SerializeField] private Button searchClearButton;
    [SerializeField] private RectTransform resultContainer;
    [SerializeField] private TeamBrowserRow rowPrefab;

    [Header("Takım Bilgisi popup")]
    [SerializeField] private GameObject infoPopupRoot;
    [SerializeField] private Image infoEmblem;
    [SerializeField] private TMP_Text infoNameText;
    [SerializeField] private TMP_Text infoCapacityText;
    [SerializeField] private TMP_Text infoDescText;
    [SerializeField] private TMP_Text infoMinChapterText;
    [SerializeField] private Button infoJoinButton;
    [SerializeField] private TMP_Text infoJoinLabel;
    [SerializeField] private Button infoCloseButton;

    [Header("Oluştur")]
    [SerializeField] private TMP_InputField createNameInput;
    [SerializeField] private Image createEmblemImage;
    [SerializeField] private Button browseEmblemButton;      // "Gözat" → havuzda sıradaki
    [SerializeField] private TMP_InputField createDescInput;
    [SerializeField] private TMP_Text minChapterText;
    [SerializeField] private Button minChapterMinusButton;
    [SerializeField] private Button minChapterPlusButton;
    [SerializeField] private Button createButton;
    [SerializeField] private TMP_Text createButtonLabel;     // "Oluştur  100"
    [SerializeField] private TMP_Text createFeedbackText;    // hata/bilgi satırı (başta boş)

    [Header("Amblem havuzu")]
    [Tooltip("Takım amblemi sprite'ları. Gözat sırayla gezer; satır amblemleri de buradan.")]
    [SerializeField] private Sprite[] emblemPool;

    /// <summary>Katılma/kurma başarıyla bitince tetiklenir (TeamScreenController dinler).</summary>
    public event Action OnTeamEntered;

    private readonly List<TeamBrowserRow> rows = new();
    private TeamDirectoryEntry infoEntry;
    private int emblemIndex;
    private int minChapter;
    private bool wired;

    private void OnEnable()
    {
        Wire();
        ShowSearch(true);
        if (infoPopupRoot != null) infoPopupRoot.SetActive(false);
        if (createFeedbackText != null) createFeedbackText.text = "";
        UpdateCreateVisuals();
        RenderResults(TeamDirectory.Browse(null));
    }

    private void Wire()
    {
        if (wired) return;

        if (searchTabButton != null) searchTabButton.onClick.AddListener(() => ShowSearch(true));
        if (createTabButton != null) createTabButton.onClick.AddListener(() => ShowSearch(false));

        if (searchButton != null) searchButton.onClick.AddListener(OnSearch);
        if (searchInput != null) searchInput.onSubmit.AddListener(_ => OnSearch());
        if (searchClearButton != null) searchClearButton.onClick.AddListener(() =>
        {
            if (searchInput != null) searchInput.text = "";
            RenderResults(TeamDirectory.Browse(null));
        });

        if (infoCloseButton != null) infoCloseButton.onClick.AddListener(() => infoPopupRoot.SetActive(false));
        if (infoJoinButton != null) infoJoinButton.onClick.AddListener(OnJoin);

        if (browseEmblemButton != null) browseEmblemButton.onClick.AddListener(() =>
        {
            if (emblemPool == null || emblemPool.Length == 0) return;
            emblemIndex = (emblemIndex + 1) % emblemPool.Length;
            UpdateCreateVisuals();
        });
        if (minChapterMinusButton != null) minChapterMinusButton.onClick.AddListener(() =>
        {
            minChapter = Mathf.Max(0, minChapter - 10);
            UpdateCreateVisuals();
        });
        if (minChapterPlusButton != null) minChapterPlusButton.onClick.AddListener(() =>
        {
            minChapter = Mathf.Min(500, minChapter + 10);
            UpdateCreateVisuals();
        });
        if (createButton != null) createButton.onClick.AddListener(OnCreate);

        wired = true;
    }

    // ── Sekmeler ────────────────────────────────────────────────────

    private void ShowSearch(bool search)
    {
        if (searchRoot != null) searchRoot.SetActive(search);
        if (createRoot != null) createRoot.SetActive(!search);

        if (theme != null)
        {
            if (searchTabBg != null) searchTabBg.color = search ? theme.accentAmber : theme.screenBackground;
            if (createTabBg != null) createTabBg.color = search ? theme.screenBackground : theme.accentAmber;
        }
    }

    // ── Ara ─────────────────────────────────────────────────────────

    private void OnSearch()
        => RenderResults(TeamDirectory.Browse(searchInput != null ? searchInput.text : null));

    private void RenderResults(List<TeamDirectoryEntry> entries)
    {
        foreach (var r in rows)
        {
            if (r == null) continue;
            r.gameObject.SetActive(false);
            r.transform.SetParent(null, false);
            Destroy(r.gameObject);
        }
        rows.Clear();

        if (resultContainer == null || rowPrefab == null || entries == null) return;

        foreach (var e in entries)
        {
            var captured = e;
            var row = Instantiate(rowPrefab, resultContainer);
            row.Bind(captured, EmblemFor(captured.emblemSeed), () => OpenInfo(captured));
            rows.Add(row);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(resultContainer);
    }

    private void OpenInfo(TeamDirectoryEntry entry)
    {
        infoEntry = entry;
        if (infoPopupRoot == null || entry == null) return;

        infoPopupRoot.SetActive(true);
        if (infoNameText != null) infoNameText.text = entry.name;
        if (infoCapacityText != null) infoCapacityText.text = $"{entry.members}/{entry.capacity}";
        if (infoDescText != null) infoDescText.text = entry.description;
        if (infoEmblem != null)
        {
            var s = EmblemFor(entry.emblemSeed);
            infoEmblem.sprite = s;
            infoEmblem.enabled = s != null;
            infoEmblem.preserveAspect = true;
        }

        int myChapter = Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1));
        bool chapterOk = myChapter >= entry.minChapter;
        bool hasRoom = entry.members < entry.capacity;

        if (infoMinChapterText != null)
        {
            infoMinChapterText.gameObject.SetActive(entry.minChapter > 0);
            infoMinChapterText.text = $"Gereken Bölüm: {entry.minChapter}";
        }
        if (infoJoinButton != null) infoJoinButton.interactable = chapterOk && hasRoom;
        if (infoJoinLabel != null)
            infoJoinLabel.text = !hasRoom ? "Dolu" : (!chapterOk ? $"Bölüm {entry.minChapter} gerek" : "Katıl");
    }

    private void OnJoin()
    {
        if (infoEntry == null) return;

        PlayerTeamState.JoinTeam(
            infoEntry.name,
            emblemIndexFromSeed(infoEntry.emblemSeed),
            infoEntry.description,
            infoEntry.minChapter);

        BackendServices.ResetTeam();   // yeni takım adıyla taze sohbet servisi
        if (infoPopupRoot != null) infoPopupRoot.SetActive(false);
        OnTeamEntered?.Invoke();
    }

    // ── Oluştur ─────────────────────────────────────────────────────

    private void UpdateCreateVisuals()
    {
        if (minChapterText != null) minChapterText.text = minChapter.ToString();
        if (createButtonLabel != null) createButtonLabel.text = $"Oluştur  {CreateCostCoins}";
        if (createEmblemImage != null)
        {
            var s = emblemPool != null && emblemPool.Length > 0
                ? emblemPool[emblemIndex % emblemPool.Length]
                : null;
            createEmblemImage.sprite = s;
            createEmblemImage.enabled = s != null;
            createEmblemImage.preserveAspect = true;
        }
    }

    private void OnCreate()
    {
        string name = createNameInput != null ? createNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
        {
            SetFeedback("Takım adı boş olamaz.");
            return;
        }

        if (!PlayerWallet.SpendCoins(CreateCostCoins))
        {
            SetFeedback($"Yetersiz coin ({CreateCostCoins} gerekiyor).");
            return;
        }

        PlayerTeamState.CreateTeam(
            name,
            emblemIndex,
            createDescInput != null ? createDescInput.text.Trim() : "",
            minChapter);

        BackendServices.ResetTeam();   // yeni (1 üyeli) takımla taze sohbet servisi
        OnTeamEntered?.Invoke();
    }

    private void SetFeedback(string msg)
    {
        if (createFeedbackText != null) createFeedbackText.text = msg;
    }

    // ── Amblem yardımcıları ─────────────────────────────────────────

    private int emblemIndexFromSeed(int seed)
        => emblemPool == null || emblemPool.Length == 0 ? 0 : Mathf.Abs(seed) % emblemPool.Length;

    private Sprite EmblemFor(int seed)
        => emblemPool == null || emblemPool.Length == 0 ? null : emblemPool[Mathf.Abs(seed) % emblemPool.Length];
}
