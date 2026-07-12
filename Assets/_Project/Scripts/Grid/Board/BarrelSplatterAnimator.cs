using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barrel kırılınca 4x4 mud saçılma animasyonu:
///   • Footprint merkezinde bir yağ-sıçraması particle burst (opsiyonel, board FX hattı).
///   • Merkezden her hedef hücreye bir DAMLA uçar (üst VFX katmanında — obstacle/tile'ların
///     üstünde, gizlenmez), varışta o hücreye mud sıvanır (onLand) + küçük splat-pop.
/// Damlalar PatchBot dash'iyle aynı üst katmanda (BoardVfxPlayer.VfxRoot) çizilir.
/// BoardController GameObject'ine component olarak eklenir.
/// </summary>
public sealed class BarrelSplatterAnimator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoardController board;

    [Header("Damla sprite'ları")]
    [Tooltip("Uçan damla + burst sprite'ları (yağ damlaları). Boşsa splat/mud sprite'ına düşülür.")]
    [SerializeField] private List<Sprite> splashSprites = new();

    [Header("Uçan damlalar (fıskiye)")]
    [Tooltip("Bir damlanın merkezden hücreye uçuş süresi (sn).")]
    [SerializeField] private float dropletFlightDuration = 0.5f;
    [Tooltip("Damlaların başlangıçları arasına serpiştirilen maksimum rastgele gecikme (saçılma).")]
    [SerializeField] private float spreadWindow = 0.18f;
    [Tooltip("Damla görsel boyutu (tile oranı).")]
    [Range(0.15f, 1.5f)] [SerializeField] private float dropletSizeRatio = 0.65f;
    [Tooltip("Fıskiye yayının tepe yüksekliği (tile oranı). Yüksek = daha çok yukarı fışkırır.")]
    [SerializeField] private float arcHeightRatio = 1.4f;
    [Tooltip("Yay yüksekliğine eklenen rastgelelik (±oran). Damlalar farklı yükseklikte fışkırır.")]
    [Range(0f, 0.6f)] [SerializeField] private float arcHeightJitter = 0.35f;

    [Header("Varış (splat)")]
    [Tooltip("Varışta oluşan splat-pop sprite'ı. Boş ise damla/mud sprite'ı.")]
    [SerializeField] private Sprite splatSprite;
    [Tooltip("Splat-pop tepe boyutu (tile oranı). 0 = kapalı.")]
    [Range(0f, 1.5f)] [SerializeField] private float splatSizeRatio = 0.75f;

    [Header("Oil Splash (Particle — opsiyonel bonus)")]
    [Tooltip("Merkezde ekstra particle burst prefab'ı. Boş ise ObstacleBreakFxPrefab kullanılır.")]
    [SerializeField] private GameObject splashFxPrefab;
    [SerializeField] private float splashLifetime = 1.0f;
    [Tooltip("Açıksa merkez particle burst'ü de oynatılır (uçan damlalara ek). KAPALI önerilir: " +
             "generic break prefab 4 sprite'ı texture-sheet'e alamayıp tek yuvarlağa düşüyor.")]
    [SerializeField] private bool playCenterParticle = false;

    private RectTransform FlightRoot =>
        (board != null && board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null)
            ? board.BoardVfxPlayer.VfxRoot
            : (board != null ? board.Parent : null);

    // Round-robin: her damla farklı sprite alsın (hep aynı yuvarlağı kullanmasın).
    private Sprite PickDropletSprite(int index)
    {
        if (splashSprites != null && splashSprites.Count > 0)
        {
            int n = splashSprites.Count;
            for (int j = 0; j < n; j++)
            {
                var s = splashSprites[(index + j) % n];
                if (s != null) return s;
            }
        }
        if (splatSprite != null) return splatSprite;
        var mud = FindFirstObjectByType<MudOverlayService>();
        return mud != null ? mud.BorderedMudSprite : null;
    }

    /// <summary>
    /// Barrel footprint merkezinden hedeflere damlalar saçar; her damla varışında onLand(hedef)
    /// çağrılır (mud stamp). Tüm damlalar inince coroutine döner.
    /// </summary>
    public IEnumerator PlaySplatter(Vector2Int origin, Vector2Int size, IReadOnlyList<Vector2Int> targets, Action<Vector2Int> onLand)
    {
        var root = FlightRoot;
        if (board == null || root == null || targets == null || targets.Count == 0)
        {
            InvokeAll(targets, onLand);
            yield break;
        }

        Vector3 centerWorld = FootprintWorldCenter(origin, size);

        // Bonus: merkezde particle burst.
        if (playCenterParticle)
        {
            var prefab = splashFxPrefab != null ? splashFxPrefab : board.ObstacleBreakFxPrefab;
            if (prefab != null)
            {
                var sprites = (splashSprites != null && splashSprites.Count > 0) ? splashSprites : null;
                board.BreakFx?.PlaySplashFx(prefab, splashLifetime, centerWorld, sprites);
            }
        }

        Vector2 center = WorldToLocal(centerWorld, root);
        // Üst katmandaki gerçek tile boyutu (VfxRoot ölçeği farklı olabilir).
        float ts = Vector2.Distance(
            CellAnchored(origin, root),
            CellAnchored(origin + Vector2Int.right, root));
        if (ts < 1f) ts = Mathf.Max(1f, board.TileSize);

        int startOffset = UnityEngine.Random.Range(0, Mathf.Max(1, splashSprites != null ? splashSprites.Count : 1));
        int remaining = targets.Count;
        for (int i = 0; i < targets.Count; i++)
        {
            float delay = spreadWindow <= 0f ? 0f : UnityEngine.Random.Range(0f, spreadWindow);
            var sprite = PickDropletSprite(startOffset + i);
            StartCoroutine(FlyDroplet(root, center, targets[i], ts, sprite, onLand, delay, () => remaining--));
        }

        while (remaining > 0)
            yield return null;
    }

    private IEnumerator FlyDroplet(
        RectTransform root,
        Vector2 center,
        Vector2Int cell,
        float ts,
        Sprite sprite,
        Action<Vector2Int> onLand,
        float delay,
        Action onDone)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector2 tgt = CellAnchored(cell, root);

        var go = new GameObject("MudDroplet", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float size = ts * dropletSizeRatio;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = center;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = sprite != null;

        // Fıskiye: yüksek + rastgele yay; damla yörünge yönüne (tanjant) döner.
        float jitter = 1f + UnityEngine.Random.Range(-arcHeightJitter, arcHeightJitter);
        float arc = ts * arcHeightRatio * jitter;
        float dur = Mathf.Max(0.05f, dropletFlightDuration * UnityEngine.Random.Range(0.85f, 1.2f));

        Vector2 Path(float k) => Vector2.Lerp(center, tgt, k) + Vector2.up * (arc * Mathf.Sin(Mathf.PI * k));

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);

            Vector2 pos = Path(k);
            rt.anchoredPosition = pos;

            Vector2 dir = Path(Mathf.Min(1f, k + 0.02f)) - pos;
            if (dir.sqrMagnitude > 0.0001f)
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

            float sc = Mathf.Lerp(1.2f, 0.7f, k);
            rt.localScale = new Vector3(sc, sc, 1f);
            yield return null;
        }

        Destroy(go);

        onLand?.Invoke(cell);   // mud belirir

        if (splatSizeRatio > 0f && sprite != null)
            StartCoroutine(SplatPop(root, sprite, ts, tgt));

        onDone?.Invoke();
    }

    private IEnumerator SplatPop(RectTransform root, Sprite sprite, float ts, Vector2 pos)
    {
        var go = new GameObject("MudSplat", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        float peak = ts * splatSizeRatio;
        rt.sizeDelta = new Vector2(peak, peak);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        float dur = 0.2f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float sc = Mathf.SmoothStep(0.5f, 1.2f, k);
            rt.localScale = new Vector3(sc, sc, 1f);
            var c = img.color; c.a = 1f - k; img.color = c;
            yield return null;
        }

        Destroy(go);
    }

    private Vector3 FootprintWorldCenter(Vector2Int origin, Vector2Int size)
    {
        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);
        Vector3 c0 = board.GetCellWorldCenterPosition(origin.x, origin.y);
        Vector3 c1 = board.GetCellWorldCenterPosition(origin.x + w - 1, origin.y + h - 1);
        return (c0 + c1) * 0.5f;
    }

    private Vector2 CellAnchored(Vector2Int cell, RectTransform space)
        => WorldToLocal(board.GetCellWorldCenterPosition(cell.x, cell.y), space);

    private static Vector2 WorldToLocal(Vector3 worldPos, RectTransform space)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            space,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint);
        return localPoint;
    }

    private static void InvokeAll(IReadOnlyList<Vector2Int> targets, Action<Vector2Int> onLand)
    {
        if (targets == null || onLand == null) return;
        for (int i = 0; i < targets.Count; i++)
            onLand(targets[i]);
    }
}
