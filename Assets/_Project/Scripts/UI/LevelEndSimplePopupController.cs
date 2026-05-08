using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class LevelEndSimplePopupController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [SerializeField] private BonusMovesService bonusMovesService;
    [SerializeField] private TileIconLibrary tileIconLibrary;
    [Header("Success Animation (image sequence — takes priority over video)")]
    [SerializeField] private SuccessAnimationPlayer successAnimationPlayer;

    [Header("Bonus Round Skip")]
    [Tooltip("Transparent fullscreen button shown during the bonus round. Tap to skip the comet animation.")]
    [SerializeField] private Button skipBonusRoundButton;

    [Header("Success Video")]
    [SerializeField] private bool playSuccessVideoBeforeMainMenu = true;
    [SerializeField] private GameObject successVideoRoot;
    [SerializeField] private VideoPlayer successVideoPlayer;
    [SerializeField] private float successVideoStartDelay = 0.4f;
    [SerializeField] private float successVideoFallbackSeconds = 8f;


    [Header("Popup Roots")]
    [SerializeField] private GameObject failPopupRoot;
    [SerializeField] private GameObject successPopupRoot;
    [SerializeField] private GameObject blockerRoot;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text failDescriptionText;
    [SerializeField] private TMP_Text successDescriptionText;

    [Header("Buttons")]
    [SerializeField] private Button buyMovesButton;
    [SerializeField] private Button failCloseButton;
    [SerializeField] private Button successCloseButton;
    [SerializeField] private Button successContinueButton; // BtnsContinue

    [Header("Fail Offer")]
    [SerializeField] private int[] extraMoveOfferAmounts = { 5, 10, 15 };
    [SerializeField] private int baseExtraMovesCost = 900;
    [SerializeField] private float extraMoveCostMultiplier = 1.5f;
    [SerializeField] private Sprite[] extraMoveOfferSprites;
    [SerializeField] private int extraMoveOfferAttempt;

    [Header("Fail Content")]
    [SerializeField] private TMP_Text failTitleText;
    [SerializeField] private Image extraMovesIcon;
    [SerializeField] private TMP_Text failMessageText;
    [SerializeField] private TMP_Text failContinueText;

    [Header("Success — Yıldız Görselleri (sırayla 1., 2., 3. yıldız)")]
    [SerializeField] private UnityEngine.UI.Image[] starImages;
    [SerializeField] private Sprite starFilledSprite;
    [SerializeField] private Sprite starEmptySprite;

    [Header("Success Content")]
    [SerializeField] private TMP_Text successTitleText;
    [SerializeField] private TMP_Text successContinueText;
    [SerializeField] private TMP_Text scoreLabelText;
    [SerializeField] private TMP_Text scoreValueText;
    [SerializeField] private GameObject[] goalResultRoots = new GameObject[4];
    [SerializeField] private Image[] goalIconImages = new Image[4];
    [SerializeField] private GameObject[] goalCheckMarks = new GameObject[4];

    [Header("Success — Para Metni")]
    [SerializeField] private TMP_Text coinsEarnedText;
    [SerializeField] private string coinsPrefix = "+";
    [SerializeField] private string coinsSuffix = " coin";

    [Header("Yıldız Eşikleri (kalan hamle / başlangıç hamle)")]
    [Range(0f, 1f)] [SerializeField] private float star3Ratio = 0.5f;  // >= %50 kalan → 3 yıldız
    [Range(0f, 1f)] [SerializeField] private float star2Ratio = 0.2f;  // >= %20 kalan → 2 yıldız

    [Header("Coin Ödülü")]
    [SerializeField] private int baseCoins = 100;
    [SerializeField] private int coinsPerRemainingMove = 20;

    [Header("Progression")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string prefsLevelKey = "current_level";

    private bool failPopupShown;
    private bool successPopupShown;
    private bool successReturnQueued;
    private int  _movesAtWin;
    private int currentOfferAmount;
    private int currentCost;

    // We may receive moves/goal events while the board is still resolving cascades.
    // This gate ensures we only evaluate & show end popups after the board becomes idle.
    private bool endCheckQueued;

    private void ResolveSerializedReferences()
    {
        // Prefer deterministic lookup by object name under this popup root.
        // This protects us from broken scene override references on prefab instances.
        var failRootByName = transform.Find("FailPopupRoot");
        if (failRootByName != null)
            failPopupRoot = failRootByName.gameObject;

        var successRootByName = transform.Find("SuccessPopupRoot");
        if (successRootByName != null)
            successPopupRoot = successRootByName.gameObject;

        var blockerByName = transform.Find("Blocker") ?? transform.Find("blocker");
        if (blockerByName != null)
            blockerRoot = blockerByName.gameObject;

        if (failPopupRoot != null)
        {
            var failContinue = failPopupRoot.transform.Find("UI/BtnContinue");
            if (failContinue != null)
                buyMovesButton = failContinue.GetComponent<Button>();

            var failClose = failPopupRoot.transform.Find("UI/BtnClose");
            if (failClose != null)
                failCloseButton = failClose.GetComponent<Button>();

            failTitleText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/FailTitleText") ?? failTitleText;
            extraMovesIcon = FindComponent<Image>(failPopupRoot.transform, "UI/ExtraMovesIcon") ?? extraMovesIcon;
            failMessageText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/FailMessageText") ?? failMessageText;
            failContinueText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/BtnContinue/BtnContinueText") ?? failContinueText;

            if (failDescriptionText == null)
                failDescriptionText = failMessageText;
        }

        if (successPopupRoot != null)
        {
            var successClose = successPopupRoot.transform.Find("UIS/BtnSClose");
            if (successClose != null)
                successCloseButton = successClose.GetComponent<Button>();

            var successContinue = successPopupRoot.transform.Find("UIS/BtnsContinue");
            if (successContinue != null)
                successContinueButton = successContinue.GetComponent<Button>();

            successTitleText = FindComponent<TMP_Text>(successPopupRoot.transform, "UIS/SuccessTitleText") ?? successTitleText;
            successContinueText = FindComponent<TMP_Text>(successPopupRoot.transform, "UIS/BtnsContinue/BtnContinueText") ?? successContinueText;
            scoreLabelText = FindComponent<TMP_Text>(successPopupRoot.transform, "UIS/ScoreRow/ScoreLabelText") ?? scoreLabelText;
            scoreValueText = FindComponent<TMP_Text>(successPopupRoot.transform, "UIS/ScoreRow/ScoreValueText") ?? scoreValueText;

            EnsureGoalResultArrays();
            EnsureStarImageArray();

            starImages[0] = FindComponent<Image>(successPopupRoot.transform, "UIS/StarsRow/Star1") ?? starImages[0];
            starImages[1] = FindComponent<Image>(successPopupRoot.transform, "UIS/StarsRow/Star2") ?? starImages[1];
            starImages[2] = FindComponent<Image>(successPopupRoot.transform, "UIS/StarsRow/Star3") ?? starImages[2];

            for (int i = 0; i < 4; i++)
            {
                string resultPath = $"UIS/GoalsRow/GoalResult_{i + 1}";
                var result = successPopupRoot.transform.Find(resultPath);
                if (result != null)
                    goalResultRoots[i] = result.gameObject;

                goalIconImages[i] = FindComponent<Image>(successPopupRoot.transform, $"{resultPath}/GoalIcon") ?? goalIconImages[i];

                var checkMark = successPopupRoot.transform.Find($"{resultPath}/CheckMark");
                if (checkMark != null)
                    goalCheckMarks[i] = checkMark.gameObject;
            }
        }
        if (successVideoRoot != null && successVideoPlayer == null)
            successVideoPlayer = successVideoRoot.GetComponent<VideoPlayer>();
    }

    private void OnEnable()
    {
        failPopupShown = false;
        successPopupShown = false;
        successReturnQueued = false;
        ResolveSerializedReferences();
        HideAllPopups();
        RegisterButtonListeners();

        if (skipBonusRoundButton != null)
            skipBonusRoundButton.gameObject.SetActive(false);

        StartCoroutine(InitializeWhenReady());
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnregisterButtonListeners();
    }

    private IEnumerator InitializeWhenReady()
    {
        if (board == null)
            board = FindFirstObjectByType<BoardController>()
                ?? FindFirstObjectByType<BoardController>(FindObjectsInactive.Include);

        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>()
                ?? FindFirstObjectByType<TopHudController>(FindObjectsInactive.Include);

        while (board == null || topHud == null || board.ActiveLevelData == null)
            yield return null;

        UnregisterButtonListeners();
        ResolveSerializedReferences();
        RegisterButtonListeners();
        Subscribe();
        RefreshPopupCopy();
        SetBlockerVisible(false);
        RequestEvaluateLevelEndState();
    }

    private void HandleFailCloseClicked()
    {
        HideAllPopups();
        ReturnToMainMenu();
    }

    private void HandleSuccessCloseClicked()
    {
        HideAllPopups();
        ReturnToMainMenu();
    }
    private void QueueSuccessReturnToMainMenu()
    {
        if (board == null || topHud == null)
            return;

        if (successPopupShown || successReturnQueued)
            return;

        successReturnQueued = true;

        Debug.Log("[LevelEnd] Success queued. Waiting board settle...");

        StartCoroutine(CompleteSuccessAfterBoardSettled());
    }

    private IEnumerator CompleteSuccessAfterBoardSettled()
    {
        // Goal completed event clear/fall sırasında gelebilir.
        // Bu yüzden önce en az 1 frame bekliyoruz.
        yield return null;

        const int requiredStableFrames = 3;
        int stableFrames = 0;

        while (board != null && stableFrames < requiredStableFrames)
        {
            bool boardStillWorking =
                board.IsBusy ||
                board.ActiveBackgroundJobs > 0;

            if (boardStillWorking)
            {
                stableFrames = 0;
            }
            else
            {
                stableFrames++;
            }

            yield return null;
        }

        // Son bir güvenlik: aynı anda tekrar busy olduysa bekle.
        while (board != null && (board.IsBusy || board.ActiveBackgroundJobs > 0))
            yield return null;

        // Capture remaining moves BEFORE the bonus round — used for star/coin rewards.
        _movesAtWin = board != null ? board.RemainingMoves : 0;

        // Kalan moves varsa bonus round: her move için random normal tile'a LineV/LineH at.
        if (bonusMovesService != null && board != null && board.RemainingMoves > 0)
        {
            board.SetInputLocked(true);
            SetSkipBonusOverlayVisible(true);
            yield return StartCoroutine(bonusMovesService.RunBonusRound());
            SetSkipBonusOverlayVisible(false);
        }

        // Görsel yumuşak geçiş. Bu delay artık SADECE burada var.
        if (successVideoStartDelay > 0f)
            yield return new WaitForSecondsRealtime(successVideoStartDelay);

        successReturnQueued = false;

        if (board == null || topHud == null)
            yield break;

        if (!topHud.AreAllGoalsCompleted)
        {
            Debug.Log("[LevelEnd] Success cancelled after settle; goals no longer completed.");
            yield break;
        }

        Debug.Log("[LevelEnd] Board settled. Showing success popup.");

        ShowSuccessPopup();
    }
    private IEnumerator PlaySuccessVideoThenReturnToMainMenu()
    {
        // Image-sequence animation takes priority over video when assigned
        if (successAnimationPlayer != null)
        {
            SetBlockerVisible(true);
            Debug.Log("[LevelEnd] successAnimationPlayer atandı, animasyon başlıyor");
            yield return StartCoroutine(successAnimationPlayer.Play());
            ReturnToMainMenu();
            yield break;
        }

        if (!playSuccessVideoBeforeMainMenu || successVideoRoot == null || successVideoPlayer == null)
        {
            Debug.Log("[LevelEnd] Success video disabled or missing references. Returning to main menu.");
            ReturnToMainMenu();
            yield break;
        }

        SetBlockerVisible(true);

        successVideoRoot.SetActive(true);
        successVideoRoot.transform.SetAsLastSibling();

        bool finished = false;

        void HandleFinished(VideoPlayer player)
        {
            finished = true;
        }

        void HandleError(VideoPlayer player, string message)
        {
            Debug.LogWarning($"[LevelEnd] Success video error: {message}");
            finished = true;
        }

        successVideoPlayer.loopPointReached += HandleFinished;
        successVideoPlayer.errorReceived += HandleError;

        successVideoPlayer.playOnAwake = false;
        successVideoPlayer.isLooping = false;
        successVideoPlayer.waitForFirstFrame = true;

        successVideoPlayer.Stop();
        successVideoPlayer.time = 0;
        successVideoPlayer.Prepare();

        float prepareTimer = 0f;
        const float prepareTimeout = 3f;

        while (!successVideoPlayer.isPrepared && prepareTimer < prepareTimeout)
        {
            prepareTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!successVideoPlayer.isPrepared)
        {
            Debug.LogWarning("[LevelEnd] Success video prepare timeout. Returning to main menu.");

            successVideoPlayer.loopPointReached -= HandleFinished;
            successVideoPlayer.errorReceived -= HandleError;

            ReturnToMainMenu();
            yield break;
        }

        Debug.Log("[LevelEnd] Success video prepared. Playing.");

        successVideoPlayer.Play();

        float timer = 0f;

        while (!finished && timer < successVideoFallbackSeconds)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        successVideoPlayer.loopPointReached -= HandleFinished;
        successVideoPlayer.errorReceived -= HandleError;

        successVideoPlayer.Stop();

        ReturnToMainMenu();
    }
    private void AdvanceToNextLevel()
    {
        int level = PlayerPrefs.GetInt(prefsLevelKey, 1);
        PlayerPrefs.SetInt(prefsLevelKey, level + 1);
        PlayerPrefs.Save();
    }
    private void ReturnToMainMenu() => SceneManager.LoadScene(mainMenuSceneName);

    private void SetBlockerVisible(bool isVisible)
    {
        if (blockerRoot != null)
            blockerRoot.SetActive(isVisible);
    }

    private void SetSkipBonusOverlayVisible(bool visible)
    {
        if (skipBonusRoundButton != null)
            skipBonusRoundButton.gameObject.SetActive(visible);
    }

    private void HandleSkipBonusRound()
    {
        if (bonusMovesService != null)
            bonusMovesService.RequestSkip();
    }

    private void Subscribe()
    {
        Unsubscribe();

        board.OnMovesChanged += HandleMovesChanged;
        topHud.OnGoalsCompletionChanged += HandleGoalsCompletionChanged;
    }

    private void Unsubscribe()
    {
        if (board != null)
            board.OnMovesChanged -= HandleMovesChanged;

        if (topHud != null)
            topHud.OnGoalsCompletionChanged -= HandleGoalsCompletionChanged;
    }

    private void HandleMovesChanged(int _)
    {
        RequestEvaluateLevelEndState();
    }

    private void HandleGoalsCompletionChanged(bool completed)
    {
        if (completed)
        {
            QueueSuccessReturnToMainMenu();
            return;
        }

        RequestEvaluateLevelEndState();
    }

    private void RequestEvaluateLevelEndState()
    {
        if (board == null || topHud == null)
            return;

        if (endCheckQueued)
            return;

        Debug.Log("[LevelEnd] RequestEvaluate queued. Waiting idle...");
        endCheckQueued = true;

        // If board is still resolving cascades (fall/spawn/matches/specials), wait.
        // When it becomes idle, we re-check the conditions and only then show the popup.
        board.RunAfterIdle(() =>
        {
            Debug.Log("[LevelEnd] Board idle -> Evaluate");
            endCheckQueued = false;
            EvaluateAndShowIfEnded();
        });
    }

    private void EvaluateAndShowIfEnded()
    {
        if (board == null || topHud == null)
            return;

        if (failPopupShown || successPopupShown)
            return;

        if (topHud.AreAllGoalsCompleted)
        {
            QueueSuccessReturnToMainMenu();
            return;
        }

        if (board.RemainingMoves <= 0)
        {
            ShowFailPopup();
            return;
        }

        Debug.Log($"[LevelEndSimplePopupController] End check skipped. RemainingMoves={board.RemainingMoves}, GoalsCompleted={topHud.AreAllGoalsCompleted}");
    }

    private void ShowFailPopup()
    {
        Debug.Log("[LevelEnd] ShowFailPopup CALLED");
        if (failPopupShown)
            return;

        failPopupShown = true;
        successPopupShown = false;
        RefreshFailOfferVisuals();

        // Önemli olan popup root'unu değil,
        // LevelEndPopup controller objesini en üste almak.
        transform.SetAsLastSibling();

        if (failPopupRoot != null)
        {
            failPopupRoot.SetActive(true);
            Debug.Log("[LevelEnd] fail popup set active true");
        }
        else
        {
            Debug.LogError("[LevelEndSimplePopupController] failPopupRoot is NULL. Fail popup cannot be shown.");
        }

        if (successPopupRoot != null)
            successPopupRoot.SetActive(false);

        SetBlockerVisible(true);
    }

    private void ShowSuccessPopup()
    {
        if (successPopupShown)
            return;

        successPopupShown = true;
        failPopupShown = false;

        int stars = CalculateStars();
        int coins = CalculateCoins();
        int score = CalculateScore();

        ApplyRewardVisuals(stars, coins, score);
        SaveRewards(stars, coins);
        AdvanceToNextLevel();

        transform.SetAsLastSibling();

        if (successVideoRoot != null)
            successVideoRoot.SetActive(false);

        if (successPopupRoot != null)
        {
            successPopupRoot.SetActive(true);
            successPopupRoot.transform.SetAsLastSibling();
        }
        if (failPopupRoot != null)
            failPopupRoot.SetActive(false);

        SetBlockerVisible(true);

        Debug.Log($"[LevelEnd] Success — Yıldız: {stars}, Coin: {coins}, Score: {score}");
    }

    private int CalculateStars()
    {
        if (board == null || board.ActiveLevelData == null)
            return 1;

        int totalMoves = board.ActiveLevelData.moves;
        if (totalMoves <= 0)
            return 1;

        float ratio = (float)_movesAtWin / totalMoves;

        if (ratio >= star3Ratio) return 3;
        if (ratio >= star2Ratio) return 2;
        return 1;
    }

    private int CalculateCoins()
    {
        return baseCoins + _movesAtWin * coinsPerRemainingMove;
    }

    private int CalculateScore()
    {
        return 1000 + _movesAtWin * 250;
    }

    private void ApplyRewardVisuals(int stars, int coins, int score)
    {
        int level = PlayerPrefs.GetInt(prefsLevelKey, 1);

        if (successTitleText != null)
            successTitleText.text = LocalizedFormat("level_end_success_title_level", "Seviye {0}", level);

        if (successContinueText != null)
            successContinueText.text = LocalizedText("level_end_continue", "Devam Et");

        if (scoreLabelText != null)
            scoreLabelText.text = LocalizedText("level_end_score_label", "Puan:");

        if (scoreValueText != null)
            scoreValueText.text = score.ToString("N0");

        // Yıldızlar
        if (starImages != null)
        {
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null) continue;

                bool filled = i < stars;

                if (filled && starFilledSprite != null)
                    starImages[i].sprite = starFilledSprite;
                else if (!filled && starEmptySprite != null)
                    starImages[i].sprite = starEmptySprite;

                var c = starImages[i].color;
                c.a = filled ? 1f : 0.35f;
                starImages[i].color = c;
            }
        }

        // Para
        if (coinsEarnedText != null)
            coinsEarnedText.text = coinsPrefix + coins + coinsSuffix;

        if (successDescriptionText != null && scoreValueText == null)
            successDescriptionText.text = $"{LocalizedText("level_end_score_label", "Puan:")} {score:N0}";

        ApplyGoalVisuals();
    }

    private void SaveRewards(int stars, int coins)
    {
        int level = PlayerPrefs.GetInt(prefsLevelKey, 1);
        PlayerWallet.SetLevelStars(level, stars);
        PlayerWallet.AddCoins(coins);
    }

    private void HandleBuyMovesClicked()
    {
        if (board == null)
            return;

        ResolveCurrentFailOffer();

        if (!PlayerWallet.SpendCoins(currentCost))
        {
            Debug.LogWarning($"[LevelEnd] Extra move purchase failed. RequiredCoins={currentCost}, PlayerCoins={PlayerWallet.Coins}");
            return;
        }

        board.AddMoves(currentOfferAmount);
        extraMoveOfferAttempt++;

        failPopupShown = false;
        HideAllPopups();

        // Force full board sync to prevent any accumulated drift after popup
        board.ForceFullBoardSync();
    }

    private void RefreshPopupCopy()
    {
        RefreshFailOfferVisuals();

        if (successContinueText != null)
            successContinueText.text = LocalizedText("level_end_continue", "Devam Et");

        if (scoreLabelText != null)
            scoreLabelText.text = LocalizedText("level_end_score_label", "Puan:");
    }

    private void RegisterButtonListeners()
    {
        if (buyMovesButton != null)
            buyMovesButton.onClick.AddListener(HandleBuyMovesClicked);
        if (failCloseButton != null)
            failCloseButton.onClick.AddListener(HandleFailCloseClicked);
        if (successCloseButton != null)
            successCloseButton.onClick.AddListener(HandleSuccessCloseClicked);
        if (successContinueButton != null)
            successContinueButton.onClick.AddListener(HandleSuccessCloseClicked);
        if (skipBonusRoundButton != null)
            skipBonusRoundButton.onClick.AddListener(HandleSkipBonusRound);
    }

    private void UnregisterButtonListeners()
    {
        if (buyMovesButton != null)
            buyMovesButton.onClick.RemoveListener(HandleBuyMovesClicked);
        if (failCloseButton != null)
            failCloseButton.onClick.RemoveListener(HandleFailCloseClicked);
        if (successCloseButton != null)
            successCloseButton.onClick.RemoveListener(HandleSuccessCloseClicked);
        if (successContinueButton != null)
            successContinueButton.onClick.RemoveListener(HandleSuccessCloseClicked);
        if (skipBonusRoundButton != null)
            skipBonusRoundButton.onClick.RemoveListener(HandleSkipBonusRound);
    }

    private void ResolveCurrentFailOffer()
    {
        int safeAttempt = Mathf.Max(0, extraMoveOfferAttempt);
        int offerIndex = 0;

        if (extraMoveOfferAmounts != null && extraMoveOfferAmounts.Length > 0)
            offerIndex = Mathf.Min(safeAttempt, extraMoveOfferAmounts.Length - 1);

        currentOfferAmount = extraMoveOfferAmounts != null && extraMoveOfferAmounts.Length > 0
            ? Mathf.Max(1, extraMoveOfferAmounts[offerIndex])
            : 5;

        float multiplier = Mathf.Max(0f, extraMoveCostMultiplier);
        currentCost = Mathf.RoundToInt(baseExtraMovesCost * Mathf.Pow(multiplier, safeAttempt));
        currentCost = Mathf.Max(0, currentCost);
    }

    private void RefreshFailOfferVisuals()
    {
        ResolveCurrentFailOffer();

        if (failTitleText != null)
            failTitleText.text = LocalizedText("level_end_fail_title", "Hamle Kalmadı!");

        string message = LocalizedFormat("level_end_fail_extra_moves_text", "{0} hamle ekleyerek devam et", currentOfferAmount);
        if (failMessageText != null)
            failMessageText.text = message;

        if (failDescriptionText != null && failDescriptionText != failMessageText)
            failDescriptionText.text = message;

        if (failContinueText != null)
            failContinueText.text = LocalizedFormat("level_end_fail_continue_cost", "Devam Et {0}", currentCost);

        if (extraMovesIcon != null)
        {
            var sprite = ResolveExtraMoveOfferSprite();
            if (sprite != null)
                extraMovesIcon.sprite = sprite;

            extraMovesIcon.enabled = sprite != null;
        }
    }

    private Sprite ResolveExtraMoveOfferSprite()
    {
        if (extraMoveOfferSprites == null || extraMoveOfferSprites.Length == 0)
            return null;

        int index = extraMoveOfferAmounts != null && extraMoveOfferAmounts.Length > 0
            ? Mathf.Min(Mathf.Max(0, extraMoveOfferAttempt), extraMoveOfferAmounts.Length - 1)
            : 0;

        index = Mathf.Min(index, extraMoveOfferSprites.Length - 1);
        return extraMoveOfferSprites[index];
    }

    private void ApplyGoalVisuals()
    {
        EnsureGoalResultArrays();

        var levelData = board != null ? board.ActiveLevelData : null;
        var goals = levelData != null ? levelData.goals : null;

        for (int i = 0; i < 4; i++)
        {
            var goal = goals != null && i < goals.Length ? goals[i] : null;
            var icon = ResolveGoalResultIcon(goal);

            if (goal == null || icon == null)
            {
                SetGoalResultVisible(i, false);
                continue;
            }

            SetGoalResultVisible(i, true);

            if (goalIconImages[i] != null)
            {
                goalIconImages[i].sprite = icon;
                goalIconImages[i].enabled = true;
            }

            if (goalCheckMarks[i] != null)
                goalCheckMarks[i].SetActive(true);
        }
    }

    private Sprite ResolveGoalResultIcon(LevelGoalDefinition goal)
    {
        if (goal == null)
            return null;

        if (goal.iconOverride != null)
            return goal.iconOverride;

        if (goal.targetType == LevelGoalTargetType.Tile)
        {
            var sprite = tileIconLibrary != null ? tileIconLibrary.Get(goal.tileType) : null;
            return sprite != null ? sprite : board != null ? board.GetIcon(goal.tileType) : null;
        }

        if (goal.targetType == LevelGoalTargetType.Obstacle)
        {
            var levelData = board != null ? board.ActiveLevelData : null;
            var obstacleDef = levelData != null && levelData.obstacleLibrary != null
                ? levelData.obstacleLibrary.Get(goal.obstacleId)
                : null;

            return obstacleDef != null ? obstacleDef.GetPreviewSprite() : null;
        }

        return null;
    }

    private void SetGoalResultVisible(int index, bool visible)
    {
        if (index < 0 || index >= 4)
            return;

        if (goalResultRoots != null && index < goalResultRoots.Length && goalResultRoots[index] != null)
        {
            goalResultRoots[index].SetActive(visible);
            return;
        }

        if (goalIconImages != null && index < goalIconImages.Length && goalIconImages[index] != null)
            goalIconImages[index].gameObject.SetActive(visible);
        if (goalCheckMarks != null && index < goalCheckMarks.Length && goalCheckMarks[index] != null)
            goalCheckMarks[index].SetActive(visible);
    }

    private void EnsureStarImageArray()
    {
        if (starImages == null || starImages.Length < 3)
            Array.Resize(ref starImages, 3);
    }

    private void EnsureGoalResultArrays()
    {
        if (goalResultRoots == null || goalResultRoots.Length < 4)
            Array.Resize(ref goalResultRoots, 4);
        if (goalIconImages == null || goalIconImages.Length < 4)
            Array.Resize(ref goalIconImages, 4);
        if (goalCheckMarks == null || goalCheckMarks.Length < 4)
            Array.Resize(ref goalCheckMarks, 4);
    }

    private static T FindComponent<T>(Transform root, string path) where T : Component
    {
        if (root == null)
            return null;

        var child = root.Find(path);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static string LocalizedText(string key, string fallback)
    {
        string value = GameLocalization.Get(key);
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    private static string LocalizedFormat(string key, string fallback, params object[] args)
    {
        string format = LocalizedText(key, fallback);

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public void HideAllPopups()
    {
        if (failPopupRoot != null)
            failPopupRoot.SetActive(false);

        if (successPopupRoot != null)
            successPopupRoot.SetActive(false);

        if (successVideoRoot != null)
            successVideoRoot.SetActive(false);

        SetBlockerVisible(false);
    }
}
