using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridge Repair "Nasıl oynanır?" overlay'i — koddan kurulur (prefab yok). Referans görseldeki zikzak düzen:
///   1) Yarışmacılar (sol)  → ok →  2) Level taşları + tik (sağ)  → ok →  3) Ödül sandığı (sol)
/// Adımlar sırayla pop-in olur, oklar arada yukarıdan aşağı "çizilir". Erken dokunuş animasyonu sona sarar;
/// son hâlde dokunuş kapatır ve onDone'u çağırır.
/// </summary>
public sealed class BridgeRepairHowToPlay : MonoBehaviour
{
    public struct Art
    {
        public BridgeRepairConfig config;
        public Sprite checkMark;
        public Sprite coin;
        public Sprite chest;
    }

    private Action onDone;
    private bool skipRequested, finished, tapReady;
    private RectTransform design;
    private TMP_Text tapText;

    public static BridgeRepairHowToPlay Show(Transform canvasRoot, Art art, Action onDone)
    {
        var dim = BridgeRepairUI.Solid("BridgeRepairHowToPlay", canvasRoot, new Color(0.02f, 0.04f, 0.1f, 0.96f));
        dim.raycastTarget = true;
        dim.transform.SetAsLastSibling();
        var view = dim.gameObject.AddComponent<BridgeRepairHowToPlay>();
        view.onDone = onDone;
        var button = dim.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(view.OnTap);
        view.StartCoroutine(view.Run(art));
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

    private IEnumerator Run(Art art)
    {
        design = BridgeRepairUI.Rect("Design", transform, new Vector2(1000f, 1800f), Vector2.zero);
        Canvas.ForceUpdateCanvases();
        var viewport = ((RectTransform)transform).rect;
        design.localScale = Vector3.one * Mathf.Min(1f, Mathf.Min(viewport.width / 1040f, viewport.height / 1840f));

        var title = BridgeRepairUI.Label("Title", design, BridgeRepairUI.L("bridge_howto_title", "Nasıl oynanır?"),
            84f, new Vector2(900f, 120f), new Vector2(0f, 800f));

        // Adım 1 — yarışmacılar (karakterler yan yana yürür).
        var step1 = BridgeRepairUI.Rect("Step1", design, new Vector2(460f, 340f), new Vector2(-215f, 470f));
        BuildRacers(step1, art.config);
        var text1 = StepText("Text1", BridgeRepairUI.L("bridge_howto_step1", "Diğer oyunculara karşı yarış!"),
            new Vector2(-215f, 255f));

        var arrow1 = Arrow("Arrow1", false, new Vector2(215f, 330f));

        // Adım 2 — 3x3 taş ızgarası + yeşil tik.
        var step2 = BridgeRepairUI.Rect("Step2", design, new Vector2(330f, 330f), new Vector2(220f, 60f));
        BuildTiles(step2, art.checkMark);
        var text2 = StepText("Text2", BridgeRepairUI.LFormat("bridge_howto_step2", "{0} seviyeyi herkesten önce geç",
            art.config != null ? art.config.levelsToFinish : 15), new Vector2(220f, -160f));

        var arrow2 = Arrow("Arrow2", true, new Vector2(-230f, -160f));

        // Adım 3 — ödül sandığı + altınlar.
        var step3 = BridgeRepairUI.Rect("Step3", design, new Vector2(420f, 330f), new Vector2(-215f, -420f));
        BuildChest(step3, art.chest, art.coin);
        var text3 = StepText("Text3", BridgeRepairUI.LFormat("bridge_howto_step3", "İlk {0}'e girene büyük ödül!",
            art.config != null ? art.config.prizeRanks : 3), new Vector2(-215f, -635f));

        tapText = BridgeRepairUI.Label("Tap", design, BridgeRepairUI.L("bridge_howto_tap", "Devam etmek için dokun"),
            56f, new Vector2(900f, 90f), new Vector2(0f, -800f), BridgeRepairUI.Gold);

        // Başlangıç: hepsi gizli.
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

        // Erken dokunuş → her şeyi son hâline getir.
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

    private void BuildRacers(RectTransform parent, BridgeRepairConfig config)
    {
        var glow = BridgeRepairUI.Picture("Glow", parent, BridgeRepairUI.Glow(), new Vector2(520f, 380f), Vector2.zero);
        glow.color = new Color(1f, 0.85f, 0.4f, 0.35f);
        if (config == null || config.characters == null) return;

        int shown = Mathf.Min(3, config.characters.Count);
        for (int i = 0; i < shown; i++)
        {
            var def = config.characters[i];
            var sprite = def.walk != null && def.walk.Length > 0 ? def.walk[0] : def.Idle;
            float h = 250f - i * 20f;
            var img = BridgeRepairUI.Picture($"Racer{i}", parent, sprite, new Vector2(h * 0.9f, h),
                new Vector2(110f - i * 125f, -20f - (250f - h) * 0.5f));
            img.transform.SetAsFirstSibling();
            glow.transform.SetAsFirstSibling();
            StartCoroutine(WalkLoop(img, def, i * 0.13f));
        }
    }

    private IEnumerator WalkLoop(Image img, BridgeCharacterDef def, float phase)
    {
        if (def?.walk == null || def.walk.Length == 0) yield break;
        float t = phase;
        var rt = img.rectTransform;
        Vector2 basePos = rt.anchoredPosition;
        while (img != null)
        {
            t += Time.unscaledDeltaTime;
            img.sprite = def.walk[(int)(t * 7f) % def.walk.Length];
            rt.anchoredPosition = basePos + new Vector2(0f, Mathf.Abs(Mathf.Sin(t * 7f * Mathf.PI)) * 6f);
            yield return null;
        }
    }

    private static void BuildTiles(RectTransform parent, Sprite checkMark)
    {
        var frame = BridgeRepairUI.Picture("Frame", parent, BridgeRepairUI.Pill(), new Vector2(320f, 320f), Vector2.zero);
        frame.type = Image.Type.Sliced;
        frame.color = new Color(0.98f, 0.9f, 0.72f, 1f);
        var inner = BridgeRepairUI.Picture("Inner", parent, BridgeRepairUI.Pill(), new Vector2(296f, 296f), Vector2.zero);
        inner.type = Image.Type.Sliced;
        inner.color = new Color(0.2f, 0.33f, 0.55f, 1f);

        var lib = TileIconLibrary.Shared;
        TileType[] layout =
        {
            TileType.Gear, TileType.Core, TileType.Core,
            TileType.Gear, TileType.Core, TileType.Bolt,
            TileType.Bolt, TileType.Plate, TileType.Plate,
        };
        for (int i = 0; i < 9; i++)
        {
            int x = i % 3, y = i / 3;
            var sprite = lib != null ? lib.Get(layout[i]) : null;
            var tile = BridgeRepairUI.Picture($"Tile{i}", parent, sprite, new Vector2(88f, 88f),
                new Vector2((x - 1) * 94f, (1 - y) * 94f));
            if (sprite == null) { tile.enabled = true; tile.sprite = BridgeRepairUI.Pill(); tile.color = Color.HSVToRGB(i / 9f, 0.6f, 0.95f); }
        }

        if (checkMark != null)
            BridgeRepairUI.Picture("Check", parent, checkMark, new Vector2(170f, 170f), new Vector2(120f, -120f));
    }

    private static void BuildChest(RectTransform parent, Sprite chest, Sprite coin)
    {
        var glow = BridgeRepairUI.Picture("Glow", parent, BridgeRepairUI.Glow(), new Vector2(480f, 400f), Vector2.zero);
        glow.color = new Color(1f, 0.75f, 0.25f, 0.45f);
        if (coin != null)
        {
            Vector2[] spots = { new(-150f, -110f), new(-100f, -135f), new(140f, -115f), new(95f, -140f), new(170f, -75f), new(-175f, -70f) };
            for (int i = 0; i < spots.Length; i++)
                BridgeRepairUI.Picture($"Coin{i}", parent, coin, Vector2.one * (62f - i * 3f), spots[i]);
        }
        BridgeRepairUI.Picture("Chest", parent, chest, new Vector2(300f, 270f), new Vector2(0f, 10f));
    }

    // ── Animasyon yardımcıları (skip destekli) ───────────────────

    private int popIndex;
    private IEnumerator PopPair(RectTransform image, RectTransform text)
    {
        if (!skipRequested) EventSfx.Play(x => x.stepPop, 1f, 1f + 0.08f * popIndex);   // her adımda biraz tiz
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
