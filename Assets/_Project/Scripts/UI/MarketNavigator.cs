using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Marketi (BottomTabController "Market" sekmesi) açmak için tek giriş noktası.
/// Ana menüde doğrudan sekmeye geçer; market sekmesi olmayan sahnelerde (01_Game)
/// ana menüye döner ve orada market otomatik açılır (PendingOpenMarket).
/// </summary>
public static class MarketNavigator
{
    // BottomTabController tabs sırası: Journey0, Ranks1, Home2, Teams3, Market4.
    private const int MarketTabIndex = 4;

    private const string MainMenuSceneName = "MainMenu";

    /// <summary>Ana menü yüklenince market sekmesine geçilsin mi (başka sahneden istendi).</summary>
    public static bool PendingOpenMarket { get; set; }

    /// <summary>Marketi açar. Sekme bu sahnede yoksa ana menüye yönlendirir.</summary>
    public static void OpenMarket()
    {
        var tabs = Object.FindFirstObjectByType<BottomTabController>();
        if (tabs != null)
        {
            tabs.Select(MarketTabIndex);
            return;
        }

        PendingOpenMarket = true;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    /// <summary>
    /// BottomTabController hazır olunca çağrılır: başka sahneden market istendiyse
    /// (PendingOpenMarket) market sekmesine geçer ve bayrağı temizler.
    /// </summary>
    public static void ConsumePendingIfAny(BottomTabController tabs)
    {
        if (!PendingOpenMarket || tabs == null) return;
        PendingOpenMarket = false;
        tabs.Select(MarketTabIndex);
    }
}
