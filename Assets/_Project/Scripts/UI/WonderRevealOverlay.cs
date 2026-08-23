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
    [SerializeField] private float revealDuration = 1.9f;

    WonderScene _scene;
    public bool IsPlaying { get; private set; }

    /// <summary>fromStage = harcamadan ÖNCEki kademe. CurrentStage zaten artmış olmalı.</summary>
    public IEnumerator PlayReveal(WonderCatalog cat, int fromStage)
    {
        IsPlaying = true;
        var w = WonderProgress.CurrentWonder(cat);
        if (w == null) { IsPlaying = false; yield break; }

        int toStage = WonderProgress.CurrentStage;
        float fromN = w.TaskCount > 0 ? (float)fromStage / w.TaskCount : 0f;
        float toN = w.TaskCount > 0 ? (float)toStage / w.TaskCount : 1f;

        EnsureScene();
        if (root != null) root.SetActive(true);

        var view = _scene.Build(w);
        view.animateDuration = revealDuration;   // yavaş, tatmin edici kaynak
        view.SetRevealImmediate(fromN);

        yield return Fade(0f, 1f);
        yield return view.PlayRevealRoutine(toN);   // kaynak animasyonu

        // Son görev tamamlandıysa: sandık + sıradaki harikaya geç
        if (WonderProgress.IsCurrentWonderComplete(cat))
        {
            yield return PlayChest(w);
            WonderProgress.AdvanceToNextWonder();
        }

        if (holdAfterReveal > 0f) yield return new WaitForSeconds(holdAfterReveal);
        yield return Fade(1f, 0f);
        if (root != null) root.SetActive(false);
        IsPlaying = false;
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
