using UnityEngine;
using UnityEngine.UI;

// A reusable body + two hinged wings. All motion is driven by FlyFruitService's shared update.
public sealed class FlyFruitFlyView : MonoBehaviour
{
    private const string PoolKey = "FlyFruit.Fly";
    private Image body, left, right;
    private RectTransform root;
    private bool sideView, facingLeft;
    private ObstacleDef art;
    private float cell;

    public static FlyFruitFlyView Rent(RectTransform parent, ObstacleDef def, float cellSize)
    {
        var go = UiVfxPool.RentRect(PoolKey, parent, "FruitFly");
        if (go == null) return null;
        var view = go.GetComponent<FlyFruitFlyView>();
        if (view == null) view = go.AddComponent<FlyFruitFlyView>();
        view.root = (RectTransform)go.transform; view.art = def; view.cell = cellSize;
        if (view.body == null)
        {
            view.left = Part("LeftWing", view.root);
            view.right = Part("RightWing", view.root);
            view.body = Part("Body", view.root);
        }
        view.root.anchorMin = view.root.anchorMax = parent.pivot;
        view.root.pivot = Vector2.one * .5f;
        view.root.sizeDelta = Vector2.one * cellSize;
        view.SetView(false, false, true);
        return view;
    }

    private static Image Part(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * .5f;
        return image;
    }

    private void SetView(bool profile, bool leftFacing, bool force = false)
    {
        if (!force && sideView == profile && facingLeft == leftFacing) return;
        sideView = profile; facingLeft = leftFacing;
        int offset = profile ? 3 : 0;
        var sprites = art?.auxiliarySprites;
        body.sprite = sprites != null && sprites.Count > offset ? sprites[offset] : null;
        left.sprite = sprites != null && sprites.Count > offset + 1 ? sprites[offset + 1] : null;
        right.sprite = sprites != null && sprites.Count > offset + 2 ? sprites[offset + 2] : null;
        body.rectTransform.sizeDelta = Vector2.one * cell * .78f;
        body.rectTransform.anchoredPosition = Vector2.zero;
        left.rectTransform.sizeDelta = right.rectTransform.sizeDelta = new Vector2(cell * .58f, cell * .4f);
        left.rectTransform.pivot = new Vector2(.94f, .22f);
        right.rectTransform.pivot = new Vector2(.06f, .22f);
        left.rectTransform.anchoredPosition = new Vector2(profile ? -.12f : -.19f, .04f) * cell;
        right.rectTransform.anchoredPosition = new Vector2(profile ? -.09f : .19f, .04f) * cell;
        body.transform.SetAsLastSibling();
    }

    public void Pose(Vector2 point, Vector2 direction, float age, float scale, float phase, bool front)
    {
        SetView(!front, direction.x < 0);
        root.anchoredPosition = point;
        root.localScale = new Vector3((!front && facingLeft ? -1f : 1f) * scale, scale, 1);
        float bank = front ? Mathf.Sin(age * 9f + phase) * 7f
            : Mathf.Clamp(Mathf.Atan2(direction.y, Mathf.Abs(direction.x) + .001f) * Mathf.Rad2Deg, -28f, 28f);
        root.localRotation = Quaternion.Euler(0, 0, !front && facingLeft ? -bank : bank);
        float flap = Mathf.Sin(age * Mathf.PI * 2f * 16f + phase);
        left.rectTransform.localRotation = Quaternion.Euler(0, 0, -20f + flap * 48f);
        right.rectTransform.localRotation = Quaternion.Euler(0, 0, 20f - flap * 48f);
        left.rectTransform.localScale = new Vector3(1, .62f + Mathf.Abs(flap) * .38f, 1);
        // In profile the far wing folds back behind the shoulder, rather than across the face.
        right.rectTransform.localScale = sideView ? new Vector3(-.72f, .72f, 1f) : Vector3.one;
    }

    public void Return() => UiVfxPool.Return(PoolKey, gameObject, 24);
}
