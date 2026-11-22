using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pulse+Pulse combo — SADECE charge animasyonu.
/// Squash → Stretch → Wobble (şişip inen + yamuk) → Peak Hold
///
/// Patlama bu component'te YOK — BoardController charge bittikten sonra
/// mevcut PulseCoreImpactService.PlayPulseCoreExplosionVfxAtCell() ile
/// daha geniş alanda (5x5) patlatır.
///
/// Start()'ta otomatik başlar. ChargeDuration property'si BoardController
/// tarafından senkron için okunur — değeri koruyoruz.
/// </summary>
public class PulsePulseExplosionVfx : MonoBehaviour
{
    [Header("Charge — Bomb")]
    [Tooltip("PulseCore bomba sprite'ı. TileIconLibrary'deki PulseCore. crackFrames boşsa şarj boyunca bu kullanılır.")]
    [SerializeField] private Sprite bombSprite;
    [SerializeField] private float bombBaseSize = 130f;
    [SerializeField] private float chargeDuration = 2.0f;

    [Tooltip("TNT çatlama kareleri: EN TEMİZDEN EN ÇATLAĞA sıralı. İlki temiz TNT, sonuncusu en çok " +
             "çatlamış (altından sarı alev görünen). Şarj ilerledikçe eşit dilimlerle geçilir; en son " +
             "kare patlamaya geçerken kalır. Boş bırakılırsa yalnız bombSprite kullanılır (eski davranış).")]
    [SerializeField] private Sprite[] crackFrames;

    [Tooltip("Çatlak kareleri arası ÇAPRAZ GEÇİŞ oranı (her karenin diliminin son yüzdesi). 0 = sert " +
             "geçiş (pat). 0.35 = dilimin son %35'inde bir sonraki daha-çatlak kare üstüne yavaşça " +
             "bindirilir. 1.0 = kare boyunca SÜREKLİ yumuşak morph (kareler hiç durmaz, en akışkan). " +
             "Geçiş smoothstep ile yumuşatılır (lineer değil).")]
    [SerializeField, Range(0f, 1f)] private float crackCrossfadePortion = 0.5f;

    [Tooltip("Kareler ŞEKİL değiştiriyorsa (basık/uzun/yuvarlak şarj kareleri) açık: geçişin ikinci yarısında " +
             "alttaki kare söner → eski siluet yenisinin arkasında hayalet gibi kalmaz. Çatlak kareleri için kapalı.")]
    [SerializeField] private bool fadeBaseOnBlend;

    [Header("Charge — Board-sized growth (kullanıcı 2026-11-22)")]
    [Tooltip("Şarj sonunda fıçı GÖVDESİNİN kaç hücre genişliğe ulaşacağı (0 = eski peakScale davranışı). " +
             "Büyüme şarj boyunca kesintisiz → tüm kareler büyürken görünür.")]
    [SerializeField] private float growToCells = 5.2f;
    [Tooltip("Şarj başında fıçı gövdesinin hücre genişliği (birleşen PulseCore boyuna yakın).")]
    [SerializeField] private float startCells = 1.2f;
    [Tooltip("Karede fıçı gövdesinin tuval genişliğine oranı (PC2 kareleri ≈ 0.79).")]
    [SerializeField, Range(0.1f, 1f)] private float spriteBodyFill = 0.79f;

    [Header("Charge — Halo (seviye sonu logo halesi gibi dönen ışınlar)")]
    [Tooltip("Hale çapı hücre cinsinden: patlama alanı 7x7 → 7.")]
    [SerializeField] private float haloCells = 7f;
    [SerializeField, Range(0f, 1f)] private float haloMaxAlpha = 0.8f;
    [SerializeField] private float haloSpinSpeed = 40f;
    [SerializeField] private Color haloColor = new Color(1f, 0.86f, 0.45f, 1f);
    [Tooltip("Işınların arkasındaki YUVARLAK sıcak parıltının opaklığı (hale opaklığına oranla). " +
             "2026-11-22 kullanıcı: 0.45 fazla → 0.25.")]
    [SerializeField, Range(0f, 1f)] private float haloGlowAlpha = 0.25f;

    [Header("Charge — Tingle (karıncalanma katmanı)")]
    [Tooltip("Elektrik kıvılcımı döngü kareleri (saydam, yalnız kıvılcım). Bombanın üstünde döngüde oynar, " +
             "bombayla birlikte esner. Boşsa katman yok.")]
    [SerializeField] private Sprite[] tingleFrames;
    [SerializeField, Min(1f)] private float tingleFps = 14f;
    [Tooltip("Katmanın boyutu, bomba boyutuna oranla.")]
    [SerializeField] private float tingleSizeMultiplier = 1.15f;
    [Tooltip("Karıncalanmanın görünmeye başladığı ve tam görünür olduğu şarj oranları.")]
    [SerializeField, Range(0f, 1f)] private float tingleStartProgress = 0.3f;
    [SerializeField, Range(0f, 1f)] private float tingleFullProgress = 0.7f;

    [Header("Charge — Crack Fire Overlay")]
    [Tooltip("PCF_3 gibi yalnız alev/çatlak ışığı içeren overlay sprite. Bombanın üstünde ayrı büyür.")]
    [SerializeField] private Sprite crackFireOverlaySprite;
    [Tooltip("crackFrames ile aynı index'e hizalı alev overlay'leri. Örn: PCF_2 index=PC_2, PCF_3 index=PC_3.")]
    [SerializeField] private Sprite[] crackFireOverlayFrames;
    [Tooltip("Açıkken alev, son çatlak kareye geçiş başlamadan hemen önce otomatik büyümeye başlar.")]
    [SerializeField] private bool autoStartFireOnFinalCrackBlend = true;
    [SerializeField, Range(0f, 0.2f)] private float crackFireLeadRatio = 0.035f;
    [SerializeField, Range(0f, 1f)] private float crackFireStartProgress = 0.62f;
    [SerializeField, Range(0f, 1f)] private float crackFireFullProgress = 0.86f;
    [SerializeField] private float crackFireStartScale = 0.78f;
    [SerializeField] private float crackFirePeakScale = 1.38f;
    [SerializeField] private float crackFirePulseAmplitude = 0.08f;
    [SerializeField] private float crackFirePulseFrequency = 14f;
    [SerializeField, Range(1f, 3f)] private float crackFireFrameFadeOutBoost = 1.55f;
    [SerializeField, Range(0f, 1f)] private float crackFireFadeOutStartProgress = 0.90f;
    [SerializeField, Range(0f, 1f)] private float crackFireEndAlphaMultiplier = 0.18f;
    [SerializeField] private Color crackFireColor = new Color(1f, 1f, 1f, 0.95f);

    [Header("Charge — Phase Ratios (toplamı 1.0 olmalı)")]
    [Tooltip("Çöküp genişleme (anticipation)")]
    [SerializeField, Range(0f, 1f)] private float phaseSquashRatio = 0.15f;
    [Tooltip("Yukarı uzayıp incelme")]
    [SerializeField, Range(0f, 1f)] private float phaseStretchRatio = 0.15f;
    [Tooltip("Şişip inen + yamuk salınımlı büyüme")]
    [SerializeField, Range(0f, 1f)] private float phaseWobbleRatio = 0.55f;
    [Tooltip("En büyükte nabız atan duruş")]
    [SerializeField, Range(0f, 1f)] private float phasePeakRatio = 0.15f;

    [Header("Charge — Squash/Stretch Amounts")]
    [SerializeField] private float squashScaleX = 1.22f;
    [SerializeField] private float squashScaleY = 0.75f;
    [SerializeField] private float stretchScaleX = 0.82f;
    [SerializeField] private float stretchScaleY = 1.32f;

    [Header("Charge — Wobble (yamuk/şişen his)")]
    [Tooltip("x ve y ekseninde zıt fazlı sinüs genliği (0.12 = %12)")]
    [SerializeField] private float wobbleAmplitude = 0.14f;
    [Tooltip("Saniyede kaç şişip-inme döngüsü")]
    [SerializeField] private float wobbleFrequency = 5.5f;
    [Tooltip("Z ekseninde hafif rotasyon salınımı — yamuk hissi verir")]
    [SerializeField] private float tiltDegrees = 8f;

    [Header("Charge — Peak")]
    [Tooltip("Wobble sonunda ulaşılan maksimum uniform scale")]
    [SerializeField] private float peakScale = 1.45f;
    [Tooltip("Peak hold'daki hızlı nabız genliği (%)")]
    [SerializeField] private float peakPulseAmplitude = 0.03f;
    [SerializeField] private float peakPulseFrequency = 18f;

    [Header("Charge — Shake (Sarsıntı)")]
    [Tooltip("Şarj ilerledikçe artan KONUMSAL titreme — TNT patlamaya yaklaşırken sarsılır. 0 = kapalı. " +
             "Fazla düz duran charge'a gerilim ekler; sona doğru sertleşerek patlamaya 'atlatır'.")]
    [SerializeField] private float chargeShakeMaxAmplitude = 16f;
    [Tooltip("Titremenin başladığı şarj oranı (0..1). Bu orandan önce sabit durur, sonra sarsılmaya başlar.")]
    [SerializeField, Range(0f, 1f)] private float chargeShakeStartProgress = 0.22f;
    [Tooltip("Saniyedeki titreme frekansı. Yüksek = daha sinirli/hızlı sarsılma.")]
    [SerializeField] private float chargeShakeFrequency = 32f;
    [Tooltip("Titreme zarfının kuvvet eğrisi. 1 = lineer artış, 2+ = yalnız son anlarda sertçe patlar.")]
    [SerializeField] private float chargeShakeRamp = 2.2f;
    [Tooltip("Titremeye eşlik eden mikro Z rotasyonu (derece).")]
    [SerializeField] private float chargeShakeRotation = 3.5f;

    [Header("Charge — Glow")]
    [Tooltip("Glow sprite (Knob). Bombanın arkasında yumuşak parlama.")]
    [SerializeField] private Sprite glowSprite;
    [SerializeField] private float glowSizeMultiplier = 2.2f;
    [SerializeField] private Color glowColorStart = new Color(0.7f, 0.85f, 1f, 0f);
    [SerializeField] private Color glowColorPeak = new Color(1f, 1f, 1f, 0.25f);

    [Header("Charge — Fuse Flame (Alev/Kıvılcım)")]
    [Tooltip("Kodla üretilen fitil kıvılcımları. Kareler kendi fitil alevini çiziyorsa kapat (fitil ucu karede yer değiştirir).")]
    [SerializeField] private bool emitFuseSparks = true;
    [Tooltip("Fitil kıvılcım sprite'ı (soft_circle). Atanmazsa soft circle halo kullanılır.")]
    [SerializeField] private Sprite sparkSprite;
    [Tooltip("Normal 100px tile'da fitil (10, 50) ofsetindedir (oran: x=0.10, y=0.50).")]
    [SerializeField] private Vector2 fuseNormalizedOffset = new Vector2(0.10f, 0.50f);
    [SerializeField] private float sparkSizeMin = 28f;
    [SerializeField] private float sparkSizeMax = 54f;
    [SerializeField] private float spreadRadius = 16f;
    [SerializeField] private float sparkEmitInterval = 0.018f;
    [SerializeField] private float sparkLifetime = 0.50f;
    [SerializeField] private Color sparkColorA = new Color(1.00f, 0.65f, 0.15f, 1f);
    [SerializeField] private Color sparkColorB = new Color(1.00f, 0.20f, 0.05f, 1f);

    private float _cellSize = -1f;   // VFX yerel biriminde bir hücre (BoardVfxService verir)
    private float _chargeProgress;
    private float _growthEnd = 1f;   // 1 = büyüme modu kapalı
    private bool GrowthMode => _growthEnd > 1f;

    /// Spawn eden servis çağırır (Start'tan önce): hücre boyu VFX'in kendi yerel biriminde.
    public void SetCellSize(float localCellSize) => _cellSize = localCellSize;

    private Animator animator;
    private RectTransform chargeContainer;
    private int _crackBaseIdx = -1;
    private int _crackOverlayIdx = -1;

    public float ChargeDuration => chargeDuration;

    public void AttachFuse(TileView tile)
    {
        // Geriye uyumluluk için korundu
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator) animator.enabled = false;

        // Mevcut ring/flash child'larını gizle — kullanmıyoruz
        string[] names = { "Ring", "Ring2", "Flash" };
        foreach (var name in names)
        {
            var t = transform.Find(name);
            if (t) t.gameObject.SetActive(false);
        }
    }

    // HAVUZ (BoardVfxService): örnek yok edilmez; şarj bitince kapanır, sonraki kombo Play() ile yeniden kullanır.
    private bool _played;
    private float _authoredBombBaseSize = -1f;
    private Image _haloGlow, _haloRay, _haloRay2;

    private void Start()
    {
        if (!_played) Play();   // havuz dışı (eski) kullanım: Instantiate edilince kendiliğinden oynar
    }

    /// Şarjı baştan oynatır. Servis konum/ölçek/hücre boyunu ayarladıktan SONRA çağırır.
    public void Play()
    {
        _played = true;
        StopAllCoroutines();
        CleanupCharge();
        if (_authoredBombBaseSize < 0f) _authoredBombBaseSize = bombBaseSize;
        bombBaseSize = _authoredBombBaseSize;   // büyüme modu her oynatmada yeniden hesaplar
        _growthEnd = 1f;
        _chargeProgress = 0f;

        // Inspector-kayıtlı sarı glow değerlerini eziyoruz — sadece beyaz düşük alpha glow istiyoruz.
        glowColorStart = new Color(0.7f, 0.85f, 1f, 0f);
        glowColorPeak  = new Color(1f, 1f, 1f, 0.25f);
        glowSizeMultiplier = 2.2f;

        StartCoroutine(CoChargeThenRelease());
    }

    private IEnumerator CoChargeThenRelease()
    {
        yield return CoCharge();
        CleanupCharge();
        gameObject.SetActive(false);   // boşa çıktı → havuza döner (önceden kök nesne hiç yok edilmiyordu)
    }

    private void CleanupCharge()
    {
        UiFxPool.ReleaseChargeSparks(transform);
        if (chargeContainer != null) chargeContainer.gameObject.SetActive(false);
        if (_haloGlow != null) _haloGlow.gameObject.SetActive(false);
        if (_haloRay != null) _haloRay.gameObject.SetActive(false);
        if (_haloRay2 != null) _haloRay2.gameObject.SetActive(false);
        _haloGlow = _haloRay = _haloRay2 = null;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        CleanupCharge();
    }

    public void PlayStreaks()
    {
        // Geriye uyumluluk — Start() zaten başlatıyor
    }

    // ════════════════════════════════════════════════
    //  CHARGE: Squash → Stretch → Wobble → Peak
    // ════════════════════════════════════════════════
    private IEnumerator CoCharge()
    {
        var parent = transform as RectTransform;
        if (!parent) yield break;

        // Tahta ölçeğinde büyüme: fıçı ~1 hücreden growToCells hücreye kesintisiz büyür; hale tam patlama alanı.
        if (_cellSize > 0f && growToCells > 0f && startCells > 0f)
        {
            bombBaseSize = startCells * _cellSize / spriteBodyFill;
            _growthEnd = Mathf.Max(1.01f, growToCells / startCells);
        }
        Sprite raysSprite = GrowthMode ? LevelCompletionLogoAnimation.SharedRaysSprite : null;
        if (raysSprite != null)
            StartCoroutine(CoHalo(parent, raysSprite, chargeDuration));

        var container = chargeContainer = CreateContainer(parent);

        // Glow (arkada) — hale varken düz yuvarlak parıltı kullanılmaz (kullanıcı: "çok düz")
        Image glowImg = null;
        if (glowSprite && raysSprite == null)
        {
            float glowSize = bombBaseSize * glowSizeMultiplier;
            glowImg = CreateUIImage("ChargeGlow", container, glowSprite, glowSize);
            glowImg.color = glowColorStart;
        }

        // Bomba (önde) — deformasyon bunun üstünde yaşayacak
        RectTransform bombRt = null;
        Image bombImg = null;
        Image bombOverlayImg = null;
        Image crackFireImg = null;
        Image crackFireOverlayImg = null;
        if (bombSprite)
        {
            bombImg = CreateUIImage("ChargeBomb", container, bombSprite, bombBaseSize);
            bombImg.preserveAspect = true;
            bombImg.color = Color.white;
            bombRt = bombImg.rectTransform;

            // Çapraz geçiş katmanı: deforme olan bombanın CHILD'ı → aynı squash/stretch/wobble/tilt'i
            // miras alır. Bir sonraki (daha çatlak) kareyi taşır, alfası dilim sonunda 0→1 yükselir.
            bombOverlayImg = CreateUIImage("ChargeBombCrackOverlay", bombRt, bombSprite, bombBaseSize);
            bombOverlayImg.preserveAspect = true;
            bombOverlayImg.color = new Color(1f, 1f, 1f, 0f);

            if (HasCrackFireOverlay())
            {
                Sprite initialFire = GetInitialFireOverlaySprite();
                crackFireImg = CreateUIImage("ChargeBombFireOverlay", bombRt, initialFire, GetCrackFireBaseSize());
                crackFireImg.preserveAspect = true;
                crackFireImg.color = WithAlpha(crackFireColor, 0f);
                crackFireImg.rectTransform.localScale = Vector3.one * crackFireStartScale;
                crackFireImg.rectTransform.SetAsLastSibling();

                crackFireOverlayImg = CreateUIImage("ChargeBombFireBlendOverlay", bombRt, initialFire, GetCrackFireBaseSize());
                crackFireOverlayImg.preserveAspect = true;
                crackFireOverlayImg.color = WithAlpha(crackFireColor, 0f);
                crackFireOverlayImg.rectTransform.localScale = Vector3.one * crackFireStartScale;
                crackFireOverlayImg.rectTransform.SetAsLastSibling();
            }
        }

        // Çatlak sekansı başlangıcı: en temiz kare (varsa) baştan görünsün.
        _crackBaseIdx = -1;
        _crackOverlayIdx = -1;
        UpdateChargeCrackBlend(bombImg, bombOverlayImg, 0f);
        UpdateCrackFireOverlay(crackFireImg, crackFireOverlayImg, 0f, 0f);

        // Fitil Alevi: doğrudan bombRt'nin child'ı olarak çalışır.
        // Bomba nefes aldıkça (squash/stretch/wobble/tilt), fitil ucuyla %100 senkron hareket eder.
        if (bombRt != null && emitFuseSparks)
        {
            StartCoroutine(CoEmitFuseSparks(bombRt, chargeDuration));
        }

        // Karıncalanma: bombanın child'ı (esnemeyi miras alır), kareler döngüde; alfa şarjla artar.
        if (bombRt != null && tingleFrames != null && tingleFrames.Length > 0 && tingleFrames[0] != null)
        {
            var tingleImg = CreateUIImage("ChargeTingle", bombRt, tingleFrames[0], bombBaseSize * tingleSizeMultiplier);
            tingleImg.preserveAspect = true;
            tingleImg.color = new Color(1f, 1f, 1f, 0f);
            tingleImg.rectTransform.SetAsLastSibling();
            StartCoroutine(CoTingle(tingleImg, chargeDuration));
        }

        // Evre süreleri — ratio'lar 1'e toplanmayabilir, normalize et
        float ratioSum = Mathf.Max(0.0001f,
            phaseSquashRatio + phaseStretchRatio + phaseWobbleRatio + phasePeakRatio);
        float tSquash = chargeDuration * (phaseSquashRatio / ratioSum);
        float tStretch = chargeDuration * (phaseStretchRatio / ratioSum);
        float tWobble = chargeDuration * (phaseWobbleRatio / ratioSum);
        float tPeak = chargeDuration * (phasePeakRatio / ratioSum);

        Vector3 baseScale = Vector3.one;
        float totalElapsed = 0f;

        // ─── 1) SQUASH — çöküp yanlara genişle ─────────────────
        Vector3 squashTarget = new Vector3(squashScaleX, squashScaleY, 1f);
        yield return AnimatePhase(tSquash, (k, dt) =>
        {
            totalElapsed += dt;
            float eased = EaseOutQuad(k);
            Vector3 s = Vector3.LerpUnclamped(baseScale, squashTarget, eased);
            ApplyScale(container, bombRt, s);
            ApplyChargeShake(container, totalElapsed / chargeDuration, totalElapsed);
            UpdateGlow(glowImg, totalElapsed, bombImg);
            UpdateChargeCrackBlend(bombImg, bombOverlayImg, totalElapsed / chargeDuration);
            UpdateCrackFireOverlay(crackFireImg, crackFireOverlayImg, totalElapsed / chargeDuration, totalElapsed);
        });

        // ─── 2) STRETCH — yukarı uzayıp incelme ────────────────
        Vector3 stretchTarget = new Vector3(stretchScaleX, stretchScaleY, 1f);
        yield return AnimatePhase(tStretch, (k, dt) =>
        {
            totalElapsed += dt;
            float eased = EaseInOutQuad(k);
            Vector3 s = Vector3.LerpUnclamped(squashTarget, stretchTarget, eased);
            ApplyScale(container, bombRt, s);
            ApplyChargeShake(container, totalElapsed / chargeDuration, totalElapsed);
            UpdateGlow(glowImg, totalElapsed, bombImg);
            UpdateChargeCrackBlend(bombImg, bombOverlayImg, totalElapsed / chargeDuration);
            UpdateCrackFireOverlay(crackFireImg, crackFireOverlayImg, totalElapsed / chargeDuration, totalElapsed);
        });

        // ─── 3) WOBBLE — şişip inen + yamuk salınımlı büyüme ───
        Vector3 wobbleStart = stretchTarget;
        Vector3 wobbleEnd = Vector3.one * (GrowthMode ? 1f : peakScale);
        float wobbleElapsed = 0f;
        yield return AnimatePhase(tWobble, (k, dt) =>
        {
            totalElapsed += dt;
            wobbleElapsed += dt;
            float eased = EaseOutCubic(k);

            // Ana büyüme: stretch → peak
            Vector3 mid = Vector3.Lerp(wobbleStart, wobbleEnd, eased);

            // Non-uniform sinüs: x ve y zıt fazda → şişip inen his
            // Sönümlü: (1 - k) çarpanı ile salınım peak'e yaklaştıkça azalır
            float phase = wobbleElapsed * wobbleFrequency * Mathf.PI * 2f;
            float damp = 1f - k;
            float sx = 1f + Mathf.Sin(phase) * wobbleAmplitude * damp;
            float sy = 1f + Mathf.Sin(phase + Mathf.PI) * wobbleAmplitude * damp;

            Vector3 deformed = new Vector3(mid.x * sx, mid.y * sy, 1f);
            ApplyScale(container, bombRt, deformed);
            ApplyChargeShake(container, totalElapsed / chargeDuration, totalElapsed);

            // Yamuk his — Z rotasyonu damped sine
            float tilt = Mathf.Sin(phase * 0.8f) * tiltDegrees * damp;
            if (bombRt) bombRt.localRotation = Quaternion.Euler(0f, 0f, tilt);

            UpdateGlow(glowImg, totalElapsed, bombImg);
            UpdateChargeCrackBlend(bombImg, bombOverlayImg, totalElapsed / chargeDuration);
            UpdateCrackFireOverlay(crackFireImg, crackFireOverlayImg, totalElapsed / chargeDuration, totalElapsed);
        });

        // ─── 4) PEAK HOLD — en büyükte hızlı nabız ─────────────
        if (bombRt) bombRt.localRotation = Quaternion.identity;
        float peakElapsed = 0f;
        yield return AnimatePhase(tPeak, (k, dt) =>
        {
            totalElapsed += dt;
            peakElapsed += dt;

            float pulse = Mathf.Sin(peakElapsed * peakPulseFrequency) * peakPulseAmplitude;
            Vector3 s = wobbleEnd * (1f + pulse);
            ApplyScale(container, bombRt, s);
            ApplyChargeShake(container, totalElapsed / chargeDuration, totalElapsed);

            // Son evrede glow full-peak + hafif flash
            UpdateGlowPeak(glowImg, totalElapsed, bombImg, k);
            // Peak boyunca en çatlak (son) kare kalır — patlamaya geçişe kadar.
            UpdateChargeCrackBlend(bombImg, bombOverlayImg, totalElapsed / chargeDuration);
            UpdateCrackFireOverlay(crackFireImg, crackFireOverlayImg, totalElapsed / chargeDuration, totalElapsed);
        });

        UiFxPool.ReleaseChargeSparks(transform);
        container.gameObject.SetActive(false);
    }

    private float GrowthAt(float p)
    {
        if (!GrowthMode) return 1f;
        p = Mathf.Clamp01(p);
        return Mathf.Lerp(1f, _growthEnd, p * p * (3f - 2f * p));
    }

    private float HaloScaleAt(float p) => GrowthMode ? GrowthAt(p) / _growthEnd : 1f;

    // Fıçının arkasında, patlama alanı (haloCells x hücre) çapında dönen ışın halesi + sıcak parıltı.
    // Sarsıntıdan etkilenmez (container dışında); şarjla açılır, büyür, hızlanır; şarj bitince kaldırılır.
    private IEnumerator CoHalo(RectTransform parent, Sprite rays, float duration)
    {
        float full = haloCells * _cellSize;
        var glow = _haloGlow = glowSprite != null ? CreateUIImage("ChargeHaloGlow", parent, glowSprite, full * 1.05f) : null;
        var ray = _haloRay = CreateUIImage("ChargeHaloRays", parent, rays, full);
        var ray2 = _haloRay2 = CreateUIImage("ChargeHaloRays2", parent, rays, full * 0.8f);
        if (glow != null) glow.rectTransform.SetAsFirstSibling();
        ray.rectTransform.SetSiblingIndex(glow != null ? 1 : 0);
        ray2.rectTransform.SetSiblingIndex(ray.rectTransform.GetSiblingIndex() + 1);
        float t = 0f, angle = 0f;
        while (t < duration && ray != null)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float p = Mathf.Clamp01(t / duration);
            float open = Mathf.Clamp01(p / 0.35f);
            float a = haloMaxAlpha * open * open * (3f - 2f * open);
            // Hale fıçıyla AYNI oranda büyür (ApplyScale'deki büyüme eğrisi): bitişte tam haloCells, başta orantılı küçük.
            float s = HaloScaleAt(_chargeProgress);   // fıçının o kareki ilerlemesiyle birebir senkron
            angle += haloSpinSpeed * (0.6f + 1.4f * p) * dt;   // şarj ilerledikçe hızlanır
            ray.rectTransform.localScale = Vector3.one * s;
            ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            ray.color = new Color(haloColor.r, haloColor.g, haloColor.b, a);
            ray2.rectTransform.localScale = Vector3.one * s;
            ray2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle * 0.7f + 15f);
            ray2.color = new Color(1f, 0.97f, 0.85f, a * 0.55f);
            if (glow != null)
            {
                glow.rectTransform.localScale = Vector3.one * s;
                glow.color = new Color(haloColor.r, haloColor.g * 0.9f, haloColor.b * 0.6f, a * haloGlowAlpha);
            }
            yield return null;
        }
        if (glow != null) glow.gameObject.SetActive(false);
        if (ray != null) ray.gameObject.SetActive(false);
        if (ray2 != null) ray2.gameObject.SetActive(false);
        _haloGlow = _haloRay = _haloRay2 = null;
    }

    private IEnumerator CoTingle(Image img, float duration)
    {
        float t = 0f;
        int last = -1;
        float span = Mathf.Max(0.01f, tingleFullProgress - tingleStartProgress);
        while (t < duration && img != null)
        {
            t += Time.unscaledDeltaTime;
            int idx = (int)(t * tingleFps) % tingleFrames.Length;
            if (idx != last && tingleFrames[idx] != null) { img.sprite = tingleFrames[idx]; last = idx; }
            float k = Mathf.Clamp01((t / duration - tingleStartProgress) / span);
            img.color = new Color(1f, 1f, 1f, k);
            yield return null;
        }
    }

    // ────────────────────────────────────────────────
    //  Phase runner — süre bitene kadar her frame callback çağırır
    //  Callback: (k = normalized progress, dt = unscaledDeltaTime)
    // ────────────────────────────────────────────────
    private IEnumerator AnimatePhase(float duration, System.Action<float, float> onTick)
    {
        if (duration <= 0f) yield break;

        float t = 0f;
        while (t < duration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / duration);
            onTick(k, dt);
            yield return null;
        }
    }

    // Scale'i hem container hem bomb'a uygula. Glow'u bozmamak için
    // sadece bomb üzerinde non-uniform deformasyon yapıyoruz; container'a
    // uniform genel ölçek veriyoruz ki glow da büyüsün ama yamulmasın.
    private void ApplyScale(RectTransform container, RectTransform bombRt, Vector3 nonUniform)
    {
        // Container uniform: ortalama büyüme (glow da orantılı büyür)
        float uniform = (nonUniform.x + nonUniform.y) * 0.5f;
        // Tahta ölçeğinde büyüme: şarj boyunca yumuşak-kesintisiz (S eğrisi) 1 → _growthEnd.
        float growth = GrowthAt(_chargeProgress);
        container.localScale = Vector3.one * uniform * growth;

        // Bomb non-uniform üstüne binecek — container zaten uniform büyüttü,
        // yani bomb'un kendi local scale'i sadece "oran farkını" vermeli:
        if (bombRt)
        {
            float bx = uniform > 0.0001f ? nonUniform.x / uniform : 1f;
            float by = uniform > 0.0001f ? nonUniform.y / uniform : 1f;
            bombRt.localScale = new Vector3(bx, by, 1f);
        }
    }

    // Şarj ilerledikçe artan konumsal titreme (sarsıntı). Container'a uygulanır → glow+bomba+alev birlikte
    // sarsılır; bomba kendi wobble/tilt rotasyonunu (bombRt) ayrıca korur, ikisi üst üste biner.
    // Zarf: chargeShakeStartProgress'ten sonra 0→1, chargeShakeRamp ile son anlarda sertçe patlar.
    private void ApplyChargeShake(RectTransform container, float progress01, float elapsed)
    {
        _chargeProgress = Mathf.Clamp01(progress01);
        if (container == null) return;

        if (chargeShakeMaxAmplitude <= 0f)
        {
            container.anchoredPosition = Vector2.zero;
            container.localRotation = Quaternion.identity;
            return;
        }

        float p = Mathf.InverseLerp(chargeShakeStartProgress, 1f, Mathf.Clamp01(progress01));
        if (p <= 0f)
        {
            container.anchoredPosition = Vector2.zero;
            container.localRotation = Quaternion.identity;
            return;
        }

        float env = Mathf.Pow(p, Mathf.Max(0.01f, chargeShakeRamp));   // sona doğru sertleşen zarf
        float amp = chargeShakeMaxAmplitude * env;
        float w = elapsed * chargeShakeFrequency * Mathf.PI * 2f;

        // Çok-sinüslü → tekdüze olmayan, sinirli titreme (tek sinüs "salınım" gibi düz durur)
        float ox = (Mathf.Sin(w) + 0.5f * Mathf.Sin(w * 2.3f + 1.1f)) * amp;
        float oy = (Mathf.Sin(w * 1.37f + 0.7f) + 0.5f * Mathf.Sin(w * 2.9f)) * amp * 0.85f;
        container.anchoredPosition = new Vector2(ox, oy);

        float rot = Mathf.Sin(w * 0.9f + 0.3f) * chargeShakeRotation * env;
        container.localRotation = Quaternion.Euler(0f, 0f, rot);
    }

    // Şarj ilerlemesine (0..1) göre çatlak karelerini ÇAPRAZ GEÇİŞLE gösterir. N kare eşit dilime
    // bölünür; her dilimin son crackCrossfadePortion'ında bir sonraki (daha çatlak) kare overlay
    // katmanında 0→1 alfa ile üste bindirilir → çatlaklar pat diye değişmez, büyüyerek birleşir.
    // Base her zaman opak; overlay base ile aynı RGB tint'i (glow flash) alır, yalnız alfası değişir.
    // Deformasyon base'te yaşadığı için overlay (child) aynı nefes/yamulmayı miras alır.
    private void UpdateChargeCrackBlend(Image baseImg, Image overlayImg, float progress01)
    {
        if (baseImg == null || crackFrames == null || crackFrames.Length == 0)
            return;

        GetCrackFrameBlend(progress01, out int baseIdx, out int nextIdx, out float blend);

        // Base sprite yalnız değişince set edilir.
        if (baseIdx != _crackBaseIdx)
        {
            _crackBaseIdx = baseIdx;
            if (crackFrames[baseIdx] != null)
                baseImg.sprite = crackFrames[baseIdx];
        }

        if (overlayImg == null)
            return;

        if (nextIdx != _crackOverlayIdx)
        {
            _crackOverlayIdx = nextIdx;
            if (crackFrames[nextIdx] != null)
                overlayImg.sprite = crackFrames[nextIdx];
        }

        // Overlay RGB = base RGB (glow flash/bleach ile tutarlı), alfa = blend.
        var bc = baseImg.color;
        overlayImg.color = new Color(bc.r, bc.g, bc.b, Mathf.Clamp01(blend));
        if (fadeBaseOnBlend)
        {
            // Alttaki kare yalnız geçişin son %30'unda söner: hayalet siluet kalmaz, toplam opaklık da düşmez (yanıp sönme yok).
            bc.a = 1f - Mathf.Clamp01((blend - 0.7f) / 0.3f);
            baseImg.color = bc;
        }
    }

    private void GetCrackFrameBlend(float progress01, out int baseIdx, out int nextIdx, out float blend)
    {
        int n = crackFrames != null && crackFrames.Length > 0 ? crackFrames.Length : 1;
        float p = Mathf.Clamp01(progress01);

        // Kare pozisyonu [0, n); son karede sabitlenir (n-1'i geçmez).
        float pos = Mathf.Min(p * n, n - 0.0001f);
        baseIdx = Mathf.Clamp(Mathf.FloorToInt(pos), 0, n - 1);
        float frac = pos - baseIdx;                       // 0..1 bu dilim içinde
        nextIdx = Mathf.Min(baseIdx + 1, n - 1);

        // Blend, dilimin son crackCrossfadePortion'ında lineer artar; sonra smoothstep ile yumuşatılır
        // (S-eğrisi → giriş/çıkış kenarları erir). cf=1 → kare boyunca sürekli morph. Son karede 0.
        float cf = Mathf.Clamp01(crackCrossfadePortion);
        blend = (cf <= 0f || frac <= (1f - cf)) ? 0f : (frac - (1f - cf)) / cf;
        blend = Smooth01(blend);
        if (nextIdx == baseIdx)
            blend = 0f;
    }

    private void UpdateCrackFireOverlay(Image fireImg, Image fireOverlayImg, float progress01, float elapsed)
    {
        if (fireImg == null)
            return;

        GetCrackFrameBlend(progress01, out int baseIdx, out int nextIdx, out float blend);
        Sprite baseFire = GetFireOverlaySprite(baseIdx);
        Sprite nextFire = GetFireOverlaySprite(nextIdx);

        if (baseFire != null && fireImg.sprite != baseFire)
            fireImg.sprite = baseFire;

        if (fireOverlayImg != null && nextFire != null && fireOverlayImg.sprite != nextFire)
            fireOverlayImg.sprite = nextFire;

        float start = GetCrackFireStartProgress();
        float full = Mathf.Max(start + 0.001f, crackFireFullProgress);
        float k = Smooth01(Mathf.Clamp01((Mathf.Clamp01(progress01) - start) / (full - start)));
        float frameBlend = Smooth01(Mathf.Clamp01(blend * crackFireFrameFadeOutBoost));

        ApplyCrackFireTransform(fireImg, k, elapsed);
        ApplyCrackFireTransform(fireOverlayImg, k, elapsed);

        float fade = GetCrackFireEndFade(progress01);

        float baseAlpha = baseFire != null ? (1f - frameBlend) : 0f;
        float overlayAlpha = nextFire != null ? frameBlend : 0f;

        fireImg.color = WithAlpha(crackFireColor, crackFireColor.a * k * fade * baseAlpha);
        if (fireOverlayImg != null)
            fireOverlayImg.color = WithAlpha(crackFireColor, crackFireColor.a * k * fade * overlayAlpha);
    }

    private void ApplyCrackFireTransform(Image fireImg, float intensity, float elapsed)
    {
        if (fireImg == null)
            return;

        RectTransform rt = fireImg.rectTransform;
        if (rt == null)
            return;

        rt.SetAsLastSibling();

        float pulse = Mathf.Sin(elapsed * crackFirePulseFrequency) * crackFirePulseAmplitude * intensity;
        float scale = Mathf.Lerp(crackFireStartScale, crackFirePeakScale, intensity) * (1f + pulse);
        rt.localScale = Vector3.one * scale;
    }

    private float GetCrackFireEndFade(float progress01)
    {
        if (progress01 <= crackFireFadeOutStartProgress)
            return 1f;

        float fadeK = Smooth01(Mathf.InverseLerp(crackFireFadeOutStartProgress, 1f, progress01));
        return Mathf.Lerp(1f, crackFireEndAlphaMultiplier, fadeK);
    }

    private bool HasCrackFireOverlay()
    {
        if (crackFireOverlaySprite != null)
            return true;

        if (crackFireOverlayFrames == null)
            return false;

        for (int i = 0; i < crackFireOverlayFrames.Length; i++)
        {
            if (crackFireOverlayFrames[i] != null)
                return true;
        }

        return false;
    }

    private Sprite GetInitialFireOverlaySprite()
    {
        if (crackFireOverlayFrames != null)
        {
            for (int i = 0; i < crackFireOverlayFrames.Length; i++)
            {
                if (crackFireOverlayFrames[i] != null)
                    return crackFireOverlayFrames[i];
            }
        }

        return crackFireOverlaySprite;
    }

    private Sprite GetFireOverlaySprite(int crackFrameIndex)
    {
        if (crackFireOverlayFrames != null &&
            crackFrameIndex >= 0 &&
            crackFrameIndex < crackFireOverlayFrames.Length &&
            crackFireOverlayFrames[crackFrameIndex] != null)
        {
            return crackFireOverlayFrames[crackFrameIndex];
        }

        int lastFrameIndex = crackFrames != null ? crackFrames.Length - 1 : -1;
        if (crackFrameIndex == lastFrameIndex)
            return crackFireOverlaySprite;

        return null;
    }

    private float GetCrackFireStartProgress()
    {
        if (!autoStartFireOnFinalCrackBlend || crackFrames == null || crackFrames.Length < 2)
            return crackFireStartProgress;

        int n = crackFrames.Length;
        int firstFireFrameIndex = GetFirstFireOverlayFrameIndex();
        if (firstFireFrameIndex >= 0)
            return Mathf.Clamp01((firstFireFrameIndex / (float)n) - crackFireLeadRatio);

        float cf = Mathf.Clamp01(crackCrossfadePortion);
        float finalBlendStart = ((n - 2) + (1f - cf)) / n;
        return Mathf.Clamp01(finalBlendStart - crackFireLeadRatio);
    }

    private float GetCrackFireBaseSize()
    {
        Sprite fireSprite = GetLastFireOverlaySprite();
        if (fireSprite == null)
            return bombBaseSize;

        Sprite finalFrame = null;
        if (crackFrames != null && crackFrames.Length > 0)
            finalFrame = crackFrames[crackFrames.Length - 1];

        if (finalFrame == null || finalFrame.rect.width <= 0f || finalFrame.rect.height <= 0f)
            return bombBaseSize;

        float widthRatio = fireSprite.rect.width / finalFrame.rect.width;
        float heightRatio = fireSprite.rect.height / finalFrame.rect.height;
        return bombBaseSize * Mathf.Max(widthRatio, heightRatio);
    }

    private Sprite GetLastFireOverlaySprite()
    {
        if (crackFireOverlayFrames != null)
        {
            for (int i = crackFireOverlayFrames.Length - 1; i >= 0; i--)
            {
                if (crackFireOverlayFrames[i] != null)
                    return crackFireOverlayFrames[i];
            }
        }

        return crackFireOverlaySprite;
    }

    private int GetFirstFireOverlayFrameIndex()
    {
        if (crackFireOverlayFrames == null)
            return -1;

        for (int i = 0; i < crackFireOverlayFrames.Length; i++)
        {
            if (crackFireOverlayFrames[i] != null)
                return i;
        }

        return -1;
    }

    private void UpdateGlow(Image glowImg, float totalElapsed, Image bombImg)
    {
        if (!glowImg) return;
        float u = Mathf.Clamp01(totalElapsed / chargeDuration);
        Color gc = Color.Lerp(glowColorStart, glowColorPeak, u * u);
        glowImg.color = gc;

        if (bombImg)
        {
            float flashU = Mathf.Clamp01((u - 0.75f) / 0.25f);
            bombImg.color = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.78f), flashU);
        }
    }

    private void UpdateGlowPeak(Image glowImg, float totalElapsed, Image bombImg, float peakK)
    {
        if (glowImg)
        {
            Color gc = glowColorPeak;
            // Peak hold'da hafif titreyen alfa
            float pulse = Mathf.Sin(totalElapsed * peakPulseFrequency) * 0.1f;
            gc.a = Mathf.Clamp01(gc.a + pulse);
            glowImg.color = gc;
        }

        if (bombImg)
        {
            // Peak'in son %40'ında beyaza doğru çok hafif yıkama
            float bleach = Mathf.Clamp01((peakK - 0.6f) / 0.4f) * 0.35f;
            bombImg.color = Color.Lerp(new Color(1f, 0.92f, 0.78f), Color.white, bleach);
        }
    }

    // ════════════════════════════════════════════════
    //  FUSE FLAME (Fitil Alevi & Kıvılcım)
    // ════════════════════════════════════════════════
    private IEnumerator CoEmitFuseSparks(RectTransform parentBombRt, float duration)
    {
        Sprite sprite = sparkSprite != null ? sparkSprite : TileClearBurstVfx.SoftCircleHaloSprite;
        Vector2 fusePos = new Vector2(bombBaseSize * fuseNormalizedOffset.x, bombBaseSize * fuseNormalizedOffset.y);
        float elapsed = 0f;

        while (parentBombRt != null && elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            // Şarj ilerledikçe alev yoğunluğu ve kıvılcım sayısı kademeli artar
            float intensity = Mathf.Lerp(1.3f, 3.2f, progress);
            int count = Mathf.Max(1, Mathf.RoundToInt(intensity));

            for (int i = 0; i < count; i++)
            {
                if (parentBombRt == null) yield break;
                SpawnChargeSpark(parentBombRt, fusePos, sprite, intensity, progress);
            }

            float wait = sparkEmitInterval / Mathf.Max(0.5f, intensity);
            elapsed += wait;
            yield return new WaitForSeconds(wait);
        }
    }

    private void SpawnChargeSpark(RectTransform parentBombRt, Vector2 basePos, Sprite sprite, float intensity, float chargeProgress)
    {
        if (parentBombRt == null) return;

        // Havuzdan: şarj boyunca ~0.018 sn'de bir kıvılcım doğuyor (bomba başına saniyede 55+);
        // PulseCore combo/zincirinde birkaç bomba aynı anda → new GameObject/Destroy fırtınası olmasın.
        var img = UiVfxPool.RentImage(UiFxPool.ChargeSparkKey, parentBombRt, "_FuseSpark");
        if (img == null) return;
        var sparkRt = img.rectTransform;
        if (sparkRt.GetSiblingIndex() != parentBombRt.childCount - 1) sparkRt.SetAsLastSibling();

        float sizeBoost = Mathf.Lerp(1f, 1.4f, chargeProgress);
        float size = Random.Range(sparkSizeMin, sparkSizeMax) * sizeBoost;
        sparkRt.sizeDelta = Vector2.one * size;
        sparkRt.anchoredPosition = basePos + Random.insideUnitCircle * (spreadRadius * sizeBoost);
        sparkRt.localScale = Vector3.one;

        img.enabled = true;
        img.color = Color.white;
        img.canvasRenderer.SetAlpha(1f);
        img.preserveAspect = false;
        img.sprite = sprite;
        img.raycastTarget = false;

        // Erken aşamada turuncu-kırmızı, tepe noktasında parlak akkor sarı-beyaz
        Color baseCol = Color.Lerp(sparkColorA, sparkColorB, Random.value);
        if (chargeProgress > 0.60f && Random.value < (chargeProgress - 0.60f) * 2f)
        {
            baseCol = Color.Lerp(baseCol, Color.white, 0.65f);
        }
        img.color = baseCol;

        UiFxPool.TrackChargeSpark(img, transform, sparkLifetime, size);
    }

    // ────────────────────────────────────────────────
    //  Utility
    // ────────────────────────────────────────────────
    private static void HideChildren(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++) parent.GetChild(i).gameObject.SetActive(false);
    }

    private RectTransform CreateContainer(RectTransform parent)
    {
        var go = chargeContainer != null ? chargeContainer.gameObject
            : new GameObject("ChargeContainer", typeof(RectTransform));
        go.SetActive(true);
        HideChildren(go.transform);
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private Image CreateUIImage(string name, RectTransform parent, Sprite sprite, float size)
    {
        var existing = parent.Find(name);
        var go = existing != null ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.SetActive(true);
        HideChildren(go.transform);
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        var img = go.GetComponent<Image>();
        img.enabled = true;
        img.color = Color.white;
        img.canvasRenderer.SetAlpha(1f);
        img.preserveAspect = false;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // Easing
    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    // Smoothstep: kenarları yumuşak S-eğrisi (0 ve 1 civarı yavaş). Çatlak çapraz geçişini yumuşatır.
    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
