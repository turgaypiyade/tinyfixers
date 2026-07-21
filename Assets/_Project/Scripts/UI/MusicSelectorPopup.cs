using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Müzik seçici popup (profil sayfasından açılır). Kütüphanedeki parçaları listeler:
/// her satır isim + durum butonu — "Seçili" / "Seç" (sahipse) / "100 🪙" (kilitli).
/// Kilitli parçaya basınca 100 altın harcanır (MusicState.TryUnlock), açılır ve çalar;
/// yetersiz altında satır "Yetersiz altın" uyarır. UI tamamen RUNTIME kurulur
/// (SaveProgressPopup deseni) — sahne bağımlılığı yok.
/// </summary>
public sealed class MusicSelectorPopup : MonoBehaviour
{
    private MusicLibrary library;
    private RectTransform listRoot;
    private readonly List<TrackRow> rows = new();

    private sealed class TrackRow
    {
        public int id;
        public TMP_Text actionLabel;
        public Image actionBg;
    }

    private static readonly Color SelectedGreen = new Color(0.3f, 0.65f, 0.35f);
    private static readonly Color OwnedBlue     = new Color(0.25f, 0.45f, 0.7f);
    private static readonly Color LockedAmber   = new Color(0.9f, 0.62f, 0.15f);

    public static void Show(Transform parentCanvas, MusicLibrary library)
    {
        if (library == null || library.Count == 0)
        {
            Debug.LogWarning("[MusicSelector] MusicLibrary atanmamış/boş.");
            return;
        }

        MusicState.Library = library;

        var existing = parentCanvas.GetComponentInChildren<MusicSelectorPopup>(true);
        if (existing == null)
        {
            var go = new GameObject("MusicSelectorPopup", typeof(RectTransform));
            go.transform.SetParent(parentCanvas, false);
            go.layer = parentCanvas.gameObject.layer;   // Screen Space Camera culling tuzağı
            existing = go.AddComponent<MusicSelectorPopup>();
            existing.library = library;
            existing.Build();
        }
        existing.Open();
    }

    private void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshRows();
        MusicState.OnChanged += RefreshRows;
    }

    private void OnDisable() => MusicState.OnChanged -= RefreshRows;
    private void Close() => gameObject.SetActive(false);

    private void RefreshRows()
    {
        int selected = MusicState.SelectedTrack;
        foreach (var row in rows)
        {
            bool isSel = row.id == selected;
            bool owned = MusicState.IsOwned(row.id);
            row.actionLabel.text = isSel ? "Seçili" : owned ? "Seç" : $"{MusicState.TrackCostCoins} 🪙";
            row.actionBg.color = isSel ? SelectedGreen : owned ? OwnedBlue : LockedAmber;
        }
    }

    private void OnRowClicked(int id)
    {
        if (MusicState.IsOwned(id)) { MusicState.Select(id); return; }

        if (!MusicState.TryUnlock(id))   // 100 altın harca (başarıda seçer de)
            FlashInsufficient(id);
    }

    private void FlashInsufficient(int id)
    {
        var row = rows.Find(r => r.id == id);
        if (row != null) row.actionLabel.text = "Yetersiz altın";
    }

    // ── Runtime UI ──────────────────────────────────────────────────

    private void Build()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var scrim = gameObject.AddComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.72f);
        var scrimBtn = gameObject.AddComponent<Button>();
        scrimBtn.transition = Selectable.Transition.None;
        scrimBtn.onClick.AddListener(Close);

        var card = NewRect("Card", transform, new Vector2(760, 900));
        var cardImg = card.gameObject.AddComponent<Image>();
        cardImg.color = new Color(0.16f, 0.22f, 0.42f, 0.98f);

        var title = NewText("Title", card, "Müzik Seç", 46, FontStyles.Bold, new Vector2(680, 70));
        Top(title.rectTransform, 70, 24);

        var closeBtn = NewButton("Close", card, "✕", new Color(0.75f, 0.2f, 0.2f), new Vector2(84, 84), out _);
        var crt = (RectTransform)closeBtn.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-10, -10);
        closeBtn.onClick.AddListener(Close);

        // Kayan liste (parça sayısı çoksa)
        var scroll = NewRect("Scroll", card, new Vector2(700, 720));
        Top(scroll, 720, 120);
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
        var viewport = NewRect("Viewport", scroll, Vector2.zero);
        StretchFull(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        listRoot = NewRect("Content", viewport, Vector2.zero);
        listRoot.anchorMin = new Vector2(0, 1); listRoot.anchorMax = new Vector2(1, 1);
        listRoot.pivot = new Vector2(0.5f, 1); listRoot.anchoredPosition = Vector2.zero;
        var vlg = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 16; vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        var csf = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = viewport; sr.content = listRoot;

        for (int i = 0; i < library.Count; i++)
            BuildTrackRow(i);
    }

    private void BuildTrackRow(int id)
    {
        var track = library.Get(id);
        var rowRt = NewRect("Track_" + id, listRoot, new Vector2(680, 110));
        rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;
        var rowBg = rowRt.gameObject.AddComponent<Image>();
        rowBg.color = new Color(1f, 1f, 1f, 0.08f);

        var name = NewText("Name", rowRt, track != null ? track.displayName : "Parça " + id,
            32, FontStyles.Bold, new Vector2(400, 60));
        var nrt = name.rectTransform;
        nrt.anchorMin = new Vector2(0, 0.5f); nrt.anchorMax = new Vector2(0, 0.5f);
        nrt.pivot = new Vector2(0, 0.5f); nrt.anchoredPosition = new Vector2(28, 0);
        name.alignment = TextAlignmentOptions.Left;

        var actionBtn = NewButton("Action", rowRt, "", OwnedBlue, new Vector2(200, 80), out var actionLabel);
        var art = (RectTransform)actionBtn.transform;
        art.anchorMin = art.anchorMax = new Vector2(1, 0.5f); art.pivot = new Vector2(1, 0.5f);
        art.anchoredPosition = new Vector2(-24, 0);
        int captured = id;
        actionBtn.onClick.AddListener(() => OnRowClicked(captured));

        rows.Add(new TrackRow { id = id, actionLabel = actionLabel, actionBg = (Image)actionBtn.targetGraphic });
    }

    // ── küçük UGUI yardımcıları (SaveProgressPopup ile aynı) ────────

    private RectTransform NewRect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        return rt;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style, Vector2 sz)
    {
        var rt = NewRect(name, parent, sz);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private Button NewButton(string name, Transform parent, string label, Color color, Vector2 size, out TMP_Text lbl)
    {
        var rt = NewRect(name, parent, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        lbl = NewText("Label", rt, label, 28, FontStyles.Bold, size);
        StretchFull(lbl.rectTransform);
        return btn;
    }

    private static void Top(RectTransform rt, float height, float y)
    {
        rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -y);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
    }
}
