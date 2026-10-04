using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Satın alma kutlaması — tam ekran siyah overlay, koddan kurulur (prefab yok). Akış:
///   karartma → satın alınanlar SIRAYLA küçükten büyüğe pop (overshoot) → "HARİKA!" (DynaPuff,
///   oyun anı başlığı) → altta yanıp sönen "Devam etmek için dokun" → dokun → kapan.
/// Animasyon sürerken dokunmak beklemeyi atlar (tüm öğeler + başlık anında yerine oturur);
/// oyuncu hiçbir zaman bekletilmez. Fontlar CommonPopupSkin'den (celebrationFont / font).
///
/// Toplama modu (CollectRoute verilirse — yalnız ana menü marketi): dokununca arka planda ana ekrana
/// geçilir, karartma/başlık söner ve öğeler BULUNDUKLARI yerden hedeflerine (altın sayacı, kalp,
/// level butonu) uçar; hedef zıplayıp "yakalar". Verilmezse (oyun içi) sadece kapanır.
/// </summary>
public sealed class ShopPurchaseCelebration : MonoBehaviour
{
    private const float DimAlpha    = 0.96f;   // siyah overlay koyuluğu (sandık töreni 0.93)
    private const float DimFadeIn   = 0.25f;
    private const float ItemPop     = 0.42f;
    private const float ItemStagger = 0.2f;
    private const float TitlePop    = 0.45f;

    private const float IconSize    = 210f;
    private const float CellWidth   = 300f;
    private const float CellHeight  = 340f;
    private const int   PerRow      = 3;

    private const float FlyDuration = 0.6f;
    private const float FlyStagger  = 0.08f;

    /// Toplama modu: ana ekrana geçiş, öğe başına hedef ve öğe vardığında çağrılacak geri bildirim.
    public sealed class CollectRoute
    {
        public Action SwitchToHome;
        public Func<int, RectTransform> TargetFor;
        public Action<int> OnLanded;
    }

    private static ShopPurchaseCelebration _instance;

    private IReadOnlyList<RuntimeChoicePopup.RewardItem> _items;
    private Action _onDone;
    private CanvasGroup _group;
    private readonly List<RectTransform> _cells = new();
    private RectTransform _title;
    private TMP_Text _titleText;
    private Image _dimImage;
    private TMP_Text _tapText;
    private CollectRoute _collect;
    private bool _collecting;
    private readonly HashSet<RectTransform> _punching = new();
    private bool _skipRequested;
    private bool _tapToClose;
    private bool _finished;

    public static void Show(IReadOnlyList<RuntimeChoicePopup.RewardItem> items, Action onDone = null,
                            CollectRoute collect = null)
    {
        if (_instance != null) Destroy(_instance.gameObject);

        var root = new GameObject("ShopPurchaseCelebration");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32750;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var view = root.AddComponent<ShopPurchaseCelebration>();
        view._items = items ?? Array.Empty<RuntimeChoicePopup.RewardItem>();
        view._onDone = onDone;
        view._collect = collect;
        _instance = view;
        DontDestroyOnLoad(root);
        view.Build();
        view.StartCoroutine(view.Run());
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        Finish(destroy: false);   // yarıda yok edilirse (sahne değişimi) callback yine çalışsın
    }

    // ── Kurulum ─────────────────────────────────────────────────────

    private void Build()
    {
        var skin = CommonPopupSkin.Shared;

        var dim = NewRect("Dim", transform);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, DimAlpha);
        _dimImage = dimImg;
        var tap = dim.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(HandleTap);

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        // Öğe ızgarası: satır başına en fazla 3, her satır kendi içinde ortalanır.
        var grid = NewRect("Items", transform);
        grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.5f);
        grid.anchoredPosition = new Vector2(0f, -40f);
        int count = _items.Count;
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)PerRow));
        for (int i = 0; i < count; i++)
        {
            int row = i / PerRow;
            int inRow = Mathf.Min(PerRow, count - row * PerRow);
            int col = i % PerRow;
            var cell = BuildItem(_items[i], grid, skin);
            cell.anchoredPosition = new Vector2(
                (col - (inRow - 1) * 0.5f) * CellWidth,
                ((rows - 1) * 0.5f - row) * CellHeight);
            cell.localScale = Vector3.zero;
            _cells.Add(cell);
        }

        // "HARİKA!" — ızgaranın üstünde; DynaPuff + RoyalTitle (siyah zeminde açık yüz, koyu kontur).
        float gridTop = rows * CellHeight * 0.5f - 40f;
        _title = NewRect("Title", transform);
        _title.anchorMin = _title.anchorMax = new Vector2(0.5f, 0.5f);
        _title.anchoredPosition = new Vector2(0f, gridTop + 170f);
        _title.sizeDelta = new Vector2(1000f, 220f);
        var title = _title.gameObject.AddComponent<TextMeshProUGUI>();
        _titleText = title;
        title.text = GameLocalization.Get("purchase_celebration_title");
        var titleFont = skin != null && skin.celebrationFont != null ? skin.celebrationFont
                      : skin != null ? skin.titleFont : null;
        if (titleFont != null) title.font = titleFont;
        if (skin != null && skin.celebrationFont != null && skin.celebrationMaterial != null)
            title.fontSharedMaterial = skin.celebrationMaterial;
        title.fontSize = 150f;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(Color.white, Color.white,
            new Color(1f, 0.84f, 0.3f), new Color(1f, 0.84f, 0.3f));
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.raycastTarget = false;
        _title.localScale = Vector3.zero;

        // "Devam etmek için dokun" — altta, başlık oturunca yanıp söner.
        var tapRt = NewRect("TapText", transform);
        tapRt.anchorMin = tapRt.anchorMax = new Vector2(0.5f, 0f);
        tapRt.anchoredPosition = new Vector2(0f, 170f);
        tapRt.sizeDelta = new Vector2(900f, 80f);
        _tapText = tapRt.gameObject.AddComponent<TextMeshProUGUI>();
        _tapText.text = GameLocalization.Get("common_tap_continue");
        if (skin != null && skin.font != null) _tapText.font = skin.font;
        _tapText.fontSize = 40f;
        _tapText.fontStyle = FontStyles.Bold;
        _tapText.alignment = TextAlignmentOptions.Center;
        _tapText.color = new Color(1f, 1f, 1f, 0f);
        _tapText.raycastTarget = false;
    }

    // İkon (büyük) + altında miktar; ikon yoksa adı ikon yerine yazılır.
    private static RectTransform BuildItem(RuntimeChoicePopup.RewardItem item, Transform parent, CommonPopupSkin skin)
    {
        var cell = NewRect("Item", parent);
        cell.sizeDelta = new Vector2(CellWidth, CellHeight);

        var iconRt = NewRect("Icon", cell);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.sizeDelta = new Vector2(IconSize, IconSize);
        if (item.Icon != null)
        {
            var icon = iconRt.gameObject.AddComponent<Image>();
            icon.sprite = item.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        else
        {
            var name = iconRt.gameObject.AddComponent<TextMeshProUGUI>();
            name.text = item.Name;
            if (skin != null && skin.font != null) name.font = skin.font;
            name.alignment = TextAlignmentOptions.Center;
            name.enableAutoSizing = true;
            name.fontSizeMin = 28f;
            name.fontSizeMax = 54f;
            name.raycastTarget = false;
            RewardTextStyle.Apply(name, 54f);
        }

        var amountRt = NewRect("Amount", cell);
        amountRt.anchorMin = amountRt.anchorMax = new Vector2(0.5f, 0f);
        amountRt.pivot = new Vector2(0.5f, 0f);
        amountRt.sizeDelta = new Vector2(CellWidth + 40f, 100f);
        var amount = amountRt.gameObject.AddComponent<TextMeshProUGUI>();
        amount.text = item.Amount;
        amount.alignment = TextAlignmentOptions.Center;
        amount.textWrappingMode = TextWrappingModes.NoWrap;
        amount.raycastTarget = false;
        RewardTextStyle.Apply(amount, 64f);
        return cell;
    }

    // ── Akış ────────────────────────────────────────────────────────

    private IEnumerator Run()
    {
        yield return Animate(DimFadeIn, t => _group.alpha = t);

        // Öğeler sırayla pop; atlama istenirse kalanlar anında yerine.
        for (int i = 0; i < _cells.Count && !_skipRequested; i++)
        {
            StartCoroutine(Pop(_cells[i], ItemPop, 1.28f));
            yield return Wait(ItemStagger);
        }
        if (!_skipRequested) yield return Wait(ItemPop - ItemStagger + 0.1f);
        SettleAll();

        GameEventSfx.PlayFireworkBurst(0.7f);
        yield return Pop(_title, _skipRequested ? 0.01f : TitlePop, 1.22f);

        _tapToClose = true;
        StartCoroutine(BlinkTapText());
    }

    private void HandleTap()
    {
        if (_tapToClose)
        {
            _tapToClose = false;
            StartCoroutine(_collect != null ? CollectAndClose() : Close());
            return;
        }
        _skipRequested = true;
    }

    // Atlamada başlamamış/yarıda kalmış hücreler anında yerine (Pop korutinleri _skipRequested'ı görüp biter).
    private void SettleAll()
    {
        foreach (var c in _cells) if (c != null) c.localScale = Vector3.one;
    }

    private IEnumerator Pop(RectTransform rt, float duration, float peak)
    {
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);
        while (t < d && !(_skipRequested && rt != _title))
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            // 0 → peak (ilk %60) → 1: küçükten büyüğe patlayıp yerine oturur.
            float s = k < 0.6f
                ? Mathf.Lerp(0f, peak, EaseOut(k / 0.6f))
                : Mathf.Lerp(peak, 1f, EaseInOut((k - 0.6f) / 0.4f));
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator BlinkTapText()
    {
        float t = 0f;
        while (!_finished && !_collecting && _tapText != null)
        {
            t += Time.unscaledDeltaTime;
            _tapText.color = new Color(1f, 1f, 1f, 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 4.5f)));
            yield return null;
        }
    }

    // Ana ekrana geç → karartma/başlık/yazı söner → öğeler yerlerinden hedeflerine uçar → kapan.
    private IEnumerator CollectAndClose()
    {
        _collecting = true;
        _collect.SwitchToHome?.Invoke();
        yield return null;                 // ana ekran (HUD, level butonu) yerleşsin
        Canvas.ForceUpdateCanvases();

        float titleA = _titleText != null ? _titleText.alpha : 1f;
        float tapA = _tapText != null ? _tapText.color.a : 1f;
        yield return Animate(0.25f, t =>
        {
            if (_dimImage != null) _dimImage.color = new Color(0f, 0f, 0f, DimAlpha * (1f - t));
            if (_titleText != null) _titleText.alpha = titleA * (1f - t);
            if (_tapText != null) _tapText.color = new Color(1f, 1f, 1f, tapA * (1f - t));
        });
        if (_dimImage != null) _dimImage.raycastTarget = false;   // uçuş sırasında menü dokunulabilir

        int flying = 0;
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] == null) continue;
            flying++;
            StartCoroutine(FlyToTarget(_cells[i], i, i * FlyStagger, () => flying--));
        }
        while (flying > 0) yield return null;
        while (_punching.Count > 0) yield return null;   // yarım punch hedefi büyük bırakmasın
        Finish(destroy: true);
    }

    private IEnumerator FlyToTarget(RectTransform cell, int index, float delay, Action done)
    {
        var root = (RectTransform)transform;
        Vector2 from = root.InverseTransformPoint(cell.position);
        cell.SetParent(root, false);
        cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 0.5f);
        cell.anchoredPosition = from;

        float w = 0f;
        while (w < delay) { w += Time.unscaledDeltaTime; yield return null; }

        var target = _collect.TargetFor?.Invoke(index);
        Vector2 to = target != null
            ? (Vector2)root.InverseTransformPoint(target.position)
            : new Vector2(0f, -root.rect.height * 0.38f);
        Vector2 control = (from + to) * 0.5f + new Vector2(0f, 260f);   // yukarı kavis

        yield return Animate(FlyDuration, t =>
        {
            float e = t * t * (3f - 2f * t);
            Vector2 a = Vector2.LerpUnclamped(from, control, e);
            Vector2 b = Vector2.LerpUnclamped(control, to, e);
            cell.anchoredPosition = Vector2.LerpUnclamped(a, b, e);
            cell.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, e);
        });

        cell.gameObject.SetActive(false);
        _collect.OnLanded?.Invoke(index);
        if (target != null && _punching.Add(target)) StartCoroutine(Punch(target));
        done();
    }

    // Hedef "yakaladı": hafif çöküp zıplar. Aynı hedefe üst üste gelenler punch'ı yeniden başlatmaz
    // (taban ölçek kaymasın).
    private IEnumerator Punch(RectTransform target)
    {
        Vector3 baseScale = target.localScale;
        const float duration = 0.28f;
        float t = 0f;
        while (t < duration && target != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = k < 0.35f ? Mathf.Lerp(1f, 0.88f, k / 0.35f)
                    : k < 0.7f  ? Mathf.Lerp(0.88f, 1.14f, (k - 0.35f) / 0.35f)
                                : Mathf.Lerp(1.14f, 1f, (k - 0.7f) / 0.3f);
            target.localScale = baseScale * s;
            yield return null;
        }
        if (target != null) target.localScale = baseScale;
        _punching.Remove(target);
    }

    private IEnumerator Close()
    {
        yield return Animate(0.2f, t => _group.alpha = 1f - t);
        Finish(destroy: true);
    }

    private void Finish(bool destroy)
    {
        if (_finished) return;
        _finished = true;
        var cb = _onDone;
        _onDone = null;
        if (destroy)
        {
            if (_instance == this) _instance = null;
            Destroy(gameObject);
        }
        cb?.Invoke();
    }

    // ── Yardımcılar ─────────────────────────────────────────────────

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds && !_skipRequested) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static IEnumerator Animate(float duration, Action<float> apply)
    {
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            apply(Mathf.Clamp01(t / d));
            yield return null;
        }
        apply(1f);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    private static float EaseInOut(float t) => t * t * (3f - 2f * t);

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
