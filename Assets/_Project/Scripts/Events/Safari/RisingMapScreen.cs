using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Yükseliş (Rising) tam-ekran overlay'i — Safari'nin dikey asansör yeniden tasarımı.
///
/// Kule: arka plan görselindeki 7 platform, her katın merkezi <see cref="floorAnchors"/> ile işaretli (index 0 = 1.kat, alttan).
/// Kalabalık iki yeri kullanır: yükselmek için scissor <see cref="lift"/> platformuna biner, sonra sol
/// kat kabinine geçip orada dinlenir. Kaldıraç bulunulan kat hizasında park eder.
///
/// Dönüş koreografisi:
///  - İlerleme (oldP→newP): elenenler kabinden düşer → kalanlar kaldıraca biner → kaldıraç bir kat
///    yükselir → kalanlar yeni kabine atlar → kaldıraç orada park eder.
///  - İlk kazanım: dinlenme konumundaki (kat 0) kaldıraçtan 1. kata yükselme.
///  - Düşme: kabinde toplan → oyuncu + elenenler düşer → yarış dinlenme konumuna döner.
///  - Tamamlandı: son kata yükselme + atlayış, ardından kutlama + paylaşılan ödül.
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
    [Tooltip("Kat 0 (zemin) platformunun yüzeyi. Kalabalık burada başlar ve düşünce buraya döner. " +
             "Boşsa eski davranışa düşer (lift tablası).")]
    [SerializeField] private RectTransform groundAnchor;
    [Tooltip("Kalabalığın kat kabinine geçince yatay ince ayarı.")]
    [SerializeField] private float cabinStandOffsetX = 25f;
    [Tooltip("YAPISAL ofset: kat yüzeyi ile LİFT TABLASININ o kattaki durak yüksekliği. " +
             "Lift bu değere göre durur — avatarı kaydırmak için bunu DEĞİL cabinAvatarOffsetY'yi kullan.")]
    [SerializeField] private float cabinStandOffsetY = 0f;
    [Tooltip("SADECE avatar ofseti: kalabalık kat platformunda ne kadar aşağıda dursun. " +
             "Lift'in durak yüksekliğini etkilemez.")]
    [SerializeField] private float cabinAvatarOffsetY = -70f;

    [Header("Kat numaraları (kule kenarı)")]
    [Tooltip("1..N kat numarası etiketleri (index0 = 1.kat). Geçilen katlar sarı, kalanlar beyaz.")]
    [SerializeField] private TMP_Text[] floorNumberLabels;
    [SerializeField] private Color floorReachedColor   = new Color(1f, 0.85f, 0.2f, 1f);  // sarı (geçilen kat)
    [SerializeField] private Color floorRemainingColor = Color.white;                     // beyaz (kalan kat)

    [Header("Scissor kaldıraç")]
    [SerializeField] private ScissorLiftView lift;
    [Tooltip("Rest (ilk duruş) için statik lift görseli (RisingLiftT2). Rise sırasında gizlenir; prosedürel scissor açılır.")]
    [SerializeField] private GameObject restLift;
    [Tooltip("Dinlenme konumundaki lift tablasının yüzeyi (kat 0). RestLift görseline bağlı.")]
    [SerializeField] private RectTransform liftAnchor;
    [Tooltip("Avatarları lift tablasının içine doğru indirir; kat platformlarını etkilemez (UI birimi).")]
    [SerializeField] private float liftAvatarOffsetY = -22f;
    [Tooltip("Prosedürel asansörde BASILAN yüzeyin, platform sprite'ının ÜST KENARINA göre farkı " +
             "(UI birimi, negatif = aşağı). 0 = üst kenar (eski davranış). Avatarlar tablada havada " +
             "duruyorsa negatif yönde artır: asansörün duruş yüksekliği ve avatarlar BİRLİKTE kayar, " +
             "aradaki ilişki ve kat-0 geçişi bozulmaz.")]
    [SerializeField] private float liftDeckOffsetY = -45f;
    [SerializeField, Min(16f)] private float liftMaxHeightUI = 900f;
    [SerializeField, Min(16f)] private float liftTileSize = 120f;

    [Header("Kalabalık")]
    [SerializeField] private SafariAvatarStackView crowdStack;
    [SerializeField, Min(16)] private float crowdAvatarSize = 112f;
    [SerializeField, Min(0)]  private float crowdSpread = 58f;
    [SerializeField, Min(1)]  private int   maxVisibleCrowdAvatars = 20;
    [SerializeField, Min(1)]  private int   minCrowd = 2;
    [SerializeField, Range(0.5f, 0.98f)] private float botRoundWinChance = 0.75f;

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

    [Header("Hareket Sesleri")]
    [SerializeField] private AudioMixerGroup motionSfxGroup;
    [SerializeField] private AudioClip jumpSfx;
    [SerializeField] private AudioClip landSfx;
    [SerializeField, Range(0f, 1f)] private float liftSfxVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float jumpSfxVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float landSfxVolume = 0.3f;

    [Header("Final Ödül")]
    [SerializeField] private Sprite finalGoldMoneySprite;
    [SerializeField] private Sprite rewardRibbonSprite;
    [SerializeField] private Sprite rewardButtonSprite;
    [SerializeField] private AudioClip rewardCollectSfx;
    private SafariRewardView rewardView;
    [SerializeField, Min(0.1f)] private float rewardCountDuration = 0.85f;

    private SafariEventController controller;
    private Coroutine active;
    private bool liftBuilt;
    private float collapsedPlatformY;
    private AudioSource liftAudio, jumpAudio, landAudio;
    private readonly List<RectTransform> detachedAvatars = new();
    private bool countedThisSession;
    private bool continuePromptVisible;
    private bool continuePromptArmed;
    private bool showRequested;   // Open/PrepareIntroTarget ile mi uyandık, yoksa editörde aktif mi bırakıldık?
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
        // root == bu obje: editörde aktif bırakılmışsa ana menüyü kapatmasın (Show/Open yolu showRequested ile muaf).
        else if (!showRequested) gameObject.SetActive(false);
    }

    public override void Open(SafariEventController owner, SafariRoundOutcome outcome)
    {
        controller = owner;
        showRequested = true;
        if (topHud != null) topHud.Bind(owner);
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);

        ApplyPromptTextColor();
        EnsureLift();

        StopPresentation();
        active = StartCoroutine(Present(outcome));
    }

    public Vector3 PrepareIntroTarget(SafariEventController owner)
    {
        controller = owner;
        showRequested = true;
        if (topHud != null) topHud.Bind(owner);
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);

        ApplyPromptTextColor();
        EnsureLift();
        StopPresentation();
        SetContinueVisible(false);
        RefreshStatus();

        int pit = SafariState.CurrentPitstop;
        int posPit = Mathf.Max(0, pit);
        ParkLiftAtFloor(posPit);
        RefreshHud(pit);
        // Kalabalık HER zaman kat platformunda durur (kat 0 = zemin platformu); lift yalnız
        // yükselme koreografisi sırasında kullanılır.
        crowdParkedOnLift = false;
        if (crowdStack != null)
            crowdStack.Clear();

        return CabinPos(posPit);
    }

    public void AdoptIntroCrowd(IReadOnlyList<RectTransform> avatars)
    {
        if (crowdStack == null)
            return;

        int pit = SafariState.CurrentPitstop;
        int posPit = Mathf.Max(0, pit);
        crowdStack.Container.position = CabinPos(posPit);
        crowdStack.AdoptDetached(avatars);
        crowdParkedOnLift = false;
        countedThisSession = true;
        hasIntroCrowd = true;
        RefreshHud(pit);
        SetContinueVisible(true);
        RefreshContinueInteractable();
    }

    public override void Hide()
    {
        StopPresentation();
        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopPresentation();
        if (crowdStack != null) crowdStack.Clear();
        hasIntroCrowd = false;
    }

    private void StopPresentation()
    {
        if (rewardView != null)
        {
            Destroy(rewardView.gameObject);
            rewardView = null;
        }
        if (active != null) { StopCoroutine(active); active = null; }
        foreach (var avatar in detachedAvatars)
            if (avatar != null) Destroy(avatar.gameObject);
        detachedAvatars.Clear();
        if (liftAudio != null) liftAudio.Stop();
        if (jumpAudio != null) jumpAudio.Stop();
        if (landAudio != null) landAudio.Stop();
    }

    // ── Kurulum ──────────────────────────────────────────────────

    private void EnsureLift()
    {
        if (lift == null || liftBuilt) return;

        Canvas.ForceUpdateCanvases();
        // Measure travel in the lift's own space; the canvas may be scaled or cropped.
        Vector3 highest = FloorCount > 0 && floorAnchors[FloorCount - 1] != null
            ? floorAnchors[FloorCount - 1].position : transform.position;
        liftMaxHeightUI = Mathf.Max(16f, lift.transform.InverseTransformPoint(highest).y);
        lift.Build(liftMaxHeightUI, liftTileSize);
        collapsedPlatformY = lift.transform.InverseTransformPoint(lift.PlatformTopWorldPosition).y;
        liftBuilt = true;
    }

    private void SetLiftMode(bool resting)
    {
        if (restLift != null) restLift.SetActive(resting);
        if (lift != null) lift.gameObject.SetActive(!resting);
    }

    private void ParkLiftAtFloor(int floor)
    {
        SetLiftFloor(floor);
        SetLiftMode(floor <= 0);
    }

    // ── Konum yardımcıları ───────────────────────────────────────

    private int FloorCount => floorAnchors != null ? floorAnchors.Length : 0;
    private Transform MotionSpace => root != null ? root.transform : transform;

    // The front player's helmet bottom rests on the surface, not its centre.
    private Vector3 CrowdStandOffset => MotionSpace.TransformVector(Vector3.up * CrowdAvatarPixels() * 0.74f);

    private Vector3 FloorSurface(int floor)
    {
        if (floor <= 0 || FloorCount == 0)
        {
            if (groundAnchor != null) return groundAnchor.position;
            return liftAnchor != null ? liftAnchor.position : transform.position;
        }
        var anchor = floorAnchors[Mathf.Clamp(floor - 1, 0, FloorCount - 1)];
        return anchor != null ? anchor.position : transform.position;
    }

    private Vector3 CabinPos(int floor)
    {
        return FloorSurface(floor) + CrowdStandOffset
            + MotionSpace.TransformVector(new Vector3(cabinStandOffsetX, cabinStandOffsetY + cabinAvatarOffsetY, 0f));
    }

    private Vector3 LiftSurface(int floor)
    {
        Vector3 rest = liftAnchor != null ? liftAnchor.position : transform.position;
        if (floor <= 0) return rest;
        // Align with the actual platform height, including alternating left/right floors.
        Vector3 p = MotionSpace.InverseTransformPoint(FloorSurface(floor));
        p.x = MotionSpace.InverseTransformPoint(rest).x;
        p.y += cabinStandOffsetY;
        return MotionSpace.TransformPoint(p);
    }

    private Vector3 LiftCrowdOffset => CrowdStandOffset + MotionSpace.TransformVector(Vector3.up * liftAvatarOffsetY);
    private Vector3 LiftPos(int floor) => LiftSurface(floor) + LiftCrowdOffset;

    // Prosedürel tablanın sprite ÜST KENARI (PlatformTopWorldPosition) ile BASILAN yüzey aynı
    // değildir (RLU1 perspektifli bir plaka: üst yüz sprite'ın üst kısmında). Fark SANAT sabiti →
    // liftAnchor'dan TÜRETİLEMEZ (o zaten rest lift'in referansı; türetmek offseti iptal edip
    // asansörü aşağı çeker). Tek knob: liftDeckOffsetY. Hem duruş yüksekliği hem kalabalık aynı
    // yüzeyi kullandığı için kat-0 geçişi her değerde dikişsizdir.
    private Vector3 LiftDeckWorld()
    {
        EnsureLift();
        if (lift == null) return liftAnchor != null ? liftAnchor.position : transform.position;
        return lift.PlatformTopWorldPosition + lift.transform.TransformVector(Vector3.up * liftDeckOffsetY);
    }

    private float LiftHeight(int floor)
    {
        if (lift == null) return 0f;
        // Hedef: BASILAN yüzey LiftSurface(floor) hizasına gelsin (sprite üst kenarı değil).
        return Mathf.Max(0f, lift.transform.InverseTransformPoint(LiftSurface(floor)).y
                             - collapsedPlatformY - liftDeckOffsetY);
    }

    private void SetLiftFloor(int floor)
    {
        EnsureLift();
        if (lift != null) lift.SetPlatformHeight(LiftHeight(floor));
    }

    // ── Kalabalık boyutu (Safari ile aynı deterministik simülasyon) ──

    private int TotalParticipants()
    {
        var cfg = controller != null ? controller.Config : null;
        return Mathf.Max(1, cfg != null ? cfg.participantVisualCount : 100);
    }

    private int VisibleCrowdAt(int floor) =>
        Mathf.Clamp(CrowdSizeAt(floor), 1, Mathf.Min(maxVisibleCrowdAvatars, TotalParticipants()));

    private int CrowdSizeAt(int floor) =>
        SafariCrowdSimulation.SurvivorsAt(TotalParticipants(), Mathf.Clamp(floor, 0, Pitstops),
            botRoundWinChance, minCrowd);

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

    // Düşüş (kaybetme) sunumundan sonra dokunuş yeni level BAŞLATMAZ: kapatma butonu gibi haritayı
    // kapatıp ana menüye döner. Oyuncu tekrar denemeyi kendisi seçer.
    private bool presentedFall;

    private IEnumerator Present(SafariRoundOutcome outcome)
    {
        presentedFall = outcome == SafariRoundOutcome.Fell;
        SetContinueVisible(false);
        RefreshStatus();

        switch (outcome)
        {
            case SafariRoundOutcome.Advanced:
            {
                yield return AdvanceToFloor(SafariState.CurrentPitstop);
                break;
            }

            case SafariRoundOutcome.Fell:
            {
                int oldP = controller != null && controller.FallFromPitstop >= 0
                    ? controller.FallFromPitstop : 1;
                oldP = Mathf.Max(0, oldP);
                BuildCrowd(oldP, oldP, onLift: false);
                ParkLiftAtFloor(oldP);
                yield return new WaitForSecondsRealtime(gatherPause);

                int elim = VisibleEliminationCount(oldP, oldP + 1, includePlayer: true);
                yield return EliminateFall(elim, includePlayer: true);
                yield return new WaitForSecondsRealtime(0.3f);

                BuildCrowd(0, 0, onLift: false);   // düşenler zemin platformuna toplanır
                ParkLiftAtFloor(0);
                break;
            }

            case SafariRoundOutcome.Completed:
            {
                int pit = SafariState.CurrentPitstop;
                yield return AdvanceToFloor(pit);
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
                const bool onLift = false;   // açılışta da kat platformunda durulur
                int posPit = Mathf.Max(0, pit);
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

    private IEnumerator AdvanceToFloor(int newFloor)
    {
        int oldFloor = Mathf.Max(0, newFloor - 1);
        BuildCrowd(oldFloor, oldFloor, onLift: false);
        ParkLiftAtFloor(oldFloor);
        yield return new WaitForSecondsRealtime(gatherPause);
        yield return EliminateFall(VisibleEliminationCount(oldFloor, newFloor, false), false);
        // Kat platformundan (0 dahil) lift tablasına geç, sonra yüksel ve üst kata hop.
        yield return JumpCrowd(LiftPos(oldFloor), boardDuration, true);
        yield return RiseLift(oldFloor, newFloor);
        yield return JumpCrowd(CabinPos(newFloor), hopDuration, false);
        ParkLiftAtFloor(newFloor);
        RefreshHud(newFloor);
        // Keep the landed avatars; rebuilding here caused a visible size/position snap.
    }

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
        detachedAvatars.AddRange(fallers);

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
        {
            if (fallers[i] != null) Destroy(fallers[i].gameObject);
            detachedAvatars.Remove(fallers[i]);
        }
    }

    private IEnumerator JumpCrowd(Vector3 targetCenter, float duration, bool ontoLift)
    {
        if (crowdStack == null) yield break;
        Transform host = MotionSpace;
        Vector3 delta = host.InverseTransformPoint(targetCenter)
            - host.InverseTransformPoint(crowdStack.Container.position);
        var movers = crowdStack.DetachAll(host);
        if (movers.Count == 0) yield break;
        detachedAvatars.AddRange(movers);

        var starts = new Vector3[movers.Count];
        var rotations = new Quaternion[movers.Count];
        var scales = new Vector3[movers.Count];
        for (int i = 0; i < movers.Count; i++)
        {
            starts[i] = movers[i].localPosition;
            rotations[i] = movers[i].localRotation;
            scales[i] = movers[i].localScale;
        }
        // Preserve draw order: the player stays in front throughout the jump.
        for (int i = movers.Count - 1; i >= 0; i--) movers[i].SetAsLastSibling();

        float itemDuration = Mathf.Max(0.55f, duration);
        int batchSize = Mathf.Max(1, jumpBatchSize);
        float stagger = Mathf.Max(0.105f, (batchSize - 1) * 0.025f + 0.04f);
        float Delay(int index) => index / batchSize * stagger + index % batchSize * 0.025f;
        float totalDuration = itemDuration + Delay(movers.Count - 1);
        float hop = Mathf.Clamp(Mathf.Abs(delta.x) * 0.20f, CrowdAvatarPixels() * 0.7f, CrowdAvatarPixels() * 1.8f);
        var tookOff = new bool[movers.Count];
        var landed = new bool[movers.Count];
        float t = 0f;
        while (t < totalDuration)
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < movers.Count; i++)
            {
                if (movers[i] == null) continue;
                float k = Mathf.Clamp01((t - Delay(i)) / itemDuration);
                ApplyJumpPose(movers[i], starts[i], starts[i] + delta, rotations[i], scales[i], k,
                    hop * (1f + (i % 3 - 1) * 0.045f));
                if (k >= 0.12f && !tookOff[i])
                {
                    tookOff[i] = true;
                    if (i % batchSize == 0) PlayJumpSound(false, i / batchSize);
                }
                if (k >= 0.82f && !landed[i])
                {
                    landed[i] = true;
                    if (i % batchSize == 0) PlayJumpSound(true, i / batchSize);
                }
            }
            yield return null;
        }

        crowdStack.Container.position = targetCenter;
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            movers[i].localPosition = starts[i] + delta;
            movers[i].localRotation = rotations[i];
            movers[i].localScale = scales[i];
            detachedAvatars.Remove(movers[i]);
        }
        crowdStack.AdoptDetached(movers);
        crowdParkedOnLift = ontoLift;
    }

    // Anticipation -> ballistic arc -> damped landing. All distances use local UI units.
    public static void ApplyJumpPose(RectTransform avatar, Vector3 from, Vector3 to,
        Quaternion rotation, Vector3 scale, float progress, float height)
    {
        float k = Mathf.Clamp01(progress);
        float flight = Mathf.Clamp01((k - 0.12f) / 0.70f);
        Vector3 p = Vector3.LerpUnclamped(from, to, flight);
        float squash;
        if (k < 0.12f)
            squash = -0.12f * Mathf.SmoothStep(0f, 1f, k / 0.12f);
        else if (k < 0.82f)
        {
            p.y += 4f * flight * (1f - flight) * height;
            squash = Mathf.Lerp(-0.12f, 0.10f, Mathf.Clamp01(flight / 0.12f))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.8f, flight)));
        }
        else
        {
            float settle = (k - 0.82f) / 0.18f;
            squash = -0.14f * Mathf.Sin(settle * Mathf.PI * 2f) * (1f - settle);
        }
        avatar.localScale = Vector3.Scale(scale, new Vector3(1f - squash * 0.5f, 1f + squash, 1f));
        // Squash around the feet rather than sinking the avatar through the platform.
        p.y += avatar.rect.height * scale.y * squash * 0.5f;
        avatar.localPosition = p;
        avatar.localRotation = rotation * Quaternion.Euler(0f, 0f,
            -Mathf.Sign(to.x - from.x) * Mathf.Sin(flight * Mathf.PI) * 5f);
    }

    private IEnumerator RiseLift(int fromFloor, int toFloor)
    {
        if (toFloor <= fromFloor) { SetLiftFloor(toFloor); yield break; }
        SetLiftFloor(fromFloor);
        SetLiftMode(false);
        crowdParkedOnLift = true;
        EnsureMotionAudio();
        if (liftAudio.clip != null)
        {
            liftAudio.mute = !GameSettings.SoundEnabled;
            liftAudio.volume = 0f;
            liftAudio.Play();
        }

        float fromHeight = LiftHeight(fromFloor);
        float toHeight = LiftHeight(toFloor);

        // Rest lift ile prosedürel lift ARTIK aynı kalibre yüzeyi kullanıyor (LiftDeckWorld);
        // bu yüzden geçişte fark yok — eski "seam'i yükseliş boyunca erit" yaması kaldırıldı
        // (görünen etkisi: kalabalık yükselirken tablada bir miktar yukarı kayıyordu).
        float duration = Mathf.Max(0.3f, riseDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = k * k * k * (k * (k * 6f - 15f) + 10f);
            if (lift != null) lift.SetPlatformHeight(Mathf.Lerp(fromHeight, toHeight, e));
            if (crowdStack != null)
                crowdStack.Container.position = lift != null
                    ? LiftDeckWorld() + LiftCrowdOffset
                    : Vector3.Lerp(LiftPos(fromFloor), LiftPos(toFloor), e);
            liftAudio.volume = liftSfxVolume * Mathf.Min(Mathf.Clamp01(k / 0.12f), Mathf.Clamp01((1f - k) / 0.15f));
            yield return null;
        }
        liftAudio.Stop();
        SetLiftFloor(toFloor);
        if (crowdStack != null)
            crowdStack.Container.position = lift != null
                ? LiftDeckWorld() + LiftCrowdOffset
                : LiftPos(toFloor);
    }

    private void EnsureMotionAudio()
    {
        if (liftAudio != null) return;
        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = motionSfxGroup;
            return source;
        }
        liftAudio = NewSource("LiftAudio");
        liftAudio.clip = Resources.Load<AudioClip>("Audio/Jokers/Mini2");
        liftAudio.loop = true;
        jumpAudio = NewSource("JumpAudio");
        landAudio = NewSource("LandingAudio");
    }

    public void PlayJumpSound(bool landing, int batch = 0)
    {
        if (!GameSettings.SoundEnabled) return;
        var clip = landing ? landSfx : jumpSfx;
        if (clip == null) return;
        EnsureMotionAudio();
        var source = landing ? landAudio : jumpAudio;
        source.mute = false;
        source.pitch = 1f + (batch % 3 - 1) * 0.035f;
        source.PlayOneShot(clip, landing ? landSfxVolume : jumpSfxVolume);
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

        rewardView = SafariRewardView.Create(parent);
        yield return rewardView.Present(share, prizePool, winners, finalGoldMoneySprite,
            rewardRibbonSprite, rewardButtonSprite, continueLabel != null ? continueLabel.font : null,
            rewardCollectSfx, motionSfxGroup, rewardCountDuration);
        controller?.ClaimFinalReward(share, winners);
        Destroy(rewardView.gameObject);
        rewardView = null;
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
        if (presentedFall)
        {
            presentedFall = false;
            Hide();
            return;
        }
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
        bool canContinue = presentedFall || (controller != null && controller.CanContinueNow(out _));
        if (continueButton != null) continueButton.interactable = canContinue;
        if (continueLabel != null)
        {
            continueLabel.color = promptTextColor;
            continueLabel.text = presentedFall ? "Ana menüye dönmek için dokunun"
                : canContinue ? "Devam etmek için dokunun" : "Tekrar denemek için bekleyin";
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
        if (liftAudio != null) liftAudio.mute = !GameSettings.SoundEnabled;
        if (jumpAudio != null) jumpAudio.mute = !GameSettings.SoundEnabled;
        if (landAudio != null) landAudio.mute = !GameSettings.SoundEnabled;
        if (!IsOpen() || controller == null) return;
        if (continueButton != null && !continueButton.interactable)
            RefreshContinueInteractable();
        if (!continuePromptVisible || !continuePromptArmed) return;
        if (!presentedFall && !controller.CanContinueNow(out _)) return;
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

    private static string FormatRemaining(TimeSpan remaining)
    {
        return TimeFormat.Countdown(remaining);
    }
}
