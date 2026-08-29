using UnityEngine;


[CreateAssetMenu(menuName = "CoreCollapse/Tile Icon Library")]
public class TileIconLibrary : ScriptableObject
{
    public Sprite gear;
    public Sprite core;
    public Sprite bolt;
    public Sprite plate;
    public Sprite key;

    [Header("Special Tile Icons")]
    public Sprite lineH;
    public Sprite lineV;
    public Sprite patchBotAlone;
    public Sprite patchBotAll;
    public Sprite pulseCore;

    [Header("Override Icons (color-keyed)")]
    public Sprite overrideRed;
    public Sprite overrideYellow;
    public Sprite overrideBlue;
    public Sprite overrideGreen;

    [Header("Booster Icons")]
    [Tooltip("Hammer / Single booster (tek hücre).")]
    public Sprite boosterHammer;
    [Tooltip("Row booster (yatay satır).")]
    public Sprite boosterRow;
    [Tooltip("Column / Mini Elevator booster (dikey sütun).")]
    public Sprite boosterColumn;
    [Tooltip("Shuffle booster (tahtayı karıştır).")]
    public Sprite boosterShuffle;

    // ─────────────────────────────────────────────────────────────────
    // Global erişim: event/reward/menü UI'ları serialize ref olmadan ikon çözebilsin.
    // Asset Resources kökünde (TileIconLibrary_Main) → her sahnede güvenilir yüklenir.
    // ─────────────────────────────────────────────────────────────────
    private const string ResourcePath = "TileIconLibrary_Main";
    private static TileIconLibrary _shared;

    public static TileIconLibrary Shared
    {
        get
        {
            if (_shared != null) return _shared;

            _shared = Resources.Load<TileIconLibrary>(ResourcePath);

#if UNITY_EDITOR
            if (_shared == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("t:TileIconLibrary");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _shared = UnityEditor.AssetDatabase.LoadAssetAtPath<TileIconLibrary>(path);
                }
            }
#endif
            if (_shared == null)
            {
                var all = Resources.FindObjectsOfTypeAll<TileIconLibrary>();
                if (all != null && all.Length > 0) _shared = all[0];
            }

            return _shared;
        }
    }

    private void OnEnable()
    {
        if (_shared == null) _shared = this;
    }

    public Sprite GetSpecialIcon(TileSpecial special)
    {
        switch (special)
        {
            case TileSpecial.LineH:
                return lineH;

            case TileSpecial.LineV:
                return lineV;

            case TileSpecial.PatchBot:
                return patchBotAlone;

            case TileSpecial.PulseCore:
                return pulseCore;

            case TileSpecial.SystemOverride:
                return overrideRed; // default fallback — callers should use GetOverrideIcon for color-keyed

            default:
                return null;
        }
    }

    /// <summary>
    /// Returns the color-specific Override icon based on the base TileType
    /// that formed the 5-match.
    /// </summary>
    public Sprite GetOverrideIcon(TileType baseType)
    {
        switch (baseType)
        {
            case TileType.Gear:   return overrideYellow;
            case TileType.Core:   return overrideRed;
            case TileType.Bolt:   return overrideBlue;
            case TileType.Plate:  return overrideGreen;
            default:              return overrideRed;
        }
    }
    public Sprite GetPatchBotFlightIcon() => patchBotAlone;

    public Sprite GetPatchBotFullIcon() => patchBotAll != null ? patchBotAll : patchBotAlone;
    public Sprite Get(TileType type)
    {
        return type switch
        {
            TileType.Gear => gear,
            TileType.Core => core,
            TileType.Bolt => bolt,
            TileType.Plate => plate,
            TileType.Key => key,
            _ => null
        };
    }

    /// <summary>Booster ikonu (Single/Hammer, Row, Column/Elevator, Shuffle).</summary>
    public Sprite GetBoosterIcon(BoardController.BoosterMode mode)
    {
        switch (mode)
        {
            case BoardController.BoosterMode.Single:  return boosterHammer;
            case BoardController.BoosterMode.Row:     return boosterRow;
            case BoardController.BoosterMode.Column:  return boosterColumn;
            case BoardController.BoosterMode.Shuffle: return boosterShuffle;
            default:                                  return null;
        }
    }

    /// <summary>
    /// Event/daily-slot ödül ikonu. Joker'lar special ikonuna, booster'lar booster ikonuna map'lenir.
    /// Coins/Lives/Stars gibi tipler burada null döner (çağıran kendi para birimi ikonunu kullanır).
    /// </summary>
    public Sprite GetRewardIcon(DailySlotRewardType type)
    {
        switch (type)
        {
            case DailySlotRewardType.Joker_LineH:          return lineH;
            case DailySlotRewardType.Joker_Line:           return lineH; // LineH/LineV shared icon
            case DailySlotRewardType.Joker_PulseCore:      return pulseCore;
            case DailySlotRewardType.Joker_SystemOverride: return overrideRed;

            case DailySlotRewardType.Booster_Hammer:       return boosterHammer;
            case DailySlotRewardType.Booster_Row:          return boosterRow;
            case DailySlotRewardType.Booster_Column:       return boosterColumn;
            case DailySlotRewardType.Booster_Shuffle:      return boosterShuffle;

            default:                                       return null;
        }
    }

    /// <summary>Workshop ödül ikonu — joker/booster tiplerini library'den çözer.</summary>
    public Sprite GetRewardIcon(WorkshopRewardType type)
    {
        switch (type)
        {
            case WorkshopRewardType.Joker_LineH:          return lineH;
            case WorkshopRewardType.Joker_PulseCore:      return pulseCore;
            case WorkshopRewardType.Joker_SystemOverride: return overrideRed;

            case WorkshopRewardType.Booster_Hammer:       return boosterHammer;
            case WorkshopRewardType.Booster_Row:          return boosterRow;
            case WorkshopRewardType.Booster_Column:       return boosterColumn;
            case WorkshopRewardType.Booster_Shuffle:      return boosterShuffle;

            default:                                      return null;
        }
    }
}
