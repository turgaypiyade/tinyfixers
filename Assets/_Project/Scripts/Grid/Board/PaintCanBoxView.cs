using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boya kutusu piramidi görseli. Engel Image'ı boş kasayı gösterir; kutular ayrı child Image'lardır
/// (4-3-2 piramit, tabanından sallanır). Vuruşta kalan tüm kutular sallanır, düşen kutular kısa bir
/// sallanmadan sonra takla atarak kasadan dışarı uçar. Kutu dizilimi kova sprite'ının en/boy oranından
/// hesaplanır; daha uzun kova çizilirse piramit kendiliğinden yukarıya kadar uzar.
/// </summary>
[DisallowMultipleComponent]
public sealed class PaintCanBoxView : MonoBehaviour
{
    private const string CanSpritePath = "PaintCanBox/PaintCan";

    // Kasa sprite'ında (normalize, UI y yukarı) zemin çizgisi ve kutulara ayrılan alan.
    private const float FloorY = 0.137f;
    private const float MaxPyramidHeight = 0.72f;
    private const float CanWidth = 0.19f;
    private const float RowOverlap = 0.78f;   // üst sıra alttakinin ağzına oturur
    private static readonly int[] RowCounts = { 4, 3, 2 };

    // Düşme sırası (slot index'i, alt sıradan yukarı soldan sağa): önce tepe, sonra orta sıranın ortası
    // (kalan iki kutu simetrik dursun), en son alt sıranın ortası.
    private static readonly int[] RemovalOrder = { 8, 7, 5, 6, 4, 3, 0, 2, 1 };

    private const float WobbleSeconds = 0.45f;
    private const float WobbleDegrees = 7f;
    private const float WobbleHz = 3.5f;
    private const float FallDelay = 0.12f;
    private const float FallSeconds = 0.75f;

    private static readonly Dictionary<PaintCanColor, Sprite> canSprites = new();

    private readonly List<RectTransform> slots = new();
    private int count;
    private float cellSize;
    private PaintCanColor color = PaintCanColor.Blue;
    private Coroutine wobble;

    public static int Capacity => RemovalOrder.Length;

    /// <param name="color">Kasadaki tüm kutuların rengi (Auto burada geçersiz; çağıran çözer).</param>
    public static PaintCanBoxView Ensure(Image boxImage, int canCount, float cellSize, PaintCanColor color)
    {
        if (boxImage == null) return null;
        var view = boxImage.GetComponent<PaintCanBoxView>();
        if (view == null) view = boxImage.gameObject.AddComponent<PaintCanBoxView>();
        view.cellSize = cellSize;
        view.color = color == PaintCanColor.Auto ? PaintCanColor.Blue : color;
        view.Build(canCount);
        return view;
    }

    /// Auto renk: kasanın satır bandına göre (her 2 satır bir renk) — yatayda kendiliğinden simetrik.
    public static PaintCanColor AutoColorForRow(int originY)
    {
        switch ((originY / 2) % 4)
        {
            case 0: return PaintCanColor.Red;
            case 1: return PaintCanColor.Yellow;
            case 2: return PaintCanColor.Green;
            default: return PaintCanColor.Blue;
        }
    }

    private static Sprite CanSprite(PaintCanColor color)
    {
        if (!canSprites.TryGetValue(color, out var sprite) || sprite == null)
        {
            // Renge özel çizim (PaintCanRed, PaintCanGreen...) varsa o; yoksa varsayılan kova (PaintCan).
            sprite = Resources.Load<Sprite>(CanSpritePath + color);
            if (sprite == null) sprite = Resources.Load<Sprite>(CanSpritePath);
            canSprites[color] = sprite;
        }
        return sprite;
    }

    private void Build(int canCount)
    {
        foreach (var rt in slots)
            if (rt != null) Destroy(rt.gameObject);
        slots.Clear();

        var sprite = CanSprite(color);
        if (sprite == null) return;

        float aspect = sprite.rect.height / Mathf.Max(1f, sprite.rect.width);
        float w = CanWidth;
        float h = w * aspect;
        float step = h * RowOverlap;
        float total = (RowCounts.Length - 1) * step + h;
        if (total > MaxPyramidHeight)
        {
            float k = MaxPyramidHeight / total;
            w *= k; h *= k; step *= k;
        }

        // Alt sıradan yukarı çiz: üst kutular alttakilerin üstünde kalsın.
        for (int row = 0; row < RowCounts.Length; row++)
        {
            int n = RowCounts[row];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("PaintCan", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                float cx = 0.5f + (i - (n - 1) * 0.5f) * w;
                float by = FloorY + row * step;
                rt.anchorMin = new Vector2(cx - w * 0.5f, by);
                rt.anchorMax = new Vector2(cx + w * 0.5f, by + h);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0f);   // tabanından sallanır
                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                slots.Add(rt);
            }
        }

        count = Mathf.Clamp(canCount, 0, Capacity);
        for (int k = 0; k < RemovalOrder.Length; k++)
            SetSlotVisible(RemovalOrder[k], k >= Capacity - count);
    }

    private void SetSlotVisible(int slot, bool visible)
    {
        if (slot >= 0 && slot < slots.Count && slots[slot] != null)
            slots[slot].gameObject.SetActive(visible);
    }

    /// Vuruş: kalan tüm kutular sallanır; eksilen kutular <paramref name="fxRoot"/> altına alınıp uçar.
    /// Düşen kutuların animasyonu <paramref name="host"/> üzerinde koşar (kasa yıkılsa da sürer).
    public void SetCount(int newCount, MonoBehaviour host, RectTransform fxRoot)
    {
        newCount = Mathf.Clamp(newCount, 0, Capacity);
        if (slots.Count == 0) { count = newCount; return; }
        if (newCount > count) { Build(newCount); return; }   // geri yükleme (snapshot)

        for (int k = Capacity - count; k < Capacity - newCount; k++)
            DropSlot(RemovalOrder[k], host, fxRoot);
        count = Mathf.Min(count, newCount);

        if (count > 0 && isActiveAndEnabled)
        {
            if (wobble != null) StopCoroutine(wobble);
            wobble = StartCoroutine(CoWobbleAll());
        }
    }

    private IEnumerator CoWobbleAll()
    {
        var phases = new float[slots.Count];
        for (int i = 0; i < phases.Length; i++)
            phases[i] = Random.value < 0.5f ? 0f : Mathf.PI;

        for (float t = 0f; t < WobbleSeconds; t += Time.deltaTime)
        {
            float k = t / WobbleSeconds;
            float damp = (1f - k) * (1f - k);
            for (int i = 0; i < slots.Count; i++)
            {
                var rt = slots[i];
                if (rt == null || !rt.gameObject.activeSelf) continue;
                float rowBoost = i < RowCounts[0] ? 1f : i < RowCounts[0] + RowCounts[1] ? 1.25f : 1.5f;
                float a = WobbleDegrees * rowBoost * damp * Mathf.Sin(t * WobbleHz * Mathf.PI * 2f + phases[i]);
                rt.localRotation = Quaternion.Euler(0f, 0f, a);
            }
            yield return null;
        }
        foreach (var rt in slots)
            if (rt != null) rt.localRotation = Quaternion.identity;
        wobble = null;
    }

    private void DropSlot(int slot, MonoBehaviour host, RectTransform fxRoot)
    {
        if (slot < 0 || slot >= slots.Count) return;
        var src = slots[slot];
        if (src == null || !src.gameObject.activeSelf) return;

        // Uçan kopya: kasa yıkılsa bile yaşasın diye fxRoot altında, aynı yer ve boyda.
        var parent = fxRoot != null ? fxRoot : (RectTransform)transform.parent;
        var go = new GameObject("PaintCanFall", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector3 world = src.TransformPoint(src.rect.center);
        rt.position = world;
        Vector3 lossy = src.lossyScale;
        Vector3 parentScale = parent.lossyScale;
        rt.sizeDelta = new Vector2(src.rect.width * lossy.x / Mathf.Max(1e-4f, parentScale.x),
            src.rect.height * lossy.y / Mathf.Max(1e-4f, parentScale.y));
        var img = go.GetComponent<Image>();
        img.sprite = src.GetComponent<Image>().sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        src.gameObject.SetActive(false);

        // Kasanın ortasına göre dışa doğru savrulur; ortadaki kutu rastgele bir yana.
        float side = Mathf.Sign(src.anchorMin.x + src.anchorMax.x - 1f);
        if (Mathf.Abs(src.anchorMin.x + src.anchorMax.x - 1f) < 0.01f)
            side = Random.value < 0.5f ? -1f : 1f;

        float cell = cellSize > 0f ? cellSize : rt.sizeDelta.x * 2.5f;
        var runner = host != null && host.isActiveAndEnabled ? host : this;
        runner.StartCoroutine(CoFall(rt, img, side, cell));
    }

    private static IEnumerator CoFall(RectTransform rt, Image img, float side, float cell)
    {
        // Önce yerinde bir an sallanır…
        for (float t = 0f; t < FallDelay; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            rt.localRotation = Quaternion.Euler(0f, 0f, -side * 10f * Mathf.Sin(t / FallDelay * Mathf.PI * 2f));
            yield return null;
        }

        // …sonra hoplayıp takla atarak dışarı düşer.
        Vector2 pos = rt.anchoredPosition;
        Vector2 vel = new Vector2(side * cell * Random.Range(1.6f, 2.2f), cell * Random.Range(2.6f, 3.2f));
        float gravity = cell * 14f;
        float spin = -side * Random.Range(420f, 600f);
        float angle = 0f;
        for (float t = 0f; t < FallSeconds; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float dt = Time.deltaTime;
            vel.y -= gravity * dt;
            pos += vel * dt;
            angle += spin * dt;
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            float k = t / FallSeconds;
            var c = img.color;
            c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            img.color = c;
            yield return null;
        }
        if (rt != null) Object.Destroy(rt.gameObject);
    }
}
