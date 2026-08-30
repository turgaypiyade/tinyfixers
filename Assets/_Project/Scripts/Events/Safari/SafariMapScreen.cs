using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tiny Safari tam-ekran harita overlay'i. Sol-üstte roster dairesi + sayaç, yolda
/// 7 pitstop ve pitstoptaki YUVARLAK avatar kalabalığı, altta dokunma prompt'u.
///
/// Dönüş koreografisi (kullanıcı isteği):
///  - İlerleme: önce herkes ESKİ pitstopta toplanır → elenenler (sayısı = elenen kadar) sağdaki
///    uçuruma DÖKÜLÜR → kalanlar bir sonraki pitstopa SIÇRAR.
///  - Düşme: eski pitstopta toplanır, oyuncu + elenenler uçuruma dökülür, sonra başa döner (retry).
///  - Tamamlandı: kutlama + paylaşılan ödül.
///
/// Kurulum: root, avatarStack (roster), counterText, pitstopAnchors (1..7), cliffPoint,
/// continueButton/Label, statusText, closeButton bağlanır. Pitstop kalabalığı runtime'da üretilir.
/// </summary>
public sealed class SafariMapScreen : SafariMapScreenBase
{
    [Header("Kök")]
    [SerializeField] private GameObject root;

    [Header("Sol-üst (oyuncu avatarı) + Sayaç")]
    [SerializeField] private SafariAvatarStackView avatarStack;
    [SerializeField] private TMP_Text counterText;
    [Tooltip("Sol-üst köşedeki oyuncu avatarının boyutu.")]
    [SerializeField, Min(16)] private float soloAvatarSize = 154f;

    [Header("Pitstop Kalabalığı")]
    [Tooltip("Avatar boyutu (px).")]
    [SerializeField, Min(16)] private float crowdAvatarSize = 112f;
    [SerializeField, Min(0)]  private float crowdSpread = 58f;
    [Tooltip("İlk pitstoptaki (ekrana sığdığı kadar) kalabalık.")]
    [SerializeField, Min(1)]  private int   startCrowd = 100;
    [Tooltip("Yarış 100 kişi sürerken ekranda çizilecek maksimum avatar sayısı.")]
    [SerializeField, Min(1)] private int maxVisibleCrowdAvatars = 20;
    [SerializeField, Min(1)]  private int   minCrowd = 2;
    [Tooltip("Her pitstopta botların yarışta kalma ihtimali. Yüksek değer daha yavaş eleme demektir.")]
    [SerializeField, Range(0.5f, 0.98f)] private float botRoundWinChance = 0.88f;

    [Header("Yol")]
    [Tooltip("Yarışa ilk girildiğinde oyuncuların beklediği başlangıç noktası.")]
    [SerializeField] private RectTransform startAnchor;
    [Tooltip("Pitstop noktaları — alttan üste sıralı (1..7).")]
    [SerializeField] private RectTransform[] pitstopAnchors;
    [Tooltip("Kare placeholder marker (gizlenir; kalabalık temsil eder).")]
    [SerializeField] private RectTransform playerMarker;
    [Tooltip("Düşenlerin gideceği sağ uçurum noktası.")]
    [SerializeField] private RectTransform cliffPoint;

    [Header("Kontroller")]
    [SerializeField] private GameObject continueRoot;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Color promptTextColor = new Color(1f, 0.43f, 0.24f, 1f);

    [Header("Animasyon")]
    [SerializeField, Min(0.2f)] private float counterDuration = 1.4f;
    [SerializeField, Min(0.1f)] private float gatherPause = 0.5f;
    [SerializeField, Min(0.1f)] private float advanceDuration = 2f;
    [SerializeField, Min(0.1f)] private float fallDuration = 2f;

    [Header("İlk Giriş")]
    [SerializeField, Min(0.1f)] private float initialAvatarRevealDuration = 0.42f;
    [SerializeField, Min(0f)] private float initialAvatarRevealStagger = 0.018f;

    [Header("Final Ödül")]
    [SerializeField] private Sprite finalGoldMoneySprite;
    [SerializeField, Min(0.1f)] private float finalAvatarHopDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float rewardCountDuration = 0.85f;

    private SafariEventController controller;
    private bool countedThisSession;
    private Coroutine active;
    private bool continuePromptVisible;
    private bool continuePromptArmed;

    private RectTransform crowdRoot;
    private SafariAvatarStackView crowdStack;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (closeButton != null)    closeButton.onClick.AddListener(Hide);
        PrepareContinuePrompt();
        ApplyPromptTextColor();
        // root == bu obje ise burada kapatma (lazy-Awake tuzağı, StartCoroutine ölür). Editör pasif author'lar.
        if (root != null && root != gameObject) root.SetActive(false);
    }

    public override void Open(SafariEventController owner, SafariRoundOutcome outcome)
    {
        controller = owner;
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);

        ApplyPromptTextColor();
        EnsureCrowd();
        BuildRoster();

        if (active != null) StopCoroutine(active);
        active = StartCoroutine(Present(outcome));
    }

    public override void Hide()
    {
        if (active != null) { StopCoroutine(active); active = null; }
        if (root != null) root.SetActive(false);
    }

    // ── Kurulum ──────────────────────────────────────────────────

    private void EnsureCrowd()
    {
        if (crowdStack != null) return;
        Transform parent = root != null ? root.transform : transform;

        var go = new GameObject("SafariPitstopCrowd", typeof(RectTransform)) { layer = parent.gameObject.layer };
        crowdRoot = (RectTransform)go.transform;
        crowdRoot.SetParent(parent, false);
        crowdRoot.anchorMin = crowdRoot.anchorMax = new Vector2(0.5f, 0.5f);
        crowdRoot.pivot = new Vector2(0.5f, 0.5f);
        crowdStack = go.AddComponent<SafariAvatarStackView>();

        var bg = parent.Find("BG");
        int targetSibling = bg != null ? bg.GetSiblingIndex() + 1 : 1;
        crowdRoot.SetSiblingIndex(Mathf.Clamp(targetSibling, 0, parent.childCount - 1));
        crowdStack.CopyHelmetSettingsFrom(avatarStack);

        // Kare placeholder marker'ı gizle — artık kalabalık temsil ediyor.
        if (playerMarker != null)
        {
            var mimg = playerMarker.GetComponent<Image>();
            if (mimg != null) mimg.enabled = false;
        }
    }

    // Sol-üst köşe: SADECE oyuncunun avatarı (kullanıcı isteği — roster yığını değil).
    private void BuildRoster()
    {
        if (avatarStack == null) return;
        var player = new SafariParticipant
        {
            id          = "player",
            displayName = PlayerProfile.PlayerName,
            avatar      = PlayerAvatarProvider.Current,
            level       = CurrentLevel.Global,
            isPlayer    = true
        };
        avatarStack.BuildSolo(player, Mathf.Max(soloAvatarSize, 154f));
    }

    private int TotalParticipants()
    {
        var cfg = controller != null ? controller.Config : null;
        return Mathf.Max(1, cfg != null ? cfg.participantVisualCount : startCrowd);
    }

    private int VisibleCrowdAt(int progress)
    {
        return Mathf.Clamp(CrowdSizeAt(progress), 1, Mathf.Min(maxVisibleCrowdAvatars, TotalParticipants()));
    }

    private float CrowdAvatarPixels() => Mathf.Max(crowdAvatarSize, 112f);

    private float CrowdSpreadPixels()
    {
        float size = CrowdAvatarPixels();
        return crowdSpread > 0f ? Mathf.Min(crowdSpread, size * 0.58f) : size * 0.52f;
    }

    private int CrowdSizeAt(int pitstop)
    {
        int total = TotalParticipants();
        if (pitstop <= 0) return total;

        int survivors = 1; // oyuncu
        int maxPitstop = controller != null && controller.Config != null ? controller.Config.pitstopCount : 7;
        int rounds = Mathf.Clamp(pitstop, 0, maxPitstop);
        float winChance = Mathf.Clamp(botRoundWinChance, 0.5f, 0.98f);

        for (int bot = 1; bot < total; bot++)
        {
            bool alive = true;
            for (int round = 1; round <= rounds; round++)
            {
                if (Hash01(bot, round) > winChance)
                {
                    alive = false;
                    break;
                }
            }

            if (alive) survivors++;
        }

        return Mathf.Clamp(survivors, Mathf.Min(minCrowd, total), total);
    }

    private static float Hash01(int botIndex, int round)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)(botIndex * 73856093)) * 16777619u;
            h = (h ^ (uint)(round * 19349663)) * 16777619u;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }

    // Kalabalığı verilen ilerleme noktasında kurar: 0 = StartAnchor, 1..N = Pitstop.
    private void BuildCrowdAt(int progress)
    {
        EnsureCrowd();
        if (crowdStack == null) return;

        var anchor = AnchorFor(progress);
        if (crowdRoot != null && anchor != null) crowdRoot.position = anchor.position;

        int total = TotalParticipants();
        int n = VisibleCrowdAt(progress);
        var list = SafariParticipantPool.Build(total, CurrentLevel.Global, seed: 1);
        crowdStack.Build(list, n, CrowdAvatarPixels(), CrowdSpreadPixels());
        RefreshCounter(progress);
    }

    // ── Sunum akışı ──────────────────────────────────────────────

    private IEnumerator Present(SafariRoundOutcome outcome)
    {
        SetContinueVisible(false);
        RefreshStatus();

        switch (outcome)
        {
            case SafariRoundOutcome.Advanced:
            {
                int newP = SafariState.CurrentPitstop;
                int oldP = Mathf.Max(0, newP - 1);
                BuildCrowdAt(oldP);                                  // herkes eski noktada
                yield return new WaitForSecondsRealtime(gatherPause);
                int elim = Mathf.Max(0, VisibleCrowdAt(oldP) - VisibleCrowdAt(newP));
                yield return EliminateFall(elim, includePlayer: false);   // elenenler dökülür
                RefreshCounter(newP);
                yield return JumpCrowd(oldP, newP);                  // kalanlar sıçrar
                break;
            }

            case SafariRoundOutcome.Fell:
            {
                int oldP = controller != null && controller.FallFromPitstop >= 0
                    ? controller.FallFromPitstop : 0;
                BuildCrowdAt(oldP);
                yield return new WaitForSecondsRealtime(gatherPause);
                int elim = Mathf.Max(1, VisibleCrowdAt(oldP) - VisibleCrowdAt(oldP + 1));
                yield return EliminateFall(elim, includePlayer: true);    // oyuncu da dökülür
                yield return new WaitForSecondsRealtime(0.3f);
                BuildCrowdAt(0);                                     // yarış başa döner (retry)
                break;
            }

            case SafariRoundOutcome.Completed:
                BuildCrowdAt(SafariState.CurrentPitstop);
                RefreshCounter(SafariState.CurrentPitstop);
                if (continueRoot != null) continueRoot.SetActive(false);
                yield return CelebrateFinalCrowd();
                yield return ShowFinalRewardOverlay();
                yield break;

            default: // None — taze açılış
                if (!countedThisSession)
                {
                    yield return RunInitialJoinReveal();
                    countedThisSession = true;
                }
                else if (counterText != null) counterText.text = FullCountText();
                if (countedThisSession && crowdStack != null && crowdStack.BotCount <= 0)
                    BuildCrowdAt(SafariState.CurrentPitstop);
                break;
        }

        SetContinueVisible(true);
        RefreshContinueInteractable();
    }

    // ── Koreografi ───────────────────────────────────────────────

    // Elenenleri kalabalıktan çıkar ve kısa yana kayışla aşağı dök (sayı = elenen kadar).
    private IEnumerator EliminateFall(int botCount, bool includePlayer)
    {
        if (crowdStack == null) yield break;
        Transform host = root != null ? root.transform : transform;

        var fallers = crowdStack.DetachBotFallers(botCount, host);
        if (includePlayer)
        {
            var pl = crowdStack.DetachPlayer(host);
            if (pl != null) fallers.Add(pl);
        }
        if (fallers.Count == 0) yield break;

        var starts = new Vector2[fallers.Count];
        var targets = new Vector2[fallers.Count];
        var startRot = new Quaternion[fallers.Count];
        var targetRot = new Quaternion[fallers.Count];
        float drop = Mathf.Max(360f, CrowdAvatarPixels() * 4.8f);
        float side = Mathf.Clamp(Mathf.Tan(5f * Mathf.Deg2Rad) * drop, 36f, 72f);
        for (int i = 0; i < fallers.Count; i++)
        {
            if (fallers[i] == null) continue;
            starts[i] = fallers[i].anchoredPosition;
            float sign = cliffPoint != null && cliffPoint.position.x < fallers[i].position.x ? -1f : 1f;
            float stagger = 0.85f + (i % 3) * 0.12f;
            targets[i] = starts[i] + new Vector2(sign * side * stagger, -drop);
            startRot[i] = fallers[i].localRotation;
            targetRot[i] = Quaternion.Euler(0f, 0f, -sign * 5f);
        }

        float t = 0f;
        while (t < fallDuration)
        {
            t += Time.unscaledDeltaTime;
            float elapsed = Mathf.Min(t, fallDuration);
            for (int i = 0; i < fallers.Count; i++)
            {
                if (fallers[i] == null) continue;
                float k = StaggeredProgress(i, fallers.Count, elapsed, fallDuration);
                if (k <= 0f) continue;
                float e = k * k;
                fallers[i].anchoredPosition = Vector2.LerpUnclamped(starts[i], targets[i], e);
                fallers[i].localRotation = Quaternion.Slerp(startRot[i], targetRot[i], k);
                SetAlpha(fallers[i], 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, k)));
            }
            yield return null;
        }
        for (int i = 0; i < fallers.Count; i++)
            if (fallers[i] != null) Destroy(fallers[i].gameObject);
    }

    // Kalan kalabalık bir sonraki noktaya batch'ler halinde sıçrar.
    private IEnumerator JumpCrowd(int oldP, int newP)
    {
        if (crowdRoot == null || crowdStack == null) yield break;
        Transform host = root != null ? root.transform : transform;
        var movers = crowdStack.DetachAll(host);
        if (movers.Count == 0) yield break;

        Vector3 a = AnchorPos(oldP);
        Vector3 b = AnchorPos(newP);
        float hop = Vector3.Distance(a, b) * 0.15f;

        var starts = new Vector3[movers.Count];
        var targets = new Vector3[movers.Count];
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            starts[i] = movers[i].position;
            targets[i] = b + (starts[i] - a);
        }

        float t = 0f;
        while (t < advanceDuration)
        {
            t += Time.unscaledDeltaTime;
            float elapsed = Mathf.Min(t, advanceDuration);
            for (int i = 0; i < movers.Count; i++)
            {
                if (movers[i] == null) continue;
                int order = i == 0 ? movers.Count - 1 : movers.Count - i - 1;
                float k = StaggeredProgress(order, movers.Count, elapsed, advanceDuration);
                if (k <= 0f) continue;
                float e = Mathf.SmoothStep(0f, 1f, k);
                Vector3 p = Vector3.Lerp(starts[i], targets[i], e);
                p.y += Mathf.Sin(e * Mathf.PI) * hop;
                movers[i].position = p;
            }
            yield return null;
        }

        if (crowdRoot != null)
            crowdRoot.position = b;

        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            movers[i].position = targets[i];
        }

        crowdStack.AdoptDetached(movers);
        RefreshCounter(newP);
    }

    private IEnumerator CelebrateFinalCrowd()
    {
        if (crowdStack == null) yield break;

        var avatars = crowdStack.SnapshotAvatars();
        if (avatars.Count == 0) yield break;

        var baseScales = new Vector3[avatars.Count];
        for (int i = 0; i < avatars.Count; i++)
        {
            if (avatars[i] == null) continue;
            baseScales[i] = avatars[i].localScale;
        }

        float duration = Mathf.Max(0.1f, finalAvatarHopDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float elapsed = Mathf.Min(t, duration);
            for (int i = 0; i < avatars.Count; i++)
            {
                if (avatars[i] == null) continue;
                float phase = Mathf.Clamp01((elapsed - (i % 8) * 0.035f) / (duration * 0.75f));
                float hop = Mathf.Sin(phase * Mathf.PI) * 0.18f;
                avatars[i].localScale = baseScales[i] * (1f + hop);
            }
            yield return null;
        }

        for (int i = 0; i < avatars.Count; i++)
            if (avatars[i] != null) avatars[i].localScale = baseScales[i];
    }

    private IEnumerator ShowFinalRewardOverlay()
    {
        Transform parent = root != null ? root.transform : transform;
        int winners = Mathf.Max(1, CrowdSizeAt(SafariState.CurrentPitstop));
        var cfg = controller != null ? controller.Config : null;
        int prizePool = cfg != null ? cfg.prizePoolGold : 0;
        int share = Mathf.Max(1, prizePool / winners);

        var overlay = NewStretchRect("SafariRewardOverlay", parent);
        overlay.SetAsLastSibling();
        var dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.88f);
        dim.raycastTarget = true;

        var coin = NewRect("GoldMoney", overlay, 220f, new Vector2(0f, 180f));
        var coinImg = coin.gameObject.AddComponent<Image>();
        coinImg.sprite = ResolveGoldMoneySprite();
        coinImg.preserveAspect = true;
        coinImg.raycastTarget = false;

        var shareText = NewText("ShareText", overlay, 44, new Vector2(0f, 24f), new Vector2(820f, 120f));
        shareText.text = $"{prizePool:N0} altını {winners} kişi ile paylaşıyorsun";

        var amountText = NewText("RewardAmount", overlay, 72, new Vector2(0f, -100f), new Vector2(720f, 120f));
        amountText.text = "+0";

        var tapText = NewText("TapText", overlay, 30, new Vector2(0f, -240f), new Vector2(720f, 80f));
        tapText.text = "Cüzdana eklemek için dokun";

        yield return AnimateRewardOverlay(coin, amountText, share);
        yield return null;

        while (!WasContinueTap())
            yield return null;

        controller?.ClaimFinalReward(share, winners);
        Destroy(overlay.gameObject);
        Hide();
    }

    private IEnumerator AnimateRewardOverlay(RectTransform coin, TMP_Text amountText, int share)
    {
        float duration = Mathf.Max(0.1f, rewardCountDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = Mathf.SmoothStep(0f, 1f, k);
            if (coin != null)
            {
                float pulse = 1f + Mathf.Sin(k * Mathf.PI * 5f) * 0.06f;
                coin.localScale = Vector3.one * pulse;
            }
            if (amountText != null)
                amountText.text = $"+{Mathf.RoundToInt(Mathf.Lerp(0, share, e)):N0}";
            yield return null;
        }

        if (coin != null) coin.localScale = Vector3.one;
        if (amountText != null) amountText.text = $"+{share:N0}";
    }

    private static float StaggeredProgress(int index, int count, float elapsed, float totalDuration)
    {
        if (count <= 1 || totalDuration <= 0f)
            return totalDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / totalDuration);

        int activeSlots = Mathf.Min(2, count);
        float step = totalDuration / Mathf.Max(1, count + activeSlots - 1);
        float itemDuration = step * activeSlots;
        float start = index * step;
        return Mathf.Clamp01((elapsed - start) / itemDuration);
    }

    private void SetAlpha(RectTransform rt, float a)
    {
        var imgs = rt.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imgs.Length; i++)
        {
            var c = imgs[i].color; c.a = a; imgs[i].color = c;
        }
    }

    private RectTransform AnchorFor(int progress)
    {
        if (progress <= 0 && startAnchor != null) return startAnchor;
        if (pitstopAnchors == null || pitstopAnchors.Length == 0) return null;
        int idx = Mathf.Clamp(progress - 1, 0, pitstopAnchors.Length - 1);
        return pitstopAnchors[idx];
    }

    private Vector3 AnchorPos(int progress)
    {
        var a = AnchorFor(progress);
        return a != null ? a.position : (crowdRoot != null ? crowdRoot.position : Vector3.zero);
    }

    // ── Sayaç ────────────────────────────────────────────────────

    private IEnumerator RunInitialJoinReveal()
    {
        EnsureCrowd();

        int progress = SafariState.CurrentPitstop;
        var anchor = AnchorFor(progress);
        if (crowdRoot != null && anchor != null) crowdRoot.position = anchor.position;

        int total = TotalParticipants();
        int n = VisibleCrowdAt(progress);
        var list = SafariParticipantPool.Build(total, CurrentLevel.Global, seed: 1);
        crowdStack.Build(list, n, CrowdAvatarPixels(), CrowdSpreadPixels());
        RefreshCounter(progress);

        var avatars = crowdStack.SnapshotAvatars();
        var targetScales = new Vector3[avatars.Count];
        for (int i = 0; i < avatars.Count; i++)
        {
            if (avatars[i] == null) continue;
            targetScales[i] = avatars[i].localScale;
            avatars[i].localScale = Vector3.zero;
            SetAlpha(avatars[i], 0f);
        }

        float itemDuration = Mathf.Max(0.1f, initialAvatarRevealDuration);
        float totalDuration = Mathf.Max(counterDuration, itemDuration + Mathf.Max(0, avatars.Count - 1) * initialAvatarRevealStagger);

        if (statusText != null)
        {
            statusText.color = promptTextColor;
            statusText.text = "Kullanıcılar seçiliyor...";
        }

        float t = 0f;
        while (t < totalDuration)
        {
            t += Time.unscaledDeltaTime;
            float elapsed = Mathf.Min(t, totalDuration);

            for (int i = 0; i < avatars.Count; i++)
            {
                if (avatars[i] == null) continue;
                float start = i * initialAvatarRevealStagger;
                float k = Mathf.Clamp01((elapsed - start) / itemDuration);
                float e = Mathf.SmoothStep(0f, 1f, k);
                float pop = e + Mathf.Sin(e * Mathf.PI) * 0.08f;
                avatars[i].localScale = targetScales[i] * pop;
                SetAlpha(avatars[i], k);
            }

            if (counterText != null)
            {
                counterText.color = promptTextColor;
                counterText.text = CountText(CrowdSizeAt(progress), TotalParticipants());
            }

            yield return null;
        }

        for (int i = 0; i < avatars.Count; i++)
        {
            if (avatars[i] == null) continue;
            avatars[i].localScale = targetScales[i];
            SetAlpha(avatars[i], 1f);
        }

        if (counterText != null)
        {
            counterText.color = promptTextColor;
            counterText.text = CountText(CrowdSizeAt(progress), TotalParticipants());
        }
        RefreshStatus();
    }

    private string FullCountText()
    {
        return CountText(CrowdSizeAt(SafariState.CurrentPitstop), TotalParticipants());
    }

    private string CountText(int n, int target) => $"{n} / {target}";

    private void RefreshCounter(int progress)
    {
        if (counterText == null) return;
        counterText.color = promptTextColor;
        counterText.text = CountText(CrowdSizeAt(progress), TotalParticipants());
    }

    // ── Devam / durum ────────────────────────────────────────────

    private void PrepareContinuePrompt()
    {
        if (continueLabel != null)
        {
            continueLabel.color = promptTextColor;
            continueLabel.text = "Devam etmek için dokunun";
        }

        if (continueRoot != null)
        {
            var rootImage = continueRoot.GetComponent<Image>();
            if (rootImage != null && (continueLabel == null || rootImage.gameObject != continueLabel.gameObject))
                rootImage.enabled = false;
        }

        if (continueButton == null) return;

        continueButton.transition = Selectable.Transition.None;
        continueButton.enabled = false;
        if (continueButton.targetGraphic != null && (continueLabel == null || continueButton.targetGraphic.gameObject != continueLabel.gameObject))
            continueButton.targetGraphic.enabled = false;
        continueButton.targetGraphic = null;

        var img = continueButton.GetComponent<Image>();
        if (img != null) img.enabled = false;
    }

    private void OnContinueClicked()
    {
        if (controller == null) return;
        if (!controller.CanContinueNow(out _)) { RefreshStatus(); return; }
        controller.RequestContinue();
    }

    private void SetContinueVisible(bool visible)
    {
        continuePromptVisible = visible;
        continuePromptArmed = false;
        if (continueRoot != null) continueRoot.SetActive(visible);
        else if (continueButton != null) continueButton.gameObject.SetActive(visible);

        if (visible && isActiveAndEnabled)
            StartCoroutine(ArmContinuePromptNextFrame());
    }

    private IEnumerator ArmContinuePromptNextFrame()
    {
        yield return null;
        continuePromptArmed = continuePromptVisible;
    }

    private void RefreshContinueInteractable()
    {
        bool canContinue = controller != null && controller.CanContinueNow(out _);
        if (continueButton != null) continueButton.interactable = canContinue;
        if (continueLabel != null)
        {
            continueLabel.color = promptTextColor;
            continueLabel.text = canContinue ? "Devam etmek için dokunun" : "Tekrar denemek için bekleyin";
        }
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (statusText == null) return;
        statusText.color = promptTextColor;
        if (controller == null) { statusText.text = ""; return; }

        if (!controller.CanContinueNow(out var remaining))
        {
            statusText.text = $"Tekrar denemek için: {FormatRemaining(remaining)}";
        }
        else
        {
            int n = controller.Config != null ? controller.Config.pitstopCount : 7;
            int pos = Mathf.Clamp(SafariState.CurrentPitstop, 0, n);
            statusText.text = pos <= 0 ? $"Başlangıç / {n}" : $"Pitstop {pos} / {n}";
        }
    }

    private void Update()
    {
        if (!IsOpen() || controller == null) return;

        if (continueButton != null && !continueButton.interactable)
            RefreshContinueInteractable();

        if (!continuePromptVisible || !continuePromptArmed) return;
        if (!controller.CanContinueNow(out _)) return;
        if (WasContinueTap())
            OnContinueClicked();
    }

    private bool IsOpen() => root != null ? root.activeSelf : gameObject.activeInHierarchy;

    private bool WasContinueTap()
    {
        if (Pointer.current != null && Pointer.current.press.wasReleasedThisFrame)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            return true;

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.wasReleasedThisFrame)
                    return true;
            }
        }

        return false;
    }

    private void ApplyPromptTextColor()
    {
        if (continueLabel != null) continueLabel.color = promptTextColor;
        if (statusText != null) statusText.color = promptTextColor;
        if (counterText != null) counterText.color = promptTextColor;
    }

    private RectTransform NewStretchRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private RectTransform NewRect(string name, Transform parent, float size, Vector2 pos)
    {
        return NewRect(name, parent, new Vector2(size, size), pos);
    }

    private RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private TMP_Text NewText(string name, Transform parent, int fontSize, Vector2 pos, Vector2 size)
    {
        var rt = NewRect(name, parent, size, pos);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = true;
        text.color = promptTextColor;
        text.fontSize = fontSize;
        return text;
    }

    private static Sprite _goldCoinSprite;
    private Sprite ResolveGoldMoneySprite()
    {
        if (finalGoldMoneySprite != null) return finalGoldMoneySprite;
#if UNITY_EDITOR
        finalGoldMoneySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/GoldMoney.png");
        if (finalGoldMoneySprite != null) return finalGoldMoneySprite;
#endif
        return GoldCoinSprite();
    }

    private static Sprite GoldCoinSprite()
    {
        if (_goldCoinSprite != null) return _goldCoinSprite;

        const int s = 128;
        float half = s * 0.5f;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color[s * s];
        Color outer = new Color(1f, 0.56f, 0.04f, 1f);
        Color inner = new Color(1f, 0.88f, 0.22f, 1f);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x + 0.5f - half) / half;
            float dy = (y + 0.5f - half) / half;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01((1f - r) * 18f);
            float light = Mathf.Clamp01(1f - r * 0.75f + Mathf.Max(0f, -dx - dy) * 0.12f);
            Color c = Color.Lerp(outer, inner, light);
            if (r > 0.72f && r < 0.86f) c = new Color(1f, 0.7f, 0.08f, 1f);
            c.a = a;
            px[y * s + x] = c;
        }

        tex.SetPixels(px);
        tex.Apply();
        _goldCoinSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _goldCoinSprite;
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
        int minutes = seconds / 60;
        int secs = seconds % 60;
        return $"{minutes:00}:{secs:00}";
    }
}
