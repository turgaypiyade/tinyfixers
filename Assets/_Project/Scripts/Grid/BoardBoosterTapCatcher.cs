using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Joker (booster) hedeflemesinde board kökündeki TEK hücre çözücü. Taşlar (TileView) ve tıklama vekili olan
/// engeller (ObstacleClickProxy) dokunuşu kendileri işler; geri kalan her şey buraya düşer:
///   • kendi işleyicisi olmayan ama raycast alan engel görselleri → olay hiyerarşide köke kabarcıklanır,
///   • raycast'i kapalı görseller ve taşsız hücre alanı → en alttaki şeffaf yüzey yakalar (o da köke kabarcıklanır).
/// Böylece taşsız bir engel hücresine dokunmak, engelin türüne/görünüm sınıfına bakmadan jokeri çalıştırır.
/// Hücre, ızgaranın sol-üst köşesine hizalı tiles kökünün (pivot 0,1) yerel uzayından hesaplanır.
/// Joker modu kapalıyken hiçbir şey yapmaz (normal oyun girdisine dokunmaz).
/// </summary>
public sealed class BoardBoosterTapCatcher : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    private const string SurfaceName = "BoosterTapSurface";

    private BoardController board;
    private RectTransform cellSpace;
    private bool handledOnDown;

    public static void Install(RectTransform root, BoardController board, RectTransform cellSpace)
    {
        if (root == null || board == null || cellSpace == null) return;

        if (!root.TryGetComponent(out BoardBoosterTapCatcher catcher))
            catcher = root.gameObject.AddComponent<BoardBoosterTapCatcher>();
        catcher.board = board;
        catcher.cellSpace = cellSpace;

        if (root.Find(SurfaceName) != null) return;
        var go = new GameObject(SurfaceName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = root.gameObject.layer;   // runtime UI: layer 0'da doğarsa kamera görmez
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling();   // her görselin altında: yalnız başka hiçbir şeyin almadığı dokunuşu alır
        var img = go.GetComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;
        go.GetComponent<CanvasRenderer>().cullTransparentMesh = true;   // çizilmez, yalnız raycast
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        handledOnDown = board != null && board.IsBoosterModeActive;
        if (handledOnDown) TryUse(eventData.position, eventData.pressEventCamera);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (handledOnDown) return;
        TryUse(eventData.position, eventData.pressEventCamera);
    }

    private void TryUse(Vector2 screenPos, Camera cam)
    {
        if (board == null || cellSpace == null || !board.IsBoosterModeActive) return;
        int size = board.TileSize;
        if (size <= 0) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(cellSpace, screenPos, cam, out var local)) return;

        int x = Mathf.FloorToInt(local.x / size);
        int y = Mathf.FloorToInt(-local.y / size);
        board.TryUseBoosterAtBoardCell(x, y);
    }
}
