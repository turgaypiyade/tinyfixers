using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class PreLevelSpecialRuntimeInjector : MonoBehaviour
{
    // Board hazır olduğu ZAMANLA değil BoardController.OnBecameIdle EVENT'iyle anlaşılır
    // (giriş slide + ilk resolve bitince EndBusy tetikler). Güvenlik tavanı: event beklenmedik
    // şekilde hiç gelmezse sonsuza takılmayalım — normalde devreye girmez.
    [SerializeField] private float boardReadySafetyCap = 30f;
    [SerializeField] private int startupDelayFrames = 2;
    [SerializeField] private float revealDuration = 0.4f;
    [SerializeField] private float startScale = 2.5f;
    [SerializeField] private float placementGap = 0.08f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip placeSfx;
    [SerializeField, Range(0f, 1f)] private float placeSfxVolume = 1f;

    private BoardController board;
    private readonly List<TileView> candidates = new();
    private readonly List<TileSpecial> pendingSelection = new();
    private bool initialized;

    // ── Harici teslimat hook'ları (ör. UFO streak event) ──────────────────────
    // Board hazır olduktan SONRA, ilk yerleşimden ÖNCE beklenir (UFO uçup gelsin diye).
    public System.Func<IEnumerator> PrePlacementGate;
    // Her special bir hücreye uygulanınca (Reveal'den önce) çağrılır — UFO o hücreye ışın atar.
    public System.Action<TileView, TileSpecial> OnSpecialPlaced;
    // Tüm yerleşim bitince (Destroy'dan önce) çağrılır — UFO uçup gider.
    public System.Action OnPlacementFinished;
    // UFO kendi ışın-görseli kullanacaksa injector'ın kendi Reveal animasyonunu atla.
    public bool SuppressDefaultReveal;
    // Streak teslimatı prelevel selection state'ini TEMİZLEMESİN (o ayrı akış).
    public bool SuppressSelectionStateClear;

    /// <summary>
    /// Prelevel ile ÇAKIŞMAYAN, kendine ait bir injector instance'ı oluşturur (find-existing yapmaz).
    /// Streak/UFO teslimatı bunu kullanır; pendingSelection'ı ezmez.
    /// </summary>
    public static PreLevelSpecialRuntimeInjector CreateDedicated(IReadOnlyList<TileSpecial> selected)
    {
        if (selected == null || selected.Count == 0)
            return null;

        var go = new GameObject("StreakBoosterInjector_Runtime");
        DontDestroyOnLoad(go);
        var inj = go.AddComponent<PreLevelSpecialRuntimeInjector>();
        inj.SuppressSelectionStateClear = true;
        inj.Initialize(selected);
        return inj;
    }

    public static PreLevelSpecialRuntimeInjector EnsureForSelection(IReadOnlyList<TileSpecial> selected)
    {
        if (selected == null || selected.Count == 0)
            return null;

        var existing = FindFirstObjectByType<PreLevelSpecialRuntimeInjector>(FindObjectsInactive.Include);
        if (existing == null)
        {
            var go = new GameObject("PreLevelSpecialInjector_Runtime");
            DontDestroyOnLoad(go);
            existing = go.AddComponent<PreLevelSpecialRuntimeInjector>();
        }
        else
        {
            // Ensure existing GO survives the scene change regardless of where it lives.
            DontDestroyOnLoad(existing.gameObject);
        }

        existing.Initialize(selected);
        Debug.Log($"[PreLevelSpecialRuntimeInjector] EnsureForSelection count={selected.Count}");
        return existing;
    }

    public void Initialize(IReadOnlyList<TileSpecial> selected)
    {
        pendingSelection.Clear();

        if (selected != null)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] != TileSpecial.None)
                    pendingSelection.Add(selected[i]);
            }
        }

        initialized = true;
        Debug.Log($"[PreLevelSpecialRuntimeInjector] Initialized with {pendingSelection.Count} selected specials.");
    }

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private IEnumerator Start()
    {
        var selected = initialized
            ? new List<TileSpecial>(pendingSelection)
            : PreLevelSpecialSelectionState.GetSelectionSnapshot();

        if (selected.Count == 0)
        {
            Debug.Log("[PreLevelSpecialRuntimeInjector] No selected specials; destroying injector.");
            Destroy(gameObject);
            yield break;
        }

        // pendingSelection = yerleştirilmeyi BEKLEYEN kalan liste (otoritatif). Her başarılı yerleşim
        // buradan düşer; board'da yer yoksa kalanlar burada kalır ve sonraki settle'da tekrar denenir.
        pendingSelection.Clear();
        pendingSelection.AddRange(selected);

        for (int i = 0; i < startupDelayFrames; i++)
            yield return null;

        // Önce loading ekranı kapansın (görsel netlik), sonra board OnBecameIdle event'iyle
        // gerçekten otursun. İkisi de zaman poll'u değil — biri IsVisible, biri idle event'i.
        yield return WaitForLoadingScreenHidden();

        yield return WaitForReadyBoard();

        if (board == null)
            board = FindFirstObjectByType<BoardController>();
        if (board == null)
        {
            Debug.LogWarning($"[PreLevelSpecialRuntimeInjector] Board yok; teslimat atlandı. {GetBoardStatus()}");
            Destroy(gameObject);
            yield break;
        }

        // Bu teslimat YALNIZ bu level'a ait. Level değişir/board yok olursa (level bitti/çıkıldı)
        // döngü kendini kapatır — DontDestroyOnLoad injector başka level'a taşıp UFO uçurmasın.
        int levelAtStart = CurrentLevel.Global;
        int totalPlaced = 0;

        // ── Deferred (ertelenen) teslimat döngüsü ─────────────────────────────────────
        // Kullanıcı isteği: başta board'da yer yoksa (ör. açılış tek pulse ile; patlayınca taşlar
        // açılıyor) special sessizce DÜŞMESİN. Board her oturduğunda (ilk hamleden sonra taşlar
        // açılınca) yer var mı diye bak; varsa kalan special'ları yerleştir. Hepsi yerleşene veya
        // level bitene kadar sürer. Yer zaten başta varsa (sık durum) davranış AYNEN eskisi gibi:
        // ilk iterasyonda hemen yerleşir, biter.
        while (pendingSelection.Count > 0)
        {
            bool roomReady = false;
            while (true)
            {
                // Board (sahne objesi) yok olduysa level bitti/çıkıldı → bırak. Bu, DontDestroyOnLoad
                // injector'ın başka level'a taşmasını engelleyen definitif sinyal (per-frame ucuz null
                // kontrolü; CurrentLevel.Global = PlayerPrefs okuması olduğundan döngüde ÇAĞIRMIYORUZ).
                if (board == null)
                    break;

                if (IsBoardStableForDeferredPlacement() && HasEligibleCandidate())
                {
                    roomReady = true;
                    break;
                }

                yield return null;
            }

            // Ekstra güvenlik (ziyaret başına bir kez): level numarası değiştiyse (board yeniden
            // kurulduysa) bu eski teslimatı bırak — streak ödülü sonraki denemede yeniden hesaplanır.
            if (!roomReady || CurrentLevel.Global != levelAtStart)
                break;

            // Yer açıldı → bu partiyi teslim et (UFO hook'luysa uçup gelir).
            if (PrePlacementGate != null)
                yield return StartCoroutine(PrePlacementGate());

            BuildCandidates();

            // Sırayı koru: yerleşenler pending'den düşer, sığmayanlar kalır (sonraki ziyaret).
            var batch = new List<TileSpecial>(pendingSelection);
            pendingSelection.Clear();
            int placedThisVisit = 0;

            foreach (var special in batch)
            {
                var tile = TakeAndApplyToNextEligibleCandidate(special);
                if (tile == null)
                {
                    // Bu ziyarette yer bitti → kalan special'ları tekrar pending'e koy.
                    pendingSelection.Add(special);
                    continue;
                }

                totalPlaced++;
                placedThisVisit++;
                PlayPlacementSfx(special);

                // UFO ışını: bu hücreye at (Reveal'den önce). Reveal harici görselce bastırılabilir.
                OnSpecialPlaced?.Invoke(tile, special);

                if (!SuppressDefaultReveal)
                    yield return Reveal(tile);

                if (placementGap > 0f)
                    yield return new WaitForSeconds(placementGap);
            }

            OnPlacementFinished?.Invoke();

            Debug.Log($"[PreLevelSpecialRuntimeInjector] Visit placed {placedThisVisit}; remaining pending={pendingSelection.Count}");

            // Güvenlik: bu ziyaret hiç yerleştiremediyse (beklenmez — HasEligibleCandidate true'ydu)
            // sıkı döngüye girme, bir kare bekle.
            if (placedThisVisit == 0)
                yield return null;
        }

        if (totalPlaced > 0 && !SuppressSelectionStateClear)
            PreLevelSpecialSelectionState.Clear();

        Debug.Log($"[PreLevelSpecialRuntimeInjector] Finished. placed={totalPlaced}, leftover={pendingSelection.Count}");
        Destroy(gameObject);
    }

    // Board'un hazır olduğunu ZAMANLA değil EVENT'le anla: BoardController.OnBecameIdle.
    // Giriş slide + ilk resolve tek bir BeginBusy scope'unda; depth 0'a düşünce (EndBusy)
    // OnBecameIdle bir kez tetiklenir → board GERÇEKTEN oturmuştur. Zaten idle ise anında geçeriz.
    private IEnumerator WaitForReadyBoard()
    {
        if (board == null)
            board = FindFirstObjectByType<BoardController>();
        if (board == null)
            yield break;

        // Board zaten meşgul değilse beklemeye gerek yok (event'i kaçırmış olabiliriz — yarış).
        if (!board.IsBusy)
            yield break;

        bool becameIdle = false;
        System.Action onIdle = () => becameIdle = true;
        board.OnBecameIdle += onIdle;

        // EVENT gelene (veya state busy'den çıkana) kadar bekle. Zaman poll'u YOK; yalnızca
        // event hiç gelmezse sistemi kilitlememek için cömert bir güvenlik tavanı var.
        float safety = 0f;
        while (board != null && board.IsBusy && !becameIdle && safety < boardReadySafetyCap)
        {
            safety += Time.unscaledDeltaTime;
            yield return null;
        }

        if (board != null)
            board.OnBecameIdle -= onIdle;

        // Idle olduktan sonra veri (gridData/candidate) aynı kare stabilize olsun diye 1 kare ver.
        yield return null;
    }

    private IEnumerator WaitForLoadingScreenHidden()
    {
        // Güvenlik tavanı: loading ekranı beklenmedik şekilde kapanmazsa sonsuza
        // kadar takılmayalım.
        const float maxWait = 10f;
        float elapsed = 0f;

        while (LoadingScreenManager.IsVisible && elapsed < maxWait)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool IsBoardReady() => IsBoardFilledIdle() && HasEligibleCandidate();

    // Deferred teslimat için "board tamamen dolu / bütün tile'lar kusursuz seated" şartı fazla sert.
    // Yerleştirme zaten TakeAndApplyToNextEligibleCandidate → IsEligible ile tek hücre seviyesinde
    // tekrar doğrulanır. Burada yalnız resolve'un durduğunu ararız; eligible hücre varsa teslimat
    // başlasın. Aksi halde obstacle-heavy board'da tek bir stale/boş hücre tüm ödülü sonsuza bekletir.
    private bool IsBoardStableForDeferredPlacement()
    {
        if (board == null || board.Tiles == null || board.Holes == null || board.GridData == null)
            return false;

        if (board.Width <= 0 || board.Height <= 0)
            return false;

        if (board.IsBusy || board.BlockingBackgroundJobs > 0)
            return false;

        return true;
    }

    // Board settled (arrays valid, not busy, no background jobs, tiles seated) — AMA "yerleştirilecek
    // uygun taş var mı" (HasEligibleCandidate) HARİÇ. Deferred teslimat bunu kullanır: board oturmuş
    // ama yer yoksa YOK sayıp destroy etmek yerine bekleyip sonraki settle'da tekrar dener.
    private bool IsBoardFilledIdle()
    {
        if (board == null || board.Tiles == null || board.Holes == null || board.GridData == null)
            return false;

        if (board.Width <= 0 || board.Height <= 0)
            return false;

        // Yalnız GERÇEK resolve işi (mid-cascade) placement'ı engellesin — BlockingBackgroundJobs.
        // ActiveBackgroundJobs (uçan goal-orb / key / patchbot dash gibi async işler) placement'ı
        // BLOKLAMAMALI: taşı normal bir hücreye koymak onlarla çakışmaz. Eski `ActiveBackgroundJobs>0`
        // kontrolü, aktif oyunda hep bir async uçuş olduğu için board'u neredeyse hiç "idle" saymıyor,
        // teslimatı oyun sonuna sarkıtıyordu (kullanıcı: "bazen 1. bazen 2. hamle, bazen oyun sonu").
        if (board.IsBusy || board.BlockingBackgroundJobs > 0)
            return false;

        int filledCells = 0;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (IsIgnoredForBoardReady(x, y))
                    continue;

                var tile = board.Tiles[x, y];
                if (tile == null || board.GridData[x, y] == null)
                    return false;

                if (tile.X != x || tile.Y != y)
                    return false;

                filledCells++;
            }
        }

        return filledCells > 0;
    }

    private void BuildCandidates()
    {
        candidates.Clear();

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                var tile = board.Tiles[x, y];
                if (IsEligible(tile, x, y))
                    candidates.Add(tile);
            }
        }
    }

    private bool IsEligible(TileView tile, int x, int y)
    {
        if (tile == null)
            return false;

        if (board == null || board.Tiles == null || board.GridData == null || board.Holes == null)
            return false;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (board.Tiles[x, y] != tile || tile.X != x || tile.Y != y)
            return false;

        if (board.Holes[x, y] || board.GridData[x, y] == null)
            return false;

        if (tile.GetSpecial() != TileSpecial.None)
            return false;

        // Obstacle'lar STACK/stage mantığında (bir hücreye üst üste birçok obstacle konabilir).
        // ESKİ HATA: `HasObstacleAt` "altında obstacle var mı" diye bakıyordu → EN ÜSTTE normal
        // hareketli taş olsa bile hücreyi eliyordu; obstacle'la kaplı level'larda (ör. 29) hiç aday
        // kalmıyor, teslimat atlanıyordu.
        //
        // DOĞRUSU (oyunun zaten bildiği): EN ÜSTTEKİ stack'te KULLANILABİLİR normal taş var mı?
        //  - `IsCellBlocked`: en üst stack blocker (Tube/Safe/Magnet/emitter…) → taş yok, ele.
        //  - `IsMovableObstacleAt`: en üstteki "taş" bir obstacle (barrel/chest) → normal değil, ele.
        //  - swap-bloklu (oyuncu kullanamaz): interaction-locked, oil, mud OLMAYAN under-tile → ele.
        // İzinli: mud/grass ve altında obstacle olan ama üstte normal hareketli taş olan hücreler.
        var obs = board.ObstacleStateService;
        if (obs != null &&
            (obs.IsCellBlocked(x, y) ||
             obs.IsMovableObstacleAt(x, y) ||
             obs.IsInteractionLockedAt(x, y) ||
             obs.IsOilAt(x, y) ||
             (obs.IsUnderTileObstacleAt(x, y) && !obs.IsInteractiveUnderTileOverlayAt(x, y))))
            return false;

        return true;
    }

    private bool IsIgnoredForBoardReady(int x, int y)
    {
        if (board == null)
            return true;

        if (board.IsMaskHoleCell(x, y))
            return true;

        // ESKİ HATA: HERHANGİ obstacle olan hücreyi yok sayıyordu → obstacle'la kaplı board'da
        // (ör. her hücre mud) TÜM hücreler yok sayılıp filledCells=0 → board asla "hazır" görünmez
        // → IsEligible düzelse bile teslimat atlanırdı. ARTIK yalnız taş TUTAMAYAN hücreleri yok say
        // (blocker + movable obstacle). Mud/grass/oil ÜSTÜNDE normal taş var → sayılır.
        var obs = board.ObstacleStateService;
        if (obs != null && (obs.IsCellBlocked(x, y) || obs.IsMovableObstacleAt(x, y)))
            return true;

        return false;
    }

    private TileView TakeRandomCandidate()
    {
        int index = Random.Range(0, candidates.Count);
        var tile = candidates[index];
        candidates.RemoveAt(index);
        return tile;
    }

    private TileView TakeAndApplyToNextEligibleCandidate(TileSpecial special)
    {
        while (candidates.Count > 0)
        {
            var tile = TakeRandomCandidate();
            if (tile == null)
                continue;

            if (ApplySpecial(tile, special))
                return tile;
        }

        return null;
    }

    private bool HasEligibleCandidate()
    {
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (IsEligible(board.Tiles[x, y], x, y))
                    return true;
            }
        }

        return false;
    }

    private string GetBoardStatus()
    {
        if (board == null)
            return "board=null";

        if (board.Tiles == null || board.Holes == null || board.GridData == null)
            return $"arrays tiles={board.Tiles != null} holes={board.Holes != null} gridData={board.GridData != null}";

        int tiles = 0;
        int gridData = 0;
        int holes = 0;
        int candidatesCount = 0;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y])
                {
                    holes++;
                    continue;
                }

                if (board.Tiles[x, y] != null)
                    tiles++;

                if (board.GridData[x, y] != null)
                    gridData++;

                if (IsEligible(board.Tiles[x, y], x, y))
                    candidatesCount++;
            }
        }

        return $"size={board.Width}x{board.Height} busy={board.IsBusy} bgJobs={board.ActiveBackgroundJobs} holes={holes} tiles={tiles} gridData={gridData} candidates={candidatesCount} obstacles=[{DescribeObstacleHistogram()}]";
    }

    // "Yer yok" durumunda hangi obstacle'ların board'u kapladığını gösterir (teşhis).
    private string DescribeObstacleHistogram()
    {
        var obs = board != null ? board.ObstacleStateService : null;
        if (obs == null)
            return "obstacleService=null";

        var counts = new Dictionary<ObstacleId, int>();
        for (int x = 0; x < board.Width; x++)
        for (int y = 0; y < board.Height; y++)
        {
            if (board.Holes[x, y]) continue;
            var id = obs.GetObstacleIdAt(x, y);
            if (id == ObstacleId.None) continue;
            counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;
        }

        if (counts.Count == 0)
            return "none";

        var sb = new System.Text.StringBuilder();
        foreach (var kv in counts)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(kv.Key).Append('x').Append(kv.Value);
        }
        return sb.ToString();
    }

    private bool ApplySpecial(TileView tile, TileSpecial special)
    {
        if (tile == null || special == TileSpecial.None || board == null)
            return false;

        if (!IsEligible(tile, tile.X, tile.Y))
            return false;

        if (special == TileSpecial.SystemOverride)
        {
            TileType baseType = tile.GetTileType();
            tile.SetSpecial(TileSpecial.SystemOverride, deferVisualUpdate: true);
            tile.SetOverrideBaseType(baseType);
        }
        else
        {
            tile.SetSpecial(special, deferVisualUpdate: true);
        }

        tile.RefreshIcon();
        board.SyncTileData(tile.X, tile.Y);
        board.RefreshTileObstacleVisual(tile);
        tile.ApplyTileSize(board.TileSize);
        return true;
    }

    private void PlayPlacementSfx(TileSpecial special)
    {
        if (audioSource != null)
        {
            AudioClip clip = placeSfx != null ? placeSfx : audioSource.clip;
            if (clip != null && GameSettings.SoundEnabled)
                audioSource.PlayOneShot(clip, placeSfxVolume);

            return;
        }

        if (board == null || board.Audio == null || special == TileSpecial.None)
            return;

        board.Audio.Emit(BoardSfxRequest.SpecialCreate(special));
    }

    private IEnumerator Reveal(TileView tile)
    {
        if (tile == null || tile.IconImage == null)
            yield break;

        RectTransform rt = tile.IconImage.rectTransform;
        Vector3 baseScale = rt.localScale;
        Color baseColor = tile.IconImage.color;

        rt.localScale = baseScale * startScale;
        tile.IconImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, revealDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = GetRevealScale(t);
            float alpha = Mathf.Clamp01(t / 0.18f) * baseColor.a;

            rt.localScale = baseScale * scale;
            tile.IconImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        rt.localScale = baseScale;
        tile.IconImage.color = baseColor;
    }

    private float GetRevealScale(float t)
    {
        if (t < 0.58f)
            return Mathf.LerpUnclamped(startScale, 1.18f, EaseOut(t / 0.58f));

        if (t < 0.82f)
            return Mathf.LerpUnclamped(1.18f, 0.96f, EaseOut((t - 0.58f) / 0.24f));

        return Mathf.LerpUnclamped(0.96f, 1f, EaseOut((t - 0.82f) / 0.18f));
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }
}
