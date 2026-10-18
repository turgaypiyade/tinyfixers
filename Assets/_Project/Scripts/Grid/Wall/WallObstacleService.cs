using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Duvar türünün kuralları + görsel ayarları. Yeni bir duvar türü = yeni bir WallKind (+ ObstacleId +
/// pişmiş sprite öneki); servis/görünüm/editör fırçası ortaktır.
/// </summary>
public sealed class WallKind
{
    public ObstacleId Id;
    public string SpritePrefix;            // Resources/Wall/<önek>Q_*, <önek>Concave_*, <önek>Detail_*
    public string[] StageDetails;          // aşama → detay sprite'ı (0 = rastgele kabartma, null)
    public int CollapseStage;              // bu aşamadaki hücreye gelen vuruş parçayı yıkar
    public bool ResetUnhitOnMoveEnd;       // hamle bitince o hamlede vurulmayan hücre aşama 0'a döner
    public bool PlainBase;                 // aşama 0'da kabartma YOK (düz yüzey); detay yalnız çatlak aşamalarında
    public bool DetailFullCell;            // detay sprite'ı tüm hücreye yayılır (kendi çiziminde ortalanmış, iç-alan ölçeği yok)
    public bool StepBackUnhit;             // ResetUnhitOnMoveEnd: vurulmayan hücre başa değil BİR aşama geri döner
    public System.Collections.Generic.Dictionary<string, UnityEngine.Vector2[]> FuseTips;   // türe özel fitil uçları (uv, sol-üst)

    /// Kiremit duvar (bej): 1 çatlak1 → 2 çatlak2 (alev) → 3 çatlak3 (alev) → 4 çatlak4 (alev) → 5. vuruş yıkım.
    public static readonly WallKind Brick = new WallKind
    {
        Id = ObstacleId.Wall,
        SpritePrefix = "Wall",
        StageDetails = new[] { null, "Crack1", "Crack2", "Crack3", "Crack4" },
        CollapseStage = 4,
        ResetUnhitOnMoveEnd = false,
    };

    /// Metal duvar (gri): 1 çatlak1 → 2 çatlak3 (alev) → 3. vuruş yıkım; vuruşlar ardışık olmalı.
    public static readonly WallKind Metal = new WallKind
    {
        Id = ObstacleId.MetalWall,
        SpritePrefix = "MetalWall",
        // Kullanıcı kararı 2026-10-05: düz başlar; 1. vuruş çatlak2 → 2. çatlak3 → 3. çatlak4 → 4. vuruş yıkım.
        // Ardışık kural: hamlede vurulmayan hücre BİR aşama geri gelir. Çizimler (Resources/Wall/MetalWallDetail_*)
        // düz metalin üstüne tam hücre oturur; renkleri son hali (yeniden boyanmaz).
        StageDetails = new[] { null, "Crack2", "Crack3", "Crack4" },
        CollapseStage = 3,
        ResetUnhitOnMoveEnd = true,
        StepBackUnhit = true,
        PlainBase = true,
        DetailFullCell = true,
        FuseTips = new System.Collections.Generic.Dictionary<string, UnityEngine.Vector2[]>
        {
            ["Crack2"] = new[] { new UnityEngine.Vector2(402f / 734f, 272f / 736f) },
            ["Crack3"] = new[] { new UnityEngine.Vector2(430f / 734f, 237f / 736f), new UnityEngine.Vector2(517f / 734f, 376f / 736f) },
            ["Crack4"] = new[] { new UnityEngine.Vector2(462f / 734f, 220f / 736f), new UnityEngine.Vector2(551f / 734f, 287f / 736f),
                                 new UnityEngine.Vector2(577f / 734f, 408f / 736f) },
        },
    };

    public static WallKind For(ObstacleId id) =>
        id == ObstacleId.Wall ? Brick : id == ObstacleId.MetalWall ? Metal : null;
}

/// <summary>
/// Duvar engelleri (ObstacleId.Wall, ObstacleId.MetalWall) — hücre aşamaları + parça yaşam döngüsü + görseller.
///
/// Veri modeli: bir duvar PARÇASI = tek origin'li çok-hücreli engel (editör numaralı fırçayla kurar;
/// şekil serbest). Hedef parça başına sayılır (ObstacleStateService parçayı tek seferde temizler).
/// Her HÜCRE kendi aşamasını taşır; son aşamadaki hücre bir vuruş daha alırsa parçanın tamamı yıkılır
/// (aşama dizisi türe göre: <see cref="WallKind"/>). Metal duvarda hamle bitince o hamlede vurulmayan
/// hücre başa döner.
///
/// Board'a runtime'da eklenir (sahne kurulumu yok): GridSpawner level çizerken <see cref="Build"/> çağırır.
/// </summary>
public sealed class WallObstacleService : MonoBehaviour
{
    private BoardController board;
    private ObstacleStateService boundState;
    private RectTransform root;
    private int width;
    private float tileSize;

    private readonly Dictionary<int, int> stageByCell = new();
    private readonly Dictionary<int, WallPieceView> pieces = new();      // origin → görünüm
    private readonly Dictionary<int, List<int>> cellsByOrigin = new();
    private readonly Dictionary<int, WallKind> kindByOrigin = new();
    private readonly Dictionary<int, int> collapseCellByOrigin = new();  // yıkımı tetikleyen hücre
    private readonly HashSet<int> hitSinceMoveEnd = new();               // son hamle sonundan beri vurulan hücreler
    private readonly List<(ObstacleId id, int origin)> layerBuffer = new();
    private int cellCount;
    private System.Func<int, int, ObstacleHitContext, WallHitOutcome> hitHandler;

    public static WallObstacleService Ensure(BoardController board)
    {
        if (board == null) return null;
        if (!board.TryGetComponent<WallObstacleService>(out var service))
        {
            service = board.gameObject.AddComponent<WallObstacleService>();
            board.OnPlayerMoveResolved += service.HandleMoveResolved;
            board.OnObstacleViewRestored += service.HandleViewRestored;
        }
        service.board = board;
        return service;
    }

    /// Level çizilirken çağrılır: tüm parçaları kaydeder, aşamaları sıfırlar, görselleri kurar.
    public void Build(LevelData level, RectTransform parent, float cellSize)
    {
        Clear();
        if (board == null || level == null || level.obstacles == null || level.obstacleOrigins == null || parent == null)
            return;

        root = parent;
        width = level.width;
        tileSize = cellSize;
        cellCount = level.obstacles.Length;
        Bind(board.ObstacleStateService);

        for (int i = 0; i < level.obstacles.Length; i++)
        {
            // Saydam örtünün (Grass/GrassFlower/Oil) altındaki duvar hücreleri de baştan kaydedilip çizilir:
            // örtü saydam olduğu için duvar görünür; örtü kalkınca altta boşluk kalmasın.
            if (!TryGetVisibleWallLayer(level, i, out var kind, out int origin)) continue;
            if (!cellsByOrigin.TryGetValue(origin, out var list))
            {
                cellsByOrigin[origin] = list = new List<int>();
                kindByOrigin[origin] = kind;
            }
            list.Add(i);
            stageByCell[i] = 0;
        }

        foreach (var kv in cellsByOrigin)
        {
            var kind = kindByOrigin[kv.Key];
            pieces[kv.Key] = WallPieceView.Create(root, kv.Key, kv.Value, width, tileSize, kind);
            boundState?.SetWallRemainingHits(kv.Key, kind.Id, kind.CollapseStage + 1);
        }
    }

    public void Clear()
    {
        foreach (var view in pieces.Values)
            if (view != null) Destroy(view.gameObject);
        pieces.Clear();
        cellsByOrigin.Clear();
        kindByOrigin.Clear();
        stageByCell.Clear();
        collapseCellByOrigin.Clear();
        hitSinceMoveEnd.Clear();
    }

    public int GetStageAt(int cellIndex) => stageByCell.TryGetValue(cellIndex, out int s) ? s : -1;

    /// Engel ipucu vurgusu için: verilen türdeki canlı parça görünümlerinin kökleri.
    public void CollectViewRoots(ObstacleId id, List<Transform> result)
    {
        foreach (var kv in pieces)
            if (kv.Value != null && kindByOrigin.TryGetValue(kv.Key, out var kind) && kind.Id == id)
                result.Add(kv.Value.transform);
    }

    private void OnDestroy()
    {
        Bind(null);
        if (board != null)
        {
            board.OnPlayerMoveResolved -= HandleMoveResolved;
            board.OnObstacleViewRestored -= HandleViewRestored;
        }
    }

    // Hücrenin görünür duvar katmanı: en üst katman duvar ya da duvarın üstündeki her katman saydam
    // örtüyse o duvar. Opak bir katmanın altındaki duvar henüz çizilmez (açığa çıkınca çizilir).
    private bool TryGetVisibleWallLayer(LevelData level, int cell, out WallKind kind, out int origin)
    {
        kind = null;
        origin = -1;
        if (boundState == null)
        {
            kind = WallKind.For((ObstacleId)level.obstacles[cell]);
            origin = level.obstacleOrigins[cell];
            return kind != null && origin >= 0;
        }

        boundState.GetLayersTopDown(cell, layerBuffer);
        foreach (var (id, layerOrigin) in layerBuffer)
        {
            var k = WallKind.For(id);
            if (k != null)
            {
                kind = k;
                origin = layerOrigin;
                return origin >= 0;
            }
            if (!ObstacleStateService.IsSeeThroughLayer(id)) return false;
        }
        return false;
    }

    // Örtüsü kalkan duvar: hücre aşamalarını koruyup kapasiteyi yenile. Opak örtünün
    // altından çıkan yeni hücreleri de parçanın görünümüne ekle.
    private void HandleViewRestored(int x, int y)
    {
        if (boundState == null || root == null || width <= 0) return;
        int origin = y * width + x;
        var level = board != null ? board.LevelData : null;
        if (level == null || level.obstacles == null || level.obstacleOrigins == null) return;

        WallKind pieceKind = null;
        var cells = new List<int>();
        for (int i = 0; i < Mathf.Min(cellCount, level.obstacles.Length); i++)
        {
            if (!TryGetVisibleWallLayer(level, i, out var kind, out int o) || o != origin) continue;
            pieceKind ??= kind;
            cells.Add(i);
        }
        if (pieceKind == null || cells.Count == 0) return;

        // An opaque cover may reveal more cells of an already active piece. Rebuild
        // its footprint without resetting cracks on cells that were already exposed.
        if (cellsByOrigin.TryGetValue(origin, out var previousCells)
            && previousCells.Count == cells.Count && previousCells.TrueForAll(cells.Contains))
        {
            RefreshRemaining(origin);
            return;
        }
        if (pieces.TryGetValue(origin, out var previousView) && previousView != null)
            Destroy(previousView.gameObject);

        cellsByOrigin[origin] = cells;
        kindByOrigin[origin] = pieceKind;
        foreach (int c in cells)
            if (!stageByCell.ContainsKey(c)) stageByCell[c] = 0;
        pieces[origin] = WallPieceView.Create(root, origin, cells, width, tileSize, pieceKind);
        foreach (int c in cells)
            if (stageByCell[c] > 0) pieces[origin].SetCellStage(c, stageByCell[c]);
        RefreshRemaining(origin);
    }

    private void Bind(ObstacleStateService state)
    {
        if (boundState == state) return;
        if (boundState != null)
        {
            if (boundState.WallHitInterceptor == hitHandler)
                boundState.WallHitInterceptor = null;
            boundState.OnObstacleDestroyed -= HandleObstacleDestroyed;
        }
        boundState = state;
        if (boundState != null)
        {
            hitHandler ??= HandleHit;
            boundState.WallHitInterceptor = hitHandler;
            boundState.OnObstacleDestroyed += HandleObstacleDestroyed;
        }
    }

    // ObstacleStateService bir duvar hücresi vurulunca çağırır.
    private WallHitOutcome HandleHit(int origin, int cellIndex, ObstacleHitContext context)
    {
        if (!cellsByOrigin.TryGetValue(origin, out var cells) || !kindByOrigin.TryGetValue(origin, out var kind))
            return WallHitOutcome.Ignored;

        hitSinceMoveEnd.Add(cellIndex);

        int stage = stageByCell.TryGetValue(cellIndex, out int s) ? s : 0;
        if (stage >= kind.CollapseStage)
        {
            collapseCellByOrigin[origin] = cellIndex;
            return WallHitOutcome.Collapse;   // temizlik ObstacleStateService'te → HandleObstacleDestroyed
        }

        SetStage(origin, cellIndex, stage + 1);
        return WallHitOutcome.Advanced;
    }

    private void SetStage(int origin, int cellIndex, int stage)
    {
        stageByCell[cellIndex] = stage;
        if (pieces.TryGetValue(origin, out var view) && view != null)
            view.SetCellStage(cellIndex, stage);
        RefreshRemaining(origin);
    }

    // Parçanın "anlamlı kalan vuruşu" = en ilerlemiş hücrenin yıkıma kalan vuruşu (PatchBot kapasitesi).
    private void RefreshRemaining(int origin)
    {
        if (!cellsByOrigin.TryGetValue(origin, out var cells) || !kindByOrigin.TryGetValue(origin, out var kind))
            return;
        int maxStage = 0;
        foreach (int c in cells)
            if (stageByCell.TryGetValue(c, out int cs) && cs > maxStage) maxStage = cs;
        boundState?.SetWallRemainingHits(origin, kind.Id, kind.CollapseStage + 1 - maxStage);
    }

    // Oyuncu hamlesi (zincirleri dahil) bitti, board durdu: ardışık vuruş isteyen türlerde (metal) bu
    // hamlede vurulmayan ilerlemiş hücreler başa döner — delik animasyonla kapanır.
    private void HandleMoveResolved()
    {
        foreach (var kv in cellsByOrigin)
        {
            if (!kindByOrigin.TryGetValue(kv.Key, out var kind) || !kind.ResetUnhitOnMoveEnd) continue;
            foreach (int c in kv.Value)
            {
                if (hitSinceMoveEnd.Contains(c)) continue;
                if (stageByCell.TryGetValue(c, out int st) && st > 0)
                    SetStage(kv.Key, c, kind.StepBackUnhit ? st - 1 : 0);
            }
        }
        hitSinceMoveEnd.Clear();
    }

    // Patlama sesi (breakSound) parça temizlenirken BoardBreakFxService'ten çalar; yığılma sesi
    // (followUpBreakSound) görünüm çöküşe geçtiği an buradan.
    private void PlayCollapseSound(ObstacleId id)
    {
        if (board == null || !GameSettings.SoundEnabled) return;
        var def = board.LevelData?.obstacleLibrary?.Get(id);
        if (def == null || def.followUpBreakSound == null) return;
        board.SfxSource?.PlayOneShot(def.followUpBreakSound, def.followUpBreakSoundVolume);
    }

    private void HandleObstacleDestroyed(int origin, ObstacleId id)
    {
        if (WallKind.For(id) == null) return;
        if (!cellsByOrigin.TryGetValue(origin, out var cells)) return;

        foreach (int c in cells)
        {
            stageByCell.Remove(c);
            hitSinceMoveEnd.Remove(c);
        }
        cellsByOrigin.Remove(origin);
        kindByOrigin.Remove(origin);
        int trigger = collapseCellByOrigin.TryGetValue(origin, out int t) ? t : origin;
        collapseCellByOrigin.Remove(origin);

        if (pieces.TryGetValue(origin, out var view) && view != null)
        {
            // Her hücre kendi patlama anında açılır; level-end son hücrenin patlamasını
            // bekler. Erken iptal kalan hücreleri ve işi tek seferde bırakır.
            var held = new List<Vector2Int>(cells.Count);
            foreach (int c in cells) held.Add(new Vector2Int(c % width, c / width));
            var hold = board != null ? board.HoldCells(held) : null;
            var job = board != null ? board.BeginJob(BoardController.BoardJobKind.ObstacleCollapse) : null;
            int collapseWidth = width; // level yeniden kurulsa da eski hold'un koordinatları değişmesin

            view.PlayCollapse(trigger,
                onBlast: (cell, isTrigger) => OnBlast(id, isTrigger),
                onCollapseStart: () => PlayCollapseSound(id),
                onFinished: () => { hold?.Dispose(); job?.Dispose(); },
                onCellVacated: cell => hold?.Release(new Vector2Int(cell % collapseWidth, cell / collapseWidth)));
        }
        pieces.Remove(origin);
    }

    // Her patlamada board kısa sarsılır; tetik patlamasının sesi parça temizlenirken zaten çaldı
    // (breakSound) → sonraki hücrelerde aynı patlama sesi biraz kısık ("pat pat").
    private void OnBlast(ObstacleId id, bool isTrigger)
    {
        if (board == null) return;
        var animator = board.boardAnimatorRef;
        if (animator != null)
            board.StartCoroutine(animator.MicroShake(isTrigger ? 0.18f : 0.1f, isTrigger ? 10f : 5f));

        if (isTrigger || !GameSettings.SoundEnabled) return;
        var def = board.LevelData?.obstacleLibrary?.Get(id);
        if (def != null && def.breakSound != null)
            board.SfxSource?.PlayOneShot(def.breakSound, def.breakSoundVolume * 0.6f);
    }
}
