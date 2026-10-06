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
    [SerializeField] private TMP_Text chapterText;  // "Seviye 4401" (0 = gizli)
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

    // ── RM satır yerleşimi (skin'den, runtime) ─────────────────────────────
    //   [rozet] [avatar] [üst: Seviye N (küçük) · isim (büyük) · alt: takım (soluk)]
    //   ............ [hediye/kapasite] [sağ sütun: etiket küçük + DEĞER büyük, tek satır]
    // Prefab'taki eski konumlar ezilir; sayılar asla iki satıra kaymaz (NoWrap + autosize).
    private static readonly Color NameColor  = new Color(0.13f, 0.15f, 0.32f, 1f);   // koyu lacivert
    private static readonly Color LabelColor = new Color(0.62f, 0.48f, 0.36f, 1f);   // soluk kahve
    private static readonly Color ValueColor = new Color(0.36f, 0.24f, 0.62f, 1f);   // koyu mor
    private const float RightColumnWidth = 210f;

    private const float TrophySize = 66f;

    private bool layoutApplied;
    private Image trophyImage;        // runtime: puanın solunda kupa
    private Image avatarCircleImage;  // avatar kökünün daire zemini
    private Mask avatarMask;          // daire maskesi (takım kalkanı için kapatılır)
    private Image avatarMaskImage;
    private RectTransform avatarRoot;
    private const float PodiumSideInset = 26f;
    private float podiumInset;
    private float infoXCurrent;

    // Kürsü kartı (yanları sivri özel görsel) 9-slice ile esnetilince bozulur → KENDİ ORANINDA çizilir:
    // yükseklik = satır genişliği × görselin yükseklik/genişlik oranı. Kendi (yeşil) satırın kürsüdeyse
    // yeşil 9-slice görsel aynı boya esner → üç kürsü satırı hep eşit yükseklikte.
    private float PodiumHeight(LeaderboardEntry e, LeaderboardSkin skin)
    {
        var card = skin.topThreeCardBackground;
        if (card == null) return skin.weeklyTopThreeRowHeight;   // kendi satırın kürsüdeyse de aynı boy
        float width = transform.parent is RectTransform p && p.rect.width > 1f ? p.rect.width : 880f;
        if (transform.parent != null && transform.parent.TryGetComponent(out HorizontalOrVerticalLayoutGroup g))
            width -= g.padding.horizontal;
        return width * card.rect.height / Mathf.Max(1f, card.rect.width);
    }

    // Kürsü (ilk 3, Arkadaşlar hariç her sekme): daha yüksek kart + büyük madalya/avatar/isim.
    // Diğer satırlarda normal ölçü. Bind her seferinde çağırır (satırlar yeniden kullanılabilir).
    private void ApplyPodium(bool podium, LeaderboardSkin skin)
    {
        if (!layoutApplied || skin == null) return;
        float badge  = podium ? skin.podiumBadgeSize  : skin.rankBadgeSize;
        float avSize = podium ? skin.podiumAvatarSize : skin.avatarSize;
        float shift  = avSize - skin.avatarSize;   // avatar büyüyünce isim bloğu sağa kayar

        podiumInset = podium ? PodiumSideInset : 0f;   // kürsü kartının sivri uçlarına binmesin
        if (rankBadge != null) Place(rankBadge.rectTransform, 0f, skin.rankBadgeX + podiumInset, 0f, badge, badge);
        if (avatarRoot != null)
            Place(avatarRoot, 0f, skin.avatarX + (badge - skin.rankBadgeSize) + podiumInset, 0f, avSize, avSize);

        float infoX = skin.infoX + shift + (badge - skin.rankBadgeSize) + podiumInset;
        infoXCurrent = infoX;
        foreach (var t in new[] { chapterText, nameText, subtitleText })
            if (t != null) t.rectTransform.anchoredPosition = new Vector2(infoX, t.rectTransform.anchoredPosition.y);
        if (nameText != null) nameText.fontSizeMax = podium ? 56f : 42f;
        if (scoreText != null) scoreText.fontSizeMax = podium ? 58f : 48f;
        if (trophyImage != null) trophyImage.rectTransform.localScale = Vector3.one * (podium ? 1.15f : 1f);
    }

    private void ApplyLayout(LeaderboardSkin skin)
    {
        if (layoutApplied || skin == null) return;
        layoutApplied = true;

        float badge = skin.rankBadgeSize;
        if (rankBadge != null) Place(rankBadge.rectTransform, 0f, skin.rankBadgeX, 0f, badge, badge);
        // Avatar kökü: çerçeve atanmışsa o; yoksa prefab düzeni AvatarCircle > Mask > AvatarImage.
        RectTransform avatarRoot = avatarFrame != null ? avatarFrame.rectTransform
            : avatar != null && avatar.transform.parent != null ? avatar.transform.parent.parent as RectTransform : null;
        if (avatarRoot != null && avatarRoot != transform)
        {
            this.avatarRoot = avatarRoot;
            Place(avatarRoot, 0f, skin.avatarX, 0f, skin.avatarSize, skin.avatarSize);
            avatarRoot.TryGetComponent(out avatarCircleImage);
        }
        if (avatar != null && avatar.transform.parent != null)
        {
            avatar.transform.parent.TryGetComponent(out avatarMask);
            avatar.transform.parent.TryGetComponent(out avatarMaskImage);
        }

        if (skin.trophyIcon != null && scoreText != null)
        {
            var go = new GameObject("Trophy", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            trophyImage = go.AddComponent<Image>();
            trophyImage.sprite = skin.trophyIcon;
            trophyImage.preserveAspect = true;
            trophyImage.raycastTarget = false;
        }
        if (avatar != null)
        {
            // Avatar maskenin içinde çerçeveyi doldursun (prefab'ta sabit 128px idi).
            var art = avatar.rectTransform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = art.offsetMax = Vector2.zero;
        }

        float infoW = 880f - skin.infoX - RightColumnWidth - skin.scoreRightPad - 140f;   // hediye/kapasite payı
        if (chapterText != null) { Place(chapterText.rectTransform, 0f, skin.infoX, 0f, infoW, 32f); Style(chapterText, 26f, TextAlignmentOptions.Left); }
        if (nameText != null)    { Place(nameText.rectTransform, 0f, skin.infoX, 0f, infoW, 50f);    Style(nameText, 42f, TextAlignmentOptions.Left); }
        if (subtitleText != null){ Place(subtitleText.rectTransform, 0f, skin.infoX, 0f, infoW, 32f); Style(subtitleText, 26f, TextAlignmentOptions.Left); }

        float rightX = -skin.scoreRightPad;
        if (scoreLabel != null) { Place(scoreLabel.rectTransform, 1f, rightX, 26f, RightColumnWidth, 30f); Style(scoreLabel, 24f, TextAlignmentOptions.Right); }
        if (scoreText != null)  { Place(scoreText.rectTransform, 1f, rightX, -14f, RightColumnWidth, 56f); Style(scoreText, 48f, TextAlignmentOptions.Right); }

        float sideX = rightX - RightColumnWidth - 12f;
        if (giftIcon != null) Place(giftIcon.rectTransform, 1f, sideX, 0f, skin.giftIconSize, skin.giftIconSize);
        if (capacityRoot != null) Place((RectTransform)capacityRoot.transform, 1f, sideX, 0f, 130f, 96f);
        if (bannerImage != null) bannerImage.gameObject.SetActive(false);   // RM satırında dekoratif bant yok
    }

    // Satır içinde dikey ortalı, yatayda sol (ax=0) ya da sağ (ax=1) kenara göre yerleştirir.
    private static void Place(RectTransform rt, float ax, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, 0.5f);
        rt.pivot = new Vector2(ax, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Style(TMP_Text t, float size, TextAlignmentOptions align)
    {
        t.alignment = align == TextAlignmentOptions.Right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.55f;
        t.fontSize = size;
    }

    // İsim bloğu genişliği: sağdaki blokların (puan sütunu + kapasite/hediye) BAŞLADIĞI yere kadar.
    // Kürsüde avatar büyüyüp blok sağa kaydığında sabit genişlik kapasite kutusunun altına giriyordu.
    private void FitInfoWidth(LeaderboardSkin skin, bool capacityShown, bool giftShown)
    {
        if (!layoutApplied || skin == null) return;
        float rowW = ((RectTransform)transform).rect.width;
        if (rowW <= 1f && transform.parent is RectTransform p) rowW = p.rect.width;
        if (rowW <= 1f) rowW = 880f;

        float right = skin.scoreRightPad + podiumInset + RightColumnWidth + 12f;
        if (capacityShown) right += 130f + 14f;
        else if (giftShown && giftIcon != null) right += giftIcon.rectTransform.sizeDelta.x + 14f;

        float w = Mathf.Max(120f, rowW - infoXCurrent - right);
        foreach (var t in new[] { chapterText, nameText, subtitleText })
            if (t != null) t.rectTransform.sizeDelta = new Vector2(w, t.rectTransform.sizeDelta.y);
    }

    // İsim bloğunun dikey dizilimi: hangi satırlar görünürse ona göre ortalanır.
    private void ArrangeInfo(bool showChapter, bool showSubtitle)
    {
        float nameY = 0f;
        if (showChapter && showSubtitle) nameY = 2f;
        else if (showChapter) nameY = -14f;
        else if (showSubtitle) nameY = 14f;
        if (nameText != null) nameText.rectTransform.anchoredPosition = new Vector2(nameText.rectTransform.anchoredPosition.x, nameY);
        if (chapterText != null) chapterText.rectTransform.anchoredPosition = new Vector2(chapterText.rectTransform.anchoredPosition.x, nameY + 36f);
        if (subtitleText != null) subtitleText.rectTransform.anchoredPosition = new Vector2(subtitleText.rectTransform.anchoredPosition.x, nameY - 36f);
    }

    public void Bind(LeaderboardEntry e, UITheme theme, LeaderboardSkin skin, LeaderboardTab tab)
    {
        if (e == null) return;

        // ── Haftalık top-3: BÜYÜK kart yüksekliği (self/yeşil satır DAHİL) + hediye kutusu ──
        // Yükseklik ve award ikonu self dahil tüm weekly top-3 satırlarında gösterilir.
        ApplyLayout(skin);

        bool weeklyTop3     = tab == LeaderboardTab.Weekly && e.rank <= 3;
        bool weeklyTop3Gift = weeklyTop3;
        // Kürsü: Haftalık + Oyuncular + Takım sekmelerinde ilk 3 belirgin büyük (Arkadaşlar kısa liste → normal).
        bool podium = e.rank <= 3 && tab != LeaderboardTab.Friends;
        var le = GetComponent<UnityEngine.UI.LayoutElement>();
        if (le != null && skin != null)
            le.preferredHeight = podium ? PodiumHeight(e, skin) : skin.rowHeight;
        ApplyPodium(podium, skin);

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
            {
                float size = podium ? skin.giftIconSize * 1.25f : skin.giftIconSize;
                giftIcon.rectTransform.sizeDelta = new Vector2(size, size);
                if (layoutApplied)
                    giftIcon.rectTransform.anchoredPosition = new Vector2(
                        -(skin.scoreRightPad + podiumInset) - RightColumnWidth - 12f, 0f);
            }
        }
        if (layoutApplied && skin != null && capacityRoot != null)
            ((RectTransform)capacityRoot.transform).anchoredPosition = new Vector2(
                -(skin.scoreRightPad + podiumInset) - RightColumnWidth - 12f, 0f);

        // ── Rütbe: top-3 madalya sprite'ı (sayı ÇİZİLİ → yazı gizlenir); yoksa plaka + sayı.
        // Madalya gelmeden top-3: plaka madalya rengine boyanır, sayı görünür kalır.
        Sprite medal = e.rank switch
        {
            1 => skin != null ? skin.medalGold   : null,
            2 => skin != null ? skin.medalSilver : null,
            3 => skin != null ? skin.medalBronze : null,
            _ => null,
        };
        // 4+ haneli sıra (pinli kendi satırında 14977 gibi): halka kalkar, sayı düz ve okunur yazılır
        // (RM). 1-3 hane: sayı halkanın İÇ alanına sığdırılır (eskiden rozetin tamamını kaplayıp halkaya biniyordu).
        bool longRank = medal == null && e.rank >= 1000;
        if (rankText != null)
        {
            rankText.text = e.rank.ToString();
            rankText.gameObject.SetActive(medal == null);
            if (!rankText.enableAutoSizing)
            {
                rankText.fontSizeMax = rankText.fontSize;
                rankText.enableAutoSizing = true;
            }
            rankText.fontSizeMin = Mathf.Max(12f, rankText.fontSizeMax * 0.4f);
            rankText.textWrappingMode = TextWrappingModes.NoWrap;
            rankText.overflowMode = TextOverflowModes.Overflow;
            rankText.alignment = TextAlignmentOptions.Center;

            var rt = rankText.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            if (longRank)
            {
                // Halka yok → rozet kutusunun biraz dışına da taşabilir (avatara kadar boşluk var).
                rt.offsetMin = new Vector2(-8f, 0f);
                rt.offsetMax = new Vector2(8f, 0f);
            }
            else
            {
                float inset = rankBadge != null ? rankBadge.rectTransform.rect.width * 0.17f : 12f;
                rt.offsetMin = new Vector2(inset, inset);
                rt.offsetMax = new Vector2(-inset, -inset);
            }
            // Yeşil (kendi) satırda düz sayı beyaz okunsun; diğerlerinde prefab rengi korunur.
            if (e.isSelf && longRank) rankText.color = Color.white;
        }
        if (rankBadge != null)
        {
            Sprite badge = medal != null ? medal : (skin != null ? skin.rankPlate : null);
            rankBadge.sprite = badge;
            rankBadge.enabled = badge != null && !longRank;
            rankBadge.preserveAspect = true;
            rankBadge.color = medal != null || e.rank > 3 ? Color.white : e.rank switch
            {
                1 => theme != null ? theme.goldTrim : new Color(1f, 0.8f, 0.25f),
                2 => new Color(0.75f, 0.78f, 0.85f),
                _ => new Color(0.80f, 0.52f, 0.30f),
            };
        }

        // ── Avatar + çerçeve ──
        if (avatar != null)
        {
            avatar.sprite = e.avatar;
            avatar.enabled = e.avatar != null;
            avatar.preserveAspect = true;
        }
        // Takım sekmesinde avatar = kalkan amblemi → daire maske/zemin kapanır, kalkan bütün görünür.
        bool showCircle = tab != LeaderboardTab.Team;
        if (avatarMask != null) avatarMask.enabled = showCircle;
        if (avatarMaskImage != null) avatarMaskImage.enabled = showCircle;
        if (avatarCircleImage != null) avatarCircleImage.enabled = showCircle;
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
        // Arkadaşlar sekmesinde seviye sağ sütunda büyük yazılır (RM "Bölüm 2669") → solda tekrar etme.
        bool friendsTab = tab == LeaderboardTab.Friends;
        bool showChapter = e.chapter > 0 && tab != LeaderboardTab.Team && !friendsTab;
        if (chapterText != null)
        {
            chapterText.gameObject.SetActive(showChapter);
            if (showChapter) chapterText.text = string.Format(GameLocalization.Get("leaderboard_level"), e.chapter);   // "Seviye N" / "Level N"
        }
        if (nameText != null) nameText.text = e.playerName;
        if (!layoutApplied) SingleLineText.Fit(nameText);
        // Takım satırında alt yazı üye sayısıysa ("44/50") kapasite kutusuyla aynı bilgi → tekrar etme.
        bool showSubtitle = !string.IsNullOrEmpty(e.subtitle)
            && !(e.capacityMax > 0 && e.subtitle == $"{e.capacityCurrent}/{e.capacityMax}");
        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(showSubtitle);
            subtitleText.text = e.subtitle;
        }
        if (layoutApplied) ArrangeInfo(showChapter && chapterText != null, showSubtitle && subtitleText != null);

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
        // Sağ sütun: Arkadaşlar = seviye yarışı (RM) → "Seviye / 270"; diğerleri "Puan / 32,605".
        bool showRight = !friendsTab || e.chapter > 0;
        if (scoreLabel != null)
        {
            scoreLabel.gameObject.SetActive(showRight);
            scoreLabel.text = friendsTab
                ? string.Format(GameLocalization.Get("leaderboard_level"), "").Trim()
                : GameLocalization.Get("leaderboard_score");
        }
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(showRight);
            scoreText.text = friendsTab ? e.chapter.ToString("N0") : e.score.ToString("N0");
            scoreText.textWrappingMode = TextWrappingModes.NoWrap;   // büyük puan asla iki satıra kaymaz
        }

        // Kupa varsa (puan sekmeleri): [kupa][sayı] — "Puan" etiketi yerine ikon (RM). Yoksa etiket + sayı.
        bool useTrophy = trophyImage != null && showRight && !friendsTab;
        if (trophyImage != null) trophyImage.gameObject.SetActive(useTrophy);
        if (layoutApplied && skin != null && scoreText != null)
        {
            float rightX = -(skin.scoreRightPad + podiumInset);
            float colLeft = rightX - RightColumnWidth;
            if (useTrophy)
            {
                if (scoreLabel != null) scoreLabel.gameObject.SetActive(false);
                Place(trophyImage.rectTransform, 1f, colLeft + TrophySize, 0f, TrophySize, TrophySize);
                Place(scoreText.rectTransform, 1f, rightX, 0f, RightColumnWidth - TrophySize - 8f, 56f);
                scoreText.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                Place(scoreText.rectTransform, 1f, rightX, -14f, RightColumnWidth, 56f);
                scoreText.alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        // ── Zemin: öncelik self > top-3 kartı > normal satır ──
        if (rowBackground != null && theme != null)
        {
            Sprite bgSprite;
            if (e.isSelf && skin != null && skin.selfRowBackground != null)
                bgSprite = skin.selfRowBackground;
            else if (podium && !e.isSelf && skin != null && skin.topThreeCardBackground != null)
                bgSprite = skin.topThreeCardBackground;
            else
                bgSprite = skin != null ? skin.rowBackground : null;

            if (bgSprite != null)
            {
                rowBackground.sprite = bgSprite;
                bool podiumCard = skin != null && bgSprite == skin.topThreeCardBackground;
                // Kürsü kartı oranında tam çizilir (Simple); diğer satırlar 9-slice.
                rowBackground.type = podiumCard ? Image.Type.Simple : Image.Type.Sliced;
                rowBackground.preserveAspect = false;
                rowBackground.color = podiumCard ? skin.PodiumTint(e.rank)
                    : (!e.isSelf && skin != null ? skin.rowTint : Color.white);
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
            // RM hiyerarşisi: isim koyu lacivert ve baskın, alt yazı/etiketler soluk kahve, değer koyu mor.
            // Kendi (yeşil) satırında hepsi beyaz. rankText rengine DOKUNMA → prefab rengi.
            theme.ApplyText(nameText, e.isSelf ? theme.textLight : NameColor, heading: true);
            theme.ApplyText(chapterText, e.isSelf ? theme.textLight : LabelColor, heading: true);
            theme.ApplyText(scoreText, e.isSelf ? theme.textLight : ValueColor, heading: true);
            theme.ApplyText(scoreLabel, e.isSelf ? theme.textLight : LabelColor, heading: true);
            theme.ApplyText(capacityLabel, e.isSelf ? theme.textLight : LabelColor, heading: true);
            theme.ApplyText(capacityText, e.isSelf ? theme.textLight : theme.textOnCream, heading: true);
            theme.ApplyText(subtitleText, e.isSelf ? theme.textLight : LabelColor, heading: true);
        }

        // Buğusuz yazı: fontun yumuşak kontur/gölgeli varsayılan materyali yerine net kopya.
        foreach (var t in new[] { nameText, chapterText, subtitleText, scoreText, scoreLabel, capacityLabel, capacityText, rankText })
            CrispTextMaterial.Apply(t);

        FitInfoWidth(skin, e.capacityMax > 0, giftIcon != null && giftIcon.enabled);
    }
}
