using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pençe Makinesi ekranı (plan §4, §9). Kod ile kurulur, bir kez; oyuncak görünümleri yeniden kullanılır.
/// 2.5D (2026-11-28): basılı tutulan iki yön tuşu pençeyi yalnız sağa (u) ve geriye (w, derinlik) götürür, ayrı İNDİR
/// butonu iner. Pençeyi taşıyan kiriş de derinlikle geriye gider (ekranda iner, daralır).
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
    // Hazne Machine_Bottom'da 2026-11-28 sağa kaydırıldı (merkez %77.7 → 0.277): baca tam üstünde, oyun alanı geniş.
    private const float ChuteX = 0.277f, ChuteInBottom = 0.40f;
    private const float GlassCut = 0.05f, BackdropDim = 0f;   // akşam lunaparkı zaten koyu (gündüz görselinde 0.08 idi)
    // Baca (ClawChute_Back/Front): genişlik, cam tabanına gömülme, kutu tabanının yüksekliği (alttan oran);
    // hazne penceresi Machine_Bottom'da üstten %62.
    private const float ReleaseFall = 0.5f, ReleaseBounce = 0.68f;
    private const float ChuteBoxWidth = 0.21f * MachineWidth, ChuteSink = 6f, ChuteFloorInBox = 0.2f, HatchInBottom = 0.62f;
    // Baca halının ön kenarında değil biraz içeride (kullanıcı 2026-11-28: "sarı çerçevenin üstünde duruyor"):
    // bu derinlikte durur, perspektifle küçülür; pençe oyuncağı bu derinliğe taşır.
    private const float ChuteDepth = 0.2f;
    private const float ClawWidth = 165f, CapsuleWidth = 0.15f, PlushWidth = 0.17f;   // 4 sıra/16 oyuncak: biraz küçük, tek tek okunsun
    // Derinlik (w 0..1): arka taban yukarıda, nesneler küçük, yatay hafif daralır (perspektif).
    // Varsayılan (iç görsel yoksa); iç görsel varsa BuildInterior görselin zemininden hesaplar.
    private float depthLift = 120f, depthShrink = 0.28f, depthNarrow = 0.1f;
    // ClawInterior.png: kaynak 1855×848 + üstte 760 px duvar uzatması (üst satır tekrarı) = 1855×1608. Halı derinliği
    // toplam yüksekliğin %23.2'si, arka kenar ön kenarın %70'i. Genişlik cam içinin 1.12 katı; cam çerçevesiyle kırpılır.
    private const float InteriorWidthScale = 1.12f, InteriorFloorDepth = 0.232f, InteriorBackScale = 0.70f;
    // Zemin rafının üst kenarı Machine_Bottom'da yüksekliğin %29.6'sı (halının ön kenarı bunun arkasına girer).
    private const float LedgeInBottom = 0.296f;
    private const float ControlsY = -815f, ArrowButtonSize = 140f, HintY = 684f;
    // Kiriş: geriye (w=1) giderken ekranda bu kadar iner (tavan perspektifi). İç görsel varsa ufuk çizgisinden hesaplanır.
    private float gantryDrop = 64f;
    // Pençenin yatay menzili kiriş boyunca: tutucuların iç kenarına kadar (genişliğin %47'si, yarı). Çubuk %40.6'da
    // biter; araba tutucuya dayanabilir → halının kenarlarına da oyuncak sığar (kullanıcı 2026-11-28: "sola da sıkıştır").
    private const float GantryRodHalf = 0.47f;

    private sealed class ItemView
    {
        public RectTransform root;
        public Image back, icon, bottom, front, plush;
        public TMP_Text amount;   // kapsüldeki ödülün miktarı (bir kez kurulur)
        public float height, iconBase;
        public int sibling;       // derinlik sırasındaki yeri (bacaya düşerken geçici değişir)
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
    private float railY, floorY, interiorX0, interiorW, chuteX, chuteY;
    private float playX0, playW, hatchY, chuteLandY, chuteTopY, bottomTopY, bottomHeight, chuteDrawX;
    private float phaseTime, phaseLength, nextSecond;
    // Pençe: mantıksal konum (u, w), sunumdaki derinlik (taşıma/dönüşte değişir), sallanma, süre.
    private float clawU, clawW, clawDepth, swayAmp, swayT, swayOffset, moveLeft;
    private bool moveStarted, moveTimed;
    private int shownSecond = -1;
    private float homeFromX, homeFromDepth, carryFromDepth;
    private ClawMachineHoldButton rightButton, backButton;
    private Image chuteBackImage, chuteFrontImage;
    private Image gantry;
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

        if (cfg.topHud != null) BuildTopHud();
        else
        {
            // Yedek (görsel yoksa): yarı saydam bant + ayrı altın hapı.
            var hudBand = ClawMachineUI.Picture("TopHudBand", design, BridgeRepairUI.Pill(), new Vector2(980, 150), new Vector2(0, 822), false);
            hudBand.type = Image.Type.Sliced;
            hudBand.color = new Color(0.06f, 0.01f, 0.12f, 0.55f);
            title = ClawMachineUI.Label("Title", design, "", 62, new Vector2(760, 90), new Vector2(0, 845), TextSurfaceStyle.Base(TextSurface.Purple));
            timer = ClawMachineUI.Label("Timer", design, "", 32, new Vector2(600, 50), new Vector2(0, 796), TextSurfaceStyle.Base(TextSurface.Purple));
            BuildCoinHud();
        }
        var close = ClawMachineUI.Picture("Close", design, cfg.closeIcon, new Vector2(90, 90), new Vector2(400, 930));   // kullanıcı ayarı
        close.raycastTarget = true;
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close; closeButton.onClick.AddListener(Close);

        BuildMachine();
        BuildMarquee();
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
        if (BackdropDim <= 0f) return;   // perde yoksa boş katman çizilmesin
        var dim = ClawMachineUI.Stretch("Dim", fitRoot).gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, BackdropDim); dim.raycastTarget = false;   // nötr: mavi gökyüzü morlaşmasın
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

    // Üst HUD görseli (ClawTopHud.png, oranlar kırpılmış görselden): sol koyu yuva = altın, orta plaka = başlık,
    // sağ alan = kalan süre; ✕ barın sağ üst köşesinde.
    private const float TopHudWidth = 900f, TopHudY = 810f;
    private static readonly Vector2 HudSlot = new Vector2(0.15f, 0.555f), HudPlate = new Vector2(0.50f, 0.555f), HudRight = new Vector2(0.87f, 0.555f);

    private void BuildTopHud()
    {
        var size = ClawMachineUI.SizeForWidth(cfg.topHud, TopHudWidth);
        var bar = ClawMachineUI.Picture("TopHud", design, cfg.topHud, size, new Vector2(0f, TopHudY), false).rectTransform;
        Vector2 At(Vector2 uv) => new Vector2((uv.x - 0.5f) * size.x, (0.5f - uv.y) * size.y);
        var red = TextSurfaceStyle.Base(TextSurface.Red);
        var slot = At(HudSlot);
        var lib = TileIconLibrary.Shared;
        ClawMachineUI.Picture("CoinIcon", bar, lib != null ? lib.GetRewardIcon(DailySlotRewardType.Coins) : null, new Vector2(58, 58), slot + new Vector2(-58f, 2f));
        coinsText = ClawMachineUI.Label("Coins", bar, "", 32, new Vector2(110, 50), slot + new Vector2(24f, 0f), red);
        title = ClawMachineUI.Label("Title", bar, "", 46, new Vector2(size.x * 0.44f, size.y * 0.5f), At(HudPlate), red);
        timer = ClawMachineUI.Label("Timer", bar, "", 32, new Vector2(size.x * 0.2f, 50), At(HudRight), red);
    }

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

    private static readonly Color InteriorWall = new Color(0.09f, 0.169f, 0.471f, 1f);   // ClawInterior üst satırı

    // Makinenin içi (kullanıcı 2026-11-28): perspektif zemin + arka duvar (WONDI neon). En arkada, cam iç alanıyla kırpılır
    // (RectMask2D); üstündeki boşluk duvar rengiyle uzar. Derinlik (zemin yüksekliği, arka ölçek) görselden gelir.
    private void BuildInterior(float machineTop, float topH, float bottomTop, float bottomH)
    {
        float ledgeY = bottomTop - bottomH * LedgeInBottom;
        float innerTop = machineTop - topH * 0.69f;            // Machine_Top'ta cam alanı y 397/575'ten başlar
        float frameInset = MachineWidth * 0.058f;               // cam altın yan çerçevesinin iç kenarı (60/1022)
        float clipBottom = ledgeY - 8f;
        var clip = ClawMachineUI.Rect("Interior", machine, new Vector2(MachineWidth - 2f * frameInset, innerTop - clipBottom),
            new Vector2(0f, (innerTop + clipBottom) * 0.5f));
        clip.gameObject.AddComponent<RectMask2D>();
        float centerY = (innerTop + clipBottom) * 0.5f;

        var size = ClawMachineUI.SizeForWidth(cfg.interior, MachineWidth * (1f - 2f * GlassInset) * InteriorWidthScale);
        float imageBottom = ledgeY - 6f;
        float imageTop = imageBottom + size.y;
        if (imageTop < innerTop + 20f)
        {
            var wall = ClawMachineUI.Picture("Wall", clip, null, new Vector2(size.x, innerTop + 20f - imageTop + 4f),
                new Vector2(0f, (imageTop - 4f + innerTop + 20f) * 0.5f - centerY), false);
            wall.enabled = true;
            wall.color = InteriorWall;
        }
        ClawMachineUI.Picture("Room", clip, cfg.interior, size, new Vector2(0f, imageBottom + size.y * 0.5f - centerY), false);

        depthLift = size.y * InteriorFloorDepth;
        depthShrink = depthNarrow = 1f - InteriorBackScale;
    }

    // ── Tabela lambaları (kullanıcı 2026-11-28): sırayla yanıp söner ─────────────────────────────
    // Lambalar Machine_Top görselinde çizili; her birinin üstünde sönük (koyu disk) + yanık (sıcak parıltı) katmanı.
    // Döngü ~3 sn: ışık dalgası soldan sağa iki kez koşar, sonra hepsi iki kez birlikte yanıp söner. Yalnız ekran açıkken,
    // ~20 Hz'de yalnız saydamlık değişir (mesh/layout yeniden kurulmaz).
    private static readonly Vector2[] BulbUV =   // kemer boyunca soldan sağa (Machine_Top 1022×575 üzerinde ölçüldü)
    {
        new Vector2(0.0988f, 0.471f), new Vector2(0.1398f, 0.317f), new Vector2(0.2418f, 0.241f), new Vector2(0.3534f, 0.179f),
        new Vector2(0.6443f, 0.180f), new Vector2(0.7586f, 0.242f), new Vector2(0.8580f, 0.319f), new Vector2(0.8992f, 0.473f),
    };
    private const float BulbSize = 0.03f, LightTick = 0.05f, ChaseStep = 0.11f;
    private static readonly Color BulbGlow = new Color(1f, 0.86f, 0.45f, 1f), BulbOff = new Color(0.32f, 0.12f, 0.04f, 1f);
    private Image[] bulbGlow, bulbShade;
    private float lightTime, lightAcc;

    private void BuildMarqueeLights(float topCenter, float topH)
    {
        float d = MachineWidth * BulbSize;
        bulbGlow = new Image[BulbUV.Length];
        bulbShade = new Image[BulbUV.Length];
        for (int i = 0; i < BulbUV.Length; i++)
        {
            var pos = new Vector2((BulbUV[i].x - 0.5f) * MachineWidth, topCenter + (0.5f - BulbUV[i].y) * topH);
            bulbShade[i] = ClawMachineUI.Picture("BulbOff" + i, machine, GlowSprite(), Vector2.one * d * 1.25f, pos, false);
            bulbShade[i].color = BulbOff;
            bulbGlow[i] = ClawMachineUI.Picture("BulbOn" + i, machine, GlowSprite(), Vector2.one * d * 2.8f, pos, false);
            bulbGlow[i].color = BulbGlow;
        }
        TickLights(LightTick);
    }

    private void TickLights(float dt)
    {
        if (bulbGlow == null) return;
        lightAcc += dt;
        if (lightAcc < LightTick) return;
        lightTime += lightAcc;
        lightAcc = 0f;
        int n = bulbGlow.Length;
        float pass = (n + 2) * ChaseStep, chase = pass * 2f, blink = 1f, cycle = chase + blink;
        float t = lightTime % cycle;
        for (int i = 0; i < n; i++)
        {
            float on;
            if (partyTime >= 0f) on = ((lightTime * 8f) + i) % 2f < 1f ? 1f : 0f;   // şölen: hızlı, sırayla
            else if (t < chase)
            {
                // Dalga başı i'ye geldiğinde tam yanar, arkasında ~2 lambalık sönen kuyruk.
                float head = (t % pass) / ChaseStep - 1f;
                float behind = head - i;
                on = behind < 0f ? 0f : Mathf.Clamp01(1f - behind / 2.2f);
            }
            else on = ((t - chase) / blink * 4f) % 2f < 1f ? 1f : 0f;   // iki kez birlikte
            bulbGlow[i].canvasRenderer.SetAlpha(0.15f + 0.85f * on);
            bulbShade[i].canvasRenderer.SetAlpha(0.55f * (1f - on));
        }
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
        if (cfg.interior != null) BuildInterior(machineTop, topH, bottomTop, bottomH);
        ClawMachineUI.Picture("Top", machine, cfg.machineTop, new Vector2(MachineWidth, topH), new Vector2(0, topCenter), false);
        BuildMarqueeLights(topCenter, topH);
        ClawMachineUI.Picture("Glass", machine, cfg.machineGlass, new Vector2(MachineWidth, glassH), new Vector2(0, glassCenter), false);
        ClawMachineUI.Picture("Bottom", machine, cfg.machineBottom, new Vector2(MachineWidth, bottomH), new Vector2(0, bottomTop - bottomH * 0.5f), false);

        railY = machineTop - topH * RailInTop;
        if (cfg.gantry != null)
        {
            gantry = ClawMachineUI.Picture("Gantry", machine, cfg.gantry,
                ClawMachineUI.SizeForWidth(cfg.gantry, MachineWidth * cfg.gantry.rect.width / Mathf.Max(1f, cfg.machineTop.rect.width)),
                new Vector2(0f, railY), false);
        }
        // İç görsel varsa halının ön kenarı zemin rafının hemen arkasında; yoksa eski düz taban.
        floorY = cfg.interior != null ? bottomTop - bottomH * LedgeInBottom - 6f : bottomTop - bottomH * FloorInBottom;
        if (cfg.interior != null)
        {
            // Ufuk: arka zemin ölçeği k ise zemin çizgileri floor + lift/(1-k)'da buluşur. Tavan (ray) ufkun üstündeyse
            // geriye giderken ufka doğru iner: (ray − ufuk)·(1−k).
            float horizon = floorY + depthLift / Mathf.Max(0.05f, 1f - InteriorBackScale);
            gantryDrop = Mathf.Max(0f, railY - horizon) * (1f - InteriorBackScale);
        }
        interiorX0 = -MachineWidth * 0.5f + MachineWidth * GlassInset;
        interiorW = MachineWidth * (1f - 2f * GlassInset);
        chuteX = MachineWidth * ChuteX;
        chuteY = bottomTop - bottomH * ChuteInBottom;
        hatchY = bottomTop - bottomH * HatchInBottom;
        bottomTopY = bottomTop; bottomHeight = bottomH;

        // Ödül bacası haznenin tam üstünde, cam tabanında; oyun alanı (pençe + oyuncaklar) bacanın solunda kalır.
        // Kurallar 0..1 oyun alanı koordinatında çalışır → bu daraltma dengeyi değiştirmez.
        // Oyun alanı kirişin çubuğu boyunca (halının tamamı; kullanıcı 2026-11-28: "hepsi bir yere sıkışmış").
        // En ön sıra bacanın soluna kadar (ClawMachineRules.FrontRowMaxU), arka sıralar bacanın arkasına da uzanır.
        float rodHalf = gantry != null ? gantry.rectTransform.sizeDelta.x * GantryRodHalf : -interiorX0;
        playX0 = Mathf.Max(interiorX0, -rodHalf + 6f);
        playW = Mathf.Min(interiorW, rodHalf - 6f - playX0);
        itemsLayer = ClawMachineUI.Rect("Items", machine, Vector2.zero, Vector2.zero);
        if (cfg.chuteBack != null)
        {
            var size = ClawMachineUI.SizeForWidth(cfg.chuteBack, ChuteBoxWidth * DepthScale(ChuteDepth));
            chuteDrawX = chuteX * (1f - depthNarrow * ChuteDepth);
            float baseY = FloorAt(ChuteDepth) - ChuteSink;
            var pos = new Vector2(chuteDrawX, baseY + size.y * 0.5f);
            chuteLandY = baseY + size.y * ChuteFloorInBox;
            chuteTopY = baseY + size.y;
            // Baca oyuncak katmanında, derinlik sırasında durur (arka sıralar arkasında, en ön sıra önünde).
            chuteBackImage = ClawMachineUI.Picture("ChuteBack", itemsLayer, cfg.chuteBack, size, pos, false);
            if (cfg.chuteFront != null) chuteFrontImage = ClawMachineUI.Picture("ChuteFront", itemsLayer, cfg.chuteFront, size, pos, false);
        }
        else
        {
            chuteLandY = chuteY;
            chuteTopY = floorY + 160f;
            chuteDrawX = chuteX;
        }
        int count = cfg.ItemCount;
        items = new ItemView[count];
        // Arka sıra önce çizilir (öndekiler önde kalır); baca kendi derinliğinde araya girer.
        bool chutePlaced = false;
        for (int row = ClawMachineRules.Rows - 1; row >= 0; row--)
        {
            if (!chutePlaced && ClawMachineRules.RowDepth(row) < ChuteDepth) { PlaceChuteInOrder(); chutePlaced = true; }
            for (int i = 0; i < count; i++)
                if (ClawMachineRules.Row(i) == row) { items[i] = BuildItem(i); items[i].sibling = items[i].root.GetSiblingIndex(); }
        }
        if (!chutePlaced) PlaceChuteInOrder();
        // Nişan gölgesi: pençenin tabana düşen noktası (derinlik ancak böyle okunur). Oyuncakların üstünde.
        clawShadow = ClawMachineUI.Picture("ClawShadow", machine, GlowSprite(), new Vector2(110f, 30f), Vector2.zero, false);
        clawShadow.color = new Color(0.05f, 0.02f, 0.1f, 0.45f);

        cable = ClawMachineUI.Rect("Cable", machine, new Vector2(6, 10), new Vector2(0, railY));
        cable.pivot = new Vector2(0.5f, 1f);
        var cableImage = cable.gameObject.AddComponent<Image>();
        cableImage.color = new Color(0.28f, 0.28f, 0.32f, 1f); cableImage.raycastTarget = false;
        clawImage = ClawMachineUI.Picture("Claw", machine, cfg.clawOpen, Vector2.one, Vector2.zero, false);
        claw = clawImage.rectTransform;
        claw.pivot = new Vector2(0.5f, 1f);
        SetClawSprite(false);

        BuildControls();
    }

    // Kontroller (kullanıcı 2026-11-28, gerçek makine): solda yarı saydam → (sağa) ve ↑ (geriye) tuşları, basılı
    // tuttukça gider; öne/sola tuş yok (tek yönlü kural). Sağda ayrı büyük İNDİR butonu.
    private void BuildControls()
    {
        // Sıkı grup (kullanıcı 2026-11-28: "dümen gibi, uzağa gitmeden bassın"): oklar arası 10, İNDİR hemen yanında.
        rightButton = ArrowButton("Right", new Vector2(-220f, ControlsY), 0f);
        backButton = ArrowButton("Back", new Vector2(-70f, ControlsY), 90f);
        var drop = ClawMachineUI.TextButton("Drop", design, "", new Vector2(260, 130), new Vector2(165f, ControlsY), UiButtons.Kind.Green, Drop);
        dropLabel = drop.GetComponentInChildren<TMP_Text>();
        dropButton = (RectTransform)drop.transform;
    }

    private ClawMachineHoldButton ArrowButton(string name, Vector2 pos, float angle)
    {
        var pad = ClawMachineUI.Picture(name, design, BridgeRepairUI.Pill(), Vector2.one * ArrowButtonSize, pos, false);
        pad.type = Image.Type.Sliced;
        pad.color = new Color(1f, 1f, 1f, 0.18f);
        pad.raycastTarget = true;
        var arrow = ClawMachineUI.Picture("Arrow", pad.rectTransform, cfg.arrowIcon, Vector2.one * (ArrowButtonSize * 0.62f), Vector2.zero);
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        return pad.gameObject.AddComponent<ClawMachineHoldButton>();
    }

    // Basılıyken tuş biraz küçülür ve parlar (yalnız durum değişince yazılır).
    private bool rightShown, backShown;
    private void ShowHeld(ClawMachineHoldButton b, ref bool shown)
    {
        if (b == null || b.Held == shown) return;
        shown = b.Held;
        b.transform.localScale = Vector3.one * (shown ? 0.92f : 1f);
        if (b.TryGetComponent(out Image img)) img.color = new Color(1f, 1f, 1f, shown ? 0.34f : 0.18f);
    }

    private TMP_Text dropLabel;
    private RectTransform dropButton;

    // ── İlk giriş öğreticisi (kullanıcı 2026-11-28): sağa → geriye → İNDİR ─────────────────────────
    // Ömür boyu ilk deneme (IsTutorialAttempt; o deneme zaten geniş toleranslı + güçlü). Hedef oyuncağın üstünde aşağı
    // bakan zıplayan ok; el sıradaki tuşun üstünde, tuş nabız gibi atar. Adım, tuş bırakılınca ilerler.
    private int tutStep;            // 0 yok, 1 sağa, 2 geriye, 3 İNDİR
    private int tutTarget = -1;
    private float tutT;
    private Image tutHand, tutArrow;
    private static readonly Vector2 HandTip = new Vector2(0.33f, 0.02f);

    private void TryStartTutorial()
    {
        if (tutStep != 0 || phase != Phase.Ready || closing || !ClawMachineState.HasJoined
            || !ClawMachineState.IsTutorialAttempt || ClawMachineState.Tokens <= 0 || intro.gameObject.activeSelf) return;
        if (tutHand == null)
        {
            tutHand = ClawMachineUI.Picture("TutorialHand", design, Resources.Load<Sprite>("UIIcons/FingerTutorial"), new Vector2(110, 120), Vector2.zero);
            tutHand.rectTransform.pivot = HandTip;
            tutArrow = ClawMachineUI.Picture("TutorialTarget", machine, cfg.arrowIcon, new Vector2(90, 90), Vector2.zero);
            tutArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);   // aşağı bakar
        }
        // Hedef: orta sırada ortaya en yakın oyuncak (geriye gitmek anlamlı olsun).
        float best = float.MaxValue;
        for (int i = 0; i < items.Length; i++)
        {
            ClawMachineRules.Slot(i, items.Length, out float u, out _);
            if (ClawMachineRules.Row(i) == 1 && Mathf.Abs(u - 0.5f) < best) { best = Mathf.Abs(u - 0.5f); tutTarget = i; }
        }
        SetTutorialStep(1);
    }

    private void SetTutorialStep(int step)
    {
        tutStep = step;
        bool on = step > 0;
        if (tutHand != null) { tutHand.gameObject.SetActive(on); tutHand.transform.SetAsLastSibling(); }
        if (tutArrow != null) { tutArrow.gameObject.SetActive(on && tutTarget >= 0); tutArrow.transform.SetAsLastSibling(); }
        if (rightButton != null) rightButton.transform.localScale = Vector3.one;
        if (backButton != null) backButton.transform.localScale = Vector3.one;
        if (dropButton != null) dropButton.localScale = Vector3.one;
        if (!on) { RefreshTexts(); return; }
        hint.text = step == 1 ? ClawMachineUI.L("tut_right", "Sağ tuşu basılı tut: pençe sağa gider.")
            : step == 2 ? ClawMachineUI.L("tut_back", "Şimdi yukarı tuşla pençeyi geriye götür.")
            : ClawMachineUI.L("tut_drop", "Oyuncağın üstündeyken İNDİR'e bas!");
    }

    private void TickTutorial(float dt)
    {
        tutT += dt;
        float bob = Mathf.Sin(tutT * 6f);
        Transform target = tutStep == 1 ? rightButton != null ? rightButton.transform : null
            : tutStep == 2 ? backButton != null ? backButton.transform : null : dropButton;
        if (target != null)
        {
            tutHand.rectTransform.anchoredPosition = ((RectTransform)target).anchoredPosition + new Vector2(10f, 30f + 12f * bob);
            bool held = target.TryGetComponent(out ClawMachineHoldButton hb) && hb.Held;
            if (!held) target.localScale = Vector3.one * (1f + 0.06f * (0.5f + 0.5f * bob));
        }
        if (tutTarget >= 0 && tutArrow != null)
        {
            var v = items[tutTarget];
            tutArrow.rectTransform.anchoredPosition = SlotPos(tutTarget) + new Vector2(0f, v.height * v.root.localScale.y + 50f + 10f * bob);
        }
        // Adım, tuş bırakılınca ilerler (pençe gerçekten hareket ettiyse).
        if (tutStep == 1 && clawU > 0.04f && rightButton != null && !rightButton.Held) SetTutorialStep(2);
        else if (tutStep == 2 && clawW > 0.04f && backButton != null && !backButton.Held) SetTutorialStep(3);
    }

    private void PlaceChuteInOrder()
    {
        if (chuteBackImage != null) chuteBackImage.transform.SetAsLastSibling();
        if (chuteFrontImage != null) chuteFrontImage.transform.SetAsLastSibling();
    }

    // Bacaya düşen oyuncak ön camın arkasında görünsün; ödül sonrası yerine (derinlik sırası) döner.
    private void PutIntoChute(ItemView v)
    {
        if (chuteFrontImage == null) return;
        int front = chuteFrontImage.transform.GetSiblingIndex();
        v.root.SetSiblingIndex(v.root.GetSiblingIndex() < front ? front - 1 : front);
    }

    // Tek oyuncak yer değiştirdi: eski indeksine dönmesi tüm sırayı aynen geri getirir.
    private static void RestoreItemOrder(ItemView v) => v.root.SetSiblingIndex(v.sibling);

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
        view.iconBase = w * 0.58f;
        // Miktar: kapsülün renkli alt yarısında, ön camın önünde (okunur).
        view.amount = ClawMachineUI.Label("Amount", view.root, "", 24, new Vector2(w * 0.9f, 30f), new Vector2(0, w * 0.17f),
            TextSurfaceStyle.Base(TextSurface.Purple));
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
        powerText.fontStyle |= FontStyles.Italic;   // makinenin atari yazısına yakışsın (kullanıcı 2026-11-28)

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
    private const float IntroDim = 0.98f;   // kullanıcı 2026-11-28: alttaki ekranla karışmasın
    // Engel tanıtım ekranı renkleri (TutorialOverlayController.EnsureHintLayout).
    private static readonly Color IntroEyebrow = new Color(0.38f, 0.90f, 0.88f, 1f), IntroBodyColor = new Color(1f, 0.95f, 0.85f, 1f);
    private static readonly Color IntroTitleTop = new Color(1f, 0.92f, 0.62f, 1f), IntroTitleBottom = new Color(1f, 0.65f, 0.22f, 1f);

    private TMP_Text IntroText(string name, string text, float size, Vector2 box, Vector2 pos, Color color)
    {
        var label = ClawMachineUI.Rect(name, introContent, box, pos).gameObject.AddComponent<TextMeshProUGUI>();
        if (cfg.introFont != null) label.font = cfg.introFont;
        label.text = text;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = label.fontSizeMax = size;
        label.fontSizeMin = size * 0.6f;
        label.enableAutoSizing = true;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }
    private static readonly int[] IntroSidePlush = { 1, 0, 3, 5 };   // sol iç, sol dış, sağ iç, sağ dış
    private RectTransform introContent;
    private TMP_Text introBody, introStart;

    private void BuildIntro()
    {
        intro = ClawMachineUI.Stretch("Intro", root);
        // Engel tanıtım ekranının stili (kullanıcı 2026-11-28): çerçevesiz, DynaPuff, turkuaz üst satır, altın geçişli
        // başlık, krem açıklama, altta "dokun" satırı. Herhangi bir yere dokunmak katılır (✕ hariç).
        var dim = intro.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, IntroDim); dim.raycastTarget = true;
        var join = intro.gameObject.AddComponent<Button>();
        join.targetGraphic = dim; join.transition = Selectable.Transition.None;
        join.onClick.AddListener(Join);
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
        var eyebrow = IntroText("Eyebrow", ClawMachineUI.L("intro_eyebrow", "YENİ ETKİNLİK"), 60, new Vector2(900, 80), new Vector2(0, 770), IntroEyebrow);
        eyebrow.characterSpacing = 5f;
        var title = IntroText("IntroTitle", ClawMachineUI.L("intro_title", "PENÇE MAKİNESİ"), 120, new Vector2(940, 170), new Vector2(0, 620), Color.white);
        if (cfg.introTitleMaterial != null) title.fontSharedMaterial = cfg.introTitleMaterial;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(IntroTitleTop, IntroTitleTop, IntroTitleBottom, IntroTitleBottom);
        introBody = IntroText("IntroBody", "", 50, new Vector2(820, 420), new Vector2(0, -150), IntroBodyColor);
        introBody.lineSpacing = 12f;
        introStart = IntroText("Continue", "", 42, new Vector2(900, 70), new Vector2(0, -500), IntroEyebrow);
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
        TryStartTutorial();
        EventSfx.Play(l => l.popupOpen);
        iris.PlayOpen(origin);
    }

    public void Close()
    {
        if (closing || iris.IsRunning || phase != Phase.Ready || plushAnim >= 0f || partyTime >= 0f) return;   // peluş tabelaya uçarken kapanmaz (tören kaybolmasın)
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
        HideBuyCard();
        if (tutStep > 0) SetTutorialStep(0);
        if (partyTime >= 0f)
        {
            partyTime = -1f;
            for (int i = 0; i < collectionPlush.Length; i++)
            {
                collectionPlush[i].rectTransform.anchoredPosition = MarqueeSlot(i);
                collectionPlush[i].rectTransform.localScale = Vector3.one;
            }
        }
    }
    private void OnApplicationFocus(bool value) => focused = value;

    private void Join()
    {
        if (ClawMachineState.TryJoin(cfg, DateTime.UtcNow))
        {
            EventSfx.Play(l => l.stepPop);
            RefreshTexts();
            TryStartTutorial();
        }
    }

    // ── Altınla Al kartı (RuntimeChoicePopup yerine, ekranın içinde) ───────────────────────────
    // Krem zemin + lacivert çerçeve (RewardPeekBubble dili): jeton, günlük kalan hak, fiyat butonu. Bir kez kurulur.
    // Kapsül ödülleri görünür olduğu için olasılık satırı yok (altınla alınan jeton rastgele ödüle gitmez).
    private RectTransform buyRoot, buyContent;
    private TMP_Text buyLeft, buyPrice;
    private bool BuyOpen => buyRoot != null && buyRoot.gameObject.activeSelf;

    private void AskBuy()
    {
        if (phase != Phase.Ready || closing) return;
        var now = DateTime.UtcNow;
        if (ClawMachineState.Tokens >= cfg.tokenCap) { Flash(string.Format(ClawMachineUI.L("cap", "Jeton dolu ({0})"), cfg.tokenCap)); return; }
        int left = ClawMachineState.BuysLeftToday(cfg, now);
        if (left <= 0) { Flash(ClawMachineUI.L("buy_limit", "Bugünlük alım hakkın bitti.")); return; }
        if (buyRoot == null) BuildBuyCard();
        buyLeft.text = string.Format(ClawMachineUI.L("buy_today", "Bugün kalan hak: {0}/{1}"), left, cfg.coinTokensPerDay);
        buyPrice.text = cfg.coinTokenPrice.ToString();
        buyRoot.gameObject.SetActive(true);
        buyRoot.SetAsLastSibling();
        Layout();
        EventSfx.Play(l => l.popupOpen);
    }

    private void HideBuyCard() { if (buyRoot != null) buyRoot.gameObject.SetActive(false); }

    private void ConfirmBuy()
    {
        if (ClawMachineState.TryBuyToken(cfg, DateTime.UtcNow))
        {
            EventSfx.Play(l => l.rankBadge);
            HideBuyCard();
            Flash(ClawMachineUI.L("bought", "+1 JETON"));
            return;
        }
        buyLeft.text = ClawMachineUI.L("buy_failed", "Yeterli altın yok.");
    }

    private void BuildBuyCard()
    {
        buyRoot = ClawMachineUI.Stretch("BuyCard", root);
        var dim = buyRoot.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.85f);   // kullanıcı 2026-11-28: daha koyu
        var outside = buyRoot.gameObject.AddComponent<Button>();
        outside.targetGraphic = dim; outside.transition = Selectable.Transition.None;
        outside.onClick.AddListener(HideBuyCard);
        buyContent = ClawMachineUI.Rect("Content", buyRoot, new Vector2(1000, 1800), Vector2.zero);

        var card = ClawMachineUI.Picture("Card", buyContent, Resources.Load<Sprite>("UIEffects/LegalPanel"), new Vector2(620, 560), new Vector2(0, -40), false);
        card.type = Image.Type.Sliced;
        card.color = new Color(1f, 0.96f, 0.86f);
        card.raycastTarget = true;   // karta dokunmak kapatmaz
        var frame = ClawMachineUI.Picture("Frame", card.rectTransform, Resources.Load<Sprite>("UIEffects/RewardPeekFrame"), new Vector2(632, 572), Vector2.zero, false);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.25f;

        var surface = TextSurfaceStyle.Base(TextSurface.Purple);
        ClawMachineUI.Label("Title", card.rectTransform, ClawMachineUI.L("buy_title", "Jeton Al"), 54, new Vector2(520, 70), new Vector2(0, 205), surface);
        ClawMachineUI.Picture("Token", card.rectTransform, cfg.tokenIcon, new Vector2(150, 150), new Vector2(0, 80));
        ClawMachineUI.Label("One", card.rectTransform, ClawMachineUI.L("one_token", "1 JETON"), 40, new Vector2(400, 54), new Vector2(0, -25), surface);
        buyLeft = ClawMachineUI.BodyLabel("Left", card.rectTransform, "", 30, new Vector2(520, 44), new Vector2(0, -85), surface);
        var buy = ClawMachineUI.TextButton("Buy", card.rectTransform, "", new Vector2(340, 110), new Vector2(0, -180), UiButtons.Kind.Green, ConfirmBuy);
        buyPrice = buy.GetComponentInChildren<TMP_Text>();
        var lib = TileIconLibrary.Shared;
        var coin = lib != null ? lib.GetRewardIcon(DailySlotRewardType.Coins) : null;
        if (coin != null)
        {
            ClawMachineUI.Picture("Coin", buy.transform, coin, new Vector2(64, 64), new Vector2(-105, 4));
            buyPrice.rectTransform.anchoredPosition += new Vector2(28f, 0f);
        }
        var close = ClawMachineUI.Picture("Close", card.rectTransform, cfg.closeIcon, new Vector2(72, 72), new Vector2(290, 260));
        close.raycastTarget = true;
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close; closeButton.onClick.AddListener(HideBuyCard);
        buyRoot.gameObject.SetActive(false);
    }

    // ── Girdi (yön tuşları + İNDİR) ─────────────────────────────────
    // Gerçek makine kuralı: pençe sol-önden başlar, yalnız sağa ve geriye gider (geri dönüş yok); İNDİR durdurulamaz.
    // Hareket bırakılınca pençe hızıyla orantılı sallanır; sallanırken indirmek isabeti kaydırır (beceri).
    // Süre sınırı (sezonun untimedAttempts denemesinden sonra) ilk hareketle başlar, bitince pençe kendiliğinden iner.

    private bool CanControl => phase == Phase.Ready && !closing && !iris.IsRunning && !intro.gameObject.activeSelf && !BuyOpen && partyTime < 0f
        && !ClawMachineState.InCooldown(DateTime.UtcNow);   // molada (oturum bitti) pençe oynamaz

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
        ShowHeld(rightButton, ref rightShown);
        ShowHeld(backButton, ref backShown);
        var v = new Vector2(rightButton != null && rightButton.Held ? 1f : 0f, backButton != null && backButton.Held ? 1f : 0f);
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
        if (tutStep > 0) SetTutorialStep(0);
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
        TickLights(Time.unscaledDeltaTime);
        if (tokenAnim >= 0f) TickTokenInsert(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        if (plushAnim >= 0f) TickPlushFly(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        if (partyTime >= 0f) TickParty(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        if (phase == Phase.Ready)
        {
            UpdateControl(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            if (tutStep > 0) TickTutorial(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
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
                SetClawTop(Mathf.Lerp(ClawRestTop(), grabTop, EaseInOut(k)));
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
                float top = Mathf.Lerp(grabTop, ClawRestTop(), EaseInOut(k));
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
                // Bacaya: yatayda sağa, derinlikte bacanın derinliğine (ChuteDepth).
                SetClawDepth(Mathf.Lerp(carryFromDepth, ChuteDepth, EaseInOut(k)));
                claw.anchoredPosition = new Vector2(Mathf.Lerp(carryFrom.x, chuteDrawX, EaseInOut(k)), claw.anchoredPosition.y);
                SyncCable(); AttachToClaw(view);
                if (k >= 1f) { SetClawSprite(false); releaseFromY = ClawTipY() - view.height * 0.45f; PutIntoChute(view); Begin(Phase.Release, 0.8f); }
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
                view.root.anchoredPosition = new Vector2(chuteDrawX, y);
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
                    RestoreItemOrder(view);
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
                claw.anchoredPosition = new Vector2(Mathf.Lerp(homeFromX, ClawX(0f, 0f), e), ClawRestTop());
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
        SetClawTop(ClawRestTop());
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
    // Koleksiyon şöleni (kullanıcı 2026-11-28): 1) tabelada parti — 6 peluş sırayla zıplar, lambalar hızlı yanıp söner,
    // konfeti + fanfar; 2) ortak sandık açılış töreni (RewardChestRevealOverlay, yalnız sunum — ödüller yakalama anında
    // kaydedildi). Parti sürerken kontroller ve kapatma kilitli.
    private const float PartySeconds = 1.6f, PartyHopStagger = 0.12f, PartyHop = 0.35f, PartyHopHeight = 22f;
    private float partyTime = -1f;

    private void TryCollectionCeremony()
    {
        if (!needsCollectionCeremony || phase != Phase.Ready || plushAnim >= 0f || partyTime >= 0f) return;
        needsCollectionCeremony = false;
        partyTime = 0f;
        EventSfx.Play(l => l.confetti);
        EventSfx.Play(l => l.victoryFanfare);
    }

    private void TickParty(float dt)
    {
        partyTime += dt;
        for (int i = 0; i < collectionPlush.Length; i++)
        {
            // Sırayla iki kez zıplar (dalga).
            float local = (partyTime - i * PartyHopStagger) % (PartyHop * 2f);
            float hop = partyTime >= i * PartyHopStagger && local < PartyHop ? Mathf.Sin(local / PartyHop * Mathf.PI) : 0f;
            var rt = collectionPlush[i].rectTransform;
            rt.anchoredPosition = MarqueeSlot(i) + new Vector2(0f, hop * PartyHopHeight);
            rt.localScale = Vector3.one * (1f + 0.15f * hop);
        }
        if (partyTime < PartySeconds) return;
        partyTime = -1f;
        for (int i = 0; i < collectionPlush.Length; i++)
        {
            collectionPlush[i].rectTransform.anchoredPosition = MarqueeSlot(i);
            collectionPlush[i].rectTransform.localScale = Vector3.one;
        }
        RewardChestRevealOverlay.Show(cfg.collectionRewards, null, titleKey: "claw_complete_title");
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
    private float DepthScale(float w) => 1f - depthShrink * Mathf.Clamp01(w);
    // Perspektif makinenin ortasına (x=0) göre: kiriş de aynı merkezden daralır → pençe hep çubukta kalır.
    private float ClawX(float u, float w) => (playX0 + playW * u) * (1f - depthNarrow * w);
    private float FloorAt(float w) => floorY + depthLift * Mathf.Clamp01(w);
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
        // Pençe uçları oyuncağın üst üçte birini kavrar. Boşta (iki oyuncağın arası / bir oyuncağın kenarı) gerçek
        // makinedeki gibi aralarından aşağı kayar: pençenin derinliğindeki tabana yakın alt katmana iner.
        float tips = attempt.Outcome == ClawMachineRules.Outcome.Empty
            ? FloorAt(clawDepth) + 30f * DepthScale(clawDepth) : itemTop - v.height * scale * 0.35f;
        return tips + ClawHeight() * 0.92f;
    }

    /// Yuva başına sabit küçük eğim (yığılmış görünsün, dizilmiş değil). Deterministik: açılışlar arasında değişmez.
    private const float MaxRestTilt = 7f;
    private static float Tilt(int seed) => ((seed * 37 + 11) % 15 - 7) / 7f * MaxRestTilt;
    private static Quaternion RestRotation(int slot) => Quaternion.Euler(0f, 0f, Tilt(slot));

    // Kiriş derinlikle iner; pençe kirişin altından sarkar (ölçekle).
    private float GantryY() => railY - gantryDrop * clawDepth;
    private float ClawRestTop() => GantryY() - 8f * DepthScale(clawDepth);

    private void PlaceClawAtRest()
    {
        claw.anchoredPosition = new Vector2(ClawX(Mathf.Clamp01(clawU + swayOffset), clawW), ClawRestTop());
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
        float gy = GantryY();
        cable.anchoredPosition = new Vector2(claw.anchoredPosition.x, gy);
        cable.sizeDelta = new Vector2(6f * DepthScale(clawDepth), Mathf.Max(0f, gy - claw.anchoredPosition.y + 6f));
        if (gantry != null)
        {
            // Perspektif: geriye gittikçe aşağı iner, yatayda daralır (ClawX ile aynı oran), kalınlık küçülür.
            var grt = gantry.rectTransform;
            grt.anchoredPosition = new Vector2(0f, gy);   // camın ortası; yalnız iner ve daralır
            grt.localScale = new Vector3(1f - depthNarrow * clawDepth, DepthScale(clawDepth), 1f);
        }
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
        v.plush.enabled = plush;
        // Kapsülün ödülü görünür (2026-11-28: sürpriz kapsül kalktı — oyuncu hedefini seçer).
        var prize = plush ? null : cfg.Prize(ClawMachineState.PrizeAt(slot));
        var reward = prize != null ? prize.reward : null;
        v.icon.sprite = reward != null ? reward.ResolveIcon() : null;
        v.icon.enabled = v.icon.sprite != null;
        float iconSize = v.iconBase * (prize != null && prize.big ? 1.15f : 1f);
        v.icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        v.amount.gameObject.SetActive(reward != null);
        if (reward != null)
            v.amount.text = reward.IsTimed ? reward.AmountLabel
                : reward.type == DailySlotRewardType.Coins ? reward.amount.ToString() : "x" + reward.amount;
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
        if (v.amount != null) v.amount.canvasRenderer.SetAlpha(a);
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
                "Her {0} seviyede 1 jeton, zor seviyede hemen 1 jeton.\nOklarla pençeyi sağa ve geriye götür, İNDİR'e bas!\n6 peluşu topla, büyük sandığı aç.\nEtkinlik bitince kalan jetonlar silinir."), cfg.winsPerToken);
            if (introStart != null) introStart.text = string.Format(ClawMachineUI.L("tap_start", "Başlamak için dokun (+{0} jeton)"), cfg.joinTokens);
        }
        title.text = ClawMachineUI.L("title", "PENÇE MAKİNESİ");
        tokensText.text = cfg.tokenIcon != null ? ClawMachineState.Tokens.ToString()
            : string.Format(ClawMachineUI.L("tokens", "JETON {0}"), ClawMachineState.Tokens);
        buyButton.gameObject.SetActive(joined && ClawMachineState.Tokens < cfg.tokenCap);
        // Güçlü pençe: kenar da tutar, nişan yine şart (garanti değil). Öğretici denemesi de güçlü sayılır.
        bool guaranteed = ClawMachineState.NextIsStrong(cfg);
        powerText.text = guaranteed ? ClawMachineUI.L("strong", "PENÇE GÜÇLÜ! İyi nişan al.")
            : ClawMachineUI.L("power", "PENÇE GÜCÜ");   // büyük ödül artık makinede görünür (sayaç yok)
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
        var nowUtc = DateTime.UtcNow;
        if (Time.unscaledTime >= flashUntil && ClawMachineState.InCooldown(nowUtc))
            hint.text = string.Format(ClawMachineUI.L("resting", "Makine dinleniyor: {0}"), TimeFormat.Countdown(ClawMachineState.CooldownUntil - nowUtc));
        else if (Time.unscaledTime >= flashUntil && !moveTimed && tutStep == 0)
            hint.text = ClawMachineState.Tokens <= 0 ? ClawMachineUI.L("no_tokens", "Jeton yok! Seviye kazan ya da altınla al.")
                : ClawMachineState.IsTutorialAttempt ? ClawMachineUI.L("tutorial", "Oklarla pençeyi oyuncağın tam üstüne getir, İNDİR'e bas!")
                : ClawMachineUI.L("hint", "Okları basılı tut, sonra İNDİR!");
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        var now = DateTime.UtcNow;
        if (!ClawMachineState.IsLive(cfg, now)) { if (phase == Phase.Ready && !closing) Close(); return; }
        // Oturum sürüyorsa kalan oyun, yoksa bugünün kapanışına kalan süre.
        var session = ClawMachineState.SessionLeft(cfg, now);
        timer.text = session > TimeSpan.Zero
            ? string.Format(ClawMachineUI.L("session_left", "Kalan oyun {0}"), TimeFormat.Countdown(session))
            : TimeFormat.Countdown(ClawMachineState.OpenDayEnd(cfg, now) - now);
        // Mola: kontroller kilitli, ipucunda mola sonuna kalan süre (öncelikli, saniyede bir).
        if (ClawMachineState.InCooldown(now) && phase == Phase.Ready && Time.unscaledTime >= flashUntil)
            hint.text = string.Format(ClawMachineUI.L("resting", "Makine dinleniyor: {0}"), TimeFormat.Countdown(ClawMachineState.CooldownUntil - now));
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
        if (buyContent != null)
        {
            buyContent.localScale = design.localScale;
            buyContent.anchoredPosition = design.anchoredPosition;
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
