using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss düellosu "alet tehdidi" geri sayımı. Board'daki ToolThreat engellerini (başlangıçta editörle konmuş
/// ya da düşmanın fırlattığı — ayrım yok) tarar, üstlerine 3-2-1 rozeti koyar ve her oyuncu turunda azaltır.
///
/// Engel sistemine dokunmaz: engel sıradan tek-vuruş movable'dır; geri sayım, engelin bindiği TileView'a
/// bağlı tutulur (taş düşünce/karışınca rozet onu izler). Kimlik doğrulaması her kare yapılır: taş havuza
/// dönüp başka hücrede yeniden kullanılsa bile o hücrede ToolThreat yoksa kayıt düşer → rozet başka taşta
/// asla kalmaz. Rozetler taşların üst katmanında (TilesTopOverlayRoot), taşın çocuğu DEĞİL.
///
/// Sayaç 0 olunca tehdit "harcanır": rozet kalkar, engel sıradan engel olarak board'da kalır (ceza bir kez).
/// </summary>
public sealed class BossDuelToolThreat
{
    /// Gri ToolThreat + renkli versiyonları (48-51).
    public static bool IsToolThreat(ObstacleId id)
        => id == ObstacleId.ToolThreat || (id >= ObstacleId.ToolThreatYellow && id <= ObstacleId.ToolThreatGreen);

    private sealed class Entry
    {
        public TileView tile;
        public int remaining;          // > 0 aktif, 0 = harcandı
        public RectTransform badge;
        public TMP_Text label;
        public int shown;              // ekranda yazan sayı (geçiş ortasında yeni sayıya döner)
        public float popStart = -1f;   // sayım geçişi başlangıcı (unscaled); -1 = yok
    }

    // Sayım geçişi: eski sayı büyür (PopPeak katı), tepe noktasında yeni sayıya döner, küçülerek oturur.
    private const float PopDuration = 0.6f;
    private const float PopPeak = 2.2f;

    private readonly BoardController board;
    private readonly List<Entry> entries = new();
    private readonly Sprite badgeSprite;
    private readonly TMP_FontAsset badgeFont;
    private readonly Material badgeMaterial;
    private readonly bool numberOnPlate;
    private readonly Vector2 plateCenter;   // taş içinde normalize (0..1, sol-alt kökenli)
    private readonly Vector2 plateSize;     // taş boyuna oran
    private static readonly Color LastTurnColor = new Color(1f, 0.22f, 0.2f, 1f);

    /// <param name="numberOnPlate">true: sayı engel görselindeki plakanın içine yazılır (arka plan yok);
    /// false: taşın sağ-üst köşesinde badgeSprite'lı ayrı rozet.</param>
    public BossDuelToolThreat(BoardController board, Sprite badgeSprite, TMP_FontAsset badgeFont, Material badgeMaterial,
        bool numberOnPlate = false, Vector2 plateCenter = default, Vector2 plateSize = default)
    {
        this.board = board;
        this.badgeSprite = badgeSprite;
        this.badgeFont = badgeFont;
        this.badgeMaterial = badgeMaterial;
        this.numberOnPlate = numberOnPlate;
        this.plateCenter = plateCenter;
        this.plateSize = plateSize;
    }

    /// <summary>Board'da geri sayımı süren (harcanmamış) tehdit var mı.</summary>
    public int ActiveCount
    {
        get
        {
            int n = 0;
            foreach (var e in entries) if (e.remaining > 0) n++;
            return n;
        }
    }

    /// <summary>Board'u tara: yeni ToolThreat'leri tam geri sayımla kaydet, geçersizleri düşür.</summary>
    public void Sync(int countdown)
    {
        Prune();
        var service = board != null ? board.ObstacleStateService : null;
        if (service == null || board.Tiles == null) return;

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!IsToolThreat(service.GetObstacleIdAt(x, y))) continue;
                var tile = board.Tiles[x, y];
                if (tile == null || Find(tile) != null) continue;
                var entry = new Entry { tile = tile, remaining = Mathf.Max(1, countdown) };
                CreateBadge(entry);
                entries.Add(entry);
            }
        RefreshLabels();
    }

    /// <summary>Bir oyuncu turu geçti: aktif sayaçları azalt. Sıfıra inen varsa true (tehdit harcanır).</summary>
    public bool Tick()
    {
        Prune();
        bool fired = false;
        foreach (var e in entries)
        {
            if (e.remaining <= 0) continue;
            e.remaining--;
            if (e.remaining <= 0)
            {
                fired = true;
                DestroyBadge(e);   // harcandı: engel sıradan engel olarak kalır
            }
            else
            {
                e.popStart = Time.unscaledTime;   // eski sayı büyüsün, tepede yenisine dönsün
            }
        }
        return fired;
    }

    /// <summary>Her kare: rozetleri taşın üstünde tut, kimliği bozulan kayıtları düşür.</summary>
    public void UpdateVisuals()
    {
        Prune();
        var root = board != null ? board.TilesTopOverlayRoot : null;
        if (root == null) return;
        foreach (var e in entries)
        {
            if (e.badge == null) continue;
            var rt = e.tile.RectTransform;
            if (rt == null) continue;
            Rect r = rt.rect;

            if (e.label != null)
            {
                float scale = 1f;
                if (e.popStart >= 0f)
                {
                    // Sayım geçişi: eski sayı büyür → tepede yeni sayı → küçülerek oturur.
                    float k = Mathf.Clamp01((Time.unscaledTime - e.popStart) / PopDuration);
                    if (k < 0.5f)
                    {
                        float u = k / 0.5f;
                        scale = Mathf.Lerp(1f, PopPeak, 1f - (1f - u) * (1f - u));   // hızlı büyü
                    }
                    else
                    {
                        if (e.shown != e.remaining) { e.shown = e.remaining; e.label.text = e.shown.ToString(); }
                        float u = (k - 0.5f) / 0.5f;
                        scale = Mathf.Lerp(PopPeak, 1f, u * u * (3f - 2f * u));      // yumuşak otur
                    }
                    if (k >= 1f) e.popStart = -1f;
                    e.badge.SetAsLastSibling();   // büyürken komşu rozetlerin üstünde
                }
                else if (e.remaining == 1)
                {
                    // Son tur: nabız (oyuncu "şimdi kırmalıyım" desin).
                    scale = 1f + 0.18f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.6f));
                }
                // Kırmızı yalnız ekranda "1" yazarken (geçişte eski sayı beyaz büyür, 1'e dönünce kızarır).
                e.label.color = e.shown == 1 ? LastTurnColor : Color.white;
                e.label.rectTransform.localScale = Vector3.one * scale;
            }

            if (numberOnPlate)
            {
                // Görseldeki plakanın merkezi/boyu (taşın dikdörtgenine göre normalize).
                Vector3 local = new Vector3(Mathf.Lerp(r.xMin, r.xMax, plateCenter.x),
                                            Mathf.Lerp(r.yMin, r.yMax, plateCenter.y), 0f);
                e.badge.localPosition = root.InverseTransformPoint(rt.TransformPoint(local));
                e.badge.sizeDelta = new Vector2(board.TileSize * plateSize.x, board.TileSize * plateSize.y);
            }
            else
            {
                // Taşın sağ-üst köşesi (dünya → overlay yerel).
                Vector3 corner = rt.TransformPoint(new Vector3(r.xMax * 0.62f, r.yMax * 0.62f, 0f));
                e.badge.localPosition = root.InverseTransformPoint(corner);
                float size = board.TileSize * 0.5f;
                e.badge.sizeDelta = new Vector2(size, size);
            }
        }
    }

    public void Clear()
    {
        foreach (var e in entries) DestroyBadge(e);
        entries.Clear();
    }

    // Engel kırıldıysa / taş havuza döndüyse / başka hücreye yeniden kullanıldıysa kaydı düşür.
    private void Prune()
    {
        var service = board != null ? board.ObstacleStateService : null;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            var t = e.tile;
            bool valid = t != null && t && t.gameObject.activeInHierarchy && service != null
                && t.X >= 0 && t.X < board.Width && t.Y >= 0 && t.Y < board.Height
                && board.Tiles[t.X, t.Y] == t
                && IsToolThreat(service.GetObstacleIdAt(t.X, t.Y));
            if (valid) continue;
            DestroyBadge(e);
            entries.RemoveAt(i);
        }
    }

    private Entry Find(TileView tile)
    {
        foreach (var e in entries) if (e.tile == tile) return e;
        return null;
    }

    // Geçişte olmayan etiketleri anlık değere eşitle (yeni kayıtlar vb.).
    private void RefreshLabels()
    {
        foreach (var e in entries)
        {
            if (e.label == null || e.popStart >= 0f) continue;
            e.shown = e.remaining;
            e.label.text = e.shown.ToString();
        }
    }

    private void CreateBadge(Entry e)
    {
        var root = board != null ? board.TilesTopOverlayRoot : null;
        if (root == null) return;

        var go = new GameObject("ToolThreatBadge", typeof(RectTransform), typeof(Image));
        go.layer = root.gameObject.layer;   // Screen Space Camera culling guard
        e.badge = (RectTransform)go.transform;
        e.badge.SetParent(root, false);
        e.badge.anchorMin = e.badge.anchorMax = new Vector2(0.5f, 0.5f);
        e.badge.pivot = new Vector2(0.5f, 0.5f);
        var bg = go.GetComponent<Image>();
        bg.sprite = badgeSprite;
        bg.preserveAspect = true;
        bg.raycastTarget = false;
        if (numberOnPlate) bg.enabled = false;                                    // plaka görselin kendisinde
        else if (badgeSprite == null) bg.color = new Color(0.78f, 0.1f, 0.12f, 1f);

        var textGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.layer = go.layer;
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(e.badge, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var label = textGo.GetComponent<TextMeshProUGUI>();
        if (badgeFont != null) label.font = badgeFont;
        if (badgeMaterial != null && badgeFont != null) label.fontSharedMaterial = badgeMaterial;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 200f;
        label.margin = numberOnPlate ? Vector4.zero : new Vector4(4f, 2f, 4f, 2f);
        label.color = Color.white;
        label.raycastTarget = false;
        e.label = label;
    }

    private static void DestroyBadge(Entry e)
    {
        if (e.badge != null) Object.Destroy(e.badge.gameObject);
        e.badge = null;
        e.label = null;
    }
}
