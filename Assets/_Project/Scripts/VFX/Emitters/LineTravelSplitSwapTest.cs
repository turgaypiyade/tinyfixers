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
    public float afterImageLife = 0.15f;
    public float afterImageAlpha = 0.55f;
    public float afterImageScaleUp = 1.08f;

    // ────────────────────────────────────────────────
    // ✅ Beam Trail
    // ────────────────────────────────────────────────
    [Header("Beam Trail")]
    [Tooltip("RocketTrailBeam prefab. Null bırakırsan trail çıkmaz.")]
    public RocketTrailBeam trailBeamPrefab;

    [Tooltip("Trail beam'lerin spawn edileceği parent. Yoksa afterImageParent kullanılır.")]
    public RectTransform trailParent;

    [Tooltip("Trail spawn etme — false yaparak kapatabilirsin")]
    public bool enableTrailBeam = true;
    // ────────────────────────────────────────────────

    [Header("Timing")]
    public float stepDuration = 0.06f;
    public float postDelay = 0.02f;

    [Header("Cleanup")]
    public bool hideOnComplete = true;

    public enum LineAxis { Horizontal, Vertical }

    [Header("Axis")]
    public LineAxis axis = LineAxis.Horizontal;

    private bool rocketMode = false;
    private Vector2 leftStart;
    private Vector2 rightStart;

    private int _stepCount = 6;
    private float _cellSizePx = 110f;

    public Action<Vector2Int> OnStepCell;
    private Vector2Int _originCell;
    private bool _originCellValid;

    private int _boardWidth = 9;
    private int _boardHeight = 9;
    private bool _completionRaised;

    public Action OnCompleted;

    private RocketTrailBeam _leftTrail;
    private RocketTrailBeam _rightTrail;

    private void Awake()
    {
        if (leftImage) leftStart = leftImage.rectTransform.anchoredPosition;
        if (rightImage) rightStart = rightImage.rectTransform.anchoredPosition;
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
        _cellSizePx = Mathf.Max(1f, cellSizePxOverride);

        KillTrails();

        if (leftImage)
        {
            leftImage.rectTransform.anchoredPosition = leftStart;
            leftImage.rectTransform.localScale = Vector3.one;
            if (splitLeftSprite) leftImage.sprite = splitLeftSprite;
        }

        if (rightImage)
        {
            rightImage.rectTransform.anchoredPosition = rightStart;
            rightImage.rectTransform.localScale = Vector3.one;
            if (splitRightSprite) rightImage.sprite = splitRightSprite;
        }

        ApplyAxisVisualRotation();

        if (leftImage) leftImage.enabled = true;
        if (rightImage) rightImage.enabled = true;

        StopAllCoroutines();
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        float elapsed = 0f;
        while (elapsed < startDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Vector2 posDir = (axis == LineAxis.Horizontal) ? Vector2.right : Vector2.down;
        Vector2 negDir = -posDir;

        if (leftImage && splitLeftSprite) leftImage.sprite = splitLeftSprite;
        if (rightImage && splitRightSprite) rightImage.sprite = splitRightSprite;
        ApplyAxisVisualRotation();

        // ✅ Split ÖNCESI origin world pozisyonunu yakala
        //    İki trail de bu noktadan başlayacak → tüm yolu kapsayacak
        Vector3 originWorldPos = Vector3.zero;
        if (leftImage) originWorldPos = leftImage.rectTransform.position;
        else if (rightImage) originWorldPos = rightImage.rectTransform.position;
        originWorldPos.z = 0f;

        Vector2 leftTarget = leftStart + negDir * splitOffset;
        Vector2 rightTarget = rightStart + posDir * splitOffset;

        float st = 0f;
        while (st < splitTime)
        {
            st += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(st / splitTime);
            u = u * u * (3f - 2f * u);

            if (leftImage)
                leftImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(leftStart, leftTarget, u);
            if (rightImage)
                rightImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(rightStart, rightTarget, u);

            yield return null;
        }

        if (leftImage) leftImage.rectTransform.anchoredPosition = leftTarget;
        if (rightImage) rightImage.rectTransform.anchoredPosition = rightTarget;

        if (leftImage && rocketLeftSprite) leftImage.sprite = rocketLeftSprite;
        if (rightImage && rocketRightSprite) rightImage.sprite = rocketRightSprite;
        ApplyAxisVisualRotation();
        rocketMode = true;

        // ✅ Trail'leri başlat — her iki trail de origin merkez noktasından başlar
        SpawnTrails(originWorldPos);

        if (_originCellValid && OnStepCell != null)
            OnStepCell(_originCell);

        float movePortion = 0.1f;
        float cellSizePx = _cellSizePx;
        int stepCount = _stepCount;

        for (int i = 0; i < stepCount; i++)
        {
            Vector2 rStart = rightImage ? rightImage.rectTransform.anchoredPosition : Vector2.zero;
            Vector2 lStart = leftImage ? leftImage.rectTransform.anchoredPosition : Vector2.zero;

            Vector2 rTarget = rStart + posDir * cellSizePx;
            Vector2 lTarget = lStart + negDir * cellSizePx;

            float moveTime = stepDuration * movePortion;
            float restTime = stepDuration - moveTime;

            float mt = 0f;
            while (mt < moveTime)
            {
                mt += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(mt / moveTime);
                u = u * u * (3f - 2f * u);

                if (rightImage)
                    rightImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(rStart, rTarget, u);
                if (leftImage)
                    leftImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(lStart, lTarget, u);

                UpdateTrailHeads();

                yield return null;
            }

            if (rightImage) rightImage.rectTransform.anchoredPosition = rTarget;
            if (leftImage) leftImage.rectTransform.anchoredPosition = lTarget;

            UpdateTrailHeads();

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

            if (emittersImpactPrefab && impactParent)
            {
                if (rightImage && HasTileAtStep(i, true))
                {
                    var goR = Instantiate(emittersImpactPrefab, impactParent);
                    var rtR = goR.GetComponent<RectTransform>();
                    if (rtR) rtR.anchoredPosition = rTarget;
                    EnsureAutoDestroy(goR, 0.15f);
                }

                if (leftImage && HasTileAtStep(i, false))
                {
                    var goL = Instantiate(emittersImpactPrefab, impactParent);
                    var rtL = goL.GetComponent<RectTransform>();
                    if (rtL) rtL.anchoredPosition = lTarget;
                    EnsureAutoDestroy(goL, 0.15f);
                }
            }

            if (rightImage) SpawnAfterImage(rightImage, rTarget);
            if (leftImage) SpawnAfterImage(leftImage, lTarget);

            if (restTime > 0f)
            {
                float rt2 = 0f;
                while (rt2 < restTime)
                {
                    rt2 += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        rocketMode = false;

        FadeOutTrails();

        if (hideOnComplete)
        {
            if (leftImage) leftImage.enabled = false;
            if (rightImage) rightImage.enabled = false;

            if (leftImage) leftImage.rectTransform.anchoredPosition = leftStart;
            if (rightImage) rightImage.rectTransform.anchoredPosition = rightStart;
        }

        CompleteOnce();
    }

    // ────────────────────────────────────────────────
    // ✅ Trail yönetimi
    // ────────────────────────────────────────────────
    private void SpawnTrails(Vector3 originWorldPos)
    {
        if (!enableTrailBeam || !trailBeamPrefab)
        {
            Debug.LogWarning($"[LineTravelSplit.SpawnTrails] SKIP — enableTrailBeam={enableTrailBeam} prefab={trailBeamPrefab}");
            return;
        }

        RectTransform parent = trailParent ? trailParent : afterImageParent;
        if (!parent)
        {
            Debug.LogWarning("[LineTravelSplit.SpawnTrails] SKIP — no parent");
            return;
        }

        // ✅ Her iki trail de aynı merkez noktasından başlar
        //    Böylece trail, origin cell'den board kenarına kadar tüm yolu kapsar
        Debug.Log($"[LineTravelSplit.SpawnTrails] originWorldPos={originWorldPos}");

        if (leftImage)
        {
            _leftTrail = CreateTrailInstance(parent, originWorldPos);
            // İlk head = roketin şu anki pozisyonu (split sonrası)
            _leftTrail.UpdateHead(leftImage.rectTransform.position);
        }

        if (rightImage)
        {
            _rightTrail = CreateTrailInstance(parent, originWorldPos);
            _rightTrail.UpdateHead(rightImage.rectTransform.position);
        }
    }

    private RocketTrailBeam CreateTrailInstance(RectTransform parent, Vector3 originWorld)
    {
        var beam = Instantiate(trailBeamPrefab, parent);
        beam.transform.position = Vector3.zero;
        beam.transform.localRotation = Quaternion.identity;
        beam.transform.localScale = Vector3.one;

        Debug.Log($"[LineTravelSplit.CreateTrail] parent={parent.name} beamWorldScale={beam.transform.lossyScale} originWorld={originWorld}");

        beam.Init(originWorld);
        return beam;
    }

    private void UpdateTrailHeads()
    {
        if (_rightTrail && rightImage) _rightTrail.UpdateHead(rightImage.rectTransform.position);
        if (_leftTrail && leftImage) _leftTrail.UpdateHead(leftImage.rectTransform.position);
    }

    private void FadeOutTrails()
    {
        if (_leftTrail) _leftTrail.FadeOutAndDestroy();
        if (_rightTrail) _rightTrail.FadeOutAndDestroy();
        _leftTrail = null;
        _rightTrail = null;
    }

    private void KillTrails()
    {
        if (_leftTrail) { _leftTrail.Kill(); _leftTrail = null; }
        if (_rightTrail) { _rightTrail.Kill(); _rightTrail = null; }
    }
    // ────────────────────────────────────────────────

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
        KillTrails();
        CompleteOnce();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        KillTrails();
        CompleteOnce();
    }

    private bool HasTileAtStep(int stepIndex, bool isRight) => true;

    private void SpawnAfterImage(Image sourceImage, Vector2 anchoredPos)
    {
        if (!rocketAfterImagePrefab || !afterImageParent || !sourceImage) return;

        var go = Instantiate(rocketAfterImagePrefab, afterImageParent);
        EnsureAutoDestroy(go, afterImageLife + 0.05f);

        var img = go.GetComponentInChildren<Image>(true);
        var rt = img ? img.rectTransform : go.GetComponent<RectTransform>();
        if (!img || !rt) return;

        img.sprite = sourceImage.sprite;
        img.color = new Color(1f, 1f, 1f, afterImageAlpha);

        rt.anchorMin = sourceImage.rectTransform.anchorMin;
        rt.anchorMax = sourceImage.rectTransform.anchorMax;
        rt.pivot = sourceImage.rectTransform.pivot;
        rt.sizeDelta = sourceImage.rectTransform.sizeDelta;
        rt.localScale = sourceImage.rectTransform.localScale;
        rt.localRotation = sourceImage.rectTransform.localRotation;
        rt.anchoredPosition = anchoredPos;

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

        while (ft < life)
        {
            ft += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(ft / life);

            if (img) img.color = new Color(1f, 1f, 1f, Mathf.Lerp(c0.a, 0f, u));
            if (rt) rt.localScale = Vector3.LerpUnclamped(s0, s1, u);

            yield return null;
        }
    }

    private void ApplyAxisVisualRotation()
    {
        if (!leftImage || !rightImage) return;

        if (axis == LineAxis.Horizontal)
        {
            leftImage.rectTransform.localEulerAngles = Vector3.zero;
            rightImage.rectTransform.localEulerAngles = Vector3.zero;
        }
        else
        {
            rightImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
            leftImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
        }
    }

    private void Update()
    {
        if (!rocketMode) return;

        float scaleOffset = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        if (leftImage) leftImage.rectTransform.localScale = new Vector3(scaleOffset, 1f, 1f);
        if (rightImage) rightImage.rectTransform.localScale = new Vector3(scaleOffset, 1f, 1f);
    }

    public float EstimateDuration(int steps)
    {
        return startDelay + splitTime + (steps * stepDuration) + postDelay;
    }

    [ContextMenu("DEBUG Play Horizontal")]
    private void DebugPlayH() => Play(LineAxis.Horizontal, rightStart, 6, 110f);

    [ContextMenu("DEBUG Play Vertical")]
    private void DebugPlayV() => Play(LineAxis.Vertical, rightStart, 6, 110f);
}