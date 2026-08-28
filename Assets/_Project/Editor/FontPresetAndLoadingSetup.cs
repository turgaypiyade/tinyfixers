using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click setup for:
///  1) Shared TMP material presets ("shaders") for BakBak One and Inter. Because every text using a
///     preset shares the SAME material asset, tweaking that one .mat (outline colour/width, shadow…)
///     updates EVERY text using it — that's the "change one, all change" the design wants.
///  2) Styling the live loading prefab (Assets/_Project/Prefabs/UI/LoadingHintView): removes the blank
///     background plates behind the text lines and sets the object-name/info/Loading fonts to the
///     reference cartoon look. Full-screen hint image and layout are left untouched.
///
/// Run from the menu: TinyFixers ▸ Fonts.
/// </summary>
public static class FontPresetAndLoadingSetup
{
    // ── Asset paths ──────────────────────────────────────────────────────────
    private const string BakBakFontPath   = "Assets/_Project/Fonts/BakBakOne/BakbakOne-Regular SDF.asset";
    private const string InterFontPath    = "Assets/_Project/Fonts/Inter/Static/Inter_24pt-Bold SDF.asset";
    private const string MaterialsDir     = "Assets/_Project/Fonts/Materials";
    private const string BakBakPresetPath = MaterialsDir + "/BakBakOne_Cartoon.mat";
    private const string InterPresetPath  = MaterialsDir + "/Inter_Cartoon.mat";
    // The live loading prefab (referenced by LoadingScreenPrefabProvider in the scene).
    private const string LoadingPrefabPath = "Assets/_Project/Prefabs/UI/LoadingHintView.prefab";

    // ── Cartoon style (matches the POWERBALL reference) ─────────────────────────
    private static readonly Color FaceWhite   = Color.white;
    private static readonly Color OutlineDark  = new Color(0.14f, 0.07f, 0.28f, 1f); // deep purple
    private static readonly Color ShadowDark   = new Color(0.06f, 0.02f, 0.16f, 0.55f);

    // ═══════════════════════════════════════════════════════════════════════════

    [MenuItem("TinyFixers/Fonts/Create Shared Font Presets")]
    public static void CreatePresets()
    {
        CreatePresetsInternal(out _, out _);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Fonts] Presets ready:\n • {BakBakPresetPath}\n • {InterPresetPath}\n" +
                  "Assign these as the text's Material Preset; editing the .mat updates every text using it.");
    }

    [MenuItem("TinyFixers/Fonts/Style Loading Prefab (remove panels + fonts)")]
    public static void StyleLoadingPrefab()
    {
        CreatePresetsInternal(out TMP_FontAsset bakbak, out Material bakPreset,
                              out TMP_FontAsset inter, out Material interPreset);
        if (bakbak == null)
        {
            Debug.LogError($"[Fonts] BakBak font not found at {BakBakFontPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(LoadingPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[Fonts] Loading prefab not found at {LoadingPrefabPath}");
            return;
        }

        try
        {
            // Remove the blank background plates behind the text lines (user doesn't want them).
            DestroyChild(root.transform, "TitlePanelImage");
            DestroyChild(root.transform, "DescPanelImage");
            DestroyChild(root.transform, "BottomPanelImage");

            // Data mapping: *_title = match condition (small info, top), *_desc = created object name (big, below).
            // Info (small, lowercase) — Inter, top. e.g. "4 yatay eşleşme".
            StyleText(root.transform, "TitleText",
                inter != null ? inter : bakbak, interPreset != null ? interPreset : bakPreset,
                new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(940f, 90f), 46f, 26f,
                upper: false, bold: false, lower: true);

            // Object name (big + bold + ALL CAPS) — BakBak, the reference look. e.g. "ROCKET".
            StyleText(root.transform, "DescText", bakbak, bakPreset,
                new Vector2(0.5f, 1f), new Vector2(0f, -275f), new Vector2(1000f, 240f), 120f, 60f,
                upper: true, bold: true);

            // "Loading..." — BakBak, bottom, bold.
            StyleText(root.transform, "LoadingText", bakbak, bakPreset,
                new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(860f, 160f), 88f, 44f, upper: false, bold: true);

            // Null the (now-destroyed) panel refs on the view so it won't try to re-show them.
            var view = root.GetComponent<LoadingHintView>();
            if (view != null)
            {
                var so = new SerializedObject(view);
                SetRef(so, "titlePanelImage", null);
                SetRef(so, "descriptionPanelImage", null);
                SetRef(so, "bottomPanelImage", null);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, LoadingPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Fonts] Loading prefab styled: blank text panels removed, fonts set to reference " +
                  "(BakBak name / Inter info / BakBak loading). Backgrounds & layout untouched.");
    }

    // ═══════════════════════════════════════════════════════════════════════════

    private static void CreatePresetsInternal(out Material bakPreset, out Material interPreset)
        => CreatePresetsInternal(out _, out bakPreset, out _, out interPreset);

    private static void CreatePresetsInternal(
        out TMP_FontAsset bakbak, out Material bakPreset,
        out TMP_FontAsset inter, out Material interPreset)
    {
        EnsureFolder(MaterialsDir);

        bakbak = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BakBakFontPath);
        inter  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterFontPath);

        // BakBak: thick outline + soft drop shadow (big display titles).
        bakPreset = bakbak != null
            ? CreateOrUpdatePreset(bakbak, BakBakPresetPath, FaceWhite, OutlineDark, 0.25f,
                                   ShadowDark, new Vector2(0f, -0.6f), 0.15f, 0.1f)
            : null;

        // Inter: thinner outline + faint shadow (body / general UI).
        interPreset = inter != null
            ? CreateOrUpdatePreset(inter, InterPresetPath, FaceWhite, OutlineDark, 0.15f,
                                   ShadowDark, new Vector2(0f, -0.4f), 0.1f, 0.05f)
            : null;
    }

    /// Duplicates the font's default material (keeps its atlas + shader) and applies the cartoon
    /// outline/underlay. Overwrites the existing preset in place so shared references survive.
    private static Material CreateOrUpdatePreset(
        TMP_FontAsset font, string path,
        Color face, Color outline, float outlineWidth,
        Color shadow, Vector2 shadowOffset, float shadowSoftness, float shadowDilate)
    {
        var preset = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (preset == null)
        {
            preset = new Material(font.material);
            AssetDatabase.CreateAsset(preset, path);
        }
        else
        {
            preset.shader = font.material.shader;
            preset.CopyPropertiesFromMaterial(font.material);
        }

        preset.SetColor(ShaderUtilities.ID_FaceColor, face);

        preset.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        preset.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        preset.EnableKeyword("OUTLINE_ON");

        preset.SetColor(ShaderUtilities.ID_UnderlayColor, shadow);
        preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffset.x);
        preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, shadowOffset.y);
        preset.SetFloat(ShaderUtilities.ID_UnderlaySoftness, shadowSoftness);
        preset.SetFloat(ShaderUtilities.ID_UnderlayDilate, shadowDilate);
        preset.EnableKeyword("UNDERLAY_ON");

        EditorUtility.SetDirty(preset);
        return preset;
    }

    // ── Loading prefab editing ───────────────────────────────────────────────────

    private static void StyleText(
        Transform root, string childName, TMP_FontAsset font, Material preset,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, float fontMax, float fontMin,
        bool upper, bool bold, bool lower = false)
    {
        Transform child = FindDeep(root, childName);
        var t = child != null ? child.GetComponent<TMP_Text>() : null;
        if (t == null)
            return;

        if (font != null) t.font = font;
        if (preset != null) t.fontSharedMaterial = preset;
        t.color = Color.white;                          // white face; outline/shadow come from the preset

        var style = FontStyles.Normal;
        if (upper) style |= FontStyles.UpperCase;       // specials → ALL CAPS
        if (lower) style |= FontStyles.LowerCase;       // info/title lines → lowercase
        if (bold)  style |= FontStyles.Bold;
        t.fontStyle = style;

        t.enableAutoSizing = true;
        t.fontSizeMax = fontMax;
        t.fontSizeMin = fontMin;
        t.alignment = TextAlignmentOptions.Center;

        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        EditorUtility.SetDirty(t);
    }

    private static void DestroyChild(Transform root, string childName)
    {
        Transform child = FindDeep(root, childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c.name == name) return c;
            Transform found = FindDeep(c, name);
            if (found != null) return found;
        }
        return null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
