using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dünya haritasında bir harikaya dokununca açılan büyük görünüm: başlık (harf harf), harika resmi (Görevlerle aynı
/// onarım durumu), tamamlanmışsa "Arka plan yap". Boşluğa dokununca kapanır. Koddan kurulur.
/// </summary>
public sealed class WorldTourWonderView : MonoBehaviour
{
    private static WorldTourWonderView _instance;
    private CanvasGroup _group;
    private bool _closing;

    public static void Show(WonderCatalog catalog, int index)
    {
        var wonder = catalog != null ? catalog.Get(index) : null;
        if (wonder == null || _instance != null) return;
        var go = new GameObject("WorldTourWonderView", typeof(RectTransform));
        _instance = go.AddComponent<WorldTourWonderView>();
        _instance.Build(catalog, index, wonder);
    }

    private void OnDestroy() { if (_instance == this) _instance = null; }

    private void Build(WonderCatalog catalog, int index, WonderDefinition wonder)
    {
        gameObject.layer = 5;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 860;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        var dim = NewRect("Dim", transform);
        dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
        var closeTap = dim.gameObject.AddComponent<Button>();
        closeTap.transition = Selectable.Transition.None;
        closeTap.onClick.AddListener(Close);

        var sprite = wonder.backgroundSprite;
        float aspect = sprite != null && sprite.rect.width > 1f ? sprite.rect.height / sprite.rect.width : 16f / 9f;
        float w = 900f, h = Mathf.Min(1250f, w * aspect);
        var pic = NewRect("Wonder", transform);
        pic.sizeDelta = new Vector2(h / aspect, h);
        pic.anchoredPosition = new Vector2(0f, -20f);
        var img = pic.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        if (sprite != null) pic.gameObject.AddComponent<WonderRevealView>()
            .ShowRestorationSnapshot(wonder, WonderProgress.RevealNormalized(catalog, index));

        var title = NewText("Title", new Vector2(0f, h * 0.5f + 70f), new Vector2(980f, 140f), 88f);
        title.text = wonder.LocalizedName;
        TextSurfaceStyle.Apply(title, new Color(0.72f, 0.30f, 0.05f));
        StartCoroutine(TitleLetterPop.Play(title));

        if (WonderProgress.IsEventComplete(catalog, index))
        {
            bool inUse = WonderProgress.BackgroundIndex(catalog) == index;
            var label = NewText("Use", new Vector2(0f, -h * 0.5f - 90f), new Vector2(700f, 110f), 54f);
            label.text = GameLocalization.Get(inUse ? "journey_background_in_use" : "journey_use_background");
            TextSurfaceStyle.Apply(label, new Color(0.35f, 0.15f, 0.55f));
            if (!inUse)
            {
                label.raycastTarget = true;
                var b = label.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() =>
                {
                    WonderProgress.SelectBackground(catalog, index);
                    label.text = GameLocalization.Get("journey_background_in_use");
                });
            }
        }
        StartCoroutine(Fade(1f));
    }

    private void Close()
    {
        if (_closing) return;
        _closing = true;
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        yield return Fade(0f);
        Destroy(gameObject);
    }

    private IEnumerator Fade(float to)
    {
        float from = _group.alpha;
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            _group.alpha = Mathf.Lerp(from, to, t / 0.2f);
            yield return null;
        }
        _group.alpha = to;
    }

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
    {
        var rt = NewRect(name, transform);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = fontSize;
        tmp.fontSizeMin = fontSize * 0.5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }
}
