using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toplanan taşın hayaleti, dört perde:
///   1) pop — taş yerinde parlayıp şişer (kırılma parçacıklarının üstünde okunur),
///   2) toplanma — aynı eşleşmenin taşları (GatherBatch) ortak noktada küçük, dönen bir halka olur,
///   3) fırlama — sırayla (order) hafif geri çekilip kavisle mühürdeki yuvaya uçar, renkli iz bırakır,
///   4) varış — renkli flaş + halka dalgası + kıvılcımlar; gameplay sayacı YALNIZ burada düşer.
/// </summary>
public sealed class AncientSealCollectFx : IDisposable
{
    private const float PopTime = 0.12f;
    private const float GatherTime = 0.24f;
    private const float LaunchStagger = 0.07f;
    private const float MaxStagger = 0.35f;
    private const float OrbitDegPerSec = 220f;
    private const float WindUpTime = 0.08f;

    private static Sprite ringSprite;

    private readonly AncientSealView target;
    private readonly int color;
    private readonly AncientSealService.GatherBatch batch;
    private readonly int order;
    private readonly float cellSize;
    private readonly Color tint;
    private readonly Vector2 start;
    private RectTransform root;
    private RectTransform stone;
    private Image stoneImage;
    private RectTransform halo;
    private Image haloImage;
    private FlightTrailFx trail;
    private readonly MonoBehaviour host;

    internal AncientSealCollectFx(MonoBehaviour host, RectTransform overlay, BoardController board,
        AncientSealView target, TileType type, int color, Vector3 worldStart,
        AncientSealService.GatherBatch batch, int order)
    {
        this.host = host;
        this.target = target;
        this.color = color;
        this.batch = batch;
        this.order = order;
        tint = color == 0 ? new Color(1f, 0.25f, 0.3f)
            : color == 1 ? new Color(1f, 0.82f, 0.2f) : new Color(0.3f, 1f, 0.45f);

        var go = new GameObject("SealStoneFlight", typeof(RectTransform));
        go.layer = overlay.gameObject.layer;
        root = (RectTransform)go.transform;
        root.SetParent(overlay, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = root.offsetMax = Vector2.zero;
        start = root.InverseTransformPoint(worldStart);
        cellSize = Mathf.Max(1f, root.InverseTransformVector(
            board.TilesRoot.TransformVector(Vector3.right * board.TileSize)).magnitude);

        // Hale taşın arkasında (önce oluşturulur), toplanma boyunca renkli parlar.
        halo = FlightTrailFx.CreateImage(root, "CollectedHalo", FlightTrailFx.GlowSprite(), FlightTrailFx.WithAlpha(tint, 0f), cellSize * 1.15f);
        haloImage = halo.GetComponent<Image>();
        halo.anchoredPosition = start;
        stone = FlightTrailFx.CreateImage(root, "CollectedStone", board.GetIcon(type), Color.white, cellSize * 0.7f);
        stoneImage = stone.GetComponent<Image>();
        stoneImage.preserveAspect = true;
        stone.anchoredPosition = start;
    }

    private bool HasTarget => root != null && stone != null && target != null && target.isActiveAndEnabled;

    public IEnumerator Play(Action onArrival)
    {
        // 1) Pop: aynı karede doğan bütün taşlar bu sırada gruba katılmış olur.
        for (float t = 0f; t < PopTime; t += Time.deltaTime)
        {
            if (!HasTarget) yield break;
            float k = Mathf.Clamp01(t / PopTime);
            stone.localScale = Vector3.one * (0.85f + 0.4f * Mathf.Sin(k * Mathf.PI * 0.75f));
            SetHalo(start, Mathf.Lerp(0.6f, 1.1f, k), 0.75f * k);
            yield return null;
        }

        // 2) Toplanma: ortak merkezin biraz üstünde, sıraya göre halkadaki yerine; beklerken halka döner.
        Vector2 center = (Vector2)root.InverseTransformPoint(batch.WorldCenter) + Vector2.up * (cellSize * 0.35f);
        int count = Mathf.Max(1, batch.count);
        float radius = count > 1 ? cellSize * Mathf.Lerp(0.22f, 0.42f, Mathf.Clamp01((count - 2) / 4f)) : 0f;
        float baseAngle = order * 360f / count;
        float wait = Mathf.Min(order * LaunchStagger, MaxStagger);
        float orbit = 0f;
        Vector2 gathered = start;
        for (float t = 0f; t < GatherTime + wait; t += Time.deltaTime)
        {
            if (!HasTarget) yield break;
            orbit += OrbitDegPerSec * Time.deltaTime;
            gathered = center + Polar(baseAngle + orbit, radius);
            float k = Mathf.Clamp01(t / GatherTime);
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            var pos = Vector2.LerpUnclamped(start, gathered, e);
            stone.anchoredPosition = pos;
            stone.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.9f, e);
            stone.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(orbit * Mathf.Deg2Rad * 2f) * 8f);
            SetHalo(pos, 1f, 0.75f - 0.25f * e + 0.1f * Mathf.Sin(orbit * Mathf.Deg2Rad * 3f));
            yield return null;
        }

        if (!HasTarget) yield break;
        Vector2 launch = stone.anchoredPosition;
        Vector2 initialEnd = root.InverseTransformPoint(target.GetCollectionTarget(color));
        Vector2 delta = initialEnd - launch;
        float distance = delta.magnitude;
        Vector2 forward = distance > 0.01f ? delta / distance : Vector2.up;
        Vector2 normal = new Vector2(-forward.y, forward.x);
        float bend = Mathf.Clamp(distance * 0.22f, cellSize * 0.6f, cellSize * 2f);
        float direction = delta.x >= 0f ? 1f : -1f;

        // 3a) Kısa geri çekilme (fırlama hissi), hale söner, iz başlar.
        Vector2 windUp = launch - forward * (cellSize * 0.18f);
        for (float t = 0f; t < WindUpTime; t += Time.deltaTime)
        {
            if (!HasTarget) yield break;
            float k = Mathf.Clamp01(t / WindUpTime);
            var pos = Vector2.Lerp(launch, windUp, Mathf.Sin(k * Mathf.PI * 0.5f));
            stone.anchoredPosition = pos;
            stone.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.05f, k);
            SetHalo(pos, 1f, 0.5f * (1f - k));
            yield return null;
        }
        if (halo != null) halo.gameObject.SetActive(false);
        trail = FlightTrailFx.Create(host, stone, tint, cellSize * 0.45f);

        // 3b) Kavisli uçuş: hızlanarak çıkar, yuvaya yaklaşırken yumuşar.
        Vector2 control = (windUp + initialEnd) * 0.5f + normal * (bend * direction);
        float duration = Mathf.Clamp(0.42f + distance / cellSize * 0.03f, 0.45f, 0.72f);
        float progress = 0f;
        while (progress < 1f)
        {
            if (!HasTarget) yield break;
            progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
            // Sıradaki renkli yuva hâlâ dönüyor olabilir: inişin hemen öncesinde bekle.
            if (!target.IsCollectionTargetReady(color)) progress = Mathf.Min(progress, 0.9f);
            float k = progress < 0.5f ? 2f * progress * progress : 1f - Mathf.Pow(-2f * progress + 2f, 2f) * 0.5f;
            Vector2 end = root.InverseTransformPoint(target.GetCollectionTarget(color));
            Vector2 position = (1f - k) * (1f - k) * windUp + 2f * (1f - k) * k * control + k * k * end;
            stone.anchoredPosition = position;
            stone.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.3f, k * k);
            stone.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI) * direction * 24f);
            trail?.Step(position, progress);
            if (progress < 1f) yield return null;
        }

        // 4) Varış — gameplay bu taşı yalnız burada tüketir.
        Vector2 impact = stone.anchoredPosition;
        stoneImage.enabled = false;
        trail?.Finish();
        trail = null;
        onArrival?.Invoke();

        var flash = FlightTrailFx.CreateImage(root, "SealArrivalGlow", FlightTrailFx.GlowSprite(), tint, cellSize);
        flash.anchoredPosition = impact;
        var flashImage = flash.GetComponent<Image>();
        RectTransform ring = null;
        Image ringImage = null;
        var ringArt = Ring();
        if (ringArt != null)
        {
            ring = FlightTrailFx.CreateImage(root, "SealArrivalRing", ringArt, tint, cellSize * 0.5f);
            ring.anchoredPosition = impact;
            ringImage = ring.GetComponent<Image>();
        }
        var sparks = new RectTransform[6];
        var sparkImages = new Image[sparks.Length];
        for (int i = 0; i < sparks.Length; i++)
        {
            sparks[i] = FlightTrailFx.CreateImage(root, "SealArrivalSpark", FlightTrailFx.GlowSprite(), Color.white, cellSize * 0.13f);
            sparkImages[i] = sparks[i].GetComponent<Image>();
        }
        const float impactTime = 0.26f;
        float spin = UnityEngine.Random.Range(0f, 60f);
        for (float t = 0f; t < impactTime; t += Time.deltaTime)
        {
            if (root == null || flash == null) yield break;
            float k = Mathf.Clamp01(t / impactTime);
            float e = 1f - (1f - k) * (1f - k);
            flash.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.05f, e);
            flashImage.color = FlightTrailFx.WithAlpha(tint, (1f - k) * 0.9f);
            if (ring != null)
            {
                ring.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.1f, e);
                ringImage.color = FlightTrailFx.WithAlpha(Color.Lerp(tint, Color.white, 0.4f), (1f - k) * 0.85f);
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = (spin + i * 360f / sparks.Length) * Mathf.Deg2Rad;
                sparks[i].anchoredPosition = impact + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (cellSize * 0.55f * e);
                sparkImages[i].color = new Color(1f, 1f, 0.8f, 1f - k);
            }
            yield return null;
        }
    }

    private void SetHalo(Vector2 pos, float scale, float alpha)
    {
        if (halo == null) return;
        halo.anchoredPosition = pos;
        halo.localScale = Vector3.one * scale;
        haloImage.color = FlightTrailFx.WithAlpha(tint, Mathf.Clamp01(alpha));
    }

    private static Vector2 Polar(float degrees, float radius)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
    }

    private static Sprite Ring()
    {
        if (ringSprite == null) ringSprite = Resources.Load<Sprite>("VFX/shockwave_ring");
        return ringSprite;
    }

    public void Dispose()
    {
        trail?.Finish();
        trail = null;
        if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        root = null;
    }
}
