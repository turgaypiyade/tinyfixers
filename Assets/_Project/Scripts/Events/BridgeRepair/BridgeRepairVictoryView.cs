using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridge Repair kazanma ekranı (koddan kurulur, Safari kazanma ekranı deseni):
///   kurdele "KÖPRÜ TAMAMLANDI" + yay başlık "1. OLDUN!" + sıra madalyası + oyuncunun karakteri sevinçle zıplar
///   + konfeti + ödül önizleme sırası + "ÖDÜLÜ AL".
/// "ÖDÜLÜ AL" → ekran kapanır → <see cref="RewardChestRevealOverlay"/> (sandık sallanır, açılır, ödüller uçar).
/// Ödül BU EKRANDA VERİLMEZ (kazanma anında BridgeRepairState verdi) → önizleme/debug güvenli.
/// </summary>
public sealed class BridgeRepairVictoryView : MonoBehaviour
{
    public struct Art
    {
        public Sprite ribbon;
        public Sprite button;
        public Sprite chestClosed;
        public Material eventLabelMaterial;
    }

    private static readonly Color[] ConfettiColors =
    {
        new(1f, 0.82f, 0.2f), new(0.3f, 0.75f, 1f), new(1f, 0.4f, 0.45f), new(0.45f, 0.9f, 0.4f), new(0.85f, 0.55f, 1f)
    };

    private RectTransform design;
    private bool claimed;
    private Action onDone;

    public static BridgeRepairVictoryView Show(Transform canvasRoot, int rank, int contestants,
        BridgeCharacterDef character, IReadOnlyList<DailySlotReward> rewards, Art art, Action onDone = null)
    {
        var dim = BridgeRepairUI.Solid("BridgeRepairVictory", canvasRoot, new Color(0.02f, 0.05f, 0.12f, 0.95f));
        dim.raycastTarget = true;
        dim.transform.SetAsLastSibling();
        var view = dim.gameObject.AddComponent<BridgeRepairVictoryView>();
        view.onDone = onDone;
        view.StartCoroutine(view.Run(rank, contestants, character, rewards ?? Array.Empty<DailySlotReward>(), art));
        return view;
    }

    private IEnumerator Run(int rank, int contestants, BridgeCharacterDef character,
        IReadOnlyList<DailySlotReward> rewards, Art art)
    {
        design = BridgeRepairUI.Rect("Celebration", transform, new Vector2(900f, 1500f), Vector2.zero);
        Canvas.ForceUpdateCanvases();
        Rect viewport = ((RectTransform)transform).rect;
        float fit = Mathf.Min(1f, Mathf.Min(viewport.width / 980f, viewport.height / 1600f));
        design.localScale = Vector3.one * fit;
        var group = design.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Color medal = RankColor(rank);

        // Arkada dönen ışık + altın hale.
        var rays = BridgeRepairUI.Picture("Rays", design, Rays(), new Vector2(1100f, 1100f), new Vector2(0f, 120f));
        rays.color = new Color(1f, 0.85f, 0.4f, 0.28f);
        var halo = BridgeRepairUI.Picture("Halo", design, BridgeRepairUI.Glow(), new Vector2(820f, 820f), new Vector2(0f, 120f));
        halo.color = new Color(medal.r, medal.g, medal.b, 0.55f);

        var confetti = BridgeRepairUI.Rect("Confetti", transform, Vector2.zero, Vector2.zero);

        // Kurdele + başlıklar.
        BridgeRepairUI.Picture("Ribbon", design, art.ribbon, new Vector2(960f, 398f), new Vector2(0f, 560f));
        var eventLabel = BridgeRepairUI.Label("Event", design, BridgeRepairUI.L("bridge_victory_event", "KÖPRÜ TAMAMLANDI"),
            40f, new Vector2(820f, 80f), new Vector2(0f, 850f));
        if (art.eventLabelMaterial != null) eventLabel.fontSharedMaterial = art.eventLabelMaterial;
        var title = BridgeRepairUI.Label("Title", design, RankTitle(rank), 92f, new Vector2(700f, 150f),
            new Vector2(0f, 545f));
        title.gameObject.AddComponent<TMPArcText>().arcDegrees = -15f;

        // Karakter (köprü parçası üstünde) + madalya.
        var stage = BridgeRepairUI.Rect("Stage", design, new Vector2(600f, 460f), new Vector2(0f, 110f));
        var hero = BridgeRepairUI.Picture("Hero", stage, character?.Idle, new Vector2(340f, 400f), new Vector2(-40f, 0f));
        hero.rectTransform.pivot = new Vector2(0.5f, 0f);
        hero.rectTransform.anchoredPosition = new Vector2(-40f, -200f);
        var medalRt = BuildMedal(stage, rank, medal, new Vector2(190f, 80f));
        medalRt.localScale = Vector3.zero;

        var standing = BridgeRepairUI.Label("Standing", design,
            BridgeRepairUI.LFormat("bridge_victory_standing", "{1} yarışmacı arasında {0}. sırada bitirdin!", rank, contestants),
            38f, new Vector2(820f, 60f), new Vector2(0f, -160f));

        // Ödül önizlemesi.
        BridgeRepairUI.Label("Caption", design, BridgeRepairUI.L("bridge_victory_caption", "ÖDÜLLERİN"), 40f,
            new Vector2(700f, 60f), new Vector2(0f, -250f), BridgeRepairUI.Gold);
        var row = BuildRewardRow(rewards, art.chestClosed, new Vector2(0f, -380f));
        row.localScale = Vector3.zero;

        var buttonImg = BridgeRepairUI.Picture("ClaimButton", design, art.button,
            new Vector2(520f, 520f * 116f / 289f), new Vector2(0f, -590f), preserveAspect: false);
        if (art.button == null) { buttonImg.enabled = true; buttonImg.sprite = BridgeRepairUI.Pill(); buttonImg.type = Image.Type.Sliced; buttonImg.color = new Color(0.3f, 0.75f, 0.2f); }
        var button = BridgeRepairUI.MakeButton(buttonImg, () => { EventSfx.Play(x => x.uiTap); claimed = true; });
        button.interactable = false;
        BridgeRepairUI.Label("Label", buttonImg.rectTransform, BridgeRepairUI.L("bridge_victory_claim", "ÖDÜLÜ AL"), 64f,
            new Vector2(440f, 130f), new Vector2(0f, 6f));
        buttonImg.rectTransform.localScale = Vector3.zero;

        // Giriş.
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.4f);
            group.alpha = BridgeRepairUI.Smooth01(k);
            design.localScale = Vector3.one * fit * (1f - 0.12f * Mathf.Pow(1f - k, 3f));
            yield return null;
        }
        group.alpha = 1f;
        design.localScale = Vector3.one * fit;

        EventSfx.Play(x => x.victoryFanfare);
        EventSfx.Play(x => x.confetti);
        SpawnConfetti(confetti, viewport, 70);
        EventSfx.Play(x => x.rankBadge);
        yield return Pop(medalRt, 0.45f);
        yield return Pop(row, 0.35f);
        yield return Pop(buttonImg.rectTransform, 0.3f);
        button.interactable = true;

        // Bekle: karakter sevinçle zıplar, ışık döner, buton nefes alır.
        float idle = 0f;
        var heroRt = hero.rectTransform;
        Vector2 heroBase = heroRt.anchoredPosition;
        while (!claimed)
        {
            idle += Time.unscaledDeltaTime;
            float hop = Mathf.Abs(Mathf.Sin(idle * 3.4f));
            heroRt.anchoredPosition = heroBase + new Vector2(0f, hop * 60f);
            heroRt.localScale = new Vector3(1f + (1f - hop) * 0.05f, 1f - (1f - hop) * 0.06f, 1f);
            rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -idle * 14f);
            halo.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(idle * 1.7f) * 0.06f);
            medalRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(idle * 2.2f) * 6f);
            buttonImg.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(idle * 3f) * 0.02f);
            yield return null;
        }

        button.interactable = false;
        t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - t / 0.22f;
            yield return null;
        }

        var cb = onDone;
        onDone = null;
        Destroy(gameObject);

        // Sandık töreni (ödüller sandıktan uçar). Ödül zaten verildi → yalnız gösterim.
        if (rewards.Count > 0) RewardChestRevealOverlay.Show(rewards, cb);
        else cb?.Invoke();
    }

    // ── Parçalar ─────────────────────────────────────────────────

    private static string RankTitle(int rank) => rank switch
    {
        1 => BridgeRepairUI.L("bridge_victory_rank1", "BİRİNCİSİN!"),
        2 => BridgeRepairUI.L("bridge_victory_rank2", "İKİNCİSİN!"),
        3 => BridgeRepairUI.L("bridge_victory_rank3", "ÜÇÜNCÜSÜN!"),
        _ => BridgeRepairUI.L("bridge_victory_event", "KÖPRÜ TAMAMLANDI")
    };

    private static Color RankColor(int rank) => rank switch
    {
        1 => new Color(1f, 0.8f, 0.18f),
        2 => new Color(0.82f, 0.86f, 0.92f),
        3 => new Color(0.86f, 0.53f, 0.27f),
        _ => new Color(0.45f, 0.55f, 0.7f)
    };

    private static RectTransform BuildMedal(RectTransform parent, int rank, Color color, Vector2 pos)
    {
        var root = BridgeRepairUI.Rect("Medal", parent, Vector2.one * 190f, pos);
        var glow = BridgeRepairUI.Picture("Glow", root, BridgeRepairUI.Glow(), Vector2.one * 300f, Vector2.zero);
        glow.color = new Color(color.r, color.g, color.b, 0.6f);
        var rim = BridgeRepairUI.Picture("Rim", root, BridgeRepairUI.Circle(), Vector2.one * 190f, Vector2.zero);
        rim.color = Color.Lerp(color, BridgeRepairUI.Ink, 0.35f);
        var disc = BridgeRepairUI.Picture("Disc", root, BridgeRepairUI.Circle(), Vector2.one * 166f, Vector2.zero);
        disc.color = color;
        var shine = BridgeRepairUI.Picture("Shine", root, BridgeRepairUI.Circle(), Vector2.one * 128f, new Vector2(0f, 6f));
        shine.color = Color.Lerp(color, Color.white, 0.4f);
        var num = BridgeRepairUI.Label("Rank", root, rank.ToString(), 110f, Vector2.one * 150f, new Vector2(0f, 4f),
            BridgeRepairUI.Ink, rewardStyle: false);
        num.outlineWidth = 0f;
        return root;
    }

    private RectTransform BuildRewardRow(IReadOnlyList<DailySlotReward> rewards, Sprite chest, Vector2 pos)
    {
        int n = rewards.Count;
        const float cell = 170f;
        var row = BridgeRepairUI.Rect("Rewards", design, new Vector2(Mathf.Max(1, n) * cell, 200f), pos);
        for (int i = 0; i < n; i++)
        {
            var r = rewards[i];
            var x = (i - (n - 1) * 0.5f) * cell;
            var plate = BridgeRepairUI.Picture($"Plate{i}", row, BridgeRepairUI.Pill(), new Vector2(150f, 170f), new Vector2(x, 0f));
            plate.type = Image.Type.Sliced;
            plate.color = new Color(1f, 1f, 1f, 0.12f);
            var icon = r.ResolveIcon();
            var img = BridgeRepairUI.Picture($"Icon{i}", row, icon != null ? icon : chest, new Vector2(110f, 110f),
                new Vector2(x, 18f));
            img.enabled = img.sprite != null;
            BridgeRepairUI.Label($"Amount{i}", row, r.type == DailySlotRewardType.Coins ? $"{r.amount:N0}" : $"x{r.amount}",
                38f, new Vector2(150f, 50f), new Vector2(x, -58f));
        }
        return row;
    }

    // ── Konfeti ──────────────────────────────────────────────────

    private void SpawnConfetti(RectTransform layer, Rect viewport, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var piece = BridgeRepairUI.Picture("Confetti", layer, BridgeRepairUI.Pill(),
                new Vector2(UnityEngine.Random.Range(14f, 22f), UnityEngine.Random.Range(24f, 36f)), Vector2.zero);
            piece.enabled = true;
            piece.color = ConfettiColors[i % ConfettiColors.Length];
            StartCoroutine(Fall(piece.rectTransform, viewport, UnityEngine.Random.Range(0f, 0.9f)));
        }
    }

    private static IEnumerator Fall(RectTransform rt, Rect viewport, float delay)
    {
        float x = UnityEngine.Random.Range(viewport.xMin, viewport.xMax);
        float y = viewport.yMax + 60f;
        float vx = UnityEngine.Random.Range(-80f, 80f);
        float vy = UnityEngine.Random.Range(-520f, -360f);
        float spin = UnityEngine.Random.Range(-360f, 360f);
        float sway = UnityEngine.Random.Range(1.5f, 3.5f);
        rt.localPosition = new Vector3(x, y, 0f);
        float t = -delay;
        while (rt != null && rt.localPosition.y > viewport.yMin - 80f)
        {
            t += Time.unscaledDeltaTime;
            if (t > 0f)
            {
                float dt = Time.unscaledDeltaTime;
                var p = rt.localPosition;
                p.x += (vx + Mathf.Sin(t * sway) * 120f) * dt;
                p.y += vy * dt;
                rt.localPosition = p;
                rt.localRotation = Quaternion.Euler(0f, 0f, t * spin);
                rt.localScale = new Vector3(Mathf.Cos(t * sway * 2f), 1f, 1f);
            }
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private static IEnumerator Pop(RectTransform rt, float d)
    {
        float t = 0f;
        while (t < d && rt != null)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * BridgeRepairUI.EaseOutBack(Mathf.Clamp01(t / d));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    private static Sprite s_rays;
    private static Sprite Rays()
    {
        if (s_rays != null) return s_rays;
        const int size = 256, spokes = 14;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * spokes;
            float spoke = Mathf.Clamp01((Mathf.Abs(Mathf.Sin(ang * Mathf.PI)) - 0.55f) * 4f);
            float a = spoke * Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        s_rays = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_rays;
    }
}
