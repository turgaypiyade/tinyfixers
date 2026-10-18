using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Kullanıcı içeriği denetimi (App Store 1.2 / Google Play UGC): takım sohbeti, oyuncu adı, takım adı.
///   • Filtre: Resources/Moderation/blocked_words.txt (satır = kelime; "=" önekli = yalnız tam kelime). Harf oyunları
///     (0→o, 1/!→i, 3→e, 4/@→a, 5/$→s, 7→t) çözülür, tekrarlar teke iner; eşleşen kelime "***" olur. Gönderirken VE
///     gösterirken uygulanır (eski/elle yazılmış mesajlar da maskelenir).
///   • Şikâyet: Firestore reports/{auto} (yalnız oluşturma; okuma Console'dan). Kuralı firestore.rules'ta.
///   • Engelleme: cihazda (PlayerPrefs, bulut kayıtta) engellenen UID listesi; onların mesajları gösterilmez.
/// </summary>
public static class ChatModeration
{
    private const string BlockedKey = "chat_blocked_uids";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private static HashSet<string> exact, contains;
    private static HashSet<string> blocked;

    // ── Filtre ───────────────────────────────────────────────────

    private static void EnsureWords()
    {
        if (exact != null) return;
        exact = new HashSet<string>(); contains = new HashSet<string>();
        var asset = Resources.Load<TextAsset>("Moderation/blocked_words");
        if (asset == null) return;
        foreach (var raw in asset.text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            if (line[0] == '=') { if (line.Length > 1) exact.Add(Normalize(line.Substring(1))); }
            else contains.Add(Normalize(line));
        }
    }

    // Küçük harf (Türkçe kuralı), harf oyunlarını çöz, 3+ tekrarı teke indir ("siiiik" → "sik"). Çift harf KORUNUR:
    // teke indirmek "ass"→"as", "nigger"→"niger" yapıp masum kelimeleri (as, Niger) yakalardı.
    private static string Normalize(string s)
    {
        var mapped = new StringBuilder(s.Length);
        foreach (char ch in s.ToLower(Tr))
            mapped.Append(ch switch
            {
                '0' => 'o', '1' => 'i', '!' => 'i', '3' => 'e', '4' => 'a', '@' => 'a', '5' => 's', '$' => 's', '7' => 't',
                _ => ch,
            });
        var sb = new StringBuilder(mapped.Length);
        int i = 0;
        while (i < mapped.Length)
        {
            int j = i;
            while (j < mapped.Length && mapped[j] == mapped[i]) j++;
            int run = j - i;
            sb.Append(mapped[i], run >= 3 ? 1 : run);
            i = j;
        }
        return sb.ToString();
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '@' || c == '$' || c == '!';

    private static bool IsBad(string token)
    {
        string n = Normalize(token);
        if (n.Length == 0) return false;
        if (exact.Contains(n)) return true;
        foreach (var w in contains)
            if (w.Length > 0 && n.Contains(w)) return true;
        return false;
    }

    /// Uygunsuz kelimeleri "***" ile değiştirir (boşluk/noktalama korunur).
    public static string Filter(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        EnsureWords();
        if (exact.Count == 0 && contains.Count == 0) return text;

        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (!IsWordChar(text[i])) { sb.Append(text[i++]); continue; }
            int start = i;
            while (i < text.Length && IsWordChar(text[i])) i++;
            string token = text.Substring(start, i - start);
            sb.Append(IsBad(token) ? "***" : token);
        }
        return sb.ToString();
    }

    public static bool ContainsBlocked(string text) => Filter(text) != text;

    // ── Engelleme ────────────────────────────────────────────────

    private static void EnsureBlocked()
    {
        if (blocked != null) return;
        blocked = new HashSet<string>();
        foreach (var id in PlayerPrefs.GetString(BlockedKey, "").Split(','))
            if (!string.IsNullOrWhiteSpace(id)) blocked.Add(id.Trim());
    }

    public static bool IsBlocked(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return false;
        EnsureBlocked();
        return blocked.Contains(uid);
    }

    public static event Action OnBlockedChanged;

    public static void Block(string uid)
    {
        if (string.IsNullOrEmpty(uid) || uid == FirebaseAuthService.UserId) return;
        EnsureBlocked();
        if (!blocked.Add(uid)) return;
        PlayerPrefs.SetString(BlockedKey, string.Join(",", blocked));
        PlayerPrefs.Save();
        FirebaseCloudSaveService.MarkDirty();
        OnBlockedChanged?.Invoke();
    }

    // ── Şikâyet ──────────────────────────────────────────────────

    /// Mesajı şikâyet eder (Console: Firestore → reports). onDone(ok).
    public static void Report(string reportedUid, string reportedName, string teamId, string messageId, string text,
        Action<bool> onDone)
    {
        if (!FirebaseAuthService.IsReady || string.IsNullOrEmpty(FirebaseAuthService.UserId)) { onDone?.Invoke(false); return; }
        FirebaseFirestore.DefaultInstance.Collection("reports").AddAsync(new Dictionary<string, object>
        {
            { "reporterId", FirebaseAuthService.UserId },
            { "reportedId", reportedUid ?? "" },
            { "reportedName", reportedName ?? "" },
            { "teamId", teamId ?? "" },
            { "messageId", messageId ?? "" },
            { "text", text ?? "" },
            { "kind", "team_chat" },
            { "createdAt", FieldValue.ServerTimestamp },
        }).ContinueWithOnMainThread(t => onDone?.Invoke(!t.IsFaulted && !t.IsCanceled));
    }
}
