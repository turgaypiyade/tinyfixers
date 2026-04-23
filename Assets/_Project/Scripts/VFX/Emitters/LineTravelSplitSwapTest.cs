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

    [Header("Sizing")]
    [SerializeField, Range(0.5f, 1.2f)] private float headSizeFactor = 0.95f;
    [SerializeField, Range(0.2f, 1f)] private float splitOffsetFactor = 0.55f;

    [Header("Beam Trail")]
    public RocketTrailBeam trailBeamPrefab;       // Core
    public RocketTrailBeam glowTrailBeamPrefab;   // Glow
    public RectTransform trailParent;
    public bool enableTrailBeam = true;

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

    private RocketTrailBeam _leftGlowTrail;
    private RocketTrailBeam _rightGlowTrail;

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
        ApplyCellScaledVisualSize();
        KillTrails();

        var leftRt = SafeRect(leftImage);
        if (leftRt)
        {
            leftRt.anchoredPosition = leftStart;
            leftRt.localScale = Vector3.one;
            if (splitLeftSprite) leftImage.sprite = splitLeftSprite;
        }

        var rightRt = SafeRect(rightImage);
        if (rightRt)
        {
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
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Vector2 posDir = (axis == LineAxis.Horizontal) ? Vector2.right : Vector2.down;
        Vector2 negDir = -posDir;

        if (leftImage && splitLeftSprite && IsAlive(leftImage)) leftImage.sprite = splitLeftSprite;
        if (rightImage && splitRightSprite && IsAlive(rightImage)) rightImage.sprite = splitRightSprite;
        ApplyAxisVisualRotation();

        Vector3 originWorldPos = Vector3.zero;
        var leftRt0 = SafeRect(leftImage);
        var rightRt0 = SafeRect(rightImage);
        if (leftRt0) originWorldPos = leftRt0.position;
        else if (rightRt0) originWorldPos = rightRt0.position;
        originWorldPos.z = 0f;

        float dynamicSplitOffset = _cellSizePx * splitOffsetFactor;

        Vector2 leftTarget = leftStart + negDir * dynamicSplitOffset;
        Vector2 rightTarget = rightStart + posDir * dynamicSplitOffset;

        float st = 0f;
        while (st < splitTime)
        {
            st += Time.unscaledDeltaTime;
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
        ApplyAxisVisualRotation();
        rocketMode = true;

        SpawnTrails(originWorldPos);

        if (_originCellValid && OnStepCell != null)
            OnStepCell(_originCell);

        float movePortion = 0.1f;
        float cellSizePx = _cellSizePx;
        int stepCount = _stepCount;

        for (int i = 0; i < stepCount; i++)
        {
            var rightStartRt = SafeRect(rightImage);
            var leftStartRt = SafeRect(leftImage);

            Vector2 rStart = rightStartRt ? rightStartRt.anchoredPosition : Vector2.zero;
            Vector2 lStart = leftStartRt ? leftStartRt.anchoredPosition : Vector2.zero;

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

                var rightRt = SafeRect(rightImage);
                if (rightRt)
                    rightRt.anchoredPosition = Vector2.LerpUnclamped(rStart, rTarget, u);

                var leftRt = SafeRect(leftImage);
                if (leftRt)
                    leftRt.anchoredPosition = Vector2.LerpUnclamped(lStart, lTarget, u);

                UpdateTrailHeads();
                yield return null;
            }

            var rightRt2 = SafeRect(rightImage);
            if (rightRt2) rightRt2.anchoredPosition = rTarget;
            var leftRt2 = SafeRect(leftImage);
            if (leftRt2) leftRt2.anchoredPosition = lTarget;

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
                if (rightRt2 && HasTileAtStep(i, true))
                {
                    var goR = Instantiate(emittersImpactPrefab, impactParent);
                    var rtR = goR.GetComponent<RectTransform>();
                    if (rtR) rtR.anchoredPosition = rTarget;
                    EnsureAutoDestroy(goR, 0.15f);
                }

                if (leftRt2 && HasTileAtStep(i, false))
                {
                    var goL = Instantiate(emittersImpactPrefab, impactParent);
                    var rtL = goL.GetComponent<RectTransform>();
                    if (rtL) rtL.anchoredPosition = lTarget;
                    EnsureAutoDestroy(goL, 0.15f);
                }
            }

            if (rightRt2 && rightImage) SpawnAfterImage(rightImage, rTarget);
            if (leftRt2 && leftImage) SpawnAfterImage(leftImage, lTarget);

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

    private void SpawnTrails(Vector3 originWorldPos)
    {
        if (!enableTrailBeam)
            return;

        RectTransform parent = trailParent ? trailParent : afterImageParent;
        if (!parent)
            return;

        var leftRt = SafeRect(leftImage);
        if (_leftTrail && !leftRt)
        {
            _leftTrail.Kill();
            _leftTrail = null;
        }
        if (_leftGlowTrail && !leftRt)
        {
            _leftGlowTrail.Kill();
            _leftGlowTrail = null;
        }

        if (leftRt)
        {
            if (trailBeamPrefab)
            {
                _leftTrail = CreateTrailInstance(trailBeamPrefab, parent, originWorldPos);
                _leftTrail.UpdateHead(leftRt.position);
            }

            if (glowTrailBeamPrefab)
            {
                _leftGlowTrail = CreateTrailInstance(glowTrailBeamPrefab, parent, originWorldPos);
                _leftGlowTrail.UpdateHead(leftRt.position);
            }
        }

        var rightRt = SafeRect(rightImage);
        if (_rightTrail && !rightRt)
        {
            _rightTrail.Kill();
            _rightTrail = null;
        }
        if (_rightGlowTrail && !rightRt)
        {
            _rightGlowTrail.Kill();
            _rightGlowTrail = null;
        }

        if (rightRt)
        {
            if (trailBeamPrefab)
            {
                _rightTrail = CreateTrailInstance(trailBeamPrefab, parent, originWorldPos);
                _rightTrail.UpdateHead(rightRt.position);
            }

            if (glowTrailBeamPrefab)
            {
                _rightGlowTrail = CreateTrailInstance(glowTrailBeamPrefab, parent, originWorldPos);
                _rightGlowTrail.UpdateHead(rightRt.position);
            }
        }
    }

    private RocketTrailBeam CreateTrailInstance(RocketTrailBeam prefab, RectTransform parent, Vector3 originWorld)
    {
        var beam = Instantiate(prefab, parent);
        beam.transform.position = Vector3.zero;
        beam.transform.localRotation = Quaternion.identity;
        beam.transform.localScale = Vector3.one;
        beam.Init(originWorld);
        return beam;
    }

    private void UpdateTrailHeads()
    {
        var rightRt = SafeRect(rightImage);
        if (_rightTrail != null)
        {
            if (rightRt != null) _rightTrail.UpdateHead(rightRt.position);
            else { _rightTrail.Kill(); _rightTrail = null; }
        }
        if (_rightGlowTrail != null)
        {
            if (rightRt != null) _rightGlowTrail.UpdateHead(rightRt.position);
            else { _rightGlowTrail.Kill(); _rightGlowTrail = null; }
        }

        var leftRt = SafeRect(leftImage);
        if (_leftTrail != null)
        {
            if (leftRt != null) _leftTrail.UpdateHead(leftRt.position);
            else { _leftTrail.Kill(); _leftTrail = null; }
        }
        if (_leftGlowTrail != null)
        {
            if (leftRt != null) _leftGlowTrail.UpdateHead(leftRt.position);
            else { _leftGlowTrail.Kill(); _leftGlowTrail = null; }
        }
    }

    private void FadeOutTrails()
    {
        if (_leftTrail) _leftTrail.FadeOutAndDestroy();
        if (_rightTrail) _rightTrail.FadeOutAndDestroy();
        if (_leftGlowTrail) _leftGlowTrail.FadeOutAndDestroy();
        if (_rightGlowTrail) _rightGlowTrail.FadeOutAndDestroy();

        _leftTrail = null;
        _rightTrail = null;
        _leftGlowTrail = null;
        _rightGlowTrail = null;
    }

    private void KillTrails()
    {
        if (_leftTrail) { _leftTrail.Kill(); _leftTrail = null; }
        if (_rightTrail) { _rightTrail.Kill(); _rightTrail = null; }

        if (_leftGlowTrail) { _leftGlowTrail.Kill(); _leftGlowTrail = null; }
        if (_rightGlowTrail) { _rightGlowTrail.Kill(); _rightGlowTrail = null; }
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
        var sourceRt = SafeRect(sourceImage);
        if (!sourceRt) return;

        var go = Instantiate(rocketAfterImagePrefab, afterImageParent);
        EnsureAutoDestroy(go, afterImageLife + 0.05f);

        var img = go.GetComponentInChildren<Image>(true);
        var rt = img ? img.rectTransform : go.GetComponent<RectTransform>();
        if (!img || !rt) return;

        img.sprite = sourceImage.sprite;
        img.color = new Color(1f, 1f, 1f, afterImageAlpha);

        rt.anchorMin = sourceRt.anchorMin;
        rt.anchorMax = sourceRt.anchorMax;
        rt.pivot = sourceRt.pivot;
        rt.sizeDelta = sourceRt.sizeDelta;
        rt.localScale = sourceRt.localScale;
        rt.localRotation = sourceRt.localRotation;
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
        var leftRt = SafeRect(leftImage);
        var rightRt = SafeRect(rightImage);
        if (!leftRt || !rightRt) return;

        if (axis == LineAxis.Horizontal)
        {
            leftRt.localEulerAngles = Vector3.zero;
            rightRt.localEulerAngles = Vector3.zero;
        }
        else
        {
            rightRt.localEulerAngles = new Vector3(0f, 0f, -90f);
            leftRt.localEulerAngles = new Vector3(0f, 0f, -90f);
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
        return startDelay + splitTime + (steps * stepDuration) + postDelay;
    }

    [ContextMenu("DEBUG Play Horizontal")]
    private void DebugPlayH() => Play(LineAxis.Horizontal, rightStart, 6, 110f);

    [ContextMenu("DEBUG Play Vertical")]
    private void DebugPlayV() => Play(LineAxis.Vertical, rightStart, 6, 110f);
}
