using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PatchbotDashUI : MonoBehaviour
{
    private const string LaunchSparkPoolKey = "PatchbotDash.LaunchSpark";
    private const string ImpactSparkPoolKey = "PatchbotDash.ImpactSpark";
    private const string ImpactFlashOuterPoolKey = "PatchbotDash.ImpactFlashOuter";
    private const string ImpactFlashInnerPoolKey = "PatchbotDash.ImpactFlashInner";
    private const string AfterImagePoolKey = "PatchbotDash.AfterImage";

    private readonly List<(string key, GameObject go)> rentedVisuals = new(32);
    private readonly List<BoardAudioDirector.ClipLoop> flightLoops = new(2);
    private BoardAudioDirector.ClipLoop legacyFlightLoop;

    private const string SpinnerRingName = "PatchbotSpinnerRing";

    // Şu an inişe giden botların (ilk) hedef noktaları — aynı yere inenleri görsel olarak ayırmak için.
    private readonly List<Vector2> activeLandings = new();

    private GameObject RentVisual(string name, Transform parent, bool image = true)
    {
        string key = "PatchbotDash." + name;
        var go = image ? UiVfxPool.RentImage(key, parent, name)?.gameObject
                       : UiVfxPool.RentRect(key, parent, name);
        if (go != null) rentedVisuals.Add((key, go));
        return go;
    }

    private void ReleaseVisualTree(GameObject root)
    {
        if (root == null) return;
        for (int i = rentedVisuals.Count - 1; i >= 0; i--)
        {
            var item = rentedVisuals[i];
            if (item.go != null && item.go != root && !item.go.transform.IsChildOf(root.transform)) continue;
            rentedVisuals.RemoveAt(i);
            UiVfxPool.Return(item.key, item.go, 32);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        co = null;
        activeLandings.Clear();
        for (int i = flightLoops.Count - 1; i >= 0; i--) flightLoops[i]?.Dispose();
        flightLoops.Clear();
        legacyFlightLoop = null;
        for (int i = rentedVisuals.Count - 1; i >= 0; i--)
            UiVfxPool.Return(rentedVisuals[i].key, rentedVisuals[i].go, 32);
        rentedVisuals.Clear();
        UiFxPool.ReleaseGhosts(transform);
        UiFxPool.ReleasePatchbotSparks(transform);
    }

    private sealed class DashMotionState
    {
        public float elapsed;
        public float afterTimer;
    }

    // Dalış boyunca canlı hedef takibi. resolve her çağrıda hedefi mantıksal board
    // durumundan yeniden doğrular (ölmüşse koordinatör yeni hücre seçer); goal son
    // çözülen hedef, target ise drone'un o anda yöneldiği (yumuşatılmış) nokta.
    private sealed class LiveTargetState
    {
        public Vector2 target;
        public Vector2? goal;
        public System.Func<Vector2?> resolve;
        public float resolveTimer;
        public bool HasTarget => resolve == null || goal.HasValue;
    }

    [Header("Refs")]
    [SerializeField] private RectTransform boardContent;   // Tile'ların bulunduğu root (sadece test/path bulma için)
    [SerializeField] private RectTransform vfxRoot;        // VFXRoot (runner + afterimage burada)
    [SerializeField] private Image runnerImage;            // PatchbotRunner Image (template only)
    [SerializeField] private TileIconLibrary tileIcons;    // TileIconLibrary asset

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 100f;       // UI units per second (anchored space)
    [SerializeField] private float arriveEps = 2f;
    [SerializeField, Range(0.5f, 1.5f)] private float syncedDurationMultiplier = 1f;

    [Header("Drone Flight Feel")]
    [SerializeField, Min(0f)] private float takeoffBurstDuration = 0.12f;
    [SerializeField, Range(0f, 1.5f)] private float takeoffLateralFactor = 0.46f;
    [SerializeField, Range(0f, 2f)] private float takeoffLiftFactor = 0.78f;
    [SerializeField, Min(0f)] private float hoverHoldDuration = 0.035f;
    [SerializeField, Range(0f, 1f)] private float diveArcFactor = 0.11f;
    [Tooltip("Pike (dalış) hızı = dashSpeed * bu çarpan. Büyüdükçe hedefe daha hızlı/keskin pike. " +
             "Takeoff (2.5x büyüme) ve hover sabit kalır; yalnızca dalış hızlanır.")]
    [SerializeField, Range(0.5f, 4f)] private float pikeSpeedMultiplier = 2.4f;
    [Tooltip("Dalışın ivmelenme keskinliği. 1 = lineer, 2-2.5 = hedefe doğru hızlanan pike hissi.")]
    [SerializeField, Range(1f, 3f)] private float diveEaseInPower = 2.2f;

    [Header("Combo Flourish (PatchBot+PatchBot: yay + pike)")]
    [Tooltip("Tepe yüksekliği = mesafe × bu çarpan (en az 1.6 bot boyu). Büyüdükçe yay yükselir.")]
    [SerializeField, Range(0.1f, 1.5f)] private float flourishApexFactor = 0.55f;
    [Tooltip("Yana açılma = mesafe × bu çarpan (en az 1.2 bot boyu). Botlar sırayla sola/sağa açılır.")]
    [SerializeField, Range(0f, 1f)] private float flourishLateralFactor = 0.35f;
    [Tooltip("Tepede asılı kalma hissi. 0 = düz akış, 1 = tepede tam duraksama, sonra hızlanan pike.")]
    [SerializeField, Range(0f, 1f)] private float flourishApexHang = 0.55f;
    [Tooltip("Tepeye çıkarken ekstra büyüme (yakına gelme hissi).")]
    [SerializeField, Range(0f, 1.5f)] private float flourishApexScaleBoost = 0.45f;
    [Tooltip("Gösterili dalışın en kısa süresi (sn). Yakın hedefte yay göz kırpması gibi kalmasın. 0 = mesafe hızına bırak.")]
    [SerializeField, Range(0f, 1.2f)] private float flourishMinDiveDuration = 0.45f;
    [Tooltip("AÇIK: TÜM PatchBot uçuşları (solo, combo, Override+PatchBot) yay + pike çizer. KAPALI: yalnız PatchBot+PatchBot combo'su.")]
    [SerializeField] private bool flourishAllDashes = true;
    [Tooltip("Aynı noktaya inen botların iniş yeri bu kadar (× bot boyu) kaydırılır → ayrı uçuşlar görünür. YALNIZ GÖRSEL: hedef/hasar değişmez. 0 = kapalı.")]
    [SerializeField, Range(0f, 1f)] private float landingSpreadFactor = 0.45f;

    [Header("Live Retargeting")]
    [SerializeField, Min(0.01f)] private float liveRetargetInterval = 0.05f;   // hedef doğrulama sıklığı (sn)
    [SerializeField, Min(0.5f)] private float retargetSteerSpeed = 9f;         // yeni hedefe bank/kavis hızı
    [SerializeField, Min(0f)] private float maxRetargetHomingDuration = 0.6f;  // dalış bitince hedefe kilitli ek uçuş sınırı

    [Header("Flight Audio")]
    [SerializeField] private AudioClip flightLoopClip;
    [SerializeField, Min(0f)] private float flightLoopVolume = 0.18f;
    [SerializeField, Min(0f)] private float flightFadeIn = 0.015f;
    [SerializeField, Min(0f)] private float flightFadeOut = 0.04f;
    [SerializeField, Range(0.5f, 2f)] private float flightPitch = 1.12f;
    [SerializeField, Range(0f, 0.25f)] private float flightPitchJitter = 0.05f;
    [SerializeField, Range(0f, 0.15f)] private float flightVolumeJitter = 0.03f;

    [Header("AfterImage (hayalet dönme izi)")]
    [Tooltip("Ne sıklıkta hayalet iz doğar (sn). KÜÇÜK = daha çok hayalet (daha yoğun iz).")]
    [SerializeField] private float spawnEvery = 0.011f;
    [Tooltip("Her hayaletin yaşam süresi (sn). BÜYÜK = iz daha uzun/görünür kalır.")]
    [SerializeField] private float afterLife = 0.42f;
    [Tooltip("Hayalet rengi + alfa. Alfa BÜYÜK = daha belirgin iz.")]
    [SerializeField] private Color afterColor = new Color(0.60f, 0.88f, 1f, 0.95f);
    [Header("Carry Orbit")]
    [SerializeField] private float carrySizeFactor = 0.72f;
    [SerializeField, Range(0.05f, 0.6f)] private float carryOrbitRadiusFactor = 0.32f;

    [Header("Takeoff Propeller (dönüşümden ÖNCEKİ idle-tarzı spin)")]
    [Tooltip("Kalkışta (blade'e dönüşmeden önce) dönen pervane sprite'ı. BOŞ: board.PatchBotPropellerSprite kullanılır.")]
    [SerializeField] private Sprite takeoffPropellerSprite;
    [Tooltip("Kalkış pervanesinin frame'leri — idle tile ile AYNI olmalı (2+ sprite). Öncelik sırası: " +
             "bu alan → board.PatchBotPropellerFrames → idle tile'ın son bilinen frame'leri. Hepsi boşsa " +
             "eski transform-rotasyon spin'i (istenmeyen).")]
    [SerializeField] private Sprite[] takeoffPropellerFrames;
    [SerializeField, Min(1f)] private float takeoffPropellerFps = 18f;

    [Header("Blade Spinner (PatchBot Redesign)")]
    [Tooltip("Pervanenin dönüşeceği bıçaklı spinner frame'leri. Sırayla döngüye girer (verilen sıra: 4 → 33 → 11 → 22). " +
             "Boş bırakılırsa eski davranış (gövde+pervane hedefe uçar) korunur.")]
    [SerializeField] private Sprite[] spinnerFrames;
    [Tooltip("Frame değişim hızı (frame/sn). Yalnız 'Animate Frames While Spinning' açıkken veya " +
             "Spin Speed = 0 iken kullanılır (saf frame animasyonu).")]
    [SerializeField, Min(1f)] private float spinnerFps = 30f;
    [Tooltip("Sürekli transform dönüşü (derece/sn). Dönme bunu kullanır — kesintisiz, tam 360°, " +
             "frame'ler tam tur oluşturmasa bile boşluk olmaz. 0 = kapalı (o zaman frame döngüsü döner). " +
             "2880 = sn'de 8 tur. Daha da hızlı istersen artır.")]
    [SerializeField, Min(0f)] private float spinnerSpinSpeed = 2880f;
    [Tooltip("Transform dönerken frame'leri de değiştir (şimşek/glow parıltısı için). KAPALI önerilir: " +
             "frame'ler tam 360°'yi kapamıyorsa açıkken başa sarma sıçraması görünür. Kapalıyken tek frame " +
             "sürekli döner → tam 360°, boşluksuz.")]
    [SerializeField] private bool animateFramesWhileSpinning = false;
    [Tooltip("Frizbi eğimi (X): diski dikey olarak cos(açı) kadar KISALTIR (sabit elips foreshorten), " +
             "spinner bu elipsin İÇİNDE döner → uçarken de sabit kalan açılı disk. NOT: 15° yalnız ~%3 " +
             "kısalma (fark edilmez); belirgin frizbi için 30-45° dene. 0 = düz.")]
    [SerializeField, Range(0f, 60f)] private float spinnerTiltX = 30f;
    [Tooltip("Yatay eğim (Y): diski yatay olarak cos(açı) kadar kısaltır. Genelde 0 bırakılır.")]
    [SerializeField, Range(-60f, 60f)] private float spinnerTiltY = 0f;
    [Tooltip("Bıçağın arkasındaki ince hız halkası (ayrı görsel; bıçakla aynı eğimde, kendi hızında döner). BOŞ = halka yok.")]
    [SerializeField] private Sprite spinnerRingSprite;
    [Tooltip("Halkanın dönüş hızı (derece/sn). Bıçaktan yavaş olmalı ki kuyruğu okunsun.")]
    [SerializeField, Min(0f)] private float spinnerRingSpinSpeed = 540f;
    [Tooltip("Halka boyutu (spinner boyuna oran).")]
    [SerializeField, Range(0.5f, 1.6f)] private float spinnerRingScale = 1.08f;
    [Tooltip("Halka opaklığı.")]
    [SerializeField, Range(0f, 1f)] private float spinnerRingAlpha = 0.9f;
    [Tooltip("Gövde ayrılma + pervane→spinner cross-fade süresi (sn).")]
    [SerializeField, Min(0.01f)] private float separationDuration = 0.16f;
    [Tooltip("Ayrılma sırasında pervanenin yukarı çıkış miktarı (tile boyutuna oran).")]
    [SerializeField, Range(0f, 1.5f)] private float propellerRiseFactor = 0.35f;

    [Header("Body Retreat (Güvenli Bölge)")]
    [Tooltip("Ayrışan robot GÖVDESİ sprite'ı (blade üstte dönerken TopHUD'a hayalet olup giden alt " +
             "robot). BOŞ bırakılırsa uçuş ikonunun kendisi kullanılır (eski davranış). Kendi robot " +
             "gövde çizimini buraya bağla → retreat onunla oynar.")]
    [SerializeField] private Sprite bodyRetreatSprite;
    [Tooltip("Gövdenin HAYALET olup uçacağı güvenli nokta. BOŞ bırak: runtime'da TopHUD avatarını " +
             "(AvatarView) otomatik bulur → gövde ona doğru gider (prefab sahne objesine referans " +
             "tutamadığı için bu yol tercih edilir). Elle override etmek istersen bir RectTransform bağla.")]
    [SerializeField] private RectTransform bodySafeZone;
    [SerializeField, Min(0.05f)] private float bodyRetreatDuration = 0.55f;
    [Tooltip("bodySafeZone boşsa gövdenin yukarı offset miktarı (tile boyutuna oran). Büyük = daha net çıkış.")]
    [SerializeField, Range(0f, 6f)] private float bodyRetreatRiseFactor = 3f;
    [SerializeField, Range(0.1f, 1f)] private float bodyRetreatEndScale = 0.4f;
    [Tooltip("Retreat eden gövdenin HAYALET rengi/alfası (mavimsi yarı-saydam = hayalet hissi).")]
    [SerializeField] private Color bodyGhostTint = new Color(0.62f, 0.82f, 1f, 0.8f);

    [Header("Launch Sparks (kalkışta etrafa roket/kıvılcım saçma)")]
    [Tooltip("Kalkış anında spinner'ın etrafına saçılan kıvılcım/roket adedi. 0 = kapalı.")]
    [SerializeField, Min(0)] private int launchSparkCount = 16;
    [SerializeField] private Color launchSparkColor = new Color(1f, 0.80f, 0.32f, 1f);
    [Tooltip("Kıvılcımların uçuş mesafesi (tile boyutuna oran).")]
    [SerializeField, Range(0.3f, 3f)] private float launchSparkDistanceFactor = 1.5f;
    [SerializeField, Min(0.05f)] private float launchSparkLife = 0.42f;

    [Header("Impact Sparks (çarpmada etrafa kıvılcım saçma)")]
    [Tooltip("Hedefe çarpma anında etrafa saçılan kıvılcım adedi. 0 = kapalı.")]
    [SerializeField, Min(0)] private int impactSparkCount = 36;
    [Tooltip("Özel çarpışma kıvılcımı sprite'ı. Boş bırakılırsa yönlü eliptik parıltı sprite'ı kullanılır.")]
    [SerializeField] private Sprite impactSparkSprite;
    [Tooltip("Kıvılcımların fırlama mesafesi (tile boyutuna oran).")]
    [SerializeField, Range(0.3f, 4f)] private float impactSparkDistanceFactor = 2.1f;
    [Tooltip("Mevcut distance değerini koruyup çarpma kıvılcımlarını daha dar bir daireye toplar. 1 = eski yayılım.")]
    [SerializeField, Range(0.35f, 1f)] private float impactSparkSpreadScale = 0.62f;
    [Tooltip("Çarpma kıvılcımı/parıltı parçalarının boyut çarpanı.")]
    [SerializeField, Range(0.75f, 1.8f)] private float impactSparkSizeMultiplier = 1.38f;
    [SerializeField, Min(0.05f)] private float impactSparkLife = 0.48f;
    [Tooltip("Çarpma anında anlık parıldama patlaması (flash) çıksın mı?")]
    [SerializeField] private bool impactFlashEnabled = true;

    private Coroutine co;

    private void Awake()
    {
        if (impactSparkCount <= 0) impactSparkCount = 36;
        if (impactSparkDistanceFactor <= 0.05f) impactSparkDistanceFactor = 2.1f;
        if (impactSparkSpreadScale <= 0.05f) impactSparkSpreadScale = 0.62f;
        if (impactSparkSizeMultiplier <= 0.05f) impactSparkSizeMultiplier = 1.38f;
        if (impactSparkLife <= 0.05f) impactSparkLife = 0.48f;
        impactFlashEnabled = true;
    }

    void Reset()
    {
        runnerImage = GetComponent<Image>();
    }

    // Coroutine host'unun (bu obje) activeInHierarchy olması şart. Kendini açmak yetmez;
    // bir ÜST parent kapalıysa activeInHierarchy false kalır → StartCoroutine patlar
    // ("game object 'PatchbotRunner' is inactive"). Tüm ata zincirini aktif et.
    private void EnsureHierarchyActive()
    {
        for (Transform t = transform; t != null; t = t.parent)
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
    }

    public void PlayDash(List<RectTransform> pathTiles)
    {
        EnsureHierarchyActive();

        if (co != null) StopCoroutine(co);
        ReleaseFlightAudio(legacyFlightLoop);
        legacyFlightLoop = null;
        co = StartCoroutine(DashRoutine(pathTiles));
    }

    // ADDITIVE (2026-08-16): yeni parti öncekini İPTAL ETMEZ.
    //
    // ESKİ DAVRANIŞ HATALIYDI: `if (co != null) StopCoroutine(co)` çalışan parti rutinini öldürüyordu.
    // Çocuk SingleDashRoutine'ler ayrı coroutine olduğu için ölmüyor, AMA parti döngüsü yarıda kesildiği
    // için KALAN istekler hiç başlatılmıyordu → (a) "botların bir kısmı havalanmadı", (b) o istekler için
    // `BeginPatchBotDashFlight()` kuyruğa girerken ÇAĞRILMIŞ ama karşılığı `EndPatchBotDashFlight()`
    // yalnız onArrived'ın finally'sinde olduğundan JOB SIZDI → level-end ActiveBackgroundJobs'ı bekledi →
    // "win/lose popup çok geç çıktı". İki semptom da tek kökten; yarış-koşulu olduğu için aralıklıydı.
    //
    // İptalin meşru bir amacı yok: her dash kendi "PatchbotRunnerInstance" GameObject'ini yaratır, partiler
    // görsel olarak bağımsızdır. Bu yüzden partiler artık yan yana koşar (üst üste pompa GÜVENLİ hale gelir;
    // Faz 3'te clear'ı non-blocking yapmanın da ön koşuluydu).
    public Coroutine PlayDashParallel(List<BoardController.PatchbotDashRequest> requests, BoardController board, float syncDuration = -1f)
    {
        EnsureHierarchyActive();

        var requestCopy = requests != null
            ? new List<BoardController.PatchbotDashRequest>(requests)
            : null;

        return StartCoroutine(DashParallelRoutine(requestCopy, board, syncDuration));
    }

    private IEnumerator DashParallelRoutine(List<BoardController.PatchbotDashRequest> requests, BoardController board, float syncDuration)
    {
        if (vfxRoot == null || board == null) yield break;
        if (requests == null || requests.Count == 0) yield break;

        if (runnerImage != null) runnerImage.enabled = false;

        Sprite patchbotSprite = null;
        if (runnerImage != null && runnerImage.sprite != null)
            patchbotSprite = runnerImage.sprite;
        else if (tileIcons != null)
            patchbotSprite = tileIcons.GetPatchBotFlightIcon();

        const float stagger = 0.02f;
        int remaining = 0;

        for (int i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            remaining++;

            StartCoroutine(SingleDashRoutine(req, board, patchbotSprite, syncDuration, () => remaining--));

            if (stagger > 0f)
                yield return new WaitForSeconds(stagger);
        }

        while (remaining > 0)
            yield return null;

        // NOT: `co` alanına DOKUNULMAZ — o yalnız legacy PlayDash (tek-yol) API'sine ait. Parti
        // rutinleri artık bağımsız koştuğu için paylaşılan bir "aktif rutin" göstergesi tutmuyoruz.
    }

    private IEnumerator SingleDashRoutine(
       BoardController.PatchbotDashRequest req,
       BoardController board,
       Sprite sprite,
       float syncDuration,
       System.Action onComplete)
    {
        req.onStart?.Invoke();

        var go = RentVisual("PatchbotRunnerInstance", vfxRoot);
        if (go == null) { onComplete?.Invoke(); yield break; }
        go.transform.SetParent(vfxRoot, false);

        BoardAudioDirector.ClipLoop flightSource = null;
        Coroutine spinCycler = null;
        Vector2 landingOffset = Vector2.zero, landingKey = Vector2.zero;
        bool landingRegistered = false;
        try
        {
            var img = go.GetComponent<Image>();
            var rt = (RectTransform)go.transform;

            img.sprite = sprite;
            img.raycastTarget = false;
            img.enabled = true;
            img.color = Color.white;

            rt.SetAsLastSibling();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Vector2 size = new Vector2(90f, 90f);
            if (runnerImage != null &&
                runnerImage.rectTransform != null &&
                runnerImage.rectTransform.sizeDelta.sqrMagnitude > 1f)
            {
                size = runnerImage.rectTransform.sizeDelta;
            }
            rt.sizeDelta = size;

            GameObject propGo = null;
            Image propImg = null;
            PatchBotPropellerView propView = null;

            // Kalkış pervanesi sprite'ı: PatchbotDashUI local öncelikli, yoksa board.
            var propellerSprite = takeoffPropellerSprite != null ? takeoffPropellerSprite : board.PatchBotPropellerSprite;
            if (propellerSprite != null)
            {
                propGo = RentVisual("PatchBotPropeller", rt);
                propGo.transform.SetParent(rt, false);

                var propRt = propGo.GetComponent<RectTransform>();
                propRt.anchorMin = new Vector2(0.5f, 0.5f);
                propRt.anchorMax = new Vector2(0.5f, 0.5f);
                propRt.pivot = new Vector2(0.5f, 0.5f);
                propRt.sizeDelta = size;
                propRt.anchoredPosition = Vector2.zero;

                propImg = propGo.GetComponent<Image>();
                propImg.sprite = propellerSprite;
                propImg.preserveAspect = true;
                propImg.raycastTarget = false;
                propImg.color = Color.white;

                propView = propGo.GetComponent<PatchBotPropellerView>();
                if (propView == null) propView = propGo.AddComponent<PatchBotPropellerView>();
                // Uçan pervane de tile'daki gibi frame animasyonu kullansın (rotasyon değil).
                // Öncelik: PatchbotDashUI local → board.PatchBotPropellerFrames → idle tile'ın son bilinen
                // frame'leri (elle eşleme gerekmez → takeoff spin'i idle ile aynı olur). Hepsi boşsa rotasyon.
                Sprite[] propFrames;
                float propFps;
                if (takeoffPropellerFrames != null && takeoffPropellerFrames.Length >= 2)
                {
                    propFrames = takeoffPropellerFrames;
                    propFps = takeoffPropellerFps;
                }
                else if (board.PatchBotPropellerFrames != null && board.PatchBotPropellerFrames.Length >= 2)
                {
                    propFrames = board.PatchBotPropellerFrames;
                    propFps = board.PatchBotPropellerFrameFps;
                }
                else
                {
                    propFrames = PatchBotPropellerView.LastKnownSpinFrames;
                    propFps = PatchBotPropellerView.LastKnownSpinFrameFps > 0f
                        ? PatchBotPropellerView.LastKnownSpinFrameFps
                        : takeoffPropellerFps;
                }

                propView.SetSpinFrames(propFrames, propFps);
                propView.StartActivationSpin(5400f);
            }

            RectTransform carryRt = null;
            Image carryImg = null;

            if (req.orbitCarry && req.carriedSprite != null)
            {
                var carryGo = RentVisual("PatchbotCarrySpecial", rt);
                carryGo.transform.SetParent(rt, false);

                carryRt = carryGo.GetComponent<RectTransform>();
                carryImg = carryGo.GetComponent<Image>();

                carryRt.anchorMin = new Vector2(0.5f, 0.5f);
                carryRt.anchorMax = new Vector2(0.5f, 0.5f);
                carryRt.pivot = new Vector2(0.5f, 0.5f);
                carryRt.sizeDelta = size * carrySizeFactor;
                carryRt.anchoredPosition = Vector2.down * (Mathf.Min(size.x, size.y) * carryOrbitRadiusFactor);

                carryImg.sprite = req.carriedSprite;
                carryImg.preserveAspect = true;
                carryImg.raycastTarget = false;
                carryImg.color = Color.white;

                carryRt.SetAsLastSibling();
            }

            flightSource = CreateFlightAudioSource(board);
            StartFlightAudio(flightSource);

            Vector3 fromWorld = board.GetCellWorldPosition(req.from.x, req.from.y);
            Vector3 toWorld = AimWorldPosition(board, req.to.x, req.to.y);

            Vector2 start = WorldToAnchoredIn(vfxRoot, fromWorld);
            Vector2 target = WorldToAnchoredIn(vfxRoot, toWorld);
            rt.anchoredPosition = start;

            float totalDistance = Vector2.Distance(start, target);
            float effectiveSpeed = Mathf.Max(1f, dashSpeed * Mathf.Max(0.01f, pikeSpeedMultiplier));

            // Blade spinner YALNIZ solo patchbot'a özel. Special TAŞIYAN (combo) patchbot blade'e
            // dönüşmez — pervane olarak kalır (kullanıcı kararı). Taşımada gövde-retreat da yok.
            bool useSpinner = spinnerFrames != null && spinnerFrames.Length > 0 && !req.orbitCarry;

            float takeoffDuration;
            float hoverDuration;
            float separationPhase;
            float diveDuration;

            if (syncDuration > 0f)
            {
                // Zincir/sync modunda toplam süre dışarıdan veriliyor; fazlara böl.
                float travelDuration = Mathf.Max(0f, syncDuration * Mathf.Max(0.01f, syncedDurationMultiplier));
                takeoffDuration = Mathf.Min(Mathf.Max(0f, takeoffBurstDuration), Mathf.Max(0f, travelDuration * 0.35f));
                hoverDuration = Mathf.Min(Mathf.Max(0f, hoverHoldDuration), Mathf.Max(0f, travelDuration * 0.18f));
                separationPhase = useSpinner ? Mathf.Min(Mathf.Max(0f, separationDuration), Mathf.Max(0f, travelDuration * 0.25f)) : 0f;
                diveDuration = Mathf.Max(0.01f, travelDuration - takeoffDuration - hoverDuration - separationPhase);
            }
            else
            {
                // Solo dash: takeoff (2.5x şarj) ve hover SABİT; dalış pike hızıyla kısa ve keskin.
                takeoffDuration = Mathf.Max(0f, takeoffBurstDuration);
                hoverDuration = Mathf.Max(0f, hoverHoldDuration);
                separationPhase = useSpinner ? Mathf.Max(0f, separationDuration) : 0f;
                diveDuration = totalDistance > arriveEps ? Mathf.Max(0.05f, totalDistance / effectiveSpeed) : 0.01f;
            }

            bool flourishOn = req.diveFlourish || flourishAllDashes;
            // Alt sınır yalnız serbest uçuşta; zincir/sync modunda toplam süre dışarıdan sabittir (bozulmasın).
            if (flourishOn && syncDuration <= 0f)
                diveDuration = Mathf.Max(diveDuration, flourishMinDiveDuration);

            int side = ((req.from.x + req.from.y + req.to.x + req.to.y) & 1) == 0 ? -1 : 1;
            Vector2 takeoff = start + new Vector2(
                side * Mathf.Min(size.x, size.y) * takeoffLateralFactor,
                Mathf.Min(size.x, size.y) * takeoffLiftFactor);

            if (board != null && board.BoardFlowTraceEnabled)
                {}

            var motion = new DashMotionState();
            yield return RunTakeoffBurst(rt, carryRt, size, sprite, start, takeoff, takeoffDuration, motion);
            yield return RunHoverHold(rt, carryRt, size, sprite, takeoff, hoverDuration, motion);

            // Ayrılma: gövde güvenli bölgeye süzülürken pervane bıçaklı spinner'a dönüşür.
            Image spinnerImg = null;
            if (useSpinner)
                yield return RunSeparation(rt, img, sprite, size, propGo, propImg, propView, separationPhase, motion, s => spinnerImg = s);

            var live = AcquireLiveTarget(req, board, target);

            // Spinner uçuş boyunca frame'lerini hızlıca döngüler (pervane gibi dönme).
            spinCycler = spinnerImg != null ? StartCoroutine(SpinnerFrameCycler(spinnerImg)) : null;

            // Yay yüksekliği botlara göre 1.0 / 1.15 / 1.3 (aynı anda kalkanlar üst üste binmesin).
            float flourishLane = flourishOn ? 1f + 0.15f * (((req.from.x + req.from.y * 3) % 3 + 3) % 3) : 0f;

            // Aynı noktaya inen başka bot varsa iniş yerini çevresine kaydır (yalnız görsel; hasar hedef hücrede).
            landingOffset = Vector2.zero;
            if (live.HasTarget && landingSpreadFactor > 0f)
            {
                float near = Mathf.Min(size.x, size.y) * 0.6f;
                int crowd = 0;
                for (int i = 0; i < activeLandings.Count; i++)
                    if ((activeLandings[i] - live.target).sqrMagnitude < near * near) crowd++;
                if (crowd > 0)
                {
                    float ang = (crowd * 137.5f + (side < 0 ? 0f : 60f)) * Mathf.Deg2Rad;
                    landingOffset = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (Mathf.Min(size.x, size.y) * landingSpreadFactor);
                }
                landingKey = live.target;
                activeLandings.Add(landingKey);
                landingRegistered = true;
            }

            yield return RunDive(rt, carryRt, size, sprite, rt.anchoredPosition, live, diveDuration, effectiveSpeed, motion, spinnerImg,
                flourishLane, side, landingOffset);

            if (spinCycler != null) StopCoroutine(spinCycler);

            if (live.HasTarget) rt.anchoredPosition = live.target + landingOffset;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            if (carryRt != null)
                carryRt.localRotation = Quaternion.identity;

            if (live.HasTarget) SpawnImpactSparks(live.target + landingOffset, size, board, req.to);

            req.onArrived?.Invoke();
            yield return StopFlightAudioRoutine(flightSource);

        }
        finally
        {
            if (spinCycler != null) StopCoroutine(spinCycler);
            if (landingRegistered) activeLandings.Remove(landingKey);
            ReleaseFlightAudio(flightSource);
            ReleaseVisualTree(go);
            onComplete?.Invoke();
        }
    }

    // Görsel varış noktası. Hedef hücre çok-hücreli bir obstacle'a aitse (Safe NxN gibi)
    // drone footprint'in dünya-merkezine iner; hasar yine mantıksal hedef hücreye işler.
    // Magnet uçları, tüp tabanı ve duvarın seçilen zayıf hücresi doğrudan hedeflenir;
    // dolu dikdörtgen olsalar bile parçanın merkezine inilmez. Diğer ayrık/delikli
    // footprint'lerde de hedef hücreye inilir.
    internal static Vector3 AimWorldPosition(BoardController board, int x, int y)
    {
        var obstacleService = board.ObstacleStateService;
        if (obstacleService != null)
        {
            var id = obstacleService.GetObstacleIdAt(x, y);
            if (id == ObstacleId.Magnet || id == ObstacleId.Tube
                || id == ObstacleId.Wall || id == ObstacleId.MetalWall)
                return board.GetCellWorldPosition(x, y);
        }
        int origin = obstacleService != null ? obstacleService.GetObstacleOriginAt(x, y) : -1;
        if (origin < 0)
            return board.GetCellWorldPosition(x, y);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        int count = 0;

        // Yalnız origin üyeliğine bak — hasar/hit filtresi YOK. Interceptor-yönetimli
        // obstacle'larda (Safe: kilit durumu SafeObstacleService'te) jenerik hit sorguları
        // hücreleri elediğinden merkez hesaplanamıyor, köşeye dönülüyordu. Görsel varış
        // noktası için footprint yeterli; hasar mantığı zaten hedef hücrede kalıyor.
        for (int cx = 0; cx < board.Width; cx++)
            for (int cy = 0; cy < board.Height; cy++)
            {
                if (obstacleService.GetObstacleOriginAt(cx, cy) != origin)
                    continue;

                minX = Mathf.Min(minX, cx); maxX = Mathf.Max(maxX, cx);
                minY = Mathf.Min(minY, cy); maxY = Mathf.Max(maxY, cy);
                count++;
            }

        if (count <= 1 || count != (maxX - minX + 1) * (maxY - minY + 1))
            return board.GetCellWorldPosition(x, y);

        Vector3 a = board.GetCellWorldPosition(minX, minY);
        Vector3 b = board.GetCellWorldPosition(maxX, maxY);
        return (a + b) * 0.5f;
    }

    // Dive başında bu dash'ın canlı çözücüsünü teslim alır ve ilk hedefi çözer.
    // Çözücü yoksa (sabit hedefli eski dash'lar) davranış birebir eski hâli: statik hedef.
    private LiveTargetState AcquireLiveTarget(BoardController.PatchbotDashRequest req, BoardController board, Vector2 fallbackTarget)
    {
        var live = new LiveTargetState { target = fallbackTarget };

        if (board == null || vfxRoot == null)
            return live;

        if (!PatchbotLiveDashTargetRegistry.TryAcquireLiveResolver(req.from, req.to, out var cellResolver))
            return live;

        live.resolve = () =>
        {
            var maybeCell = cellResolver();
            if (!maybeCell.HasValue)
                return null;

            var cell = maybeCell.Value;
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                return null;

            return WorldToAnchoredIn(vfxRoot, AimWorldPosition(board, cell.x, cell.y));
        };

        var initial = live.resolve();
        if (initial.HasValue)
        {
            live.target = initial.Value;
            live.goal = initial.Value;
        }

        return live;
    }

    // Hedefi periyodik yeniden doğrular; hedef değiştiyse drone'un yöneldiği noktayı
    // exponential smoothing ile yeni hedefe kaydırır (havada dönüş/bank hissi).
    private void TickLiveRetarget(LiveTargetState live, float dt)
    {
        if (live.resolve == null)
            return;

        live.resolveTimer += dt;
        if (live.resolveTimer >= liveRetargetInterval)
        {
            live.resolveTimer = 0f;
            var desired = live.resolve();
            // Null invalidates the previous cell; it must not keep attracting the bot.
            live.goal = desired;
        }

        if (!live.goal.HasValue || live.goal.Value == live.target)
            return;

        float blend = 1f - Mathf.Exp(-retargetSteerSpeed * dt);
        live.target = Vector2.Lerp(live.target, live.goal.Value, blend);

        if ((live.target - live.goal.Value).sqrMagnitude <= 1f)
            live.target = live.goal.Value;
    }

    private IEnumerator RunTakeoffBurst(
        RectTransform rt,
        RectTransform carryRt,
        Vector2 size,
        Sprite sprite,
        Vector2 start,
        Vector2 takeoff,
        float duration,
        DashMotionState motion)
    {
        // Kalkış anı: spinner yerinde revlerken etrafına roket/kıvılcım demeti saçar.
        SpawnLaunchSparks(start, size);
        Haptics.OnComboMoment(Haptics.ComboMoment.PatchBotTakeoff);   // yalnız kombo oturumunda titrer

        if (duration <= 0f)
        {
            rt.anchoredPosition = takeoff;
            yield break;
        }

        float local = 0f;
        while (local < duration)
        {
            float dt = Time.deltaTime;
            local += dt;
            motion.elapsed += dt;

            float t = Mathf.Clamp01(local / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float punch = Mathf.Sin(t * Mathf.PI) * Mathf.Min(size.x, size.y) * 0.08f;

            rt.anchoredPosition = Vector2.LerpUnclamped(start, takeoff, eased) + Vector2.up * punch;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 2.5f, eased);

            UpdateCarryOrbit(carryRt, size, motion.elapsed);
            TickAfterImage(rt, sprite, motion);
            yield return null;
        }
    }

    private IEnumerator RunHoverHold(
        RectTransform rt,
        RectTransform carryRt,
        Vector2 size,
        Sprite sprite,
        Vector2 hover,
        float duration,
        DashMotionState motion)
    {
        if (duration <= 0f)
            yield break;

        float local = 0f;
        while (local < duration)
        {
            float dt = Time.deltaTime;
            local += dt;
            motion.elapsed += dt;

            float wobbleX = Mathf.Sin(motion.elapsed * 8.5f) * Mathf.Min(size.x, size.y) * 0.035f;
            float wobbleY = Mathf.Sin(motion.elapsed * 10.0f) * Mathf.Min(size.x, size.y) * 0.025f;

            rt.anchoredPosition = hover + new Vector2(wobbleX, wobbleY);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * 2.5f;

            UpdateCarryOrbit(carryRt, size, motion.elapsed);
            TickAfterImage(rt, sprite, motion);
            yield return null;
        }
    }

    private IEnumerator RunDive(
        RectTransform rt,
        RectTransform carryRt,
        Vector2 size,
        Sprite sprite,
        Vector2 start,
        LiveTargetState live,
        float duration,
        float homingSpeed,
        DashMotionState motion,
        Image spinnerImg = null,
        float flourishLane = 0f,
        int flourishSide = 1,
        Vector2 landingOffset = default)
    {
        if (!live.HasTarget) yield break;
        Vector2 initialDelta = live.target - start;
        float arc = Mathf.Clamp(initialDelta.magnitude * Mathf.Max(0f, diveArcFactor), Mathf.Min(size.x, size.y) * 0.10f, Mathf.Min(size.x, size.y) * 0.45f);

        // Combo gösterisi (flourishLane > 0): kübik Bezier — yana-yukarı açılış (P1), hedefin TAM
        // üstü (P2) → son bölüm dikey pike. P3 canlı hedeftir (retarget'i izler). Süre çağıranda
        // (flourishMinDiveDuration alt sınırı dışında) değişmez. Kontrol noktaları VFX alanında tutulur.
        bool flourish = flourishLane > 0f;
        float botSize = Mathf.Min(size.x, size.y);
        float dist = initialDelta.magnitude;
        float apex = Mathf.Max(botSize * 1.6f, dist * flourishApexFactor) * flourishLane;
        float lateral = Mathf.Max(botSize * 1.2f, dist * flourishLateralFactor) * (flourishSide < 0 ? -1f : 1f);
        Rect area = vfxRoot != null ? vfxRoot.rect : new Rect(-1e5f, -1e5f, 2e5f, 2e5f);
        float maxY = area.yMax - botSize * 1.5f;
        Vector2 p1 = start + new Vector2(lateral, apex);
        p1.x = Mathf.Clamp(p1.x, area.xMin + botSize, area.xMax - botSize);
        p1.y = Mathf.Min(p1.y, maxY);

        float local = 0f;
        while (local < duration)
        {
            float dt = Time.deltaTime;
            local += dt;
            motion.elapsed += dt;

            // Hedef uçuş sırasında ölmüş olabilir — her tick mantıksal board'dan
            // doğrula; değiştiyse target yumuşakça yeni hücreye kayar (kavisli dönüş).
            TickLiveRetarget(live, dt);
            if (!live.HasTarget) yield break;

            float t = Mathf.Clamp01(local / duration);

            if (flourish)
            {
                // Tırmanış yavaşlar → tepede kısa asılı kalma → hızlanan pike (orta noktada hız ~0).
                float hang = t < 0.5f
                    ? 0.5f * (1f - (1f - 2f * t) * (1f - 2f * t))
                    : 0.5f + 0.5f * (2f * t - 1f) * (2f * t - 1f);
                float s = Mathf.Lerp(t, hang, flourishApexHang);
                Vector2 p3 = live.target + landingOffset;
                Vector2 p2 = new Vector2(p3.x, Mathf.Max(p3.y, Mathf.Min(p3.y + apex, maxY)));
                float u = 1f - s;
                rt.anchoredPosition = u * u * u * start + 3f * u * u * s * p1 + 3f * u * s * s * p2 + s * s * s * p3;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one * (Mathf.Lerp(2.5f, 1.0f, t) + flourishApexScaleBoost * Mathf.Sin(t * Mathf.PI));

                UpdateCarryOrbit(carryRt, size, motion.elapsed);
                TickAfterImage(rt, spinnerImg != null ? spinnerImg.sprite : sprite, motion);
                yield return null;
                continue;
            }

            // Ease-IN: hedefe doğru ivmelenen pike (smoothstep yerine).
            float eased = Mathf.Pow(t, Mathf.Max(1f, diveEaseInPower));
            float curve = Mathf.Sin(t * Mathf.PI) * arc;
            float snap = Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Min(size.x, size.y) * 0.025f;

            Vector2 delta = live.target - start;
            Vector2 normal = delta.sqrMagnitude > 0.001f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;

            rt.anchoredPosition = Vector2.LerpUnclamped(start, live.target + landingOffset, eased) + normal * (curve + snap);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * Mathf.Lerp(2.5f, 1.0f, t);

            UpdateCarryOrbit(carryRt, size, motion.elapsed);
            TickAfterImage(rt, spinnerImg != null ? spinnerImg.sprite : sprite, motion);
            yield return null;
        }

        // Hedef dalış sırasında değiştiyse süre dolduğunda hâlâ uzakta olabiliriz —
        // canlı hedefe kilitli düz uçuşla tamamla (süre sınırlı; hedef yine ölürse
        // geçerli hedef kalmazsa dalış iptal edilir).
        float homingElapsed = 0f;
        while ((rt.anchoredPosition - (live.target + landingOffset)).magnitude > arriveEps && homingElapsed < maxRetargetHomingDuration)
        {
            float dt = Time.deltaTime;
            homingElapsed += dt;
            motion.elapsed += dt;

            TickLiveRetarget(live, dt);
            if (!live.HasTarget) yield break;

            rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, live.target + landingOffset, Mathf.Max(1f, homingSpeed) * dt);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            UpdateCarryOrbit(carryRt, size, motion.elapsed);
            TickAfterImage(rt, spinnerImg != null ? spinnerImg.sprite : sprite, motion);
            yield return null;
        }
    }

    // Ayrılma fazı: gövde pervaneden kopar (güvenli bölgeye paralel süzülür) ve pervane
    // görseli bıçaklı spinner'a cross-fade ile dönüşür. Bittiğinde spinner Image'ı callback
    // ile teslim edilir; dalış artık bu spinner'ı taşır.
    private IEnumerator RunSeparation(
        RectTransform rt,
        Image bodyImg,
        Sprite bodySprite,
        Vector2 size,
        GameObject propGo,
        Image propImg,
        PatchBotPropellerView propView,
        float duration,
        DashMotionState motion,
        System.Action<Image> onSpinnerReady)
    {
        // Frizbi tilt-holder: DÖNMEYEN parent, sabit dikey/yatay squash uygular (foreshorten).
        // Spinner bunun İÇİNDE Z'de döner → disk sabit bir elips olur, bıçaklar içinde döner
        // (elips titremez). Uçarken de eğim görünür kalır. squash = cos(tilt).
        var tiltGo = RentVisual("PatchbotSpinnerTilt", rt, false);
        tiltGo.transform.SetParent(rt, false);
        var tiltRt = tiltGo.GetComponent<RectTransform>();
        tiltRt.anchorMin = tiltRt.anchorMax = tiltRt.pivot = new Vector2(0.5f, 0.5f);
        tiltRt.sizeDelta = size;
        tiltRt.anchoredPosition = Vector2.zero;
        tiltRt.localScale = new Vector3(
            Mathf.Cos(spinnerTiltY * Mathf.Deg2Rad),   // Y-tilt → yatay squash
            Mathf.Cos(spinnerTiltX * Mathf.Deg2Rad),   // X-tilt → dikey squash (frizbi)
            1f);
        tiltRt.SetAsLastSibling();

        // İnce hız halkası: eğim kabında bıçağın ARKASINDA (önce eklenir), alfa 0'dan başlar.
        Image ringImg = null;
        if (spinnerRingSprite != null)
        {
            var ringGo = RentVisual(SpinnerRingName, tiltGo.transform);
            ringGo.transform.SetParent(tiltGo.transform, false);
            var ringRt = ringGo.GetComponent<RectTransform>();
            ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = size * spinnerRingScale;
            ringRt.anchoredPosition = Vector2.zero;
            ringRt.localRotation = Quaternion.identity;
            ringRt.localScale = Vector3.one;
            ringImg = ringGo.GetComponent<Image>();
            ringImg.sprite = spinnerRingSprite;
            ringImg.preserveAspect = true;
            ringImg.raycastTarget = false;
            ringImg.color = new Color(1f, 1f, 1f, 0f);
            ringRt.SetAsLastSibling();
        }

        // Bıçaklı spinner overlay'i (pervanenin dönüşeceği yapı), alfa 0'dan başlar.
        var spinGo = RentVisual("PatchbotBladeSpinner", tiltGo.transform);
        spinGo.transform.SetParent(tiltGo.transform, false);

        var spinRt = spinGo.GetComponent<RectTransform>();
        spinRt.anchorMin = new Vector2(0.5f, 0.5f);
        spinRt.anchorMax = new Vector2(0.5f, 0.5f);
        spinRt.pivot = new Vector2(0.5f, 0.5f);
        spinRt.sizeDelta = size;
        spinRt.anchoredPosition = Vector2.zero;

        var spinImg = spinGo.GetComponent<Image>();
        spinImg.sprite = spinnerFrames[0];
        spinImg.preserveAspect = true;
        spinImg.raycastTarget = false;
        spinImg.color = new Color(1f, 1f, 1f, 0f);
        spinRt.SetAsLastSibling();

        // Gövdeyi ayır: mevcut konumun kopyasını vfxRoot'a doğur, paralel (non-blocking)
        // güvenli bölgeye uçur; ana görselden gövdeyi gizle.
        if (bodyImg != null)
        {
            DetachBodyAndRetreat(rt, bodySprite, size);
            bodyImg.enabled = false;
        }

        Vector2 startPos = rt.anchoredPosition;
        Vector2 risePos = startPos + Vector2.up * (Mathf.Min(size.x, size.y) * propellerRiseFactor);

        float safeDur = Mathf.Max(0.01f, duration);
        float local = 0f;
        while (local < safeDur)
        {
            float dt = Time.deltaTime;
            local += dt;
            motion.elapsed += dt;

            float t = Mathf.Clamp01(local / safeDur);
            if (propImg != null) propImg.color = new Color(1f, 1f, 1f, 1f - t);
            spinImg.color = new Color(1f, 1f, 1f, t);
            if (ringImg != null) ringImg.color = new Color(1f, 1f, 1f, t * spinnerRingAlpha);

            rt.anchoredPosition = Vector2.Lerp(startPos, risePos, t);
            rt.localScale = Vector3.one * 2.5f;
            yield return null;
        }

        if (propImg != null) propImg.color = new Color(1f, 1f, 1f, 0f);
        spinImg.color = Color.white;
        if (ringImg != null) ringImg.color = new Color(1f, 1f, 1f, spinnerRingAlpha);

        if (propView != null) propView.Stop();
        if (propGo != null) propGo.SetActive(false);

        onSpinnerReady?.Invoke(spinImg);
    }

    // Gövdenin kopyasını doğurup güvenli bölgeye/yukarı süzülerek fade-out yapan paralel iş.
    private void DetachBodyAndRetreat(RectTransform rt, Sprite bodySprite, Vector2 size)
    {
        if (vfxRoot == null) return;

        var go = RentVisual("PatchbotBodyRetreat", vfxRoot);
        go.transform.SetParent(vfxRoot, false);

        var brt = go.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = size;
        brt.anchoredPosition = rt.anchoredPosition;   // rt ile aynı parent (vfxRoot) uzayında
        brt.localScale = rt.localScale;

        var bimg = go.GetComponent<Image>();
        // Ayrı robot-gövde sprite'ı atanmışsa onu kullan; yoksa uçuş ikonuna düş (eski davranış).
        bimg.sprite = bodyRetreatSprite != null ? bodyRetreatSprite : bodySprite;
        bimg.preserveAspect = true;
        bimg.raycastTarget = false;
        bimg.color = bodyGhostTint;   // hayalet hissi (mavimsi yarı-saydam)
        brt.SetAsLastSibling();

        StartCoroutine(BodyRetreat(go, brt, bimg, size));
    }

    // Kalkışta spinner'ın etrafına radyal kıvılcım/roket demeti saçar (aşağı yarıya hafif bias =
    // egzoz hissi). Her kıvılcım dışarı fırlar, küçülüp söner.
    private void SpawnLaunchSparks(Vector2 centerAnchored, Vector2 size)
    {
        if (vfxRoot == null || launchSparkCount <= 0)
            return;

        float baseR = Mathf.Min(size.x, size.y);
        var sparkSprite = GetSparkSprite();

        for (int i = 0; i < launchSparkCount; i++)
        {
            float ang = (i / (float)launchSparkCount) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) - 0.35f).normalized;   // egzoz için aşağı bias
            float dist = baseR * launchSparkDistanceFactor * Random.Range(0.55f, 1.15f);
            float sparkSize = baseR * Random.Range(0.10f, 0.22f);

            var img = UiVfxPool.RentImage(LaunchSparkPoolKey, vfxRoot, "PatchbotLaunchSpark");
            var go = img.gameObject;
            var srt = img.rectTransform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(sparkSize, sparkSize);
            srt.anchoredPosition = centerAnchored;
            srt.SetAsLastSibling();

            img.sprite = sparkSprite;
            img.raycastTarget = false;
            img.color = launchSparkColor;

            UiFxPool.TrackPatchbotSpark(img, transform, centerAnchored, centerAnchored + dir * dist,
                ResolveSparkLife(launchSparkLife), LaunchSparkPoolKey);
        }
    }

    /// <summary>
    /// Roket/PatchBot çarpma alevini (kıvılcım demeti + flash) verilen hücrede oynatır.
    /// Hammer booster gibi dışarıdan tetiklenen efektler için public giriş noktası.
    /// sizeScale: patlama büyüklüğü çarpanı (1 = tile boyu; biraz büyük için ~1.3).
    /// </summary>
    public void PlayImpactBurstAtCell(BoardController board, Vector2Int cell, float sizeScale = 1f)
    {
        if (vfxRoot == null || board == null) return;

        // Bu efekt StartCoroutine (this) kullanıyor; PatchbotRunner idle'da PASİF olabilir
        // (hammer gibi dash dışı çağrılarda). Coroutine host'unu aktifleştir, template runner'ı gizle.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            if (runnerImage != null) runnerImage.enabled = false;   // stray runner görünmesin
        }

        Vector2 center = WorldToAnchoredIn(vfxRoot, AimWorldPosition(board, cell.x, cell.y));
        float s = board.TileSize * Mathf.Max(0.5f, sizeScale);
        SpawnImpactSparks(center, new Vector2(s, s), board, cell);
    }

    // Hedefe çarpma anında 360 derece saçılan, beyaz + alev sarısı/turuncu kıvılcım demeti.
    private void SpawnImpactSparks(Vector2 centerAnchored, Vector2 size, BoardController board, Vector2Int targetCell)
    {
        if (vfxRoot == null)
            return;

        int count = impactSparkCount > 0 ? impactSparkCount : 36;
        float distFactor = (impactSparkDistanceFactor > 0.05f ? impactSparkDistanceFactor : 2.1f)
            * Mathf.Clamp(impactSparkSpreadScale, 0.35f, 1f);
        float sizeMul = Mathf.Clamp(impactSparkSizeMultiplier, 0.75f, 1.8f);
        float life = impactSparkLife > 0.05f ? impactSparkLife : 0.48f;

        float baseR = size.sqrMagnitude > 1f ? Mathf.Min(size.x, size.y) : (board != null ? board.TileSize : 96f);
        if (baseR < 50f) baseR = 96f;

        var streakSprite = impactSparkSprite != null ? impactSparkSprite : GetStreakSprite();
        var glowSprite = GetSparkSprite();

        // 1. Çarpma paleti: hedef tile rengini okumadan beyaz + alev sarısı/turuncu.
        Color flameColor = new Color(1f, 0.68f, 0.10f, 1f);
        Color hotYellow = new Color(1f, 0.88f, 0.24f, 1f);
        Color flashColor = new Color(1f, 0.76f, 0.16f, 1f);

        if (board != null && targetCell.x >= 0 && targetCell.x < board.Width && targetCell.y >= 0 && targetCell.y < board.Height)
        {
            var tile = board.Tiles[targetCell.x, targetCell.y];
            if (tile != null)
            {
                // Hedef taşı anlık %100 beyaza çekip yumuşakça eski rengine döndür
                tile.FlashWhiteSpark(1.0f, 0.30f);
            }
        }

        // 2. Anlık parıltı patlaması (merkezde beyaz + renkli çift katmanlı parlama)
        if (impactFlashEnabled)
        {
            StartCoroutine(ImpactFlashRoutine(centerAnchored, baseR * 1.65f, flashColor));
        }

        // 3. 360 derece saçılan directional kıvılcımlar
        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)).normalized;
            float dist = baseR * distFactor * Random.Range(0.45f, 1.08f);

            // Büyük ana kıvılcımlar ve küçük hızlı parçacıklar
            bool isBigStreak = (i % 3 == 0);
            float sparkLen = (isBigStreak ? baseR * Random.Range(0.54f, 0.92f) : baseR * Random.Range(0.34f, 0.62f)) * sizeMul;
            float sparkWid = sparkLen * Random.Range(0.45f, 0.72f);

            var img = UiVfxPool.RentImage(ImpactSparkPoolKey, vfxRoot, "PatchbotImpactSpark");
            var go = img.gameObject;
            var srt = img.rectTransform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(sparkLen, sparkWid);
            srt.anchoredPosition = centerAnchored;
            srt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            srt.localScale = Vector3.one;
            srt.SetAsLastSibling();

            img.sprite = (i % 4 == 0) ? glowSprite : streakSprite;
            img.raycastTarget = false;

            // Beyaz + alev sarısı/turuncu karışımı; hedef tile rengine bağlanmaz.
            Color baseFlame = Random.value < 0.65f ? hotYellow : flameColor;
            float whiteBlend = (i % 2 == 0) ? Random.Range(0.58f, 0.92f) : Random.Range(0.25f, 0.55f);
            Color sparkColor = Color.Lerp(baseFlame, Color.white, whiteBlend);
            sparkColor.a = 1f;
            img.color = sparkColor;

            float sparkIndividualLife = life * Random.Range(0.75f, 1.25f);
            UiFxPool.TrackPatchbotSpark(img, transform, centerAnchored, centerAnchored + dir * dist,
                ResolveSparkLife(sparkIndividualLife), ImpactSparkPoolKey);
        }
    }

    private IEnumerator ImpactFlashRoutine(Vector2 centerAnchored, float maxRadius, Color hitColor)
    {
        if (vfxRoot == null) yield break;

        // 1. Dış renkli parlama halkası
        var outerImg = UiVfxPool.RentImage(ImpactFlashOuterPoolKey, vfxRoot, "PatchbotImpactFlashOuter");
        var outerGo = outerImg.gameObject;
        var outerRt = outerImg.rectTransform;
        outerRt.anchorMin = outerRt.anchorMax = outerRt.pivot = new Vector2(0.5f, 0.5f);
        outerRt.sizeDelta = Vector2.one * (maxRadius * 0.4f);
        outerRt.anchoredPosition = centerAnchored;
        outerRt.localScale = Vector3.one;
        outerRt.SetAsLastSibling();

        outerImg.sprite = GetSparkSprite();
        outerImg.raycastTarget = false;
        outerImg.color = hitColor;

        // 2. İç parlak beyaz çekirdek
        var innerImg = UiVfxPool.RentImage(ImpactFlashInnerPoolKey, vfxRoot, "PatchbotImpactFlashInner");
        var innerGo = innerImg.gameObject;
        var innerRt = innerImg.rectTransform;
        innerRt.anchorMin = innerRt.anchorMax = innerRt.pivot = new Vector2(0.5f, 0.5f);
        innerRt.sizeDelta = Vector2.one * (maxRadius * 0.25f);
        innerRt.anchoredPosition = centerAnchored;
        innerRt.localScale = Vector3.one;
        innerRt.SetAsLastSibling();

        innerImg.sprite = GetSparkSprite();
        innerImg.raycastTarget = false;
        innerImg.color = Color.white;

        float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration && (outerGo != null || innerGo != null))
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (outerGo != null && outerRt != null && outerImg != null)
            {
                float scale = Mathf.Lerp(0.5f, 2.8f, Mathf.Sqrt(t));
                outerRt.sizeDelta = Vector2.one * (maxRadius * scale);
                float alpha = (1f - t) * (1f - t);
                outerImg.color = new Color(hitColor.r, hitColor.g, hitColor.b, alpha * 0.95f);
            }

            if (innerGo != null && innerRt != null && innerImg != null)
            {
                float scale = Mathf.Lerp(0.4f, 1.9f, Mathf.Sqrt(t));
                innerRt.sizeDelta = Vector2.one * (maxRadius * scale);
                float alpha = Mathf.Clamp01(1f - t * 1.4f);
                innerImg.color = new Color(1f, 1f, 1f, alpha);
            }

            yield return null;
        }

        if (outerGo != null) UiVfxPool.Return(ImpactFlashOuterPoolKey, outerGo);
        if (innerGo != null) UiVfxPool.Return(ImpactFlashInnerPoolKey, innerGo);
    }

    private float ResolveSparkLife(float customLife) => customLife > 0.05f
        ? customLife : (impactSparkLife > 0.05f ? impactSparkLife : 0.48f);

    private static Sprite _sparkSprite;
    private static Sprite GetSparkSprite()
    {
        if (_sparkSprite != null)
            return _sparkSprite;

        const int res = 64;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
        float radius = res * 0.5f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center) / radius;
            if (d >= 1f)
            {
                tex.SetPixel(x, y, Color.clear);
            }
            else
            {
                float a = Mathf.Clamp01(1f - d);
                float core = Mathf.Clamp01(1f - d * 1.5f);
                float alpha = Mathf.Clamp01(a * a + core * 0.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        _sparkSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
        _sparkSprite.name = "GeneratedPatchbotSpark";
        return _sparkSprite;
    }

    private static Sprite _streakSprite;
    private static Sprite GetStreakSprite()
    {
        if (_streakSprite != null)
            return _streakSprite;

        const int w = 64, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var center = new Vector2((w - 1) * 0.5f, (h - 1) * 0.5f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = (x - center.x) / (w * 0.5f);
            float ny = (y - center.y) / (h * 0.5f);
            float d = Mathf.Sqrt(nx * nx + ny * ny * 2.2f);
            if (d >= 1f)
            {
                tex.SetPixel(x, y, Color.clear);
            }
            else
            {
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }

        tex.Apply();
        _streakSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        _streakSprite.name = "GeneratedPatchbotStreak";
        return _streakSprite;
    }

    // Güvenli bölge: elle atanan bodySafeZone; boşsa runtime'da TopHUD avatarını (AvatarView) bul.
    // Prefab sahne objesine referans TUTAMAZ; bu yüzden fallback otomatik bulur — bağlama gerektirmez.
    private RectTransform _cachedAvatarRect;
    private RectTransform ResolveBodySafeZone()
    {
        if (bodySafeZone != null) return bodySafeZone;
        if (_cachedAvatarRect != null) return _cachedAvatarRect;

        // Asıl hedef: TopHUD içindeki robot maskesi
        // (canvas/safearea/tophud/topcontent/centerpanel/robotmask). İsimle (casing-duyarsız)
        // descendant araması → hiyerarşi yeniden düzenlense de bulur.
        var hud = FindFirstObjectByType<TopHudController>();
        if (hud != null)
        {
            var robotMask = FindDescendantByName(hud.transform, "robotmask") as RectTransform;
            if (robotMask != null)
            {
                _cachedAvatarRect = robotMask;
                return _cachedAvatarRect;
            }
        }

        // Yedekler: AvatarView (profil), sonra HUD kökü — yine de yukarı-kayıp-sönmekten iyidir.
        var av = FindFirstObjectByType<AvatarView>();
        if (av != null)
        {
            _cachedAvatarRect = av.transform as RectTransform;
            return _cachedAvatarRect;
        }
        if (hud != null)
        {
            _cachedAvatarRect = hud.transform as RectTransform;
            return _cachedAvatarRect;
        }

        return _cachedAvatarRect;
    }

    // Kök altındaki (pasif dahil) ilk eşleşen isimli transform'u bulur (casing-duyarsız).
    private static Transform FindDescendantByName(Transform root, string name)
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (string.Equals(all[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                return all[i];
        return null;
    }

    private IEnumerator BodyRetreat(GameObject go, RectTransform brt, Image bimg, Vector2 size)
    {
        Vector2 startPos = brt.anchoredPosition;
        var safeZone = ResolveBodySafeZone();
        Vector2 target = safeZone != null
            ? WorldToAnchoredIn(vfxRoot, safeZone.position)
            : startPos + Vector2.up * (Mathf.Min(size.x, size.y) * bodyRetreatRiseFactor);

        Vector3 startScale = brt.localScale;
        Vector3 endScale = startScale * bodyRetreatEndScale;

        float dur = Mathf.Max(0.05f, bodyRetreatDuration);
        float local = 0f;
        while (local < dur && go != null)
        {
            local += Time.deltaTime;
            float t = Mathf.Clamp01(local / dur);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            brt.anchoredPosition = Vector2.LerpUnclamped(startPos, target, eased);
            brt.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            bimg.color = new Color(bodyGhostTint.r, bodyGhostTint.g, bodyGhostTint.b, bodyGhostTint.a * (1f - t));
            yield return null;
        }

        if (go != null) ReleaseVisualTree(go);
    }

    // Spinner frame'lerini spinnerFps hızında döngüler; ayrıca opsiyonel sürekli transform
    // dönüşü uygular (frame'ler zaten dönme snapshot'ları — ikisi birlikte hızlı bulanık spin).
    private IEnumerator SpinnerFrameCycler(Image spinImg)
    {
        if (spinImg == null || spinnerFrames == null || spinnerFrames.Length == 0)
            yield break;

        var srt = spinImg.rectTransform;
        // Hız halkası spinner'ın kardeşi (aynı eğim kabı); kendi, daha yavaş hızında döner.
        var ringRt = srt.parent != null ? srt.parent.Find(SpinnerRingName) as RectTransform : null;

        // Dönme, transform rotasyonu ile sağlanır → kesintisiz, tam 360°, boşluksuz.
        // Frame değişimi yalnızca (a) spin kapalıyken saf frame animasyonu, ya da
        // (b) spin açık + animateFramesWhileSpinning ile şimşek parıltısı içindir.
        bool spinning = spinnerSpinSpeed > 0f;
        bool cycleFrames = !spinning || animateFramesWhileSpinning;

        // Frizbi eğimi artık DÖNMEYEN tilt-holder parent'ta (sabit squash). Burada spinner yalnız
        // kendi düzleminde (Z) döner → holder'ın elipsi içinde bıçaklar döner, elips titremez.
        float step = 1f / Mathf.Max(1f, spinnerFps);
        float frameT = 0f;
        int idx = 0;

        while (true)
        {
            if (cycleFrames)
            {
                frameT += Time.deltaTime;
                if (frameT >= step)
                {
                    frameT -= step;
                    idx = (idx + 1) % spinnerFrames.Length;
                    spinImg.sprite = spinnerFrames[idx];
                }
            }

            if (spinning)
                srt.Rotate(0f, 0f, -spinnerSpinSpeed * Time.deltaTime);
            if (ringRt != null && spinnerRingSpinSpeed > 0f)
                ringRt.Rotate(0f, 0f, -spinnerRingSpinSpeed * Time.deltaTime);

            yield return null;
        }
    }

    private void UpdateCarryOrbit(RectTransform carryRt, Vector2 size, float elapsed)
    {
        if (carryRt == null)
            return;

        float footOffset = Mathf.Min(size.x, size.y) * carryOrbitRadiusFactor;
        carryRt.anchoredPosition = Vector2.down * footOffset;
        carryRt.localRotation = Quaternion.identity;
    }

    private void TickAfterImage(RectTransform rt, Sprite sprite, DashMotionState motion)
    {
        motion.afterTimer += Time.deltaTime;
        if (motion.afterTimer < spawnEvery)
            return;

        motion.afterTimer = 0f;
        SpawnAfterImageAt(rt, sprite);
    }

    private IEnumerator DashRoutine(List<RectTransform> path)
    {
        if (runnerImage == null || tileIcons == null || boardContent == null || vfxRoot == null) yield break;
        if (path == null || path.Count == 0) yield break;

        if (transform.parent != vfxRoot)
            transform.SetParent(vfxRoot, false);

        if (runnerImage.sprite == null && tileIcons != null)
            runnerImage.sprite = tileIcons.GetPatchBotFlightIcon();
        runnerImage.raycastTarget = false;
        runnerImage.enabled = true;
        runnerImage.color = Color.white;

        Vector2 tileSize = new Vector2(90f, 90f);
        var tileImage = path[0].GetComponent<Image>();
        if (tileImage != null)
        {
            var tileRT = tileImage.rectTransform;
            if (tileRT.rect.width > 1f && tileRT.rect.height > 1f)
                tileSize = tileRT.rect.size;
        }

        runnerImage.rectTransform.sizeDelta = tileSize;
        runnerImage.rectTransform.SetAsLastSibling();
        runnerImage.rectTransform.anchoredPosition = WorldToAnchoredIn(vfxRoot, path[0].position);

        var flightSource = legacyFlightLoop = CreateFlightAudioSource(GetComponentInParent<BoardController>());
        StartFlightAudio(flightSource);
        try
        {
            float tAfter = 0f;

            for (int i = 0; i < path.Count; i++)
            {
                Vector2 target = WorldToAnchoredIn(vfxRoot, path[i].position);

                while (Vector2.Distance(runnerImage.rectTransform.anchoredPosition, target) > arriveEps)
                {
                    runnerImage.rectTransform.anchoredPosition =
                        Vector2.MoveTowards(runnerImage.rectTransform.anchoredPosition, target, dashSpeed * Time.deltaTime);

                    tAfter += Time.deltaTime;
                    if (tAfter >= spawnEvery)
                    {
                        tAfter = 0f;
                        SpawnAfterImageAt(runnerImage.rectTransform, runnerImage.sprite);
                    }

                    yield return null;
                }
            }

            var rt = runnerImage.rectTransform;
            Vector3 baseScale = rt.localScale;
            rt.localScale = baseScale * 1.15f;
            yield return new WaitForSeconds(0.06f);
            rt.localScale = baseScale;

            yield return StopFlightAudioRoutine(flightSource);

            runnerImage.enabled = false;
            co = null;
        }
        finally
        {
            ReleaseFlightAudio(flightSource);
            if (legacyFlightLoop == flightSource) legacyFlightLoop = null;
        }
    }

    private void SpawnAfterImageAt(RectTransform source, Sprite sprite)
    {
        if (vfxRoot == null || source == null) return;

        var img = UiVfxPool.RentImage(AfterImagePoolKey, vfxRoot, "PatchbotAfterImage");
        if (img == null) return;
        var go = img.gameObject;
        var rt = img.rectTransform;

        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = afterColor;

        rt.anchorMin = source.anchorMin;
        rt.anchorMax = source.anchorMax;
        rt.pivot = source.pivot;

        rt.sizeDelta = source.sizeDelta;
        rt.anchoredPosition = source.anchoredPosition;
        rt.localScale = source.localScale;

        rt.SetSiblingIndex(Mathf.Max(0, source.GetSiblingIndex() - 1));

        UiFxPool.TrackLinearGhost(go, AfterImagePoolKey, img, afterLife, transform);
    }

    public float EstimateDashDuration(BoardController board, Vector2Int fromCell, Vector2Int toCell, float syncDuration = -1f)
    {
        if (board == null || vfxRoot == null) return 0f;

        Vector3 fromWorld = board.GetCellWorldPosition(fromCell.x, fromCell.y);
        Vector3 toWorld = board.GetCellWorldPosition(toCell.x, toCell.y);
        Vector2 from = WorldToAnchoredIn(vfxRoot, fromWorld);
        Vector2 to = WorldToAnchoredIn(vfxRoot, toWorld);

        float distance = Vector2.Distance(from, to);
        if (distance <= arriveEps) return 0f;

        if (syncDuration > 0f)
            return syncDuration * Mathf.Max(0.01f, syncedDurationMultiplier);

        // Solo dash timing ile aynı: takeoff + hover sabit, dive pike hızıyla.
        float speed = Mathf.Max(1f, dashSpeed * Mathf.Max(0.01f, pikeSpeedMultiplier));
        float dive = Mathf.Max(0.05f, distance / speed);
        float takeoff = Mathf.Max(0f, takeoffBurstDuration);
        float hover = Mathf.Max(0f, hoverHoldDuration);
        float sep = (spinnerFrames != null && spinnerFrames.Length > 0) ? Mathf.Max(0f, separationDuration) : 0f;
        return takeoff + hover + sep + dive;
    }

    private BoardAudioDirector.ClipLoop CreateFlightAudioSource(BoardController board)
    {
        // Reserve central voices for impacts/other effects during Override + PatchBot bursts.
        if (board == null || board.Audio == null || flightLoopClip == null || flightLoops.Count >= 2) return null;
        var loop = board.Audio.StartLoopClip(flightLoopClip,
            Mathf.Max(0f, flightLoopVolume + Random.Range(-flightVolumeJitter, flightVolumeJitter)),
            Mathf.Max(0.01f, flightPitch + Random.Range(-flightPitchJitter, flightPitchJitter)));
        if (loop != null) flightLoops.Add(loop);
        return loop;
    }

    private void ReleaseFlightAudio(BoardAudioDirector.ClipLoop loop)
    {
        if (loop == null) return;
        loop.Dispose();
        flightLoops.Remove(loop);
    }

    private void StartFlightAudio(BoardAudioDirector.ClipLoop source)
    {
        if (source == null) return;
        float targetVolume = source.Volume;
        if (flightFadeIn > 0f)
        {
            source.Volume = 0f;
            StartCoroutine(FadeAudioVolumeRoutine(source, targetVolume, flightFadeIn));
        }
    }

    private IEnumerator StopFlightAudioRoutine(BoardAudioDirector.ClipLoop source)
    {
        if (source == null) yield break;
        if (!GameSettings.SoundEnabled) { ReleaseFlightAudio(source); yield break; }
        float startVolume = source.Volume;
        for (float elapsed = 0f; elapsed < flightFadeOut && !source.IsDisposed; elapsed += Time.deltaTime)
        {
            source.Volume = Mathf.Lerp(startVolume, 0f, elapsed / flightFadeOut);
            yield return null;
        }
        ReleaseFlightAudio(source);
    }

    private IEnumerator FadeAudioVolumeRoutine(BoardAudioDirector.ClipLoop source, float targetVolume, float duration)
    {
        for (float elapsed = 0f; elapsed < duration && !source.IsDisposed; elapsed += Time.deltaTime)
        {
            source.Volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
            yield return null;
        }
        if (!source.IsDisposed) source.Volume = targetVolume;
    }

static Vector2 WorldToAnchoredIn(RectTransform targetSpace, Vector3 worldPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetSpace,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint
        );
        return localPoint;
    }
}
