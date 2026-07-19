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

    /// <summary>
    /// Takım servisi: TeamId'li (Firestore) takım → GERÇEK servis (canlı sohbet);
    /// TeamId'siz eski yerel takım → sim. Yeni katılımlar hep TeamId'li olur.
    /// </summary>
    public static ITeamService Team
        => team ??= string.IsNullOrEmpty(PlayerTeamState.TeamId)
            ? new SimTeamService()
            : new FirebaseTeamService();

    /// <summary>
    /// Takım servisini sıfırla — takıma katılınca/kurunca/ayrılınca çağrılır; eski
    /// servisin dinleyicileri kapatılır, sonraki erişim taze servis kurar.
    /// </summary>
    public static void ResetTeam()
    {
        (team as System.IDisposable)?.Dispose();
        team = null;
    }
}
