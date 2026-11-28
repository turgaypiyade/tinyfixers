using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Streak booster teslimatı: oyun başında (board hazır) level'a göre hak edilen special'lar varsa
/// UFO uçup gelir, hedef hücrelere SIRALI ışın atarak implant eder (yerleşim motoru
/// PreLevelSpecialRuntimeInjector — prelevel booster ile birebir kural), sonra uçup gider.
///
/// Kurulum: Game sahnesinde bir objeye koy. UFO görselini (RectTransform) ufo'ya ata; başta gizli
/// olacak/ekran dışına alınacak. Beam prefab'ı ata (UFO→hücre ışını). Global level otomatik
/// CurrentLevel.Global'dan alınır (catalog GEREKMEZ). Konum/süre alanlarını zevkine göre ayarla.
/// </summary>
public sealed class UfoImplantController : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("UFO görseli (RectTransform). Başta gizli tut; controller uçurur.")]
    [SerializeField] private RectTransform ufo;
    [Tooltip("UFO'dan hedef hücreye çizilecek ışın prefab'ı (kendini yok etmesi iyi olur). Boşsa ışın atlanır.")]
    [SerializeField] private GameObject beamPrefab;
    [Tooltip("Işın/UFO'nun konacağı canvas alanı. Boşsa ufo'nun parent'ı kullanılır.")]
    [SerializeField] private RectTransform uiSpace;

    [Header("Flight")]
    [Tooltip("UFO'nun hover (duruş) anchoredPosition'ı.")]
    [SerializeField] private Vector2 hoverAnchoredPos = new Vector2(0f, 250f);
    [Tooltip("Başlangıç: hover'dan bu kadar YUKARIDA (ekran dışı) belirir.")]
    [SerializeField] private float offscreenRise = 900f;
    [SerializeField, Min(0.05f)] private float flyInDuration = 0.8f;
    [SerializeField, Min(0.05f)] private float flyOutDuration = 0.7f;
    [Tooltip("Hover sırasında hafif salınım (px, 0=kapalı).")]
    [SerializeField] private float hoverBobAmplitude = 12f;
    [SerializeField] private float hoverBobSpeed = 2f;

    [Header("Beam")]
    [Tooltip("Her ışının süresi (sn).")]
    [SerializeField, Min(0.05f)] private float beamDuration = 0.35f;
    [Tooltip("beamPrefab BOŞsa: prosedürel ışının rengi (UFO girdabıyla uyumlu yeşil/cyan iyi durur).")]
    [SerializeField] private Color beamColor = new Color(0.25f, 1f, 0.75f, 1f);
    [Tooltip("Prosedürel ışının kalınlığı (px).")]
    [SerializeField, Min(2f)] private float beamWidth = 22f;

    [Header("Pervane (helikopter)")]
    [Tooltip("Pervane dönüşü (tur/sn). Önden bakış: pervanenin yatay ölçeği cos(açı). 0 = döndürme.")]
    [SerializeField, Min(0f)] private float rotorRevsPerSecond = 4.5f;

    // Helikopter görseli (kullanıcı 2026-11-26): UFO.png = gövde (pervanesiz); pervane + göbek ayrı sprite'lar
    // Resources/StreakBooster'dan, ufo'nun çocuğu olarak kurulur. Konumlar kaynak görselde (1405×1119) normalize.
    private const string RotorSpritePath = "StreakBooster/HeliRotor";
    private const string HubSpritePath = "StreakBooster/HeliHub";
    private static readonly Rect RotorArea = Rect.MinMaxRect(0.0705f, 0.8499f, 0.9295f, 0.9669f);
    private const float RotorPivotX = 0.5008f;
    private static readonly Rect HubArea = Rect.MinMaxRect(0.4164f, 0.8651f, 0.5858f, 0.9964f);
    private RectTransform rotor;

    private BoardController board;
    private bool hovering;
    private bool visualsCancelled;
    private bool ownsInputLock;
    private PreLevelSpecialRuntimeInjector injector;
    private readonly List<GameObject> activeBeams = new(3);

    private void OnDisable()
    {
        visualsCancelled = true;
        hovering = false;
        // The injector owns placement and may finish granting rewards without the visuals.
        // Its currently running FlyInGate also checks visualsCancelled after every yield.
        if (injector != null)
        {
            injector.PrePlacementGate = null;
            injector.OnSpecialPlaced = null;
            injector.OnPlacementFinished = null;
        }
        StopAllCoroutines();
        for (int i = 0; i < activeBeams.Count; i++)
            if (activeBeams[i] != null) Destroy(activeBeams[i]);
        activeBeams.Clear();
        if (rotor != null) rotor.localScale = Vector3.one;
        if (ufo != null) ufo.gameObject.SetActive(false);
        ReleaseInputLock();
    }

    private void ReleaseInputLock()
    {
        if (!ownsInputLock) return;
        ownsInputLock = false;
        if (board != null) board.SetInputLocked(false);
    }

    private IEnumerator Start()
    {
        if (ufo != null) ufo.gameObject.SetActive(false);
        if (visualsCancelled) yield break;
        // Simulation owns its explicitly configured starting boosters. Saved streak
        // rewards must not silently change either control or boosted samples.
        if (RuntimeSimulationSession.IsActive) yield break;
        if (uiSpace == null && ufo != null) uiSpace = ufo.parent as RectTransform;

        // Level numarası merkezi kaynaktan (catalog gerekmez). Ödül yoksa board'u beklemeden çık.
        int globalLevel = CurrentLevel.Global;
        int streak = PlayerStats.CurrentStreak;
        int stage = StreakBoosterEvent.CurrentStage;
        var earned = StreakBoosterEvent.GetEarnedSpecials(globalLevel);

        if (earned == null || earned.Count == 0)
            yield break;

        // Işın koordinatları için board hazır olana kadar bekle.
        float t = 0f;
        while (t < 12f)
        {
            board = board != null ? board : FindFirstObjectByType<BoardController>();
            if (board != null && board.ActiveLevelData != null) break;
            t += Time.deltaTime;
            yield return null;
        }
        if (board == null)
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning("[UfoImplant] Board hazır olmadı (timeout) — teslimat atlandı.");
            yield break;
        }

        // Dedicated injector (prelevel selection'ı ezmez). Hook'ları bağla; timing'i injector yönetir:
        // board+loading hazır → PrePlacementGate (uç gel) → her special'da OnSpecialPlaced (ışın) → bitti (uç git).
        BuildRotor();   // yalnız teslimat olacaksa
        injector = PreLevelSpecialRuntimeInjector.CreateDedicated(earned);
        if (injector == null)
            yield break;

        injector.PrePlacementGate = FlyInGate;
        injector.OnSpecialPlaced = (tile, _) => FireBeam(tile);
        injector.OnPlacementFinished = () =>
        {
            // Taşlar yerleşti → oyuncu artık oynayabilir. UFO uçup giderken oyun serbest.
            ReleaseInputLock();
            if (visualsCancelled || !isActiveAndEnabled) return;
            StartCoroutine(FlyOut());
        };
    }

    // Injector board hazır olunca çağırır: UFO uçup gelir, hover'a oturur.
    private IEnumerator FlyInGate()
    {
        if (visualsCancelled || !isActiveAndEnabled || ufo == null) yield break;

        // Tutorial/hint overlay'i ekrandayken UFO GELMESİN — önce onlar bitsin (kılavuz-swap
        // tutorial'ı varsa oyuncu onu yaparken input SERBEST kalmalı, o yüzden kilit tutorial'lardan SONRA).
        yield return WaitForTutorialsClear();
        if (visualsCancelled || !isActiveAndEnabled || ufo == null) yield break;

        // Teslimat boyunca oyuncu OYNAYAMASIN — UFO uçup taşları yerleştirene kadar board kilitli.
        // Placement bitince OnPlacementFinished açar (UFO uçup giderken oyun serbest).
        if (board != null)
        {
            ownsInputLock = !board.IsExplicitlyLocked;
            if (ownsInputLock) board.SetInputLocked(true);
        }

        ufo.gameObject.SetActive(true);
        if (rotor != null && rotorRevsPerSecond > 0f) StartCoroutine(SpinRotor());
        Vector2 start = hoverAnchoredPos + Vector2.up * offscreenRise;
        ufo.anchoredPosition = start;

        float d = Mathf.Max(0.05f, flyInDuration);
        float e = 0f;
        while (e < d)
        {
            if (visualsCancelled || !isActiveAndEnabled || ufo == null) yield break;
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / d);
            float eased = 1f - Mathf.Pow(1f - k, 3f); // OutCubic
            ufo.anchoredPosition = Vector2.LerpUnclamped(start, hoverAnchoredPos, eased);
            yield return null;
        }
        if (visualsCancelled || !isActiveAndEnabled || ufo == null) yield break;
        ufo.anchoredPosition = hoverAnchoredPos;

        hovering = true;
        StartCoroutine(HoverBob());
        // Kısa nefes — yerleşim başlamadan UFO "yerleşsin".
        yield return new WaitForSeconds(0.2f);
    }

    // Ekranda tutorial/hint overlay'i (swap veya obstacle hint) varken ya da board hâlâ
    // kilitli/çözülüyorken bekle. Hint'ler arası kısa boşlukta erken tetiklenmemek için
    // "temiz" durumun bir settle penceresi boyunca sürmesini şart koş.
    private IEnumerator WaitForTutorialsClear()
    {
        const float settle = 0.4f;  // temiz durumun kesintisiz sürmesi gereken süre
        const float maxWait = 30f;  // güvenlik tavanı — asla sonsuza takılma
        float waited = 0f;
        float clearFor = 0f;
        bool reasonLogged = false;

        while (waited < maxWait)
        {
            if (visualsCancelled || !isActiveAndEnabled) yield break;
            // FindFirstObjectByType (varsayılan) yalnız AKTİF objeyi bulur → overlay
            // gizliyken null döner. Show/ShowHint her ikisi de bu objeyi aktif eder.
            bool overlayActive = FindFirstObjectByType<TutorialOverlayController>() != null;
            bool boardNotReady = board != null && board.InputLocked;

            if (!overlayActive && !boardNotReady)
            {
                clearFor += Time.deltaTime;
                if (clearFor >= settle) yield break;
            }
            else
            {
                clearFor = 0f;
                // Teşhis (tek sefer): UFO 2 sn'den uzun bekliyorsa nedenini yaz.
                if (!reasonLogged && waited > 2f)
                {
                    reasonLogged = true;
                    if (GameDiagnostics.CriticalErrors)
                        Debug.LogWarning($"[UfoImplant] UFO bekliyor ({waited:0.0}s): tutorialOverlay={overlayActive} " + $"boardBusy={(board != null && board.IsBusy)} boardLocked={(board != null && board.IsExplicitlyLocked)}");
                }
            }

            waited += Time.deltaTime;
            yield return null;
        }
    }

    // Pervane + göbek çocuklarını kurar (sprite'lar yoksa UFO eskisi gibi tek görsel kalır).
    private void BuildRotor()
    {
        if (ufo == null || rotor != null) return;
        var rotorSprite = Resources.Load<Sprite>(RotorSpritePath);
        var hubSprite = Resources.Load<Sprite>(HubSpritePath);
        if (rotorSprite == null || hubSprite == null) return;
        rotor = CreatePart("Rotor", rotorSprite, RotorArea, new Vector2(RotorPivotX, 0.5f));
        CreatePart("RotorHub", hubSprite, HubArea, new Vector2(0.5f, 0.5f));   // pervanenin ortasını örter
    }

    private RectTransform CreatePart(string partName, Sprite sprite, Rect area, Vector2 pivot)
    {
        var go = new GameObject(partName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.layer = ufo.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(ufo, false);
        rt.anchorMin = area.min;
        rt.anchorMax = area.max;
        rt.pivot = pivot;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return rt;
    }

    // Helikopter görünür olduğu sürece döner (uçuş boyunca); UFO kapanınca döngü biter — boşta çalışmaz.
    private IEnumerator SpinRotor()
    {
        float phase = 0f;
        while (ufo != null && ufo.gameObject.activeInHierarchy && rotor != null)
        {
            phase += Time.deltaTime * rotorRevsPerSecond * Mathf.PI * 2f;
            if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;
            rotor.localScale = new Vector3(Mathf.Cos(phase), 1f, 1f);   // önden bakış: kanatlar uzayıp kısalır
            yield return null;
        }
    }

    private IEnumerator HoverBob()
    {
        if (hoverBobAmplitude <= 0f) yield break;
        while (hovering && ufo != null)
        {
            float off = Mathf.Sin(Time.time * hoverBobSpeed) * hoverBobAmplitude;
            ufo.anchoredPosition = new Vector2(hoverAnchoredPos.x, hoverAnchoredPos.y + off);
            yield return null;
        }
    }

    private void FireBeam(TileView tile)
    {
        if (visualsCancelled || !isActiveAndEnabled || tile == null || uiSpace == null || board == null || ufo == null)
            return;

        Vector2 from = ufo.anchoredPosition;
        Vector2 to = WorldToLocal(board.GetCellWorldCenterPosition(tile.X, tile.Y));
        StartCoroutine(CoBeam(from, to));
    }

    private IEnumerator CoBeam(Vector2 from, Vector2 to)
    {
        // beamPrefab varsa onu kullan; yoksa prosedürel ışın (prefab GEREKMEZ).
        var go = beamPrefab != null ? Instantiate(beamPrefab, uiSpace) : CreateProceduralBeam();
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) { Destroy(go); yield break; }
        activeBeams.Add(go);

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f); // pivot ALTTA: yukarıdan (UFO) aşağı uzasın

        Vector2 dir = to - from;
        float len = dir.magnitude;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // +Y'yi dir'e döndür
        rt.anchoredPosition = from;
        rt.localRotation = Quaternion.Euler(0f, 0f, ang);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, len);

        float d = Mathf.Max(0.05f, beamDuration);
        float e = 0f;
        var graphics = go.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        while (e < d && rt != null)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / d);
            // hızlı belir, sona doğru sön
            float a = k < 0.3f ? Mathf.Lerp(0f, 1f, k / 0.3f) : Mathf.Lerp(1f, 0f, (k - 0.3f) / 0.7f);
            for (int i = 0; i < graphics.Length; i++)
            {
                var c = graphics[i].color; c.a = a; graphics[i].color = c;
            }
            yield return null;
        }
        activeBeams.Remove(go);
        if (go != null) Destroy(go);
    }

    // beamPrefab yoksa: kod-üretimi yumuşak-kenarlı ışın (prefab GEREKMEZ).
    private GameObject CreateProceduralBeam()
    {
        var go = new GameObject("StreakBeam",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(uiSpace, false);

        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.sprite = BeamSprite();
        img.type = UnityEngine.UI.Image.Type.Simple;
        img.color = beamColor;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(beamWidth, 10f); // yükseklik CoBeam'de mesafeye göre set edilir
        return go;
    }

    // Yatay ekseni yumuşak (merkez parlak, kenarlar şeffaf) ince ışın sprite'ı — bir kez üretilir.
    private static Sprite _beamSprite;
    private static Sprite BeamSprite()
    {
        if (_beamSprite != null) return _beamSprite;

        const int w = 32, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color[w * h];
        for (int x = 0; x < w; x++)
        {
            float nx = w > 1 ? x / (float)(w - 1) : 0.5f;
            float a = Mathf.Clamp01(Mathf.Sin(nx * Mathf.PI)); // 0 kenar → 1 merkez
            for (int y = 0; y < h; y++)
                px[y * w + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();

        _beamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return _beamSprite;
    }

    private IEnumerator FlyOut()
    {
        hovering = false;
        if (ufo == null) yield break;

        yield return new WaitForSeconds(0.25f);

        Vector2 from = ufo.anchoredPosition;
        Vector2 target = hoverAnchoredPos + Vector2.up * offscreenRise;
        float d = Mathf.Max(0.05f, flyOutDuration);
        float e = 0f;
        while (e < d)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / d);
            float eased = k * k; // InQuad (hızlanarak kaçış)
            ufo.anchoredPosition = Vector2.LerpUnclamped(from, target, eased);
            yield return null;
        }
        ufo.gameObject.SetActive(false);
    }

    private Vector2 WorldToLocal(Vector3 worldPos)
    {
        var canvas = uiSpace.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(uiSpace, screen, cam, out var local);
        return local;
    }
}
