using System;
using System.Collections;
using UnityEngine;

public enum SafariRoundOutcome { None, Advanced, Fell, Completed }

/// <summary>
/// Tiny Safari eventinin MainMenu koordinatörü.
///  - Aktiflik: schedule (Ptesi/Çrş/Ctesi/Pazar, 24s) + level kapısı (≥ minLevelGate).
///  - Otomatik "katıl" popup'ı: ilk aktivasyonda ve hiç katılmadıysa saatte bir.
///  - "Devam": kaldığı leveli başlatır; MainMenu'ye dönünce ilk-hak snapshot'ıyla sonucu değerlendirir
///    (arttı → ilerle, aynı → düş) ve haritayı uygun animasyonla açar.
///  - 7. pitstopa ulaşınca büyük ödülü verir.
///
/// Kurulum: MainMenu sahnesinde bir objeye ekle; ikon/popup/harita referanslarını bağla.
/// Config boşsa Resources/Events/SafariConfig otomatik yüklenir.
/// </summary>
public sealed class SafariEventController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private SafariConfig config;

    [Header("UI Refs")]
    [SerializeField] private SafariEventButton         eventButton;
    [SerializeField] private SafariJoinPopupController joinPopup;
    [SerializeField] private SafariMapScreenBase       mapScreen;
    [SerializeField] private RisingIntroOverlay        risingIntroOverlay;

    public SafariConfig Config => config;

    /// <summary>Bu oturumda düşülmeden önce bulunulan pitstop (düşüş animasyonu buradan başlar). -1 = yok.</summary>
    public int FallFromPitstop { get; private set; } = -1;

    /// <summary>Son tamamlamada oyuncuya düşen ödül payı (UI göstermek için). 0 = henüz yok.</summary>
    public int LastRewardShare { get; private set; }
    public bool FinalRewardClaimed { get; private set; }

    private static DateTime UtcNow => DateTime.UtcNow;

    private void Awake()
    {
        if (config == null)
            config = Resources.Load<SafariConfig>("Events/SafariConfig");
    }

    private void Start()
    {
        if (config == null)
        {
            Debug.LogWarning("[Safari] SafariConfig yok (Resources/Events/SafariConfig). Event devre dışı.");
            eventButton?.SetVisible(false);
            return;
        }

        SafariState.SyncCycle(config, UtcNow);

        bool available = IsEventAvailable;
        eventButton?.SetVisible(available);
        if (!available) return;

        // Level'den yeni döndüysek sonucu değerlendir ve haritayı animasyonla aç.
        // Tutorial/overlay ekrandayken AÇMA — üstüne binip input'u kilitler. Temizlenince aç.
        if (SafariState.RunStatus == SafariRunStatus.AwaitingResult)
        {
            var outcome = EvaluateReturn();
            StartCoroutine(RunWhenClear(() => OpenMap(outcome)));
            return;
        }

        // Otomatik popup YALNIZ config'te açıksa (şimdilik kapalı → yalnız ikona tıklayınca).
        if (config.autoShowJoinPopup && ShouldAutoAsk())
            StartCoroutine(RunWhenClear(() =>
            {
                if (IsEventAvailable && !SafariState.HasJoined) ShowJoinPopup();
            }));
    }

    // Tutorial/overlay ekrandayken Safari UI'ı açılmasın (deadlock önlemi). Temizlenince action çalışır.
    private IEnumerator RunWhenClear(Action action)
    {
        const float maxWait = 60f;
        float t = 0f;
        while (t < maxWait && IsTutorialBlocking())
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        action?.Invoke();
    }

    private bool IsTutorialBlocking()
    {
        if (FindFirstObjectByType<TutorialOverlayController>() != null) return true;
        // Mission/workshop tutorial'ı ilk kez gösteriliyorsa (görülmedi + sahnede) engelle.
        if (!WorkshopRepairButtonTutorial.IsSeen() &&
            FindFirstObjectByType<WorkshopRepairButtonTutorial>() != null) return true;
        return false;
    }

    // ── Aktiflik ─────────────────────────────────────────────────

    public bool IsEventAvailable
    {
        get
        {
            if (config == null) return false;
#if UNITY_EDITOR
            if (config.debugForceAvailable) return true;   // editor-only test override
#endif
            return CurrentLevel.Global >= config.minLevelGate &&
                   SafariSchedule.IsActiveNow(config, UtcNow);
        }
    }

    // ── İkon tıklaması ───────────────────────────────────────────

    public void OnIconClicked()
    {
        if (!IsEventAvailable) return;
        if (IsTutorialBlocking()) return;   // tutorial açıkken açma

        // Düşüş sonrası yeniden katılım: kaybedince pitstop sıfırlanır + 30dk cooldown başlar,
        // ama HasJoined true kalır. Cooldown bitince ikona basmak taze bir yarışa "yeniden katılım"dır
        // → katıl-popup + intro matchmaking overlay tekrar oynasın (kullanıcı kararı).
        // (Cooldown boyunca SafariEventButton butonu pasif; bu noktaya normalde gelinmez.)
        bool needsRejoin = SafariState.RunStatus == SafariRunStatus.Fell;

        // Güvenlik önlemi: event devam ediyorsa (katıldı & düşmedi) tekrar ikona basmak her şeyi
        // baştan başlatmasın → doğrudan kaldığı kata (CurrentPitstop) git. Progress korunur.
        // Yeni pencere (cycle) açıldığında SyncCycle HasJoined'i sıfırlar → o zaman popup çıkar.
        if (SafariState.HasJoined && !needsRejoin)
        {
            OpenMap(SafariRoundOutcome.None);
            return;
        }

        ShowJoinPopup();
    }

    // ── Popup akışı ──────────────────────────────────────────────

    private bool ShouldAutoAsk()
    {
        if (SafariState.HasJoined) return false;

        // YALNIZ ilk aktivasyonda bir kez sor: event bu cycle'da yaratılıp uygun an bulununca
        // popup çıkar; sonra (katılsa da katılmasa da) tekrar nag YOK — ikon manuel tıklama için kalır.
        // Yeni pencere (cycle) → SyncCycle LastAskUtc'yi sıfırlar → yeni event'te tekrar bir kez sorulur.
        return SafariState.LastAskUtc == DateTime.MinValue;
    }

    private void ShowJoinPopup()
    {
        SafariState.MarkAsked(UtcNow);
        if (joinPopup != null) joinPopup.Show(this);
    }

    /// <summary>Popup "Katıl" — katılımı işaretle ve haritayı aç.</summary>
    public void OnJoinAccepted()
    {
        SafariState.MarkJoined(UtcNow);
        // Düşüş sonrası yeniden katılımda RunStatus=Fell idi; kabul edilince taze tura geç —
        // aksi halde her ikon tıklaması intro'yu tekrar oynatırdı. İlk katılımda zaten Idle.
        if (SafariState.RunStatus == SafariRunStatus.Fell)
            SafariState.SetRunStatus(SafariRunStatus.Idle);
        if (risingIntroOverlay != null)
        {
            risingIntroOverlay.Show(this);
            return;
        }
        OpenMap(SafariRoundOutcome.None);
    }

    public void OpenMapFromIntro()
    {
        OpenMap(SafariRoundOutcome.None);
    }

    /// <summary>Popup "Şimdi değil" — ikon aktif kalır, kural 1 geçerli.</summary>
    public void OnJoinDeclined()
    {
        joinPopup?.Hide();
    }

    // ── Harita & tur ─────────────────────────────────────────────

    private void OpenMap(SafariRoundOutcome outcome)
    {
        if (mapScreen == null) return;
        mapScreen.Open(this, outcome);
    }

    /// <summary>Düşme cooldown'ı dolmadıysa devam kilitli.</summary>
    public bool CanContinueNow(out TimeSpan remaining)
    {
        remaining = SafariState.FallCooldownRemaining(UtcNow);
        return remaining <= TimeSpan.Zero;
    }

    /// <summary>Harita "Devam" — kaldığı leveli başlat (sonucu dönüşte değerlendireceğiz).</summary>
    public void RequestContinue()
    {
        if (!CanContinueNow(out _)) return;

        if (!SafariState.HasJoined) SafariState.MarkJoined(UtcNow);

        SafariState.SnapshotFirstTryClears(PlayerStats.FirstTryClears);
        SafariState.SetRunStatus(SafariRunStatus.AwaitingResult);

        // Safari UI'ını kapat: yoksa level başlatmanın açtığı pre-level popup / loading ekranı
        // safari harita overlay'inin ARKASINDA kalır ve "hiçbir şey olmuyor" gibi görünür.
        mapScreen?.Hide();
        joinPopup?.Hide();

        LaunchCurrentLevel();
    }

    private void LaunchCurrentLevel()
    {
        var launcher = FindFirstObjectByType<MainMenuLevelButtonController>();
        if (launcher != null)
        {
            Debug.Log("[Safari] Devam → level başlatılıyor (OnLevelButtonClicked).");
            launcher.OnLevelButtonClicked();
        }
        else
        {
            Debug.LogWarning("[Safari] MainMenuLevelButtonController bulunamadı — level başlatılamadı.");
            // Sonuç değerlendirmesi asılı kalmasın.
            SafariState.SetRunStatus(SafariRunStatus.Idle);
        }
    }

    // ── Level dönüş değerlendirmesi ──────────────────────────────

    private SafariRoundOutcome EvaluateReturn()
    {
        bool firstTryWin = PlayerStats.FirstTryClears > SafariState.FirstTryClearsSnapshot;

        if (!firstTryWin)
        {
            // İlk-hakta geçemedi → düş. Düşüş, bulunulan pitstoptan başlasın diye önce hatırla,
            // sonra ilerleme SIFIRLANIR: 30 dk sonra tekrar katılınca 1. pitstoptan başlanır.
            // (Oyuncunun normal oyun level'ı CurrentLevel.Global etkilenmez; yalnız safari sayacı.)
            FallFromPitstop = SafariState.CurrentPitstop;
            SafariState.SetPitstop(0);
            SafariState.SetRunStatus(SafariRunStatus.Fell);
            SafariState.StartFallCooldown(UtcNow, config.fallCooldownMinutes);
            Debug.Log("[Safari] Tur sonucu: DÜŞTÜ → pitstop sıfırlandı, 30dk cooldown.");
            return SafariRoundOutcome.Fell;
        }

        int pit = SafariState.CurrentPitstop + 1;
        SafariState.SetPitstop(pit);

        if (pit >= config.pitstopCount)
        {
            PrepareFinalReward(config.simulatedWinnerCount);
            SafariState.SetRunStatus(SafariRunStatus.Completed);
            Debug.Log("[Safari] Tur sonucu: TAMAMLANDI (7. pitstop).");
            return SafariRoundOutcome.Completed;
        }

        SafariState.SetRunStatus(SafariRunStatus.Idle);
        Debug.Log($"[Safari] Tur sonucu: İLERLEDİ → pitstop {pit}.");
        return SafariRoundOutcome.Advanced;
    }

    private void PrepareFinalReward(int winners)
    {
        // Ödül havuzu tüm kazananlar arasında paylaşılır → oyuncunun payı = havuz / kazanan sayısı.
        winners = Mathf.Max(1, winners);
        int pool = config != null ? config.prizePoolGold : 0;
        LastRewardShare = Mathf.Max(1, pool / winners);
    }

    public void ClaimFinalReward(int share, int winners)
    {
        if (FinalRewardClaimed) return;

        winners = Mathf.Max(1, winners);
        LastRewardShare = Mathf.Max(1, share);
        PlayerWallet.AddCoins(LastRewardShare);
        FinalRewardClaimed = true;
        int pool = config != null ? config.prizePoolGold : 0;
        Debug.Log($"[Safari] Ödül claim: {pool} altın / {winners} kazanan → oyuncu payı {LastRewardShare}.");
    }
}
