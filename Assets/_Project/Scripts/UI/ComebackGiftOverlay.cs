using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Geri dönüş hediyesi töreni (kullanıcı 2026-11-18, Royal Match "Kraliyet Hediyesi" referansı): karartma → başlık
/// harf harf "domino" gibi ezilip uzayarak yerine oturur → alt yazı → hediyeler tek tek zıplayarak dizilir → dokun.
/// Dokunuş hediyeyi başlatır (ComebackBoost.Claim; varsa WelcomeBackReward altını) ve kapanır. Koddan kurulur,
/// prefab yok. Animasyonlar yalnız giriş boyunca çalışır; bekleme anında her kare iş yapılmaz.
/// </summary>
public sealed class ComebackGiftOverlay : MonoBehaviour
{
    private static ComebackGiftOverlay _instance;
    public static bool IsShowing => _instance != null;

    private const float DimAlpha = 0.86f;
    private const float FadeIn = 0.25f;
    private const float GiftStagger = 0.12f;
    private const float GiftPop = 0.32f;
    private const int Columns = 3;
    private static readonly Vector2 Cell = new Vector2(300f, 260f);
    private static readonly Color TitleSurface = new Color(0.72f, 0.30f, 0.05f);   // turuncu-kahve kontur

    private IReadOnlyList<DailySlotReward> _gifts;
    private Action _onClaim;
    private CanvasGroup _group;
    private TMP_Text _title, _subtitle, _tapText;
    private RectTransform _grid;
    private readonly List<RectTransform> _tiles = new List<RectTransform>();
    private bool _ready, _closing;

    public static void Show(IReadOnlyList<DailySlotReward> gifts, Action onClaim)
    {
        if (_instance != null || gifts == null || gifts.Count == 0) return;
        var go = new GameObject("ComebackGiftOverlay", typeof(RectTransform));
        _instance = go.AddComponent<ComebackGiftOverlay>();
        _instance._gifts = gifts;
        _instance._onClaim = onClaim;
        _instance.Build();
        _instance.StartCoroutine(_instance.Run());
    }

    private void OnDestroy() { if (_instance == this) _instance = null; }

    // ── Kurulum ──────────────────────────────────────────────────────

    private void Build()
    {
        gameObject.layer = 5;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 880;   // menü + öğretici (850) üstü, yükleme (999) altı
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        var dim = NewRect("Dim", transform);
        dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, DimAlpha);
        var tap = dim.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(HandleTap);

        _title = NewText("Title", new Vector2(0f, 640f), new Vector2(980f, 170f), 104f);
        _title.text = GameLocalization.GetOr("comeback_gift_title", "Hoş Geldin Hediyesi!");
        TextSurfaceStyle.Apply(_title, TitleSurface);

        _subtitle = NewText("Subtitle", new Vector2(0f, 500f), new Vector2(900f, 140f), 54f);
        _subtitle.text = GameLocalization.GetOr("comeback_gift_subtitle", "Tamir ekibinden birkaç hediye!");
        TextSurfaceStyle.Apply(_subtitle, new Color(0.15f, 0.15f, 0.2f));
        _subtitle.alpha = 0f;

        _grid = NewRect("Gifts", transform);
        _grid.anchoredPosition = new Vector2(0f, 60f);
        int rows = Mathf.CeilToInt(_gifts.Count / (float)Columns);
        for (int i = 0; i < _gifts.Count; i++)
        {
            int row = i / Columns, inRow = Mathf.Min(Columns, _gifts.Count - row * Columns), col = i % Columns;
            float x = (col - (inRow - 1) * 0.5f) * Cell.x;
            float y = ((rows - 1) * 0.5f - row) * Cell.y;
            _tiles.Add(BuildTile(_gifts[i], new Vector2(x, y)));
        }

        _tapText = NewText("Tap", new Vector2(0f, -700f), new Vector2(900f, 90f), 46f);
        _tapText.text = GameLocalization.Get("common_tap_continue");
        TextSurfaceStyle.Apply(_tapText, new Color(0.15f, 0.15f, 0.2f));
        _tapText.alpha = 0f;
    }

    private RectTransform BuildTile(DailySlotReward gift, Vector2 pos)
    {
        var tile = NewRect("Gift", _grid);
        tile.sizeDelta = Cell;
        tile.anchoredPosition = pos;
        tile.localScale = Vector3.zero;

        var icon = NewRect("Icon", tile);
        icon.sizeDelta = new Vector2(180f, 180f);
        icon.anchoredPosition = new Vector2(0f, 30f);
        var img = icon.gameObject.AddComponent<Image>();
        img.sprite = gift.ResolveIcon();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = img.sprite != null;

        var label = NewTextUnder(tile, "Amount", new Vector2(0f, -95f), new Vector2(Cell.x, 70f), 50f);
        label.text = gift.IsTimed ? gift.AmountLabel : Mathf.Max(1, gift.amount).ToString();
        TextSurfaceStyle.Apply(label, new Color(0.62f, 0.08f, 0.10f));   // kırmızı kurdele koyusu
        return tile;
    }

    // ── Akış ─────────────────────────────────────────────────────────

    private IEnumerator Run()
    {
        for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
        {
            _group.alpha = t / FadeIn;
            yield return null;
        }
        _group.alpha = 1f;

        yield return TitleLetterPop.Play(_title);
        yield return FadeText(_subtitle, 0.25f);
        for (int i = 0; i < _tiles.Count; i++)
        {
            StartCoroutine(PopTile(_tiles[i]));
            yield return new WaitForSecondsRealtime(GiftStagger);
        }
        yield return new WaitForSecondsRealtime(GiftPop);
        yield return FadeText(_tapText, 0.25f);
        _ready = true;
    }

    private static IEnumerator PopTile(RectTransform tile)
    {
        for (float t = 0f; t < GiftPop; t += Time.unscaledDeltaTime)
        {
            float k = t / GiftPop;
            tile.localScale = Vector3.one * EaseOutBack(k);
            yield return null;
        }
        tile.localScale = Vector3.one;
    }

    private static float EaseOutBack(float k)
    {
        const float s = 1.9f;
        k -= 1f;
        return k * k * ((s + 1f) * k + s) + 1f;
    }

    private static IEnumerator FadeText(TMP_Text text, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            text.alpha = t / duration;
            yield return null;
        }
        text.alpha = 1f;
    }

    private void HandleTap()
    {
        if (!_ready || _closing) return;
        _closing = true;
        _onClaim?.Invoke();
        StartCoroutine(Close());
    }

    private IEnumerator Close()
    {
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            _group.alpha = 1f - t / 0.2f;
            yield return null;
        }
        Destroy(gameObject);
    }

    // ── Yardımcılar ──────────────────────────────────────────────────

    private RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private TMP_Text NewText(string name, Vector2 pos, Vector2 size, float fontSize)
        => NewTextUnder(transform, name, pos, size, fontSize);

    private TMP_Text NewTextUnder(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize)
    {
        var rt = NewRect(name, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = fontSize;
        tmp.fontSizeMin = fontSize * 0.5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }
}
