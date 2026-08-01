using System.Collections.Generic;
using UnityEngine;

/// Grass (bitki örtüsü) katmanının GÖRSEL yöneticisi. Grass davranışı Oil ile aynıdır
/// (ObstacleDef: behavior=CellAnchoredOverlay, blocksCells=0, locksInteraction=1, holdsTile=0):
/// taş altından akar ama o hücrede match/swap KİLİTLİDİR → grass kendi hücresinden hasar almaz,
/// yalnızca KOMŞU match'ten aşınır (obstacle sisteminin adjacent-damage yolu). Bu servis sadece
/// görseli çizer ve hasar/temizlenmeyi ObstacleVisualChanged'dan dinleyip yansıtır (MudOverlayService gibi).
///
///  • İki sprite (A/B) hücre konumuna göre (dama tahtası) atanır; boşsa library def sprite'ı kullanılır.
///  • Her hit'te yapraklar sallanır; hücre kırılınca fade ile kalkar.
public class GrassOverlayService : MonoBehaviour
{
    [Header("Sprites (A/B — opsiyonel checkerboard)")]
    [Tooltip("Sprite A. (x+y) ÇİFT. Boşsa library def sprite'ı kullanılır.")]
    [SerializeField] private Sprite grassSpriteA;
    [Tooltip("Sprite B. (x+y) TEK. Boşsa A'ya, o da boşsa library def sprite'ına düşer.")]
    [SerializeField] private Sprite grassSpriteB;

    [Header("Yerleşim")]
    [Tooltip("Sprite'ı hücreden kaç PİKSEL büyüt (komşularla üst üste binip dikiş/mor boşluk kalmasın). " +
             "Yaprak sanatı kareyi tam doldurmadığı için boşlukları kapatmak birkaç piksel değil ~14-20px " +
             "ister. 0 bırakılırsa güvenli varsayılan (16) kullanılır.")]
    [SerializeField] private float overlapPixels = 15f;   // A sprite: 105px hücre + 15 = 120px

    [Tooltip("B hücreleri A'dan bu kadar PİKSEL daha büyük çizilir + A'nın üstüne biner. Boy+katman farkı " +
             "'kare grid' hissini kırar ve B'ler A'ların boşluğunu örter. 0 = A/B eşit boy.")]
    [SerializeField] private float bExtraPixels = 15f;    // B sprite: A + 15 = 135px

    // Sahne komponentinde alan 0'a düşmüşse (rename) görünür bindirme kalsın diye fallback.
    private float EffectiveOverlap => overlapPixels > 0f ? overlapPixels : 15f;

    [Header("Hit Sallanması")]
    [Tooltip("Bir grass hit alınca KALAN TÜM grass hücreleri topluca sallanır (çalıya vurunca hepsi " +
             "sarsılır gibi). Vuruş hücresine yakın olanlar daha çok, uzaktakiler daha az sallanır.")]
    [SerializeField] private float swayAmplitudeDeg = 12f;
    [SerializeField] private float swayDuration = 0.30f;
    [SerializeField] private float swayCycles = 3f;
    [Tooltip("Uzaklıkla sallanma zayıflaması. 0 = her hücre eşit sallanır; büyük = yalnız yakınlar sallanır.")]
    [SerializeField] private float shakeFalloffPerCell = 0.12f;

    [Header("Temizlenme")]
    [SerializeField] private float clearFadeDuration = 0.22f;

    private BoardController board;
    private int gridWidth, gridHeight, tileSize;

    private sealed class Cell
    {
        public GrassCellView view;
        public int remaining;
        public int maxHits;
    }

    private readonly Dictionary<int, Cell> cellsByIndex = new();

    private Sprite defBaseSprite;
    public void SetBaseSprite(Sprite s) { if (s != null) defBaseSprite = s; }

    /// Hücrenin sprite'ı: service A/B (checkerboard), boşsa library def sprite'ı.
    public Sprite GetSpriteForCell(int x, int y)
    {
        Sprite chosen = ((x + y) & 1) == 0 ? grassSpriteA : grassSpriteB;
        if (chosen == null) chosen = grassSpriteA != null ? grassSpriteA : grassSpriteB;
        if (chosen == null) chosen = defBaseSprite;
        return chosen;
    }

    public void Init(BoardController board, int width, int height, int tileSize)
    {
        this.board = board;
        gridWidth = Mathf.Max(1, width);
        gridHeight = Mathf.Max(1, height);
        if (tileSize > 0) this.tileSize = tileSize;

        if (board != null)
        {
            board.ObstacleVisualChanged -= HandleVisualChanged;
            board.ObstacleVisualChanged += HandleVisualChanged;
        }
    }

    private void OnDestroy()
    {
        if (board != null) board.ObstacleVisualChanged -= HandleVisualChanged;
    }

    public bool HasGrassAt(int x, int y) => cellsByIndex.ContainsKey(CellIndex(x, y));

    public bool TryGetView(int x, int y, out GrassCellView view)
    {
        if (cellsByIndex.TryGetValue(CellIndex(x, y), out var cell) && cell.view != null)
        {
            view = cell.view;
            return true;
        }
        view = null;
        return false;
    }

    public void RegisterCell(int x, int y, GrassCellView view, int remaining, int maxHits)
    {
        if (view == null) return;

        int idx = CellIndex(x, y);
        bool isA = ((x + y) & 1) == 0;

        // B (tek hücreler) A'dan bExtraPixels kadar daha büyük + üstte → dokuma/organik his.
        float expand = isA ? EffectiveOverlap : EffectiveOverlap + Mathf.Max(0f, bExtraPixels);

        view.Init(GetSpriteForCell(x, y), x, y);
        view.PlaceInCell(tileSize, expand);
        view.SetSortingHint();   // doğal shingle (yalnız sağ+alt örtüşür, dört yandan kesilmez)

        cellsByIndex[idx] = new Cell
        {
            view = view,
            remaining = Mathf.Max(1, remaining),
            maxHits = Mathf.Max(1, maxHits)
        };
    }

    // ── Obstacle sisteminden hasar/temizlenme ────────────────────────────────────
    private void HandleVisualChanged(ObstacleVisualChange change)
    {
        if (change.obstacleId != ObstacleId.Grass) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool _found = cellsByIndex.TryGetValue(change.originIndex, out var _c) && _c.view != null;
        Debug.Log($"[GrassVis] origin={change.originIndex} (x={change.originIndex % gridWidth},y={change.originIndex / gridWidth}) " +
                  $"cleared={change.cleared} found={_found} tracked={cellsByIndex.Count} gridW={gridWidth}");
#endif
        if (!cellsByIndex.TryGetValue(change.originIndex, out var cell) || cell.view == null) return;

        int hx = change.originIndex % gridWidth;
        int hy = change.originIndex / gridWidth;

        if (change.cleared)
        {
            cell.view.PlayClear(clearFadeDuration);
            cellsByIndex.Remove(change.originIndex);
        }
        else if (change.remainingHits < cell.remaining)
        {
            cell.remaining = change.remainingHits;
        }

        // Her hitte KALAN tüm grass topluca titrer (vuruş noktasına yakınlıkla azalarak).
        ShakeAll(hx, hy);
    }

    // Vuruş hücresine (hx,hy) yakınlıkla azalarak KALAN tüm grass'ı sallar.
    private void ShakeAll(int hx, int hy)
    {
        foreach (var kv in cellsByIndex)
        {
            var c = kv.Value;
            if (c?.view == null) continue;

            int cx = kv.Key % gridWidth;
            int cy = kv.Key / gridWidth;
            float dx = cx - hx, dy = cy - hy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float amp = swayAmplitudeDeg / (1f + dist * Mathf.Max(0f, shakeFalloffPerCell));

            c.view.PlaySway(amp, swayDuration, swayCycles);
        }
    }

    public void ClearAll()
    {
        foreach (var kv in cellsByIndex)
            if (kv.Value?.view != null) kv.Value.view.HardClear();
        cellsByIndex.Clear();
    }

    private int CellIndex(int x, int y) => y * gridWidth + x;
}
