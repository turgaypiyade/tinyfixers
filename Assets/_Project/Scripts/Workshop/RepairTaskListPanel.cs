using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Repair (Onar) butonundan açılan task list paneli.
/// Aynı anda sadece N (varsayılan 3) görevi gösterir.
/// Üstteki aktif, altındakiler preview/kilitli.
/// Aktif görev tamamlanınca: kaybolur, alttakiler yukarı kayar, en alta yeni bir tane gelir.
/// </summary>
public class RepairTaskListPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorkshopController workshop;
    [SerializeField] private RepairTaskItem itemPrefab;
    [SerializeField] private RectTransform itemsContainer;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundDismissButton; // boşluğa tıklayarak kapatmak için (opsiyonel)
    [SerializeField] private GameObject allCompletedMessage;  // tüm görevler bittiğinde gösterilecek panel
    [SerializeField] private RepairProgressBar progressBar;   // üstteki ilerleme barı (opsiyonel)

    [Header("Layout")]
    [Tooltip("Aynı anda görünen task sayısı (genelde 3).")]
    [SerializeField, Min(1)] private int visibleSlotCount = 3;
    [Tooltip("Item RectTransform'unun yükseklik değeri (sizeDelta.y).")]
    [SerializeField, Min(20f)] private float itemHeight = 160f;
    [Tooltip("Itemler arası boşluk.")]
    [SerializeField, Min(0f)]  private float itemSpacing = 12f;
    [Tooltip("İlk slot'un (en üstteki) anchoredPosition.y değeri. " +
             "ItemsContainer'ın pivot/anchor ayarına göre 0 ya da başka bir değer olabilir.")]
    [SerializeField] private float firstSlotY = 0f;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float slideDuration     = 0.35f;
    [SerializeField, Min(0.05f)] private float completeDuration  = 0.25f;
    [SerializeField, Min(0.05f)] private float panelFadeDuration = 0.20f;
    [SerializeField, Min(0f)]    private float newItemEnterOffset = 100f;

    private readonly List<RepairTaskItem> items = new();
    private bool isAnimating;

    private float SlotY(int slotIndex) => firstSlotY - slotIndex * (itemHeight + itemSpacing);

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backgroundDismissButton != null) backgroundDismissButton.onClick.AddListener(Close);

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ─── Public API ─────────────────────────────────────────────────────────

    public void Open()
    {
        if (workshop == null || workshop.StageData == null)
        {
            Debug.LogWarning("[RepairPanel] Workshop veya StageData atanmamış.");
            return;
        }

        if (panelRoot != null) panelRoot.SetActive(true);
        if (panelGroup != null) panelGroup.alpha = 0f;

        RebuildItemsInstant();
        if (progressBar != null) progressBar.ApplyInstant();
        StartCoroutine(FadePanel(0f, 1f, panelFadeDuration));
    }

    public void Close()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;
        StartCoroutine(CloseRoutine());
    }

    /// Aktif (üstteki) task'a tıklanınca çağrılır. RepairTaskItem button'unun listener'ı.
    public void OnActiveTaskClicked()
    {
        if (isAnimating) return;
        if (workshop == null) return;

        if (!workshop.TryRepairCurrent())
        {
            // Yetersiz yıldız veya başka bir sebep (transition vs.) — sessiz başarısızlık.
            // İleride yetersiz yıldız feedback (shake / popup) eklenebilir.
            return;
        }

        StartCoroutine(AnimateShift());
    }

    // ─── Build / Rebuild ────────────────────────────────────────────────────

    private void RebuildItemsInstant()
    {
        foreach (var it in items) if (it != null) Destroy(it.gameObject);
        items.Clear();

        var stages = workshop.StageData.stages;
        int currentStage = workshop.CurrentStage;

        if (allCompletedMessage != null)
            allCompletedMessage.SetActive(currentStage >= stages.Count);

        for (int slot = 0; slot < visibleSlotCount; slot++)
        {
            int stageIndex = currentStage + slot;
            if (stageIndex >= stages.Count) break;

            var item = Instantiate(itemPrefab, itemsContainer);
            item.SetInstantY(SlotY(slot));
            item.Bind(stageIndex, stages[stageIndex], this, isActive: slot == 0);
            items.Add(item);
        }
    }

    // ─── Animation ──────────────────────────────────────────────────────────

    private IEnumerator AnimateShift()
    {
        isAnimating = true;

        // 1. En üstteki görevi tamamlanma animasyonu ile yok et.
        if (items.Count > 0 && items[0] != null)
        {
            yield return items[0].PlayCompleteAnimation(completeDuration);
            Destroy(items[0].gameObject);
            items.RemoveAt(0);
        }

        // 2. Kalan itemleri bir slot yukarı kaydır (paralel).
        var slides = new List<Coroutine>();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            slides.Add(StartCoroutine(items[i].SlideToY(SlotY(i), slideDuration)));
        }
        foreach (var co in slides) yield return co;

        // 3. Aktif state'leri yenile (yeni 0. slot artık aktif).
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                items[i].RefreshActiveState(i == 0);
        }

        // 4. En alta yeni görev ekle (varsa).
        var stages = workshop.StageData.stages;
        int nextNewStageIndex = workshop.CurrentStage + items.Count;
        if (nextNewStageIndex < stages.Count && items.Count < visibleSlotCount)
        {
            var newItem = Instantiate(itemPrefab, itemsContainer);
            newItem.Bind(nextNewStageIndex, stages[nextNewStageIndex], this, isActive: false);

            int targetSlot = items.Count;
            items.Add(newItem);

            yield return newItem.SlideInFromBelow(SlotY(targetSlot), newItemEnterOffset, slideDuration);
        }

        // 5. Tüm görevler bittiyse bilgilendirme paneli.
        if (allCompletedMessage != null && items.Count == 0)
            allCompletedMessage.SetActive(true);

        isAnimating = false;
    }

    private IEnumerator CloseRoutine()
    {
        yield return FadePanel(panelGroup != null ? panelGroup.alpha : 1f, 0f, panelFadeDuration);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private IEnumerator FadePanel(float from, float to, float dur)
    {
        if (panelGroup == null) yield break;

        panelGroup.alpha = from;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        panelGroup.alpha = to;
    }
}
