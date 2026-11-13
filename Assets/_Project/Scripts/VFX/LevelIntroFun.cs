using UnityEngine;

public enum LevelIntroKind { None, Brumo, Pemi }

/// <summary>
/// Level başlangıcı eğlencesi seçimi (kullanıcı 2026-11-13): tahta girişinde ara sıra kısa bir karakter anı.
///   • Brumo (BoardFixIntro): yamuk tahtayı ip-makarayla düzeltir — level 5'in ilk girişinde garantili, bir kez.
///   • Pemi (PaintCleanIntro): kova cama boya döker, cephe asansörüyle Tinko + Pemi siler — level 8 garantili, bir kez.
///   • Level 6+: girişlerin ~%15'inde ikisinden biri rastgele. Oturum başına en fazla 1; tekrar denemede ve boss'ta yok.
/// Eğlence oynarken öğretici/ipucu pencereleri bekler (IsPlaying).
/// </summary>
public static class LevelIntroFun
{
    private const float Chance = 0.15f;
    // Pemi v1 beğenilmedi; v2 (cephe asansörü: Tinko + Pemi) kullanıcı onayıyla açık (2026-11-13: "çok daha iyi oldu").
    private const bool PemiEnabled = true;
    private const int MinRandomLevel = 6;
    private const int BrumoLevel = 5, PemiLevel = 8;
    private const string BrumoSeenKey = "boardfix_guaranteed_seen";
    private const string PemiSeenKey = "paintclean_guaranteed_seen";
    private static bool playedThisSession;

    /// Bir giriş eğlencesi oynuyor mu? Engel ipucu penceresi bitene dek bekler.
    public static bool IsPlaying { get; internal set; }

#if UNITY_EDITOR
    /// Editör testi: TinyFixers/Debug menüleri bir sonraki girişte kuralları atlayıp zorla gösterir.
    public const string DebugForceBrumoKey = "debug_boardfix_force";
    public const string DebugForcePemiKey = "debug_paintclean_force";
#endif

    /// Bu girişte hangi eğlence oynansın? Karar girişin başında bir kez verilir.
    public static LevelIntroKind Pick(LevelData level)
    {
        if (level == null || RuntimeSimulationSession.IsActive) return LevelIntroKind.None;
#if UNITY_EDITOR
        if (ConsumeDebug(DebugForceBrumoKey) && BoardFixIntro.CanPlay()) return LevelIntroKind.Brumo;
        if (ConsumeDebug(DebugForcePemiKey) && PaintCleanIntro.CanPlay()) return LevelIntroKind.Pemi;
#endif
        if (playedThisSession || level.levelKind != LevelKind.Normal) return LevelIntroKind.None;
        int n = CurrentLevel.Played;
        if (LevelAttemptStats.AttemptsOn(n) > 1) return LevelIntroKind.None;

        if (n == BrumoLevel && PlayerPrefs.GetInt(BrumoSeenKey, 0) == 0 && BoardFixIntro.CanPlay())
            return Mark(LevelIntroKind.Brumo, BrumoSeenKey);
        if (PemiEnabled && n == PemiLevel && PlayerPrefs.GetInt(PemiSeenKey, 0) == 0 && PaintCleanIntro.CanPlay())
            return Mark(LevelIntroKind.Pemi, PemiSeenKey);

        if (n < MinRandomLevel || Random.value > Chance) return LevelIntroKind.None;
        var kind = PemiEnabled && Random.value < 0.5f ? LevelIntroKind.Pemi : LevelIntroKind.Brumo;
        bool ok = kind == LevelIntroKind.Brumo ? BoardFixIntro.CanPlay() : PaintCleanIntro.CanPlay();
        if (!ok) return LevelIntroKind.None;
        playedThisSession = true;
        return kind;
    }

    private static LevelIntroKind Mark(LevelIntroKind kind, string key)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        playedThisSession = true;
        return kind;
    }

#if UNITY_EDITOR
    private static bool ConsumeDebug(string key)
    {
        if (PlayerPrefs.GetInt(key, 0) != 1) return false;
        PlayerPrefs.DeleteKey(key);
        return true;
    }
#endif
}
