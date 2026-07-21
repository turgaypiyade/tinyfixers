using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liderlik panosu satırı (Royal Match anatomisi):
///   [rütbe rozeti/madalya] [çerçeveli avatar] [Bölüm N + isim/alt-isim]
///   [banner art] [sağ: Kapasite çipi (takım) + Puan]
/// Görseller LeaderboardSkin'den gelir; boş slotlar tema rengine düşer.
/// </summary>
public sealed class LeaderboardRow : MonoBehaviour
{
    [Header("Zemin")]
    [SerializeField] private Image rowBackground;

    [Header("Rütbe")]
    [SerializeField] private Image rankBadge;      // madalya/plaka (skin)
    [SerializeField] private TMP_Text rankText;

    [Header("Avatar")]
    [SerializeField] private Image avatarFrame;
    [SerializeField] private Image avatar;

    [Header("Bilgi")]
    [SerializeField] private TMP_Text chapterText;  // "Bölüm 4401" (0 = gizli)
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Banner")]
    [SerializeField] private Image bannerImage;     // sağdaki dekoratif art

    [Header("Haftalık top-3")]
    [SerializeField] private Image giftIcon;            // hediye kutusu (yalnız Weekly top-3)

    [Header("Sağ blok")]
    [SerializeField] private GameObject capacityRoot;   // takım dışında gizlenir
    [SerializeField] private TMP_Text capacityLabel;    // "Kapasite"
    [SerializeField] private Image capacityChip;
    [SerializeField] private TMP_Text capacityText;     // "49/50"
    [SerializeField] private TMP_Text scoreLabel;       // "Puan"
    [SerializeField] private TMP_Text scoreText;

    public void Bind(LeaderboardEntry e, UITheme theme) => Bind(e, theme, null, LeaderboardTab.Weekly);

    public void Bind(LeaderboardEntry e, UITheme theme, LeaderboardSkin skin, LeaderboardTab tab)
    {
        if (e == null) return;

        // ── Haftalık top-3: BÜYÜK kart yüksekliği (self/yeşil satır DAHİL) + hediye kutusu ──
        // Yükseklik ve award ikonu self dahil tüm weekly top-3 satırlarında gösterilir.
        bool weeklyTop3     = tab == LeaderboardTab.Weekly && e.rank <= 3;
        bool weeklyTop3Gift = weeklyTop3;
        var le = GetComponent<UnityEngine.UI.LayoutElement>();
        if (le != null && skin != null)
            le.preferredHeight = weeklyTop3 ? skin.weeklyTopThreeRowHeight : skin.rowHeight;

        if (giftIcon != null)
        {
            Sprite gift = null;
            if (weeklyTop3Gift && skin != null)
            {
                gift = e.rank switch
                {
                    1 => skin.giftTier1,
                    2 => skin.giftTier2,
                    3 => skin.giftTier3,
                    _ => null,
                };
            }
            giftIcon.sprite = gift;
            giftIcon.enabled = gift != null;
            giftIcon.preserveAspect = true;
            if (skin != null)
                giftIcon.rectTransform.sizeDelta = new Vector2(skin.giftIconSize, skin.giftIconSize);
        }

        // ── Rütbe: top-3 madalya sprite'ı, yoksa plaka + sayı ──
        if (rankText != null) rankText.text = e.rank.ToString();
        if (rankBadge != null)
        {
            Sprite medal = e.rank switch
            {
                1 => skin != null ? skin.medalGold   : null,
                2 => skin != null ? skin.medalSilver : null,
                3 => skin != null ? skin.medalBronze : null,
                _ => skin != null ? skin.rankPlate   : null,
            };
            rankBadge.sprite = medal;
            rankBadge.enabled = medal != null;
            rankBadge.preserveAspect = true;

            // Madalya sprite'ı yokken top-3 hissi: rozet alanını madalya rengine boya.
            if (medal == null && theme != null)
            {
                rankBadge.enabled = e.rank <= 3;
                rankBadge.color = e.rank switch
                {
                    1 => theme.goldTrim,
                    2 => new Color(0.75f, 0.78f, 0.85f),
                    3 => new Color(0.80f, 0.52f, 0.30f),
                    _ => Color.clear,
                };
            }
            else
            {
                rankBadge.color = Color.white;
            }
        }

        // ── Avatar + çerçeve ──
        if (avatar != null)
        {
            avatar.sprite = e.avatar;
            avatar.enabled = e.avatar != null;
            avatar.preserveAspect = true;
        }
        if (avatarFrame != null)
        {
            Sprite frame = tab == LeaderboardTab.Team
                ? (skin != null ? skin.teamEmblemFrame : null)
                : (skin != null ? skin.avatarFrame : null);
            avatarFrame.sprite = frame;
            avatarFrame.preserveAspect = true;
            // Çerçeve sprite'ı yoksa hafif koyu plaka olarak kalsın.
            avatarFrame.color = frame != null ? Color.white : new Color(0f, 0f, 0f, 0.18f);
            if (frame != null) avatarFrame.type = Image.Type.Simple;
        }

        // ── Metinler ──
        if (chapterText != null)
        {
            bool showChapter = e.chapter > 0 && tab != LeaderboardTab.Team;
            chapterText.gameObject.SetActive(showChapter);
            if (showChapter) chapterText.text = $"Bölüm {e.chapter}";
        }
        if (nameText != null) nameText.text = e.playerName;
        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(e.subtitle));
            subtitleText.text = e.subtitle;
        }

        // ── Banner art ──
        if (bannerImage != null)
        {
            Sprite banner = e.bannerArt != null ? e.bannerArt : (skin != null ? skin.defaultRowBanner : null);
            bannerImage.sprite = banner;
            bannerImage.enabled = banner != null;
        }

        // ── Sağ blok: kapasite (takım) + puan ──
        bool isTeamRow = e.capacityMax > 0;
        if (capacityRoot != null) capacityRoot.SetActive(isTeamRow);
        if (isTeamRow)
        {
            if (capacityText != null) capacityText.text = $"{e.capacityCurrent}/{e.capacityMax}";
            if (capacityChip != null && skin != null && skin.capacityChip != null)
            {
                capacityChip.sprite = skin.capacityChip;
                capacityChip.type = Image.Type.Sliced;
                capacityChip.color = Color.white;
            }
        }
        // Arkadaşlar sekmesi puan yarışı değil bölüm yarışı (referans RM) → Puan bloğu gizli.
        bool showScore = tab != LeaderboardTab.Friends;
        if (scoreLabel != null) scoreLabel.gameObject.SetActive(showScore);
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(showScore);
            if (showScore) scoreText.text = e.score.ToString("N0");
        }

        // ── Zemin: öncelik self > top-3 kartı > normal satır ──
        if (rowBackground != null && theme != null)
        {
            Sprite bgSprite;
            if (e.isSelf && skin != null && skin.selfRowBackground != null)
                bgSprite = skin.selfRowBackground;
            else if (e.rank <= 3 && !e.isSelf && skin != null && skin.topThreeCardBackground != null)
                bgSprite = skin.topThreeCardBackground;
            else
                bgSprite = skin != null ? skin.rowBackground : null;

            if (bgSprite != null)
            {
                rowBackground.sprite = bgSprite;
                rowBackground.type = Image.Type.Sliced;
                rowBackground.color = Color.white;
                // Sprite tek (self ayrı sprite yok) ise self'i tintle yeşillendir.
                if (e.isSelf && (skin == null || skin.selfRowBackground == null))
                    rowBackground.color = theme.ctaGreen;
            }
            else
            {
                UITheme.ApplySurface(rowBackground, theme.cardBackground,
                    e.isSelf ? theme.ctaGreen : theme.creamSurface);
            }
        }

        // Metin renkleri: krem zemin üstünde koyu, yeşil (self) üstünde beyaz.
        if (theme != null)
        {
            // İsim her zaman SİYAH (bold + boyut prefab'tan gelir; ApplyText onlara dokunmaz).
            theme.ApplyText(nameText, Color.black, heading: true);
            // rankText rengine DOKUNMA (self dahil) → herkes prefab'daki rengi alır.
            theme.ApplyText(chapterText, e.isSelf ? theme.textLight : theme.headerBand, heading: true);
            theme.ApplyText(scoreText, e.isSelf ? theme.textLight : theme.headerBand, heading: true);
            theme.ApplyText(scoreLabel, e.isSelf ? theme.textLight : theme.headerBand, heading: true);
            theme.ApplyText(capacityLabel, e.isSelf ? theme.textLight : new Color(0.72f, 0.58f, 0.45f), heading: true);
            theme.ApplyText(capacityText, e.isSelf ? theme.textLight : theme.textOnCream, heading: true);
            theme.ApplyText(subtitleText, e.isSelf ? theme.textLight : theme.textSub);
        }
    }
}
