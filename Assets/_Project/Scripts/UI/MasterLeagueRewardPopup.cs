using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Usta Ligi haftalık ödül penceresi (MasterLeagueRewards): ana menüde, bekleyen hafta varsa kesin sıra hesaplanınca açılır.
/// "Geçen hafta N. oldun!" + kazanılan rozet (varsa, büyük) + altın; "Topla" → teslim (tek kayıt, iki kez alınamaz).
/// Sıra hesaplanamazsa (çevrimdışı) pencere açılmaz, ödül bir sonraki açılışta beklemeye devam eder.
/// </summary>
public sealed class MasterLeagueRewardPopup : MonoBehaviour
{
    private static bool s_startedThisSession;

    private int rank;

    /// Bekleyen haftalık ödül varsa sırayı hesaplatıp pencereyi açar. Dönüş: ödül yolu başladı mı.
    /// Sıra hesaplanamazsa (çevrimdışı) pencere açılmaz, ödül bekler ve <paramref name="onSkipped"/> çağrılır → ana menü
    /// sıradaki pencereye (lig özeti / bildirim izni) geçer. Giriş (auth) henüz hazır değilse hazır olunca hesaplanır.
    public static bool TryShow(Component anyMainMenuUi, System.Action onSkipped)
    {
        if (s_startedThisSession || anyMainMenuUi == null) return false;
        MasterLeagueRewards.RollWeek();
        if (!MasterLeagueRewards.HasPending) return false;

        var canvas = anyMainMenuUi.GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        canvas = canvas.rootCanvas;

        s_startedThisSession = true;
        void Fetch()
        {
            FirebaseLeaderboardService.FetchMasterFinalRank(MasterLeagueRewards.PendingWeek, MasterLeagueRewards.PendingPoints, r =>
            {
                if (canvas == null) return;   // sahne değişti
                if (r <= 0) { onSkipped?.Invoke(); return; }   // çevrimdışı: ödül bekler, sıra diğer pencereye
                var go = new GameObject("MasterLeagueRewardPopup", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                go.layer = canvas.gameObject.layer;   // Screen Space Camera culling tuzağı
                go.AddComponent<MasterLeagueRewardPopup>().Build(r);
            });
        }
        if (FirebaseAuthService.IsReady) Fetch();
        else
        {
            void OnReady() { FirebaseAuthService.OnReady -= OnReady; Fetch(); }
            FirebaseAuthService.OnReady += OnReady;
        }
        return true;
    }

    private void Build(int finalRank)
    {
        rank = finalRank;
        transform.SetAsLastSibling();
        CommonPopupView.Region((RectTransform)transform, new Rect(0, 0, 1, 1));
        gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);

        var skin = CommonPopupSkin.Shared;
        var view = CommonPopupView.Create(transform, GameLocalization.Get("master_league_reward_title"), Collect);

        var badge = MasterLeagueRewards.BadgeFor(rank);
        int coins = MasterLeagueRewards.CoinsFor(rank, MasterLeagueRewards.PendingPoints);

        var headline = CommonPopupView.Text(view.Body, "Rank",
            GameLocalization.GetFormat("master_league_reward_rank", rank), 52, skin.bodyTextColor, readable: true);
        CommonPopupView.Region(headline.rectTransform, new Rect(0, 0.80f, 1, 0.20f));

        if (badge != MasterBadge.None)
        {
            var icon = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            icon.layer = gameObject.layer;
            icon.transform.SetParent(view.Body, false);
            var img = icon.GetComponent<Image>();
            img.sprite = MasterLeagueRewards.SpriteOf(badge);
            img.preserveAspect = true;
            img.raycastTarget = false;
            CommonPopupView.Region(img.rectTransform, new Rect(0.25f, 0.24f, 0.5f, 0.54f));
        }

        var reward = CommonPopupView.Text(view.Body, "Coins",
            GameLocalization.GetFormat("master_league_reward_coins", coins.ToString("N0")), 46, skin.bodyTextColor);
        reward.spriteAsset = skin.coinSpriteAsset;
        CommonPopupView.Region(reward.rectTransform, badge != MasterBadge.None ? new Rect(0, 0, 1, 0.22f) : new Rect(0, 0.2f, 1, 0.5f));

        var collect = CommonPopupView.Button(view.Actions, "BtnContinue", GameLocalization.Get("master_league_reward_collect"),
            Collect, skin.continueButton);
        CommonPopupView.Region((RectTransform)collect.transform, CommonPopupView.ActionRegion(0, 1));
    }

    private void Collect()
    {
        MasterLeagueRewards.Claim(rank);
        Destroy(gameObject);
    }
}
