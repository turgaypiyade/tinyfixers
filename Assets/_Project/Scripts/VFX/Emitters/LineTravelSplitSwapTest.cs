using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LineTravelSplitSwapTestUI : MonoBehaviour
{
    [Header("Impact")]
    public GameObject emittersImpactPrefab;   // EmittersImpactParticles.prefab
    public RectTransform impactParent;        // VFXRoot (UI altında)

    [Header("UI Images")]
    public Image leftImage;
    public Image rightImage;

    [Header("Rocket Sprites (flame'li)")]
    public Sprite rocketLeftSprite;   // lineh_left_flame
    public Sprite rocketRightSprite;  // lineh_right_flame

    [Header("Split Sprites (alevsiz)")]
    public Sprite splitLeftSprite;   // LineH_Left (alevsiz yarım)
    public Sprite splitRightSprite;  // LineH_Right (alevsiz yarım)

    [Header("Tuning")]
    public float startDelay = 0.15f;
    public float splitTime = 0.06f;
    public float splitOffset = 60f;   // UI px gibi düşün

    [Header("Rocket FX")]
    public float pulseSpeed = 20f;
    public float pulseAmount = 0.05f;

    [Header("AfterImage Trail")]
    public GameObject rocketAfterImagePrefab;   // RocketAfterImage.prefab
    public RectTransform afterImageParent;      // VFXRoot
    public float afterImageLife = 0.15f;
    public float afterImageAlpha = 0.55f;
    public float afterImageScaleUp = 1.08f;

    [Header("Timing")]
    public float stepDuration = 0.06f;   // Run içindeki stepDuration ile aynı olsun
    public float postDelay = 0.02f;      // küçük güvenlik payı

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

    private void Awake()
    {
        if (leftImage) leftStart = leftImage.rectTransform.anchoredPosition;
        if (rightImage) rightStart = rightImage.rectTransform.anchoredPosition;
    }

    private void OnEnable()
    {
        // Test otomatiği kapalı
        // StartCoroutine(Run());
    }

    private void Start()
    {
        // Test otomatiği kapalı
        // Play(axis, rightStart, 6, 110f);
    }

    public void Play(LineAxis axisMode, Vector2 originAnchoredPos, int steps, float cellSizePxOverride)
    {
        // 0) Önce kapat (1 frame flash engeli)
        if (leftImage) leftImage.enabled = false;
        if (rightImage) rightImage.enabled = false;

        rocketMode = false;
        axis = axisMode;

        // 1) origin set
        leftStart = originAnchoredPos;
        rightStart = originAnchoredPos;

        _stepCount = Mathf.Max(0, steps);
        _cellSizePx = Mathf.Max(1f, cellSizePxOverride);

        // 2) Pozisyon/scale/rotation RESET (görünmeden önce)
        if (leftImage)
        {
            leftImage.rectTransform.anchoredPosition = leftStart;
            leftImage.rectTransform.localScale = Vector3.one;
            if (splitLeftSprite) leftImage.sprite = splitLeftSprite; // ✅ önce split sprite
        }

        if (rightImage)
        {
            rightImage.rectTransform.anchoredPosition = rightStart;
            rightImage.rectTransform.localScale = Vector3.one;
            if (splitRightSprite) rightImage.sprite = splitRightSprite; // ✅ önce split sprite
        }

        ApplyAxisVisualRotation(); // ✅ doğru axis rotation da görünmeden önce

        // 3) Sonra aç
        if (leftImage) leftImage.enabled = true;
        if (rightImage) rightImage.enabled = true;

        StopAllCoroutines();
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(startDelay);

        Vector2 posDir = (axis == LineAxis.Horizontal) ? Vector2.right : Vector2.up;
        Vector2 negDir = -posDir;

        if (leftImage && splitLeftSprite) leftImage.sprite = splitLeftSprite;
        if (rightImage && splitRightSprite) rightImage.sprite = splitRightSprite;
        ApplyAxisVisualRotation();

        // --- SPLIT ---
        Vector2 leftTarget = leftStart + negDir * splitOffset;
        Vector2 rightTarget = rightStart + posDir * splitOffset;

        float st = 0f;
        while (st < splitTime)
        {
            st += Time.deltaTime;
            float u = Mathf.Clamp01(st / splitTime);
            u = u * u * (3f - 2f * u);

            if (leftImage) leftImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(leftStart, leftTarget, u);
            if (rightImage) rightImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(rightStart, rightTarget, u);

            yield return null;
        }

        if (leftImage) leftImage.rectTransform.anchoredPosition = leftTarget;
        if (rightImage) rightImage.rectTransform.anchoredPosition = rightTarget;

        // --- SWAP (ROCKET MODE) ---
        if (leftImage && rocketLeftSprite) leftImage.sprite = rocketLeftSprite;
        if (rightImage && rocketRightSprite) rightImage.sprite = rocketRightSprite;

        ApplyAxisVisualRotation();
        rocketMode = true;

        // --- STEP TRAVEL ---
        float movePortion = 0.85f;
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
                mt += Time.deltaTime;
                float u = Mathf.Clamp01(mt / moveTime);
                u = u * u * (3f - 2f * u);

                if (rightImage) rightImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(rStart, rTarget, u);
                if (leftImage) leftImage.rectTransform.anchoredPosition = Vector2.LerpUnclamped(lStart, lTarget, u);

                yield return null;
            }

            if (rightImage) rightImage.rectTransform.anchoredPosition = rTarget;
            if (leftImage) leftImage.rectTransform.anchoredPosition = lTarget;

            // Impact (tile varsa - test deseni)
            if (emittersImpactPrefab && impactParent)
            {
                if (rightImage && HasTileAtStep(i, true))
                {
                    var goR = Instantiate(emittersImpactPrefab, impactParent);
                    var rtR = goR.GetComponent<RectTransform>();
                    if (rtR) rtR.anchoredPosition = rTarget;

                    // ✅ clone kendi kendini silecek (unscaled)
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

            // AfterImage trail
            if (rightImage) SpawnAfterImage(rightImage, rTarget);
            if (leftImage) SpawnAfterImage(leftImage, lTarget);

            if (restTime > 0f)
                yield return new WaitForSeconds(restTime);
        }

        rocketMode = false;

        if (hideOnComplete)
        {
            if (leftImage) leftImage.enabled = false;
            if (rightImage) rightImage.enabled = false;

            // Bir sonraki Play için reset (origin'e)
            if (leftImage) leftImage.rectTransform.anchoredPosition = leftStart;
            if (rightImage) rightImage.rectTransform.anchoredPosition = rightStart;
        }
    }

    private bool HasTileAtStep(int stepIndex, bool isRight)
    {
        // TEST: örnek desen
        if (isRight) return stepIndex == 0 || stepIndex == 1 || stepIndex == 3;
        return stepIndex == 0 || stepIndex == 2 || stepIndex == 4;
    }

    private void SpawnAfterImage(Image sourceImage, Vector2 anchoredPos)
    {
        if (!rocketAfterImagePrefab || !afterImageParent || !sourceImage) return;

        var go = Instantiate(rocketAfterImagePrefab, afterImageParent);

        // ✅ Kritik: Ana script disable olsa bile clone kendini silecek.
        EnsureAutoDestroy(go, afterImageLife + 0.05f);

        // Prefab'da Image root'ta olmayabilir; child'tan bul.
        var img = go.GetComponentInChildren<Image>(true);
        var rt = img ? img.rectTransform : go.GetComponent<RectTransform>();
        if (!img || !rt) return; // AutoDestroy zaten var

        img.sprite = sourceImage.sprite;
        img.color = new Color(1f, 1f, 1f, afterImageAlpha);

        rt.anchorMin = sourceImage.rectTransform.anchorMin;
        rt.anchorMax = sourceImage.rectTransform.anchorMax;
        rt.pivot = sourceImage.rectTransform.pivot;
        rt.sizeDelta = sourceImage.rectTransform.sizeDelta;
        rt.localScale = sourceImage.rectTransform.localScale;
        rt.localRotation = sourceImage.rectTransform.localRotation;
        rt.anchoredPosition = anchoredPos;

        // Fade sadece görsel
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

            if (img)
            {
                float a = Mathf.Lerp(c0.a, 0f, u);
                img.color = new Color(1f, 1f, 1f, a);
            }

            if (rt)
                rt.localScale = Vector3.LerpUnclamped(s0, s1, u);

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
            rightImage.rectTransform.localEulerAngles = new Vector3(0, 0, 90f);
            leftImage.rectTransform.localEulerAngles = new Vector3(0, 0, -90f);
        }
    }

    private void Update()
    {
        if (!rocketMode) return;

        float scaleOffset = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        if (leftImage)
            leftImage.rectTransform.localScale = new Vector3(scaleOffset, 1f, 1f);

        if (rightImage)
            rightImage.rectTransform.localScale = new Vector3(scaleOffset, 1f, 1f);
    }

    public float EstimateDuration(int steps)
    {
        return startDelay + splitTime + (steps * stepDuration) + postDelay;
    }

    [ContextMenu("DEBUG Play Horizontal")]
    private void DebugPlayH()
    {
        Play(LineAxis.Horizontal, rightStart, 6, 110f);
    }

    [ContextMenu("DEBUG Play Vertical")]
    private void DebugPlayV()
    {
        Play(LineAxis.Vertical, rightStart, 6, 110f);
    }
}
// Clone üzerinde yaşar: timeScale 0 olsa bile kendini yok eder.
/* internal class AutoDestroyUnscaled : MonoBehaviour
{
    public float lifetime = 0.2f;
    private float _t;

    private void Update()
    {
        _t += Time.unscaledDeltaTime;
        if (_t >= lifetime)
            Destroy(gameObject);
    } 
}*/