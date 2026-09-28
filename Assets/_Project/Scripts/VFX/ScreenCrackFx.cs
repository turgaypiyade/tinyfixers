using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Telefon camı çatladı" şakası: ekranın en üstünde (tüm UI'ın önünde) örümcek-ağı çatlak açar,
/// ardından kaynakçı maymun iple yukarıdan sarkıp çatlağı kaynakla tamir eder ve geri çekilir.
///
/// Tamamen görsel: kendi Screen Space Overlay canvas'ında yaşar, GraphicRaycaster yok ve tüm
/// Graphic'ler raycastTarget=false → input'u (dynamic input) asla kesmez, oyunu bekletmez.
/// Sahne kapanınca canvas'la birlikte yok olur.
///
/// Config'te crackSprite atanmışsa o kullanılır; yoksa prosedürel çizgi ağı.
/// Maymun kareleri Resources/ScreenCrack/ScreenCrackConfig'ten (Bridge event'in Monkey_* kareleri).
/// Çatlama sesini çağıran taraf çalar (hammer: Audio/Jokers/GlassCrack).
/// </summary>
public class ScreenCrackFx : MonoBehaviour
{
    private const int SortingOrder = 900;                 // board/HUD üstü; sistem popup'larının (1000+) altı
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    private const float CrackGrowDuration = 0.09f;
    private const float RepairDelay = 0.55f;              // hammer vuruşu bitsin, sonra maymun gelsin
    private const float RopeDropDuration = 0.45f;
    private const float WeldDuration = 1.1f;
    private const float RopeRiseDuration = 0.4f;
    private const float MonkeyHeight = 340f;              // referans çözünürlükte

    private static readonly Color CrackColor = new Color(1f, 1f, 1f, 0.92f);
    private static readonly Color CrackShadowColor = new Color(0.08f, 0.12f, 0.2f, 0.45f);
    private static readonly Color RopeColor = new Color(0.55f, 0.4f, 0.25f, 1f);

    private RectTransform root;
    private RectTransform crackRoot;
    private CanvasGroup crackGroup;
    private readonly List<CrackSegment> segments = new List<CrackSegment>();
    private Vector2 crackPos;
    private ScreenCrackConfig config;
    private bool weldingSfxOn;

    private struct CrackSegment
    {
        public RectTransform rt;
        public Graphic graphic;
        public float baseAlpha;
        public float length;
        public float startDelay;   // merkezden uzaklığa göre büyüme gecikmesi
        public float distance;     // tamir sırası (dıştan içe söner)
    }

    /// <summary>screenPoint: çatlağın merkezi (piksel, ekran uzayı). offset: referans çözünürlükte
    /// (1080x1920) ek kaydırma — cihazdan bağımsız aynı görsel mesafe.</summary>
    public static ScreenCrackFx Play(Vector2 screenPoint, Vector2 offset = default)
    {
        var go = new GameObject("__ScreenCrackFx", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.layer = LayerMask.NameToLayer("UI");

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        var fx = go.AddComponent<ScreenCrackFx>();
        fx.Begin(screenPoint, offset);
        return fx;
    }

    private void Begin(Vector2 screenPoint, Vector2 offset)
    {
        root = (RectTransform)transform;
        config = Resources.Load<ScreenCrackConfig>("ScreenCrack/ScreenCrackConfig");

        Canvas.ForceUpdateCanvases();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out crackPos);
        crackPos += offset;

        crackRoot = CreateRect("Crack", root);
        crackRoot.anchoredPosition = crackPos;
        crackGroup = crackRoot.gameObject.AddComponent<CanvasGroup>();
        crackGroup.blocksRaycasts = false;
        crackGroup.interactable = false;

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        BuildCrack();
        SpawnGlassChips();
        yield return GrowCrack();

        yield return Wait(RepairDelay);

        if (config != null && config.monkeyWalk != null && config.monkeyWalk.Length > 0)
            yield return MonkeyRepair();
        else
            yield return FadeCrack(0.6f);   // maymun yoksa sessizce sön (asla ekranda kalmasın)

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (weldingSfxOn) GameEventSfx.StopWelding();
    }

    // ── Çatlak ───────────────────────────────────────────────────────────────

    private void BuildCrack()
    {
        Sprite crackSprite = config != null ? config.crackSprite : null;
        if (crackSprite != null)
        {
            var img = CreateImage("CrackSprite", crackRoot, crackSprite, Color.white);
            img.preserveAspect = true;
            float size = 620f;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            AddStatic(img, 0f);
            return;
        }

        // Prosedürel örümcek ağı: 7-9 ana ışın (kırıklı polyline) + dallar + ışınlar arası halka çatlakları.
        int rays = Random.Range(7, 10);
        float baseAngle = Random.Range(0f, 360f);
        var ringPoints = new List<Vector2>[2] { new List<Vector2>(), new List<Vector2>() };
        float[] ringRadii = { 70f, 150f };

        for (int r = 0; r < rays; r++)
        {
            float angle = baseAngle + r * (360f / rays) + Random.Range(-14f, 14f);
            float maxLen = Random.Range(190f, 380f);
            Vector2 p = Vector2.zero;
            float travelled = 0f;
            int steps = Random.Range(3, 6);
            bool[] ringAdded = new bool[2];

            for (int s = 0; s < steps && travelled < maxLen; s++)
            {
                float segLen = maxLen / steps * Random.Range(0.75f, 1.25f);
                angle += Random.Range(-18f, 18f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector2 q = p + dir * segLen;
                float width = Mathf.Lerp(5f, 2f, travelled / maxLen);
                AddSegment(p, q, width, travelled);

                for (int k = 0; k < 2; k++)
                    if (!ringAdded[k] && travelled + segLen >= ringRadii[k])
                    {
                        ringPoints[k].Add(p + dir * (ringRadii[k] - travelled));
                        ringAdded[k] = true;
                    }

                // Ara sıra kısa bir dal.
                if (s > 0 && Random.value < 0.45f)
                {
                    float bAngle = angle + (Random.value < 0.5f ? -1f : 1f) * Random.Range(30f, 55f);
                    Vector2 bDir = new Vector2(Mathf.Cos(bAngle * Mathf.Deg2Rad), Mathf.Sin(bAngle * Mathf.Deg2Rad));
                    AddSegment(q, q + bDir * Random.Range(35f, 80f), 1.6f, travelled + segLen);
                }

                travelled += segLen;
                p = q;
            }
        }

        // Halka çatlakları: komşu ışınların aynı yarıçaptaki noktalarını birleştir (hepsini değil).
        for (int k = 0; k < 2; k++)
        {
            var pts = ringPoints[k];
            for (int i = 0; i < pts.Count; i++)
                if (Random.value < (k == 0 ? 0.85f : 0.55f))
                    AddSegment(pts[i], pts[(i + 1) % pts.Count], k == 0 ? 2.4f : 1.8f, ringRadii[k]);
        }

        // Darbe noktası: küçük beyaz ezik.
        var dot = CreateImage("Impact", crackRoot, null, new Color(1f, 1f, 1f, 0.8f));
        dot.rectTransform.sizeDelta = new Vector2(22f, 22f);
        dot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        AddStatic(dot, 0f);
    }

    // Uzunluğu büyümeyen parça (sprite çatlak / darbe noktası): ölçekle belirir.
    private void AddStatic(Image img, float distance)
    {
        segments.Add(new CrackSegment
        {
            rt = img.rectTransform, graphic = img, baseAlpha = img.color.a, length = -1f, distance = distance
        });
    }

    private void AddSegment(Vector2 from, Vector2 to, float width, float distance)
    {
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len < 1f) return;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        // Önce koyu gölge (cam derinliği), üstüne parlak kırık çizgisi.
        var shadow = CreateImage("CrackShadow", crackRoot, null, CrackShadowColor);
        SetupLine(shadow.rectTransform, from + new Vector2(1.5f, -1.5f), ang, width + 1.5f);
        var line = CreateImage("CrackLine", crackRoot, null, CrackColor);
        SetupLine(line.rectTransform, from, ang, width);

        float delay = distance / 400f * CrackGrowDuration;
        segments.Add(new CrackSegment
        {
            rt = shadow.rectTransform, graphic = shadow, baseAlpha = shadow.color.a,
            length = len, startDelay = delay, distance = distance
        });
        segments.Add(new CrackSegment
        {
            rt = line.rectTransform, graphic = line, baseAlpha = line.color.a,
            length = len, startDelay = delay, distance = distance
        });
    }

    private static void SetupLine(RectTransform rt, Vector2 from, float angle, float width)
    {
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = from;
        rt.localEulerAngles = new Vector3(0f, 0f, angle);
        rt.sizeDelta = new Vector2(0f, width);
    }

    private IEnumerator GrowCrack()
    {
        float total = CrackGrowDuration * 2f;
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            foreach (var s in segments)
            {
                if (s.length < 0f)
                {
                    float k = Mathf.Clamp01(t / CrackGrowDuration);
                    s.rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, k);
                    continue;
                }
                float kk = Mathf.Clamp01((t - s.startDelay) / CrackGrowDuration);
                s.rt.sizeDelta = new Vector2(s.length * kk, s.rt.sizeDelta.y);
            }
            yield return null;
        }
        foreach (var s in segments)
            if (s.length >= 0f) s.rt.sizeDelta = new Vector2(s.length, s.rt.sizeDelta.y);
    }

    // Darbe anında etrafa saçılan minik cam kırıntıları.
    private void SpawnGlassChips()
    {
        int count = Random.Range(8, 13);
        for (int i = 0; i < count; i++)
        {
            var chip = CreateImage("GlassChip", root, null, new Color(0.9f, 0.97f, 1f, 0.9f));
            float size = Random.Range(6f, 14f);
            chip.rectTransform.sizeDelta = new Vector2(size, size * Random.Range(0.4f, 1f));
            chip.rectTransform.anchoredPosition = crackPos;
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(250f, 650f);
            StartCoroutine(Particle(chip, v, 1400f, Random.Range(0.45f, 0.75f), Random.Range(-720f, 720f)));
        }
    }

    // ── Tamir: iple inen kaynakçı maymun ───────────────────────────────────

    private IEnumerator MonkeyRepair()
    {
        Sprite[] walk = config.monkeyWalk;
        Sprite[] bend = config.monkeyBend != null && config.monkeyBend.Length > 0 ? config.monkeyBend : walk;

        Rect r = root.rect;
        // Çatlak ekranın sağ yarısındaysa maymun solda durup sağa bakar; tersi de aynı.
        float facing = crackPos.x > 0f ? 1f : -1f;

        Vector2 spriteSize = walk[0].rect.size;
        float h = MonkeyHeight;
        float w = h * spriteSize.x / spriteSize.y;

        // Eğilme karesinde eller spritenin sağ-alt bölgesinde (~+0.3w, -0.2h).
        Vector2 handOffset = new Vector2(0.3f * w * facing, -0.2f * h);
        Vector2 hangPos = crackPos - handOffset;
        Vector2 startPos = new Vector2(hangPos.x, r.yMax + h * 0.7f);

        var rope = CreateImage("Rope", root, null, RopeColor);
        rope.rectTransform.pivot = new Vector2(0.5f, 0f);
        rope.rectTransform.sizeDelta = new Vector2(5f, 0f);

        var monkey = CreateImage("Monkey", root, walk[0], Color.white);
        monkey.preserveAspect = true;
        monkey.rectTransform.sizeDelta = new Vector2(w, h);
        monkey.rectTransform.localScale = new Vector3(facing, 1f, 1f);

        void PlaceMonkey(Vector2 pos, float swing)
        {
            monkey.rectTransform.anchoredPosition = pos;
            monkey.rectTransform.localEulerAngles = new Vector3(0f, 0f, swing);
            Vector2 ropeBottom = pos + new Vector2(0f, h * 0.42f);
            rope.rectTransform.anchoredPosition = ropeBottom;
            rope.rectTransform.sizeDelta = new Vector2(5f, Mathf.Max(0f, r.yMax - ropeBottom.y + 10f));
        }

        // İn: hafif aşma + sarkaç salınımı.
        float t = 0f;
        while (t < RopeDropDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / RopeDropDuration);
            float e = EaseOutBack(k);
            PlaceMonkey(Vector2.LerpUnclamped(startPos, hangPos, e), Mathf.Sin(k * Mathf.PI * 2f) * 6f * (1f - k));
            monkey.sprite = walk[(int)(t * 8f) % walk.Length];
            yield return null;
        }
        PlaceMonkey(hangPos, 0f);

        // Eğil (kaynak pozisyonu).
        for (int i = 0; i < bend.Length; i++)
        {
            monkey.sprite = bend[i];
            yield return Wait(0.08f);
        }

        // Kaynak: kıvılcımlar + parlama, çatlak dıştan içe söner.
        if (config.useWeldingSfx)
        {
            GameEventSfx.StartWelding();
            weldingSfxOn = true;
        }

        var glow = CreateImage("WeldGlow", root, null, new Color(0.75f, 0.9f, 1f, 0f));
        glow.rectTransform.sizeDelta = new Vector2(34f, 34f);
        glow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

        float maxDist = 1f;
        foreach (var s in segments) maxDist = Mathf.Max(maxDist, s.distance);

        t = 0f;
        float sparkTimer = 0f;
        while (t < WeldDuration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / WeldDuration);

            // Kaynak ucu çatlak merkezinin etrafında küçük daireler çizer.
            Vector2 tip = crackPos + new Vector2(Mathf.Cos(t * 14f), Mathf.Sin(t * 11f)) * 18f;
            glow.rectTransform.anchoredPosition = tip;
            glow.color = new Color(0.75f, 0.9f, 1f, Random.Range(0.5f, 1f));
            glow.rectTransform.localScale = Vector3.one * Random.Range(0.7f, 1.3f);
            monkey.rectTransform.anchoredPosition = hangPos + new Vector2(0f, Mathf.Sin(t * 30f) * 1.5f);

            sparkTimer -= dt;
            if (sparkTimer <= 0f)
            {
                sparkTimer = 0.025f;
                SpawnWeldSpark(tip);
            }

            // Dıştan içe: en uzak çatlaklar önce söner.
            foreach (var s in segments)
            {
                float fadeStart = (1f - s.distance / maxDist) * 0.7f;   // uzak=0 → erken
                float a = 1f - Mathf.Clamp01((k - fadeStart) / 0.3f);
                var c = s.graphic.color;
                c.a = s.baseAlpha * a;
                s.graphic.color = c;
            }
            yield return null;
        }

        if (weldingSfxOn)
        {
            GameEventSfx.StopWelding();
            weldingSfxOn = false;
        }
        crackGroup.alpha = 0f;
        Destroy(glow.gameObject);

        // Tamir bitti: cam parlaması.
        StartCoroutine(RepairShine());

        // Doğrul.
        for (int i = bend.Length - 2; i >= 0; i--)
        {
            monkey.sprite = bend[i];
            yield return Wait(0.07f);
        }
        monkey.sprite = walk[0];
        yield return Wait(0.2f);

        // İple yukarı çekil.
        t = 0f;
        Vector2 from = hangPos;
        while (t < RopeRiseDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / RopeRiseDuration);
            PlaceMonkey(Vector2.LerpUnclamped(from, startPos, k * k), Mathf.Sin(k * Mathf.PI) * -5f * facing);
            yield return null;
        }
    }

    private void SpawnWeldSpark(Vector2 at)
    {
        Color c = Random.value < 0.5f ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(1f, 0.55f, 0.15f, 1f);
        var spark = CreateImage("WeldSpark", root, null, c);
        spark.rectTransform.sizeDelta = new Vector2(Random.Range(5f, 9f), Random.Range(2f, 3.5f));
        spark.rectTransform.anchoredPosition = at;
        float a = Random.Range(-160f, -20f) * Mathf.Deg2Rad;   // ağırlıkla aşağı-yana
        Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.8f) * Random.Range(180f, 420f);
        StartCoroutine(Particle(spark, v, 1600f, Random.Range(0.25f, 0.45f), 0f, alignToVelocity: true));
    }

    private IEnumerator RepairShine()
    {
        var shine = CreateImage("RepairShine", root, null, new Color(1f, 1f, 1f, 0.7f));
        shine.rectTransform.anchoredPosition = crackPos;
        shine.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        float t = 0f;
        const float d = 0.3f;
        while (t < d && shine != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            float s = Mathf.Lerp(20f, 140f, k);
            shine.rectTransform.sizeDelta = new Vector2(s, s);
            shine.color = new Color(1f, 1f, 1f, 0.7f * (1f - k));
            yield return null;
        }
        if (shine != null) Destroy(shine.gameObject);
    }

    private IEnumerator FadeCrack(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            crackGroup.alpha = 1f - Mathf.Clamp01(t / duration);
            yield return null;
        }
        crackGroup.alpha = 0f;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private IEnumerator Particle(Image img, Vector2 velocity, float gravity, float life, float spin, bool alignToVelocity = false)
    {
        RectTransform rt = img.rectTransform;
        Color baseColor = img.color;
        float t = 0f;
        float rot = Random.Range(0f, 360f);
        while (t < life && rt != null)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            velocity.y -= gravity * dt;
            rt.anchoredPosition += velocity * dt;
            rot = alignToVelocity ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : rot + spin * dt;
            rt.localEulerAngles = new Vector3(0f, 0f, rot);
            img.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - t / life));
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private RectTransform CreateRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var rt = CreateRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.4f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
