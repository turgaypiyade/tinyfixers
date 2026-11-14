using UnityEditor;
using UnityEngine;

// İki resimli inşaat modu pilotu. Canlı sürümü etkilemez: WonderDefinition.buildModeLive kapalıyken yalnız
// editör anahtarı açar. Play'de ana menüdeyken harikanın menüsüne her basış o harikanın SIRADAKİ görevini oynatır
// (son görevden sonra başa döner). Yıldız harcamaz, ilerleme kaydetmez (overlay önizlemesi).
// İnşaat verisi olmayan harika gri görünür.
public static class WonderBuildPilotDebugMenu
{
    private const string Root = "TinyFixers/Debug/Harika İnşaat/";
    private const string ToggleMenu = Root + "Pilot Açık (Play'den önce de açılabilir)";

    [MenuItem(Root + "1 - Pisa (mis1)", false, 1)] private static void Play1() => PlayNext("mis1");
    [MenuItem(Root + "1 - Pisa (mis1)", true)] private static bool Can1() => CanPlay("mis1");
    [MenuItem(Root + "2 - Golden Gate (mis2)", false, 2)] private static void Play2() => PlayNext("mis2");
    [MenuItem(Root + "2 - Golden Gate (mis2)", true)] private static bool Can2() => CanPlay("mis2");
    [MenuItem(Root + "3 - Eyfel (mis3)", false, 3)] private static void Play3() => PlayNext("mis3");
    [MenuItem(Root + "3 - Eyfel (mis3)", true)] private static bool Can3() => CanPlay("mis3");
    [MenuItem(Root + "4 - Piramit (mis4)", false, 4)] private static void Play4() => PlayNext("mis4");
    [MenuItem(Root + "4 - Piramit (mis4)", true)] private static bool Can4() => CanPlay("mis4");
    [MenuItem(Root + "5 - Çin Seddi (mis5)", false, 5)] private static void Play5() => PlayNext("mis5");
    [MenuItem(Root + "5 - Çin Seddi (mis5)", true)] private static bool Can5() => CanPlay("mis5");
    [MenuItem(Root + "6 - Kanyon (mis6)", false, 6)] private static void Play6() => PlayNext("mis6");
    [MenuItem(Root + "6 - Kanyon (mis6)", true)] private static bool Can6() => CanPlay("mis6");
    [MenuItem(Root + "7 - Ayasofya (mis7)", false, 7)] private static void Play7() => PlayNext("mis7");
    [MenuItem(Root + "7 - Ayasofya (mis7)", true)] private static bool Can7() => CanPlay("mis7");
    [MenuItem(Root + "8 - Rodos (mis8)", false, 8)] private static void Play8() => PlayNext("mis8");
    [MenuItem(Root + "8 - Rodos (mis8)", true)] private static bool Can8() => CanPlay("mis8");
    [MenuItem(Root + "9 - Big Ben (mis9)", false, 9)] private static void Play9() => PlayNext("mis9");
    [MenuItem(Root + "9 - Big Ben (mis9)", true)] private static bool Can9() => CanPlay("mis9");
    [MenuItem(Root + "10 - Sidney (mis10)", false, 10)] private static void Play10() => PlayNext("mis10");
    [MenuItem(Root + "10 - Sidney (mis10)", true)] private static bool Can10() => CanPlay("mis10");

    [MenuItem(Root + "Başa Sar (1. görevden başla)", false, 50)]
    private static void Rewind()
    {
        var overlay = FindOverlay();
        if (overlay != null) overlay.ResetPreview();
        Debug.Log("[WonderBuild] Önizleme başa sarıldı: sonraki basış 1. görevi oynatır.");
    }

    [MenuItem(Root + "Başa Sar (1. görevden başla)", true)]
    private static bool RewindValidate() => Application.isPlaying;

    [MenuItem(ToggleMenu, false, 100)]
    private static void Toggle() => SetPilot(PlayerPrefs.GetInt(WonderDefinition.BuildPilotPrefKey, 0) == 0);

    [MenuItem(ToggleMenu, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(ToggleMenu, PlayerPrefs.GetInt(WonderDefinition.BuildPilotPrefKey, 0) == 1);
        return true;
    }

    private static void PlayNext(string id)
    {
        var overlay = FindOverlay();
        if (overlay == null) { Debug.LogWarning("[WonderBuild] Ana menüde WonderRevealOverlay bulunamadı (Play'de ana menüde ol)."); return; }
        if (overlay.IsPlaying) { Debug.Log("[WonderBuild] Önceki görev hâlâ oynuyor."); return; }
        var wonder = Load(id);
        if (wonder == null) return;
        if (PlayerPrefs.GetInt(WonderDefinition.BuildPilotPrefKey, 0) == 0) SetPilot(true);
        overlay.PreviewRustRestoration(wonder);
    }

    private static bool CanPlay(string id)
    {
        if (!Application.isPlaying) return false;
        var w = Load(id);
        return w != null && w.buildSiteTexture != null && w.buildPieceMap != null;
    }

    private static WonderDefinition Load(string id) =>
        AssetDatabase.LoadAssetAtPath<WonderDefinition>($"Assets/_Project/Settings/Wonders/Wonder_{id}.asset");

    private static WonderRevealOverlay FindOverlay() =>
        Object.FindFirstObjectByType<WonderRevealOverlay>(FindObjectsInactive.Include);

    private static void SetPilot(bool on)
    {
        PlayerPrefs.SetInt(WonderDefinition.BuildPilotPrefKey, on ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[WonderBuild] İnşaat pilotu: {(on ? "AÇIK" : "KAPALI")}.");
    }
}
