using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "Hesabımı Sil" (App Store 5.1.1(v); Gizlilik Politikası §5–6). Sıra:
///   1) bulut kayıt itmesi durdurulur (silinen veriyi geri yazmasın),
///   2) Firestore'daki kişisel veriler silinir: users/{uid} (save, stats, statsStreams, levels), players/{uid},
///      sıralama skorları (global + bu hafta), takım üyeliği (+ realMembers −1) ve kişinin takım sohbeti mesajları,
///   3) Firebase Authentication kullanıcısı silinir (Apple'a bağlıysa ve oturum eskiyse Apple ile yeniden doğrulanır),
///   4) cihazdaki kayıt (PlayerPrefs) temizlenir, oturum kapanır, oyun baştan açılır (yeni anonim hesap).
/// Kural gereği istemci yalnız KENDİ belgelerini silebilir (firestore.rules). Apple token iptali (revoke) sunucu
/// gerektirir — henüz yok (Docs: Cloud Function). Bir adım başarısız olursa kullanıcıya hata gösterilir; tekrar
/// denemek güvenlidir (silinmiş belgeyi yeniden silmek hata vermez).
/// </summary>
public static class AccountDeletionService
{
    public static bool InProgress { get; private set; }

    /// onDone(ok, errorMessage). Başarıda oyun yeniden yükleneceği için çağıran UI'ı kapatmaya gerek yok.
    public static void DeleteAccount(Action<bool, string> onDone)
    {
        if (InProgress) return;
        string uid = FirebaseAuthService.UserId;
        var user = FirebaseAuthService.CurrentUser;
        if (!FirebaseAuthService.IsReady || string.IsNullOrEmpty(uid) || user == null)
        {
            onDone?.Invoke(false, GameLocalization.Get("account_delete_offline"));
            return;
        }

        InProgress = true;
        FirebaseCloudSaveService.SuspendForDeletion = true;
        DeleteCloudData(uid).ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted || t.IsCanceled)
            {
                Fail(onDone, t.Exception);
                return;
            }
            DeleteAuthUser(user, onDone);
        });
    }

    // ── 2) Firestore ─────────────────────────────────────────────

    private static async Task DeleteCloudData(string uid)
    {
        var db = FirebaseFirestore.DefaultInstance;
        var userDoc = db.Collection("users").Document(uid);

        // Takım: üyelik + kişinin mesajları (takım belgesinin kendisi diğer üyelerindir).
        string teamId = PlayerTeamState.HasTeam ? PlayerTeamState.TeamId : "";
        if (!string.IsNullOrEmpty(teamId))
        {
            var team = FirebaseTeamCloud.TeamDoc(teamId);
            var mine = await team.Collection("chat").WhereEqualTo("senderId", uid).GetSnapshotAsync();
            await DeleteAll(db, mine.Documents);
            await team.Collection("lifeRequests").Document(uid).DeleteAsync();   // can isteğim
            var member = team.Collection("members").Document(uid);
            var memberSnap = await member.GetSnapshotAsync();
            if (memberSnap.Exists)
            {
                await member.DeleteAsync();
                await team.SetAsync(new Dictionary<string, object>
                {
                    { "realMembers", FieldValue.Increment(-1) },
                    { "updatedAt", FieldValue.ServerTimestamp },
                }, SetOptions.MergeAll);
            }
        }

        foreach (var sub in new[] { "save", "stats", "statsStreams", "levels" })
        {
            var snap = await userDoc.Collection(sub).GetSnapshotAsync();
            await DeleteAll(db, snap.Documents);
        }
        await userDoc.DeleteAsync();

        await db.Collection("players").Document(uid).DeleteAsync();
        await db.Document($"leaderboards/global/scores/{uid}").DeleteAsync();
        await db.Document($"leaderboards/{FirebaseLeaderboardService.CurrentWeekId}/scores/{uid}").DeleteAsync();
    }

    // Firestore toplu yazma en fazla 500 işlem.
    private static async Task DeleteAll(FirebaseFirestore db, IEnumerable<DocumentSnapshot> docs)
    {
        var batch = db.StartBatch();
        int n = 0;
        foreach (var d in docs)
        {
            batch.Delete(d.Reference);
            if (++n % 450 == 0) { await batch.CommitAsync(); batch = db.StartBatch(); }
        }
        if (n % 450 != 0) await batch.CommitAsync();
    }

    // ── 3) Authentication ────────────────────────────────────────

    private static void DeleteAuthUser(FirebaseUser user, Action<bool, string> onDone)
    {
        user.DeleteAsync().ContinueWithOnMainThread(t =>
        {
            if (!t.IsFaulted && !t.IsCanceled) { Finish(onDone); return; }

            // Oturum eskiyse Firebase yeniden doğrulama ister: Apple'a bağlı hesapta Apple ile tekrar giriş, sonra sil.
            if (RequiresRecentLogin(t.Exception) && AuthLinkService.AppleTokenProvider != null)
            {
                AuthLinkService.AppleTokenProvider((credential, error) =>
                {
                    if (credential == null) { Fail(onDone, null, error); return; }
                    user.ReauthenticateAsync(credential).ContinueWithOnMainThread(r =>
                    {
                        if (r.IsFaulted || r.IsCanceled) { Fail(onDone, r.Exception); return; }
                        user.DeleteAsync().ContinueWithOnMainThread(d =>
                        {
                            if (d.IsFaulted || d.IsCanceled) Fail(onDone, d.Exception);
                            else Finish(onDone);
                        });
                    });
                });
                return;
            }
            Fail(onDone, t.Exception);
        });
    }

    private static bool RequiresRecentLogin(AggregateException e)
    {
        if (e == null) return false;
        foreach (var inner in e.Flatten().InnerExceptions)
            if (inner is Firebase.FirebaseException fe && fe.ErrorCode == (int)AuthError.RequiresRecentLogin) return true;
        return false;
    }

    // ── 4) Yerel temizlik ────────────────────────────────────────

    private static void Finish(Action<bool, string> onDone)
    {
        InProgress = false;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        FirebaseAuthService.ResetAfterAccountDeletion();
        FirebaseCloudSaveService.SuspendForDeletion = false;
        onDone?.Invoke(true, null);
        SceneManager.LoadScene(0);   // Boot → yeni anonim hesap, kabul ekranı ve ilk açılış akışı baştan
    }

    private static void Fail(Action<bool, string> onDone, AggregateException e, string message = null)
    {
        InProgress = false;
        FirebaseCloudSaveService.SuspendForDeletion = false;
        if (GameDiagnostics.Backend) Debug.LogWarning($"[AccountDeletion] başarısız: {message ?? e?.GetBaseException().Message}");
        onDone?.Invoke(false, GameLocalization.Get("account_delete_failed"));
    }
}
