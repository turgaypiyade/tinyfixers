using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pençe Makinesi ekranı (plan §4, §9). Kod ile kurulur, bir kez; oyuncak görünümleri yeniden kullanılır.
/// 2.5D (2026-11-28): joystick pençeyi yalnız sağa (u) ve geriye (w, derinlik) götürür; ortasına dokunmak İNDİR.
/// Fizik yok: sonuç ClawMachineState.TryGrab ile İNDİR anında kalıcı yazılır, bu ekran yalnız o sonucu oynatır
/// (iniş → kapanma → yükseliş [kayma/boş] → bacaya taşıma → ödül → başlangıca dönüş). Tek Update, yalnız ekran açıkken.
/// Makine üç parçadır; cam bölümü ekran yüksekliğine göre uzar (pençe ile oyuncaklar arasında iniş payı).
/// </summary>
public sealed class ClawMachineScreen : MonoBehaviour
{
    private enum Phase { Ready, Descend, Close, Lift, Carry, Release, Reward, Refill, Home }

    // Tasarım alanı (1000×1800) — güvenli alana ölçeklenir.
    // 2026-11-28: makine 720→820 genişledi (üst/alt görseller enle büyür, cam kısalır) + cam %5 kesilir.
    private const float MachineWidth = 820f, MachineTopY = 655f, MachineBottomY = -720f;   // 2026-11-28: raf kalktı, makine 70 yukarı
    // Kaynak görsel oranları (Machine_Top içinde ray, Machine_Bottom içinde cam tabanı ve hazne).
    private const float RailInTop = 0.76f, FloorInBottom = 0.26f, GlassInset = 0.06f;
    private const float ChuteX = 0.21f, ChuteInBottom = 0.40f;
    private const float GlassCut = 0.05f, BackdropDim = 0.38f;
    // Baca (ClawChute_Back/Front): genişlik, cam tabanına gömülme, kutu tabanının yüksekliği (alttan oran);
    // hazne penceresi Machine_Bottom'da üstten %62.
    private const float ReleaseFall = 0.5f, ReleaseBounce = 0.68f;
    private const float ChuteBoxWidth = 0.244f * MachineWidth, ChuteSink = 6f, ChuteFloorInBox = 0.2f, HatchInBottom = 0.62f;
    private const float ClawWidth = 165f, CapsuleWidth = 0.17f, PlushWidth = 0.19f;
    // Derinlik (w 0..1): arka taban yukarıda, nesneler küçük, yatay hafif daralır (perspektif).
    private const float DepthLift = 120f, DepthShrink = 0.28f, DepthNarrow = 0.1f;
    private const float JoystickY = -815f, JoystickSize = 176f, KnobSize = 96f, HintY = 722f;

    private sealed class ItemView
    {
        public RectTransform root;
        public Image back, icon, bottom, front, plush;
        public float height;
    }

    private ClawMachineConfig cfg;
    private RectTransform root, design, machine, itemsLayer, claw, cable, iconOrigin, rewardRoot, intro;
    private Image clawImage, background, rewardIcon;
    private TMP_Text title, timer, tokensText, powerText, hint, rewardText;
    private readonly Image[] collectionPlush = new Image[ClawMachineConfig.PlushCount];
    private readonly Image[] powerPips = new Image[5];
    private ItemView[] items = Array.Empty<ItemView>();
    private Button buyButton;
    private EventScreenIris iris;
    private float railY, floorY, interiorX0, interiorW, chuteX, chuteY, clawRestTop;
    private float playX0, playW, hatchY, chuteLandY, chuteTopY, bottomTopY, bottomHeight;
    private float phaseTime, phaseLength, nextSecond;
    // Pençe: mantıksal konum (u, w), sunumdaki derinlik (taşıma/dönüşte değişir), sallanma, süre.
    private float clawU, clawW, clawDepth, swayAmp, swayT, swayOffset, moveLeft;
    private bool moveStarted, moveTimed;
    private int shownSecond = -1;
    private float homeFromX, homeFromDepth, carryFromDepth;
    private ClawMachineJoystick joystick;
    private Image clawShadow;
    private Phase phase;
    private ClawMachineState.Attempt attempt;
    private Vector2 carryFrom;
    private float releaseFromY;
    private bool closing, focused = true, needsCollectionCeremony, slipLanded, releaseLanded;
    private int width, height;
    private static Texture2D gradient;

    public void Build(ClawMachineConfig config)
    {
        cfg = config;
        ClawMachineUI.Font = cfg.font;
        ClawMachineUI.BodyFont = cfg.bodyFont;
        root = (RectTransform)transform;
        background = root.gameObject.AddComponent<Image>();
        background.sprite = GradientSprite(cfg.backgroundTop, cfg.backgroundBottom);
        background.raycastTarget = true;   // modal: alttaki menüye dokunuş geçmez
        BuildBackdrop();
        design = ClawMachineUI.Rect("Design", root, new Vector2(1000, 1800), Vector2.zero);

        title = ClawMachineUI.Label("Title", design, "", 62, new Vector2(760, 90), new Vector2(0, 845), TextSurfaceStyle.Base(TextSurface.Purple));
        timer = ClawMachineUI.Label("Timer", design, "", 32, new Vector2(600, 50), new Vector2(0, 796), TextSurfaceStyle.Base(TextSurface.Purple));
        var close = ClawMachineUI.Picture("Close", design, cfg.closeIcon, new Vector2(96, 96), new Vector2(440, 840));
        close.raycastTarget = true;
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close; closeButton.onClick.AddListener(Close);

        BuildMachine();
        BuildMarquee();
        BuildCoinHud();
        BuildHud();
        BuildReward();
        BuildIntro();
        iris = EventScreenIris.For(root);
    }

    // Atari salonu görseli ekranı orantılı doldurur (taşan kırpılır); üstünde koyu perde makineyi öne çıkarır.
    private void BuildBackdrop()
    {
        if (cfg.background == null) return;
        var fitRoot = ClawMachineUI.Stretch("Backdrop", root);
        var img = ClawMachineUI.Picture("Image", fitRoot, cfg.background, Vector2.one, Vector2.zero, false);
        var fit = img.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = cfg.background.rect.width / Mathf.Max(1f, cfg.background.rect.height);
        var dim = ClawMachineUI.Stretch("Dim", fitRoot).gameObject.AddComponent<Image>();
        dim.color = new Color(0.04f, 0.01f, 0.06f, BackdropDim); dim.raycastTarget = false;
    }

    // Koleksiyon makinenin tabelasında (kullanıcı 2026-11-28; raf kaldırıldı): eksik peluş gri, yakalanınca
    // hazneden uçup yerine oturur. Tabelaya sığsınlar diye ±10° eğik. Oranlar Machine_Top.png'deki kırmızı panelden.
    private const float MarqueeX0 = 0.21f, MarqueeX1 = 0.80f, MarqueeY = 0.435f, MarqueePlushH = 0.205f, MarqueeTilt = 10f;
    private const float PlushFlyTime = 0.75f, PlushPopTime = 0.2f;
    private static readonly Color MissingPlush = new Color(0.62f, 0.6f, 0.66f, 0.85f);
    private static Material grayMaterial;
    private float topCenterY, topHeight, marqueePlushSize;
    private Image plushFly;
    private int pendingReveal = -1;
    private float plushAnim = -1f;
    private Vector2 plushFrom;

    private Vector2 MarqueeSlot(int i)
    {
        float step = (MarqueeX1 - MarqueeX0) / collectionPlush.Length;
        return new Vector2((MarqueeX0 + step * (i + 0.5f) - 0.5f) * MachineWidth, topCenterY + (0.5f - MarqueeY) * topHeight);
    }
    private static Quaternion MarqueeRotation(int i) => Quaternion.Euler(0f, 0f, i % 2 == 0 ? MarqueeTilt : -MarqueeTilt);

    private void BuildMarquee()
    {
        if (grayMaterial == null) grayMaterial = Resources.Load<Material>("UIEffects/UIGrayscale");
        marqueePlushSize = topHeight * MarqueePlushH;
        for (int i = 0; i < collectionPlush.Length; i++)
        {
            var img = ClawMachineUI.Picture("Plush" + i, machine, cfg.PlushSprite(i), Vector2.one * marqueePlushSize, MarqueeSlot(i));
            img.rectTransform.localRotation = MarqueeRotation(i);
            collectionPlush[i] = img;
        }
        plushFly = ClawMachineUI.Picture("PlushFly", machine, null, Vector2.one * 130f, Vector2.zero);
        plushFly.gameObject.SetActive(false);
    }

    private void SetPlushOwned(int i, bool owned)
    {
        var img = collectionPlush[i];
        img.material = owned || grayMaterial == null ? null : grayMaterial;
        img.color = owned ? Color.white : MissingPlush;
    }

    // Yeni peluş: hazne → tabeladaki yeri (yay), yerine oturunca kısa büyüyüp küçülme.
    private void StartPlushFly(int id)
    {
        plushFrom = rewardRoot.anchoredPosition + rewardIcon.rectTransform.anchoredPosition;
        plushFly.sprite = cfg.PlushSprite(id);
        plushFly.gameObject.SetActive(true);
        plushFly.transform.SetAsLastSibling();
        plushAnim = 0f;
        TickPlushFly(0f);
    }

    private void TickPlushFly(float dt)
    {
        plushAnim += dt;
        int id = pendingReveal;
        if (id < 0) { StopPlushFly(); return; }
        var to = MarqueeSlot(id);
        if (plushAnim < PlushFlyTime)
        {
            float t = EaseInOut(plushAnim / PlushFlyTime);
            var rt = plushFly.rectTransform;
            rt.anchoredPosition = Vector2.Lerp(plushFrom, to, t) + new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 120f);
            rt.sizeDelta = Vector2.one * Mathf.Lerp(130f, marqueePlushSize, t);
            rt.localRotation = Quaternion.Slerp(Quaternion.identity, MarqueeRotation(id), t);
            return;
        }
        if (plushFly.gameObject.activeSelf)
        {
            plushFly.gameObject.SetActive(false);
            SetPlushOwned(id, true);
            EventSfx.Play(l => l.stepPop);
        }
        float pop = Mathf.Clamp01((plushAnim - PlushFlyTime) / PlushPopTime);
        collectionPlush[id].rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(pop * Mathf.PI) * 0.3f);
        if (pop >= 1f)
        {
            StopPlushFly();
            TryCollectionCeremony();
        }
    }

    private void StopPlushFly()
    {
        if (pendingReveal >= 0) collectionPlush[pendingReveal].rectTransform.localScale = Vector3.one;
        pendingReveal = -1;
        plushAnim = -1f;
        if (plushFly != null) plushFly.gameObject.SetActive(false);
        RefreshCollection();
    }

    private void RefreshCollection()
    {
        for (int i = 0; i < collectionPlush.Length; i++)
            if (collectionPlush[i] != null) SetPlushOwned(i, ClawMachineState.HasPlush(i) && i != pendingReveal);
    }

    // Üstte altın bakiyesi (altınla jeton alımı için oyuncu bakiyesini görür).
    private TMP_Text coinsText;

    private void BuildCoinHud()
    {
        var pill = ClawMachineUI.Picture("Coins", design, BridgeRepairUI.Pill(), new Vector2(200, 64), new Vector2(-370, 845), false);
        pill.type = Image.Type.Sliced;
        pill.color = new Color(0f, 0f, 0f, 0.35f);
        var lib = TileIconLibrary.Shared;
        var icon = lib != null ? lib.GetRewardIcon(DailySlotRewardType.Coins) : null;
        ClawMachineUI.Picture("Icon", pill.rectTransform, icon, new Vector2(70, 70), new Vector2(-82, 2));
        coinsText = ClawMachineUI.Label("Amount", pill.rectTransform, "", 34, new Vector2(130, 56), new Vector2(22, 0), TextSurfaceStyle.Base(TextSurface.Purple));
    }

    private void RefreshCoins(int _ = 0)
    {
        if (coinsText != null) coinsText.text = PlayerWallet.Coins.ToString("N0");
    }

    private void BuildMachine()
    {
        machine = ClawMachineUI.Rect("Machine", design, new Vector2(MachineWidth, MachineTopY - MachineBottomY), Vector2.zero);
        float topH = ClawMachineUI.SizeForWidth(cfg.machineTop, MachineWidth).y;
        float bottomH = ClawMachineUI.SizeForWidth(cfg.machineBottom, MachineWidth).y;
        float glassH = Mathf.Max(120f, MachineTopY - MachineBottomY - topH - bottomH);
        // Cam bölümü kısaltılır (kullanıcı 2026-11-28: %5-10); makine dikeyde ortalı kalır.
        float cut = glassH * GlassCut;
        glassH -= cut;
        float machineTop = MachineTopY - cut * 0.5f;
        float topCenter = machineTop - topH * 0.5f;
        topCenterY = topCenter; topHeight = topH;
        float glassCenter = machineTop - topH - glassH * 0.5f;
        float bottomTop = machineTop - topH - glassH;
        ClawMachineUI.Picture("Top", machine, cfg.machineTop, new Vector2(MachineWidth, topH), new Vector2(0, topCenter), false);
        ClawMachineUI.Picture("Glass", machine, cfg.machineGlass, new Vector2(MachineWidth, glassH), new Vector2(0, glassCenter), false);
        ClawMachineUI.Picture("Bottom", machine, cfg.machineBottom, new Vector2(MachineWidth, bottomH), new Vector2(0, bottomTop - bottomH * 0.5f), false);

        railY = machineTop - topH * RailInTop;
        floorY = bottomTop - bottomH * FloorInBottom;
        interiorX0 = -MachineWidth * 0.5f + MachineWidth * GlassInset;
        interiorW = MachineWidth * (1f - 2f * GlassInset);
        chuteX = MachineWidth * ChuteX;
        chuteY = bottomTop - bottomH * ChuteInBottom;
        hatchY = bottomTop - bottomH * HatchInBottom;
        bottomTopY = bottomTop; bottomHeight = bottomH;

        // Ödül bacası haznenin tam üstünde, cam tabanında; oyun alanı (pençe + oyuncaklar) bacanın solunda kalır.
        // Kurallar 0..1 oyun alanı koordinatında çalışır → bu daraltma dengeyi değiştirmez.
        Image chuteFront = null;
        playX0 = interiorX0;
        playW = interiorW;
        if (cfg.chuteBack != null)
        {
            var size = ClawMachineUI.SizeForWidth(cfg.chuteBack, ChuteBoxWidth);
            var pos = new Vector2(chuteX, floorY - ChuteSink + size.y * 0.5f);
            chuteLandY = floorY - ChuteSink + size.y * ChuteFloorInBox;
            chuteTopY = floorY - ChuteSink + size.y;
            playW = Mathf.Max(200f, chuteX - ChuteBoxWidth * 0.5f - 14f - interiorX0);
            ClawMachineUI.Picture("ChuteBack", machine, cfg.chuteBack, size, pos, false);
            itemsLayer = ClawMachineUI.Rect("Items", machine, Vector2.zero, Vector2.zero);
            if (cfg.chuteFront != null) chuteFront = ClawMachineUI.Picture("ChuteFront", machine, cfg.chuteFront, size, pos, false);
        }
        else
        {
            chuteLandY = chuteY;
            chuteTopY = floorY + 160f;
            itemsLayer = ClawMachineUI.Rect("Items", machine, Vector2.zero, Vector2.zero);
        }
        int count = cfg.ItemCount;
        items = new ItemView[count];
        // Arka sıra önce çizilir (öndekiler önde kalır).
        for (int row = ClawMachineRules.Rows - 1; row >= 0; row--)
            for (int i = 0; i < count; i++)
                if (ClawMachineRules.Row(i) == row) items[i] = BuildItem(i);
        // Nişan gölgesi: pençenin tabana düşen noktası (derinlik ancak böyle okunur). Oyuncakların üstünde.
        clawShadow = ClawMachineUI.Picture("ClawShadow", machine, GlowSprite(), new Vector2(110f, 30f), Vector2.zero, false);
        clawShadow.color = new Color(0.05f, 0.02f, 0.1f, 0.45f);
        if (chuteFront != null) chuteFront.transform.SetAsLastSibling();

        cable = ClawMachineUI.Rect("Cable", machine, new Vector2(6, 10), new Vector2(0, railY));
        cable.pivot = new Vector2(0.5f, 1f);
        var cableImage = cable.gameObject.AddComponent<Image>();
        cableImage.color = new Color(0.28f, 0.28f, 0.32f, 1f); cableImage.raycastTarget = false;
        clawImage = ClawMachineUI.Picture("Claw", machine, cfg.clawOpen, Vector2.one, Vector2.zero, false);
        claw = clawImage.rectTransform;
        claw.pivot = new Vector2(0.5f, 1f);
        clawRestTop = railY - 8f;
        SetClawSprite(false);

        BuildJoystick();
    }

    private void BuildJoystick()
    {
        var pad = ClawMachineUI.Picture("Joystick", design, BridgeRepairUI.Pill(), Vector2.one * JoystickSize, new Vector2(0f, JoystickY), false);
        pad.type = Image.Type.Sliced;
        pad.color = new Color(0f, 0f, 0f, 0.35f);
        pad.raycastTarget = true;
        var knob = ClawMachineUI.Picture("Knob", pad.rectTransform, BridgeRepairUI.Pill(), Vector2.one * KnobSize, Vector2.zero, false);
        knob.type = Image.Type.Sliced;
        knob.color = new Color(0.36f, 0.78f, 0.25f, 1f);
        ClawMachineUI.Picture("Icon", knob.rectTransform, cfg.clawOpen, Vector2.one * (KnobSize * 0.5f), new Vector2(0f, 10f));
        dropLabel = ClawMachineUI.Label("Drop", knob.rectTransform, "", 20, new Vector2(KnobSize, 26), new Vector2(0f, -26f), TextSurfaceStyle.Base(TextSurface.Green));
        joystick = pad.gameObject.AddComponent<ClawMachineJoystick>();
        joystick.knob = knob.rectTransform;
        joystick.radius = (JoystickSize - KnobSize) * 0.5f + 12f;
        joystick.Tapped = Drop;
    }

    private TMP_Text dropLabel;

    private ItemView BuildItem(int slot)
    {
        var view = new ItemView { root = ClawMachineUI.Rect("Item" + slot, itemsLayer, Vector2.zero, Vector2.zero) };
        view.root.pivot = new Vector2(0.5f, 0f);
        float w = interiorW * CapsuleWidth;
        view.back = ClawMachineUI.Picture("Back", view.root, cfg.capsuleBack, ClawMachineUI.SizeForWidth(cfg.capsuleBack, w), Vector2.zero);
        view.icon = ClawMachineUI.Picture("Icon", view.root, null, new Vector2(w * 0.58f, w * 0.58f), Vector2.zero);
        view.bottom = ClawMachineUI.Picture("Bottom", view.root, cfg.capsuleBottom, ClawMachineUI.SizeForWidth(cfg.capsuleBottom, w), Vector2.zero);
        view.front = ClawMachineUI.Picture("Front", view.root, cfg.capsuleFront, ClawMachineUI.SizeForWidth(cfg.capsuleFront, w), Vector2.zero);
        foreach (var img in new[] { view.back, view.bottom, view.front })
        {
            img.rectTransform.pivot = new Vector2(0.5f, 0f);
            img.rectTransform.anchoredPosition = Vector2.zero;
        }
        view.icon.rectTransform.anchoredPosition = new Vector2(0, w * 0.52f);
        view.plush = ClawMachineUI.Picture("Plush", view.root, null, Vector2.one, Vector2.zero);
        view.plush.rectTransform.pivot = new Vector2(0.5f, 0f);
        return view;
    }

    // Kontrol paneli (ClawPanel.png, kırpılmış görselde oranlar): solda jeton yuvası, sağ üstte sayaç penceresi,
    // altta 3 lamba (Pençe Gücü). Lamba "yanık" hâli kodla: lambanın üstünde sarı parıltı.
    private const float PanelWidth = 0.403f * MachineWidth, PanelCenterX = -0.271f * MachineWidth, PanelInBottom = 0.57f;
    private static readonly Vector2 PanelSlot = new Vector2(0.23f, 0.50f), PanelDisplay = new Vector2(0.625f, 0.353f);
    private static readonly float[] PanelLampX = { 0.447f, 0.621f, 0.797f };
    private const float PanelLampY = 0.724f, PanelLampSize = 0.085f, TokenInsertTime = 0.62f;
    private static readonly Color LampOn = new Color(1f, 0.86f, 0.3f, 1f);
    private static Texture2D glow;
    private static Sprite glowSprite;

    private bool panelHud;
    private Image tokenFly;
    private Vector2 tokenFrom, tokenSlot;
    private float tokenAnim = -1f;

    private void BuildHud()
    {
        if (cfg.controlPanel != null) { BuildPanelHud(); return; }
        float panelY = floorY - 175f;
        float x = -MachineWidth * 0.5f + 175f;
        if (cfg.tokenIcon != null)
        {
            // İkon + sayı (kullanıcı 2026-11-28): "JETON" yazısı yerine bakır jeton.
            ClawMachineUI.Picture("TokenIcon", machine, cfg.tokenIcon, new Vector2(72, 72), new Vector2(x - 100, panelY + 55));
            tokensText = ClawMachineUI.Label("Tokens", machine, "", 44, new Vector2(130, 60), new Vector2(x + 5, panelY + 55), TextSurfaceStyle.Base(TextSurface.Red));
        }
        else
            tokensText = ClawMachineUI.Label("Tokens", machine, "", 40, new Vector2(240, 60), new Vector2(x - 30, panelY + 55), TextSurfaceStyle.Base(TextSurface.Red));
        buyButton = ClawMachineUI.TextButton("Buy", machine, "+", new Vector2(76, 70), new Vector2(x + 120, panelY + 55), UiButtons.Kind.Green, AskBuy);
        powerText = ClawMachineUI.Label("Power", machine, "", 26, new Vector2(300, 44), new Vector2(x + 10, panelY - 8), TextSurfaceStyle.Base(TextSurface.Red));
        for (int i = 0; i < powerPips.Length; i++)
        {
            powerPips[i] = ClawMachineUI.Picture("Pip" + i, machine, BridgeRepairUI.Pill(), new Vector2(46, 22), new Vector2(x - 80 + i * 54, panelY - 50));
            powerPips[i].type = Image.Type.Sliced;
        }
        BuildHint();
    }

    private void BuildHint() =>
        hint = ClawMachineUI.Label("Hint", design, "", 34, new Vector2(860, 60), new Vector2(0, HintY), TextSurfaceStyle.Base(TextSurface.Purple));

    private void BuildPanelHud()
    {
        panelHud = true;
        var size = ClawMachineUI.SizeForWidth(cfg.controlPanel, PanelWidth);
        var center = new Vector2(PanelCenterX, bottomTopY - bottomHeight * PanelInBottom);
        var panel = ClawMachineUI.Picture("Panel", machine, cfg.controlPanel, size, center, false).rectTransform;
        Vector2 At(Vector2 uv) => new Vector2((uv.x - 0.5f) * size.x, (0.5f - uv.y) * size.y);

        var display = At(PanelDisplay);
        var iconPos = display + new Vector2(-34f, 0f);
        if (cfg.tokenIcon != null)
            ClawMachineUI.Picture("TokenIcon", panel, cfg.tokenIcon, new Vector2(38, 38), iconPos);
        tokensText = ClawMachineUI.Label("Tokens", panel, "", 40, new Vector2(80, 46),
            display + new Vector2(cfg.tokenIcon != null ? 18f : 0f, 0f), TextSurfaceStyle.Base(TextSurface.Red));

        float lamp = size.x * PanelLampSize * 1.6f;
        for (int i = 0; i < powerPips.Length; i++)
        {
            bool real = i < PanelLampX.Length;
            powerPips[i] = ClawMachineUI.Picture("Lamp" + i, panel, GlowSprite(), new Vector2(lamp, lamp),
                real ? At(new Vector2(PanelLampX[i], PanelLampY)) : Vector2.zero, false);
        }

        float rowY = center.y + display.y;
        buyButton = ClawMachineUI.TextButton("Buy", machine, "+", new Vector2(60, 60), new Vector2(center.x + size.x * 0.5f + 34f, rowY), UiButtons.Kind.Green, AskBuy);
        powerText = ClawMachineUI.Label("Power", machine, "", 24, new Vector2(340, 40), new Vector2(center.x, center.y - size.y * 0.5f - 20f), TextSurfaceStyle.Base(TextSurface.Red));

        // Uçan jeton: sayaçtan yuvaya (tek görsel, yeniden kullanılır).
        tokenFrom = center + iconPos;
        tokenSlot = center + At(PanelSlot);
        if (cfg.tokenIcon != null)
        {
            tokenFly = ClawMachineUI.Picture("TokenFly", machine, cfg.tokenIcon, new Vector2(38, 38), tokenFrom);
            tokenFly.gameObject.SetActive(false);
        }
        BuildHint();
    }

    private void StartTokenInsert()
    {
        if (tokenFly == null) return;
        tokenAnim = 0f;
        tokenFly.gameObject.SetActive(true);
        tokenFly.transform.SetAsLastSibling();
        TickTokenInsert(0f);
    }

    /// Sayaçtan yaylanarak yuvanın üstüne → yuvaya girerken kenarına döner (dikey yuva) → içeri kayar ve kaybolur.
    private void TickTokenInsert(float dt)
    {
        tokenAnim += dt;
        float t = tokenAnim / TokenInsertTime;
        var rt = tokenFly.rectTransform;
        var above = tokenSlot + new Vector2(0f, 46f);
        float p1 = Mathf.Clamp01(t / 0.5f), p2 = Mathf.Clamp01((t - 0.5f) / 0.25f), p3 = Mathf.Clamp01((t - 0.75f) / 0.25f);
        float e1 = EaseInOut(p1);
        Vector2 pos = Vector2.Lerp(tokenFrom, above, e1) + new Vector2(0f, Mathf.Sin(p1 * Mathf.PI) * 60f);
        pos.y -= Mathf.Lerp(0f, 36f, p2) + Mathf.Lerp(0f, 34f, p3);
        rt.anchoredPosition = pos;
        float scale = Mathf.Lerp(1f, 1.45f, e1);
        rt.localScale = new Vector3(scale * Mathf.Lerp(1f, 0.18f, EaseInOut(p2)), scale, 1f);
        tokenFly.canvasRenderer.SetAlpha(1f - p3);
        if (p2 > 0f && tokenAnim - dt < TokenInsertTime * 0.5f) EventSfx.Play(l => l.uiTap, 1f, 1.4f);
        if (t >= 1f) StopTokenInsert();
    }

    private void StopTokenInsert()
    {
        tokenAnim = -1f;
        if (tokenFly != null) tokenFly.gameObject.SetActive(false);
    }

    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        if (glow == null)
        {
            const int n = 48;
            glow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ClawLampGlow" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }
            glow.SetPixels(px); glow.Apply(false, true);
        }
        return glowSprite = Sprite.Create(glow, new Rect(0, 0, glow.width, glow.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void BuildReward()
    {
        rewardRoot = ClawMachineUI.Rect("Reward", machine, new Vector2(220, 220), Vector2.zero);
        rewardIcon = ClawMachineUI.Picture("Icon", rewardRoot, null, new Vector2(130, 130), new Vector2(0, 10));
        rewardText = ClawMachineUI.Label("Text", rewardRoot, "", 40, new Vector2(320, 60), new Vector2(0, -80), TextSurfaceStyle.Base(TextSurface.Purple));
        rewardRoot.gameObject.SetActive(false);
    }

    // Giriş: tam ekran overlay (kullanıcı 2026-11-28: popup değil; karartma %96). İçerik tasarım ölçeğinde
    // (Layout introContent'i de ölçekler): peluş, başlık, açıklama, BAŞLA, kapat.
    private const float IntroDim = 0.96f;
    private static readonly int[] IntroSidePlush = { 1, 0, 3, 5 };   // sol iç, sol dış, sağ iç, sağ dış
    private RectTransform introContent;
    private TMP_Text introBody, introStart;

    private void BuildIntro()
    {
        intro = ClawMachineUI.Stretch("Intro", root);
        var dim = intro.gameObject.AddComponent<Image>();
        dim.color = new Color(0.05f, 0.02f, 0.07f, IntroDim); dim.raycastTarget = true;
        introContent = ClawMachineUI.Rect("Content", intro, new Vector2(1000, 1800), Vector2.zero);

        // Koleksiyon önizlemesi: ortada büyük kahraman, iki yanında ikişer peluş (yan yana bindirilmiş, arkada).
        for (int i = 0; i < IntroSidePlush.Length; i++)
        {
            float side = i < 2 ? -1f : 1f;
            bool outer = i % 2 == 1;
            ClawMachineUI.Picture("Plush" + i, introContent, cfg.PlushSprite(IntroSidePlush[i]),
                Vector2.one * (outer ? 125f : 160f), new Vector2(side * (outer ? 330f : 205f), outer ? 300f : 325f));
        }
        ClawMachineUI.Picture("Hero", introContent, cfg.PlushSprite(4), new Vector2(330, 330), new Vector2(0, 360));
        ClawMachineUI.Label("IntroTitle", introContent, ClawMachineUI.L("intro_title", "PENÇE MAKİNESİ"), 76,
            new Vector2(880, 100), new Vector2(0, 120), TextSurfaceStyle.Base(TextSurface.Purple));
        introBody = ClawMachineUI.BodyLabel("IntroBody", introContent, "", 38, new Vector2(820, 330), new Vector2(0, -110),
            TextSurfaceStyle.Base(TextSurface.Purple));
        var start = ClawMachineUI.TextButton("Start", introContent, "", new Vector2(500, 130), new Vector2(0, -380), UiButtons.Kind.Green, Join);
        introStart = start.GetComponentInChildren<TMP_Text>();
        var close = ClawMachineUI.Picture("Close", introContent, cfg.closeIcon, new Vector2(96, 96), new Vector2(440, 840));
        close.raycastTarget = true;
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close; closeButton.onClick.AddListener(Close);
        intro.gameObject.SetActive(false);
    }

    // ── Aç / kapat ──────────────────────────────────────────────────

    public void Open(RectTransform origin)
    {
        if (cfg == null) return;
        ClawMachineState.Sync(cfg, DateTime.UtcNow);
        if (!ClawMachineState.IsLive(cfg, DateTime.UtcNow)) return;
        iconOrigin = origin; closing = false; phase = Phase.Ready; needsCollectionCeremony = false;
        gameObject.SetActive(true); transform.SetAsLastSibling();
        rewardRoot.gameObject.SetActive(false);
        ResetClawHome(); SetClawSprite(false);
        LayoutItems(); RefreshTexts(); Layout();
        EventSfx.Play(l => l.popupOpen);
        iris.PlayOpen(origin);
    }

    public void Close()
    {
        if (closing || iris.IsRunning || phase != Phase.Ready || plushAnim >= 0f) return;   // peluş tabelaya uçarken kapanmaz (tören kaybolmasın)
        closing = true;
        iris.PlayClose(iconOrigin, () => gameObject.SetActive(false));
    }

    private void OnEnable()
    {
        ClawMachineState.OnChanged += RefreshTexts; GameLocalization.OnLanguageChanged += RefreshTexts;
        PlayerWallet.OnCoinsChanged += RefreshCoins;
    }
    private void OnDisable()
    {
        ClawMachineState.OnChanged -= RefreshTexts; GameLocalization.OnLanguageChanged -= RefreshTexts;
        PlayerWallet.OnCoinsChanged -= RefreshCoins;
        StopTokenInsert();
        StopPlushFly();
    }
    private void OnApplicationFocus(bool value) => focused = value;

    private void Join()
    {
        if (ClawMachineState.TryJoin(cfg, DateTime.UtcNow))
        {
            EventSfx.Play(l => l.stepPop);
            RefreshTexts();
        }
    }

    private void AskBuy()
    {
        if (phase != Phase.Ready || closing) return;
        var now = DateTime.UtcNow;
        if (ClawMachineState.Tokens >= cfg.tokenCap) { Flash(string.Format(ClawMachineUI.L("cap", "Jeton dolu ({0})"), cfg.tokenCap)); return; }
        int left = ClawMachineState.BuysLeftToday(cfg, now);
        if (left <= 0) { Flash(ClawMachineUI.L("buy_limit", "Bugünlük alım hakkın bitti.")); return; }
        RuntimeChoicePopup.Show(ClawMachineUI.L("buy_title", "Jeton Al"),
            string.Format(ClawMachineUI.L("buy_body", "1 jeton = {0} altın\nBugün {1} alım hakkın kaldı."), cfg.coinTokenPrice, left),
            new RuntimeChoicePopup.Choice(string.Format(ClawMachineUI.L("buy_button", "{0} ALTIN"), cfg.coinTokenPrice), () =>
            {
                if (ClawMachineState.TryBuyToken(cfg, DateTime.UtcNow)) EventSfx.Play(l => l.rankBadge);
                else Flash(ClawMachineUI.L("buy_failed", "Yeterli altın yok."));
            }, true),
            new RuntimeChoicePopup.Choice(ClawMachineUI.L("cancel", "VAZGEÇ"), () => { }));
    }

    // ── Girdi (joystick) ───────────────────────────────────────────
    // Gerçek makine kuralı: pençe sol-önden başlar, yalnız sağa ve geriye gider (geri dönüş yok); İNDİR durdurulamaz.
    // Hareket bırakılınca pençe hızıyla orantılı sallanır; sallanırken indirmek isabeti kaydırır (beceri).
    // Süre sınırı (sezonun untimedAttempts denemesinden sonra) ilk hareketle başlar, bitince pençe kendiliğinden iner.

    private bool CanControl => phase == Phase.Ready && !closing && !iris.IsRunning && !intro.gameObject.activeSelf;

    private void ResetClawHome()
    {
        clawU = clawW = 0f;
        swayAmp = swayT = swayOffset = 0f;
        moveStarted = moveTimed = false;
        shownSecond = -1;
        SetClawDepth(0f);
        PlaceClawAtRest();
    }

    private void UpdateControl(float dt)
    {
        if (!CanControl || ClawMachineState.Tokens <= 0) return;
        var v = joystick != null ? joystick.Value : Vector2.zero;
        float du = Mathf.Max(0f, v.x) * cfg.moveSpeed * dt;
        float dw = Mathf.Max(0f, v.y) * cfg.depthSpeed * dt;
        bool moving = (du > 0f && clawU < 1f) || (dw > 0f && clawW < 1f);
        if (moving)
        {
            if (!moveStarted)
            {
                moveStarted = true;
                moveTimed = ClawMachineState.NextIsTimed(cfg);
                moveLeft = cfg.moveSeconds;
            }
            clawU = Mathf.Min(1f, clawU + du);
            clawW = Mathf.Min(1f, clawW + dw);
            swayAmp = Mathf.Max(swayAmp, cfg.swayAmplitude * Mathf.Clamp01(v.magnitude));
            swayT = 0f;
        }
        else if (swayAmp > 0f)
        {
            swayT += dt;
            if (swayT > cfg.swaySettle * 2f) swayAmp = 0f;
        }
        // Sönümlü salınım; hareket sürerken 0 (sin 0), bırakınca başlar.
        swayOffset = swayAmp <= 0f ? 0f : swayAmp * Mathf.Exp(-swayT * 3f / Mathf.Max(0.05f, cfg.swaySettle)) * Mathf.Sin(swayT * 9f);
        SetClawDepth(clawW);
        PlaceClawAtRest();

        if (moveTimed)
        {
            moveLeft -= dt;
            int sec = Mathf.Max(0, Mathf.CeilToInt(moveLeft));
            if (sec != shownSecond)
            {
                shownSecond = sec;
                hint.text = string.Format(ClawMachineUI.L("time_left", "Süre: {0}"), sec);
            }
            if (moveLeft <= 0f) Drop();
        }
    }

    private void Drop()
    {
        if (!CanControl) return;
        if (ClawMachineState.Tokens <= 0) { AskBuy(); return; }
        float u = Mathf.Clamp01(clawU + swayOffset);
        if (!ClawMachineState.TryGrab(cfg, u, clawW, DateTime.UtcNow, out attempt)) { RefreshTexts(); return; }
        moveTimed = false; shownSecond = -1; swayAmp = 0f;
        claw.localRotation = Quaternion.identity;   // iniş düz (isabet sallanmanın o anki konumundan alındı)
        pendingReveal = attempt.IsPlush && attempt.NewPlush ? attempt.PlushId : -1;
        RefreshCollection();
        StartTokenInsert();
        EventSfx.Play(l => l.arrowWhoosh);
        // Pençe olduğu yere iner (ışınlama yok): sonuç zaten indiği noktanın isabetinden.
        Begin(Phase.Descend, 0.75f);
    }

    // ── Sunum (tek Update) ─────────────────────────────────────────

    private void Begin(Phase next, float seconds) { phase = next; phaseTime = 0f; phaseLength = Mathf.Max(0.01f, seconds); releaseLanded = false; }

    private void Update()
    {
        if (!focused) return;
        if (Screen.width != width || Screen.height != height) Layout();
        if (tokenAnim >= 0f) TickTokenInsert(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        if (plushAnim >= 0f) TickPlushFly(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        if (phase == Phase.Ready)
        {
            UpdateControl(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            if (Time.unscaledTime >= nextSecond) { nextSecond = Time.unscaledTime + 1f; RefreshTimer(); }
            return;
        }
        phaseTime += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float k = Mathf.Clamp01(phaseTime / phaseLength);
        var view = items[attempt.Slot];
        float grabTop = GrabClawTop(attempt.Slot);
        switch (phase)
        {
            case Phase.Descend:
                SetClawTop(Mathf.Lerp(clawRestTop, grabTop, EaseInOut(k)));
                if (k >= 1f) { SetClawSprite(true); EventSfx.Play(l => l.hammerTap); Begin(Phase.Close, 0.25f); }
                break;
            case Phase.Close:
                if (k >= 1f)
                {
                    if (attempt.Outcome == ClawMachineRules.Outcome.Catch) EventSfx.Play(l => l.stepPop);
                    slipLanded = false;
                    Begin(Phase.Lift, 0.85f);
                }
                break;
            case Phase.Lift:
            {
                float top = Mathf.Lerp(grabTop, clawRestTop, EaseInOut(k));
                SetClawTop(top);
                bool carrying = attempt.Outcome == ClawMachineRules.Outcome.Catch
                    || (attempt.Outcome == ClawMachineRules.Outcome.Slip && k < 0.45f);
                if (carrying) AttachToClaw(view);
                else if (attempt.Outcome == ClawMachineRules.Outcome.Slip)
                {
                    // Kayma: oyuncak pençeden düşer, yerine sekerek oturur.
                    float fall = Mathf.Clamp01((k - 0.45f) / 0.3f);
                    var at = view.root.anchoredPosition;
                    var home = SlotPos(attempt.Slot);
                    view.root.anchoredPosition = new Vector2(Mathf.Lerp(at.x, home.x, fall), Mathf.Lerp(at.y, home.y, fall * fall));
                    if (fall >= 1f && !slipLanded) { slipLanded = true; view.root.localRotation = RestRotation(attempt.Slot); EventSfx.Play(l => l.eliminated); }
                }
                if (k >= 1f)
                {
                    if (attempt.Outcome == ClawMachineRules.Outcome.Catch)
                    {
                        carryFrom = claw.anchoredPosition; carryFromDepth = clawDepth;
                        Begin(Phase.Carry, 0.7f);
                    }
                    else
                    {
                        SetClawSprite(false); ShowResultText();
                        BeginHome();
                    }
                }
                break;
            }
            case Phase.Carry:
                // Bacaya: yatayda sağa, derinlikte öne (baca ön sırada).
                SetClawDepth(Mathf.Lerp(carryFromDepth, 0f, EaseInOut(k)));
                claw.anchoredPosition = new Vector2(Mathf.Lerp(carryFrom.x, chuteX, EaseInOut(k)), claw.anchoredPosition.y);
                SyncCable(); AttachToClaw(view);
                if (k >= 1f) { SetClawSprite(false); releaseFromY = ClawTipY() - view.height * 0.45f; Begin(Phase.Release, 0.8f); }
                break;
            case Phase.Release:
            {
                // Bacaya düşer (ön cam/çubuğun arkasından), tabanda seker, sonra tabandan hazneye geçer (solar).
                float y;
                if (k < ReleaseFall)
                {
                    float f = k / ReleaseFall;
                    y = Mathf.Lerp(releaseFromY, chuteLandY, f * f);
                }
                else if (k < ReleaseBounce)
                {
                    if (!releaseLanded) { releaseLanded = true; EventSfx.Play(l => l.plankPlace); }
                    y = chuteLandY + Mathf.Sin((k - ReleaseFall) / (ReleaseBounce - ReleaseFall) * Mathf.PI) * 14f;
                }
                else
                {
                    float sink = (k - ReleaseBounce) / (1f - ReleaseBounce);
                    y = chuteLandY - sink * 34f;
                    SetItemAlpha(view, 1f - sink);
                }
                view.root.anchoredPosition = new Vector2(chuteX, y);
                if (k >= 1f) { ShowReward(); Begin(Phase.Reward, 1.3f); }
                break;
            }
            case Phase.Reward:
                // Hazneye düşer: pencerenin üstünden tepsiye.
                rewardRoot.anchoredPosition = new Vector2(chuteX, hatchY + 60f * (1f - EaseInOut(Mathf.Clamp01(k / 0.25f))));
                rewardRoot.localScale = Vector3.one * (k < 0.2f ? Mathf.Lerp(0.4f, 1.15f, k / 0.2f) : Mathf.Lerp(1.15f, 1f, (k - 0.2f) / 0.8f));
                if (k >= 1f)
                {
                    rewardRoot.gameObject.SetActive(false);
                    if (pendingReveal >= 0) StartPlushFly(pendingReveal);
                    ApplyItem(attempt.Slot, attempt.NewItem);
                    SetItemAlpha(view, 1f);
                    view.root.anchoredPosition = new Vector2(SlotPos(attempt.Slot).x, railY);
                    Begin(Phase.Refill, 0.45f);
                }
                break;
            case Phase.Refill:
            {
                var home = SlotPos(attempt.Slot);
                view.root.anchoredPosition = new Vector2(home.x, Mathf.Lerp(railY - 40f, home.y, k * k));
                if (k >= 1f) BeginHome();
                break;
            }
            case Phase.Home:
            {
                // Pençe başlangıca (sol-ön) döner; sıradaki deneme oradan başlar.
                float e = EaseInOut(k);
                SetClawDepth(Mathf.Lerp(homeFromDepth, 0f, e));
                claw.anchoredPosition = new Vector2(Mathf.Lerp(homeFromX, ClawX(0f, 0f), e), clawRestTop);
                SyncCable();
                if (k >= 1f) { ResetClawHome(); Finish(); }
                break;
            }
        }
    }

    private void BeginHome()
    {
        homeFromX = claw.anchoredPosition.x;
        homeFromDepth = clawDepth;
        SetClawTop(clawRestTop);
        Begin(Phase.Home, 0.55f);
    }

    private void Finish()
    {
        phase = Phase.Ready;
        LayoutItems();
        RefreshTexts();
        TryCollectionCeremony();
    }

    // Son peluş tabelaya oturduktan sonra (uçuş sürerken bekler).
    private void TryCollectionCeremony()
    {
        if (!needsCollectionCeremony || phase != Phase.Ready || plushAnim >= 0f) return;
        {
            needsCollectionCeremony = false;
            EventSfx.Play(l => l.victoryFanfare);
            ComebackGiftOverlay.Show(cfg.collectionRewards, () => { },
                ClawMachineUI.L("complete_title", "Koleksiyon Tamam!"),
                ClawMachineUI.L("complete_sub", "6 peluşun hepsini topladın!"), true);
        }
    }

    private void ShowReward()
    {
        rewardRoot.gameObject.SetActive(true);
        rewardRoot.anchoredPosition = new Vector2(chuteX, hatchY + 60f);
        // Yazı bacanın üstünde (hazne penceresi dar; ikon tepsiden taşar, yazı okunur kalsın).
        rewardText.rectTransform.anchoredPosition = new Vector2(0f, chuteTopY - hatchY + 46f);
        if (attempt.IsPlush)
        {
            rewardIcon.sprite = cfg.PlushSprite(attempt.PlushId);
            rewardText.text = attempt.NewPlush ? ClawMachineUI.L("new_plush", "YENİ PELUŞ!")
                : string.Format(ClawMachineUI.L("duplicate", "+{0} JETON"), attempt.BonusTokens);
            EventSfx.Play(l => attempt.NewPlush ? l.podiumArrive : l.rankBadge);
            needsCollectionCeremony = attempt.CollectionCompleted;
        }
        else
        {
            var prize = cfg.Prize(attempt.PrizeIndex);
            rewardIcon.sprite = prize?.reward?.ResolveIcon();
            rewardText.text = prize == null ? "" : prize.reward.type == DailySlotRewardType.Coins
                ? "+" + prize.reward.amount : prize.reward.AmountLabel;
            EventSfx.Play(l => l.rankBadge);
        }
        rewardIcon.enabled = rewardIcon.sprite != null;
    }

    private void ShowResultText()
    {
        Flash(attempt.Outcome == ClawMachineRules.Outcome.Slip ? ClawMachineUI.L("slip", "Kaydı! Pençe Gücü doluyor.")
            : ClawMachineUI.L("empty", "Kaçtı! Pençe Gücü doluyor."));
    }

    private float flashUntil;
    private void Flash(string text) { hint.text = text; flashUntil = Time.unscaledTime + 2f; }

    // ── Yerleşim ────────────────────────────────────────────────────

    // 2.5D eşleme: (u, w) → makine koordinatı. Arka taban yukarıda, ölçek küçük, yatay hafif daralır.
    private static float DepthScale(float w) => 1f - DepthShrink * Mathf.Clamp01(w);
    private float ClawX(float u, float w) => playX0 + playW * (0.5f + (u - 0.5f) * (1f - DepthNarrow * w));
    private float FloorAt(float w) => floorY + DepthLift * Mathf.Clamp01(w);
    private Vector2 SlotPos(int slot)
    {
        ClawMachineRules.Slot(slot, items.Length, out float u, out float w);
        return new Vector2(ClawX(u, w), FloorAt(w));
    }
    private float SlotDepth(int slot) { ClawMachineRules.Slot(slot, items.Length, out _, out float w); return w; }

    private float ClawHeight() => claw.sizeDelta.y * claw.localScale.y;
    private float ClawTipY() => claw.anchoredPosition.y - ClawHeight() * 0.92f;
    private float GrabClawTop(int slot)
    {
        var v = items[slot];
        float scale = v.root.localScale.y;
        float itemTop = SlotPos(slot).y + v.height * scale;
        // Pençe uçları oyuncağın üst üçte birini kavrar; tamamen boşta pençenin derinliğindeki tabana iner.
        float tips = attempt.Outcome == ClawMachineRules.Outcome.Empty && attempt.Accuracy <= 0f
            ? FloorAt(clawDepth) + 30f * DepthScale(clawDepth) : itemTop - v.height * scale * 0.35f;
        return tips + ClawHeight() * 0.92f;
    }

    /// Yuva başına sabit küçük eğim (yığılmış görünsün, dizilmiş değil). Deterministik: açılışlar arasında değişmez.
    private const float MaxRestTilt = 7f;
    private static float Tilt(int seed) => ((seed * 37 + 11) % 15 - 7) / 7f * MaxRestTilt;
    private static Quaternion RestRotation(int slot) => Quaternion.Euler(0f, 0f, Tilt(slot));

    private void PlaceClawAtRest()
    {
        claw.anchoredPosition = new Vector2(ClawX(Mathf.Clamp01(clawU + swayOffset), clawW), clawRestTop);
        // Sallanma görünür: genlikle orantılı küçük açı.
        float tilt = cfg.swayAmplitude > 0f ? -swayOffset / cfg.swayAmplitude * 6f : 0f;
        claw.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SyncCable();
    }
    private void SetClawDepth(float w)
    {
        clawDepth = Mathf.Clamp01(w);
        float s = DepthScale(clawDepth);
        claw.localScale = new Vector3(s, s, 1f);
    }
    private void SetClawTop(float top) { claw.anchoredPosition = new Vector2(claw.anchoredPosition.x, top); SyncCable(); }
    private void SyncCable()
    {
        cable.anchoredPosition = new Vector2(claw.anchoredPosition.x, railY);
        cable.sizeDelta = new Vector2(6f * DepthScale(clawDepth), Mathf.Max(0f, railY - claw.anchoredPosition.y + 6f));
        if (clawShadow != null)
        {
            float s = DepthScale(clawDepth);
            var srt = clawShadow.rectTransform;
            srt.anchoredPosition = new Vector2(claw.anchoredPosition.x, FloorAt(clawDepth) + 4f);
            srt.localScale = new Vector3(s, s, 1f);
        }
    }

    private void SetClawSprite(bool closed)
    {
        var sprite = closed ? cfg.clawClosed : cfg.clawOpen;
        clawImage.sprite = sprite;
        // Açık ve kapalı kareler aynı ölçekle çizilmiş: genişlik açık sprite'a göre ölçeklenir.
        float scale = cfg.clawOpen != null && cfg.clawOpen.rect.width > 0 ? ClawWidth / cfg.clawOpen.rect.width : 1f;
        if (sprite != null) claw.sizeDelta = sprite.rect.size * scale;
    }

    private void AttachToClaw(ItemView view)
    {
        float s = DepthScale(clawDepth);
        view.root.localScale = new Vector3(s, s, 1f);
        view.root.anchoredPosition = new Vector2(claw.anchoredPosition.x, ClawTipY() - view.height * s * 0.45f);
        view.root.localRotation = attempt.Outcome == ClawMachineRules.Outcome.Slip ? Quaternion.Euler(0, 0, -8f) : Quaternion.identity;
    }

    private void LayoutItems()
    {
        var state = ClawMachineState.Items;
        for (int i = 0; i < items.Length; i++)
        {
            int item = i < state.Count ? state[i] : ClawMachineState.CapsuleBase;
            ApplyItem(i, item);
            items[i].root.anchoredPosition = SlotPos(i);
            items[i].root.localRotation = RestRotation(i);
            SetItemAlpha(items[i], 1f);
        }
    }

    private void ApplyItem(int slot, int item)
    {
        var v = items[slot];
        bool plush = item < ClawMachineState.CapsuleBase;
        v.back.enabled = v.bottom.enabled = v.front.enabled = !plush;
        v.icon.enabled = false;   // kapsül içeriği yakalanınca açılır (sürpriz kapsül)
        v.plush.enabled = plush;
        float scale = DepthScale(SlotDepth(slot));
        v.root.localScale = new Vector3(scale, scale, 1f);
        if (plush)
        {
            v.plush.sprite = cfg.PlushSprite(item);
            v.plush.rectTransform.sizeDelta = ClawMachineUI.SizeForWidth(v.plush.sprite, interiorW * PlushWidth);
            v.height = v.plush.rectTransform.sizeDelta.y;
        }
        else
        {
            int color = item - ClawMachineState.CapsuleBase;
            var colors = cfg.capsuleColors;
            v.bottom.color = colors != null && colors.Length > 0 ? colors[Mathf.Abs(color) % colors.Length] : Color.white;
            v.height = v.back.rectTransform.sizeDelta.y;
        }
    }

    // Kare başına çağrılır (bırakma animasyonu): dizi ayırmadan.
    private static void SetItemAlpha(ItemView v, float a)
    {
        SetAlpha(v.back, a); SetAlpha(v.icon, a); SetAlpha(v.bottom, a); SetAlpha(v.front, a); SetAlpha(v.plush, a);
    }
    private static void SetAlpha(Image img, float a) { if (img != null) img.canvasRenderer.SetAlpha(a); }

    private void RefreshTexts()
    {
        if (cfg == null || title == null) return;
        bool joined = ClawMachineState.HasJoined;
        intro.gameObject.SetActive(!joined);
        if (!joined)
        {
            if (introBody != null) introBody.text = string.Format(ClawMachineUI.L("intro_body",
                "Seviye kazan, jeton kazan (zor seviye: {0}).\nPençeyi kaydır, bırak, oyuncak yakala!\n6 peluşu topla, büyük sandığı aç.\nEtkinlik bitince kalan jetonlar silinir."), cfg.tokensPerHardWin);
            if (introStart != null) introStart.text = string.Format(ClawMachineUI.L("start", "BAŞLA (+{0} JETON)"), cfg.joinTokens);
        }
        title.text = ClawMachineUI.L("title", "PENÇE MAKİNESİ");
        tokensText.text = cfg.tokenIcon != null ? ClawMachineState.Tokens.ToString()
            : string.Format(ClawMachineUI.L("tokens", "JETON {0}"), ClawMachineState.Tokens);
        buyButton.gameObject.SetActive(joined && ClawMachineState.Tokens < cfg.tokenCap);
        // Güçlü pençe: kenar da tutar, nişan yine şart (garanti değil). Öğretici denemesi de güçlü sayılır.
        bool guaranteed = ClawMachineState.NextIsStrong(cfg);
        powerText.text = guaranteed ? ClawMachineUI.L("strong", "PENÇE GÜÇLÜ! İyi nişan al.")
            : string.Format(ClawMachineUI.L("power", "PENÇE GÜCÜ  •  BÜYÜK ÖDÜLE {0}"), ClawMachineState.AttemptsToBig(cfg));
        int need = Mathf.Clamp(cfg.missesForPower, 1, panelHud ? PanelLampX.Length : powerPips.Length);
        for (int i = 0; i < powerPips.Length; i++)
        {
            powerPips[i].gameObject.SetActive(i < need);
            bool full = guaranteed || i < ClawMachineState.Misses;
            powerPips[i].color = panelHud ? (full ? LampOn : Color.clear)
                : full ? new Color(1f, 0.82f, 0.2f) : new Color(1f, 1f, 1f, 0.25f);
        }
        RefreshCollection();
        RefreshCoins();
        if (dropLabel != null) dropLabel.text = ClawMachineUI.L("drop", "İNDİR");
        if (Time.unscaledTime >= flashUntil && !moveTimed)
            hint.text = ClawMachineState.Tokens <= 0 ? ClawMachineUI.L("no_tokens", "Jeton yok! Seviye kazan ya da altınla al.")
                : ClawMachineState.IsTutorialAttempt ? ClawMachineUI.L("tutorial", "Pençeyi oyuncağın tam üstüne getir, ortasına dokun!")
                : ClawMachineUI.L("hint", "Sağa ve geriye götür, ortasına dokun!");
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        var now = DateTime.UtcNow;
        if (!ClawMachineState.IsLive(cfg, now)) { if (phase == Phase.Ready && !closing) Close(); return; }
        timer.text = TimeFormat.Countdown(ClawMachineState.Window(cfg, now).End - now);
        if (Time.unscaledTime >= flashUntil && flashUntil > 0f) { flashUntil = 0f; RefreshTexts(); }
    }

    private void Layout()
    {
        width = Screen.width; height = Screen.height;
        var safe = Screen.safeArea;
        Canvas.ForceUpdateCanvases();
        Rect r = root.rect;
        if (r.width <= 0 || r.height <= 0) return;
        float top = height > 0 ? (height - safe.yMax) * r.height / height : 0;
        float bottom = height > 0 ? safe.yMin * r.height / height : 0;
        float scale = Mathf.Min(r.width / 1000f, Mathf.Max(1, r.height - top - bottom) / 1800f);
        design.localScale = Vector3.one * scale;
        design.anchoredPosition = new Vector2(0, (bottom - top) * 0.5f);
        if (introContent != null)
        {
            introContent.localScale = design.localScale;
            introContent.anchoredPosition = design.anchoredPosition;
        }
    }

    private static float EaseInOut(float t) => t * t * (3f - 2f * t);

    private static Sprite GradientSprite(Color top, Color bottom)
    {
        if (gradient == null)
        {
            gradient = new Texture2D(2, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ClawMachineGradient" };
            var px = new Color[2 * 64];
            for (int y = 0; y < 64; y++) { var c = Color.Lerp(bottom, top, y / 63f); px[y * 2] = px[y * 2 + 1] = c; }
            gradient.SetPixels(px); gradient.Apply(false, true);
        }
        return Sprite.Create(gradient, new Rect(0, 0, 2, 64), new Vector2(0.5f, 0.5f), 100f);
    }
}
