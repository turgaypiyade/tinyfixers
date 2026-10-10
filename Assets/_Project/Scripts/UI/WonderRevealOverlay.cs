using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Ana menüde overlay" reveal: bir görev alınınca menüyü karartıp üstte harikayı
/// (WonderScene) bir kademe kaynaklar. Son görevde sandık töreni + sıradaki harikaya geçiş.
/// Panel (RegionUnlockListPanel wonder modu) bunu çağırır. [[project_wonder_reveal_background]]
/// </summary>
public class WonderRevealOverlay : MonoBehaviour
{
    [SerializeField] private GameObject root;           // overlay kökü (aç/kapa)
    [SerializeField] private CanvasGroup group;         // fade
    [SerializeField] private RectTransform sceneParent; // WonderScene buraya (tam ekran)
    [SerializeField] private Shader revealShader;
    [SerializeField] private Sprite weldLightSprite;
    [SerializeField] private float fadeDur = 0.25f;
    [SerializeField] private float holdAfterReveal = 1.1f;
    [Tooltip("Bir kademe kaynak animasyon süresi (yavaş = daha tatmin edici)")]
    [SerializeField] private float revealDuration = 3.4f;
    [Tooltip("Paslı ve solgun resmi eğri kenarlı bölgeler halinde onarır.")]
    [SerializeField] private bool useRustRestoration = true;

    WonderScene _scene;
    int _previewStage;
    public bool IsPlaying { get; private set; }

    /// <summary>Editor preview uses the real overlay without spending stars or saving progress.</summary>
    public void PreviewRustRestoration(WonderDefinition wonder)
    {
        if (!Application.isPlaying || IsPlaying || wonder == null) return;
        if (group != null) group.alpha = 0f;
        gameObject.SetActive(true);
        StartCoroutine(PreviewRestorationRoutine(wonder));
    }

    IEnumerator PreviewRestorationRoutine(WonderDefinition wonder)
    {
        IsPlaying = true;
        try
        {
            int stages = Mathf.Max(1, wonder.TaskCount);
            float from = (_previewStage++ % stages) / (float)stages;
            yield return PrepareReveal(wonder, from, true);
            var view = _scene.View;
            if (!view.isActiveAndEnabled) yield break;
            yield return Fade(0f, 1f);
            yield return view.PlayRevealRoutine(from + 1f / stages);
            if (holdAfterReveal > 0f) yield return new WaitForSeconds(holdAfterReveal);
            yield return Fade(1f, 0f);
        }
        finally
        {
            IsPlaying = false;
            if (root != null) root.SetActive(false);
        }
    }

    /// <summary>
    /// wonderIndex = görevi yapılan EVENT indeksi; fromStage = harcamadan ÖNCEki kademe
    /// (o event'in stage'i zaten artmış olmalı).
    /// </summary>
    public IEnumerator PlayReveal(WonderCatalog cat, int wonderIndex, int fromStage)
    {
        IsPlaying = true;
        var w = cat != null ? cat.Get(wonderIndex) : null;
        if (w == null) { IsPlaying = false; yield break; }

        int toStage = WonderProgress.StageOf(wonderIndex);
        float fromN = w.TaskCount > 0 ? (float)fromStage / w.TaskCount : 0f;
        float toN = w.TaskCount > 0 ? (float)toStage / w.TaskCount : 1f;

        try
        {
            yield return PrepareReveal(w, fromN, useRustRestoration);
            var view = _scene.View;
            if (!view.isActiveAndEnabled) yield break;
            yield return Fade(0f, 1f);
            yield return view.PlayRevealRoutine(toN);

            // Complete only after the reveal has actually run.
            if (!view.isActiveAndEnabled) yield break;
            if (WonderProgress.IsEventComplete(cat, wonderIndex))
            {
                yield return PlayChest(w);
                WonderProgress.MarkEventCompleted(cat, wonderIndex);
            }

            if (holdAfterReveal > 0f) yield return new WaitForSeconds(holdAfterReveal);
            yield return Fade(1f, 0f);
        }
        finally
        {
            IsPlaying = false;
            if (root != null) root.SetActive(false);
        }
    }

    IEnumerator PrepareReveal(WonderDefinition wonder, float from, bool restoration)
    {
        // Activate before building. Cold-start Awake/OnEnable and the first canvas layout
        // must finish before applying the explicit task progress and starting the reveal.
        if (group != null) group.alpha = 0f;
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);
        EnsureScene();
        // Reuse the character, material and spark buffer on subsequent tasks.
        var view = _scene.definition == wonder && _scene.View != null ? _scene.View : _scene.Build(wonder);
        view.useRustRestoration = restoration;
        view.animateDuration = revealDuration;
        yield return null;
        if (!view.isActiveAndEnabled) yield break;
        view.SetRevealImmediate(from);
    }

    void EnsureScene()
    {
        if (_scene != null) return;
        var parent = sceneParent != null ? sceneParent : (RectTransform)transform;
        var go = new GameObject("OverlayWonderScene", typeof(RectTransform), typeof(WonderScene))
        { layer = gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling(); // dim/UI'ın altında kalsın

        _scene = go.GetComponent<WonderScene>();
        _scene.revealShader = revealShader != null ? revealShader : Shader.Find("UI/WonderReveal");
        _scene.weldLightSprite = weldLightSprite;
        _scene.buildOnStart = false;
        _scene.charactersWalkImmediately = false;
        _scene.includeCharacters = false;   // overlay'de robot/dron YOK (onlar ana menüde)
    }

    IEnumerator PlayChest(WonderDefinition w)
    {
        var rewards = new List<DailySlotReward>();
        if (w.chestRewards != null)
            foreach (var rw in w.chestRewards)
                if (rw != null) rewards.Add(rw);
        if (rewards.Count == 0) yield break;

        foreach (var rw in rewards) DailySlotRewardService.Grant(rw);

        bool done = false;
        RewardChestRevealOverlay.Show(rewards, () => done = true, w.chestClosedSprite, w.chestOpenedSprite);
        yield return new WaitUntil(() => done);
    }

    IEnumerator Fade(float a, float b)
    {
        if (group == null) yield break;
        group.alpha = a;
        float t = 0f;
        while (t < fadeDur)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(a, b, Mathf.Clamp01(t / fadeDur));
            yield return null;
        }
        group.alpha = b;
    }
}
