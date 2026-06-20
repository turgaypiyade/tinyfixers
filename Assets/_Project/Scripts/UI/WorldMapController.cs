using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dünya haritasının beyni. Sıralı bölge listesini tutar, açılma durumlarını yükler,
/// yıldız HARCAYARAK bölge açar (sis kalkar + kutlama VFX/SFX).
///
/// Görev listesi (RegionUnlockListPanel) buradaki sıralı listeyi okur ve TryUnlock çağırır.
/// </summary>
public sealed class WorldMapController : MonoBehaviour
{
    [Tooltip("Açılış SIRASIYLA tüm bölgeler. Liste, kilitli bölgeleri bu sırayla gösterir.")]
    [SerializeField] private WorldMapRegion[] regions;

    [Header("Kutlama (açılınca, bölgenin RevealFocus'unda)")]
    [Tooltip("Opsiyonel — KENDİ animasyonu olan UI burst prefab'ı. Boş bırakabilirsin; aşağıdaki procedural parlama yeter.")]
    [SerializeField] private GameObject sparkleBurstPrefab;
    [SerializeField] private GameObject confettiPrefab;
    [SerializeField, Min(0f)] private float vfxLifetime = 2f;
    [Tooltip("Sparkle/VFX'in spawn olacağı katman. Sis (fog) DEĞİL — fog sönüp kapanınca yıldızlar da kaybolur. " +
             "Boşsa Canvas kökü kullanılır. En iyisi her şeyin ÜSTünde bir tam-ekran RectTransform ata.")]
    [SerializeField] private RectTransform vfxOverlay;

    [Header("Parlama Yıldızları (procedural — sadece bir sprite yeter)")]
    [Tooltip("Saçılacak yıldız/parlama sprite'ı (şeffaf PNG). Atanırsa açılışta bölgeye yıldızlar saçılır.")]
    [SerializeField] private Sprite sparkleStarSprite;
    [SerializeField, Range(0, 100)] private int sparkleCount = 20;
    [Tooltip("Yıldızların focus etrafında yayılma yarıçapı (piksel).")]
    [SerializeField, Min(10f)] private float sparkleSpread = 300f;
    [SerializeField, Min(10f)] private float sparkleSize = 60f;
    [SerializeField, Min(0.1f)] private float sparkleDuration = 0.9f;
    [SerializeField, Min(0f)] private float sparkleStagger = 0.03f;
    [SerializeField] private Color[] sparkleColors =
    {
        new Color(1.00f, 0.92f, 0.30f), new Color(0.30f, 0.65f, 1.00f),
        new Color(0.40f, 0.85f, 0.40f), new Color(1.00f, 0.45f, 0.55f),
    };
    [Tooltip("Yıldızlar hedefe varsın diye sis kalkmadan önceki bekleme (sinematik senkron).")]
    [SerializeField, Min(0f)] private float starImpactDelay = 0.45f;
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("Bulut açılış sesi (whoosh). Sis kalkarken çalar.")]
    [SerializeField] private AudioClip unlockSfx;
    [Tooltip("Yıldız/parlama sesi (çın/sparkle). Opsiyonel — yıldızlar saçılırken çalar. " +
             "İkisini birlikte koyabilirsin (whoosh ana, sparkle ikincil) ya da birini boş bırak.")]
    [SerializeField] private AudioClip sparkleSfx;

    [Header("Sinematik (tam ekran açılış)")]
    [Tooltip("Harita ScrollRect'i. Atanırsa açılırken bölge ekran ortasına KAYDIRILIR.")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private bool panToRegion = true;
    [SerializeField, Min(0.05f)] private float panDuration = 0.6f;
    [Tooltip("Açılırken haritanın zoom oranı (1 = zoom yok, 1.4 = %40 yaklaş). Bölge merkezde kalır.")]
    [SerializeField, Min(1f)] private float zoomScale = 1.4f;
    [Tooltip("Sis kalktıktan sonra bölgeyi izletme süresi.")]
    [SerializeField, Min(0f)] private float holdAfterReveal = 0.7f;
    [Tooltip("Normal gezinme/odak zoom oranı (açılışta ve açma sonrası). 1 = zoom yok.")]
    [SerializeField, Min(0.1f)] private float browseScale = 1f;
    [Tooltip("Açma bitince boş ekrana DÖNME; sıradaki kilitli bölgeye geç.")]
    [SerializeField] private bool goToNextAfterUnlock = true;
    [Tooltip("Sahne/harita açılınca otomatik sıradaki kilitli bölgeye konumlan (boş ekranda başlama).")]
    [SerializeField] private bool focusOnEnable = true;

    /// <summary>Bir bölge açıldığında (sis kalkma bitince) tetiklenir.</summary>
    public event Action<WorldMapRegion> OnRegionUnlocked;

    /// <summary>Şu an bir sis kalkma animasyonu sürüyor mu? (panel buna göre bekler)</summary>
    public bool IsRevealing { get; private set; }

    public IReadOnlyList<WorldMapRegion> Regions => regions;

    private void OnEnable()
    {
        ApplyAllInstant();
        if (focusOnEnable) StartCoroutine(FocusNextEndOfFrame());
    }

    private IEnumerator FocusNextEndOfFrame()
    {
        yield return null;   // layout (rect boyutları) otursun
        FocusNextLocked(instant: true);
    }

    /// <summary>Sıradaki kilitli bölgeye kamerayı odakla (görev butonu / açılış için).</summary>
    public void FocusNextLocked(bool instant)
    {
        var locked = GetLockedRegions(1);
        if (locked.Count > 0) FocusOnRegion(locked[0], instant);
    }

    /// <summary>Kamerayı bir bölgeye getirir (browseScale zoom'uyla). instant = anında snap.</summary>
    public void FocusOnRegion(WorldMapRegion region, bool instant)
    {
        if (region == null || scrollRect == null || scrollRect.content == null) return;
        if (instant)
        {
            scrollRect.content.localScale = Vector3.one * browseScale;
            Canvas.ForceUpdateCanvases();   // rect/konum güncel olsun, doğru ortalansın
            scrollRect.content.anchoredPosition = CenteredAnchoredPos(region.RevealFocus);
            scrollRect.velocity = Vector2.zero;
        }
        else
        {
            StartCoroutine(PanZoomTo(region.RevealFocus, browseScale, panDuration));
        }
    }

    private void ApplyAllInstant()
    {
        if (regions == null) return;
        for (int i = 0; i < regions.Length; i++)
            if (regions[i] != null) regions[i].ApplyInstant();
    }

    /// <summary>Sırayla ilk N kilitli bölgeyi döndürür (görev listesi için).</summary>
    public List<WorldMapRegion> GetLockedRegions(int max = int.MaxValue)
    {
        var list = new List<WorldMapRegion>();
        if (regions == null) return list;
        for (int i = 0; i < regions.Length && list.Count < max; i++)
        {
            var r = regions[i];
            if (r != null && !r.IsUnlocked) list.Add(r);
        }
        return list;
    }

    public bool AllUnlocked => GetLockedRegions(1).Count == 0;

    /// <summary>Toplam (null olmayan) bölge sayısı.</summary>
    public int TotalRegions
    {
        get { int c = 0; if (regions != null) foreach (var r in regions) if (r != null) c++; return c; }
    }

    /// <summary>Açılmış bölge sayısı.</summary>
    public int UnlockedCount
    {
        get { int c = 0; if (regions != null) foreach (var r in regions) if (r != null && r.IsUnlocked) c++; return c; }
    }

    /// <summary>İlerleme (0-1) = açılmış / toplam.</summary>
    public float ProgressNormalized
    {
        get { int t = TotalRegions; return t > 0 ? (float)UnlockedCount / t : 0f; }
    }

    public bool CanUnlock(WorldMapRegion region) =>
        region != null && !region.IsUnlocked && !IsRevealing &&
        PlayerWallet.HasEnoughStars(region.StarCost);

    /// <summary>
    /// Bölgeyi açmayı dener: yıldız yeterliyse harcar, sis kalkma sekansını başlatır.
    /// Yıldız uçma sinematiğini panel yürütür; bu yalnızca harca + reveal yapar.
    /// </summary>
    public bool TryUnlock(WorldMapRegion region)
    {
        if (region == null || region.IsUnlocked) return false;
        if (IsRevealing) return false;
        if (!PlayerWallet.HasEnoughStars(region.StarCost)) return false;
        if (!PlayerWallet.SpendStars(region.StarCost)) return false;

        StartCoroutine(UnlockSequence(region));
        return true;
    }

    private IEnumerator UnlockSequence(WorldMapRegion region)
    {
        IsRevealing = true;

        var focus = region.RevealFocus;     // ekran ortalama (kadraj)
        var starAt = region.StarPoint;       // yıldız/VFX = PIN konumu
        bool canPan = panToRegion && scrollRect != null && scrollRect.content != null;

        // 1. Haritayı bölgeye kaydır + zoom (tam ekran sinematik).
        if (canPan) yield return PanZoomTo(focus, zoomScale, panDuration);

        // 2. Yıldızlar hedefe varsın diye kısa bekleme (panel paralel uçuruyor).
        if (starImpactDelay > 0f)
            yield return new WaitForSeconds(starImpactDelay);

        // 3. Kutlama + sis kalkışı. VFX/yıldız PIN'de, ortalama focus'ta.
        SpawnVfx(sparkleBurstPrefab, starAt);
        SpawnVfx(confettiPrefab, starAt);
        if (sparkleStarSprite != null) StartCoroutine(SpawnSparkleStars(starAt));
        if (GameSettings.SoundEnabled && unlockSfx != null && sfxSource != null)
            sfxSource.PlayOneShot(unlockSfx);

        yield return region.RevealRoutine();

        // 4. Bir an izlet.
        if (holdAfterReveal > 0f) yield return new WaitForSeconds(holdAfterReveal);

        // 5. Boş ekrana DÖNME: sıradaki kilitli bölgeye (yoksa mevcut bölgeye) normal zoom'la geç.
        if (canPan)
        {
            var next = GetLockedRegions(1);   // bu bölge artık açık → listeye girmez
            var target = (goToNextAfterUnlock && next.Count > 0) ? next[0].RevealFocus : focus;
            yield return PanZoomTo(target, browseScale, panDuration);
        }

        IsRevealing = false;
        OnRegionUnlocked?.Invoke(region);
    }

    /// <summary>Content'i focus ekran ortasına gelecek + targetScale'e zoom yapacak şekilde animasyonla taşır.</summary>
    private IEnumerator PanZoomTo(RectTransform focus, float targetScale, float dur)
    {
        var content = scrollRect.content;
        scrollRect.velocity = Vector2.zero;

        // Hedef konumu BİTİŞ scale'inde hesapla (focus'u o scale'de merkeze alır).
        Canvas.ForceUpdateCanvases();
        Vector3 savedScale = content.localScale;
        content.localScale = Vector3.one * targetScale;
        Canvas.ForceUpdateCanvases();
        Vector2 endPos = CenteredAnchoredPos(focus);
        content.localScale = savedScale;

        yield return PanZoomToValues(Vector3.one * targetScale, endPos, dur);
    }

    private IEnumerator PanZoomToValues(Vector3 targetScale, Vector2 targetPos, float dur)
    {
        var content = scrollRect.content;
        Vector3 startScale = content.localScale;
        Vector2 startPos = content.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            content.localScale = Vector3.Lerp(startScale, targetScale, k);
            content.anchoredPosition = Vector2.Lerp(startPos, targetPos, k);
            yield return null;
        }
        content.localScale = targetScale;
        content.anchoredPosition = targetPos;
        scrollRect.velocity = Vector2.zero;
    }

    /// <summary>Mevcut scale'de, focus'u viewport ortasına getirecek anchoredPosition.</summary>
    private Vector2 CenteredAnchoredPos(RectTransform focus)
    {
        var content = scrollRect.content;
        var viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        Vector3 vpCenterWorld = viewport.TransformPoint(viewport.rect.center);
        Vector3 deltaWorld = vpCenterWorld - focus.position;
        Vector3 localDelta = content.parent.InverseTransformVector(deltaWorld);
        return content.anchoredPosition + (Vector2)localDelta;
    }

    // ─── Procedural Parlama Yıldızları (WorkshopController'dan uyarlandı) ──────

    private IEnumerator SpawnSparkleStars(RectTransform focus)
    {
        var overlay = Overlay();
        if (overlay == null) yield break;

        if (GameSettings.SoundEnabled && sparkleSfx != null && sfxSource != null)
            sfxSource.PlayOneShot(sparkleSfx);

        Vector3 focusWorld = focus.position;
        for (int i = 0; i < sparkleCount; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * sparkleSpread;
            StartCoroutine(AnimateOneSparkle(overlay, focusWorld, offset));
            if (sparkleStagger > 0f) yield return new WaitForSeconds(sparkleStagger);
        }
    }

    private IEnumerator AnimateOneSparkle(RectTransform parent, Vector3 focusWorld, Vector2 offset)
    {
        var go = new GameObject("Sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = focusWorld;               // bölgenin ekran konumu
        rt.anchoredPosition += offset;          // etrafına saç
        rt.sizeDelta = Vector2.one * sparkleSize * UnityEngine.Random.Range(0.7f, 1.3f);
        rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

        var img = go.GetComponent<Image>();
        img.sprite = sparkleStarSprite;
        img.raycastTarget = false;
        Color col = (sparkleColors != null && sparkleColors.Length > 0)
            ? sparkleColors[UnityEngine.Random.Range(0, sparkleColors.Length)] : Color.white;

        float half = sparkleDuration * 0.5f;
        float t = 0f;
        while (t < half)   // fade in + büyü
        {
            t += Time.unscaledDeltaTime;
            float ek = Mathf.SmoothStep(0f, 1f, t / half);
            col.a = ek; img.color = col;
            rt.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.3f, ek);
            rt.Rotate(0f, 0f, 120f * Time.unscaledDeltaTime);
            yield return null;
        }
        t = 0f;
        while (t < half)   // fade out + küçül
        {
            t += Time.unscaledDeltaTime;
            float ek = Mathf.SmoothStep(0f, 1f, t / half);
            col.a = 1f - ek; img.color = col;
            rt.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.4f, ek);
            rt.Rotate(0f, 0f, 120f * Time.unscaledDeltaTime);
            yield return null;
        }
        Destroy(go);
    }

    private void SpawnVfx(GameObject prefab, RectTransform at)
    {
        var overlay = Overlay();
        if (prefab == null || overlay == null || at == null) return;
        var go = Instantiate(prefab, overlay);
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.position = at.position;   // bölgenin ekran konumu (fog değil, overlay altında)
        }
        if (vfxLifetime > 0f) Destroy(go, vfxLifetime);
    }

    private RectTransform cachedOverlay;
    private RectTransform Overlay()
    {
        if (vfxOverlay != null) return vfxOverlay;
        if (cachedOverlay == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) cachedOverlay = canvas.transform as RectTransform;
        }
        return cachedOverlay;
    }
}
