using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Usta Ligi Özeti" (Docs/MasterLeague_Plan.md): yeni seviye paketi gelip ana yol yeniden açıldığında, ana menüye ilk
/// dönüşte bir kez açılır — lig döneminde (lig açıldı → paket geldi) kazanılan seviye, lig puanı, en iyi haftalık puan/sıra,
/// altın ve ulaşılan tur. Gösterilince dönem istatistikleri sıfırlanır (bir sonraki lig dönemi yeniden biriktirir).
/// Ortak popup kalıbı (CommonPopupView); kendi kökünü kurar, kapanınca kendini yok eder.
/// </summary>
public sealed class MasterLeagueSummary : MonoBehaviour
{
    private const float ShowDelaySeconds = 0.6f;

    private static bool s_shownThisSession;

    private bool preview;   // önizleme: örnek değerler, kapanınca kayıtlara dokunmaz

    /// Önizleme (editör debug menüsü, Play mode): koşullara bakmadan örnek değerlerle açar; hiçbir kaydı değiştirmez.
    public static void ShowPreview(Canvas canvas)
    {
        if (canvas == null) return;
        canvas = canvas.rootCanvas;
        var go = new GameObject("MasterLeagueSummaryPreview", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.layer = canvas.gameObject.layer;
        go.AddComponent<MasterLeagueSummary>().preview = true;
    }

    /// Lig dönemi bitti (ana yol yeniden açık) ve özet bekliyorsa açar. Dönüş: açılacak mı.
    public static bool TryShow(Component anyMainMenuUi, LevelCatalog catalog)
    {
        if (s_shownThisSession || anyMainMenuUi == null || catalog == null) return false;
        if (!MasterLeague.Reached || MasterLeague.IsUnlocked(catalog)) return false;   // hâlâ ligdeyse özet yok
        if (PlayerPrefs.GetInt(MasterLeague.KeyStintWins, 0) <= 0) return false;      // bu dönemde lig oynanmadı

        var canvas = anyMainMenuUi.GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        canvas = canvas.rootCanvas;

        s_shownThisSession = true;
        var go = new GameObject("MasterLeagueSummary", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.layer = canvas.gameObject.layer;   // Screen Space Camera culling tuzağı
        go.AddComponent<MasterLeagueSummary>();
        return true;
    }

    private System.Collections.IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(ShowDelaySeconds);
        Build();
        transform.SetAsLastSibling();
    }

    private void Build()
    {
        CommonPopupView.Region((RectTransform)transform, new Rect(0, 0, 1, 1));
        gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);

        var skin = CommonPopupSkin.Shared;
        var view = CommonPopupView.Create(transform, GameLocalization.Get("master_league_summary_title"), Close);

        var intro = CommonPopupView.Text(view.Body, "Intro", GameLocalization.Get("master_league_summary_intro"), 40, skin.bodyTextColor, readable: true);
        CommonPopupView.Region(intro.rectTransform, new Rect(0, 0.78f, 1, 0.22f));

        var stats = CommonPopupView.Text(view.Body, "Stats", preview ? PreviewText() : StatsText(), 40, skin.bodyTextColor, readable: true);
        CommonPopupView.Region(stats.rectTransform, new Rect(0, 0, 1, 0.76f));
        stats.richText = true;
        stats.lineSpacing = 18f;

        var start = CommonPopupView.Button(view.Actions, "BtnContinue", GameLocalization.Get("master_league_summary_start"),
            Close, skin.continueButton);
        CommonPopupView.Region((RectTransform)start.transform, CommonPopupView.ActionRegion(0, 1));
    }

    // Satır başına "Etiket: <b>değer</b>"; sıra bilinmiyorsa (liderlik ekranı hiç açılmadı) o satır yok.
    private static string StatsText()
    {
        var sb = new StringBuilder(256);
        Line(sb, "master_league_summary_wins", PlayerPrefs.GetInt(MasterLeague.KeyStintWins, 0));
        Line(sb, "master_league_summary_points", PlayerPrefs.GetInt(MasterLeague.KeyStintPoints, 0));
        Line(sb, "master_league_summary_best_weekly", PlayerPrefs.GetInt(MasterLeague.KeyStintBestWeekly, 0));
        int rank = PlayerPrefs.GetInt(MasterLeague.KeyStintBestRank, 0);
        if (rank > 0) Line(sb, "master_league_summary_best_rank", rank);
        Line(sb, "master_league_summary_coins", PlayerPrefs.GetInt(MasterLeague.KeyStintCoins, 0));
        Line(sb, "master_league_summary_lap", MasterLeague.Lap);
        return sb.ToString();
    }

    private static string PreviewText()
    {
        var sb = new StringBuilder(256);
        Line(sb, "master_league_summary_wins", 23);
        Line(sb, "master_league_summary_points", 5640);
        Line(sb, "master_league_summary_best_weekly", 3200);
        Line(sb, "master_league_summary_best_rank", 12);
        Line(sb, "master_league_summary_coins", 1150);
        Line(sb, "master_league_summary_lap", 2);
        return sb.ToString();
    }

    private static void Line(StringBuilder sb, string key, int value)
    {
        if (sb.Length > 0) sb.Append('\n');
        sb.Append(GameLocalization.Get(key)).Append(": <b>").Append(value.ToString("N0")).Append("</b>");   // binlik ayraçlı (proje kalıbı)
    }

    private void Close()
    {
        if (preview) { Destroy(gameObject); return; }
        foreach (var key in MasterLeague.StintKeys) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        FirebaseCloudSaveService.MarkDirty();
        Destroy(gameObject);
    }
}
