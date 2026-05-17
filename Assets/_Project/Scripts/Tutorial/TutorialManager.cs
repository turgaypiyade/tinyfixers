using System.Collections;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    private const string PrefKeyPrefix = "tutorial_seen_";

    [SerializeField] private TutorialContentLibrary contentLibrary;
    [SerializeField] private TutorialOverlayController overlay;

    [SerializeField] private int maxPerLevel = 2;

    private BoardController board;
    private bool tutorialActive;
    private int shownThisLevel;

    private static TutorialManager instance;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (instance == this) instance = null;
    }

    private void Start()
    {
        StartCoroutine(InitBoard());
    }

    private IEnumerator InitBoard()
    {
        // Board henüz instantiate olmamış olabilir
        while (board == null)
        {
            board = FindFirstObjectByType<BoardController>();
            yield return null;
        }

        board.OnBecameIdle += OnBoardIdle;

        // Tile spawn + fall animasyonları tamamlansın
        yield return new WaitUntil(() =>
            board.Tiles != null && board.Width > 0 && !board.IsBusy && !board.InputLocked);

        // Bir frame daha bekle — OnBecameIdle zaten ateşlenmişse biz onu kaçırdık
        yield return new WaitForSeconds(0.3f);

        StartCoroutine(CheckNextTutorial());
    }

    private void Unsubscribe()
    {
        if (board != null)
            board.OnBecameIdle -= OnBoardIdle;
    }

    private void OnBoardIdle()
    {
        if (tutorialActive) return;
        StartCoroutine(CheckNextTutorial());
    }

    private IEnumerator CheckNextTutorial()
    {
        yield return null;
        yield return null;

        if (tutorialActive || board == null || board.IsBusy || board.InputLocked) yield break;
        if (shownThisLevel >= maxPerLevel) yield break;

        for (int i = 0; i <= (int)TutorialId.OverrideCreated; i++)
        {
            var id = (TutorialId)i;
            if (IsSeen(id)) continue;
            if (!BoardTutorialScanner.TryFind(id, board, out var opp)) continue;
            if (!opp.IsValid) continue;

            yield return StartCoroutine(RunTutorial(id, opp));
            yield break;
        }
    }

    private IEnumerator RunTutorial(TutorialId id, BoardTutorialScanner.Opportunity opp)
    {
        tutorialActive = true;

        string description = string.Empty;
        Sprite illustration = null;

        if (contentLibrary != null && contentLibrary.TryGet(id, out var entry))
        {
            description = string.IsNullOrEmpty(entry.locKey)
                ? string.Empty
                : GameLocalization.Get(entry.locKey);
            illustration = entry.illustrationSprite;
        }

        overlay?.Show(opp.From, opp.To, description, illustration);

        bool done = false;
        board.SetTutorialSwapFilter(
            new Vector2Int(opp.From.X, opp.From.Y),
            new Vector2Int(opp.To.X, opp.To.Y),
            onCompleted: () => done = true
        );

        yield return new WaitUntil(() => done);

        overlay?.Hide();
        MarkSeen(id);
        shownThisLevel++;
        tutorialActive = false;
    }

    // ── PlayerPrefs helpers ──

    public static bool IsSeen(TutorialId id) =>
        PlayerPrefs.GetInt(PrefKeyPrefix + (int)id, 0) == 1;

    public static void MarkSeen(TutorialId id)
    {
        PlayerPrefs.SetInt(PrefKeyPrefix + (int)id, 1);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        for (int i = 0; i <= (int)TutorialId.OverrideCreated; i++)
            PlayerPrefs.DeleteKey(PrefKeyPrefix + i);
        PlayerPrefs.Save();
    }
}
