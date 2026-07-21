using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleHintManager : MonoBehaviour
{
    private const string PrefKeyPrefix = "obstacle_hint_seen_";

    [Tooltip("Kapalıyken hiçbir hint gösterilmez. Hazır olduğunda aç.")]
    [SerializeField] private bool enableHints = false;
    [SerializeField] private ObstacleHintLibrary hintLibrary;
    [SerializeField] private ObstacleLibrary obstacleLibrary;
    [SerializeField] private TutorialOverlayController overlay;

    private BoardController board;

    private void Start()
    {
        if (!enableHints) return;
        StartCoroutine(InitAndCheck());
    }

    private IEnumerator InitAndCheck()
    {
        while (board == null)
        {
            board = FindFirstObjectByType<BoardController>();
            yield return null;
        }

        yield return new WaitUntil(() =>
            board.Tiles != null && board.Width > 0 && !board.IsBusy && !board.InputLocked);

        yield return new WaitForSeconds(0.3f);
        yield return ShowPendingHints();
    }

    private IEnumerator ShowPendingHints()
    {
        if (hintLibrary == null || overlay == null) yield break;

        var ids = CollectObstacleIds();

        foreach (var id in ids)
        {
            if (IsHintSeen(id)) continue;
            if (!hintLibrary.TryGet(id, out var entry)) continue;

            Sprite icon = entry.iconOverride != null
                ? entry.iconOverride
                : obstacleLibrary?.Get(id)?.GetPreviewSprite();

            string text = !string.IsNullOrEmpty(entry.locKey)
                ? GameLocalization.Get(entry.locKey)
                : entry.fallbackText;

            bool dismissed = false;
            overlay.ShowHint(icon, text, () => dismissed = true);
            yield return new WaitUntil(() => dismissed);

            MarkHintSeen(id);
        }
    }

    private List<ObstacleId> CollectObstacleIds()
    {
        var seen = new HashSet<ObstacleId>();
        var result = new List<ObstacleId>();

        var levelData = board?.ActiveLevelData;
        if (levelData == null) return result;

        if (levelData.obstacles != null)
        {
            foreach (int raw in levelData.obstacles)
                AddId((ObstacleId)raw);
        }

        if (levelData.safes != null)
        {
            for (int i = 0; i < levelData.safes.Length; i++)
                AddId(ObstacleId.Safe);
        }

        if (levelData.stackedObstacles != null)
        {
            for (int i = 0; i < levelData.stackedObstacles.Length; i++)
                AddId(levelData.stackedObstacles[i].obstacleId);
        }

        if (levelData.tubes != null)
        {
            for (int i = 0; i < levelData.tubes.Length; i++)
                AddId(ObstacleId.Tube);
        }

        if (levelData.magnets != null)
        {
            for (int i = 0; i < levelData.magnets.Length; i++)
                AddId(ObstacleId.Magnet);
        }

        return result;

        void AddId(ObstacleId id)
        {
            if (id == ObstacleId.None) return;
            if (seen.Add(id))
                result.Add(id);
        }
    }

    public static bool IsHintSeen(ObstacleId id) =>
        PlayerPrefs.GetInt(PrefKeyPrefix + (int)id, 0) == 1;

    public static void MarkHintSeen(ObstacleId id)
    {
        PlayerPrefs.SetInt(PrefKeyPrefix + (int)id, 1);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        foreach (ObstacleId id in System.Enum.GetValues(typeof(ObstacleId)))
            PlayerPrefs.DeleteKey(PrefKeyPrefix + (int)id);
        PlayerPrefs.Save();
    }
}
