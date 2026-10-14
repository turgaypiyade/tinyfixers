using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ancient Mühür görseli — "Piramit Mührü": lacivert taş gövde, ortada dönen lacivert disk ve diskteki üç
/// renkli taş (kırmızı/sarı/yeşil kilit), üstteki kartuşta TEK sayaç (aktif taşın renginde). Mekanik AncientSealService'te;
/// bu sınıf yalnız olaylarını oynatır:
///   • taş toplandı: sayaç "pop" + disk hafif titrer
///   • kilit kapandı: taş yuvasına gömülüp grileşir; disk döner, sıradaki taş tepeye
///     gelir, kartuş yeni renge/sayıya döner
///   • mühür açıldı: çark hızlanarak döner, sonra mühür (gövde + çark + taşlar) birçok parçaya ayrılıp savrulur
/// Sprite'lar Resources/SealSafe altında; gövde 9-slice (köşeler hücre boyuna göre sabit). Çark ve sayaç iki
/// standart ölçüde (SizeTiers): kısa kenarı 2 hücre olan mühür ve her kenarı en az 3 hücre olan mühür.
/// </summary>
public sealed class AncientSealView : MonoBehaviour, ICollectionFxTarget
{
    private const string ArtPath = "SealSafe/";

    private static readonly Vector2 DiskCenter = new Vector2(0.5f, 0.5f);
    private const float BorderReferenceCells = 1.5f;
    private const float CounterWidthCells = 0.95f;     // × tier.counterScale
    private const float CounterTopInsetCells = 0.2f;   // × tier.counterScale

    // Standart ölçüler (kullanıcı oyunda 105 px hücreyle ayarladı, 2026-10-11). Hücre birimi:
    //   2×2:  çark 145 px, merkezin 6.75 px altında; sayaç taban boy.
    //   3×9:  çark 228.4 px, merkezin 13.6 px altında; sayaç ×1.5.
    private struct SizeTier { public float diskCells, diskDropCells, counterScale; }
    private static readonly SizeTier SmallTier = new SizeTier { diskCells = 145f / 105f, diskDropCells = 6.75f / 105f, counterScale = 1f };
    private static readonly SizeTier LargeTier = new SizeTier { diskCells = 228.375f / 105f, diskDropCells = 13.625f / 105f, counterScale = 1.5f };
    private static readonly Vector2 DiskPivot = new Vector2(0.5f, 0.5f);   // disk sprite'ı kendi merkezine kırpılı
    // Disk kanvasına göre: taş yuvası yarıçapı ve taş boyu.
    private const float SocketRadius = 0.3016f;
    private const float GemSize = 0.2569f;
    // Sıradaki slotların açısı (derece, UI): tepe, sağ-alt, sol-alt. Disk sprite'ı 3 katlı simetrik
    // (yuvalar 120° arayla); disk her adımda sıradaki slotu tepeye getirir.
    private static readonly float[] SlotAngles = { 90f, -30f, 210f };

    // Kartuş zemini toplanan rengin plakası (SealCounterPlateRed/Yellow/Green — mavi plakadan ton kaydırılmış
    // kopyalar; tint çarptığı için mavi zemin boyanamaz), sayı net BEYAZ + çok koyu kontur ve gölge: her renkli
    // zeminde aynı okunurluk. Gölge ayrı bir kopya yazıdır (TMP underlay keyword'ü build'de soyulabilir).
    private static readonly string[] PlateNames =
        { "SealCounterPlateRed", "SealCounterPlateYellow", "SealCounterPlateGreen" };
    private static readonly Sprite[] plateSprites = new Sprite[3];
    private static Sprite neutralPlate;
    private static readonly Color32 CounterDark = new Color32(28, 14, 10, 255);
    private const float CounterOutlineWidth = 0.4f;
    private const float ShadowDrop = 0.012f;   // gövde boyuna oranla aşağı kayma

    private const float PopSeconds = 0.18f;
    private const float JiggleSeconds = 0.25f;
    private const float JiggleDegrees = 3.5f;
    private const float SinkSeconds = 0.25f;
    private const float TurnSeconds = 0.5f;
    // Kırılma: çark hızlanır (SpinSeconds sonunda ~MaxSpinDegPerSec), sonra parçalar ShatterSeconds boyunca savrulur.
    private const float SpinSeconds = 0.5f;
    private const float MaxSpinDegPerSec = 1600f;
    private const float ShatterSeconds = 0.65f;
    // Parçalar mühür alanından savrulup çıkınca taş akışı başlar (kullanıcı: "kasa yok olsun, sonra düşsün").
    private const float ReleaseCellsAfterShatter = 0.3f;
    private const int MaxShardAxis = 5;

    private AncientSealService service;
    private int origin = -1;

    private RectTransform body;
    private Image bodyImage;
    private RectTransform disk;
    private Image diskImage;
    private RectTransform counterRoot;          // pop/flip animasyonu bunu ölçekler (yazı + gölge birlikte)
    private Image counterPlateImage;
    private TextMeshProUGUI counter;
    private TextMeshProUGUI counterShadow;
    private readonly RectTransform[] gems = new RectTransform[3];       // slot sırasıyla
    private readonly Image[] gemSpent = new Image[3];
    private readonly int[] slotLock = { 0, 1, 2 };                       // slot → kilit index'i

    private float cellUnit = 1f;
    private float baseAngle;          // gösterilen slotu tepeye getiren disk açısı (Ordered)
    private float jiggleAngle;
    private int shownStep;            // diskin şu an gösterdiği adım
    private bool broken;
    private Coroutine jiggleCo;
    private Coroutine popCo;
    private Coroutine gemPulseCo;
    private int gemPulseSlot = -1;
    private readonly Queue<IEnumerator> sequence = new();
    private Coroutine sequenceCo;

    public static bool HasArt => Resources.Load<Sprite>(ArtPath + "SealBody") != null;

    public static AncientSealView Create(RectTransform parent)
    {
        var go = new GameObject("AncientSeal", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<AncientSealView>();
        view.Build();
        return view;
    }

    private void Build()
    {
        body = NewImage("Body", transform, Load("SealBody"), out bodyImage);
        bodyImage.type = Image.Type.Sliced;
        bodyImage.preserveAspect = false;
        Stretch(body);

        disk = NewImage("Disk", body, Load("SealDisk"), out diskImage);
        disk.anchorMin = disk.anchorMax = DiskCenter;
        disk.pivot = DiskPivot;

        for (int i = 0; i < gems.Length; i++)
        {
            gems[i] = NewImage("Gem" + i, disk, null, out _);
            float a = SlotAngles[i] * Mathf.Deg2Rad;
            Vector2 c = DiskPivot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * SocketRadius;
            gems[i].anchorMin = gems[i].anchorMax = c;
            var spent = NewImage("Spent", gems[i], Load("SealGemSpent"), out gemSpent[i]);
            Stretch(spent);
            gemSpent[i].color = new Color(1f, 1f, 1f, 0f);
        }

        var rootGo = new GameObject("Counter", typeof(RectTransform));
        rootGo.layer = gameObject.layer;
        counterRoot = (RectTransform)rootGo.transform;
        counterRoot.SetParent(body, false);
        counterRoot.anchorMin = counterRoot.anchorMax = new Vector2(0.5f, 1f);
        var plate = NewImage("Plate", counterRoot, Load("SealCounterPlate"), out counterPlateImage);
        Stretch(plate);
        counterShadow = NewCounterText("Shadow", CounterDark);   // önce: arkada kalır
        counter = NewCounterText("Text", Color.white);
    }

    /// Gövde tüm dikdörtgeni doldurur; köşeler hücre boyuna göre sabit, çark + sayaç yer oldukça büyür.
    public void SetBodySize(float width, float height, float cellSize)
    {
        if (body == null) return;
        // Legacy undersized entries still fit without changing their logical footprint.
        float unit = Mathf.Max(1f, Mathf.Min(cellSize, Mathf.Min(width, height) * 0.5f));
        body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
        body.pivot = new Vector2(0.5f, 0.5f);
        body.sizeDelta = new Vector2(width, height);
        body.anchoredPosition = Vector2.zero;

        // Use the imported sprite width so texture downscaling also preserves border thickness.
        if (bodyImage.sprite != null)
            bodyImage.pixelsPerUnitMultiplier = bodyImage.sprite.rect.width
                / (bodyImage.pixelsPerUnit * unit * BorderReferenceCells);

        var plateSprite = counterPlateImage.sprite;
        float aspect = plateSprite != null ? plateSprite.rect.height / plateSprite.rect.width : 0.42f;

        // Kısa kenar 3 hücre ve üstüyse büyük standart (3 hücre yükseklik büyük çarkı alır).
        var tier = Mathf.Min(width, height) / unit >= 2.5f ? LargeTier : SmallTier;
        float fu = unit * tier.counterScale;

        cellUnit = unit;
        float d = unit * tier.diskCells;
        disk.sizeDelta = new Vector2(d, d);
        disk.anchoredPosition = new Vector2(0f, -unit * tier.diskDropCells);
        for (int i = 0; i < gems.Length; i++)
        {
            gems[i].sizeDelta = new Vector2(d * GemSize, d * GemSize);
            gems[i].anchoredPosition = Vector2.zero;
        }
        float counterWidth = fu * CounterWidthCells;
        counterRoot.sizeDelta = new Vector2(counterWidth, counterWidth * aspect);
        counterRoot.anchoredPosition = new Vector2(0f, -fu * CounterTopInsetCells);
        counter.fontSize = counterShadow.fontSize = fu * 0.24f;
        counterShadow.rectTransform.anchoredPosition = new Vector2(0f, -fu * ShadowDrop);
    }

    /// GridSpawner çağırır: service'e bağla, kilit sırasını slotlara yerleştir, mevcut durumu çiz.
    public void Setup(AncientSealService svc, int sealOrigin)
    {
        service = svc;
        origin = sealOrigin;
        for (int step = 0; step < slotLock.Length; step++)
        {
            int color = service != null ? service.GetLockAtStep(origin, step) : step;
            slotLock[step] = color >= 0 ? color : step;
            var gemImage = gems[step].GetComponent<Image>();
            gemImage.sprite = Load(GemName(slotLock[step]));
        }

        if (service != null)
        {
            service.OnCollected += HandleCollected;
            service.OnLockClosed += HandleLockClosed;
            service.OnSealBroken += HandleSealBroken;
        }
        service?.RegisterView(origin, this);
        ApplyStateInstant();
    }

    public Vector3 GetCollectionTarget(int color)
    {
        int slot = System.Array.IndexOf(slotLock, color);
        var target = slot >= 0 ? gems[slot] : counterRoot;
        return target.TransformPoint(target.rect.center);
    }

    public bool IsCollectionTargetReady(int color)
        => !broken && slotLock[Mathf.Clamp(shownStep, 0, 2)] == color && sequenceCo == null;

    // Arayüz üzerinden null kontrolü Unity'nin yok-edilmiş kontrolünü atlar → burada `this != null` (Unity ==).
    public bool IsCollectionAlive => this != null && isActiveAndEnabled;

    private void OnEnable()
    {
        if (service != null && !broken) ApplyStateInstant();
    }

    private void OnDestroy()
    {
        if (service == null) return;
        if (broken) service.ReleaseBreakHold(origin);   // kırılma yarıda kesildiyse hücreler kilitli kalmasın
        service.UnregisterView(origin, this);
        service.OnCollected -= HandleCollected;
        service.OnLockClosed -= HandleLockClosed;
        service.OnSealBroken -= HandleSealBroken;
    }

    private int ClosedSteps()
    {
        int n = 0;
        while (n < slotLock.Length && service != null && service.GetRemaining(origin, slotLock[n]) <= 0) n++;
        return n;
    }

    // Animasyonsuz: taşlar, disk açısı ve sayaç servis durumundan.
    private void ApplyStateInstant()
    {
        if (service == null) return;
        for (int i = 0; i < gems.Length; i++)
        {
            bool closed = service.GetRemaining(origin, slotLock[i]) <= 0;
            SetGemSpent(i, closed ? 1f : 0f);
        }
        shownStep = Mathf.Min(ClosedSteps(), slotLock.Length - 1);
        baseAngle = AngleForStep(shownStep);
        jiggleAngle = 0f;
        ApplyDiskAngle();
        RefreshCounter();
    }

    private void RefreshCounter()
    {
        if (service == null || counter == null) return;
        int active = service.GetActiveLock(origin);
        if (active < 0 || active >= AncientSealService.LockCount) { SetCounter("", 0); return; }
        SetCounter(service.GetRemaining(origin, active).ToString(), active);
    }

    // ── Olaylar ─────────────────────────────────────────────────────────────

    private void HandleCollected(int o, int lockIdx, int remaining, int total)
    {
        if (o != origin || broken) return;
        if (!isActiveAndEnabled) { ApplyStateInstant(); return; }

        // Sayaç: aktif kilit (dönüş sırasında değişmesin diye yalnız gösterilen kilidin vuruşu yazılır).
        if (slotLock[Mathf.Clamp(shownStep, 0, 2)] == lockIdx)
            SetCounter(remaining.ToString(), lockIdx);
        if (popCo != null) StopCoroutine(popCo);
        popCo = StartCoroutine(CoPop(counterRoot));
        if (jiggleCo != null) StopCoroutine(jiggleCo);
        jiggleCo = StartCoroutine(CoJiggle());
        // Varış nabzı: taşı "yutan" aktif yuva kısa bir an şişer.
        int slot = System.Array.IndexOf(slotLock, lockIdx);
        if (slot >= 0 && gemSpent[slot].color.a <= 0f)
        {
            StopGemPulse();
            gemPulseSlot = slot;
            gemPulseCo = StartCoroutine(CoGemPulse(slot));
        }
    }

    private void StopGemPulse()
    {
        if (gemPulseCo != null) StopCoroutine(gemPulseCo);
        if (gemPulseSlot >= 0 && gemSpent[gemPulseSlot].color.a <= 0f) gems[gemPulseSlot].localScale = Vector3.one;
        gemPulseCo = null;
        gemPulseSlot = -1;
    }

    private IEnumerator CoGemPulse(int slot)
    {
        var rt = gems[slot];
        const float seconds = 0.2f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (gemSpent[slot].color.a > 0f) break;   // kilit kapandı: gömülme animasyonu ölçeği yönetir
            rt.localScale = Vector3.one * (1f + 0.28f * Mathf.Sin(t / seconds * Mathf.PI));
            yield return null;
        }
        if (gemSpent[slot].color.a <= 0f) rt.localScale = Vector3.one;
        gemPulseCo = null;
        gemPulseSlot = -1;
    }

    private void HandleLockClosed(int o, int lockIdx)
    {
        if (o != origin || broken) return;
        if (!isActiveAndEnabled) { ApplyStateInstant(); return; }
        int slot = System.Array.IndexOf(slotLock, lockIdx);
        if (slot < 0) return;
        if (gemPulseSlot == slot) StopGemPulse();
        Enqueue(CoSink(slot));
        if (service != null && service.GetRemainingTotal(origin) > 0)
            Enqueue(CoTurnToNext());
    }

    private void HandleSealBroken(int o)
    {
        if (o != origin || broken) return;
        broken = true;
        if (!isActiveAndEnabled) { Destroy(gameObject); return; }
        LiftAboveRevealedLayers();
        Enqueue(CoBreak());
    }

    private void Enqueue(IEnumerator step)
    {
        sequence.Enqueue(step);
        if (sequenceCo == null) sequenceCo = StartCoroutine(CoRunSequence());
    }

    private IEnumerator CoRunSequence()
    {
        while (sequence.Count > 0)
            yield return sequence.Dequeue();
        sequenceCo = null;
    }

    // ── Animasyonlar ────────────────────────────────────────────────────────

    private IEnumerator CoPop(RectTransform rt)
    {
        for (float t = 0f; t < PopSeconds; t += Time.deltaTime)
        {
            float k = t / PopSeconds;
            rt.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        rt.localScale = Vector3.one;
        popCo = null;
    }

    private IEnumerator CoJiggle()
    {
        for (float t = 0f; t < JiggleSeconds; t += Time.deltaTime)
        {
            float k = t / JiggleSeconds;
            jiggleAngle = JiggleDegrees * (1f - k) * Mathf.Sin(k * Mathf.PI * 4f);
            ApplyDiskAngle();
            yield return null;
        }
        jiggleAngle = 0f;
        ApplyDiskAngle();
        jiggleCo = null;
    }

    // Taş yuvasına gömülür (küçülür) ve grileşir.
    private IEnumerator CoSink(int slot)
    {
        var rt = gems[slot];
        for (float t = 0f; t < SinkSeconds; t += Time.deltaTime)
        {
            float k = t / SinkSeconds;
            float dip = k < 0.4f ? Mathf.Lerp(1f, 0.74f, k / 0.4f) : Mathf.Lerp(0.74f, 0.84f, (k - 0.4f) / 0.6f);
            rt.localScale = Vector3.one * dip;
            SetGemSpent(slot, k);
            yield return null;
        }
        SetGemSpent(slot, 1f);
    }

    // Disk sıradaki taşı tepeye getirir (taş sürtünmesi: sona doğru yavaşlar, hafif geri yaylanır); kartuş yarıda takla atar.
    private IEnumerator CoTurnToNext()
    {
        yield return new WaitForSeconds(0.1f);
        float from = baseAngle, to = AngleForStep(Mathf.Min(shownStep + 1, slotLock.Length - 1));
        bool flipped = false;
        var crt = counterRoot;
        for (float t = 0f; t < TurnSeconds; t += Time.deltaTime)
        {
            float k = t / TurnSeconds;
            baseAngle = Mathf.LerpUnclamped(from, to, EaseOutBack(k));
            ApplyDiskAngle();
            // Kartuş: önce kapanır (y→0), yarıda yeni renk/sayı, sonra açılır.
            float flip = Mathf.Abs(Mathf.Cos(k * Mathf.PI));
            crt.localScale = new Vector3(1f, flip, 1f);
            if (!flipped && k >= 0.5f)
            {
                flipped = true;
                shownStep = Mathf.Min(shownStep + 1, slotLock.Length - 1);
                RefreshCounter();
            }
            yield return null;
        }
        baseAngle = to;
        ApplyDiskAngle();
        crt.localScale = Vector3.one;
        if (!flipped) { shownStep = Mathf.Min(shownStep + 1, slotLock.Length - 1); RefreshCounter(); }
    }

    // Çark hızlanarak döner (gövde titrer, sayaç söner); sonra mühür birçok parçaya ayrılıp savrulur.
    // Parça = dönebilen stencil pencere (Mask) + içinde o anki gövdenin kopyası → kesik kenarlar gerçek görsel.
    // Kopyalar yalnız kırılma anında bir kez üretilir ve mühürle birlikte yok edilir.
    private IEnumerator CoBreak()
    {
        SetCounter("", -1);
        Vector2 bodyHome = body.anchoredPosition;
        float speed = 120f;
        for (float t = 0f; t < SpinSeconds; t += Mathf.Min(Time.deltaTime, 1f / 30f))
        {
            float k = t / SpinSeconds;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            speed = Mathf.Lerp(120f, MaxSpinDegPerSec, k * k);
            baseAngle += speed * dt;
            jiggleAngle = 0f;
            ApplyDiskAngle();
            float shake = cellUnit * 0.025f * k;
            body.anchoredPosition = bodyHome + new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));
            counterRoot.localScale = Vector3.one * Mathf.Max(0f, 1f - k * 3f);
            yield return null;
        }
        body.anchoredPosition = bodyHome;
        counterRoot.gameObject.SetActive(false);

        var shards = BuildShards(out var velocities, out var spins);
        body.gameObject.SetActive(false);

        float gravity = cellUnit * 9f;
        bool released = false;
        for (float t = 0f; t < ShatterSeconds; t += Mathf.Min(Time.deltaTime, 1f / 30f))
        {
            if (!released && t >= ReleaseCellsAfterShatter) { released = true; ReleaseCells(); }
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            float k = t / ShatterSeconds;
            for (int i = 0; i < shards.Count; i++)
            {
                velocities[i] += Vector2.down * (gravity * dt);
                var rt = shards[i].rect;
                rt.anchoredPosition += velocities[i] * dt;
                rt.localRotation = Quaternion.Euler(0f, 0f, rt.localEulerAngles.z + spins[i] * dt);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, k);
                shards[i].group.alpha = k < 0.45f ? 1f : 1f - (k - 0.45f) / 0.55f;
            }
            yield return null;
        }
        ReleaseCells();
        Destroy(gameObject);
    }

    private void ReleaseCells() => service?.ReleaseBreakHold(origin);

    private struct Shard { public RectTransform rect; public CanvasGroup group; }

    private List<Shard> BuildShards(out List<Vector2> velocities, out List<float> spins)
    {
        Vector2 size = body.rect.size;
        int cols = Mathf.Clamp(Mathf.RoundToInt(size.x / cellUnit * 1.5f), 3, MaxShardAxis);
        int rows = Mathf.Clamp(Mathf.RoundToInt(size.y / cellUnit * 1.5f), 3, MaxShardAxis);
        float[] xs = Cuts(cols), ys = Cuts(rows);
        var list = new List<Shard>(cols * rows);
        velocities = new List<Vector2>(cols * rows);
        spins = new List<float>(cols * rows);
        Vector2 half = size * 0.5f;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                // Parça dikdörtgeni (gövde merkezli, y yukarı).
                float x0 = xs[c] * size.x - half.x, x1 = xs[c + 1] * size.x - half.x;
                float y1 = half.y - ys[r] * size.y, y0 = half.y - ys[r + 1] * size.y;
                var center = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);

                var go = new GameObject("SealShard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                    typeof(Mask), typeof(CanvasGroup));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(x1 - x0, y1 - y0);
                rt.anchoredPosition = body.anchoredPosition + center;
                go.GetComponent<Image>().raycastTarget = false;
                go.GetComponent<Mask>().showMaskGraphic = false;
                var group = go.GetComponent<CanvasGroup>();
                group.interactable = group.blocksRaycasts = false;

                var copy = (RectTransform)Instantiate(body.gameObject, rt, false).transform;
                copy.anchorMin = copy.anchorMax = copy.pivot = new Vector2(0.5f, 0.5f);
                copy.sizeDelta = size;
                copy.anchoredPosition = -center;
                copy.localScale = Vector3.one;

                Vector2 dir = center.sqrMagnitude > 1f ? center.normalized : Random.insideUnitCircle.normalized;
                velocities.Add(dir * (cellUnit * Random.Range(2.2f, 3.6f)) + Vector2.up * (cellUnit * Random.Range(1.5f, 2.6f)));
                spins.Add(Random.Range(220f, 520f) * (Random.value < 0.5f ? -1f : 1f));
                list.Add(new Shard { rect = rt, group = group });
            }
        return list;
    }

    // 0..1 arası düzensiz kesimler (eşit ızgara "kare kare" görünmesin).
    private static float[] Cuts(int n)
    {
        var cuts = new float[n + 1];
        cuts[n] = 1f;
        for (int i = 1; i < n; i++)
            cuts[i] = (i + Random.Range(-0.22f, 0.22f)) / n;
        return cuts;
    }

    // ── Yardımcılar ─────────────────────────────────────────────────────────

    private TextMeshProUGUI NewCounterText(string name, Color32 color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(counterRoot, false);
        Stretch(rt);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (CommonPopupSkin.Shared != null && CommonPopupSkin.Shared.font != null)
            text.font = CommonPopupSkin.Shared.font;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        CrispTextMaterial.Apply(text);
        text.color = color;
        TmpOutline.Apply(text, CounterOutlineWidth, CounterDark);
        return text;
    }

    /// Yazı + gölgesi beyaz; plaka toplanan rengin plakası (lockIdx < 0 → mavi plaka).
    private void SetCounter(string value, int lockIdx)
    {
        counter.text = counterShadow.text = value;
        var plate = PlateFor(lockIdx);
        if (plate != null && counterPlateImage.sprite != plate) counterPlateImage.sprite = plate;
    }

    private static Sprite PlateFor(int lockIdx)
    {
        if (lockIdx < 0 || lockIdx >= PlateNames.Length)
            return neutralPlate != null ? neutralPlate : (neutralPlate = Load("SealCounterPlate"));
        if (plateSprites[lockIdx] == null) plateSprites[lockIdx] = Load(PlateNames[lockIdx]);
        return plateSprites[lockIdx] != null ? plateSprites[lockIdx] : PlateFor(-1);
    }

    private void ApplyDiskAngle()
    {
        float a = baseAngle + jiggleAngle;
        disk.localRotation = Quaternion.Euler(0f, 0f, a);
        // Taşların parıltısı hep sol-üstte kalsın: disk dönerken taşlar ters döner.
        for (int i = 0; i < gems.Length; i++)
            gems[i].localRotation = Quaternion.Euler(0f, 0f, -a);
    }

    private void SetGemSpent(int slot, float k)
    {
        var c = gemSpent[slot].color;
        c.a = Mathf.Clamp01(k);
        gemSpent[slot].color = c;
        if (k >= 1f) gems[slot].localScale = Vector3.one * 0.84f;
        else if (k <= 0f) gems[slot].localScale = Vector3.one;
    }

    // Kasa kırılınca altındaki katmanlar (ör. grass) hemen açılır ve kasa kökünün üstünde çizilir; kırılma
    // animasyonu arkada kalmasın diye kasayı efekt katmanına taşı (SafeObstacleView ile aynı kural).
    private void LiftAboveRevealedLayers()
    {
        var board = service != null ? service.Board : null;
        var overlay = board != null ? board.TilesTopOverlayRoot : null;
        if (overlay == null || transform.parent == overlay) return;
        transform.SetParent(overlay, worldPositionStays: true);
        transform.SetAsLastSibling();
        foreach (var t in GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = overlay.gameObject.layer;
    }

    // Adım k'daki slotu tepeye (90°) getiren açı; hep ileri (saat yönünün tersine) döner.
    private static float AngleForStep(int step)
    {
        float a = 0f;
        for (int i = 1; i <= step && i < SlotAngles.Length; i++)
            a += Mathf.Repeat(SlotAngles[i - 1] - SlotAngles[i], 360f);
        return a;
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static string GemName(int lockIdx)
        => lockIdx == 0 ? "SealGemRed" : lockIdx == 1 ? "SealGemYellow" : "SealGemGreen";

    private static Sprite Load(string name) => Resources.Load<Sprite>(ArtPath + name);

    private RectTransform NewImage(string name, Transform parent, Sprite sprite, out Image img)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
