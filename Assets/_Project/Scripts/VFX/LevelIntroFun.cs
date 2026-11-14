using UnityEngine;

public enum LevelIntroKind { None, Brumo, Pemi }

/// <summary>
/// Level başlangıcı eğlencesi seçimi (kullanıcı 2026-11-13): tahta girişinde ara sıra kısa bir karakter anı.
///   • Brumo (BoardFixIntro): yamuk tahtayı ip-makarayla düzeltir — level 5'te garantili, bir kez.
///   • Pemi (PaintCleanIntro): kova cama boya döker, gondolla Tinko + Pemi siler — level 8'de garantili, bir kez.
///   • Garantiler kaçırılırsa (tekrar deneme vb.) sonraki ilk uygun girişte gelir.
///   • Level 6+: ZAR YOK (2026-11-14: %15 ile 47. seviyeye kadar hiç çıkmadı) — her EveryEntries ilk-deneme
///     girişinden birinde sırayla Brumo / Pemi. Sayaç cihazda kalıcı; tekrar denemede ve boss'ta sayılmaz.
/// Eğlence oynarken öğretici/ipucu pencereleri bekler (IsPlaying).
/// </summary>
public static class LevelIntroFun
{
    private const int EveryEntries = 6;   // ≈ %17: her 6 yeni seviyeden birinde
    // Pemi v1 beğenilmedi; v2 (cephe asansörü: Tinko + Pemi) kullanıcı onayıyla açık (2026-11-13: "çok daha iyi oldu").
    private const bool PemiEnabled = true;
    private const int MinRandomLevel = 6;
    private const int BrumoLevel = 5, PemiLevel = 8;
    private const string BrumoSeenKey = "boardfix_guaranteed_seen";
    private const string PemiSeenKey = "paintclean_guaranteed_seen";
    private const string SinceKey = "levelintro_entries_since";
    private const string NextPemiKey = "levelintro_next_is_pemi";

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
        if (level.levelKind != LevelKind.Normal) return LevelIntroKind.None;
        int n = CurrentLevel.Played;
        if (LevelAttemptStats.AttemptsOn(n) > 1) return LevelIntroKind.None;

        if (n >= BrumoLevel && PlayerPrefs.GetInt(BrumoSeenKey, 0) == 0 && BoardFixIntro.CanPlay())
            return Mark(LevelIntroKind.Brumo, BrumoSeenKey);
        if (PemiEnabled && n >= PemiLevel && PlayerPrefs.GetInt(PemiSeenKey, 0) == 0 && PaintCleanIntro.CanPlay())
            return Mark(LevelIntroKind.Pemi, PemiSeenKey);

        if (n < MinRandomLevel) return LevelIntroKind.None;
        int since = PlayerPrefs.GetInt(SinceKey, 0) + 1;
        if (since < EveryEntries)
        {
            PlayerPrefs.SetInt(SinceKey, since);
            PlayerPrefs.Save();
            return LevelIntroKind.None;
        }
        bool pemi = PemiEnabled && PlayerPrefs.GetInt(NextPemiKey, 0) == 1;
        var kind = pemi && PaintCleanIntro.CanPlay() ? LevelIntroKind.Pemi
            : BoardFixIntro.CanPlay() ? LevelIntroKind.Brumo
            : PemiEnabled && PaintCleanIntro.CanPlay() ? LevelIntroKind.Pemi : LevelIntroKind.None;
        if (kind == LevelIntroKind.None) return kind;
        PlayerPrefs.SetInt(NextPemiKey, kind == LevelIntroKind.Brumo ? 1 : 0);   // sırayla
        PlayerPrefs.SetInt(SinceKey, 0);
        PlayerPrefs.Save();
        return kind;
    }

    private static LevelIntroKind Mark(LevelIntroKind kind, string key)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.SetInt(SinceKey, 0);   // garantili gösterimden sonra sayaç baştan
        PlayerPrefs.Save();
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
