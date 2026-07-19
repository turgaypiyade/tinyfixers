using UnityEngine;

/// <summary>
/// Oyuncunun takım durumu — KALICI tek kaynak (PlayerPrefs). Takım ekranı, takım
/// tarayıcı (Ara/Oluştur) ve liderlik panosunun "Takım" sekmesi hep buradan okur.
/// HasTeam=false iken oyuncu takımsızdır: Team tab'ında Ara/Oluştur görünür,
/// liderlikte "Senin takımın" satırı basılmaz.
/// (Gerçek Firebase takımı gelince Join/Create sunucu çağrısına bağlanır.)
/// </summary>
public static class PlayerTeamState
{
    private const string KeyJoined      = "player_team_joined";
    private const string KeyTeamId      = "player_team_id";
    private const string KeyName        = "player_team_name";
    private const string KeyEmblem      = "player_team_emblem";
    private const string KeyDesc        = "player_team_desc";
    private const string KeyMinChapter  = "player_team_min_chapter";
    private const string KeyIsCreator   = "player_team_is_creator";

    public static bool HasTeam => PlayerPrefs.GetInt(KeyJoined, 0) == 1;

    /// <summary>Firestore teams/{id} doküman kimliği ("" = eski yerel-sim takım).</summary>
    public static string TeamId => PlayerPrefs.GetString(KeyTeamId, "");

    /// <summary>Oyuncu takımı KURAN kişi mi? (Kurulan takım 1 üyeyle başlar.)</summary>
    public static bool IsCreator => PlayerPrefs.GetInt(KeyIsCreator, 0) == 1;

    /// <summary>Amblem havuzundaki index (TeamScreenController/Browser sprite'a çevirir).</summary>
    public static int EmblemIndex => PlayerPrefs.GetInt(KeyEmblem, 0);

    public static string Description => PlayerPrefs.GetString(KeyDesc, "");
    public static int MinChapter => PlayerPrefs.GetInt(KeyMinChapter, 0);

    /// <summary>
    /// Takım adı. Takımsızken (eski davranışla uyum için) havuzdan geçici bir ad döner —
    /// ama kalıcılaşmaz; UI akışları HasTeam'i kontrol etmelidir.
    /// </summary>
    public static string TeamName
    {
        get
        {
            var stored = PlayerPrefs.GetString(KeyName, "");
            if (!string.IsNullOrEmpty(stored)) return stored;
            return NamePool.HasTeams ? NamePool.TeamAt(0) : "Tamirciler";
        }
    }

    /// <summary>Var olan bir takıma katıl (Ara → Takım Bilgisi → Katıl).</summary>
    public static void JoinTeam(string name, int emblemIndex, string description = "", int minChapter = 0, string teamId = "")
        => Persist(name, emblemIndex, description, minChapter, isCreator: false, teamId: teamId);

    /// <summary>Yeni takım kur (Oluştur formu). Coin harcaması ÇAĞIRANDA yapılır.</summary>
    public static void CreateTeam(string name, int emblemIndex, string description, int minChapter, string teamId = "")
        => Persist(name, emblemIndex, description, minChapter, isCreator: true, teamId: teamId);

    /// <summary>Takımdan ayrıl → takımsız duruma dön (Ara/Oluştur ekranları).</summary>
    public static void LeaveTeam()
    {
        PlayerPrefs.SetInt(KeyJoined, 0);
        PlayerPrefs.SetInt(KeyIsCreator, 0);
        PlayerPrefs.SetString(KeyName, "");
        PlayerPrefs.SetString(KeyTeamId, "");
        PlayerPrefs.Save();
    }

    /// <summary>Gerçek/dışarıdan gelen takım adını ata (Firebase entegrasyonu için).</summary>
    public static void SetTeamName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        PlayerPrefs.SetString(KeyName, name.Trim());
        PlayerPrefs.Save();
    }

    private static void Persist(string name, int emblemIndex, string description, int minChapter, bool isCreator, string teamId)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        PlayerPrefs.SetInt(KeyJoined, 1);
        PlayerPrefs.SetInt(KeyIsCreator, isCreator ? 1 : 0);
        PlayerPrefs.SetString(KeyName, name.Trim());
        PlayerPrefs.SetString(KeyTeamId, teamId ?? "");
        PlayerPrefs.SetInt(KeyEmblem, Mathf.Max(0, emblemIndex));
        PlayerPrefs.SetString(KeyDesc, description ?? "");
        PlayerPrefs.SetInt(KeyMinChapter, Mathf.Max(0, minChapter));
        PlayerPrefs.Save();
    }
}
