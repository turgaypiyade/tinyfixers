using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liderlik Panosu ekranı. Sekmeler (Haftalık/Arkadaşlar/Oyuncular/Takım) arasında geçiş yapar,
/// servisin verdiği sıralı listeyi satır prefab'ı ile basar. v1'de MockLeaderboardService.
/// </summary>
public sealed class LeaderboardScreenController : MonoBehaviour
{
    [Serializable]
    public sealed class TabButton
    {
        public LeaderboardTab tab;
        public Button button;
        public Image highlight;   // seçili sekme vurgusu (opsiyonel)
    }

    [Header("Veri & Tema")]
    [SerializeField] private UITheme theme;

    [Header("Liste")]
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private LeaderboardRow rowPrefab;
    [Tooltip("Üstte sabit (pinlenmiş) kendi satırın — scroll'dan bağımsız, her zaman görünür.")]
    [SerializeField] private LeaderboardRow selfRow;

    [Header("Sekmeler")]
    [SerializeField] private List<TabButton> tabButtons = new();

    [Header("Üst bilgi")]
    [SerializeField] private TMP_Text timeLabelText;

    [Header("Görsel")]
    [Tooltip("Avatarı olmayan girdilere isme göre deterministik dağıtılan mock avatar havuzu.")]
    [SerializeField] private Sprite[] avatarPool;

    private ILeaderboardService service;
    private readonly List<LeaderboardRow> rows = new();
    private LeaderboardTab current = LeaderboardTab.Weekly;
    private bool wired;

    private void OnEnable()
    {
        service ??= BackendServices.Leaderboard;
        service.OnChanged += Render;
        WireTabs();
        Build(current);
    }

    private void OnDisable()
    {
        if (service != null) service.OnChanged -= Render;
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
        wired = true;
    }

    // Sekme seç → async yüklemeyi tetikle + mevcut cache'i hemen göster.
    private void Build(LeaderboardTab tab)
    {
        current = tab;
        service?.Fetch(tab);      // async; sonuç OnChanged → Render ile gelir
        Render();                 // eldeki cache'i hemen bas (ilk açılışta boş olabilir)
    }

    // Servisten haber gelince (veya sekme değişince) mevcut sekmeyi yeniden bas.
    private void Render()
    {
        foreach (var r in rows) if (r != null) Destroy(r.gameObject);
        rows.Clear();

        var entries = service?.GetEntries(current);

        // Kendi satırını en üste sabitle (pin); scroll listesinde tekrarlamа.
        var self = entries?.Find(e => e.isSelf);
        if (selfRow != null)
        {
            selfRow.gameObject.SetActive(self != null);
            if (self != null) { EnsureAvatar(self); selfRow.Bind(self, theme); }
        }

        if (entries != null && contentContainer != null && rowPrefab != null)
        {
            foreach (var entry in entries)
            {
                if (entry.isSelf) continue;   // pinlenmiş, listede tekrar etme
                EnsureAvatar(entry);
                var row = Instantiate(rowPrefab, contentContainer);
                row.Bind(entry, theme);
                rows.Add(row);
            }
        }

        if (timeLabelText != null)
        {
            string label = service?.GetTimeLabel(current) ?? "";
            timeLabelText.text = label;
            timeLabelText.gameObject.SetActive(!string.IsNullOrEmpty(label));
        }

        UpdateTabHighlights();
    }

    // Avatarı olmayan girdiye havuzdan isme göre deterministik avatar ata
    // (aynı isim her render'da aynı robotu alsın).
    private void EnsureAvatar(LeaderboardEntry entry)
    {
        if (entry == null || entry.avatar != null) return;
        if (avatarPool == null || avatarPool.Length == 0) return;

        int hash = string.IsNullOrEmpty(entry.playerName) ? 0 : Mathf.Abs(entry.playerName.GetHashCode());
        entry.avatar = avatarPool[hash % avatarPool.Length];
    }

    private void UpdateTabHighlights()
    {
        foreach (var t in tabButtons)
        {
            if (t?.highlight != null) t.highlight.enabled = t.tab == current;
        }
    }
}
