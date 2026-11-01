using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Runtime event screen using the supplied artwork and native vector timing UI.
/// Sixteen cells, one ball, one impact glow and eight fragments are built once and reused.
/// One unscaled update drives poses, projectile, indicator and effects; no shot coroutines or scans.
/// </summary>
public sealed class GiftFestivalScreen : MonoBehaviour
{
    private enum Phase { Ready, Shot, Intro }
    private GiftFestivalConfig cfg;
    private RectTransform root, design, grid, selection, indicator, meter, iconOrigin;
    private Image background, bear, rabbit, ball, impact, panorama;
    private readonly GiftFestivalCellView[] cells = new GiftFestivalCellView[16];
    private readonly RectTransform[] fragments = new RectTransform[8];
    private readonly Image[] fragmentImages = new Image[8];
    private TMP_Text title, roundText, timerText, ballsText, hitText, hint, result, skipText;
    private GiftFestivalHitButton hitButton;
    private Button closeButton;
    private CanvasGroup contentAlpha;
    private GameObject introBlocker;
    private EventScreenIris iris;
    private GiftFestivalTutorialOverlay tutorial;
    private bool tutorialActive, tutorialGreen, tutorialChecked;
    private int tutorialTarget = -1;
    private Phase phase;
    private GiftFestivalState.Shot shot;
    private int selected = -1, shownRound = -1, size = 2, bearPose = -1, rabbitPose = -1, spin = -1;
    private int width, height;
    private Rect safeArea;
    private float age, nextSecond, indicatorPhase, position = 0.5f;
    private float currentSweepSeconds = 1.05f, shotPlaybackSpeed = 1.35f;
    private readonly EventSfx.Handle[] sounds = new EventSfx.Handle[5];
    private int shotSoundMask;
    private bool impacted, focused = true, closing;
    private bool canTime;
    private Vector2 impactPosition;
    private static readonly Vector2 BearFeet = new(-370, -533), RabbitFeet = new(250, -510);
    private static readonly Vector2 Release = new(120, -375), Contact = new(-60, -340);

    public void Build(GiftFestivalConfig config)
    {
        cfg = config;
        root = (RectTransform)transform;
        background = GiftFestivalUI.Picture("Stadium", root, cfg.Art.background, Vector2.one, Vector2.zero);
        background.raycastTarget = true; // modal screen blocks the main menu underneath
        if (cfg.Art.introPanorama != null)
        {
            panorama = GiftFestivalUI.Picture("StadiumIntroPanorama", root, cfg.Art.introPanorama, Vector2.one, Vector2.zero);
            panorama.gameObject.SetActive(false);
        }
        design = GiftFestivalUI.Rect("Design", root, new Vector2(1000, 1800), Vector2.zero);
        contentAlpha = design.gameObject.AddComponent<CanvasGroup>();
        GiftFestivalUI.Picture("WoodenBoard", design, cfg.Art.board, new Vector2(660, 825), new Vector2(0, 28));
        grid = GiftFestivalUI.Rect("Gifts", design, Vector2.one * 454, new Vector2(-1, 75));
        for (int i = 0; i < cells.Length; i++) cells[i] = new GiftFestivalCellView(grid, cfg, i, Select);
        selection = GiftFestivalUI.Rect("SelectedTarget", grid, Vector2.one * 100, Vector2.zero);
        for (int i = 0; i < 4; i++)
        {
            var sprite = cfg.Art.targetCorners != null && i < cfg.Art.targetCorners.Length ? cfg.Art.targetCorners[i] : null;
            var corner = GiftFestivalUI.Picture("TargetCorner" + i, selection, sprite, new Vector2(32, 30), Vector2.zero);
            corner.rectTransform.anchorMin = corner.rectTransform.anchorMax = new Vector2(i % 2 == 0 ? 0 : 1, i < 2 ? 1 : 0);
            corner.rectTransform.pivot = corner.rectTransform.anchorMin;
        }
        selection.gameObject.SetActive(false);

        var sign = GiftFestivalUI.Picture("TopSign", design, cfg.Art.topSign, new Vector2(920, 438), new Vector2(0, 670));
        title = GiftFestivalUI.Label("Title", sign.transform, cfg, "", 58, new Vector2(700, 110), new Vector2(0, 8));
        roundText = GiftFestivalUI.Label("Round", sign.transform, cfg, "", 27, new Vector2(276, 70), new Vector2(-303, -141));
        timerText = GiftFestivalUI.Label("Time", sign.transform, cfg, "", 29, new Vector2(276, 70), new Vector2(0, -141));
        GiftFestivalUI.Picture("BallIcon", sign.transform, cfg.Art.ball, Vector2.one * 49, new Vector2(238, -140));
        ballsText = GiftFestivalUI.Label("Balls", sign.transform, cfg, "", 38, new Vector2(140, 70), new Vector2(326, -141));

        bear = GiftFestivalUI.Picture("Bear", design, null, new Vector2(450, 441), BearFeet);
        bear.rectTransform.pivot = new Vector2(0.17f, 0.08f);
        rabbit = GiftFestivalUI.Picture("Rabbit", design, null, new Vector2(300, 371), RabbitFeet);
        rabbit.rectTransform.pivot = new Vector2(0.12f, 0.04f);

        meter = GiftFestivalUI.Rect("TimingBar", design, new Vector2(800, 128), new Vector2(0, -629));
        meter.gameObject.AddComponent<GiftFestivalTimingGraphic>().Configure(GiftFestivalTimingGraphic.Part.Frame, cfg);
        var zones = GiftFestivalUI.Rect("Zones", meter, new Vector2(660, 52), new Vector2(0, 2));
        zones.gameObject.AddComponent<GiftFestivalTimingGraphic>().Configure(GiftFestivalTimingGraphic.Part.Zones, cfg);
        indicator = GiftFestivalUI.Rect("Indicator", meter, new Vector2(20, 89), new Vector2(0, -3.5f));
        indicator.gameObject.AddComponent<GiftFestivalTimingGraphic>().Configure(GiftFestivalTimingGraphic.Part.Indicator, cfg);
        BuildHitButton();
        hint = GiftFestivalUI.Label("Hint", design, cfg, "", 32, new Vector2(900, 50), new Vector2(0, -553), new Color(0.35f, 1f, 0.15f));
        result = GiftFestivalUI.Label("Result", design, cfg, "", 42, new Vector2(850, 110), new Vector2(0, -450));

        var close = GiftFestivalUI.Picture("Close", design, cfg.Art.closeIcon, Vector2.one * 86, new Vector2(440, 805));
        closeButton = GiftFestivalUI.Button(close, Close);
        if (cfg.Art.closeIcon == null) GiftFestivalUI.Label("X", close.transform, cfg, "×", 58, Vector2.one * 80, Vector2.zero);
        var help = GiftFestivalUI.Rect("Help", design, Vector2.one * 75, new Vector2(-440, 805)).gameObject.AddComponent<Image>();
        help.color = new Color(0.03f, 0.12f, 0.24f, 0.75f);
        GiftFestivalUI.Button(help, Help);
        GiftFestivalUI.Label("Question", help.transform, cfg, "?", 49, Vector2.one * 70, Vector2.zero);

        impact = GiftFestivalUI.Picture("ImpactGlow", design, BridgeRepairUI.Glow(), Vector2.one * 190, Vector2.zero);
        impact.color = new Color(1f, 0.77f, 0.12f, 0f);
        for (int i = 0; i < fragments.Length; i++)
        {
            var image = GiftFestivalUI.Rect("GlassFragment" + i, design, new Vector2(12, 22), Vector2.zero).gameObject.AddComponent<Image>();
            image.raycastTarget = false; image.color = new Color(0.64f, 0.92f, 1f, 0f);
            fragmentImages[i] = image; fragments[i] = image.rectTransform;
        }
        ball = GiftFestivalUI.Picture("FlyingBall", design, cfg.Art.ball, Vector2.one * 48, Vector2.zero);
        ball.gameObject.SetActive(false);
        tutorial = new GiftFestivalTutorialOverlay(design, cfg);

        var blocker = GiftFestivalUI.Stretch("SkipIntro", root).gameObject.AddComponent<Image>();
        blocker.color = Color.clear;
        GiftFestivalUI.Button(blocker, FinishIntro);
        introBlocker = blocker.gameObject;
        skipText = GiftFestivalUI.Label("Skip", blocker.transform, cfg, "", 28, new Vector2(800, 70), Vector2.zero);
        skipText.rectTransform.anchorMin = skipText.rectTransform.anchorMax = new Vector2(0.5f, 0);
        skipText.rectTransform.anchoredPosition = new Vector2(0, 85);
        introBlocker.SetActive(false);
        iris = EventScreenIris.For(root);
        Layout();
    }

    private void BuildHitButton()
    {
        Vector2 frameSize = GiftFestivalUI.FitSize(cfg.Art.hitFrame, new Vector2(300, 200));
        var holder = GiftFestivalUI.Rect("HitButton", design, frameSize, new Vector2(0, -797));
        GiftFestivalUI.Picture("RedFrame", holder, cfg.Art.hitFrame, frameSize, Vector2.zero);
        Vector2 faceSize = GiftFestivalUI.FitSize(cfg.Art.hitNormal, frameSize * 0.82f);
        var face = GiftFestivalUI.Picture("Surface", holder, cfg.Art.hitNormal, faceSize, new Vector2(0, 6));
        face.raycastTarget = true;
        hitText = GiftFestivalUI.Label("Action", face.transform, cfg, "", 56, new Vector2(faceSize.x * 0.9f, 90), new Vector2(0, 5), GiftFestivalUI.Ink);
        hitButton = face.gameObject.AddComponent<GiftFestivalHitButton>();
        hitButton.Configure(face, hitText, cfg);
        hitButton.Strike = Strike;
        face.gameObject.AddComponent<UiClickSound>().mode = UiClickSound.Mode.None;
    }

    public void Open(RectTransform origin)
    {
        if (cfg == null) return;
        GiftFestivalState.Sync(cfg, DateTime.UtcNow);
        if (!GiftFestivalState.IsLive(cfg, DateTime.UtcNow) || !GiftFestivalState.HasJoined) return;
        iconOrigin = origin; closing = false; tutorialChecked = false; phase = Phase.Ready;
        gameObject.SetActive(true); transform.SetAsLastSibling();
        selected = -1; shownRound = -1; indicatorPhase = UnityEngine.Random.Range(0f, 2f);
        position = Mathf.PingPong(indicatorPhase, 1f);
        indicator.anchoredPosition = new Vector2((position - 0.5f) * 660, -3.5f);
        ResetActors(); ResetEffects(); Refresh(); Layout();
        if (panorama != null) BeginIntro(); else iris.PlayOpen(origin);
    }

    public void Close()
    {
        if (closing || iris.IsRunning) return;
        StopSounds();
        EndTutorial();
        closing = true; hitButton.interactable = false;
        hint.enabled = false;
        iris.PlayClose(iconOrigin, () => gameObject.SetActive(false));
    }

    private void OnEnable()
    {
        GiftFestivalState.OnChanged += Changed;
        GameLocalization.OnLanguageChanged += RefreshLanguage;
        focused = true; nextSecond = 0;
    }
    private void OnDisable()
    {
        StopSounds();
        EndTutorial();
        GiftFestivalState.OnChanged -= Changed;
        GameLocalization.OnLanguageChanged -= RefreshLanguage;
        if (cfg == null) return;
        phase = Phase.Ready; closing = false;
        if (introBlocker != null) introBlocker.SetActive(false);
        if (panorama != null) panorama.gameObject.SetActive(false);
        if (contentAlpha != null) contentAlpha.alpha = 1;
        ResetEffects();
    }

    private void OnApplicationPause(bool paused) => SetFocus(!paused);
    private void OnApplicationFocus(bool value) => SetFocus(value);
    private void SetFocus(bool value)
    {
        focused = value;
        if (!value) StopSounds();
        if (value && cfg != null) { GiftFestivalState.Sync(cfg, DateTime.UtcNow); nextSecond = 0; }
    }

    private void Update()
    {
        if (cfg == null || !focused) return;
        if (width != Screen.width || height != Screen.height || safeArea != Screen.safeArea) Layout();
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (Time.unscaledTime >= nextSecond)
        {
            nextSecond = Time.unscaledTime + 1f;
            GiftFestivalState.Sync(cfg, DateTime.UtcNow);
            RefreshTimer();
            if (phase == Phase.Ready) RefreshControls();
        }
        if (closing || iris.IsRunning) return;
        if (phase == Phase.Intro) { TickIntro(); return; }
        if (phase == Phase.Shot) { TickShot(); return; }
        if (tutorialActive && (GiftFestivalState.TutorialCompleted || !canTime || !GiftFestivalState.HasJoined))
        {
            EndTutorial(); RefreshControls();
        }
        if (!tutorialChecked)
        {
            tutorialChecked = true;
            if (!GiftFestivalState.TutorialCompleted) BeginTutorial();
        }
        if (tutorialActive) tutorial.Tick(Time.unscaledTime);
        if (hint.enabled && canTime && selected < 0)
            hint.alpha = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * (Mathf.PI * 2f / 1.2f)));
        if (canTime)
        {
            if (tutorialActive)
            {
                // Only the lifetime-first demonstration waits on green. Real shots keep sweeping.
                if (selected >= 0 && !tutorialGreen)
                {
                    indicatorPhase = Mathf.Min(0.5f, indicatorPhase + Time.unscaledDeltaTime / currentSweepSeconds);
                    if (indicatorPhase >= 0.5f) { tutorialGreen = true; RefreshControls(); }
                }
                position = indicatorPhase;
            }
            else
            {
                indicatorPhase = (indicatorPhase + Time.unscaledDeltaTime / currentSweepSeconds) % 2f;
                position = Mathf.PingPong(indicatorPhase, 1f);
            }
            indicator.anchoredPosition = new Vector2((position - 0.5f) * 660, -3.5f);
            if (selected >= 0) selection.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5) * 0.018f);
        }
    }

    private void Changed() { if (phase == Phase.Ready && !closing) Refresh(); }
    private void Refresh()
    {
        if (cfg == null || title == null) return;
        RefreshLanguage(); RefreshBoard(); RefreshTimer(); RefreshControls();
    }

    private void RefreshLanguage()
    {
        if (title == null) return;
        title.text = GiftFestivalUI.L("title");
        hitText.text = GiftFestivalUI.L("hit");
        skipText.text = GiftFestivalUI.L("skip_intro");
        if (phase == Phase.Ready) RefreshControls();
        if (tutorialActive) tutorial.Show(selected < 0 ? cells[tutorialTarget].Root : (RectTransform)hitButton.transform, selected >= 0);
    }

    private void RefreshBoard()
    {
        int roundIndex = GiftFestivalState.Round;
        bool done = GiftFestivalState.IsFinished(cfg);
        var round = cfg.GetRound(Mathf.Min(roundIndex, cfg.RoundCount - 1));
        if (round == null) return;
        size = round.Size;
        if (shownRound != roundIndex)
        {
            selected = -1; shownRound = roundIndex; result.text = "";
            currentSweepSeconds = cfg.SweepSeconds(roundIndex);
        }
        float step = 454f / size;
        int mask = done ? (1 << (size * size)) - 1 : GiftFestivalState.OpenMask;
        int opened = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            bool active = i < size * size;
            cells[i].Root.gameObject.SetActive(active);
            if (!active) continue;
            bool isOpen = GiftFestivalRules.IsOpen(mask, i);
            if (isOpen) opened++;
            cells[i].Place(new Vector2((i % size + 0.5f) * step - 227, 227 - (i / size + 0.5f) * step), step * 0.91f);
            cells[i].SetOpen(isOpen, GiftFestivalState.PrizeAt(Mathf.Min(roundIndex, cfg.RoundCount - 1), i));
            if (isOpen && selected == i) selected = -1;
        }
        roundText.text = string.Format(GiftFestivalUI.L("round"), Mathf.Min(roundIndex + 1, cfg.RoundCount), cfg.RoundCount) + "\n" + opened + "/" + size * size;
        ballsText.text = "×" + GiftFestivalState.Balls;
        selection.gameObject.SetActive(selected >= 0 && !done);
    }

    private void RefreshTimer()
    {
        var now = DateTime.UtcNow;
        var window = GiftFestivalState.Window(cfg, now);
        bool live = GiftFestivalState.IsLive(cfg, now);
        timerText.text = live ? TimeFormat.Countdown(window.End - now) : GiftFestivalUI.L("ended");
    }

    private void RefreshControls()
    {
        var now = DateTime.UtcNow;
        bool live = GiftFestivalState.IsLive(cfg, now);
        // Resting between laps behaves like a finished board for input; the hint carries the next-lap countdown.
        bool resting = GiftFestivalState.IsResting(cfg, now);
        bool done = GiftFestivalState.IsFinished(cfg) || resting;
        canTime = live && !done && GiftFestivalState.Balls > 0;
        hitButton.interactable = phase == Phase.Ready && live && !done && selected >= 0 && GiftFestivalState.Balls > 0 && !closing
            && (!tutorialActive || tutorialGreen);
        hint.text = !live ? GiftFestivalUI.L("ended")
            : resting ? string.Format(GiftFestivalUI.L("next_lap"), TimeFormat.Countdown(GiftFestivalState.RestEnds - now))
            : done ? GiftFestivalUI.L("finished")
            : GiftFestivalState.Balls <= 0 ? GiftFestivalUI.L("earn_balls") : selected < 0 ? GiftFestivalUI.L("select") : GiftFestivalUI.L("time_hit");
        bool showHint = phase == Phase.Ready && !closing && !tutorialActive && selected < 0;
        if (!hint.enabled || !canTime) hint.alpha = 1f;
        hint.enabled = showHint;
        for (int i = 0; i < size * size; i++)
            cells[i].Button.interactable = phase == Phase.Ready && live && !done && !GiftFestivalRules.IsOpen(GiftFestivalState.OpenMask, i)
                && (!tutorialActive || (selected < 0 && i == tutorialTarget));
    }

    private void Select(int cell)
    {
        if (phase != Phase.Ready || closing || iris.IsRunning || GiftFestivalRules.IsOpen(GiftFestivalState.OpenMask, cell)) return;
        if (tutorialActive && (selected >= 0 || cell != tutorialTarget)) return;
        selected = cell;
        selection.gameObject.SetActive(true);
        selection.anchoredPosition = cells[cell].Root.anchoredPosition;
        selection.sizeDelta = Vector2.one * (454f / size * 0.97f);
        selection.localScale = Vector3.one;
        result.text = "";
        if (tutorialActive) tutorial.Show((RectTransform)hitButton.transform, true);
        RefreshControls();
    }

    private void Strike()
    {
        if (phase != Phase.Ready || closing || iris.IsRunning || selected < 0) return;
        if (tutorialActive && (!tutorialGreen || selected != tutorialTarget)) return;
        // Capture the visible position on POINTER DOWN, with no release/animation-frame adjustment.
        phase = Phase.Shot;
        hint.enabled = false;
        if (!GiftFestivalState.TryHit(cfg, selected, position, DateTime.UtcNow, out shot))
        {
            EndTutorial(); phase = Phase.Ready; Refresh(); return;
        }
        EndTutorial();
        GameEventSfx.PlayUi(GameEventSfx.UiSound.Confirm);
        // The model may already have advanced after the last box. Animate at the board that was hit.
        shotPlaybackSpeed = cfg.ShotSpeed(shot.Round);
        StopSounds(); shotSoundMask = 0;
        age = 0; impacted = false; result.text = "";
        hitButton.ShowStrike(true);
        hitButton.interactable = false;
        for (int i = 0; i < cells.Length; i++) cells[i].Button.interactable = false;
        selection.localScale = Vector3.one;
        ballsText.text = "×" + GiftFestivalState.Balls;
        impactPosition = shot.Target >= 0 ? (Vector2)design.InverseTransformPoint(cells[shot.Target].Root.position) : new Vector2(470, -530);
    }

    private void TickShot()
    {
        age += Time.unscaledDeltaTime;
        float motionAge = age * shotPlaybackSpeed;
        float impactTime = 1.42f / shotPlaybackSpeed;
        float soundPitch = Mathf.Clamp(shotPlaybackSpeed, 1f, 1.5f);
        // Consume each milestone even while muted; unmuting must never replay an old throw or hit.
        if (motionAge >= 0.50f && (shotSoundMask & 1) == 0)
        {
            shotSoundMask |= 1;
            if (motionAge < 0.90f) sounds[1] = EventSfx.PlayTracked(l => l.giftFestivalThrow, pitch: soundPitch);
        }
        if (motionAge >= 0.90f && (shotSoundMask & 2) == 0)
        {
            shotSoundMask |= 2;
            StopSound(1);
            if (motionAge < 1.42f)
            {
                if (shot.Hit != GiftFestivalRules.Hit.Miss)
                    sounds[2] = EventSfx.PlayTracked(l => l.giftFestivalBatHit, pitch: soundPitch);
                sounds[3] = EventSfx.PlayTracked(l => l.giftFestivalBallFlight, pitch: soundPitch);
            }
        }
        if (age >= 0.16f && hitButton.IsStriking) hitButton.ShowStrike(false);
        SetRabbit(motionAge < 0.12f ? 0 : motionAge < 0.26f ? 1 : motionAge < 0.38f ? 2 : motionAge < 0.50f ? 3 : 4);
        SetBear(motionAge < 0.64f ? 0 : motionAge < 0.74f ? 1 : motionAge < 0.84f ? 2 : motionAge < 0.96f ? 3 : motionAge < 1.09f ? 4 : 5);
        bool flying = motionAge >= 0.50f && motionAge < 1.42f;
        ball.gameObject.SetActive(flying);
        if (flying)
        {
            bool outbound = motionAge >= 0.90f;
            float t = outbound ? (motionAge - 0.90f) / 0.52f : (motionAge - 0.50f) / 0.40f;
            Vector2 from = outbound ? Contact : Release, to = outbound ? impactPosition : Contact;
            ball.rectTransform.anchoredPosition = Vector2.Lerp(from, to, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * (outbound ? 65 : 26));
            ball.rectTransform.sizeDelta = Vector2.one * (outbound ? Mathf.Lerp(48, 29, t) : 48);
            int frame = cfg.Art.ballSpin != null && cfg.Art.ballSpin.Length > 0 ? (int)((motionAge - 0.5f) * 20) % cfg.Art.ballSpin.Length : -1;
            if (frame != spin) { spin = frame; ball.sprite = frame >= 0 ? cfg.Art.ballSpin[frame] : cfg.Art.ball; }
        }
        if (motionAge >= 1.42f && !impacted)
        {
            impacted = true;
            StopSound(1); StopSound(3);
            if (shot.Opened)
            {
                cells[shot.Target].SetOpen(true, shot.BoxReward);
                impact.rectTransform.anchoredPosition = impactPosition;
                sounds[4] = EventSfx.PlayTracked(l => l.giftFestivalGlassBreak);
            }
            string key = shot.Hit == GiftFestivalRules.Hit.Miss ? "miss" : !shot.Opened ? "already_open"
                : shot.Empty ? "empty_box" : shot.Hit == GiftFestivalRules.Hit.Perfect ? "perfect" : "deflected";
            result.text = GiftFestivalUI.L(key) + (shot.Opened && !shot.Empty
                ? "\n" + shot.BoxReward.AmountLabel + " " + shot.BoxReward.DisplayName : "");
        }
        if (impacted && shot.Opened) TickImpact(Mathf.Clamp01((age - impactTime) / 0.70f));
        if (age < impactTime + 0.74f) return;
        phase = Phase.Ready; selected = -1; ResetActors(); ResetEffects();
        Refresh();
        if (shot.Completed) ShowCompletion();
    }

    private void TickImpact(float t)
    {
        impact.color = new Color(1, 0.77f, 0.12f, (1 - t) * 0.9f);
        impact.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.4f, t);
        for (int i = 0; i < fragments.Length; i++)
        {
            float angle = (i * 45 + 20) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            fragments[i].anchoredPosition = impactPosition + direction * (20 + 120 * t) + Vector2.down * (65 * t * t);
            fragments[i].localRotation = Quaternion.Euler(0, 0, i * 31 + t * 150);
            fragmentImages[i].color = new Color(0.64f, 0.92f, 1, 1 - t);
        }
    }

    private void ResetEffects()
    {
        if (ball != null) ball.gameObject.SetActive(false);
        if (impact != null) impact.color = Color.clear;
        for (int i = 0; i < fragmentImages.Length; i++) if (fragmentImages[i] != null) fragmentImages[i].color = Color.clear;
    }

    private void StopSound(int index)
    {
        EventSfx.Stop(sounds[index]);
        sounds[index] = default;
    }

    private void StopSounds()
    {
        for (int i = 0; i < sounds.Length; i++) StopSound(i);
    }

    private void ResetActors() { if (hitButton != null) hitButton.ShowStrike(false); bearPose = rabbitPose = -1; spin = -1; SetBear(0); SetRabbit(0); }
    private void SetBear(int frame)
    {
        if (frame == bearPose || cfg.Art.bearFrames == null || cfg.Art.bearFrames.Length == 0) return;
        bearPose = frame; bear.sprite = cfg.Art.bearFrames[Mathf.Min(frame, cfg.Art.bearFrames.Length - 1)]; bear.enabled = bear.sprite != null;
    }
    private void SetRabbit(int frame)
    {
        if (frame == rabbitPose || cfg.Art.rabbitFrames == null || cfg.Art.rabbitFrames.Length == 0) return;
        rabbitPose = frame; rabbit.sprite = cfg.Art.rabbitFrames[Mathf.Min(frame, cfg.Art.rabbitFrames.Length - 1)]; rabbit.enabled = rabbit.sprite != null;
        bool release = frame >= 4;
        rabbit.rectTransform.pivot = release ? new Vector2(0.34f, 0.04f) : new Vector2(0.12f, 0.04f);
        rabbit.rectTransform.sizeDelta = release ? new Vector2(408, 371) : new Vector2(300, 371);
        rabbit.rectTransform.anchoredPosition = RabbitFeet;
    }

    private void Help() => RuntimeChoicePopup.Show(GiftFestivalUI.L("title"), GiftFestivalUI.L("howto"),
        new RuntimeChoicePopup.Choice(GameLocalization.GetOr("common_ok", "OK"), null, true));

    private void ShowCompletion()
    {
        var round = cfg.GetRound(shot.Round);
        int count = round?.completionRewards != null ? round.completionRewards.Length : 0;
        var items = new RuntimeChoicePopup.RewardItem[count + (round != null && round.completionCoins > 0 ? 1 : 0)];
        int offset = 0;
        if (round != null && round.completionCoins > 0)
        {
            var icon = TileIconLibrary.Shared != null ? TileIconLibrary.Shared.GetRewardIcon(DailySlotRewardType.Coins) : null;
            int bonus = GiftFestivalRewards.Scale(new DailySlotReward { type = DailySlotRewardType.Coins, amount = round.completionCoins },
                cfg.LapRewardScale(shot.Lap)).amount;
            items[offset++] = new RuntimeChoicePopup.RewardItem(icon, "+" + bonus, GameLocalization.GetOr("shop_reward_coins", "Coins"));
        }
        for (int i = 0; i < count; i++)
        {
            // Show what was actually paid: completion rewards are scaled by the lap the board belonged to.
            var reward = GiftFestivalRewards.Scale(round.completionRewards[i], cfg.LapRewardScale(shot.Lap));
            if (reward != null) items[offset + i] = new RuntimeChoicePopup.RewardItem(reward.ResolveIcon(), reward.AmountLabel, reward.DisplayName);
        }
        var now = DateTime.UtcNow;
        RuntimeChoicePopup.ShowRewards(GiftFestivalUI.L("round_complete"),
            GiftFestivalState.IsResting(cfg, now) ? string.Format(GiftFestivalUI.L("lap_done"), TimeFormat.Countdown(GiftFestivalState.RestEnds - now))
            : GiftFestivalState.IsFinished(cfg) ? GiftFestivalUI.L("finished") : GiftFestivalUI.L("next_board"),
            items, GameLocalization.GetOr("common_ok", "OK"));
    }

    private void Layout()
    {
        width = Screen.width; height = Screen.height; safeArea = Screen.safeArea;
        Canvas.ForceUpdateCanvases();
        Rect r = root.rect;
        if (r.width <= 0 || r.height <= 0) return;
        float top = height > 0 ? (height - safeArea.yMax) * r.height / height : 0;
        float bottom = height > 0 ? safeArea.yMin * r.height / height : 0;
        float scale = Mathf.Min(r.width / 1000, Mathf.Max(1, r.height - top - bottom) / 1800);
        design.localScale = Vector3.one * scale;
        design.anchoredPosition = new Vector2(0, (bottom - top) * 0.5f);
        if (background.sprite != null)
        {
            float cover = Mathf.Max(r.width / background.sprite.rect.width, r.height / background.sprite.rect.height);
            background.rectTransform.sizeDelta = background.sprite.rect.size * cover;
        }
        if (panorama != null)
        {
            float cover = Mathf.Max(r.width / panorama.sprite.rect.width, r.height / panorama.sprite.rect.height);
            panorama.rectTransform.sizeDelta = panorama.sprite.rect.size * cover;
        }
        if (tutorialActive) tutorial.Show(selected < 0 ? cells[tutorialTarget].Root : (RectTransform)hitButton.transform, selected >= 0);
    }

    private void BeginTutorial()
    {
        if (!GiftFestivalState.TryBeginTutorial(cfg, DateTime.UtcNow)) return;
        tutorialTarget = -1;
        for (int i = 0; i < size * size; i++)
        {
            if (GiftFestivalRules.IsOpen(GiftFestivalState.OpenMask, i)) continue;
            if (tutorialTarget < 0) tutorialTarget = i;
            // The introduction demonstrates collecting a gift; the saved deck is never rerolled.
            if (!GiftFestivalRewards.IsEmpty(GiftFestivalState.PrizeAt(GiftFestivalState.Round, i))) { tutorialTarget = i; break; }
        }
        if (tutorialTarget < 0) return;
        tutorialActive = true; tutorialGreen = false; selected = -1;
        indicatorPhase = position = 0;
        indicator.anchoredPosition = new Vector2(-330, -3.5f);
        selection.gameObject.SetActive(false);
        tutorial.Show(cells[tutorialTarget].Root, false);
        RefreshControls();
    }

    private void EndTutorial()
    {
        tutorialActive = tutorialGreen = false; tutorialTarget = -1;
        tutorial?.Hide();
    }

    private void BeginIntro()
    {
        phase = Phase.Intro; age = 0; panorama.gameObject.SetActive(true);
        StopSounds();
        sounds[0] = EventSfx.PlayTracked(l => l.giftFestivalIntro);
        hint.enabled = false;
        contentAlpha.alpha = 0; introBlocker.SetActive(true); closeButton.interactable = false;
        ApplyIntro(0);
    }
    private void TickIntro()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / Mathf.Max(1, cfg.introSeconds));
        ApplyIntro(t);
        if (t >= 1) CompleteIntro();
    }
    private void ApplyIntro(float t)
    {
        float eased = t * t * (3 - 2 * t);
        Rect r = root.rect;
        float cover = Mathf.Max(r.width / panorama.sprite.rect.width, r.height / panorama.sprite.rect.height);
        float zoom = Mathf.Lerp(1.55f, 2.2f, eased);
        Vector2 extent = panorama.sprite.rect.size * (cover * zoom);
        panorama.rectTransform.sizeDelta = extent;
        float travel = Mathf.Max(0, extent.y - r.height) * 0.5f;
        float focus = Mathf.Lerp(0.30f, 0.83f, eased);
        panorama.rectTransform.anchoredPosition = new Vector2(0, Mathf.Clamp((focus - 0.5f) * extent.y, -travel, travel));
        // Last part blends to the established gameplay background; controls stay blocked until settled.
        float reveal = Mathf.Clamp01((t - 0.72f) / 0.28f);
        panorama.color = new Color(1, 1, 1, 1 - reveal);
        background.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, reveal);
        contentAlpha.alpha = reveal;
    }
    private void FinishIntro()
    {
        StopSound(0);
        CompleteIntro();
    }
    private void CompleteIntro()
    {
        // Let the natural audio tail finish after the camera settles. Its tracked
        // handle still cancels on skip, close, focus loss or the next shot.
        if (panorama != null) panorama.gameObject.SetActive(false);
        background.rectTransform.localScale = Vector3.one;
        introBlocker.SetActive(false); contentAlpha.alpha = 1; closeButton.interactable = true;
        phase = Phase.Ready; RefreshControls();
    }
}
