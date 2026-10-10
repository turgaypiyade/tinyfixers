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
///   • mühür açıldı: disk hızla bir tur döner, ortadan ikiye ayrılıp yanlara kayar, gövde söner
/// Sprite'lar Resources/SealSafe altında; gövde 9-slice, disk ve sayaç hücre boyuna göre sabittir.
/// </summary>
public sealed class AncientSealView : MonoBehaviour
{
    private const string ArtPath = "SealSafe/";

    private static readonly Vector2 DiskCenter = new Vector2(0.5f, 0.5f);
    private const float DiskSizeCells = 1.08f;
    private const float BorderReferenceCells = 1.5f;
    private const float CounterWidthCells = 1f;
    private const float CounterTopInsetCells = 0.24f;
    private static readonly Vector2 DiskPivot = new Vector2(0.5f, 0.5f);   // disk sprite'ı kendi merkezine kırpılı
    // Disk kanvasına göre: taş yuvası yarıçapı ve taş boyu.
    private const float SocketRadius = 0.3016f;
    private const float GemSize = 0.2569f;
    // Sıradaki slotların açısı (derece, UI): tepe, sağ-alt, sol-alt. Disk sprite'ı 3 katlı simetrik
    // (yuvalar 120° arayla); disk her adımda sıradaki slotu tepeye getirir.
    private static readonly float[] SlotAngles = { 90f, -30f, 210f };

    // Kartuş sayısı aktif taşın renginde (açık tonlar), kartuş zemininin çok koyu laciverti kontur + gölge:
    // renkli rakam koyu lacivert zeminde ancak kalın koyu konturla okunur. Gölge ayrı bir kopya yazıdır
    // (TMP underlay keyword'ü build'de soyulabildiği için materyale güvenilmez).
    private static readonly Color32[] LockColors =
    {
        new Color32(255, 72, 72, 255),    // kırmızı
        new Color32(255, 214, 48, 255),   // sarı
        new Color32(88, 226, 96, 255),    // yeşil
    };
    private static readonly Color32 CounterDark = new Color32(10, 22, 66, 255);
    private const float CounterOutlineWidth = 0.4f;
    private const float ShadowDrop = 0.012f;   // gövde boyuna oranla aşağı kayma

    private const float PopSeconds = 0.18f;
    private const float JiggleSeconds = 0.25f;
    private const float JiggleDegrees = 3.5f;
    private const float SinkSeconds = 0.25f;
    private const float TurnSeconds = 0.5f;
    private const float SpinSeconds = 0.4f;
    private const float SplitSeconds = 0.4f;

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

    private float baseAngle;          // gösterilen slotu tepeye getiren disk açısı (Ordered)
    private float jiggleAngle;
    private int shownStep;            // diskin şu an gösterdiği adım
    private bool broken;
    private Coroutine jiggleCo;
    private Coroutine popCo;
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
        counter = NewCounterText("Text", LockColors[0]);
    }

    /// Gövde tüm dikdörtgeni doldurur; köşeler, disk ve sayaç hücre boyuna göre sabit kalır.
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

        float d = unit * DiskSizeCells;
        disk.sizeDelta = new Vector2(d, d);
        disk.anchoredPosition = Vector2.zero;
        for (int i = 0; i < gems.Length; i++)
        {
            gems[i].sizeDelta = new Vector2(d * GemSize, d * GemSize);
            gems[i].anchoredPosition = Vector2.zero;
        }
        float counterWidth = unit * CounterWidthCells;
        var plateSprite = counterPlateImage.sprite;
        float aspect = plateSprite != null ? plateSprite.rect.height / plateSprite.rect.width : 0.42f;
        counterRoot.sizeDelta = new Vector2(counterWidth, counterWidth * aspect);
        counterRoot.anchoredPosition = new Vector2(0f, -unit * CounterTopInsetCells);
        counter.fontSize = counterShadow.fontSize = unit * 0.24f;
        counterShadow.rectTransform.anchoredPosition = new Vector2(0f, -unit * ShadowDrop);
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

    private void OnEnable()
    {
        if (service != null && !broken) ApplyStateInstant();
    }

    private void OnDestroy()
    {
        if (service == null) return;
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
    }

    private void HandleLockClosed(int o, int lockIdx)
    {
        if (o != origin || broken) return;
        if (!isActiveAndEnabled) { ApplyStateInstant(); return; }
        int slot = System.Array.IndexOf(slotLock, lockIdx);
        if (slot < 0) return;
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

    // Disk hızla bir tur döner, ortadan ikiye ayrılıp yanlara kayar; gövde ve sayaç söner.
    private IEnumerator CoBreak()
    {
        SetCounter("", -1);
        // En az ~300° hızlanarak döner ve tam tura (0°) oturur: yarımlar disk sprite'ıyla aynı açıda doğar.
        float from = baseAngle;
        float to = Mathf.Ceil((from + 300f) / 360f) * 360f;
        for (float t = 0f; t < SpinSeconds; t += Time.deltaTime)
        {
            float k = t / SpinSeconds;
            baseAngle = Mathf.Lerp(from, to, k * k);
            ApplyDiskAngle();
            yield return null;
        }
        baseAngle = to;
        jiggleAngle = 0f;
        ApplyDiskAngle();

        var left = MakeHalf(Image.OriginHorizontal.Left, out var leftGroup);
        var right = MakeHalf(Image.OriginHorizontal.Right, out var rightGroup);
        // Taşlar bulundukları yarımla birlikte kayar (tepe taşı sağa).
        float centerX = disk.position.x;
        foreach (var gem in gems)
            gem.SetParent(gem.position.x < centerX - 0.01f ? left : right, worldPositionStays: true);
        disk.gameObject.SetActive(false);

        float shift = disk.rect.width * 0.6f;
        for (float t = 0f; t < SplitSeconds; t += Time.deltaTime)
        {
            float k = t / SplitSeconds;
            float e = 1f - (1f - k) * (1f - k);
            left.anchoredPosition = new Vector2(-shift * e, 0f);
            right.anchoredPosition = new Vector2(shift * e, 0f);
            leftGroup.alpha = rightGroup.alpha = 1f - k;
            body.localScale = Vector3.one * (1f + 0.08f * e);
            bodyImage.color = Fade(1f - k);
            counterPlateImage.color = Fade(1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }

    // Diskin bir yarısı: aynı sprite, yatay doldurma %50 (disk o an 0°'de; taşlar ayrıca taşınır).
    private RectTransform MakeHalf(Image.OriginHorizontal side, out CanvasGroup group)
    {
        var rt = NewImage("DiskHalf", body, diskImage.sprite, out var img);
        rt.anchorMin = rt.anchorMax = DiskCenter;
        rt.pivot = DiskPivot;
        rt.sizeDelta = disk.sizeDelta;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)side;
        img.fillAmount = 0.5f;
        group = rt.gameObject.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        return rt;
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

    /// Yazı + gölgesi; lockIdx < 0 → AnyColor toplamı (beyaz).
    private void SetCounter(string value, int lockIdx)
    {
        counter.text = counterShadow.text = value;
        counter.color = lockIdx >= 0 && lockIdx < LockColors.Length ? LockColors[lockIdx] : (Color32)Color.white;
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

    private static Color Fade(float a) => new Color(1f, 1f, 1f, Mathf.Clamp01(a));

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
