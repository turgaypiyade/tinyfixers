using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GoalFlyFx : MonoBehaviour
{
    [Header("Overlay Root (same Canvas space as tiles / HUD)")]
    [SerializeField] private RectTransform overlayRoot; // Canvas altında bir root (Screen Space - Overlay ise Canvas root da olur)

    [Header("Timing")]
    [SerializeField] private float popUpPortion = 0.20f;      // ilk pop süresi oranı
    [SerializeField] private float fadeOutPortion = 0.18f;    // son fade oranı
    [SerializeField] private float arcHeight = 160f;          // bezier yükseliği
    [SerializeField] private float sideOffset = 90f;          // bezier yana kaçış

    [Header("Punch")]
    [SerializeField] private float punchScale = 1.12f;
    [SerializeField] private float punchTime = 0.10f;
    public void SetOverlayRoot(RectTransform root) => overlayRoot = root;

    public IEnumerator Play(TileView fromTile, RectTransform targetSlot, float baseDuration)
    {
        if (fromTile == null || targetSlot == null)
            yield break;

        if (overlayRoot == null)
        {
            // Overlay root atanmadıysa: güvenli fallback
            yield break;
        }

        var sprite = fromTile.GetIconSprite();
        if (sprite == null)
            yield break;

        // Ghost GO
        var go = new GameObject("GoalFlyGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(overlayRoot, worldPositionStays: false);
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;

        // Subtle glow effect
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(1f, 1f, 1f, 0.35f); // yumuşak beyaz glow
        shadow.effectDistance = Vector2.zero;              // blur gibi davransın
        shadow.useGraphicAlpha = true;

        // Ghost size = tile icon size (UI hizası için kritik)
        var srcRt = fromTile.RectTransform != null ? fromTile.RectTransform : (RectTransform)fromTile.transform;
        rt.sizeDelta = srcRt.rect.size;

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 1f;

        // Start/End positions in overlayRoot local space
        Vector2 start = WorldToLocalIn(overlayRoot, fromTile.RectTransform);
        Vector2 end   = WorldToLocalIn(overlayRoot, targetSlot);

        rt.anchoredPosition = start;

        float duration = Mathf.Max(0.12f, baseDuration); // toplam ghost uçuş süresi (arttırırsan daha uzun kalır)
        float t = 0f;

        // Bezier control point
        Vector2 mid = (start + end) * 0.5f;
        float dir = (end.x >= start.x) ? 1f : -1f;
        Vector2 control = mid + new Vector2(sideOffset * dir, arcHeight);

        // pop phase
        float popT = Mathf.Clamp01(popUpPortion);
        float popTime = Mathf.Max(0.02f, duration * popT);

        // fly phase (remaining)
        float flyTime = Mathf.Max(0.06f, duration - popTime);

        // 1) Pop-up (quick scale up)
        float p = 0f;
        while (p < popTime)
        {
            p += Time.deltaTime;
            float k = Mathf.Clamp01(p / popTime);
            float s = EaseOutBack(k);
            rt.localScale = Vector3.LerpUnclamped(Vector3.one, Vector3.one * 1.30f, s);
            yield return null;
        }

        // 2) Fly along bezier + shrink into thin line + fade near end
        t = 0f;
        while (t < flyTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flyTime);

            // Position
            Vector2 pos = Bezier2(start, control, end, EaseInOut(k));
            rt.anchoredPosition = pos;
            // base shrink
            float sx = Mathf.Lerp(1.30f, 0.05f, EaseIn(k));
            float sy = Mathf.Lerp(1.30f, 0.20f, EaseInOut(k));

            // motion stretch
            Vector2 v = (end - start);
            float stretch = Mathf.Lerp(0.18f, 0f, k); // başta var, sonda yok
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            {
                sx *= 1f + stretch;   // yatay uçuşta x uzasın
                sy *= 1f - stretch*0.6f;
            }
            else
            {
                sy *= 1f + stretch;   // dikey uçuşta y uzasın
                sx *= 1f - stretch*0.6f;
            }

            rt.localScale = new Vector3(sx, sy, 1f);


            // Fade out near the end
            float fadeStart = 1f - Mathf.Clamp01(fadeOutPortion);
            if (k >= fadeStart)
            {
                float fk = Mathf.InverseLerp(fadeStart, 1f, k);
                cg.alpha = 1f - fk;
            }

            yield return null;
        }

        // Kill ghost
        Destroy(go);

        // 3) Punch target slot (subtle)
        // Punch: icon'u değil, slot'un container'ını oynat (ikon scale bozulmasın)
        // Ayrıca aynı anda gelen birden çok ghost, scale biriktirmesin diye her seferinde baseScale'e döndür.
        var punchTarget = (targetSlot.parent as RectTransform) ?? targetSlot;

        yield return PunchSafe(punchTarget);

    }


    // Cargo (işçi robot) çıkış animasyonu:
    //   1) Bir hücre aşağı (board altındaki boşluğa) düşer
    //   2) Dizini büküp yere konuyormuş gibi kısa squash (landing)
    //   3) Oradan TopHUD goal slotuna yay çizerek zıplar + slot punch
    // Ghost overlayRoot'ta oynar (board mask'i kırpmaz). Gerçek tile'ı çağıran taraf gizler/yok eder.
    public IEnumerator PlayCargoExit(TileView fromTile, RectTransform targetSlot, float jumpDuration)
    {
        if (fromTile == null || overlayRoot == null)
            yield break;

        var sprite = fromTile.GetIconSprite();
        if (sprite == null)
            yield break;

        var go = new GameObject("CargoExitGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(overlayRoot, worldPositionStays: false);
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;

        var srcRt = fromTile.RectTransform != null ? fromTile.RectTransform : (RectTransform)fromTile.transform;
        rt.sizeDelta = srcRt.rect.size;

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 1f;

        Vector2 startPos = WorldToLocalIn(overlayRoot, srcRt);
        rt.anchoredPosition = startPos;

        float cell = rt.rect.height > 1f ? rt.rect.height : rt.sizeDelta.y;

        // 1) Bir hücre aşağı düş (ease-in, hızlanan iniş)
        Vector2 landPos = startPos + new Vector2(0f, -cell);
        const float dropDur = 0.16f;
        float t = 0f;
        while (t < dropDur)
        {
            t += Time.deltaTime;
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, landPos, EaseIn(Mathf.Clamp01(t / dropDur)));
            yield return null;
        }
        rt.anchoredPosition = landPos;

        // 2) Diz bükme (landing squash) — ayaklar yerde kalsın diye squash sırasında biraz aşağı it
        yield return KneeBendLanding(rt, landPos, cell);

        // "Biraz sakin" — yere konup kısa bir an dur, sonra zıpla.
        yield return new WaitForSeconds(0.12f);

        // 3) HUD slotuna yay çizerek zıpla + punch
        if (targetSlot != null)
        {
            Vector2 end = WorldToLocalIn(overlayRoot, targetSlot);
            Vector2 mid = (landPos + end) * 0.5f;
            float dir = (end.x >= landPos.x) ? 1f : -1f;
            Vector2 control = mid + new Vector2(sideOffset * dir, arcHeight);

            float jumpTime = Mathf.Max(0.18f, jumpDuration);
            t = 0f;
            while (t < jumpTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / jumpTime);
                rt.anchoredPosition = Bezier2(landPos, control, end, EaseInOut(k));

                float sx = Mathf.Lerp(1f, 0.25f, EaseIn(k));
                float sy = Mathf.Lerp(1f, 0.40f, EaseInOut(k));
                rt.localScale = new Vector3(sx, sy, 1f);

                float fadeStart = 1f - Mathf.Clamp01(fadeOutPortion);
                if (k >= fadeStart)
                    cg.alpha = 1f - Mathf.InverseLerp(fadeStart, 1f, k);

                yield return null;
            }

            Destroy(go);

            var punchTarget = (targetSlot.parent as RectTransform) ?? targetSlot;
            yield return PunchSafe(punchTarget);
        }
        else
        {
            // Hedef slot yoksa: yerde kısa bir an dur, sonra sönerek kaybol.
            float fade = 0.18f;
            t = 0f;
            while (t < fade)
            {
                t += Time.deltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / fade);
                yield return null;
            }
            Destroy(go);
        }
    }

    private IEnumerator KneeBendLanding(RectTransform rt, Vector2 footPos, float cell)
    {
        Vector3 baseScale = Vector3.one;
        Vector3 squash = new Vector3(1.18f, 0.72f, 1f);
        // Squash'ta merkez yukarı kalkmasın diye yarı-yükseklik kadar aşağı kaydır (ayak yerde hissi).
        float drop = cell * (1f - squash.y) * 0.5f;
        Vector2 squashPos = footPos + new Vector2(0f, -drop);

        const float downDur = 0.08f;
        const float upDur = 0.12f;

        float t = 0f;
        while (t < downDur)
        {
            t += Time.deltaTime;
            float k = EaseOut(Mathf.Clamp01(t / downDur));
            rt.localScale = Vector3.LerpUnclamped(baseScale, squash, k);
            rt.anchoredPosition = Vector2.LerpUnclamped(footPos, squashPos, k);
            yield return null;
        }
        t = 0f;
        while (t < upDur)
        {
            t += Time.deltaTime;
            float k = EaseOut(Mathf.Clamp01(t / upDur));
            rt.localScale = Vector3.LerpUnclamped(squash, baseScale, k);
            rt.anchoredPosition = Vector2.LerpUnclamped(squashPos, footPos, k);
            yield return null;
        }
        rt.localScale = baseScale;
        rt.anchoredPosition = footPos;
    }

    private static readonly System.Collections.Generic.Dictionary<int, Vector3> _baseScales
        = new System.Collections.Generic.Dictionary<int, Vector3>();

    private IEnumerator PunchSafe(RectTransform target)
    {
        if (target == null) yield break;

        int id = target.GetInstanceID();

        // baseScale'i sabitle (ilk gördüğümüz scale)
        if (!_baseScales.TryGetValue(id, out var baseScale) || baseScale == Vector3.zero)
        {
            baseScale = target.localScale;
            _baseScales[id] = baseScale;
        }

        // Her punch öncesi kesin reset (birikmeyi engeller)
        target.localScale = baseScale;

        float half = Mathf.Max(0.02f, punchTime * 0.5f);

        // up
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            target.localScale = Vector3.Lerp(baseScale, baseScale * punchScale, EaseOut(k));
            yield return null;
        }

        // down
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            target.localScale = Vector3.Lerp(baseScale * punchScale, baseScale, EaseIn(k));
            yield return null;
        }

        // kesin eski hal
        target.localScale = baseScale;
    }

    private static Vector2 WorldToLocalIn(RectTransform root, RectTransform other)
    {
        // other pivot world pos -> root local
        Vector3 world = other.TransformPoint(other.rect.center);
        return root.InverseTransformPoint(world);
    }

    private static Vector2 Bezier2(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return (u * u) * a + (2f * u * t) * b + (t * t) * c;
    }

    private static float EaseInOut(float t)
    {
        // smoothstep
        return t * t * (3f - 2f * t);
    }

    private static float EaseIn(float t) => t * t;
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    private static float EaseOutBack(float t)
    {
        // small back overshoot
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
