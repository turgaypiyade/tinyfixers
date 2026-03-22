using UnityEngine;

public enum TileSpecial
{
    None,
    LineH,
    LineV,
    PatchBot,
    PulseCore,       // L/T or 5-cluster
    SystemOverride   // 5 straight
}

public class TileModel : MonoBehaviour
{
    [Header("Base Type (normal gem tipi)")]
    public TileType type;

    [Header("Special / Power")]
    public bool isPower;

    public TileSpecial special = TileSpecial.None;
    public TileType overrideBaseType;
    public bool hasOverrideBaseType;

    /// <summary>
    /// Bu tile special mı?
    /// </summary>
    public bool IsSpecial => special != TileSpecial.None;

    /// <summary>
    /// Yatay/Dikey line mı?
    /// </summary>
    public bool IsLine => special == TileSpecial.LineH || special == TileSpecial.LineV;

    /// <summary>
    /// Tile'ı normal hale getirir.
    /// </summary>
    public void ClearSpecial()
    {
        special = TileSpecial.None;
        isPower = false;
        hasOverrideBaseType = false;
    }

    /// <summary>
    /// Tile'a special verir (isPower otomatik true olur).
    /// </summary>
    public void SetSpecial(TileSpecial newSpecial)
    {
        special = newSpecial;
        isPower = (special != TileSpecial.None);
        if (special != TileSpecial.SystemOverride)
            hasOverrideBaseType = false;
    }

    public void SetOverrideBaseType(TileType type)
    {
        overrideBaseType = type;
        hasOverrideBaseType = true;
    }

    public bool TryGetOverrideBaseType(out TileType type)
    {
        type = overrideBaseType;
        return hasOverrideBaseType;
    }

    /// <summary>
    /// Base type değiştirir (special'ı değiştirmez).
    /// </summary>
    public void SetType(TileType newType)
    {
        type = newType;
    }

#if UNITY_EDITOR
    // Inspector'da elle oynandığında bile tutarlılık bozulmasın diye.
    private void OnValidate()
    {
        // Kural: special varsa isPower true olmalı; special yoksa false olmalı.
        bool shouldBePower = (special != TileSpecial.None);
        if (isPower != shouldBePower)
            isPower = shouldBePower;
    }
#endif
}
