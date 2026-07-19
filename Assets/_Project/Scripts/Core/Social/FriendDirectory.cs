using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bir arkadaş adayının profil özeti (öneri kartı / ID arama sonucu).</summary>
public sealed class FriendProfile
{
    public string name;
    public int chapter;       // "Bölüm N"
    public int mutualCount;   // "N ortak arkadaş"
    public string teamName;   // satır alt-ismi (takımı)
    public string uid;        // GERÇEK oyuncuysa Firebase uid; bot önerilerde null
}

/// <summary>
/// Arkadaş adayları dizini — öneriler ve ID araması. v1: NamePool bot evreninden
/// DETERMİNİSTİK üretir (aynı isim/kod her zaman aynı profili verir); gerçek backend
/// gelince aynı API Firestore sorgusuna bağlanır.
/// </summary>
public static class FriendDirectory
{
    // Öneri havuzu, liderlik botlarından (0..120 ve 1000..) ayrı bir index aralığından
    // başlar ki aynı isimler iki listede birden dolaşmasın.
    private const int SuggestionBase = 2000;
    private const int SuggestionScan = 400;

    /// <summary>Arkadaş/reddedilmiş olmayanlardan en fazla count öneri döndürür.</summary>
    public static List<FriendProfile> GetSuggestions(int count)
    {
        var list = new List<FriendProfile>();
        for (int i = 0; i < SuggestionScan && list.Count < count; i++)
        {
            string name = NamePool.PlayerAt(SuggestionBase + i);
            if (FriendState.IsFriend(name) || FriendState.IsDismissed(name)) continue;
            list.Add(ProfileFor(name));
        }
        return list;
    }

    /// <summary>
    /// Arkadaş koduyla GERÇEK oyuncu ara (players dizini, async). callback(null) =
    /// geçersiz format / bulunamadı / kendi kodun. Bot fallback YOK — arama gerçektir
    /// (production kuralı); öneriler bot evreninden gelmeye devam eder.
    /// </summary>
    public static void SearchByCode(string code, Action<FriendProfile> callback)
    {
        if (!IsValidCodeFormat(code))
        {
            callback?.Invoke(null);
            return;
        }

        PlayerDirectoryService.FindByFriendCode(code, player =>
        {
            if (player == null)
            {
                callback?.Invoke(null);
                return;
            }
            callback?.Invoke(new FriendProfile
            {
                name = player.name,
                chapter = player.chapter,
                mutualCount = 0,
                teamName = "",
                uid = player.uid,
            });
        });
    }

    // Format: 2 harf + 6-8 rakam (kendi kod üretimiyle aynı kalıp).
    private static bool IsValidCodeFormat(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        if (code.Length < 8 || code.Length > 10) return false;
        if (!char.IsLetter(code[0]) || !char.IsLetter(code[1])) return false;
        for (int i = 2; i < code.Length; i++)
            if (!char.IsDigit(code[i])) return false;
        return true;
    }

    /// <summary>İsimden deterministik bölüm (arkadaş listesi sıralaması bunu kullanır).</summary>
    public static int ChapterOf(string name)
    {
        int myChapter = Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1));
        int hash = string.IsNullOrEmpty(name) ? 0 : Mathf.Abs(name.GetHashCode());
        // Oyuncunun civarında dağıl: [%40 .. %130] aralığı, en az 1.
        int lo = Mathf.Max(1, (int)(myChapter * 0.4f));
        int hi = Mathf.Max(lo + 1, (int)(myChapter * 1.3f) + 3);
        return lo + hash % (hi - lo);
    }

    /// <summary>Davet paylaşım metni (Davet Et → panoya kopyalanır).</summary>
    public static string InviteMessage()
        => $"TinyFixers'ta bana katıl! Arkadaş kodum: {FriendState.MyCode}";

    private static FriendProfile ProfileFor(string name)
    {
        int hash = Mathf.Abs(name.GetHashCode());
        return new FriendProfile
        {
            name = name,
            chapter = ChapterOf(name),
            mutualCount = 1 + hash % 3,
            teamName = NamePool.TeamAt(hash % 500),
        };
    }
}
