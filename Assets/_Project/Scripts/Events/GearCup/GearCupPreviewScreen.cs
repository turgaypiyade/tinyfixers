using System;
using System.Collections.Generic;
using Firebase.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Live Gear Cup screen. Historical name retained to preserve serialized scene bindings.</summary>
public sealed class GearCupPreviewScreen : MonoBehaviour
{
    [Serializable]
    public sealed class TeamRow
    {
        public TMP_Text rank;
        public TMP_Text name;
        public TMP_Text score;
        public TMP_Text ownLabel;
        public Image emblem;
        public GameObject ownOutline;
    }

    [Serializable]
    public sealed class RewardCard
    {
        public TMP_Text rank;
        public TMP_Text coins;
        public TMP_Text amount;
        public Image item;
    }

    [Serializable]
    public sealed class View
    {
        public RectTransform safeArea;
        public RectTransform design;
        public TMP_Text title;
        public TMP_Text timer;
        public TMP_Text previewLabel;
        public TMP_Text contributionLabel;
        public TMP_Text contribution;
        public Button close;
        public Button info;
        public TeamRow[] teams;
        public RewardCard[] rewards;
        public RectTransform hero;
    }

    [SerializeField] private GearCupPreviewConfig config;
    [SerializeField] private View view = new View();
    [SerializeField] private RectTransform launcher;
    private IReadOnlyList<GearCupTeamScore> standings;
    private float nextTimerRefresh;
    private float continuePulseStarted;
    private bool bound;
    private bool layoutPending;
    private bool frameBuilt;
    private RectTransform titlePlate;
    private bool heroCaptured;
    private float heroHomeY;
    private GearCupJoinPopup joinPopup;
    private EventScreenIris iris;

    public void ConfigureLauncher(RectTransform value) => launcher = value;
    public bool IsInvitationVisible => joinPopup != null && joinPopup.gameObject.activeInHierarchy;

    public TimeSpan PreviewTimeRemaining
    {
        get
        {
            var cfg = config != null ? config : GearCupPreviewConfig.Shared;
            if (cfg == null) return TimeSpan.Zero;
            DateTime end = GearCupState.HasJoined && !GearCupState.CanShowInvitation ? GearCupState.End :
                GearCupRules.SeasonStart(DateTime.UtcNow, cfg.seasonHours).AddHours(cfg.seasonHours);
            var remaining = end - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public void Configure(GearCupPreviewConfig value, View bindings)
    {
        config = value;
        view = bindings;
    }

    private void Awake() => Bind();

    private void Bind()
    {
        if (bound || view == null) return;
        if (view.close != null) view.close.onClick.AddListener(Close);
        if (view.info != null) view.info.onClick.AddListener(ShowInfo);
        bound = true;
    }

    private void OnEnable()
    {
        Bind();
        GameLocalization.OnLanguageChanged += Refresh;
        GearCupState.Changed += HandleChanged;
        GearCupRuntime.Changed += HandleChanged;
        GearCupRuntime.SetVisible(true);
        continuePulseStarted = Time.unscaledTime;
        layoutPending = true;
        Refresh();
    }

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= Refresh;
        GearCupState.Changed -= HandleChanged;
        GearCupRuntime.Changed -= HandleChanged;
        GearCupRuntime.SetVisible(false);
        if (view.previewLabel != null) view.previewLabel.canvasRenderer.SetAlpha(1f);
    }

    private void HandleChanged()
    {
        Refresh();
        if (iris == null || !iris.IsRunning) PresentResult();
    }

    private void OnDestroy()
    {
        if (joinPopup != null) Destroy(joinPopup.gameObject);
        if (!bound || view == null) return;
        if (view.close != null) view.close.onClick.RemoveListener(Close);
        if (view.info != null) view.info.onClick.RemoveListener(ShowInfo);
    }

    public void Open()
    {
        if (config == null) { if (GameDiagnostics.CriticalErrors)
            Debug.LogWarning("[GearCup] Preview config is missing."); return; }
        if (iris != null && iris.IsRunning) return;
        if (!GearCupState.IsUnlocked || !config.eventEnabled)
        {
            RuntimeChoicePopup.Show(L("gearcup_title", "GEAR CUP"),
                GameLocalization.GetFormat("gearcup_unlock", config.minLevelGate),
                new RuntimeChoicePopup.Choice(L("common_ok", "OK"), null));
            return;
        }
        if (GearCupState.CanShowInvitation)
        {
            ShowInvitation();
            return;
        }
        OpenEvent();
    }

    public bool TryShowPendingResult()
    {
        if (!GearCupState.HasPendingResult || gameObject.activeInHierarchy || IsInvitationVisible) return false;
        OpenEvent();
        return true;
    }

    // Invitation acceptance persists enrollment before opening the real standings.
    public void AcceptPreviewJoin()
    {
        if (!GearCupState.TryJoin(out var error))
        {
            RuntimeChoicePopup.Show(L("gearcup_title", "GEAR CUP"), error,
                new RuntimeChoicePopup.Choice(L("common_ok", "OK"), null));
            return;
        }
        OpenEvent();
    }

    private void OpenEvent()
    {
        bool alreadyOpen = gameObject.activeSelf;
        gameObject.SetActive(true);
        if (alreadyOpen) Refresh();
        transform.SetAsLastSibling();
        // Fit before starting the mask so the first circular frame has the final safe-area layout.
        Fit();
        layoutPending = false;
        if (alreadyOpen) return;
        if (iris == null) iris = EventScreenIris.For((RectTransform)transform);
        iris.PlayOpen(launcher, PresentResult);
    }

    public void Close()
    {
        if (iris != null && iris.IsRunning) return;
        if (iris != null && gameObject.activeInHierarchy) iris.PlayClose(launcher, Hide);
        else Hide();
    }

    private void Hide() => gameObject.SetActive(false);

    public void ContinuePlaying()
    {
        if (!isActiveAndEnabled || (iris != null && iris.IsRunning)) return;
        var levelLauncher = FindFirstObjectByType<MainMenuLevelButtonController>(FindObjectsInactive.Include);
        if (levelLauncher == null)
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning("[GearCup] MainMenuLevelButtonController bulunamadı — Devam iptal.");
            return;
        }

        // Use the normal level entry point, including pre-level and lives checks, as Bridge does.
        EventSfx.Play(x => x.uiTap);
        Hide();
        if (levelLauncher.StartLevel() == LevelStartResult.Suppressed) OpenEvent();
    }

    private void OnRectTransformDimensionsChange() => layoutPending = true;

    private void OnApplicationFocus(bool focused)
    {
        if (focused) { layoutPending = true; nextTimerRefresh = 0f; }
    }

    private void Update()
    {
        if (layoutPending) { layoutPending = false; Fit(); }
        if (view.previewLabel != null)
        {
            // Renderer alpha avoids rebuilding the text mesh for each pulse frame.
            float pulse = 0.7f + 0.3f * Mathf.Cos((Time.unscaledTime - continuePulseStarted) * (2f * Mathf.PI / 1.4f));
            view.previewLabel.canvasRenderer.SetAlpha(pulse);
        }
        if (Time.unscaledTime < nextTimerRefresh) return;
        nextTimerRefresh = Time.unscaledTime + 1f;
        RefreshTimer();
    }

    private void Fit()
    {
        if (view.safeArea == null || view.design == null || Screen.width <= 0 || Screen.height <= 0) return;
        var safe = Screen.safeArea;
        view.safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        view.safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        view.safeArea.offsetMin = view.safeArea.offsetMax = Vector2.zero;
        var size = view.safeArea.rect.size;
        float scale = Mathf.Min(size.x / 1080f, size.y / 1920f);
        if (scale <= 0f) return;
        view.design.localScale = Vector3.one * scale;

        BuildFrame();
        if (config == null) return;
        // Spare safe-area height above the centred design, in design units (0 on 16:9 and wider screens).
        float lift = Mathf.Clamp((size.y / scale - 1920f) * 0.5f, 0f, config.maxHeaderLift);
        float titleY = config.titleY + lift;
        SetY(view.title != null ? view.title.rectTransform : null, titleY);
        SetY(titlePlate, titleY);
        SetY(view.timer != null ? view.timer.rectTransform.parent as RectTransform : null, config.timerY + lift);
        SetY(view.info != null ? (RectTransform)view.info.transform : null, titleY);
        SetY(view.close != null ? (RectTransform)view.close.transform : null, titleY);

        if (view.previewLabel != null)
        {
            // Spare space below the design equals the space above it (the design is centred).
            float spare = Mathf.Max(0f, (size.y / scale - 1920f) * 0.5f);
            var notice = view.previewLabel;
            notice.fontSizeMax = config.noticeFontSize;
            notice.rectTransform.sizeDelta = new Vector2(notice.rectTransform.sizeDelta.x, config.noticeFontSize * 1.3f);
            SetY(notice.rectTransform, Mathf.Min(config.noticeMaxY, -1920f - spare + config.noticeBottomInset));
        }

        if (view.hero != null)
        {
            // Keep the hero clear of the header: shrink around the platform bottom only when space runs out.
            if (!heroCaptured) { heroHomeY = view.hero.anchoredPosition.y; heroCaptured = true; }
            float headerBottom = config.timerY + lift - 33f;
            float span = config.heroTopY - config.heroBottomY;
            float heroScale = span > 0f ? Mathf.Clamp((headerBottom - config.heroBottomY) / span, 0.75f, 1f) : 1f;
            view.hero.localScale = Vector3.one * heroScale;
            SetY(view.hero, config.heroBottomY + (heroHomeY - config.heroBottomY) * heroScale);
        }
    }

    // One-time runtime chrome over the scene-authored layout: framed team list and tilted title plate.
    private void BuildFrame()
    {
        if (frameBuilt || config == null) return;
        frameBuilt = true;
        var sprite = config.listPanel;

        if (view.teams != null && view.teams.Length > 0)
        {
            float center = (config.listPanelTop + config.listPanelBottom) * 0.5f;
            float first = center + config.teamRowStep * (view.teams.Length - 1) * 0.5f;
            RectTransform firstRow = null;
            for (int i = 0; i < view.teams.Length; i++)
            {
                var root = view.teams[i]?.rank != null ? view.teams[i].rank.rectTransform.parent as RectTransform : null;
                if (root == null) continue;
                if (firstRow == null) firstRow = root;
                SetY(root, first - i * config.teamRowStep);
                root.localScale = Vector3.one * config.teamRowScale;
            }
            if (sprite != null && firstRow != null)
            {
                var rowSize = firstRow.rect.size * config.teamRowScale;
                var panel = FramePiece("TeamListPanel", sprite, config.listPanelBorderScale, firstRow.GetSiblingIndex());
                panel.anchoredPosition = new Vector2(0f, center);
                panel.sizeDelta = new Vector2(rowSize.x + config.listPanelPadding * 2f,
                    config.teamRowStep * (view.teams.Length - 1) + rowSize.y + config.listPanelPaddingY * 2f);
            }
        }

        if (view.title != null)
        {
            var tilt = new Vector3(0f, 0f, config.titleTilt);
            var title = view.title.rectTransform;
            title.sizeDelta = config.titleTextSize;
            title.localEulerAngles = tilt;
            if (sprite != null)
            {
                titlePlate = FramePiece("TitlePlate", sprite, config.titlePlateBorderScale, title.GetSiblingIndex());
                titlePlate.anchoredPosition = new Vector2(title.anchoredPosition.x, title.anchoredPosition.y);
                titlePlate.sizeDelta = config.titlePlateSize;
                titlePlate.localEulerAngles = tilt;
            }
        }
    }

    private RectTransform FramePiece(string name, Sprite sprite, float borderScale, int siblingIndex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = view.design.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(view.design, false);
        rect.SetSiblingIndex(siblingIndex);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = borderScale;
        image.raycastTarget = false;
        return rect;
    }

    private static void SetY(RectTransform target, float y)
    {
        if (target == null) return;
        var position = target.anchoredPosition;
        position.y = y;
        target.anchoredPosition = position;
    }

    private void Refresh()
    {
        if (view == null || config == null) return;
        Set(view.title, L("gearcup_title", "GEAR CUP"));
        standings = GearCupRuntime.Standings();
        var state = GearCupState.Data;
        Set(view.previewLabel, L("common_tap_continue", "Tap to continue"));
        Set(view.contributionLabel, L("gearcup_contribution", "YOUR CONTRIBUTION"));
        Set(view.contribution, state.personal.ToString("N0"));
        if (view.teams != null)
        {
            for (int i = 0; i < view.teams.Length; i++)
            {
                var row = view.teams[i];
                if (row == null) continue;
                var team = standings != null && i < standings.Count ? standings[i] : null;
                Set(row.rank, (i + 1).ToString());
                Set(row.name, TeamName(i));
                Set(row.score, TeamScore(i).ToString("N0"));
                Set(row.ownLabel, L("gearcup_your_team", "YOUR TEAM"));
                bool own = team != null && team.id == state.teamId;
                if (row.name != null)
                {
                    var position = row.name.rectTransform.anchoredPosition;
                    position.y = own ? 14f : 0f;
                    row.name.rectTransform.anchoredPosition = position;
                }
                if (row.ownLabel != null) row.ownLabel.gameObject.SetActive(own);
                if (row.ownOutline != null) row.ownOutline.SetActive(own);
                if (row.emblem != null && TeamEmblemLibrary.All.Length > 0)
                {
                    int index = team != null ? team.emblem : i;
                    row.emblem.sprite = TeamEmblemLibrary.All[((index % TeamEmblemLibrary.All.Length) + TeamEmblemLibrary.All.Length) % TeamEmblemLibrary.All.Length];
                }
            }
        }
        if (view.rewards != null)
        {
            for (int i = 0; i < view.rewards.Length; i++)
            {
                var card = view.rewards[i];
                var reward = Reward(i);
                if (card == null) continue;
                Set(card.rank, RankLabel(i));
                Set(card.coins, reward != null ? reward.coins.ToString("N0") : "—");
                Set(card.amount, reward != null ? "×" + reward.itemAmount : "");
                if (card.item != null)
                {
                    card.item.sprite = reward != null && TileIconLibrary.Shared != null
                        ? TileIconLibrary.Shared.GetRewardIcon(reward.item) : null;
                    card.item.enabled = card.item.sprite != null;
                }
            }
        }
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        Set(view.timer, L("gearcup_ends_in", "ENDS IN") + "  " + TimeFormat.Countdown(PreviewTimeRemaining));
    }

    public void ShowInfo()
    {
        string message = L("gearcup_howto", "Collect gears with your team. The three highest-scoring teams win different rewards.") +
            "\n\n" + L("gearcup_scoring_rules", "Each normal tile earns a gear. Only won levels add gears to your team; unfinished level gears are lost when you give up.");
        var ok = new RuntimeChoicePopup.Choice(L("common_ok", "OK"), null);
        // Keep next-season enrollment available after repurposing the footer for normal play.
        if (DateTime.UtcNow >= GearCupState.End && GearCupState.Data.settledRank > 0)
            RuntimeChoicePopup.Show(L("gearcup_title", "GEAR CUP"), message, ok,
                new RuntimeChoicePopup.Choice(L("gearcup_next_event", "Tap to join the next event"), JoinNextSeason, true));
        else RuntimeChoicePopup.Show(L("gearcup_title", "GEAR CUP"), message, ok);
    }

    public void ShowFirstReward() => ShowReward(0);
    public void ShowSecondReward() => ShowReward(1);
    public void ShowThirdReward() => ShowReward(2);

    private void ShowReward(int index)
    {
        var reward = Reward(index);
        if (reward == null) return;
        var icons = TileIconLibrary.Shared;
        RuntimeChoicePopup.ShowRewards(RankLabel(index), L("gearcup_reward_live", "Awarded to contributing team members at the end of the event"), new[]
        {
            new RuntimeChoicePopup.RewardItem(icons != null ? icons.GetRewardIcon(DailySlotRewardType.Coins) : null,
                reward.coins.ToString("N0"), L("shop_reward_coins", "Coins")),
            new RuntimeChoicePopup.RewardItem(icons != null ? icons.GetRewardIcon(reward.item) : null,
                "×" + reward.itemAmount, L("gearcup_reward_item", "Event reward"))
        }, L("common_ok", "OK"));
    }

    public void ShowTeamOne() => ShowTeam(0);
    public void ShowTeamTwo() => ShowTeam(1);
    public void ShowTeamThree() => ShowTeam(2);
    public void ShowTeamFour() => ShowTeam(3);
    public void ShowTeamFive() => ShowTeam(4);

    private void ShowTeam(int index)
    {
        if (standings == null || index < 0 || index >= standings.Count) return;
        var team = standings[index];
        string summary = GameLocalization.GetFormat("gearcup_team_total", team.score.ToString("N0"));
        if (team.bot || !FirebaseAuthService.IsReady)
        {
            RuntimeChoicePopup.Show(team.name, summary,
                new RuntimeChoicePopup.Choice(L("common_ok", "OK"), null));
            return;
        }
        string season = GearCupState.Data.season;
        GearCupCloud.Members(season, team.id).ContinueWithOnMainThread(task =>
        {
            if (this == null || !gameObject.activeInHierarchy || GearCupState.Data.season != season) return;
            string members = !task.IsFaulted && !task.IsCanceled ? string.Join("\n", task.Result) : "";
            RuntimeChoicePopup.Show(team.name, summary + "\n\n" + members,
                new RuntimeChoicePopup.Choice(L("common_ok", "OK"), null));
        });
    }

    public void JoinNextSeason()
    {
        if (DateTime.UtcNow < GearCupState.End) return;
        Hide();
        ShowInvitation();
    }

    private void ShowInvitation()
    {
        if (IsInvitationVisible) return;
        if (joinPopup == null) joinPopup = GearCupJoinPopup.Create(transform.parent, config);
        joinPopup.Show(this);
        if (!joinPopup.gameObject.activeInHierarchy) return;
        string cycle = GearCupRules.SeasonKey(GearCupRules.SeasonStart(DateTime.UtcNow, config.seasonHours));
        PlayerPrefs.SetString(GearCupState.InvitationAskedKey, cycle);
        PlayerPrefs.Save();
    }

    private void PresentResult()
    {
        var state = GearCupState.Data;
        if (!gameObject.activeInHierarchy || state.settledRank <= 0 || state.resultPresented || !state.coinDelivered || !state.itemDelivered) return;
        state.resultPresented = true;
        GearCupState.Save();
        string message = GameLocalization.GetFormat("gearcup_result", state.settledRank);
        if (state.rewardCoins <= 0)
        {
            RuntimeChoicePopup.Show(L("gearcup_title", "GEAR CUP"), message,
                new RuntimeChoicePopup.Choice(L("common_ok", "OK"), null));
            return;
        }
        var lib = TileIconLibrary.Shared;
        RuntimeChoicePopup.ShowRewards(L("gearcup_title", "GEAR CUP"), message, new[]
        {
            new RuntimeChoicePopup.RewardItem(lib != null ? lib.coin : null, state.rewardCoins.ToString("N0"), L("shop_reward_coins", "Coins")),
            new RuntimeChoicePopup.RewardItem(lib != null ? lib.GetRewardIcon((DailySlotRewardType)state.rewardItem) : null,
                "×" + state.rewardAmount, L("gearcup_reward_item", "Event reward"))
        }, L("common_ok", "OK"));
    }

    private string TeamName(int index) => standings != null && index < standings.Count ? standings[index].name : "";
    private int TeamScore(int index) => standings != null && index < standings.Count ? standings[index].score : 0;
    private GearCupPreviewConfig.RewardSample Reward(int index) => config.rewards != null && index < config.rewards.Length
        ? config.rewards[index] : null;

    private static string RankLabel(int index) => L("gearcup_rank_" + (index + 1), (index + 1).ToString());
    private static string L(string key, string fallback) => GameLocalization.GetOr(key, fallback);
    private static void Set(TMP_Text target, string value) { if (target != null) target.text = value; }
}
