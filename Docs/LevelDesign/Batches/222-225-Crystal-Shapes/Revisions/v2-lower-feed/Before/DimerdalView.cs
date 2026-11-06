using UnityEngine;
using UnityEngine.UI;

/// Layered shutter: allocate once per host, move a single masked panel per hit.
public sealed class DimerdalView : MonoBehaviour
{
    private RectTransform panel;
    private float shown, from, target, elapsed;
    private bool animating;
    private Image diamond;
    private readonly Image[] slats = new Image[4];

    public static DimerdalView Ensure(Image host, ObstacleDef def, int remaining)
    {
        if (!host.TryGetComponent<DimerdalView>(out var view))
            view = host.gameObject.AddComponent<DimerdalView>();
        host.sprite = def.auxiliarySprites[0];
        host.preserveAspect = false;
        if (view.panel == null)
        {
            view.diamond = NewImage("DimerdalDiamond", host.rectTransform);
            var gem = view.diamond.rectTransform;
            gem.anchorMin = new Vector2(.22f, .23f);
            gem.anchorMax = new Vector2(.78f, .77f);
            gem.sizeDelta = gem.anchoredPosition = Vector2.zero;
            view.diamond.preserveAspect = true;

            var mask = new GameObject("DimerdalAperture", typeof(RectTransform), typeof(RectMask2D));
            var rt = (RectTransform)mask.transform;
            rt.SetParent(host.transform, false);
            rt.anchorMin = new Vector2(.108f, .132f);
            rt.anchorMax = new Vector2(.892f, .885f);
            rt.sizeDelta = rt.anchoredPosition = Vector2.zero;
            view.panel = (RectTransform)new GameObject("ShutterPanel", typeof(RectTransform)).transform;
            view.panel.SetParent(rt, false);
            view.panel.sizeDelta = view.panel.anchoredPosition = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                var image = NewImage("Slat" + i, view.panel);
                image.rectTransform.anchorMin = new Vector2(0, (3 - i) * .25f);
                image.rectTransform.anchorMax = new Vector2(1, (4 - i) * .25f);
                image.rectTransform.sizeDelta = image.rectTransform.anchoredPosition = Vector2.zero;
                view.slats[i] = image;
            }
        }
        view.diamond.sprite = def.auxiliarySprites[1];
        for (int i = 0; i < 4; i++) view.slats[i].sprite = def.auxiliarySprites[2];
        view.SetRemaining(remaining, false);
        return view;
    }

    private static Image NewImage(string name, RectTransform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.raycastTarget = false;
        return image;
    }

    public void SetRemaining(int remaining, bool animate)
    {
        target = 1f - Mathf.Clamp(remaining, 0, 4) * .25f;
        from = shown;
        elapsed = 0;
        animating = animate && isActiveAndEnabled && !Mathf.Approximately(shown, target);
        if (!animating) Apply(target);
    }

    public bool Tick(float delta)
    {
        if (!animating || !isActiveAndEnabled) return false;
        elapsed += delta;
        float t = Mathf.Clamp01(elapsed / .22f);
        Apply(Mathf.Lerp(from, target, t * t * (3 - 2 * t)));
        animating = t < 1;
        return animating;
    }

    private void Apply(float value)
    {
        shown = value;
        if (panel == null) return;
        panel.anchorMin = new Vector2(0, value);
        panel.anchorMax = new Vector2(1, 1 + value);
    }

    private void OnDisable() { animating = false; Apply(target); }
}
