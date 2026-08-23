using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Yolculuk ekranı. Ada modu (worldMap atanınca): TÜM adalar dikey kaydırmalı liste
/// halinde birer kart olarak dizilir — tamamlanan ada renkli, tamamlanmayan GRİ + kilitli.
/// Kart kutusu resmin en-boy oranına göre çizilir (9:16 resim → 9:16 kart, tam oturur).
/// Liste ters sıralıdır (ilk ada en altta), açılışta en alta kaydırılır — Royal Match tarzı.
///
/// worldMap boşsa eski katalog akışı (currentCard/nextCard mockup) çalışır.
/// </summary>
public sealed class JourneyScreenController : MonoBehaviour
{
    /// <summary>UnityEvent&lt;int&gt; serialize edilebilsin diye concrete alt-sınıf.</summary>
    [System.Serializable] public sealed class IntEvent : UnityEvent<int> { }

    [Header("Veri & Tema")]
    [SerializeField] private JourneyCatalog catalog;
    [SerializeField] private UITheme theme;

    [Header("Dünya Haritası (adalar)")]
    [Tooltip("Atanırsa TÜM adalar liste halinde kart olur; tamamlanmayanlar gri + kilit. " +
             "Boşsa eski katalog mockup akışı.")]
    [SerializeField] private WorldMapController worldMap;

    [Header("Wonder Modu (atanırsa harikalar liste olur)")]
    [Tooltip("Atanırsa: açılan harikalar tam, açılmamışlar hologram (reveal shader).")]
    [SerializeField] private WonderCatalog wonderCatalog;

    [Header("Ada Listesi (procedural)")]
    [Tooltip("Kartların dizileceği ScrollRect CONTENT'i. Boşsa panel içine otomatik ScrollRect kurulur.")]
    [SerializeField] private RectTransform listContent;
    [Tooltip("Kilitli kartın ortasındaki kilit ikonu.")]
    [SerializeField] private Sprite lockSprite;
    [Tooltip("Üst kenar boşluğu (başlık bandının altından başlasın).")]
    [SerializeField, Min(0f)] private float topMargin = 150f;
    [SerializeField, Min(0f)] private float bottomMargin = 40f;
    [SerializeField, Min(0f)] private float cardSpacing = 70f;
    [Tooltip("Kart genişliği / panel genişliği oranı.")]
    [SerializeField, Range(0.4f, 1f)] private float cardWidthRatio = 0.82f;
    [Tooltip("Kilitli ada görselinin tonu (griye/karanlığa çekme).")]
    [SerializeField] private Color lockedImageTint = new Color(0.42f, 0.42f, 0.46f, 1f);
    [Tooltip("Kart çerçevesinin rengi (metalik gri).")]
    [SerializeField] private Color frameColor = new Color(0.64f, 0.66f, 0.71f, 1f);

    [Header("Kartlar (eski mockup — ada modunda gizlenir)")]
    [SerializeField] private JourneyChapterCard currentCard;
    [SerializeField] private JourneyChapterCard nextCard;

    [Header("Olay")]
    [Tooltip("'İzle'ye basınca tetiklenir — bölüm numarasını verir. Level açma/resmi gösterme buraya.")]
    [SerializeField] private IntEvent onWatch;

    private ScrollRect builtScroll;

    private void OnEnable() => Build();

    private void Build()
    {
        if (TryBuildWonderList()) return;
        if (TryBuildIslandList())
            return;

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

    // ─── Ada listesi ─────────────────────────────────────────────────────────

    // Wonder modu: catalog'daki tüm harikalar; açılan tam, açılmamış hologram.
    private bool TryBuildWonderList()
    {
        if (wonderCatalog == null || wonderCatalog.Count == 0) return false;

        if (currentCard != null) currentCard.gameObject.SetActive(false);
        if (nextCard != null) nextCard.gameObject.SetActive(false);

        var content = EnsureListContent();
        if (content == null) return false;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        float panelW = ((RectTransform)transform).rect.width;
        if (panelW < 10f) panelW = 1080f;
        float cardW = panelW * cardWidthRatio;

        // Ters sıra: son harika üstte, ilk (Mahalron) altta.
        for (int i = wonderCatalog.Count - 1; i >= 0; i--)
        {
            var w = wonderCatalog.Get(i);
            if (w == null) continue;
            BuildWonderCard(content, w, i, cardW);
        }

        if (builtScroll != null) StartCoroutine(SnapToBottomNextFrame());
        return true;
    }

    // Harikanın açılma oranı: tamamlanan=1, aktif=stage/count, gelecek=0 (hologram).
    private float WonderRevealFor(int index)
    {
        int completed = WonderProgress.CompletedCount;
        if (index < completed) return 1f;
        if (index > completed) return 0f;
        var w = wonderCatalog.Get(index);
        int count = w != null ? w.TaskCount : 0;
        return count > 0 ? (float)WonderProgress.CurrentStage / count : 0f;
    }

    private void BuildWonderCard(RectTransform parent, WonderDefinition wonder, int index, float cardW)
    {
        var sprite = wonder.backgroundSprite;
        float aspect = sprite != null && sprite.rect.width > 1f
            ? sprite.rect.height / sprite.rect.width : 16f / 9f;

        const float framePad = 18f;
        float imgH = cardW * aspect;

        var card = NewUiRect($"WonderCard_{index + 1}", parent);
        card.sizeDelta = new Vector2(cardW + framePad * 2f, imgH + framePad * 2f);
        var frame = card.gameObject.AddComponent<Image>();
        if (theme != null && theme.cardBackground != null) { frame.sprite = theme.cardBackground; frame.type = Image.Type.Sliced; }
        frame.color = frameColor;

        var imgRt = NewUiRect("Image", card);
        imgRt.anchorMin = Vector2.zero; imgRt.anchorMax = Vector2.one;
        imgRt.offsetMin = new Vector2(framePad, framePad);
        imgRt.offsetMax = new Vector2(-framePad, -framePad);
        var img = imgRt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.enabled = sprite != null;
        img.preserveAspect = true;

        // Reveal shader: açılma oranına göre (açılmamış = hologram)
        var shader = Shader.Find("UI/WonderReveal");
        if (shader != null && sprite != null)
        {
            var mat = new Material(shader) { name = "JourneyWonderReveal" };
            mat.SetFloat("_Reveal", WonderRevealFor(index));
            img.material = mat;
        }

        string name = string.IsNullOrEmpty(wonder.displayName) ? wonder.wonderId : wonder.displayName;
        BuildPlaque(card, name, new Vector2(0.5f, 1f), 0f, cardW * 0.72f, 92f, bold: true);
    }

    private bool TryBuildIslandList()
    {
        if (worldMap == null) return false;
        var islands = worldMap.Islands;
        if (islands == null || islands.Count == 0) return false;

        // Eski mockup kartları ada modunda görünmesin.
        if (currentCard != null) currentCard.gameObject.SetActive(false);
        if (nextCard != null) nextCard.gameObject.SetActive(false);

        var content = EnsureListContent();
        if (content == null) return false;

        // Yeniden kur (durum değişmiş olabilir: yeni bölge/ada açılmıştır).
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        float panelW = ((RectTransform)transform).rect.width;
        if (panelW < 10f) panelW = 1080f;
        float cardW = panelW * cardWidthRatio;

        // Ters sıra: son ada üstte, ilk ada altta (liste yukarı doğru ilerler).
        for (int i = islands.Count - 1; i >= 0; i--)
        {
            var island = islands[i];
            if (island == null) continue;
            BuildIslandCard(content, island, i, cardW);
        }

        // Açılışta en alta (ilk adaya) kaydır — layout otursun diye frame sonu.
        if (builtScroll != null)
            StartCoroutine(SnapToBottomNextFrame());

        return true;
    }

    private System.Collections.IEnumerator SnapToBottomNextFrame()
    {
        yield return null;
        if (builtScroll != null)
        {
            builtScroll.verticalNormalizedPosition = 0f;
            builtScroll.velocity = Vector2.zero;
        }
    }

    // listContent atanmadıysa: paneli kaplayan ScrollRect + dikey layout'lu content kur.
    private RectTransform EnsureListContent()
    {
        if (listContent != null) return listContent;
        if (builtScroll != null) { listContent = builtScroll.content; return listContent; }

        var scrollGo = new GameObject("IslandScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.layer = gameObject.layer;
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.SetParent(transform, false);
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(0f, bottomMargin);
        scrollRt.offsetMax = new Vector2(0f, -topMargin);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        viewportGo.layer = gameObject.layer;
        var viewportRt = (RectTransform)viewportGo.transform;
        viewportRt.SetParent(scrollRt, false);
        viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero; viewportRt.offsetMax = Vector2.zero;
        var vpImg = viewportGo.GetComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.001f);   // raycast hedefi (drag için), görünmez

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.layer = gameObject.layer;
        var contentRt = (RectTransform)contentGo.transform;
        contentRt.SetParent(viewportRt, false);
        contentRt.anchorMin = new Vector2(0f, 1f); contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero; contentRt.offsetMax = Vector2.zero;

        var layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = cardSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false; layout.childControlHeight = false;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(0, 0, Mathf.RoundToInt(cardSpacing * 0.5f), Mathf.RoundToInt(cardSpacing * 0.5f));

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        builtScroll = scroll;
        listContent = contentRt;
        return contentRt;
    }

    // Tek ada kartı: çerçeve + orana göre resim + üstte ad plaketi + altta "Bölüm N" +
    // kilitliyse gri ton + karartma + kilit ikonu.
    private void BuildIslandCard(RectTransform parent, WorldMapIsland island, int index, float cardW)
    {
        bool unlocked = island.AllRegionsUnlocked;
        var sprite = island.JourneySprite;
        float aspect = sprite != null && sprite.rect.width > 1f
            ? sprite.rect.height / sprite.rect.width
            : 16f / 9f;

        const float framePad = 18f;
        float imgH = cardW * aspect;

        var card = NewUiRect($"IslandCard_{index + 1}", parent);
        card.sizeDelta = new Vector2(cardW + framePad * 2f, imgH + framePad * 2f);
        var frame = card.gameObject.AddComponent<Image>();
        if (theme != null && theme.cardBackground != null)
        {
            frame.sprite = theme.cardBackground;
            frame.type = Image.Type.Sliced;
        }
        frame.color = frameColor;   // metalik gri çerçeve

        // Resim (oran kutuya işlendi — tam oturur).
        var imgRt = NewUiRect("Image", card);
        imgRt.anchorMin = Vector2.zero; imgRt.anchorMax = Vector2.one;
        imgRt.offsetMin = new Vector2(framePad, framePad);
        imgRt.offsetMax = new Vector2(-framePad, -framePad);
        var img = imgRt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.enabled = sprite != null;
        img.preserveAspect = true;
        img.color = unlocked ? Color.white : lockedImageTint;

        if (!unlocked)
        {
            // Karartma + kilit — "üstü kapalı alan".
            var dimRt = NewUiRect("Dim", card);
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = new Vector2(framePad, framePad);
            dimRt.offsetMax = new Vector2(-framePad, -framePad);
            dimRt.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            if (lockSprite != null)
            {
                var lockRt = NewUiRect("Lock", card);
                lockRt.sizeDelta = Vector2.one * (cardW * 0.32f);
                var lockImg = lockRt.gameObject.AddComponent<Image>();
                lockImg.sprite = lockSprite;
                lockImg.preserveAspect = true;
            }
        }

        // Üst plaket: ada adı (kenara oturur, hafif taşar).
        BuildPlaque(card, island.DisplayName, new Vector2(0.5f, 1f), 0f, cardW * 0.72f, 92f, bold: true);
    }

    private void BuildPlaque(RectTransform card, string text, Vector2 anchor, float yOffset, float w, float h, bool bold)
    {
        var rt = NewUiRect("Plaque", card);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(w, h);

        // Dış katman: metalik gri çerçeve (kartla aynı), içte koyu zemin + yazı.
        var bg = rt.gameObject.AddComponent<Image>();
        if (theme != null && theme.cardBackground != null)
        {
            bg.sprite = theme.cardBackground;
            bg.type = Image.Type.Sliced;
        }
        bg.color = frameColor;

        var innerRt = NewUiRect("Inner", rt);
        innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(8f, 8f); innerRt.offsetMax = new Vector2(-8f, -8f);
        var inner = innerRt.gameObject.AddComponent<Image>();
        if (theme != null && theme.cardBackground != null)
        {
            inner.sprite = theme.cardBackground;
            inner.type = Image.Type.Sliced;
            inner.color = theme.panelSurface;
        }
        else inner.color = new Color(0.24f, 0.2f, 0.45f, 1f);

        var txtRt = NewUiRect("Text", rt);
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(14f, 8f); txtRt.offsetMax = new Vector2(-14f, -8f);
        var tmp = txtRt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f; tmp.fontSizeMax = bold ? 52f : 40f;
        tmp.raycastTarget = false;
        if (theme != null) theme.ApplyText(tmp, theme.textLight, heading: bold);
    }

    private RectTransform NewUiRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;   // Screen Space Camera culling tuzağına düşme
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }
}
