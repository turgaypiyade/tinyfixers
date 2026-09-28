using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Yatay sayfalı kaydırıcı (carousel iskeleti): maskeli görünüm + sayfa izi + alt nokta göstergesi.
/// Kendiliğinden sonraki sayfaya geçer (autoAdvanceSeconds), parmakla sağa/sola kaydırılır.
/// İçerik bilmez: çağıran <see cref="AddPage"/> ile sayfa alır, içine istediğini koyar, <see cref="Finish"/> der.
/// Kullananlar: fail popup teklif carousel'i, level öncesi event şeridi.
///
/// Sayfa üstündeki Button'lar sürüklemeyi yakalamaz → drag buraya gelir; sürükleme başlayınca EventSystem
/// tıklamayı iptal eder (yanlışlıkla satın alma/katılma olmaz).
/// </summary>
public sealed class HorizontalPager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float SlideDuration = 0.35f;
    private const float SwipeThresholdFraction = 0.12f;   // sayfa genişliğinin bu kadarı sürüklenirse sayfa değişir

    public float PageSpacing = 1000f;
    public float AutoAdvanceSeconds = 3.5f;
    public Sprite DotSprite;
    public float DotSize = 20f;
    public float DotGap = 14f;
    [Tooltip("Noktaların alt kenardan yüksekliği (merkez).")]
    public float DotsBottomOffset = 20f;

    /// <summary>Sayfa değişince (otomatik ya da sürükleme) yeni index.</summary>
    public event Action<int> PageChanged;

    public int PageCount => pages.Count;
    public int CurrentIndex => index;

    private RectTransform track;
    private RectTransform dotsRoot;
    private readonly List<RectTransform> pages = new();
    private readonly List<Image> dots = new();

    private int index;
    private float trackX;
    private float slideFrom;
    private float slideT = 1f;
    private bool dragging;
    private float dragStartX;
    private float idleTimer;

    /// <summary>parent altında tam boyutlu (sizeDelta ile ayarlanır) bir pager kurar.</summary>
    public static HorizontalPager Create(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;   // Screen Space Camera culling guard
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var pager = go.AddComponent<HorizontalPager>();
        pager.BuildFrame();
        return pager;
    }

    private void BuildFrame()
    {
        // Görünmez yakalayıcı: sayfanın boş yerlerinden de sürüklenebilsin.
        var hit = gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        gameObject.AddComponent<RectMask2D>();

        track = NewRect("Track", (RectTransform)transform);
        track.anchorMin = Vector2.zero;
        track.anchorMax = Vector2.one;
        track.offsetMin = track.offsetMax = Vector2.zero;

        dotsRoot = NewRect("Dots", (RectTransform)transform);
        dotsRoot.anchorMin = new Vector2(0f, 0f);
        dotsRoot.anchorMax = new Vector2(1f, 0f);
        dotsRoot.sizeDelta = new Vector2(0f, DotSize);
    }

    /// <summary>Tüm sayfaları siler (içerikleriyle).</summary>
    public void Clear()
    {
        foreach (var page in pages)
            if (page != null) Destroy(page.gameObject);
        pages.Clear();
        foreach (var dot in dots)
            if (dot != null) Destroy(dot.gameObject);
        dots.Clear();
        index = 0;
        trackX = 0f;
        slideT = 1f;
        ApplyTrack();
    }

    /// <summary>Yeni sayfa: pager boyutunda, sıradaki yatay konumda.</summary>
    public RectTransform AddPage(string name = "Page")
    {
        var page = NewRect(name, track);
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        page.offsetMin = page.offsetMax = Vector2.zero;
        page.anchoredPosition = new Vector2(pages.Count * PageSpacing, 0f);
        pages.Add(page);
        return page;
    }

    /// <summary>Sayfalar eklendikten sonra: noktaları kur, başa sar.</summary>
    public void Finish()
    {
        foreach (var dot in dots)
            if (dot != null) Destroy(dot.gameObject);
        dots.Clear();

        dotsRoot.anchoredPosition = new Vector2(0f, DotsBottomOffset);
        if (pages.Count >= 2)   // tek sayfada nokta gereksiz
        {
            float totalWidth = pages.Count * DotSize + (pages.Count - 1) * DotGap;
            for (int i = 0; i < pages.Count; i++)
            {
                var dotRt = NewRect("Dot", dotsRoot);
                dotRt.sizeDelta = new Vector2(DotSize, DotSize);
                dotRt.anchoredPosition = new Vector2(-totalWidth * 0.5f + DotSize * 0.5f + i * (DotSize + DotGap), 0f);
                var img = dotRt.gameObject.AddComponent<Image>();
                img.sprite = DotSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                if (DotSprite == null) dotRt.localEulerAngles = new Vector3(0f, 0f, 45f);   // sprite yoksa baklava
                dots.Add(img);
            }
        }

        index = 0;
        trackX = 0f;
        slideT = 1f;
        idleTimer = 0f;
        ApplyTrack();
        RefreshDots();
        PageChanged?.Invoke(index);
    }

    private void Update()
    {
        if (pages.Count == 0) return;

        if (slideT < 1f)
        {
            slideT = Mathf.Min(1f, slideT + Time.unscaledDeltaTime / SlideDuration);
            float e = 1f - Mathf.Pow(1f - slideT, 3f);   // easeOutCubic
            trackX = Mathf.LerpUnclamped(slideFrom, -index * PageSpacing, e);
            ApplyTrack();
        }

        if (dragging || pages.Count < 2 || AutoAdvanceSeconds <= 0f) return;
        idleTimer += Time.unscaledDeltaTime;
        if (idleTimer >= AutoAdvanceSeconds)
            GoTo((index + 1) % pages.Count);
    }

    public void GoTo(int newIndex)
    {
        if (pages.Count == 0) return;
        int clamped = Mathf.Clamp(newIndex, 0, pages.Count - 1);
        bool changed = clamped != index;
        index = clamped;
        slideFrom = trackX;
        slideT = 0f;
        idleTimer = 0f;
        RefreshDots();
        if (changed) PageChanged?.Invoke(index);
    }

    private void ApplyTrack()
    {
        if (track != null) track.anchoredPosition = new Vector2(trackX, 0f);
    }

    private void RefreshDots()
    {
        for (int i = 0; i < dots.Count; i++)
            if (dots[i] != null)
                dots[i].color = i == index ? Color.white : new Color(1f, 1f, 1f, 0.35f);
    }

    // ── Sürükleme ──────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (pages.Count < 2) return;
        dragging = true;
        slideT = 1f;
        dragStartX = trackX;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        // Ekran pikselini yerel birime çevir (canvas modu/ölçeğinden bağımsız).
        var rt = (RectTransform)transform;
        Camera cam = eventData.pressEventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, cam, out Vector2 now)
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position - eventData.delta, cam, out Vector2 prev))
            trackX += now.x - prev.x;

        float min = -(pages.Count - 1) * PageSpacing;
        if (trackX > 0f) trackX *= 0.5f;                       // kenarda lastik
        else if (trackX < min) trackX = min + (trackX - min) * 0.5f;
        ApplyTrack();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;
        float moved = trackX - dragStartX;
        float threshold = PageSpacing * SwipeThresholdFraction;
        int target = index;
        if (moved <= -threshold) target = index + 1;
        else if (moved >= threshold) target = index - 1;
        GoTo(target);
    }

    private RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }
}
