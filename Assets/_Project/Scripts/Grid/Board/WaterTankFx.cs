using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Su deposu kırılma görselleri (WaterTank / WaterTankSmall):
///   • PlayCrackFrames — ara vuruşta config.crackFrames'i (Bidon → Bidon2 → Bidon3) hızla oynatır,
///     sonunda stage sprite'ında kalır.
///   • SpawnShards — deponun gövdesinden kesilmiş plastik parçalar dönerek sıçrar, yerçekimiyle
///     düşüp söner. Parçalar ayrı art istemez: kaynak sprite'ın texture'ından alt-bölge sprite'ı
///     (Sprite.Create texture'ı okunabilir olmayı gerektirmez); texture başına bir kez üretilir.
/// Coroutine'ler çağıranın (GridSpawner / WaterTankSplashAnimator) üstünde koşar.
/// </summary>
public static class WaterTankFx
{
    private const int ShardVariants = 6;
    private static readonly Dictionary<Texture2D, Sprite[]> ShardCache = new();

    public static IEnumerator PlayCrackFrames(Image image, WaterTankConfig config, Sprite finalSprite)
    {
        var frames = config != null ? config.crackFrames : null;
        float frameDuration = config != null ? Mathf.Max(0.01f, config.crackFrameDuration) : 0.06f;

        if (frames != null)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                if (image == null) yield break;
                if (frames[i] == null) continue;
                image.sprite = frames[i];
                float t = 0f;
                while (t < frameDuration)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }
        }

        if (image != null && finalSprite != null)
            image.sprite = finalSprite;
    }

    /// <param name="footprint">Deponun genişliği, root uzayında (piksel). Parça boyu/menzili buna göre.</param>
    public static void SpawnShards(MonoBehaviour host, RectTransform root, Vector2 center, float footprint,
        Sprite source, int count)
    {
        if (host == null || root == null || source == null || count <= 0 || footprint <= 0f)
            return;

        var shards = GetShardSprites(source);
        if (shards == null || shards.Length == 0)
            return;

        int offset = Random.Range(0, shards.Length);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-25f, 25f);
            host.StartCoroutine(FlyShard(root, center, shards[(offset + i) % shards.Length],
                footprint * Random.Range(0.16f, 0.26f), angle,
                footprint * Random.Range(0.45f, 0.95f),
                footprint * Random.Range(0.35f, 0.7f),
                Random.Range(0.42f, 0.58f)));
        }
    }

    private static Sprite[] GetShardSprites(Sprite source)
    {
        var tex = source.texture;
        if (tex == null)
            return null;
        if (ShardCache.TryGetValue(tex, out var cached) && cached != null)
            return cached;

        // Gövdenin ortasından (opak kırmızı bölge; kapak/kulp kenarları hariç) kare parçalar kes.
        Rect r = source.textureRect;
        float side = Mathf.Min(r.width, r.height) * 0.2f;
        var result = new Sprite[ShardVariants];
        for (int i = 0; i < ShardVariants; i++)
        {
            float x = r.x + Random.Range(r.width * 0.18f, r.width * 0.82f - side);
            float y = r.y + Random.Range(r.height * 0.12f, r.height * 0.62f - side);
            result[i] = Sprite.Create(tex, new Rect(x, y, side, side), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            result[i].name = $"{tex.name}_Shard{i}";
        }

        ShardCache[tex] = result;
        return result;
    }

    private static IEnumerator FlyShard(RectTransform root, Vector2 center, Sprite sprite, float size,
        float angleDeg, float distance, float height, float duration)
    {
        var go = new GameObject("WaterTankShard", typeof(RectTransform), typeof(Image));
        go.layer = root.gameObject.layer;
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size * Random.Range(0.6f, 1f));
        rt.anchoredPosition = center;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;

        Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
        float spin = Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f);
        float startRot = Random.Range(0f, 360f);

        float t = 0f;
        while (t < duration)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // Yatayda sabit hızla açılır; dikeyde sıçrar ve yerçekimiyle düşer (sonunda başlangıcın altında).
            float lift = height * 4f * k * (0.8f - k);
            rt.anchoredPosition = center + dir * (distance * k) + Vector2.up * lift;
            rt.localRotation = Quaternion.Euler(0f, 0f, startRot + spin * k);
            float sc = Mathf.Lerp(1f, 0.6f, k);
            rt.localScale = new Vector3(sc, sc, 1f);
            var c = img.color; c.a = 1f - Mathf.Clamp01((k - 0.55f) / 0.45f); img.color = c;
            yield return null;
        }

        if (go != null)
            Object.Destroy(go);
    }

    /// <summary>World-uzayı dikdörtgen (RectTransform) → root local merkez + genişlik.</summary>
    public static bool TryGetLocalCenterAndWidth(RectTransform target, RectTransform root, out Vector2 center, out float width)
    {
        center = default;
        width = 0f;
        if (target == null || root == null)
            return false;

        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 a = WorldToLocal(corners[0], root);   // sol-alt
        Vector2 b = WorldToLocal(corners[2], root);   // sağ-üst
        center = (a + b) * 0.5f;
        width = Mathf.Abs(b.x - a.x);
        return true;
    }

    public static Vector2 WorldToLocal(Vector3 worldPos, RectTransform space)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            space,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint);
        return localPoint;
    }
}
