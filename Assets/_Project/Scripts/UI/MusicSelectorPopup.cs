using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Müzik seçici popup (profil sayfasından açılır). Kütüphanedeki parçaları listeler:
/// her satır isim + durum butonu — "Seçili" / "Seç" (sahipse) / "100 <altın ikonu>" (kilitli).
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
        public Button actionButton;
    }

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
        MusicState.OnChanged -= RefreshRows;
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
            row.actionLabel.text = isSel ? "Seçili" : owned ? "Seç" : $"{MusicState.TrackCostCoins}<space=0.3em><sprite name=\"goldmoney\">";
            row.actionButton.interactable = !isSel;
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
        CommonPopupView.Region((RectTransform)transform, new Rect(0, 0, 1, 1));
        gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);
        var view = CommonPopupView.Create(transform, "Müzik Seç", Close);
        var done = CommonPopupView.Button(view.Actions, "BtnContinue", "Tamam", Close);
        CommonPopupView.Region((RectTransform)done.transform, CommonPopupView.ActionRegion(0, 1));

        var scroll = CommonPopupView.NewRect(view.Body, "Scroll", Vector2.zero);
        CommonPopupView.Region(scroll, new Rect(0, 0, 1, 1));
        // Transparent graphic receives drags even between rows.
        scroll.gameObject.AddComponent<Image>().color = Color.clear;
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        var viewport = CommonPopupView.NewRect(scroll, "Viewport", Vector2.zero);
        CommonPopupView.Region(viewport, new Rect(0, 0, 1, 1));
        viewport.gameObject.AddComponent<RectMask2D>();
        listRoot = CommonPopupView.NewRect(viewport, "Content", Vector2.zero);
        listRoot.anchorMin = new Vector2(0, 1);
        listRoot.anchorMax = Vector2.one;
        listRoot.pivot = new Vector2(0.5f, 1);
        listRoot.anchoredPosition = Vector2.zero;
        var layout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.padding = new RectOffset(0, 0, 8, 8);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        listRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = viewport;
        sr.content = listRoot;
        for (int i = 0; i < library.Count; i++) BuildTrackRow(i);
    }

    private void BuildTrackRow(int id)
    {
        var track = library.Get(id);
        var row = CommonPopupView.NewRect(listRoot, "Track_" + id, new Vector2(0, 116));
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 116;
        row.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.14f, 0.08f, 0.08f);
        var name = CommonPopupView.Text(row, "Name", track != null ? track.displayName : "Parça " + id,
            30, CommonPopupSkin.Shared.bodyTextColor);
        CommonPopupView.Region(name.rectTransform, new Rect(0.025f, 0.08f, 0.55f, 0.84f));
        name.alignment = TextAlignmentOptions.Left;
        var button = CommonPopupView.Button(row, "Action", "", () => OnRowClicked(id));
        CommonPopupView.Region((RectTransform)button.transform, new Rect(0.60f, 0.12f, 0.38f, 0.76f));
        var label = button.GetComponentInChildren<TMP_Text>();
        label.spriteAsset = CommonPopupSkin.Shared.coinSpriteAsset;
        rows.Add(new TrackRow
        {
            id = id,
            actionLabel = label,
            actionButton = button
        });
    }
}
