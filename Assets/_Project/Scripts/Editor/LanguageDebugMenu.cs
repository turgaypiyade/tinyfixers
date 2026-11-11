using UnityEditor;

// Editörde oyunu Türkçe / İngilizce görmek için (cihaz dili yerine geçer). Bir sonraki Play'de uygulanır;
// Play açıkken seçilirse metinler hemen değişir. Build'i etkilemez.
public static class LanguageDebugMenu
{
    private const string Root = "TinyFixers/Debug/Dil/";

    [MenuItem(Root + "Türkçe")] private static void Turkish() => Set("tr");
    [MenuItem(Root + "English")] private static void English() => Set("en");
    [MenuItem(Root + "Cihaz dili (varsayılan)")] private static void Device() => Set("");

    [MenuItem(Root + "Türkçe", true)] private static bool TurkishCheck() => Check("tr");
    [MenuItem(Root + "English", true)] private static bool EnglishCheck() => Check("en");
    [MenuItem(Root + "Cihaz dili (varsayılan)", true)] private static bool DeviceCheck() => Check("");

    private static void Set(string code)
    {
        if (string.IsNullOrEmpty(code)) EditorPrefs.DeleteKey(GameLocalization.EditorLanguageOverrideKey);
        else EditorPrefs.SetString(GameLocalization.EditorLanguageOverrideKey, code);
        if (EditorApplication.isPlaying && !string.IsNullOrEmpty(code)) GameLocalization.SetLanguage(code);
    }

    private static bool Check(string code)
    {
        string name = code == "tr" ? "Türkçe" : code == "en" ? "English" : "Cihaz dili (varsayılan)";
        Menu.SetChecked(Root + name, EditorPrefs.GetString(GameLocalization.EditorLanguageOverrideKey, "") == code);
        return true;
    }
}
