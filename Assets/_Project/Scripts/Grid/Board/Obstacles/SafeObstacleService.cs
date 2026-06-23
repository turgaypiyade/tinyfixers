using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ObstacleId.Safe için kilit state-machine'i (izole mantık).
///
/// Her kasa (origin hücresine göre) 3 SIRALI kilide sahiptir: kırmızı → sarı → yeşil.
/// Her kilidin kendi hit sayısı vardır. Her vuruş AKTİF kilidin kalan hit'ini düşürür;
/// 0'a inince kilit kapanır (knob en alta) ve sıradaki kilit aktif olur; 3'ü de kapanınca
/// kasa kırılır (altındaki içerik reveal edilir).
///
/// Bu sınıf SADECE mantık + event'lerdir — tile/gravity/match'e DOKUNMAZ. Board (damage flow)
/// ProcessHit'i çağırır; view event'leri dinleyip knob/sayaç/animasyonu yönetir. Stamping,
/// freeze ve reveal ayrı entegrasyon adımlarında bağlanır.
/// </summary>
public sealed class SafeObstacleService : MonoBehaviour
{
    public const int LockCount = 3;   // 0=kırmızı, 1=sarı, 2=yeşil

    private sealed class SafeState
    {
        public readonly int[] remaining = new int[LockCount];
        public readonly int[] total     = new int[LockCount];
        public readonly SafeLockColor[] order = new SafeLockColor[LockCount];
        public SafeLockHitMode hitMode;
        public int activeOrderStep;     // Ordered mod için 0..2; LockCount(3) = kırık
    }

    [Header("References")]
    [SerializeField] private BoardController board;

    /// Kasa altında saklanan içerik (per-cell). Model (a): stamp ederken kaydedilir, kırılınca restore.
    public struct BeneathCell
    {
        public int cell;          // flat hücre index
        public ObstacleId id;     // altındaki obstacle (None = boş/normal)
        public int origin;        // altındaki obstacle'ın origin'i (single-cell ise == cell)
    }

    private readonly Dictionary<int, SafeState> _byOrigin = new();
    private readonly Dictionary<int, List<BeneathCell>> _beneathByOrigin = new();
    private readonly Dictionary<int, int> _lastHitFrameByOrigin = new();
    private Func<int, ObstacleHitContext, TileType?, bool> _hitHandler;

    // (origin, lockIndex 0..2, remaining, total) — her vuruşta: sayaç + knob pozisyonu güncellemesi.
    public event Action<int, int, int, int> OnSafeHit;
    // (origin, lockIndex) — kilit kapandı (knob en alta indi).
    public event Action<int, int> OnLockClosed;
    // (origin) — 3 kilit de bitti, kasa kırıldı → reveal.
    public event Action<int> OnSafeBroken;

    // ── Board binding (interceptor) ──────────────────────────────────────────

    private void OnEnable() => StartCoroutine(BindWhenReady());

    private void OnDisable()
    {
        if (board != null && board.ObstacleStateService != null
            && board.ObstacleStateService.SafeHitInterceptor == _hitHandler)
            board.ObstacleStateService.SafeHitInterceptor = null;
    }

    private IEnumerator BindWhenReady()
    {
        while (board == null || board.ObstacleStateService == null)
        {
            if (board == null)
                board = GetComponent<BoardController>()
                        ?? GetComponentInParent<BoardController>(true)
                        ?? FindFirstObjectByType<BoardController>();
            yield return null;
        }
        _hitHandler ??= HandleSafeHit;
        board.ObstacleStateService.SafeHitInterceptor = _hitHandler;
    }

    // Bir safe hücresine vurulunca ObstacleStateService bunu origin + hit kaynağı ile çağırır.
    private bool HandleSafeHit(int origin, ObstacleHitContext context, TileType? sourceTileType)
    {
        bool consumed = TryProcessHit(origin, context, sourceTileType, out bool broke);
        if (broke)
            Reveal(origin);
        return consumed;
    }

    public void Clear()
    {
        _byOrigin.Clear();
        _beneathByOrigin.Clear();
        _lastHitFrameByOrigin.Clear();
    }

    /// Level başında her kasa için bir kez çağrılır (GridSpawner/stamp aşaması).
    public void RegisterSafe(int origin, int redHits, int yellowHits, int greenHits)
    {
        RegisterSafe(
            origin,
            redHits,
            yellowHits,
            greenHits,
            SafeLockHitMode.Ordered,
            SafeLockColor.Red,
            SafeLockColor.Yellow,
            SafeLockColor.Green);
    }

    public void RegisterSafe(
        int origin,
        int redHits,
        int yellowHits,
        int greenHits,
        SafeLockHitMode hitMode,
        SafeLockColor firstLock,
        SafeLockColor secondLock,
        SafeLockColor thirdLock)
    {
        var s = new SafeState();
        s.total[0] = s.remaining[0] = Mathf.Max(1, redHits);
        s.total[1] = s.remaining[1] = Mathf.Max(1, yellowHits);
        s.total[2] = s.remaining[2] = Mathf.Max(1, greenHits);
        s.hitMode = hitMode;
        FillNormalizedOrder(s.order, firstLock, secondLock, thirdLock);
        s.activeOrderStep = 0;
        _byOrigin[origin] = s;
    }

    /// Stamp sırasında kasa altındaki içerik kaydı (GridSpawner). Kırılınca restore edilir.
    public void RegisterBeneath(int safeOrigin, List<BeneathCell> beneath)
    {
        _beneathByOrigin[safeOrigin] = beneath;
    }

    // Kasa kırıldı → altındaki içeriği geri yükle, hedefi bildir, board refill etsin.
    private void Reveal(int safeOrigin)
    {
        var state = board != null ? board.ObstacleStateService : null;

        if (state != null && _beneathByOrigin.TryGetValue(safeOrigin, out var beneath) && beneath != null)
        {
            // 1) Her hücreyi altındaki içeriğine geri yükle (None → hücre açılır + OnCellUnlocked).
            foreach (var b in beneath)
                state.RestoreCellObstacle(b.cell, b.id, b.origin);

            // 2) Restore edilen beneath obstacle origin'lerinin hit-state'ini yeniden kur.
            var reinited = new HashSet<int>();
            foreach (var b in beneath)
                if (b.id != ObstacleId.None && b.origin == b.cell && reinited.Add(b.origin))
                {
                    state.InitObstacleStateAt(b.origin);
                    board?.RaiseObstacleCreatedDynamic(b.origin % board.Width, b.origin / board.Width);
                }
        }

        // 3) Goal (Obstacle/Safe) ilerlesin.
        state?.NotifySafeBroken(safeOrigin);

        _beneathByOrigin.Remove(safeOrigin);

        // 4) Açılan boş hücreler, devam eden resolve/cascade ile gravity+spawn'dan dolar
        //    (kasa kırılması bir match-clear sırasında olur). Gerekirse board kendi akışında refill eder.
        Debug.Log($"[Safe] REVEALED origin={safeOrigin}");
    }

    public bool HasSafe(int origin)  => _byOrigin.ContainsKey(origin);
    public bool IsBroken(int origin) => _byOrigin.TryGetValue(origin, out var s) && AreAllLocksClosed(s);

    /// Aktif kilit index'i (0..2), kırıksa LockCount, kasa yoksa -1.
    public int GetActiveLock(int origin)
        => _byOrigin.TryGetValue(origin, out var s) ? GetFirstOpenLockInOrder(s) : -1;

    public int GetRemaining(int origin, int lockIndex)
        => _byOrigin.TryGetValue(origin, out var s) && IsValidLock(lockIndex) ? s.remaining[lockIndex] : 0;

    public int GetTotal(int origin, int lockIndex)
        => _byOrigin.TryGetValue(origin, out var s) && IsValidLock(lockIndex) ? s.total[lockIndex] : 0;

    /// <summary>
    /// Kasaya script/special gibi renksiz bir vuruş işler. Bu vuruşla kasa KIRILDIYSA true döner.
    /// Normal match kullanımı için TryProcessHit(context, sourceTileType) çağrılmalı.
    /// </summary>
    public bool ProcessHit(int origin)
    {
        return TryProcessHit(origin, ObstacleHitContext.Scripted, null, out bool broke) && broke;
    }

    /// <summary>
    /// Kasaya bir vuruş işler. Normal match yalnızca aktif kilidin rengine denk gelen tile'dan
    /// gelirse tüketilir: kırmızı=Core, sarı=Gear, yeşil=Plate. Special/Booster/Scripted aktif
    /// kilide joker hit gibi davranır. Kasa bu hit ile kırıldıysa broke=true döner.
    /// Kasa yoksa veya zaten kırıksa false.
    /// </summary>
    public bool TryProcessHit(
        int origin,
        ObstacleHitContext context,
        TileType? sourceTileType,
        out bool broke)
    {
        broke = false;
        if (!_byOrigin.TryGetValue(origin, out var s)) return false;
        if (AreAllLocksClosed(s)) return false;

        int li = ResolveHitLock(s, context, sourceTileType);
        if (!IsValidLock(li))
            return false;

        if (_lastHitFrameByOrigin.TryGetValue(origin, out int lastFrame) && lastFrame == Time.frameCount)
            return false;

        _lastHitFrameByOrigin[origin] = Time.frameCount;

        s.remaining[li] = Mathf.Max(0, s.remaining[li] - 1);
        OnSafeHit?.Invoke(origin, li, s.remaining[li], s.total[li]);

        if (s.remaining[li] <= 0)
        {
            OnLockClosed?.Invoke(origin, li);
            AdvanceOrderStep(s);

            if (AreAllLocksClosed(s))
            {
                OnSafeBroken?.Invoke(origin);
                broke = true;
                return true;
            }
        }

        return true;
    }

    private static int ResolveHitLock(SafeState state, ObstacleHitContext context, TileType? sourceTileType)
    {
        if (state == null)
            return -1;

        if (context != ObstacleHitContext.NormalMatch)
            return GetFirstOpenLockInOrder(state);

        if (!sourceTileType.HasValue || !TryGetLockForTile(sourceTileType.Value, out int sourceLock))
            return -1;

        if (state.hitMode == SafeLockHitMode.AnyColor)
            return state.remaining[sourceLock] > 0 ? sourceLock : -1;

        int activeLock = GetFirstOpenLockInOrder(state);
        if (activeLock < 0)
            return -1;

        return sourceLock == activeLock ? activeLock : -1;
    }

    private static bool TryGetLockForTile(TileType tileType, out int lockIndex)
    {
        switch (tileType)
        {
            case TileType.Core:
                lockIndex = (int)SafeLockColor.Red;
                return true;
            case TileType.Gear:
                lockIndex = (int)SafeLockColor.Yellow;
                return true;
            case TileType.Plate:
                lockIndex = (int)SafeLockColor.Green;
                return true;
            default:
                lockIndex = -1;
                return false;
        }
    }

    private static int GetFirstOpenLockInOrder(SafeState state)
    {
        if (state == null)
            return -1;

        AdvanceOrderStep(state);
        if (state.activeOrderStep >= LockCount)
            return LockCount;

        return (int)state.order[state.activeOrderStep];
    }

    private static void AdvanceOrderStep(SafeState state)
    {
        if (state == null)
            return;

        while (state.activeOrderStep < LockCount)
        {
            int lockIndex = (int)state.order[state.activeOrderStep];
            if (IsValidLock(lockIndex) && state.remaining[lockIndex] > 0)
                break;

            state.activeOrderStep++;
        }
    }

    private static bool AreAllLocksClosed(SafeState state)
    {
        if (state == null)
            return true;

        for (int i = 0; i < LockCount; i++)
            if (state.remaining[i] > 0)
                return false;

        return true;
    }

    private static void FillNormalizedOrder(
        SafeLockColor[] target,
        SafeLockColor first,
        SafeLockColor second,
        SafeLockColor third)
    {
        if (target == null || target.Length < LockCount)
            return;

        bool[] used = new bool[LockCount];
        int write = 0;

        AddIfValid(first);
        AddIfValid(second);
        AddIfValid(third);

        for (int i = 0; i < LockCount && write < LockCount; i++)
            AddIfValid((SafeLockColor)i);

        void AddIfValid(SafeLockColor color)
        {
            int idx = (int)color;
            if (!IsValidLock(idx) || used[idx] || write >= LockCount)
                return;

            target[write++] = color;
            used[idx] = true;
        }
    }

    private static bool IsValidLock(int lockIndex) => lockIndex >= 0 && lockIndex < LockCount;
}
