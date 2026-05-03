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
    [Header("Success Animation (image sequence — takes priority over video)")]
    [SerializeField] private SuccessAnimationPlayer successAnimationPlayer;

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
    [SerializeField] private int extraMovesAmount = 5;
    [SerializeField] private int extraMovesCost = 900;

    [Header("Success — Yıldız Görselleri (sırayla 1., 2., 3. yıldız)")]
    [SerializeField] private UnityEngine.UI.Image[] starImages;
    [SerializeField] private Sprite starFilledSprite;
    [SerializeField] private Sprite starEmptySprite;

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
        }

        if (successPopupRoot != null)
        {
            var successClose = successPopupRoot.transform.Find("UIS/BtnSClose");
            if (successClose != null)
                successCloseButton = successClose.GetComponent<Button>();

            var successContinue = successPopupRoot.transform.Find("UIS/BtnsContinue");
            if (successContinue != null)
                successContinueButton = successContinue.GetComponent<Button>();
        }
        if (successVideoRoot != null && successVideoPlayer == null)
            successVideoPlayer = successVideoRoot.GetComponent<VideoPlayer>();
    }

    private void OnEnable()
    {
        failPopupShown = false;
        successPopupShown = false;
        HideAllPopups();

        StartCoroutine(InitializeWhenReady());

        if (buyMovesButton != null)
            buyMovesButton.onClick.AddListener(HandleBuyMovesClicked);
        if (failCloseButton != null)
            failCloseButton.onClick.AddListener(HandleFailCloseClicked);
        if (successCloseButton != null)
            successCloseButton.onClick.AddListener(HandleSuccessCloseClicked);

        if (successContinueButton != null)
            successContinueButton.onClick.AddListener(HandleSuccessCloseClicked);

    }

    private void OnDisable()
    {
        Unsubscribe();

        if (buyMovesButton != null)
            buyMovesButton.onClick.RemoveListener(HandleBuyMovesClicked);
        if (failCloseButton != null)
            failCloseButton.onClick.RemoveListener(HandleFailCloseClicked);
        if (successCloseButton != null)
            successCloseButton.onClick.RemoveListener(HandleSuccessCloseClicked);
        if (successContinueButton != null)
            successContinueButton.onClick.RemoveListener(HandleSuccessCloseClicked);
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

        Subscribe();
        RefreshPopupCopy();
        ResolveSerializedReferences();
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
        QueueSuccessReturnToMainMenu();
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

        // Kalan moves varsa bonus round: her move için random normal tile'a LineV/LineH at.
        if (bonusMovesService != null && board != null && board.RemainingMoves > 0)
            yield return StartCoroutine(bonusMovesService.RunBonusRound());

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

        Debug.Log("[LevelEnd] Board settled. Completing success.");

        CompleteSuccessAndReturnToMainMenu();
    }
    private void CompleteSuccessAndReturnToMainMenu()
    {
        if (successPopupShown)
            return;

        successPopupShown = true;
        failPopupShown = false;

        int stars = CalculateStars();
        int coins = CalculateCoins();

        SaveRewards(stars, coins);
        AdvanceToNextLevel();

        HideAllPopups();

        Debug.Log($"[LevelEnd] Success completed. Stars={stars}, Coins={coins}. Playing success video before loading={mainMenuSceneName}");

        StartCoroutine(PlaySuccessVideoThenReturnToMainMenu());
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

        if (successPopupRoot != null)
        {
            successPopupRoot.SetActive(true);
            successPopupRoot.transform.SetAsLastSibling();
        }
        if (failPopupRoot != null)
            failPopupRoot.SetActive(false);

        SetBlockerVisible(true);

        int stars = CalculateStars();
        int coins = CalculateCoins();

        ApplyRewardVisuals(stars, coins);
        SaveRewards(stars, coins);

        Debug.Log($"[LevelEnd] Success — Yıldız: {stars}, Coin: {coins}");
    }

    private int CalculateStars()
    {
        if (board == null || board.ActiveLevelData == null)
            return 1;

        int totalMoves = board.ActiveLevelData.moves;
        if (totalMoves <= 0)
            return 1;

        float ratio = (float)board.RemainingMoves / totalMoves;

        if (ratio >= star3Ratio) return 3;
        if (ratio >= star2Ratio) return 2;
        return 1;
    }

    private int CalculateCoins()
    {
        int remaining = board != null ? board.RemainingMoves : 0;
        return baseCoins + remaining * coinsPerRemainingMove;
    }

    private void ApplyRewardVisuals(int stars, int coins)
    {
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

        // Ekonomi entegrasyonu henüz yok. Şimdilik direkt +hamle veriyoruz.
        board.AddMoves(extraMovesAmount);

        failPopupShown = false;
        HideAllPopups();

        // Force full board sync to prevent any accumulated drift after popup
        board.ForceFullBoardSync();
    }

    private void RefreshPopupCopy()
    {
        if (failDescriptionText != null)
            failDescriptionText.text = $"Hedefe ulaşamadın. {extraMovesCost} coin ile +{extraMovesAmount} hamle alıp devam edebilirsin.";

        if (successDescriptionText != null)
            successDescriptionText.text = "Hedefler tamamlandı! Kalan hamleler bonusa dönüşecek.";
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
