using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Yolculuk ekranı. Aktif bölümün (tamir görevleriyle oluşan resim) büyük kartını ve
/// bir sonraki bölümün önizlemesini gösterir. "İzle" → onWatch event'i (level aç / resmi göster).
/// </summary>
public sealed class JourneyScreenController : MonoBehaviour
{
    /// <summary>UnityEvent&lt;int&gt; serialize edilebilsin diye concrete alt-sınıf.</summary>
    [System.Serializable] public sealed class IntEvent : UnityEvent<int> { }

    [Header("Veri & Tema")]
    [SerializeField] private JourneyCatalog catalog;
    [SerializeField] private UITheme theme;

    [Header("Kartlar")]
    [SerializeField] private JourneyChapterCard currentCard;
    [SerializeField] private JourneyChapterCard nextCard;

    [Header("Olay")]
    [Tooltip("'İzle'ye basınca tetiklenir — bölüm numarasını verir. Level açma/resmi gösterme buraya.")]
    [SerializeField] private IntEvent onWatch;

    private void OnEnable() => Build();

    private void Build()
    {
        if (catalog == null || catalog.chapters == null || catalog.chapters.Count == 0) return;

        int count = catalog.chapters.Count;
        int idx = Mathf.Clamp(JourneyCatalog.CurrentIndex, 0, count - 1);

        if (currentCard != null)
            currentCard.Bind(catalog.chapters[idx], theme, isPreview: false, HandleWatch);

        if (nextCard != null)
        {
            bool hasNext = idx + 1 < count;
            nextCard.gameObject.SetActive(hasNext);
            if (hasNext)
                nextCard.Bind(catalog.chapters[idx + 1], theme, isPreview: true, null);
        }
    }

    private void HandleWatch(JourneyChapter chapter)
    {
        if (chapter != null) onWatch?.Invoke(chapter.chapterNumber);
    }
}
