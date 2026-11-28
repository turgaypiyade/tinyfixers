using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Haftalık sekmenin kürsüsü (2026-11-24 tasarım; ChatGPT görselleri Art/UI/Leaderboard/V2): perdeli atölye sahnesi,
/// "Haftalık Yarış" kurdele tabelası + "i" butonu, 2-1-3 altın/gümüş/bronz kartlar (avatar, isim, hediye, puan, madalya).
/// Listenin İLK çocuğu olarak kayar; ilk 3 sıra satır olarak tekrar basılmaz. Bir kez kurulur, her Render'da yalnız
/// yeniden bağlanır (yeni nesne yok). Tüm yerleşim kök dikdörtgene göre normalize (ekran genişliğinden bağımsız).
/// </summary>
public sealed class WeeklyPodiumView : MonoBehaviour
{
    // Kök yüksekliği = RootAspect × genişlik. Sahne görseli (oranı ~1.07) alttan hizalı ve daha uzun: üstteki boş
    // kemer kısmı yukarı taşar (viewport keser) → kürsü kısalır, altta 3-4 satır görünür (kullanıcı 2026-11-24).
    private const float RootAspect = 0.76f;
    private const float PlatformFromBottom = 0.0375f;   // kartların bastığı platform, genişliğe oran (alttan)

    private sealed class Card
    {
        public RectTransform root;
        public Image body, ring, avatar, gift, medal;
        public TMP_Text name, score, level;
        public Button giftButton;
    }

    private LeaderboardSkin skin;
    private UITheme theme;
    private RectTransform rt;
    private LayoutElement layout;
    private Image stage;
    private TMP_Text bannerText;
    private Button infoButton;
    private GameObject infoRoot;
    private readonly Card[] cards = new Card[3];   // index = rank - 1
    private static Sprite circle;

    public static WeeklyPodiumView Create(RectTransform parent, LeaderboardSkin skin, UITheme theme)
    {
        var go = new GameObject("WeeklyPodium", typeof(RectTransform), typeof(LayoutElement));
        go.layer = parent.gameObject.layer;   // runtime UI: kamera katmanı
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<WeeklyPodiumView>();
        view.Build(skin, theme);
        return view;
    }

    private void Build(LeaderboardSkin s, UITheme t)
    {
        skin = s; theme = t;
        rt = (RectTransform)transform;
        layout = GetComponent<LayoutElement>();

        stage = NewImage("Stage", rt, skin.weeklyStageBackground);
        stage.preserveAspect = false;
        float stageAspect = StageAspect();
        Box(stage.rectTransform, 0f, 1f - stageAspect / RootAspect, 1f, 1f);   // alttan hizalı, üstü taşar

        // Kartlar: önce 2 ve 3 (arkada), sonra 1 (önde).
        cards[1] = BuildCard(rt, skin.podiumCardSilver, skin.medalSilver, skin.giftTier2, 0.185f, 0.235f, false, new Color(0.30f, 0.36f, 0.48f));
        cards[2] = BuildCard(rt, skin.podiumCardBronze, skin.medalBronze, skin.giftTier3, 0.815f, 0.225f, false, new Color(0.42f, 0.20f, 0.08f));
        cards[0] = BuildCard(rt, skin.podiumCardGold, skin.medalGold, skin.giftTier1, 0.5f, 0.30f, true, new Color(0.55f, 0.35f, 0.02f));

        var banner = NewImage("Banner", rt, skin.weeklyRaceBanner);
        PlaceTop(banner.rectTransform, 0.5f, 0.02f / RootAspect, 0.62f, banner.sprite);
        bannerText = NewText("Title", banner.rectTransform, theme != null ? theme.headingFont : null, 52f);
        Box(bannerText.rectTransform, 0.20f, 0.18f, 0.80f, 0.80f);
        // Dünya/Türkiye hapları ile AYNI stil: TinyFixersPop + yazı kuralı materyali (yüzey: lacivert kurdele).
        // Font sekmeye göre DEĞİŞTİRİLMEZ — sekme geçişinde font/materyal çakışması başlığı bozuyordu.
        TextSurfaceStyle.Apply(bannerText, TextSurface.Blue);

        var info = NewImage("Info", rt, skin.infoButton);
        info.raycastTarget = true;
        PlaceTop(info.rectTransform, 0.925f, 0.035f / RootAspect, 0.09f, info.sprite);
        infoRoot = info.gameObject;
        infoButton = info.gameObject.AddComponent<Button>();
        infoButton.transition = Selectable.Transition.None;
        infoButton.onClick.AddListener(ShowInfo);
    }

    private Card BuildCard(RectTransform parent, Sprite body, Sprite medal, Sprite gift, float centerX, float width, bool first,
        Color surface)
    {
        var c = new Card();
        c.body = NewImage(first ? "Card1" : "Card", parent, body);
        c.root = c.body.rectTransform;
        PlaceBottom(c.root, centerX, 1f - PlatformFromBottom / RootAspect, width, body);

        // Avatar: halka + daire maskeli resim, kartın üst kenarına biner.
        float aspect = body != null ? body.rect.width / body.rect.height : 0.6f;
        float avW = first ? 0.64f : 0.66f;
        float avH = avW * aspect;
        float avCenter = first ? 0.10f : 0.12f;
        c.ring = NewImage("Ring", c.root, Circle());
        c.ring.color = first ? new Color(1f, 0.82f, 0.25f) : Color.white;
        Box(c.ring.rectTransform, 0.5f - avW * 0.56f, avCenter - avH * 0.56f, 0.5f + avW * 0.56f, avCenter + avH * 0.56f);
        var mask = NewImage("Mask", c.ring.rectTransform, Circle());
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Box(mask.rectTransform, 0.06f, 0.06f, 0.94f, 0.94f);
        c.avatar = NewImage("Avatar", mask.rectTransform, null);
        c.avatar.preserveAspect = true;
        Stretch(c.avatar.rectTransform);

        c.name = NewText("Name", c.root, theme != null ? theme.bodyFont : null, first ? 60f : 54f);
        c.name.color = theme != null ? theme.textOnCream : new Color32(90, 62, 43, 255);
        c.name.fontSizeMin = c.name.fontSizeMax * 0.32f;   // 12 karakterlik isim ("Oyuncu 72737") dar plakaya tam sığsın
        Box(c.name.rectTransform, 0.12f, 0.385f, 0.88f, 0.47f);

        float gW = first ? 0.44f : 0.52f, gH = gW * aspect;
        c.gift = NewImage("Gift", c.root, gift);
        Box(c.gift.rectTransform, 0.5f - gW * 0.5f, 0.605f - gH * 0.5f, 0.5f + gW * 0.5f, 0.605f + gH * 0.5f);
        // Haftalık'ta hediyeye dokununca içerik balonu (RM/MV gibi). Oyuncular sekmesinde kupa → dokunuş kapalı.
        int rank = first ? 1 : (centerX < 0.5f ? 2 : 3);
        var giftButton = c.gift.gameObject.AddComponent<Button>();
        giftButton.transition = Selectable.Transition.None;
        giftButton.onClick.AddListener(() => ShowRewardBubble(rank, c.gift.rectTransform));
        c.giftButton = giftButton;

        // Ödülsüz sekmede (Oyuncular) hediyenin yerinde "Seviye N" — beyaz yüz, kart renginin koyusu kontur.
        c.level = NewText("Level", c.root, theme != null ? theme.headingFont : null, first ? 58f : 50f);
        Box(c.level.rectTransform, 0.08f, 0.50f, 0.92f, 0.71f);
        TextSurfaceStyle.Apply(c.level, surface);

        c.score = NewText("Score", c.root, theme != null ? theme.bodyFont : null, first ? 64f : 56f);
        c.score.color = new Color32(92, 58, 170, 255);
        Box(c.score.rectTransform, 0.10f, 0.74f, 0.90f, 0.84f);

        float mW = 0.42f, mH = mW * aspect;
        c.medal = NewImage("Medal", c.root, medal);
        Box(c.medal.rectTransform, 0.5f - mW * 0.5f, 1.0f - mH * 0.5f, 0.5f + mW * 0.5f, 1.0f + mH * 0.5f);
        return c;
    }

    /// <summary>İlk 3 girdiyi bağlar (rank 1..3). Eksik sıra kartı gizlenir. Kök yüksekliği sahne oranından.</summary>
    /// <summary>bleedX: liste yan boşluğu (px) — sahne bu kadar dışarı taşıp kenardan kenara uzanır.
    /// rewards: Haftalık (hediye + i butonu); false = Oyuncular (hediye yerine seviye, i yok).</summary>
    public void Bind(IReadOnlyList<LeaderboardEntry> top, float width, float bleedX, bool rewards, string title)
    {
        if (infoRoot != null) infoRoot.SetActive(rewards);
        if (stage != null)
        {
            var srt = stage.rectTransform;
            srt.offsetMin = new Vector2(-bleedX, 0f);
            srt.offsetMax = new Vector2(bleedX, 0f);
        }
        if (bannerText != null) bannerText.text = title;

        if (layout != null) layout.preferredHeight = Mathf.Max(400f, width + 2f * bleedX) * RootAspect;

        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            LeaderboardEntry e = null;
            if (top != null)
                for (int k = 0; k < top.Count; k++)
                    if (top[k] != null && top[k].rank == i + 1) { e = top[k]; break; }
            c.root.gameObject.SetActive(e != null);
            if (e == null) continue;
            c.avatar.sprite = e.avatar;
            c.avatar.enabled = e.avatar != null;
            c.name.text = e.playerName;
            c.score.text = e.score.ToString("N0");   // satırlarla aynı biçim
            bool showLevel = !rewards && e.chapter > 0;
            // Ödülsüz sekmede seviye bilinmiyorsa (diğer oyuncuların seviyesi tabloya gelmiyor) ortada kupa.
            var mid = rewards ? GiftOf(i) : (showLevel ? null : (skin != null ? skin.trophyIcon : null));
            c.gift.sprite = mid;
            c.gift.enabled = mid != null;
            c.gift.raycastTarget = rewards && mid != null;
            if (c.giftButton != null) c.giftButton.enabled = rewards;
            if (!rewards) HideRewardBubble();
            c.level.gameObject.SetActive(showLevel);
            if (showLevel) c.level.text = string.Format(GameLocalization.Get("leaderboard_level"), e.chapter);
        }
    }

    // Kök boyutu değişince (ekran ilk açılışta genişlik geç oturur) yazılar bir sonraki karede yeniden çizilir.
    // Bileşen yalnız bu iş için kısa süre açılır; boşta LateUpdate çalışmaz.
    private void OnRectTransformDimensionsChange()
    {
        if (cards[0] != null && isActiveAndEnabled == false && gameObject.activeInHierarchy) enabled = true;
    }

    private void LateUpdate()
    {
        RefreshTexts();
        enabled = false;
    }

    // ── Ödül balonu ─────────────────────────────────────────────────
    // Ortak RewardPeekBubble (krem zemin + lacivert çerçeve; kullanıcı 2026-11-28). Kök = podyum: balon kök içinde
    // kalır, başka yere dokununca kapanır; aynı hediyeye yeniden dokunmak da kapatır.
    private readonly List<RewardPeekBubble.Item> bubbleItems = new();

    private void ShowRewardBubble(int rank, RectTransform anchor)
    {
        var rewards = WeeklyRaceRewards.RewardsFor(rank);
        bubbleItems.Clear();
        foreach (var r in rewards)
            bubbleItems.Add(new RewardPeekBubble.Item(r.ResolveIcon(),
                r.IsTimed ? r.AmountLabel : r.type == DailySlotRewardType.Coins ? r.amount.ToString() : "x" + r.amount));
        RewardPeekBubble.On(rt)?.Toggle(rank, null, bubbleItems, anchor);
    }

    private void HideRewardBubble()
    {
        for (int i = 0; i < rt.childCount; i++)
            if (rt.GetChild(i).TryGetComponent(out RewardPeekBubble peek)) peek.Hide();
    }

    private Sprite GiftOf(int index) => skin == null ? null
        : index == 0 ? skin.giftTier1 : index == 1 ? skin.giftTier2 : skin.giftTier3;

    /// <summary>Yerleşim (genişlik) netleşince yazıları yeniden çizer — ilk kurulumda boyut 0 iken çizilen
    /// otomatik boyutlu yazı bozuk kalıyordu (kullanıcı 2026-11-24).</summary>
    public void RefreshTexts()
    {
        if (bannerText != null) bannerText.ForceMeshUpdate(true);
        foreach (var c in cards)
        {
            if (c == null) continue;
            c.name.ForceMeshUpdate(true);
            c.score.ForceMeshUpdate(true);
            if (c.level.gameObject.activeSelf) c.level.ForceMeshUpdate(true);
        }
    }

    // Popup yerine ortak göz atma balonu (kullanıcı 2026-11-28).
    private void ShowInfo()
    {
        string text = "<b>" + GameLocalization.GetOr("leaderboard_weekly_race", "Haftalık Yarış") + "</b>\n"
            + GameLocalization.GetOr("leaderboard_weekly_info",
                "Bu hafta kazandığın seviyelerden puan topla. Hafta bitince ilk 3'e ödül sandığı verilir.");
        RewardPeekBubble.On(rt)?.Toggle(infoButton, text, null, (RectTransform)infoRoot.transform);
    }

    // ── Yerleşim yardımcıları (y yukarıdan aşağı, kök/ebeveyn dikdörtgenine oran) ──

    private static void Box(RectTransform r, float x0, float y0, float x1, float y1)
    {
        r.anchorMin = new Vector2(x0, 1f - y1);
        r.anchorMax = new Vector2(x1, 1f - y0);
        r.offsetMin = r.offsetMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void Stretch(RectTransform r) => Box(r, 0f, 0f, 1f, 1f);

    // Genişlik oranı verilir; yükseklik sprite oranından (kök kare değil: sahne oranı kadar uzun).
    private void PlaceTop(RectTransform r, float centerX, float top, float width, Sprite s)
    {
        float h = HeightFor(width, s);
        Box(r, centerX - width * 0.5f, top, centerX + width * 0.5f, top + h);
    }

    private void PlaceBottom(RectTransform r, float centerX, float bottom, float width, Sprite s)
    {
        float h = HeightFor(width, s);
        Box(r, centerX - width * 0.5f, bottom - h, centerX + width * 0.5f, bottom);
    }

    private float HeightFor(float widthNorm, Sprite s)
    {
        float spriteRatio = s != null ? s.rect.height / s.rect.width : 1f;
        return widthNorm * spriteRatio / RootAspect;
    }

    private float StageAspect()
    {
        var bg = skin != null ? skin.weeklyStageBackground : null;
        return bg != null ? bg.rect.height / bg.rect.width : 1.07f;
    }

    private Image NewImage(string n, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = sprite != null || n == "Mask" || n == "Avatar";
        return img;
    }

    private TMP_Text NewText(string n, RectTransform parent, TMP_FontAsset font, float max)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true;
        t.fontSizeMax = max;
        t.fontSizeMin = max * 0.45f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    // Daire maskesi/halka için tek seferlik yumuşak kenarlı daire.
    private static Sprite Circle()
    {
        if (circle != null) return circle;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[N * N];
        float c = (N - 1) * 0.5f, r = N * 0.5f - 1f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                px[y * N + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        circle = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return circle;
    }
}
