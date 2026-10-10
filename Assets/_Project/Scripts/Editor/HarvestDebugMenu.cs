using UnityEditor;
using UnityEngine;

/// Bostan Hasadı test menüsü (yalnız editör).
public static class HarvestDebugMenu
{
    private const string Root = "TinyFixers/Debug/Harvest/";

    [MenuItem(Root + "+10 Kürek")]
    private static void AddTrowels() => HarvestState.AddTrowels(10);

    [MenuItem(Root + "Kazı Ekranını Aç (Play)")]
    private static void Open()
    {
        var screen = Object.FindFirstObjectByType<HarvestScreen>(FindObjectsInactive.Include);
        if (screen == null) { Debug.LogWarning("[Harvest] Sahnede HarvestScreen yok — TinyFixers ▸ Mockup ▸ Harvest Event."); return; }
        screen.Open();
    }

    [MenuItem(Root + "Kazı Ekranını Aç (Play)", true)]
    private static bool OpenValidate() => Application.isPlaying;

    [MenuItem(Root + "Sıfırla")]
    private static void ResetAll()
    {
        HarvestState.DebugReset();
        PlayerPrefs.DeleteKey("harvest_howto_cycle");   // "Nasıl oynanır" yeniden gösterilsin
        PlayerPrefs.Save();
        Debug.Log("[Harvest] Durum sıfırlandı (kürek, hasat, kazılanlar, nasıl oynanır).");
    }
}
