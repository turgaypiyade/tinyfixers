using UnityEngine;
using UnityEngine.EventSystems;

public class ObstacleClickProxy : MonoBehaviour, IPointerClickHandler
{
    private BoardController board;
    private int x;
    private int y;

    public void Init(BoardController board, int x, int y)
    {
        this.board = board;
        this.x = x;
        this.y = y;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        board?.TryUseBoosterAtCell(x, y);
    }
}
