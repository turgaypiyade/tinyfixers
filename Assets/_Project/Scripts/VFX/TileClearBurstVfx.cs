using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime'da UI tabanlı match clear burst efekti yaratır.
/// 3 katman: Halka (glow ring), Yıldızlar (halka içinde), Altın shardlar (radyal patlama).
///
/// Sprite/prefab gerektirmez — procedural mesh/color kullanır.
/// İstersen Inspector'dan sprite geçirebilirsin (ileride).
///
/// Kullanım:
///   yield return TileClearBurstVfx.CoPlayBurst(tile, duration: 0.30f);
/// </summary>
public static class TileClearBurstVfx
{
    // ===== AYARLANABİLİR PARAMETRELER =====
    private const float RING_SIZE_START = 0.3f;     // Halka başlangıç scale (hücre boyutunun oranı)
    private const float RING_SIZE_PEAK = 1.25f;     // Halka tepe scale
    private const float RING_SIZE_END = 1.55f;      // Halka bitiş scale (yayılıp solar)
    private const int RING_STAR_COUNT = 5;          // Halka içinde kaç yıldız
    private const int SHARD_COUNT = 7;              // Radyal saçılan altın shard sayısı
    private const float SHARD_DISTANCE = 1.2f;      // Shard uçuş mesafesi (hücre boyutunun oranı)
    private const float SHARD_GRAVITY = 1.8f;       // Shard yerçekimi (hücre/s² oranı)

    private static readonly Color RING_COLOR = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color STAR_COLOR = new Color(1f, 1f, 0.9f, 1f);
    private static readonly Color SHARD_COLOR_A = new Color(1f, 0.82f, 0.18f, 1f); // altın
    private static readonly Color SHARD_COLOR_B = new Color(1f, 0.65f, 0.05f, 1f); // koyu altın

    // Debug: Koordinat sorununu araştırırken true yap, sonra false'a çevir
    public static bool DebugPositionLogging = false;

    /// <summary>
    /// Taşın bulunduğu yerde burst efektini oynatır.
    /// Taş scale'ine dokunmaz — çağıran tarafta PlayPop yapılır.
    /// </summary>
    public static IEnumerator CoPlayBurst(TileView tile, BoardController board, float duration)
    {
        if (tile == null) yield break;

        // ÖNEMLİ: tile.RectTransform grid pozisyonunda ama sprite ICON'da render oluyor.
        // Bazı tile yapılarında iconRt tile root'undan offset'li olabilir.
        // Sprite'ın GERÇEK görünen merkezi iconRt'nin merkezidir.
        RectTransform targetRt = null;
        if (tile.IconImage != null && tile.IconImage.rectTransform != null)
            targetRt = tile.IconImage.rectTransform;
        else
            targetRt = tile.RectTransform;

        if (targetRt == null) yield break;

        // Taşın world-space merkezini al → pivot/scale/rotation'dan bağımsız
        Vector3[] corners = new Vector3[4];
        targetRt.GetWorldCorners(corners);
        Vector3 tileWorldCenter = (corners[0] + corners[2]) * 0.5f;

        // Parent seçimi: board.Parent STABİL ve bilinen bir konteyner
        Transform effectParent = (board != null && board.Parent != null)
            ? board.Parent
            : targetRt.parent;

        yield return CoPlayBurstAtWorldPosition(tileWorldCenter, effectParent, board, duration);
    }

    /// <summary>
    /// Belirli bir world pozisyonunda burst efektini oynatır.
    /// Special creation gibi taşın yok olmadığı durumlarda kullanılır.
    /// </summary>
    public static IEnumerator CoPlayBurstAtWorldPosition(Vector3 worldPosition, Transform parent, BoardController board, float duration)
    {
        if (parent == null) yield break;

        int tileSize = board != null ? board.TileSize : 100;

        // Ana efekt root'u
        GameObject rootGo = new GameObject("ClearBurst", typeof(RectTransform));
        RectTransform rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.SetParent(parent, false);
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(tileSize, tileSize);
        rootRt.localScale = Vector3.one;
        rootRt.localRotation = Quaternion.identity;
        rootRt.SetAsLastSibling();

        // KRİTİK: anchoredPosition yerine DOĞRUDAN world position ata
        // Bu, parent'ın anchor/pivot ayarından bağımsız kesin sonuç verir
        RectTransform parentRt = parent as RectTransform;

        if (parentRt != null)
        {
            if (board != null)
                rootRt.anchoredPosition = board.WorldToAnchoredIn(parentRt, worldPosition);
            else
                rootRt.localPosition = parentRt.InverseTransformPoint(worldPosition);
        }
        else
        {
            rootRt.position = worldPosition;
        }

        if (DebugPositionLogging)
        {
            Debug.Log($"[Burst] parent={parent.name} target_world={worldPosition:F2} actual_world={rootRt.position:F2} anchored={rootRt.anchoredPosition:F2}");
        }

        // === 1) Halka (glow ring) ===
        var ring = CreateRing(rootRt, tileSize);

        // === 2) Halka içi yıldızlar ===
        var stars = new List<StarInstance>(RING_STAR_COUNT);
        for (int i = 0; i < RING_STAR_COUNT; i++)
        {
            float angle = (360f / RING_STAR_COUNT) * i + Random.Range(-15f, 15f);
            float dist = tileSize * Random.Range(0.18f, 0.35f);
            stars.Add(CreateStar(rootRt, tileSize, angle, dist));
        }

        // === 3) Altın shardlar (radyal patlama) ===
        var shards = new List<ShardInstance>(SHARD_COUNT);
        for (int i = 0; i < SHARD_COUNT; i++)
        {
            float angle = (360f / SHARD_COUNT) * i + Random.Range(-20f, 20f);
            float speed = tileSize * SHARD_DISTANCE * Random.Range(0.8f, 1.2f);
            shards.Add(CreateShard(rootRt, tileSize, angle, speed));
        }

        // === ANİMASYON ===
        float t = 0f;
        while (t < duration)
        {
            if (rootGo == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            AnimateRing(ring, k, tileSize);
            AnimateStars(stars, k);
            AnimateShards(shards, k, tileSize);

            yield return null;
        }

        if (rootGo != null)
            Object.Destroy(rootGo);
    }

    // ========== RING ==========
    private class RingInstance
    {
        public RectTransform rt;
        public Image image;
        public Image outline;
    }

    private static RingInstance CreateRing(RectTransform parent, int tileSize)
    {
        var go = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        float s = tileSize * RING_SIZE_START;
        rt.sizeDelta = new Vector2(s, s);

        var img = go.GetComponent<Image>();
        img.color = new Color(RING_COLOR.r, RING_COLOR.g, RING_COLOR.b, 0f);
        img.raycastTarget = false;
        img.sprite = GetSoftCircleSprite();
        img.type = Image.Type.Simple;

        return new RingInstance { rt = rt, image = img };
    }

    private static void AnimateRing(RingInstance ring, float k, int tileSize)
    {
        if (ring == null || ring.rt == null || ring.image == null) return;

        // Scale: hızlı büyü (0→peak @ k=0.4), sonra yavaş yayıl (peak→end @ k=0.4→1.0)
        float scale;
        if (k < 0.4f)
        {
            float kk = k / 0.4f;
            float eased = 1f - (1f - kk) * (1f - kk); // easeOutQuad
            scale = Mathf.Lerp(RING_SIZE_START, RING_SIZE_PEAK, eased);
        }
        else
        {
            float kk = (k - 0.4f) / 0.6f;
            scale = Mathf.Lerp(RING_SIZE_PEAK, RING_SIZE_END, kk);
        }

        float pixelSize = tileSize * scale;
        ring.rt.sizeDelta = new Vector2(pixelSize, pixelSize);

        // Alpha: hızlı yüksel, sonra solarak kaybol
        float alpha;
        if (k < 0.25f)
            alpha = (k / 0.25f) * RING_COLOR.a;
        else
            alpha = RING_COLOR.a * (1f - (k - 0.25f) / 0.75f);

        Color c = ring.image.color;
        c.a = alpha;
        ring.image.color = c;
    }

    // ========== STARS ==========
    private class StarInstance
    {
        public RectTransform rt;
        public Image image;
        public Vector2 basePos;
        public float baseAngle;
        public float floatRadius;
        public float rotationSpeed;
    }

    private static StarInstance CreateStar(RectTransform parent, int tileSize, float angle, float distance)
    {
        var go = new GameObject("Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        float rad = angle * Mathf.Deg2Rad;
        Vector2 pos = new Vector2(Mathf.Cos(rad) * distance, Mathf.Sin(rad) * distance);
        rt.anchoredPosition = pos;

        float starSize = tileSize * Random.Range(0.14f, 0.22f);
        rt.sizeDelta = new Vector2(starSize, starSize);
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        var img = go.GetComponent<Image>();
        img.color = new Color(STAR_COLOR.r, STAR_COLOR.g, STAR_COLOR.b, 0f);
        img.raycastTarget = false;
        img.sprite = GetStarSprite();
        img.type = Image.Type.Simple;

        return new StarInstance
        {
            rt = rt,
            image = img,
            basePos = pos,
            baseAngle = angle,
            floatRadius = tileSize * 0.04f,
            rotationSpeed = Random.Range(180f, 360f) * (Random.value < 0.5f ? 1f : -1f)
        };
    }

    private static void AnimateStars(List<StarInstance> stars, float k)
    {
        foreach (var s in stars)
        {
            if (s == null || s.rt == null || s.image == null) continue;

            // Scale: 0 → 1.1 → 0
            float scale;
            if (k < 0.3f)
                scale = (k / 0.3f) * 1.1f;
            else
                scale = 1.1f * (1f - (k - 0.3f) / 0.7f);
            scale = Mathf.Max(0f, scale);
            s.rt.localScale = new Vector3(scale, scale, 1f);

            // Rotation (sürekli dönüş)
            s.rt.localRotation = Quaternion.Euler(0, 0, k * s.rotationSpeed);

            // Dans (hafif salınım)
            float dance = Mathf.Sin(k * Mathf.PI * 4f + s.baseAngle) * s.floatRadius;
            float rad = s.baseAngle * Mathf.Deg2Rad;
            Vector2 perpendicular = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            s.rt.anchoredPosition = s.basePos + perpendicular * dance;

            // Alpha
            float alpha;
            if (k < 0.15f)
                alpha = k / 0.15f;
            else
                alpha = 1f - (k - 0.15f) / 0.85f;
            alpha = Mathf.Clamp01(alpha);

            Color c = s.image.color;
            c.a = alpha;
            s.image.color = c;
        }
    }

    // ========== SHARDS ==========
    private class ShardInstance
    {
        public RectTransform rt;
        public Image image;
        public Vector2 velocity;   // piksel/saniye
        public float rotationSpeed;
        public float lifetime;     // saniye
        public float age;
    }

    private static ShardInstance CreateShard(RectTransform parent, int tileSize, float angle, float speed)
    {
        var go = new GameObject("Shard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        float shardSize = tileSize * Random.Range(0.10f, 0.18f);
        rt.sizeDelta = new Vector2(shardSize, shardSize * 1.2f); // hafif dikey
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        var img = go.GetComponent<Image>();
        img.color = Random.value < 0.5f ? SHARD_COLOR_A : SHARD_COLOR_B;
        img.raycastTarget = false;
        img.sprite = GetShardSprite();
        img.type = Image.Type.Simple;

        float rad = angle * Mathf.Deg2Rad;
        Vector2 vel = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;

        return new ShardInstance
        {
            rt = rt,
            image = img,
            velocity = vel,
            rotationSpeed = Random.Range(180f, 540f) * (Random.value < 0.5f ? 1f : -1f),
            lifetime = 1f,
            age = 0f
        };
    }

    private static void AnimateShards(List<ShardInstance> shards, float k, int tileSize)
    {
        foreach (var s in shards)
        {
            if (s == null || s.rt == null || s.image == null) continue;

            // Radyal hareket + yerçekimi (k = normalize edilmiş zaman)
            // pos(t) = v0 * t + 0.5 * g * t²
            float t = k; // 0-1 zaman
            Vector2 pos = s.velocity * t;
            // Yerçekimi (aşağı doğru): g * 0.5 * t² (piksel cinsinden)
            float gravity = tileSize * SHARD_GRAVITY;
            pos.y -= 0.5f * gravity * t * t;

            s.rt.anchoredPosition = pos;

            // Scale: hızlı beliriyor → sonunda küçülüyor
            float scale;
            if (k < 0.2f)
                scale = k / 0.2f;
            else
                scale = 1f - (k - 0.2f) / 0.8f * 0.4f;
            s.rt.localScale = new Vector3(scale, scale, 1f);

            // Rotation
            s.rt.localRotation = Quaternion.Euler(0, 0, k * s.rotationSpeed);

            // Alpha (yavaş solma)
            float alpha;
            if (k < 0.1f)
                alpha = k / 0.1f;
            else
                alpha = 1f - (k - 0.1f) / 0.9f;
            alpha = Mathf.Clamp01(alpha);

            Color c = s.image.color;
            c.a = alpha;
            s.image.color = c;
        }
    }

    // ========== SPRITE GENERATION (procedural) ==========
    // Performans için cache
    private static Sprite _softCircleSprite;
    private static Sprite _starSprite;
    private static Sprite _shardSprite;

    private static Sprite GetSoftCircleSprite()
    {
        if (_softCircleSprite != null) return _softCircleSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float outerR = size * 0.48f;
        float innerR = size * 0.34f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float a;
                if (d < innerR)
                {
                    // İç kısım: soft beyaz parlama (merkeze doğru yoğunlaşır)
                    a = Mathf.Lerp(0.3f, 0.05f, d / innerR);
                }
                else if (d < outerR)
                {
                    // Halka kenarı: parlak beyaz şerit (gaussian-like)
                    float ringK = (d - innerR) / (outerR - innerR);
                    a = Mathf.Sin(ringK * Mathf.PI); // 0→1→0
                }
                else
                {
                    a = 0f;
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        _softCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _softCircleSprite;
    }

    private static Sprite GetStarSprite()
    {
        if (_starSprite != null) return _starSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / (size * 0.5f);
                float dy = (y - cy) / (size * 0.5f);

                // 4-uçlu yıldız: dikey + yatay şeritler
                float horizShaft = Mathf.Exp(-dy * dy * 20f) * Mathf.Exp(-Mathf.Abs(dx) * 2f);
                float vertShaft = Mathf.Exp(-dx * dx * 20f) * Mathf.Exp(-Mathf.Abs(dy) * 2f);
                float center = Mathf.Exp(-(dx * dx + dy * dy) * 15f);

                float a = Mathf.Min(1f, horizShaft + vertShaft + center * 0.8f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        _starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _starSprite;
    }

    private static Sprite GetShardSprite()
    {
        if (_shardSprite != null) return _shardSprite;

        const int size = 24;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;

        // Elmas/shard şekli: Y eksenine uzamış eşkenar dörtgen
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - cx) / (size * 0.35f);
                float dy = Mathf.Abs(y - cy) / (size * 0.48f);
                float d = dx + dy; // manhattan distance → elmas

                float a;
                if (d < 0.9f)
                    a = 1f; // iç dolgu
                else if (d < 1f)
                    a = (1f - d) * 10f; // kenar soft
                else
                    a = 0f;

                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        _shardSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _shardSprite;
    }
}