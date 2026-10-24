using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wardrobe obstacle görsel kontrolcüsü.
/// Kapalı → kapı açılır → içindeki item'lar önden arkaya sırayla kırılır.
/// Depth layout: merkezdeki item en büyük/en önde, kenardakiler küçük/arkada.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class WardrobeObstacleView : MonoBehaviour
{
    // ── Constants ───────────────────────────────────────────────────────────
    private const float MinScale       = 0.75f;
    private const float MaxScale       = 1.00f;
    private const float OverlapFactor  = 0.42f;   // spacing = itemWidth * this (küçük = daha fazla overlap)
    private const float YOffsetFactor  = 0.10f;   // back items bu kadar yukarı kayar (oransal)
    private const float MinTint        = 0.62f;   // en arka item'ın parlaklık çarpanı
    private const float ShakeMagnitude = 6f;
    private const float ShakeDuration  = 0.35f;

    // ── Kapı Düşüşü (ilk hit) ──────────────────────────────────────────────────
    [Header("Kapı Düşüşü (ilk hit)")]
    [Tooltip("İlk hitte kapalı sprite'ı 2 büyük parçaya (sol/sağ kapak) bölüp yerçekimiyle düşür.")]
    [SerializeField] private bool doorFallEnabled = true;
    [SerializeField, Min(0.05f)] private float doorFallDuration = 0.95f;
    [Tooltip("Düşme ivmesi (px/s²). Büyük = daha hızlı/ağır düşer.")]
    [SerializeField] private float doorFallGravity = 2600f;
    [Tooltip("Kapakların yanlara savrulma hızı (px/s). Sol sola, sağ sağa.")]
    [SerializeField] private float doorFallSideKick = 150f;
    [Tooltip("İlk fırlatma yukarı hızı (px/s). Parçalar önce yukarı zıplar, sonra yerçekimiyle düşer.")]
    [SerializeField] private float doorFallLaunchUp = 600f;
    [Tooltip("Düşerken toplam dönme (derece/sn).")]
    [SerializeField] private float doorSpinDegrees = 80f;

    [Header("Item Düşüşü")]
    [SerializeField, Min(0.05f)] private float itemFallDuration = 0.65f;
    [SerializeField] private float itemFallGravity = 1800f;
    [SerializeField] private float itemFallSideKick = 90f;
    [SerializeField] private float itemFallLaunchUp = 260f;
    [SerializeField] private float itemSpinDegrees = 160f;

    // ── State ────────────────────────────────────────────────────────────────
    private Image _rootImage;
    private Coroutine _shakeRoutine;
    private Vector2 _shakeBasePos;

    // Ön → arka sırasıyla tutulan item image'ları (index 0 = en önde = ilk kırılacak)
    private readonly List<Image> _frontToBack = new();

    // ── Setup ────────────────────────────────────────────────────────────────

    private void Awake() => _rootImage = GetComponent<Image>();

    /// <summary>GridSpawner tarafından spawn sonrası çağrılır.</summary>
    public void SetClosedSprite(Sprite closed)
    {
        if (_rootImage != null && closed != null)
            _rootImage.sprite = closed;
    }

    /// <summary>Kapı açıldığında çağrılır. Arka plan değişir, item'lar yerleştirilir.</summary>
    public void OpenDoor(Sprite openBackground, List<Sprite> itemSprites, int shelfCount = 1)
    {
        // Kapakları kapalı sprite'ın sol/sağ yarısından üretmek için ÖNCE yakala (swap'tan önce).
        Sprite closedSprite = _rootImage != null ? _rootImage.sprite : null;

        // Açık arka plan + item'lar yerleşir.
        if (_rootImage != null && openBackground != null)
            _rootImage.sprite = openBackground;

        if (itemSprites != null && itemSprites.Count > 0)
        {
            var rt = GetComponent<RectTransform>();
            float w = rt != null ? rt.rect.width  : 100f;
            float h = rt != null ? rt.rect.height : 100f;
            PlaceItems(itemSprites, w, h, Mathf.Max(1, shelfCount));
        }

        // Kapaklar EN SON spawn edilir → içerik/item'ların ÜSTÜnde düşer (arkada kalma fix'i).
        if (doorFallEnabled && closedSprite != null)
            SpawnFallingDoors(closedSprite);
    }

    /// <summary>En öndeki item'ı kaldırır (fade-out + destroy).</summary>
    public void RemoveFrontItem()
    {
        if (_frontToBack.Count == 0) return;
        var img = _frontToBack[0];
        _frontToBack.RemoveAt(0);
        if (img != null)
            StartCoroutine(CoItemFallAndDestroy(img));
    }

    public float RecommendedClearDestroyDelay => Mathf.Max(0.1f, itemFallDuration + 0.05f);

    public void Shake() => StartShake();

    // ── Kapı Düşüşü ────────────────────────────────────────────────────────────

    /// <summary>Kapalı sprite'ı sol/sağ iki büyük parçaya bölüp yerçekimiyle düşürür.</summary>
    private void SpawnFallingDoors(Sprite closed)
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        float w = rt.rect.width  > 1f ? rt.rect.width  : 100f;
        float h = rt.rect.height > 1f ? rt.rect.height : 100f;

        SpawnDoorPiece(MakeHalfSprite(closed, true),  new Vector2(-w * 0.25f, 0f), w * 0.5f, h, -1);
        SpawnDoorPiece(MakeHalfSprite(closed, false), new Vector2(+w * 0.25f, 0f), w * 0.5f, h, +1);
    }

    private void SpawnDoorPiece(Sprite sp, Vector2 localCenter, float w, float h, int dir)
    {
        if (sp == null) return;

        var go = new GameObject("DoorPiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var prt = go.GetComponent<RectTransform>();
        prt.SetParent(transform, false);
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(w, h);
        prt.anchoredPosition = localCenter;
        prt.SetAsLastSibling();   // açık arka planın ÜSTÜNde düşsün

        var img = go.GetComponent<Image>();
        img.sprite = sp;
        img.color = Color.white;      // tam opak başla (yarı-saydam görünme fix'i)
        img.raycastTarget = false;

        StartCoroutine(CoDoorFall(prt, img, dir));
    }

    private IEnumerator CoDoorFall(RectTransform rt, Image img, int dir)
    {
        Vector2 pos = rt.anchoredPosition;
        Vector2 vel = new Vector2(dir * doorFallSideKick, doorFallLaunchUp);   // önce yukarı fırlar, sonra düşer
        float rot = 0f;
        float t = 0f;
        while (t < doorFallDuration && rt != null)
        {
            float dt = Time.deltaTime;
            t += dt;
            vel.y -= doorFallGravity * dt;
            pos   += vel * dt;
            rot   += dir * doorSpinDegrees * dt;
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, rot);

            float k = Mathf.Clamp01(t / doorFallDuration);
            // Fade yalnız SON %20'de → parçalar düşüşün büyük kısmında tam opak kalır.
            if (img != null && k > 0.8f)
                SetImageAlpha(img, Mathf.Lerp(1f, 0f, (k - 0.8f) / 0.2f));
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    // Kapalı sprite'ın sol veya sağ YARISINI aynı texture'dan yeni bir Sprite olarak keser.
    private static Sprite MakeHalfSprite(Sprite src, bool leftHalf)
    {
        if (src == null || src.texture == null) return src;
        Rect r = src.rect;
        Rect half = new Rect(leftHalf ? r.x : r.x + r.width * 0.5f, r.y, r.width * 0.5f, r.height);
        float ppu = src.pixelsPerUnit > 0f ? src.pixelsPerUnit : 100f;
        return Sprite.Create(src.texture, half, new Vector2(0.5f, 0.5f), ppu);
    }

    private static void SetImageAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color; c.a = a; img.color = c;
    }

    // ── Depth Layout ─────────────────────────────────────────────────────────

    private void PlaceItems(List<Sprite> sprites, float parentW, float parentH, int shelfCount)
    {
        int n = sprites.Count;
        if (n == 0) return;

        int itemsPerShelf = Mathf.CeilToInt((float)n / shelfCount);
        float shelfH      = parentH / shelfCount;

        for (int shelf = 0; shelf < shelfCount; shelf++)
        {
            int startIdx = shelf * itemsPerShelf;
            int endIdx   = Mathf.Min(startIdx + itemsPerShelf, n);
            if (startIdx >= n) break;

            // Raf'ın alt kenarı, items pivot=bottom ile bu Y'ye oturur.
            // shelf=0 → üst raf tabanı: -shelfH; shelf=1 → alt raf tabanı: -2*shelfH
            bool isBottomShelf = shelf == shelfCount - 1;
            float shelfBaseY = -(shelf + 1) * shelfH + (isBottomShelf ? 18f : 0f);

            var shelfSprites = sprites.GetRange(startIdx, endIdx - startIdx);
            PlaceShelf(shelfSprites, parentW, shelfH, shelfBaseY, startIdx);
        }
    }

    private void PlaceShelf(List<Sprite> sprites, float shelfW, float shelfH, float shelfBaseY, int globalOffset)
    {
        int n = sprites.Count;

        float itemDisplayW = shelfW / Mathf.Max(1f, n * 0.52f + 0.48f);
        float spacing      = itemDisplayW * OverlapFactor;
        float totalSpan    = (n - 1) * spacing;

        // depthFactor: 0 = kenar (arka), 1 = merkez (ön)
        var depthData = new List<(int pos, float depth)>(n);
        for (int i = 0; i < n; i++)
        {
            float dist        = Mathf.Abs(i - (n - 1) / 2f);
            float depthFactor = n > 1 ? 1f - (dist / ((n - 1) / 2f)) : 1f;
            depthData.Add((i, depthFactor));
        }

        // Render sırası: arkadan öne
        var renderOrder = new List<(int pos, float depth)>(depthData);
        renderOrder.Sort((a, b) => a.depth.CompareTo(b.depth));

        var spawnedByPos = new Dictionary<int, Image>();
        foreach (var (pos, depth) in renderOrder)
        {
            float x = -totalSpan * 0.5f + pos * spacing;
            // pivot=bottom: shelfBaseY = raf tabanı; arka item'lar hafif yukarı kalkar
            float yOffset = shelfH * YOffsetFactor * (1f - depth);
            float y       = shelfBaseY + yOffset;
            float scale = MinScale + (MaxScale - MinScale) * depth;

            var go = new GameObject($"WardrobeItem_{globalOffset + pos}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var imgRt = go.GetComponent<RectTransform>();
            imgRt.anchorMin        = new Vector2(0.5f, 1f);   // üst-orta anchor, Y aşağı negatif
            imgRt.anchorMax        = new Vector2(0.5f, 1f);
            imgRt.pivot            = new Vector2(0.5f, 0f);   // pivot alt-orta → item tabanı Y'ye oturur
            imgRt.sizeDelta        = new Vector2(itemDisplayW, itemDisplayW);
            imgRt.anchoredPosition = new Vector2(x, y);
            imgRt.localScale       = Vector3.one * scale;

            var img = go.GetComponent<Image>();
            img.sprite         = sprites[pos];
            img.preserveAspect = true;
            img.raycastTarget  = false;
            img.color          = Color.white;

            spawnedByPos[pos] = img;
        }

        // _frontToBack: en ön önce (depth descending)
        var frontOrder = new List<(int pos, float depth)>(depthData);
        frontOrder.Sort((a, b) => b.depth.CompareTo(a.depth));
        foreach (var (pos, _) in frontOrder)
        {
            if (spawnedByPos.TryGetValue(pos, out var img))
                _frontToBack.Add(img);
        }
    }

    // ── Animations ───────────────────────────────────────────────────────────

    private void StartShake()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // Taban pozisyonu yalnızca SALLANMIYORKEN yakala. Shake coroutine'i ilk
        // yield'e kadar senkron koştuğu için pozisyonu anında kaydırır; üst üste
        // gelen ikinci Shake (örn. special'ın çift item kırması aynı frame'de iki
        // event yollar) kaymış pozisyonu taban sanıp dolabı kalıcı yürütüyordu.
        if (_shakeRoutine != null)
            StopCoroutine(_shakeRoutine);
        else
            _shakeBasePos = rt.anchoredPosition;

        _shakeRoutine = StartCoroutine(CoShake(rt));
    }

    private IEnumerator CoShake(RectTransform rt)
    {
        float elapsed = 0f;

        while (elapsed < ShakeDuration)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / ShakeDuration;
            float damped = ShakeMagnitude * (1f - t);
            float offsetX = Mathf.Sin(t * Mathf.PI * 6f) * damped;
            rt.anchoredPosition = _shakeBasePos + new Vector2(offsetX, 0f);
            yield return null;
        }

        rt.anchoredPosition = _shakeBasePos;
        _shakeRoutine = null;
    }

    private IEnumerator CoItemFallAndDestroy(Image img)
    {
        float duration = Mathf.Max(0.05f, itemFallDuration);
        float elapsed = 0f;
        var rt = img.GetComponent<RectTransform>();
        if (rt == null)
        {
            if (img != null) Destroy(img.gameObject);
            yield break;
        }

        rt.SetAsLastSibling();
        Color start = img.color;
        Vector2 pos = rt.anchoredPosition;
        Vector3 startScale = rt.localScale;
        float side = Random.value < 0.5f ? -1f : 1f;
        Vector2 vel = new Vector2(side * itemFallSideKick, itemFallLaunchUp);
        float rot = rt.localEulerAngles.z;

        while (elapsed < duration && img != null)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            vel.y -= itemFallGravity * dt;
            pos += vel * dt;
            rot += side * itemSpinDegrees * dt;

            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, rot);
            rt.localScale = startScale * Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(elapsed / duration));

            float k = Mathf.Clamp01(elapsed / duration);
            if (k > 0.72f)
                img.color = new Color(start.r, start.g, start.b, Mathf.Lerp(start.a, 0f, (k - 0.72f) / 0.28f));

            yield return null;
        }

        if (img != null) Destroy(img.gameObject);
    }
}
