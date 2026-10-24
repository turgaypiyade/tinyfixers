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
    public void OnHit(int remainingHits)
    {
        SetStage(StageForRemaining(remainingHits));
        if (hitCo != null) StopCoroutine(hitCo);
        if (isActiveAndEnabled) hitCo = StartCoroutine(HitMotion());
    }

    private IEnumerator HitMotion()
    {
        const float T = 0.5f;
        for (float t = 0f; t < T; t += Time.deltaTime)
        {
            float u = t / T;
            // Kılıç: önce geri savrulur (+15°), sonra ileri (−9°), sönümle yerine oturur.
            float swordAngle = 15f * Mathf.Sin(u * Mathf.PI * 2.4f) * Mathf.Pow(1f - u, 1.6f);
            // Kalkan: ters yönde daha küçük yalpalama + darbe anında hafif sıkışma.
            float shieldAngle = -6f * Mathf.Sin(u * Mathf.PI * 3f) * Mathf.Pow(1f - u, 1.8f);
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
