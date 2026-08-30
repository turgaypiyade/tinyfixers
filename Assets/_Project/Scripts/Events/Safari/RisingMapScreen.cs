using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Yükseliş (Rising) tam-ekran overlay'i — Safari'nin dikey asansör yeniden tasarımı.
///
/// Kule: 7 cam kat (fon), her katın merkezi <see cref="floorAnchors"/> ile işaretli (index 0 = 1.kat, alttan).
/// Kalabalık iki yeri kullanır: yükselmek için scissor <see cref="lift"/> platformuna biner, sonra sol
/// kat kabinine geçip orada dinlenir. Kaldıraç bulunulan kat hizasında park eder.
///
/// Dönüş koreografisi:
///  - İlerleme (oldP→newP): elenenler kabinden düşer → kalanlar kaldıraca biner → kaldıraç bir kat
///    yükselir → kalanlar yeni kabine atlar → kaldıraç orada park eder.
///  - 1.kat özel: kalabalık zaten kaldıraçta (1.kat hizası); yükselme yok, yalnız eleme + kabine geçiş.
///  - Düşme: kabinde toplan → oyuncu + elenenler düşer → yarış 1.kata (kaldıraç) döner.
///  - Tamamlandı: kutlama + paylaşılan ödül.
///
/// Kalabalık boyutu (survivors) burada hesaplanır ve <see cref="RisingTopHud"/>'a beslenir (tek kaynak).
/// </summary>
public sealed class RisingMapScreen : SafariMapScreenBase
{
    [Header("Kök")]
    [SerializeField] private GameObject root;
    [SerializeField] private RisingTopHud topHud;

    [Header("Kule katları (alttan üste: index0 = 1.kat)")]
    [SerializeField] private RectTransform[] floorAnchors;
    [Tooltip("Kalabalığın kat kabinine geçince yatay ince ayarı.")]
    [SerializeField] private float cabinStandOffsetX = 25f;
    [Tooltip("Kalabalığın kat kabininde durduğu dikey ofset (avatar zeminde dursun diye).")]
    [SerializeField] private float cabinStandOffsetY = 0f;

    [Header("Kat numaraları (kule kenarı)")]
    [Tooltip("1..N kat numarası etiketleri (index0 = 1.kat). Geçilen katlar sarı, kalanlar beyaz.")]
    [SerializeField] private TMP_Text[] floorNumberLabels;
    [SerializeField] private Color floorReachedColor   = new Color(1f, 0.85f, 0.2f, 1f);  // sarı (geçilen kat)
    [SerializeField] private Color floorRemainingColor = Color.white;                     // beyaz (kalan kat)

    [Header("Scissor kaldıraç")]
    [SerializeField] private ScissorLiftView lift;
    [Tooltip("Rest (ilk duruş) için statik lift görseli (RisingLiftT2). Rise sırasında gizlenir; prosedürel scissor açılır.")]
    [SerializeField] private GameObject restLift;
    [Tooltip("Kalabalığın 1.kat hizasında (başlangıç) kaldıraç platformunda durduğu nokta.")]
    [SerializeField] private RectTransform liftAnchor;
    [SerializeField, Min(16f)] private float liftMaxHeightUI = 900f;
    [SerializeField, Min(16f)] private float liftTileSize = 120f;

    [Header("Kalabalık")]
    [SerializeField] private SafariAvatarStackView crowdStack;
    [SerializeField, Min(16)] private float crowdAvatarSize = 112f;
    [SerializeField, Min(0)]  private float crowdSpread = 58f;
    [SerializeField, Min(1)]  private int   maxVisibleCrowdAvatars = 20;
    [SerializeField, Min(1)]  private int   minCrowd = 2;
    [SerializeField, Range(0.5f, 0.98f)] private float botRoundWinChance = 0.88f;

    [Header("Kontroller")]
    [SerializeField] private GameObject continueRoot;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Color promptTextColor = new Color(1f, 0.43f, 0.24f, 1f);

    [Header("Animasyon")]
    [SerializeField, Min(0.1f)] private float gatherPause = 0.5f;
    [SerializeField, Min(0.1f)] private float boardDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float riseDuration = 1.4f;
    [SerializeField, Min(0.1f)] private float hopDuration = 1.0f;
    [SerializeField, Min(0.1f)] private float fallDuration = 2.2f;
    [SerializeField, Min(1)] private int visualEliminationCount = 2;
    [SerializeField, Min(1)] private int jumpBatchSize = 2;
    [SerializeField, Min(0.1f)] private float initialRevealDuration = 0.42f;
    [SerializeField, Min(0f)]   private float initialRevealStagger = 0.018f;

    [Header("Final Ödül")]
    [SerializeField] private Sprite finalGoldMoneySprite;
    [SerializeField, Min(0.1f)] private float rewardCountDuration = 0.85f;

    private SafariEventController controller;
    private Coroutine active;
    private bool liftBuilt;
    private bool countedThisSession;
    private bool continuePromptVisible;
    private bool continuePromptArmed;
    private bool crowdParkedOnLift;
    private bool hasIntroCrowd;

    private int Pitstops => controller != null && controller.Config != null ? controller.Config.pitstopCount : 7;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (closeButton != null)    closeButton.onClick.AddListener(Hide);
        PrepareContinuePrompt();
        ApplyPromptTextColor();
        if (root != null && root != gameObject) root.SetActive(false);
    }

    public override void Open(SafariEventController owner, SafariRoundOutcome outcome)
    {
        controller = owner;
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);

        ApplyPromptTextColor();
        EnsureLift();

        if (active != null) StopCoroutine(active);
        active = StartCoroutine(Present(outcome));
    }

    public Vector3 PrepareIntroTarget(SafariEventController owner)
    {
        controller = owner;
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);

        ApplyPromptTextColor();
        EnsureLift();
        if (active != null) { StopCoroutine(active); active = null; }
        SetContinueVisible(false);
        RefreshStatus();

        int pit = SafariState.CurrentPitstop;
        int posPit = pit <= 0 ? 1 : pit;
        ParkLiftAtFloor(posPit);
        RefreshHud(pit);
        crowdParkedOnLift = pit <= 0;
        if (crowdStack != null)
            crowdStack.Clear();

        return pit <= 0 ? LiftPos(posPit) : CabinPos(posPit);
    }

    public void AdoptIntroCrowd(IReadOnlyList<RectTransform> avatars)
    {
        if (crowdStack == null)
            return;

        int pit = SafariState.CurrentPitstop;
        int posPit = pit <= 0 ? 1 : pit;
        crowdStack.Container.position = pit <= 0 ? LiftPos(posPit) : CabinPos(posPit);
        crowdStack.AdoptDetached(avatars);
        crowdParkedOnLift = pit <= 0;
        countedThisSession = true;
        hasIntroCrowd = true;
        RefreshHud(pit);
        SetContinueVisible(true);
        RefreshContinueInteractable();
    }

    public override void Hide()
    {
        if (active != null) { StopCoroutine(active); active = null; }
        if (root != null) root.SetActive(false);
    }

    // ── Kurulum ──────────────────────────────────────────────────

    private void EnsureLift()
    {
        if (lift == null || liftBuilt) return;

        Canvas.ForceUpdateCanvases();
        if (FloorCount > 1 && liftAnchor != null && lift.transform.parent != null)
        {
            var parent = lift.transform.parent;
            float startY = parent.InverseTransformPoint(liftAnchor.position).y;
            float endY = parent.InverseTransformPoint(floorAnchors[FloorCount - 1].position).y + cabinStandOffsetY;
            liftMaxHeightUI = Mathf.Max(16f, endY - startY);
        }

        lift.Build(liftMaxHeightUI, liftTileSize);
        liftBuilt = true;
    }

    // Rest = statik RisingLiftT2 görünür, prosedürel scissor gizli. Rise sırasında tersi.
    private void SetLiftMode(bool resting)
    {
        if (restLift != null) restLift.SetActive(resting);
        if (lift != null) lift.gameObject.SetActive(!resting);
    }

    private void ParkLiftAtFloor(int floor)
    {
        int clamped = Mathf.Max(1, floor);
        SetLiftFloor(clamped);
        SetLiftMode(clamped <= 1);
    }

    // ── Konum yardımcıları ───────────────────────────────────────

    private int FloorCount => floorAnchors != null ? floorAnchors.Length : 0;

    // Kat kabini merkezi (floor: 1..N). Kalabalık burada dinlenir.
    private Vector3 CabinPos(int floor)
    {
        if (FloorCount == 0) return liftAnchor != null ? liftAnchor.position : transform.position;
        int idx = Mathf.Clamp(floor - 1, 0, FloorCount - 1);
        var p = floorAnchors[idx].position;
        p.x += cabinStandOffsetX;
        p.y += cabinStandOffsetY;
        return p;
    }

    // Kaldıraç platformunun ilgili kat hizasındaki (kalabalığın bindiği) konumu — X kaldıraçta, Y kat hizası.
    private Vector3 LiftPos(int floor)
    {
        float x = liftAnchor != null ? liftAnchor.position.x : CabinPos(floor).x;
        float y = floor <= 1 && liftAnchor != null ? liftAnchor.position.y : CabinPos(floor).y;
        return new Vector3(x, y, 0f);
    }

    // Kat 1 → 0, kat N → 1 (kaldıraç uzama oranı).
    private float FloorFrac(int floor)
    {
        int n = Pitstops;
        if (n <= 1) return 0f;
        return Mathf.Clamp01((floor - 1) / (float)(n - 1));
    }

    private void SetLiftFloor(int floor)
    {
        EnsureLift();
        if (lift != null) lift.SetExtension01(FloorFrac(floor));
    }

    // ── Kalabalık boyutu (Safari ile aynı deterministik simülasyon) ──

    private int TotalParticipants()
    {
        var cfg = controller != null ? controller.Config : null;
        return Mathf.Max(1, cfg != null ? cfg.participantVisualCount : 100);
    }

    private int VisibleCrowdAt(int floor) =>
        Mathf.Clamp(CrowdSizeAt(floor), 1, Mathf.Min(maxVisibleCrowdAvatars, TotalParticipants()));

    private int CrowdSizeAt(int floor)
    {
        int total = TotalParticipants();
        if (floor <= 0) return total;

        int survivors = 1; // oyuncu
        int rounds = Mathf.Clamp(floor, 0, Pitstops);
        float winChance = Mathf.Clamp(botRoundWinChance, 0.5f, 0.98f);

        for (int bot = 1; bot < total; bot++)
        {
            bool alive = true;
            for (int round = 1; round <= rounds; round++)
            {
                if (Hash01(bot, round) > winChance) { alive = false; break; }
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

    private float CrowdAvatarPixels() => Mathf.Max(crowdAvatarSize, 112f);
    private float CrowdSpreadPixels()
    {
        float size = CrowdAvatarPixels();
        return crowdSpread > 0f ? Mathf.Min(crowdSpread, size * 0.58f) : size * 0.52f;
    }

    // Kalabalığı kurar. sizeFloor = kaç kişi (o kattaki hayatta kalan); posFloor = nerede duracak.
    // Eleme animasyonu ÖNCESİ eski sayı gösterilsin diye ikisi ayrı (boyut eski kat, konum eski kat).
    private void BuildCrowd(int sizeFloor, int posFloor, bool onLift)
    {
        if (crowdStack == null) return;
        Vector3 pos = onLift ? LiftPos(posFloor) : CabinPos(posFloor);
        crowdStack.Container.position = pos;
        crowdParkedOnLift = onLift;

        int total = TotalParticipants();
        int n = VisibleCrowdAt(sizeFloor);
        var list = SafariParticipantPool.Build(total, CurrentLevel.Global, seed: 1);
        crowdStack.Build(list, n, CrowdAvatarPixels(), CrowdSpreadPixels());
        RefreshHud(sizeFloor);
    }

    private void RefreshHud(int floor)
    {
        RefreshFloorNumbers(floor);
        if (topHud == null) return;
        topHud.SetLevel(floor, Pitstops);
        topHud.SetPlayers(CrowdSizeAt(floor));
    }

    // Kule kenarındaki kat numaralarını ilerlemeye göre renklendir:
    // geçilen katlar (numara <= reachedFloor) sarı, kalanlar beyaz. Metin de burada garanti (1..N).
    private void RefreshFloorNumbers(int reachedFloor)
    {
        if (floorNumberLabels == null) return;
        for (int i = 0; i < floorNumberLabels.Length; i++)
        {
            var lbl = floorNumberLabels[i];
            if (lbl == null) continue;
            int number = i + 1;
            lbl.text  = number.ToString();
            lbl.color = number <= reachedFloor ? floorReachedColor : floorRemainingColor;
        }
    }

    private int VisibleEliminationCount(int fromFloor, int toFloor, bool includePlayer)
    {
        int actualLost = Mathf.Max(0, CrowdSizeAt(fromFloor) - CrowdSizeAt(toFloor));
        if (includePlayer)
            actualLost = Mathf.Max(actualLost, 1);
        if (actualLost <= 0 || crowdStack == null)
            return 0;

        int availableBots = Mathf.Max(0, crowdStack.BotCount);
        if (availableBots <= 0)
            return 0;

        return Mathf.Clamp(Mathf.Min(actualLost, visualEliminationCount), 1, availableBots);
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
                bool startOnLift = oldP <= 0;
                int posOld = oldP <= 0 ? 1 : oldP;

                BuildCrowd(oldP, posOld, startOnLift);   // eski boyut + eski konumda topla
                ParkLiftAtFloor(posOld);
                yield return new WaitForSecondsRealtime(gatherPause);

                int elim = VisibleEliminationCount(oldP, newP, includePlayer: false);
                yield return EliminateFall(elim, includePlayer: false);
                RefreshHud(newP);

                if (!startOnLift)
                {
                    yield return JumpCrowdToLift(oldP);                      // kabinden scissor'a atla
                    crowdParkedOnLift = true;
                }

                yield return RiseLift(posOld, newP);                        // bir kat yüksel
                yield return JumpCrowdToCabin(posOld, newP);                 // kazananlar tekli/ikili kabine atlar
                crowdParkedOnLift = false;
                ParkLiftAtFloor(newP);                                      // 2+ katlarda açık scissor park eder
                BuildCrowd(newP, newP, onLift: false);                      // yerleşince 8'li temsil tekrar dolsun
                break;
            }

            case SafariRoundOutcome.Fell:
            {
                int oldP = controller != null && controller.FallFromPitstop >= 0
                    ? controller.FallFromPitstop : 1;
                oldP = Mathf.Max(1, oldP);
                BuildCrowd(oldP, oldP, onLift: false);
                ParkLiftAtFloor(oldP);
                yield return new WaitForSecondsRealtime(gatherPause);

                int elim = VisibleEliminationCount(oldP, oldP + 1, includePlayer: true);
                yield return EliminateFall(elim, includePlayer: true);
                yield return new WaitForSecondsRealtime(0.3f);

                BuildCrowd(0, 1, onLift: true);     // yarış 1.kata (kaldıraç) döner — taze tam kalabalık
                ParkLiftAtFloor(1);
                break;
            }

            case SafariRoundOutcome.Completed:
            {
                int pit = SafariState.CurrentPitstop;
                BuildCrowd(pit, pit, onLift: false);
                ParkLiftAtFloor(pit);
                if (continueRoot != null) continueRoot.SetActive(false);
                yield return CelebrateFinalCrowd();
                yield return ShowFinalRewardOverlay();
                yield break;
            }

            default: // None — taze açılış
            {
                if (hasIntroCrowd)
                {
                    hasIntroCrowd = false;
                    break;
                }

                int pit = SafariState.CurrentPitstop;
                bool onLift = pit <= 0;
                int posPit = pit <= 0 ? 1 : pit;
                if (!countedThisSession)
                {
                    yield return RunInitialReveal(pit, posPit, onLift);
                    countedThisSession = true;
                }
                else
                {
                    BuildCrowd(pit, posPit, onLift);
                }
                ParkLiftAtFloor(posPit);
                break;
            }
        }

        SetContinueVisible(true);
        RefreshContinueInteractable();
    }

    // ── Koreografi ───────────────────────────────────────────────

    // Elenenleri kalabalıktan çıkar ve aşağı dök.
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
        var rotations = new Quaternion[fallers.Count];
        var scales = new Vector3[fallers.Count];
        float drop = Mathf.Max(420f, CrowdAvatarPixels() * 5.4f);
        for (int i = 0; i < fallers.Count; i++)
        {
            if (fallers[i] == null) continue;
            starts[i] = fallers[i].anchoredPosition;
            float side = ((i % 3) - 1) * CrowdAvatarPixels() * 0.35f;
            targets[i] = starts[i] + new Vector2(side, -drop);
            rotations[i] = fallers[i].localRotation;
            scales[i] = fallers[i].localScale;
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
                float e = k * k * (3f - 2f * k);
                Vector2 p = Vector2.LerpUnclamped(starts[i], targets[i], e);
                p.x += Mathf.Sin(k * Mathf.PI * 2f) * CrowdAvatarPixels() * 0.08f;
                fallers[i].anchoredPosition = p;
                fallers[i].localRotation = rotations[i] * Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, ((i % 2) == 0 ? -14f : 14f), k));
                fallers[i].localScale = scales[i] * (1f + Mathf.Sin(k * Mathf.PI) * 0.08f);
                SetAlpha(fallers[i], 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, k)));
            }
            yield return null;
        }
        for (int i = 0; i < fallers.Count; i++)
            if (fallers[i] != null) Destroy(fallers[i].gameObject);
    }

    // Tüm kalabalığı (container) hedefe taşır (üzerindeki avatarlar birlikte gelir).
    private IEnumerator MoveCrowd(Vector3 to, float duration, float hop = 0f)
    {
        if (crowdStack == null) yield break;
        var c = crowdStack.Container;
        Vector3 from = c.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = Mathf.SmoothStep(0f, 1f, k);
            Vector3 p = Vector3.Lerp(from, to, e);
            if (hop > 0f) p.y += Mathf.Sin(e * Mathf.PI) * hop;
            c.position = p;
            yield return null;
        }
        c.position = to;
    }

    private IEnumerator JumpCrowdToLift(int floor)
    {
        if (crowdStack == null) yield break;

        Transform host = root != null ? root.transform : transform;
        Vector3 sourceCenter = crowdStack.Container.position;
        Vector3 targetCenter = LiftPos(floor);
        var movers = crowdStack.DetachAll(host);
        if (movers.Count == 0) yield break;

        var starts = new Vector3[movers.Count];
        var targets = new Vector3[movers.Count];
        var rotations = new Quaternion[movers.Count];
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            starts[i] = movers[i].position;
            targets[i] = targetCenter + (starts[i] - sourceCenter) * 0.9f;
            rotations[i] = movers[i].localRotation;
        }

        float itemDuration = Mathf.Max(0.2f, boardDuration);
        float stagger = Mathf.Min(0.16f, itemDuration * 0.2f);
        int batchSize = Mathf.Max(1, jumpBatchSize);
        int lastBatch = Mathf.Max(0, (movers.Count - 1) / batchSize);
        float totalDuration = itemDuration + lastBatch * stagger;
        float hop = Mathf.Max(CrowdAvatarPixels() * 0.35f, Vector3.Distance(sourceCenter, targetCenter) * 0.14f);

        float t = 0f;
        while (t < totalDuration)
        {
            t += Time.unscaledDeltaTime;
            float elapsed = Mathf.Min(t, totalDuration);
            for (int i = 0; i < movers.Count; i++)
            {
                if (movers[i] == null) continue;
                int batch = i / batchSize;
                float k = Mathf.Clamp01((elapsed - batch * stagger) / itemDuration);
                if (k <= 0f) continue;
                float e = Mathf.SmoothStep(0f, 1f, k);
                Vector3 p = Vector3.Lerp(starts[i], targets[i], e);
                p.y += Mathf.Sin(e * Mathf.PI) * hop;
                movers[i].position = p;
                movers[i].localRotation = rotations[i] * Quaternion.Euler(0f, 0f, Mathf.Sin(e * Mathf.PI) * ((i % 2 == 0) ? 5f : -5f));
            }
            yield return null;
        }

        crowdStack.Container.position = targetCenter;
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            movers[i].position = targets[i];
            movers[i].localRotation = rotations[i];
        }
        crowdStack.AdoptDetached(movers);
        crowdParkedOnLift = true;
    }

    private IEnumerator JumpCrowdToCabin(int fromFloor, int toFloor)
    {
        if (crowdStack == null) yield break;

        Transform host = root != null ? root.transform : transform;
        Vector3 sourceCenter = crowdStack.Container.position;
        Vector3 targetCenter = CabinPos(toFloor);
        var movers = crowdStack.DetachAll(host);
        if (movers.Count == 0) yield break;

        var starts = new Vector3[movers.Count];
        var targets = new Vector3[movers.Count];
        var rotations = new Quaternion[movers.Count];
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            starts[i] = movers[i].position;
            targets[i] = targetCenter + (starts[i] - sourceCenter) * 0.82f;
            rotations[i] = movers[i].localRotation;
        }

        float itemDuration = Mathf.Max(0.2f, hopDuration);
        float stagger = Mathf.Min(0.18f, itemDuration * 0.22f);
        int batchSize = Mathf.Max(1, jumpBatchSize);
        int lastBatch = Mathf.Max(0, (movers.Count - 1) / batchSize);
        float totalDuration = itemDuration + lastBatch * stagger;
        float hop = Mathf.Max(CrowdAvatarPixels() * 0.45f, Vector3.Distance(sourceCenter, targetCenter) * 0.18f);

        float t = 0f;
        while (t < totalDuration)
        {
            t += Time.unscaledDeltaTime;
            float elapsed = Mathf.Min(t, totalDuration);
            for (int i = 0; i < movers.Count; i++)
            {
                if (movers[i] == null) continue;
                int batch = i / batchSize;
                float k = Mathf.Clamp01((elapsed - batch * stagger) / itemDuration);
                if (k <= 0f) continue;
                float e = Mathf.SmoothStep(0f, 1f, k);
                Vector3 p = Vector3.Lerp(starts[i], targets[i], e);
                p.y += Mathf.Sin(e * Mathf.PI) * hop;
                movers[i].position = p;
                movers[i].localRotation = rotations[i] * Quaternion.Euler(0f, 0f, Mathf.Sin(e * Mathf.PI) * ((i % 2 == 0) ? -6f : 6f));
            }
            yield return null;
        }

        crowdStack.Container.position = targetCenter;
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            movers[i].position = targets[i];
            movers[i].localRotation = rotations[i];
        }
        crowdStack.AdoptDetached(movers);
        crowdParkedOnLift = false;
    }

    // Kaldıraç bir kat yükselir; kalabalık platformla birlikte çıkar.
    private IEnumerator RiseLift(int fromFloor, int toFloor)
    {
        if (toFloor <= fromFloor) { SetLiftFloor(toFloor); yield break; }

        SetLiftMode(resting: false);   // rise başladı: prosedürel scissor açılır (RisingLiftT2 gizlenir)
        crowdParkedOnLift = true;

        var c = crowdStack != null ? crowdStack.Container : null;
        Vector3 crowdFrom = c != null ? c.position : Vector3.zero;
        Vector3 crowdTo = LiftPos(toFloor);
        float fFrom = FloorFrac(fromFloor);
        float fTo = FloorFrac(toFloor);

        float t = 0f;
        while (t < riseDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / riseDuration);
            float e = Mathf.SmoothStep(0f, 1f, k);
            if (lift != null) lift.SetExtension01(Mathf.Lerp(fFrom, fTo, e));
            if (c != null) c.position = Vector3.Lerp(crowdFrom, crowdTo, e);
            yield return null;
        }
        if (lift != null) lift.SetExtension01(fTo);
        if (c != null) c.position = crowdTo;
    }

    private IEnumerator RunInitialReveal(int sizeFloor, int posFloor, bool onLift)
    {
        if (crowdStack == null) yield break;
        Vector3 pos = onLift ? LiftPos(posFloor) : CabinPos(posFloor);
        crowdStack.Container.position = pos;
        crowdParkedOnLift = onLift;

        int total = TotalParticipants();
        int n = VisibleCrowdAt(sizeFloor);
        var list = SafariParticipantPool.Build(total, CurrentLevel.Global, seed: 1);
        crowdStack.Build(list, n, CrowdAvatarPixels(), CrowdSpreadPixels());
        RefreshHud(sizeFloor);

        var avatars = crowdStack.SnapshotAvatars();
        var targetScales = new Vector3[avatars.Count];
        for (int i = 0; i < avatars.Count; i++)
        {
            if (avatars[i] == null) continue;
            targetScales[i] = avatars[i].localScale;
            avatars[i].localScale = Vector3.zero;
            SetAlpha(avatars[i], 0f);
        }

        if (statusText != null)
        {
            statusText.color = promptTextColor;
            statusText.text = "Kullanıcılar seçiliyor...";
        }

        float itemDuration = Mathf.Max(0.1f, initialRevealDuration);
        float totalDuration = Mathf.Max(itemDuration, itemDuration + Mathf.Max(0, avatars.Count - 1) * initialRevealStagger);
        float t = 0f;
        while (t < totalDuration)
        {
            t += Time.unscaledDeltaTime;
            float elapsed = Mathf.Min(t, totalDuration);
            for (int i = 0; i < avatars.Count; i++)
            {
                if (avatars[i] == null) continue;
                float start = i * initialRevealStagger;
                float k = Mathf.Clamp01((elapsed - start) / itemDuration);
                float e = Mathf.SmoothStep(0f, 1f, k);
                float pop = e + Mathf.Sin(e * Mathf.PI) * 0.08f;
                avatars[i].localScale = targetScales[i] * pop;
                SetAlpha(avatars[i], k);
            }
            yield return null;
        }
        for (int i = 0; i < avatars.Count; i++)
        {
            if (avatars[i] == null) continue;
            avatars[i].localScale = targetScales[i];
            SetAlpha(avatars[i], 1f);
        }
        RefreshStatus();
    }

    private IEnumerator CelebrateFinalCrowd()
    {
        if (crowdStack == null) yield break;
        var avatars = crowdStack.SnapshotAvatars();
        if (avatars.Count == 0) yield break;

        var baseScales = new Vector3[avatars.Count];
        for (int i = 0; i < avatars.Count; i++)
            if (avatars[i] != null) baseScales[i] = avatars[i].localScale;

        float duration = 0.8f;
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

    // ── Final ödül overlay (Safari ile aynı yapı) ────────────────

    private IEnumerator ShowFinalRewardOverlay()
    {
        Transform parent = root != null ? root.transform : transform;
        int winners = Mathf.Max(1, CrowdSizeAt(SafariState.CurrentPitstop));
        var cfg = controller != null ? controller.Config : null;
        int prizePool = cfg != null ? cfg.prizePoolGold : 0;
        int share = Mathf.Max(1, prizePool / winners);

        var overlay = NewStretchRect("RisingRewardOverlay", parent);
        overlay.SetAsLastSibling();
        var dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.88f);
        dim.raycastTarget = true;

        var coin = NewRect("GoldMoney", overlay, new Vector2(220f, 220f), new Vector2(0f, 180f));
        var coinImg = coin.gameObject.AddComponent<Image>();
        coinImg.sprite = finalGoldMoneySprite;
        coinImg.preserveAspect = true;
        coinImg.raycastTarget = false;
        coinImg.enabled = finalGoldMoneySprite != null;

        var shareText = NewText("ShareText", overlay, 44, new Vector2(0f, 24f), new Vector2(820f, 120f));
        shareText.text = $"{prizePool:N0} altını {winners} kişi ile paylaşıyorsun";

        var amountText = NewText("RewardAmount", overlay, 72, new Vector2(0f, -100f), new Vector2(720f, 120f));
        amountText.text = "+0";

        var tapText = NewText("TapText", overlay, 30, new Vector2(0f, -240f), new Vector2(720f, 80f));
        tapText.text = "Cüzdana eklemek için dokun";

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

        yield return null;
        while (!WasContinueTap()) yield return null;

        controller?.ClaimFinalReward(share, winners);
        Destroy(overlay.gameObject);
        Hide();
    }

    // ── Ortak yardımcılar ────────────────────────────────────────

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

    // ── Devam / durum (Safari ile aynı desen) ────────────────────

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

        // Kat bilgisi TopHUD'da (Seviye N/7); alt statü yalnız cooldown gösterir, aksi halde boş.
        statusText.text = !controller.CanContinueNow(out var remaining)
            ? $"Tekrar denemek için: {FormatRemaining(remaining)}"
            : "";
    }

    private void Update()
    {
        if (!IsOpen() || controller == null) return;
        if (continuePromptVisible && crowdParkedOnLift && crowdStack != null)
            crowdStack.Container.position = LiftPos(1);
        if (continueButton != null && !continueButton.interactable)
            RefreshContinueInteractable();
        if (!continuePromptVisible || !continuePromptArmed) return;
        if (!controller.CanContinueNow(out _)) return;
        if (WasContinueTap()) OnContinueClicked();
    }

    private bool IsOpen() => root != null ? root.activeSelf : gameObject.activeInHierarchy;

    private bool WasContinueTap()
    {
        if (Pointer.current != null && Pointer.current.press.wasReleasedThisFrame) return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) return true;
        if (Touchscreen.current != null)
            foreach (var touch in Touchscreen.current.touches)
                if (touch.press.wasReleasedThisFrame) return true;
        return false;
    }

    private void ApplyPromptTextColor()
    {
        if (continueLabel != null) continueLabel.color = promptTextColor;
        if (statusText != null) statusText.color = promptTextColor;
    }

    // ── Runtime UI kurucular (reward overlay) ────────────────────

    private RectTransform NewStretchRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
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

    private static string FormatRemaining(TimeSpan remaining)
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
