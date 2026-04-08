using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TopHUD'ın altında çalışan canlı yıldız progress bar'ı.
/// Hedefler tamamlandıkça ve hamleler kullanıldıkça bar güncellenir.
/// Bar üzerinde 3 yıldız marker'ı bulunur; geçilen marker'lar yanar.
/// </summary>
public class LiveStarHudController : MonoBehaviour
{
    [Header("Progress Bar")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image fillImage;           // Slider'ın Fill image'ı

    [Header("Bar Renkleri (1 → 2 → 3 yıldız)")]
    [SerializeField] private Color colorStar1 = new Color(0.85f, 0.35f, 0.2f);   // turuncu-kırmızı
    [SerializeField] private Color colorStar2 = new Color(0.95f, 0.75f, 0.1f);   // sarı
    [SerializeField] private Color colorStar3 = new Color(0.2f,  0.85f, 0.35f);  // yeşil-altın

    [Header("Yıldız Marker'ları (bar üzerinde 1., 2., 3. yıldız noktaları)")]
    [SerializeField] private Image starMarker1;         // barın ~%33'ünde
    [SerializeField] private Image starMarker2;         // barın ~%66'sında
    [SerializeField] private Image starMarker3;         // barın %100'ünde
    [SerializeField] private Sprite markerOnSprite;     // yanan yıldız
    [SerializeField] private Sprite markerOffSprite;    // sönük yıldız

    [Header("Eşikler (0-1 arası progress skoru)")]
    [Tooltip("Bu skoru geçince 2. yıldız yanar")]
    [SerializeField] private float star2Threshold = 0.35f;
    [Tooltip("Bu skoru geçince 3. yıldız yanar")]
    [SerializeField] private float star3Threshold = 0.65f;

    [Header("Animasyon")]
    [SerializeField] private float barSmoothSpeed = 4f;     // bar yumuşak kayar
    [SerializeField] private float markerPopScale = 1.45f;
    [SerializeField] private float markerPopDuration = 0.28f;

    // -----------------------------------------------------------------------

    private BoardController board;
    private TopHudController topHud;

    private float targetValue;
    private float displayValue;
    private int currentStars = 0;
    private bool initialized;

    private Coroutine popCoroutine1, popCoroutine2, popCoroutine3;

    // -----------------------------------------------------------------------

    private void OnEnable()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private void OnDisable()
    {
        Unsubscribe();
        initialized = false;
    }

    private void Update()
    {
        if (!initialized) return;

        // Bar yumuşak kayar
        if (Mathf.Abs(displayValue - targetValue) > 0.001f)
        {
            displayValue = Mathf.Lerp(displayValue, targetValue, Time.deltaTime * barSmoothSpeed);
            if (progressSlider != null)
                progressSlider.value = displayValue;
        }
    }

    // -----------------------------------------------------------------------

    private IEnumerator InitializeWhenReady()
    {
        if (board == null)
            board = FindFirstObjectByType<BoardController>();
        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>();

        while (board == null || topHud == null || board.ActiveLevelData == null)
            yield return null;

        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.interactable = false;
        }

        displayValue = 0f;
        targetValue  = 0f;
        currentStars = 0;

        SetMarkersOff();
        Subscribe();
        Refresh();
        initialized = true;
    }

    private void Subscribe()
    {
        Unsubscribe();
        board.OnMovesChanged          += OnMovesChanged;
        board.OnTilesCleared          += OnTilesCleared;
        board.OnObstacleDestroyed     += OnObstacleDestroyed;
        topHud.OnGoalsCompletionChanged += OnGoalsChanged;
    }

    private void Unsubscribe()
    {
        if (board != null)
        {
            board.OnMovesChanged          -= OnMovesChanged;
            board.OnTilesCleared          -= OnTilesCleared;
            board.OnObstacleDestroyed     -= OnObstacleDestroyed;
        }
        if (topHud != null)
            topHud.OnGoalsCompletionChanged -= OnGoalsChanged;
    }

    private void OnMovesChanged(int _)              => Refresh();
    private void OnTilesCleared(TileType _, int __)  => Refresh();
    private void OnObstacleDestroyed(int _, ObstacleId __) => Refresh();
    private void OnGoalsChanged(bool _)              => Refresh();

    // -----------------------------------------------------------------------

    private void Refresh()
    {
        if (board == null || board.ActiveLevelData == null) return;

        float score = CalculateScore();
        targetValue = Mathf.Clamp01(score);

        int newStars = ScoreToStars(score);
        if (newStars != currentStars)
        {
            bool gained = newStars > currentStars;
            currentStars = newStars;
            UpdateMarkers(gained);
            UpdateBarColor();
        }
    }

    /// <summary>
    /// 0→1 progress skoru. Hedefler ve hamleler birlikte değerlendirilir.
    /// Tüm hedefler bitince son %34'lük dilim hamle oranına göre dolar.
    /// </summary>
    private float CalculateScore()
    {
        float goalRatio = topHud != null ? topHud.GetGoalProgressRatio() : 0f;

        int totalMoves = board.ActiveLevelData.moves;
        float moveRatio = totalMoves > 0
            ? Mathf.Clamp01((float)board.RemainingMoves / totalMoves)
            : 0f;

        if (goalRatio >= 1f)
        {
            // Hedefler tamam: son dilim hamle durumuna göre 0.66→1 arasına map'le
            return Mathf.Lerp(star3Threshold, 1f, moveRatio);
        }

        // Hedefler devam ediyor: 0→star3Threshold arasına map'le
        float combined = goalRatio * Mathf.Lerp(0.5f, 1f, moveRatio);
        return Mathf.Lerp(0f, star3Threshold, combined);
    }

    private int ScoreToStars(float score)
    {
        if (score >= star3Threshold) return 3;
        if (score >= star2Threshold) return 2;
        return 1;
    }

    // -----------------------------------------------------------------------
    // Görsel

    private void UpdateBarColor()
    {
        if (fillImage == null) return;

        if (currentStars >= 3)
            fillImage.color = colorStar3;
        else if (currentStars >= 2)
            fillImage.color = colorStar2;
        else
            fillImage.color = colorStar1;
    }

    private void UpdateMarkers(bool gained)
    {
        SetMarker(starMarker1, currentStars >= 1, gained && currentStars == 1, ref popCoroutine1);
        SetMarker(starMarker2, currentStars >= 2, gained && currentStars == 2, ref popCoroutine2);
        SetMarker(starMarker3, currentStars >= 3, gained && currentStars == 3, ref popCoroutine3);
    }

    private void SetMarkersOff()
    {
        SetMarkerSprite(starMarker1, false);
        SetMarkerSprite(starMarker2, false);
        SetMarkerSprite(starMarker3, false);
    }

    private void SetMarker(Image marker, bool on, bool pop, ref Coroutine co)
    {
        if (marker == null) return;
        SetMarkerSprite(marker, on);

        if (pop)
        {
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(CoPop(marker.transform));
        }
    }

    private void SetMarkerSprite(Image marker, bool on)
    {
        if (marker == null) return;

        if (on && markerOnSprite != null)
            marker.sprite = markerOnSprite;
        else if (!on && markerOffSprite != null)
            marker.sprite = markerOffSprite;

        var c = marker.color;
        c.a = on ? 1f : 0.4f;
        marker.color = c;
    }

    private IEnumerator CoPop(Transform t)
    {
        Vector3 original = Vector3.one;
        Vector3 peak = Vector3.one * markerPopScale;
        float half = markerPopDuration * 0.5f;
        float e = 0f;

        while (e < half)
        {
            e += Time.unscaledDeltaTime;
            t.localScale = Vector3.LerpUnclamped(original, peak, e / half);
            yield return null;
        }
        e = 0f;
        while (e < half)
        {
            e += Time.unscaledDeltaTime;
            t.localScale = Vector3.LerpUnclamped(peak, original, e / half);
            yield return null;
        }
        t.localScale = original;
    }
}
