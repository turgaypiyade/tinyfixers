/// <summary>
/// Backend servislerinin tek erişim/geçiş noktası. Mock ↔ Firebase geçişi burada tek satır.
/// Controller'lar somut sınıfı değil bu locator'ı kullanır.
/// </summary>
public static class BackendServices
{
    private static ILeaderboardService leaderboard;
    private static ITeamService team;

    /// <summary>Liderlik servisi. Firebase kuruluysa gerçek; mock'a dönmek istersen aşağıyı değiştir.</summary>
    public static ILeaderboardService Leaderboard
        => leaderboard ??= new FirebaseLeaderboardService();
        // Mock'a dönmek için: leaderboard ??= new MockLeaderboardService();

    /// <summary>Takım servisi. Şu an yerel bot simülasyonu; gerçek Firebase takımı gelince burası değişir.</summary>
    public static ITeamService Team
        => team ??= new SimTeamService();
        // Mock'a dönmek için: team ??= new MockTeamService();
}
