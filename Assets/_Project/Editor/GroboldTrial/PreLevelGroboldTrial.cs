using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

// Local visual evaluation only: this tool and its generated TMP objects stay in
// the Editor. Selecting a trial does not change prefab references or saved scenes.
[InitializeOnLoad]
internal static class PreLevelGroboldTrial
{
    private const string Menu = "TinyFixers/Fonts/PreLevel Font Trial/Enabled (Play Mode Only)";
    private const string EnabledKey = "TinyFixers.PreLevelGroboldTrial.ArcEvaluationEnabled";
    private const string SelectedFontKey = "TinyFixers.PreLevelFontTrial.Source";
    private static bool UseOriginalDraft => SessionState.GetBool(SelectedFontKey, false);
    private static string FontPath => UseOriginalDraft
        ? "Assets/_Project/Fonts/TinyFixersPop/TinyFixersPop-Regular.ttf"
        : "Assets/_Project/Editor/GroboldTrial/GROBOLD.ttf";
    private static string FontName => UseOriginalDraft ? "TinyFixers Pop" : "Grobold";
    private const string MaterialsPath = "Assets/_Project/Fonts/Materials/";
    private static readonly List<TextState> Targets = new();
    private static TMP_FontAsset trialFont;
    private static Material titleMaterial;
    private static Material buttonMaterial;

    private sealed class TextState
    {
        internal TMP_Text text;
        internal TMP_FontAsset font;
        internal Material material;
        internal bool extraPadding;
        internal bool gradient;
        internal float size;
        internal float minimumSize;
        internal float maximumSize;
        internal FontStyles style;
    }

    static PreLevelGroboldTrial()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
        EditorApplication.delayCall += Apply;
    }

    [MenuItem(Menu)]
    private static void Toggle()
    {
        bool enabled = !SessionState.GetBool(EnabledKey, true);
        SessionState.SetBool(EnabledKey, enabled);
        if (enabled) Apply();
        else Clear();
    }

    [MenuItem(Menu, true)]
    private static bool ValidateMenu()
    {
        UnityEditor.Menu.SetChecked(Menu, SessionState.GetBool(EnabledKey, true));
        return true;
    }

    [MenuItem("TinyFixers/Fonts/PreLevel Font Trial/Grobold (evaluation)")]
    private static void SelectGrobold()
    {
        Clear();
        SessionState.SetBool(SelectedFontKey, false);
        SessionState.SetBool(EnabledKey, true);
        Apply();
    }

    [MenuItem("TinyFixers/Fonts/PreLevel Font Trial/TinyFixers Pop (original)")]
    private static void SelectOriginalDraft()
    {
        Clear();
        SessionState.SetBool(SelectedFontKey, true);
        SessionState.SetBool(EnabledKey, true);
        Apply();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) Apply();
        else if (state == PlayModeStateChange.ExitingPlayMode) Clear();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    private static void Apply()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(EnabledKey, true)) return;

        // Once per scene/play transition; no per-frame scans or material cloning.
        var popups = Object.FindObjectsByType<PreLevelSpecialPopupController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var popup in popups)
        {
            var serialized = new SerializedObject(popup);
            var title = serialized.FindProperty("titleText").objectReferenceValue as TMP_Text;
            var button = serialized.FindProperty("continueText").objectReferenceValue as TMP_Text;
            if (title == null || button == null || !EnsureFont(title.font)) continue;
            ApplyText(title, titleMaterial);
            ApplyText(button, buttonMaterial);
        }
    }

    private static bool EnsureFont(TMP_FontAsset fallback)
    {
        if (trialFont != null) return true;
        var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (source == null) return false;

        // 13 px padding (same as the TinyFixers Pop asset builder): thick outline + underlay fit the SDF.
        trialFont = TMP_FontAsset.CreateFontAsset(source, 90, 13, GlyphRenderMode.SDFAA,
            1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (trialFont == null) return false;
        trialFont.name = FontName + " Local Trial (Editor Only)";
        trialFont.hideFlags = HideFlags.HideAndDontSave;
        trialFont.material.hideFlags = HideFlags.HideAndDontSave;
        // Grobold's public file lacks several Turkish letters. TinyFixers Pop draws
        // them itself; both trials keep a fallback for characters outside their coverage.
        if (fallback != null) trialFont.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        titleMaterial = CreateMaterial("Baloo2-ExtraBold_PreLevelTitle.mat");
        buttonMaterial = CreateMaterial("Baloo2-ExtraBold_PreLevelGreenButton.mat");
        return true;
    }

    private static Material CreateMaterial(string preset)
    {
        // Start from this font's own atlas/metrics, then copy only the visual treatment.
        var material = new Material(trialFont.material)
        {
            name = FontName + " Trial " + preset,
            hideFlags = HideFlags.HideAndDontSave
        };
        var style = AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + preset);
        if (style != null)
        {
            foreach (string property in new[] { "_FaceColor", "_OutlineColor", "_UnderlayColor" })
                material.SetColor(property, style.GetColor(property));
            foreach (string property in new[] { "_OutlineWidth", "_OutlineSoftness", "_UnderlayOffsetX",
                         "_UnderlayOffsetY", "_UnderlayDilate", "_UnderlaySoftness" })
                material.SetFloat(property, style.GetFloat(property));
        }
        material.EnableKeyword("OUTLINE_ON");
        material.EnableKeyword("UNDERLAY_ON");
        return material;
    }

    private static void ApplyText(TMP_Text text, Material material)
    {
        for (int i = 0; i < Targets.Count; i++)
            if (Targets[i].text == text) return;
        Targets.Add(new TextState
        {
            text = text, font = text.font, material = text.fontSharedMaterial,
            extraPadding = text.extraPadding, gradient = text.enableVertexGradient,
            size = text.fontSize, minimumSize = text.fontSizeMin, maximumSize = text.fontSizeMax,
            style = text.fontStyle
        });
        text.font = trialFont;
        // Both trial faces are drawn heavy: TMP faux-bold would fatten them further and open the spacing.
        text.fontStyle &= ~FontStyles.Bold;
        text.fontSharedMaterial = material;
        text.enableVertexGradient = false;
        text.extraPadding = true;
        text.ForceMeshUpdate(true);
    }

    private static void Clear()
    {
        foreach (var state in Targets)
        {
            if (state.text == null) continue;
            state.text.font = state.font;
            state.text.fontSharedMaterial = state.material;
            state.text.extraPadding = state.extraPadding;
            state.text.enableVertexGradient = state.gradient;
            state.text.fontSize = state.size;
            state.text.fontSizeMin = state.minimumSize;
            state.text.fontSizeMax = state.maximumSize;
            state.text.fontStyle = state.style;
        }
        Targets.Clear();
        // Promo buttons inherit the trial material, so restore those before disposal too.
        if (trialFont != null)
        {
            foreach (var text in Object.FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.font != trialFont) continue;
                var original = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/_Project/Fonts/PaytoneOne/PaytoneOne-Regular SDF.asset");
                text.font = original;
                text.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    MaterialsPath + "PaytoneOne_PreLevelGreenButton.mat");
            }
        }
        if (titleMaterial != null) Object.DestroyImmediate(titleMaterial);
        if (buttonMaterial != null) Object.DestroyImmediate(buttonMaterial);
        if (trialFont != null)
        {
            foreach (var atlas in trialFont.atlasTextures)
                if (atlas != null) Object.DestroyImmediate(atlas);
            if (trialFont.material != null) Object.DestroyImmediate(trialFont.material);
            Object.DestroyImmediate(trialFont);
        }
        trialFont = null;
        titleMaterial = buttonMaterial = null;
    }
}
