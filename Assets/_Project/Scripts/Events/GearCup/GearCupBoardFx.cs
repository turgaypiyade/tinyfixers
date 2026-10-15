using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>At most three pooled blue-gear labels. Bursts merge without changing earned points.</summary>
public sealed class GearCupBoardFx : MonoBehaviour
{
    private const string PoolKey = "GearCupBlueGainLabel";
    private const int Capacity = 3;
    private const float Lifetime = 0.7f, Travel = 55f, MergeDistance = 125f;
    private const float LabelScale = 1.25f;
    private readonly Vector3[] positionSums = new Vector3[4];
    private readonly int[] buffered = new int[4];
    private readonly GearCupFloatLabel[] active = new GearCupFloatLabel[Capacity];
    private int count;
    private Camera boardCamera;
    private RectTransform root;
    private GearCupPreviewConfig config;
    private bool initialized;

    public static void Install()
    {
        if (!GearCupState.CanEarn || RuntimeSimulationSession.IsActive || Object.FindFirstObjectByType<GearCupBoardFx>() != null) return;
        var go = new GameObject("GearCupBoardFx", typeof(RectTransform));
        go.layer = 5;
        go.AddComponent<GearCupBoardFx>().Build();
    }
    private void Build()
    {
        config = GearCupPreviewConfig.Shared;
        root = (RectTransform)transform;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var board = Object.FindFirstObjectByType<BoardController>();
        var boardCanvas = board != null ? board.GetComponentInParent<Canvas>() : null;
        boardCamera = boardCanvas != null && boardCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? boardCanvas.worldCamera : null;
        // Hold all three rentals before returning them so every slot is warmed once.
        for (int i = 0; i < Capacity; i++) active[i] = Rent();
        for (int i = 0; i < Capacity; i++) { UiVfxPool.Return(PoolKey, active[i].gameObject); active[i] = null; }
        initialized = true;
        Subscribe();
        StateChanged();
    }
    private void OnEnable() { if (initialized) Subscribe(); }
    private void Subscribe()
    {
        GameEventBus.OnTileClearedAt -= Position;
        GearCupState.Gained -= Gain;
        GearCupState.Changed -= StateChanged;
        GameEventBus.OnTileClearedAt += Position;
        GearCupState.Gained += Gain;
        GearCupState.Changed += StateChanged;
    }
    private static int Index(TileType t) => t == TileType.Gear ? 0 : t == TileType.Core ? 1 : t == TileType.Bolt ? 2 : t == TileType.Plate ? 3 : -1;
    private void Position(TileType type, Vector3 world)
    {
        int index = Index(type);
        if (!GearCupState.Tracking || index < 0 || !GearCupRules.Counts(type, config.onlyYellowTiles)) return;
        positionSums[index] += world;
        buffered[index]++;
    }
    private void Gain(TileType type, int amount)
    {
        int index = Index(type);
        if (index < 0 || amount <= 0) return;
        int samples = buffered[index];
        Vector3 world = samples > 0 ? positionSums[index] / samples : Vector3.zero;
        buffered[index] = 0; positionSums[index] = Vector3.zero;
        // Counts can arrive without a position hint. Do not invent a counter/HUD fallback.
        if (samples == 0) return;
        var screen = RectTransformUtility.WorldToScreenPoint(boardCamera, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
        var origin = local + new Vector2(18, 22);

        int nearest = -1;
        float distance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            float candidate = (active[i].rect.anchoredPosition - origin).sqrMagnitude;
            if (candidate < distance) { distance = candidate; nearest = i; }
        }
        GearCupFloatLabel label;
        if (nearest >= 0 && (count == Capacity || distance <= MergeDistance * MergeDistance))
        {
            label = active[nearest];
            // Keep the current visual position, and renew the fade so the merged amount is readable.
            label.origin = label.rect.anchoredPosition;
            label.amount += amount;
        }
        else
        {
            label = Rent(); label.origin = origin; label.amount = amount;
            active[count++] = label;
        }
        label.age = 0;
        label.rect.anchoredPosition = label.origin;
        label.text.SetText("+{0}", label.amount);
        label.group.alpha = 1;
    }
    private GearCupFloatLabel Rent()
    {
        var go = UiVfxPool.RentRect(PoolKey, root, "GearGain");
        var label = go.GetComponent<GearCupFloatLabel>();
        if (label == null)
        {
            label = go.AddComponent<GearCupFloatLabel>();
            label.rect = (RectTransform)go.transform;
            label.group = go.AddComponent<CanvasGroup>(); label.group.blocksRaycasts = false;
            var bgRect = CommonPopupView.NewRect(go.transform, "BlueGear", Vector2.zero);
            CommonPopupView.Region(bgRect, new Rect(0, 0, 1, 1));
            var background = bgRect.gameObject.AddComponent<Image>();
            background.sprite = config.gear;
            background.material = config.gainLabelMaterial;
            background.preserveAspect = true;
            background.raycastTarget = false;
            label.text = CommonPopupView.Text(go.transform, "Amount", "", 29, Color.white);
            label.text.font = config.headingFont;
            label.text.textWrappingMode = TextWrappingModes.NoWrap;
            label.text.raycastTarget = false;
            label.text.enableAutoSizing = true;
            label.text.fontSizeMin = 18;
            label.text.fontSizeMax = 29;
            label.text.rectTransform.anchoredPosition = Vector2.zero;
            label.text.rectTransform.sizeDelta = new Vector2(62, 46);
        }
        label.rect.sizeDelta = new Vector2(76, 76);
        label.rect.localScale = Vector3.one * LabelScale;
        return label;
    }
    private void StateChanged() { if (!GearCupState.Tracking) Clear(); }
    private void Update()
    {
        for (int i = count - 1; i >= 0; i--)
        {
            var label = active[i];
            label.age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(label.age / Lifetime);
            label.rect.anchoredPosition = label.origin + Vector2.up * (Travel * t);
            label.group.alpha = t < 0.5f ? 1 : (1 - t) * 2;
            if (t < 1) continue;
            UiVfxPool.Return(PoolKey, label.gameObject);
            active[i] = active[--count]; active[count] = null;
        }
    }
    private void Clear()
    {
        for (int i = 0; i < count; i++) { if (active[i] != null) UiVfxPool.Return(PoolKey, active[i].gameObject); active[i] = null; }
        count = 0;
        for (int i = 0; i < buffered.Length; i++) { buffered[i] = 0; positionSums[i] = Vector3.zero; }
    }
    private void OnDisable()
    {
        GameEventBus.OnTileClearedAt -= Position; GearCupState.Gained -= Gain; GearCupState.Changed -= StateChanged;
        Clear();
    }
}
