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
    private AudioSource audioSource;
    private bool claimed;
    private static Sprite glowSprite;
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

    public IEnumerator Present(int share, int prizePool, int winners, Sprite coinSprite,
        Sprite ribbonSprite, Sprite buttonSprite, TMP_FontAsset displayFont,
        AudioClip collectSfx, AudioMixerGroup mixer, float countDuration, Material eventLabelMaterial = null)
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
        var eventLabel = Label("Event", "SAFARİ TAMAMLANDI", 40, new Vector2(820f, 80f), new Vector2(0f, 715f), Cream);
        // Kalın sarı outline + etrafında yumuşak gölge (Fonts/Materials/Inter_ExtraBold_GoldOutlineGlow).
        if (eventLabelMaterial != null)
            eventLabel.fontSharedMaterial = eventLabelMaterial;
        var title = Label("Title", "ZİRVE SENİN!", 92, new Vector2(700f, 150f), new Vector2(0f, 405f), Cream, true);
        title.gameObject.AddComponent<TMPArcText>().arcDegrees = -15f;
        var hero = Picture("GoldReward", design, coinSprite, new Vector2(270f, 270f), new Vector2(0f, 45f));
        Label("RewardCaption", "KAZANDIĞIN ALTIN", 36, new Vector2(780f, 60f), new Vector2(0f, -132f), Cream);
        var amount = Label("Amount", "+0", 150, new Vector2(850f, 190f), new Vector2(0f, AmountY), Gold, true);
        Label("Pool", $"{prizePool:N0} ALTINLIK BÜYÜK ÖDÜL", 42,
            new Vector2(820f, 65f), new Vector2(0f, -378f), Gold);
        Label("Winners", $"{winners} kazanan arasında paylaşıldı", 36,
            new Vector2(820f, 70f), new Vector2(0f, -442f), Cream);

        // GreenButonwoStroke 289x116 oranında (≈2.49) büyütülür.
        var buttonImage = Picture("ClaimButton", design, buttonSprite,
            new Vector2(520f, 520f * 116f / 289f), new Vector2(0f, -650f));
        buttonImage.raycastTarget = true;
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.interactable = false;
        button.onClick.AddListener(() => claimed = true);
        var buttonLabel = Label("ClaimLabel", "ALTINLAR TOPLANIYOR", 44,
            new Vector2(440f, 130f), new Vector2(0f, 6f), Cream, false, buttonImage.transform);
        var hint = Label("Hint", "Ödülün birazdan hazır!", 30,
            new Vector2(820f, 55f), new Vector2(0f, -785f), Cream);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.outputAudioMixerGroup = mixer;

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
                if (collectSfx != null && GameSettings.SoundEnabled && elapsed - lastSound >= Mathf.Max(0.1f, collectSfx.length * 0.45f))
                {
                    audioSource.pitch = Mathf.Lerp(0.94f, 1.14f, landed / (float)count);
                    audioSource.PlayOneShot(collectSfx, 0.25f);
                    lastSound = elapsed;
                }
            }
            audioSource.mute = !GameSettings.SoundEnabled;
            amount.rectTransform.localScale = Vector3.one * (1f + 0.075f * Mathf.Exp(-12f * Mathf.Max(0f, elapsed - lastArrival)));
            hero.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(elapsed * 3.2f) * 0.035f);
            halo.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(elapsed * 1.7f) * 0.06f);
            yield return null;
        }
        Destroy(rainLayer.gameObject);
        amount.text = $"+{share:N0}";
        amount.rectTransform.localScale = Vector3.one;
        buttonLabel.text = "ÖDÜLÜ AL";
        buttonLabel.fontSize = buttonLabel.fontSizeMax = 72f;
        hint.text = "Altınlarını cüzdanına ekle";
        // A release from the counting animation must not claim the reward accidentally.
        yield return null;
        button.interactable = true;
        float idle = 0f;
        while (!claimed)
        {
            idle += Time.unscaledDeltaTime;
            audioSource.mute = !GameSettings.SoundEnabled;
            hero.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(idle * 2.4f) * 0.025f);
            buttonImage.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(idle * 3f) * 0.015f);
            yield return null;
        }
        button.interactable = false;
    }

    private void OnDisable()
    {
        if (audioSource != null) audioSource.Stop();
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
