using TMPro;
using UnityEngine;

/// Ana menüde TEK bir progress bar satırı gösterir.
/// Her zaman ilk tamamlanmamış hedefi gösterir.
/// Hedef bitince sonrakine geçer.
public class ProgressEventPanel : MonoBehaviour
{
    [SerializeField] private ProgressBarView  goalRowPrefab;
    [SerializeField] private RectTransform    particleOrigin;
    [SerializeField] private TMP_Text         timerText;

    [Header("Bar Pozisyon")]
    [SerializeField] private float barOffsetX = 11f; // sol kenar kaydırma
    [SerializeField] private float barOffsetY = 3f;  // yukarı kaydırma

    private ProgressBarView currentBar;
    private int             currentGoalIndex = -1;

    // Ana menü şov sırası (kullanıcı 2026-11-22: "önce bar ilerlesin, hedefe ulaşınca taş büyüsün küçülsün, sonra hediye"):
    // MainMenuRewardCollectFx her ödülü, kendi hedefinin barı dolup taşı nabız attıktan SONRA gösterir.
    /// Bu menü girişinde ilerleme şovu sırada ya da sürüyor.
    public static bool SessionPlaybackPending { get; private set; }
    /// Şovda ödül anına gelen (bar doldu + taş nabız attı) hedef sayısı.
    public static int RewardMomentsShown { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────

    private void Start()
    {
        ProgressEventService.OnProgressGained += OnProgressGained;
        Show();
    }

    private void OnEnable()
    {
        GameLocalization.OnLanguageChanged += ResetTimerCache;
        if (currentBar != null) Show();
    }

    private void ResetTimerCache() => lastTimerKey = long.MinValue;

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= ResetTimerCache;
        // Şov ortasında panel kapanırsa coroutine ölür — kilit asılı kalmasın, ödüller beklemesin.
        sessionPlaybackRunning = false;
        SessionPlaybackPending = false;
    }

    private void OnDestroy()
    {
        ProgressEventService.OnProgressGained -= OnProgressGained;
        SessionPlaybackPending = false;
    }

    private void Update()
    {
        if (timerText == null) return;
        var service = ProgressEventService.Instance;
        if (ShowLockedIfGated(service)) return;
        if (service == null || service.State != ProgressEventState.Active)
        {
            HideVisuals();
            return;
        }

        int activeIndex = FindActiveGoal(service);
        if (activeIndex < 0)
        {
            // Son hedef bu oturumda bittiyse şov onun barını oynatıyor: paneli kapatma, sandık bayrağını düşürme.
            // Şov bitince CoPlaySessionQueue paneli kendisi gizler.
            if (sessionPlaybackRunning)
            {
                SetTimerText(service.TimeRemaining);
                return;
            }
            HideVisuals();
            return;
        }

        bool needsRefresh = currentBar == null
            || !currentBar.gameObject.activeSelf
            || currentGoalIndex != activeIndex;

        SetVisualsActive(true);
        SetTimerText(service.TimeRemaining);

        if (!sessionPlaybackRunning && needsRefresh)
            Show();
    }

    // Süre metni dakika çözünürlüğünde: yalnız gösterilen dakika değişince yeniden üretilir (her kare string yok).
    private long lastTimerKey = long.MinValue;

    private void SetTimerText(System.TimeSpan remaining)
    {
        long key = remaining <= System.TimeSpan.Zero ? -1 : (long)remaining.TotalMinutes;
        if (key == lastTimerKey) return;
        lastTimerKey = key;
        timerText.text = FormatRemaining(remaining);
    }

    private static string FormatRemaining(System.TimeSpan t)
    {
        if (t <= System.TimeSpan.Zero)
            return GameLocalization.Get("progress_timer_ended");

        if (t.TotalHours >= 24)
        {
            int days  = (int)t.TotalDays;
            int hours = t.Hours;
            return hours > 0
                ? GameLocalization.GetFormat("progress_timer_days_hours", days, hours)
                : GameLocalization.GetFormat("progress_timer_days", days);
        }

        int h = (int)t.TotalHours;
        int m = t.Minutes;
        if (h > 0)
            return m > 0
                ? GameLocalization.GetFormat("progress_timer_hours_mins", h, m)
                : GameLocalization.GetFormat("progress_timer_hours", h);

        return GameLocalization.GetFormat("progress_timer_mins", m);
    }

    // ── Göster / Güncelle ─────────────────────────────────────────

    private bool sessionPlaybackRunning;

    private void Show()
    {
        var service = ProgressEventService.Instance;
        if (ShowLockedIfGated(service)) return;
        if (service == null || service.State != ProgressEventState.Active)
        {
            HideVisuals();
            return;
        }

        SetVisualsActive(true);
        if (sessionPlaybackRunning) return;   // sıralı şov sürüyor — bar'ı ezme

        // Session'da kazanım olan hedefler (sıralı): her biri için bar dolumu ayrı oynar —
        // aynı oturumda iki hedef bittiyse önce ilkinin barı dolar/ödül kutlanır,
        // sonra bar sıradaki hedefe geçip onunkini oynatır.
        var gains = service.ConsumeSessionGains();
        var queue = new System.Collections.Generic.List<SessionGainRecord>();
        if (gains != null)
            for (int i = 0; i < gains.Count; i++)
                if (gains[i] != null && gains[i].GainedCount > 0)
                    queue.Add(gains[i]);

        if (queue.Count > 0 && gameObject.activeInHierarchy)
        {
            SessionPlaybackPending = true;
            RewardMomentsShown = 0;
            sessionPlaybackRunning = true;
            StartCoroutine(CoPlaySessionQueue(queue, service));
            return;
        }

        // Aktif hedef aranması kuyruktan SONRA: son hedef de bittiyse (hepsi alındı) önce onun barı dolup
        // taşı nabız atsın, sandık ondan sonra açılsın; panel şovdan sonra gizlenir (CoPlaySessionQueue sonu).
        int activeIndex = FindActiveGoal(service);
        if (activeIndex < 0) { HideVisuals(); return; }

        if (activeIndex != currentGoalIndex)
            SpawnBar(activeIndex, service);

        currentBar?.RefreshDisplay();
    }

    private System.Collections.IEnumerator CoPlaySessionQueue(
        System.Collections.Generic.List<SessionGainRecord> queue, IProgressEventService service)
    {
        sessionPlaybackRunning = true;

        // Yükleme ekranının ARKASINDA oynamasın: oyuncu barın dolduğunu görsün.
        while (LoadingScreenManager.IsVisible) yield return null;
        yield return null;

        for (int q = 0; q < queue.Count; q++)
        {
            if (service.State != ProgressEventState.Active)
            {
                HideVisuals();
                yield break;
            }

            var rec = queue[q];
            if (rec.GoalIndex >= service.Goals.Count) continue;

            SpawnBar(rec.GoalIndex, service);
            currentBar.RefreshDisplay(applyFill: false);   // fill'i animasyon kuracak
            yield return null;
            yield return null;   // bar layout otursun (anchor hesapları doğru olsun)

            yield return currentBar.PlaySessionAnimation(rec);

            // Ödül anı: taş nabız attı → ödül uçuşu (MainMenuRewardCollectFx) başlar; bitene dek sıradaki bar beklesin.
            if (rec.RewardGranted && rec.Reward != null)
            {
                RewardMomentsShown++;
                yield return null;
                yield return null;   // uçuş coroutine'u RewardFlying'i kursun (çalışma sırası farkı)
                while (MainMenuRewardCollectFx.RewardFlying) yield return null;
            }

            // Hedefler arası kısa nefes — oyuncu tamamlananı görsün.
            if (q < queue.Count - 1)
                yield return new WaitForSecondsRealtime(0.5f);
        }

        sessionPlaybackRunning = false;
        SessionPlaybackPending = false;

        // Şov bitti: aktif (sıradaki) hedefin barına geç.
        if (service.State != ProgressEventState.Active)
        {
            HideVisuals();
            yield break;
        }

        int activeIndex = FindActiveGoal(service);
        if (activeIndex < 0) { HideVisuals(); yield break; }
        if (activeIndex != currentGoalIndex)
            SpawnBar(activeIndex, service);
        currentBar?.RefreshDisplay();
    }

    private void OnProgressGained(int amount)
    {
        if (sessionPlaybackRunning) return;   // sıralı şov barı yönetiyor
        var service = ProgressEventService.Instance;
        if (service == null || service.State != ProgressEventState.Active)
        {
            HideVisuals();
            return;
        }

        int activeIndex = FindActiveGoal(service);
        if (activeIndex < 0) { HideVisuals(); return; }

        // Hedef değiştiyse yeni bar spawn et
        if (activeIndex != currentGoalIndex)
            SpawnBar(activeIndex, service);

        currentBar?.RefreshDisplay();
    }

    // ── Helpers ──────────────────────────────────────────────────

    // Açılış seviyesine gelinmediyse panel boş kalmaz (kullanıcı 2026-11-10): ilk hedef + ilk ödülle dolu çubuk ve sayaç
    // kutusu birlikte gri, köşede kilit; sayaç kutusunda "SEVİYE N". Gelince kilit kalkar, normal akış sürer.
    private bool lockedBarFilled;

    private bool ShowLockedIfGated(ProgressEventService service)
    {
        if (service == null || !service.IsLevelLocked || service.Goals == null || service.Goals.Count == 0)
        {
            EventLockOverlay.Release(gameObject);
            lockedBarFilled = false;
            return false;
        }
        if (currentBar == null || currentGoalIndex != 0) { SpawnBar(0, service); lockedBarFilled = false; }
        SetVisualsActive(true);
        if (!lockedBarFilled && currentBar != null) { currentBar.RefreshDisplay(); lockedBarFilled = true; }
        EventLockOverlay.Apply(gameObject, service.UnlockLevel, timerText, keepTexts: true);
        lastTimerKey = long.MinValue;   // kilit metni süre yazısının yerini aldı: kilit kalkınca süre yeniden yazılsın
        return true;
    }

    private void SpawnBar(int index, IProgressEventService service)
    {
        if (currentBar != null) Destroy(currentBar.gameObject);

        currentGoalIndex = index;
        currentBar       = Instantiate(goalRowPrefab, transform);

        var rt = currentBar.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(barOffsetX, barOffsetY);
        rt.offsetMax = new Vector2(0f,         barOffsetY);

        currentBar.SetGoalIndex(index);
        if (particleOrigin != null)
            currentBar.SetParticleOrigin(particleOrigin);
    }

    private void HideVisuals()
    {
        if (timerText != null)
            timerText.text = "";
        lastTimerKey = long.MinValue;

        SetVisualsActive(false);
        sessionPlaybackRunning = false;
        SessionPlaybackPending = false;
    }

    private void SetVisualsActive(bool active)
    {
        var timerRoot = timerText != null && timerText.transform.parent != null
            ? timerText.transform.parent.gameObject
            : timerText != null ? timerText.gameObject : null;

        if (timerRoot != null && timerRoot.activeSelf != active)
            timerRoot.SetActive(active);

        if (currentBar != null && currentBar.gameObject.activeSelf != active)
            currentBar.gameObject.SetActive(active);
    }

    private static int FindActiveGoal(IProgressEventService service)
    {
        for (int i = 0; i < service.Goals.Count; i++)
            if (!service.Goals[i].IsRewardClaimed) return i;
        return -1;
    }
}
