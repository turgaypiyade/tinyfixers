using System.Collections;
using UnityEngine;

public class ComboTutorialManager : MonoBehaviour
{
    private const string PrefKeyPrefix = "combo_tutorial_seen_";

    [SerializeField] private TutorialOverlayController overlay;

    private BoardController board;

    private void Start()
    {
        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        while (board == null)
        {
            board = FindFirstObjectByType<BoardController>();
            yield return null;
        }

        yield return new WaitUntil(() =>
            board.Tiles != null && board.Width > 0 && !board.IsBusy && !board.InputLocked);

        yield return new WaitForSeconds(0.3f);

        var levelData = board.ActiveLevelData;
        if (levelData == null || levelData.comboTutorial == ComboTutorialId.None)
            yield break;

        var comboId = levelData.comboTutorial;
        if (IsSeen(comboId))
            yield break;

        if (!TryGetComboSpecials(comboId, out var specialA, out var specialB))
            yield break;

        if (!TryFindCenterPair(out var tileA, out var tileB))
        {
            Debug.LogWarning($"[ComboTutorial] No eligible center pair found for {comboId}.");
            yield break;
        }

        tileA.SetSpecial(specialA);
        tileB.SetSpecial(specialB);

        // Specials görünür olsun
        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => !board.IsBusy && !board.InputLocked);

        overlay?.Show(tileA, tileB, string.Empty, null);

        bool done = false;
        board.SetTutorialSwapFilter(
            new Vector2Int(tileA.X, tileA.Y),
            new Vector2Int(tileB.X, tileB.Y),
            onCompleted: () => done = true
        );

        yield return new WaitUntil(() => done);

        overlay?.Hide();
        MarkSeen(comboId);
    }

    private bool TryFindCenterPair(out TileView tileA, out TileView tileB)
    {
        tileA = tileB = null;

        float cx = (board.Width  - 1) * 0.5f;
        float cy = (board.Height - 1) * 0.5f;

        TileView bestA = null, bestB = null;
        float bestDist = float.MaxValue;

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width - 1; x++)
            {
                if (!IsEligible(x, y) || !IsEligible(x + 1, y)) continue;

                float dist = Mathf.Abs(x + 0.5f - cx) + Mathf.Abs(y - cy);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestA = board.Tiles[x, y];
                    bestB = board.Tiles[x + 1, y];
                }
            }
        }

        tileA = bestA;
        tileB = bestB;
        return bestA != null;
    }

    private bool IsEligible(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return false;
        if (board.Holes[x, y]) return false;
        var tile = board.Tiles[x, y];
        return tile != null && tile.GetSpecial() == TileSpecial.None;
    }

    private static bool TryGetComboSpecials(ComboTutorialId id, out TileSpecial a, out TileSpecial b)
    {
        switch (id)
        {
            case ComboTutorialId.LineVLineH:        a = TileSpecial.LineV;          b = TileSpecial.LineH;          return true;
            case ComboTutorialId.LinePulseCore:     a = TileSpecial.LineH;          b = TileSpecial.PulseCore;      return true;
            case ComboTutorialId.LinePatchBot:      a = TileSpecial.LineH;          b = TileSpecial.PatchBot;       return true;
            case ComboTutorialId.PulseCorePatchBot: a = TileSpecial.PulseCore;      b = TileSpecial.PatchBot;       return true;
            case ComboTutorialId.OverrideLine:      a = TileSpecial.SystemOverride; b = TileSpecial.LineH;          return true;
            case ComboTutorialId.OverridePulseCore: a = TileSpecial.SystemOverride; b = TileSpecial.PulseCore;      return true;
            case ComboTutorialId.OverrideOverride:  a = TileSpecial.SystemOverride; b = TileSpecial.SystemOverride; return true;
            case ComboTutorialId.PulsePulse:        a = TileSpecial.PulseCore;      b = TileSpecial.PulseCore;      return true;
            case ComboTutorialId.PatchBotPatchBot:  a = TileSpecial.PatchBot;       b = TileSpecial.PatchBot;       return true;
            default:                                a = b = TileSpecial.None;                                       return false;
        }
    }

    public static bool IsSeen(ComboTutorialId id) =>
        PlayerPrefs.GetInt(PrefKeyPrefix + (int)id, 0) == 1;

    public static void MarkSeen(ComboTutorialId id)
    {
        PlayerPrefs.SetInt(PrefKeyPrefix + (int)id, 1);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        for (int i = 1; i <= (int)ComboTutorialId.PatchBotPatchBot; i++)
            PlayerPrefs.DeleteKey(PrefKeyPrefix + i);
        PlayerPrefs.Save();
    }
}
