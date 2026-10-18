using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wonder Boya Kovası (5. joker, BoosterMode.PaintBucket). Kurallar: Docs/Joker_PaintBucket_Plan.md.
///   • Oyuncu normal bir taşa dokunur → o TÜRÜN tahtadaki bütün normal taşları kalan türlerden rastgele birine döner
///     (special, engel altındaki/kilitli ve hareketli engel taşları dönmez). Dönüşüm, dokunulan taştan her hedefe akan
///     gökkuşağı kuşağıyla (PaintRibbonFx) olur; oluşan eşleşmeler hemen patlar.
///   • Sonraki 3 oyuncu hamlesi boyunca o tür yeniden üretilmez (BoardController.RandomPool süzülür → dolum, mıknatıs,
///     karıştırma hepsi aynı kaynaktan). 3. hamle çözülünce tür geri gelir; dönüşmüş taşlar geri dönmez.
///   • Kullanılamaz (hak düşmez): türü level hedefi / brandalı heykel / mühür istiyorsa, tahtada 2'den az tür kalacaksa,
///     ambargo zaten sürüyorsa, dokunulan taş special ya da kilitliyse.
/// Joker hamle tüketmez (diğer jokerler gibi).
/// </summary>
public sealed class PaintBucketJoker : MonoBehaviour
{
    public const int EmbargoMoves = 3;
    // Kuşaklar yakından uzağa SIRAYLA çıkar (kullanıcı: "aralarında kısa bir süre"); çok taşlı tahtada toplam süre sınırlı.
    private const float LaunchInterval = 0.07f;
    private const float MaxLaunchSpan = 0.9f;

    // Reddetme sebepleri → yerelleştirme anahtarları (odak ekranının açıklama satırında gösterilir).
    public const string RejectGoal = "joker_paint_reject_goal";
    public const string RejectMinColors = "joker_paint_reject_min";
    public const string RejectActive = "joker_paint_reject_active";
    public const string RejectTile = "joker_paint_reject_tile";

    // Sesler (Resources/Audio/Jokers/): dökülme bir kez, şapırtı her varışta (seyreltilmiş). Dosya yoksa sessiz.
    private const string PourSfx = "PaintPour";
    private const string SplatSfx = "PaintSplat";
    private const float SplatMinInterval = 0.05f;
    private const float SplatVolume = 0.55f;
    private static AudioClip pourClip, splatClip;
    private static bool sfxLoaded;
    private float lastSplatTime = -1f;

    private BoardController board;
    private TileType[] filteredPool;
    private TileType? embargoType;
    private int movesStarted;
    private readonly List<TileView> targets = new();

    /// Ambargo sürerken kalan hamle (rozet bunu gösterir); 0 = ambargo yok ya da son hamle çözülüyor.
    public int RemainingMoves => embargoType.HasValue ? Mathf.Max(0, EmbargoMoves - movesStarted) : 0;
    public TileType? EmbargoType => embargoType;
    public event Action OnChanged;

    public static PaintBucketJoker Ensure(BoardController board)
    {
        if (board == null) return null;
        if (!board.TryGetComponent<PaintBucketJoker>(out var joker))
            joker = board.gameObject.AddComponent<PaintBucketJoker>();
        joker.Bind(board);
        return joker;
    }

    private void Bind(BoardController b)
    {
        if (board == b) return;
        Unbind();
        board = b;
        board.OnPlayerMoveConsumed += HandleMoveConsumed;
        board.OnPlayerMoveResolved += HandleMoveResolved;
    }

    private void Unbind()
    {
        if (board == null) return;
        board.OnPlayerMoveConsumed -= HandleMoveConsumed;
        board.OnPlayerMoveResolved -= HandleMoveResolved;
        board = null;
    }

    private void OnDestroy()
    {
        Unbind();
        PaintRibbonFx.FinishAll();
        DestroyChip();
    }

    /// Level kurulumu (BoardController.Init): önceki levelın ambargosu taşınmaz.
    public void ResetForLevel()
    {
        PaintRibbonFx.FinishAll();
        embargoType = null;
        filteredPool = null;
        movesStarted = 0;
        RefreshChip();
        OnChanged?.Invoke();
    }

    /// Ambargo sürerken süzülmüş havuz, yoksa null (BoardController.RandomPool bunu kullanır).
    public TileType[] FilteredPoolOrNull => embargoType.HasValue ? filteredPool : null;

    // ── Doğrulama ────────────────────────────────────────────────

    public bool TryValidate(TileView tile, out string rejectKey)
    {
        rejectKey = null;
        if (board == null || tile == null) { rejectKey = RejectTile; return false; }
        if (embargoType.HasValue) { rejectKey = RejectActive; return false; }
        if (!IsConvertible(tile.X, tile.Y, tile)) { rejectKey = RejectTile; return false; }

        var type = tile.GetTileType();
        var pool = board.RandomPool;
        if (pool == null || Array.IndexOf(pool, type) < 0) { rejectKey = RejectTile; return false; }
        if (DistinctCount(pool) - 1 < 2) { rejectKey = RejectMinColors; return false; }
        if (IsColorNeeded(type)) { rejectKey = RejectGoal; return false; }
        return true;
    }

    /// Bu levelda joker hiç kullanılabilir mi (en az 3 tür)? Slot görünümü için.
    public bool LevelAllowsUse()
    {
        var pool = board != null ? board.RandomPool : null;
        return pool != null && DistinctCount(pool) >= 3;
    }

    private bool IsConvertible(int x, int y, TileView tile)
    {
        if (tile == null || !tile || tile.GetSpecial() != TileSpecial.None) return false;
        if (board.Holes[x, y]) return false;
        var obstacles = board.ObstacleStateService;
        if (obstacles == null) return true;
        return !obstacles.IsCellBlocked(x, y) && !obstacles.IsInteractionLockedAt(x, y) && !obstacles.IsMovableObstacleAt(x, y);
    }

    // Renk bir görevin parçasıysa çıkarılamaz: level hedefi (taş toplama), brandalı heykel etiketi, mühür sayacı.
    private bool IsColorNeeded(TileType type)
    {
        var hud = FindFirstObjectByType<TopHudController>();
        if (hud != null && hud.HasGoalForTile(type)) return true;
        if (board.TryGetComponent<TarpStatueService>(out var tarp) && tarp.NeedsColor(type)) return true;
        if (board.TryGetComponent<AncientSealService>(out var seal) && seal.NeedsColor(type)) return true;
        return false;
    }

    private static int DistinctCount(TileType[] pool)
    {
        int mask = 0, n = 0;
        foreach (var t in pool)
        {
            int bit = 1 << ((int)t & 31);
            if ((mask & bit) != 0) continue;
            mask |= bit; n++;
        }
        return n;
    }

    // ── Uygulama ─────────────────────────────────────────────────

    /// Doğrulanmış dokunuş: ambargoyu başlatır, dönüşümü oynatır, tahtayı çözer. Hak düşümü çağıranda.
    public IEnumerator Apply(TileView source)
    {
        var type = source.GetTileType();
        var basePool = board.RandomPool;

        // Ambargo HEMEN başlar: dönüşümden doğan eşleşmelerin dolumu da bu türü getirmez.
        var rest = new List<TileType>(basePool.Length);
        foreach (var t in basePool) if (t != type && !rest.Contains(t)) rest.Add(t);
        filteredPool = rest.ToArray();
        embargoType = type;
        movesStarted = 0;
        OnChanged?.Invoke();

        board.BeginBusy();
        try
        {
            PlaySfx(Pour());
            CollectTargets(type);
            var root = board.TilesTopOverlayRoot;
            Vector2 from = CellCenter(root, source);
            float cell = board.TileSize;
            int pending = targets.Count;

            // Yakından uzağa sıralı; aralık taş sayısına göre kısalır (toplam ≤ MaxLaunchSpan).
            targets.Sort((a, b) => Vector2.SqrMagnitude(CellCenter(root, a) - from)
                .CompareTo(Vector2.SqrMagnitude(CellCenter(root, b) - from)));
            float interval = targets.Count > 1 ? Mathf.Min(LaunchInterval, MaxLaunchSpan / (targets.Count - 1)) : 0f;
            Color fromColor = PaintColor(type);

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var newType = filteredPool[UnityEngine.Random.Range(0, filteredPool.Length)];
                Vector2 to = CellCenter(root, target);
                float delay = i * interval;
                PaintRibbonFx.Launch(board, root, from, to, cell, delay, fromColor, PaintColor(newType), () =>
                {
                    pending--;
                    if (target == null || !target || target.GetTileType() != type || target.GetSpecial() != TileSpecial.None) return;
                    target.SetType(newType);
                    board.SyncTileData(target.X, target.Y);
                    board.RefreshTileObstacleVisual(target);
                    target.PlayBeingLandedOnSquash(0.2f, 0.35f);
                    if (Time.time - lastSplatTime >= SplatMinInterval)
                    {
                        lastSplatTime = Time.time;
                        PlaySfx(Splat(), SplatVolume);
                    }
                });
            }

            float guard = Time.time + 4f;   // görsel kök kaybolursa bile tahta kilitli kalmasın
            while (pending > 0 && Time.time < guard)
                yield return null;

            yield return board.ResolveBoardPublic();
        }
        finally
        {
            targets.Clear();
            board.EndBusy();
            RefreshChip();
        }
    }

    private static AudioClip Pour() { LoadSfx(); return pourClip; }
    private static AudioClip Splat() { LoadSfx(); return splatClip; }

    private static void LoadSfx()
    {
        if (sfxLoaded) return;
        sfxLoaded = true;
        pourClip = Resources.Load<AudioClip>("Audio/Jokers/" + PourSfx);
        splatClip = Resources.Load<AudioClip>("Audio/Jokers/" + SplatSfx);
    }

    private void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null && board != null && board.Audio != null) board.Audio.PlayOneShotClip(clip, volume);
    }

    private void CollectTargets(TileType type)
    {
        targets.Clear();
        var tiles = board.Tiles;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var t = tiles[x, y];
                if (t != null && t && t.GetTileType() == type && IsConvertible(x, y, t))
                    targets.Add(t);
            }
    }

    // Taş renkleri (sprite ölçümüne yakın, parlak): kuşak kaynak renginden hedef rengine akar.
    private static Color PaintColor(TileType type) => type switch
    {
        TileType.Gear  => new Color(1.00f, 0.80f, 0.16f),
        TileType.Core  => new Color(0.93f, 0.20f, 0.18f),
        TileType.Bolt  => new Color(0.16f, 0.45f, 1.00f),
        TileType.Plate => new Color(0.24f, 0.78f, 0.30f),
        _ => Color.white,
    };

    private static Vector2 CellCenter(RectTransform root, TileView tile)
    {
        var rt = (RectTransform)tile.transform;
        return root.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
    }

    // ── Hamle sayımı ─────────────────────────────────────────────

    private void HandleMoveConsumed()
    {
        if (!embargoType.HasValue) return;
        movesStarted++;
        RefreshChip();
        OnChanged?.Invoke();
    }

    // 3. hamlenin tahtası durunca tür geri gelir (dynamic input: çakışan hamleler tek pencerede çözülür).
    private void HandleMoveResolved()
    {
        if (!embargoType.HasValue || movesStarted < EmbargoMoves) return;
        embargoType = null;
        filteredPool = null;
        movesStarted = 0;
        RefreshChip();
        OnChanged?.Invoke();
    }

    // ── Rozet: joker slotunun üstünde soluk taş ikonu + kalan hamle ──

    private RectTransform chip;
    private Image chipIcon;
    private TMP_Text chipCount;

    private void RefreshChip()
    {
        int remaining = RemainingMoves;
        if (!embargoType.HasValue || remaining <= 0)
        {
            if (chip != null) chip.gameObject.SetActive(false);
            return;
        }
        if (chip == null && !CreateChip()) return;
        chip.gameObject.SetActive(true);
        chipIcon.sprite = board.GetIcon(embargoType.Value);
        chipCount.text = remaining.ToString();
    }

    private bool CreateChip()
    {
        BoosterSlotView slot = null;
        foreach (var v in FindObjectsByType<BoosterSlotView>(FindObjectsSortMode.None))
            if (v.ResolvedIndex == BoosterAccessService.PaintBucketIndex) { slot = v; break; }
        if (slot == null) return false;

        var go = new GameObject("PaintEmbargoChip", typeof(RectTransform));
        go.layer = slot.gameObject.layer;
        chip = (RectTransform)go.transform;
        chip.SetParent(slot.transform, false);
        chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 1f);
        chip.pivot = new Vector2(0.5f, 0f);
        chip.sizeDelta = new Vector2(84f, 84f);
        chip.anchoredPosition = new Vector2(0f, 6f);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.layer = go.layer;
        chipIcon = iconGo.GetComponent<Image>();
        chipIcon.raycastTarget = false;
        chipIcon.preserveAspect = true;
        chipIcon.color = new Color(1f, 1f, 1f, 0.55f);   // soluk: "bu renk şu an yok"
        var irt = chipIcon.rectTransform;
        irt.SetParent(chip, false);
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = irt.offsetMax = Vector2.zero;

        var textGo = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.layer = go.layer;
        chipCount = textGo.GetComponent<TextMeshProUGUI>();
        var font = slot.GetComponentInChildren<TMP_Text>(true);
        if (font != null) { chipCount.font = font.font; chipCount.fontSharedMaterial = font.fontSharedMaterial; }
        chipCount.fontSize = 46f;
        chipCount.alignment = TextAlignmentOptions.Center;
        chipCount.raycastTarget = false;
        chipCount.color = Color.white;
        var trt = chipCount.rectTransform;
        trt.SetParent(chip, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return true;
    }

    private void DestroyChip()
    {
        if (chip != null) Destroy(chip.gameObject);
        chip = null; chipIcon = null; chipCount = null;
    }
}
