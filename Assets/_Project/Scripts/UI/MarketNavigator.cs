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

    // Oyun sahnesinin market içeriği (LevelEndSimplePopupController kaydeder). Varken market
    // sahneden ÇIKMADAN overlay olarak açılır — oyuncu oynadığı level'dan atılmaz.
    private static InGameShopOverlay.Refs? _inGameShop;

    public static void RegisterInGameShop(InGameShopOverlay.Refs refs)
    {
        if (refs.IsValid) _inGameShop = refs;
    }

    public static void UnregisterInGameShop(InGameShopOverlay.Refs refs)
    {
        if (_inGameShop.HasValue && _inGameShop.Value.catalog == refs.catalog
            && _inGameShop.Value.panelPrefab == refs.panelPrefab)
            _inGameShop = null;
    }

    /// <summary>Marketi açar: ana menüde sekmeye geçer, oyun sahnesinde overlay açar; ikisi de
    /// yoksa ana menüye yönlendirir.</summary>
    public static void OpenMarket()
    {
        var tabs = Object.FindFirstObjectByType<BottomTabController>();
        if (tabs != null)
        {
            tabs.Select(MarketTabIndex);
            return;
        }

        if (_inGameShop.HasValue)
        {
            InGameShopOverlay.Open(_inGameShop.Value, null);
            return;
        }

        PendingOpenMarket = true;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    /// <summary>Ana menü marketi şu an açık mı (alt menü bu sahnede var ve market sekmesi seçili)?
    /// Oyun sahnesinde alt menü yoktur → false (oyun içi satın alma akışı değişmez).</summary>
    public static bool IsMenuMarketOpen()
    {
        var tabs = Object.FindFirstObjectByType<BottomTabController>();
        return tabs != null && tabs.CurrentIndex == MarketTabIndex;
    }

    /// <summary>Ana menüde HOME sekmesine döner (alt menü yoksa no-op).</summary>
    public static void ReturnHome()
    {
        var tabs = Object.FindFirstObjectByType<BottomTabController>();
        if (tabs != null) tabs.SelectHome();
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
