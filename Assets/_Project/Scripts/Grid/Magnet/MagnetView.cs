using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Visual for one magnet pair obstacle.
/// Two magnet sprites sit at the endpoints; overlapping glow circles fill the
/// connecting path, naturally rounding every corner without needing corner sprites.
///
/// Setup: call Init() after Instantiate. The view manages its own children and
/// is destroyed via PlayDestroyAnimation() when the pair meets.
public class MagnetView : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("Mıknatıs uç sprite'ı. MagnetB yatay olarak çevrilir.")]
    [SerializeField] private Sprite magnetSprite;
    [Tooltip("Zincir baklası sprite'ı: dikey oval RING (ortası boş). Yön'e göre döndürülür.")]
    [SerializeField] private Sprite glowCircleSprite;

    [Header("Chain Link")]
    [Tooltip("Bakla rengi (tint). Sprite zaten renkliyse beyaz bırak.")]
    [SerializeField] private Color glowColor = Color.white;
    [Tooltip("Baklanın KISA ekseni (kalınlık) / hücre.")]
    [SerializeField, Range(0.3f, 1.2f)] private float chainLinkWidthRatio = 0.72f;
    [Tooltip("Baklanın UZUN ekseni (boy) / hücre. >1 → bakla hücreden BÜYÜK olur, komşularla içiçe geçer.")]
    [SerializeField, Range(1f, 2.2f)] private float chainLinkLengthRatio = 1.55f;
    [Tooltip("Kose baglanti baklasinin capi / duz bakla kalinligi.")]
    [SerializeField, Range(0.6f, 1.6f)] private float chainCornerScale = 1.08f;
    [Tooltip("Duz baklalari dugumlerin otesine tasiran ek bindirme / hucre.")]
    [SerializeField, Range(0f, 0.45f)] private float chainCornerOffset = 0.14f;

    [Header("Pulse")]
    [SerializeField, Min(0.2f)] private float pulseDuration = 1.4f;
    [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.5f;
    [SerializeField, Range(0f, 1f)] private float pulseMaxAlpha = 0.88f;

    [Header("Move Animation")]
    [SerializeField, Min(0.05f)] private float moveDuration = 0.2f;

    [Header("Destroy Animation")]
    [SerializeField, Min(0.05f)] private float destroyDuration = 0.35f;
    [SerializeField, Range(1, 4)] private int destroyShardCountPerMagnet = 2;
    [SerializeField, Min(0.1f)] private float destroyShardFallDuration = 1.35f;
    [SerializeField] private float destroyShardGravity = 720f;
    [SerializeField] private float destroyShardSideKick = 260f;
    [SerializeField] private float destroyShardLaunchUp = 430f;

    // ── Yeni Tüp stili (eski ChainLinks korunur; geri dönüş için bayrakla seçilir) ──
    public enum MagnetStyle { ChainLinks, Tube }

    [Header("Style")]
    [Tooltip("ChainLinks = mevcut/ESKİ görünüm (geri dönüş için korunur). Tube = yeni tüp + akan akım.")]
    [SerializeField] private MagnetStyle style = MagnetStyle.ChainLinks;

    [Header("Tube — Sprites")]
    [Tooltip("DÜZ DİKEY tüp. Kırmızı kılıf + mavi core.")]
    [SerializeField] private Sprite straightTubeSprite;
    [Tooltip("DÜZ YATAY tüp (opsiyonel). Boşsa dikey tüp 90° döndürülür; atarsan döndürme olmaz (highlight/hiza tam oturur).")]
    [SerializeField] private Sprite straightTubeHorizontalSprite;
    [Tooltip("L-elbow (köşe). TEK sprite; 4 yönelime döndürülür.")]
    [SerializeField] private Sprite elbowTubeSprite;
    [Tooltip("AÇIK (önerilen): elbow 90° DÖNDÜRÜLMEZ, FLIP (aynalama) ile 4 köşe üretilir → kollar hep " +
             "yatay/dikey kalır, düz tüplere tam oturur. KAPALI: eski döndürme (elbowRotationOffset).")]
    [SerializeField] private bool elbowUseFlip = true;
    [Tooltip("Base L'nin YATAY kolu SAĞA mı bakıyor? Standart L (└) = SAĞ. L SOLA açılıyorsa (┘/ters-L) KAPAT.")]
    [SerializeField] private bool elbowBaseRight = true;
    [Tooltip("Base L'nin DİKEY kolu YUKARI mı bakıyor? Standart L yukarı açılır → AÇIK.")]
    [SerializeField] private bool elbowBaseUp = true;
    [Tooltip("elbowUseFlip KAPALIYKEN: L-elbow döndürme ofseti (°).")]
    [SerializeField] private float elbowRotationOffset = 0f;
    [Tooltip("Elbow ince dikey hizalama (px): ÜST köşeler (L/ters-L) +YUKARI, ALT köşeler (alt-sağ/alt-sol) -AŞAĞI kayar.")]
    [SerializeField] private float elbowVerticalNudge = 1f;
    [Tooltip("Tüp/elbow hücre kaplama oranı (1 = tam hücre; darlık sprite padding'inden gelir).")]
    [SerializeField, Range(0.5f, 1.2f)] private float tubeCellScale = 1f;

    [Header("Tube — Flow (akan akım)")]
    [Tooltip("Kanal boyunca akan enerji sprite'ı (ince parlak çizgi, yatay). Boşsa akış çizilmez.")]
    [SerializeField] private Sprite flowDashSprite;
    [Tooltip("Akış için opsiyonel additive UI material (parlama). Boşsa normal alpha.")]
    [SerializeField] private Material flowMaterial;
    [SerializeField] private Color flowColor = new Color(0.4f, 0.95f, 1f, 1f);
    [Tooltip("Akış hızı (px/sn). Negatif = ters yön.")]
    [SerializeField] private float flowSpeed = 220f;
    [Tooltip("Aynı anda kanalda görünen akış çizgisi sayısı.")]
    [SerializeField, Range(1, 12)] private int flowDashCount = 4;
    [Tooltip("Akış çizgisi UZUNLUĞU (flow yönünde) / hücre.")]
    [SerializeField, Range(0.2f, 1.5f)] private float flowDashLength = 0.7f;
    [Tooltip("Akış çizgisi KALINLIĞI / hücre (mavi kanala sığmalı).")]
    [SerializeField, Range(0.05f, 0.6f)] private float flowThicknessRatio = 0.22f;
    [Tooltip("Köşe yuvarlama (0..0.49 hücre): akış köşeyi bu yarıçapla döner (snap yerine kavis). 0 = keskin.")]
    [SerializeField, Range(0f, 0.49f)] private float flowCornerRound = 0.4f;

    [Header("Tube — Hit FX (elektrik boşalması)")]
    [Tooltip("Magnet hit alınca uçta elektrik animasyonu oynasın mı.")]
    [SerializeField] private bool hitFxEnabled = true;
    [Tooltip("Zikzak yıldırım rengi (elektrik: cyan/beyaz iyi durur).")]
    [SerializeField] private Color hitFxColor = new Color(0.7f, 0.95f, 1f, 1f);
    [Tooltip("Efekt ömrü (sn) — kısa/keskin.")]
    [SerializeField, Min(0.05f)] private float hitFxDuration = 0.22f;
    [Tooltip("Aynı anda kaç zikzak (1-2 iyi).")]
    [SerializeField, Range(1, 4)] private int hitBoltCount = 2;
    [Tooltip("Zikzak kırılma sayısı (segment).")]
    [SerializeField, Range(2, 12)] private int hitZigzagSegments = 5;
    [Tooltip("Zikzak GENİŞLİĞİ (U ağzı boyunca) / hücre.")]
    [SerializeField, Range(0.3f, 1.5f)] private float hitZigzagSpan = 0.9f;
    [Tooltip("Zikzak SAPMA yüksekliği / hücre (öne-arkaya kırılma).")]
    [SerializeField, Range(0.02f, 0.35f)] private float hitZigzagAmplitude = 0.1f;
    [Tooltip("Çizgi KALINLIĞI / hücre.")]
    [SerializeField, Range(0.02f, 0.2f)] private float hitBoltThickness = 0.06f;
    [Tooltip("Magnetin ÖNÜNDE ne kadar (ağız yönünde) / hücre.")]
    [SerializeField, Range(0f, 0.6f)] private float hitFrontOffset = 0.12f;
    [Tooltip("Magnet punch ölçeği (hit'te uç büyüme).")]
    [SerializeField, Range(1f, 2f)] private float hitFlashScale = 1.25f;
    [Tooltip("Hit sesi (opsiyonel). Atanırsa hit'te çalınır.")]
    [SerializeField] private AudioClip hitSfx;
    [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 0.7f;

    [Header("Drain Pulse")]
    [SerializeField, Range(0f, 1f)] private float drainTintStrength = 0.72f;
    [SerializeField, Range(0f, 1f)] private float drainGlowAlpha = 0.92f;

    private readonly List<GameObject> activeHitFxObjects = new List<GameObject>();

    // Tube runtime
    private RectTransform[] flowDashes;
    private Vector2[] pathPoints;
    private float flowDistance;
    private int activeAIdx, activeBIdx;
    // Köşeleri yuvarlanmış akış yolu (dash bunu takip eder → köşede snap yok)
    private Vector2[] smoothPts;
    private float[] smoothCum;      // kümülatif arc uzunluğu
    private float[] cellArcPos;     // her cell index'in smooth path'teki arc mesafesi (aktif aralık için)

    private int[] path;
    private int gridWidth;
    private float cellSize;

    private Image magnetAImage;
    private Image magnetBImage;
    private Image[] glowCircles;        // zincir baklaları (isim korundu: Pulse + visibility kullanır)
    private float[] linkPathPos;        // her baklanın path-index konumu (görünürlük için)
    private Color baseMagnetColor = Color.white;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Init(int[] pathCellIndices, int gridWidth, float cellSize, RectTransform parent)
    {
        path = pathCellIndices;
        this.gridWidth = gridWidth;
        this.cellSize = cellSize;

        var rt = GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;

        BuildChildren();
        RefreshGlowVisibility(0, path.Length - 1);

        // Eski stil alpha-pulse; yeni tüp stili Update()'te akan akım kullanır.
        if (style == MagnetStyle.ChainLinks)
            StartCoroutine(PulseRoutine());
    }

    private void OnDisable()
    {
        CleanupActiveHitFx();
    }

    private void OnDestroy()
    {
        CleanupActiveHitFx();
    }

    /// Called by MagnetObstacleService after a hit moves one of the endpoints.
    public void UpdatePositions(int newAIdx, int newBIdx, int prevAIdx, int prevBIdx)
    {
        RefreshGlowVisibility(newAIdx, newBIdx);

        bool aChanged = newAIdx != prevAIdx;
        Vector2 aFrom = CellCenter(path[prevAIdx]);
        Vector2 aTo   = CellCenter(path[newAIdx]);
        Vector2 bFrom = CellCenter(path[prevBIdx]);
        Vector2 bTo   = CellCenter(path[newBIdx]);

        if (aChanged)
        {
            OrientMagnet(magnetAImage, newAIdx, newAIdx + 1);   // köşeyi geçince yön güncellenir
            StartCoroutine(MoveImageRoutine(magnetAImage, aFrom, aTo));
            PlayHitFx(aTo, magnetAImage, ScreenDir(path[newAIdx], path[newAIdx + 1]));   // ağız içeri (path'e) bakar
        }
        else
        {
            OrientMagnet(magnetBImage, newBIdx, newBIdx - 1);
            StartCoroutine(MoveImageRoutine(magnetBImage, bFrom, bTo));
            PlayHitFx(bTo, magnetBImage, ScreenDir(path[newBIdx], path[newBIdx - 1]));
        }
    }

    public void SetDrainColor(Color color)
    {
        Color tint = Color.Lerp(baseMagnetColor, color, Mathf.Clamp01(drainTintStrength));
        tint.a = 1f;

        if (magnetAImage != null)
            magnetAImage.color = tint;
        if (magnetBImage != null)
            magnetBImage.color = tint;

        Color glow = Color.Lerp(Color.white, color, 0.65f);
        glow.a = Mathf.Clamp01(drainGlowAlpha);
        if (glowCircles != null)
        {
            for (int i = 0; i < glowCircles.Length; i++)
            {
                if (glowCircles[i] != null)
                    glowCircles[i].color = glow;
            }
        }
    }

    public Vector3 GetEndpointWorldPosition(int cellIndex)
    {
        if (magnetAImage != null && activeAIdx >= 0 && activeAIdx < path.Length && path[activeAIdx] == cellIndex)
            return magnetAImage.rectTransform.position;
        if (magnetBImage != null && activeBIdx >= 0 && activeBIdx < path.Length && path[activeBIdx] == cellIndex)
            return magnetBImage.rectTransform.position;
        return transform.TransformPoint(CellCenter(cellIndex));
    }

    public bool TryGetDrainRouteWorld(int entryCellIndex, int sampleCount, out Vector3 entryWorld, out Vector3 exitWorld, out Vector3[] routeWorld)
    {
        entryWorld = GetEndpointWorldPosition(entryCellIndex);
        exitWorld = entryWorld;
        routeWorld = null;

        if (path == null || path.Length < 2)
            return false;

        bool fromA = activeAIdx >= 0 && activeAIdx < path.Length && path[activeAIdx] == entryCellIndex;
        bool fromB = activeBIdx >= 0 && activeBIdx < path.Length && path[activeBIdx] == entryCellIndex;
        if (!fromA && !fromB)
            return false;

        int exitIdx = fromA ? activeBIdx : activeAIdx;
        if (exitIdx < 0 || exitIdx >= path.Length)
            return false;

        exitWorld = GetEndpointWorldPosition(path[exitIdx]);
        int count = Mathf.Max(2, sampleCount);
        routeWorld = new Vector3[count];

        if (smoothPts != null && smoothCum != null && cellArcPos != null && smoothPts.Length >= 2)
        {
            float startD = cellArcPos[Mathf.Clamp(fromA ? activeAIdx : activeBIdx, 0, cellArcPos.Length - 1)];
            float endD = cellArcPos[Mathf.Clamp(exitIdx, 0, cellArcPos.Length - 1)];

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 1f : i / (float)(count - 1);
                float d = Mathf.Lerp(startD, endD, t);
                SmoothPathAt(d, out Vector2 pos, out _);
                routeWorld[i] = transform.TransformPoint(pos);
            }

            return true;
        }

        int startIdx = fromA ? activeAIdx : activeBIdx;
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 1f : i / (float)(count - 1);
            float pathPos = Mathf.Lerp(startIdx, exitIdx, t);
            int lo = Mathf.Clamp(Mathf.FloorToInt(pathPos), 0, path.Length - 1);
            int hi = Mathf.Clamp(Mathf.CeilToInt(pathPos), 0, path.Length - 1);
            float segmentT = Mathf.Clamp01(pathPos - lo);
            Vector2 pos = Vector2.Lerp(CellCenter(path[lo]), CellCenter(path[hi]), segmentT);
            routeWorld[i] = transform.TransformPoint(pos);
        }

        return true;
    }

    // Board'un üst VFX overlay'i (break/goal FX ile aynı; tile'ların üstünde). Runtime'da bulunur/cache'lenir.
    private RectTransform _fxOverlay;
    private bool _fxOverlayResolved;
    private RectTransform ResolveFxOverlay()
    {
        if (_fxOverlayResolved) return _fxOverlay;
        _fxOverlayResolved = true;
        var board = FindFirstObjectByType<BoardController>();
        _fxOverlay = board != null ? board.BreakFxParent : null;
        return _fxOverlay;
    }

    // ── Hit FX: U-mıknatısın önünde zikzak yıldırım + magnet punch + ses ──
    private void PlayHitFx(Vector2 at, Image core, Vector2 mouthDir)
    {
        if (!hitFxEnabled) return;
        if (hitSfx != null)
            AudioSource.PlayClipAtPoint(hitSfx, Camera.main != null ? Camera.main.transform.position : Vector3.zero, hitSfxVolume);
        StartCoroutine(CoHitFx(at, core, mouthDir));
    }

    // Sprite atanmasa bile bolt çizilebilsin diye 1x1 beyaz fallback.
    private static Sprite _whiteSprite;
    private static Sprite WhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var px = new Color[4]; for (int i = 0; i < 4; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }

    private static Sprite _softCircleSprite;
    private static Sprite SoftCircleSprite()
    {
        if (_softCircleSprite != null) return _softCircleSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - d);
                alpha = alpha * alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        _softCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _softCircleSprite;
    }

    // U-mıknatısın ÖNÜNDE, ağzı boyunca (kutuplar arası) prosedürel ZİKZAK yıldırım; flicker'lı, kısa.
    private IEnumerator CoHitFx(Vector2 at, Image core, Vector2 mouthDir)
    {
        var sprite = WhiteSprite();   // prosedürel — sprite gerekmez
        RectTransform parentRt = ResolveFxOverlay() ?? (RectTransform)transform;
        int layer = parentRt.gameObject.layer;

        if (mouthDir.sqrMagnitude < 0.0001f) mouthDir = Vector2.up;
        mouthDir = mouthDir.normalized;
        Vector2 perp = new Vector2(-mouthDir.y, mouthDir.x);          // ağız boyunca (bir uçtan diğerine)
        Vector2 center = at + mouthDir * (cellSize * hitFrontOffset); // magnetin ÖNÜnde

        int bolts = Mathf.Max(1, hitBoltCount);
        int segs = Mathf.Max(2, hitZigzagSegments);
        float span = cellSize * hitZigzagSpan;
        float amp = cellSize * hitZigzagAmplitude;
        float thick = Mathf.Max(2f, cellSize * hitBoltThickness);
        float dur = Mathf.Max(0.05f, hitFxDuration);

        int total = bolts * segs;
        var rts  = new RectTransform[total];
        var imgs = new Image[total];
        for (int i = 0; i < total; i++)
        {
            var go = new GameObject("Zigzag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parentRt, false);
            go.layer = layer;
            activeHitFxObjects.Add(go);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = hitFxColor; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.SetAsLastSibling();
            rts[i] = rt; imgs[i] = img;
        }

        float t = 0f;
        try
        {
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float alpha = (Random.value < 0.78f ? 1f : 0.25f) * (1f - k * 0.6f);   // hızlı flicker + sön

                for (int b = 0; b < bolts; b++)
                {
                    Vector2 A = center - perp * (span * 0.5f);
                    Vector2 B = center + perp * (span * 0.5f);
                    Vector2 prev = A;
                    for (int s = 0; s < segs; s++)
                    {
                        float f1 = (s + 1) / (float)segs;
                        Vector2 pt = Vector2.Lerp(A, B, f1);
                        if (s < segs - 1) pt += mouthDir * Random.Range(-amp, amp);   // öne-arkaya zikzak
                        PlaceZig(rts[b * segs + s], imgs[b * segs + s], prev, pt, thick, alpha);
                        prev = pt;
                    }
                }

                if (core != null)
                    core.rectTransform.localScale = Vector3.one * (1f + (hitFlashScale - 1f) * Mathf.Sin(k * Mathf.PI));

                yield return null;
            }
        }
        finally
        {
            for (int i = 0; i < total; i++)
            {
                if (rts[i] == null) continue;
                var go = rts[i].gameObject;
                activeHitFxObjects.Remove(go);
                Destroy(go);
            }
            if (core != null) core.rectTransform.localScale = Vector3.one;
        }
    }

    private void PlaceZig(RectTransform rt, Image img, Vector2 fromA, Vector2 toA, float thick, float alpha)
    {
        Vector2 d = toA - fromA;
        Vector3 fromW = transform.TransformPoint(fromA.x, fromA.y, 0f);
        Vector3 toW   = transform.TransformPoint(toA.x,   toA.y,   0f);
        rt.position = (fromW + toW) * 0.5f;
        rt.sizeDelta = new Vector2(Mathf.Max(1f, d.magnitude), thick);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var c = img.color; c.a = alpha; img.color = c;
    }

    /// Fade-out then destroy.
    public void PlayDestroyAnimation()
    {
        CleanupActiveHitFx();
        StopAllCoroutines();
        PlayDetachedDestroyFx();
        Destroy(gameObject);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void BuildChildren()
    {
        // Tüp/zincir (alt katman). Tube ayrıca akış çizgilerini oluşturur.
        if (style == MagnetStyle.Tube)
            BuildTube();
        else
            BuildChainLinks();

        // Magnet A — yönelim path yönüne göre döndürülür (sabit flip yerine rotation).
        magnetAImage = CreateMagnetImage("MagnetA", flip: false);
        magnetAImage.rectTransform.anchoredPosition = CellCenter(path[0]);
        OrientMagnet(magnetAImage, 0, 1);

        // Magnet B.
        magnetBImage = CreateMagnetImage("MagnetB", flip: false);
        magnetBImage.rectTransform.anchoredPosition = CellCenter(path[path.Length - 1]);
        OrientMagnet(magnetBImage, path.Length - 1, path.Length - 2);
    }

    // ── Yeni tüp render ────────────────────────────────────────────────────────
    // Her İÇ path hücresine komşularına göre DÜZ tüp veya L-elbow koyar (uçlarda magnet).
    // Akış çizgileri path polyline'ı boyunca akar. Görünürlük eski sistemle aynı (glowCircles).
    private void BuildTube()
    {
        int n = path.Length;
        var imgs = new System.Collections.Generic.List<Image>();
        var poss = new System.Collections.Generic.List<float>();

        float size = cellSize * tubeCellScale;

        // ── Geçiş 1: corner'ların flip'ini hesapla ──
        var fxArr = new bool[n];
        var fyArr = new bool[n];
        var isCorner = new bool[n];
        for (int i = 1; i < n - 1; i++)
        {
            Vector2 inD  = ScreenDir(path[i - 1], path[i]);
            Vector2 outD = ScreenDir(path[i], path[i + 1]);
            if (Vector2.Dot(inD, outD) <= 0.99f)
            {
                isCorner[i] = true;
                ComputeElbowFlip(inD, outD, out fxArr[i], out fyArr[i]);
            }
        }

        // ── Geçiş 2: flip'i path boyunca YÜRÜYEREK yay — bir RUN içinde SABİT kalır (mid-run kırılma yok).
        // Corner kendi flip'ini tutar ve sonraki straight'lara taşır; ilk corner'ın flip'i öncesine de uygulanır.
        bool curFx = false, curFy = false;
        for (int i = 1; i < n - 1; i++) { if (isCorner[i]) { curFx = fxArr[i]; curFy = fyArr[i]; break; } }
        for (int i = 1; i < n - 1; i++)
        {
            if (isCorner[i]) { curFx = fxArr[i]; curFy = fyArr[i]; }
            else { fxArr[i] = curFx; fyArr[i] = curFy; }
        }

        // ── Geçiş 3: yerleştir (corner + straight AYNI flip'le → gölge/çizgi birleşir) ──
        for (int i = 1; i < n - 1; i++)   // uçlar magnet; iç hücreler tüp
        {
            Vector2 inD  = ScreenDir(path[i - 1], path[i]);
            Vector2 outD = ScreenDir(path[i], path[i + 1]);
            var scale = new Vector3(fxArr[i] ? -1f : 1f, fyArr[i] ? -1f : 1f, 1f);

            Image cell;
            if (!isCorner[i])
            {
                bool horizontal = Mathf.Abs(outD.x) > Mathf.Abs(outD.y);
                if (horizontal && straightTubeHorizontalSprite != null)
                    cell = CreateTubeCell("TubeStraightH", straightTubeHorizontalSprite, CellCenter(path[i]), 0f, size);
                else
                    cell = CreateTubeCell("TubeStraight", straightTubeSprite, CellCenter(path[i]),
                        horizontal ? AngleForDirection(outD) : 0f, size);
                cell.rectTransform.localScale = scale;
            }
            else if (elbowUseFlip)
            {
                // Üst köşe (dikey kol yukarı) +nudge, alt köşe -nudge.
                float vy = Mathf.Abs((-inD).y) > Mathf.Abs((-inD).x) ? (-inD).y : outD.y;
                Vector2 nudged = CellCenter(path[i]);
                nudged.y += (vy > 0f) ? elbowVerticalNudge : -elbowVerticalNudge;
                cell = CreateTubeCell("TubeElbow", elbowTubeSprite, nudged, 0f, size);
                cell.rectTransform.localScale = scale;
            }
            else
            {
                cell = CreateTubeCell("TubeElbow", elbowTubeSprite, CellCenter(path[i]), ElbowAngle(inD, outD), size);
            }

            imgs.Add(cell);
            poss.Add(i);
        }

        glowCircles = imgs.ToArray();     // görünürlük/destroy eski sistemle ortak
        linkPathPos = poss.ToArray();

        BuildFlow();
    }

    // Köşe iki komşuya açılır (dirToPrev=-inD, dirToNext=outD). Bisector'a göre 4 yönelimden biri.
    // Base L'nin varsayılan yönü için elbowRotationOffset ile bir kez kalibre edilir.
    // Elbow'u DÖNDÜRMEK yerine FLIP ile yerleştir: bir kol yatay bir kol dikey; aynalama kolları
    // eksen-hizalı tutar → düz tüplere tam oturur, highlight tutarlı kalır. Base L'nin yönü
    // elbowBaseRight/elbowBaseUp ile bir kez bildirilir; 4 köşe flipX/flipY kombinasyonuyla çıkar.
    private void ComputeElbowFlip(Vector2 inD, Vector2 outD, out bool flipX, out bool flipY)
    {
        Vector2 toPrev = -inD;
        Vector2 toNext = outD;

        // Köşenin yatay kol yönü (x) ve dikey kol yönü (y).
        float hx = Mathf.Abs(toPrev.x) > Mathf.Abs(toPrev.y) ? toPrev.x : toNext.x;
        float vy = Mathf.Abs(toPrev.y) > Mathf.Abs(toPrev.x) ? toPrev.y : toNext.y;

        flipX = (hx > 0f) != elbowBaseRight;
        flipY = (vy > 0f) != elbowBaseUp;
    }

    private float ElbowAngle(Vector2 inD, Vector2 outD)
    {
        // Köşe iki komşuya açılır: bisector standart açısı (CCW, +x). 4 köşe → 45/135/225/315.
        // -45 ile base'i "UP+RIGHT köşesi = 0°" kabul ederiz; base L farklıysa offset ile 90° adımlarla hizala.
        Vector2 bis = (-inD + outD);
        if (bis.sqrMagnitude < 0.0001f) return elbowRotationOffset;
        bis.Normalize();
        float bisAngle = Mathf.Atan2(bis.y, bis.x) * Mathf.Rad2Deg;   // standart (localRotation.z ile aynı çerçeve)
        return bisAngle - 45f + elbowRotationOffset;
    }

    private Image CreateTubeCell(string goName, Sprite sprite, Vector2 pos, float angleZ, float size)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;
        rt.localRotation = Quaternion.Euler(0f, 0f, angleZ);
        return img;
    }


    private void BuildFlow()
    {
        int n = path.Length;
        pathPoints = new Vector2[n];
        for (int i = 0; i < n; i++) pathPoints[i] = CellCenter(path[i]);

        activeAIdx = 0;
        activeBIdx = n - 1;

        if (flowDashSprite == null || Mathf.Approximately(flowSpeed, 0f))
        {
            flowDashes = null;
            return;
        }

        BuildSmoothPath();   // köşeleri yuvarla → dash köşede snap yapmadan döner

        flowDashes = new RectTransform[Mathf.Max(1, flowDashCount)];
        float len   = cellSize * flowDashLength;
        float thick = cellSize * flowThicknessRatio;

        for (int i = 0; i < flowDashes.Length; i++)
        {
            var go = new GameObject("FlowDash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.sprite = flowDashSprite;
            img.color  = flowColor;
            img.raycastTarget = false;
            if (flowMaterial != null) img.material = flowMaterial;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(len, thick);   // yatay dash: uzun eksen = flow yönü
            flowDashes[i] = rt;
        }
    }

    // Akan akım: dash'ler YUVARLANMIŞ akış yolu boyunca, GÜNCEL uçlar (activeAIdx..activeBIdx) arasında akar.
    private void Update()
    {
        if (style != MagnetStyle.Tube || flowDashes == null || cellArcPos == null) return;

        float aD = cellArcPos[Mathf.Clamp(activeAIdx, 0, cellArcPos.Length - 1)];
        float bD = cellArcPos[Mathf.Clamp(activeBIdx, 0, cellArcPos.Length - 1)];
        float span = bD - aD;

        if (span <= 1f)
        {
            for (int i = 0; i < flowDashes.Length; i++)
                if (flowDashes[i] != null && flowDashes[i].gameObject.activeSelf)
                    flowDashes[i].gameObject.SetActive(false);
            return;
        }

        flowDistance += flowSpeed * Time.deltaTime;
        float spacing = span / flowDashes.Length;

        for (int i = 0; i < flowDashes.Length; i++)
        {
            var rt = flowDashes[i];
            if (rt == null) continue;
            if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);

            float d = aD + Mathf.Repeat(flowDistance + i * spacing, span);
            SmoothPathAt(d, out Vector2 pos, out Vector2 dir);
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
    }

    // Hücre merkezlerinden geçen keskin polyline'ı, köşelerde quadratic-bezier ARC ile yuvarlar.
    // Böylece akış köşede pozisyon/rotasyon zıplaması yapmaz, tüpün kıvrımını takip eder.
    private void BuildSmoothPath()
    {
        int n = pathPoints.Length;
        var pts = new System.Collections.Generic.List<Vector2>();
        const int arcSeg = 6;
        float w = Mathf.Clamp(flowCornerRound, 0f, 0.49f) * cellSize;

        pts.Add(pathPoints[0]);
        for (int k = 1; k < n; k++)
        {
            Vector2 prev = pathPoints[k - 1];
            Vector2 cur  = pathPoints[k];
            bool corner = (k < n - 1)
                && Vector2.Dot((cur - prev).normalized, (pathPoints[k + 1] - cur).normalized) < 0.99f;

            if (!corner || w <= 0.001f)
            {
                pts.Add(cur);
            }
            else
            {
                Vector2 dIn  = (cur - prev).normalized;
                Vector2 dOut = (pathPoints[k + 1] - cur).normalized;
                Vector2 p0 = cur - dIn * w;    // kavis başı (giren kolda)
                Vector2 p2 = cur + dOut * w;   // kavis sonu (çıkan kolda)
                pts.Add(p0);
                for (int j = 1; j <= arcSeg; j++)
                {
                    float u = j / (float)arcSeg, omu = 1f - u;
                    pts.Add(omu * omu * p0 + 2f * omu * u * cur + u * u * p2);
                }
            }
        }

        smoothPts = pts.ToArray();
        smoothCum = new float[smoothPts.Length];
        for (int i = 1; i < smoothPts.Length; i++)
            smoothCum[i] = smoothCum[i - 1] + Vector2.Distance(smoothPts[i - 1], smoothPts[i]);

        // Her cell index → en yakın smooth noktasının arc mesafesi (aktif aralık daralınca kullanılır).
        cellArcPos = new float[n];
        for (int k = 0; k < n; k++)
        {
            float best = float.MaxValue; int bi = 0;
            for (int i = 0; i < smoothPts.Length; i++)
            {
                float dd = (smoothPts[i] - pathPoints[k]).sqrMagnitude;
                if (dd < best) { best = dd; bi = i; }
            }
            cellArcPos[k] = smoothCum[bi];
        }
    }

    private void SmoothPathAt(float d, out Vector2 pos, out Vector2 dir)
    {
        int m = smoothPts.Length;
        d = Mathf.Clamp(d, 0f, smoothCum[m - 1]);

        int s = m - 2;
        for (int i = 1; i < m; i++)
            if (smoothCum[i] >= d) { s = i - 1; break; }

        float segLen = Mathf.Max(0.0001f, smoothCum[s + 1] - smoothCum[s]);
        float t = (d - smoothCum[s]) / segLen;
        pos = Vector2.Lerp(smoothPts[s], smoothPts[s + 1], t);
        dir = smoothPts[s + 1] - smoothPts[s];
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right; else dir.Normalize();
    }

    // Chain links are drawn on the edges between path cells. A separate round
    // junction is added on turns so L-shaped corners do not depend on a diagonal
    // link to hide the join.
    private void BuildChainLinks()
    {
        int n = path.Length;
        float linkW = cellSize * chainLinkWidthRatio;
        float linkL = cellSize * (chainLinkLengthRatio + chainCornerOffset);

        var imgs = new System.Collections.Generic.List<Image>();
        var poss = new System.Collections.Generic.List<float>();

        for (int i = 0; i < n - 1; i++)
        {
            Vector2 from = CellCenter(path[i]);
            Vector2 to = CellCenter(path[i + 1]);
            Vector2 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) continue;

            float distance = dir.magnitude;
            dir /= distance;

            float angle = AngleForDirection(dir);
            float segmentLength = Mathf.Max(linkL, distance + cellSize * chainCornerOffset);
            imgs.Add(CreateLink((from + to) * 0.5f, angle, linkW, segmentLength));
            poss.Add(i + 0.5f);
        }

        float cornerSize = linkW * chainCornerScale;
        for (int i = 1; i < n - 1; i++)
        {
            Vector2 inD = ScreenDir(path[i - 1], path[i]);
            Vector2 outD = ScreenDir(path[i], path[i + 1]);
            if (inD.sqrMagnitude < 0.0001f || outD.sqrMagnitude < 0.0001f) continue;
            if (Vector2.Dot(inD, outD) > 0.99f) continue;

            imgs.Add(CreateLink(CellCenter(path[i]), 0f, cornerSize, cornerSize));
            poss.Add(i);
        }

        glowCircles = imgs.ToArray();
        linkPathPos = poss.ToArray();
    }

    private Image CreateLink(Vector2 anchoredPos, float angleZ, float w, float h)
    {
        var go = new GameObject("ChainLink",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        var img = go.GetComponent<Image>();
        img.sprite = glowCircleSprite;
        img.color  = glowColor;
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = anchoredPos;
        rt.localRotation = Quaternion.Euler(0f, 0f, angleZ);
        return img;
    }

    private Image CreateMagnetImage(string goName, bool flip)
    {
        var go = new GameObject(goName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        if (flip)
            go.transform.localScale = new Vector3(-1f, 1f, 1f);

        var img = go.GetComponent<Image>();
        img.sprite = magnetSprite;
        img.color = baseMagnetColor;
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(cellSize, cellSize);

        return img;
    }

    private void RefreshGlowVisibility(int aIdx, int bIdx)
    {
        // Akış da aynı güncel aralıkta akar.
        activeAIdx = aIdx;
        activeBIdx = bIdx;

        // Bakla SADECE güncel uçlar (aIdx,bIdx) ARASINDA görünür; uçlarda magnet sprite var.
        // Küçüldükçe (aIdx↑ / bIdx↓) dışarıda kalan baklalar gizlenir. linkPathPos = path-index konumu.
        for (int i = 0; i < glowCircles.Length; i++)
        {
            float p = linkPathPos[i];
            glowCircles[i].gameObject.SetActive(p > aIdx && p < bIdx);
        }
    }

    // Uç magnet'i, bağlandığı komşu hücreye (içeri) doğru yönlendirir: U'nun ağzı path yönüne bakar.
    // Base sprite ağzı YUKARI bakar (∪). endpointIdx/neighborIdx = path[] içindeki indexler.
    private void OrientMagnet(Image img, int endpointIdx, int neighborIdx)
    {
        if (img == null) return;
        if (endpointIdx < 0 || endpointIdx >= path.Length) return;
        if (neighborIdx < 0 || neighborIdx >= path.Length) return;

        int eCell = path[endpointIdx];
        int nCell = path[neighborIdx];
        int dx = (nCell % gridWidth) - (eCell % gridWidth);
        int dy = (nCell / gridWidth) - (eCell / gridWidth);   // grid y aşağı artar

        // UP(0,1)'i hedef ekran yönüne (dx, -dy) çeviren Z dönüşü: Atan2(-dx, -dy).
        // down → 180° (∩), sol → 90°, sağ → -90°, up → 0° (∪).
        float angle = Mathf.Atan2(-dx, -dy) * Mathf.Rad2Deg;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // İki hücre arası birim EKRAN yönü (grid y aşağı artar → ekran y = -dy).
    private Vector2 ScreenDir(int fromCell, int toCell)
    {
        int dx = (toCell % gridWidth) - (fromCell % gridWidth);
        int dy = (toCell / gridWidth) - (fromCell / gridWidth);
        var v = new Vector2(dx, -dy);
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.zero;
    }

    private float AngleForDirection(Vector2 dir)
    {
        return Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
    }

    private Vector2 CellCenter(int cellIndex)
    {
        int cx = cellIndex % gridWidth;
        int cy = cellIndex / gridWidth;
        return new Vector2(cx * cellSize + cellSize * 0.5f, -(cy * cellSize + cellSize * 0.5f));
    }

    private IEnumerator MoveImageRoutine(Image img, Vector2 from, Vector2 to)
    {
        if (img == null) yield break;
        var rt = img.rectTransform;
        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveDuration));
            rt.anchoredPosition = Vector2.Lerp(from, to, k);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    private IEnumerator PulseRoutine()
    {
        float half = pulseDuration * 0.5f;
        while (true)
        {
            float t = 0f;
            while (t < pulseDuration)
            {
                t += Time.deltaTime;
                float k = t < half
                    ? Mathf.Clamp01(t / half)
                    : 1f - Mathf.Clamp01((t - half) / half);
                float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, k);

                foreach (var circle in glowCircles)
                {
                    if (circle == null || !circle.gameObject.activeSelf) continue;
                    var c = circle.color;
                    c.a = alpha;
                    circle.color = c;
                }
                yield return null;
            }
        }
    }

    private void PlayDetachedDestroyFx()
    {
        var parent = ResolveFxOverlay() ?? (transform.parent as RectTransform);
        if (parent == null)
            return;

        var runnerGo = new GameObject("MagnetDestroyFx", typeof(RectTransform), typeof(MagnetDestroyFxRunner));
        runnerGo.transform.SetParent(parent, false);
        runnerGo.transform.SetAsLastSibling();
        runnerGo.layer = parent.gameObject.layer;

        var runnerRt = runnerGo.GetComponent<RectTransform>();
        runnerRt.anchorMin = runnerRt.anchorMax = runnerRt.pivot = new Vector2(0.5f, 0.5f);
        runnerRt.anchoredPosition = Vector2.zero;
        runnerRt.sizeDelta = Vector2.zero;

        var shards = new List<RectTransform>();
        var shardImages = new List<Image>();
        var shardVelocities = new List<Vector2>();
        var shardSpins = new List<float>();
        var flashRts = new List<RectTransform>();
        var flashImages = new List<Image>();

        SpawnDestroyShards(magnetAImage, runnerRt, shards, shardImages, shardVelocities, shardSpins);
        SpawnDestroyShards(magnetBImage, runnerRt, shards, shardImages, shardVelocities, shardSpins);
        SpawnDestroyFlash(magnetAImage, runnerRt, flashRts, flashImages);
        SpawnDestroyFlash(magnetBImage, runnerRt, flashRts, flashImages);

        if (shards.Count == 0 && flashRts.Count == 0)
        {
            Destroy(runnerGo);
            return;
        }

        runnerGo.GetComponent<MagnetDestroyFxRunner>().Play(
            shards,
            shardImages,
            shardVelocities,
            shardSpins,
            flashRts,
            flashImages,
            Mathf.Max(0.1f, destroyShardFallDuration),
            destroyShardGravity);
    }

    private void SpawnDestroyShards(
        Image source,
        RectTransform parent,
        List<RectTransform> shards,
        List<Image> shardImages,
        List<Vector2> shardVelocities,
        List<float> shardSpins)
    {
        if (source == null || source.rectTransform == null)
            return;

        if (parent == null)
            return;

        Sprite shardSprite = ResolveDestroyShardSprite(source);
        if (shardSprite == null)
            return;

        int count = Mathf.Clamp(destroyShardCountPerMagnet, 1, 4);
        Vector3 sourceWorld = source.rectTransform.position;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("MagnetShard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            go.layer = parent.gameObject.layer;

            var img = go.GetComponent<Image>();
            img.sprite = shardSprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            Color c = Color.Lerp(source.color, Color.white, Random.Range(0.25f, 0.55f));
            c.a = 1f;
            img.color = c;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            float w = cellSize * Random.Range(0.95f, 1.25f);
            float h = cellSize * Random.Range(0.8f, 1.05f);
            rt.sizeDelta = new Vector2(w, h);
            rt.position = sourceWorld;
            Vector2 offset = Random.insideUnitCircle * (cellSize * 0.52f);
            rt.anchoredPosition += offset;
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Vector2 outward = offset.sqrMagnitude > 0.001f ? offset.normalized : Random.insideUnitCircle.normalized;
            float side = Random.Range(-destroyShardSideKick, destroyShardSideKick) + outward.x * destroyShardSideKick;
            float up = Random.Range(destroyShardLaunchUp * 0.55f, destroyShardLaunchUp);

            shards.Add(rt);
            shardImages.Add(img);
            shardVelocities.Add(new Vector2(side, up + Mathf.Max(0f, outward.y) * destroyShardLaunchUp * 0.35f));
            shardSpins.Add(Random.Range(-480f, 480f));
        }
    }

    private Sprite ResolveDestroyShardSprite(Image source)
    {
        if (source != null)
        {
            if (source.overrideSprite != null)
                return source.overrideSprite;
            if (source.sprite != null)
                return source.sprite;
        }

        if (magnetAImage != null)
        {
            if (magnetAImage.overrideSprite != null)
                return magnetAImage.overrideSprite;
            if (magnetAImage.sprite != null)
                return magnetAImage.sprite;
        }

        if (magnetBImage != null)
        {
            if (magnetBImage.overrideSprite != null)
                return magnetBImage.overrideSprite;
            if (magnetBImage.sprite != null)
                return magnetBImage.sprite;
        }

        return magnetSprite;
    }

    private void SpawnDestroyFlash(
        Image source,
        RectTransform parent,
        List<RectTransform> flashRts,
        List<Image> flashImages)
    {
        if (source == null || source.rectTransform == null)
            return;

        if (parent == null)
            return;

        var go = new GameObject("MagnetDestroyFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        go.layer = parent.gameObject.layer;

        var img = go.GetComponent<Image>();
        img.sprite = SoftCircleSprite();
        img.raycastTarget = false;
        img.preserveAspect = false;
        img.color = new Color(0.75f, 0.95f, 1f, 0.65f);

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = source.rectTransform.position;
        rt.sizeDelta = Vector2.one * Mathf.Max(1f, cellSize * 2.8f);
        rt.localScale = Vector3.one * 0.5f;

        flashRts.Add(rt);
        flashImages.Add(img);
    }

    private void SpawnDestroyShards(
        Image source,
        List<RectTransform> shards,
        List<Image> shardImages,
        List<Vector2> shardVelocities,
        List<float> shardSpins)
    {
        SpawnDestroyShards(source, ResolveFxOverlay() ?? (transform.parent as RectTransform), shards, shardImages, shardVelocities, shardSpins);
    }

    private void CleanupActiveHitFx()
    {
        for (int i = activeHitFxObjects.Count - 1; i >= 0; i--)
        {
            var go = activeHitFxObjects[i];
            if (go != null)
                Destroy(go);
        }

        activeHitFxObjects.Clear();
        ResetMagnetPunchScales();
    }

    private void ResetMagnetPunchScales()
    {
        if (magnetAImage != null)
            magnetAImage.rectTransform.localScale = Vector3.one;
        if (magnetBImage != null)
            magnetBImage.rectTransform.localScale = Vector3.one;
    }
}
