using UnityEngine;
using UnityEngine.EventSystems;

public class ObstacleClickProxy : MonoBehaviour, IPointerClickHandler
{
    private BoardController board;
    private int originX;
    private int originY;
    private int width = 1;
    private int height = 1;
    private float tileSize;
    private RectTransform rect;

    // 1x1 obstacle / per-cell proxy: sadece origin hücresi.
    public void Init(BoardController board, int x, int y)
    {
        Init(board, x, y, 1, 1, 0f);
    }

    // Multi-cell obstacle: tek bir view tüm NxN alanı kaplar. Tıklanan alt-hücreyi
    // pointer pozisyonundan çözebilmek için footprint + tileSize gerekir.
    public void Init(BoardController board, int originX, int originY, int width, int height, float tileSize)
    {
        this.board = board;
        this.originX = originX;
        this.originY = originY;
        this.width = Mathf.Max(1, width);
        this.height = Mathf.Max(1, height);
        this.tileSize = tileSize;
        rect = transform as RectTransform;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (board == null) return;

        int cx = originX;
        int cy = originY;

        // Multi-cell: tıklama origin'e değil, pointer'ın altındaki gerçek hücreye gitmeli.
        // Böylece ör. row/column booster doğru satır/sütundan ateşlenir (origin'den değil).
        if ((width > 1 || height > 1) && tileSize > 0f && rect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out var local))
        {
            // Pivot'tan bağımsız: sol/üst kenara göre ofset (rect, board ile aynı yönde, top-left origin).
            float fromLeft = local.x - rect.rect.xMin;
            float fromTop = rect.rect.yMax - local.y;

            int subCol = Mathf.Clamp(Mathf.FloorToInt(fromLeft / tileSize), 0, width - 1);
            int subRow = Mathf.Clamp(Mathf.FloorToInt(fromTop / tileSize), 0, height - 1);

            cx = originX + subCol;
            cy = originY + subRow;
        }

        board.TryUseBoosterAtCell(cx, cy);
    }
}
