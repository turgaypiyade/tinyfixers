using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Oyundan ana menüye dönüşte, o levelde kazanılan progress-event ödüllerini
/// törenle "torbaya" atar:
///   ödül ekran ortasında BÜYÜK belirir (pop-in + nefes) → kavisle LevelSelector
///   butonuna uçar (küçülerek) → buton punch-scale ile "yakalar".
///
/// Kurulum gerektirmez: MainMenu yüklenince ProgressEventService'ten bekleyen
/// ödülleri kendisi çeker (ConsumePendingMenuRewardFx). Loading ekranı kalkana
/// kadar bekler. Ödül yoksa hiçbir şey yapmaz.
/// </summary>
public sealed class MainMenuRewardCollectFx : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    // ── Bootstrap (sahne kurulumu yok) ────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MainMenuSceneName) return;

        var rewards = ProgressEventService.Instance != null
            ? ProgressEventService.Instance.ConsumePendingMenuRewardFx()
            : null;
        if (rewards == null || rewards.Count == 0) return;

        var root = new GameObject("MainMenuRewardCollectFx");
        var fx = root.AddComponent<MainMenuRewardCollectFx>();
        fx._rewards = rewards;
    }

    // ── Instance ──────────────────────────────────────────────────────

    private List<DailySlotReward> _rewards;
    private Canvas _canvas;
    private RectTransform _canvasRt;

    private void Start()
    {
        BuildCanvas();
        StartCoroutine(Run());
    }

    private void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 900;   // LoadingScreen(999) altında, normal UI üstünde

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasRt = (RectTransform)transform;
    }

    private IEnumerator Run()
    {
        // Loading ekranı tamamen kalksın (fade dahil), menü otursun.
        while (LoadingScreenManager.IsVisible)
            yield return null;
        yield return null;

        RectTransform target = FindLevelButton();

        for (int i = 0; i < _rewards.Count; i++)
            yield return CoCollectOne(_rewards[i], target);

        Destroy(gameObject);
    }

    private RectTransform FindLevelButton()
    {
        var ctrl = FindFirstObjectByType<MainMenuLevelButtonController>();
        if (ctrl != null) return ctrl.transform as RectTransform;
        return null;   // bulunamazsa alt-orta noktaya uçar
    }

    // Tek ödülün tam turu: büyük göster → uç → butona çarp.
    private IEnumerator CoCollectOne(DailySlotReward reward, RectTransform target)
    {
        // ── Görsel kur ──
        var item = new GameObject("Reward", typeof(RectTransform));
        var rt = (RectTransform)item.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 120f);   // ekran ortasının biraz üstü
        rt.sizeDelta = new Vector2(280f, 280f);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.SetParent(rt, false);
        iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
        var icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Sprite resolvedIcon = reward != null ? reward.ResolveIcon() : null;
        if (resolvedIcon != null) icon.sprite = resolvedIcon;
        else icon.color = new Color(1f, 0.7f, 0.24f, 1f);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRt = (RectTransform)labelGo.transform;
        labelRt.SetParent(rt, false);
        labelRt.anchorMin = new Vector2(0.5f, 0f); labelRt.anchorMax = new Vector2(0.5f, 0f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.anchoredPosition = new Vector2(0f, -6f);
        labelRt.sizeDelta = new Vector2(360f, 60f);
        var label = labelGo.GetComponent<TextMeshProUGUI>();
        int amount = reward != null ? Mathf.Max(1, reward.amount) : 1;
        string name = reward != null && !string.IsNullOrEmpty(reward.fallbackName) ? reward.fallbackName + " " : "";
        label.text = $"{name}x{amount}";
        label.fontSize = 44;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.9f, 0.55f, 1f);
        label.raycastTarget = false;

        // ── 1) Pop-in (büyük belirme) ──
        yield return Animate(0.35f, t => rt.localScale = Vector3.one * EaseOutBack(t));

        // ── 2) Nefes (hafif yüzme) ──
        Vector2 home = rt.anchoredPosition;
        float breathe = 0f;
        const float breatheDur = 0.55f;
        while (breathe < breatheDur)
        {
            breathe += Time.unscaledDeltaTime;
            rt.anchoredPosition = home + new Vector2(0f, Mathf.Sin(breathe * 6f) * 8f);
            yield return null;
        }

        // ── 3) Kavisli uçuş hedefe ──
        Vector2 from = rt.anchoredPosition;
        Vector2 to = ResolveTargetAnchored(target);
        Vector2 control = (from + to) * 0.5f + new Vector2(160f, 120f);   // yay tepesi sağ-üst

        yield return Animate(0.55f, t =>
        {
            float e = t * t * (3f - 2f * t);   // smoothstep
            Vector2 a = Vector2.LerpUnclamped(from, control, e);
            Vector2 b = Vector2.LerpUnclamped(control, to, e);
            rt.anchoredPosition = Vector2.LerpUnclamped(a, b, e);   // quadratic bezier
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.22f, e);
        });

        // ── 4) Buton "torbaya aldı" punch'ı ──
        Destroy(item);
        if (target != null)
            yield return PunchScale(target, 0.28f);
    }

    // Hedef butonun canvas'ımızdaki anchored karşılığı (yoksa alt-orta).
    private Vector2 ResolveTargetAnchored(RectTransform target)
    {
        if (target == null)
            return new Vector2(0f, -_canvasRt.rect.height * 0.38f);

        Vector2 local = _canvasRt.InverseTransformPoint(target.position);
        return local;
    }

    // Torba yakalama hissi: buton hafif çöker, zıplar, oturur.
    private static IEnumerator PunchScale(RectTransform target, float duration)
    {
        Vector3 baseScale = target.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = k < 0.35f ? Mathf.Lerp(1f, 0.88f, k / 0.35f)
                    : k < 0.7f  ? Mathf.Lerp(0.88f, 1.12f, (k - 0.35f) / 0.35f)
                                : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
            target.localScale = baseScale * s;
            yield return null;
        }
        target.localScale = baseScale;
    }

    private static IEnumerator Animate(float duration, System.Action<float> apply)
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
}
