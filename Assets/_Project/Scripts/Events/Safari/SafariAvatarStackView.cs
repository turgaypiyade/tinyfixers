using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Katılımcı avatarlarını helmet çerçevelerde "huddle" (kama) düzeninde yığar:
/// oyuncu ÖN-ORTADA en büyük, yanlara doğru küçülen bir ön sıra ve arkada
/// istiflenen (küçülen + koyulaşan) satırlar → kalabalık hissi. Referans görsele göre.
///
/// Her avatar: helmet frame + iç daire mask (kare/opak köşe yok) + avatar görseli.
/// Helmet sprite yoksa eski procedural circle frame'e düşer. Eleme için tek tek dışarı-alma API'leri var.
/// Runtime UI layer'ı container'dan kopyalanır (Screen Space - Camera culling guard).
/// </summary>
public sealed class SafariAvatarStackView : MonoBehaviour
{
    [Header("Yerleşim")]
    [SerializeField] private RectTransform container;
    [SerializeField, Min(16f)] private float baseSize = 100f;
    [SerializeField, Min(1f)]  private float playerSizeMultiplier = 1.25f;
    [Tooltip("Ön sıradaki avatar sayısı (oyuncu dahil, ortada).")]
    [SerializeField, Min(3)]   private int   frontRowWidth = 5;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.6f, 0.65f, 1f);
    [SerializeField] private Color frameColor       = new Color(0.9f, 0.92f, 0.96f, 1f);
    [SerializeField] private Color playerRingColor  = new Color(1f, 0.82f, 0.2f, 1f);

    [Header("Çerçeve Renkleri")]
    [SerializeField] private bool randomizeRingColors = true;
    [SerializeField, Range(0.75f, 1f)] private float ringColorAlpha = 0.96f;
    [SerializeField, Range(0.6f, 1f)] private float ringColorSaturation = 0.88f;
    [SerializeField, Range(0.75f, 1f)] private float ringColorValue = 1f;

    [Header("Helmet Frame")]
    [SerializeField] private Sprite[] helmetSprites;
    [SerializeField, Range(0.45f, 0.9f)] private float helmetAvatarScale = 0.66f;
    [SerializeField, Range(-0.25f, 0.25f)] private float helmetAvatarOffsetY = -0.08f;
    [SerializeField, Range(0.85f, 1.25f)] private float helmetFrameScale = 1.05f;
    [SerializeField] private Sprite playerHelmetSprite;
    [SerializeField] private Sprite[] botHelmetSprites;

    private readonly List<GameObject> spawned = new();
    private readonly List<RectTransform> botAvatars = new(); // ön→arka sıralı
    private RectTransform playerAvatar;

    public RectTransform Container => container != null ? container : (RectTransform)transform;
    public int BotCount => botAvatars.Count;
    public RectTransform PlayerAvatar => playerAvatar;

    public void CopyHelmetSettingsFrom(SafariAvatarStackView source)
    {
        if (source == null || source == this)
            return;

        helmetSprites = source.helmetSprites;
        helmetAvatarScale = source.helmetAvatarScale;
        helmetAvatarOffsetY = source.helmetAvatarOffsetY;
        helmetFrameScale = source.helmetFrameScale;
        playerHelmetSprite = source.playerHelmetSprite;
        botHelmetSprites = source.botHelmetSprites;
    }

    /// <summary>Sol-üst köşe gibi yerler için: SADECE oyuncunun tek avatarı.</summary>
    public void BuildSolo(SafariParticipant player, float size = -1f)
    {
        Clear();
        float s = size > 0f ? size : baseSize;
        playerAvatar = CreateAvatar(Container, player, Vector2.zero, s, true, 1f);
    }

    /// <summary>Huddle düzeninde yığ. sizeOverride verilirse ön-sıra avatar boyutu odur.</summary>
    public void Build(IReadOnlyList<SafariParticipant> participants, int maxOnScreen,
                      float sizeOverride = -1f, float spreadOverride = -1f)
    {
        Clear();
        if (participants == null || participants.Count == 0) return;

        float size = sizeOverride > 0f ? sizeOverride : baseSize;
        int total = Mathf.Max(1, Mathf.Min(maxOnScreen, participants.Count));

        // Oyuncu (ön-orta) + botları ayır.
        SafariParticipant player = default;
        bool hasPlayer = false;
        var bots = new List<SafariParticipant>();
        for (int i = 0; i < participants.Count && (bots.Count + (hasPlayer ? 1 : 0)) < total; i++)
        {
            var p = participants[i];
            if (p.isPlayer && !hasPlayer) { player = p; hasPlayer = true; }
            else bots.Add(p);
        }
        int slotsNeeded = bots.Count + (hasPlayer ? 1 : 0);
        if (slotsNeeded == 0) return;

        // Slot pozisyonları: ön beşli sabit kalır; kalanlar yalnız arkadan hafif görünür.
        float spacing = spreadOverride > 0f ? spreadOverride : size * 0.54f;
        int adaptiveFront = Mathf.CeilToInt(Mathf.Sqrt(slotsNeeded) * 1.1f);
        int front = Mathf.Max(Mathf.Max(5, frontRowWidth), adaptiveFront);

        var slots = new List<Slot>(slotsNeeded);
        var frontSlots = new[]
        {
            new Slot { pos = new Vector2(0f, -size * 0.08f),              scale = 1f,    dim = 1f    },
            new Slot { pos = new Vector2(-spacing * 0.55f, size * 0.06f), scale = 0.94f, dim = 0.96f },
            new Slot { pos = new Vector2( spacing * 0.55f, size * 0.06f), scale = 0.94f, dim = 0.96f },
            new Slot { pos = new Vector2(-spacing * 1.10f, -size * 0.02f), scale = 0.9f, dim = 0.92f },
            new Slot { pos = new Vector2( spacing * 1.10f, -size * 0.02f), scale = 0.9f, dim = 0.92f },
        };

        for (int i = 0; i < frontSlots.Length && slots.Count < slotsNeeded; i++)
            slots.Add(frontSlots[i]);

        int back = 0;
        while (slots.Count < slotsNeeded)
        {
            int row = back / front;
            int col = back % front;
            float rowOffset = (row % 2 == 1) ? spacing * 0.32f : 0f;
            float x = (col - (front - 1) / 2f) * spacing + rowOffset;
            float y = size * 0.18f + row * size * 0.16f;
            float scale = Mathf.Max(0.68f, 0.86f - row * 0.06f);
            float dim   = Mathf.Max(0.66f, 0.86f - row * 0.08f);
            slots.Add(new Slot { pos = new Vector2(x, y), scale = scale, dim = dim });
            back++;
        }

        // Atama: slot[0]=oyuncu, kalanları botlar (ön→arka).
        var root = Container;
        var holders = new RectTransform[slotsNeeded];
        // Çizimi ARKADAN öne yap (UGUI: sonraki sibling önde) → ön-orta en üstte.
        for (int k = slotsNeeded - 1; k >= 0; k--)
        {
            var s = slots[k];
            bool isPlayer = hasPlayer && k == 0;
            var who = isPlayer ? player : bots[k - (hasPlayer ? 1 : 0)];
            float avSize = isPlayer ? size * playerSizeMultiplier : size * s.scale;
            holders[k] = CreateAvatar(root, who, s.pos, avSize, isPlayer, isPlayer ? 1f : s.dim);
        }

        // Takip: oyuncu + botlar (ön→arka; Detach sondan = en arkadakini alır).
        if (hasPlayer) playerAvatar = holders[0];
        botAvatars.Clear();
        for (int k = (hasPlayer ? 1 : 0); k < slotsNeeded; k++)
            botAvatars.Add(holders[k]);
    }

    public void Clear()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);
        spawned.Clear();
        botAvatars.Clear();
        playerAvatar = null;
    }

    public List<RectTransform> SnapshotAvatars()
    {
        var result = new List<RectTransform>(spawned.Count);
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null) continue;
            result.Add((RectTransform)spawned[i].transform);
        }
        return result;
    }

    public void AdoptDetached(IReadOnlyList<RectTransform> avatars)
    {
        spawned.Clear();
        botAvatars.Clear();
        playerAvatar = null;

        if (avatars == null) return;

        for (int i = 0; i < avatars.Count; i++)
        {
            var rt = avatars[i];
            if (rt == null) continue;

            rt.SetParent(Container, true);
            spawned.Add(rt.gameObject);
            if (i == 0)
                playerAvatar = rt;
            else
                botAvatars.Add(rt);
        }

        ApplyDepthOrder();
    }

    private void ApplyDepthOrder()
    {
        for (int i = botAvatars.Count - 1; i >= 0; i--)
        {
            if (botAvatars[i] != null)
                botAvatars[i].SetAsLastSibling();
        }

        if (playerAvatar != null)
            playerAvatar.SetAsLastSibling();
    }

    /// <summary>Sondan (en arkadan) <paramref name="count"/> bot avatarı dışarı al, döndür.</summary>
    public List<RectTransform> DetachBotFallers(int count, Transform newParent)
    {
        var result = new List<RectTransform>();
        for (int k = 0; k < count && botAvatars.Count > 0; k++)
        {
            int idx = botAvatars.Count - 1;   // en arkadaki
            var rt = botAvatars[idx];
            botAvatars.RemoveAt(idx);
            if (rt != null)
            {
                spawned.Remove(rt.gameObject);
                rt.SetParent(newParent, true);
                result.Add(rt);
            }
        }
        return result;
    }

    /// <summary>Tüm canlı avatarları dışarı alır; geçiş animasyonu bittiğinde stack yeniden kurulur.</summary>
    public List<RectTransform> DetachAll(Transform newParent)
    {
        var result = new List<RectTransform>();
        if (playerAvatar != null)
        {
            spawned.Remove(playerAvatar.gameObject);
            playerAvatar.SetParent(newParent, true);
            result.Add(playerAvatar);
            playerAvatar = null;
        }

        for (int i = 0; i < botAvatars.Count; i++)
        {
            var rt = botAvatars[i];
            if (rt == null) continue;
            spawned.Remove(rt.gameObject);
            rt.SetParent(newParent, true);
            result.Add(rt);
        }

        botAvatars.Clear();
        return result;
    }

    /// <summary>Oyuncu avatarını dışarı al (düşme için), döndür.</summary>
    public RectTransform DetachPlayer(Transform newParent)
    {
        var rt = playerAvatar;
        playerAvatar = null;
        if (rt != null)
        {
            spawned.Remove(rt.gameObject);
            rt.SetParent(newParent, true);
        }
        return rt;
    }

    // ── Avatar oluşturma (yuvarlak) ──────────────────────────────

    private RectTransform CreateAvatar(RectTransform parent, SafariParticipant p, Vector2 pos,
                                       float size, bool isPlayer, float dim)
    {
        var holder = NewRect("Avatar", parent, size, pos);

        Sprite helmet = PickHelmetSprite(p);
        if (helmet == null)
        {
            var frame = AddImage(holder, "Frame", size);
            frame.sprite = CircleSprite();
            Color ring = randomizeRingColors ? BrightRingColor(p) : (isPlayer ? playerRingColor : frameColor);
            frame.color = Mul(ring, Mathf.Lerp(0.82f, 1f, dim));
        }

        float inner = helmet != null ? size * helmetAvatarScale : size * 0.84f;
        var maskRt = NewRect("Mask", holder, inner);
        if (helmet != null)
            maskRt.anchoredPosition = new Vector2(0f, size * helmetAvatarOffsetY);
        var maskImg = maskRt.gameObject.AddComponent<Image>();
        maskImg.sprite = CircleSprite();
        maskImg.raycastTarget = false;
        var mask = maskRt.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var av = AddImage(maskRt, "Img", inner);
        if (p.avatar != null) { av.sprite = p.avatar; av.color = Mul(Color.white, dim); av.preserveAspect = false; }
        else                  { av.sprite = null;     av.color = Mul(placeholderColor, dim); }

        if (helmet != null)
        {
            var helmetFrame = AddImage(holder, "Helmet", size * helmetFrameScale);
            helmetFrame.sprite = helmet;
            helmetFrame.color = Mul(Color.white, Mathf.Lerp(0.78f, 1f, dim));
            helmetFrame.preserveAspect = true;
        }

        spawned.Add(holder.gameObject);
        return holder;
    }

    private static Color Mul(Color c, float m) => new Color(c.r * m, c.g * m, c.b * m, c.a);

    private Color BrightRingColor(SafariParticipant p)
    {
        uint h = HashParticipant(p);
        float hue = (h % 360u) / 360f;
        float satJitter = ((h >> 9) & 0xFFu) / 255f * 0.1f;
        float valJitter = ((h >> 17) & 0xFFu) / 255f * 0.04f;
        Color c = Color.HSVToRGB(hue, Mathf.Clamp01(ringColorSaturation + satJitter), Mathf.Clamp01(ringColorValue - valJitter));
        c.a = ringColorAlpha;
        return c;
    }

    private static uint HashParticipant(SafariParticipant p)
    {
        string key = !string.IsNullOrEmpty(p.id) ? p.id : p.displayName;
        if (string.IsNullOrEmpty(key)) key = p.isPlayer ? "player" : "safari";

        const uint offset = 2166136261u;
        const uint prime = 16777619u;
        uint hash = offset;
        for (int i = 0; i < key.Length; i++)
        {
            hash ^= key[i];
            hash *= prime;
        }
        return hash;
    }

    private Sprite PickHelmetSprite(SafariParticipant p)
    {
        if (p.isPlayer && playerHelmetSprite != null)
            return playerHelmetSprite;

        if (!p.isPlayer)
        {
            var botHelmet = PickFrom(botHelmetSprites, HashParticipant(p));
            if (botHelmet != null)
                return botHelmet;
        }

        var fallback = PickFrom(helmetSprites, HashParticipant(p));
        if (fallback != null)
            return fallback;

        return null;
    }

    private static Sprite PickFrom(Sprite[] sprites, uint hash)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        int start = (int)(hash % (uint)sprites.Length);
        for (int i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[(start + i) % sprites.Length];
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private RectTransform NewRect(string name, Transform parent, float size, Vector2 pos = default)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;
        return rt;
    }

    private Image AddImage(Transform parent, string name, float size)
    {
        var rt = NewRect(name, parent, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    private struct Slot { public Vector2 pos; public float scale; public float dim; }

    // Prosedürel yumuşak-kenarlı dolu daire (mask + çerçeve). Bir kez üretilir.
    private static Sprite _circle;
    private static Sprite CircleSprite()
    {
        if (_circle != null) return _circle;

        const int s = 128;
        float half = s * 0.5f;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color[s * s];
        float edge = 1.5f / half;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x + 0.5f - half) / half;
            float dy = (y + 0.5f - half) / half;
            float rr = Mathf.Sqrt(dx * dx + dy * dy);
            px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - rr) / edge));
        }
        tex.SetPixels(px);
        tex.Apply();
        _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _circle;
    }
}
