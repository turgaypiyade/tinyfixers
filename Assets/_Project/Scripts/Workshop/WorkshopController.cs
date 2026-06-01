using System;
using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("Mevcut chapter'ı veren library. Workshop içeriği ChapterTheme.workshopStages'ten alınır.")]
    [SerializeField] private ChapterThemeLibrary chapterThemeLibrary;
    [Tooltip("Fallback: chapterThemeLibrary null veya chapter'da workshopStages yoksa kullanılır. " +
             "Production'da chapterThemeLibrary kullan, bu sadece test/legacy için.")]
    [SerializeField] private WorkshopStageData fallbackStageData;

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

    [Header("Layer Fly-In (mode=LayerFlyIn)")]
    [Tooltip("Fly-in parçalarının spawn edileceği parent. currentImage ile aynı boyutta, onun üstünde bir RectTransform.")]
    [SerializeField] private RectTransform layerParent;
    [Tooltip("Parçanın ekrana uçma süresi (saniye).")]
    [SerializeField, Min(0.1f)] private float flyInDuration = 0.55f;
    [Tooltip("Uçuş easing curve'ü. Ease Out Back gibi bir şey iyi durur.")]
    [SerializeField] private AnimationCurve flyInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Animation Tuning")]
    [SerializeField, Min(0.05f)] private float crossfadeDuration = 0.8f;
    [SerializeField, Min(0f)]    private float flashDuration     = 0.18f;
    [SerializeField, Min(0f)]    private float shakeDuration     = 0.25f;
    [SerializeField, Min(0f)]    private float shakeStrength     = 14f;
    [SerializeField, Min(0f)]    private float vfxLifetime       = 2.0f;
    [SerializeField, Min(0f)]    private float starFlyToTargetDelay = 0.45f; // panel'den uçan yıldızların hedeflerine varması için bekleme

    [Header("Sparkle Stars (Transition Sırasında)")]
    [Tooltip("Crossfade sırasında atölye alanına serpilen yıldız sprite'ı (parlama efekti).")]
    [SerializeField] private Sprite sparkleStarSprite;
    [Tooltip("Toplam kaç yıldız serpilecek.")]
    [SerializeField, Range(0, 200)] private int sparkleStarCount = 25;
    [Tooltip("Yıldızların yayılma genişliği (vfxFocus etrafındaki yarıçap, piksel).")]
    [SerializeField, Min(50f)] private float sparkleSpreadRadius = 350f;
    [Tooltip("Tek bir yıldız animasyonu süresi.")]
    [SerializeField, Min(0.1f)] private float sparkleDuration = 0.9f;
    [Tooltip("Yıldızların arasındaki spawn gecikmesi (stagger).")]
    [SerializeField, Min(0f)] private float sparkleSpawnStagger = 0.04f;
    [Tooltip("Yıldız boyutu (pixel).")]
    [SerializeField, Min(10f)] private float sparkleSize = 60f;
    [Tooltip("Yıldız renkleri — her yıldız bu listeden rastgele bir renk seçer. Boş ise beyaz kullanılır.")]
    [SerializeField] private Color[] sparkleColors = new Color[]
    {
        new Color(1.00f, 0.92f, 0.30f, 1f), // sarı
        new Color(0.30f, 0.65f, 1.00f, 1f), // mavi
        new Color(0.40f, 0.85f, 0.40f, 1f), // yeşil
        new Color(1.00f, 0.40f, 0.40f, 1f), // kırmızı/pembe
    };
    [Tooltip("Tüm yıldızlar workshop alanı boyunca rastgele dağılsın mı (true), yoksa vfxFocus etrafında mı toplansın (false)?")]
    [SerializeField] private bool sparkleScatterFullArea = true;

    // ChapterTheme.workshopStages → mevcut chapter'ın stage data'sı. Yoksa fallback'e döner.
    public WorkshopStageData StageData
    {
        get
        {
            if (chapterThemeLibrary != null)
            {
                var theme = chapterThemeLibrary.GetCurrentTheme();
                if (theme != null && theme.workshopStages != null)
                    return theme.workshopStages;
            }
            return fallbackStageData;
        }
    }

    public int CurrentChapterIndex => chapterThemeLibrary != null ? chapterThemeLibrary.GetCurrentChapter() : 1;

    // Per-chapter PlayerPrefs key'leri — her chapter'ın progress'i ayrı.
    private string KeyCurrentStage => $"workshop_chapter_{CurrentChapterIndex}_stage";
    public bool IsTransitioning { get; private set; }
    public event Action<int> OnStageCompleted; // tamamlanan stage index
    public event Action<WorkshopRewardBundle> OnFinalRewardGranted; // tüm aşamalar bitti, ödül paketi verildi

    private string KeyFinalRewardClaimed => $"workshop_chapter_{CurrentChapterIndex}_reward_claimed";

    public bool FinalRewardClaimed
    {
        get => PlayerPrefs.GetInt(KeyFinalRewardClaimed, 0) == 1;
        private set { PlayerPrefs.SetInt(KeyFinalRewardClaimed, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // stages[0] = initial broken görsel (task değil), stages[1..N-1] = ödenen tasklar.
    // Yani toplam task = stages.Count - 1.
    public int TotalTasks => StageData != null ? Mathf.Max(0, StageData.stages.Count - 1) : 0;
    public int TasksCompleted => CurrentStage;
    public float ProgressNormalized => TotalTasks > 0 ? Mathf.Clamp01((float)TasksCompleted / TotalTasks) : 0f;

    public int CurrentStage
    {
        get => PlayerPrefs.GetInt(KeyCurrentStage, 0);
        private set { PlayerPrefs.SetInt(KeyCurrentStage, value); PlayerPrefs.Save(); }
    }

    public bool IsAllCompleted => StageData != null && CurrentStage >= TotalTasks;

    /// Şu an yapılacak sıradaki task (stages[CurrentStage + 1]). Hepsi bittiyse null.
    public WorkshopStage GetActiveStage()
    {
        var data = StageData;
        if (data == null) return null;
        int idx = CurrentStage + 1;
        if (idx >= data.stages.Count) return null;
        return data.stages[idx];
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private int lastAppliedChapter = -1;
    private readonly List<GameObject> spawnedLayers = new();

    private void Start()
    {
        ApplyAndCacheChapter();
    }

    private void OnEnable()
    {
        // Sahne yeniden aktif olduysa (örn levelden geri dönüş) chapter değişmiş olabilir.
        ApplyAndCacheChapter();
    }

    private void ApplyAndCacheChapter()
    {
        int cur = CurrentChapterIndex;
        if (cur != lastAppliedChapter)
        {
            Debug.Log($"[Workshop] Chapter değişti: {lastAppliedChapter} → {cur}. Yeniden uygulanıyor.");
            lastAppliedChapter = cur;
        }
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
        var data = StageData;
        if (data == null || data.stages.Count == 0)
        {
            Debug.LogWarning($"[Workshop] StageData boş — Chapter {CurrentChapterIndex} için gösterilecek aşama yok.");
            return;
        }

        if (flashOverlay != null) flashOverlay.alpha = 0f;

        var s0img = data.stages.Count > 0 ? data.stages[0].stageImage : null;
        Debug.Log($"[Workshop] ApplyInstant — mode={data.mode} chapter={CurrentChapterIndex} stage={CurrentStage} " +
                  $"stages[0].stageImage={(s0img != null ? s0img.name : "NULL")} " +
                  $"currentImage={(currentImage != null ? currentImage.name : "NULL")}");

        if (data.mode == WorkshopMode.LayerFlyIn)
        {
            ApplyCurrentStageInstant_LayerFlyIn(data);
            return;
        }

        // ── SpriteSwap (mevcut davranış) ──────────────────────────────────────
        int visibleIndex = Mathf.Clamp(CurrentStage, 0, data.stages.Count - 1);
        var stage = data.stages[visibleIndex];

        if (currentImage != null)
        {
            if (!currentImage.gameObject.activeSelf) currentImage.gameObject.SetActive(true);
            currentImage.sprite = stage.stageImage;
            SetImageAlpha(currentImage, 1f);
        }
        if (nextImage != null)
        {
            if (!nextImage.gameObject.activeSelf) nextImage.gameObject.SetActive(true);
            SetImageAlpha(nextImage, 0f);
        }
    }

    private void ApplyCurrentStageInstant_LayerFlyIn(WorkshopStageData data)
    {
        // CurrentStage'in stageImage'ı varsa onu göster — en temiz birleşik resim.
        // Yoksa bir önceki stage'in stageImage'ından itibaren kalan layerları spawn et.
        ClearSpawnedLayers();

        int compositeStage = -1;
        for (int i = CurrentStage; i >= 0; i--)
        {
            if (data.stages[i].stageImage != null) { compositeStage = i; break; }
        }

        Sprite bg = compositeStage >= 0 ? data.stages[compositeStage].stageImage : null;
        Debug.Log($"[Workshop] LayerFlyIn apply — compositeStage={compositeStage} bg={(bg != null ? bg.name : "NULL")}");
        if (currentImage != null)
        {
            if (!currentImage.gameObject.activeSelf) currentImage.gameObject.SetActive(true);
            currentImage.sprite = bg;
            SetImageAlpha(currentImage, 1f);
            Debug.Log($"[Workshop] currentImage.sprite SET → {(currentImage.sprite != null ? currentImage.sprite.name : "NULL")} alpha={currentImage.color.a}");
        }
        StartCoroutine(DebugCheckSpriteNextFrame());
        if (nextImage != null) SetImageAlpha(nextImage, 0f);

        // compositeStage'den sonra stageImage'sız kalmış parçaları layerSprite olarak ekle.
        for (int i = compositeStage + 1; i <= CurrentStage && i < data.stages.Count; i++)
        {
            var s = data.stages[i];
            if (s.layerSprite == null) continue;
            var go = SpawnLayerImage(s.layerSprite);
            go.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            spawnedLayers.Add(go);
        }
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
        if (IsTransitioning) { Debug.Log("[Workshop] TryRepair FAIL: zaten transition var"); return false; }
        if (IsAllCompleted)  { Debug.Log("[Workshop] TryRepair FAIL: tüm tasklar bitti"); return false; }
        var data = StageData;
        if (data == null) { Debug.LogWarning("[Workshop] TryRepair FAIL: StageData null (chapter library veya theme'da workshop yok?)"); return false; }

        int nextIndex = CurrentStage + 1;
        if (nextIndex >= data.stages.Count)
        {
            Debug.Log($"[Workshop] TryRepair FAIL: nextIndex {nextIndex} >= stagesCount {data.stages.Count}");
            return false;
        }

        var nextStage = data.stages[nextIndex];
        int cost = nextStage.starCost;
        Debug.Log($"[Workshop] TryRepair: cost={cost}, stars={PlayerWallet.TotalStars}, nextIndex={nextIndex}");

        if (!PlayerWallet.HasEnoughStars(cost))
        {
            Debug.LogWarning($"[Workshop] TryRepair FAIL: yetersiz yıldız ({PlayerWallet.TotalStars} < {cost})");
            return false;
        }

        if (!PlayerWallet.SpendStars(cost))
        {
            Debug.LogError("[Workshop] TryRepair FAIL: SpendStars başarısız");
            return false;
        }

        Debug.Log($"[Workshop] TryRepair OK: stages[{nextIndex}] yükleniyor, sprite={(nextStage.stageImage != null ? nextStage.stageImage.name : "NULL")}");
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
        var data = StageData;
        if (data == null) return false;

        int nextIndex = CurrentStage + 1;
        if (nextIndex >= data.stages.Count) return false;

        StartCoroutine(PlayStageTransition(nextIndex));
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Transition
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator PlayStageTransition(int nextIndex)
    {
        if (StageData?.mode == WorkshopMode.LayerFlyIn)
        {
            yield return StartCoroutine(PlayLayerFlyInTransition(nextIndex));
            yield break;
        }
        yield return StartCoroutine(PlaySpriteSwapTransition(nextIndex));
    }

    private IEnumerator PlayLayerFlyInTransition(int nextIndex)
    {
        IsTransitioning = true;
        var nextStage = StageData.stages[nextIndex];

        if (starFlyToTargetDelay > 0f)
            yield return new WaitForSeconds(starFlyToTargetDelay);

        // Parçayı ekran dışından başlat
        var go = SpawnLayerImage(nextStage.layerSprite);
        var rt = go.GetComponent<RectTransform>();

        var parent = layerParent != null ? layerParent : vfxParent;
        float canvasW = parent.rect.width;
        float canvasH = parent.rect.height;

        Vector2 startPos = nextStage.flyInFrom switch
        {
            FlyInFrom.Right  => new Vector2( canvasW * 1.2f, 0f),
            FlyInFrom.Bottom => new Vector2(0f, -canvasH * 1.2f),
            FlyInFrom.Top    => new Vector2(0f,  canvasH * 1.2f),
            _                => new Vector2(-canvasW * 1.2f, 0f), // Left
        };
        rt.anchoredPosition = startPos;

        // Uçuş animasyonu
        float t = 0f;
        while (t < flyInDuration)
        {
            t += Time.deltaTime;
            float k = flyInCurve.Evaluate(Mathf.Clamp01(t / flyInDuration));
            rt.anchoredPosition = Vector2.Lerp(startPos, Vector2.zero, k);
            yield return null;
        }
        rt.anchoredPosition = Vector2.zero;

        // Landing: stageImage varsa currentImage'a geçir ve layer'ları temizle.
        // Yoksa layer birikmeye devam eder (stageImage opsiyonel).
        if (nextStage.stageImage != null)
        {
            if (currentImage != null) currentImage.sprite = nextStage.stageImage;
            ClearSpawnedLayers();
            Destroy(go);
        }
        else
        {
            spawnedLayers.Add(go);
        }

        // VFX & SFX
        SpawnVfx(sparkleBurstPrefab, nextStage.vfxFocusNormalized);
        StartCoroutine(SpawnSparkleStars(nextStage.vfxFocusNormalized));
        if (flashOverlay != null) StartCoroutine(PlayFlash());
        if (shakeRoot != null)    StartCoroutine(PlayShake());
        SpawnVfx(confettiPrefab, nextStage.vfxFocusNormalized);

        if (GameSettings.SoundEnabled && nextStage.sfxOnComplete != null && sfxSource != null)
            sfxSource.PlayOneShot(nextStage.sfxOnComplete);

        CurrentStage = nextIndex;
        IsTransitioning = false;
        OnStageCompleted?.Invoke(nextIndex);
        TryGrantFinalReward();
    }

    private IEnumerator PlaySpriteSwapTransition(int nextIndex)
    {
        IsTransitioning = true;
        Debug.Log($"[Workshop] PlayStageTransition START → stages[{nextIndex}]. currentImage={(currentImage != null ? "OK" : "NULL")}, nextImage={(nextImage != null ? "OK" : "NULL")}");

        var nextStage = StageData.stages[nextIndex];
        Debug.Log($"[Workshop] nextStage.stageImage = {(nextStage.stageImage != null ? nextStage.stageImage.name : "NULL")}");

        // 0. Yıldız uçma animasyonu için kısa gecikme (gelecek entegrasyon)
        if (starFlyToTargetDelay > 0f)
            yield return new WaitForSeconds(starFlyToTargetDelay);

        // 1. nextImage'i hazırla (henüz görünmez)
        if (nextImage != null)
        {
            if (!nextImage.gameObject.activeSelf) nextImage.gameObject.SetActive(true);
            nextImage.sprite = nextStage.stageImage;
            SetImageAlpha(nextImage, 0f);
            Debug.Log($"[Workshop] NextImage sprite set, alpha=0, ready to crossfade");
        }
        else
        {
            Debug.LogWarning("[Workshop] NextImage NULL! Crossfade çalışmayacak.");
        }

        // 2. Sparkle VFX patlat (focus point'te) — varsa prefab
        SpawnVfx(sparkleBurstPrefab, nextStage.vfxFocusNormalized);

        // 2b. Procedural sparkle stars — atölye boyunca yıldızlar saçılır (parlama efekti)
        StartCoroutine(SpawnSparkleStars(nextStage.vfxFocusNormalized));

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
        if (GameSettings.SoundEnabled && nextStage.sfxOnComplete != null && sfxSource != null)
            sfxSource.PlayOneShot(nextStage.sfxOnComplete);

        // 8. Görseli currentImage'a sabitle (sonraki transition için)
        if (currentImage != null && nextImage != null)
        {
            currentImage.sprite = nextStage.stageImage;
            SetImageAlpha(currentImage, 1f);
            SetImageAlpha(nextImage, 0f);
            Debug.Log($"[Workshop] Crossfade DONE. currentImage.sprite = {(nextStage.stageImage != null ? nextStage.stageImage.name : "NULL")}");
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
        var data = StageData;
        if (data == null || data.finalReward == null) return;
        if (!IsAllCompleted) return;
        if (FinalRewardClaimed) return;

        WorkshopRewardService.Grant(data.finalReward);
        FinalRewardClaimed = true;
        OnFinalRewardGranted?.Invoke(data.finalReward);

        int itemCount = data.finalReward.items != null ? data.finalReward.items.Count : 0;
        Debug.Log($"[Workshop] Final reward bundle verildi: {itemCount} item.");
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

    // ─────────────────────────────────────────────────────────────────────────
    //  Sparkle Stars (procedural — atölye temizlenirken parlama)
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator SpawnSparkleStars(Vector2 focusNormalized)
    {
        if (sparkleStarSprite == null || vfxParent == null) yield break;
        if (sparkleStarCount <= 0) yield break;

        var parentRect = vfxParent.rect;
        Vector2 focusPos = new Vector2(parentRect.width * focusNormalized.x,
                                       parentRect.height * focusNormalized.y);

        for (int i = 0; i < sparkleStarCount; i++)
        {
            Vector2 pos;
            if (sparkleScatterFullArea)
            {
                // Atölye alanı boyunca rastgele dağıt
                pos = new Vector2(
                    UnityEngine.Random.Range(parentRect.width * 0.1f, parentRect.width * 0.9f),
                    UnityEngine.Random.Range(parentRect.height * 0.1f, parentRect.height * 0.9f));
            }
            else
            {
                // Focus point etrafında topla
                Vector2 offset = UnityEngine.Random.insideUnitCircle * sparkleSpreadRadius;
                pos = focusPos + offset;
            }

            StartCoroutine(AnimateOneSparkleStar(pos));

            if (sparkleSpawnStagger > 0f)
                yield return new WaitForSeconds(sparkleSpawnStagger);
        }
    }

    private IEnumerator AnimateOneSparkleStar(Vector2 anchoredPos)
    {
        var go = new GameObject("Sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(vfxParent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        // Hafif boyut varyasyonu
        float sizeMul = UnityEngine.Random.Range(0.7f, 1.3f);
        rt.sizeDelta = Vector2.one * sparkleSize * sizeMul;

        // Rastgele başlangıç rotasyonu
        rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

        var img = go.GetComponent<Image>();
        img.sprite = sparkleStarSprite;
        img.raycastTarget = false;

        // Renk paletinden rastgele bir renk seç (boş ise beyaz)
        Color pickedColor = (sparkleColors != null && sparkleColors.Length > 0)
            ? sparkleColors[UnityEngine.Random.Range(0, sparkleColors.Length)]
            : Color.white;

        Color start = pickedColor; start.a = 0f;
        img.color = start;

        float halfDur = sparkleDuration * 0.5f;
        float t = 0f;

        // Faz 1: fade in + scale up + rotate
        while (t < halfDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / halfDur);
            float ek = Mathf.SmoothStep(0f, 1f, k);
            var c = pickedColor; c.a = ek; img.color = c;
            rt.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.3f, ek);
            rt.Rotate(0f, 0f, 90f * Time.deltaTime);
            yield return null;
        }

        // Faz 2: fade out + scale down + rotate
        t = 0f;
        while (t < halfDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / halfDur);
            float ek = Mathf.SmoothStep(0f, 1f, k);
            var c = pickedColor; c.a = 1f - ek; img.color = c;
            rt.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.4f, ek);
            rt.Rotate(0f, 0f, 90f * Time.deltaTime);
            yield return null;
        }

        Destroy(go);
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

    // ─────────────────────────────────────────────────────────────────────────
    //  Layer Fly-In Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private GameObject SpawnLayerImage(Sprite sprite)
    {
        var parent = layerParent != null ? layerParent : (vfxParent != null ? vfxParent : currentImage?.rectTransform?.parent as RectTransform);
        if (parent == null)
        {
            Debug.LogWarning("[Workshop] LayerParent bulunamadı — layerParent alanını Inspector'da ata.");
            return new GameObject("Layer_Fallback");
        }

        var go = new GameObject("WorkshopLayer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = parent.rect.size; // background ile aynı canvas boyutu

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;

        return go;
    }

    private void ClearSpawnedLayers()
    {
        foreach (var go in spawnedLayers)
            if (go != null) Destroy(go);
        spawnedLayers.Clear();
    }

    private void OnDestroy() => ClearSpawnedLayers();

    private System.Collections.IEnumerator DebugCheckSpriteNextFrame()
    {
        yield return null;
        if (currentImage != null)
            Debug.Log($"[Workshop] 1 frame sonra currentImage.sprite={(currentImage.sprite != null ? currentImage.sprite.name : "NULL")} alpha={currentImage.color.a}");
    }
}
