using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Harika sahnesi açıldıktan sonra arka planda gezinen ambient robot.
/// Waypoint'ler arası sabit hızla yürür, gidiş yönüne göre flip olur, hafif zıplar (bob),
/// noktada kısa durur. İsteğe bağlı yürüme frame animasyonu.
/// Konteyner (bu obje) yolu takip eder; görsel çocuk (visual) bob/flip/frame yapar.
/// [[project_wonder_reveal_background]]
/// </summary>
public class WonderAmbientAgent : MonoBehaviour
{
    [Header("Yol")]
    [Tooltip("Sırayla gidilecek noktalar (RectTransform). En az 2 tane.")]
    public RectTransform[] waypoints;
    [Tooltip("Yürüme hızı (px/sn)")]
    public float speed = 90f;
    [Tooltip("Açık: uçlarda geri döner (ping-pong). Kapalı: başa döner (loop).")]
    public bool pingPong = true;
    [Tooltip("Her noktada bekleme süresi (sn)")]
    public float pauseAtPoint = 0.4f;

    [Header("Görsel (çocuk)")]
    public RectTransform visual;
    public Image visualImage;
    public float bobAmplitude = 8f;
    public float bobFrequency = 6f;

    public enum FacingMode
    {
        DirectionalFrontBack, // gidiş yönüne göre front/back seti (robotlar)
        SideMirror,           // walkFrames + yatay aynalama (dron/yan görünüm)
    }

    [Header("Yön Modu")]
    [Tooltip("Front/Back: ileri giderken front, dönüşte back. SideMirror: walkFrames + aynalama.")]
    public FacingMode facingMode = FacingMode.DirectionalFrontBack;

    [Header("Yürüme Kareleri")]
    [Tooltip("İLERİ giderken (A→B→…) tekrar eden kareler — BİZE DÖNÜK")]
    public Sprite[] frontFrames;
    [Tooltip("DÖNÜŞTE (…→A) tekrar eden kareler — ARKASI DÖNÜK")]
    public Sprite[] backFrames;
    [Tooltip("SideMirror modu için yan kareler (robotlarda gerekmez)")]
    public Sprite[] walkFrames;
    public float walkFps = 5f;
    [Tooltip("SideMirror: sağa/sola giderken yatay aynala")]
    public bool mirrorBySide = false;

    [Header("Yol (data-driven)")]
    [Tooltip("Anchored nokta listesi. Doluysa waypoints yerine BUNU kullanır (data-driven).")]
    public Vector2[] pathPoints;

    [Header("Başlangıç")]
    [Tooltip("Açık: hemen yürür (test). Kapalı: sahne açılınca WonderRevealView başlatır.")]
    public bool startWalking = true;

    RectTransform _rt;
    int _idx = 1;
    int _dir = 1;
    float _pauseT;
    float _walkT;
    float _baseVisualY;
    bool _walking;

    int PointCount => (pathPoints != null && pathPoints.Length >= 2)
        ? pathPoints.Length
        : (waypoints?.Length ?? 0);

    Vector2 Point(int i) => (pathPoints != null && pathPoints.Length >= 2)
        ? pathPoints[i]
        : (waypoints[i] != null ? waypoints[i].anchoredPosition : _rt.anchoredPosition);

    void Awake()
    {
        _rt = (RectTransform)transform;
        if (visual != null) _baseVisualY = visual.anchoredPosition.y;
        _walking = startWalking;

        // Editör waypoint'leri varsa Vector2 yola bake et (yoksa pathPoints kalır)
        if ((pathPoints == null || pathPoints.Length < 2) && waypoints != null && waypoints.Length >= 2)
        {
            pathPoints = new Vector2[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
                pathPoints[i] = waypoints[i] != null ? waypoints[i].anchoredPosition : Vector2.zero;
        }

        // Başlangıç noktasına otur, bir sonrakine yönel
        if (PointCount >= 2)
        {
            _rt.anchoredPosition = Point(0);
            _idx = 1;
        }

        // Editör waypoint işaretlerini oyunda gizle
        if (waypoints != null)
            foreach (var w in waypoints)
            {
                if (w == null) continue;
                var img = w.GetComponent<Image>();
                if (img != null) img.enabled = false;
            }
    }

    /// <summary>Sahne açılınca dışarıdan çağrılır (reveal %100).</summary>
    public void BeginWalking() => _walking = true;
    public void StopWalking() => _walking = false;

    void Update()
    {
        if (!_walking || PointCount < 2) return;

        if (_pauseT > 0f)
        {
            _pauseT -= Time.deltaTime;
            UpdateVisual(Vector2.zero, false);
            return;
        }

        var target = Point(_idx);
        var pos = _rt.anchoredPosition;
        var np = Vector2.MoveTowards(pos, target, speed * Time.deltaTime);
        _rt.anchoredPosition = np;

        UpdateVisual(np - pos, true);

        if (Vector2.Distance(np, target) < 0.5f)
        {
            _pauseT = pauseAtPoint;
            Advance();
        }
    }

    void Advance()
    {
        int n = PointCount;
        if (pingPong)
        {
            int next = _idx + _dir;
            if (next < 0 || next >= n)
            {
                _dir = -_dir;
                next = _idx + _dir;
            }
            _idx = Mathf.Clamp(next, 0, n - 1);
        }
        else
        {
            _idx = (_idx + 1) % n;
        }
    }

    void UpdateVisual(Vector2 delta, bool walking)
    {
        if (visual == null) return;

        // Zıplama (bob)
        float bob = walking ? Mathf.Abs(Mathf.Sin(Time.time * bobFrequency)) * bobAmplitude : 0f;
        var p = visual.anchoredPosition;
        p.y = _baseVisualY + bob;
        visual.anchoredPosition = p;

        // Yatay aynalama — yalnız SideMirror modunda
        if (facingMode == FacingMode.SideMirror && mirrorBySide && Mathf.Abs(delta.x) > 0.0001f)
        {
            var s = visual.localScale;
            s.x = Mathf.Abs(s.x) * (delta.x > 0f ? 1f : -1f);
            visual.localScale = s;
        }

        // Frame animasyonu
        if (!walking || visualImage == null) return;
        var set = ChooseFrameSet();
        if (set == null || set.Length == 0) return;
        _walkT += Time.deltaTime;
        int fi = Mathf.FloorToInt(_walkT * walkFps) % set.Length;
        if (set[fi] != null) visualImage.sprite = set[fi];
    }

    Sprite[] ChooseFrameSet()
    {
        // SideMirror (dron/yan): tek set, walkFrames > front > back.
        if (facingMode == FacingMode.SideMirror)
        {
            if (walkFrames != null && walkFrames.Length > 0) return walkFrames;
            if (frontFrames != null && frontFrames.Length > 0) return frontFrames;
            return backFrames;
        }

        // Front/Back: gidiş yönüne göre. İleri (_dir>=0)=front, dönüş=back.
        var set = _dir >= 0 ? frontFrames : backFrames;
        if (set == null || set.Length == 0) set = _dir >= 0 ? backFrames : frontFrames;
        if (set == null || set.Length == 0) set = walkFrames;
        return set;
    }
}

