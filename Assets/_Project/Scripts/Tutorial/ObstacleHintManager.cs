using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObstacleHintManager : MonoBehaviour
{
    private const string PrefKeyPrefix = "obstacle_hint_seen_";

    [Tooltip("Kapalıyken hiçbir hint gösterilmez. Hazır olduğunda aç.")]
    [SerializeField] private bool enableHints = false;
    [SerializeField] private ObstacleHintLibrary hintLibrary;
    [SerializeField] private ObstacleLibrary obstacleLibrary;
    [SerializeField] private TutorialOverlayController overlay;
    [SerializeField, Min(0.1f)] private float highlightDuration = 0.55f;

    private BoardController board;
    private static ObstacleHintManager activeManager;
    private bool ownsPause;
    private float previousTimeScale;
    private System.IDisposable flowPause;
    private readonly List<Outline> highlights = new();

    public static bool HasPendingHints => activeManager != null;

    private void Start()
    {
        if (RuntimeSimulationSession.IsActive) return;
        if (!enableHints || hintLibrary == null || overlay == null || activeManager != null) return;
        activeManager = this;
        StartCoroutine(InitAndCheck());
    }

    private IEnumerator InitAndCheck()
    {
        while (board == null)
        {
            board = FindFirstObjectByType<BoardController>();
            yield return null;
        }

        try
        {
            yield return new WaitUntil(CanIntroduceObstacle);
            yield return new WaitForSeconds(0.3f);
            // Recheck after the delay: an intro, resolve or another modal may have started.
            yield return new WaitUntil(CanIntroduceObstacle);
            yield return ShowPendingHints();
        }
        finally
        {
            if (overlay != null) overlay.CancelHint();
            ReleasePause();
            if (activeManager == this) activeManager = null;
        }
    }

    private bool CanIntroduceObstacle() => board != null && board.Tiles != null && board.Width > 0
        && !board.InputLocked && board.ActiveBackgroundJobs == 0 && !board.IsActionSequencePlaying
        && overlay != null && !overlay.IsVisible && Time.timeScale > 0f;

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

            if (!ownsPause)
            {
                previousTimeScale = Time.timeScale;
                ownsPause = true;
                board.SetInputLocked(true);
                flowPause = board.PauseFlowPump();
                Time.timeScale = 0f;
            }

            bool dismissed = false;
            overlay.ShowHint(icon, entry.GetTitle(), entry.GetDescription(), () => dismissed = true);
            yield return new WaitUntil(() => dismissed || overlay == null || !overlay.IsVisible);
            if (!dismissed) yield break;

            MarkHintSeen(id);
            // Renkli alet tehditleri (sarı/kırmızı/mavi/yeşil) TEK kural → biri anlatılınca hepsi görülmüş.
            if (IsColoredToolThreat(id))
                for (var c = ObstacleId.ToolThreatYellow; c <= ObstacleId.ToolThreatGreen; c++) MarkHintSeen(c);
            yield return HighlightObstacles(id);
        }
    }

    private IEnumerator HighlightObstacles(ObstacleId id)
    {
        GridSpawner spawner = null;
        foreach (var candidate in FindObjectsByType<GridSpawner>(FindObjectsSortMode.None))
            if (candidate.board == board) { spawner = candidate; break; }
        if (spawner == null) yield break;

        var graphics = new HashSet<Graphic>();
        spawner.CollectObstacleHintGraphics(id, graphics);
        foreach (var graphic in graphics)
        {
            if (graphic == null || !graphic.isActiveAndEnabled || graphic.color.a <= 0f) continue;
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.94f, 0.7f, 0f);
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = true;
            highlights.Add(outline);
        }

        if (highlights.Count == 0) yield break;
        float duration = Mathf.Max(0.1f, highlightDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float alpha = Mathf.Sin(Mathf.PI * elapsed / duration) * 0.9f;
            foreach (var outline in highlights)
                if (outline != null) outline.effectColor = new Color(1f, 0.94f, 0.7f, alpha);
            yield return null;
        }
        ClearHighlights();
    }

    private void ClearHighlights()
    {
        foreach (var outline in highlights)
        {
            if (outline == null) continue;
            outline.enabled = false;
            Destroy(outline);
        }
        highlights.Clear();
    }

    private void ReleasePause()
    {
        ClearHighlights();
        if (!ownsPause) return;
        ownsPause = false;
        Time.timeScale = previousTimeScale;
        flowPause?.Dispose();
        flowPause = null;
        if (board != null) board.SetInputLocked(false);
    }

    private void OnDisable()
    {
        if (activeManager == this)
        {
            if (overlay != null) overlay.CancelHint();
            ReleasePause();
            activeManager = null;
        }
        StopAllCoroutines();
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

        // obstacles[] runtime'da yalnız EN ÜST katmanı tutar (StampLayeredEntriesIntoLevel); 3+ katlı
        // yığında en alttaki authored katman yalnız beneath store'da kalır → oradan da topla.
        var beneath = new List<ObstacleId>();
        board.ObstacleStateService?.CollectStampedBeneathIds(beneath);
        foreach (var id in beneath)
            AddId(id);

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

        // Boss düellosu: düşmanın OYUN ORTASINDA fırlatacağı engeller tahtada başta yok → ilk oynayan
        // oyuncuya level başında anlatılır (yağ/plastik vb. + alet tehdidi; renkliler tek ipucu).
        if (levelData.levelKind == LevelKind.BossDuel)
        {
            foreach (var id in BossDuelObstaclePressure.GetPool(levelData))
                AddId(id);

            if (levelData.bossToolThreatEnabled)
            {
                bool anyColored = false;
                if (levelData.bossToolThreatTypes != null)
                    foreach (var t in levelData.bossToolThreatTypes)
                    {
                        if (!BossDuelToolThreat.IsToolThreat(t)) continue;
                        if (IsColoredToolThreat(t))
                        {
                            if (anyColored) continue;   // renkliler tek ipucu: ilk renk örnek olarak gösterilir
                            anyColored = true;
                        }
                        AddId(t);
                    }
                if (levelData.bossToolThreatTypes == null || levelData.bossToolThreatTypes.Length == 0)
                    AddId(ObstacleId.ToolThreat);   // liste boş = gri alet tehdidi
            }
        }

        return result;

        void AddId(ObstacleId id)
        {
            if (id == ObstacleId.None) return;
            if (seen.Add(id))
                result.Add(id);
        }
    }

    private static bool IsColoredToolThreat(ObstacleId id)
        => id >= ObstacleId.ToolThreatYellow && id <= ObstacleId.ToolThreatGreen;

    public static bool IsHintSeen(ObstacleId id) =>
        PlayerPrefs.GetInt(PrefKeyPrefix + (int)id, 0) == 1;

    public static void MarkHintSeen(ObstacleId id)
    {
        PlayerPrefs.SetInt(PrefKeyPrefix + (int)id, 1);
        PlayerPrefs.Save();
    }

    [ContextMenu("Reset Obstacle Hints (Show Again Next Level)")]
    private void ResetHintsForPreview() => ResetAll();

    public static void ResetAll()
    {
        foreach (ObstacleId id in System.Enum.GetValues(typeof(ObstacleId)))
            PlayerPrefs.DeleteKey(PrefKeyPrefix + (int)id);
        PlayerPrefs.Save();
    }
}
