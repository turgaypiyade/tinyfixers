using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir Safe (kasa) instance'ının görseli. GridSpawner her SafeEntry için bir tane spawn eder ve
/// Setup ile SafeObstacleService'e + origin'e bağlar. Görsel mantık:
///   - Body (mor gövde) GridSpawner tarafından NxN boyutuna ölçeklenir.
///   - LockPanel (ön yüz + 3 knob + 3 sayaç) sabit boyutta, gövdeyle büyümez (prefab'da kurulur).
///   - Her vuruşta: aktif kilidin sayacı güncellenir + knob'u yukarıdan aşağı kademeli kayar
///     (progress = (total-remaining)/total). Kilit kapanınca knob en altta sabit kalır.
///   - Kasa kırılınca: kırık sprite gösterilir, panel gizlenir, opsiyonel break particle.
///
/// Knob'ları prefab'da slot TEPESİNE yerleştir; view o pozisyonu "top" alır ve aşağı kaydırır.
/// </summary>
public sealed class SafeObstacleView : MonoBehaviour
{
    [Header("Body / Panel")]
    [Tooltip("Mor gövde — GridSpawner NxN boyutuna ölçekler.")]
    [SerializeField] private RectTransform bodyRect;
    [Tooltip("Ön yüz + knob + sayaçları içeren sabit panel (gövdeyle büyümez).")]
    [SerializeField] private GameObject lockPanel;
    [Tooltip("Kırık/açık kasa görseli — başta gizli, kırılınca gösterilir.")]
    [SerializeField] private GameObject brokenVisual;

    [Header("Locks (sıra: kırmızı, sarı, yeşil)")]
    [Tooltip("3 knob — prefab'da slot TEPESİNE yerleştir (top pozisyonu buradan alınır).")]
    [SerializeField] private RectTransform[] knobs = new RectTransform[3];
    [Tooltip("3 sayaç (kalan hit). Renkleri prefab'da ayarla.")]
    [SerializeField] private TMP_Text[] counters = new TMP_Text[3];

    [Header("Hit Mode Görseli")]
    [Tooltip("Ordered modda aktif olmayan ama hâlâ açık kilitlerin alpha değeri.")]
    [SerializeField, Range(0.1f, 1f)] private float inactiveOpenLockAlpha = 0.38f;
    [Tooltip("Kapanmış kilitlerin alpha değeri.")]
    [SerializeField, Range(0.1f, 1f)] private float closedLockAlpha = 0.58f;
    [Tooltip("Ordered modda aktif kilide uygulanacak hafif ölçek vurgusu.")]
    [SerializeField, Range(1f, 1.2f)] private float activeLockScale = 1.06f;

    [Header("Animasyon")]
    [Tooltip("Knob progress=1'de yukarıdan ne kadar AŞAĞI kayar (panel-local birim). Slot yüksekliği - knob yüksekliği.")]
    [SerializeField] private float knobTravelY = 102.797f;
    [SerializeField, Min(0.05f)] private float knobSlideDuration = 0.35f;

    [Header("Break FX (opsiyonel)")]
    [SerializeField] private GameObject breakParticlePrefab;
    [Tooltip("Kasa çökerken merkezden saçılan 'dökülme' partikülü (opsiyonel). Boşsa breakParticlePrefab kullanılır.")]
    [SerializeField] private GameObject dissolveParticlePrefab;
    [SerializeField, Min(0f)] private float brokenVisualDuration = 0.45f;

    [Header("Break Animasyonu")]
    [Tooltip("Knob'ların patlama (scale-up + sönme) süresi.")]
    [SerializeField, Min(0f)] private float knobBurstDuration = 0.16f;
    [Tooltip("brokenVisual'ın pop-in süresi ve overshoot'u.")]
    [SerializeField, Min(0f)] private float revealPopDuration = 0.28f;
    [SerializeField, Range(1f, 1.6f)] private float revealPopOvershoot = 1.18f;

    [Header("Ezilip Büzülme (Squash & Stretch)")]
    [Tooltip("Kırılma öncesi yumuşak squash-stretch salınımının süresi.")]
    [SerializeField, Min(0.05f)] private float breakSquashDuration = 0.42f;
    [Tooltip("Squash genliği — ne kadar ezilip büzülür (0.18 = %18).")]
    [SerializeField, Range(0.05f, 0.4f)] private float breakSquashAmount = 0.18f;
    [Tooltip("Squash boyunca kaç salınım (jelly wobble sayısı).")]
    [SerializeField, Range(1f, 5f)] private float breakSquashOscillations = 2.5f;
    [Tooltip("Eğil-bük yalpasında gövdenin sağa-sola dönme açısı (derece).")]
    [SerializeField, Range(0f, 30f)] private float breakBendAngle = 14f;

    [Header("Kırılma Parçaları (Shatter)")]
    [Tooltip("Caseparts sheet'inden kırık parça sprite'ları. Atanırsa her kırılmada rastgele 3-4 tanesi " +
             "seçilir; boşsa gövde sprite'ından kopya parça kullanılır.")]
    [SerializeField] private Sprite[] caseFragmentSprites;
    [Tooltip("Gövde sprite'ından kopya parça kullanılırken kaç parçaya ayrılır (case sprite yoksa).")]
    [SerializeField, Min(3)] private int fragmentCount = 7;
    [Tooltip("Parça boyutu — gövdenin küçük kenarının oranı (0.42 = büyükçe parçalar).")]
    [SerializeField, Range(0.2f, 0.8f)] private float fragmentScale = 0.42f;
    [Tooltip("Saçılma hızı — gövde küçük kenarının oranı.")]
    [SerializeField, Range(0.3f, 2f)] private float fragmentSpread = 0.9f;
    [Tooltip("Parça yerçekimi — gövde küçük kenarının oranı.")]
    [SerializeField, Range(0f, 3f)] private float fragmentGravity = 1.3f;
    [Tooltip("Parça ömrü (sn). Yumuşak saçılıp söner.")]
    [SerializeField, Min(0.1f)] private float fragmentLifetime = 0.6f;

    [Header("Reveal Glow (opsiyonel)")]
    [Tooltip("İçeriğin ARKASINA konacak yumuşak radial glow Image'i. Başta gizli; kasa açılınca " +
             "büyüyüp sönerek 'kilit açıldı, ödül geldi' hissi verir. Atanmazsa no-op.")]
    [SerializeField] private RectTransform revealGlow;
    [Tooltip("Glow'un ulaşacağı ölçek ve toplam süre.")]
    [SerializeField, Range(1f, 2.5f)] private float revealGlowScale = 1.6f;
    [SerializeField, Min(0f)] private float revealGlowDuration = 0.5f;
    [Tooltip("Glow'un tepe alpha'sı.")]
    [SerializeField, Range(0f, 1f)] private float revealGlowPeakAlpha = 0.85f;

    private SafeObstacleService service;
    private int origin = -1;
    private Vector2[] knobTop;          // her knob'un başlangıç (top) anchoredPosition'ı
    private Vector3[] knobBaseScale;
    private Coroutine[] knobCo;
    private Coroutine focusRefreshCo;

    /// GridSpawner çağırır: service'e bağla, origin'i ata, knob başlangıçlarını yakala.
    public void Setup(SafeObstacleService svc, int safeOrigin)
    {
        service = svc;
        origin  = safeOrigin;

        knobTop = new Vector2[knobs.Length];
        knobBaseScale = new Vector3[knobs.Length];
        knobCo  = new Coroutine[knobs.Length];
        for (int i = 0; i < knobs.Length; i++)
        {
            if (knobs[i] == null) continue;
            knobTop[i] = knobs[i].anchoredPosition;
            knobBaseScale[i] = knobs[i].localScale;
        }

        if (brokenVisual != null) brokenVisual.SetActive(false);
        if (lockPanel != null)    lockPanel.SetActive(true);

        // Başlangıç sayaçları.
        for (int i = 0; i < counters.Length; i++)
            if (counters[i] != null && service != null)
                counters[i].text = service.GetTotal(origin, i).ToString();

        if (service != null)
        {
            service.OnSafeHit    += HandleSafeHit;
            service.OnLockClosed += HandleLockClosed;
            service.OnSafeBroken += HandleSafeBroken;
        }

        UpdateLockFocusVisuals();
    }

    /// Body'yi NxN boyutuna ölçekler (GridSpawner çağırır).
    public void SetBodySize(float width, float height)
    {
        if (bodyRect != null) bodyRect.sizeDelta = new Vector2(width, height);
    }

    private void OnDestroy()
    {
        if (service != null)
        {
            service.OnSafeHit    -= HandleSafeHit;
            service.OnLockClosed -= HandleLockClosed;
            service.OnSafeBroken -= HandleSafeBroken;
        }
    }

    private void HandleSafeHit(int o, int lockIdx, int remaining, int total)
    {
        if (o != origin) return;
        if (lockIdx < 0 || lockIdx >= knobs.Length) return;

        if (counters[lockIdx] != null) counters[lockIdx].text = remaining.ToString();

        float progress = total > 0 ? (float)(total - remaining) / total : 1f;
        SlideKnob(lockIdx, progress);

        if (remaining > 0)
            UpdateLockFocusVisuals();
        else
            ScheduleLockFocusRefresh();
    }

    private void HandleLockClosed(int o, int lockIdx)
    {
        if (o != origin) return;
        SlideKnob(lockIdx, 1f);   // en altta sabit
        ScheduleLockFocusRefresh();
    }

    private void HandleSafeBroken(int o)
    {
        if (o != origin) return;
        SetRaycastTargets(false);
        StartCoroutine(CoPlaySafeBreak());
    }

    // ÇİZGİ FİLM AKIŞI (sırayla): 1) ŞİŞ (nefes alır gibi büyür) → 2) EĞİL-BÜK (jöle gibi
    // squash/stretch + sağa-sola yalpalama) → 3) KIRIL (gövde ANINDA kaybolur — arkada iz kalmaz —
    // parçalar etrafa saçılır + glow). Adımlar üst üste binmez, sırayla oynar.
    private IEnumerator CoPlaySafeBreak()
    {
        var body = bodyRect != null ? bodyRect : transform as RectTransform;
        Vector3 baseScale = body != null ? body.localScale : Vector3.one;
        Quaternion baseRot = body != null ? body.localRotation : Quaternion.identity;
        var bodyGraphic = body != null ? body.GetComponent<Graphic>() : null;
        Color bodyBaseColor = bodyGraphic != null ? bodyGraphic.color : Color.white;

        // 1) ŞİŞ — önce ufak nefes çekişi (anticipation), sonra yumuşakça şişer.
        if (body != null)
        {
            yield return CoScaleTo(body, body.localScale, baseScale * 0.93f, 0.07f);
            yield return CoScaleTo(body, body.localScale, baseScale * 1.25f, 0.16f);
        }

        // 2) EĞİL-BÜK — jöle: şişmiş hâlin etrafında squash/stretch + rotasyon yalpası (birkaç salınım).
        if (body != null)
            yield return CoBendWobble(body, baseRot);

        // 3) KIRIL — gövde + panel + perçinler ANINDA kaybolur; parçalar saçılır, glow patlar.
        if (lockPanel != null) lockPanel.SetActive(false);
        if (brokenVisual != null) brokenVisual.SetActive(false);   // arkada açık-kasa görseli KALMASIN
        BurstKnobs();
        SpawnShatterPieces(body, bodyGraphic, bodyBaseColor);
        if (body != null) body.gameObject.SetActive(false);         // gövde tamamen gitti (iz yok)

        if (breakParticlePrefab != null)
            Instantiate(breakParticlePrefab, SafeCenterWorld(), Quaternion.identity, transform.parent);
        if (revealGlow != null)
            StartCoroutine(CoRevealGlow());

        // Parçalar uçup sönene kadar bekle, sonra sahneden çık.
        yield return new WaitForSeconds(Mathf.Max(fragmentLifetime, 0.5f) + 0.05f);
        Destroy(gameObject);
    }

    // Çizgi film jölesi: şişmiş gövdeyi sağa-sola büker (rotasyon) + squash/stretch yapar; genlik
    // sona doğru artar, son yalpada en belirgin — sonra kırılır. Sinüs tabanlı, tamamen smooth.
    private IEnumerator CoBendWobble(RectTransform rt, Quaternion baseRot)
    {
        Vector3 center = rt.localScale;                              // şişmiş ölçek
        float dur = Mathf.Max(0.05f, breakSquashDuration);
        float osc = Mathf.Max(1f, breakSquashOscillations);
        float amp = breakSquashAmount;
        float tilt = breakBendAngle;
        float t = 0f;
        while (t < dur && rt != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float grow = Mathf.Lerp(0.55f, 1f, k);                   // yalpa sona doğru büyür
            float s = Mathf.Sin(k * Mathf.PI * 2f * osc);
            float sq = s * amp * grow;
            rt.localScale = new Vector3(center.x * (1f + sq), center.y * (1f - sq), center.z);
            rt.localRotation = baseRot * Quaternion.Euler(0f, 0f, s * tilt * grow);
            yield return null;
        }
        if (rt != null) rt.localRotation = baseRot;
    }

    // Gövdeyi büyükçe parçalara böl: her parça gövde sprite'ından (hafif koyulaştırılmış) bir kopya,
    // merkezden radyal + yerçekimiyle YUMUŞAK saçılır. Her parça kendi kendini yönetir (leak yok).
    private void SpawnShatterPieces(RectTransform body, Graphic bodyGraphic, Color baseColor)
    {
        var parent = transform.parent;
        if (parent == null) return;

        Sprite bodyFrag = (bodyGraphic is Image bi) ? bi.sprite : null;
        Vector3 centerWorld = SafeCenterWorld();
        Vector2 bodySize = body != null ? body.rect.size : new Vector2(100f, 100f);
        float minDim = Mathf.Max(1f, Mathf.Min(bodySize.x, bodySize.y));
        float pieceBase = minDim * fragmentScale;

        // Caseparts sheet atanmışsa: her kırılmada rastgele 3-4 farklı parça seç. Yoksa gövde kopyası.
        bool useCase = caseFragmentSprites != null && caseFragmentSprites.Length > 0;
        int[] pick = null;
        int n;
        if (useCase)
        {
            n = Mathf.Min(Random.Range(3, 5), caseFragmentSprites.Length);   // 3-4 parça
            pick = PickDistinctIndices(caseFragmentSprites.Length, n);
        }
        else
        {
            n = Mathf.Max(3, fragmentCount);
        }

        for (int i = 0; i < n; i++)
        {
            Sprite s = useCase ? caseFragmentSprites[pick[i]] : bodyFrag;
            if (useCase && s == null) continue;

            var go = new GameObject("SafeFragment", typeof(Image));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.position = centerWorld;

            float sz = pieceBase * Random.Range(0.85f, 1.25f);
            // Case parçaları kendi en-boy oranını korusun (kare değil).
            float ar = (s != null && s.rect.width > 0f) ? s.rect.height / s.rect.width : 1f;
            rt.sizeDelta = new Vector2(sz, sz * ar);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (useCase)
            {
                // Gerçek parça art'ı → kendi renginde kalsın (tint yok).
                img.color = Color.white;
            }
            else
            {
                float shade = Random.Range(0.72f, 1f);
                img.color = new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 1f);
            }

            float angle = (360f / n) * i + Random.Range(-22f, 22f);
            float speed = minDim * fragmentSpread * Random.Range(0.8f, 1.25f);
            Vector2 vel = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                      Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;

            var motion = go.AddComponent<SafeFragmentMotion>();
            motion.Init(vel, Random.Range(-200f, 200f), fragmentLifetime, minDim * fragmentGravity);
        }
    }

    // 0..total-1 arasından tekrarsız 'count' indeks seç (kısmi Fisher-Yates).
    private static int[] PickDistinctIndices(int total, int count)
    {
        count = Mathf.Clamp(count, 0, total);
        var pool = new int[total];
        for (int i = 0; i < total; i++) pool[i] = i;
        for (int i = 0; i < count; i++)
        {
            int j = Random.Range(i, total);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        var result = new int[count];
        System.Array.Copy(pool, result, count);
        return result;
    }

    // Patlama flash'i: gövde bir an sıcak-beyaza parlar (hızlı yükselir, yavaş söner).
    private IEnumerator CoFlash(Graphic g, Color baseColor)
    {
        Color hot = new Color(1f, 0.97f, 0.85f, baseColor.a);
        float d = 0.16f;
        float t = 0f;
        while (t < d && g != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            float a = k < 0.3f ? Mathf.Lerp(0f, 1f, k / 0.3f) : Mathf.Lerp(1f, 0f, (k - 0.3f) / 0.7f);
            g.color = Color.Lerp(baseColor, hot, a);
            yield return null;
        }
        if (g != null) g.color = baseColor;
    }

    // Kasanın görsel merkezinin dünya pozisyonu (pivot köşede olsa da). Partikülleri buraya doğur.
    private Vector3 SafeCenterWorld()
    {
        var r = bodyRect != null ? bodyRect : transform as RectTransform;
        if (r == null) return transform.position;
        Vector2 size = r.rect.size;
        Vector3 localCenter = new Vector3((0.5f - r.pivot.x) * size.x, (0.5f - r.pivot.y) * size.y, 0f);
        return r.TransformPoint(localCenter);
    }

    // Sahneden çıkış: MERKEZE doğru büzülür + alpha söner + merkezden partikül dökülür.
    private IEnumerator CoFadeShrinkOut(float dur)
    {
        var root = transform as RectTransform;
        if (root == null) yield break;

        // Merkeze büzülsün: ölçek pivot etrafında küçülür; pivot köşedeyse (UI'da tipik sol-üst)
        // oraya kaçardı. Pivot'u merkeze al + pozisyonu telafi et (görsel yerinde kalır).
        Vector2 size = root.rect.size;
        Vector2 dPivot = new Vector2(0.5f, 0.5f) - root.pivot;
        root.anchoredPosition += new Vector2(dPivot.x * size.x * root.localScale.x,
                                             dPivot.y * size.y * root.localScale.y);
        root.pivot = new Vector2(0.5f, 0.5f);

        // Dökülme partikülü: kasa çökerken merkezden bir kez saç (ayrı prefab yoksa break partikülü).
        var dissolveFx = dissolveParticlePrefab != null ? dissolveParticlePrefab : breakParticlePrefab;
        if (dissolveFx != null)
            Instantiate(dissolveFx, SafeCenterWorld(), Quaternion.identity, transform.parent);

        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        Vector3 s0 = root.localScale;
        float d = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            float e = k * k * (3f - 2f * k);   // smoothstep
            root.localScale = Vector3.LerpUnclamped(s0, s0 * 0.25f, e);   // merkeze belirgin çöküş
            cg.alpha = 1f - e;
            yield return null;
        }
    }

    // Knob'ları yerinde patlat: hızlı büyü + sön (kilitler "kopuyor" hissi).
    private void BurstKnobs()
    {
        if (knobs == null) return;
        for (int i = 0; i < knobs.Length; i++)
            if (knobs[i] != null)
            {
                if (knobCo != null && i < knobCo.Length && knobCo[i] != null) StopCoroutine(knobCo[i]);
                StartCoroutine(CoKnobBurst(knobs[i]));
            }
    }

    private IEnumerator CoKnobBurst(RectTransform knob)
    {
        Vector3 s0 = knob.localScale;
        var graphics = knob.GetComponentsInChildren<Graphic>(true);
        float d = Mathf.Max(0.01f, knobBurstDuration);
        float t = 0f;
        while (t < d && knob != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            // 0→0.35: 1→1.35 büyü, 0.35→1: 1.35→0 küçül; alpha 1→0.
            float scale = k < 0.35f
                ? Mathf.Lerp(1f, 1.35f, k / 0.35f)
                : Mathf.Lerp(1.35f, 0f, (k - 0.35f) / 0.65f);
            knob.localScale = s0 * scale;
            for (int g = 0; g < graphics.Length; g++) SetGraphicAlpha(graphics[g], 1f - k);
            yield return null;
        }
        if (knob != null) knob.gameObject.SetActive(false);
    }

    private IEnumerator CoScaleTo(RectTransform rt, Vector3 from, Vector3 to, float dur)
    {
        float d = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < d && rt != null)
        {
            t += Time.deltaTime;
            float e = Mathf.Clamp01(t / d);
            e = e * e * (3f - 2f * e); // smoothstep
            rt.localScale = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }
        if (rt != null) rt.localScale = to;
    }

    // Overshoot'lu pop-in: 0 → overshoot → 1 (back-ease hissi).
    private IEnumerator CoPopIn(RectTransform rt, float dur, float overshoot)
    {
        Vector3 target = rt.localScale;
        float d = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < d && rt != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            // 0→0.6: 0→overshoot, 0.6→1: overshoot→1
            float s = k < 0.6f
                ? Mathf.Lerp(0f, overshoot, k / 0.6f)
                : Mathf.Lerp(overshoot, 1f, (k - 0.6f) / 0.4f);
            rt.localScale = target * s;
            yield return null;
        }
        if (rt != null) rt.localScale = target;
    }

    // İçeriğin arkasında ışıma: 0.5→revealGlowScale büyür, alpha hızlı yükselip söner. Sonra gizle.
    private IEnumerator CoRevealGlow()
    {
        revealGlow.gameObject.SetActive(true);
        var graphics = revealGlow.GetComponentsInChildren<Graphic>(true);
        Vector3 baseScale = revealGlow.localScale;
        float d = Mathf.Max(0.01f, revealGlowDuration);
        float t = 0f;
        while (t < d && revealGlow != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            float ease = 1f - Mathf.Pow(1f - k, 3f);                       // OutCubic (büyüme)
            revealGlow.localScale = baseScale * Mathf.Lerp(0.5f, revealGlowScale, ease);
            // alpha: 0→peak (ilk %25) → 0 (kalan) — bloom sonra sön.
            float a = k < 0.25f ? Mathf.Lerp(0f, revealGlowPeakAlpha, k / 0.25f)
                                : Mathf.Lerp(revealGlowPeakAlpha, 0f, (k - 0.25f) / 0.75f);
            for (int g = 0; g < graphics.Length; g++) SetGraphicAlpha(graphics[g], a);
            yield return null;
        }
        if (revealGlow != null) revealGlow.gameObject.SetActive(false);
    }

    private void SetRaycastTargets(bool value)
    {
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = value;
    }

    private void ScheduleLockFocusRefresh()
    {
        if (focusRefreshCo == null)
            focusRefreshCo = StartCoroutine(CoRefreshLockFocusNextFrame());
    }

    private IEnumerator CoRefreshLockFocusNextFrame()
    {
        yield return null;
        focusRefreshCo = null;
        UpdateLockFocusVisuals();
    }

    private void UpdateLockFocusVisuals()
    {
        if (service == null)
            return;

        SafeLockHitMode hitMode = service.GetHitMode(origin);
        int activeLock = service.GetActiveLock(origin);
        int count = Mathf.Max(knobs != null ? knobs.Length : 0, counters != null ? counters.Length : 0);

        for (int i = 0; i < count; i++)
        {
            bool open = service.GetRemaining(origin, i) > 0;
            bool active = open && hitMode == SafeLockHitMode.Ordered && i == activeLock;
            float alpha = ResolveLockAlpha(hitMode, open, active);

            SetLockAlpha(i, alpha);

            if (knobs != null && i < knobs.Length && knobs[i] != null && knobBaseScale != null && i < knobBaseScale.Length)
                knobs[i].localScale = knobBaseScale[i] * (active ? activeLockScale : 1f);
        }
    }

    private float ResolveLockAlpha(SafeLockHitMode hitMode, bool open, bool active)
    {
        if (!open)
            return closedLockAlpha;
        if (hitMode == SafeLockHitMode.AnyColor)
            return 1f;
        return active ? 1f : inactiveOpenLockAlpha;
    }

    private void SetLockAlpha(int lockIndex, float alpha)
    {
        if (knobs != null && lockIndex >= 0 && lockIndex < knobs.Length && knobs[lockIndex] != null)
        {
            var graphics = knobs[lockIndex].GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                SetGraphicAlpha(graphics[i], alpha);
        }

        if (counters != null && lockIndex >= 0 && lockIndex < counters.Length && counters[lockIndex] != null)
            SetGraphicAlpha(counters[lockIndex], alpha);
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private void SlideKnob(int i, float progress)
    {
        if (i < 0 || i >= knobs.Length || knobs[i] == null || knobTop == null) return;
        Vector2 target = knobTop[i] + Vector2.down * (knobTravelY * Mathf.Clamp01(progress));
        if (knobCo[i] != null) StopCoroutine(knobCo[i]);
        knobCo[i] = StartCoroutine(CoSlide(knobs[i], target));
    }

    private IEnumerator CoSlide(RectTransform knob, Vector2 target)
    {
        Vector2 from = knob.anchoredPosition;
        float t = 0f;
        while (t < knobSlideDuration && knob != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / knobSlideDuration);
            float e = k * k * (3f - 2f * k);   // smoothstep
            knob.anchoredPosition = Vector2.LerpUnclamped(from, target, e);
            yield return null;
        }
        if (knob != null) knob.anchoredPosition = target;
    }
}

/// <summary>
/// Kasa kırılınca saçılan tek bir parçayı kendi kendine yönetir: merkezden radyal savrulur
/// (easeOut ile YUMUŞAK yavaşlar) + yerçekimi + yavaş dönme + son bölümde sönme; ömrü bitince
/// kendini yok eder. Kendi Update'inde çalıştığı için kasa root'u destroy olsa bile leak olmaz.
/// </summary>
public sealed class SafeFragmentMotion : MonoBehaviour
{
    private RectTransform rt;
    private Graphic gfx;
    private Vector2 startPos;
    private Vector2 velocity;
    private float angularVel;
    private float lifetime;
    private float gravity;
    private float age;
    private Color baseColor;
    private Vector3 baseScale;

    public void Init(Vector2 velocity, float angularVel, float lifetime, float gravityPx)
    {
        rt = transform as RectTransform;
        gfx = GetComponent<Graphic>();
        this.velocity = velocity;
        this.angularVel = angularVel;
        this.lifetime = Mathf.Max(0.05f, lifetime);
        this.gravity = gravityPx;
        startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
        baseScale = rt != null ? rt.localScale : Vector3.one;
        baseColor = gfx != null ? gfx.color : Color.white;
    }

    private void Update()
    {
        if (rt == null) { Destroy(gameObject); return; }

        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / lifetime);

        // Yumuşak dışa savrulma (easeOutQuad → başta hızlı, sona doğru nazikçe yavaşlar) + yerçekimi.
        float eo = 1f - (1f - k) * (1f - k);
        Vector2 pos = startPos + velocity * eo;
        pos.y -= 0.5f * gravity * k * k;
        rt.anchoredPosition = pos;

        rt.localRotation *= Quaternion.Euler(0f, 0f, angularVel * Time.deltaTime);

        // Hafif beliriş + sona doğru hafif küçülme.
        float sc = k < 0.14f ? Mathf.Lerp(0.7f, 1f, k / 0.14f)
                             : Mathf.Lerp(1f, 0.82f, (k - 0.14f) / 0.86f);
        rt.localScale = baseScale * sc;

        // Son %40'ta yumuşak sön.
        if (gfx != null)
        {
            float a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            Color c = baseColor; c.a = Mathf.Clamp01(a); gfx.color = c;
        }

        if (k >= 1f) Destroy(gameObject);
    }
}
