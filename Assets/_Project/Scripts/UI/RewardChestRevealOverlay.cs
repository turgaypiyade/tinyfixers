using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ödül sandığı açılış töreni — yeniden kullanılabilir tam-ekran overlay.
/// Koddan kurulur (prefab yok), her sahnede çalışır. Akış:
///   karartma → sandık pop-in → sarsılma → açılma (sprite swap + flash)
///   → ödüller sandıktan yay çizerek üst sıraya dizilir → dokun → kapan.
///
/// Kullanım:
///   RewardChestRevealOverlay.Show(rewards, onDone);
/// rewards = DailySlotReward listesi (icon + amount + isim). Sandık görselleri
/// Resources/UI/RewardChestClosed|Opened'dan gelir; istenirse parametreyle ezilir.
/// </summary>
public sealed class RewardChestRevealOverlay : MonoBehaviour
{
    private const string ClosedResourcePath = "UI/RewardChestClosed";
    private const string OpenedResourcePath = "UI/RewardChestOpened";

    private static RewardChestRevealOverlay _instance;

    [Header("Zamanlama (sn)")]
    [SerializeField] private float dimFadeIn      = 0.25f;
    [SerializeField] private float chestPopIn     = 0.30f;
    [SerializeField] private float shakeDuration  = 1.0f;
    [SerializeField] private float openPunch      = 0.18f;
    [SerializeField] private float rewardFly      = 0.45f;
    [SerializeField] private float rewardStagger  = 0.18f;

    private CanvasGroup _group;
    private RectTransform _chest;
    private Image _chestImage;
    private Image _flash;
    private TMP_Text _tapText;
    private RectTransform _rewardRow;

    private Sprite _closedSprite;
    private Sprite _openedSprite;
    private IReadOnlyList<DailySlotReward> _rewards;
    private Action _onDone;
    private bool _finished;
    private bool _tapAllowed;

    // ─────────────────────────────────────────────────────────────────

    public static void Show(IReadOnlyList<DailySlotReward> rewards, Action onDone = null,
                            Sprite chestClosed = null, Sprite chestOpened = null)
    {
        if (_instance != null)
            Destroy(_instance.gameObject);

        var root = new GameObject("RewardChestReveal");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32750;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        var overlay = root.AddComponent<RewardChestRevealOverlay>();
        overlay._rewards = rewards ?? Array.Empty<DailySlotReward>();
        overlay._onDone = onDone;
        overlay._closedSprite = chestClosed != null ? chestClosed : Resources.Load<Sprite>(ClosedResourcePath);
        overlay._openedSprite = chestOpened != null ? chestOpened : Resources.Load<Sprite>(OpenedResourcePath);

        _instance = overlay;
        DontDestroyOnLoad(root);
        overlay.Build();
        overlay.StartCoroutine(overlay.Run());
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        // Yarıda destroy edilirse (sahne değişimi vb.) callback yine çalışsın.
        FinishOnce(invokeOnly: true);
    }

    // ─────────────────────────────────────────────────────────────────

    private void Build()
    {
        // Karartma + tıklama yakalayıcı.
        var dim = NewRect("Dim", transform);
        StretchFull(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.93f);
        dimImg.raycastTarget = true;
        var tapBtn = dim.gameObject.AddComponent<Button>();
        tapBtn.transition = Selectable.Transition.None;
        tapBtn.onClick.AddListener(HandleTap);

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = true;

        // Ödül sırası (sandığın üstünde, ortalanmış yatay dizilim).
        _rewardRow = NewRect("RewardRow", transform);
        _rewardRow.anchorMin = _rewardRow.anchorMax = new Vector2(0.5f, 0.5f);
        _rewardRow.anchoredPosition = new Vector2(0f, 330f);
        _rewardRow.sizeDelta = new Vector2(960f, 240f);
        var row = _rewardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 28f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = false;
        row.childForceExpandWidth = row.childForceExpandHeight = false;

        // Sandık.
        _chest = NewRect("Chest", transform);
        _chest.anchorMin = _chest.anchorMax = new Vector2(0.5f, 0.5f);
        _chest.anchoredPosition = new Vector2(0f, -140f);
        _chest.sizeDelta = new Vector2(520f, 460f);
        _chestImage = _chest.gameObject.AddComponent<Image>();
        _chestImage.sprite = _closedSprite;
        _chestImage.preserveAspect = true;
        _chestImage.raycastTarget = false;
        _chest.localScale = Vector3.zero;

        // Açılış flaşı (tam ekran beyaz, başta görünmez).
        var flashRt = NewRect("Flash", transform);
        StretchFull(flashRt);
        _flash = flashRt.gameObject.AddComponent<Image>();
        _flash.color = new Color(1f, 1f, 1f, 0f);
        _flash.raycastTarget = false;

        // "Devam etmek için dokun".
        var tap = NewRect("TapText", transform);
        tap.anchorMin = new Vector2(0.5f, 0f); tap.anchorMax = new Vector2(0.5f, 0f);
        tap.anchoredPosition = new Vector2(0f, 140f);
        tap.sizeDelta = new Vector2(800f, 70f);
        _tapText = tap.gameObject.AddComponent<TextMeshProUGUI>();
        _tapText.text = "Devam etmek için dokun";
        _tapText.fontSize = 34;
        _tapText.alignment = TextAlignmentOptions.Center;
        _tapText.color = new Color(1f, 1f, 1f, 0f);
        _tapText.raycastTarget = false;
    }

    private IEnumerator Run()
    {
        // 1) Karartma fade-in.
        yield return Animate(dimFadeIn, t => _group.alpha = t);

        // 2) Sandık pop-in (overshoot'lu).
        yield return Animate(chestPopIn, t => _chest.localScale = Vector3.one * EaseOutBack(t));
        _chest.localScale = Vector3.one;

        // 3) Sarsılma — şiddet artarak (kapanışa doğru kriz anı hissi).
        Vector2 home = _chest.anchoredPosition;
        float st = 0f;
        while (st < shakeDuration)
        {
            st += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(st / shakeDuration);
            float amp = Mathf.Lerp(3f, 16f, k * k);
            _chest.anchoredPosition = home + new Vector2(
                (Mathf.PerlinNoise(st * 34f, 0.3f) - 0.5f) * 2f * amp,
                (Mathf.PerlinNoise(0.7f, st * 34f) - 0.5f) * 2f * amp * 0.6f);
            _chest.localEulerAngles = new Vector3(0f, 0f, (Mathf.PerlinNoise(st * 28f, 5f) - 0.5f) * 2f * amp * 0.45f);
            yield return null;
        }
        _chest.anchoredPosition = home;
        _chest.localEulerAngles = Vector3.zero;

        // 4) Açılış: flash + sprite swap + scale punch.
        if (_openedSprite != null) _chestImage.sprite = _openedSprite;
        StartCoroutine(FlashOnce(0.35f));
        yield return Animate(openPunch, t =>
        {
            float s = 1f + 0.22f * Mathf.Sin(t * Mathf.PI);
            _chest.localScale = new Vector3(s, s, 1f);
        });
        _chest.localScale = Vector3.one;

        // 5) Ödüller sandıktan yay çizerek sıraya uçar.
        for (int i = 0; i < _rewards.Count; i++)
        {
            StartCoroutine(FlyReward(_rewards[i]));
            float w = 0f;
            while (w < rewardStagger) { w += Time.unscaledDeltaTime; yield return null; }
        }

        // Son uçuş bitene kadar bekle, sonra dokunmaya izin ver.
        float tail = rewardFly + 0.1f;
        while (tail > 0f) { tail -= Time.unscaledDeltaTime; yield return null; }

        _tapAllowed = true;
        StartCoroutine(PulseTapText());
    }

    // Tek ödül: sandık ağzından çıkar, yay (arc) ile RewardRow'daki yerine uçar.
    private IEnumerator FlyReward(DailySlotReward reward)
    {
        var slot = BuildRewardItem(reward, _rewardRow);

        // Layout yerleşsin ki hedef pozisyon doğru olsun.
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rewardRow);
        yield return null;
        if (slot == null) yield break;

        // Hedefi sakla, başlangıcı sandık ağzına taşı (RewardRow uzayında).
        Vector2 target = slot.anchoredPosition;
        Vector2 chestInRow = (Vector2)_rewardRow.InverseTransformPoint(_chest.TransformPoint(new Vector3(0f, 60f, 0f)));
        slot.anchoredPosition = chestInRow;
        slot.localScale = Vector3.zero;

        float t = 0f;
        while (t < rewardFly)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / rewardFly);
            float e = 1f - Mathf.Pow(1f - k, 3f);

            Vector2 p = Vector2.LerpUnclamped(chestInRow, target, e);
            p.y += Mathf.Sin(k * Mathf.PI) * 120f;   // yay tepesi
            slot.anchoredPosition = p;
            slot.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(k * 1.4f));
            yield return null;
        }

        slot.anchoredPosition = target;
        slot.localScale = Vector3.one;
    }

    // Ödül görseli: ikon + altında "xN" (veya ikon yoksa isim etiketi).
    private RectTransform BuildRewardItem(DailySlotReward reward, Transform parent)
    {
        var item = NewRect("Reward", parent);
        item.sizeDelta = new Vector2(150f, 190f);

        var iconRt = NewRect("Icon", item);
        iconRt.anchorMin = new Vector2(0.5f, 1f); iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta = new Vector2(140f, 140f);
        var icon = iconRt.gameObject.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        if (reward != null && reward.icon != null)
        {
            icon.sprite = reward.icon;
        }
        else
        {
            icon.color = new Color(1f, 0.7f, 0.24f, 1f);   // ikon yoksa amber rozet
        }

        var labelRt = NewRect("Label", item);
        labelRt.anchorMin = new Vector2(0.5f, 0f); labelRt.anchorMax = new Vector2(0.5f, 0f);
        labelRt.pivot = new Vector2(0.5f, 0f);
        labelRt.anchoredPosition = Vector2.zero;
        labelRt.sizeDelta = new Vector2(170f, 46f);
        var label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
        int amount = reward != null ? Mathf.Max(1, reward.amount) : 1;
        string name = reward != null && !string.IsNullOrEmpty(reward.fallbackName) ? reward.fallbackName : "";
        label.text = reward != null && reward.icon != null ? $"x{amount}" : $"{name} x{amount}";
        label.fontSize = 34;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.9f, 0.55f, 1f);
        label.raycastTarget = false;

        return item;
    }

    // ─────────────────────────────────────────────────────────────────

    private void HandleTap()
    {
        if (!_tapAllowed) return;
        _tapAllowed = false;
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        yield return Animate(0.2f, t => _group.alpha = 1f - t);
        FinishOnce(invokeOnly: false);
    }

    private void FinishOnce(bool invokeOnly)
    {
        if (_finished) return;
        _finished = true;

        var cb = _onDone;
        _onDone = null;

        if (!invokeOnly)
        {
            if (_instance == this) _instance = null;
            Destroy(gameObject);
        }

        cb?.Invoke();
    }

    private IEnumerator FlashOnce(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            _flash.color = new Color(1f, 1f, 1f, 0.85f * (1f - k));
            yield return null;
        }
        _flash.color = new Color(1f, 1f, 1f, 0f);
    }

    private IEnumerator PulseTapText()
    {
        float t = 0f;
        while (!_finished)
        {
            t += Time.unscaledDeltaTime;
            float a = 0.55f + 0.45f * Mathf.Sin(t * 3.2f);
            if (_tapText != null) _tapText.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
    }

    private static IEnumerator Animate(float duration, Action<float> apply)
    {
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            apply(Mathf.Clamp01(t / d));
            yield return null;
        }
        apply(1f);
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.5f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
