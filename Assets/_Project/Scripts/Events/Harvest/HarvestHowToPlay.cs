using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bostan Hasadı "Nasıl oynanır?" overlay'i — Bridge'inkiyle aynı dil (koddan kurulur, BridgeRepairUI yardımcıları):
///   1) Seviye geç, kürek kazan (sol) → ok → 2) Kareleri kaz, ürünleri bul (sağ) → ok → 3) Hasadı topla, ödülü kap (sol)
/// Adımlar sırayla pop-in olur; erken dokunuş animasyonu sona sarar, son hâlde dokunuş kapatır.
/// Her sezonun ilk açılışında otomatik, kazı ekranındaki "?" ile istendiğinde gösterilir.
/// </summary>
public sealed class HarvestHowToPlay : MonoBehaviour
{
    private const string SeenKey = "harvest_howto_cycle";

    private Action onDone;
    private bool skipRequested, finished, tapReady;
    private RectTransform design;
    private int popIndex;

    /// Bu sezonda gösterildi mi? (sezon değişince tekrar gösterilir)
    public static bool SeenThisCycle(HarvestConfig cfg) =>
        PlayerPrefs.GetString(SeenKey, "") == HarvestSchedule.GetCycleKey(cfg, DateTime.UtcNow);

    public static void MarkSeen(HarvestConfig cfg)
    {
        PlayerPrefs.SetString(SeenKey, HarvestSchedule.GetCycleKey(cfg, DateTime.UtcNow));
        PlayerPrefs.Save();
    }

    public static HarvestHowToPlay Show(Transform parent, HarvestConfig cfg, Action onDone)
    {
        var dim = BridgeRepairUI.Solid("HarvestHowToPlay", parent, new Color(0.02f, 0.06f, 0.03f, 0.95f));
        dim.raycastTarget = true;
        dim.transform.SetAsLastSibling();
        var view = dim.gameObject.AddComponent<HarvestHowToPlay>();
        view.onDone = onDone;
        var button = dim.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(view.OnTap);
        view.StartCoroutine(view.Run(cfg));
        EventSfx.Play(x => x.popupOpen);
        return view;
    }

    private void OnTap()
    {
        if (finished) return;
        if (!tapReady) { skipRequested = true; return; }
        finished = true;
        EventSfx.Play(x => x.uiTap);
        StartCoroutine(Close());
    }

    private IEnumerator Run(HarvestConfig cfg)
    {
        design = BridgeRepairUI.Rect("Design", transform, new Vector2(1000f, 1800f), Vector2.zero);
        Canvas.ForceUpdateCanvases();
        var viewport = ((RectTransform)transform).rect;
        design.localScale = Vector3.one * Mathf.Min(1f, Mathf.Min(viewport.width / 1040f, viewport.height / 1840f));

        var title = BridgeRepairUI.Label("Title", design, BridgeRepairUI.L("harvest_howto_title", "Nasıl oynanır?"),
            84f, new Vector2(900f, 120f), new Vector2(0f, 800f));

        // Adım 1 — ayı + kürek: seviye geç, kürek kazan.
        var step1 = BridgeRepairUI.Rect("Step1", design, new Vector2(460f, 340f), new Vector2(-215f, 470f));
        BuildEarn(step1, cfg);
        var text1 = StepText("Text1", BridgeRepairUI.L("harvest_howto_step1", "Seviye geç, kürek kazan!"),
            new Vector2(-215f, 255f));

        var arrow1 = Arrow("Arrow1", false, new Vector2(215f, 330f));

        // Adım 2 — iki toprak karesi + çukurdan çıkan havuç: kareleri kaz, ürünleri bul.
        var step2 = BridgeRepairUI.Rect("Step2", design, new Vector2(420f, 330f), new Vector2(220f, 60f));
        BuildDig(step2, cfg);
        var text2 = StepText("Text2", BridgeRepairUI.L("harvest_howto_step2", "Kareleri kaz, gizli ürünleri bul!"),
            new Vector2(220f, -160f));

        var arrow2 = Arrow("Arrow2", true, new Vector2(-230f, -160f));

        // Adım 3 — sandık + ürünler: hasadı topla, ödülü kap.
        var step3 = BridgeRepairUI.Rect("Step3", design, new Vector2(420f, 330f), new Vector2(-215f, -420f));
        BuildReward(step3, cfg);
        var text3 = StepText("Text3", BridgeRepairUI.LFormat("harvest_howto_step3", "{0} hasadı tamamla, ödülleri kap!",
            cfg != null ? cfg.FloorCount : 4), new Vector2(-215f, -635f));

        var tapText = BridgeRepairUI.Label("Tap", design, BridgeRepairUI.L("bridge_howto_tap", "Devam etmek için dokun"),
            56f, new Vector2(900f, 90f), new Vector2(0f, -800f), BridgeRepairUI.Gold);

        foreach (var rt in new[] { step1, step2, step3, text1.rectTransform, text2.rectTransform, text3.rectTransform })
            rt.localScale = Vector3.zero;
        arrow1.fillAmount = arrow2.fillAmount = 0f;
        tapText.alpha = 0f;
        title.alpha = 0f;

        yield return Fade(title, 0.3f);
        yield return PopPair(step1, text1.rectTransform);
        yield return Draw(arrow1);
        yield return PopPair(step2, text2.rectTransform);
        yield return Draw(arrow2);
        yield return PopPair(step3, text3.rectTransform);

        foreach (var rt in new[] { step1, step2, step3, text1.rectTransform, text2.rectTransform, text3.rectTransform })
            rt.localScale = Vector3.one;
        arrow1.fillAmount = arrow2.fillAmount = 1f;
        title.alpha = 1f;

        yield return WaitSkippable(0.25f);
        tapReady = true;
        float t = 0f;
        while (!finished)
        {
            t += Time.unscaledDeltaTime;
            tapText.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 2.6f));
            yield return null;
        }
    }

    // ── Adım görselleri ──────────────────────────────────────────

    private static void BuildEarn(RectTransform parent, HarvestConfig cfg)
    {
        var glow = BridgeRepairUI.Picture("Glow", parent, BridgeRepairUI.Glow(), new Vector2(520f, 380f), Vector2.zero);
        glow.color = new Color(1f, 0.85f, 0.4f, 0.35f);
        if (cfg == null) return;
        BridgeRepairUI.Picture("Bear", parent, cfg.bearIdle, new Vector2(330f, 330f), new Vector2(-60f, 0f));
        var trowel = BridgeRepairUI.Picture("Trowel", parent, cfg.trowelIcon, new Vector2(150f, 150f), new Vector2(150f, 40f));
        trowel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        var plus = BridgeRepairUI.Label("Plus", parent, "+1", 64f, new Vector2(140f, 80f), new Vector2(170f, -70f), BridgeRepairUI.Gold);
        plus.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void BuildDig(RectTransform parent, HarvestConfig cfg)
    {
        if (cfg == null) return;
        var soil = cfg.soilTiles != null && cfg.soilTiles.Length > 0 ? cfg.soilTiles[0] : null;
        // Kareler: arkada kapalı toprak, önde kazılmış çukur; çukurdan havuç yükselir.
        BridgeRepairUI.Picture("Soil", parent, soil, new Vector2(230f, 172f), new Vector2(-95f, 40f));
        BridgeRepairUI.Picture("Dug", parent, cfg.dugTile, new Vector2(230f, 172f), new Vector2(95f, -40f));
        var carrot = cfg.crops != null && cfg.crops.Length > 0 ? cfg.crops[0].sprite : null;
        var crop = BridgeRepairUI.Picture("Crop", parent, carrot, new Vector2(150f, 230f), new Vector2(100f, 60f));
        crop.preserveAspect = true;
        var trowel = BridgeRepairUI.Picture("Trowel", parent, cfg.trowelIcon, new Vector2(120f, 120f), new Vector2(-150f, -70f));
        trowel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 20f);
    }

    private static void BuildReward(RectTransform parent, HarvestConfig cfg)
    {
        var glow = BridgeRepairUI.Picture("Glow", parent, BridgeRepairUI.Glow(), new Vector2(480f, 400f), Vector2.zero);
        glow.color = new Color(1f, 0.75f, 0.25f, 0.45f);
        if (cfg?.crops != null)
        {
            // Sandığın etrafında hasat: kabak, karpuz, çilek, mısır.
            (int crop, Vector2 pos, float size)[] spots =
            {
                (5, new Vector2(-165f, -95f), 130f), (4, new Vector2(160f, -110f), 140f),
                (7, new Vector2(-170f, 55f), 95f), (3, new Vector2(175f, 50f), 120f),
            };
            foreach (var (crop, pos, size) in spots)
            {
                var sprite = crop < cfg.crops.Length ? cfg.crops[crop].sprite : null;
                if (sprite == null) continue;
                var img = BridgeRepairUI.Picture($"Crop{crop}", parent, sprite, Vector2.one * size, pos);
                img.preserveAspect = true;
            }
        }
        var chest = Resources.Load<Sprite>("UI/RewardChestClosed");
        BridgeRepairUI.Picture("Chest", parent, chest, new Vector2(290f, 260f), new Vector2(0f, 10f));
    }

    // ── Yardımcılar (Bridge'inkiyle aynı) ────────────────────────

    private TMP_Text StepText(string name, string value, Vector2 pos)
    {
        var text = BridgeRepairUI.Label(name, design, value, 46f, new Vector2(520f, 130f), pos);
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private Image Arrow(string name, bool flip, Vector2 pos)
    {
        var arrow = BridgeRepairUI.Picture(name, design, BridgeRepairUI.CurvedArrow(flip), new Vector2(190f, 210f), pos);
        arrow.type = Image.Type.Filled;
        arrow.fillMethod = Image.FillMethod.Vertical;
        arrow.fillOrigin = (int)Image.OriginVertical.Top;
        return arrow;
    }

    private IEnumerator PopPair(RectTransform image, RectTransform text)
    {
        if (!skipRequested) EventSfx.Play(x => x.stepPop, 1f, 1f + 0.08f * popIndex);
        popIndex++;
        float t = 0f;
        const float d = 0.38f;
        while (t < d && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            image.localScale = Vector3.one * BridgeRepairUI.EaseOutBack(k);
            text.localScale = Vector3.one * BridgeRepairUI.EaseOutBack(Mathf.Clamp01((t - 0.1f) / d));
            yield return null;
        }
        image.localScale = Vector3.one;
        text.localScale = Vector3.one;
        yield return WaitSkippable(0.12f);
    }

    private IEnumerator Draw(Image arrow)
    {
        if (!skipRequested) EventSfx.Play(x => x.arrowWhoosh);
        float t = 0f;
        const float d = 0.35f;
        while (t < d && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            arrow.fillAmount = BridgeRepairUI.Smooth01(t / d);
            yield return null;
        }
        arrow.fillAmount = 1f;
    }

    private IEnumerator Fade(TMP_Text text, float d)
    {
        float t = 0f;
        while (t < d && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            text.alpha = Mathf.Clamp01(t / d);
            yield return null;
        }
        text.alpha = 1f;
    }

    private IEnumerator WaitSkippable(float d)
    {
        float t = 0f;
        while (t < d && !skipRequested) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private IEnumerator Close()
    {
        var group = gameObject.AddComponent<CanvasGroup>();
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - t / 0.2f;
            yield return null;
        }
        var cb = onDone;
        onDone = null;
        Destroy(gameObject);
        cb?.Invoke();
    }
}
