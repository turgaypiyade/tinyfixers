using System;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Owns one UI material instance; the source sprite stays untouched and non-readable.</summary>
public sealed class BridgeRepairRiverFlow : IDisposable
{
    private static readonly int FlowClock = Shader.PropertyToID("_FlowClock");
    private static readonly int FlowStrength = Shader.PropertyToID("_FlowStrength");
    private static readonly int SpriteUV = Shader.PropertyToID("_SpriteUV");
    private static readonly int SpritePixelSize = Shader.PropertyToID("_SpritePixelSize");
    private readonly Image image;
    private readonly Material previousMaterial;
    private readonly Material material;
    private readonly Sprite sprite;
    private float clock;

    public BridgeRepairRiverFlow(Image image)
    {
        this.image = image;
        if (image == null) return;
        sprite = image.overrideSprite;
        if (sprite == null) return;
        // This mask is authored for this exact image, not for arbitrary replacement backgrounds.
        if (!sprite.name.StartsWith("BridgeRepairBG", StringComparison.Ordinal)) return;
        if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None) return;
        var template = Resources.Load<Material>("Events/BridgeRepairRiver");
        if (template == null || template.shader == null || !template.shader.isSupported)
        {
            Debug.LogWarning("[BridgeRepair] River material unavailable; using the original background.");
            return;
        }
        previousMaterial = image.material;
        material = new Material(template)
        {
            name = "BridgeRepairRiver (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        material.SetVector(SpriteUV, DataUtility.GetOuterUV(sprite));
        material.SetVector(SpritePixelSize, new Vector4(sprite.rect.width, sprite.rect.height, 0f, 0f));
    }

    public void Tick(float deltaTime, bool enabled, float speed, float strength)
    {
        if (image == null || material == null) return;
        if (!enabled || strength <= 0f || image.overrideSprite != sprite)
        {
            Restore();
            return;
        }
        if (image.material != material) image.material = material;
        // Both wave frequencies and the 6-second flow period repeat at 600 seconds.
        clock = Mathf.Repeat(clock + Mathf.Max(0f, deltaTime) * Mathf.Clamp(speed, 0f, 3f), 600f);
        ApplyParameters(material, strength);
        // UGUI may use a stencil copy under a Mask. Keep its clock in sync as well.
        var rendered = image.materialForRendering;
        if (rendered != null && rendered != material) ApplyParameters(rendered, strength);
    }

    private void ApplyParameters(Material target, float strength)
    {
        target.SetFloat(FlowClock, clock);
        target.SetFloat(FlowStrength, Mathf.Clamp(strength, 0f, 2f));
    }

    private void Restore()
    {
        if (image != null && material != null && image.material == material)
            image.material = previousMaterial;
    }

    public void Dispose()
    {
        Restore();
        if (material != null) UnityEngine.Object.Destroy(material);
    }
}
