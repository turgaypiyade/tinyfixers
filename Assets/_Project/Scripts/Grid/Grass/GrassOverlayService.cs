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
    private int lastReconcileFrame = -1;

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

    /// Hücre için toplam overhang (piksel): sprite hücreden bu kadar BÜYÜK çizilir ki komşu
    /// grass'larla üst üste binip grid/board çizgilerini örtsün. A (çift): overlap; B (tek):
    /// overlap + bExtra (B daha büyük, A'nın boşluğunu örter). Standart obstacle image yolu
    /// (GridSpawner.DrawObstacleImage) bu değeri kullanır; overlay registry'si artık yok.
    public float GetOverhangExpandPixels(int x, int y)
    {
        bool isA = ((x + y) & 1) == 0;
        return isA ? EffectiveOverlap : EffectiveOverlap + Mathf.Max(0f, bExtraPixels);
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
            board.OnObstacleDestroyed -= HandleObstacleDestroyed;
            board.OnObstacleDestroyed += HandleObstacleDestroyed;
            // Board her yatıştığında veri-otoriteli orphan süpürücü çalışır → grass verisi HANGİ yolla
            // (LineV/Override/cascade...) silinirse silinsin, event kaçsa bile view ekranda kalmaz.
            board.OnBecameIdle -= HandleBoardIdle;
            board.OnBecameIdle += HandleBoardIdle;
        }
    }

    private void OnDestroy()
    {
        if (board != null)
        {
            board.ObstacleVisualChanged -= HandleVisualChanged;
            board.OnObstacleDestroyed -= HandleObstacleDestroyed;
            board.OnBecameIdle -= HandleBoardIdle;
        }
    }

    // Board yatıştı: verisi gitmiş ama görseli duran grass'ları kesin temizle (event-kaçış güvenlik ağı).
    private void HandleBoardIdle() => ReconcileGrassViewsAgainstData();

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

        view.Init(GetSpriteForCell(x, y), x, y);
        view.SetSortingHint();   // doğal shingle (yalnız sağ+alt örtüşür, dört yandan kesilmez)

        cellsByIndex[idx] = new Cell
        {
            view = view,
            remaining = Mathf.Max(1, remaining),
            maxHits = Mathf.Max(1, maxHits)
        };

        RefreshCellAndNeighbors(x, y);
    }

    // ── Obstacle sisteminden hasar/temizlenme ────────────────────────────────────
    private void HandleVisualChanged(ObstacleVisualChange change)
    {
        if (change.obstacleId != ObstacleId.Grass) return;
        int hx = change.originIndex >= 0 ? change.originIndex % gridWidth : -1;
        int hy = change.originIndex >= 0 ? change.originIndex / gridWidth : -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool _found = cellsByIndex.TryGetValue(change.originIndex, out var _c) && _c.view != null;
        Debug.Log($"[GrassVis] origin={change.originIndex} (x={change.originIndex % gridWidth},y={change.originIndex / gridWidth}) " +
                  $"cleared={change.cleared} found={_found} tracked={cellsByIndex.Count} gridW={gridWidth}");
#endif
        if (!cellsByIndex.TryGetValue(change.originIndex, out var cell) || cell.view == null)
        {
            // Kayıtta yok (registry desync — found=False). Data'yı otorite al: temizlenen hücrede
            // hâlâ duran grass view'ını hem koordinat hem ground-truth taramasıyla yok et.
            if (change.cleared && hx >= 0 && hy >= 0)
            {
                HardClearOrphanViewsAt(hx, hy);
                ReconcileGrassViewsAgainstData();
            }
            return;
        }

        if (change.cleared)
        {
            cell.view.PlayClear(clearFadeDuration);
            cellsByIndex.Remove(change.originIndex);
            RefreshCellAndNeighbors(hx, hy);
            HardClearOrphanViewsAt(hx, hy, cell.view);
            ReconcileGrassViewsAgainstData();
        }
        else if (change.remainingHits < cell.remaining)
        {
            cell.remaining = change.remainingHits;
        }

        // Her hitte KALAN tüm grass topluca titrer (vuruş noktasına yakınlıkla azalarak).
        ShakeAll(hx, hy);
    }

    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId)
    {
        if (obstacleId != ObstacleId.Grass || originIndex < 0) return;

        int x = originIndex % gridWidth;
        int y = originIndex / gridWidth;

        GrassCellView clearingView = null;
        if (cellsByIndex.TryGetValue(originIndex, out var cell) && cell.view != null)
        {
            clearingView = cell.view;
            clearingView.PlayClear(clearFadeDuration);
            cellsByIndex.Remove(originIndex);
        }

        RefreshCellAndNeighbors(x, y);
        HardClearOrphanViewsAt(x, y, clearingView);
        ReconcileGrassViewsAgainstData();
    }

    // Ground-truth orphan süpürücü: obstacle DATA'sında artık grass OLMAYAN bir hücrede grass view'ı
    // duruyorsa (registry/anahtar desync'i yüzünden normal PlayClear yolu kaçırdıysa) onu yok eder.
    // found=False olan clear'larda tek güvenilir yol data'yı otorite almak. Frame başına 1 kez tarar
    // (aynı hamlede onlarca grass temizlenince tekrar tekrar taramasın).
    private void ReconcileGrassViewsAgainstData()
    {
        if (board == null || board.ObstacleStateService == null) return;
        if (lastReconcileFrame == Time.frameCount) return;
        lastReconcileFrame = Time.frameCount;

        var views = FindObjectsByType<GrassCellView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < views.Length; i++)
        {
            var v = views[i];
            if (v == null || v.IsClearing) continue;

            int vx = v.GridX, vy = v.GridY;
            if (vx < 0 || vx >= gridWidth || vy < 0 || vy >= gridHeight) continue;

            // Data'da hâlâ grass varsa dokunma; yalnız verisi gitmiş orphan view'ı temizle.
            if (board.ObstacleStateService.IsGrassAt(vx, vy)) continue;

            int idx = CellIndex(vx, vy);
            if (cellsByIndex.TryGetValue(idx, out var c) && c != null && c.view == v)
                cellsByIndex.Remove(idx);

            // Normal temizlik gibi fade ile kalksın (PlayClear kendi Destroy'unu zamanlar).
            v.PlayClear(clearFadeDuration);
        }
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

    private void HardClearOrphanViewsAt(int x, int y, GrassCellView except = null)
    {
        var views = FindObjectsByType<GrassCellView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < views.Length; i++)
        {
            var view = views[i];
            if (view == null || view == except) continue;
            if (view.IsClearing) continue;
            if (view.GridX != x || view.GridY != y) continue;

            view.HardClear();
            Destroy(view.gameObject);
        }
    }

    private void RefreshCellAndNeighbors(int x, int y)
    {
        RefreshCellLayout(x, y);
        RefreshCellLayout(x - 1, y);
        RefreshCellLayout(x + 1, y);
        RefreshCellLayout(x, y - 1);
        RefreshCellLayout(x, y + 1);
    }

    private void RefreshCellLayout(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            return;

        int idx = CellIndex(x, y);
        if (!cellsByIndex.TryGetValue(idx, out var cell) || cell?.view == null)
            return;

        bool isA = ((x + y) & 1) == 0;
        // B hücreleri büyük kalır; ama taşma yalnız canlı grass komşusuna doğru verilir.
        float expand = isA ? EffectiveOverlap : EffectiveOverlap + Mathf.Max(0f, bExtraPixels);
        float side = expand * 0.5f;

        float left = HasLiveCellAt(x - 1, y) ? side : 0f;
        float right = HasLiveCellAt(x + 1, y) ? side : 0f;
        float top = HasLiveCellAt(x, y - 1) ? side : 0f;
        float bottom = HasLiveCellAt(x, y + 1) ? side : 0f;

        cell.view.PlaceInCell(tileSize, left, right, top, bottom);
    }

    private bool HasLiveCellAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            return false;

        return cellsByIndex.TryGetValue(CellIndex(x, y), out var cell)
               && cell?.view != null
               && !cell.view.IsClearing;
    }

    private int CellIndex(int x, int y) => y * gridWidth + x;
}
