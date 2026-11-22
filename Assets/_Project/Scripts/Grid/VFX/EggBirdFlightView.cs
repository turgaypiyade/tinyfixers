using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BirdSheet: 0..5 front / right-quarter / right / back / left / left-quarter;
/// 6..11 are the same views with thrust; 12..20 are separate hinged wings.
/// Slot 21 is the RocketBasket impact flame fallback. All art is referenced by
/// ObstacleDef.auxiliarySprites, so player builds need no asset lookup.
/// </summary>
public sealed class EggBirdFlightView : MonoBehaviour
{
    // Yumurta kırılınca çarpmaya kadar toplam ≈ 1.97 sn (0.10+0.70+0.14+0.40+0.18+0.45).
    private const float HatchWindupDuration = 0.10f;
    private const float RiseDuration = 0.70f;
    private const float SplitChargeDuration = 0.14f;
    private const float SplitDuration = 0.40f;
    private const float SplitHoldDuration = 0.18f;
    private const float DiveDuration = 0.45f;
    private const float FlapsPerSecond = 12f;

    private sealed class Bird
    {
        public RectTransform root;
        public Image body;
        public Image leftWing;
        public Image rightWing;
        public int view = -1;
        public bool thrust;
        public float phase;
    }

    private sealed class Feather
    {
        public Image image;
        public Vector2 velocity;
        public float spin;
        public float age;
        public float lifetime;
    }

    private BoardController board;
    private LevelData level;
    private RectTransform space;
    private List<Sprite> sprites;
    private ObstacleDef obstacleDef;
    private System.IDisposable flightLoop;
    private readonly List<Bird> birds = new List<Bird>(3);
    // HAVUZ (performans): görünüm bitince yok edilmez, kapatılıp aynı VFX kökünde bekler. Kuşlar, tüyler ve
    // patlama halkaları görünümün içinde yeniden kullanılır → tetikleme başına GameObject üretimi/çöp yok.
    private static readonly List<EggBirdFlightView> s_free = new List<EggBirdFlightView>(4);
    private readonly List<Bird> birdPool = new List<Bird>(3);
    private readonly Stack<Image> featherPool = new Stack<Image>(32);
    private readonly List<EggBirdBurstGraphic> bursts = new List<EggBirdBurstGraphic>(12);
    private readonly HashSet<Transform> ownedChildren = new HashSet<Transform>();
    private readonly List<EggBirdBurstGraphic> targetRings = new List<EggBirdBurstGraphic>(3);
    private readonly List<Feather> feathers = new List<Feather>(32);
    private Vector2 source;
    private Vector2 apex;
    private float tileSize;
    private float elapsed;
    private float impactTime;
    private bool diving;

    private bool Alive => board != null && board.isActiveAndEnabled && board.LevelData == level;

    public static EggBirdFlightView Create(BoardController board, Vector2Int origin, ObstacleDef def)
    {
        RectTransform parent = board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null
            ? board.BoardVfxPlayer.VfxRoot : board.TilesRoot;
        if (parent == null || def == null || def.auxiliarySprites == null
            || def.auxiliarySprites.Count < 21 || def.auxiliarySprites[0] == null)
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning("[EggBird] Flight needs BirdSheet_0..20 in auxiliarySprites and a board VFX root.");
            return null;
        }

        var view = TakePooled(parent);
        RectTransform root;
        if (view != null)
        {
            root = (RectTransform)view.transform;
            view.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("EggBird_HatchSplitDive", typeof(RectTransform), typeof(EggBirdFlightView));
            root = (RectTransform)go.transform;
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            view = go.GetComponent<EggBirdFlightView>();
        }
        root.pivot = parent.pivot;
        root.SetAsLastSibling();

        view.birds.Clear();
        view.targetRings.Clear();
        view.elapsed = 0f;
        view.impactTime = 0f;
        view.diving = false;
        view.board = board;
        view.level = board.LevelData;
        view.space = root;
        view.sprites = def.auxiliarySprites;
        view.obstacleDef = def;
        view.source = view.CellPoint(origin);
        view.tileSize = Vector2.Distance(view.source, view.CellPoint(origin + Vector2Int.right));
        if (view.tileSize < 0.01f)
            view.tileSize = Mathf.Max(1, board.TileSize);

        Vector2 topLeft = view.CellPoint(Vector2Int.zero);
        Vector2 topRight = view.CellPoint(new Vector2Int(board.Width - 1, 0));
        float margin = Mathf.Min(view.tileSize * 1.15f, Mathf.Abs(topRight.x - topLeft.x) * 0.5f);
        view.apex = new Vector2(
            Mathf.Clamp(view.source.x, topLeft.x + margin, topRight.x - margin),
            Mathf.Min(view.source.y + view.tileSize * 2.15f, topLeft.y + view.tileSize * 0.85f));
        view.birds.Add(view.RentBird(0));
        view.birds[0].root.anchoredPosition = view.source;
        view.birds[0].root.localScale = Vector3.one * 0.68f;
        return view;
    }

    private static EggBirdFlightView TakePooled(RectTransform parent)
    {
        for (int i = s_free.Count - 1; i >= 0; i--)
        {
            var v = s_free[i];
            if (v == null) { s_free.RemoveAt(i); continue; }   // sahne kapandı: yok edilmiş görünüm
            if (v.transform.parent != parent) continue;
            s_free.RemoveAt(i);
            return v;
        }
        return null;
    }

    /// Uçuş bitti (EggBirdHatchAction finally): görünümü kapatıp havuza bırakır; tahta gittiyse yok eder.
    public static void Release(EggBirdFlightView view)
    {
        if (view == null) return;
        if (view.board == null)
        {
            Destroy(view.gameObject);
            return;
        }
        view.StopFlightSound();
        for (int i = 0; i < view.birdPool.Count; i++)
            if (view.birdPool[i].root != null) view.birdPool[i].root.gameObject.SetActive(false);
        for (int i = view.feathers.Count - 1; i >= 0; i--)
            view.ReleaseFeather(view.feathers[i].image);
        view.feathers.Clear();
        for (int i = 0; i < view.bursts.Count; i++)
            if (view.bursts[i] != null) view.bursts[i].Release();
        view.birds.Clear();
        view.targetRings.Clear();
        // Yedek çarpma patlaması (RocketProjectileFlight.PlayImpactExplosion) kendi nesnelerini bu köke kurar;
        // görünüm kapanınca coroutine'i durur → yarım kalan nesneler sonraki kullanımda belirmesin.
        var tr = view.transform;
        for (int i = tr.childCount - 1; i >= 0; i--)
        {
            var child = tr.GetChild(i);
            if (!view.ownedChildren.Contains(child)) Destroy(child.gameObject);
        }
        view.gameObject.SetActive(false);
        if (!s_free.Contains(view)) s_free.Add(view);
    }

    private void OnDestroy() => s_free.Remove(this);

    public IEnumerator RiseAndSplit()
    {
        Burst(source, tileSize * 1.25f, new Color(1f, 0.78f, 0.24f), 0.28f);
        Bird leader = birds[0];
        float time = 0f;
        // A small crouch and recoil make the chick feel like it pushes out of the shell.
        while (time < HatchWindupDuration && Alive)
        {
            time += Time.deltaTime;
            float pulse = Mathf.Sin(Mathf.Clamp01(time / HatchWindupDuration) * Mathf.PI);
            leader.root.anchoredPosition = source + Vector2.down * (tileSize * 0.08f * pulse);
            leader.root.localScale = new Vector3(0.68f + pulse * 0.14f, 0.68f - pulse * 0.17f, 1f);
            leader.root.localRotation = Quaternion.Euler(0f, 0f, pulse * -9f);
            yield return null;
        }
        if (!Alive) yield break;

        StartFlightSound();
        SpawnFeathers(source, 7, 1.5f, 0.48f);
        time = 0f;
        float nextTrail = 0f;
        while (time < RiseDuration && Alive)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / RiseDuration);
            float ease = 1f - Mathf.Pow(1f - t, 2.3f);
            float arc = Mathf.Sin(t * Mathf.PI);
            float sway = Mathf.Sin(t * Mathf.PI * 2f);
            leader.root.anchoredPosition = Vector2.Lerp(source, apex, ease)
                + new Vector2(sway * 0.28f, arc * 0.13f) * tileSize;
            float size = Mathf.Lerp(0.68f, 1.75f, ease);
            float stretch = Mathf.Sin(Mathf.Min(1f, t * 2f) * Mathf.PI) * 0.19f;
            leader.root.localScale = new Vector3(size * (1f - stretch * 0.5f), size * (1f + stretch), 1f);
            leader.root.localRotation = Quaternion.Euler(0f, 0f, -sway * 16f);
            int angle = t < 0.2f ? 0 : t < 0.35f ? 1 : t < 0.5f ? 2
                : t < 0.65f ? 3 : t < 0.8f ? 4 : t < 0.92f ? 5 : 0;
            SetView(leader, angle, t > 0.12f && t < 0.85f);
            if (time >= nextTrail && t < 0.85f)
            {
                nextTrail = time + 0.09f;
                SpawnFeathers(leader.root.anchoredPosition + Vector2.down * tileSize * 0.35f, 2, 0.55f, 0.36f);
            }
            yield return null;
        }
        if (!Alive) yield break;

        // Compress at the apex before releasing the three birds, with a faster body wobble.
        time = 0f;
        while (time < SplitChargeDuration && Alive)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / SplitChargeDuration);
            float size = Mathf.Lerp(1.75f, 1.48f, t);
            leader.root.localScale = new Vector3(size * (1f + t * 0.14f), size * (1f - t * 0.15f), 1f);
            leader.root.anchoredPosition = apex + Vector2.down * (tileSize * 0.09f * t);
            leader.root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 4f) * 9f * t);
            yield return null;
        }
        if (!Alive) yield break;

        Vector2 splitOrigin = leader.root.anchoredPosition;
        Vector3 leaderScale = leader.root.localScale;
        Burst(splitOrigin, tileSize * 3.5f, new Color(0.7f, 0.43f, 1f), 0.56f);
        Burst(splitOrigin, tileSize * 2.15f, new Color(1f, 0.83f, 0.3f), 0.34f);
        SpawnFeathers(splitOrigin, 12, 2.6f, 0.56f);
        birds.Add(RentBird(1));
        birds.Add(RentBird(2));
        for (int i = 0; i < birds.Count; i++)
        {
            birds[i].root.anchoredPosition = splitOrigin;
            birds[i].root.localScale = i == 0 ? leaderScale : Vector3.one * 0.28f;
            if (i > 0) SetOpacity(birds[i], 0f);
        }

        time = 0f;
        while (time < SplitDuration && Alive)
        {
            time += Time.deltaTime;
            for (int i = 0; i < birds.Count; i++)
            {
                Bird bird = birds[i];
                float delay = i * 0.045f;
                float t = Mathf.Clamp01((time - delay) / (SplitDuration - delay));
                float ease = EaseOutBack(t);
                float arc = Mathf.Sin(t * Mathf.PI);
                int side = i == 1 ? -1 : i == 2 ? 1 : 0;
                bird.root.anchoredPosition = Vector2.LerpUnclamped(splitOrigin, FormationPoint(i), ease)
                    + Vector2.up * (arc * tileSize * (i == 0 ? 0.22f : 0.4f));
                bird.root.localScale = Vector3.LerpUnclamped(i == 0 ? leaderScale : Vector3.one * 0.28f,
                    new Vector3(1.12f, 1.12f, 1f), ease);
                bird.root.localRotation = Quaternion.Euler(0f, 0f, -side * arc * 28f);
                SetOpacity(bird, i == 0 ? 1f : Mathf.Clamp01(t * 5f));
                SetView(bird, i == 0 ? 0 : i == 1 ? 5 : 1, false);
            }
            yield return null;
        }

        time = 0f;
        while (time < SplitHoldDuration && Alive)
        {
            time += Time.deltaTime;
            float envelope = Mathf.Sin(Mathf.Clamp01(time / SplitHoldDuration) * Mathf.PI);
            for (int i = 0; i < birds.Count; i++)
            {
                float bob = Mathf.Sin(time * 17f + i * 1.7f) * envelope;
                birds[i].root.anchoredPosition = FormationPoint(i)
                    + new Vector2(Mathf.Cos(time * 12f + i) * 0.035f * envelope, bob * 0.065f) * tileSize;
                birds[i].root.localRotation = Quaternion.Euler(0f, 0f, bob * 6f);
                birds[i].root.localScale = Vector3.one * (1.12f + bob * 0.035f);
            }
            yield return null;
        }
    }

    public IEnumerator Dive(IReadOnlyList<Vector2Int> targets)
    {
        if (!Alive) yield break;
        diving = true;
        var starts = new Vector2[birds.Count];
        var ends = new Vector2[birds.Count];
        var controls = new Vector2[birds.Count];
        for (int i = 0; i < birds.Count; i++)
        {
            starts[i] = birds[i].root.anchoredPosition;
            ends[i] = CellPoint(targets[i]);
            float side = i == 1 ? -1f : i == 2 ? 1f : Mathf.Sign(ends[i].x - starts[i].x);
            controls[i] = starts[i] + new Vector2(side * tileSize * 0.75f, tileSize * 0.85f);
            var ring = SpawnBurst(ends[i], tileSize * 1.05f,
                new Color(1f, 0.75f, 0.2f), DiveDuration + 0.1f, true);
            targetRings.Add(ring);
        }

        float time = 0f;
        while (time < DiveDuration && Alive)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / DiveDuration);
            // Accelerate into the board; shrink to sell the descent from above it.
            float travel = t * t;
            for (int i = 0; i < birds.Count; i++)
            {
                Bird bird = birds[i];
                Vector2 position = Bezier(starts[i], controls[i], ends[i], travel);
                Vector2 direction = 2f * ((1f - travel) * (controls[i] - starts[i])
                    + travel * (ends[i] - controls[i]));
                bool right = ends[i].x >= starts[i].x;
                SetView(bird, right ? 2 : 4, false);
                float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - (right ? 0f : 180f);
                bird.root.localRotation = Quaternion.Euler(0f, 0f, rotation);
                bird.root.anchoredPosition = position;
                bird.root.localScale = Vector3.one * Mathf.Lerp(1.12f, 0.48f, travel);
            }
            yield return null;
        }
    }

    public void Impact(IReadOnlyList<Vector2Int> targets)
    {
        StopFlightSound();
        foreach (var bird in birds)
            if (bird.root != null) bird.root.gameObject.SetActive(false);
        foreach (var ring in targetRings)
            if (ring != null) ring.Release();
        targetRings.Clear();
        var rocketFlame = board.GetComponent<RocketProjectileFlight>();
        foreach (var cell in new HashSet<Vector2Int>(targets))
        {
            bool played = rocketFlame != null && rocketFlame.TryPlayImpactExplosionAtCell(cell.x, cell.y);
            if (!played && sprites.Count > 21 && sprites[21] != null)
                StartCoroutine(RocketProjectileFlight.PlayImpactExplosion(space, CellPoint(cell), tileSize, sprites[21]));
            else if (!played)
                Burst(CellPoint(cell), tileSize * 2.35f, new Color(1f, 0.62f, 0.13f), 0.43f);
        }
        impactTime = Time.time;
        // The three birds arrive together: play one bomb cue without tripling its loudness.
        // Merkezi ses yolu (AGENTS): tahtanın ses yönetmeni; tahta kaynağında çalar → 1 sn kuyruk bu kısa görseli aşar.
        if (obstacleDef != null && obstacleDef.impactSound != null && board.Audio != null)
            board.Audio.PlayOneShotClip(obstacleDef.impactSound, obstacleDef.impactSoundVolume);
    }

    // Uçuş döngüsü merkezi döngü API'sinden (ayar kapalıyken çalmaz/susar; efekt başına AudioSource açılmaz).
    private void StartFlightSound()
    {
        if (flightLoop != null || obstacleDef == null || obstacleDef.flightLoopSound == null || board.Audio == null)
            return;
        flightLoop = board.Audio.StartLoopClip(obstacleDef.flightLoopSound, obstacleDef.flightLoopSoundVolume);
    }

    private void StopFlightSound()
    {
        var loop = flightLoop;
        flightLoop = null;
        loop?.Dispose();
    }

    private void OnDisable() => StopFlightSound();

    public IEnumerator FinishBursts()
    {
        while (Alive && Time.time - impactTime < 0.46f)
            yield return null;
    }

    private Bird RentBird(int index)
    {
        Bird bird;
        if (index < birdPool.Count)
        {
            bird = birdPool[index];
            bird.root.gameObject.SetActive(true);
        }
        else
        {
            RectTransform root = CreateRect("Bird_" + index, space);
            ownedChildren.Add(root);
            bird = new Bird { root = root, phase = index * 0.8f };
            bird.leftWing = CreateImage("LeftWing", root);
            bird.rightWing = CreateImage("RightWing", root);
            bird.body = CreateImage("Body", root);
            birdPool.Add(bird);
        }
        bird.root.SetAsLastSibling();
        bird.root.sizeDelta = Vector2.one * tileSize;   // tahta değişmiş olabilir
        bird.root.localRotation = Quaternion.identity;
        bird.root.localScale = Vector3.one;
        SetOpacity(bird, 1f);
        bird.view = -1;                                  // SetView boyutları yeni tileSize ile yeniden kursun
        SetView(bird, 0, false);
        return bird;
    }

    private void SetView(Bird bird, int view, bool thrust)
    {
        if (bird.view == view && bird.thrust == thrust) return;
        bird.view = view;
        bird.thrust = thrust;
        Sprite body = sprites[view + (thrust ? 6 : 0)] ?? sprites[view];
        bird.body.sprite = body;
        // Keep the head the same size when switching to a taller sprite with exhaust.
        float width = tileSize * 0.86f;
        float height = body != null ? width * body.rect.height / Mathf.Max(1f, body.rect.width) : width;
        Sprite resting = sprites[view];
        float headHeight = resting != null ? width * resting.rect.height / Mathf.Max(1f, resting.rect.width) : width;
        bird.body.rectTransform.sizeDelta = new Vector2(width, height);
        bird.body.rectTransform.anchoredPosition = Vector2.down * (height - headHeight) * 0.5f;

        int leftIndex = view == 3 ? 17 : view == 4 || view == 5 ? 13 : 12;
        int rightIndex = view == 3 ? 20 : view == 1 || view == 2 ? 14 : 15;
        bird.leftWing.sprite = sprites[leftIndex];
        bird.rightWing.sprite = sprites[rightIndex];
        SetupWing(bird.leftWing.rectTransform, true);
        SetupWing(bird.rightWing.rectTransform, false);
        // Both wings behind the front/back; the nearer wing in front for side views.
        bird.body.transform.SetAsLastSibling();
        if (view == 1 || view == 2) bird.leftWing.transform.SetAsLastSibling();
        if (view == 4 || view == 5) bird.rightWing.transform.SetAsLastSibling();
    }

    private void SetupWing(RectTransform wing, bool left)
    {
        wing.pivot = new Vector2(left ? 0.94f : 0.06f, 0.86f);
        wing.anchoredPosition = new Vector2(left ? -0.34f : 0.34f, -0.04f) * tileSize;
        wing.sizeDelta = new Vector2(0.55f, 0.46f) * tileSize;
    }

    private void Update()
    {
        if (!Alive)
        {
            Destroy(gameObject);
            return;
        }
        elapsed += Time.deltaTime;
        UpdateFeathers();
        foreach (Bird bird in birds)
        {
            if (bird.root == null || !bird.root.gameObject.activeSelf) continue;
            float flap = Mathf.Sin(elapsed * Mathf.PI * 2f * (diving ? 15f : FlapsPerSecond) + bird.phase);
            float angle = Mathf.Lerp(-48f, 55f, flap * 0.5f + 0.5f);
            bird.leftWing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            bird.rightWing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            float squash = Mathf.Lerp(0.5f, 1f, Mathf.Abs(flap));
            bird.leftWing.rectTransform.localScale = new Vector3(1f, squash, 1f);
            bird.rightWing.rectTransform.localScale = new Vector3(1f, squash, 1f);
        }
    }

    private Vector2 FormationPoint(int index)
    {
        float spread = Mathf.Min(tileSize * 1.4f, tileSize * board.Width * 0.28f);
        return apex + (index == 0 ? new Vector2(0f, tileSize * 0.18f)
            : new Vector2(index == 1 ? -spread : spread, -tileSize * 0.18f));
    }

    private Vector2 CellPoint(Vector2Int cell)
        => space.InverseTransformPoint(board.GetCellWorldCenterPosition(cell.x, cell.y));

    private void Burst(Vector2 position, float size, Color color, float lifetime)
        => SpawnBurst(position, size, color, lifetime, false);

    private EggBirdBurstGraphic SpawnBurst(Vector2 position, float size, Color color, float lifetime, bool target)
    {
        for (int i = 0; i < bursts.Count; i++)
        {
            var b = bursts[i];
            if (b != null && b.IsFree)
            {
                b.Restart(position, size, color, lifetime, target);
                return b;
            }
        }
        var created = EggBirdBurstGraphic.Spawn(space, position, size, color, lifetime, target);
        bursts.Add(created);
        ownedChildren.Add(created.transform);
        return created;
    }

    private void ReleaseFeather(Image image)
    {
        if (image == null) return;
        image.enabled = false;
        featherPool.Push(image);
    }

    private void SpawnFeathers(Vector2 position, int count, float speed, float lifetime)
    {
        for (int i = 0; i < count; i++)
        {
            Image image;
            if (featherPool.Count > 0)
            {
                image = featherPool.Pop();
                image.enabled = true;
                image.rectTransform.localScale = Vector3.one;
            }
            else
            {
                image = CreateImage("EggBird_Feather", space);
                ownedChildren.Add(image.transform);
            }
            image.sprite = sprites[i % 2 == 0 ? 12 : 15];
            image.rectTransform.anchoredPosition = position + Random.insideUnitCircle * tileSize * 0.12f;
            image.rectTransform.sizeDelta = new Vector2(0.17f, 0.14f) * tileSize;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-180f, 180f));
            image.transform.SetAsFirstSibling();
            feathers.Add(new Feather
            {
                image = image,
                velocity = Random.insideUnitCircle * tileSize * speed,
                spin = Random.Range(-270f, 270f),
                lifetime = lifetime
            });
        }
    }

    private void UpdateFeathers()
    {
        for (int i = feathers.Count - 1; i >= 0; i--)
        {
            var feather = feathers[i];
            feather.age += Time.deltaTime;
            if (feather.image == null || feather.age >= feather.lifetime)
            {
                ReleaseFeather(feather.image);
                feathers.RemoveAt(i);
                continue;
            }
            float t = feather.age / feather.lifetime;
            feather.velocity += Vector2.down * (tileSize * 1.4f * Time.deltaTime);
            var rect = feather.image.rectTransform;
            rect.anchoredPosition += feather.velocity * Time.deltaTime;
            rect.Rotate(0f, 0f, feather.spin * Time.deltaTime);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);
            feather.image.color = new Color(1f, 0.95f, 0.82f, (1f - t) * 0.85f);
        }
    }

    private static void SetOpacity(Bird bird, float alpha)
    {
        Color color = new Color(1f, 1f, 1f, alpha);
        bird.body.color = bird.leftWing.color = bird.rightWing.color = color;
    }

    private static float EaseOutBack(float t)
    {
        float u = t - 1f;
        return 1f + 2.25f * u * u * u + 1.25f * u * u;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        => (1f - t) * (1f - t) * a + 2f * (1f - t) * t * b + t * t * c;

    private static RectTransform CreateRect(string name, RectTransform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        // Anchors at the parent's pivot make anchoredPosition exactly its local point.
        rect.anchorMin = rect.anchorMax = parent.pivot;
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private static Image CreateImage(string name, RectTransform parent)
    {
        Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }
}
