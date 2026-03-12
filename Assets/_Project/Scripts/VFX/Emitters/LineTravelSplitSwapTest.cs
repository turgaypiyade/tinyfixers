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
    public float startDelay = 0.15f; // Başlangıç bekleme süresi (düşürürsen daha erken başlar)
    public float splitTime = 0.06f;  // Roketin ayrışma süresi (düşürürsen daha hızlı split olur)
    public float splitOffset = 60f;

    [Header("Rocket FX")]
    public float pulseSpeed = 20f;
    public float pulseAmount = 0.05f;

    [Header("AfterImage Trail")]
    public GameObject rocketAfterImagePrefab;
    public RectTransform afterImageParent;
    public float afterImageLife = 0.15f; // Arkadaki hayalet izinin ekranda kalma süresi
    public float afterImageAlpha = 0.55f;
    public float afterImageScaleUp = 1.08f;

    [Header("Timing")]
    public float stepDuration = 0.06f; // Her hücre adımının süresi (düşürürsen roket daha hızlı gider)
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

    // ✅ OnStepCell: Board'a step hücresi bildirir — Run() içinde move bitince tetiklenir
    public Action<Vector2Int> OnStepCell;
    private Vector2Int _originCell;
    private bool _originCellValid;

    public Action OnCompleted;

    private void Awake()
    {
        if (leftImage) leftStart = leftImage.rectTransform.anchoredPosition;
        if (rightImage) rightStart = rightImage.rectTransform.anchoredPosition;
    }

    // ✅ Full overload: originCell + callback ile
    public void Play(
        LineAxis axisMode,
        Vector2 originAnchoredPos,
        Vector2Int originCell,
        int steps,
        float cellSizePxOverride,
        Action<Vector2Int> onStepCell,
        Action onCompleted = null)
    {
        OnStepCell = onStepCell;
        OnCompleted = onCompleted;
        _originCell = originCell;
        _originCellValid = true;
        Play(axisMode, originAnchoredPos, steps, cellSizePxOverride);
    }

    // ✅ Basit overload: callback olmadan
    public void Play(LineAxis axisMode, Vector2 originAnchoredPos, int steps, float cellSizePxOverride)
    {

        if (leftImage) leftImage.enabled = false;
        if (rightImage) rightImage.enabled = false;

        rocketMode = false;
        axis = axisMode;

        leftStart = originAnchoredPos;
        rightStart = originAnchoredPos;

        _stepCount = Mathf.Max(0, steps);
        _cellSizePx = Mathf.Max(1f, cellSizePxOverride);

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
        // ─── startDelay ───────────────────────────────────────────────────────
        // ✅ scaled time kullan (FadeOnly hariç her yer scaled — C maddesi)
        float elapsed = 0f;
        while (elapsed < startDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ─── Hareket yönleri ──────────────────────────────────────────────────
        // ✅ FIX A: UI'da Y ekseni ters — Vertical için Vector2.down kullan
        // Horizontal: left = -X (sol), right = +X (sağ)
        // Vertical:   left = +Y (yukarı, roket 1), right = -Y (aşağı, roket 2)
        //             Ama UI grid'de Y azaldıkça aşağı gidiliyor.
        //             posDir = sağ/aşağı yön, negDir = sol/yukarı yön
        Vector2 posDir = (axis == LineAxis.Horizontal) ? Vector2.right : Vector2.down;
        Vector2 negDir = -posDir;

        if (leftImage && splitLeftSprite)  leftImage.sprite  = splitLeftSprite;
        if (rightImage && splitRightSprite) rightImage.sprite = splitRightSprite;
        ApplyAxisVisualRotation();

        // ─── SPLIT ────────────────────────────────────────────────────────────
        Vector2 leftTarget  = leftStart  + negDir * splitOffset;
        Vector2 rightTarget = rightStart + posDir * splitOffset;

        float st = 0f;
        while (st < splitTime)
        {
            st += Time.deltaTime;
            float u = Mathf.Clamp01(st / splitTime);
            u = u * u * (3f - 2f * u); // smoothstep

            if (leftImage)  leftImage.rectTransform.anchoredPosition  = Vector2.LerpUnclamped(leftStart,  leftTarget,  u);
            if (rightImage) rightImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(rightStart, rightTarget, u);

            yield return null;
        }

        if (leftImage)  leftImage.rectTransform.anchoredPosition  = leftTarget;
        if (rightImage) rightImage.rectTransform.anchoredPosition = rightTarget;

        // ─── ROCKET MODE ──────────────────────────────────────────────────────
        if (leftImage  && rocketLeftSprite)  leftImage.sprite  = rocketLeftSprite;
        if (rightImage && rocketRightSprite) rightImage.sprite = rocketRightSprite;

        ApplyAxisVisualRotation();
        rocketMode = true;

        // ─── STEP TRAVEL ──────────────────────────────────────────────────────
        float movePortion = 0.85f;
        float cellSizePx  = _cellSizePx;
        int   stepCount   = _stepCount;

        for (int i = 0; i < stepCount; i++)
        {
            Vector2 rStart = rightImage ? rightImage.rectTransform.anchoredPosition : Vector2.zero;
            Vector2 lStart = leftImage  ? leftImage.rectTransform.anchoredPosition  : Vector2.zero;

            Vector2 rTarget = rStart + posDir * cellSizePx;
            Vector2 lTarget = lStart + negDir * cellSizePx;

            float moveTime = stepDuration * movePortion;
            float restTime = stepDuration - moveTime;

            float mt = 0f;
            while (mt < moveTime)
            {
                mt += Time.deltaTime;
                float u = Mathf.Clamp01(mt / moveTime);
                u = u * u * (3f - 2f * u);

                if (rightImage) rightImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(rStart, rTarget, u);
                if (leftImage)  leftImage.rectTransform.anchoredPosition  = Vector2.LerpUnclamped(lStart,  lTarget,  u);

                yield return null;
            }

            if (rightImage) rightImage.rectTransform.anchoredPosition = rTarget;
            if (leftImage)  leftImage.rectTransform.anchoredPosition  = lTarget;

            // ✅ FIX B: OnStepCell callback move tamamlanınca tetiklenir (görselle senkron)
            // BoardController'daki EmitH/V artık bu callback'e güveniyor;
            // ayrı coroutine tabanlı timing kaldırıldı.
            if (_originCellValid && OnStepCell != null)
            {
                int s = i + 1;

                if (axis == LineAxis.Horizontal)
                {
                    int leftX = _originCell.x - s;
                    int rightX = _originCell.x + s;

                    if (leftX >= 0)
                        OnStepCell(new Vector2Int(leftX, _originCell.y));

                    if (rightX >= 0 && rightX < 9)
                        OnStepCell(new Vector2Int(rightX, _originCell.y));
                }
                else
                {
                    int downY = _originCell.y - s;
                    int upY = _originCell.y + s;

                    if (downY >= 0)
                        OnStepCell(new Vector2Int(_originCell.x, downY));

                    if (upY >= 0 && upY < 9)
                        OnStepCell(new Vector2Int(_originCell.x, upY));
                }
            }

            // ─── Impact VFX ───────────────────────────────────────────────────
            if (emittersImpactPrefab && impactParent)
            {
                if (rightImage && HasTileAtStep(i, true))
                {
                    var goR = Instantiate(emittersImpactPrefab, impactParent);
                    var rtR = goR.GetComponent<RectTransform>();
                    if (rtR) rtR.anchoredPosition = rTarget;
                    EnsureAutoDestroy(goR, 0.35f);
                }

                if (leftImage && HasTileAtStep(i, false))
                {
                    var goL = Instantiate(emittersImpactPrefab, impactParent);
                    var rtL = goL.GetComponent<RectTransform>();
                    if (rtL) rtL.anchoredPosition = lTarget;
                    EnsureAutoDestroy(goL, 0.35f);
                }
            }

            // ─── AfterImage trail ─────────────────────────────────────────────
            if (rightImage) SpawnAfterImage(rightImage, rTarget);
            if (leftImage)  SpawnAfterImage(leftImage,  lTarget);

            // ─── Rest ─────────────────────────────────────────────────────────
            if (restTime > 0f)
            {
                float rt2 = 0f;
                while (rt2 < restTime)
                {
                    rt2 += Time.deltaTime;
                    yield return null;
                }
            }
        }

        rocketMode = false;

        if (hideOnComplete)
        {
            if (leftImage)  leftImage.enabled  = false;
            if (rightImage) rightImage.enabled = false;

            if (leftImage)  leftImage.rectTransform.anchoredPosition  = leftStart;
            if (rightImage) rightImage.rectTransform.anchoredPosition = rightStart;
        }
        OnCompleted?.Invoke();
    }

    private bool HasTileAtStep(int stepIndex, bool isRight)
    {
        return true;
    }

    private void SpawnAfterImage(Image sourceImage, Vector2 anchoredPos)
    {
        if (!rocketAfterImagePrefab || !afterImageParent || !sourceImage) return;

        var go = Instantiate(rocketAfterImagePrefab, afterImageParent);
        EnsureAutoDestroy(go, afterImageLife + 0.05f);

        var img = go.GetComponentInChildren<Image>(true);
        var rt  = img ? img.rectTransform : go.GetComponent<RectTransform>();
        if (!img || !rt) return;

        img.sprite = sourceImage.sprite;
        img.color  = new Color(1f, 1f, 1f, afterImageAlpha);

        rt.anchorMin       = sourceImage.rectTransform.anchorMin;
        rt.anchorMax       = sourceImage.rectTransform.anchorMax;
        rt.pivot           = sourceImage.rectTransform.pivot;
        rt.sizeDelta       = sourceImage.rectTransform.sizeDelta;
        rt.localScale      = sourceImage.rectTransform.localScale;
        rt.localRotation   = sourceImage.rectTransform.localRotation;
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

    // ✅ FIX C: FadeOnly scaled time kullan (Run ile aynı zaman tabanı)
    private IEnumerator FadeOnly(Image img, RectTransform rt, float life, float scaleUp)
    {
        float ft = 0f;
        Color  c0 = img ? img.color    : Color.white;
        Vector3 s0 = rt  ? rt.localScale : Vector3.one;
        Vector3 s1 = s0 * scaleUp;

        while (ft < life)
        {
            ft += Time.deltaTime; // ✅ scaled (unscaled'dan değiştirildi)
            float u = Mathf.Clamp01(ft / life);

            if (img)
                img.color = new Color(1f, 1f, 1f, Mathf.Lerp(c0.a, 0f, u));

            if (rt)
                rt.localScale = Vector3.LerpUnclamped(s0, s1, u);

            yield return null;
        }
    }

    // ✅ FIX A (rotation): Her iki roket de aynı yönde dönmeli
    // Horizontal: 0° (sprite zaten yatay)
    // Vertical:   Her ikisi de 90° — görsel "yukarı/aşağı bakan roket" etkisi
    //             Sol = -90°, Sağ = 90° olunca görsel iptal oluyor (eski hata).
    //             Şimdi her ikisi de 90° — sprite yataysa dikey çevrilir.
    private void ApplyAxisVisualRotation()
    {
        if (!leftImage || !rightImage) return;

        if (axis == LineAxis.Horizontal)
        {
            leftImage.rectTransform.localEulerAngles  = Vector3.zero;
            rightImage.rectTransform.localEulerAngles = Vector3.zero;
        }
        else // Vertical
        {
            // ✅ İki roket de aynı sprite rotasyonunda — biri +Y biri -Y yönünde hareket eder
            // Sprite'ın hangi yönde olduğuna göre bu değeri inspector'dan ayarlayabilirsiniz.
            // Varsayılan: sağa bakan sprite'ı 90° CW çevirerek aşağı bak, -90° CCW ile yukarı bak.
            rightImage.rectTransform.localEulerAngles = new Vector3(0f, 0f,  90f); // aşağı bakan
            leftImage.rectTransform.localEulerAngles  = new Vector3(0f, 0f, -90f); // yukarı bakan
        }
    }

    private void Update()
    {
        if (!rocketMode) return;

        float scaleOffset = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        if (leftImage)
            leftImage.rectTransform.localScale  = new Vector3(scaleOffset, 1f, 1f);
        if (rightImage)
            rightImage.rectTransform.localScale = new Vector3(scaleOffset, 1f, 1f);
    }

    // ✅ EstimateDuration: startDelay + splitTime + steps*stepDuration + postDelay
    // BoardController bu değeri DestroyAfterUnscaled için kullanıyor.
    public float EstimateDuration(int steps)
    {
        return startDelay + splitTime + (steps * stepDuration) + postDelay;
    }

    [ContextMenu("DEBUG Play Horizontal")]
    private void DebugPlayH() => Play(LineAxis.Horizontal, rightStart, 6, 110f);

    [ContextMenu("DEBUG Play Vertical")]
    private void DebugPlayV() => Play(LineAxis.Vertical, rightStart, 6, 110f);
}
