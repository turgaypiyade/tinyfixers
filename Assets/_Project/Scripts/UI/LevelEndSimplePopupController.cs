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
    private const float ExtraMovesDigitSlant = 0.02f;
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [SerializeField] private BonusMovesService bonusMovesService;
    [SerializeField] private TileIconLibrary tileIconLibrary;
    [SerializeField] private ChapterThemeApplier chapterThemeApplier;

    [Header("Level Completion Logo Animation")]
    [SerializeField] private LevelCompletionLogoAnimation levelCompletionLogoAnimation;

    [Header("Success Animation (image sequence - takes priority over video)")]
    [SerializeField] private SuccessAnimationPlayer successAnimationPlayer;

    [Header("Bonus Round Skip")]
    [Tooltip("Transparent fullscreen button shown during the bonus round. Tap once to hard-skip remaining-moves effects and open success popup.")]
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

    [Header("Fail Offer Badge")]
    [SerializeField] private TMP_Text extraMovesAmountText;
    [SerializeField] private TMP_Text extraMovesFlyAmountText;
    [SerializeField] private TMP_FontAsset extraMovesAmountFont;
    [SerializeField] private Material extraMovesAmountMaterial;
    [SerializeField, Min(1f)] private float extraMovesAmountFontSize = 112f;
    [SerializeField, Min(0.05f)] private float extraMovesFlyDuration = 0.55f;
    [SerializeField] private Vector2 extraMovesFlyStartOffset = new Vector2(0f, 760f);

    [Header("Fail Wallet Balance")]
    [SerializeField] private GameObject failWalletBalanceRoot;
    [SerializeField] private Image failWalletBalanceBackground;
    [SerializeField] private Image failWalletBalanceIcon;
    [SerializeField] private TMP_Text failWalletBalanceText;
    [SerializeField] private Sprite failWalletBalanceBackgroundSprite;
    [SerializeField] private Sprite failWalletBalanceIconSprite;
    [SerializeField] private TMP_FontAsset failWalletBalanceFont;
    [SerializeField] private Material failWalletBalanceMaterial;
    [SerializeField] private Vector2 failWalletBalancePosition = new Vector2(120f, 585f);
    [SerializeField, Min(1f)] private float failWalletBalanceBackgroundHeight = 76f;
    [SerializeField, Min(1f)] private float failWalletBalanceIconSize = 84f;
    [SerializeField] private float failWalletBalanceIconOverlap = 18f;

    [Header("Fail Content")]
    [SerializeField] private TMP_Text failTitleText;
    [SerializeField] private Image extraMovesIcon;
    [SerializeField] private TMP_Text failMessageText;
    [SerializeField] private TMP_Text failContinueText;
    [Tooltip("Opsiyonel (eski/metin fallback): ikon satırları atanmamışsa bu TMP'ye birleşik metin yazılır.")]
    [SerializeField] private TMP_Text failEventLossWarningText;

    [Header("Fail - Loss Icons (vazgeçince kaybedilecekler)")]
    [Tooltip("Tüm kayıp özeti paneli. Kaybedilecek bir şey yoksa gizlenir.")]
    [SerializeField] private GameObject lossSummaryRoot;
    [Tooltip("Tek kayıp satırı prefab'ı (LossRowView: ikon + miktar). Her kayıp için biri instantiate edilir.")]
    [SerializeField] private LossRowView lossRowPrefab;
    [Tooltip("Satırların ekleneceği container (VerticalLayoutGroup önerilir).")]
    [SerializeField] private Transform lossRowContainer;
    [Tooltip("Altın ikonu sprite'ı (base reward; event değil, UI'ın kendi config'i).")]
    [SerializeField] private Sprite coinIcon;
    // NOT: Event kaybı ikonları artık her event'in KENDİ config'inde (Safari→SafariConfig,
    // Streak→StreakBoosterConfig). UI onları tanımaz; provider'lar kendi config'inden okur.
    [Tooltip("Kayıp satırı sağ-alt rozeti: gerçekleşmiş (kazanılmış) öğe için checkmark.")]
    [SerializeField] private Sprite achievedIcon;
    [Tooltip("Kayıp satırı sağ-alt rozeti: gerçekleşmemiş öğe için cancel ikonu.")]
    [SerializeField] private Sprite notAchievedIcon;
    [Tooltip("Kayıp panelinin başlığı ('Şunları kaybedeceksin').")]
    [SerializeField] private TMP_Text lossTitleText;
    [Tooltip("İlk cancel'da sağdan kayıp gelen kayıp paneli (RectTransform). Aşama 1'de gizli.")]
    [SerializeField] private RectTransform lossSlidePanel;
    [SerializeField, Min(0.05f)] private float lossSlideDuration = 0.35f;
    private readonly System.Collections.Generic.List<GameObject> _lossRows = new();
    private bool failSecondStage;   // false: ilk cancel kayıpları kaydırır; true: ikinci cancel → ana menü
    private bool _hasLosses;        // gösterilecek kayıp var mı (RefreshFailLossSummary set eder)

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
    [Range(0f, 1f)][SerializeField] private float star3Ratio = 0.5f;
    [Range(0f, 1f)][SerializeField] private float star2Ratio = 0.2f;

    [Header("Coin Reward")]
    [Tooltip("Fallback only when ActiveLevelData is unavailable. Normal rewards come from LevelData.baseCoinReward.")]
    [Min(0)]
    [SerializeField] private int baseCoins = 100;
    [SerializeField, Range(0f, 2f)] private float remainingMoveBonusRatio = 0.5f;

    [Tooltip("Açık ise level-sonu coin ödülü SABİT (küçük) bir değerdir; baseCoinReward + hamle " +
             "bonusu formülü kullanılmaz. In-game gold'dan ayrıdır.")]
    [SerializeField] private bool useFlatCoinReward = true;
    [Tooltip("useFlatCoinReward açıkken verilecek sabit level-sonu coin.")]
    [SerializeField, Min(0)] private int flatCoinReward = 10;

    [Header("Score (Puan)")]
    [Tooltip("Level kazanınca verilen taban puan. Leaderboard/team metriğine (TotalScore) eklenir.")]
    [SerializeField, Min(0)] private int scoreBase = 100;
    [Tooltip("Artakalan her hamle için eklenen puan. Küçük tut → puan yavaş birikir.")]
    [SerializeField, Min(0)] private int scorePerRemainingMove = 15;
#pragma warning disable 0414
    [Obsolete("Use LevelData.baseCoinReward and remainingMoveBonusRatio instead.")]
    [SerializeField, HideInInspector] private int coinsPerRemainingMove = 20;
#pragma warning restore 0414

    [Header("Progression")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string prefsLevelKey = "current_level";
    [SerializeField] private LevelCatalog levelCatalog;

    [Header("Fail Guard")]
    [Tooltip("Fail popup'ı göstermeden önceki doğrulama beklemesi (sn). Son hamlenin bazı " +
             "geç efektleri (line-sweep beam varışı, obstacle kırılma kuyruğu) board 'idle' " +
             "göründükten az sonra hedef kredisini verebiliyor. Bu sürede hedefler tamamlanırsa " +
             "fail yerine success gösterilir.")]
    [SerializeField, Min(0f)] private float failConfirmGraceSeconds = 0.6f;

    // Hamle 0 olduktan sonra board'a settle/altın toplama için verilen ÜST SÜRE.
    // Bu süre dolduğunda "board hâlâ çalışıyor / pending" olsa bile sonuç ZORLA verilir
    // (hedefler tamam→success, değilse→fail). Aksi halde uçan altın orb'u ActiveBackgroundJobs'ı
    // sürekli >0 tutup EvaluateAfterBoardSettled'ı sonsuz beklemede bırakıyordu → oyun 0 hamleyle
    // devam ediyordu. 5sn meşru altın/cascade için fazlasıyla yeterli.
    [SerializeField, Min(0.5f)] private float levelEndForceTimeoutSeconds = 5f;
    private float levelEndForceDeadlineUnscaled = -1f;
    private bool LevelEndForceDeadlinePassed =>
        levelEndForceDeadlineUnscaled >= 0f && Time.unscaledTime >= levelEndForceDeadlineUnscaled;

    private bool failPopupShown;
    private bool successPopupShown;
    private bool successReturnQueued;
    private int _movesAtWin;
    private int currentOfferAmount;
    private int currentCost;
    private bool endCheckQueued;
    private bool failSettleWaitRunning;
    private bool failConfirmRunning;
    private Coroutine starRevealRoutine;
    private Coroutine mainScreenDimRoutine;
    private Coroutine extraMovesAmountRoutine;
    private int lastDisplayedOfferAmount = -1;
    private readonly List<CanvasGroup> mainScreenDimTargets = new();

    private bool isBonusRoundRunning;
    private bool hardSkipBonusRoundRequested;

    private void ResolveSerializedReferences()
    {
        if (chapterThemeApplier == null)
            chapterThemeApplier = FindFirstObjectByType<ChapterThemeApplier>(FindObjectsInactive.Include);

        if (levelCatalog == null)
        {
            var runtimeSelector = FindFirstObjectByType<LevelRuntimeSelector>(FindObjectsInactive.Include);
            if (runtimeSelector != null)
                levelCatalog = runtimeSelector.Catalog;
        }

        if (levelCompletionLogoAnimation == null)
            levelCompletionLogoAnimation = FindFirstObjectByType<LevelCompletionLogoAnimation>(FindObjectsInactive.Include);

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

        if (skipBonusRoundButton == null)
            skipBonusRoundButton = ResolveSkipBonusButton();

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
            extraMovesAmountText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/ExtraMovesIcon/ExtraMovesAmountText") ?? extraMovesAmountText;
            extraMovesFlyAmountText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/ExtraMovesIcon/ExtraMovesFlyAmountText") ?? extraMovesFlyAmountText;
            failMessageText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/FailMessageText") ?? failMessageText;
            failContinueText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/BtnContinue/BtnContinueText") ?? failContinueText;
            failEventLossWarningText = FindComponent<TMP_Text>(failPopupRoot.transform, "UI/EventLossWarningText") ?? failEventLossWarningText;

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

    private Button ResolveSkipBonusButton()
    {
        Transform canvasRoot = transform.parent != null ? transform.parent : transform;

        Transform skip = canvasRoot.Find("LogoAnimPanel/skipButton")
            ?? canvasRoot.Find("LogoAnimPanel/SkipButton")
            ?? FindDeepChild(canvasRoot, "skipButton")
            ?? FindDeepChild(canvasRoot, "SkipButton");

        return skip != null ? skip.GetComponent<Button>() : null;
    }

    private void OnEnable()
    {
        failPopupShown = false;
        successPopupShown = false;
        successReturnQueued = false;
        failSecondStage = false;
        failSettleWaitRunning = false;
        failConfirmRunning = false;
        isBonusRoundRunning = false;
        hardSkipBonusRoundRequested = false;
        levelEndForceDeadlineUnscaled = -1f;   // yeni seviye: üst-süre saati sıfırlansın
        lastDisplayedOfferAmount = -1;

        ResolveSerializedReferences();
        ApplyChapterThemeVisuals();
        HideAllPopups();
        RegisterButtonListeners();
        PlayerWallet.OnCoinsChanged += HandleWalletCoinsChanged;

        if (skipBonusRoundButton != null)
            skipBonusRoundButton.gameObject.SetActive(false);

        StartCoroutine(InitializeWhenReady());
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnregisterButtonListeners();
        PlayerWallet.OnCoinsChanged -= HandleWalletCoinsChanged;
        StopMainScreenDimRoutine();
        SetMainScreenAlpha(1f);
        isBonusRoundRunning = false;
        hardSkipBonusRoundRequested = false;
    }

    private void Update()
    {
        if (!endCheckQueued || failPopupShown || successPopupShown) return;
        if (board == null || board.RemainingMoves > 0) return;

        // Board already idle but OnBecameIdle may not have fired — act as fallback.
        // Arka plan job'ları (deferred Override/PulseCore clear'ları) da bitmiş olmalı.
        if (!IsBoardWorkingForLevelEnd())
        {
            Debug.Log("[LevelEnd] Update fallback: board idle, moves exhausted, evaluating.");
            endCheckQueued = false;
            EvaluateAndShowIfEnded();
            return;
        }

        // Board still animating — a user tap skips the remaining animation.
        bool tapped = UnityEngine.InputSystem.Pointer.current != null
            && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame;
        if (!tapped) return;

        Debug.Log("[LevelEnd] User tap: skipping out-of-moves animation, showing popup now.");
        levelEndForceDeadlineUnscaled = Time.unscaledTime;
        endCheckQueued = false;
        EvaluateAndShowIfEnded();
    }

    // TEK KURAL (2026-08-07): Board TAM oturmadıkça — senkron VEYA async hiçbir iş kalmadıkça —
    // level-end kararı verme. Hem win hem fail yolu (WaitBoardQuietForLevelEnd + EvaluateAndShowIfEnded)
    // bu tek yordamdan geçer.
    //
    // Kritik: ActiveBackgroundJobs'un TAMAMINI bekle — uçan orb/patchbot/rocket/boss-strike dahil.
    // Bunlar "BlockingBackgroundJobs" hesabından çıkarılır (resolve loop'u parklamasın diye), AMA
    // hepsi goal/win durumunu değiştirebilir. Eskiden level-end yalnız BlockingBackgroundJobs'ı +
    // "remainingGoals <= nonBlockingJobs" gibi kırılgan bir tahmini bekliyordu → PatchBot/rocket
    // HAVADAYKEN (henüz hedefe konmadan) fail popup'ı açılabiliyordu. Artık tek ölçüt: iş var mı yok mu.
    // KURAL (2026-08-09): board bu predikata göre ÇALIŞIRKEN asla karar verilmez; zaman-tabanlı
    // zorlama kaldırıldı. Yalnız gerçek leak'e karşı EvaluateAfterBoardSettled'da uzun teşhis tavanı var.
    private bool IsBoardWorkingForLevelEnd()
    {
        if (board == null)
            return false;

        return board.IsBusy
            || board.ActiveBackgroundJobs > 0
            || board.IsActionSequencePlaying;
    }

    private string DescribeBoardWorkForLevelEnd()
    {
        if (board == null)
            return "board=null";

        return $"IsBusy={board.IsBusy}, ActiveBackgroundJobs={board.ActiveBackgroundJobs}, " +
               $"BlockingBackgroundJobs={board.BlockingBackgroundJobs}, FlyingGoalOrbs={board.FlyingGoalOrbs}, " +
               $"FlyingPatchBotDashes={board.FlyingPatchBotDashes}, DrainingBossStrikes={board.DrainingBossStrikes}, " +
               $"ActionSequencePlaying={board.IsActionSequencePlaying}";
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
        // 1. cancel: kaybedecekleri sağdan kaydırıp göster (ana menüye gitme). BtnContinue aynı kalır.
        if (!failSecondStage && _hasLosses && lossSlidePanel != null)
        {
            failSecondStage = true;
            // Kayıp ekranı gelince "hamle ekle" teklifi ikonu artık geçersiz → gizle.
            if (extraMovesIcon != null)
                extraMovesIcon.enabled = false;
            SetExtraMovesBadgeTextVisible(false);
            lossSlidePanel.gameObject.SetActive(true);
            StartCoroutine(SlideInFromRight(lossSlidePanel, lossSlideDuration));
            return;
        }

        // 2. cancel (veya gösterilecek kayıp yok): hamle eklemeyi reddetti → GERÇEKTEN vazgeçti.
        // Fail'i ŞİMDİ işaretle (streak kırılır); event item'ları kaybolur.
        PlayerStats.MarkCurrentLevelFailed();
        ProgressEventService.Instance?.DiscardStagedGains();

        // Game sahnesinde bir pre-level popup instance'ı varsa (prefab yerleştirilmiş) → sahne
        // yüklemeden ANINDA "Tekrar Dene" modunda aç (aradaki beyaz MainMenu yükleme gap'i olmaz).
        var inScenePreLevel = FindFirstObjectByType<PreLevelSpecialPopupController>(FindObjectsInactive.Include);
        if (inScenePreLevel != null)
        {
            if (failPopupRoot != null)
                failPopupRoot.SetActive(false);
            // Kayıp board'un dimmed kalması için blocker açık bırakılır; popup üstüne açılır.
            inScenePreLevel.gameObject.SetActive(true);
            inScenePreLevel.OpenForInGameRetry();
            return;
        }

        // Fallback (instance yok): ana menü yüklenince pre-level popup'ı "Tekrar Dene" modunda
        // otomatik aç. Oradan da vazgeçerse (cancel) popup kapanır ve zaten ana menüde kalınır.
        PreLevelSpecialPopupController.RetryRequested = true;
        ReturnToMainMenuImmediate();
    }

    // Kayıp panelini ekran-dışı-sağdan, sahnede yazdığın hedef konuma kaydırır.
    private System.Collections.IEnumerator SlideInFromRight(RectTransform panel, float dur)
    {
        Vector2 target = panel.anchoredPosition;   // sahnedeki yazılı (hedef) konum
        float dist = (panel.parent is RectTransform p && p.rect.width > 1f) ? p.rect.width : Mathf.Max(panel.rect.width, 600f);
        Vector2 start = target + new Vector2(dist, 0f);
        panel.anchoredPosition = start;

        float t = 0f;
        float d = Mathf.Max(0.05f, dur);
        while (t < d && panel != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            float e = 1f - Mathf.Pow(1f - k, 3f);   // ease-out
            panel.anchoredPosition = Vector2.LerpUnclamped(start, target, e);
            yield return null;
        }
        if (panel != null) panel.anchoredPosition = target;
    }

    private bool returningToMainMenu;

    private void HandleSuccessCloseClicked()
    {
        // Bu levelde progress-event ödülü kazanıldıysa önce sandık açılış töreni,
        // sonra ana menü. Overlay DontDestroyOnLoad; onDone sahne geçişini yapar.
        var chestRewards = ProgressEventService.Instance?.ConsumePendingRewardReveals();
        if (chestRewards != null && chestRewards.Count > 0)
        {
            RewardChestRevealOverlay.Show(chestRewards, ReturnToMainMenuImmediate);
            return;
        }

        ReturnToMainMenuImmediate();
    }

    private void ReturnToMainMenuImmediate()
    {
        if (returningToMainMenu)
            return;

        returningToMainMenu = true;

        // Kullanıcı devam dediği anda arkayı göstermemek için popup'ı kapatma.
        // Sadece input'u kilitle ve blocker'ı en üstte tut.
        if (board != null)
            board.SetInputLocked(true);

        transform.SetAsLastSibling();

        if (blockerRoot != null)
        {
            blockerRoot.SetActive(true);
            blockerRoot.transform.SetAsLastSibling();
        }

        if (dimImage != null)
        {
            dimImage.enabled = true;
            dimImage.raycastTarget = true;
            dimImage.color = Color.black; // Tam siyah geçiş perdesi
        }

        var library = chapterThemeApplier != null ? chapterThemeApplier.ThemeLibrary : null;
        LoadingScreenManager.Show(library != null ? library.GetRandomLoadingImage() : null, minimumDisplayTime: 0f);
        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void QueueSuccessReturnToMainMenu()
    {
        if (board == null || topHud == null)
            return;

        if (successPopupShown || successReturnQueued)
            return;

        successReturnQueued = true;
        Debug.Log("[LevelEnd] Success queued. Waiting board settle...");

        // Level kazanıldı: staging'deki event item'larını kalıcılaştır. Bonus round
        // bundan sonra koşar; oradaki temizlikler canlı (staging'siz) sayılır.
        ProgressEventService.Instance?.CommitStagedGains();

        StartCoroutine(CompleteSuccessAfterBoardSettled());
    }

    private IEnumerator CompleteSuccessAfterBoardSettled()
    {
        yield return null;

        const int requiredStableFrames = 3;
        const float initialSettleTimeoutSeconds = 5f;
        int stableFrames = 0;
        float initialSettleElapsed = 0f;

        while (board != null && stableFrames < requiredStableFrames)
        {
            bool boardStillWorking = IsBoardWorkingForLevelEnd();
            stableFrames = boardStillWorking ? 0 : stableFrames + 1;

            if (boardStillWorking)
            {
                initialSettleElapsed += Time.unscaledDeltaTime;
                if (initialSettleElapsed >= initialSettleTimeoutSeconds)
                {
                    Debug.LogWarning(
                        $"[LevelEnd] Success initial settle timeout. " +
                        DescribeBoardWorkForLevelEnd());
                    break;
                }
            }

            yield return null;
        }

        yield return StartCoroutine(WaitBoardQuietForLevelEnd(3f, "before_success_logo"));

        if (board != null)
            board.SetInputLocked(true);

        if (levelCompletionLogoAnimation != null)
        {
            // Havai fişek sesi, görsel gösteri PENCERESİ boyunca loop'lar: ilk patlamada başlar,
            // fişekler bitince (FireworksFinished) tam olarak durur → erken bitmez, animasyonla senkron biter.
            void StartWinSfx() => GameEventSfx.StartLevelWinLoop();
            void StopWinSfx()  => GameEventSfx.StopLevelWinLoop();

            levelCompletionLogoAnimation.FireworksStarted += StartWinSfx;
            levelCompletionLogoAnimation.FireworksFinished += StopWinSfx;
            yield return StartCoroutine(levelCompletionLogoAnimation.Play());
            levelCompletionLogoAnimation.FireworksStarted -= StartWinSfx;
            levelCompletionLogoAnimation.FireworksFinished -= StopWinSfx;
            StopWinSfx();
        }

        _movesAtWin = board != null ? board.RemainingMoves : 0;

        // Snapshot goals BEFORE bonus round — bonus cascade must not cancel a win that already happened.
        bool goalsWereCompleted = topHud != null && topHud.AreAllGoalsCompleted;

        hardSkipBonusRoundRequested = false;

        if (bonusMovesService != null && board != null && board.RemainingMoves > 0)
        {
            board.SetInputLocked(true);
            isBonusRoundRunning = true;
            SetSkipBonusOverlayVisible(true);

            yield return StartCoroutine(bonusMovesService.RunBonusRound());

            isBonusRoundRunning = false;
            SetSkipBonusOverlayVisible(false);

            // Hard-skip stops the bonus coroutine mid-execution; wait for board to fully settle.
            const float settleTimeout = 5f;
            float settleElapsed = 0f;
            while (IsBoardWorkingForLevelEnd() && settleElapsed < settleTimeout)
            {
                settleElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (successVideoStartDelay > 0f && !hardSkipBonusRoundRequested)
            yield return new WaitForSecondsRealtime(successVideoStartDelay);

        successReturnQueued = false;

        if (board == null || topHud == null)
            yield break;

        if (!goalsWereCompleted)
        {
            Debug.LogWarning("[LevelEnd] Success cancelled; goals were not completed before bonus round.");
            yield break;
        }

        Debug.Log(hardSkipBonusRoundRequested
            ? "[LevelEnd] Bonus round hard skipped. Showing success popup."
            : "[LevelEnd] Board settled. Showing success popup.");

        ShowSuccessPopup();
    }
    private IEnumerator WaitBoardQuietForLevelEnd(float timeoutSeconds, string context)
    {
        float elapsed = 0f;

        while (IsBoardWorkingForLevelEnd())
        {
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= timeoutSeconds)
            {
                Debug.LogWarning(
                    $"[LevelEnd] Wait timeout. context={context}, " +
                    DescribeBoardWorkForLevelEnd());
                yield break;
            }

            yield return null;
        }
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
        PlayerStats.RecordLevelCleared();   // ilk-deneme/seri/haftalık istatistikleri güncelle

        int level = ResolveCurrentLevelNumber();
        PlayerPrefs.SetInt(prefsLevelKey, level + 1);
        PlayerPrefs.Save();
    }

    private void ReturnToMainMenu()
    {
        var library = chapterThemeApplier != null ? chapterThemeApplier.ThemeLibrary : null;
        LoadingScreenManager.Show(library != null ? library.GetRandomLoadingImage() : null, minimumDisplayTime: 0f);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetBlockerVisible(bool isVisible)
    {
        if (isVisible)
            ApplyDimImageColor();

        if (blockerRoot != null)
            blockerRoot.SetActive(isVisible);
    }

    private void SetSkipBonusOverlayVisible(bool visible)
    {
        if (skipBonusRoundButton == null)
            return;

        if (visible)
        {
            EnsureParentHierarchyActive(skipBonusRoundButton.transform);
            skipBonusRoundButton.transform.SetAsLastSibling();

            var image = skipBonusRoundButton.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = true;
                image.raycastTarget = true;
                var c = image.color;
                c.a = 0f;
                image.color = c;
            }
        }

        skipBonusRoundButton.gameObject.SetActive(visible);
    }

    private void HandleSkipBonusRound()
    {
        if (!isBonusRoundRunning || hardSkipBonusRoundRequested)
            return;

        hardSkipBonusRoundRequested = true;

        if (bonusMovesService != null)
            bonusMovesService.RequestHardSkip();

        SetSkipBonusOverlayVisible(false);
    }

    private void Subscribe()
    {
        Unsubscribe();
        board.OnMovesChanged += HandleMovesChanged;
        board.OnLevelFailRequested += HandleForcedFail;
        board.ObstacleVisualChanged += HandleGoldMoneyVisual;
        topHud.OnGoalsCompletionChanged += HandleGoalsCompletionChanged;
    }

    private void Unsubscribe()
    {
        if (board != null)
        {
            board.OnMovesChanged -= HandleMovesChanged;
            board.OnLevelFailRequested -= HandleForcedFail;
            board.ObstacleVisualChanged -= HandleGoldMoneyVisual;
        }
        if (topHud != null)
            topHud.OnGoalsCompletionChanged -= HandleGoalsCompletionChanged;
    }

    // Player HP=0 gibi kesin kayıp (BossDuel). Hedefler aynı anda tamamlandıysa
    // (düşman da öldüyse) galibiyet öncelikli; aksi hâlde direkt fail.
    private void HandleForcedFail()
    {
        if (failPopupShown || successPopupShown)
            return;

        if (topHud != null && topHud.AreAllGoalsCompleted)
        {
            QueueSuccessReturnToMainMenu();
            return;
        }

        ShowFailPopup();
    }

    private void HandleMovesChanged(int _) => RequestEvaluateLevelEndState();

    private void HandleGoalsCompletionChanged(bool completed)
    {
        if (completed)
        {
            TryQueueSuccessOrDeferForGold();
            return;
        }

        RequestEvaluateLevelEndState();
    }

    // GoldMoney (bonus coin) board'da dururken ve hamle varken win'i ERTELE — oyuncu kalan
    // hamlelerle altınları toplasın. Hamle biterse (mevcut akış zaten win verir) ya da tüm
    // GoldMoney toplanırsa (HandleGoldMoneyVisual → re-eval) win gelir. Sadece GoldMoney'e özgü.
    private bool ShouldDeferWinForGoldMoney()
    {
        return board != null
            && board.RemainingMoves > 0
            && board.ObstacleStateService != null
            && board.ObstacleStateService.CountAliveOrigins(ObstacleId.GoldMoney) > 0;
    }

    private void TryQueueSuccessOrDeferForGold()
    {
        if (ShouldDeferWinForGoldMoney()) return;   // GoldMoney + hamle var → ertele, oynamaya devam
        QueueSuccessReturnToMainMenu();
    }

    // Son GoldMoney toplanınca end-değerlendirmeyi yeniden tetikle → erteleme kalkar, win gelir.
    private void HandleGoldMoneyVisual(ObstacleVisualChange change)
    {
        if (change.cleared && change.obstacleId == ObstacleId.GoldMoney)
            RequestEvaluateLevelEndState();
    }

    private void RequestEvaluateLevelEndState()
    {
        if (board == null || topHud == null)
            return;

        if (endCheckQueued)
            return;

        // Deadline'ı ilk 0-hamle sinyalinde başlat. Aksi halde RunAfterIdle'in 3sn timeout'u
        // force-deadline'a eklenip safe reveal / geç efekt durumlarında fail beklemesi uzardı.
        StartLevelEndForceDeadlineIfOutOfMoves();

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

        // Hamle 0 ilk gözlendiğinde üst-süre saatini başlat (bir kez). Bu süre dolunca
        // board hâlâ meşgul/pending olsa bile aşağıdaki ertelemeler devre dışı kalır → sonuç zorla verilir.
        StartLevelEndForceDeadlineIfOutOfMoves();

        // Son hamlenin board çözümü (cascade + special zinciri + arka plan job'lar)
        // tamamen bitmeden sonucu değerlendirme. RunAfterIdle 3s timeout'u board hâlâ
        // çözülürken erken tetiklenebilir; o anda "hamle kalmadı" değerlendirilirse
        // hedefler cascade sırasında tamamlanacakken fail popup'ı erken açılır.
        // Success yolu (CompleteSuccessAfterBoardSettled) zaten bu şekilde bekliyor;
        // fail yolunu da gerçek idle'a kadar erteleyerek simetriyi sağlıyoruz.
        // KURAL: aktif board akışı MUTLAK bitene kadar KARAR VERME — zaman-tabanlı zorlama yok.
        if (IsBoardWorkingForLevelEnd())
        {
            if (!failSettleWaitRunning)
            {
                failSettleWaitRunning = true;
                StartCoroutine(EvaluateAfterBoardSettled());
            }
            return;
        }

        Debug.Log(
            $"[LevelEnd] Evaluate. " +
            $"RemainingMoves={board.RemainingMoves}, " +
            $"GoalsCompleted={topHud.AreAllGoalsCompleted}, " +
            DescribeBoardWorkForLevelEnd());
            
        if (topHud.AreAllGoalsCompleted)
        {
            TryQueueSuccessOrDeferForGold();
            return;
        }

        if (board.RemainingMoves <= 0)
        {
            if (ResolvePendingBoardBeforeFail())
                return;

            // Hemen fail gösterme: son hamlenin geç efektleri (beam varışı, obstacle
            // kırılma kuyruğu) board idle göründükten az sonra hedefi tamamlayabilir.
            // Kısa bir doğrulama beklemesiyle bu yarışı kapat.
            BeginFailConfirmation();
            return;
        }

        Debug.Log($"[LevelEndSimplePopupController] End check skipped. RemainingMoves={board.RemainingMoves}, GoalsCompleted={topHud.AreAllGoalsCompleted}");
    }

    private void StartLevelEndForceDeadlineIfOutOfMoves()
    {
        if (board != null && board.RemainingMoves <= 0 && levelEndForceDeadlineUnscaled < 0f)
            levelEndForceDeadlineUnscaled = Time.unscaledTime + Mathf.Max(0.5f, levelEndForceTimeoutSeconds);
    }

    private void BeginFailConfirmation()
    {
        if (failConfirmRunning || failPopupShown || successPopupShown)
            return;

        failConfirmRunning = true;
        StartCoroutine(ConfirmFailAfterGrace());
    }

    // Fail'i kesinleştirmeden önce kısa bir bekleme: bu sürede hedefler tamamlanırsa
    // success'e geçer; board yeniden meşgul olursa (cascade/efekt) gerçek settle'ı bekler;
    // yalnızca grace sonunda hâlâ hedefler eksik ve board idle ise gerçekten fail gösterir.
    private IEnumerator ConfirmFailAfterGrace()
    {
        float t = 0f;
        while (t < failConfirmGraceSeconds)
        {
            if (failPopupShown || successPopupShown)
            {
                failConfirmRunning = false;
                yield break;
            }

            if (topHud != null && topHud.AreAllGoalsCompleted)
            {
                failConfirmRunning = false;
                QueueSuccessReturnToMainMenu();
                yield break;
            }

            // Board yeniden çalışmaya başladıysa (geç cascade/efekt) — gerçek idle'ı bekle,
            // sonra yeniden değerlendir. KURAL: board çalışırken fail kesinleştirilmez.
            if (IsBoardWorkingForLevelEnd())
            {
                failConfirmRunning = false;
                if (!failSettleWaitRunning)
                {
                    failSettleWaitRunning = true;
                    StartCoroutine(EvaluateAfterBoardSettled());
                }
                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        failConfirmRunning = false;

        if (failPopupShown || successPopupShown || board == null || topHud == null)
            yield break;

        if (topHud.AreAllGoalsCompleted)
        {
            QueueSuccessReturnToMainMenu();
            yield break;
        }

        if (board.RemainingMoves <= 0 && !IsBoardWorkingForLevelEnd())
        {
            if (ResolvePendingBoardBeforeFail())
                yield break;

            ShowFailPopup();
        }
    }

    private int failResolveDeferCount;
    private const int MaxFailResolveDefers = 3;

    private bool ResolvePendingBoardBeforeFail()
    {
        // Üst-süre dolduysa artık erteleme — pending (boş hücre/match/altın) olsa bile fail'e devam.
        // Bu, "altın varken 0 hamleyle oyun devam ediyor" sonsuz ertelemesinin kesin kapısı.
        if (LevelEndForceDeadlinePassed)
        {
            failResolveDeferCount = 0;
            return false;
        }

        if (board == null || !board.HasPendingAutoResolveForLevelEnd())
        {
            failResolveDeferCount = 0;
            return false;
        }

        // Sonsuz erteleme guard'ı: pending koşullarından biri yalancı-pozitife takılırsa
        // (ör. hiç doldurulamayacak boş hücre) fail hiç gösterilmiyor, oyuncu 0 hamleyle
        // oynamaya devam edebiliyordu. Sınırlı sayıda ertele, sonra fail akışı ilerlesin.
        if (failResolveDeferCount >= MaxFailResolveDefers)
        {
            Debug.LogWarning(
                $"[LevelEnd] Pending auto-resolve {failResolveDeferCount} kez ertelendi ama hâlâ pending " +
                $"({board.DescribePendingAutoResolveForLevelEnd()}) — fail akışına devam ediliyor.");
            failResolveDeferCount = 0;
            return false;
        }

        failResolveDeferCount++;
        Debug.Log(
            $"[LevelEnd] Pending auto-resolve before fail (defer {failResolveDeferCount}/{MaxFailResolveDefers}): " +
            $"{board.DescribePendingAutoResolveForLevelEnd()}");
        board.RequestResolveAfterActionSequence();

        if (!failSettleWaitRunning)
        {
            failSettleWaitRunning = true;
            StartCoroutine(EvaluateAfterBoardSettled());
        }

        return true;
    }

    // Board tamamen oturana kadar (3 ardışık idle frame) bekleyip değerlendirmeyi
    // yeniden çalıştırır. Bu sırada hedefler tamamlanırsa success yolu devreye girer
    // ve fail popup'ı hiç açılmaz; gerçekten kaybedildiyse popup ancak son hamlenin
    // tüm efektleri bittikten sonra çıkar.
    private IEnumerator EvaluateAfterBoardSettled()
    {
        // KURAL: aktif board akışı MUTLAK bitene kadar bekle. Zaman-tabanlı karar YOK.
        // Tek istisna: board GERÇEKTEN takılıp (leak) hiç idle olmazsa oyunu sonsuza
        // kilitlememek için uzun bir TEŞHİS tavanı — aşılırsa ERROR loglanır (bug yüzeye çıkar),
        // sonra yine de değerlendirilir. Normal oyunda (uzun zincir/efekt bile) devreye girmez.
        const int requiredStableFrames = 3;
        const float leakDiagnosticSeconds = 30f;   // mutlak tavan: blocking dahil her şey takılırsa
        const float asyncLeakSeconds = 10f;         // blocking bitti ama SADECE async job takılı kaldıysa
                                                    // (legit çoklu orb uçuşunu kesmeyecek kadar uzun)
        int stableFrames = 0;
        float workingElapsed = 0f;
        float asyncLingerElapsed = 0f;

        while (board != null && stableFrames < requiredStableFrames)
        {
            if (IsBoardWorkingForLevelEnd())
            {
                stableFrames = 0;
                workingElapsed += Time.unscaledDeltaTime;

                // Blocking/görsel iş bitti ama yalnız async job (uçan orb/patchbot/barrel-spread/keygen)
                // takılı: legit uçuşlar kısadır (<3s). Bu kadar sürerse LEAK'tir (Begin/End dengesizliği).
                // resolve loop çoktan çıktığı için onun 5s timeout'u bunu temizleyemez → BURADA drain et.
                bool onlyAsyncLingering = !board.IsBusy
                    && board.BlockingBackgroundJobs == 0
                    && !board.IsActionSequencePlaying
                    && board.ActiveBackgroundJobs > 0;

                if (onlyAsyncLingering)
                {
                    asyncLingerElapsed += Time.unscaledDeltaTime;
                    if (asyncLingerElapsed >= asyncLeakSeconds)
                    {
                        Debug.LogWarning(
                            $"[LevelEnd] Async job {asyncLeakSeconds:0}s+ takılı (leak) — force-drain edip " +
                            $"karar veriliyor. {DescribeBoardWorkForLevelEnd()}");
                        board.ForceDrainAllJobs();
                        break;
                    }
                }
                else
                {
                    asyncLingerElapsed = 0f;
                }

                if (workingElapsed >= leakDiagnosticSeconds)
                {
                    Debug.LogError(
                        $"[LevelEnd] Board {leakDiagnosticSeconds:0}s+ boyunca çalışıyor — olası takılma/leak. " +
                        $"Force-drain edip değerlendiriliyor. {DescribeBoardWorkForLevelEnd()}");
                    board.ForceDrainAllJobs();
                    break;
                }
            }
            else
            {
                stableFrames++;
            }
            yield return null;
        }

        failSettleWaitRunning = false;
        // Force-drain sonrası IsBoardWorkingForLevelEnd artık false → gate'e takılmadan karar verilir
        // (eskiden break sonrası gate hâlâ true görüp yeniden ertelerdi = 30s'lik sonsuz döngü).
        EvaluateAndShowIfEnded();
    }

    private void ShowFailPopup()
    {
        Debug.Log("[LevelEnd] ShowFailPopup CALLED");
        if (failPopupShown)
            return;

        failPopupShown = true;
        successPopupShown = false;

        // NOT: Fail'i BURADA işaretleME. Oyuncu reklam/altınla DEVAM edip kazanabilir → o zaman
        // "ilk hamlede kazanıldı" sayılmalı (streak korunur). Fail ancak oyuncu gerçekten VAZGEÇİP
        // ana menüye/market'e çıkınca işaretlenir (HandleFailCloseClicked / GoToMarketFromFail).

        if (board != null)
            board.SetInputLocked(true);

        LivesManager.SpendLife();
        ApplyChapterThemeVisuals();
        RefreshFailOfferVisuals(animateIncrease: true);
        RefreshEventLossWarning();

        // Aşama 1: kayıp paneli gizli; ilk cancel'da sağdan kaydırılır.
        failSecondStage = false;
        if (lossSlidePanel != null) lossSlidePanel.gameObject.SetActive(false);

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
        ShowFailWalletBalance(true);
    }

    // Vazgeçersen (hamle eklemezsen) neleri kaybedeceğinin özeti: kazanılacak altın ödülü +
    // bu bölümde toplanan event puanı. İlgili olan satırlar gösterilir; hiçbiri yoksa gizli.
    private void RefreshEventLossWarning()
    {
        RefreshFailLossSummary();
    }

    // Vazgeçince kaybedilecekler — DİNAMİK ikon satırı listesi (coin, event(ler), ileride yıldız...).
    // Her kayıp öğesi için lossRowPrefab instantiate edilir. Prefab/container yoksa metne düşer.
    private void RefreshFailLossSummary()
    {
        // Eski satırları temizle
        for (int i = 0; i < _lossRows.Count; i++)
            if (_lossRows[i] != null) Destroy(_lossRows[i]);
        _lossRows.Clear();

        var items = BuildLossItems();
        bool any = items.Count > 0;
        _hasLosses = any;

        // Başlık ("Şunları kaybedeceksin") — kayıp varken göster.
        if (lossTitleText != null)
        {
            lossTitleText.gameObject.SetActive(any);
            if (any)
                lossTitleText.text = LocalizedText("level_end_loss_title", "Şunları kaybedeceksin");
        }

        if (lossRowPrefab != null && lossRowContainer != null)
        {
            foreach (var it in items)
            {
                var row = Instantiate(lossRowPrefab, lossRowContainer);
                row.Set(it.icon, it.label, it.amount, it.achieved ? achievedIcon : notAchievedIcon);
                _lossRows.Add(row.gameObject);
            }

            if (lossSummaryRoot != null) lossSummaryRoot.SetActive(any);
            if (failEventLossWarningText != null) failEventLossWarningText.gameObject.SetActive(false);
            return;
        }

        // ── Fallback: prefab yoksa birleşik metin ──
        if (failEventLossWarningText != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var it in items)
                sb.AppendLine(string.IsNullOrEmpty(it.label) ? $"•  {it.amount}" : $"•  {it.label}: {it.amount}");
            failEventLossWarningText.gameObject.SetActive(any);
            if (any)
                failEventLossWarningText.text = $"Vazgeçersen kaybedeceklerin:\n{sb.ToString().TrimEnd()}";
        }

        if (lossSummaryRoot != null) lossSummaryRoot.SetActive(any);
    }

    // Kayıp öğeleri (ikon + miktar + gerçekleşme durumu). "achieved" = bu level'da fiilen kazanıldı/elde
    // edildi mi (checkmark); false ise henüz gerçekleşmemiş potansiyel ödül (cancel). Yeni tür buraya eklenir.
    private System.Collections.Generic.List<(Sprite icon, string label, int amount, bool achieved)> BuildLossItems()
    {
        var items = new System.Collections.Generic.List<(Sprite icon, string label, int amount, bool achieved)>();

        // Altın (kazanılacak ödül) — base reward, UI'ın kendi config'i; bir "event" değil → burada kalır.
        // Level KAZANILINCA verilir; fail ekranında henüz gerçekleşmedi → cancel.
        var level = board != null ? board.ActiveLevelData : null;
        int coinReward = useFlatCoinReward
            ? Mathf.Max(0, flatCoinReward)
            : (level != null ? Mathf.Max(0, level.baseCoinReward) : 0);
        if (coinReward > 0)
            items.Add((coinIcon, LocalizedText("level_end_loss_coin", "Ödül"), coinReward, false));

        // Event'ler DİNAMİK: her sistem kendi riskini LevelLossRegistry'ye kaydeder. UI hiçbir event'i
        // tanımaz — yeni event eklemek için buraya DOKUNULMAZ (bkz. LevelLossRegistry).
        foreach (var it in LevelLossRegistry.Collect())
            items.Add((it.Icon, it.Label, it.Amount, it.Achieved));

        return items;
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

        ShowFailWalletBalance(false);
        SetMainScreenDimmed(true);
        SetBlockerVisible(true);
        ApplyRewardVisuals(stars, coins, score);
        SaveRewards(stars, coins, score);
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

    private int CalculateCoins()
    {
        // Sabit (küçük) level-sonu ödülü — in-game gold'dan ayrı. baseCoinReward/hamle bonusu
        // formülü kullanılmaz; "her kazanışta ekstra ~10 coin" gibi öngörülebilir küçük ödül.
        if (useFlatCoinReward)
            return Mathf.Max(0, flatCoinReward);

        LevelData levelData = board != null ? board.ActiveLevelData : null;
        int baseReward = Mathf.Max(0, levelData != null ? levelData.baseCoinReward : baseCoins);
        if (levelData == null || levelData.moves <= 0)
            return baseReward;

        float remainingRatio = Mathf.Clamp01((float)_movesAtWin / levelData.moves);
        int remainingBonus = Mathf.RoundToInt(baseReward * remainingRatio * Mathf.Max(0f, remainingMoveBonusRatio));
        return baseReward + Mathf.Max(0, remainingBonus);
    }

    private int CalculateScore() => scoreBase + _movesAtWin * scorePerRemainingMove;

    private void ApplyRewardVisuals(int stars, int coins, int score)
    {
        int level = ResolveCurrentLevelNumber();

        if (successTitleText != null)
            successTitleText.text = LocalizedFormat("level_end_success_title_level", "Seviye {0}", level);

        if (successContinueText != null)
            successContinueText.text = LocalizedText("level_end_continue", "Devam Et");

        string scoreLabel = LocalizedText("level_end_score_label", "Puan:");

        if (scoreLabelText != null)
            scoreLabelText.text = $"{scoreLabel} {score:N0}";

        if (scoreValueText != null)
            scoreValueText.gameObject.SetActive(false);

        PlayStarReveal(stars);

        if (coinsEarnedText != null)
            coinsEarnedText.text = coinsPrefix + coins + coinsSuffix;

        if (successDescriptionText != null && scoreValueText == null)
            successDescriptionText.text = $"{LocalizedText("level_end_score_label", "Puan:")} {score:N0}";

        ApplyGoalVisuals();
    }

    private void SaveRewards(int stars, int coins, int score)
    {
        int currentLevel = ResolveCurrentLevelNumber();

        // Kümülatif puan: level başına en iyi skor toplanır (leaderboard/team metriği).
        PlayerWallet.SetLevelScore(currentLevel, score);
        int previousStars = PlayerWallet.GetLevelStars(currentLevel);
        int earnedStars = Mathf.Clamp(stars, 0, 3);
        int starBefore = PlayerWallet.TotalStars;

        PlayerWallet.SetLevelStars(currentLevel, earnedStars);
        int starAfter = PlayerWallet.TotalStars;
        int gainedStars = starAfter - starBefore;

        if (gainedStars > 0)
        {
            PlayerPrefs.SetInt(StarFlyToWalletAnimator.PendingRewardKey, gainedStars);
            PlayerPrefs.SetInt(StarFlyToWalletAnimator.PendingBeforeKey, starBefore);
            PlayerPrefs.SetInt(StarFlyToWalletAnimator.PendingAfterKey, starAfter);
            PlayerPrefs.Save();
            Debug.Log($"[LevelEnd] Pending star reward saved. Level={currentLevel}, Previous={previousStars}, Earned={earnedStars}, Gained={gainedStars}, Before={starBefore}, After={starAfter}");
        }
        else
        {
            StarFlyToWalletAnimator.ClearPendingReward();
            Debug.Log($"[LevelEnd] No pending star reward. Level={currentLevel}, Previous={previousStars}, Earned={earnedStars}, Before={starBefore}, After={starAfter}");
        }

        int coinBefore = PlayerWallet.Coins;
        PlayerWallet.AddCoins(coins);
        int coinAfter = PlayerWallet.Coins;

        if (coins > 0)
        {
            CoinFlyToWalletAnimator.QueuePendingReward(coins, coinBefore, coinAfter);
        }
    }

    private int ResolveCurrentLevelNumber()
    {
        LevelData activeLevel = board != null ? board.ActiveLevelData : null;
        if (activeLevel != null && levelCatalog != null && levelCatalog.TryGetGlobalLevel(activeLevel, out int activeLevelNumber))
            return Mathf.Max(1, activeLevelNumber);

        return Mathf.Max(1, PlayerPrefs.GetInt(prefsLevelKey, 1));
    }

    private void HandleBuyMovesClicked()
    {
        if (board == null)
            return;

        ResolveCurrentFailOffer();

        // Yeterli coin varsa: normal akış (coin harca → devam).
        if (PlayerWallet.HasEnoughCoins(currentCost))
        {
            PlayerWallet.SpendCoins(currentCost);
            PerformContinue();
            return;
        }

        // Yeterli coin yok → reklam izle (bedava devam) ya da satın al (market) seçimi.
        RuntimeChoicePopup.Show(
            "Yetersiz Altın",
            $"Devam için {currentCost} coin gerekli, {PlayerWallet.Coins} coinin var.\nReklam izleyerek bedava devam edebilir veya market'ten altın alabilirsin.",
            new RuntimeChoicePopup.Choice("Reklam İzle", WatchAdThenContinue, primary: true),
            new RuntimeChoicePopup.Choice("Satın Al", GoToMarketFromFail),
            new RuntimeChoicePopup.Choice("Kapat", null));
    }

    // Coin harcandıktan (veya reklam ödülünden) sonra ortak "devam" akışı.
    private void PerformContinue()
    {
        if (board == null) return;

        // Fail popup açılırken harcanan hakkı geri ver; oyuncu devam ediyor.
        LivesManager.AddLives(1);

        board.AddMoves(currentOfferAmount);
        board.SetInputLocked(false);
        extraMoveOfferAttempt++;

        // Devam edildi → TÜM level-end değerlendirme state'ini sıfırla ki eklenen hamleler
        // tekrar 0'a düşünce fail/success yeniden temiz değerlendirilsin. Aksi halde takılı
        // flag'ler (özellikle endCheckQueued) ikinci 0'da RequestEvaluate'i erken döndürüyor,
        // deadline ise eski (geçmiş) değerinde kalıyordu → "0 hamleyle devam" tekrarlanıyordu.
        failPopupShown = false;
        failSecondStage = false;
        failConfirmRunning = false;
        failSettleWaitRunning = false;
        failResolveDeferCount = 0;
        endCheckQueued = false;
        levelEndForceDeadlineUnscaled = -1f;

        HideAllPopups();
        board.ForceFullBoardSync();
    }

    private void WatchAdThenContinue()
    {
        StartCoroutine(CoWatchAdThenContinue());
    }

    private System.Collections.IEnumerator CoWatchAdThenContinue()
    {
        // TODO: Gerçek rewarded-ad SDK bağlanınca burada gösterilecek. Şimdilik
        // editör/dev-build'de kısa bir bekleme ile ödül verilir (bedava devam).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[LevelEnd] Reklam simülasyonu → bedava devam.");
        yield return new WaitForSecondsRealtime(1f);
        PerformContinue();
#else
        Debug.LogWarning("[LevelEnd] Rewarded ad SDK bağlı değil; bedava devam verilemedi.");
        yield break;
#endif
    }

    private void GoToMarketFromFail()
    {
        // 01_Game'de market yok → ana menüye dön, orada market otomatik açılsın.
        // Devam reddedilip level'dan çıkılıyor → fail'i işaretle (streak kırılır).
        // Devam reddedildiği için staged event item'ları da temizlenir.
        PlayerStats.MarkCurrentLevelFailed();
        ProgressEventService.Instance?.DiscardStagedGains();
        MarketNavigator.PendingOpenMarket = true;
        ReturnToMainMenuImmediate();
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

        if (extraMoveOfferAmounts != null && extraMoveOfferAmounts.Length > 0)
        {
            currentOfferAmount = Mathf.Max(1, extraMoveOfferAmounts[offerIndex]);

            if (safeAttempt >= extraMoveOfferAmounts.Length)
            {
                int lastIndex = extraMoveOfferAmounts.Length - 1;
                int previous = lastIndex > 0 ? extraMoveOfferAmounts[lastIndex - 1] : 0;
                int step = Mathf.Max(1, extraMoveOfferAmounts[lastIndex] - previous);
                currentOfferAmount = Mathf.Max(1, extraMoveOfferAmounts[lastIndex] + step * (safeAttempt - lastIndex));
            }
        }
        else
        {
            currentOfferAmount = 5 + safeAttempt * 5;
        }

        float multiplier = Mathf.Max(0f, extraMoveCostMultiplier);
        currentCost = Mathf.RoundToInt(baseExtraMovesCost * Mathf.Pow(multiplier, safeAttempt));
        currentCost = Mathf.Max(0, currentCost);
    }

    private void RefreshFailOfferVisuals(bool animateIncrease = false)
    {
        ResolveCurrentFailOffer();

        if (failTitleText != null)
            failTitleText.text = LocalizedText("level_end_fail_title", "Hamle Kalmadi!");

        string message = LocalizedFormat("level_end_fail_extra_moves_text", "{0} hamle ile devam et.", currentOfferAmount);
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
            if (sprite == null)
            {
                SetExtraMovesBadgeTextVisible(false);
                return;
            }
        }

        RefreshExtraMovesAmountVisual(animateIncrease);
    }

    private void RefreshExtraMovesAmountVisual(bool animateIncrease)
    {
        if (extraMovesIcon == null)
            return;

        EnsureExtraMovesAmountText();

        if (extraMovesAmountText == null)
            return;

        SetExtraMovesBadgeTextVisible(true);

        bool shouldAnimate = animateIncrease
            && failPopupShown
            && lastDisplayedOfferAmount > 0
            && currentOfferAmount > lastDisplayedOfferAmount;

        if (extraMovesAmountRoutine != null)
        {
            StopCoroutine(extraMovesAmountRoutine);
            extraMovesAmountRoutine = null;
        }

        if (shouldAnimate)
        {
            int previousAmount = lastDisplayedOfferAmount;
            int delta = currentOfferAmount - previousAmount;
            SetExtraMovesAmountText(extraMovesAmountText, previousAmount);
            extraMovesAmountRoutine = StartCoroutine(CoAnimateExtraMovesIncrease(previousAmount, delta, currentOfferAmount));
        }
        else
        {
            SetExtraMovesAmountText(extraMovesAmountText, currentOfferAmount);
            if (extraMovesFlyAmountText != null)
                extraMovesFlyAmountText.gameObject.SetActive(false);
        }

        lastDisplayedOfferAmount = currentOfferAmount;
    }

    private IEnumerator CoAnimateExtraMovesIncrease(int previousAmount, int delta, int finalAmount)
    {
        EnsureExtraMovesAmountText();

        if (extraMovesAmountText == null || extraMovesFlyAmountText == null)
            yield break;

        SetExtraMovesAmountText(extraMovesAmountText, previousAmount);
        SetExtraMovesAmountText(extraMovesFlyAmountText, delta);

        var mainRect = extraMovesAmountText.rectTransform;
        var flyRect = extraMovesFlyAmountText.rectTransform;
        Vector2 target = mainRect.anchoredPosition;
        Vector2 start = target + extraMovesFlyStartOffset;
        float duration = Mathf.Max(0.05f, extraMovesFlyDuration);

        extraMovesFlyAmountText.gameObject.SetActive(true);
        extraMovesFlyAmountText.alpha = 0f;
        flyRect.anchoredPosition = start;
        flyRect.localScale = Vector3.one * 0.75f;

        float elapsed = 0f;
        while (elapsed < duration && extraMovesAmountText != null && extraMovesFlyAmountText != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t);

            flyRect.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
            flyRect.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.08f, Mathf.Sin(t * Mathf.PI));
            extraMovesFlyAmountText.alpha = Mathf.Clamp01(t * 3f);

            if (t > 0.82f)
                extraMovesFlyAmountText.alpha = Mathf.InverseLerp(1f, 0.82f, t);

            yield return null;
        }

        if (extraMovesFlyAmountText != null)
            extraMovesFlyAmountText.gameObject.SetActive(false);

        if (extraMovesAmountText != null)
        {
            SetExtraMovesAmountText(extraMovesAmountText, finalAmount);
            yield return StartCoroutine(CoPunchExtraMovesAmount(mainRect));
        }

        extraMovesAmountRoutine = null;
    }

    private IEnumerator CoPunchExtraMovesAmount(RectTransform rect)
    {
        if (rect == null)
            yield break;

        const float duration = 0.18f;
        float elapsed = 0f;

        while (elapsed < duration && rect != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
            rect.localScale = Vector3.one * scale;
            yield return null;
        }

        if (rect != null)
            rect.localScale = Vector3.one;
    }

    private void EnsureExtraMovesAmountText()
    {
        if (extraMovesIcon == null)
            return;

        if (extraMovesAmountText == null)
            extraMovesAmountText = CreateExtraMovesAmountText("ExtraMovesAmountText");

        if (extraMovesFlyAmountText == null)
        {
            extraMovesFlyAmountText = CreateExtraMovesAmountText("ExtraMovesFlyAmountText");
            if (extraMovesFlyAmountText != null)
                extraMovesFlyAmountText.gameObject.SetActive(false);
        }
    }

    private TMP_Text CreateExtraMovesAmountText(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(extraMovesIcon.transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(22f, 30f);
        rect.offsetMax = new Vector2(-22f, -28f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var text = go.AddComponent<TextMeshProUGUI>();
        ConfigureExtraMovesAmountText(text);
        return text;
    }

    private void ConfigureExtraMovesAmountText(TMP_Text text)
    {
        if (text == null)
            return;

        text.raycastTarget = false;
        text.text = string.Empty;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSize = extraMovesAmountFontSize;
        text.fontSizeMax = extraMovesAmountFontSize;
        text.fontSizeMin = Mathf.Max(36f, extraMovesAmountFontSize * 0.58f);
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
        text.characterSpacing = 0f;
        text.OnPreRenderText -= ApplyExtraMovesDigitSlant;
        text.OnPreRenderText += ApplyExtraMovesDigitSlant;
        text.color = new Color(1f, 0.86f, 0.24f, 1f);
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(
            new Color(1f, 0.98f, 0.72f, 1f),
            new Color(1f, 0.92f, 0.46f, 1f),
            new Color(0.98f, 0.66f, 0.12f, 1f),
            new Color(1f, 0.72f, 0.18f, 1f));

        if (extraMovesAmountFont != null)
            text.font = extraMovesAmountFont;
        else if (failTitleText != null && failTitleText.font != null)
            text.font = failTitleText.font;

        if (extraMovesAmountMaterial != null)
            text.fontSharedMaterial = extraMovesAmountMaterial;
    }

    private void SetExtraMovesAmountText(TMP_Text text, int amount)
    {
        if (text == null)
            return;

        ConfigureExtraMovesAmountText(text);
        text.text = $"<size=88%>+</size><size=128%>{Mathf.Max(0, amount)}</size>";
    }

    private void ApplyExtraMovesDigitSlant(TMP_TextInfo textInfo)
    {
        if (textInfo == null || textInfo.characterCount == 0)
            return;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
            if (!characterInfo.isVisible || characterInfo.character == '+')
                continue;

            int materialIndex = characterInfo.materialReferenceIndex;
            if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length)
                continue;

            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
            int vertexIndex = characterInfo.vertexIndex;
            if (vertexIndex + 3 >= vertices.Length)
                continue;

            float pivotY = (vertices[vertexIndex].y
                + vertices[vertexIndex + 1].y
                + vertices[vertexIndex + 2].y
                + vertices[vertexIndex + 3].y) * 0.25f;

            for (int vertex = 0; vertex < 4; vertex++)
            {
                int currentVertex = vertexIndex + vertex;
                vertices[currentVertex].x += (vertices[currentVertex].y - pivotY) * ExtraMovesDigitSlant;
            }
        }
    }

    private void SetExtraMovesBadgeTextVisible(bool visible)
    {
        if (extraMovesAmountText != null)
            extraMovesAmountText.gameObject.SetActive(visible);

        if (extraMovesFlyAmountText != null)
            extraMovesFlyAmountText.gameObject.SetActive(false);
    }

    private void ShowFailWalletBalance(bool visible)
    {
        if (!visible)
        {
            if (failWalletBalanceRoot != null)
                failWalletBalanceRoot.SetActive(false);
            return;
        }

        EnsureFailWalletBalance();

        if (failWalletBalanceRoot == null)
            return;

        failWalletBalanceRoot.SetActive(true);
        failWalletBalanceRoot.transform.SetAsLastSibling();
        RefreshFailWalletBalance(PlayerWallet.Coins);
    }

    private void HandleWalletCoinsChanged(int amount)
    {
        RefreshFailWalletBalance(amount);
    }

    private void RefreshFailWalletBalance(int amount)
    {
        if (failWalletBalanceText != null && failWalletBalanceRoot != null && failWalletBalanceRoot.activeInHierarchy)
            failWalletBalanceText.text = amount.ToString();
    }

    private void EnsureFailWalletBalance()
    {
        if (failWalletBalanceRoot != null)
        {
            LayoutFailWalletBalance();
            ConfigureFailWalletBalanceVisuals();
            return;
        }

        if (failPopupRoot == null)
            return;

        var root = new GameObject("FailWalletBalance", typeof(RectTransform));
        root.transform.SetParent(failPopupRoot.transform, false);
        failWalletBalanceRoot = root;

        var iconGo = new GameObject("GoldMoney", typeof(RectTransform), typeof(CanvasRenderer));
        iconGo.transform.SetParent(root.transform, false);
        failWalletBalanceIcon = iconGo.AddComponent<Image>();
        failWalletBalanceIcon.raycastTarget = false;
        failWalletBalanceIcon.preserveAspect = true;

        var backgroundGo = new GameObject("WalletAmountBackground", typeof(RectTransform), typeof(CanvasRenderer));
        backgroundGo.transform.SetParent(root.transform, false);
        failWalletBalanceBackground = backgroundGo.AddComponent<Image>();
        failWalletBalanceBackground.raycastTarget = false;

        var textGo = new GameObject("WalletAmountText", typeof(RectTransform), typeof(CanvasRenderer));
        textGo.transform.SetParent(backgroundGo.transform, false);
        failWalletBalanceText = textGo.AddComponent<TextMeshProUGUI>();
        failWalletBalanceText.raycastTarget = false;

        LayoutFailWalletBalance();
        ConfigureFailWalletBalanceVisuals();
    }

    private void LayoutFailWalletBalance()
    {
        if (failWalletBalanceRoot == null)
            return;

        float backgroundHeight = Mathf.Max(1f, failWalletBalanceBackgroundHeight);
        float backgroundWidth = CalculateSpriteWidth(failWalletBalanceBackgroundSprite, backgroundHeight);
        float iconSize = Mathf.Max(1f, failWalletBalanceIconSize);
        float overlap = Mathf.Max(0f, failWalletBalanceIconOverlap);

        var rootRect = failWalletBalanceRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = failWalletBalancePosition;
        rootRect.sizeDelta = new Vector2(iconSize + backgroundWidth - overlap, Mathf.Max(iconSize, backgroundHeight));

        if (failWalletBalanceIcon != null)
        {
            var iconRect = failWalletBalanceIcon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(iconSize * 0.5f, 0f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            failWalletBalanceIcon.transform.SetAsLastSibling();
        }

        if (failWalletBalanceBackground != null)
        {
            var bgRect = failWalletBalanceBackground.rectTransform;
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = new Vector2(iconSize - overlap, 0f);
            bgRect.sizeDelta = new Vector2(backgroundWidth, backgroundHeight);
        }

        if (failWalletBalanceText != null)
        {
            var textRect = failWalletBalanceText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 8f);
            textRect.offsetMax = new Vector2(-22f, -8f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    private void ConfigureFailWalletBalanceVisuals()
    {
        Sprite iconSprite = failWalletBalanceIconSprite != null ? failWalletBalanceIconSprite : coinIcon;

        if (failWalletBalanceIcon != null)
        {
            failWalletBalanceIcon.sprite = iconSprite;
            failWalletBalanceIcon.enabled = iconSprite != null;
        }

        if (failWalletBalanceBackground != null)
        {
            failWalletBalanceBackground.sprite = failWalletBalanceBackgroundSprite;
            failWalletBalanceBackground.enabled = failWalletBalanceBackgroundSprite != null;
            failWalletBalanceBackground.preserveAspect = false;
        }

        if (failWalletBalanceText != null)
        {
            failWalletBalanceText.alignment = TextAlignmentOptions.Center;
            failWalletBalanceText.enableAutoSizing = true;
            failWalletBalanceText.fontSize = 42f;
            failWalletBalanceText.fontSizeMax = 46f;
            failWalletBalanceText.fontSizeMin = 22f;
            failWalletBalanceText.fontStyle = FontStyles.Bold;
            failWalletBalanceText.enableWordWrapping = false;
            failWalletBalanceText.overflowMode = TextOverflowModes.Ellipsis;
            failWalletBalanceText.color = Color.white;

            if (failWalletBalanceFont != null)
                failWalletBalanceText.font = failWalletBalanceFont;
            else if (failContinueText != null && failContinueText.font != null)
                failWalletBalanceText.font = failContinueText.font;

            if (failWalletBalanceMaterial != null)
                failWalletBalanceText.fontSharedMaterial = failWalletBalanceMaterial;
        }
    }

    private static float CalculateSpriteWidth(Sprite sprite, float height)
    {
        if (sprite == null || sprite.rect.height <= 0f)
            return height * 3.2f;

        return height * (sprite.rect.width / sprite.rect.height);
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

        // Fail popup keeps its authored (prefab) sprite — theme override intentionally NOT applied here so a
        // dedicated fail artwork (e.g. LevelEndFailPopupV1) isn't clobbered by levelEndPopupBackground.
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

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
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
            if (goal.obstacleId == ObstacleId.KeyGenerator)
            {
                var keySprite = tileIconLibrary != null ? tileIconLibrary.Get(TileType.Key) : null;
                return keySprite != null ? keySprite : board != null ? board.GetIcon(TileType.Key) : null;
            }

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
            color.a = 1f;
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

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void EnsureParentHierarchyActive(Transform target)
    {
        if (target == null)
            return;

        var stack = new Stack<GameObject>();
        Transform current = target;
        while (current != null)
        {
            stack.Push(current.gameObject);
            current = current.parent;
        }

        while (stack.Count > 0)
        {
            GameObject go = stack.Pop();
            if (go != null && !go.activeSelf)
                go.SetActive(true);
        }
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

        if (extraMovesAmountRoutine != null)
        {
            StopCoroutine(extraMovesAmountRoutine);
            extraMovesAmountRoutine = null;
        }

        if (extraMovesFlyAmountText != null)
            extraMovesFlyAmountText.gameObject.SetActive(false);

        if (failPopupRoot != null)
            failPopupRoot.SetActive(false);

        if (successPopupRoot != null)
            successPopupRoot.SetActive(false);

        if (successVideoRoot != null)
            successVideoRoot.SetActive(false);

        ShowFailWalletBalance(false);
        SetMainScreenDimmed(false);
        SetBlockerVisible(false);
    }
}
