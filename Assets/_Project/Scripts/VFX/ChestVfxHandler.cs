using UnityEngine;
using UnityEngine.UI;

public class ChestVfxHandler : MonoBehaviour
{
    [SerializeField] private BoardController board;
    [SerializeField] private RectTransform vfxRoot;

    [Header("Prefabs")]
    [Tooltip("Dolap kapağının açılış animasyon prefabı (kendi kendini yok eder)")]
    [SerializeField] private GameObject chestOpenAnimPrefab;
    [Tooltip("Renkli objenin kırılma efekti prefabı (kendi kendini yok eder)")]
    [SerializeField] private GameObject chestColorBreakPrefab;

    [Header("Renk Eşleştirme")]
    [SerializeField] private Color gearColor  = Color.yellow;
    [SerializeField] private Color coreColor  = Color.cyan;
    [SerializeField] private Color boltColor  = Color.red;
    [SerializeField] private Color plateColor = Color.green;

    private void OnEnable()
    {
        if (board == null) return;
        board.OnChestOpened += HandleChestOpened;
        board.OnChestColorRemoved += HandleChestColorRemoved;
    }

    private void OnDisable()
    {
        if (board == null) return;
        board.OnChestOpened -= HandleChestOpened;
        board.OnChestColorRemoved -= HandleChestColorRemoved;
    }

    private void HandleChestOpened(int originIndex)
    {
        if (chestOpenAnimPrefab == null || vfxRoot == null) return;
        var (center, size) = GetChestVfxRect(originIndex);
        SpawnInVfx(chestOpenAnimPrefab, center, size, Color.white);
    }

    private void HandleChestColorRemoved(int originIndex, ChestColorMask removedColor)
    {
        if (chestColorBreakPrefab == null || vfxRoot == null) return;
        var (center, size) = GetChestVfxRect(originIndex);
        SpawnInVfx(chestColorBreakPrefab, center, size, ResolveColorTint(removedColor));
    }

    // Prefabı VFXRoot içinde doğru pozisyon ve boyuta set ederek spawn eder
    private void SpawnInVfx(GameObject prefab, Vector2 vfxCenter, Vector2 size, Color tint)
    {
        var go = Instantiate(prefab, vfxRoot);
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = vfxCenter;
            rt.sizeDelta = size;
        }

        if (tint != Color.white)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.color = tint;
        }
    }

    // 2x2 dolabın VFXRoot içindeki merkezi ve piksel boyutu
    // (x,y) sol üst hücre merkezi — (x+1,y+1) sağ alt hücre merkezi
    // İki hücre merkezi arasındaki mesafe = 1 tile.  2x2 = 2 tile genişlik/yükseklik.
    private (Vector2 center, Vector2 size) GetChestVfxRect(int originIndex)
    {
        int boardWidth = board.ActiveLevelData != null ? board.ActiveLevelData.width : 1;
        int x = originIndex % boardWidth;
        int y = originIndex / boardWidth;

        Vector2 vfxA = WorldToVfxAnchored(board.GetCellWorldCenterPosition(x,     y));
        Vector2 vfxB = WorldToVfxAnchored(board.GetCellWorldCenterPosition(x + 1, y + 1));

        Vector2 center = (vfxA + vfxB) * 0.5f;
        // vfxA ile vfxB arasındaki fark = 1 tile.  2x2 alan = 2 tile.
        Vector2 size = new Vector2(
            Mathf.Abs(vfxB.x - vfxA.x) * 2f,
            Mathf.Abs(vfxB.y - vfxA.y) * 2f
        );
        return (center, size);
    }

    private Vector2 WorldToVfxAnchored(Vector3 worldPos)
    {
        var canvas = vfxRoot.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(vfxRoot, screen, cam, out var local);
        return local;
    }

    private Color ResolveColorTint(ChestColorMask mask) => mask switch
    {
        ChestColorMask.Gear  => gearColor,
        ChestColorMask.Core  => coreColor,
        ChestColorMask.Bolt  => boltColor,
        ChestColorMask.Plate => plateColor,
        _                    => Color.white
    };
}
