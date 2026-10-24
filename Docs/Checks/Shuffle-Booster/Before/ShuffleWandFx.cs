using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Karıştırma (Shuffle) jokerinin sunumu: Pati Asası joker slotundan tahtanın ortasına uçar, açık UCU merkezde
/// kalacak şekilde ucunun etrafında dönerek tahtayı "karıştırır"; uçtan girdap (hortum) açılır, parıltılar
/// sarmal çizerek dışa savrulur. Taşların sarmal hareketi BoosterService.AnimateShufflePreview'de, aynı süreyle.
/// Yalnız sunum — board sonucu değişmez. Görseller bir kez kurulur ve sonraki kullanımlarda yeniden kullanılır
/// (kök sahneyle yok edildiyse yeniden kurulur); kare başına ayırma yok.
/// Konum uzayı: FX parent'ın sol-üst (0,1) çapası — BoosterService.GetCellAnchoredCenter ile aynı.
/// </summary>
public sealed class ShuffleWandFx
{
    private const int SparkleCount = 18;
    private const float SparkleLife = 0.7f;
    private const float WandHeightTiles = 2.6f;
    private const float RestAngle = 0f;                                    // çizildiği gibi (kapak sağ üstte)
    private static readonly Vector2 TipPivot = new Vector2(0.245f, 0.095f); // asa görselinde açık uç

    private readonly MonoBehaviour host;
    private RectTransform root, wand, vortex;
    private Image wandImage, vortexImage;
    private readonly RectTransform[] sparkles = new RectTransform[SparkleCount];
    private readonly Image[] sparkleImages = new Image[SparkleCount];
    private Vector2 start, center;
    private float startScale = 1f, tileSize = 100f, vortexDiameter = 400f;
    private int runId;
    private bool exiting;

    public ShuffleWandFx(MonoBehaviour host) => this.host = host;

    /// <summary>Asayı slotun üstüne, slot ikonu boyunda yerleştirir. Görseller yüklenemezse false.</summary>
    public bool Prepare(RectTransform parent, Vector2 slotCenter, float slotSize, Vector2 boardCenter,
        float tile, float boardMinSide)
    {
        if (parent == null || !Ensure(parent)) return false;
        runId++;
        exiting = false;
        tileSize = Mathf.Max(1f, tile);
        start = slotCenter;
        center = boardCenter;
        vortexDiameter = Mathf.Max(tileSize * 2f, boardMinSide * 0.85f);

        float wandH = tileSize * WandHeightTiles;
        var sprite = wandImage.sprite;
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        wand.sizeDelta = new Vector2(wandH * aspect, wandH);
        startScale = Mathf.Clamp(slotSize / wandH, 0.2f, 1f);

        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        wand.anchoredPosition = start;
        wand.localScale = Vector3.one * startScale;
        wand.localRotation = Quaternion.Euler(0f, 0f, RestAngle);
        wandImage.color = Color.white;

        vortex.anchoredPosition = center;
        vortex.sizeDelta = Vector2.one * vortexDiameter;
        vortex.localScale = Vector3.zero;
        vortexImage.color = new Color(1f, 1f, 1f, 0f);
        for (int i = 0; i < SparkleCount; i++) sparkles[i].gameObject.SetActive(false);
        return true;
    }

    /// <summary>Slot → tahta merkezi: yay çizerek büyür, arkasında kayan yıldız izi.</summary>
    public IEnumerator FlyIn(float duration)
    {
        int id = runId;
        var trail = FlightTrailFx.Create(host, wand, new Color(1f, 0.86f, 0.35f, 1f), tileSize);
        Vector2 peak = (start + center) * 0.5f + new Vector2(0f, tileSize * 1.2f);
        // İz kabı merkez çapalı (FlightTrailFx); bizim konumlar sol-üst çapalı → merkeze çevir.
        Vector2 toTrail = new Vector2(-root.rect.width * 0.5f, root.rect.height * 0.5f);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (!Alive(id)) { trail?.Finish(); yield break; }
            float k = t / duration;
            float e = 1f - (1f - k) * (1f - k);   // ease-out
            Vector2 a = Vector2.LerpUnclamped(start, peak, e), b = Vector2.LerpUnclamped(peak, center, e);
            Vector2 p = Vector2.LerpUnclamped(a, b, e);
            wand.anchoredPosition = p;
            wand.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, e);
            wand.localRotation = Quaternion.Euler(0f, 0f, RestAngle + 25f * (1f - e));
            trail?.Step(p + toTrail, k);
            yield return null;
        }
        trail?.Finish();
        if (!Alive(id)) yield break;
        wand.anchoredPosition = center;
        wand.localScale = Vector3.one;
        wand.localRotation = Quaternion.Euler(0f, 0f, RestAngle);
    }

    /// <summary>Karıştırma: asa ucunun etrafında 2 tur döner, girdap açılıp kapanır, parıltılar sarmal çizer.</summary>
    public IEnumerator Mix(float duration)
    {
        int id = runId;
        float spin = 0f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (!Alive(id)) yield break;
            float k = t / duration;
            float s = k * k * (3f - 2f * k);
            float env = Envelope(k);

            wand.localRotation = Quaternion.Euler(0f, 0f, RestAngle - 720f * s);
            wand.localScale = Vector3.one * (1f + 0.08f * env);

            spin += Time.deltaTime * (240f + 480f * env);
            vortex.localRotation = Quaternion.Euler(0f, 0f, -spin);
            vortex.localScale = Vector3.one * env;
            vortexImage.color = new Color(1f, 1f, 1f, 0.5f * env);

            StepSparkles(t, env);
            yield return null;
        }
        if (!Alive(id)) yield break;
        wand.localRotation = Quaternion.Euler(0f, 0f, RestAngle);
        vortex.localScale = Vector3.zero;
        vortexImage.color = new Color(1f, 1f, 1f, 0f);
        for (int i = 0; i < SparkleCount; i++) sparkles[i].gameObject.SetActive(false);
    }

    /// <summary>Asa hafifçe yükselip söner; kök gizlenir (sonraki kullanım için durur). Birden çok çağrı güvenli.</summary>
    public IEnumerator Exit(float duration)
    {
        if (exiting || root == null) yield break;
        exiting = true;
        int id = runId;
        Vector2 from = wand.anchoredPosition;
        Color vc = vortexImage.color;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (!Alive(id)) yield break;
            float k = t / duration;
            wand.anchoredPosition = from + new Vector2(0f, tileSize * 0.6f * k);
            wand.localScale = Vector3.one * (1f + 0.2f * k);
            wandImage.color = new Color(1f, 1f, 1f, 1f - k);
            vortexImage.color = new Color(1f, 1f, 1f, vc.a * (1f - k));
            yield return null;
        }
        if (Alive(id)) root.gameObject.SetActive(false);
    }

    private bool Alive(int id) => id == runId && root != null;

    // 0→1 hızlı açılış (ilk %25), tutma, son %25'te kapanış.
    private static float Envelope(float k)
    {
        float up = Mathf.Clamp01(k / 0.25f), down = Mathf.Clamp01((1f - k) / 0.25f);
        float e = Mathf.Min(up, down);
        return e * e * (3f - 2f * e);
    }

    // Parıltılar uçtan doğup sarmal çizerek dışa savrulur; faz kaydırmalı döngü (havuz sabit, ayırma yok).
    private void StepSparkles(float time, float env)
    {
        float maxR = vortexDiameter * 0.5f;
        for (int i = 0; i < SparkleCount; i++)
        {
            float p = Mathf.Repeat(time / SparkleLife + i / (float)SparkleCount, 1f);
            float ang = i * 2.39996f - (time * 5f + p * 3f);   // altın açı dağılımı + saat yönünde sarmal
            float r = p * maxR;
            var rt = sparkles[i];
            rt.gameObject.SetActive(env > 0.01f);
            rt.anchoredPosition = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            rt.localScale = Vector3.one * Mathf.Lerp(0.45f, 0.2f, p);
            sparkleImages[i].color = new Color(1f, 0.95f, 0.75f, Mathf.Sin(Mathf.PI * p) * env);
        }
    }

    private bool Ensure(RectTransform parent)
    {
        if (root != null)
        {
            if (root.parent != parent) root.SetParent(parent, false);
            root.gameObject.layer = parent.gameObject.layer;
            return true;
        }
        var wandSprite = Resources.Load<Sprite>("Boosters/PawMagicWand");
        var vortexSprite = Resources.Load<Sprite>("Boosters/ShuffleVortex");
        var sparkleSprite = Resources.Load<Sprite>("Boosters/ShuffleSparkle");
        if (wandSprite == null) return false;

        root = NewRect("__ShuffleWandFx", parent);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        // Sıra: girdap (en altta), parıltılar, asa (en üstte).
        vortexImage = NewImage("Vortex", root, vortexSprite, new Vector2(0.5f, 0.5f), out vortex);
        for (int i = 0; i < SparkleCount; i++)
        {
            sparkleImages[i] = NewImage("Sparkle", root, sparkleSprite, new Vector2(0.5f, 0.5f), out sparkles[i]);
            sparkles[i].sizeDelta = Vector2.one * 96f;
            sparkles[i].gameObject.SetActive(false);
        }
        wandImage = NewImage("Wand", root, wandSprite, TipPivot, out wand);
        if (vortexSprite == null) vortexImage.enabled = false;
        if (sparkleSprite == null) for (int i = 0; i < SparkleCount; i++) sparkleImages[i].enabled = false;
        return true;
    }

    private static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;   // Screen Space Camera: layer 0 çizilmez
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image NewImage(string name, RectTransform parent, Sprite sprite, Vector2 pivot, out RectTransform rt)
    {
        rt = NewRect(name, parent);
        rt.gameObject.AddComponent<CanvasRenderer>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // GetCellAnchoredCenter uzayı
        rt.pivot = pivot;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;   // boyut sprite oranında veriliyor
        return img;
    }
}
