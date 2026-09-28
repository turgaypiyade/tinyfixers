using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Bridge Repair eventinin MainMenu koordinatörü (Safari akışıyla aynı iskelet, ayrı sistem):
///  - Aktiflik: haftada 2 rastgele gün × 24 saat + level kapısı (30. seviye bitince).
///  - İlk aktivasyonda bir kez otomatik katılım popup'ı; "Devam" → Nasıl oynanır overlay'i → yarış ekranı.
///  - Kazanılan her level köprüye bir parça ekler (sayım BridgeRepairState'te, kazanma anında). Ana menüye
///    dönünce yeni ilerleme varsa yarış ekranı açılıp oynatılır.
///  - Bitiş: ödül kazanma anında verilir, burada sandık töreni gösterilir. Eleme (ilk 3 doldu): bilgi bandı.
///
/// Kurulum: TinyFixers ▸ Mockup ▸ Bridge Repair Event (MainMenu sahnesi açıkken).
/// </summary>
public sealed class BridgeRepairController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BridgeRepairConfig config;

    [Header("UI Refs")]
    [SerializeField] private BridgeRepairEventButton eventButton;
    [SerializeField] private BridgeRepairMapScreen mapScreen;
    [Tooltip("Safari popup'ının kopyası (kurulum sahnedeki SafariJoinPopup'tan üretir).")]
    [SerializeField] private BridgeRepairJoinPopup joinPopup;
    [Tooltip("Popup ve Nasıl oynanır overlay'lerinin açılacağı kök (boşsa bu objenin kök canvas'ı).")]
    [SerializeField] private Transform overlayParent;

    [Header("Popup / Nasıl oynanır görselleri")]
    [SerializeField] private Sprite continueButton;
    [SerializeField] private Sprite checkMark;
    [SerializeField] private Sprite coin;
    [SerializeField] private Sprite chest;

    [Header("Kazanma ekranı")]
    [Tooltip("Açık: önce kutlama ekranı (kurdele + sıra + karakter + ödül önizleme), 'Ödülü Al' → sandık töreni. " +
             "Kapalı: doğrudan sandık töreni (Missions/Wonder ödülüyle aynı).")]
    [SerializeField] private bool showVictoryIntro = true;
    [SerializeField] private Sprite victoryRibbon;
    [SerializeField] private Material victoryLabelMaterial;

    [Tooltip("İlk aktivasyon otomatik popup'ı için ana menü yüklendikten sonra beklenecek ek süre (sn).")]
    [SerializeField] private float autoPopupDelaySeconds = 2f;

    [Header("Level öncesi event şeridi")]
    [Tooltip("Şerit kartındaki ikon. Boşsa ana menü event ikonunun görseli.")]
    [SerializeField] private Sprite promoIcon;

    private float nextVisibilityCheck;
    private BridgeRepairHowToPlay howToPlay;
    private Coroutine pendingMapOpen;

    public BridgeRepairConfig Config => config;
    private static DateTime UtcNow => DateTime.UtcNow;

    public DateTime WindowEnd
    {
        get
        {
            DateTime end = BridgeRepairSchedule.GetWindowEnd(config, UtcNow);
#if UNITY_EDITOR
            // Debug zorlamada gerçek pencere yok → sanal pencere (bugün 00:00 UTC + windowHours) → süre hep görünür.
            if (end == DateTime.MinValue && config != null && config.debugForceAvailable)
                end = UtcNow.Date.AddHours(Mathf.Max(1, config.windowHours));
#endif
            return end;
        }
    }

    /// Yarış bitti, sonucu görüldü ve yeni giriş beklemesi sürüyor → ikon pasif, geri sayım gösterir.
    public TimeSpan RejoinCooldown =>
        BridgeRepairState.EndPresented ? BridgeRepairRace.RejoinCooldownRemaining(config, UtcNow) : TimeSpan.Zero;
    public bool IsRunLive => BridgeRepairRace.IsRunLive(config, UtcNow);
    public bool CanPlay => IsRunLive && !BridgeRepairState.IsFinished && !BridgeRepairRace.IsEliminated(config, UtcNow);

    public bool IsEventAvailable
    {
        get
        {
            if (config == null) return false;
#if UNITY_EDITOR
            if (config.debugForceAvailable) return true;   // editor-only test override
#endif
            return CurrentLevel.Global >= config.minLevelGate && BridgeRepairSchedule.IsActiveNow(config, UtcNow);
        }
    }

    private void Awake()
    {
        if (config == null) config = BridgeRepairConfig.Shared;
        if (overlayParent == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            overlayParent = canvas != null ? canvas.rootCanvas.transform : transform;
        }
    }

    private void Start()
    {
        if (mapScreen != null) mapScreen.gameObject.SetActive(false);
        if (joinPopup != null) joinPopup.gameObject.SetActive(false);

        if (config == null)
        {
            Debug.LogWarning("[BridgeRepair] Config yok (Resources/Events/BridgeRepairConfig). Event devre dışı.");
            eventButton?.SetVisible(false);
            return;
        }

        BridgeRepairState.SyncCycle(config, UtcNow);
        bool available = IsEventAvailable;
        eventButton?.SetVisible(available);
        if (!available) return;

        if (BridgeRepairState.HasJoined)
        {
            if (NeedsPresentation())
                StartCoroutine(RunWhenClear(() => OpenMap(animate: true), waitForLoadingScreen: true));
            return;
        }

        if (config.autoShowJoinPopup && BridgeRepairState.LastAskUtc == DateTime.MinValue)
            StartCoroutine(RunWhenClear(() =>
            {
                if (IsEventAvailable && !BridgeRepairState.HasJoined) ShowJoinPopup();
            }, autoPopupDelaySeconds, waitForLoadingScreen: true));
    }

    private void Update()
    {
        // Pencere oyun açıkken biterse ikon kaybolsun (saniyede bir kontrol).
        if (config == null || Time.unscaledTime < nextVisibilityCheck) return;
        nextVisibilityCheck = Time.unscaledTime + 1f;
        eventButton?.SetVisible(IsEventAvailable);
    }

    private bool NeedsPresentation()
    {
        if (BridgeRepairState.Wins > BridgeRepairState.PresentedWins) return true;
        if (BridgeRepairState.EndPresented) return false;
        return BridgeRepairState.IsFinished || BridgeRepairRace.IsEliminated(config, UtcNow);
    }

    // ── İkon ─────────────────────────────────────────────────────

    public void OnIconClicked()
    {
        if (!IsEventAvailable || IsTutorialBlocking()) return;
        if (!BridgeRepairState.HasJoined) { ShowJoinPopup(); return; }

        // Yarış bitti: sonuç görülmediyse önce onu göster; görüldüyse 20 dk bekle, sonra yeni yarış (Safari gibi).
        if (BridgeRepairRace.RunEnded(config, UtcNow) && BridgeRepairState.EndPresented)
        {
            if (RejoinCooldown > TimeSpan.Zero) return;
            ShowJoinPopup();
            return;
        }
        OpenMap(animate: true);   // son görülenden bu yana olanları oynat
    }

    // ── Katılım ──────────────────────────────────────────────────

    private void ShowJoinPopup()
    {
        BridgeRepairState.MarkAsked(UtcNow);
        if (joinPopup == null)
        {
            Debug.LogWarning("[BridgeRepair] Katılım popup'ı yok — kurulumu (TinyFixers ▸ Mockup ▸ Bridge Repair Event) tekrar çalıştır.");
            OnJoinAccepted();
            return;
        }
        joinPopup.Show(WindowEnd, OnJoinAccepted, null);
    }

    private void OnJoinAccepted()
    {
        if (!IsEventAvailable) return;
        BridgeRepairState.BeginRun(UtcNow, BridgeRepairRace.GenerateBotNames(config));
        Debug.Log("[BridgeRepair] Yarışa katıldı.");
        if (BridgeRepairState.HowToShownThisCycle) { OpenMap(animate: false); return; }
        BridgeRepairState.MarkHowToShown();
        ShowHowToPlay(() => OpenMap(animate: false));
    }

    public void ShowHowToPlay(Action then)
    {
        howToPlay = BridgeRepairHowToPlay.Show(overlayParent, new BridgeRepairHowToPlay.Art
        {
            config = config,
            checkMark = checkMark,
            coin = coin,
            chest = chest != null ? chest : Resources.Load<Sprite>("UI/RewardChestClosed"),
        }, () =>
        {
            howToPlay = null;
            then?.Invoke();
        });
    }

    // ── Yarış ekranı ─────────────────────────────────────────────

    private void OpenMap(bool animate)
    {
        if (mapScreen == null) return;
        if (pendingMapOpen != null) StopCoroutine(pendingMapOpen);
        pendingMapOpen = StartCoroutine(OpenMapWhenVisible(animate));
    }

    private bool IsPresentationBlocked => LoadingScreenManager.IsVisible ||
        CustomIntroLoadingManager.IsVisible ||
        (howToPlay != null && howToPlay.gameObject.activeInHierarchy) || IsTutorialBlocking();

    private IEnumerator OpenMapWhenVisible(bool animate)
    {
        // Overlay Destroy(), diğer Start() çağrıları ve Canvas görünürlüğü bir karede tamamlansın.
        yield return null;
        while (IsPresentationBlocked) yield return null;
        pendingMapOpen = null;
        if (mapScreen != null) mapScreen.Open(this, animate);
    }

    private void OnEnable() => PreLevelEventPromoRegistry.Register(PromoKey, BuildPromo);

    private void OnDisable()
    {
        PreLevelEventPromoRegistry.Unregister(PromoKey);
        StopAllCoroutines();
        pendingMapOpen = null;
    }

    // ── Level öncesi event şeridi ────────────────────────────────

    private const string PromoKey = "bridge_repair";

    /// Şu an katılınabilir mi: ikona basınca katılım popup'ı açılacak durumla aynı (OnIconClicked) —
    /// hiç katılmadı ya da yarışı bitti, sonucu gördü ve yeni giriş beklemesi doldu. Yalnız GERÇEK takvimde
    /// aktifken: editör debugForceAvailable şeridi açmaz (kullanıcı kuralı: aktif olmayan event hiçbir şey göstermez).
    private bool CanJoinNow
    {
        get
        {
            if (config == null || CurrentLevel.Global < config.minLevelGate
                || !BridgeRepairSchedule.IsActiveNow(config, UtcNow)) return false;
            if (!BridgeRepairState.HasJoined) return true;
            return BridgeRepairRace.RunEnded(config, UtcNow) && BridgeRepairState.EndPresented
                && RejoinCooldown <= TimeSpan.Zero;
        }
    }

    // Şerit kartı yalnız katılınabilirken (yarış sürerken kart yok).
    private PreLevelEventPromo? BuildPromo()
    {
        if (!CanJoinNow) return null;
        string title = BridgeRepairUI.L("bridge_title", "BRIDGE REPAIR");
        string tagline = BridgeRepairUI.LFormat("bridge_subtitle", "{0} seviye kazan, ödülleri kap!", config.levelsToFinish);
        return new PreLevelEventPromo(title, tagline, ResolvePromoIcon(), WindowEnd, JoinFromPromo);
    }

    private Sprite ResolvePromoIcon()
    {
        if (promoIcon != null) return promoIcon;
        var buttonImage = eventButton != null && eventButton.TryGetComponent(out UnityEngine.UI.Button b)
            ? b.image : null;
        return buttonImage != null ? buttonImage.sprite : null;
    }

    /// <summary>Şeritteki "Katıl": katılım popup'ı atlanır (oyuncu zaten karar verdi) → doğrudan katıl.</summary>
    public void JoinFromPromo()
    {
        if (!CanJoinNow) return;
        BridgeRepairState.MarkAsked(UtcNow);
        OnJoinAccepted();
    }

    /// Yarış ekranı "Oyna" — sıradaki normal leveli başlatır (ayrı level yok; kazanç kancadan sayılır).
    public void RequestPlay()
    {
        if (!CanPlay) return;
        var launcher = FindFirstObjectByType<MainMenuLevelButtonController>(FindObjectsInactive.Include);
        if (launcher == null)
        {
            Debug.LogWarning("[BridgeRepair] MainMenuLevelButtonController bulunamadı — Oyna iptal.");
            return;
        }

        // Yarış ekranı açık kalırsa pre-level popup / loading arkasında kalır.
        mapScreen?.Hide();
        var result = launcher.StartLevel();
        if (result == LevelStartResult.Suppressed) OpenMap(animate: false);
    }

    /// Harita ilerlemeyi oynattıktan sonra: bitiş töreni veya eleme bandı (her biri bir kez).
    public void OnMapPresented(BridgeRepairMapScreen map)
    {
        if (BridgeRepairState.EndPresented) return;

        if (BridgeRepairState.IsFinished)
        {
            BridgeRepairState.MarkEndPresented();
            ShowVictory(BridgeRepairState.FinalRank);
            return;
        }

        if (BridgeRepairRace.IsEliminated(config, UtcNow))
        {
            BridgeRepairState.MarkEndPresented();
            map.ShowEndBanner(
                BridgeRepairUI.L("bridge_eliminated_title", "YARIŞ BİTTİ"),
                BridgeRepairUI.LFormat("bridge_eliminated_body",
                    "İlk {0} sıra doldu. Bir sonraki yarışta görüşürüz!", config.prizeRanks),
                null);
        }
    }

    /// Kazanma ekranı + sandık töreni. Ödül burada VERİLMEZ (kazanma anında verildi) → önizleme güvenli.
    public void ShowVictory(int rank)
    {
        var rewards = config.RewardsForRank(rank);
        if (!showVictoryIntro)
        {
            if (rewards != null && rewards.Count > 0) RewardChestRevealOverlay.Show(rewards);
            return;
        }

        var contestants = BridgeRepairRace.BuildContestants(config);
        BridgeCharacterDef character = null;
        if (contestants.Count > 0 && config.characters != null && config.characters.Count > 0)
            character = config.characters[Mathf.Clamp(contestants[0].characterIndex, 0, config.characters.Count - 1)];

        BridgeRepairVictoryView.Show(overlayParent, rank, config.contestantCount, character, rewards,
            new BridgeRepairVictoryView.Art
            {
                ribbon = victoryRibbon,
                button = continueButton,
                chestClosed = chest,
                eventLabelMaterial = victoryLabelMaterial,
            });
    }

    // ── Tutorial / loading bekleme (Safari ile aynı kural) ──────

    private IEnumerator RunWhenClear(Action action, float initialDelay = 0f, bool waitForLoadingScreen = false)
    {
        yield return null;
        while (waitForLoadingScreen && (LoadingScreenManager.IsVisible || CustomIntroLoadingManager.IsVisible))
            yield return null;
        if (initialDelay > 0f) yield return new WaitForSecondsRealtime(initialDelay);
        // Süre doldu diye bilgi katmanının arkasında sunumu başlatma.
        while (IsPresentationBlocked)
            yield return null;
        action?.Invoke();
    }

    private static bool IsTutorialBlocking()
    {
        if (FindFirstObjectByType<TutorialOverlayController>() != null) return true;
        if (!WorkshopRepairButtonTutorial.IsSeen() &&
            FindFirstObjectByType<WorkshopRepairButtonTutorial>() != null) return true;
        return false;
    }
}
