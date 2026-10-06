using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Çiçekli çimin (ObstacleId.GrassFlower) GÖRSEL yöneticisi. Oyun mantığı tamamen çimdedir:
/// GridSpawner level kurulurken GrassFlower hücrelerini Grass'a çevirir ve ObstacleStateService'e
/// çiçekli hücre olarak kaydeder (çimin kalan vuruşu +1). Bu servis yalnızca:
///  • Her çiçekli hücreye 2-3 menekşe çizer (rastgele konum/boy/dönüş, hücreye göre SABİT tohum),
///  • İlk vuruşta (BoardController.OnGrassFlowerShed) çiçekleri yaprak saçarak döker,
///  • Çim yığında başka bir engelin altındaysa çiçekleri gizler, açığa çıkınca gösterir.
///
/// Kök, grass kökünün (GridSpawner.grassOverlayRoot) çocuğudur ve hep son sıradadır: çiçekler
/// komşu çimlerin taşan yapraklarının da üstünde kalır. Koordinatlar grass görselleriyle aynı
/// (anchor sol-üst, hücre (x,y) → (x*tile, -y*tile)).
public class GrassFlowerOverlayService : MonoBehaviour
{
    public const string SpriteResourcesPath = "GrassFlower";

    [Header("Sprites (boşsa Resources/GrassFlower altındaki tüm sprite'lar)")]
    [SerializeField] private List<Sprite> flowerSprites = new();

    [Header("Yerleşim")]
    [SerializeField] private int minFlowersPerCell = 2;
    [SerializeField] private int maxFlowersPerCell = 3;
    [Tooltip("Çiçek boyu, hücre kenarına oranla (min).")]
    [SerializeField] private float minSizeRatio = 0.28f;
    [Tooltip("Çiçek boyu, hücre kenarına oranla (max).")]
    [SerializeField] private float maxSizeRatio = 0.52f;
    [SerializeField] private float maxRotationDeg = 25f;
    [Tooltip("İki çiçeğin merkezleri arasındaki en az mesafe, yarıçaplar toplamının bu katı.")]
    [SerializeField] private float minSeparation = 0.62f;

    [Header("Gölge")]
    [SerializeField] private Color shadowColor = new(0f, 0.12f, 0f, 0.28f);
    [SerializeField] private Vector2 shadowOffsetRatio = new(0.03f, -0.045f);

    [Header("Dökülme")]
    [SerializeField] private float shedDuration = 0.55f;
    [SerializeField] private int petalsPerFlower = 4;
    [SerializeField] private float petalDuration = 0.6f;

    private BoardController board;
    private int gridWidth;
    private float tileSize;

    private sealed class FlowerCell
    {
        public RectTransform root;
        public bool shedding;
    }

    private readonly Dictionary<int, FlowerCell> cells = new();
    private readonly List<int> scratchCells = new();
    private readonly List<int> scratchKeys = new();

    public void Init(BoardController board, int width, float tileSize)
    {
        Unbind();
        this.board = board;
        gridWidth = Mathf.Max(1, width);
        this.tileSize = Mathf.Max(1f, tileSize);

        if (flowerSprites == null || flowerSprites.Count == 0)
        {
            var loaded = Resources.LoadAll<Sprite>(SpriteResourcesPath);
            flowerSprites = loaded != null ? new List<Sprite>(loaded) : new List<Sprite>();
            flowerSprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        if (board != null)
        {
            board.OnGrassFlowerShed += HandleShed;
            board.OnBecameIdle += HandleBoardIdle;
            board.OnObstacleViewRestored += HandleViewChanged;
            board.OnObstacleCreatedDynamic += HandleViewChanged;
        }
    }

    private void OnDestroy() => Unbind();

    private void Unbind()
    {
        if (board == null) return;
        board.OnGrassFlowerShed -= HandleShed;
        board.OnBecameIdle -= HandleBoardIdle;
        board.OnObstacleViewRestored -= HandleViewChanged;
        board.OnObstacleCreatedDynamic -= HandleViewChanged;
    }

    /// Kayıtlı tüm çiçekli hücreleri baştan çizer (level kurulumu).
    public void SpawnAll()
    {
        ClearAll();
        var state = board != null ? board.ObstacleStateService : null;
        if (state == null || flowerSprites.Count == 0) return;

        scratchCells.Clear();
        state.CollectGrassFlowerCells(scratchCells);
        scratchCells.Sort();
        for (int i = 0; i < scratchCells.Count; i++)
            SpawnCell(scratchCells[i]);

        RefreshVisibility();
    }

    public void ClearAll()
    {
        foreach (var kv in cells)
            if (kv.Value?.root != null) Destroy(kv.Value.root.gameObject);
        cells.Clear();
    }

    // Dinamik çizilen grass görselleri kökün sonuna eklenir; çiçekler hep onların üstünde kalsın.
    private void LateUpdate()
    {
        var parent = transform.parent;
        if (parent != null && transform.GetSiblingIndex() != parent.childCount - 1)
            transform.SetAsLastSibling();
    }

    // ── Çizim ────────────────────────────────────────────────────────────────

    private void SpawnCell(int cell)
    {
        if (cells.ContainsKey(cell)) return;

        int x = cell % gridWidth;
        int y = cell / gridWidth;

        var go = new GameObject($"GrassFlower_{x}_{y}", typeof(RectTransform));
        go.layer = gameObject.layer;
        var root = (RectTransform)go.transform;
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(x * tileSize + tileSize * 0.5f, -(y * tileSize + tileSize * 0.5f));
        root.sizeDelta = new Vector2(tileSize, tileSize);

        foreach (var f in BuildLayout(cell))
        {
            var sprite = flowerSprites[f.spriteIndex % flowerSprites.Count];
            float size = f.sizeRatio * tileSize;
            var pos = f.position * tileSize;

            var shadow = CreateImage(root, "Shadow", sprite, shadowColor);
            Place(shadow.rectTransform, pos + shadowOffsetRatio * tileSize, size, f.rotation);

            var flower = CreateImage(root, "Flower", sprite, Color.white);
            Place(flower.rectTransform, pos, size, f.rotation);
        }

        cells[cell] = new FlowerCell { root = root };
    }

    private readonly struct FlowerSpec
    {
        public readonly Vector2 position;   // hücre merkezine göre, hücre kenarı birimiyle
        public readonly float sizeRatio;
        public readonly float rotation;
        public readonly int spriteIndex;

        public FlowerSpec(Vector2 position, float sizeRatio, float rotation, int spriteIndex)
        {
            this.position = position;
            this.sizeRatio = sizeRatio;
            this.rotation = rotation;
            this.spriteIndex = spriteIndex;
        }
    }

    // Hücreye göre SABİT tohum: level her açıldığında aynı hücre aynı görünür. Sığmayan çiçek
    // atlanmaz; biraz küçültülüp yeniden denenir → en az minFlowersPerCell garanti.
    private List<FlowerSpec> BuildLayout(int cell)
    {
        var rng = new System.Random(cell * 7919 + 104729);
        int minCount = Mathf.Max(1, minFlowersPerCell);
        int count = rng.Next(minCount, Mathf.Max(minCount, maxFlowersPerCell) + 1);

        var sizes = new List<float>(count);
        for (int i = 0; i < count; i++)
            sizes.Add(Mathf.Lerp(minSizeRatio, maxSizeRatio, (float)rng.NextDouble()));
        sizes.Sort((a, b) => b.CompareTo(a));   // büyükler önce çizilir, küçükler önlerinde kalır

        var result = new List<FlowerSpec>(count);
        int spriteOffset = rng.Next(flowerSprites.Count);
        for (int i = 0; i < count; i++)
        {
            float size = sizes[i];
            for (int attempt = 0; ; attempt++)
            {
                if (attempt > 0 && attempt % 25 == 0)
                    size = Mathf.Max(minSizeRatio * 0.6f, size * 0.85f);

                float r = size * 0.5f;
                float limit = Mathf.Max(0f, 0.5f - r * 0.8f);   // çiçek hücreden çok az taşabilir
                var p = new Vector2(
                    Mathf.Lerp(-limit, limit, (float)rng.NextDouble()),
                    Mathf.Lerp(-limit, limit, (float)rng.NextDouble()));

                bool fits = true;
                for (int k = 0; k < result.Count && fits; k++)
                {
                    float minDist = (r + result[k].sizeRatio * 0.5f) * minSeparation;
                    fits = (p - result[k].position).sqrMagnitude >= minDist * minDist;
                }

                if (fits || attempt >= 150)
                {
                    float rot = Mathf.Lerp(-maxRotationDeg, maxRotationDeg, (float)rng.NextDouble());
                    // Aynı hücrede aynı menekşe tekrar etmesin.
                    result.Add(new FlowerSpec(p, size, rot, spriteOffset + i * 3));
                    break;
                }
            }
        }
        return result;
    }

    private Image CreateImage(RectTransform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    private static void Place(RectTransform rt, Vector2 position, float size, float rotation)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(size, size);
        rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    // ── Görünürlük ───────────────────────────────────────────────────────────

    private void HandleViewChanged(int x, int y) => RefreshVisibility();

    private void RefreshVisibility()
    {
        var state = board != null ? board.ObstacleStateService : null;
        if (state == null) return;
        foreach (var kv in cells)
        {
            var c = kv.Value;
            if (c?.root == null || c.shedding) continue;
            bool visible = state.IsGrassVisibleAt(kv.Key);
            if (c.root.gameObject.activeSelf != visible)
                c.root.gameObject.SetActive(visible);
        }
    }

    // Board yatıştı: verisi gitmiş (olay kaçmış) çiçekleri dök + görünürlüğü tazele.
    private void HandleBoardIdle()
    {
        var state = board != null ? board.ObstacleStateService : null;
        if (state == null) return;

        scratchKeys.Clear();
        foreach (var kv in cells)
            if (kv.Value != null && !kv.Value.shedding && !state.HasGrassFlowerAt(kv.Key))
                scratchKeys.Add(kv.Key);
        for (int i = 0; i < scratchKeys.Count; i++)
            HandleShed(scratchKeys[i]);

        RefreshVisibility();
    }

    // ── Dökülme ──────────────────────────────────────────────────────────────

    private void HandleShed(int cell)
    {
        if (!cells.TryGetValue(cell, out var c) || c == null || c.shedding) return;
        cells.Remove(cell);
        if (c.root == null) return;

        c.shedding = true;
        if (!c.root.gameObject.activeInHierarchy || !isActiveAndEnabled)
        {
            Destroy(c.root.gameObject);
            return;
        }
        StartCoroutine(ShedRoutine(c.root));
    }

    private IEnumerator ShedRoutine(RectTransform root)
    {
        var images = root.GetComponentsInChildren<Image>();
        int n = images.Length;
        var startPos = new Vector2[n];
        var startRot = new float[n];
        var drift = new Vector2[n];
        var spin = new float[n];
        var startColor = new Color[n];

        for (int i = 0; i < n; i++)
        {
            var rt = images[i].rectTransform;
            startPos[i] = rt.anchoredPosition;
            startRot[i] = rt.localEulerAngles.z;
            startColor[i] = images[i].color;
            // Gölge ile çiçeği aynı yöne savur (Shadow → bir sonraki Flower ile eşleşir).
            int pair = images[i].name == "Shadow" && i + 1 < n ? i + 1 : i;
            var seed = new System.Random(pair * 31 + root.GetInstanceID());
            drift[i] = new Vector2(Mathf.Lerp(-0.35f, 0.35f, (float)seed.NextDouble()), -0.55f) * tileSize;
            spin[i] = Mathf.Lerp(-160f, 160f, (float)seed.NextDouble());

            if (images[i].name == "Flower")
                SpawnPetals(images[i]);
        }

        float duration = Mathf.Max(0.05f, shedDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // Önce küçük bir "pıt" (büyüme), sonra yerçekimiyle düşüş.
            float pop = k < 0.18f ? 1f + 0.15f * (k / 0.18f) : Mathf.Lerp(1.15f, 0.7f, (k - 0.18f) / 0.82f);
            float fall = k * k;
            for (int i = 0; i < n; i++)
            {
                if (images[i] == null) continue;
                var rt = images[i].rectTransform;
                rt.anchoredPosition = startPos[i] + drift[i] * fall;
                rt.localRotation = Quaternion.Euler(0f, 0f, startRot[i] + spin[i] * k);
                rt.localScale = Vector3.one * pop;
                var c = startColor[i];
                c.a *= 1f - Mathf.Clamp01((k - 0.35f) / 0.65f);
                images[i].color = c;
            }
            yield return null;
        }

        if (root != null) Destroy(root.gameObject);
    }

    // Küçük yaprak parçacıkları: çiçek sprite'ının minik kopyaları etrafa saçılıp süzülerek düşer.
    private void SpawnPetals(Image flower)
    {
        if (petalsPerFlower <= 0 || flower == null) return;
        var rt = flower.rectTransform;
        var parentRoot = (RectTransform)transform;
        Vector2 origin = (Vector2)parentRoot.InverseTransformPoint(rt.position);
        float size = rt.sizeDelta.x * 0.32f;

        for (int i = 0; i < petalsPerFlower; i++)
        {
            var img = CreateImage(parentRoot, "Petal", flower.sprite, Color.white);
            var prt = img.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.localPosition = origin;
            prt.sizeDelta = new Vector2(size, size);
            prt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            float angle = Random.Range(0f, Mathf.PI * 2f);
            var velocity = new Vector2(Mathf.Cos(angle), Mathf.Abs(Mathf.Sin(angle)) * 0.8f + 0.4f)
                           * Random.Range(0.9f, 1.6f) * tileSize;
            StartCoroutine(PetalRoutine(img, velocity, Random.Range(-360f, 360f)));
        }
    }

    private IEnumerator PetalRoutine(Image img, Vector2 velocity, float spinPerSec)
    {
        var rt = img.rectTransform;
        float duration = Mathf.Max(0.05f, petalDuration);
        float gravity = 4.5f * tileSize;
        float t = 0f;
        while (t < duration && img != null)
        {
            float dt = Time.deltaTime;
            t += dt;
            velocity.y -= gravity * dt;
            velocity.x *= 1f - Mathf.Min(1f, 2.5f * dt);   // hava direnci: yaprak süzülür
            rt.localPosition += (Vector3)(velocity * dt);
            rt.localRotation *= Quaternion.Euler(0f, 0f, spinPerSec * dt);
            float k = t / duration;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
            var c = img.color; c.a = 1f - k * k; img.color = c;
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }
}
