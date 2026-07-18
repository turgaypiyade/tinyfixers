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

    // ── Lifecycle ────────────────────────────────────────────────

    private void Start()
    {
        ProgressEventService.OnProgressGained += OnProgressGained;
        Show();
    }

    private void OnEnable()
    {
        if (currentBar != null) Show();
    }

    private void OnDisable()
    {
        // Şov ortasında panel kapanırsa coroutine ölür — kilit asılı kalmasın.
        sessionPlaybackRunning = false;
    }

    private void OnDestroy()
    {
        ProgressEventService.OnProgressGained -= OnProgressGained;
    }

    private void Update()
    {
        if (timerText == null) return;
        var service = ProgressEventService.Instance;
        if (service == null || service.State != ProgressEventState.Active)
        {
            timerText.text = "";
            return;
        }
        timerText.text = FormatRemaining(service.TimeRemaining);
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
        if (service == null || service.State != ProgressEventState.Active) return;
        if (sessionPlaybackRunning) return;   // sıralı şov sürüyor — bar'ı ezme

        int activeIndex = FindActiveGoal(service);
        if (activeIndex < 0) { gameObject.SetActive(false); return; }

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
            StartCoroutine(CoPlaySessionQueue(queue, service));
            return;
        }

        if (activeIndex != currentGoalIndex)
            SpawnBar(activeIndex, service);

        currentBar?.RefreshDisplay();
    }

    private System.Collections.IEnumerator CoPlaySessionQueue(
        System.Collections.Generic.List<SessionGainRecord> queue, IProgressEventService service)
    {
        sessionPlaybackRunning = true;

        for (int q = 0; q < queue.Count; q++)
        {
            var rec = queue[q];
            if (rec.GoalIndex >= service.Goals.Count) continue;

            SpawnBar(rec.GoalIndex, service);
            currentBar.RefreshDisplay(applyFill: false);   // fill'i animasyon kuracak
            yield return null;
            yield return null;   // bar layout otursun (anchor hesapları doğru olsun)

            yield return currentBar.PlaySessionAnimation(rec);

            // Hedefler arası kısa nefes — oyuncu tamamlananı görsün.
            if (q < queue.Count - 1)
                yield return new WaitForSecondsRealtime(0.5f);
        }

        sessionPlaybackRunning = false;

        // Şov bitti: aktif (sıradaki) hedefin barına geç.
        int activeIndex = FindActiveGoal(service);
        if (activeIndex < 0) { gameObject.SetActive(false); yield break; }
        if (activeIndex != currentGoalIndex)
            SpawnBar(activeIndex, service);
        currentBar?.RefreshDisplay();
    }

    private void OnProgressGained(int amount)
    {
        if (sessionPlaybackRunning) return;   // sıralı şov barı yönetiyor
        var service = ProgressEventService.Instance;
        if (service == null) return;

        int activeIndex = FindActiveGoal(service);
        if (activeIndex < 0) { gameObject.SetActive(false); return; }

        // Hedef değiştiyse yeni bar spawn et
        if (activeIndex != currentGoalIndex)
            SpawnBar(activeIndex, service);

        currentBar?.RefreshDisplay();
    }

    // ── Helpers ──────────────────────────────────────────────────

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

    private static int FindActiveGoal(IProgressEventService service)
    {
        for (int i = 0; i < service.Goals.Count; i++)
            if (!service.Goals[i].IsRewardClaimed) return i;
        return -1;
    }
}
