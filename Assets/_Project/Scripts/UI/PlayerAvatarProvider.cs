using UnityEngine;

/// <summary>
/// Oyuncunun ProfileScreen'de seçtiği avatar sprite'ına her yerden erişim.
/// AvatarLibrary (Resources/Profiles/AvatarLibrary) + PlayerProfile.AvatarId.
/// Leaderboard kendi satırı, Team kendi mesajları bunu kullanır (rastgele değil).
/// </summary>
public static class PlayerAvatarProvider
{
    private static AvatarLibrary _lib;

    private static AvatarLibrary Lib =>
        _lib != null ? _lib : (_lib = Resources.Load<AvatarLibrary>("Profiles/AvatarLibrary"));

    /// <summary>Seçili avatar sprite'ı; kütüphane/asset yoksa null.</summary>
    public static Sprite Current
    {
        get { var l = Lib; return l != null ? l.Get(PlayerProfile.AvatarId) : null; }
    }
}
