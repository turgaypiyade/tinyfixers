using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Atölye sahnesini yöneten ana kontrolcü.
/// 10 (veya istenen sayıda) aşamalı, sprite-swap tabanlı bir tamir sahnesi.
///
/// Sahne yerleşimi: MainMenu Canvas'ında WorkshopBackground GameObject'i altında
/// üst üste iki Image vardır — currentImage (görünür) ve nextImage (görünmez, transition için).
/// </summary>
public class WorkshopController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WorkshopStageData stageData;

    [Header("Background Images (üst üste iki katman)")]
    [SerializeField] private Image currentImage;
    [SerializeField] private Image nextImage;

    [Header("VFX")]
    [Tooltip("Sparkle/burst patlayacağı transform — currentImage'in RectTransform'u.")]
    [SerializeField] private RectTransform vfxParent;
    [SerializeField] private GameObject sparkleBurstPrefab;
    [SerializeField] private GameObject confettiPrefab;
    [SerializeField] private CanvasGroup flashOverlay;
    [SerializeField] private RectTransform shakeRoot;
    [SerializeField] private AudioSource sfxSource;

    [Header("Animation Tuning")]
    [SerializeField, Min(0.05f)] private float crossfadeDuration = 0.8f;
    [SerializeField, Min(0f)]    private float flashDuration     = 0.18f;
    [SerializeField, Min(0f)]    private float shakeDuration     = 0.25f;
    [SerializeField, Min(0f)]    private float shakeStrength     = 14f;
    [SerializeField, Min(0f)]    private float vfxLifetime       = 2.0f;
    [SerializeField, Min(0f)]    private float starFlyToTargetDelay = 0.0f; // future: yıldız uçma anim entegrasyonu

    private const string KeyCurrentStage = "workshop_current_stage";

    public WorkshopStageData StageData => stageData;
    public bool IsTransitioning { get; private set; }
    public event Action<int> OnStageCompleted; // tamamlanan stage index
    public event Action<WorkshopReward> OnFinalRewardGranted; // tüm aşamalar bitti, ödül verildi

    private const string KeyFinalRewardClaimed = "workshop_final_reward_claimed";

    public bool FinalRewardClaimed
    {
        get => PlayerPrefs.GetInt(KeyFinalRewardClaimed, 0) == 1;
        private set { PlayerPrefs.SetInt(KeyFinalRewardClaimed, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public int TotalStages => stageData != null ? stageData.stages.Count : 0;
    public float ProgressNormalized => TotalStages > 0 ? Mathf.Clamp01((float)CurrentStage / TotalStages) : 0f;

    public int CurrentStage
    {
        get => PlayerPrefs.GetInt(KeyCurrentStage, 0);
        private set { PlayerPrefs.SetInt(KeyCurrentStage, value); PlayerPrefs.Save(); }
    }

    public bool IsAllCompleted => stageData != null && CurrentStage >= stageData.stages.Count;

    public WorkshopStage GetActiveStage()
    {
        if (stageData == null || CurrentStage >= stageData.stages.Count) return null;
        return stageData.stages[CurrentStage];
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        ApplyCurrentStageInstant();
    }

#if UNITY_EDITOR
    [Header("DEBUG (sadece editor'da)")]
    [SerializeField] private bool enableDebugKeys = true;

    private void Update()
    {
        if (!enableDebugKeys) return;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame)
        {
            Debug.Log($"[Workshop] Space → FORCE repair. CurrentStage={CurrentStage}");
            if (!DebugForceRepairCurrent())
                Debug.LogWarning("[Workshop] FORCE repair FAIL — son aşamadasın veya transition devam ediyor.");
        }
        if (kb.rKey.wasPressedThisFrame)
        {
            DebugReset();
            Debug.Log("[Workshop] R → reset to stage 0.");
        }
        if (kb.sKey.wasPressedThisFrame)
        {
            PlayerPrefs.SetInt("player_total_stars", 99);
            PlayerPrefs.Save();
            Debug.Log("[Workshop] S → 99 yıldız cheat.");
        }
#else
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"[Workshop] Space → FORCE repair. CurrentStage={CurrentStage}");
            if (!DebugForceRepairCurrent())
                Debug.LogWarning("[Workshop] FORCE repair FAIL — son aşamadasın veya transition devam ediyor.");
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            DebugReset();
            Debug.Log("[Workshop] R → reset to stage 0.");
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            PlayerPrefs.SetInt("player_total_stars", 99);
            PlayerPrefs.Save();
            Debug.Log("[Workshop] S → 99 yıldız cheat.");
        }
#endif
    }
#endif

    /// <summary>
    /// Sahne açılışında animasyonsuz olarak mevcut aşamayı uygula.
    /// CurrentStage > 0 ise atölye o ana kadar tamir edilmiş halde görünür.
    /// CurrentStage = 0 ise stage[0]'ın görseli (başlangıç/kirli) görünür.
    /// </summary>
    private void ApplyCurrentStageInstant()
    {
        if (stageData == null || stageData.stages.Count == 0)
        {
            Debug.LogWarning("[Workshop] StageData boş — gösterilecek aşama yok.");
            return;
        }

        // Tamir edildikçe yeni görsele geçeriz. Yani:
        //   CurrentStage = 0 → henüz hiç tamir yok → stages[0] göster
        //   CurrentStage = 5 → 5 tamir yapılmış → stages[5] göster (varsa)
        int visibleIndex = Mathf.Clamp(CurrentStage, 0, stageData.stages.Count - 1);
        var stage = stageData.stages[visibleIndex];

        if (currentImage != null)
        {
            currentImage.sprite = stage.stageImage;
            SetImageAlpha(currentImage, 1f);
        }
        if (nextImage != null) SetImageAlpha(nextImage, 0f);
        if (flashOverlay != null) flashOverlay.alpha = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mevcut aşamayı tamir etmeye çalış. Yeterli yıldız varsa harcar, transition başlatır.
    /// Caller (örn. RepairButton veya TaskList item) bunu çağırır.
    /// </summary>
    public bool TryRepairCurrent()
    {
        if (IsTransitioning) return false;
        if (IsAllCompleted) return false;
        if (stageData == null) return false;

        // Bir sonraki aşamaya geçeceğiz → ondan stageImage'i alacağız
        int nextIndex = CurrentStage + 1;
        if (nextIndex >= stageData.stages.Count)
        {
            // Son aşama da tamamlandı, daha yeni görsel yok
            // İsterse "son" diye marker bir state
            return false;
        }

        var nextStage = stageData.stages[nextIndex];
        int cost = nextStage.starCost;

        if (!PlayerWallet.HasEnoughStars(cost))
            return false;

        if (!PlayerWallet.SpendStars(cost))
            return false;

        StartCoroutine(PlayStageTransition(nextIndex));
        return true;
    }

    /// <summary>
    /// Test için: tüm ilerlemeyi sıfırla.
    /// </summary>
    public void DebugReset()
    {
        PlayerPrefs.DeleteKey(KeyCurrentStage);
        PlayerPrefs.DeleteKey(KeyFinalRewardClaimed);
        PlayerPrefs.Save();
        ApplyCurrentStageInstant();
    }

    /// <summary>
    /// Test için: yıldız kontrolünü bypass eden tamir. Production'da kullanma.
    /// </summary>
    public bool DebugForceRepairCurrent()
    {
        if (IsTransitioning) return false;
        if (stageData == null) return false;

        int nextIndex = CurrentStage + 1;
        if (nextIndex >= stageData.stages.Count) return false;

        StartCoroutine(PlayStageTransition(nextIndex));
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Transition
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator PlayStageTransition(int nextIndex)
    {
        IsTransitioning = true;

        var nextStage = stageData.stages[nextIndex];

        // 0. Yıldız uçma animasyonu için kısa gecikme (gelecek entegrasyon)
        if (starFlyToTargetDelay > 0f)
            yield return new WaitForSeconds(starFlyToTargetDelay);

        // 1. nextImage'i hazırla (henüz görünmez)
        if (nextImage != null)
        {
            nextImage.sprite = nextStage.stageImage;
            SetImageAlpha(nextImage, 0f);
        }

        // 2. Sparkle VFX patlat (focus point'te)
        SpawnVfx(sparkleBurstPrefab, nextStage.vfxFocusNormalized);

        // 3. Flash overlay (kısa beyaz parlama)
        if (flashOverlay != null)
            StartCoroutine(PlayFlash());

        // 4. Shake (kısa)
        if (shakeRoot != null)
            StartCoroutine(PlayShake());

        // 5. Crossfade — current fade out, next fade in
        float t = 0f;
        while (t < crossfadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / crossfadeDuration));
            if (currentImage != null) SetImageAlpha(currentImage, 1f - k);
            if (nextImage    != null) SetImageAlpha(nextImage, k);
            yield return null;
        }

        // 6. Confetti / kapanış patlaması
        SpawnVfx(confettiPrefab, nextStage.vfxFocusNormalized);

        // 7. SFX
        if (nextStage.sfxOnComplete != null && sfxSource != null)
            sfxSource.PlayOneShot(nextStage.sfxOnComplete);

        // 8. Görseli currentImage'a sabitle (sonraki transition için)
        if (currentImage != null && nextImage != null)
        {
            currentImage.sprite = nextStage.stageImage;
            SetImageAlpha(currentImage, 1f);
            SetImageAlpha(nextImage, 0f);
        }

        // 9. Save progress
        CurrentStage = nextIndex;

        IsTransitioning = false;
        OnStageCompleted?.Invoke(nextIndex);

        // 10. Tüm aşamalar bittiyse final reward'ı ver (bir kez).
        TryGrantFinalReward();
    }

    private void TryGrantFinalReward()
    {
        if (stageData == null || stageData.finalReward == null) return;
        if (!IsAllCompleted) return;
        if (FinalRewardClaimed) return;

        WorkshopRewardService.Grant(stageData.finalReward);
        FinalRewardClaimed = true;
        OnFinalRewardGranted?.Invoke(stageData.finalReward);
        Debug.Log($"[Workshop] Final reward verildi: {stageData.finalReward.type} x{stageData.finalReward.amount}");
    }

    private IEnumerator PlayFlash()
    {
        float t = 0f;
        float half = flashDuration * 0.5f;

        while (t < half)
        {
            t += Time.deltaTime;
            flashOverlay.alpha = Mathf.Lerp(0f, 1f, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            flashOverlay.alpha = Mathf.Lerp(1f, 0f, t / half);
            yield return null;
        }
        flashOverlay.alpha = 0f;
    }

    private IEnumerator PlayShake()
    {
        Vector3 origin = shakeRoot.anchoredPosition3D;
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(t / shakeDuration);
            float x = (UnityEngine.Random.value * 2f - 1f) * shakeStrength * damper;
            float y = (UnityEngine.Random.value * 2f - 1f) * shakeStrength * damper;
            shakeRoot.anchoredPosition3D = origin + new Vector3(x, y, 0f);
            yield return null;
        }
        shakeRoot.anchoredPosition3D = origin;
    }

    private void SpawnVfx(GameObject prefab, Vector2 normalized)
    {
        if (prefab == null || vfxParent == null) return;

        var go = Instantiate(prefab, vfxParent);
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);

            var parentRect = vfxParent.rect;
            rt.anchoredPosition = new Vector2(
                parentRect.width  * normalized.x,
                parentRect.height * normalized.y);
        }

        if (vfxLifetime > 0f) Destroy(go, vfxLifetime);
    }

    private static void SetImageAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }
}
