using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lazer topu görseli. Engel Image'ı tabanı (gri cıvatalı kare; auxiliarySprites[2]) gösterir; child'lar:
///   • 4 cıvata ışığı (turuncu) — her special vuruşunda bir tanesi patlayarak yanar,
///   • kule (namlu yukarı bakan sprite, tam ortadan döner) — açıyı LaserCannonService sürer.
/// Mantık yok: hedef seçimi, lazer, kafes ve kırılma LaserCannonService'te.
/// </summary>
[DisallowMultipleComponent]
public sealed class LaserCannonView : MonoBehaviour
{
    // Cıvata merkezleri (taban sprite'ında, normalize; UI y yukarı). Yanma sırası: sol üst → sağ üst → sağ alt → sol alt.
    private static readonly Vector2[] BoltCenters =
    {
        new Vector2(0.189f, 1f - 0.216f),
        new Vector2(0.814f, 1f - 0.216f),
        new Vector2(0.814f, 1f - 0.780f),
        new Vector2(0.187f, 1f - 0.779f),
    };
    private const float BoltSize = 61f / 512f;
    // Namlu ucu, kule merkezinden kule boyunun bu oranı kadar yukarıda (sprite: uç y≈40/512).
    private const float MuzzleOffset = 0.42f;
    private const float BoltPopSeconds = 0.28f;

    private RectTransform turret;
    private readonly Image[] bolts = new Image[4];
    private int lit;

    public RectTransform Rect => (RectTransform)transform;
    public RectTransform Turret => turret;
    public Sprite BoltSprite => bolts[0] != null ? bolts[0].sprite : null;
    public float Angle { get; private set; }

    public static LaserCannonView Ensure(Image baseImage, ObstacleDef def, int remainingHits)
    {
        if (baseImage == null) return null;
        var view = baseImage.GetComponent<LaserCannonView>();
        if (view == null) view = baseImage.gameObject.AddComponent<LaserCannonView>();
        var aux = def != null ? def.auxiliarySprites : null;
        // Stage sprite'ı kuleli birleşik ikondur (editör/hedef/ipucu); sahnede Image yalnız tabanı gösterir.
        if (aux != null && aux.Count > 2 && aux[2] != null) baseImage.sprite = aux[2];
        view.Build(aux != null && aux.Count > 0 ? aux[0] : null, aux != null && aux.Count > 1 ? aux[1] : null);
        int hits = def != null ? Mathf.Max(1, def.hits) : 4;
        view.SetLit(Mathf.Clamp(hits - remainingHits, 0, 4), animate: false);
        return view;
    }

    private void Build(Sprite turretSprite, Sprite boltSprite)
    {
        if (turret != null) return;
        int layer = gameObject.layer;
        for (int i = 0; i < bolts.Length; i++)
        {
            var go = new GameObject("LaserBolt" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            Vector2 c = BoltCenters[i];
            rt.anchorMin = c - Vector2.one * (BoltSize * 0.5f);
            rt.anchorMax = c + Vector2.one * (BoltSize * 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = boltSprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.enabled = false;
            bolts[i] = img;
        }

        var tgo = new GameObject("LaserTurret", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tgo.layer = layer;
        turret = (RectTransform)tgo.transform;
        turret.SetParent(transform, false);
        turret.anchorMin = Vector2.zero;
        turret.anchorMax = Vector2.one;
        turret.pivot = new Vector2(0.5f, 0.5f);
        turret.offsetMin = turret.offsetMax = Vector2.zero;
        var timg = tgo.GetComponent<Image>();
        timg.sprite = turretSprite;
        timg.raycastTarget = false;
        timg.preserveAspect = true;
    }

    public void SetAngle(float degrees)
    {
        Angle = degrees;
        if (turret != null) turret.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    /// Kule merkezi ve namlu ucu (dünya uzayı).
    public Vector3 CenterWorld => Rect.TransformPoint(Rect.rect.center);
    public Vector3 MuzzleWorld
    {
        get
        {
            if (turret == null) return CenterWorld;
            var r = turret.rect;
            return turret.TransformPoint(r.center + Vector2.up * (r.height * MuzzleOffset));
        }
    }

    /// <param name="count">Yanan cıvata sayısı (0..4). Yeni yananlar patlayarak belirir.</param>
    public void SetLit(int count, bool animate = true)
    {
        count = Mathf.Clamp(count, 0, bolts.Length);
        for (int i = 0; i < bolts.Length; i++)
        {
            var b = bolts[i];
            if (b == null) continue;
            bool on = i < count;
            bool fresh = on && i >= lit;
            b.enabled = on;
            if (!on || !fresh) continue;
            if (animate && isActiveAndEnabled) StartCoroutine(PopBolt(b.rectTransform));
            else b.rectTransform.localScale = Vector3.one;
        }
        lit = count;
    }

    private static IEnumerator PopBolt(RectTransform rt)
    {
        for (float t = 0f; t < BoltPopSeconds; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float k = t / BoltPopSeconds;
            // 0 → 1.45 → 1: hızlı şişip yerine oturur.
            float s = k < 0.45f ? Mathf.Lerp(0f, 1.45f, k / 0.45f) : Mathf.Lerp(1.45f, 1f, (k - 0.45f) / 0.55f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }
}
