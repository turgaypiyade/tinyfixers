using UnityEngine;


[CreateAssetMenu(menuName = "CoreCollapse/Tile Icon Library")]
public class TileIconLibrary : ScriptableObject
{
    public Sprite gear;
    public Sprite core;
    public Sprite bolt;
    public Sprite plate;
    public Sprite key;

    [Header("Deneme: Alet taşları (2026-11-14) — yalnız editör + Development Build")]
    [Tooltip("Sarı baret / kırmızı koni / mavi somun / yeşil metre. Mağaza sürümünde HİÇ kullanılmaz; " +
             "karar verilince gear/core/bolt/plate'e taşınır.")]
    public Sprite toolGear;
    public Sprite toolCore;
    public Sprite toolBolt;
    public Sprite toolPlate;
    [Tooltip("Deneme roketi (magenta): taş ikonu, 5'er dönme karesi, uçuş yarımları + alevli roket.")]
    public Sprite toolLineH;
    public Sprite toolLineV;
    public Sprite[] toolLineHSpin;
    public Sprite[] toolLineVSpin;
    public Sprite toolRocketLeft;
    public Sprite toolRocketRight;
    public Sprite toolSplitLeft;
    public Sprite toolSplitRight;
    [Tooltip("Deneme Override: tam küre, iki yarım, dönme kareleri (üst 8 / alt 6) + karelerin tuvale göre oranları.")]
    public Sprite toolOverride;
    public Sprite toolOverrideUp;
    public Sprite toolOverrideDown;
    public Sprite[] toolOverrideTurnTop;
    public Sprite[] toolOverrideTurnBottom;
    public Vector2 toolOverrideTopTurnSize;
    public Vector2 toolOverrideTopTurnOffset;
    public Vector2 toolOverrideBottomTurnSize;
    public Vector2 toolOverrideBottomTurnOffset;

    /// Deneme Override seti tam ve açık mı?
    public bool ToolOverrideReady => ToolTilesActive && toolOverride != null && toolOverrideUp != null && toolOverrideDown != null
        && toolOverrideTurnTop != null && toolOverrideTurnTop.Length > 0 && toolOverrideTurnBottom != null && toolOverrideTurnBottom.Length > 0;

    public const string ToolTilesPrefKey = "debug_tool_tiles";
    private static int s_toolTiles = -1;   // -1 = okunmadı; hot path'te PlayerPrefs okunmaz

    /// Deneme alet taşları açık mı? Mağaza sürümünde her zaman kapalı. Test cihazında varsayılan KAPALI (debug panelinden açılır).
    public static bool ToolTilesActive
    {
        get
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return false;
            if (s_toolTiles < 0) s_toolTiles = PlayerPrefs.GetInt(ToolTilesPrefKey, 0);   // 2026-11-15: alet taşlarından vazgeçildi → varsayılan KAPALI
            return s_toolTiles == 1;
        }
        set
        {
            s_toolTiles = value ? 1 : 0;
            PlayerPrefs.SetInt(ToolTilesPrefKey, s_toolTiles);
            PlayerPrefs.Save();
        }
    }

    private Sprite Pick(Sprite normal, Sprite tool) => tool != null && ToolTilesActive ? tool : normal;

    /// Deneme seti açıksa roket dönme kareleri; değilse null (TileView kendi karelerini kullanır).
    public static Sprite[] ToolLineSpinFrames(TileSpecial special)
    {
        if (!ToolTilesActive) return null;
        var lib = Shared;
        if (lib == null) return null;
        var f = special == TileSpecial.LineH ? lib.toolLineHSpin : special == TileSpecial.LineV ? lib.toolLineVSpin : null;
        return f != null && f.Length > 0 && f[0] != null ? f : null;
    }

    /// Elle girilmiş eski taş görseli (seviye hedefi iconOverride) → deneme seti açıksa karşılığı.
    public static Sprite MapTileSprite(Sprite sprite)
    {
        if (sprite == null || !ToolTilesActive) return sprite;
        var lib = Shared;
        if (lib == null) return sprite;
        if (sprite == lib.gear) return lib.Get(TileType.Gear);
        if (sprite == lib.core) return lib.Get(TileType.Core);
        if (sprite == lib.bolt) return lib.Get(TileType.Bolt);
        if (sprite == lib.plate) return lib.Get(TileType.Plate);
        return sprite;
    }

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
    [Tooltip("Wonder Boya Kovası (bir taş türünü 3 hamle tahtadan çıkarır).")]
    public Sprite boosterPaintBucket;

    [Header("Currency Icons")]
    [Tooltip("Altın ödülü (patili altın). Boşken ödül ekranları ikon yerine amber kare çizer.")]
    public Sprite coin;
    [Tooltip("Can ödülü (kalp).")]
    public Sprite life;
    [Tooltip("Yıldız ödülü.")]
    public Sprite star;

    [Header("Goal Icons")]
    [Tooltip("'Engel kır' görev hedefi ikonu (tahta engeli, ör. tahta sandık). Joker/booster görseli KULLANMA: " +
             "hedef ikonu ödül ikonuyla karışır (çekiç hedefi → çekiç ödülü görünür).")]
    public Sprite obstacleGoal;

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
                return Pick(lineH, toolLineH);

            case TileSpecial.LineV:
                return Pick(lineV, toolLineV);

            case TileSpecial.PatchBot:
                return patchBotAlone;

            case TileSpecial.PulseCore:
                return pulseCore;

            case TileSpecial.SystemOverride:
                return Pick(overrideRed, toolOverride); // default fallback — callers should use GetOverrideIcon for color-keyed

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
        if (toolOverride != null && ToolTilesActive) return toolOverride;
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
            TileType.Gear => Pick(gear, toolGear),
            TileType.Core => Pick(core, toolCore),
            TileType.Bolt => Pick(bolt, toolBolt),
            TileType.Plate => Pick(plate, toolPlate),
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
            case BoardController.BoosterMode.PaintBucket: return boosterPaintBucket;
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
            case DailySlotRewardType.Booster_PaintBucket:  return boosterPaintBucket;

            case DailySlotRewardType.Coins:                return coin;
            case DailySlotRewardType.Lives:                return life;
            case DailySlotRewardType.Stars:                return star;

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
            case WorkshopRewardType.Coins:                return coin;

            default:                                      return null;
        }
    }
}
