using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Layered press presentation. Actual hit rules and the outgoing blast remain board-owned.</summary>
[DisallowMultipleComponent]
public sealed class GelLauncherPressView : MonoBehaviour
{
    private static readonly List<GelLauncherPressView> moving = new(16);
    private static GelLauncherPressDriver driver;
    private static Sprite frameSprite, headSprite;
    private static Texture2D gelTexture, headTexture;
    private static Material rodMaterial;
    private static int owners;
    private readonly GelLauncherPressMotion motion = new();
    private RectTransform visual, chamber, gel, head, rod;
    private GelLauncherPressRodGraphic stripes;
    private CanvasGroup opacity;
    private GameObject cracks;
    private bool built, registered, firing;
    private int generation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        moving.Clear();
        driver = null;
        owners = 0;
        frameSprite = null;
        gelTexture = headTexture = null;
        rodMaterial = null;
        if (headSprite != null) Object.Destroy(headSprite);
        headSprite = null;
    }

    public Sprite FrameSprite => frameSprite;
    public Sprite HeadSprite => headSprite;
    public bool IsFiring => firing;

    public static GelLauncherPressView Ensure(Image image, ObstacleId id, int remainingHits)
    {
        if (image == null || !LoadAssets()) return null;
        var view = image.GetComponent<GelLauncherPressView>();
        if (view == null) view = image.gameObject.AddComponent<GelLauncherPressView>();
        if (!view.built) view.Build(image);
        view.Configure(image, id, remainingHits);
        return view;
    }

    private static bool LoadAssets()
    {
        if (frameSprite == null) frameSprite = Resources.Load<Sprite>("GelLauncher/Press/PressFrame");
        if (gelTexture == null) gelTexture = Resources.Load<Texture2D>("GelLauncher/Press/PressGel");
        if (headTexture == null) headTexture = Resources.Load<Texture2D>("GelLauncher/Press/PressHead");
        if (rodMaterial == null) rodMaterial = Resources.Load<Material>("GelLauncher/Press/PressRod");
        if (frameSprite == null || gelTexture == null || headTexture == null || rodMaterial == null) return false;
        if (headSprite == null)
        {
            // Crop in sprite UVs; the supplied source PNG stays byte-for-byte unchanged.
            headSprite = Sprite.Create(headTexture, new Rect(65, 339, 760, 385), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            headSprite.name = "GelPressHead_Runtime";
        }
        return true;
    }

    private void Build(Image image)
    {
        built = true;
        owners++;
        visual = Rect("PressVisual", image.rectTransform);
        opacity = visual.gameObject.AddComponent<CanvasGroup>();
        opacity.blocksRaycasts = false;
        opacity.interactable = false;
        chamber = Rect("Chamber", visual);
        chamber.anchorMin = new Vector2(0.205f, 0.135f);
        chamber.anchorMax = new Vector2(0.805f, 0.88f);
        chamber.gameObject.AddComponent<RectMask2D>();
        Image well = Flat("ChamberBack", chamber, new Color(0.10f, 0.025f, 0.21f, 0.94f));
        Stretch(well.rectTransform);
        var liquid = Rect("Gel", chamber).gameObject.AddComponent<RawImage>();
        liquid.texture = gelTexture;
        // Ignore the source's transparent margins and export residue outside the liquid.
        liquid.uvRect = new Rect(82f / 887f, 99f / 1774f, 730f / 887f, 995f / 1774f);
        liquid.raycastTarget = false;
        gel = liquid.rectTransform;
        Bottom(gel);
        head = Rect("PressurePlate", chamber);
        var plate = head.gameObject.AddComponent<Image>();
        plate.sprite = headSprite;
        plate.preserveAspect = true;
        plate.raycastTarget = false;
        Bottom(head);
        rod = Rect("StripedRod", chamber);
        stripes = rod.gameObject.AddComponent<GelLauncherPressRodGraphic>();
        stripes.material = rodMaterial;
        stripes.color = Color.white;
        stripes.raycastTarget = false;
        Bottom(rod);
        rod.SetSiblingIndex(head.GetSiblingIndex());
        var left = Flat("GlassReflectionLeft", chamber, new Color(1f, 0.91f, 1f, 0.12f));
        left.rectTransform.anchorMin = new Vector2(0.03f, 0.02f);
        left.rectTransform.anchorMax = new Vector2(0.065f, 0.98f);
        var right = Flat("GlassReflectionRight", chamber, new Color(1f, 0.91f, 1f, 0.07f));
        right.rectTransform.anchorMin = new Vector2(0.96f, 0.02f);
        right.rectTransform.anchorMax = new Vector2(0.978f, 0.98f);
        cracks = Rect("PressureCracks", chamber).gameObject;
        for (int i = 0; i < 3; i++)
        {
            var line = Flat("Crack" + i, cracks.transform as RectTransform, new Color(0.92f, 0.76f, 1f, 0.9f)).rectTransform;
            Bottom(line);
            line.anchorMin = line.anchorMax = new Vector2(0.62f - i * 0.15f, 0.30f + i * 0.13f);
            line.sizeDelta = new Vector2(12f, 1.4f);
            line.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 38f : -42f);
        }
        cracks.SetActive(false);
        var frame = Rect("PurpleFrame", visual).gameObject.AddComponent<Image>();
        frame.sprite = frameSprite;
        frame.raycastTarget = false;
        frame.preserveAspect = false;
        var canvas = image.canvas;
        if (canvas != null) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
    }

    private void Configure(Image image, ObstacleId id, int remainingHits)
    {
        CancelMotion();
        generation++;
        firing = false;
        opacity.alpha = 1f;
        cracks.SetActive(false);
        // Source is authored 1x2. Rotate the entire assembled rig only for launcher direction.
        var host = image.rectTransform.rect;
        bool vertical = GelLauncherFx.IsVertical(id);
        visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(0.5f, 0.5f);
        visual.anchoredPosition = Vector2.zero;
        visual.sizeDelta = vertical ? new Vector2(host.width, host.height) : new Vector2(host.height, host.width);
        float angle = id == ObstacleId.GelLauncherDown ? 180f : id == ObstacleId.GelLauncherRight ? -90f : id == ObstacleId.GelLauncherLeft ? 90f : 0f;
        visual.localRotation = Quaternion.Euler(0f, 0f, angle);
        image.sprite = frameSprite; // Existing shatter helper can use the purple frame as its source.
        image.enabled = false;
        motion.Reset(remainingHits <= 1);
        ApplyPose();
    }

    public void FirstHit()
    {
        if (firing || registered || motion.Progress >= 0.5f) return;
        motion.MoveTo(0.5f, 0.32f, 1.25f);
        Track();
    }

    public IEnumerator CompressForFire()
    {
        int version = generation;
        firing = true;
        // Two hits in the same burst still pass through a readable half-pressed pose.
        if (motion.Progress < 0.5f)
        {
            motion.MoveTo(0.5f, 0.13f, 0.65f);
            Track();
            while (IsCurrent(version) && motion.Moving) yield return null;
        }
        if (!IsCurrent(version)) yield break;
        motion.MoveTo(1f, 0.30f, 1.75f);
        Track();
        while (IsCurrent(version) && motion.Moving) yield return null;
        if (!IsCurrent(version)) yield break;
        cracks.SetActive(true);
        yield return new WaitForSeconds(0.065f);
    }

    public void SetOpacity(float value) { if (opacity != null) opacity.alpha = Mathf.Clamp01(value); }
    private bool IsCurrent(int version) => this != null && isActiveAndEnabled && generation == version;

    private void ApplyPose()
    {
        if (chamber == null || gel == null || head == null || rod == null || stripes == null) return;
        float w = chamber.rect.width, h = chamber.rect.height;
        float liquidHeight = h * Mathf.Lerp(0.63f, 0.14f, motion.Progress);
        gel.sizeDelta = new Vector2(w, liquidHeight);
        float headWidth = w * 0.96f, headHeight = headWidth * 385f / 760f;
        head.sizeDelta = new Vector2(headWidth, headHeight);
        head.anchoredPosition = new Vector2(0f, liquidHeight - headHeight * 0.025f);
        float rodBottom = liquidHeight + headHeight * 0.92f;
        rod.sizeDelta = new Vector2(w * 0.26f, Mathf.Max(1f, h - rodBottom + headHeight * 0.1f));
        rod.anchoredPosition = new Vector2(0f, rodBottom);
        stripes.SetPhase(motion.Phase);
    }

    private void Track()
    {
        if (!isActiveAndEnabled || registered) return;
        registered = true;
        moving.Add(this);
        if (driver == null)
            driver = new GameObject("GelPressMotionDriver").AddComponent<GelLauncherPressDriver>();
    }

    internal static void Tick(float delta)
    {
        for (int i = moving.Count - 1; i >= 0; i--)
        {
            var view = moving[i];
            if (view == null || !view.isActiveAndEnabled)
            {
                if (view != null) view.registered = false;
                moving.RemoveAt(i);
                continue;
            }
            view.motion.Step(delta);
            view.ApplyPose();
            if (!view.motion.Moving) { view.registered = false; moving.RemoveAt(i); }
        }
    }

    internal static void DriverDestroyed(GelLauncherPressDriver old)
    {
        if (driver != old) return;
        driver = null;
        for (int i = 0; i < moving.Count; i++)
            if (moving[i] != null) { moving[i].registered = false; moving[i].motion.Cancel(); }
        moving.Clear();
    }

    private void CancelMotion()
    {
        motion.Cancel();
        if (registered) moving.Remove(this);
        registered = false;
    }
    private void OnDisable() { generation++; CancelMotion(); }
    private void OnDestroy()
    {
        CancelMotion();
        if (!built) return;
        owners = Mathf.Max(0, owners - 1);
        if (owners == 0 && headSprite != null) { Destroy(headSprite); headSprite = null; }
    }
    private void OnRectTransformDimensionsChange() { if (built) ApplyPose(); }

    private static RectTransform Rect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);
        return rt;
    }
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
    private static void Bottom(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
    }
    private static Image Flat(string name, RectTransform parent, Color tint)
    {
        var img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.color = tint; img.raycastTarget = false;
        return img;
    }
}

public sealed class GelLauncherPressDriver : MonoBehaviour
{
    private void LateUpdate() => GelLauncherPressView.Tick(Time.deltaTime);
    private void OnDestroy() => GelLauncherPressView.DriverDestroyed(this);
}
