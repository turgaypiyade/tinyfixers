using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Haritanın dekoratif hareketi. Yarış ilerlemesinden bağımsızdır; tüm zamanlar unscaled.
/// Efektler bir kez kurulur, ayrı Canvas altında güncellenir ve dokunma almaz.
/// </summary>
public sealed class BridgeRepairScreenMotion
{
    public const float EntranceDuration = 0.85f;
    // Açılış: board hafif yakından oturur, başlık yukarıdan düşer, etiketler sırayla pop yapar.
    private const float BoardStartScale = 1.07f;
    private const float FadeDuration = 0.22f;

    private sealed class Reveal
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 position;
        public Vector3 scale;
        public Quaternion rotation;
        public float alpha;
        public float delay;
        public float offset;
        public float duration;
        public float startScale;

        public Reveal(RectTransform target, float delay, float offset, float duration, float startScale)
        {
            rect = target;
            group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
            position = target.anchoredPosition;
            scale = target.localScale;
            rotation = target.localRotation;
            alpha = group.alpha;
            this.delay = delay;
            this.offset = offset;
            this.duration = duration;
            this.startScale = startScale;
        }

        public void Apply(float time)
        {
            if (rect == null) return;
            float t = Mathf.Clamp01((time - delay) / duration);
            float settle = BridgeRepairUI.EaseOutBack(t);
            rect.anchoredPosition = position + Vector2.up * (offset * (1f - settle));
            rect.localScale = scale * Mathf.LerpUnclamped(startScale, 1f, settle);
            group.alpha = alpha * BridgeRepairUI.Smooth01(t * 2.5f);
        }

        public void Restore()
        {
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.localScale = scale;
            rect.localRotation = rotation;
            group.alpha = alpha;
        }
    }

    private sealed class Glint
    {
        public Image image;
        public Vector2 origin;
        public float phase;
        public float speed;
        public float opacity;
        public bool warm;
    }

    private readonly CanvasGroup screen;
    private readonly float screenAlpha;
    private readonly Reveal title;
    private readonly Reveal timer;
    private readonly List<Reveal> tags = new();
    private readonly List<Glint> glints = new();
    private readonly RectTransform ambience;
    private bool active;
    private bool entranceComplete;
    private float elapsed;
    private int startedFrame = -1;

    public float BoardScale { get; private set; } = 1f;
    public bool EntranceComplete => entranceComplete;

    public BridgeRepairScreenMotion(GameObject root, RectTransform board, TMP_Text titleText, TMP_Text timerText)
    {
        screen = root.GetComponent<CanvasGroup>();
        if (screen == null) screen = root.AddComponent<CanvasGroup>();
        screenAlpha = screen.alpha;
        title = CaptureHolder(titleText, root, board, 0.08f, 170f, 0.5f, 0.7f);
        timer = CaptureHolder(timerText, root, board, 0.22f, 90f, 0.42f, 0.8f);
        // Desteklenen sahnede başlık ve süre farklı kaplarda. Aynı kaptalarsa iki kez hareket ettirme.
        if (timer != null && title != null &&
            (timer.rect == title.rect || timer.rect.IsChildOf(title.rect))) timer = null;

        if (board == null) return;
        ambience = BridgeRepairUI.Stretch("WaterShimmer", board);
        ambience.SetAsFirstSibling(); // Köprü, karakter ve isimlerin arkasında.
        ambience.gameObject.AddComponent<Canvas>();
        var group = ambience.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // BridgeRepairBG üzerindeki açık su bölgeleri; koordinatlar sol-alttan normalize.
        Vector2[] water =
        {
            new(0.46f, 0.70f), new(0.60f, 0.65f), new(0.48f, 0.59f),
            new(0.58f, 0.54f), new(0.43f, 0.49f), new(0.51f, 0.43f),
            new(0.44f, 0.37f), new(0.54f, 0.32f), new(0.43f, 0.27f)
        };
        for (int i = 0; i < water.Length; i++) AddGlint(water[i], i, false);
        AddGlint(new Vector2(0.44f, 0.17f), 9, true);
        AddGlint(new Vector2(0.56f, 0.18f), 10, true);
        AddGlint(new Vector2(0.50f, 0.20f), 11, true);
        ambience.gameObject.SetActive(false);
    }

    private static Reveal CaptureHolder(TMP_Text text, GameObject root, RectTransform board,
        float delay, float offset, float duration, float startScale)
    {
        if (text == null) return null;
        var holder = text.transform.parent as RectTransform;
        if (holder == null || holder.gameObject == root || holder == board) holder = text.rectTransform;
        return new Reveal(holder, delay, offset, duration, startScale);
    }

    private void AddGlint(Vector2 uv, int index, bool warm)
    {
        // Board pikseli; ekranda ~0.45x görünür → küçük değerler kayboluyordu.
        var size = warm ? new Vector2(46f, 64f) : new Vector2(110f + index % 3 * 30f, 16f);
        var img = BridgeRepairUI.Picture($"Glint{index}", ambience, BridgeRepairUI.Glow(),
            size, Vector2.zero, preserveAspect: false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = uv;
        img.color = warm ? new Color(1f, 0.88f, 0.45f, 0f) : new Color(0.8f, 0.97f, 1f, 0f);
        glints.Add(new Glint
        {
            image = img, origin = img.rectTransform.anchoredPosition,
            phase = index * 2.39996f, speed = 0.9f + index % 4 * 0.17f,
            opacity = warm ? 0.9f : 0.85f, warm = warm
        });
    }

    public void Begin(RectTransform tagsLayer)
    {
        Reset();
        if (tagsLayer != null)
            for (int i = 0; i < tagsLayer.childCount; i++)
            {
                var tag = tagsLayer.GetChild(i) as RectTransform;
                if (tag != null && tag.gameObject.activeSelf)
                    tags.Add(new Reveal(tag, 0.3f + Mathf.Min(tags.Count, 5) * 0.07f, -40f, 0.36f, 0.4f));
            }
        active = true;
        if (ambience != null) ambience.gameObject.SetActive(true);
        Tick(0f); // İlk çizimden önce başlangıç pozu.
        startedFrame = Time.frameCount;
    }

    public void Tick(float deltaTime)
    {
        if (!active) return;
        // Open()'ın çalıştığı karenin deltaTime'ı açılıştan ÖNCEki yükleme süresidir.
        // İlk pozu en az bir kare çiz; sonrasında da tek takılma tüm girişi bitirmesin.
        if (startedFrame == Time.frameCount) return;
        elapsed += Mathf.Min(Mathf.Max(0f, deltaTime), 0.05f);
        float ease = 1f;
        if (!entranceComplete)
        {
            float t = Mathf.Clamp01(elapsed / EntranceDuration);
            ease = 1f - Mathf.Pow(1f - t, 3f);
            BoardScale = Mathf.Lerp(BoardStartScale, 1f, ease);
            screen.alpha = screenAlpha * BridgeRepairUI.Smooth01(elapsed / FadeDuration);
            title?.Apply(elapsed);
            timer?.Apply(elapsed);
            foreach (var tag in tags) tag.Apply(elapsed);
            entranceComplete = t >= 1f;
        }

        // Tabela rüzgârda hafifçe sallanır ve nefes alır (giriş bitince tam genlik).
        if (title != null && title.rect != null && entranceComplete)
        {
            float idle = elapsed - EntranceDuration;
            float fade = BridgeRepairUI.Smooth01(idle / 0.6f);
            title.rect.localRotation = title.rotation * Quaternion.Euler(0f, 0f,
                Mathf.Sin(idle * 1.35f) * 1.4f * fade);
            title.rect.anchoredPosition = title.position + Vector2.up * (Mathf.Sin(idle * 1.9f) * 5f * fade);
            title.rect.localScale = title.scale * (1f + Mathf.Sin(idle * 1.9f + 1.1f) * 0.012f * fade);
        }

        foreach (var glint in glints)
        {
            float phase = elapsed * glint.speed + glint.phase;
            float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(phase), 2f);
            var rt = glint.image.rectTransform;
            // Su parıltıları akışla aşağı süzülür; sıcak olanlar (podyum) yerinde yüzer.
            rt.anchoredPosition = glint.origin + new Vector2(Mathf.Sin(phase * 0.6f) * 14f,
                glint.warm ? Mathf.Sin(phase * 0.7f) * 14f : -Mathf.Repeat(phase + Mathf.PI * 0.5f, Mathf.PI * 2f) * 6f);   // sıfırlama pulse=0 anında
            rt.localScale = new Vector3(Mathf.Lerp(0.5f, 1.2f, pulse), Mathf.Lerp(0.7f, 1.1f, pulse), 1f);
            var color = glint.image.color;
            color.a = pulse * glint.opacity * ease;
            glint.image.color = color;
        }
    }

    public void Reset()
    {
        active = false;
        entranceComplete = false;
        elapsed = 0f;
        startedFrame = -1;
        BoardScale = 1f;
        if (screen != null) screen.alpha = screenAlpha;
        title?.Restore();
        timer?.Restore();
        foreach (var tag in tags) tag.Restore();
        tags.Clear();
        if (ambience != null) ambience.gameObject.SetActive(false);
    }
}
