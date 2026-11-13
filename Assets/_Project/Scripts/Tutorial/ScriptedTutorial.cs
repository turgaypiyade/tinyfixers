using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Önceden tanımlı öğretici: hamleler level verisinde (LevelData.tutorialSteps) hücre hücre yazılıdır, tahta
/// pinli taşlarla bu hamlelere göre hazırlanır. Şansa bağlı fırsat taraması YOK (eski TutorialManager sahnede
/// kapalı). Her adımda yazı/pencere yok: ekran kararır, yalnız eşleşecek taşlar açık kalır (spot ışığı), el
/// hamleyi gösterir ve board yalnız o değişime izin verir.
/// Pinli hücreler adımlar bitene dek "rezerve"dir: maç öncesi special yerleştirmesi onlara dokunmaz.
/// Her level'ın adımları bir kez gösterilir (tekrar denemede yok).
/// </summary>
public sealed class ScriptedTutorial : MonoBehaviour
{
    private const string SeenPrefix = "scripted_tutorial_seen_";

    private static ScriptedTutorial active;
    private static readonly HashSet<int> reserved = new();
    private static int reservedWidth;

    private BoardController board;
    private TutorialStep[] steps;
    private int levelNumber;
    private int width;

    /// Karıştırma jokeri adımı henüz bitmedi mi? Bitene dek board "hamle yok" otomatik karıştırmasını yapmaz.
    public static bool HoldsAutoShuffle => active != null && active.shuffleStepPending;

    private bool shuffleStepPending;

    /// Pre-level special yerleştirmesi vb. bu hücreye dokunmamalı mı?
    public static bool IsReservedCell(int x, int y) =>
        active != null && reserved.Count > 0 && reserved.Contains(y * reservedWidth + x);

    /// GridSpawner level kurulumunda çağırır. Adımı olmayan / görülmüş level'da hiçbir şey yapmaz.
    public static void Begin(BoardController board, LevelData level, int levelNumber)
    {
        Stop();
        if (board == null || level == null || RuntimeSimulationSession.IsActive) return;
        if (level.tutorialSteps == null || level.tutorialSteps.Length == 0) return;
        if (IsSeen(levelNumber, level.tutorialSteps.Length - 1)) return;

        if (!board.TryGetComponent<ScriptedTutorial>(out var runner))
            runner = board.gameObject.AddComponent<ScriptedTutorial>();
        runner.board = board;
        runner.steps = level.tutorialSteps;
        runner.levelNumber = levelNumber;
        runner.width = level.width;
        runner.shuffleStepPending = false;
        for (int i = 0; i < level.tutorialSteps.Length; i++)
            if (level.tutorialSteps[i].id == TutorialId.JokerShuffle && !IsSeen(levelNumber, i))
                runner.shuffleStepPending = true;

        reserved.Clear();
        reservedWidth = level.width;
        int size = level.width * level.height;
        for (int i = 0; i < size; i++)
        {
            bool pinned = (level.pinnedTileTypes != null && i < level.pinnedTileTypes.Length && level.pinnedTileTypes[i] > 0)
                       || (level.pinnedSpecialTypes != null && i < level.pinnedSpecialTypes.Length && level.pinnedSpecialTypes[i] > 0);
            if (pinned) reserved.Add(i);
        }

        active = runner;
        runner.StartCoroutine(runner.Run());
    }

    private static void Stop()
    {
        if (active != null)
        {
            active.StopAllCoroutines();
            if (active.board != null) active.board.ClearTutorialJokerGate();
            var overlay = FindOverlay();
            if (overlay != null && overlay.IsVisible) overlay.Hide();
        }
        active = null;
        reserved.Clear();
    }

    private void OnDestroy()
    {
        if (board != null) board.ClearTutorialJokerGate();
        if (active == this) { active = null; reserved.Clear(); }
    }

    private IEnumerator Run()
    {
        var overlay = FindOverlay();
        if (overlay == null)
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning("[ScriptedTutorial] TutorialOverlayController bulunamadı; öğretici atlandı.");
            Finish();
            yield break;
        }

        for (int i = 0; i < steps.Length; i++)
        {
            if (IsSeen(levelNumber, i)) continue;

            // Tahta oturana, giriş/ipucu pencereleri kapanana dek bekle.
            yield return new WaitUntil(() => board == null || IsBoardReady(overlay));
            if (board == null) yield break;
            yield return new WaitForSeconds(0.35f);
            yield return new WaitUntil(() => board == null || IsBoardReady(overlay));
            if (board == null) yield break;

            if (IsJokerStep(steps[i].id))
            {
                if (i == 0) reserved.Clear();
                yield return RunJokerStep(steps[i], overlay);
                if (board == null) yield break;
                if (steps[i].id == TutorialId.JokerShuffle) shuffleStepPending = false;
                MarkSeen(levelNumber, i);
                continue;
            }

            if (!TryResolve(steps[i], out var from, out var to))
            {
                if (GameDiagnostics.CriticalErrors)
                    Debug.LogWarning($"[ScriptedTutorial] Level {levelNumber} adım {i}: hücreler geçersiz, atlandı.");
                MarkSeen(levelNumber, i);
                continue;
            }

            // Adımın hamlesi bu tahtada gerçekten oynanabilir mi? (Level kopyalanınca öğretici başka tahtaya
            // taşınabiliyor; geçersiz hamleye kilitlenen tahta oynanamaz — 2026-10-28, yeni level 4.)
            // Değilse öğreticinin TAMAMI atlanır: sonraki adımlar ilk adımın oluşturacağı special'a dayanır.
            if (!IsPlayableMove(from, to))
            {
                if (GameDiagnostics.CriticalErrors)
                    Debug.LogWarning($"[ScriptedTutorial] Level {levelNumber} adım {i}: hamle bu tahtada eşleşme yapmıyor; öğretici atlandı.");
                for (int k = i; k < steps.Length; k++) MarkSeen(levelNumber, k);
                break;
            }

            // İlk adım başlayınca rezerv kalkar: oyuncunun hamlesinden sonra tahta normal oynar.
            if (i == 0) reserved.Clear();

            bool done = false;
            overlay.ShowSpotlight(from, to, FocusCells(from, to), Caption(steps[i].id, from, to), Icon(steps[i].id, from, to));
            board.SetTutorialSwapFilter(new Vector2Int(from.X, from.Y), new Vector2Int(to.X, to.Y), () => done = true);
            yield return new WaitUntil(() => done || board == null);
            overlay.Hide();
            MarkSeen(levelNumber, i);
        }
        Finish();
    }

    // ── Joker adımı ── slot parlar, el slota dokunur → joker seçilince yalnız hedef açık kalır, el hedefe dokunur.
    // Board kapısı başka jokeri, iptali, swap'ı ve hedef dışı dokunuşu reddeder; joker kullanılınca adım biter.
    private static bool IsJokerStep(TutorialId id) => JokerIndex(id) >= 0;

    private static int JokerIndex(TutorialId id) => id switch
    {
        TutorialId.JokerHammer => 0, TutorialId.JokerRow => 1, TutorialId.JokerColumn => 2,
        TutorialId.JokerShuffle => 3, TutorialId.JokerPaint => 4, _ => -1,
    };

    private IEnumerator RunJokerStep(TutorialStep step, TutorialOverlayController overlay)
    {
        int index = JokerIndex(step.id);
        var cell = new Vector2Int(Mathf.Max(0, step.fromCell) % width, Mathf.Max(0, step.fromCell) / width);
        if (index == 3) cell = new Vector2Int(board.Width / 2, board.Height / 2);
        if (!BoosterAccessService.CanUse(index) || !JokerFocusOverlayController.TryGetSlotRect(index, out var slot)
            || cell.x >= board.Width || cell.y >= board.Height)
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning($"[ScriptedTutorial] Level {levelNumber}: joker {index} kullanılamıyor / slot ya da hedef yok; adım atlandı.");
            if (index == 3)
            {
                shuffleStepPending = false;
                board.EnsurePlayableBoard();   // hamlesiz açılış oyuncuya kilitli kalmasın
            }
            yield break;
        }

        bool used = false;
        board.SetTutorialJokerGate(index, cell, () => used = true);
        overlay.ShowJokerSlot(slot, index, GameLocalization.Get("tutorial_joker_pick_desc"));
        yield return new WaitUntil(() => board == null || board.IsBoosterModeActive || used);
        if (board == null) yield break;

        if (!used)
        {
            focus.Clear();
            focus.Add(cell);   // kullanıcı 2026-11-13: yalnız vurulacak / dokunulacak hücre açık
            overlay.ShowJokerTarget(focus, cell, index, GameLocalization.Get(JokerHintKey(index)));
            yield return new WaitUntil(() => board == null || used);
            if (board == null) yield break;
        }
        overlay.Hide();
    }

    private static string JokerHintKey(int index) => index switch
    {
        1 => "joker_hint_row", 2 => "joker_hint_column", 3 => "joker_hint_shuffle", 4 => "joker_hint_paint",
        _ => "joker_hint_single",
    };

    private void Finish()
    {
        if (active == this) active = null;
        reserved.Clear();
    }

    private bool IsBoardReady(TutorialOverlayController overlay) =>
        board.Tiles != null && board.Width > 0 && !board.IsBusy && !board.InputLocked
        && !ObstacleHintManager.HasPendingHints && !overlay.IsVisible;

    private bool TryResolve(TutorialStep step, out TileView from, out TileView to)
    {
        from = to = null;
        if (step.fromCell >= 0)
        {
            from = TileAt(step.fromCell % width, step.fromCell / width);
            to = TileAt(step.toCell % width, step.toCell / width);
            if (from == null || to == null) return false;
            if (step.id == TutorialId.SpecialCombo)
                return from.GetSpecial() != TileSpecial.None && to.GetSpecial() != TileSpecial.None;
            var special = from.GetSpecial() != TileSpecial.None ? from.GetSpecial() : to.GetSpecial();
            return IsActivationSpecial(step.id, special);
        }

        // Otomatik: tahtadaki ilk special + ona komşu oynanabilir normal taş (sağ, sol, alt, üst sırasıyla).
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            var t = board.Tiles[x, y];
            if (t == null || t.GetSpecial() == TileSpecial.None || t.IsSpecialLocked) continue;
            if (!IsActivationSpecial(step.id, t.GetSpecial())) continue;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                var n = TileAt(nx, ny);
                if (n == null || n.GetSpecial() != TileSpecial.None) continue;
                from = t;
                to = n;
                return true;
            }
        }
        return false;
    }

    // A cascade may create another special above the one just taught. Activation
    // must demonstrate the requested kind, rather than the first special in scan order.
    private static bool IsActivationSpecial(TutorialId id, TileSpecial special) => id switch
    {
        TutorialId.LineHActivated => special == TileSpecial.LineH || special == TileSpecial.LineV,
        TutorialId.PulseCoreActivated => special == TileSpecial.PulseCore,
        TutorialId.PatchBotActivated => special == TileSpecial.PatchBot,
        TutorialId.OverrideActivated => special == TileSpecial.SystemOverride,
        _ => true,
    };

    // Komşu iki taş ve değişim ya bir special içerir ya da en az bir 3'lü / 2x2 oluşturur.
    private bool IsPlayableMove(TileView from, TileView to)
    {
        if (from == null || to == null) return false;
        if (Mathf.Abs(from.X - to.X) + Mathf.Abs(from.Y - to.Y) != 1) return false;
        if (from.GetSpecial() != TileSpecial.None || to.GetSpecial() != TileSpecial.None) return true;
        return FocusCells(from, to).Count > 2;   // yalnız iki taş kaldıysa eşleşme yok
    }

    private TileView TileAt(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height || board.Holes[x, y]) return null;
        var t = board.Tiles[x, y];
        if (t == null) return null;
        var obs = board.ObstacleStateService;
        if (obs != null && (obs.IsMovableObstacleAt(x, y) || obs.IsInteractionLockedAt(x, y))) return null;
        return t;
    }

    // 4'lünün roketi kaydırma yönünden çıkar (MatchFinder: yatay kaydırma → LineH, dikey → LineV).
    private static TileSpecial LineFromSwap(TileView from, TileView to) =>
        from != null && to != null && from.Y == to.Y ? TileSpecial.LineH : TileSpecial.LineV;

    // Patlatma adımında tahtadaki gerçek roket (special'lı taş from ya da to).
    private static TileSpecial LineOnBoard(TileView from, TileView to)
    {
        var sp = from != null ? from.GetSpecial() : TileSpecial.None;
        if (sp != TileSpecial.LineH && sp != TileSpecial.LineV) sp = to != null ? to.GetSpecial() : TileSpecial.None;
        return sp == TileSpecial.LineV ? TileSpecial.LineV : TileSpecial.LineH;
    }

    private static string Caption(TutorialId id, TileView from, TileView to)
    {
        if (id == TutorialId.LineHActivated)
            return GameLocalization.Get(LineOnBoard(from, to) == TileSpecial.LineV
                ? "tutorial_rocket_v_activated_desc" : "tutorial_rocket_h_activated_desc");
        string key = id switch
        {
            TutorialId.BasicMatch       => "tutorial_basic_match_desc",
            TutorialId.LineHCreated     => "tutorial_emitter_created_desc",
            TutorialId.LineHActivated   => "tutorial_emitter_activated_desc",
            TutorialId.PatchBotCreated  => "tutorial_patchbot_created_desc",
            TutorialId.PulseCoreCreated => "tutorial_pulsecore_created_desc",
            TutorialId.OverrideCreated  => "tutorial_override_created_desc",
            TutorialId.SpecialCombo     => "tutorial_combo_desc",
            TutorialId.PulseCoreActivated => "tutorial_pulsecore_activated_desc",
            TutorialId.PatchBotActivated  => "tutorial_patchbot_activated_desc",
            TutorialId.OverrideActivated  => "tutorial_override_activated_desc",
            _                           => null,
        };
        return key != null ? GameLocalization.Get(key) : string.Empty;
    }

    // Yazının yanındaki ikon: oluşacak / kullanılacak special (eşleştirme adımında ikon yok).
    private static Sprite Icon(TutorialId id, TileView moving, TileView other)
    {
        var lib = TileIconLibrary.Shared;
        if (lib == null) return null;
        return id switch
        {
            TutorialId.LineHCreated     => lib.GetSpecialIcon(LineFromSwap(moving, other)),
            TutorialId.LineHActivated   => lib.GetSpecialIcon(LineOnBoard(moving, other)),
            TutorialId.SpecialCombo     => lib.GetSpecialIcon(moving != null && moving.GetSpecial() != TileSpecial.None
                                                              ? moving.GetSpecial() : TileSpecial.LineH),
            TutorialId.PulseCoreCreated => lib.GetSpecialIcon(TileSpecial.PulseCore),
            TutorialId.PatchBotCreated  => lib.GetPatchBotFullIcon(),   // gövde + pervane
            TutorialId.OverrideCreated  => moving != null ? lib.GetOverrideIcon(moving.GetTileType())
                                                          : lib.GetSpecialIcon(TileSpecial.SystemOverride),
            // Kullanma adımları: tahtadaki gerçek special'ın ikonu (Spark Orb renginde).
            TutorialId.PulseCoreActivated or TutorialId.PatchBotActivated or TutorialId.OverrideActivated
                                        => BoardSpecialIcon(lib, moving, other),
            _                           => null,
        };
    }

    private static Sprite BoardSpecialIcon(TileIconLibrary lib, TileView a, TileView b)
    {
        var t = a != null && a.GetSpecial() != TileSpecial.None ? a : b;
        if (t == null || t.GetSpecial() == TileSpecial.None) return null;
        return t.GetSpecial() switch
        {
            TileSpecial.SystemOverride => lib.GetOverrideIcon(t.GetTileType()),
            TileSpecial.PatchBot       => lib.GetPatchBotFullIcon(),   // gövde + pervane
            _                          => lib.GetSpecialIcon(t.GetSpecial()),
        };
    }

    // Odak = değişimden sonra eşleşecek taşlar: iki hücrede oluşan yatay/dikey 3+ dizi ve 2x2 kareler (L/T ikisi
    // birden). Special'lı adımda (patlatma / combo) yalnız iki taş.
    private readonly HashSet<Vector2Int> focus = new();

    private HashSet<Vector2Int> FocusCells(TileView from, TileView to)
    {
        focus.Clear();
        var a = new Vector2Int(from.X, from.Y);
        var b = new Vector2Int(to.X, to.Y);
        focus.Add(a);
        focus.Add(b);
        if (from.GetSpecial() != TileSpecial.None || to.GetSpecial() != TileSpecial.None) return focus;
        AddMatchesAround(b, from.GetTileType(), a, b);   // hareket eden taş b'ye gider
        AddMatchesAround(a, to.GetTileType(), a, b);
        return focus;
    }

    private void AddMatchesAround(Vector2Int c, TileType t, Vector2Int a, Vector2Int b)
    {
        AddRun(c, t, a, b, 1, 0);
        AddRun(c, t, a, b, 0, 1);
        for (int ox = -1; ox <= 0; ox++)
        for (int oy = -1; oy <= 0; oy++)
        {
            int sx = c.x + ox, sy = c.y + oy;
            if (Is(sx, sy, t, a, b) && Is(sx + 1, sy, t, a, b) && Is(sx, sy + 1, t, a, b) && Is(sx + 1, sy + 1, t, a, b))
            {
                focus.Add(new Vector2Int(sx, sy));
                focus.Add(new Vector2Int(sx + 1, sy));
                focus.Add(new Vector2Int(sx, sy + 1));
                focus.Add(new Vector2Int(sx + 1, sy + 1));
            }
        }
    }

    private void AddRun(Vector2Int c, TileType t, Vector2Int a, Vector2Int b, int dx, int dy)
    {
        int lo = 0, hi = 0;
        while (Is(c.x - (lo + 1) * dx, c.y - (lo + 1) * dy, t, a, b)) lo++;
        while (Is(c.x + (hi + 1) * dx, c.y + (hi + 1) * dy, t, a, b)) hi++;
        if (lo + hi + 1 < 3) return;
        for (int k = -lo; k <= hi; k++) focus.Add(new Vector2Int(c.x + k * dx, c.y + k * dy));
    }

    // Değişimden SONRA (x, y)'deki taş t tipinde normal taş mı?
    private bool Is(int x, int y, TileType t, Vector2Int a, Vector2Int b)
    {
        int rx = x, ry = y;
        if (x == a.x && y == a.y) { rx = b.x; ry = b.y; }
        else if (x == b.x && y == b.y) { rx = a.x; ry = a.y; }
        var tile = TileAt(rx, ry);
        return tile != null && tile.GetSpecial() == TileSpecial.None && tile.GetTileType() == t;
    }

    private static TutorialOverlayController FindOverlay() =>
        FindFirstObjectByType<TutorialOverlayController>(FindObjectsInactive.Include);

    private static bool IsSeen(int level, int step) => PlayerPrefs.GetInt($"{SeenPrefix}{level}_{step}", 0) == 1;

    private static void MarkSeen(int level, int step)
    {
        PlayerPrefs.SetInt($"{SeenPrefix}{level}_{step}", 1);
        PlayerPrefs.Save();
    }
}
