using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liderlik Panosu ekranı (Royal Match düzeni). Sekmeler (Haftalık/Arkadaşlar/Oyuncular/Takım),
/// seçili sekme altındaki toggle bandıyla KAYNAŞIR (aynı yüzey); bandın içinde sekmeye özgü
/// ikili alt-toggle (Dünya/Türkiye, Liste/Ekle) bulunur. Satırlar servisten gelir.
/// Kendi satırın listede görünür sıradaysa inline yeşil; liste dışındaysa (örn 667.) altta sabit.
/// </summary>
public sealed class LeaderboardScreenController : MonoBehaviour
{
    [Serializable]
    public sealed class TabButton
    {
        public LeaderboardTab tab;
        public Button button;
        public Image background;   // seçilince bant rengiyle kaynaşır
        public RectTransform rect;
    }

    [Header("Veri & Görsel")]
    [SerializeField] private UITheme theme;
    [SerializeField] private LeaderboardSkin skin;

    [Header("Liste")]
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private LeaderboardRow rowPrefab;
    [Tooltip("Altta sabit (pinlenmiş) kendi satırın — yalnızca sıran görünür listenin dışındaysa.")]
    [SerializeField] private LeaderboardRow selfRow;

    [Header("Sekmeler & bağlı bant")]
    [SerializeField] private List<TabButton> tabButtons = new();
    [Tooltip("Seçili sekmeyle kaynaşan yüzey; alt-toggle hapları bunun içinde.")]
    [SerializeField] private Image connectedBand;

    [Header("Alt-toggle (bandın içi)")]
    [SerializeField] private Button[] toggleButtons = new Button[2];
    [SerializeField] private Image[]  toggleBackgrounds = new Image[2];
    [SerializeField] private TMP_Text[] toggleLabels = new TMP_Text[2];

    [Header("Dikiş yaması (sekme-bant kaynaşması)")]
    [Tooltip("Seçili sekmenin alt kenarı ile bandın üst çizgisini örten düz dolgu yaması.")]
    [SerializeField] private Image seamCover;
    [Tooltip("Yamanın sekme kenarlarından içeri çekilme payı (px) — sekmenin köşe yuvarlaklığı kadar.")]
    [SerializeField] private float seamPatchInset = 26f;
    [Tooltip("Yamanın toplam yüksekliği (px) — sekme alt bevel'i + bant üst çizgisini örtecek kadar.")]
    [SerializeField] private float seamPatchHeight = 40f;

    [Header("Üst bilgi")]
    [SerializeField] private TMP_Text timeLabelText;
    [SerializeField] private Image timeChip;

    [Header("Ekran zemin & başlık bandı")]
    [Tooltip("Panelin tam-ekran zemin Image'ı (skin.screenBackground buna basılır).")]
    [SerializeField] private Image panelBackground;
    [Tooltip("Üretilen başlık bandı — skin arka planı gömülü band içeriyorsa gizlenir.")]
    [SerializeField] private Image titleBand;

    [Header("Görsel")]
    [Tooltip("Avatarı olmayan girdilere isme göre deterministik dağıtılan mock avatar havuzu.")]
    [SerializeField] private Sprite[] avatarPool;

    [Header("Arkadaş Ekle görünümü (Arkadaşlar sekmesi)")]
    [Tooltip("Liste yerine gösterilen 'Arkadaş Bul + Önerilen Arkadaşlar' kökü (başta kapalı).")]
    [SerializeField] private GameObject addFriendsRoot;
    [SerializeField] private RectTransform suggestionContainer;
    [SerializeField] private FriendSuggestionRow suggestionRowPrefab;
    [SerializeField] private Button findFriendButton;
    [SerializeField] private FindFriendPopup findFriendPopup;
    [Tooltip("Kayan liste alanı — Ekle görünümü açıkken gizlenir.")]
    [SerializeField] private GameObject listAreaRoot;

    private ILeaderboardService service;
    private readonly List<LeaderboardRow> rows = new();
    private readonly List<FriendSuggestionRow> suggestionRows = new();
    private readonly Dictionary<LeaderboardTab, int> subFilterByTab = new();
    private LeaderboardTab current = LeaderboardTab.Weekly;
    private bool wired;

    private void OnEnable()
    {
        service ??= BackendServices.Leaderboard;
        service.OnChanged += Render;
        FriendState.OnChanged += OnFriendsChanged;
        WireTabs();
        Build(current);
    }

    private void OnDisable()
    {
        if (service != null) service.OnChanged -= Render;
        FriendState.OnChanged -= OnFriendsChanged;
    }

    // Arkadaş eklendi/çıktı (öneri kartı veya popup) → arkadaş listesi yeniden yüklensin.
    private void OnFriendsChanged()
    {
        if (current == LeaderboardTab.Friends) service?.Fetch(current);
        Render();
    }

    private void WireTabs()
    {
        if (wired) return;

        foreach (var t in tabButtons)
        {
            if (t?.button == null) continue;
            var captured = t.tab;
            t.button.onClick.AddListener(() => Build(captured));
        }

        for (int i = 0; i < toggleButtons.Length; i++)
        {
            if (toggleButtons[i] == null) continue;
            int captured = i;
            toggleButtons[i].onClick.AddListener(() => SelectSubFilter(captured));
        }

        if (findFriendButton != null && findFriendPopup != null)
            findFriendButton.onClick.AddListener(findFriendPopup.Open);

        wired = true;
    }

    private int CurrentSubFilter
        => subFilterByTab.TryGetValue(current, out int v) ? v : 0;

    private void SelectSubFilter(int index)
    {
        subFilterByTab[current] = index;
        service?.Fetch(current);
        Render();
    }

    // Sekme seç → async yüklemeyi tetikle + mevcut cache'i hemen göster.
    private void Build(LeaderboardTab tab)
    {
        current = tab;
        service?.Fetch(tab);
        Render();
    }

    // Servisten haber gelince (veya sekme/toggle değişince) mevcut görünümü yeniden bas.
    private void Render()
    {
        // Destroy AYNI frame'in sonuna ertelenir → eski satırlar o frame layout/çizimde
        // hayalet olarak kalıp yeni satırlarla üst üste binebilir. Önce kapat + kopar.
        foreach (var r in rows)
        {
            if (r == null) continue;
            r.gameObject.SetActive(false);
            r.transform.SetParent(null, false);
            Destroy(r.gameObject);
        }
        rows.Clear();

        // Arkadaşlar sekmesi: "Ekle" toggle'ı — veya hiç arkadaş yokken "Liste" — öneri
        // görünümünü açar (kullanıcı kuralı: liste boşsa öneriler gelsin, arama hep elde).
        bool addFriendsView = current == LeaderboardTab.Friends
            && (CurrentSubFilter == 1 || !FriendState.HasFriends);
        if (addFriendsRoot != null) addFriendsRoot.SetActive(addFriendsView);
        if (listAreaRoot != null) listAreaRoot.SetActive(!addFriendsView);
        if (!addFriendsView && findFriendPopup != null && findFriendPopup.gameObject.activeSelf)
            findFriendPopup.Close();

        if (addFriendsView)
        {
            if (selfRow != null) selfRow.gameObject.SetActive(false);
            RenderSuggestions();

            if (timeChip != null) timeChip.gameObject.SetActive(false);
            ApplyScreenBackground();
            UpdateTabVisuals();
            UpdateToggleVisuals();
            return;
        }

        var entries = service?.GetEntries(current, CurrentSubFilter);
        var self = entries?.Find(e => e.isSelf);

        // Kendi sıran görünür listede mi? (667 gibi kopuk sıralar altta pinlenir)
        bool selfIsPinned = self != null && entries != null && self.rank > entries.Count;

        if (selfRow != null)
        {
            selfRow.gameObject.SetActive(selfIsPinned);
            if (selfIsPinned)
            {
                EnsureAvatar(self);
                selfRow.Bind(self, theme, skin, current);

                // Pinlemeyi runtime'da ZORLA: sahnedeki instance yanlış/eski anchor'la
                // kalmış olabilir (merkezde asılı "Sen" satırı bug'ı). Her gösterimde alta çivile.
                var srt = (RectTransform)selfRow.transform;
                srt.anchorMin = new Vector2(0f, 0f);
                srt.anchorMax = new Vector2(1f, 0f);
                srt.pivot = new Vector2(0.5f, 0f);
                srt.anchoredPosition = new Vector2(0f, 4f);
                srt.sizeDelta = new Vector2(0f, skin != null ? skin.rowHeight : srt.sizeDelta.y);
                // Kayan liste self row'un ÜSTÜNE binmesin → self row'u en son sibling yap
                // (kardeşleri arasında en üstte render olur).
                srt.SetAsLastSibling();
            }
        }

        if (entries != null && contentContainer != null && rowPrefab != null)
        {
            foreach (var entry in entries)
            {
                if (entry.isSelf && selfIsPinned) continue;   // pinli, listede tekrar etme
                EnsureAvatar(entry);
                var row = Instantiate(rowPrefab, contentContainer);
                row.Bind(entry, theme, skin, current);
                rows.Add(row);
            }

            // Bind satır yüksekliklerini AYNI frame'de değiştiriyor (Weekly top-3 büyük kart);
            // layout'u hemen yeniden hesaplat — yoksa satırlar bayat pozisyonlarla üst üste biner.
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
        }

        if (timeLabelText != null)
        {
            string label = service?.GetTimeLabel(current) ?? "";
            timeLabelText.text = label;
            bool show = !string.IsNullOrEmpty(label);
            if (timeChip != null) timeChip.gameObject.SetActive(show);
            else timeLabelText.gameObject.SetActive(show);
        }

        ApplyScreenBackground();
        UpdateTabVisuals();
        UpdateToggleVisuals();
    }

    // Tam ekran arka plan sprite'ı (title bandı gömülü) atanmışsa onu kullan;
    // üretilen düz başlık bandını gizle. Sprite yoksa eski düz renk düzeni.
    private void ApplyScreenBackground()
    {
        bool hasBg = skin != null && skin.screenBackground != null;

        if (panelBackground != null)
        {
            if (hasBg)
            {
                panelBackground.sprite = skin.screenBackground;
                panelBackground.type = Image.Type.Simple;
                panelBackground.preserveAspect = false;   // tam ekrana esne
                panelBackground.color = Color.white;
            }
            else
            {
                panelBackground.sprite = null;
                panelBackground.color = theme != null ? theme.screenBackground : Color.black;
            }
        }

        if (titleBand != null)
            titleBand.enabled = !hasBg;
    }

    // ── Görsel durumlar ─────────────────────────────────────────────

    // Seçili sekme: bant rengiyle AYNI yüzey (kaynaşma) + hafif yukarı uzar.
    // Seçili olmayan: koyu, bantla ayrık.
    private void UpdateTabVisuals()
    {
        Color bandColor = theme != null ? theme.panelSurface : Color.gray;
        if (connectedBand != null)
        {
            if (skin != null && skin.connectedBand != null)
            {
                connectedBand.sprite = skin.connectedBand;
                connectedBand.type = Image.Type.Sliced;
                connectedBand.color = Color.white;
            }
            else
            {
                connectedBand.sprite = null;
                connectedBand.color = bandColor;
            }
        }

        foreach (var t in tabButtons)
        {
            if (t?.background == null) continue;
            bool selected = t.tab == current;

            Sprite s = selected
                ? (skin != null ? skin.tabSelected : null)
                : (skin != null ? skin.tabUnselected : null);

            if (s != null)
            {
                t.background.sprite = s;
                t.background.type = Image.Type.Sliced;
                t.background.color = Color.white;
            }
            else
            {
                t.background.sprite = theme != null ? theme.cardBackground : null;
                t.background.type = t.background.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                // Seçili = bantla AYNI renk (kaynaşma), değil = koyu.
                t.background.color = selected
                    ? bandColor
                    : (theme != null ? theme.screenBackground : Color.black);
            }

            // Seçili sekme hafif yukarı uzar (RM'deki kalkık görünüm).
            float raise = skin != null ? skin.selectedTabRaise : 14f;
            if (t.rect != null)
                t.rect.offsetMax = new Vector2(t.rect.offsetMax.x, selected ? raise : 0f);
        }

        UpdateSeamCover();
    }

    // Dikiş yaması (RM z-sırası): sekmeler bandın ARKASINDA; bandın üst çizgisi pasif
    // sekmelerin üzerinden geçer. Yama bandın ÇOCUĞU (bandın üstünde çizilir) ve yalnız
    // AKTİF sekmenin X aralığında, bandın ÜST KENARINA ortalanır → o bölgede bandın üst
    // çizgisini (ve sekme dibinin görünen kısmını) örter = kaynaşma.
    private void UpdateSeamCover()
    {
        if (seamCover == null || connectedBand == null) return;

        TabButton sel = null;
        foreach (var t in tabButtons)
            if (t != null && t.tab == current) { sel = t; break; }

        bool show = sel?.rect != null;
        seamCover.gameObject.SetActive(show);
        if (!show) return;

        var rt = seamCover.rectTransform;
        rt.SetParent(connectedBand.rectTransform, false);
        rt.SetAsLastSibling();

        // Ayarlar skin'den (yeniden üretimde ezilmez); skin yoksa controller fallback.
        float inset  = skin != null ? skin.seamPatchInset  : seamPatchInset;
        float height = skin != null ? skin.seamPatchHeight : seamPatchHeight;

        // Bant ve sekme sırası aynı yatay uzayı paylaşır (ikisi de tam genişlik) →
        // aktif sekmenin normalized X aralığı + px offsetleri bire bir kopyalanır.
        // NOT: bant yanlardan taşıyorsa (bandSideOverflow) X'e yarım taşma düzeltmesi gerekir —
        // bandın genişliği ekrandan 2*overflow fazla; anchor uzayı banda göre olduğundan
        // sekmenin ekran-uzayı offsetlerine overflow eklenir.
        float overflow = skin != null ? skin.bandSideOverflow : 0f;
        rt.anchorMin = new Vector2(sel.rect.anchorMin.x, 1f);
        rt.anchorMax = new Vector2(sel.rect.anchorMax.x, 1f);   // bandın üst kenarı (tam üst çizgi)
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(sel.rect.offsetMin.x + inset + overflow * (1f - 2f * sel.rect.anchorMin.x), -height * 0.5f);
        rt.offsetMax = new Vector2(sel.rect.offsetMax.x - inset + overflow * (1f - 2f * sel.rect.anchorMax.x), height * 0.5f);

        seamCover.raycastTarget = false;
        if (skin != null && skin.tabSeamPatch != null)
        {
            seamCover.sprite = skin.tabSeamPatch;
            seamCover.type = Image.Type.Sliced;
            seamCover.color = Color.white;
        }
        else
        {
            // Sprite yoksa bant rengiyle düz yama (renkler zaten flat modda eşit → görünmez dikiş).
            seamCover.sprite = null;
            seamCover.color = theme != null ? theme.panelSurface : Color.gray;
        }
    }

    private void UpdateToggleVisuals()
    {
        string[] labels = service?.GetSubFilters(current) ?? Array.Empty<string>();
        bool hasToggle = labels != null && labels.Length >= 2;
        int active = CurrentSubFilter;

        for (int i = 0; i < toggleButtons.Length; i++)
        {
            if (toggleButtons[i] == null) continue;
            toggleButtons[i].gameObject.SetActive(hasToggle);
            if (!hasToggle) continue;

            if (toggleLabels[i] != null && i < labels.Length)
                toggleLabels[i].text = labels[i];

            bool selected = i == active;
            var bg = toggleBackgrounds[i];
            if (bg == null) continue;

            Sprite s = selected
                ? (skin != null ? skin.togglePillSelected : null)
                : (skin != null ? skin.togglePillUnselected : null);

            if (s != null)
            {
                bg.sprite = s;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.sprite = theme != null ? theme.buttonBackground : null;
                bg.type = bg.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                bg.color = selected
                    ? (theme != null ? theme.accentAmber : Color.yellow)
                    : (theme != null ? theme.screenBackground : Color.gray);
            }
        }
    }

    // "Önerilen Arkadaşlar" listesini yeniden basar. Ekle/X, FriendState'i değiştirir;
    // OnChanged → Render → burası tekrar koşar (eklenen/reddedilen kart düşer).
    private void RenderSuggestions()
    {
        foreach (var r in suggestionRows)
        {
            if (r == null) continue;
            r.gameObject.SetActive(false);
            r.transform.SetParent(null, false);
            Destroy(r.gameObject);
        }
        suggestionRows.Clear();

        if (suggestionContainer == null || suggestionRowPrefab == null) return;

        foreach (var p in FriendDirectory.GetSuggestions(8))
        {
            var captured = p;
            var row = Instantiate(suggestionRowPrefab, suggestionContainer);
            row.Bind(captured, PickPoolAvatar(captured.name), theme,
                onAdd: () => FriendState.AddFriend(captured.name),
                onDismiss: () => FriendState.DismissSuggestion(captured.name));
            suggestionRows.Add(row);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(suggestionContainer);
    }

    private Sprite PickPoolAvatar(string name)
    {
        if (avatarPool == null || avatarPool.Length == 0) return null;
        int hash = string.IsNullOrEmpty(name) ? 0 : Mathf.Abs(name.GetHashCode());
        return avatarPool[hash % avatarPool.Length];
    }

    // Avatarı olmayan girdiye havuzdan isme göre deterministik avatar ata
    // (aynı isim her render'da aynı robotu alsın).
    private void EnsureAvatar(LeaderboardEntry entry)
    {
        if (entry == null || entry.avatar != null) return;

        // Kendi satırın → ProfileScreen'de SEÇTİĞİN avatar (rastgele havuz değil).
        if (entry.isSelf)
        {
            var mine = PlayerAvatarProvider.Current;
            if (mine != null) { entry.avatar = mine; return; }
        }

        if (avatarPool == null || avatarPool.Length == 0) return;
        int hash = string.IsNullOrEmpty(entry.playerName) ? 0 : Mathf.Abs(entry.playerName.GetHashCode());
        entry.avatar = avatarPool[hash % avatarPool.Length];
    }
}
