using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>Safari victory presentation. Coin arrivals drive the displayed reward; the owner grants it once.</summary>
public sealed class SafariRewardView : MonoBehaviour
{
    private RectTransform design;
    private TMP_FontAsset font;
    private bool claimed;
    private static Sprite glowSprite;
    private static Material s_wowFront, s_wowShadow;
    private const float WowSize = 150f;
    private static readonly Color Gold = new Color(1f, 0.83f, 0.25f);
    private static readonly Color Cream = new Color(1f, 0.97f, 0.84f);
    private const float AmountY = -240f;   // "+N" satırı; düşen altınlar buraya iner

    public static SafariRewardView Create(Transform parent)
    {
        var go = new GameObject("SafariRewardOverlay", typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var dim = go.GetComponent<Image>();
        dim.color = new Color(0.025f, 0.055f, 0.12f, 0.94f);
        dim.raycastTarget = true;
        return go.AddComponent<SafariRewardView>();
    }

    /// <param name="winnerAvatar">Oyuncunun kalabalıktaki avatarı: kopyası ortada "kazanan" görseli olur (yoksa altın).</param>
    public IEnumerator Present(int share, int prizePool, int winners, Sprite coinSprite,
        Sprite ribbonSprite, Sprite buttonSprite, TMP_FontAsset displayFont,
        AudioClip collectSfx, AudioMixerGroup mixer, float countDuration, Material eventLabelMaterial = null,
        RectTransform winnerAvatar = null)
    {
        font = displayFont;
        claimed = false;
        design = CreateRect("Celebration", transform, new Vector2(900f, 1240f), Vector2.zero);
        Canvas.ForceUpdateCanvases();
        Rect viewport = ((RectTransform)transform).rect;
        float fit = Mathf.Min(1f, Mathf.Min(viewport.width / 980f, viewport.height / 1640f));
        design.localScale = Vector3.one * fit;
        var group = design.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var halo = Picture("GoldenGlow", design, Glow(), new Vector2(900f, 900f), new Vector2(0f, 45f));
        halo.color = new Color(1f, 0.64f, 0.1f, 0.6f);
        var rainLayer = CreateRect("CoinRain", design, new Vector2(900f, 1240f), Vector2.zero);
        // Kurdele görseli 1187x493 (≈2.41): kutu bu orana göre → preserveAspect yüksekliğe takılıp
        // küçültmez. Başlık kurdelenin sarkmasını izlesin diye -15° yayla bükülür.
        Picture("VictoryRibbon", design, ribbonSprite, new Vector2(960f, 398f), new Vector2(0f, 420f));
        // HARİKA (kullanıcı 2026-11-22): kaldırılan ilk-taş sahnesinin başlık sistemi — krem yüz, bordo kontur,
        // altın dış halka, altında bordo gölge; ezilip büyüyerek doğar. Etkinlik satırı başlığın altına iner.
        var wow = BuildWowTitle(GameLocalization.GetOr("rising_reward_wow", "HARİKA!"));
        wow.anchoredPosition = new Vector2(0f, 760f);
        wow.localScale = Vector3.zero;
        var eventLabel = Label("Event", GameLocalization.Get("safari_reward_event"), 40, new Vector2(820f, 80f), new Vector2(0f, 628f), Cream);
        // Kalın sarı outline + etrafında yumuşak gölge (Fonts/Materials/Inter_ExtraBold_GoldOutlineGlow).
        if (eventLabelMaterial != null)
            eventLabel.fontSharedMaterial = eventLabelMaterial;
        var title = Label("Title", GameLocalization.Get("safari_reward_title"), 92, new Vector2(700f, 150f), new Vector2(0f, 405f), Cream, true);
        title.gameObject.AddComponent<TMPArcText>().arcDegrees = -15f;
        title.rectTransform.localScale = Vector3.zero;
        // Kazanan görseli: oyuncunun kendi avatarı ortada, altın kesesi sağ altında rozet gibi. Avatar yoksa eski düzen.
        RectTransform winner = CloneAvatar(winnerAvatar, design, 300f, new Vector2(0f, 60f));
        var hero = winner != null
            ? Picture("GoldReward", design, coinSprite, new Vector2(150f, 150f), new Vector2(135f, -45f))
            : Picture("GoldReward", design, coinSprite, new Vector2(270f, 270f), new Vector2(0f, 45f));
        if (winner != null) winner.localScale = Vector3.zero;
        hero.rectTransform.localScale = Vector3.zero;
        Label("RewardCaption", GameLocalization.Get("safari_reward_caption"), 36, new Vector2(780f, 60f), new Vector2(0f, -132f), Cream);
        var amount = Label("Amount", "+0", 150, new Vector2(850f, 190f), new Vector2(0f, AmountY), Gold, true);
        Label("Pool", GameLocalization.GetFormat("safari_reward_pool", prizePool), 42,
            new Vector2(820f, 65f), new Vector2(0f, -378f), Gold);
        Label("Winners", GameLocalization.GetFormat("safari_reward_winners", winners), 36,
            new Vector2(820f, 70f), new Vector2(0f, -442f), Cream);

        // GreenButonwoStroke 289x116 oranında (≈2.49) büyütülür.
        var buttonImage = Picture("ClaimButton", design, buttonSprite,
            new Vector2(520f, 520f * 116f / 289f), new Vector2(0f, -650f));
        buttonImage.raycastTarget = true;
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.interactable = false;
        button.onClick.AddListener(() => claimed = true);
        var buttonLabel = Label("ClaimLabel", GameLocalization.Get("safari_reward_collecting"), 44,
            new Vector2(440f, 130f), new Vector2(0f, 6f), Cream, false, buttonImage.transform);
        var hint = Label("Hint", GameLocalization.Get("safari_reward_hint_wait"), 30,
            new Vector2(820f, 55f), new Vector2(0f, -785f), Cream);

        float intro = 0f;
        while (intro < 0.38f)
        {
            intro += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(intro / 0.38f);
            group.alpha = Mathf.SmoothStep(0f, 1f, k);
            design.localScale = Vector3.one * fit * (1f - 0.12f * Mathf.Pow(1f - k, 3f) + Mathf.Sin(k * Mathf.PI) * 0.025f);
            yield return null;
        }
        group.alpha = 1f;
        design.localScale = Vector3.one * fit;

        // Sıra: HARİKA ezilip büyür → ZİRVE SENİN! aynı hareketle → kazanan avatar + altın rozeti.
        StartCoroutine(SquashGrow(wow, 0f));
        StartCoroutine(SquashGrow(title.rectTransform, 0.28f));
        if (winner != null) StartCoroutine(SquashGrow(winner, 0.5f));
        var heroIntro = StartCoroutine(SquashGrow(hero.rectTransform, winner != null ? 0.68f : 0.5f));
        yield return heroIntro; // Complete scale ownership before the idle/counting animation takes over.

        // Visual coin count is capped; each arrival represents an exact portion of the award.
        int count = Mathf.Clamp(share, 1, 40);
        var coins = new Image[count];
        var starts = new Vector2[count];
        var arrived = new bool[count];
        var random = new System.Random(share ^ (winners * 397));
        float duration = Mathf.Max(2.6f, countDuration);
        const float flight = 1.05f;
        float spacing = (duration - flight) / Mathf.Max(1, count - 1);
        float startY = viewport.height / (2f * fit) + 100f;
        for (int i = 0; i < count; i++)
        {
            starts[i] = new Vector2(Mathf.Lerp(-470f, 470f, (float)random.NextDouble()), startY + (float)random.NextDouble() * 120f);
            float size = Mathf.Lerp(50f, 88f, (float)random.NextDouble());
            coins[i] = Picture("FallingCoin", rainLayer, coinSprite, Vector2.one * size, starts[i]);
            coins[i].gameObject.SetActive(false);
        }

        int landed = 0;
        float elapsed = 0f, lastArrival = -1f, lastSound = -1f;
        while (landed < count)
        {
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < count; i++)
            {
                if (arrived[i] || elapsed < i * spacing) continue;
                var rt = coins[i].rectTransform;
                coins[i].gameObject.SetActive(true);
                float k = Mathf.Clamp01((elapsed - i * spacing) / flight);
                float gravity = k * k;
                float converge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, k));
                rt.anchoredPosition = new Vector2(Mathf.Lerp(starts[i].x, 0f, converge), Mathf.Lerp(starts[i].y, AmountY, gravity));
                float shrink = Mathf.Lerp(1f, 0.38f, k * k);
                rt.localScale = new Vector3(Mathf.Max(0.16f, Mathf.Abs(Mathf.Cos(k * Mathf.PI * 3f + i))) * shrink, shrink, 1f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 2f + i) * 18f);
                if (k < 1f) continue;
                arrived[i] = true;
                landed++;
                coins[i].gameObject.SetActive(false);
                amount.text = $"+{(long)share * landed / count:N0}";
                lastArrival = elapsed;
                // Merkezi ses havuzu (EventSfx): ayar kapalıysa çalmaz; görünüm başına AudioSource açılmaz.
                if (collectSfx != null && GameSettings.SoundEnabled && elapsed - lastSound >= Mathf.Max(0.1f, collectSfx.length * 0.45f))
                {
                    EventSfx.PlayTrackedClip(collectSfx, 0.25f, false, Mathf.Lerp(0.94f, 1.14f, landed / (float)count));
                    lastSound = elapsed;
                }
            }
            amount.rectTransform.localScale = Vector3.one * (1f + 0.075f * Mathf.Exp(-12f * Mathf.Max(0f, elapsed - lastArrival)));
            hero.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(elapsed * 3.2f) * 0.035f);
            halo.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(elapsed * 1.7f) * 0.06f);
            yield return null;
        }
        Destroy(rainLayer.gameObject);
        amount.text = $"+{share:N0}";
        amount.rectTransform.localScale = Vector3.one;
        buttonLabel.text = GameLocalization.Get("bridge_victory_claim");
        buttonLabel.fontSize = buttonLabel.fontSizeMax = 72f;
        hint.text = GameLocalization.Get("safari_reward_hint_claim");
        // A release from the counting animation must not claim the reward accidentally.
        yield return null;
        button.interactable = true;
        float idle = 0f;
        while (!claimed)
        {
            idle += Time.unscaledDeltaTime;
            if (winner != null) winner.localScale = Vector3.one * (1f + Mathf.Sin(idle * 2.4f + 1f) * 0.03f);
            hero.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(idle * 2.4f) * 0.025f);
            buttonImage.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(idle * 3f) * 0.015f);
            yield return null;
        }
        button.interactable = false;
    }

    // Ezilip büyüyerek doğuş: önce yatay basık, sonra dikey uzar, küçük sekmeyle oturur (kullanıcı 2026-11-22).
    private static IEnumerator SquashGrow(RectTransform rt, float delay)
    {
        if (rt == null) yield break;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        const float duration = 0.62f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            if (rt == null) yield break;
            rt.localScale = SquashGrowScale(t / duration);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    private static Vector3 SquashGrowScale(float k)
    {
        // Anahtar kareler (x, y): 0 → basık geniş → dar uzun → hafif basık → 1.
        if (k < 0.35f) return Seg(new Vector2(0.15f, 0.08f), new Vector2(1.42f, 0.5f), k / 0.35f);
        if (k < 0.6f) return Seg(new Vector2(1.42f, 0.5f), new Vector2(0.84f, 1.24f), (k - 0.35f) / 0.25f);
        if (k < 0.8f) return Seg(new Vector2(0.84f, 1.24f), new Vector2(1.07f, 0.95f), (k - 0.6f) / 0.2f);
        return Seg(new Vector2(1.07f, 0.95f), Vector2.one, (k - 0.8f) / 0.2f);
    }

    private static Vector3 Seg(Vector2 a, Vector2 b, float t)
    {
        t = Mathf.Clamp01(t);
        t = t * t * (3f - 2f * t);
        var v = Vector2.LerpUnclamped(a, b, t);
        return new Vector3(v.x, v.y, 1f);
    }

    private RectTransform CloneAvatar(RectTransform source, Transform parent, float size, Vector2 position)
    {
        if (source == null) return null;
        var copy = Instantiate(source.gameObject, parent, false);
        copy.name = "WinnerAvatar";
        copy.SetActive(true);
        var rt = (RectTransform)copy.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = position;
        Vector2 s = source.rect.size;
        float longest = Mathf.Max(1f, Mathf.Max(s.x, s.y));
        // Kalabalıktaki ölçek/şeffaflık taşınmasın: tam görünür.
        foreach (var g in copy.GetComponentsInChildren<Graphic>(true))
        {
            var c = g.color; c.a = 1f; g.color = c;
            g.raycastTarget = false;
        }
        foreach (var cg in copy.GetComponentsInChildren<CanvasGroup>(true)) cg.alpha = 1f;
        // Çocuklar avatarın eski boyutuna göre konumlu: kopyayı boyutla değil ölçekle büyüt.
        rt.sizeDelta = s;
        var holder = CreateRect("WinnerHolder", parent, s * (size / longest), position);
        rt.SetParent(holder, false);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one * (size / longest);
        var glow = Picture("WinnerGlow", holder, Glow(), Vector2.one * size * 1.9f, Vector2.zero);
        glow.color = new Color(1f, 0.85f, 0.35f, 0.75f);
        glow.transform.SetAsFirstSibling();
        return holder;
    }

    private RectTransform BuildWowTitle(string text)
    {
        var holder = CreateRect("WowTitle", design, new Vector2(1000f, 240f), Vector2.zero);
        var skin = CommonPopupSkin.Shared;
        var titleFont = skin != null && skin.celebrationFont != null ? skin.celebrationFont : font;
        var shadow = WowText("Shadow", holder, text, titleFont);
        shadow.rectTransform.anchoredPosition = new Vector2(0f, -14f);
        var front = WowText("Front", holder, text, titleFont);
        EnsureWowMaterials(front);
        if (s_wowShadow != null) shadow.fontSharedMaterial = s_wowShadow;
        if (s_wowFront != null) front.fontSharedMaterial = s_wowFront;
        front.enableVertexGradient = true;
        front.colorGradient = new VertexGradient(new Color(1f, 0.98f, 0.9f), new Color(1f, 0.98f, 0.9f),
            new Color(1f, 0.92f, 0.72f), new Color(1f, 0.92f, 0.72f));
        foreach (var t in new[] { shadow, front })
        {
            t.extraPadding = true;
            t.UpdateMeshPadding();
        }
        return holder;
    }

    private TextMeshProUGUI WowText(string name, RectTransform parent, string value, TMP_FontAsset f)
    {
        var rt = CreateRect(name, parent, parent.sizeDelta, Vector2.zero);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (f != null) t.font = f;
        t.text = value;
        t.fontSize = t.fontSizeMax = WowSize;
        t.fontSizeMin = WowSize * 0.6f;
        t.enableAutoSizing = true;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    // Malzemeler bir kez (statik), font atlası değişirse yeniden: ilk-taş sahnesindeki başlık değerleri.
    private static void EnsureWowMaterials(TMP_Text sample)
    {
        if (sample.font == null || sample.font.material == null) return;
        if (s_wowFront != null && s_wowFront.mainTexture == sample.font.material.mainTexture) return;

        var maroon = new Color(0.45f, 0.04f, 0.04f);
        s_wowFront = new Material(sample.font.material) { name = "RisingWow_TitleFront" };
        s_wowFront.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        s_wowFront.SetColor(ShaderUtilities.ID_OutlineColor, maroon);
        s_wowFront.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        s_wowFront.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        s_wowFront.EnableKeyword(ShaderUtilities.Keyword_Outline);
        s_wowFront.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        s_wowFront.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(1f, 0.74f, 0.08f));   // altın dış halka
        s_wowFront.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        s_wowFront.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        s_wowFront.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.7f);
        s_wowFront.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        ShaderUtilities.UpdateShaderRatios(s_wowFront);

        s_wowShadow = new Material(s_wowFront) { name = "RisingWow_TitleShadow" };
        s_wowShadow.SetColor(ShaderUtilities.ID_FaceColor, maroon);
        s_wowShadow.SetColor(ShaderUtilities.ID_UnderlayColor, maroon);
        ShaderUtilities.UpdateShaderRatios(s_wowShadow);
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    private Image Picture(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position)
    {
        var image = CreateRect(name, parent, size, position).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    private TMP_Text Label(string name, string value, float size, Vector2 box, Vector2 position,
        Color color, bool gradient = false, Transform parent = null)
    {
        var text = CreateRect(name, parent != null ? parent : design, box, position).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = text.fontSizeMax = size;
        text.fontSizeMin = size * 0.65f;
        text.enableAutoSizing = true;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = color;
        text.raycastTarget = false;
        text.outlineColor = new Color(0.12f, 0.065f, 0.02f, 1f);
        text.outlineWidth = 0.16f;
        if (gradient)
        {
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(Cream, Cream, Gold, Gold);
        }
        return text;
    }

    private static Sprite Glow()
    {
        if (glowSprite != null) return glowSprite;
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float radius = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        glowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f);
        return glowSprite;
    }
}
