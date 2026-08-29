using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Tek bir progress hedefinin UI satırı.
/// ProgressEventPanel tarafından yönetilir — doğrudan OnEnable'da servis okumaz.
public class ProgressBarView : MonoBehaviour
{
    [Header("Hedef")]
    [SerializeField] private int goalIndex = 0;
    public int GoalIndex => goalIndex;

    public void SetGoalIndex(int index)       => goalIndex      = index;
    public void SetParticleOrigin(RectTransform o) => particleOrigin = o;

    [Header("UI")]
    [SerializeField] private Image         goalIconImage;
    [SerializeField] private Image         progressFill;   // Image Type: Sliced, 9-slice yeşil sprite
    [SerializeField] private RectTransform barTrack;       // Mavi dikdörtgenin RectTransform'u
    [SerializeField] private TMP_Text      progressText;   // "47 / 100"
    [SerializeField] private TMP_Text      descriptionText;
    [SerializeField] private Image         rewardIconImage;
    [SerializeField] private Image         rewardWaveImage;
    [SerializeField] private TMP_Text      rewardAmountText; // "+3" veya "15dk"
    [SerializeField] private GameObject    claimedOverlay;

    [Header("Akış Animasyonu")]
    [Tooltip("Partiküllerin çıkış noktası (ekran üstündeki event ikonu).")]
    [SerializeField] private RectTransform particleOrigin;
    [Tooltip("Uçan küçük ikon prefabı (Image component'li).")]
    [SerializeField] private GameObject    particlePrefab;
    [SerializeField] private float flightDuration  = 0.35f;
    [SerializeField] private int   maxParticles    = 15;

    [Header("Dolum Süresi")]
    [Tooltip("Küçük kazanımlarda bar dolum süresi (sn).")]
    [SerializeField, Min(0.2f)] private float minFillDuration = 1.2f;
    [Tooltip("Bar 0'dan tama dolarken süre (sn) — kazanım büyüdükçe buna yaklaşır.")]
    [SerializeField, Min(0.5f)] private float maxFillDuration = 2.8f;

    private IProgressEventService service;
    private Coroutine             activeRoutine;

    // ── Panel tarafından çağrılır ─────────────────────────────────

    public void RefreshDisplay() => RefreshDisplay(applyFill: true);

    /// applyFill=false: yalnızca ikon/metinleri bağlar; fill'e dokunmaz (session
    /// animasyonu başlangıç dolumunu kendisi kuracaksa yarış çıkmasın diye).
    public void RefreshDisplay(bool applyFill)
    {
        service = ProgressEventService.Instance;
        if (service == null || goalIndex >= service.Goals.Count) return;

        var goal = service.Goals[goalIndex];
        // Goal veya Definition null olabilir (örn. doğrudan Game sahnesinden başlayınca ProgressEvent
        // ana menüde kurulmadan goal'ler sıfırlanır) → def.* erişimleri NRE atar. Sessizce atla.
        if (goal == null || goal.Definition == null) return;
        var def  = goal.Definition;

        if (goalIconImage   != null) goalIconImage.sprite   = goal.DisplayIcon;
        if (rewardIconImage != null && def.reward != null)
            rewardIconImage.sprite = def.reward.ResolveIcon();
        if (rewardWaveImage != null)
        {
            rewardWaveImage.sprite  = def.rewardWaveSprite;
            rewardWaveImage.enabled = def.rewardWaveSprite != null;
        }
        if (rewardAmountText != null && def.reward != null)
        {
            rewardAmountText.text = def.rewardDurationMinutes > 0
                ? $"+{def.rewardDurationMinutes}dk"
                : $"+{def.reward.amount}";
        }
        if (descriptionText != null) descriptionText.text   = def.fallbackDescription;
        if (claimedOverlay  != null) claimedOverlay.SetActive(goal.IsRewardClaimed);

        if (applyFill)
            StartCoroutine(CoApplyFillNextFrame(goal.NormalizedProgress, goal.CurrentCount, def.targetCount));
    }

    private IEnumerator CoApplyFillNextFrame(float normalized, int current, int target)
    {
        yield return null;
        yield return null; // anchor layout'un settle olması için 2 frame bekle
        ApplyFill(normalized, current, target);
    }

    public void StartSessionAnimation(SessionGainRecord gain)
    {
        service = ProgressEventService.Instance;
        if (service == null || goalIndex >= service.Goals.Count) return;
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(CoAnimateGains(gain));
    }

    /// Panel'in ard arda birden çok hedefi oynatabilmesi için beklenebilir sürüm.
    public IEnumerator PlaySessionAnimation(SessionGainRecord gain)
    {
        service = ProgressEventService.Instance;
        if (service == null || goalIndex >= service.Goals.Count) yield break;
        yield return CoAnimateGains(gain);
    }

    // ── Animasyonlar ─────────────────────────────────────────────

    private IEnumerator CoAnimateGains(SessionGainRecord gain)
    {
        var goal   = service.Goals[goalIndex];
        if (goal == null || goal.Definition == null) yield break;
        int target = goal.Definition.targetCount;

        int   startCount = Mathf.Max(0, goal.CurrentCount - gain.GainedCount);
        float startFill  = (float)startCount / target;
        float endFill    = goal.NormalizedProgress;

        // Süre kazanım oranıyla ölçeklenir: küçük kazanım kısa, tam dolum (0→600) uzun —
        // hedef tamamlandıysa doluşu gerçekten İZLETİR.
        float dur = Mathf.Lerp(minFillDuration, maxFillDuration, Mathf.Clamp01(endFill - startFill));

        int   particleCount = Mathf.Clamp(gain.GainedCount, 1, maxParticles);
        float particleEvery = dur / particleCount;
        float nextParticleAt = 0f;
        int   spawnedParticles = 0;

        ApplyFill(startFill, startCount, target);

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - Mathf.Pow(1f - k, 2f);   // ease-out: son kısımda yavaşlayıp "oturur"

            float fill = Mathf.Lerp(startFill, endFill, e);
            int   cnt  = Mathf.RoundToInt(Mathf.Lerp(startCount, goal.CurrentCount, e));
            ApplyFill(fill, cnt, target);   // sayaç da barla birlikte tıkır tıkır artar

            if (spawnedParticles < particleCount && t >= nextParticleAt)
            {
                SpawnParticle();
                spawnedParticles++;
                nextParticleAt += particleEvery;
            }

            yield return null;
        }

        ApplyFill(endFill, goal.CurrentCount, target);

        if (gain.RewardGranted)
        {
            yield return new WaitForSeconds(0.15f);
            yield return StartCoroutine(CoCelebrate());
            RefreshDisplay();
        }
    }

    private void ApplyFill(float normalized, int current, int target)
    {
        // Bar/track RectTransform'ları yok edilmiş olabilir (panel kapandı, goal'ler sıfırlandı,
        // CoApplyFillNextFrame 2 frame beklerken teardown oldu). Yok edilmiş nesneye erişim
        // MissingReferenceException atar — Unity-null guard'larıyla sessizce atla.
        if (progressFill != null && progressFill.rectTransform != null)
        {
            var fillRt   = progressFill.rectTransform;
            var parentRt = fillRt.parent as RectTransform;

            if (parentRt != null && barTrack != null && parentRt != barTrack)
            {
                // progressFill, BarTrack'in dışında — oranı hesapla.
                float parentW = parentRt.rect.width;
                float trackW  = barTrack.rect.width;
                // BarTrack'in parent içindeki sol kenarı
                float trackLeft = barTrack.anchoredPosition.x - barTrack.rect.width * barTrack.pivot.x;

                float anchorStart = parentW > 0f ? trackLeft / parentW              : 0f;
                float anchorEnd   = parentW > 0f ? (trackLeft + trackW * normalized) / parentW : normalized;

                fillRt.anchorMin = new Vector2(anchorStart, fillRt.anchorMin.y);
                fillRt.anchorMax = new Vector2(anchorEnd,   fillRt.anchorMax.y);
                fillRt.offsetMin = new Vector2(0f,          fillRt.offsetMin.y);
                fillRt.offsetMax = new Vector2(0f,          fillRt.offsetMax.y);
            }
            else
            {
                // progressFill zaten BarTrack'in içinde — direkt anchor kullan.
                fillRt.anchorMin = new Vector2(0f,        fillRt.anchorMin.y);
                fillRt.anchorMax = new Vector2(normalized, fillRt.anchorMax.y);
                fillRt.offsetMin = new Vector2(0f,         fillRt.offsetMin.y);
                fillRt.offsetMax = new Vector2(0f,         fillRt.offsetMax.y);
            }
        }
        if (progressText != null) progressText.text = $"{current} / {target}";
    }

    private void SpawnParticle()
    {
        if (particlePrefab == null || particleOrigin == null || progressFill == null) return;
        var go = Instantiate(particlePrefab, transform);
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) { Destroy(go); return; }
        StartCoroutine(CoFly(rt, particleOrigin.anchoredPosition,
                             progressFill.rectTransform.anchoredPosition));
    }

    private IEnumerator CoFly(RectTransform rt, Vector2 from, Vector2 to)
    {
        if (rt == null) yield break;

        rt.anchoredPosition = from;
        float elapsed = 0f;
        while (elapsed < flightDuration)
        {
            if (rt == null) yield break; // uçan partikül (veya view) yol boyunca yok edilebilir
            elapsed += Time.unscaledDeltaTime;
            rt.anchoredPosition = Vector2.Lerp(from, to,
                Mathf.SmoothStep(0f, 1f, elapsed / flightDuration));
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private IEnumerator CoCelebrate()
    {
        if (progressFill == null || progressFill.rectTransform == null) yield break;
        var rt = progressFill.rectTransform;
        var original = rt.localScale;
        float dur = 0.3f, elapsed = 0f;
        while (elapsed < dur)
        {
            if (rt == null) yield break; // celebrate sırasında bar/view yok edilebilir
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = original * (1f + 0.18f * Mathf.Sin((elapsed / dur) * Mathf.PI));
            yield return null;
        }
        if (rt != null) rt.localScale = original;
    }
}
