/// <summary>
/// Oyuncunun içinde bulunduğu takımın kimliği — Takım ekranı ile liderlik panosunun
/// "Takım" sekmesi AYNI takım adını kullansın diye tek kaynak. İlk erişimde havuzdan atanır.
/// (Gerçek Firebase takımı gelince buraya sunucudan gelen takım adı yazılır.)
/// </summary>
public static class PlayerTeamState
{
    private static string teamName;

    public static string TeamName
        => teamName ??= (NamePool.HasTeams ? NamePool.NextTeamName() : "Tamirciler");

    /// <summary>Gerçek/dışarıdan gelen takım adını ata (Firebase entegrasyonu için).</summary>
    public static void SetTeamName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name)) teamName = name.Trim();
    }
}
