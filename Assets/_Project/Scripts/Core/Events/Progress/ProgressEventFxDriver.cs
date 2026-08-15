using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Oyun sahnesine eklenir. Event aktifken ilgili taş kırılınca, KIRILAN TAŞIN
/// sağ-üst köşesinde "+1" çıkar, yukarı süzülür ve söner.
///
/// Eşleştirme: GameEventBus.OnTileClearedAt (taş tipi + dünya pozisyonu) kısa bir
/// buffer'da tutulur; ProgressEventService.OnProgressGainedTyped (SAYILAN adet)
/// gelince o tipin son pozisyonlarından gained kadarına "+1" basılır. Böylece
/// yalnızca event'e gerçekten sayılan taşlar FX üretir.
public class ProgressEventFxDriver : MonoBehaviour
{
    [SerializeField] private RectTransform spawnAnchor;  // pozisyon bulunamazsa fallback nokta
    [SerializeField] private Canvas        canvas;

    [SerializeField] private Color  textColor     = new Color(1f, 0.9f, 0.2f);
    [SerializeField] private float  floatDistance = 55f;
    [SerializeField] private float  duration      = 0.75f;
    [SerializeField] private int    fontSize      = 30;

    [Tooltip("Taş merkezinden sağ-üst köşeye ofset (px, canvas uzayı).")]
    [SerializeField] private Vector2 tileCornerOffset = new Vector2(42f, 42f);

    [Tooltip("Yazının YATAY kalınlaştırma çarpanı (yükseklik değişmez). 1.5 = %50 daha geniş/tok harfler.")]
    [SerializeField, Range(1f, 2.5f)] private float widthBoost = 1.5f;

    [Tooltip("Harf stroke şişirme (TMP FaceDilate). 0.4 = bariz kalın; 0.5+ harfler yapışabilir.")]
    [SerializeField, Range(0f, 0.6f)] private float faceDilate = 0.4f;

    [Tooltip("Tek gain batch'inde en fazla kaç '+1' basılır (performans).")]
    [SerializeField, Min(1)] private int maxPerBatch = 8;

    // Son kırılan taşların pozisyon buffer'ı (tip + dünya pozisyonu + frame).
    private readonly List<(TileType type, Vector3 pos, int frame)> recentClears = new();
    private const int BufferFrameWindow = 6;
    private const int BufferMaxSize     = 64;

    private void Awake()
    {
        GameEventBus.OnTileClearedAt += OnTileClearedAt;
        ProgressEventService.OnProgressGainedTyped += OnGainedTyped;
    }

    private void OnDestroy()
    {
        GameEventBus.OnTileClearedAt -= OnTileClearedAt;
        ProgressEventService.OnProgressGainedTyped -= OnGainedTyped;
    }

    private void OnTileClearedAt(TileType type, Vector3 worldPos)
    {
        recentClears.Add((type, worldPos, Time.frameCount));
        if (recentClears.Count > BufferMaxSize)
            recentClears.RemoveAt(0);
    }

    private void OnGainedTyped(TileType type, int gained)
    {
        if (gained <= 0) return;

        // Sahne bağı kopmuşsa kendini kurtar (sessiz ölme).
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Bayat kayıtları at (eski frame'lerden kalanlar).
        int minFrame = Time.frameCount - BufferFrameWindow;
        recentClears.RemoveAll(e => e.frame < minFrame);

        // Bu tipin buffer'daki pozisyonlarından gained kadarını tüket.
        int spawned = 0;
        int budget = Mathf.Min(gained, maxPerBatch);
        for (int i = recentClears.Count - 1; i >= 0 && spawned < budget; i--)
        {
            if (recentClears[i].type != type) continue;
            StartCoroutine(CoFloat("+1", recentClears[i].pos));
            recentClears.RemoveAt(i);
            spawned++;
        }

        // Pozisyon bulunamadıysa (senkron kaçağı) toplamı fallback noktada göster.
        if (spawned == 0)
            StartCoroutine(CoFloat($"+{gained}", null));
    }

    private IEnumerator CoFloat(string text, Vector3? worldPos)
    {
        var go  = new GameObject("ProgressFx+1", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(160f, 60f);

        // ÖNEMLİ: anchoredPosition FARKLI parent uzayından kopyalanamaz —
        // world position ile GERÇEK ekran noktasına yerleş.
        if (worldPos.HasValue)
        {
            rt.position = worldPos.Value;
            rt.anchoredPosition += tileCornerOffset;   // taşın sağ-üst köşesine
        }
        else if (spawnAnchor != null)
        {
            rt.position = spawnAnchor.position;
        }
        else
        {
            rt.anchoredPosition = Vector2.zero;
        }

        // Üst üste binmesin: küçük rastgele saçılım.
        rt.anchoredPosition += new Vector2(Random.Range(-10f, 10f), Random.Range(-6f, 6f));

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (rt == null || tmp == null) yield break;
        tmp.text          = text;
        tmp.color         = textColor;
        tmp.fontSize      = fontSize;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        // Çok haneli "+N" kutuya sığmayıp kırpılmasın: sarma kapalı, taşmaya izin.
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Overflow;

        // Board'un renkli taşları üzerinde kaybolmasın: harfleri gerçekten KALINLAŞTIR
        // (FaceDilate stroke'u şişirir) + koyu kalın kontur + YATAY genişletme
        // (yükseklik sabit — "ince uzun" görünümü kırar, tok/dolgun harf).
        tmp.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);
        tmp.outlineColor = new Color32(60, 30, 0, 255);
        tmp.outlineWidth = 0.38f;
        rt.localScale = new Vector3(Mathf.Max(1f, widthBoost), 1f, 1f);

        Vector2 from = rt.anchoredPosition;
        Vector2 to   = from + Vector2.up * floatDistance;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Level geçişinde canvas/"+1" objesi yok edilebilir — destroyed RectTransform'a
            // erişim MissingReferenceException atar; Unity-null guard'ıyla sessizce çık.
            if (rt == null || tmp == null) yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            rt.anchoredPosition = Vector2.Lerp(from, to, t);

            // Boyut SABİT (yükseklik büyümesin) — görünürlük scale'den değil,
            // kalın stroke (FaceDilate) + kalın konturdan gelir.

            // Alfa: son %40'ta sön (başında tam görünür kalsın).
            var c = tmp.color;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            tmp.color = c;

            yield return null;
        }

        if (go != null) Destroy(go);
    }
}
