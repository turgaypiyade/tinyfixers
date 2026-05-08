using System;
using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private ChapterThemeApplier chapterThemeApplier;

    [Header("Success Animation (image sequence - takes priority over video)")]
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

    [Header("Main Screen Dim")]
    [SerializeField] private CanvasGroup mainScreenCanvasGroup;
    [SerializeField] private Image dimImage;
    [SerializeField, Range(0f, 1f)] private float dimmedMainAlpha = 0.45f;
    [SerializeField, Min(0f)] private float dimTransitionDuration = 0.22f;

    [Header("Level End Popup Images")]
    [SerializeField] private Image failPopupImage;
    [SerializeField] private Image successPopupImage;
    [SerializeField] private Image failContinueButtonImage;
    [SerializeField] private Image successContinueButtonImage;
    [SerializeField] private Image failCloseButtonImage;
    [SerializeField] private Image successCloseButtonImage;

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

    [Header("Success - Star Images")]
    [SerializeField] private Image[] starImages;
    [SerializeField] private Sprite starFilledSprite;
    [SerializeField] private Sprite starEmptySprite;
    [SerializeField, Min(0f)] private float starRevealDelay = 0.4f;

    [Header("Success Content")]
    [SerializeField] private TMP_Text successTitleText;
    [SerializeField] private TMP_Text successContinueText;
    [SerializeField] private TMP_Text scoreLabelText;
    [SerializeField] private TMP_Text scoreValueText;
    [SerializeField] private GameObject[] goalResultRoots = new GameObject[4];
    [SerializeField] private Image[] goalIconImages = new Image[4];
    [SerializeField] private Image[] goalCheckMarkImages = new Image[4];
    [SerializeField] private Sprite goalCheckMarkSprite;

    [Header("Success - Coin Text")]
    [SerializeField] private TMP_Text coinsEarnedText;
    [SerializeField] private string coinsPrefix = "+";
    [SerializeField] private string coinsSuffix = " coin";

    [Header("Star Thresholds (remaining moves / starting moves)")]
    [Range(0f, 1f)] [SerializeField] private float star3Ratio = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float star2Ratio = 0.2f;

    [Header("Coin Reward")]
    [SerializeField] private int baseCoins = 100;
    [SerializeField] private int coinsPerRemainingMove = 20;

    [Header("Progression")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string prefsLevelKey = "current_level";

    private bool failPopupShown;
    private bool successPopupShown;
    private bool successReturnQueued;
    private int _movesAtWin;
    private int currentOfferAmount;
    private int currentCost;
    private bool endCheckQueued;
    private Coroutine starRevealRoutine;
    private Coroutine mainScreenDimRoutine;
    private readonly List<CanvasGroup> mainScreenDimTargets = new();

    private void ResolveSerializedReferences()
    {
        if (chapterThemeApplier == null)
            chapterThemeApplier = FindFirstObjectByType<ChapterThemeApplier>(FindObjectsInactive.Include);

        var failRootByName = transform.Find("FailPopupRoot");
        if (failRootByName != null)
            failPopupRoot = failRootByName.gameObject;

        var successRootByName = transform.Find("SuccessPopupRoot");
        if (successRootByName != null)
            successPopupRoot = successRootByName.gameObject;

        var blockerByName = transform.Find("Blocker") ?? transform.Find("blocker");
        if (blockerByName != null)
            blockerRoot = blockerByName.gameObject;

        if (blockerRoot != null && dimImage == null)
            dimImage = blockerRoot.GetComponent<Image>() ?? blockerRoot.GetComponentInChildren<Image>(true);

        ResolveMainScreenDimTargets();

        if (failPopupRoot != null)
        {
            failPopupImage = FindComponent<Image>(failPopupRoot.transform, "FailPopupImage") ?? failPopupImage;

            var failContinue = failPopupRoot.transform.Find("UI/BtnContinue");
            if (failContinue != null)
                buyMovesButton = failContinue.GetComponent<Button>();

            var failClose = failPopupRoot.transform.Find("UI/BtnClose");
            if (failClose != null)
                failCloseButton = failClose.GetComponent<Button>();

            failContinueButtonImage = FindComponent<Image>(failPopupRoot.transform, "UI/BtnContinue") ?? failContinueButtonImage;
            failCloseButtonImage = FindComponent<Image>(failPopupRoot.transform, "UI/BtnClose") ?? failCloseButtonImage;
            failTitleText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/FailTitleText") ?? failTitleText;
            extraMovesIcon = FindComponent<Image>(failPopupRoot.transform, "UI/ExtraMovesIcon") ?? extraMovesIcon;
            failMessageText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/FailMessageText") ?? failMessageText;
            failContinueText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/BtnContinue/BtnContinueText") ?? failContinueText;

            if (failDescriptionText == null)
                failDescriptionText = failMessageText;
        }

        if (successPopupRoot != null)
        {
            successPopupImage = FindComponent<Image>(successPopupRoot.transform, "SuccessPopupImage") ?? successPopupImage;

            var successClose = successPopupRoot.transform.Find("UIS/BtnSClose");
            if (successClose != null)
                successCloseButton = successClose.GetComponent<Button>();

            var successContinue = successPopupRoot.transform.Find("UIS/BtnsContinue");
            if (successContinue != null)
                successContinueButton = successContinue.GetComponent<Button>();

            successContinueButtonImage = FindComponent<Image>(successPopupRoot.transform, "UIS/BtnsContinue") ?? successContinueButtonImage;
            successCloseButtonImage = FindComponent<Image>(successPopupRoot.transform, "UIS/BtnSClose") ?? successCloseButtonImage;
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
                goalCheckMarkImages[i] = FindComponent<Image>(successPopupRoot.transform, $"{resultPath}/CheckMark") ?? goalCheckMarkImages[i];
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
        ApplyChapterThemeVisuals();
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
        StopMainScreenDimRoutine();
        SetMainScreenAlpha(1f);
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
        ApplyChapterThemeVisuals();
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
        yield return null;

        const int requiredStableFrames = 3;
        int stableFrames = 0;

        while (board != null && stableFrames < requiredStableFrames)
        {
            bool boardStillWorking = board.IsBusy || board.ActiveBackgroundJobs > 0;
            stableFrames = boardStillWorking ? 0 : stableFrames + 1;
            yield return null;
        }

        while (board != null && (board.IsBusy || board.ActiveBackgroundJobs > 0))
            yield return null;

        _movesAtWin = board != null ? board.RemainingMoves : 0;

        if (bonusMovesService != null && board != null && board.RemainingMoves > 0)
        {
            board.SetInputLocked(true);
            SetSkipBonusOverlayVisible(true);
            yield return StartCoroutine(bonusMovesService.RunBonusRound());
            SetSkipBonusOverlayVisible(false);
        }

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
        if (successAnimationPlayer != null)
        {
            SetBlockerVisible(true);
            Debug.Log("[LevelEnd] successAnimationPlayer assigned; animation starts.");
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

        void HandleFinished(VideoPlayer player) => finished = true;
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
        if (isVisible)
            ApplyDimImageColor();

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

    private void HandleMovesChanged(int _) => RequestEvaluateLevelEndState();

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
        ApplyChapterThemeVisuals();
        RefreshFailOfferVisuals();
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

        SetMainScreenDimmed(true);
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

        ApplyChapterThemeVisuals();
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

        SetMainScreenDimmed(true);
        SetBlockerVisible(true);
        ApplyRewardVisuals(stars, coins, score);
        SaveRewards(stars, coins);
        AdvanceToNextLevel();

        Debug.Log($"[LevelEnd] Success - Stars: {stars}, Coin: {coins}, Score: {score}");
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

    private int CalculateCoins() => baseCoins + _movesAtWin * coinsPerRemainingMove;
    private int CalculateScore() => 1000 + _movesAtWin * 250;

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

        PlayStarReveal(stars);

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
            failTitleText.text = LocalizedText("level_end_fail_title", "Hamle Kalmadi!");

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

    private void ApplyChapterThemeVisuals()
    {
        if (chapterThemeApplier == null)
            chapterThemeApplier = FindFirstObjectByType<ChapterThemeApplier>(FindObjectsInactive.Include);

        ChapterTheme theme = chapterThemeApplier != null ? chapterThemeApplier.CurrentTheme : null;
        if (theme == null)
            return;

        SetImageSpriteIfPresent(failPopupImage, theme.levelEndPopupBackground);
        SetImageSpriteIfPresent(successPopupImage, theme.levelEndPopupBackground);
        SetImageSpriteIfPresent(failContinueButtonImage, theme.levelEndContinueButton);
        SetImageSpriteIfPresent(successContinueButtonImage, theme.levelEndContinueButton);
        SetImageSpriteIfPresent(failCloseButtonImage, theme.levelEndCloseButton);
        SetImageSpriteIfPresent(successCloseButtonImage, theme.levelEndCloseButton);

        ApplyDimImageColor(theme);

        if (theme.levelEndStarFilled != null)
            starFilledSprite = theme.levelEndStarFilled;
        if (theme.levelEndStarEmpty != null)
            starEmptySprite = theme.levelEndStarEmpty;
        if (theme.levelEndGoalCheckMark != null)
            goalCheckMarkSprite = theme.levelEndGoalCheckMark;

        if (theme.levelEndExtraMoves5 != null || theme.levelEndExtraMoves10 != null || theme.levelEndExtraMoves15 != null)
        {
            EnsureExtraMoveOfferSpriteArray();

            if (theme.levelEndExtraMoves5 != null)
                extraMoveOfferSprites[0] = theme.levelEndExtraMoves5;
            if (theme.levelEndExtraMoves10 != null)
                extraMoveOfferSprites[1] = theme.levelEndExtraMoves10;
            if (theme.levelEndExtraMoves15 != null)
                extraMoveOfferSprites[2] = theme.levelEndExtraMoves15;
        }
    }

    private static void SetImageSpriteIfPresent(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.enabled = true;
    }

    private void ResolveMainScreenDimTargets()
    {
        mainScreenDimTargets.Clear();

        if (mainScreenCanvasGroup != null)
            AddMainScreenDimTarget(mainScreenCanvasGroup);

        Transform canvasRoot = transform.parent;
        if (canvasRoot == null)
            return;

        // Mirrors PreLevelSpecialPopupController's mainScreenCanvasGroup setup.
        // In 01_Game the visual screen is split between GameBG and SafeArea.
        AddMainScreenDimTarget(FindOrCreateCanvasGroup(canvasRoot.Find("GameBG")));
        AddMainScreenDimTarget(FindOrCreateCanvasGroup(canvasRoot.Find("BackgroundImage")));
        AddMainScreenDimTarget(FindOrCreateCanvasGroup(canvasRoot.Find("SafeArea")));
    }

    private void AddMainScreenDimTarget(CanvasGroup canvasGroup)
    {
        if (canvasGroup == null || mainScreenDimTargets.Contains(canvasGroup))
            return;

        mainScreenDimTargets.Add(canvasGroup);
    }

    private static CanvasGroup FindOrCreateCanvasGroup(Transform target)
    {
        if (target == null)
            return null;

        var canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.gameObject.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private void SetMainScreenDimmed(bool dimmed)
    {
        ResolveMainScreenDimTargets();
        StopMainScreenDimRoutine();

        float targetAlpha = dimmed ? Mathf.Clamp01(dimmedMainAlpha) : 1f;
        if (!isActiveAndEnabled || dimTransitionDuration <= 0f)
        {
            SetMainScreenAlpha(targetAlpha);
            return;
        }

        mainScreenDimRoutine = StartCoroutine(CoFadeMainScreenAlpha(targetAlpha));
    }

    private IEnumerator CoFadeMainScreenAlpha(float targetAlpha)
    {
        int count = mainScreenDimTargets.Count;
        if (count == 0)
        {
            mainScreenDimRoutine = null;
            yield break;
        }

        var targets = mainScreenDimTargets.ToArray();
        var startAlphas = new float[targets.Length];
        for (int i = 0; i < targets.Length; i++)
            startAlphas[i] = targets[i] != null ? targets[i].alpha : 1f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, dimTransitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOut(t);

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                    continue;

                targets[i].alpha = Mathf.Lerp(startAlphas[i], targetAlpha, eased);
            }

            yield return null;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].alpha = targetAlpha;
        }

        mainScreenDimRoutine = null;
    }

    private void SetMainScreenAlpha(float alpha)
    {
        ResolveMainScreenDimTargets();
        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < mainScreenDimTargets.Count; i++)
        {
            if (mainScreenDimTargets[i] != null)
                mainScreenDimTargets[i].alpha = alpha;
        }
    }

    private void StopMainScreenDimRoutine()
    {
        if (mainScreenDimRoutine == null)
            return;

        StopCoroutine(mainScreenDimRoutine);
        mainScreenDimRoutine = null;
    }

    private void ApplyDimImageColor(ChapterTheme theme = null)
    {
        if (dimImage == null)
            return;

        if (theme == null && chapterThemeApplier != null)
            theme = chapterThemeApplier.CurrentTheme;

        Color color = theme != null ? theme.preLevelDimColor : new Color(0f, 0f, 0f, 0.55f);
        dimImage.color = color;
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    private void EnsureExtraMoveOfferSpriteArray()
    {
        if (extraMoveOfferSprites == null || extraMoveOfferSprites.Length < 3)
            Array.Resize(ref extraMoveOfferSprites, 3);
    }

    private void ApplyGoalVisuals()
    {
        EnsureGoalResultArrays();

        var levelData = board != null ? board.ActiveLevelData : null;
        var goals = levelData != null ? levelData.goals : null;
        int slotIndex = 0;

        for (int i = 0; i < 4; i++)
            SetGoalResultVisible(i, false);

        if (goals == null)
            return;

        for (int i = 0; i < goals.Length && slotIndex < 4; i++)
        {
            var goal = goals[i];
            var icon = ResolveGoalResultIcon(goal);

            if (goal == null || goal.amount <= 0 || icon == null)
                continue;

            SetGoalResultVisible(slotIndex, true);

            if (goalIconImages[slotIndex] != null)
            {
                goalIconImages[slotIndex].sprite = icon;
                goalIconImages[slotIndex].enabled = true;
            }

            if (goalCheckMarkImages[slotIndex] != null)
            {
                Sprite checkSprite = goalCheckMarkSprite != null
                    ? goalCheckMarkSprite
                    : goalCheckMarkImages[slotIndex].sprite;

                goalCheckMarkImages[slotIndex].sprite = checkSprite;
                goalCheckMarkImages[slotIndex].enabled = checkSprite != null;
                goalCheckMarkImages[slotIndex].gameObject.SetActive(checkSprite != null);
            }

            slotIndex++;
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
        if (goalCheckMarkImages != null && index < goalCheckMarkImages.Length && goalCheckMarkImages[index] != null)
            goalCheckMarkImages[index].gameObject.SetActive(visible);
    }

    private void PlayStarReveal(int earnedStars)
    {
        if (starRevealRoutine != null)
            StopCoroutine(starRevealRoutine);

        starRevealRoutine = StartCoroutine(RevealStarsRoutine(earnedStars));
    }

    private IEnumerator RevealStarsRoutine(int earnedStars)
    {
        if (starImages == null)
            yield break;

        earnedStars = Mathf.Clamp(earnedStars, 0, starImages.Length);

        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null)
                continue;

            if (starEmptySprite != null)
                starImages[i].sprite = starEmptySprite;

            var color = starImages[i].color;
            color.a = 0.35f;
            starImages[i].color = color;
            starImages[i].transform.localScale = Vector3.one;
        }

        for (int i = 0; i < earnedStars; i++)
        {
            if (starImages[i] == null)
                continue;

            if (i > 0 && starRevealDelay > 0f)
                yield return new WaitForSecondsRealtime(starRevealDelay);

            if (starFilledSprite != null)
                starImages[i].sprite = starFilledSprite;

            var color = starImages[i].color;
            color.a = 1f;
            starImages[i].color = color;

            yield return StartCoroutine(PunchStarRoutine(starImages[i].transform));
        }

        starRevealRoutine = null;
    }

    private IEnumerator PunchStarRoutine(Transform target)
    {
        if (target == null)
            yield break;

        const float duration = 0.18f;
        const float punchScale = 1.18f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float scale = normalized < 0.5f
                ? Mathf.Lerp(1f, punchScale, normalized / 0.5f)
                : Mathf.Lerp(punchScale, 1f, (normalized - 0.5f) / 0.5f);

            target.localScale = Vector3.one * scale;
            yield return null;
        }

        target.localScale = Vector3.one;
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
        if (goalCheckMarkImages == null || goalCheckMarkImages.Length < 4)
            Array.Resize(ref goalCheckMarkImages, 4);
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
        if (starRevealRoutine != null)
        {
            StopCoroutine(starRevealRoutine);
            starRevealRoutine = null;
        }

        if (failPopupRoot != null)
            failPopupRoot.SetActive(false);

        if (successPopupRoot != null)
            successPopupRoot.SetActive(false);

        if (successVideoRoot != null)
            successVideoRoot.SetActive(false);

        SetMainScreenDimmed(false);
        SetBlockerVisible(false);
    }
}
