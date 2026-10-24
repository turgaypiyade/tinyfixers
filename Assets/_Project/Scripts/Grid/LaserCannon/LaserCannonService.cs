using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lazer topu (ObstacleId.LaserCannon) davranışı. Board'a runtime eklenir (Ensure), görünümler
/// GridSpawner.DrawObstacleImage'dan kaydolur.
///
/// Döngü (her top için, hamle sonunda — BoardController.OnPlayerMoveResolved):
///   1) Elinde hedef varsa ve hedef hâlâ kullanılmamış bir special ise → kafese alır (SpecialLockCoordinator,
///      releaseWhenCovered). Kafesli special başka bir special'ın etki alanına girince hamle sonunda serbest kalır.
///   2) Hedefi yoksa en yakın, kilitsiz, başka topun seçmediği special'ı seçer: kule döner, geniş saydam lazer
///      ve special'ın arkasında parlak hale çıkar; hedef düşerse lazer takip eder.
/// Örtü altındaki top (en üst katman değilse) pasiftir. Boşta kule ara ara rastgele döner.
/// Tüm görseller tek Update döngüsünde sürülür; lazer/hale nesneleri top başına bir kez kurulup yeniden kullanılır.
/// </summary>
public sealed class LaserCannonService : MonoBehaviour
{
    private const string CageSpritePath = "Magnet/SpecialLocker";
    private const int CageWindowMoves = 1_000_000;   // zaman aşımı yok: kafesi yalnız etki alanı (veya topun yıkılması) açar
    private const float AimSmoothTime = 0.2f;
    private const float AimMaxSpeed = 380f;
    private const float LockBurstSeconds = 0.5f;    // hedefe ilk varışta güçlü parlama
    private const float BeamSpreadTan = 0.026f;     // ~1.5° açıyla genişler (her yana)
    private const float BeamMuzzleWidth = 0.32f;    // namlu ucunda bant genişliği (hücre)
    private const float BeamExtendSeconds = 0.6f;   // lazer namludan hedefe bu sürede uzanır
    private const float IdleSmoothTime = 0.35f;
    private const float IdleMaxSpeed = 220f;
    private const float BeamOnAngle = 10f;
    private const float BeamFadeIn = 0.18f;
    private const float BeamFadeOut = 0.22f;
    private const float FireFlashSeconds = 0.3f;
    private const float BreakHoldSeconds = 0.16f;
    private const float CageBreakSeconds = 0.2f;

    // Lazer yeşili.
    private static readonly Color BeamGlow = new Color(0.2f, 1f, 0.3f, 1f);
    private static readonly Color BeamCore = new Color(0.85f, 1f, 0.85f, 1f);
    private static readonly Color HaloColor = new Color(0.35f, 1f, 0.4f, 1f);

    private sealed class Cannon
    {
        public int origin;
        public int x, y;            // sol üst hücre
        public string owner;        // kilit sahibi etiketi (top başına)
        public LaserCannonView view;
        public TileView target;
        public int targetVersion;
        public Vector3 lastTargetWorld;
        public float angle, angleVelocity, idleGoal, nextIdleTurn;
        public float beam;          // 0..1 görünürlük
        public float extend;        // 0..1 namludan hedefe uzama
        public float fireFlash;     // kafes atışı parlaması (s)
        public RectTransform beamGlow, beamCore, halo;
        public LaserBeamGraphic beamGlowImg, beamCoreImg;
        public Image haloImg, ringImg, reticleImg;
        public RectTransform ring, reticle;
        public bool lockedOn;       // lazer hedefe bir kez vardı (parlama tek sefer)
        public float lockBurst;     // varış parlaması kalan süre (s)
    }

    private sealed class Cage
    {
        public TileView tile;
        public int version;
        public RectTransform rt;
        public Image img;
        public float age;
        public float breakT = -1f;   // ≥0: kafes açılıyor (şişip söner)
    }

    private static Sprite beamSprite;
    private static Sprite reticleSprite;
    private static Sprite cageSprite;
    private static Sprite fallbackCageSprite;
    private static bool triedCageSprite;
    private static readonly Dictionary<Texture, Sprite[]> shardCache = new();

    private readonly List<Cannon> cannons = new();
    private readonly List<Cage> cages = new();
    private readonly Stack<Cage> cagePool = new();
    private readonly List<TileView> candidates = new();
    private readonly HashSet<TileView> freedThisMove = new();
    private readonly List<Vector2Int> cellBuffer = new(4);
    private BoardController board;
    private ObstacleStateService boundState;
    private bool subscribed;

    public static LaserCannonService Ensure(BoardController board)
    {
        if (board == null) return null;
        if (!board.TryGetComponent<LaserCannonService>(out var service))
            service = board.gameObject.AddComponent<LaserCannonService>();
        service.Bind(board);
        return service;
    }

    /// Level kurulumu başında: önceki level'ın topları/kafesleri bırakılır (görünümleri spawner temizler).
    public void Clear()
    {
        for (int i = 0; i < cannons.Count; i++) DestroyCannonFx(cannons[i]);
        cannons.Clear();
        for (int i = 0; i < cages.Count; i++)
            if (cages[i].rt != null) Destroy(cages[i].rt.gameObject);
        cages.Clear();
        cagePool.Clear();
        freedThisMove.Clear();
    }

    public void Register(int origin, LaserCannonView view)
    {
        if (view == null || board == null) return;
        for (int i = 0; i < cannons.Count; i++)
        {
            if (cannons[i].origin != origin) continue;
            cannons[i].view = view;
            return;
        }
        float start = Random.Range(-40f, 40f);
        view.SetAngle(start);
        cannons.Add(new Cannon
        {
            origin = origin,
            x = origin % board.Width,
            y = origin / board.Width,
            owner = "LaserCannon:" + origin,
            view = view,
            angle = start,
            idleGoal = start,
            nextIdleTurn = Time.time + Random.Range(1.5f, 3.5f),
        });
    }

    private void Bind(BoardController target)
    {
        if (board != target) Unsubscribe();
        board = target;
        if (subscribed || board == null) return;
        board.OnPlayerMoveResolved += HandleMoveResolved;
        boundState = board.ObstacleStateService;
        if (boundState != null) boundState.OnObstacleDestroyed += HandleObstacleDestroyed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (board != null) board.OnPlayerMoveResolved -= HandleMoveResolved;
        if (boundState != null) boundState.OnObstacleDestroyed -= HandleObstacleDestroyed;
        boundState = null;
        subscribed = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        Clear();
    }

    // ── Kurallar ────────────────────────────────────────────────────────────────

    // Top yalnız en üst katmansa çalışır (üstüne örtü gelebilir; örtü kalkana dek pasif).
    private bool IsActive(Cannon c)
    {
        var obs = board != null ? board.ObstacleStateService : null;
        if (obs == null || c.view == null) return false;
        for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
                if (obs.GetObstacleIdAt(c.x + dx, c.y + dy) != ObstacleId.LaserCannon) return false;
        return true;
    }

    private static bool IsTargetValid(Cannon c)
    {
        var t = c.target;
        return t != null && t && t.LifetimeVersion == c.targetVersion && t.gameObject.activeInHierarchy
               && t.GetSpecial() != TileSpecial.None && !t.IsSpecialLocked;
    }

    private void HandleMoveResolved()
    {
        if (cannons.Count == 0 || board == null) return;

        // 1) Bir hamle boyunca kullanılmayan hedefleri kafese al.
        for (int i = 0; i < cannons.Count; i++)
        {
            var c = cannons[i];
            if (!IsActive(c) || !IsTargetValid(c)) { DropTarget(c, flash: false); continue; }
            CageTarget(c);
        }

        // 2) Boştaki toplar yeni hedef seçer (iki top aynı special'ı seçmez; az önce kafesten
        //    kurtulan special bu hamle yeniden seçilmez).
        CollectCandidates();
        for (int i = 0; i < cannons.Count; i++)
        {
            var c = cannons[i];
            if (c.target != null || !IsActive(c)) continue;
            var pick = PickNearest(c);
            if (pick == null) continue;
            c.target = pick;
            c.targetVersion = pick.LifetimeVersion;
            candidates.Remove(pick);
        }
        freedThisMove.Clear();
    }

    private void CollectCandidates()
    {
        candidates.Clear();
        var tiles = board.Tiles;
        if (tiles == null) return;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var t = tiles[x, y];
                if (t == null || !t || t.GetSpecial() == TileSpecial.None || t.IsSpecialLocked) continue;
                if (freedThisMove.Contains(t) || IsTargetedByAny(t)) continue;
                candidates.Add(t);
            }
    }

    private bool IsTargetedByAny(TileView t)
    {
        for (int i = 0; i < cannons.Count; i++)
            if (cannons[i].target == t) return true;
        return false;
    }

    private TileView PickNearest(Cannon c)
    {
        TileView best = null;
        float bestD = float.MaxValue;
        float cx = c.x + 0.5f, cy = c.y + 0.5f;   // 2x2 merkez
        for (int i = 0; i < candidates.Count; i++)
        {
            var t = candidates[i];
            float dx = t.X - cx, dy = t.Y - cy;
            float d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    private void CageTarget(Cannon c)
    {
        var tile = c.target;
        var locks = board.SpecialLocks;
        if (locks == null) { DropTarget(c, flash: false); return; }
        locks.LockSpecial(tile, CageWindowMoves, onTimeout: null, onReleased: HandleCageReleased,
            owner: c.owner, releaseWhenCovered: true);
        ShowCage(tile);
        DropTarget(c, flash: true);
    }

    private void HandleCageReleased(TileView tile)
    {
        if (tile != null) freedThisMove.Add(tile);
        // Görsel, Update'te kilit bayrağı düşünce kalkar.
    }

    private void HandleObstacleDestroyed(int origin, ObstacleId id)
    {
        if (id != ObstacleId.LaserCannon) return;
        for (int i = cannons.Count - 1; i >= 0; i--)
        {
            var c = cannons[i];
            if (c.origin != origin) continue;
            DropTarget(c, flash: false);
            // Top yıkılınca kafesledikleri serbest kalır (patlamaz).
            board?.SpecialLocks?.ReleaseOwnedBy(c.owner);
            DestroyCannonFx(c);
            cannons.RemoveAt(i);
        }
    }

    private void DropTarget(Cannon c, bool flash)
    {
        if (c.target != null && IsTargetValid(c)) c.lastTargetWorld = TileCenterWorld(c.target);
        c.target = null;
        c.idleGoal = c.angle;   // boşta eski hedef açısına geri dönmesin
        if (flash && c.beam > 0f) c.fireFlash = FireFlashSeconds;
        c.lockedOn = false;
        ParkHalo(c);
    }

    // ── Görsel döngü ────────────────────────────────────────────────────────────

    private void Update()
    {
        if (board == null) return;
        if (cages.Count > 0) UpdateCages();
        if (cannons.Count == 0) return;

        float dt = Time.deltaTime;
        for (int i = 0; i < cannons.Count; i++)
        {
            var c = cannons[i];
            if (c.view == null) continue;
            bool active = IsActive(c);
            if (c.target != null && (!active || !IsTargetValid(c))) DropTarget(c, flash: false);

            bool aiming = c.target != null;
            float goal;
            if (aiming)
            {
                c.lastTargetWorld = TileCenterWorld(c.target);
                goal = AngleTo(c, c.lastTargetWorld);
                c.angle = Mathf.SmoothDampAngle(c.angle, goal, ref c.angleVelocity, AimSmoothTime, AimMaxSpeed, dt);
            }
            else
            {
                if (active && Time.time >= c.nextIdleTurn)
                {
                    c.idleGoal = c.angle + Random.Range(-130f, 130f);
                    c.nextIdleTurn = Time.time + Random.Range(2.5f, 5f);
                }
                goal = c.idleGoal;
                c.angle = Mathf.SmoothDampAngle(c.angle, goal, ref c.angleVelocity, IdleSmoothTime, IdleMaxSpeed, dt);
            }
            c.view.SetAngle(c.angle);

            bool beamOn = aiming && Mathf.Abs(Mathf.DeltaAngle(c.angle, goal)) < BeamOnAngle;
            c.beam = Mathf.MoveTowards(c.beam, beamOn ? 1f : 0f, dt / (beamOn ? BeamFadeIn : BeamFadeOut));
            if (beamOn) c.extend = Mathf.Min(1f, c.extend + dt / BeamExtendSeconds);
            else if (c.beam <= 0f && c.fireFlash <= 0f) c.extend = 0f;
            if (c.fireFlash > 0f) c.fireFlash = Mathf.Max(0f, c.fireFlash - dt);
            // Seçim anı: lazer special'a ilk vardığında güçlü parlama (hale + halka dalgası).
            if (aiming && !c.lockedOn && c.extend >= 1f)
            {
                c.lockedOn = true;
                c.lockBurst = LockBurstSeconds;
            }
            if (c.lockBurst > 0f) c.lockBurst = Mathf.Max(0f, c.lockBurst - dt);
            UpdateLockRing(c);
            UpdateBeam(c);
            if (aiming) UpdateHalo(c);
        }
    }

    private float AngleTo(Cannon c, Vector3 world)
    {
        var rt = c.view.Rect;
        Vector2 dir = (Vector2)rt.InverseTransformPoint(world) - rt.rect.center;
        if (dir.sqrMagnitude < 0.01f) return c.angle;
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;   // namlu sprite'ta yukarı bakar
    }

    private static Vector3 TileCenterWorld(TileView t)
    {
        var rt = (RectTransform)t.transform;
        return rt.TransformPoint(rt.rect.center);
    }

    private void UpdateBeam(Cannon c)
    {
        float flash = c.fireFlash / FireFlashSeconds;
        float alpha = Mathf.Max(c.beam, flash);
        if (alpha <= 0.001f)
        {
            if (c.beamGlow != null && c.beamGlow.gameObject.activeSelf)
            {
                c.beamGlow.gameObject.SetActive(false);
                c.beamCore.gameObject.SetActive(false);
            }
            return;
        }
        if (!EnsureBeam(c)) return;
        if (!c.beamGlow.gameObject.activeSelf)
        {
            c.beamGlow.gameObject.SetActive(true);
            c.beamCore.gameObject.SetActive(true);
        }

        var root = (RectTransform)c.beamGlow.parent;
        Vector2 a = root.InverseTransformPoint(c.view.MuzzleWorld);
        Vector2 b = root.InverseTransformPoint(c.lastTargetWorld);
        Vector2 d = b - a;
        float k = 1f - c.extend;
        float len = d.magnitude * (1f - k * k * k);   // ease-out: hızlı çıkar, hedefe yumuşak varır
        float cell = CellSizeIn(root);
        float rot = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float pulse = 0.9f + 0.1f * Mathf.Sin(Time.time * 6f);

        // Geniş, düz, şeffaf bant (taşlar altından görünür); parlak çekirdek yalnız kafes atışında çakar.
        // Namluda dar başlar, hedefe doğru çok küçük açıyla (~1.5°) genişler.
        float w0 = cell * Mathf.Lerp(BeamMuzzleWidth, BeamMuzzleWidth * 1.3f, flash);
        float w1 = w0 + 2f * len * BeamSpreadTan;
        Place(c.beamGlow, a, rot);
        Place(c.beamCore, a, rot);
        c.beamGlowImg.SetShape(len, w0, w1);
        c.beamCoreImg.SetShape(len, w0 * 0.3f, w1 * 0.3f);
        c.beamGlowImg.color = FlightTrailFx.WithAlpha(BeamGlow, alpha * 0.5f * pulse + flash * 0.2f);
        c.beamCoreImg.color = FlightTrailFx.WithAlpha(BeamCore, flash * 0.7f);
    }

    private static void Place(RectTransform rt, Vector2 from, float degrees)
    {
        rt.localPosition = from;
        rt.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    // Varış halkası: special'ın üstünde (taşların üstünde) genişleyip sönen yeşil halka; special'ı örtmez.
    private void UpdateLockRing(Cannon c)
    {
        if (c.lockBurst <= 0f)
        {
            if (c.ring != null && c.ring.gameObject.activeSelf) c.ring.gameObject.SetActive(false);
            return;
        }
        var root = board.BreakFxParent;
        if (root == null) return;
        if (c.ring == null)
        {
            var sprite = Resources.Load<Sprite>("VFX/shockwave_ring");
            c.ring = FlightTrailFx.CreateImage(root, "LaserLockRing", sprite != null ? sprite : FlightTrailFx.GlowSprite(), Color.clear, 0f);
            c.ringImg = c.ring.GetComponent<Image>();
        }
        if (!c.ring.gameObject.activeSelf) c.ring.gameObject.SetActive(true);
        float k = 1f - c.lockBurst / LockBurstSeconds;          // 0 → 1
        float e = 1f - (1f - k) * (1f - k);
        float cell = CellSizeIn(root);
        c.ring.localPosition = root.InverseTransformPoint(c.lastTargetWorld);
        c.ring.sizeDelta = Vector2.one * cell * Mathf.Lerp(0.6f, 2.4f, e);
        c.ringImg.color = FlightTrailFx.WithAlpha(Color.Lerp(Color.white, HaloColor, k), (1f - k) * 0.95f);
    }

    private float CellSizeIn(RectTransform root) =>
        Mathf.Max(1f, root.InverseTransformVector(board.TilesRoot.TransformVector(Vector3.right * board.TileSize)).magnitude);

    private bool EnsureBeam(Cannon c)
    {
        if (c.beamGlow != null && c.beamCore != null) return true;
        var root = board.BreakFxParent;
        if (root == null) return false;
        c.beamGlow = CreateBeamPart(root, "LaserBeamGlow", out c.beamGlowImg);
        c.beamCore = CreateBeamPart(root, "LaserBeamCore", out c.beamCoreImg);
        return true;
    }

    private static RectTransform CreateBeamPart(RectTransform root, string name, out LaserBeamGraphic graphic)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(LaserBeamGraphic));
        go.layer = root.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);   // namlu ucundan hedefe uzanır
        rt.sizeDelta = Vector2.zero;
        graphic = go.GetComponent<LaserBeamGraphic>();
        graphic.raycastTarget = false;
        graphic.color = Color.clear;
        graphic.SetTexture(BeamSprite().texture);
        return rt;
    }

    // Hale special'ın ARKASINDA: taşın ikonundan hemen önceki kardeş olarak taşa bağlanır, düşerken onunla gider.
    private void UpdateHalo(Cannon c)
    {
        var tile = c.target;
        var icon = tile.IconImage;
        var parent = icon != null ? (RectTransform)icon.transform.parent : (RectTransform)tile.transform;
        if (c.halo == null)
        {
            c.halo = FlightTrailFx.CreateImage(parent, "LaserHalo", FlightTrailFx.GlowSprite(), Color.clear, 0f);
            c.haloImg = c.halo.GetComponent<Image>();
        }
        if (c.halo.parent != parent)
        {
            c.halo.SetParent(parent, false);
            c.halo.gameObject.layer = parent.gameObject.layer;
        }
        if (!c.halo.gameObject.activeSelf) c.halo.gameObject.SetActive(true);
        if (c.reticle == null)
        {
            c.reticle = FlightTrailFx.CreateImage(parent, "LaserReticle", ReticleSprite(), Color.clear, 0f);
            c.reticleImg = c.reticle.GetComponent<Image>();
        }
        if (c.reticle.parent != parent)
        {
            c.reticle.SetParent(parent, false);
            c.reticle.gameObject.layer = parent.gameObject.layer;
        }
        if (!c.reticle.gameObject.activeSelf) c.reticle.gameObject.SetActive(true);
        // Sıra: hale < nişangâh < ikon (ikisi de special'ın ARKASINDA). Yalnız bozulunca düzelt.
        if (icon != null)
        {
            int hi = c.halo.GetSiblingIndex(), ri = c.reticle.GetSiblingIndex(), ii = icon.transform.GetSiblingIndex();
            if (!(hi < ri && ri < ii))
            {
                c.reticle.SetSiblingIndex(icon.transform.GetSiblingIndex());
                c.halo.SetSiblingIndex(c.reticle.GetSiblingIndex());
            }
        }

        var tileRt = (RectTransform)tile.transform;
        c.halo.localPosition = parent.InverseTransformPoint(tileRt.TransformPoint(tileRt.rect.center));
        float cell = board.TileSize;
        float pulse = Mathf.Sin(Time.time * 6f);
        // Hale lazer special'a VARINCA yanar; ilk varışta güçlü parlar (büyür + beyaza çalar), sonra normal nabız.
        float arrived = Mathf.Clamp01((c.extend - 0.85f) / 0.15f);
        float burst = c.lockBurst / LockBurstSeconds;            // 1 → 0
        burst *= burst;
        c.halo.sizeDelta = Vector2.one * cell * (1.55f + 0.1f * pulse + 1.3f * burst);
        float alpha = Mathf.Max(c.beam * arrived * (0.8f + 0.15f * pulse), burst);
        c.haloImg.color = FlightTrailFx.WithAlpha(Color.Lerp(HaloColor, Color.white, 0.6f * burst), alpha);

        // Nişangâh: varışta büyükten içe "kilitlenir", sonra hafifçe nefes alır.
        c.reticle.localPosition = c.halo.localPosition;
        c.reticle.localRotation = Quaternion.identity;
        float snap = Mathf.Lerp(1f, 1.7f, burst);
        c.reticle.sizeDelta = Vector2.one * cell * (1.12f + 0.04f * pulse) * snap;
        c.reticleImg.color = FlightTrailFx.WithAlpha(Color.Lerp(HaloColor, Color.white, 0.5f * burst),
            c.beam * arrived * 0.95f);
    }

    private void ParkHalo(Cannon c)
    {
        if (c.halo == null) return;
        // Taş havuza dönebilir: haleyi/nişangâhı taştan ayır ki başka bir taşa sızmasın.
        var park = board != null ? board.BreakFxParent : null;
        if (park != null) c.halo.SetParent(park, false);
        c.halo.gameObject.SetActive(false);
        if (c.reticle != null)
        {
            if (park != null) c.reticle.SetParent(park, false);
            c.reticle.gameObject.SetActive(false);
        }
    }

    private void DestroyCannonFx(Cannon c)
    {
        if (c.beamGlow != null) Destroy(c.beamGlow.gameObject);
        if (c.beamCore != null) Destroy(c.beamCore.gameObject);
        if (c.halo != null) Destroy(c.halo.gameObject);
        if (c.ring != null) Destroy(c.ring.gameObject);
        if (c.reticle != null) Destroy(c.reticle.gameObject);
        c.beamGlow = c.beamCore = c.halo = c.ring = c.reticle = null;
    }

    // ── Kafes görseli (Magnet ile aynı sprite; taşa bağlı, düşerken onunla gider) ──

    private void ShowCage(TileView tile)
    {
        var sprite = CageSprite();
        var cage = cagePool.Count > 0 ? cagePool.Pop() : null;
        if (cage == null || cage.rt == null)
        {
            var rt = FlightTrailFx.CreateImage((RectTransform)tile.transform, "LaserCage", sprite, Color.white, 0f);
            cage = new Cage { rt = rt, img = rt.GetComponent<Image>() };
            cage.img.preserveAspect = true;
        }
        var tileRt = (RectTransform)tile.transform;
        cage.rt.SetParent(tileRt, false);
        cage.rt.gameObject.layer = tileRt.gameObject.layer;
        cage.rt.anchorMin = Vector2.zero;
        cage.rt.anchorMax = Vector2.one;
        cage.rt.offsetMin = cage.rt.offsetMax = Vector2.zero;
        cage.rt.localScale = Vector3.zero;
        cage.rt.SetAsLastSibling();
        cage.rt.gameObject.SetActive(true);
        cage.img.color = sprite != null ? Color.white : FlightTrailFx.WithAlpha(HaloColor, 0.9f);
        cage.tile = tile;
        cage.version = tile.LifetimeVersion;
        cage.age = 0f;
        cage.breakT = -1f;
        cages.Add(cage);
    }

    private void UpdateCages()
    {
        float dt = Time.deltaTime;
        for (int i = cages.Count - 1; i >= 0; i--)
        {
            var cage = cages[i];
            var t = cage.tile;
            bool sameTile = cage.rt != null && t != null && t && t.LifetimeVersion == cage.version;
            // Kafes açıldı (taş yerinde): o anda kısa şişip sönme — açılma etkiyle senkron okunur.
            if (sameTile && !t.IsSpecialLocked && cage.breakT < 0f) cage.breakT = 0f;
            if (sameTile && cage.breakT >= 0f && cage.breakT < CageBreakSeconds)
            {
                cage.breakT += dt;
                float k = Mathf.Clamp01(cage.breakT / CageBreakSeconds);
                cage.rt.localScale = Vector3.one * (1f + 0.4f * k);
                cage.img.color = FlightTrailFx.WithAlpha(Color.Lerp(Color.white, HaloColor, k), 1f - k * k);
                continue;
            }
            if (!sameTile || !t.IsSpecialLocked)
            {
                cages.RemoveAt(i);
                if (cage.rt == null) continue;
                var park = board.BreakFxParent;
                if (park != null) cage.rt.SetParent(park, false);
                cage.rt.gameObject.SetActive(false);
                cage.tile = null;
                cage.breakT = -1f;
                cagePool.Push(cage);
                continue;
            }
            cage.age += dt;
            const float grow = 0.22f;
            if (cage.age < grow + dt) cage.rt.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(cage.age / grow));
            var col = cage.img.color;
            col.a = 0.85f + Mathf.Sin(Time.time * 4f) * 0.12f;
            cage.img.color = col;
        }
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = k - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // ── Kırılma: 4. cıvata yanar, top parçalanır; parçalar uçarken hücreler kısa süre tutulur ──

    /// GridSpawner, top temizlendiğinde (cleared) çağırır. true → görünümün yok edilmesini servis üstlendi.
    public static bool TryPlayBreak(BoardController board, int origin, Image image)
    {
        if (board == null || image == null || !image.TryGetComponent<LaserCannonView>(out var view)) return false;
        var service = Ensure(board);
        service.StartCoroutine(service.CoBreak(origin, image, view));
        return true;
    }

    private IEnumerator CoBreak(int origin, Image image, LaserCannonView view)
    {
        int ox = origin % board.Width, oy = origin / board.Width;
        cellBuffer.Clear();
        for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
                cellBuffer.Add(new Vector2Int(ox + dx, oy + dy));
        var hold = board.HoldCells(cellBuffer);
        var job = board.BeginJob(BoardController.BoardJobKind.ObstacleCollapse);
        try
        {
            view.SetLit(4);
            for (float t = 0f; t < BreakHoldSeconds; t += Time.deltaTime)
            {
                if (image == null) yield break;
                float k = t / BreakHoldSeconds;
                image.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            if (image == null) yield break;

            // Su deposu parçalarıyla aynı kök (board VFX), yoksa kırılma efekti kökü.
            var root = board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null
                ? board.BoardVfxPlayer.VfxRoot : board.BreakFxParent;
            if (root != null)
            {
                Vector2 center = root.InverseTransformPoint(view.CenterWorld);
                float footprint = CellSizeIn(root) * 2f;
                var turretImg = view.Turret != null ? view.Turret.GetComponent<Image>() : null;
                StartCoroutine(CoShards(root, center, footprint, image.sprite, turretImg != null ? turretImg.sprite : null,
                    view.BoltSprite, view.Turret != null ? view.Angle : 0f));
            }
            Destroy(image.gameObject);
        }
        finally
        {
            hold?.Dispose();
            job?.Dispose();
        }
    }

    private const int ChipCount = 14;
    private static readonly Color[] ChipColors =
    {
        new Color(0.2f, 0.45f, 1f), new Color(0.92f, 0.94f, 1f), new Color(1f, 0.55f, 0.1f), new Color(0.55f, 0.35f, 0.25f),
    };

    // Patlama: beyaz-yeşil flaş + tabandan 8, kuleden 2 büyük parça + 4 turuncu cıvata + 14 küçük renkli kırıntı.
    // Hepsi tek döngüde uçar, döner, düşer ve söner (parça başına coroutine yok).
    private IEnumerator CoShards(RectTransform root, Vector2 center, float footprint, Sprite baseSprite, Sprite turretSprite,
        Sprite boltSprite, float turretAngle)
    {
        var baseShards = ShardsFor(baseSprite, false);
        var turretShards = ShardsFor(turretSprite, true);
        int n = (baseShards?.Length ?? 0) + (turretShards?.Length ?? 0) + (boltSprite != null ? 4 : 0) + ChipCount;

        var rts = new RectTransform[n];
        var imgs = new Image[n];
        var vel = new Vector2[n];
        var spin = new float[n];
        var tints = new Color[n];
        var shrink = new float[n];
        const float life = 0.85f;
        var bounds = ObstacleDebrisBounds.For(board, root);
        int idx = 0;
        void Add(Sprite s, Vector2 offset, float size, Color tint, float speed, float endScale)
        {
            var rt = FlightTrailFx.CreateImage(root, "LaserCannonShard", s, tint, size);
            rt.localPosition = center + offset;
            Vector2 dir = offset.sqrMagnitude > 1f ? offset.normalized : Random.insideUnitCircle.normalized;
            dir = (dir + Random.insideUnitCircle * 0.35f).normalized;
            vel[idx] = (dir + Vector2.up * 0.7f) * footprint * speed;
            Vector2 start = rt.localPosition, pieceSize = rt.sizeDelta;
            bounds.Fit(ref start, ref vel[idx], ref pieceSize, life);
            rt.localPosition = start;
            rt.sizeDelta = pieceSize;
            spin[idx] = Random.Range(-600f, 600f);
            rts[idx] = rt;
            imgs[idx] = rt.GetComponent<Image>();
            imgs[idx].preserveAspect = true;
            tints[idx] = tint;
            shrink[idx] = endScale;
            idx++;
        }
        if (baseShards != null)
            for (int i = 0; i < baseShards.Length; i++)
                Add(baseShards[i], (ShardCenters[i] - new Vector2(0.5f, 0.5f)) * footprint, footprint * 0.42f,
                    Color.white, Random.Range(1.5f, 2.3f), 0.7f);
        if (turretShards != null)
            for (int i = 0; i < turretShards.Length; i++)
                Add(turretShards[i], (Vector2)(Quaternion.Euler(0f, 0f, turretAngle) * new Vector3(0f, (i == 0 ? 0.3f : -0.02f) * footprint, 0f)),
                    footprint * (i == 0 ? 0.45f : 0.6f), Color.white, Random.Range(1.2f, 1.8f), 0.75f);
        if (boltSprite != null)
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i == 0 || i == 3 ? -0.31f : 0.31f, i < 2 ? 0.28f : -0.28f) * footprint;
                Add(boltSprite, corner, footprint * 0.13f, Color.white, Random.Range(2.2f, 3f), 0.8f);
            }
        var chip = FlightTrailFx.GlowSprite();
        for (int i = 0; i < ChipCount; i++)
        {
            Vector2 off = Random.insideUnitCircle * footprint * 0.3f;
            Add(chip, off, footprint * Random.Range(0.06f, 0.11f), ChipColors[i % ChipColors.Length],
                Random.Range(2.4f, 3.6f), 0.3f);
        }

        // Kısa flaş (lazer yeşili → beyaz), parçaların altında.
        var flash = FlightTrailFx.CreateImage(root, "LaserCannonFlash", FlightTrailFx.GlowSprite(), Color.white, footprint * 1.4f);
        flash.localPosition = center;
        flash.SetAsFirstSibling();
        var flashImg = flash.GetComponent<Image>();

        const float flashTime = 0.25f;
        float gravity = footprint * 5f;
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float dt = Time.deltaTime, k = t / life;
            if (flash != null)
            {
                float f = Mathf.Clamp01(t / flashTime);
                flash.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.4f, 1f - (1f - f) * (1f - f));
                flashImg.color = FlightTrailFx.WithAlpha(Color.Lerp(Color.white, HaloColor, f), 1f - f);
            }
            float fade = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            for (int i = 0; i < n; i++)
            {
                var rt = rts[i];
                if (rt == null) continue;
                vel[i].y -= gravity * dt;
                vel[i] *= 1f - 0.6f * dt;   // hafif hava sürtünmesi
                rt.localPosition += (Vector3)(vel[i] * dt);
                rt.localPosition = bounds.ClampCenter(rt.localPosition, rt.sizeDelta);
                rt.localRotation *= Quaternion.Euler(0f, 0f, spin[i] * dt);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, shrink[i], k);
                imgs[i].color = FlightTrailFx.WithAlpha(tints[i], fade);
            }
            yield return null;
        }
        if (flash != null) Destroy(flash.gameObject);
        for (int i = 0; i < n; i++)
            if (rts[i] != null) Destroy(rts[i].gameObject);
    }

    // Taban parçaları: dört köşe + dört kenar ortası (normalize merkez, UI y yukarı).
    private static readonly Vector2[] ShardCenters =
    {
        new Vector2(0.16f, 0.84f), new Vector2(0.84f, 0.84f), new Vector2(0.84f, 0.16f), new Vector2(0.16f, 0.16f),
        new Vector2(0.5f, 0.9f), new Vector2(0.9f, 0.5f), new Vector2(0.5f, 0.1f), new Vector2(0.1f, 0.5f),
    };

    private static Sprite[] ShardsFor(Sprite source, bool turret)
    {
        if (source == null || source.texture == null) return null;
        var tex = source.texture;
        if (shardCache.TryGetValue(tex, out var cached) && cached != null) return cached;

        Rect r = source.textureRect;
        Sprite[] result;
        if (!turret)
        {
            float side = Mathf.Min(r.width, r.height) * 0.39f;   // 2026-10-13: parçalar %50 büyük (0.26 → 0.39)
            result = new Sprite[ShardCenters.Length];
            for (int i = 0; i < ShardCenters.Length; i++)
            {
                float x = Mathf.Clamp(r.x + ShardCenters[i].x * r.width - side * 0.5f, r.x, r.xMax - side);
                float y = Mathf.Clamp(r.y + ShardCenters[i].y * r.height - side * 0.5f, r.y, r.yMax - side);
                result[i] = Sprite.Create(tex, new Rect(x, y, side, side), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            }
        }
        else
        {
            // Namlu ağzı (turuncu kapak) + gövde halkası.
            result = new[]
            {
                Sprite.Create(tex, new Rect(r.x + r.width * 0.33f, r.y + r.height * 0.7f, r.width * 0.34f, r.height * 0.24f),
                    new Vector2(0.5f, 0.5f), source.pixelsPerUnit),
                Sprite.Create(tex, new Rect(r.x + r.width * 0.12f, r.y + r.height * 0.24f, r.width * 0.76f, r.height * 0.44f),
                    new Vector2(0.5f, 0.5f), source.pixelsPerUnit),
            };
        }
        shardCache[tex] = result;
        return result;
    }

    // ── Paylaşılan sprite'lar ───────────────────────────────────────────────────

    // Lazer bandı: enine düz (bant), yalnız kenarları yumuşak; boyuna düz, uzunluğuna esnetilir.
    private static Sprite BeamSprite()
    {
        if (beamSprite != null) return beamSprite;
        const int w = 4, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float d = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
            float a = d < 0.7f ? 1f : Mathf.SmoothStep(1f, 0f, (d - 0.7f) / 0.3f);
            for (int x = 0; x < w; x++) px[y * w + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        beamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 100f);
        return beamSprite;
    }

    // Nişangâh: dört köşede birbirine değmeyen L çizgiler (yumuşak kenarlı), ortası boş.
    private static Sprite ReticleSprite()
    {
        if (reticleSprite != null) return reticleSprite;
        const int n = 128;
        const float arm = 0.30f, thick = 0.075f, inset = 0.04f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Köşeye katla: her köşe aynı L.
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float fu = Mathf.Min(u, 1f - u) - inset, fv = Mathf.Min(v, 1f - v) - inset;
                float d = float.MaxValue;   // L'ye uzaklık (normalize)
                if (fu >= -thick && fv >= -thick)
                {
                    float dh = Mathf.Max(Mathf.Abs(fv - thick * 0.5f) - thick * 0.5f, fu - arm, -fu);   // yatay kol
                    float dv = Mathf.Max(Mathf.Abs(fu - thick * 0.5f) - thick * 0.5f, fv - arm, -fv);   // dikey kol
                    d = Mathf.Min(dh, dv);
                }
                float a = Mathf.Clamp01(0.5f - d * n);   // ~1 px kenar yumuşatma
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        reticleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return reticleSprite;
    }

    // Kafes = mıknatısın kullandığı kafes görseli (MagnetObstacleService Inspector'ı, zincirli çerçeve) — iki engel aynı
    // "special kafeslendi" dilini konuşur. Mıknatıs servisi sahnede yoksa eski Resources görseline düşer.
    private static Sprite CageSprite()
    {
        if (cageSprite != null) return cageSprite;
        var magnet = Object.FindFirstObjectByType<MagnetObstacleService>(FindObjectsInactive.Include);
        if (magnet != null) cageSprite = magnet.CageSprite;   // bulununca önbelleğe alınır
        if (cageSprite != null) return cageSprite;
        if (!triedCageSprite)
        {
            triedCageSprite = true;
            fallbackCageSprite = Resources.Load<Sprite>(CageSpritePath);
        }
        return fallbackCageSprite;
    }
}
