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

    // ── State ────────────────────────────────────────────────────────────────
    private Image _rootImage;
    private Coroutine _shakeRoutine;

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
        if (_rootImage != null && openBackground != null)
            _rootImage.sprite = openBackground;

        if (itemSprites == null || itemSprites.Count == 0) return;

        var rt = GetComponent<RectTransform>();
        float w = rt != null ? rt.rect.width  : 100f;
        float h = rt != null ? rt.rect.height : 100f;

        PlaceItems(itemSprites, w, h, Mathf.Max(1, shelfCount));
    }

    /// <summary>En öndeki item'ı kaldırır (fade-out + destroy).</summary>
    public void RemoveFrontItem()
    {
        if (_frontToBack.Count == 0) return;
        var img = _frontToBack[0];
        _frontToBack.RemoveAt(0);
        if (img != null)
            StartCoroutine(CoFadeAndDestroy(img));
    }

    public void Shake() => StartShake();

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
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(CoShake());
    }

    private IEnumerator CoShake()
    {
        var rt      = GetComponent<RectTransform>();
        var origPos = rt.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < ShakeDuration)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / ShakeDuration;
            float damped = ShakeMagnitude * (1f - t);
            float offsetX = Mathf.Sin(t * Mathf.PI * 6f) * damped;
            rt.anchoredPosition = origPos + new Vector2(offsetX, 0f);
            yield return null;
        }

        rt.anchoredPosition = origPos;
        _shakeRoutine = null;
    }

    private IEnumerator CoFadeAndDestroy(Image img)
    {
        const float duration = 0.25f;
        float elapsed = 0f;
        Color start = img.color;
        var rt = img.GetComponent<RectTransform>();
        Vector3 startScale = rt.localScale;

        while (elapsed < duration && img != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            img.color = new Color(start.r, start.g, start.b, Mathf.Lerp(1f, 0f, t));
            rt.localScale = startScale * Mathf.Lerp(1f, 1.3f, t);
            yield return null;
        }

        if (img != null) Destroy(img.gameObject);
    }
}
