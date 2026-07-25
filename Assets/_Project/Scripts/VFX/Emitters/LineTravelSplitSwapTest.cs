using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System;

public class LineTravelSplitSwapTestUI : MonoBehaviour
{
    [Header("Impact")]
    public GameObject emittersImpactPrefab;
    public RectTransform impactParent;

    [Header("UI Images")]
    public Image leftImage;
    public Image rightImage;

    [Header("Rocket Sprites (flame'li)")]
    public Sprite rocketLeftSprite;
    public Sprite rocketRightSprite;

    [Header("Split Sprites (alevsiz)")]
    public Sprite splitLeftSprite;
    public Sprite splitRightSprite;

    [Header("Trail Sprites (sadece alev — opsiyonel)")]
    [Tooltip("Atanmışsa trail ghost'ları bu sprite ile çizilir (saf alev görünümü). Boş bırakılırsa roket sprite'ı tekrar kullanılır.")]
    public Sprite flameLeftSprite;
    public Sprite flameRightSprite;

    [Header("Tuning")]
    public float startDelay = 0.15f;
    public float splitTime = 0.06f;
    public float splitOffset = 60f;

    [Header("Rocket FX")]
    public float pulseSpeed = 20f;
    public float pulseAmount = 0.05f;

    [Header("AfterImage Trail")]
    public GameObject rocketAfterImagePrefab;
    public RectTransform afterImageParent;
    [Tooltip("Saniye cinsinden bir ghost'un alfası 0'a düşene kadar geçen süre (7f full + 10f fade = 17f @60fps = 0.283s). " +
             "Düşen taşlar board'a girdiğinde beam %20-30 opacity seviyesine düşer, taşlar görünür kalır.")]
    public float afterImageLife = 0.283f;
    [Tooltip("Ghost'un spawn anındaki alfası.")]
    public float afterImageAlpha = 0.85f;
    [Tooltip("Fade eğrisi üssü. 1.8 = ilk 7 frame yüksek opaklık, ardından hızlı fade.")]
    [Range(0.5f, 6f)] public float afterImageFadePower = 1.8f;
    [Tooltip("Tek bir step'in substep ghost'ları arasındaki başlangıç-alfa gradyanı.")]
    [Range(0f, 1f)] public float afterImageSubStepDimBias = 0.55f;
    [Tooltip("Ghost'un spawn anındaki KALINLIK çarpanı. beamWidthCells = 0.40f hedefi için 0.42 * headSizeFactor(0.95) = 0.40 hücre genişliği.")]
    public float afterImageThicknessScale = 0.42f;
    [Tooltip("Ghost'un spawn anındaki UZUNLUK çarpanı.")]
    public float afterImageLengthScale = 1.0f;
    [Tooltip("Ghost yaşam süresi boyunca uygulanacak büyüme çarpanı.")]
    public float afterImageScaleUp = 1.15f;
    [Tooltip("Her step'in başlangıç-bitiş yoluna yerleştirilecek ghost sayısı.")]
    [Range(1, 12)] public int afterImageSubSteps = 6;
    [Tooltip("Step'ler arası beklemede roket başında ek time-based spawn. 0 = devre dışı.")]
    public float afterImageSpawnInterval = 0f;

    [Header("Sizing")]
    [SerializeField, Range(0.5f, 1.2f)] private float headSizeFactor = 0.95f;
    [SerializeField, Range(0.2f, 1f)] private float splitOffsetFactor = 0.55f;
    [Tooltip("Split bittikten sonra roket (LineHLeftRocket/RightRocket, alevli) sprite'ına geçince " +
             "uygulanan ek boyut çarpanı. 1 = split ile aynı boyut; >1 = roket görseli daha büyük.")]
    [SerializeField, Range(0.5f, 2.5f)] private float rocketSizeFactor = 1.35f;

    [Header("Beam Trail (Obsolete)")]
    [Tooltip("Unused — legacy LineRenderer trail fields kept only so existing prefab references do not break. Trail is now a sprite afterimage stream.")]
    public RocketTrailBeam trailBeamPrefab;
    public RocketTrailBeam glowTrailBeamPrefab;
    public RectTransform trailParent;
    public bool enableTrailBeam = false;

    [Header("Timing")]
    [Tooltip("Işının hücre başına seyahat süresi (sn). Düşük = LineV/LineH daha hızlı süpürür. " +
             "0.06 eski, 0.05 ~%17 hızlı. Combo senkronu EstimateDuration üzerinden otomatik uyar.")]
    public float stepDuration = 0.05f;
    public float postDelay = 0.02f;

    [Header("Cleanup")]
    public bool hideOnComplete = true;
    public float headFadeOutTime = 0.12f;

    public enum LineAxis { Horizontal, Vertical }

    [Header("Axis")]
    public LineAxis axis = LineAxis.Horizontal;

    private bool rocketMode = false;
    private Vector2 leftStart;
    private Vector2 rightStart;

    private int _stepCount = 6;
    private int _stepCountPos = 6;
    private int _stepCountNeg = 6;
    private float _cellSizePx = 110f;

    public Action<Vector2Int> OnStepCell;
    private Vector2Int _originCell;
    private bool _originCellValid;

    private int _boardWidth = 9;
    private int _boardHeight = 9;
    private bool _completionRaised;

    public Action OnCompleted;

    private float _leftAfterImageTimer;
    private float _rightAfterImageTimer;

    private RectTransform _trailLayer;

    private RectTransform SafeRect(Image img)
    {
        if (!img) return null;
        try
        {
            return img.rectTransform;
        }
        catch (MissingReferenceException)
        {
            return null;
        }
    }

    private bool IsAlive(Image img) => SafeRect(img) != null;

    private void Awake()
    {
        var leftRt = SafeRect(leftImage);
        var rightRt = SafeRect(rightImage);
        if (leftRt) leftStart = leftRt.anchoredPosition;
        if (rightRt) rightStart = rightRt.anchoredPosition;
    }

    public void Play(
        LineAxis axisMode,
        Vector2 originAnchoredPos,
        Vector2Int originCell,
        int steps,
        float cellSizePxOverride,
        Action<Vector2Int> onStepCell,
        Action onCompleted = null)
    {
        Play(axisMode, originAnchoredPos, originCell, steps, cellSizePxOverride, 9, 9, onStepCell, onCompleted);
    }

    public void Play(
        LineAxis axisMode,
        Vector2 originAnchoredPos,
        Vector2Int originCell,
        int steps,
        float cellSizePxOverride,
        int boardWidth,
        int boardHeight,
        Action<Vector2Int> onStepCell,
        Action onCompleted = null)
    {
        OnStepCell = onStepCell;
        OnCompleted = onCompleted;
        _originCell = originCell;
        _originCellValid = true;
        _boardWidth = Mathf.Max(1, boardWidth);
        _boardHeight = Mathf.Max(1, boardHeight);
        _completionRaised = false;

        Play(axisMode, originAnchoredPos, steps, cellSizePxOverride);
    }

    public void Play(
        LineAxis axisMode,
        Vector2 originAnchoredPos,
        Vector2Int originCell,
        int stepsPos,
        int stepsNeg,
        float cellSizePxOverride,
        int boardWidth,
        int boardHeight,
        Action<Vector2Int> onStepCell,
        Action onCompleted = null)
    {
        OnStepCell = onStepCell;
        OnCompleted = onCompleted;
        _originCell = originCell;
        _originCellValid = true;
        _boardWidth = Mathf.Max(1, boardWidth);
        _boardHeight = Mathf.Max(1, boardHeight);
        _completionRaised = false;

        // Base Play sets _stepCountPos = _stepCountNeg = Max(stepsPos, stepsNeg);
        // then we override with the asymmetric values. Run() starts on next frame so this is safe.
        Play(axisMode, originAnchoredPos, Mathf.Max(stepsPos, stepsNeg), cellSizePxOverride);
        _stepCountPos = Mathf.Max(0, stepsPos);
        _stepCountNeg = Mathf.Max(0, stepsNeg);
    }

    public void Play(LineAxis axisMode, Vector2 originAnchoredPos, int steps, float cellSizePxOverride)
    {
        if (leftImage) leftImage.enabled = false;
        if (rightImage) rightImage.enabled = false;

        rocketMode = false;
        _completionRaised = false;
        axis = axisMode;

        leftStart = originAnchoredPos;
        rightStart = originAnchoredPos;

        _stepCount = Mathf.Max(0, steps);
        _stepCountPos = _stepCount;
        _stepCountNeg = _stepCount;
        _cellSizePx = Mathf.Max(1f, cellSizePxOverride);
        PrepareRootForUiPlayback();
        ApplyCellScaledVisualSize();
        _leftAfterImageTimer = 0f;
        _rightAfterImageTimer = 0f;

        var leftRt = SafeRect(leftImage);
        if (leftRt)
        {
            PrepareImageForPlayback(leftImage);
            leftRt.anchoredPosition = leftStart;
            leftRt.localScale = Vector3.one;
            if (splitLeftSprite) leftImage.sprite = splitLeftSprite;
        }

        var rightRt = SafeRect(rightImage);
        if (rightRt)
        {
            PrepareImageForPlayback(rightImage);
            rightRt.anchoredPosition = rightStart;
            rightRt.localScale = Vector3.one;
            if (splitRightSprite) rightImage.sprite = splitRightSprite;
        }

        ApplyAxisVisualRotation();

        if (leftImage && leftRt) leftImage.enabled = true;
        if (rightImage && rightRt) rightImage.enabled = true;

        StopAllCoroutines();
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        float elapsed = 0f;
        while (elapsed < startDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector2 posDir = (axis == LineAxis.Horizontal) ? Vector2.right : Vector2.down;
        Vector2 negDir = -posDir;

        if (leftImage && splitLeftSprite && IsAlive(leftImage)) leftImage.sprite = splitLeftSprite;
        if (rightImage && splitRightSprite && IsAlive(rightImage)) rightImage.sprite = splitRightSprite;
        ApplyAxisVisualRotation();

        var leftRt0 = SafeRect(leftImage);
        var rightRt0 = SafeRect(rightImage);

        float dynamicSplitOffset = _cellSizePx * splitOffsetFactor;

        Vector2 leftTarget = leftStart + negDir * dynamicSplitOffset;
        Vector2 rightTarget = rightStart + posDir * dynamicSplitOffset;

        float st = 0f;
        while (st < splitTime)
        {
            st += Time.deltaTime;
            float u = Mathf.Clamp01(st / splitTime);
            u = u * u * (3f - 2f * u);

            var leftRt = SafeRect(leftImage);
            if (leftRt)
                leftRt.anchoredPosition = Vector2.LerpUnclamped(leftStart, leftTarget, u);

            var rightRt = SafeRect(rightImage);
            if (rightRt)
                rightRt.anchoredPosition = Vector2.LerpUnclamped(rightStart, rightTarget, u);

            yield return null;
        }

        var leftRt1 = SafeRect(leftImage);
        if (leftRt1) leftRt1.anchoredPosition = leftTarget;
        var rightRt1 = SafeRect(rightImage);
        if (rightRt1) rightRt1.anchoredPosition = rightTarget;

        if (leftImage && rocketLeftSprite && leftRt1) leftImage.sprite = rocketLeftSprite;
        if (rightImage && rocketRightSprite && rightRt1) rightImage.sprite = rocketRightSprite;
        ApplyRocketVisualSize();   // roket sprite'ı split'ten büyük göster
        ApplyAxisVisualRotation();
        rocketMode = true;

        // Seed the trail at the split point so the very first frame already
        // shows ghost sprites trailing from where the rockets emerged.
        SpawnTrailSamples();
        _leftAfterImageTimer = 0f;
        _rightAfterImageTimer = 0f;

        if (_originCellValid && OnStepCell != null)
            OnStepCell(_originCell);

        float movePortion = 0.1f;
        float cellSizePx = _cellSizePx;
        int stepCountPos = _stepCountPos;
        int stepCountNeg = _stepCountNeg;
        int totalSteps = Mathf.Max(stepCountPos, stepCountNeg);

        for (int i = 0; i < totalSteps; i++)
        {
            bool movePos = i < stepCountPos;
            bool moveNeg = i < stepCountNeg;

            var rightStartRt = SafeRect(rightImage);
            var leftStartRt = SafeRect(leftImage);

            Vector2 rStart = rightStartRt ? rightStartRt.anchoredPosition : Vector2.zero;
            Vector2 lStart = leftStartRt ? leftStartRt.anchoredPosition : Vector2.zero;

            // First step compensates for split-phase offset so the callback fires
            // exactly when the rocket center is over the tile center.
            float stepPx = (i == 0) ? cellSizePx - dynamicSplitOffset : cellSizePx;
            Vector2 rTarget = rStart + posDir * stepPx;
            Vector2 lTarget = lStart + negDir * stepPx;

            float moveTime = stepDuration * movePortion;
            float restTime = stepDuration - moveTime;

            float mt = 0f;
            while (mt < moveTime)
            {
                mt += Time.deltaTime;
                float u = Mathf.Clamp01(mt / moveTime);
                u = u * u * (3f - 2f * u);

                if (movePos)
                {
                    var rightRt = SafeRect(rightImage);
                    if (rightRt)
                        rightRt.anchoredPosition = Vector2.LerpUnclamped(rStart, rTarget, u);
                }

                if (moveNeg)
                {
                    var leftRt = SafeRect(leftImage);
                    if (leftRt)
                        leftRt.anchoredPosition = Vector2.LerpUnclamped(lStart, lTarget, u);
                }

                TickTrailSpawn(Time.deltaTime, movePos, moveNeg);
                yield return null;
            }

            var rightRt2 = movePos ? SafeRect(rightImage) : null;
            if (rightRt2) rightRt2.anchoredPosition = rTarget;
            var leftRt2 = moveNeg ? SafeRect(leftImage) : null;
            if (leftRt2) leftRt2.anchoredPosition = lTarget;

            // Distribute ghost sprites along this step's path so the trail
            // shows the rocket between cells instead of only at rest points.
            SpawnSubStepGhosts(rStart, rTarget, lStart, lTarget, movePos, moveNeg);

            // ÖNEMLİ SIRALAMA: Impact VFX ÖNCE spawn olsun ki rocket "çarpma" hissi versin,
            // sonra OnStepCell fire → tile clear. Aksi halde tile, impact daha gelmeden kaybolur
            // ve "senkron değil" hissi verir.
            RectTransform impactSpace = emittersImpactPrefab ? ResolveLocalEffectParent() : null;
            if (emittersImpactPrefab && impactSpace)
            {
                if (rightRt2 && HasTileAtStep(i, true))
                {
                    var goR = Instantiate(emittersImpactPrefab, impactSpace);
                    var rtR = goR.GetComponent<RectTransform>();
                    if (rtR) rtR.anchoredPosition = rTarget;
                    EnsureAutoDestroy(goR, 0.15f);
                }

                if (leftRt2 && HasTileAtStep(i, false))
                {
                    var goL = Instantiate(emittersImpactPrefab, impactSpace);
                    var rtL = goL.GetComponent<RectTransform>();
                    if (rtL) rtL.anchoredPosition = lTarget;
                    EnsureAutoDestroy(goL, 0.15f);
                }
            }

            // OnStepCell şimdi fire et — impact prefab spawn olduktan SONRA.
            // Bu sıralama "rocket çarpar → impact patlar → tile kırılır" hissini verir.
            if (_originCellValid && OnStepCell != null)
            {
                int s = i + 1;

                if (axis == LineAxis.Horizontal)
                {
                    int leftX = _originCell.x - s;
                    int rightX = _originCell.x + s;

                    if (leftX >= 0 && leftX < _boardWidth)
                        OnStepCell(new Vector2Int(leftX, _originCell.y));
                    if (rightX >= 0 && rightX < _boardWidth)
                        OnStepCell(new Vector2Int(rightX, _originCell.y));
                }
                else
                {
                    int downY = _originCell.y - s;
                    int upY = _originCell.y + s;

                    if (downY >= 0 && downY < _boardHeight)
                        OnStepCell(new Vector2Int(_originCell.x, downY));
                    if (upY >= 0 && upY < _boardHeight)
                        OnStepCell(new Vector2Int(_originCell.x, upY));
                }
            }

            if (restTime > 0f)
            {
                float rt2 = 0f;
                while (rt2 < restTime)
                {
                    rt2 += Time.deltaTime;
                    TickTrailSpawn(Time.deltaTime, movePos, moveNeg);
                    yield return null;
                }
            }
        }

        rocketMode = false;

        if (hideOnComplete)
        {
            float ft = 0f;
            float fadeDur = Mathf.Max(0.01f, headFadeOutTime);
            while (ft < fadeDur)
            {
                ft += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, ft / fadeDur);

                if (leftImage && IsAlive(leftImage))
                    leftImage.color = new Color(leftImage.color.r, leftImage.color.g, leftImage.color.b, alpha);
                if (rightImage && IsAlive(rightImage))
                    rightImage.color = new Color(rightImage.color.r, rightImage.color.g, rightImage.color.b, alpha);

                yield return null;
            }

            if (leftImage) { leftImage.enabled = false; leftImage.color = new Color(leftImage.color.r, leftImage.color.g, leftImage.color.b, 1f); }
            if (rightImage) { rightImage.enabled = false; rightImage.color = new Color(rightImage.color.r, rightImage.color.g, rightImage.color.b, 1f); }

            var leftRt = SafeRect(leftImage);
            if (leftRt) leftRt.anchoredPosition = leftStart;
            var rightRt = SafeRect(rightImage);
            if (rightRt) rightRt.anchoredPosition = rightStart;
        }

        CompleteOnce();
    }

    private void ApplyCellScaledVisualSize()
    {
        float visualSize = Mathf.Max(1f, _cellSizePx * headSizeFactor);

        var leftRt = SafeRect(leftImage);
        if (leftRt)
        {
            leftRt.sizeDelta = new Vector2(visualSize, visualSize);
            leftRt.localScale = Vector3.one;
        }

        var rightRt = SafeRect(rightImage);
        if (rightRt)
        {
            rightRt.sizeDelta = new Vector2(visualSize, visualSize);
            rightRt.localScale = Vector3.one;
        }
    }

    // Split fazından sonra roket sprite'ına geçince çağrılır: head görselini rocketSizeFactor
    // kadar büyütür (split/semi boyutu değişmez). Update'teki pulse localScale'i ayrı işler,
    // sizeDelta'ya dokunmaz; bu yüzden büyütme kalıcıdır.
    private void ApplyRocketVisualSize()
    {
        float visualSize = Mathf.Max(1f, _cellSizePx * headSizeFactor * rocketSizeFactor);

        var leftRt = SafeRect(leftImage);
        if (leftRt) leftRt.sizeDelta = new Vector2(visualSize, visualSize);

        var rightRt = SafeRect(rightImage);
        if (rightRt) rightRt.sizeDelta = new Vector2(visualSize, visualSize);
    }

    private void SpawnTrailSamples()
    {
        var leftRt = SafeRect(leftImage);
        if (leftImage && leftRt) SpawnAfterImage(leftImage, leftRt.anchoredPosition);

        var rightRt = SafeRect(rightImage);
        if (rightImage && rightRt) SpawnAfterImage(rightImage, rightRt.anchoredPosition);
    }

    private void SpawnSubStepGhosts(
        Vector2 rStart, Vector2 rTarget,
        Vector2 lStart, Vector2 lTarget,
        bool rightActive, bool leftActive)
    {
        int count = Mathf.Max(1, afterImageSubSteps);
        float bias = Mathf.Clamp01(afterImageSubStepDimBias);
        for (int j = 1; j <= count; j++)
        {
            float frac = (float)j / count;
            // Substep'ler step başından (rocket'tan en uzak) → step sonuna (rocket konumu) doğru
            // alpha çarpanı artar. bias=0 → tüm substep aynı; bias=1 → en uzak substep alfası 0'dan başlar.
            float alphaScale = Mathf.Lerp(1f - bias, 1f, frac);
            if (rightActive && rightImage)
            {
                Vector2 rSub = Vector2.LerpUnclamped(rStart, rTarget, frac);
                SpawnAfterImage(rightImage, rSub, alphaScale);
            }
            if (leftActive && leftImage)
            {
                Vector2 lSub = Vector2.LerpUnclamped(lStart, lTarget, frac);
                SpawnAfterImage(leftImage, lSub, alphaScale);
            }
        }
    }

    private void TickTrailSpawn(float dt, bool leftActive, bool rightActive)
    {
        if (afterImageSpawnInterval <= 0f) return;
        float interval = Mathf.Max(0.005f, afterImageSpawnInterval);

        if (leftActive)
        {
            _leftAfterImageTimer += dt;
            if (_leftAfterImageTimer >= interval)
            {
                _leftAfterImageTimer = 0f;
                var leftRt = SafeRect(leftImage);
                if (leftImage && leftRt) SpawnAfterImage(leftImage, leftRt.anchoredPosition);
            }
        }

        if (rightActive)
        {
            _rightAfterImageTimer += dt;
            if (_rightAfterImageTimer >= interval)
            {
                _rightAfterImageTimer = 0f;
                var rightRt = SafeRect(rightImage);
                if (rightImage && rightRt) SpawnAfterImage(rightImage, rightRt.anchoredPosition);
            }
        }
    }

    private void CompleteOnce()
    {
        if (_completionRaised) return;
        _completionRaised = true;

        var callback = OnCompleted;
        OnCompleted = null;
        callback?.Invoke();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        CompleteOnce();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        CompleteOnce();
    }

    private bool HasTileAtStep(int stepIndex, bool isRight) => true;

    private RectTransform ResolveLocalEffectParent()
    {
        var ownRoot = transform as RectTransform;
        if (ownRoot && ownRoot.gameObject.activeInHierarchy)
            return ownRoot;

        return ResolveExternalEffectParent();
    }

    // Alev iz katmanı: root'un İLK child'ı olarak yaratılır → roket head'leri (Left/Rigth)
    // her zaman bunun ÜSTÜNDE render olur. Tüm ghost'lar buraya doğar; head asla alevle örtülmez.
    private RectTransform EnsureTrailLayer()
    {
        if (_trailLayer) return _trailLayer;

        RectTransform host = ResolveLocalEffectParent();
        if (!host) return null;

        var go = new GameObject("TrailLayer", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(host, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.SetAsFirstSibling();   // head'lerin altında kalsın

        _trailLayer = rt;
        return rt;
    }

    private RectTransform ResolveExternalEffectParent()
    {
        if (afterImageParent && afterImageParent.gameObject.activeInHierarchy)
            return afterImageParent;

        if (trailParent && trailParent.gameObject.activeInHierarchy)
            return trailParent;

        if (impactParent && impactParent.gameObject.activeInHierarchy)
            return impactParent;

        var ownParent = transform.parent as RectTransform;
        if (ownParent && ownParent.gameObject.activeInHierarchy)
            return ownParent;

        return transform as RectTransform;
    }

    private Sprite ResolveTrailSprite(Image sourceImage)
    {
        if (sourceImage == leftImage && flameLeftSprite) return flameLeftSprite;
        if (sourceImage == rightImage && flameRightSprite) return flameRightSprite;
        return sourceImage ? sourceImage.sprite : null;
    }

    private void SpawnAfterImage(Image sourceImage, Vector2 anchoredPos, float alphaScale = 1f)
    {
        if (!sourceImage) return;
        var sourceRt = SafeRect(sourceImage);
        if (!sourceRt) return;

        // Alev hayaletleri roket head'lerinin ALTINDA kalan ayrı bir katmana gider.
        // Aksi halde sonradan Instantiate edilen ghost'lar head'in üstüne render olur
        // (her substep'te rokedin tam konumunda da bir ghost doğuyor) → head alevle örtülür.
        RectTransform parent = EnsureTrailLayer();
        if (!parent) return;

        GameObject go;
        if (rocketAfterImagePrefab)
        {
            go = Instantiate(rocketAfterImagePrefab, parent);
        }
        else
        {
            go = new GameObject("LineTravelAfterImage", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);
        }

        go.SetActive(true);
        EnsureAutoDestroy(go, afterImageLife + 0.05f);

        var img = go.GetComponentInChildren<Image>(true);
        if (!img)
            img = go.AddComponent<Image>();

        var rt = img ? img.rectTransform : go.GetComponent<RectTransform>();
        if (!img || !rt) return;

        img.gameObject.SetActive(true);
        img.enabled = true;
        img.raycastTarget = false;
        img.sprite = ResolveTrailSprite(sourceImage);
        img.color = new Color(1f, 1f, 1f, afterImageAlpha * Mathf.Clamp01(alphaScale));

        rt.anchorMin = sourceRt.anchorMin;
        rt.anchorMax = sourceRt.anchorMax;
        rt.pivot = sourceRt.pivot;
        rt.sizeDelta = sourceRt.sizeDelta;
        float lengthScale = Mathf.Max(0.01f, afterImageLengthScale);
        float thicknessScale = Mathf.Max(0.01f, afterImageThicknessScale);
        rt.localScale = new Vector3(lengthScale, thicknessScale, 1f);
        rt.localRotation = sourceRt.localRotation;
        rt.anchoredPosition = anchoredPos;

        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        StartCoroutine(FadeOnly(img, rt, afterImageLife, afterImageScaleUp));
    }

    private void EnsureAutoDestroy(GameObject go, float lifetime)
    {
        if (!go) return;
        var auto = go.GetComponent<AutoDestroyUnscaled>();
        if (!auto) auto = go.AddComponent<AutoDestroyUnscaled>();
        auto.lifetime = lifetime;
    }

    private IEnumerator FadeOnly(Image img, RectTransform rt, float life, float scaleUp)
    {
        float ft = 0f;
        Color c0 = img ? img.color : Color.white;
        Vector3 s0 = rt ? rt.localScale : Vector3.one;
        Vector3 s1 = s0 * scaleUp;
        float power = Mathf.Max(0.5f, afterImageFadePower);

        while (ft < life)
        {
            ft += Time.deltaTime;
            float u = Mathf.Clamp01(ft / life);
            // Power > 1 → ghost yaşam başında hızla söner, sonunda yavaş → tail koyu, head canlı.
            float k = Mathf.Pow(1f - u, power);

            if (img) img.color = new Color(1f, 1f, 1f, c0.a * k);
            if (rt) rt.localScale = Vector3.LerpUnclamped(s0, s1, u);

            yield return null;
        }
    }

    private void PrepareRootForUiPlayback()
    {
        var rt = transform as RectTransform;
        if (rt == null)
            return;

        Vector3 localPos = rt.localPosition;
        if (Mathf.Abs(localPos.z) > 0.001f)
            rt.localPosition = new Vector3(localPos.x, localPos.y, 0f);

        Vector3 localScale = rt.localScale;
        if (Mathf.Abs(localScale.z - 1f) > 0.001f)
            rt.localScale = new Vector3(localScale.x, localScale.y, 1f);
    }

    private void PrepareImageForPlayback(Image image)
    {
        if (!image)
            return;

        image.gameObject.SetActive(true);
        image.enabled = true;
        image.raycastTarget = false;
        image.color = new Color(image.color.r, image.color.g, image.color.b, 1f);

        var rt = image.rectTransform;
        Vector3 localPos = rt.localPosition;
        if (Mathf.Abs(localPos.z) > 0.001f)
            rt.localPosition = new Vector3(localPos.x, localPos.y, 0f);
    }

    private void ApplyAxisVisualRotation()
    {
        var leftRt = SafeRect(leftImage);
        var rightRt = SafeRect(rightImage);
        if (!leftRt || !rightRt) return;

        if (axis == LineAxis.Horizontal)
        {
            // Yatay: 180° → başlıklar DIŞARI (--> <--  →  <-- -->). "0" başlıkları İÇERİ
            // koyuyordu (standalone LineH'de de doğrulandı). 180° tüm sprite'ı (başlık+alev)
            // çevirir → başlık dışarı VE roketin kendi alevi içeri döner (trail ile uyumlu).
            leftRt.localEulerAngles = new Vector3(0f, 0f, 180f);
            rightRt.localEulerAngles = new Vector3(0f, 0f, 180f);
        }
        else
        {
            // Dikey: +90 → başlıklar DIŞARI (leftImage yukarı çıkar → başlık yukarı,
            // rightImage aşağı iner → başlık aşağı). -90 tersini yapıyordu (başlık içeri).
            rightRt.localEulerAngles = new Vector3(0f, 0f, 90f);
            leftRt.localEulerAngles = new Vector3(0f, 0f, 90f);
        }
    }

    private void Update()
    {
        if (!rocketMode) return;

        float scaleOffset = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        var leftRt = SafeRect(leftImage);
        if (leftRt) leftRt.localScale = new Vector3(scaleOffset, 1f, 1f);

        var rightRt = SafeRect(rightImage);
        if (rightRt) rightRt.localScale = new Vector3(scaleOffset, 1f, 1f);
    }

    public float EstimateDuration(int steps)
    {
        return startDelay + splitTime + (steps * stepDuration) + postDelay + headFadeOutTime;
    }

    public float EstimateDuration(int stepsPos, int stepsNeg)
    {
        return EstimateDuration(Mathf.Max(stepsPos, stepsNeg));
    }

    [ContextMenu("DEBUG Play Horizontal")]
    private void DebugPlayH() => Play(LineAxis.Horizontal, rightStart, 6, 110f);

    [ContextMenu("DEBUG Play Vertical")]
    private void DebugPlayV() => Play(LineAxis.Vertical, rightStart, 6, 110f);
}
