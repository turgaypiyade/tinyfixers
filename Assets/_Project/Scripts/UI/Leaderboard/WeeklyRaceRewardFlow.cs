using System.Collections;
using UnityEngine;

/// <summary>
/// Haftalık Yarış ödülü — ana menü açılışında: bulut kaydı ÇÖZÜLDÜKTEN sonra (geri yükleme bekleyen haftayı
/// değiştirebilir) sıradaki bekleyen hafta için kesin sıra hesaplanır; ilk 3 ise ödül töreni (ComebackGiftOverlay)
/// açılır, dokununca O HAFTA için teslim edilir (hafta + hesap yeniden doğrulanır). İlk 3 dışı → sessizce kapanır.
/// Sıra hesaplanamazsa (çevrimdışı) ödül bekler. Oturum başına bir kez denenir (sonraki hafta sonraki açılışta).
/// </summary>
public static class WeeklyRaceRewardFlow
{
    private static bool s_startedThisSession;

    /// Test: aynı oturumda akışı yeniden denetebilmek için (yalnız editör debug menüsü).
    public static void DebugResetSession() => s_startedThisSession = false;

    /// Dönüş: tören yolu başladı mı (açılıştaki diğer pencereler beklemeli). Çevrimdışı/ödülsüz → onSkipped.
    public static bool TryShow(Component anyMainMenuUi, System.Action onSkipped)
    {
        if (s_startedThisSession || anyMainMenuUi == null || RuntimeSimulationSession.IsActive) return false;
        WeeklyRaceRewards.RollWeek();
        // Bulut kaydı henüz çözülmediyse bekleyen ödül oradan gelebilir → yerelde yok diye vazgeçme; çözülünce bakılır.
        if (FirebaseCloudSaveService.RestoreResolved && !WeeklyRaceRewards.HasPending) return false;
        s_startedThisSession = true;

        void Fetch()
        {
            if (anyMainMenuUi == null) return;
            WeeklyRaceRewards.RollWeek();   // geri yüklenen kayıt yeni bir geçmiş hafta getirmiş olabilir
            if (!WeeklyRaceRewards.HasPending) { onSkipped?.Invoke(); return; }
            string week = WeeklyRaceRewards.PendingWeek;
            int points = WeeklyRaceRewards.PendingPoints;
            string owner = FirebaseAuthService.UserId;
            FirebaseLeaderboardService.FetchWeeklyFinalRank(week, points, rank =>
            {
                if (anyMainMenuUi == null) return;   // sahne değişti; ödül bir sonraki açılışta
                // Sorgu sürerken hesap ya da bekleyen hafta değiştiyse bu sonuç geçersiz.
                if (rank <= 0 || FirebaseAuthService.UserId != owner || WeeklyRaceRewards.PendingWeek != week)
                {
                    onSkipped?.Invoke();
                    return;
                }
                // Kayıtlı paket varsa ödülsüz sıra dalına da yeni hesaplanan sırayla girilmez.
                rank = WeeklyRaceRewards.ResolveClaimRank(week, rank, out _);
                if (rank > WeeklyRaceRewards.RewardedRankLimit)
                {
                    WeeklyRaceRewards.Claim(week, rank);   // ödülsüz sıra: bekleyen kapanır
                    onSkipped?.Invoke();
                    return;
                }
                // Başka bir tören (ör. hoş geldin hediyesi) ekrandaysa sırası gelince aç — sessizce reddedilmesin.
                if (anyMainMenuUi is MonoBehaviour host && host.isActiveAndEnabled)
                    host.StartCoroutine(ShowWhenFree(week, rank, owner));
            });
        }

        void WhenRestored()
        {
            if (FirebaseCloudSaveService.RestoreResolved) { Fetch(); return; }
            void OnRestored() { FirebaseCloudSaveService.OnRestored -= OnRestored; Fetch(); }
            FirebaseCloudSaveService.OnRestored += OnRestored;
        }

        if (FirebaseAuthService.IsReady) WhenRestored();
        else
        {
            void OnReady() { FirebaseAuthService.OnReady -= OnReady; WhenRestored(); }
            FirebaseAuthService.OnReady += OnReady;
        }
        return true;
    }

    private static IEnumerator ShowWhenFree(string week, int rank, string owner)
    {
        var wait = new WaitForSecondsRealtime(0.3f);
        while (ComebackGiftOverlay.IsShowing || RewardChestRevealOverlay.IsShowing) yield return wait;
        // Beklerken hafta/hesap değiştiyse vazgeç (ödül bekleyende kalır).
        if (FirebaseAuthService.UserId != owner || WeeklyRaceRewards.PendingWeek != week) yield break;
        rank = WeeklyRaceRewards.ResolveClaimRank(week, rank, out _);
        string title = rank == 1
            ? GameLocalization.GetOr("weekly_race_reward_title_1", "Şampiyon!")
            : GameLocalization.GetOr("weekly_race_reward_title", "Tebrikler!");
        string subtitle = string.Format(
            GameLocalization.GetOr("weekly_race_reward_subtitle", "Haftalık Yarış'ı {0}. sırada bitirdin!"), rank);
        ComebackGiftOverlay.Show(WeeklyRaceRewards.RewardsFor(rank), () =>
        {
            if (FirebaseAuthService.UserId == owner) WeeklyRaceRewards.Claim(week, rank);
        }, title, subtitle, celebrate: true);
    }
}
