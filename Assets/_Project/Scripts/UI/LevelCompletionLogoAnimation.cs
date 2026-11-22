using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

/// <summary>
/// Level completion logo animation.
/// Frame belirir, Wonder sol ustten ve Fixers sagdan ucar, hayvan maskot
/// (Pig / Bear / Ram / Rabit arasindan random) pop yapar, havai fisek patlar.
/// Tum parcalar ayni 607x627 tuvalde cizili: ayni merkezde ust uste oturur.
/// Double click during this animation skips only the logo animation.
///
/// Important: this component does not disable its root object at the end.
/// The skipButton can live under the same LogoAnimPanel and still work during BonusMovesService.RunBonusRound().
/// </summary>
public class LevelCompletionLogoAnimation : MonoBehaviour
{
    [Header("Arkaplan / Overlay")]
    [SerializeField] private Image overlayImage;
    [SerializeField] private bool keepOverlayTransparent = true;
    [SerializeField, Range(0f, 1f)] private float overlayTargetAlpha = 0.75f;
    [SerializeField, Min(0.05f)] private float dimFadeDuration = 0.22f;
    [SerializeField] private int canvasSortOrder = 200;

    [Header("Logo Parcalari (atanmayanlar atlanir)")]
    [Tooltip("Altin cerceve + manzara. Yerinde durur, sadece fade-in yapar. En altta.")]
    [SerializeField] private RectTransform frameImage;
    [Tooltip("Hayvan maskot. Yerinde pop yapar; sprite'i animalSprites icinden secilir.")]
    [SerializeField] private RectTransform animalImage;
    [Tooltip("Wonder tabelasi. Sol ust kosden ucar. Hayvanin govdesinin onunde.")]
    [SerializeField] private RectTransform wonderImage;
    [Tooltip("Fixers yazisi. Sagdan ucar. En ustte.")]
    [SerializeField] private RectTransform fixersImage;

    [Tooltip("Her oynatimda birisi random secilir (Pig / Bear / Ram / Rabit).")]
    [SerializeField] private Sprite[] animalSprites = new Sprite[0];

    [Header("Fisek VFX")]
    [SerializeField] private RectTransform vfxRoot;
    [Tooltip("Kac fisek atilir. Fisekler SIRAYLA cikar: biri yukselir-patlar, sonra digeri.")]
    [SerializeField] private int fireworkBurstCount = 4;
    [Tooltip("Iki patlama arasi sure (sn). Patlama sesinin duyulan kismi (~0.8 sn) kadar olmali ki sesler ust uste binmesin.")]
    [SerializeField, Min(0.2f)] private float fireworkInterval = 0.8f;
    [Tooltip("Roketin asagidan patlama noktasina yukselme suresi (sn).")]
    [SerializeField, Min(0.05f)] private float fireworkRiseDuration = 0.4f;
    [Tooltip("Ses klibinde patlama aninin klip basindan gecikmesi (Fireworks.wav ~0.2 sn). Ses bu kadar ERKEN baslar ki patlamayla eslessin.")]
    [SerializeField, Min(0f)] private float fireworkSoundLead = 0.2f;

    [Header("Zamanlama")]
    [SerializeField, Min(0.1f)] private float flyInDuration = 0.55f;
    [SerializeField, Min(0f)] private float holdDuration = 0.65f;
    [SerializeField, Min(0f)] private float fireworkDelay = 0.18f;

    [Header("Giris Mesafeleri (piksel)")]
    [Tooltip("Wonder sol ust kosden gelir (x negatif = sol, y pozitif = yukari).")]
    [SerializeField] private Vector2 wonderEnterOffset = new Vector2(-900f, 900f);
    [Tooltip("Fixers sagdan gelir.")]
    [SerializeField] private Vector2 fixersEnterOffset = new Vector2(1100f, 0f);

    [Header("Cerceve ve Hayvan Zamanlamasi")]
    [Tooltip("Cerceve yerinde belirir; ucmaz.")]
    [SerializeField, Min(0f)] private float frameFadeDuration = 0.18f;
    [SerializeField, Min(0f)] private float wonderEnterDelay = 0.10f;
    [SerializeField, Min(0f)] private float fixersEnterDelay = 0.20f;
    [Tooltip("Yazilar oturduktan sonra hayvan pop yapar.")]
    [SerializeField, Min(0f)] private float animalPopDelay = 0.50f;
    [SerializeField, Min(0.05f)] private float animalPopDuration = 0.34f;
    [SerializeField, Range(0.1f, 1f)] private float animalPopStartScale = 0.6f;

    [Header("Cift Tiklama Skip")]
    [SerializeField, Min(0.05f)] private float doubleTapWindow = 0.40f;

    // -------------------------------------------------------------------------
    public bool WasSkipped { get; private set; }
    public event Action FireworksStarted;
    public event Action FireworksFinished;
    /// <summary>Her tek görsel havai fişek patlamasında tetiklenir (kesintili ses için).</summary>
    /// Patlama sesi icin: her fisekte bir kez, patlamadan fireworkSoundLead once (klibin tepe ani patlamaya denk gelsin).
    public event Action FireworkBurst;

    private bool _playing;
    private bool _skipRequested;
    // Logo yerine oturdu mu: sonrasinda (bekleme + havai fisek gosterisi) TEK dokunus gosteriyi bitirir.
    // Giris sirasinda yanlislikla atlanmasin diye orada cift dokunus gerekir.
    private bool _entranceSettled;
    private float _lastTapTime = -99f;
    private RectTransform[] _flyPieces;
    private Vector2[] _flyHomePositions;
    private Vector3 _animalHomeScale = Vector3.one;
    private bool _homeStateCaptured;
    private Canvas _canvas;

    private const float FireworkBurstDuration = 0.9f;

    private float TargetOverlayAlpha => keepOverlayTransparent ? 0f : Mathf.Clamp01(overlayTargetAlpha);

    private void Awake()
    {
        // Parcalar sahnede gorunur kaydedilir (editorde konumlandirilabilsinler
        // diye). Play() cagrilana kadar hicbiri ekranda olmamali.
        SetLogoPiecesVisible(false);
        SetVfxRootVisible(false);
        // Dokular + havuz sahne yuklenirken (yukleme ekrani arkasinda) hazirlanir: gosteri sirasinda
        // GameObject olusturma / doku uretimi takilmasi olmasin (eski Android). Kapali kok altinda, Image'lar
        // devre disi → oyun boyunca maliyetleri yok.
        EnsureFxSprites();
        PrewarmFxPool(FxPrewarmSlots);
    }

    private void Update()
    {
        if (!_playing)
            return;

        bool tapped = false;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            tapped = true;

        var touch = Touchscreen.current;
        if (!tapped && touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            tapped = true;

        if (!tapped)
            return;

        float now = Time.unscaledTime;
        float gap = now - _lastTapTime;

        if (_entranceSettled || gap <= doubleTapWindow)
            _skipRequested = true;

        _lastTapTime = now;
    }

    // -------------------------------------------------------------------------
    public IEnumerator Play()
    {
        WasSkipped = false;
        _skipRequested = false;
        _entranceSettled = false;
        _playing = false;
        _lastTapTime = -99f;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (_canvas == null)
            if (!TryGetComponent(out _canvas)) _canvas = GetComponentInParent<Canvas>(true);

        if (_canvas != null)
        {
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = canvasSortOrder;
        }

        ClearSpawnedFireworks();
        SetLogoPiecesVisible(true);

        // Parcalar gorunur olur olmaz giris pozuna kurulur: arada hicbir yield
        // olmamali. Once sadece gorunur yapilip overlay fade'i beklenince,
        // tamamlanmis logo bir an oldugu gibi ekranda kaliyordu.
        PickRandomAnimal();
        ApplyLayerOrder();
        CaptureLogoHomeState();
        ArmLogoEntrance();

        SetVfxRootVisible(true);
        PrepareOverlayForAnimation();
        SetOverlayAlpha(0f);

        _playing = true;

        float targetAlpha = TargetOverlayAlpha;
        yield return StartCoroutine(FadeOverlay(0f, targetAlpha, dimFadeDuration));

        if (_skipRequested)
        {
            _playing = false;
            WasSkipped = true;
            SettleLogoToHome();
            yield return StartCoroutine(FadeOverlay(targetAlpha, 0f, dimFadeDuration));
            HideVisualsAfterPlay();
            yield break;
        }

        // Tum parcalar ayni 607x627 tuvalde cizildi: ayni merkezde ust uste
        // gelince kompozisyon oturuyor. Bu yuzden hedef konum her parcanin
        // kendi anchoredPosition'i; sadece giris yollari farkli.
        // Giris pozu Play()'in basinda kuruldu; burada sadece surulur.
        var flyPieces = _flyPieces;
        var flyOffsets = new Vector2[] { wonderEnterOffset, fixersEnterOffset };
        var flyDelays = new float[] { wonderEnterDelay, fixersEnterDelay };
        var flyCenters = _flyHomePositions;
        Vector3 animalHomeScale = _animalHomeScale;

        Coroutine fireworksRoutine = StartCoroutine(PlayFireworks());

        float totalIn = Mathf.Max(
            Mathf.Max(frameFadeDuration, animalPopDelay + animalPopDuration),
            Mathf.Max(wonderEnterDelay, fixersEnterDelay) + flyInDuration);

        float elapsed = 0f;
        while (elapsed < totalIn && !_skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;

            SetPieceAlpha(frameImage,
                frameFadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / frameFadeDuration));

            for (int i = 0; i < flyPieces.Length; i++)
            {
                if (flyPieces[i] == null)
                    continue;

                float t = Mathf.Clamp01((elapsed - flyDelays[i]) / flyInDuration);
                flyPieces[i].anchoredPosition = Vector2.LerpUnclamped(
                    flyCenters[i] + flyOffsets[i], flyCenters[i], EaseOutBack(t));
            }

            if (animalImage != null)
            {
                float at = Mathf.Clamp01((elapsed - animalPopDelay) / animalPopDuration);
                SetPieceAlpha(animalImage, at > 0f ? 1f : 0f);
                animalImage.localScale = Vector3.LerpUnclamped(
                    animalHomeScale * animalPopStartScale, animalHomeScale, EaseOutBack(at));
            }

            yield return null;
        }

        SettleLogoToHome();
        _entranceSettled = true;

        float holdElapsed = 0f;
        while (holdElapsed < holdDuration && !_skipRequested)
        {
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_skipRequested && fireworksRoutine != null)
            yield return fireworksRoutine;

        _playing = false;
        WasSkipped = _skipRequested;

        yield return StartCoroutine(FadeOverlay(targetAlpha, 0f, dimFadeDuration));
        HideVisualsAfterPlay();
    }

    /// <summary>
    /// Ev konumlari yalnizca BIR kez yakalanir: yarida kesilen bir giris
    /// (skip) sonraki oynatimda "ev" sanilip parcalarin kaymasina yol acmasin.
    /// </summary>
    private void CaptureLogoHomeState()
    {
        if (_homeStateCaptured)
            return;

        _flyPieces = new RectTransform[] { wonderImage, fixersImage };
        _flyHomePositions = new Vector2[_flyPieces.Length];

        for (int i = 0; i < _flyPieces.Length; i++)
            if (_flyPieces[i] != null)
                _flyHomePositions[i] = _flyPieces[i].anchoredPosition;

        _animalHomeScale = animalImage != null ? animalImage.localScale : Vector3.one;
        _homeStateCaptured = true;
    }

    /// <summary>Giris pozu: ucan yazilar offsette, cerceve ve hayvan gorunmez.</summary>
    private void ArmLogoEntrance()
    {
        var flyOffsets = new Vector2[] { wonderEnterOffset, fixersEnterOffset };

        for (int i = 0; i < _flyPieces.Length; i++)
            if (_flyPieces[i] != null)
                _flyPieces[i].anchoredPosition = _flyHomePositions[i] + flyOffsets[i];

        if (animalImage != null)
            animalImage.localScale = _animalHomeScale * animalPopStartScale;

        SetPieceAlpha(frameImage, 0f);
        SetPieceAlpha(animalImage, 0f);
    }

    /// <summary>Skip edilse de son kare tam oturmus olmali.</summary>
    private void SettleLogoToHome()
    {
        SetPieceAlpha(frameImage, 1f);

        if (_flyPieces != null)
        {
            for (int i = 0; i < _flyPieces.Length; i++)
                if (_flyPieces[i] != null)
                    _flyPieces[i].anchoredPosition = _flyHomePositions[i];
        }

        if (animalImage != null)
        {
            SetPieceAlpha(animalImage, 1f);
            animalImage.localScale = _animalHomeScale;
        }
    }

    // -------------------------------------------------------------------------
    private void PrepareOverlayForAnimation()
    {
        if (overlayImage == null)
            return;

        overlayImage.enabled = true;
        overlayImage.raycastTarget = false;

        var c = overlayImage.color;
        c.a = 0f;
        overlayImage.color = c;
    }

    private void SetOverlayAlpha(float a)
    {
        if (overlayImage == null)
            return;

        var c = overlayImage.color;
        c.a = keepOverlayTransparent ? 0f : Mathf.Clamp01(a);
        overlayImage.color = c;
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (overlayImage == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetOverlayAlpha(to);
    }

    private void HideVisualsAfterPlay()
    {
        _playing = false;
        _skipRequested = false;

        SetOverlayAlpha(0f);

        if (overlayImage != null)
        {
            overlayImage.enabled = keepOverlayTransparent ? false : overlayImage.enabled;
            overlayImage.raycastTarget = false;
        }

        SetLogoPiecesVisible(false);
        SetVfxRootVisible(false);
        ClearSpawnedFireworks();

        // Reset canvas override so world-space renderers (RocketTrailBeam, sortingOrder=100)
        // are not occluded on iOS Metal during the bonus round.
        if (_canvas != null)
            _canvas.overrideSorting = false;

        // Do not disable gameObject here. The bonus-round skipButton may be a child of this root.
    }

    private void SetLogoPiecesVisible(bool visible)
    {
        SetGraphicVisible(frameImage, visible);
        SetGraphicVisible(animalImage, visible);
        SetGraphicVisible(wonderImage, visible);
        SetGraphicVisible(fixersImage, visible);
    }

    /// <summary>Her oynatimda hayvan sprite'ini havuzdan yeniden secer.</summary>
    private void PickRandomAnimal()
    {
        if (animalImage == null || animalSprites == null || animalSprites.Length == 0)
            return;

        // Inspector'da bos slot birakilmis olabilir; yalnizca dolu olanlardan sec.
        int filled = 0;
        for (int i = 0; i < animalSprites.Length; i++)
            if (animalSprites[i] != null)
                filled++;

        if (filled == 0)
            return;

        if (!animalImage.TryGetComponent<Image>(out var image))
            return;

        int pick = Random.Range(0, filled);
        for (int i = 0; i < animalSprites.Length; i++)
        {
            if (animalSprites[i] == null)
                continue;

            if (pick > 0)
            {
                pick--;
                continue;
            }

            image.sprite = animalSprites[i];
            return;
        }
    }

    /// <summary>Altta -> ustte: Frame, Hayvan, Wonder, Fixers.</summary>
    private void ApplyLayerOrder()
    {
        var ordered = new RectTransform[] { frameImage, animalImage, wonderImage, fixersImage };

        // Yalnizca bu dordunu kendi aralarinda sirala. vfxRoot / overlay gibi
        // kardeslerin sirasini bozmamak icin en dusuk mevcut indexten baslanir.
        int index = int.MaxValue;
        for (int i = 0; i < ordered.Length; i++)
            if (ordered[i] != null)
                index = Mathf.Min(index, ordered[i].GetSiblingIndex());

        if (index == int.MaxValue)
            return;

        for (int i = 0; i < ordered.Length; i++)
        {
            if (ordered[i] == null)
                continue;

            ordered[i].SetSiblingIndex(index);
            index++;
        }
    }

    /// <summary>Cerceve ve hayvan yerinde belirdigi icin konum yerine alpha ile surulur.</summary>
    private static void SetPieceAlpha(RectTransform target, float alpha)
    {
        if (target == null)
            return;

        alpha = Mathf.Clamp01(alpha);

        if (target.TryGetComponent<CanvasGroup>(out var group))
        {
            group.alpha = alpha;
            return;
        }

        if (target.TryGetComponent<Image>(out var image))
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }

    private static void SetGraphicVisible(RectTransform target, bool visible)
    {
        if (target == null)
            return;

        var image = target.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = visible;
            image.raycastTarget = false;
        }

        var canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void SetVfxRootVisible(bool visible)
    {
        if (vfxRoot != null)
            vfxRoot.gameObject.SetActive(visible);
    }

    // -------------------------------------------------------------------------
    // HAVAI FISEK GOSTERISI (kullanici 2026-11-22: "daha gorsel ve saşaali"):
    //   arka planda donen isik huzmeleri → 3 sirali fisek (dolgun / tas fisegi / cift halka) → konfeti yagmuru →
    //   4 fisekli final yaylimi → yavasca suzulen altin kivilcimlar.
    // PERFORMANS: tum parcaciklar HAVUZDAN (Image'lar bir kez olusur, oynatma sirasinda Instantiate/Destroy yok),
    // TEK guncelleme dongusu (LateUpdate), tek doku atlasi (isin/nokta/parilti/konfeti/yildiz ayni doku → az draw call).
    // Gizlenince parcaciklar yalniz Image.enabled=false ile birakilir (SetActive degil → hiyerarsi churn yok).

    private enum FxKind : byte { Line, Flash, Sparkle, Gem, Confetti, Rocket }
    private enum BurstStyle : byte { Normal, DoubleRing, Gem }

    private struct Fx
    {
        public FxKind kind;
        public int slot, headSlot;           // havuz indeksleri (headSlot: isin ucu noktasi / roket izi)
        public Vector2 pos, vel, origin, target;
        public float age, life, size, size2, gravity, drag, rot, rotVel, phase, maxDist, fadeStart;
        public Color color;
        public BurstStyle style;
        public bool sound;
    }

    [Header("Fisek Gosterisi")]
    [Tooltip("Tas fisegi: patlamada sacilan oyun taslari (sari/kirmizi/mavi/yesil).")]
    [SerializeField] private Sprite[] gemSprites = new Sprite[0];
    [SerializeField, Min(0)] private int confettiCount = 60;
    [SerializeField, Range(0f, 1f)] private float raysMaxAlpha = 0.5f;
    [SerializeField] private float raysSpinSpeed = 16f;

    private const int MaxFx = 520, MaxSlots = 640;
    private readonly Fx[] _fx = new Fx[MaxFx];
    private int _fxCount;
    private readonly List<Image> _poolImg = new();
    private readonly List<RectTransform> _poolRt = new();
    private readonly Stack<int> _freeSlots = new();
    private Image _rays;
    private float _raysAlpha, _raysTarget;

    private static Texture2D s_fxAtlas;

    /// Seviye sonu logo halesinin dönen ışın dokusu (prosedürel, bir kez üretilir). Pulse+Pulse şarj halesi de kullanır.
    public static Sprite SharedRaysSprite
    {
        get { EnsureFxSprites(); return s_raysSprite; }
    }
    private static Sprite s_glow, s_dot, s_line, s_square, s_sparkle, s_raysSprite;

    private IEnumerator PlayFireworks()
    {
        if (vfxRoot == null)
            yield break;

        EnsureFxSprites();
        yield return WaitUnscaled(fireworkDelay);
        if (_skipRequested) yield break;

        StartRays();
        FireworksStarted?.Invoke();

        Rect area = vfxRoot.rect;
        float halfW = area.width > 10f ? area.width * 0.5f : 540f;

        // 1) Sirali fisekler: her biri farkli stil ve serit.
        int sequential = Mathf.Max(1, fireworkBurstCount - 1);
        float interval = Mathf.Max(0.35f, fireworkInterval * 0.8f);
        var lanes = new[] { -0.75f, 0.75f, 0f, -0.35f };
        for (int i = 0; i < sequential && !_skipRequested; i++)
        {
            var style = i == 1 && HasGemSprites() ? BurstStyle.Gem : (i % 2 == 0 ? BurstStyle.Normal : BurstStyle.DoubleRing);
            float lane = lanes[i % lanes.Length];
            var pos = new Vector2(Mathf.Clamp(lane * halfW * 0.62f, -halfW + 140f, halfW - 140f) + Random.Range(-30f, 31f),
                                  style == BurstStyle.Gem ? 420f : Random.Range(240f, 500f));
            LaunchRocket(pos, RandomVividColor(), style, sound: true);
            if (i == 0) StartCoroutine(ConfettiRain(halfW, area.height > 10f ? area.height * 0.5f : 960f));
            yield return WaitUnscaled(interval);
        }
        if (_skipRequested) yield break;

        // 2) Final yaylimi: 4 fisek ayni anda (tek ses → sesler ust uste binmesin).
        yield return WaitUnscaled(0.15f);
        for (int k = 0; k < 4 && !_skipRequested; k++)
        {
            float x = Mathf.Lerp(-0.72f, 0.72f, k / 3f) * halfW + Random.Range(-25f, 26f);
            var style = k == 1 || k == 2 ? BurstStyle.DoubleRing : BurstStyle.Normal;
            LaunchRocket(new Vector2(x, Random.Range(300f, 520f)), RandomVividColor(), style, sound: k == 0,
                         riseScale: 0.85f + 0.1f * k);
        }

        // 3) Kivilcimlar suzulurken bekle (oyuncu tek dokunusla atlayabilir).
        yield return WaitUnscaled(fireworkRiseDuration + 1.1f);
        _raysTarget = 0f;   // huzmeler yumusakca soner
        yield return WaitUnscaled(0.4f);
        if (!_skipRequested)
            FireworksFinished?.Invoke();
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        for (float t = 0f; t < seconds && !_skipRequested; t += Time.unscaledDeltaTime)
            yield return null;
    }

    private bool HasGemSprites()
    {
        if (gemSprites == null) return false;
        for (int i = 0; i < gemSprites.Length; i++) if (gemSprites[i] != null) return true;
        return false;
    }

    private static Color RandomVividColor() => Color.HSVToRGB(Random.value, Random.Range(0.55f, 0.8f), 1f);

    // ── Isik huzmeleri (logonun arkasinda yavasca doner) ─────────────────────
    private void StartRays()
    {
        if (frameImage == null) return;
        if (_rays == null)
        {
            var go = new GameObject("FxRays", typeof(RectTransform), typeof(Image)) { layer = frameImage.gameObject.layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(frameImage.parent, false);
            _rays = go.GetComponent<Image>();
            _rays.sprite = s_raysSprite;
            _rays.raycastTarget = false;
        }
        var rr = _rays.rectTransform;
        rr.SetSiblingIndex(frameImage.GetSiblingIndex());   // cercevenin hemen arkasi
        rr.anchorMin = frameImage.anchorMin;
        rr.anchorMax = frameImage.anchorMax;
        rr.anchoredPosition = frameImage.anchoredPosition;
        float s = Mathf.Max(frameImage.rect.width, frameImage.rect.height) * 1.9f;
        rr.sizeDelta = new Vector2(s, s);
        rr.localRotation = Quaternion.identity;
        _raysAlpha = 0f;
        _raysTarget = raysMaxAlpha;
        _rays.color = new Color(1f, 0.86f, 0.45f, 0f);
        _rays.enabled = true;
    }

    // ── Spawn ───────────────────────────────────────────────────────────────
    private void LaunchRocket(Vector2 target, Color color, BurstStyle style, bool sound, float riseScale = 1f)
    {
        var start = new Vector2(target.x + Random.Range(-60f, 61f), target.y - 640f);
        int head = RentSlot(s_dot, 20f, Color.Lerp(color, Color.white, 0.8f), start);
        int trail = RentSlot(s_line, 5f, Color.Lerp(color, Color.white, 0.55f), start);
        if (head < 0 || trail < 0) { ReleaseSlot(head); ReleaseSlot(trail); return; }
        AddFx(new Fx
        {
            kind = FxKind.Rocket, slot = head, headSlot = trail, origin = start, target = target, pos = start,
            life = Mathf.Max(0.05f, fireworkRiseDuration * riseScale), color = color, style = style, sound = sound,
        });
    }

    private void SpawnBurst(Vector2 center, Color color, BurstStyle style)
    {
        AddFlash(center, Color.Lerp(color, Color.white, 0.7f), style == BurstStyle.Gem ? 190f : 150f);

        // Yuk korumasi: ekranda cok parcacik varken (final yaylimi) yeni patlamalar seyrelir.
        float density = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(_fxCount / (MaxFx * 0.6f)));
        int rays = Mathf.Max(12, Mathf.RoundToInt((style == BurstStyle.Gem ? 18 : Random.Range(26, 35)) * density));
        SpawnRing(center, rays, 460f, 680f, 320f, 470f, style == BurstStyle.Gem ? new Color(1f, 0.92f, 0.6f) : color,
                  perRayHue: style == BurstStyle.Normal);
        if (style == BurstStyle.DoubleRing)
            SpawnRing(center, Mathf.RoundToInt(16 * density), 250f, 330f, 170f, 240f, RandomVividColor(), perRayHue: false);
        if (style == BurstStyle.Gem)
            SpawnGems(center);

        // Altin kivilcimlar (sogut fisegi): patlamadan sonra yavasca suzulur, isildar.
        int sparks = Mathf.RoundToInt(Random.Range(10, 15) * density);
        for (int i = 0; i < sparks; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            var c = Color.Lerp(new Color(1f, 0.82f, 0.35f), Color.white, Random.Range(0f, 0.3f));
            float size = Random.Range(14f, 22f);
            int slot = RentSlot(s_sparkle, size, c, center);
            if (slot < 0) break;
            AddFx(new Fx
            {
                kind = FxKind.Sparkle, slot = slot, headSlot = -1, pos = center,
                vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(160f, 320f),
                life = Random.Range(1.5f, 2.1f), size = size, gravity = 140f, drag = 1.7f,
                phase = Random.Range(0f, 6.28f), color = c,
            });
        }
    }

    private void SpawnRing(Vector2 center, int count, float speedMin, float speedMax, float distMin, float distMax,
                           Color color, bool perRayHue)
    {
        float offset = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float ang = (offset + 360f / count * i + Random.Range(-5f, 5f)) * Mathf.Deg2Rad;
            Color c = perRayHue
                ? Color.Lerp(Color.HSVToRGB(Random.value, Random.Range(0.6f, 0.9f), 1f), Color.white, Random.Range(0.08f, 0.22f))
                : Color.Lerp(color, Color.white, Random.Range(0.05f, 0.2f));
            float headSize = Random.Range(11f, 16f);
            int line = RentSlot(s_line, 4f, c, center);
            int head = RentSlot(s_dot, headSize, c, center);
            if (line < 0 || head < 0) { ReleaseSlot(line); ReleaseSlot(head); return; }
            float maxDist = Random.Range(distMin, distMax);
            AddFx(new Fx
            {
                kind = FxKind.Line, slot = line, headSlot = head, pos = center, origin = center,
                vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(speedMin, speedMax),
                life = FireworkBurstDuration, size = headSize, gravity = 30f, drag = 0.6f,
                maxDist = maxDist, fadeStart = maxDist * Random.Range(0.45f, 0.6f), color = c,
            });
        }
    }

    private void SpawnGems(Vector2 center)
    {
        int count = Random.Range(10, 13);
        for (int i = 0; i < count; i++)
        {
            var sprite = gemSprites[i % gemSprites.Length];
            if (sprite == null) continue;
            float a = (360f / count * i + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
            float size = Random.Range(62f, 84f);
            int slot = RentSlot(sprite, size, Color.white, center);
            if (slot < 0) return;
            AddFx(new Fx
            {
                kind = FxKind.Gem, slot = slot, headSlot = -1, pos = center,
                vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.35f) * Random.Range(360f, 540f),
                life = Random.Range(1.3f, 1.6f), size = size, gravity = 620f, drag = 0.4f,
                rotVel = Random.Range(-260f, 260f), color = Color.white,
            });
        }
    }

    private void AddFlash(Vector2 center, Color color, float size)
    {
        int slot = RentSlot(s_glow, size, color, center);
        if (slot < 0) return;
        AddFx(new Fx { kind = FxKind.Flash, slot = slot, headSlot = -1, pos = center, life = 0.3f, size = size, color = color });
    }

    private IEnumerator ConfettiRain(float halfW, float halfH)
    {
        int batches = 6;
        int per = Mathf.CeilToInt(confettiCount / (float)batches);
        for (int b = 0; b < batches && !_skipRequested; b++)
        {
            for (int i = 0; i < per; i++)
            {
                var c = Color.HSVToRGB(Random.value, Random.Range(0.55f, 0.85f), 1f);
                var p = new Vector2(Random.Range(-halfW, halfW), halfH + Random.Range(20f, 120f));
                float w = Random.Range(12f, 18f);
                int slot = RentSlot(s_square, w, c, p);
                if (slot < 0) break;
                AddFx(new Fx
                {
                    kind = FxKind.Confetti, slot = slot, headSlot = -1, pos = p,
                    vel = new Vector2(Random.Range(-50f, 50f), -Random.Range(230f, 330f)),
                    life = Random.Range(2.6f, 3.4f), size = w, size2 = w * Random.Range(1.5f, 2f),
                    rot = Random.Range(0f, 360f), rotVel = Random.Range(-200f, 200f),
                    phase = Random.Range(0f, 6.28f), color = c,
                });
            }
            yield return WaitUnscaled(0.2f);
        }
    }

    private void AddFx(in Fx fx)
    {
        if (_fxCount >= MaxFx) { ReleaseSlot(fx.slot); ReleaseSlot(fx.headSlot); return; }
        _fx[_fxCount++] = fx;
    }

    // ── Tek guncelleme dongusu ───────────────────────────────────────────────
    private void LateUpdate()
    {
        if (_fxCount == 0 && (_rays == null || !_rays.enabled)) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        StepRays(dt);

        for (int i = _fxCount - 1; i >= 0; i--)
        {
            ref Fx f = ref _fx[i];
            f.age += dt;
            if (!StepFx(ref f, dt))
            {
                ReleaseSlot(f.slot);
                ReleaseSlot(f.headSlot);
                _fx[i] = _fx[--_fxCount];
            }
        }
    }

    private void StepRays(float dt)
    {
        if (_rays == null || !_rays.enabled) return;
        _raysAlpha = Mathf.MoveTowards(_raysAlpha, _raysTarget, dt * 1.4f);
        var c = _rays.color; c.a = _raysAlpha; _rays.color = c;
        _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -raysSpinSpeed * Time.unscaledTime);
        if (_raysTarget <= 0f && _raysAlpha <= 0f) _rays.enabled = false;
    }

    // false → parcacik bitti.
    private bool StepFx(ref Fx f, float dt)
    {
        var rt = _poolRt[f.slot];
        var img = _poolImg[f.slot];
        switch (f.kind)
        {
            case FxKind.Rocket:
            {
                float k = Mathf.Clamp01(f.age / f.life);
                if (f.sound && f.age >= f.life - fireworkSoundLead) { f.sound = false; FireworkBurst?.Invoke(); }
                float e = 1f - (1f - k) * (1f - k);
                Vector2 head = Vector2.LerpUnclamped(f.origin, f.target, e);
                Vector2 tail = Vector2.Lerp(f.origin, head, Mathf.Max(0f, e - 0.28f) / Mathf.Max(0.0001f, e));
                rt.anchoredPosition = head;
                SetFireworkLine(_poolRt[f.headSlot], tail, head, Mathf.Lerp(5f, 3f, k));
                if (k < 1f) return true;
                if (f.sound) FireworkBurst?.Invoke();
                SpawnBurst(f.target, f.color, f.style);
                if (f.style != BurstStyle.Gem && _fxCount < MaxFx / 3 && Random.value < 0.5f)
                    SpawnBurst(f.target + new Vector2(Random.Range(-70f, 71f), Random.Range(-40f, 41f)), RandomVividColor(),
                               BurstStyle.Normal);
                return false;
            }
            case FxKind.Line:
            {
                f.pos += f.vel * dt;
                f.vel *= Mathf.Exp(-f.drag * dt);
                f.vel.y -= f.gravity * dt;
                float t = f.age / f.life;
                float dist = Vector2.Distance(f.pos, f.origin);
                float len = Mathf.Min(dist, Mathf.Lerp(240f, 150f, t));
                Vector2 tail = f.pos - f.vel.normalized * len;
                SetFireworkLine(rt, tail, f.pos, Mathf.Lerp(5f, 2.4f, t));
                float alpha = 1f - Mathf.InverseLerp(f.fadeStart, f.maxDist, dist);
                SetAlpha(img, f.color, alpha);
                var head = _poolRt[f.headSlot];
                head.anchoredPosition = f.pos;
                head.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, t);
                SetAlpha(_poolImg[f.headSlot], f.color, alpha);
                return t < 1f && alpha > 0.01f;
            }
            case FxKind.Flash:
            {
                float t = f.age / f.life;
                rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.2f, Mathf.Clamp01(t * 2.5f));
                SetAlpha(img, f.color, 1f - t);
                return t < 1f;
            }
            case FxKind.Sparkle:
            {
                f.pos += f.vel * dt;
                f.vel *= Mathf.Exp(-f.drag * dt);
                f.vel.y -= f.gravity * dt;
                rt.anchoredPosition = f.pos;
                float t = f.age / f.life;
                float twinkle = 0.55f + 0.45f * Mathf.Sin(f.age * 18f + f.phase);
                SetAlpha(img, f.color, twinkle * (1f - t * t));
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, t);
                return t < 1f;
            }
            case FxKind.Gem:
            {
                f.pos += f.vel * dt;
                f.vel *= Mathf.Exp(-f.drag * dt);
                f.vel.y -= f.gravity * dt;
                f.rot += f.rotVel * dt;
                rt.anchoredPosition = f.pos;
                rt.localRotation = Quaternion.Euler(0f, 0f, f.rot);
                float t = f.age / f.life;
                rt.localScale = Vector3.one * (t < 0.12f ? Mathf.Lerp(0.3f, 1f, t / 0.12f) : 1f);
                SetAlpha(img, Color.white, 1f - Mathf.InverseLerp(0.7f, 1f, t));
                return t < 1f;
            }
            case FxKind.Confetti:
            {
                f.pos += f.vel * dt;
                f.rot += f.rotVel * dt;
                rt.anchoredPosition = f.pos + new Vector2(Mathf.Sin(f.age * 3f + f.phase) * 26f, 0f);
                rt.localRotation = Quaternion.Euler(0f, 0f, f.rot);
                rt.localScale = new Vector3(Mathf.Cos(f.age * 7f + f.phase), 1f, 1f);   // kagit donerek parlar
                float t = f.age / f.life;
                SetAlpha(img, f.color, 1f - Mathf.InverseLerp(0.8f, 1f, t));
                return t < 1f;
            }
        }
        return false;
    }

    private static void SetAlpha(Image img, Color c, float a)
    {
        c.a = Mathf.Clamp01(a);
        img.color = c;
    }

    private static void SetFireworkLine(RectTransform rt, Vector2 tail, Vector2 head, float width)
    {
        Vector2 delta = head - tail;
        rt.anchoredPosition = (head + tail) * 0.5f;
        rt.sizeDelta = new Vector2(width, Mathf.Max(width, delta.magnitude));
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f);
    }

    // ── Havuz ───────────────────────────────────────────────────────────────
    // Onceden hazirlanan slot sayisi: tipik gosterinin tepe kullanimini karsilar; asarsa MaxSlots'a kadar tembel buyur.
    private const int FxPrewarmSlots = 360;

    /// Oyun sahnesi acilirken (LevelEndSimplePopupController, yukleme ekrani arkasinda) cagrilir. Panel sahnede
    /// kapali basladigi icin Awake gosteri aninda calisir; isitma oraya kalirsa ilk fiseklerde takilma olur.
    public void Prewarm()
    {
        EnsureFxSprites();
        PrewarmFxPool(FxPrewarmSlots);
    }

    private void PrewarmFxPool(int count)
    {
        if (vfxRoot == null) return;
        count = Mathf.Min(count, MaxSlots);
        while (_poolImg.Count < count)
        {
            int slot = CreateSlot();
            _poolImg[slot].enabled = false;
            _freeSlots.Push(slot);
        }
    }

    private int CreateSlot()
    {
        var go = new GameObject("Fx", typeof(RectTransform), typeof(Image)) { layer = vfxRoot.gameObject.layer };
        var rtNew = (RectTransform)go.transform;
        rtNew.SetParent(vfxRoot, false);
        rtNew.anchorMin = rtNew.anchorMax = new Vector2(0.5f, 0.5f);
        var imgNew = go.GetComponent<Image>();
        imgNew.raycastTarget = false;
        _poolImg.Add(imgNew);
        _poolRt.Add(rtNew);
        return _poolImg.Count - 1;
    }

    private int RentSlot(Sprite sprite, float size, Color color, Vector2 pos)
    {
        int slot;
        if (_freeSlots.Count > 0) slot = _freeSlots.Pop();
        else
        {
            if (_poolImg.Count >= MaxSlots || vfxRoot == null) return -1;
            slot = CreateSlot();
        }
        var img = _poolImg[slot];
        var rt = _poolRt[slot];
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = false;
        img.enabled = true;
        rt.SetAsLastSibling();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        return slot;
    }

    private void ReleaseSlot(int slot)
    {
        if (slot < 0 || slot >= _poolImg.Count) return;
        var img = _poolImg[slot];
        if (img == null || !img.enabled) return;
        img.enabled = false;
        _freeSlots.Push(slot);
    }

    private void ClearSpawnedFireworks()
    {
        for (int i = 0; i < _fxCount; i++)
        {
            ReleaseSlot(_fx[i].slot);
            ReleaseSlot(_fx[i].headSlot);
        }
        _fxCount = 0;
        if (_rays != null)
        {
            _raysTarget = 0f;
            _raysAlpha = 0f;
            _rays.enabled = false;
        }
    }

    // ── Prosedurel dokular (bir kez, statik) ────────────────────────────────
    // Atlas 128x64: [0,0] yumusak parilti 32² · [32,0] dolu nokta 32² · [64,0] isin izi 16x64 ·
    // [80,0] konfeti karesi 16² · [96,0] 4 kollu yildiz 32². Isik huzmeleri ayri 256² doku.
    private static void EnsureFxSprites()
    {
        if (s_fxAtlas != null) return;
        const int W = 128, H = 64;
        s_fxAtlas = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "FireworkAtlas", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float a = 0f;
            if (x < 32 && y < 32)
            {
                float r = new Vector2((x + 0.5f) / 16f - 1f, (y + 0.5f) / 16f - 1f).magnitude;
                a = Mathf.Clamp01(1f - r); a *= a;
            }
            else if (x < 64 && y < 32)
            {
                float r = new Vector2((x - 32 + 0.5f) / 16f - 1f, (y + 0.5f) / 16f - 1f).magnitude;
                a = Mathf.Clamp01((1f - r) * 16f);
            }
            else if (x < 80)
            {
                float across = Mathf.Abs((x - 64 + 0.5f) / 16f * 2f - 1f);
                float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, across));
                float tail = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((y + 0.5f) / H * 4f));
                a = edge * tail;
            }
            else if (x < 96 && y < 16)
            {
                a = (x > 80 && x < 95 && y > 0 && y < 15) ? 1f : 0f;
            }
            else if (x >= 96 && y < 32)
            {
                float u = (x - 96 + 0.5f) / 16f - 1f, v = (y + 0.5f) / 16f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float star = Mathf.Max(Mathf.Exp(-u * u * 60f) * (1f - Mathf.Abs(v)), Mathf.Exp(-v * v * 60f) * (1f - Mathf.Abs(u)));
                float core = Mathf.Clamp01(1f - r * 2.2f);
                a = Mathf.Clamp01(star + core * core);
            }
            px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        s_fxAtlas.SetPixels32(px);
        s_fxAtlas.Apply(false, true);
        var c = new Vector2(0.5f, 0.5f);
        s_glow = Sprite.Create(s_fxAtlas, new Rect(0, 0, 32, 32), c);
        s_dot = Sprite.Create(s_fxAtlas, new Rect(32, 0, 32, 32), c);
        s_line = Sprite.Create(s_fxAtlas, new Rect(64, 0, 16, 64), c);
        s_square = Sprite.Create(s_fxAtlas, new Rect(80, 0, 16, 16), c);
        s_sparkle = Sprite.Create(s_fxAtlas, new Rect(96, 0, 32, 32), c);

        const int R = 256;
        var raysTex = new Texture2D(R, R, TextureFormat.RGBA32, false) { name = "FireworkRays", wrapMode = TextureWrapMode.Clamp };
        var rpx = new Color32[R * R];
        const int rayCount = 12;
        for (int y = 0; y < R; y++)
        for (int x = 0; x < R; x++)
        {
            float u = (x + 0.5f) / (R * 0.5f) - 1f, v = (y + 0.5f) / (R * 0.5f) - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            float ang = Mathf.Atan2(v, u);
            float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ang * rayCount), 3f);
            float fall = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);   // merkez ve kenar yumusak
            rpx[y * R + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(ray * fall * 1.4f) * 255f));
        }
        raysTex.SetPixels32(rpx);
        raysTex.Apply(false, true);
        s_raysSprite = Sprite.Create(raysTex, new Rect(0, 0, R, R), c);
    }

    // -------------------------------------------------------------------------
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float tm1 = t - 1f;
        return 1f + c3 * (tm1 * tm1 * tm1) + c1 * (tm1 * tm1);
    }
}
