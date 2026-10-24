using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rodos Heykeli görünümü (ObstacleId.RhodesStatue, 2x2, 4 vuruş). GridSpawner'ın çizdiği engel Image'ına takılır
/// (LaserCannon / TarpStatue deseni): Image aşamanın TABANINI (kalkansız, kılıçsız sahne) gösterir; kalkan ve kılıç
/// birebir yerinde duran çocuk katmanlardır. Vuruşta aşama sprite'ları değişir, kılıç yumruktan savrulur, kalkan yalpalar.
/// Katmanlar kullanıcının tam / parçasız sahne çiftlerinden piksel farkıyla çıkarıldı (dururken görüntü = orijinal sahne).
/// Sprite'lar Resources/RhodesStatue/ (S{n}_Base 1024², parçalar kırpılmış; konumlar aşağıdaki tuval oranlarında).
/// </summary>
public sealed class RhodesStatueView : MonoBehaviour
{
    private const string Dir = "RhodesStatue/";

    // Parça kutusu (tuval oranı, sol-üst köşeden x, y, w, h) — Rhodes katman çıkarma betiğinin ölçtüğü değerler.
    private static readonly Rect[] SwordRect =
    {
        new Rect(0.7392f, 0.1970f, 0.2193f, 0.3884f),
        new Rect(0.7392f, 0.1970f, 0.2193f, 0.3907f),
        new Rect(0.7392f, 0.2169f, 0.2193f, 0.3804f),
    };
    private static readonly Rect[] ShieldRect =
    {
        new Rect(0.0598f, 0.3453f, 0.4394f, 0.4458f),
        new Rect(0.0582f, 0.3493f, 0.4386f, 0.4450f),
    };
    // Dönme eksenleri (tuval oranı, sol-üstten): kılıç yumruğun ortasından, kalkan tutulduğu yerden (merkezine yakın).
    private static readonly Vector2 SwordPivot = new Vector2(0.837f, 0.51f);
    private static readonly Vector2 ShieldPivot = new Vector2(0.28f, 0.57f);

    private Image baseImage;
    private RectTransform sword, shield;
    private Image swordImage, shieldImage;
    private int stage = -1;
    private Coroutine hitCo;
    private static readonly Sprite[] StoneChips = new Sprite[4];

    /// Kalan vuruş → aşama: 4 sağlam, 3 çatlak, 2 iskele açığa çıkmış, 1 yalnız baş + iskele.
    public static int StageForRemaining(int remainingHits) => Mathf.Clamp(5 - remainingHits, 1, 4);

    public static RhodesStatueView Ensure(Image image, int remainingHits)
    {
        if (image == null) return null;
        if (!image.TryGetComponent<RhodesStatueView>(out var view))
            view = image.gameObject.AddComponent<RhodesStatueView>();
        view.Build();
        view.SetStage(StageForRemaining(remainingHits));
        return view;
    }

    private void Build()
    {
        if (baseImage != null) return;
        baseImage = GetComponent<Image>();
        baseImage.preserveAspect = false;
        sword = Layer("Sword", out swordImage);
        shield = Layer("Shield", out shieldImage);
    }

    private RectTransform Layer(string name, out Image img)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;   // runtime UI: layer 0'da doğarsa kamera çizmez
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        img = go.GetComponent<Image>();
        img.raycastTarget = false;     // tıklama engel Image'ına (ObstacleClickProxy) gitsin
        img.preserveAspect = false;
        return rt;
    }

    private void SetStage(int s)
    {
        if (s == stage) return;
        stage = s;
        baseImage.sprite = Load($"S{s}_Base");
        Place(sword, swordImage, s <= 3 ? Load($"S{s}_Sword") : null, s <= 3 ? SwordRect[s - 1] : default, SwordPivot);
        Place(shield, shieldImage, s <= 2 ? Load($"S{s}_Shield") : null, s <= 2 ? ShieldRect[s - 1] : default, ShieldPivot);
    }

    // Parçayı tuval oranındaki kutusuna yerleştirir; pivot = dönme ekseni (kutu içi oran, UI y yukarı).
    private static void Place(RectTransform rt, Image img, Sprite sprite, Rect r, Vector2 pivotCanvas)
    {
        bool on = sprite != null;
        rt.gameObject.SetActive(on);
        if (!on) return;
        img.sprite = sprite;
        rt.anchorMin = new Vector2(r.x, 1f - r.y - r.height);
        rt.anchorMax = new Vector2(r.x + r.width, 1f - r.y);
        rt.pivot = new Vector2((pivotCanvas.x - r.x) / r.width, 1f - (pivotCanvas.y - r.y) / r.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;   // pivot değişince kutu kaymasın
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }

    /// Ara vuruş (GridSpawner): yeni aşama + kılıç savrulması + kalkan yalpalaması.
    public void OnHit(int remainingHits, BoardController board)
    {
        int nextStage = StageForRemaining(remainingHits);
        if (nextStage == stage) return;
        if (hitCo != null) StopCoroutine(hitCo);
        ResetAttachments();
        // Capture the departing attachment BEFORE SetStage hides/replaces its sprite.
        switch (DebrisForTransition(stage, nextStage))
        {
            case 1: PlayStoneChips(board); break;
            case 2: PlayAttachmentBreak(board, shieldImage, 2, 1, 1.3f); break;
            case 3: PlayAttachmentBreak(board, swordImage, 1, 4, 1.65f); break;
        }
        SetStage(nextStage);
        if (isActiveAndEnabled) hitCo = StartCoroutine(HitMotion());
    }

    // 1→2 stone chips, 2→3 shield, 3→4 sword. Reloads/duplicate stage notifications emit nothing.
    internal static int DebrisForTransition(int previous, int next)
        => next == previous + 1 && previous >= 1 && previous <= 3 ? previous : 0;

    private void PlayStoneChips(BoardController board)
    {
        var root = board != null ? board.UnclippedFxRoot : null;
        if (root == null) return;
        var body = baseImage.rectTransform;
        Vector2 size = ObstacleScatterFx.SizeIn(root, body);
        float span = Mathf.Min(size.x, size.y);
        for (int i = 0; i < StoneChips.Length; i++)
        {
            if (StoneChips[i] == null) StoneChips[i] = Resources.Load<Sprite>("TarpStatue/Chip_" + i);
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * (i < 2 ? 0.22f : 0.11f);
            float y = i < 2 ? 0.17f : -0.12f;
            Vector3 world = body.TransformPoint(body.rect.center + new Vector2(body.rect.width * x, body.rect.height * y));
            float pieceSize = span * (i < 2 ? 0.40f : 0.46f);
            ObstacleScatterFx.Play(board, root, StoneChips[i], world, new Vector2(pieceSize, pieceSize), Color.white,
                "RhodesStoneChunk", new ObstacleScatterFx.Motion
                {
                    travel = new Vector2(side * span * (i < 2 ? 1.05f : 0.65f), span * (i < 2 ? 1.4f : 0.9f)),
                    gravity = span * 4f, spin = side * (220f + i * 35f), life = 0.85f, delay = i * 0.045f
                });
        }
    }

    private void PlayAttachmentBreak(BoardController board, Image source, int columns, int rows, float scale)
    {
        var root = board != null ? board.UnclippedFxRoot : null;
        if (root == null || source == null || source.sprite == null) return;
        var pieces = ObstacleScatterFx.GetSlices(board, source.sprite, columns, rows);
        if (pieces == null) return;
        var body = source.rectTransform;
        Vector2 size = ObstacleScatterFx.SizeIn(root, body);
        Vector2 statueSize = ObstacleScatterFx.SizeIn(root, baseImage.rectTransform);
        float span = Mathf.Min(statueSize.x, statueSize.y);
        for (int i = 0; i < pieces.Length; i++)
        {
            float x = (i % columns + 0.5f) / columns - 0.5f;
            float y = (i / columns + 0.5f) / rows - 0.5f;
            Vector3 world = body.TransformPoint(body.rect.center + new Vector2(body.rect.width * x, body.rect.height * y));
            float side = i % 2 == 0 ? -1f : 1f;
            ObstacleScatterFx.Play(board, root, pieces[i], world,
                new Vector2(size.x / columns, size.y / rows) * scale, Color.white,
                columns == 2 ? "RhodesShieldPiece" : "RhodesSwordPiece", new ObstacleScatterFx.Motion
                {
                    travel = new Vector2(side * span * (0.65f + i * 0.12f), span * (1.0f + i * 0.15f)),
                    gravity = span * 4.5f, spin = side * (210f + i * 45f), life = 0.9f
                });
        }
    }

    private IEnumerator HitMotion()
    {
        const float T = 0.65f;
        for (float t = 0f; t < T; t += Time.deltaTime)
        {
            float u = t / T;
            // Readable recoil on the surviving attachments, decaying back to their authored pivots.
            float swordAngle = 34f * Mathf.Sin(u * Mathf.PI * 2.4f) * Mathf.Pow(1f - u, 1.6f);
            float shieldAngle = -22f * Mathf.Sin(u * Mathf.PI * 3f) * Mathf.Pow(1f - u, 1.8f);
            float squash = 1f - 0.05f * Mathf.Sin(Mathf.Clamp01(u * 4f) * Mathf.PI);
            if (sword.gameObject.activeSelf) sword.localRotation = Quaternion.Euler(0f, 0f, swordAngle);
            if (shield.gameObject.activeSelf)
            {
                shield.localRotation = Quaternion.Euler(0f, 0f, shieldAngle);
                shield.localScale = new Vector3(squash, squash, 1f);
            }
            yield return null;
        }
        sword.localRotation = Quaternion.identity;
        shield.localRotation = Quaternion.identity;
        shield.localScale = Vector3.one;
        hitCo = null;
    }

    private void OnDisable()
    {
        if (hitCo != null) { StopCoroutine(hitCo); hitCo = null; }
        ResetAttachments();
    }

    private void ResetAttachments()
    {
        if (sword != null) sword.localRotation = Quaternion.identity;
        if (shield != null) { shield.localRotation = Quaternion.identity; shield.localScale = Vector3.one; }
    }

    private static Sprite Load(string name)
    {
        var s = Resources.Load<Sprite>(Dir + name);
        if (s == null && GameDiagnostics.CriticalErrors) Debug.LogWarning($"[RhodesStatue] Sprite yok: {Dir}{name}");
        return s;
    }
}
