using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Four special releases → falling eggs → three flies per egg → six bites per fruit.
/// One shared tick; egg deliveries and at most eight broods use existing UI pools.
public sealed class FlyFruitService : MonoBehaviour
{
    private sealed class Box
    {
        public int origin;
        public Image image;
        public readonly Image[] eggs = new Image[4];
        public readonly FlyEggBoxProgress progress = new();
        public float hitAge = -1;
        public Vector3 homeScale;
        public bool retired;
    }
    private sealed class Fruit
    {
        public int origin;
        public Image image;
        public readonly FlyFruitProgress progress = new();
        public bool cleared;
        public float biteAge = -1;
    }
    private sealed class Brood
    {
        public Fruit target;
        public Vector2 from, apex;
        public float age;
        public int arrived;
        public readonly FlyFruitFlyView[] flies = new FlyFruitFlyView[3];
        public readonly FlightRoute[] routes = new FlightRoute[3];
    }
    private struct FlightRoute
    {
        public Vector2 formationOffset, curveOffset, biteOffset;
        public float delay, duration, phase, sway;
    }
    private readonly Dictionary<int, Box> boxes = new();
    private readonly Dictionary<int, Fruit> fruits = new();
    private readonly List<Fruit> fruitList = new();
    private readonly List<int> origins = new();
    private readonly List<Brood> active = new(8);
    private readonly Stack<Brood> spare = new(8);
    private readonly Queue<int> pending = new(128);
    private readonly List<FlyFruitFlyView> warm = new(24);
    private readonly FlyEggProductionFx production = new();
    private BoardController board;
    private LevelData level;
    private ObstacleStateService state;
    private RectTransform space;
    private ObstacleDef eggDef, fruitDef;
    private IDisposable broodJob;
    private IDisposable buzzLoop;
    private float size, retry;
    private bool bound, stalled;
    private int generation;
    private bool Alive => bound && board != null && board.isActiveAndEnabled && board.ActiveLevelData == level;

    public static FlyFruitService Ensure(BoardController board)
    {
        if (!board.TryGetComponent<FlyFruitService>(out var service)) service = board.gameObject.AddComponent<FlyFruitService>();
        service.enabled = true; return service;
    }

    public void Build(BoardController owner, LevelData data)
    {
        Clear(); board = owner; level = data; state = owner.ObstacleStateService;
        space = board.BreakFxParent != null ? board.BreakFxParent : board.ContentRoot;
        size = Mathf.Max(1, board.TileSize);
        if (space != null && (board.Width > 1 || board.Height > 1))
        {
            Vector2 a = space.InverseTransformPoint(board.GetCellWorldCenterPosition(0, 0));
            Vector2 b = space.InverseTransformPoint(board.GetCellWorldCenterPosition(board.Width > 1 ? 1 : 0, board.Width > 1 ? 0 : 1));
            size = Mathf.Max(1, Vector2.Distance(a, b));
        }
        eggDef = level.obstacleLibrary?.Get(ObstacleId.FlyEgg); fruitDef = level.obstacleLibrary?.Get(ObstacleId.FlyFruit);
        board.FlyFruits = this;
        state.FlyEggBoxHitInterceptor = HitBox; state.FlyEggBoxRemainingQuery = BoxRemaining;
        production.OnFinished = EggFinished; bound = true;
        level.GetFruitOrigins(origins);
        foreach (int origin in origins) AddFruit(origin);
        var sourceOrigins = new HashSet<int>();
        if (level.obstacles != null && level.obstacleOrigins != null)
            for (int i = 0; i < level.obstacles.Length; i++)
                if (level.obstacles[i] == (int)ObstacleId.FlyEggBox && level.obstacleOrigins[i] == i) sourceOrigins.Add(i);
        if (level.stackedObstacles != null)
            foreach (var e in level.stackedObstacles) if (e.obstacleId == ObstacleId.FlyEggBox) sourceOrigins.Add(e.originCellIndex);
        production.Warm(space, sourceOrigins.Count * 4);
        if (spare.Count == 0) for (int i = 0; i < 8; i++) spare.Push(new Brood());
        // Warm fly hierarchies at setup; future egg hatches reuse all24 views/72 part Images.
        if (space != null && eggDef != null && eggDef.auxiliarySprites.Count >= 6)
        {
            for (int i = 0; i < 24; i++)
            {
                var fly = FlyFruitFlyView.Rent(space, eggDef, size);
                if (fly != null) warm.Add(fly);
            }
            foreach (var fly in warm) fly.Return(); warm.Clear();
        }
    }

    private Fruit AddFruit(int origin)
    {
        if (fruits.TryGetValue(origin, out var found)) return found;
        var fruit = new Fruit { origin = origin }; fruits.Add(origin, fruit); fruitList.Add(fruit); return fruit;
    }

    public void Register(int origin, ObstacleDef def, Image image)
    {
        if (!Alive || image == null) return;
        image.preserveAspect = true;
        // Preserve the authored footprint while making hit/bite squash pivot around its centre.
        var rect = image.rectTransform;
        rect.anchoredPosition += new Vector2((.5f - rect.pivot.x) * rect.sizeDelta.x, (.5f - rect.pivot.y) * rect.sizeDelta.y);
        rect.pivot = Vector2.one * .5f;
        if (def.id == ObstacleId.FlyFruit)
        {
            var fruit = AddFruit(origin); fruit.image = image; UpdateFruit(fruit); return;
        }
        if (def.id != ObstacleId.FlyEggBox) return;
        if (!boxes.TryGetValue(origin, out var box)) { box = new Box { origin = origin }; boxes.Add(origin, box); }
        if (box.image == image && box.eggs[0] != null) { ShowEggs(box); return; }
        box.image = image; box.homeScale = image.rectTransform.localScale;
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("FlyEggSlot" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = image.gameObject.layer; go.transform.SetParent(image.transform, false);
            var egg = go.GetComponent<Image>(); egg.sprite = def.auxiliarySprites.Count > 0 ? def.auxiliarySprites[0] : null;
            egg.preserveAspect = true; egg.raycastTarget = false;
            var rt = egg.rectTransform;
            rt.anchorMin = new Vector2(i % 2 == 0 ? .10f : .54f, i < 2 ? .54f : .10f);
            rt.anchorMax = rt.anchorMin + Vector2.one * .36f;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            box.eggs[i] = egg;
        }
        ShowEggs(box);
    }

    private bool HasFruitWork()
    {
        foreach (var fruit in fruitList) if (fruit.progress.Remaining > 0) return true;
        return false;
    }
    private int BoxRemaining(int origin) => Alive && HasFruitWork() && boxes.TryGetValue(origin, out var box) ? box.progress.Available : 0;
    private bool HitBox(int origin, ObstacleHitContext context)
    {
        if (!Alive || !HasFruitWork() || context != ObstacleHitContext.SpecialActivation || !boxes.TryGetValue(origin, out var box)
            || box.image == null || eggDef == null || !box.progress.TryReserve(out int slot)) return false;
        if (!production.Launch(board, box.eggs[slot], eggDef.GetPreviewSprite(), ObstacleId.FlyEgg, origin, slot))
        { box.progress.Finish(slot, false); return false; }
        box.hitAge = 0; ShowEggs(box);
        return true;
    }

    private static void ShowEggs(Box box)
    {
        for (int i = 0; i < 4; i++) if (box.eggs[i] != null) box.eggs[i].gameObject.SetActive((box.progress.VisibleMask & (1 << i)) != 0);
    }
    private void EggFinished(int origin, int slot, bool placed)
    {
        if (boxes.TryGetValue(origin, out var box) && box.progress.Finish(slot, placed)) ShowEggs(box);
    }

    public void Hatch(int cell)
    {
        if (!Alive) return;
        pending.Enqueue(cell);
        broodJob ??= board.BeginJob(BoardController.BoardJobKind.EggBirdFlight);
        retry = 0; stalled = false;
    }

    private bool Exposed(Fruit fruit) => !fruit.cleared && fruit.progress.Remaining > 0
        && state.GetObstacleIdAt(fruit.origin % board.Width, fruit.origin / board.Width) == ObstacleId.FlyFruit
        && !state.IsHitLockedAt(fruit.origin % board.Width, fruit.origin / board.Width);

    private void StartBroods()
    {
        while (pending.Count > 0 && active.Count < 8)
        {
            Fruit chosen = null; bool anyRemaining = false;
            foreach (var fruit in fruitList)
            {
                if (fruit.progress.Remaining > 0) anyRemaining = true;
                if (chosen == null && Exposed(fruit) && fruit.progress.TryReserveBrood()) chosen = fruit;
            }
            if (chosen == null)
            {
                if (!anyRemaining) pending.Clear(); // Surplus eggs cannot hold level-end after the last fruit.
                // No exposed/reservable fruit: waiting eggs must not hold the board gate (level-end, booster
                // shuffle, hints). They keep retrying and re-acquire the job when a fruit opens.
                stalled = true;
                break;
            }
            stalled = false;
            broodJob ??= board.BeginJob(BoardController.BoardJobKind.EggBirdFlight);
            var brood = spare.Pop(); brood.target = chosen; brood.age = 0; brood.arrived = 0;
            int source = pending.Dequeue(); brood.from = Point(source);
            brood.apex = brood.from + new Vector2(UnityEngine.Random.Range(-.3f, .3f), UnityEngine.Random.Range(1.45f, 1.9f)) * size;
            Vector2 topLeft = Point(0), topRight = Point(board.Width - 1);
            float margin = Mathf.Min(size * 1.3f, Mathf.Abs(topRight.x - topLeft.x) * .5f);
            brood.apex.x = Mathf.Clamp(brood.apex.x, topLeft.x + margin, topRight.x - margin);
            brood.apex.y = Mathf.Min(brood.apex.y, topLeft.y + size * .85f);
            var footprint = level.GetFruitDimensions(chosen.origin);
            for (int i = 0; i < 3; i++)
            {
                // Sample once per launch, into the reused brood buffer. Three distinct
                // bite regions stay inside both1x1 and larger fruit footprints.
                Vector2 bite = i == 0 ? new Vector2(-.26f, .06f)
                    : i == 1 ? new Vector2(0f, .24f) : new Vector2(.26f, -.12f);
                bite += new Vector2(UnityEngine.Random.Range(-.04f, .04f), UnityEngine.Random.Range(-.04f, .04f));
                brood.routes[i] = new FlightRoute
                {
                    formationOffset = new Vector2((i - 1) * 1.05f + UnityEngine.Random.Range(-.14f, .14f),
                        (i == 1 ? .40f : -.22f) + UnityEngine.Random.Range(-.12f, .12f)) * size,
                    curveOffset = new Vector2((i - 1) * .5f + UnityEngine.Random.Range(-.25f, .25f), UnityEngine.Random.Range(1.05f, 1.7f)) * size,
                    biteOffset = new Vector2(bite.x * footprint.x, bite.y * footprint.y) * size,
                    delay = i * .14f + UnityEngine.Random.Range(0f, .035f),
                    duration = UnityEngine.Random.Range(.60f, .72f),
                    phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                    sway = UnityEngine.Random.Range(.08f, .18f)
                };
                brood.flies[i] = space != null ? FlyFruitFlyView.Rent(space, eggDef, size) : null;
                brood.flies[i]?.Pose(brood.from, Vector2.up, 0, .85f, brood.routes[i].phase, true);
            }
            active.Add(brood);
        }
    }

    private Vector2 Point(int cell) => space != null
        ? (Vector2)space.InverseTransformPoint(board.GetCellWorldCenterPosition(cell % board.Width, cell / board.Width)) : Vector2.zero;
    private Vector2 FruitPoint(Fruit fruit)
    {
        if (fruit.image != null && space != null) return space.InverseTransformPoint(fruit.image.rectTransform.TransformPoint(fruit.image.rectTransform.rect.center));
        var foot = level.GetFruitDimensions(fruit.origin);
        return Point(fruit.origin) + new Vector2((foot.x - 1) * .5f, -(foot.y - 1) * .5f) * size;
    }
    private void UpdateFruit(Fruit fruit)
    {
        if (fruit.image == null || fruitDef == null) return;
        fruit.image.sprite = fruit.progress.Remaining <= 3 && fruitDef.auxiliarySprites.Count > 0
            ? fruitDef.auxiliarySprites[0] : fruitDef.GetPreviewSprite();
    }

    private bool Advance(Brood brood, float dt)
    {
        brood.age += dt;
        const float rise = .70f, split = .25f, peakScale = 2.1f;
        Vector2 fruitCenter = FruitPoint(brood.target);
        for (int i = brood.arrived; i < 3; i++)
        {
            var route = brood.routes[i];
            float t = Mathf.Clamp01((brood.age - rise - split - route.delay) / route.duration);
            Vector2 end = fruitCenter + route.biteOffset;
            Vector2 formation = brood.apex + route.formationOffset;
            Vector2 pos, direction = Vector2.up; bool front = true;
            if (brood.age < rise)
                pos = Vector2.Lerp(brood.from, brood.apex, 1 - Mathf.Pow(1 - Mathf.Clamp01(brood.age / rise), 2))
                    + route.formationOffset * (.65f * Mathf.SmoothStep(0, 1, (brood.age / rise - .2f) / .8f))
                    + Vector2.right * Mathf.Sin(brood.age * 10 + route.phase) * Mathf.Sin(Mathf.Clamp01(brood.age / rise) * Mathf.PI) * size * route.sway;
            else if (brood.age < rise + split + route.delay)
                pos = Vector2.Lerp(brood.apex + route.formationOffset * .65f, formation, Mathf.SmoothStep(0, 1, (brood.age - rise) / split));
            else
            {
                front = false; float u = 1 - t;
                Vector2 control = (formation + end) * .5f + route.curveOffset;
                pos = u * u * formation + 2 * u * t * control + t * t * end;
                direction = 2 * u * (control - formation) + 2 * t * (end - control);
                if (t >= 1 && !Exposed(brood.target)) pos += Vector2.up * (Mathf.Sin(brood.age * 8 + route.phase) * size * .08f);
            }
            // Match EggBird's readable rise: grow to2.1times at the apex, then
            // settle to formation size and return to normal size at the fruit.
            float flyScale = brood.age < rise
                ? Mathf.Lerp(.85f, peakScale, 1 - Mathf.Pow(1 - Mathf.Clamp01(brood.age / rise), 2.3f))
                : front ? Mathf.Lerp(peakScale, 1.2f, Mathf.SmoothStep(0, 1, (brood.age - rise) / split))
                : Mathf.Lerp(1.2f, 1f, Mathf.SmoothStep(0, 1, t));
            brood.flies[i]?.Pose(pos, direction, brood.age, flyScale, route.phase, front);
            if (t >= 1 && i == brood.arrived && Exposed(brood.target))
            {
                brood.flies[i]?.Return(); brood.flies[i] = null; brood.arrived++;
                bool finished = brood.target.progress.Arrive(); brood.target.biteAge = 0; UpdateFruit(brood.target);
                board.Audio?.PlayOneShotClip(fruitDef.hitSound, fruitDef.hitSoundVolume);
                if (finished) brood.target.cleared = state.TryCompleteFlyFruitObstacle(brood.target.origin, ObstacleId.FlyFruit);
            }
        }
        return brood.arrived == 3;
    }

    private void Update()
    {
        if (!Alive) return;
        int version = generation; production.Tick(Time.deltaTime);
        if (version != generation || !Alive) return;
        foreach (var box in boxes.Values)
        {
            if (box.image == null || box.retired) continue;
            if (box.hitAge >= 0)
            {
                box.hitAge += Time.deltaTime;
                box.image.rectTransform.localScale = box.homeScale * (1 + Mathf.Sin(Mathf.Clamp01(box.hitAge / .22f) * Mathf.PI) * .07f);
                if (box.hitAge >= .22f) { box.image.rectTransform.localScale = box.homeScale; box.hitAge = -1; }
            }
            if (box.progress.Complete)
            {
                var owner = board; var def = level.obstacleLibrary.Get(ObstacleId.FlyEggBox);
                // Retire before clearing: the clear may reveal/register another layer during this loop.
                box.retired = true;
                if (!state.TryCompleteFlyFruitObstacle(box.origin, ObstacleId.FlyEggBox)) box.retired = false; // Covered: retry next frame.
                else
                {
                    owner.Audio?.PlayOneShotClip(def.breakSound, def.breakSoundVolume);
                    if (version != generation || !Alive) return;
                    break; // The clear may have changed the dictionary; continue next frame.
                }
            }
            if (version != generation || !Alive) return;
        }
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var brood = active[i];
            bool done = Advance(brood, Time.deltaTime);
            if (version != generation || !Alive) return;
            if (!done) continue;
            active.RemoveAt(i); brood.target = null; spare.Push(brood);
            // Re-check waiting eggs this frame so the job never drops between two broods.
            stalled = false; retry = 0;
            if (version != generation || !Alive) return;
        }
        retry -= Time.deltaTime;
        if (pending.Count > 0 && retry <= 0) { retry = .1f; StartBroods(); }
        if (active.Count > 0 && buzzLoop == null && eggDef != null)
            buzzLoop = board.Audio?.StartLoopClip(eggDef.flightLoopSound, eggDef.flightLoopSoundVolume);
        else if (active.Count == 0 && buzzLoop != null)
        { var loop = buzzLoop; buzzLoop = null; loop.Dispose(); }
        foreach (var fruit in fruitList)
            if (fruit.image != null && fruit.biteAge >= 0)
            {
                fruit.biteAge += Time.deltaTime;
                float bounce = Mathf.Sin(Mathf.Clamp01(fruit.biteAge / .20f) * Mathf.PI) * .08f;
                fruit.image.rectTransform.localScale = new Vector3(1 + bounce, 1 - bounce, 1);
                if (fruit.biteAge >= .20f) { fruit.image.rectTransform.localScale = Vector3.one; fruit.biteAge = -1; }
            }
        if (active.Count == 0 && broodJob != null && (pending.Count == 0 || stalled))
        {
            var job = broodJob; broodJob = null; job.Dispose(); board.RequestResolveAfterActionSequence();
        }
    }

    public void Clear()
    {
        var loop = buzzLoop; buzzLoop = null; loop?.Dispose();
        generation++; bound = false; production.CancelAll(); production.OnFinished = null;
        if (state != null)
        {
            if (state.FlyEggBoxHitInterceptor == HitBox) state.FlyEggBoxHitInterceptor = null;
            if (state.FlyEggBoxRemainingQuery == BoxRemaining) state.FlyEggBoxRemainingQuery = null;
        }
        foreach (var brood in active)
        {
            brood.target?.progress.Cancel(3 - brood.arrived);
            for (int i = 0; i < 3; i++) { brood.flies[i]?.Return(); brood.flies[i] = null; }
            brood.target = null; spare.Push(brood);
        }
        active.Clear(); pending.Clear(); boxes.Clear(); fruits.Clear(); fruitList.Clear();
        var job = broodJob; broodJob = null; job?.Dispose();
        if (board != null && board.FlyFruits == this) board.FlyFruits = null;
        state = null; level = null; board = null; space = null;
    }
    private void OnDisable() => Clear();
}
