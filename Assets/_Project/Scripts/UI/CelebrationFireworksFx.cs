using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ödül kutlaması havai fişeği (seviye sonu fişeğinin hafif kopyası; kullanıcı 2026-11-24): roketler sırayla yükselip
/// patlar — sırayla bir TAŞ fişeği (oyun taşları saçılır), bir normal kıvılcım fişeği — ve yukarıdan konfeti yağar
/// (seviye sonu gibi). Logo yok.
/// PERFORMANS: parçacık Image'ları ORTAK UiVfxPool'dan doğarken kiralanır, ölünce/kapanışta iade edilir (açılışta toplu
/// oluşturma ve kapanışta yok etme yok; sonraki tören havuzdan alır), TEK LateUpdate, prosedürel ortak doku
/// (LevelCompletionLogoAnimation atlası). Gösteri bitince bileşen kendini kapatır (boşta güncelleme yok).
/// Ses merkezi: GameEventSfx.PlayFireworkBurst.
/// </summary>
public sealed class CelebrationFireworksFx : MonoBehaviour
{
    private const int PoolSize = 300;           // aynı anda en çok parçacık (dizi sınırı)
    private const string PoolKey = "CelebrationFx.Particle";
    private const int PoolKeep = 160;           // havuzda bekletilecek en çok Image
    private const int ConfettiCount = 70;
    private const float ConfettiWindow = 1.4f;   // konfeti bu süre boyunca partiler halinde doğar
    private const int Rockets = 5;
    private const float RocketInterval = 0.42f;
    private const float RiseTime = 0.55f;
    private const int SparksPerBurst = 26;
    private const int GemsPerBurst = 7;

    private enum Kind : byte { None, Rocket, Spark, Gem, Confetti }

    private struct P
    {
        public Kind kind;
        public Vector2 pos, vel, target;
        public float age, life, size, rot, rotVel, phase;
        public Color color;
        public bool sound;
    }

    private readonly P[] parts = new P[PoolSize];
    private readonly Image[] images = new Image[PoolSize];
    private Sprite dot, sparkle, square;
    private Sprite[] gems;
    private RectTransform area;
    private float launchClock;
    private int launched, confettiSpawned;
    private float confettiClock;
    private bool running;

    private static readonly Color[] Palette =
    {
        new Color(1f, 0.85f, 0.25f), new Color(1f, 0.42f, 0.32f), new Color(0.40f, 0.85f, 1f),
        new Color(0.55f, 1f, 0.45f), new Color(1f, 0.55f, 0.95f),
    };

    public static CelebrationFireworksFx Create(RectTransform parent)
    {
        var go = new GameObject("CelebrationFireworks", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var fx = go.AddComponent<CelebrationFireworksFx>();
        fx.Build();
        return fx;
    }

    private void Build()
    {
        area = (RectTransform)transform;
        dot = LevelCompletionLogoAnimation.FxDotSprite;
        sparkle = LevelCompletionLogoAnimation.FxSparkleSprite;
        square = LevelCompletionLogoAnimation.FxSquareSprite;
        var lib = TileIconLibrary.Shared;
        gems = lib != null
            ? new[] { lib.Get(TileType.Gear), lib.Get(TileType.Core), lib.Get(TileType.Bolt), lib.Get(TileType.Plate) }
            : new Sprite[0];
        enabled = false;
    }

    public void Play()
    {
        launched = 0;
        launchClock = 0f;
        confettiSpawned = 0;
        confettiClock = 0f;
        running = true;
        enabled = true;
    }

    public void Stop()
    {
        running = false;
        for (int i = 0; i < PoolSize; i++) { parts[i].kind = Kind.None; Release(i); }
        enabled = false;
    }

    // Kapanışta (overlay yok edilir) kiralanmış her şey havuza döner.
    private void OnDestroy() { for (int i = 0; i < PoolSize; i++) Release(i); }

    private void Release(int i)
    {
        var img = images[i];
        if (img == null) return;
        images[i] = null;
        UiVfxPool.Return(PoolKey, img.gameObject, PoolKeep);
    }

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        Rect r = area.rect;
        float halfW = r.width > 10f ? r.width * 0.5f : 540f;
        float halfH = r.height > 10f ? r.height * 0.5f : 960f;

        if (running && launched < Rockets)
        {
            launchClock -= dt;
            if (launchClock <= 0f)
            {
                float lane = Mathf.Lerp(-0.7f, 0.7f, (launched * 0.618f) % 1f);
                var target = new Vector2(lane * halfW, Random.Range(halfH * 0.25f, halfH * 0.6f));
                int rocket = Spawn(Kind.Rocket, new Vector2(target.x * 0.8f, -halfH), Vector2.zero, RiseTime, 16f,
                                   Palette[launched % Palette.Length], target);
                if (rocket >= 0) parts[rocket].sound = launched % 2 == 0;   // sırayla: taş fişeği, normal fişek
                launched++;
                launchClock = RocketInterval;
            }
        }

        if (running && confettiSpawned < ConfettiCount)
        {
            confettiClock += dt;
            int due = Mathf.Min(ConfettiCount, Mathf.CeilToInt(ConfettiCount * confettiClock / ConfettiWindow));
            for (; confettiSpawned < due; confettiSpawned++)
            {
                var c = Color.HSVToRGB(Random.value, Random.Range(0.55f, 0.85f), 1f);
                var at = new Vector2(Random.Range(-halfW, halfW), halfH + Random.Range(20f, 120f));
                float w = Random.Range(14f, 20f);
                int idx = Spawn(Kind.Confetti, at, new Vector2(Random.Range(-50f, 50f), -Random.Range(230f, 330f)),
                                Random.Range(2.8f, 3.6f), w, c, at);
                if (idx < 0) break;
                images[idx].sprite = square;
                images[idx].rectTransform.sizeDelta = new Vector2(w, w * Random.Range(1.5f, 2f));
                parts[idx].rotVel = Random.Range(-200f, 200f);
                parts[idx].phase = Random.Range(0f, 6.28f);
            }
        }

        bool any = false;
        for (int i = 0; i < PoolSize; i++)
        {
            ref var p = ref parts[i];
            if (p.kind == Kind.None) continue;
            any = true;
            p.age += dt;
            var img = images[i];
            if (p.age >= p.life)
            {
                if (p.kind == Kind.Rocket) Burst(p.target, p.color, withGems: p.sound);
                p.kind = Kind.None;
                Release(i);
                continue;
            }
            float k = p.age / p.life;
            switch (p.kind)
            {
                case Kind.Rocket:
                    float e = 1f - (1f - k) * (1f - k);
                    p.pos = Vector2.LerpUnclamped(p.vel, p.target, e);   // vel = başlangıç noktası (roket)
                    img.color = p.color;
                    break;
                case Kind.Spark:
                    p.vel *= 1f - 1.6f * dt;
                    p.vel.y -= 260f * dt;
                    p.pos += p.vel * dt;
                    img.color = new Color(p.color.r, p.color.g, p.color.b, 1f - k * k);
                    break;
                case Kind.Confetti:
                    p.pos += p.vel * dt;
                    p.rot += p.rotVel * dt;
                    img.color = new Color(p.color.r, p.color.g, p.color.b, 1f - Mathf.InverseLerp(0.8f, 1f, k));
                    img.rectTransform.localScale = new Vector3(Mathf.Cos(p.age * 7f + p.phase), 1f, 1f);   // kağıt dönerek parlar
                    img.rectTransform.anchoredPosition = p.pos + new Vector2(Mathf.Sin(p.age * 3f + p.phase) * 26f, 0f);
                    img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, p.rot);
                    continue;
                case Kind.Gem:
                    p.vel.y -= 900f * dt;
                    p.pos += p.vel * dt;
                    p.rot += p.rotVel * dt;
                    img.color = new Color(1f, 1f, 1f, k < 0.8f ? 1f : (1f - k) * 5f);
                    break;
            }
            var rt = img.rectTransform;
            rt.anchoredPosition = p.pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, p.rot);
        }
        if (!any && (!running || (launched >= Rockets && confettiSpawned >= ConfettiCount))) enabled = false;
    }

    private void Burst(Vector2 at, Color color, bool withGems)
    {
        GameEventSfx.PlayFireworkBurst(0.6f);
        for (int s = 0; s < SparksPerBurst; s++)
        {
            float a = s / (float)SparksPerBurst * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
            float sp = Random.Range(380f, 620f);
            var c = Random.value < 0.25f ? Color.white : color;
            Spawn(Kind.Spark, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, Random.Range(0.8f, 1.2f),
                  Random.Range(14f, 26f), c, at);
        }
        if (!withGems || gems.Length == 0) return;
        for (int g = 0; g < GemsPerBurst; g++)
        {
            float a = Random.Range(0.15f, 0.85f) * Mathf.PI;   // yukarı yelpaze
            float sp = Random.Range(420f, 760f);
            int idx = Spawn(Kind.Gem, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, Random.Range(1.3f, 1.8f),
                            Random.Range(70f, 100f), Color.white, at);
            if (idx >= 0)
            {
                images[idx].sprite = gems[Random.Range(0, gems.Length)];
                parts[idx].rotVel = Random.Range(-260f, 260f);
            }
        }
    }

    private int Spawn(Kind kind, Vector2 pos, Vector2 vel, float life, float size, Color color, Vector2 target)
    {
        for (int i = 0; i < PoolSize; i++)
        {
            if (parts[i].kind != Kind.None) continue;
            parts[i] = new P
            {
                kind = kind, pos = pos, vel = kind == Kind.Rocket ? pos : vel, target = target,
                age = 0f, life = life, size = size, color = color, rot = Random.Range(0f, 360f),
            };
            var img = images[i];
            if (img == null)
            {
                img = UiVfxPool.RentImage(PoolKey, transform, "p");
                if (img == null) return -1;
                img.gameObject.layer = gameObject.layer;
                images[i] = img;
            }
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.sprite = kind == Kind.Spark && Random.value < 0.4f ? sparkle : dot;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.localScale = Vector3.one;
            img.color = color;
            img.enabled = true;
            return i;
        }
        return -1;   // havuz dolu: parçacık atlanır (görsel)
    }
}
