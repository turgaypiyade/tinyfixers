using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialOverlayController : MonoBehaviour
{
    [Header("Dim")]
    [SerializeField] private Image dimImage;
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.6f;
    [SerializeField] private float fadeSpeed = 8f;

    [Header("Description (Group 1 only)")]
    [SerializeField] private GameObject descriptionRoot;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image illustrationImage;
    [Tooltip("Description alt kenarı ile grid üst kenarı arasındaki boşluk (piksel, pozitif = grid üstünden uzaklaş)")]
    [SerializeField] private float descriptionYOffset = 10f;

    [Header("Obstacle Hint")]
    [SerializeField] private Image obstacleIconImage;
    [SerializeField] private Button hintDismissButton;

    [Header("Hand + Tile Swap (synchronized)")]
    [SerializeField] private RectTransform handIcon;
    [Tooltip("Sprite içinde parmak ucunun pivot noktası. Alt-orta = (0.5, 0)")]
    [SerializeField] private Vector2 handTipPivot    = new Vector2(0.5f, 0f);
    [SerializeField] private float swapDuration      = 0.30f;  // ileri hareket
    [SerializeField] private float holdDuration      = 0.20f;  // B'de bekleme
    [SerializeField] private float returnDuration    = 0.20f;  // geri dönüş
    [SerializeField] private float pauseDuration     = 2.00f;  // tekrar başlamadan bekleme

    private BoardController board;
    private TileView tutorialFrom;
    private TileView tutorialTo;
    private Coroutine swapRoutine;
    private Coroutine fadeRoutine;
    private Action pendingHintDismiss;

    private void Awake()
    {
        board = FindFirstObjectByType<BoardController>();

        if (dimImage != null)
        {
            var c = dimImage.color;
            c.a = 0f;
            dimImage.color = c;
            dimImage.raycastTarget = false;

            var dimBtn = dimImage.GetComponent<Button>() ?? dimImage.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(OnHintDismissClicked);
        }

        if (handIcon != null)
            foreach (var g in handIcon.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;

        if (hintDismissButton != null)
        {
            hintDismissButton.onClick.AddListener(OnHintDismissClicked);
            hintDismissButton.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
    }

    public void Show(TileView from, TileView to, string description, Sprite illustration)
    {
        if (board == null) board = FindFirstObjectByType<BoardController>();

        tutorialFrom = from;
        tutorialTo   = to;

        if (dimImage != null) dimImage.raycastTarget = false;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        bool hasText = !string.IsNullOrEmpty(description);
        if (descriptionRoot != null)  descriptionRoot.SetActive(hasText);
        if (descriptionText != null)  descriptionText.text = description;
        if (illustrationImage != null)
        {
            if (illustration != null)
                illustrationImage.sprite = illustration;
            illustrationImage.gameObject.SetActive(hasText);
        }

        if (hasText) RepositionDescriptionAboveGrid();

        StopFade();
        fadeRoutine = StartCoroutine(FadeDim(dimAlpha));

        StopSwap();
        if (from != null && to != null)
            swapRoutine = StartCoroutine(LoopSwapWithHand(from, to));
    }

    public void ShowHint(Sprite icon, string description, Action onDismiss)
    {
        if (board == null) board = FindFirstObjectByType<BoardController>();

        pendingHintDismiss = onDismiss;

        if (dimImage != null) dimImage.raycastTarget = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        bool hasText = !string.IsNullOrEmpty(description);
        if (descriptionRoot != null)  descriptionRoot.SetActive(hasText || icon != null);
        if (descriptionText != null)  descriptionText.text = description;
        if (obstacleIconImage != null)
        {
            obstacleIconImage.sprite = icon;
            obstacleIconImage.gameObject.SetActive(icon != null);
        }

        if (hintDismissButton != null)
            hintDismissButton.gameObject.SetActive(true);

        if (hasText || icon != null)
            RepositionDescriptionAboveGrid();

        StopFade();
        fadeRoutine = StartCoroutine(FadeDim(dimAlpha));
    }

    public void Hide()
    {
        StopSwap();
        SnapTilesBack();
        StopFade();
        fadeRoutine = StartCoroutine(FadeOutThenHide());
    }

    private void OnHintDismissClicked()
    {
        if (dimImage != null) dimImage.raycastTarget = false;
        if (hintDismissButton != null)
            hintDismissButton.gameObject.SetActive(false);

        var callback = pendingHintDismiss;
        pendingHintDismiss = null;

        Hide();
        callback?.Invoke();
    }

    // ── Synchronized swap + hand loop ──

    private IEnumerator LoopSwapWithHand(TileView from, TileView to)
    {
        if (board == null) yield break;

        var rtA = from.RectTransform;
        var rtB = to.RectTransform;
        if (rtA == null || rtB == null) yield break;

        // El ikonunu hazırla
        if (handIcon != null)
        {
            handIcon.pivot     = handTipPivot;
            handIcon.anchorMin = new Vector2(0.5f, 0.5f);
            handIcon.anchorMax = new Vector2(0.5f, 0.5f);
            handIcon.gameObject.SetActive(true);
        }

        Vector3 posA = GetTileCenter(from);
        Vector3 posB = GetTileCenter(to);

        while (true)
        {
            Vector2 originA = new Vector2(from.X * board.TileSize, -from.Y * board.TileSize);
            Vector2 originB = new Vector2(to.X   * board.TileSize, -to.Y   * board.TileSize);

            // İleri: taşlar A→B, el A→B
            float t = 0f;
            while (t < swapDuration)
            {
                t += Time.deltaTime;
                float k = Smoothstep(Mathf.Clamp01(t / swapDuration));
                rtA.anchoredPosition = Vector2.LerpUnclamped(originA, originB, k);
                rtB.anchoredPosition = Vector2.LerpUnclamped(originB, originA, k);
                if (handIcon != null)
                    handIcon.position = Vector3.Lerp(posA, posB, k);
                yield return null;
            }
            rtA.anchoredPosition = originB;
            rtB.anchoredPosition = originA;
            if (handIcon != null) handIcon.position = posB;

            // B'de bekle
            yield return new WaitForSeconds(holdDuration);

            // Geri: taşlar B→A, el sabit (veya sönük)
            t = 0f;
            while (t < returnDuration)
            {
                t += Time.deltaTime;
                float k = Smoothstep(Mathf.Clamp01(t / returnDuration));
                rtA.anchoredPosition = Vector2.LerpUnclamped(originB, originA, k);
                rtB.anchoredPosition = Vector2.LerpUnclamped(originA, originB, k);
                yield return null;
            }

            from.SnapToGrid(board.TileSize);
            to.SnapToGrid(board.TileSize);
            if (handIcon != null) handIcon.position = posA;

            // Uzun bekleme — kullanıcı hamle yapabilsin
            yield return new WaitForSeconds(pauseDuration);
        }
    }

    private static float Smoothstep(float k) => k * k * (3f - 2f * k);

    private static Vector3 GetTileCenter(TileView tile)
    {
        var corners = new Vector3[4];
        tile.RectTransform.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    // ── Dim ──

    private IEnumerator FadeDim(float target)
    {
        if (dimImage == null) yield break;
        Color c = dimImage.color;
        while (!Mathf.Approximately(c.a, target))
        {
            c.a = Mathf.MoveTowards(c.a, target, fadeSpeed * Time.unscaledDeltaTime);
            dimImage.color = c;
            yield return null;
        }
        c.a = target;
        dimImage.color = c;
    }

    private IEnumerator FadeOutThenHide()
    {
        if (dimImage != null)
        {
            Color c = dimImage.color;
            while (c.a > 0f)
            {
                c.a = Mathf.MoveTowards(c.a, 0f, fadeSpeed * Time.unscaledDeltaTime);
                dimImage.color = c;
                yield return null;
            }
        }

        if (handIcon != null) handIcon.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    // ── Description positioning ──

    private void RepositionDescriptionAboveGrid()
    {
        if (descriptionRoot == null || board == null) return;
        if (board.TilesRoot == null || board.Width == 0 || board.Height == 0) return;

        var rt = descriptionRoot.GetComponent<RectTransform>();
        if (rt == null) return;

        var parentRect = rt.parent as RectTransform;
        if (parentRect == null) return;

        // Grid'in üst kenarını (row-0 top edge) parent local space'e çevir
        float gridCenterX  = (board.Width - 1) * board.TileSize * 0.5f;
        float gridTopEdgeY =  board.TileSize * 0.5f; // TilesRoot local: row-0 center=0, top edge=+TileSize/2

        Vector3 worldPt = board.TilesRoot.TransformPoint(new Vector3(gridCenterX, gridTopEdgeY, 0f));
        Vector3 localPt = parentRect.InverseTransformPoint(worldPt);

        // Description alt kenarı = grid üst kenarı + offset → merkez = alt kenar + yarı yükseklik
        float halfH = rt.rect.height * 0.5f;
        Vector3 lp  = rt.localPosition;
        lp.y = localPt.y + halfH + descriptionYOffset;
        rt.localPosition = lp;
    }

    // ── Helpers ──

    private void SnapTilesBack()
    {
        if (board == null) return;
        if (tutorialFrom != null) tutorialFrom.SnapToGrid(board.TileSize);
        if (tutorialTo   != null) tutorialTo.SnapToGrid(board.TileSize);
    }

    private void StopSwap()
    {
        if (swapRoutine != null) { StopCoroutine(swapRoutine); swapRoutine = null; }
        if (handIcon != null)    handIcon.gameObject.SetActive(false);
    }

    private void StopFade()
    {
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
    }
}
