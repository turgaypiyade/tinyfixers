using UnityEngine;
using UnityEngine.UI;

/// One reusable visual tree per cabinet. The service ticks only active feedback.
public sealed class DimerdalView : MonoBehaviour
{
    private const float HitSeconds = .26f;
    private RectTransform visual, panel;
    private Image frame, diamond, flash;
    private readonly Image[] slats = new Image[4];
    private float shown, from, target, elapsed;
    private float hitElapsed;
    private bool animating, hitting;
    public bool IsAnimating => animating || hitting;

    public static DimerdalView Ensure(Image host, ObstacleDef def, int remaining)
    {
        if (!host.TryGetComponent<DimerdalView>(out var view))
            view = host.gameObject.AddComponent<DimerdalView>();
        // Keep the host/root fixed: flight origins, footprint and input never shake.
        host.sprite = def.auxiliarySprites[0];
        host.color = new Color(1f, 1f, 1f, 0f);
        host.preserveAspect = false;
        if (view.visual == null)
        {
            view.frame = NewImage("DimerdalFrame", host.rectTransform);
            view.visual = view.frame.rectTransform;
            view.visual.anchorMin = Vector2.zero;
            view.visual.anchorMax = Vector2.one;
            view.visual.pivot = new Vector2(.5f, .5f);
            view.visual.sizeDelta = view.visual.anchoredPosition = Vector2.zero;

            view.diamond = NewImage("DimerdalDiamond", view.visual);
            var gem = view.diamond.rectTransform;
            gem.anchorMin = new Vector2(.22f, .23f);
            gem.anchorMax = new Vector2(.78f, .77f);
            gem.sizeDelta = gem.anchoredPosition = Vector2.zero;
            view.diamond.preserveAspect = true;

            var mask = new GameObject("DimerdalAperture", typeof(RectTransform), typeof(RectMask2D));
            mask.layer = host.gameObject.layer;
            var rt = (RectTransform)mask.transform;
            rt.SetParent(view.visual, false);
            rt.anchorMin = new Vector2(.108f, .132f);
            rt.anchorMax = new Vector2(.892f, .885f);
            rt.sizeDelta = rt.anchoredPosition = Vector2.zero;
            view.panel = (RectTransform)new GameObject("ShutterPanel", typeof(RectTransform)).transform;
            view.panel.gameObject.layer = host.gameObject.layer;
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
            // Reuse a white UI quad inside the aperture; no per-hit VFX objects/textures.
            view.flash = NewImage("DimerdalHitFlash", view.visual);
            view.flash.rectTransform.anchorMin = new Vector2(.108f, .132f);
            view.flash.rectTransform.anchorMax = new Vector2(.892f, .885f);
            view.flash.rectTransform.sizeDelta = view.flash.rectTransform.anchoredPosition = Vector2.zero;
            view.flash.enabled = false;
        }
        view.frame.sprite = def.auxiliarySprites[0];
        view.diamond.sprite = def.auxiliarySprites[1];
        for (int i = 0; i < 4; i++) view.slats[i].sprite = def.auxiliarySprites[2];
        view.SetRemaining(remaining, false);
        return view;
    }

    private static Image NewImage(string name, RectTransform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.gameObject.layer = parent.gameObject.layer;
        image.rectTransform.SetParent(parent, false);
        image.raycastTarget = false;
        return image;
    }

    public void SetRemaining(int value, bool animate)
    {
        target = 1f - Mathf.Clamp(value, 0, 4) * .25f;
        from = shown;
        elapsed = 0f;
        animating = animate && isActiveAndEnabled && !Mathf.Approximately(shown, target);
        if (!animating) Apply(target);
    }

    public void PlayHit()
    {
        if (!isActiveAndEnabled || visual == null) return;
        hitElapsed = 0f;
        hitting = true;
        flash.enabled = true;
        ApplyHit(0f);
    }

    /// Capture the real colored frame for the shared pooled shatter before its host is removed.
    public Image PrepareForBreak()
    {
        StopAnimations();
        diamond.enabled = false;
        return frame;
    }

    public bool Tick(float delta)
    {
        if (!isActiveAndEnabled) { StopAnimations(); return false; }
        if (animating)
        {
            elapsed += delta;
            float t = Mathf.Clamp01(elapsed / .22f);
            Apply(Mathf.Lerp(from, target, t * t * (3f - 2f * t)));
            animating = t < 1f;
        }
        if (hitting)
        {
            hitElapsed += delta;
            float t = Mathf.Clamp01(hitElapsed / HitSeconds);
            if (t >= 1f) ResetHit();
            else ApplyHit(t);
        }
        return IsAnimating;
    }

    private void ApplyHit(float t)
    {
        float envelope = (1f - t) * (1f - t);
        float wave = Mathf.Sin(t * Mathf.PI * 6f);
        visual.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * .065f);
        visual.localRotation = Quaternion.Euler(0f, 0f, wave * 2.5f * envelope);
        visual.anchoredPosition = new Vector2(wave * visual.rect.width * .018f * envelope, 0f);
        flash.color = new Color(1f, 1f, 1f, .22f * envelope);
    }

    private void ResetHit()
    {
        hitting = false;
        if (visual != null)
        {
            visual.localScale = Vector3.one;
            visual.localRotation = Quaternion.identity;
            visual.anchoredPosition = Vector2.zero;
        }
        if (flash != null) flash.enabled = false;
    }

    private void Apply(float value)
    {
        shown = value;
        if (panel == null) return;
        panel.anchorMin = new Vector2(0f, value);
        panel.anchorMax = new Vector2(1f, 1f + value);
    }

    public void StopAnimations()
    {
        animating = false;
        Apply(target);
        ResetHit();
    }

    private void OnDisable() => StopAnimations();
}
