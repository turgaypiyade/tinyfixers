using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridge Repair yarış ekranı. Hiyerarşi editör kurulumundan gelir (TinyFixers ▸ Mockup ▸ Bridge Repair Event):
///   Board = arka plan (BridgeRepairBG) — boyutu ARKA PLAN PİKSELİ, ekrana ölçekle sığdırılır (genişliğe oturur,
///   taşarsa yüksekliğe); köprü anchor'ları, podyum slotları ve tüm şerit içeriği bu ölçeksiz uzayda yaşar →
///   hangi çözünürlükte olursa olsun çıkıntılara oturur. Anchor'lar Inspector'dan sürüklenerek ince ayarlanır.
///
/// Açılışta her köprü "en son gösterilen" duruma kurulur, sonra şimdiye kadar olan ilerleme oynatılır
/// (önce botlar birlikte, sonra oyuncu). Sonuç (ödül / eleme) controller'a bildirilir.
/// </summary>
public sealed class BridgeRepairMapScreen : MonoBehaviour
{
    [Header("Kök")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform board;
    [SerializeField] private Image boardImage;
    [SerializeField] private RectTransform lanesRoot;
    [SerializeField] private RectTransform tagsLayer;
    [SerializeField] private RectTransform podiumLayer;

    [Header("Köprü yerleşimi (arka plan pikseli)")]
    [Tooltip("Sol çıkıntıların ön-üst kenar ortası (üstten alta 5 köprü).")]
    [SerializeField] private RectTransform[] leftAnchors;
    [Tooltip("Sağ çıkıntıların ön-üst kenar ortası (üstten alta 5 köprü).")]
    [SerializeField] private RectTransform[] rightAnchors;
    [Tooltip("Podyum 1-2-3 üzerinde bitirenlerin avatar noktası.")]
    [SerializeField] private RectTransform[] podiumSlots;
    [SerializeField] private BridgeLaneMetrics metrics = new();
    [SerializeField] private Sprite bridgeLeftSprite;
    [SerializeField] private Sprite bridgeRightSprite;
    [SerializeField] private Sprite bridgeSegmentSprite;
    [SerializeField, Min(20f)] private float podiumAvatarSize = 92f;

    [Header("TopHUD")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Alttaki 'Sıran: N' satırı. Kapalı (kullanıcı): sıra zaten isim etiketlerinde görünüyor.")]
    [SerializeField] private bool showStatus = false;
    [Tooltip("Yanıp sönen 'Devam etmek için dokun' yazısının rengi (lacivert).")]
    [SerializeField] private Color continueColor = new Color32(18, 45, 120, 255);

    [Header("Dokunma")]
    [Tooltip("Tam ekran dokunma alanı: yarış sürüyorsa level başlatır, bittiyse ekranı kapatır.")]
    [SerializeField] private Button tapArea;
    [Tooltip("Yanıp sönen 'Devam etmek için dokun' yazısı.")]
    [SerializeField] private TMP_Text continueText;
    [Tooltip("Yazıyla birlikte açılıp kapanan kök (hap zemini). Boşsa yazının kendisi.")]
    [SerializeField] private GameObject continueRoot;
    [SerializeField] private Button closeButton;

    [Header("Animasyon")]
    [SerializeField, Min(0.1f)] private float botSpeedMul = 1.8f;
    [SerializeField, Min(0.1f)] private float playerSpeedMul = 1f;
    [Tooltip("Kısa ekran girişi, tabela hareketi ve su parıltıları.")]
    [SerializeField] private bool livelyPresentation = true;

    [Header("Nehir akışı")]
    [SerializeField] private bool animateRiver = true;
    [SerializeField, Range(0f, 2f)] private float riverFlowStrength = 1f;
    [SerializeField, Range(0f, 3f)] private float riverFlowSpeed = 1f;

    private BridgeRepairController controller;
    private readonly List<BridgeLaneView> lanes = new();
    private readonly List<GameObject> podiumAvatars = new();
    private readonly Dictionary<int, int> podiumOccupants = new();   // sıra → yarışmacı slot'u (yeni gelen pop'lar)
    private bool presenting;
    private bool bannerOpen;
    private bool showRequested;
    private int lastTimerSecond = int.MinValue;
    private BridgeRepairScreenMotion screenMotion;
    private BridgeRepairRiverFlow riverFlow;

    public bool IsOpen => root != null ? root.activeInHierarchy : gameObject.activeInHierarchy;

    private void Awake()
    {
        if (root != null && root != gameObject) root.SetActive(false);
        else if (!showRequested) { gameObject.SetActive(false); return; }
        WireButtons();
    }

    private bool wired;
    private void WireButtons()
    {
        if (wired) return;
        wired = true;
        if (tapArea != null)     tapArea.onClick.AddListener(OnTap);
        if (closeButton != null) closeButton.onClick.AddListener(() => { if (!presenting) Hide(); });
    }

    public void Open(BridgeRepairController owner, bool animate)
    {
        controller = owner;
        showRequested = true;
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        if (transform.parent != null) transform.parent.SetAsLastSibling();   // ana menü panellerinin üstünde
        WireButtons();
        ApplyStaticText();
        StopAllCoroutines();
        screenMotion?.Reset();
        if (riverFlow == null) riverFlow = new BridgeRepairRiverFlow(boardImage);
        riverFlow.Tick(0f, animateRiver, riverFlowSpeed, riverFlowStrength);
        bannerOpen = false;
        EventSfx.StartAmbient();
        StartCoroutine(Present(animate, entering: true));
    }

    private void OnTap()
    {
        if (presenting || bannerOpen || controller == null) return;
        EventSfx.Play(x => x.uiTap);
        if (controller.CanPlay) controller.RequestPlay();
        else Hide();
    }

    private void OnEnable() => BridgeRepairState.OnChanged += HandleStateChanged;

    // Ekran açıkken ilerleme değişirse (ör. debug +1, zaman atlama) hemen oynat.
    private void HandleStateChanged()
    {
        if (controller == null || presenting || !isActiveAndEnabled) return;
        if (root != null && !root.activeInHierarchy) return;
        StopAllCoroutines();
        StartCoroutine(Present(true));
    }

    public void Hide()
    {
        StopAllCoroutines();
        screenMotion?.Reset();
        presenting = false;
        EventSfx.StopAmbient();
        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        BridgeRepairState.OnChanged -= HandleStateChanged;
        StopAllCoroutines();
        screenMotion?.Reset();
        EventSfx.StopAmbient();
        presenting = false;
    }

    private void OnDestroy() => riverFlow?.Dispose();

    // ── Yerleşim ─────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (!IsOpen) return;
        screenMotion?.Tick(Time.unscaledDeltaTime);
        riverFlow?.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f), animateRiver, riverFlowSpeed, riverFlowStrength);
        FitBoard();
        RefreshTimer();
        if (continueText != null && continueText.gameObject.activeInHierarchy)
            continueText.alpha = 0.86f + 0.14f * Mathf.Sin(Time.unscaledTime * 2.6f);
    }

    // Genişliğe oturt (alt hizalı); ekrana sığmıyorsa yüksekliğe oturt. Board boyutu = arka plan pikseli.
    private void FitBoard()
    {
        if (board == null || viewport == null) return;
        Vector2 size = board.sizeDelta;
        if (size.x < 1f || size.y < 1f) return;
        Rect area = viewport.rect;
        float s = area.width / size.x;
        if (size.y * s > area.height) s = area.height / size.y;
        s *= screenMotion != null ? screenMotion.BoardScale : 1f;
        if (Mathf.Abs(board.localScale.x - s) > 0.0001f)
            board.localScale = new Vector3(s, s, 1f);
    }

    private void ApplyStaticText()
    {
        var cfg = controller != null ? controller.Config : BridgeRepairConfig.Shared;
        int levels = cfg != null ? cfg.levelsToFinish : 15;
        if (titleText != null) titleText.text = BridgeRepairUI.L("bridge_title", "BRIDGE REPAIR");
        if (statusText != null)
        {
            // Durum satırı hap zemininin içinde → zeminle birlikte gizle.
            var holder = statusText.transform.parent != null && statusText.transform.parent.name.EndsWith("Pill")
                ? statusText.transform.parent.gameObject : statusText.gameObject;
            holder.SetActive(showStatus);
        }
        if (continueText != null)
        {
            continueText.color = continueColor;
            continueText.outlineColor = Color.white;   // açık zeminde de koyu zeminde de okunsun
            continueText.outlineWidth = 0.18f;
        }
        if (subtitleText != null)
            subtitleText.text = BridgeRepairUI.LFormat("bridge_subtitle", "{0} seviye kazan, ödülleri kap!", levels);

    }

    private void RefreshTimer()
    {
        if (timerText == null || controller == null) return;
        DateTime end = controller.WindowEnd;
        TimeSpan remaining = end == DateTime.MinValue ? TimeSpan.Zero : end - DateTime.UtcNow;
        int second = (int)Math.Ceiling(remaining.TotalSeconds);
        if (second == lastTimerSecond) return;
        lastTimerSecond = second;
        timerText.text = end == DateTime.MinValue ? "--:--" : TimeFormat.Countdown(remaining);
    }

    // ── Sunum ────────────────────────────────────────────────────

    private IEnumerator Present(bool animate, bool entering = false)
    {
        presenting = true;
        SetPlayVisible(false);
        FitBoard();
        Canvas.ForceUpdateCanvases();

        var cfg = controller.Config;
        DateTime now = DateTime.UtcNow;
        int wins = Mathf.Min(BridgeRepairState.Wins, cfg.levelsToFinish);
        DateTime fromUtc = now;
        int fromWins = wins;
        if (animate)
        {
            fromUtc = BridgeRepairState.PresentedUtc;
            if (fromUtc < BridgeRepairState.JoinUtc || fromUtc > now) fromUtc = BridgeRepairState.JoinUtc;
            fromWins = Mathf.Min(BridgeRepairState.PresentedWins, wins);
        }

        RebuildLanes(cfg);
        podiumOccupants.Clear();
        foreach (var lane in lanes)
        {
            var c = lane.Contestant;
            int p = BridgeRepairRace.ProgressAt(cfg, c, fromUtc, fromWins);
            int rank = c.isPlayer
                ? (fromWins >= cfg.levelsToFinish ? BridgeRepairState.FinalRank : 0)
                : BridgeRepairRace.FinishRankAt(cfg, c, fromUtc);
            lane.SetInstant(p, rank);
        }
        RefreshPodium(fromUtc, fromWins, pop: false);
        RefreshStatus(fromUtc, fromWins);

        if (entering && livelyPresentation)
        {
            if (screenMotion == null)
                screenMotion = new BridgeRepairScreenMotion(root != null ? root : gameObject,
                    board, titleText, timerText);
            screenMotion.Begin(tagsLayer);
            // Eski sabit bekleme yerine açılış hareketi; ilerleme bunun hemen ardından başlar.
            // Sunum ile görsel aynı saate bağlı: uzun bir yükleme karesi girişi yutmasın.
            while (!screenMotion.EntranceComplete) yield return null;
        }

        // Önce botlar (birlikte), sonra oyuncu — oyuncunun anı en sonda ve tek başına.
        int running = 0;
        BridgeLaneView playerLane = null;
        foreach (var lane in lanes)
        {
            var c = lane.Contestant;
            if (c.isPlayer) { playerLane = lane; continue; }
            int target = BridgeRepairRace.BotProgressAt(cfg, c.slot, now);
            if (target <= lane.ShownProgress) continue;
            running++;
            StartCoroutine(Track(lane.Animate(target, BridgeRepairRace.FinishRankAt(cfg, c, now), botSpeedMul),
                () => running--));
        }
        while (running > 0) yield return null;
        RefreshPodium(now, fromWins, pop: true);

        if (playerLane != null && wins > playerLane.ShownProgress)
        {
            yield return Wait(0.15f);
            int rank = BridgeRepairState.IsFinished ? BridgeRepairState.FinalRank : 0;
            yield return playerLane.Animate(wins, rank, playerSpeedMul);
        }

        BridgeRepairState.MarkPresented(wins, now);
        RefreshPodium(now, wins, pop: true);
        RefreshStatus(now, wins);
        presenting = false;
        SetPlayVisible(controller.CanPlay);

        controller.OnMapPresented(this);
    }

    private IEnumerator Track(IEnumerator routine, Action done)
    {
        yield return routine;
        done?.Invoke();
    }

    private void RebuildLanes(BridgeRepairConfig cfg)
    {
        foreach (var lane in lanes)
            if (lane != null)
            {
                lane.gameObject.SetActive(false);
                Destroy(lane.gameObject);
            }
        lanes.Clear();
        if (tagsLayer != null)
            for (int i = tagsLayer.childCount - 1; i >= 0; i--)
            {
                var tag = tagsLayer.GetChild(i).gameObject;
                tag.SetActive(false);
                Destroy(tag);
            }
        if (lanesRoot == null || leftAnchors == null || rightAnchors == null) return;

        var contestants = BridgeRepairRace.BuildContestants(cfg);
        // Üstteki köprü önce çizilsin: alttaki köprünün direkleri üsttekinin önünde kalır (derinlik).
        contestants.Sort((x, y) => x.lane.CompareTo(y.lane));
        foreach (var c in contestants)
        {
            if (c.lane >= leftAnchors.Length || c.lane >= rightAnchors.Length) continue;
            var l = leftAnchors[c.lane];
            var r = rightAnchors[c.lane];
            if (l == null || r == null) continue;
            Vector2 left = lanesRoot.InverseTransformPoint(l.position);
            Vector2 right = lanesRoot.InverseTransformPoint(r.position);
            lanes.Add(BridgeLaneView.Create(lanesRoot, tagsLayer, cfg, metrics, c, left, right,
                TagLeftAnchor(c.lane, left), bridgeLeftSprite, bridgeRightSprite, bridgeSegmentSprite));
        }
    }

    /// <summary>
    /// İsim etiketi karakterin ÜSTÜNDE dursun: etiket bir üst şeridin sol anchor'ına göre yerleşir.
    /// En üst şerit için alttaki şeritle arası kadar yukarı uzatılır.
    /// </summary>
    private Vector2 TagLeftAnchor(int lane, Vector2 left)
    {
        Vector2 At(int i) => lanesRoot.InverseTransformPoint(leftAnchors[i].position);
        bool Valid(int i) => i >= 0 && i < leftAnchors.Length && leftAnchors[i] != null;
        if (Valid(lane - 1)) return At(lane - 1);
        if (Valid(lane + 1)) return left + (left - At(lane + 1));
        return left;
    }

    private void RefreshPodium(DateTime utc, int playerWins, bool pop)
    {
        var cfg = controller.Config;
        foreach (var go in podiumAvatars) if (go != null) Destroy(go);
        podiumAvatars.Clear();
        var previous = new Dictionary<int, int>(podiumOccupants);
        podiumOccupants.Clear();
        if (podiumSlots == null || podiumLayer == null) return;

        foreach (var lane in lanes)
        {
            var c = lane.Contestant;
            int rank = c.isPlayer
                ? (playerWins >= cfg.levelsToFinish ? BridgeRepairState.FinalRank : 0)
                : BridgeRepairRace.FinishRankAt(cfg, c, utc);
            if (rank < 1 || rank > podiumSlots.Length || podiumSlots[rank - 1] == null) continue;

            Vector2 pos = podiumLayer.InverseTransformPoint(podiumSlots[rank - 1].position);
            var avatar = BridgeRepairUI.Avatar($"Podium{rank}", podiumLayer, c.avatar, podiumAvatarSize, pos,
                c.isPlayer ? BridgeRepairUI.Gold : Color.white);
            avatar.localPosition = pos;
            podiumAvatars.Add(avatar.gameObject);
            podiumOccupants[rank] = c.slot;
            bool isNew = !previous.TryGetValue(rank, out int prevSlot) || prevSlot != c.slot;
            if (pop && isNew)
            {
                StartCoroutine(PopIn(avatar));
                EventSfx.Play(x => x.podiumArrive, EventSfx.ScaleFor(c.isPlayer));
            }
        }
    }

    private void RefreshStatus(DateTime utc, int playerWins)
    {
        if (statusText == null) return;
        var cfg = controller.Config;
        if (playerWins >= cfg.levelsToFinish && BridgeRepairState.FinalRank > 0)
            statusText.text = BridgeRepairUI.LFormat("bridge_status_finished", "{0}. oldun!", BridgeRepairState.FinalRank);
        else if (BridgeRepairRace.IsEliminated(cfg, utc))
            statusText.text = BridgeRepairUI.L("bridge_status_eliminated", "İlk 3 doldu — yarış bitti");
        else if (!controller.IsRunLive)
            statusText.text = BridgeRepairUI.L("bridge_status_over", "Yarış sona erdi");
        else
        {
            int ahead = 0;
            foreach (var lane in lanes)
            {
                var c = lane.Contestant;
                if (c.isPlayer) continue;
                if (BridgeRepairRace.ProgressAt(cfg, c, utc, playerWins) > playerWins) ahead++;
            }
            statusText.text = BridgeRepairUI.LFormat("bridge_status_racing", "Sıran: {0}   |   {1}/{2}",
                ahead + 1, playerWins, cfg.levelsToFinish);
        }
    }

    // Sunum bitince yanıp sönen dokunma yazısı: yarış sürüyorsa "devam", bittiyse "kapat".
    private void SetPlayVisible(bool visible)
    {
        if (continueText == null) return;
        (continueRoot != null ? continueRoot : continueText.gameObject).SetActive(!presenting);
        continueText.text = visible
            ? BridgeRepairUI.L("bridge_tap_continue", "Devam etmek için dokun")
            : BridgeRepairUI.L("bridge_tap_close", "Kapatmak için dokun");
    }

    // ── Bitiş / eleme bandı ──────────────────────────────────────

    /// Ekranın ortasında başlık + açıklama; dokununca kapanır, onClosed çağrılır (harita açık kalır).
    public void ShowEndBanner(string title, string body, Action onClosed)
    {
        if (bannerOpen) return;
        bannerOpen = true;
        EventSfx.Play(x => x.eliminated);
        var parent = root != null ? root.transform : transform;
        var dim = BridgeRepairUI.Solid("EndBanner", parent, new Color(0f, 0f, 0f, 0.8f));
        dim.raycastTarget = true;
        var design = BridgeRepairUI.Rect("Design", dim.transform, new Vector2(900f, 600f), Vector2.zero);
        BridgeRepairUI.Label("Title", design, title, 96f, new Vector2(880f, 140f), new Vector2(0f, 120f));
        var bodyText = BridgeRepairUI.Label("Body", design, body, 44f, new Vector2(820f, 180f), new Vector2(0f, -30f));
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        var tap = BridgeRepairUI.Label("Tap", design, BridgeRepairUI.L("bridge_tap_close", "Kapatmak için dokun"), 40f,
            new Vector2(820f, 70f), new Vector2(0f, -210f), BridgeRepairUI.Gold);
        var button = dim.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.interactable = false;
        button.onClick.AddListener(() =>
        {
            bannerOpen = false;
            Destroy(dim.gameObject);
            onClosed?.Invoke();
        });
        StartCoroutine(BannerIn(design, tap, button));
    }

    private IEnumerator BannerIn(RectTransform design, TMP_Text tap, Button button)
    {
        float t = 0f;
        while (t < 0.35f && design != null)
        {
            t += Time.unscaledDeltaTime;
            design.localScale = Vector3.one * BridgeRepairUI.EaseOutBack(Mathf.Clamp01(t / 0.35f));
            yield return null;
        }
        yield return Wait(0.4f);
        if (button != null) button.interactable = true;
        while (tap != null)
        {
            t += Time.unscaledDeltaTime;
            tap.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 2.6f));
            yield return null;
        }
    }

    private static IEnumerator PopIn(RectTransform rt)
    {
        float t = 0f;
        while (t < 0.4f && rt != null)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * BridgeRepairUI.EaseOutBack(Mathf.Clamp01(t / 0.4f));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }
}
