using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Usta Ligi girişi (Docs/MasterLeague_Plan.md): her lig SEZONUNDA (ana yol her bittiğinde) ana menüye ilk dönüşte bir kez oynar.
///   1. Atölye kapısı (ChapterTheme.introBackground): kepenge çok yakından başlar, yavaşça tüm resme açılır; "USTA LİGİ"
///   2. Kapıya dalış: görüntü kepengin karanlığına (lacivert) kararır
///   3. Asansör: lacivert zeminde kat sayacı; kule çatısı (gameBackground) OPAK olarak aşağıdan yukarı kayar
///      (perde çatının ARKASINDA kalır → iki görselin renkleri karışmaz)
///   4. Çatı: "LİG N" + kısa açıklama + BAŞLA → ana menü
/// Sahneye/prefab'a bağımlı değil: kendi üst katman Canvas'ını kurar, bitince kendini yok eder. Dokunuş o anki aşamayı
/// hızlandırır. Tüm zamanlar unscaled; kare başı ayırma yok (sayaç metni yalnız değer değişince yazılır).
/// </summary>
public sealed class MasterLeagueIntro : MonoBehaviour, IPointerClickHandler
{
    public const string KeySeen = "master_league_intro_seen";   // CloudSaveManifest'te

    private const int SortingOrder = 900;                       // ana menü popup'larının üstü
    private const float FadeIn = 0.8f;
    private const float RevealDuration = 3.0f;      // kepenge yakından → tüm resim
    private const float RevealStartScale = 2.4f;
    private const float TitleHold = 2.2f;
    private const float DiveDuration = 1.1f;        // kepenge dalış + kararma
    private const float DiveScale = 2.6f;
    private const float CounterDuration = 1.4f;     // lacivert zeminde kat sayacı
    private const float RiseDuration = 1.6f;        // çatı aşağıdan gelir
    private const float FadeOut = 0.4f;
    private static readonly Vector2 DoorPivot = new(0.5f, 0.78f);   // kapı görselinde kepengin merkezi (üstten ~%21)
    private static readonly Color Navy = new(0.02f, 0.06f, 0.16f, 1f);

    private RectTransform root;
    private CanvasGroup rootGroup;
    private RectTransform gate;
    private RectTransform roof;
    private Image flash;
    private CanvasGroup gateTitleGroup;
    private CanvasGroup floorGroup;
    private TMP_Text floorText;
    private CanvasGroup roofGroup;
    private RectTransform roofTitle;
    private bool skip;
    private bool started;

    /// Lig açık ve intro görülmediyse oynatır. Dönüş: oynatıldı mı.
    public static bool TryPlay(Component host, LevelCatalog catalog)
    {
        if (host == null || catalog == null) return false;
        // Önce IsUnlocked: yeni lig sezonunda intro bayrağını sıfırlar (her sezon yeni intro).
        if (!MasterLeague.IsUnlocked(catalog) || PlayerPrefs.GetInt(KeySeen, 0) == 1) return false;
        var theme = ChapterTheme.Active;
        if (theme == null || theme.introBackground == null || theme.gameBackground == null) return false;
        var canvas = host.GetComponentInParent<Canvas>();
        if (canvas == null) return false;

        var go = new GameObject("MasterLeagueIntro", typeof(RectTransform));
        go.layer = canvas.rootCanvas.gameObject.layer;
        go.transform.SetParent(canvas.rootCanvas.transform, false);
        go.AddComponent<MasterLeagueIntro>().Build(theme, catalog.MaximumGlobalLevel);
        return true;
    }

    public void OnPointerClick(PointerEventData eventData) => skip = true;

    private void Build(ChapterTheme theme, int floors)
    {
        root = (RectTransform)transform;
        Stretch(root);
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;
        gameObject.AddComponent<GraphicRaycaster>();
        rootGroup = gameObject.AddComponent<CanvasGroup>();
        var blocker = gameObject.AddComponent<Image>();   // dokunuşları yutar + sahne geçişi siyahı
        blocker.color = Navy;

        gate = Picture("Gate", theme.introBackground);
        flash = NewImage("Curtain", root);   // kapının üstünde, çatının ALTINDA
        Stretch(flash.rectTransform);
        flash.color = new Color(Navy.r, Navy.g, Navy.b, 0f);
        flash.raycastTarget = false;
        roof = Picture("Roof", theme.gameBackground);

        gateTitleGroup = Group("GateTitle", root, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.36f));
        Label(gateTitleGroup.transform, "Title", GameLocalization.Get("master_league_title"), 120f, new Vector2(0f, 0.45f), Vector2.one);
        Label(gateTitleGroup.transform, "Sub", GameLocalization.Get("master_league_intro_gate"), 48f, Vector2.zero, new Vector2(1f, 0.45f));

        floorGroup = Group("Floor", root, new Vector2(0.2f, 0.80f), new Vector2(0.8f, 0.92f));
        floorText = Label(floorGroup.transform, "Counter", "", 96f, Vector2.zero, Vector2.one);

        roofGroup = Group("RoofInfo", root, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.42f));
        roofTitle = Label(roofGroup.transform, "Title", GameLocalization.GetFormat("master_league_title_lap", MasterLeague.LevelNumber),
            130f, new Vector2(0f, 0.68f), Vector2.one).rectTransform;
        Label(roofGroup.transform, "Sub", GameLocalization.Get("master_league_intro_roof"), 44f, new Vector2(0f, 0.36f), new Vector2(1f, 0.68f));
        BuildStartButton(roofGroup.transform);

        gateTitleGroup.alpha = 0f;
        floorGroup.alpha = 0f;
        roofGroup.alpha = 0f;
        roofGroup.interactable = roofGroup.blocksRaycasts = false;
        rootGroup.alpha = 0f;
        StartCoroutine(Play(floors));
    }

    private IEnumerator Play(int floors)
    {
        yield return null;   // yerleşim boyutları otursun (envelope ölçüsü ekran boyutundan)
        Envelope(gate);
        Envelope(roof);
        SetPivotKeepingPlace(gate, DoorPivot);
        // Başlangıç: çatının üst kenarı ekranın alt kenarında (görsel ekrandan uzun olabilir → kendi boyuyla hesapla).
        float startY = -(root.rect.height + roof.sizeDelta.y) * 0.5f;
        roof.anchoredPosition = new Vector2(0f, startY);
        roof.gameObject.SetActive(false);

        // 1. Kepenge çok yakından başla, yavaşça tüm resme açıl; ardından başlık
        gate.localScale = Vector3.one * RevealStartScale;
        StartCoroutine(Tween(FadeIn, t => rootGroup.alpha = t, resetSkip: false));
        yield return Tween(RevealDuration, t =>
        {
            float e = 1f - (1f - t) * (1f - t) * (1f - t);   // ease-out: sonda yumuşakça oturur
            gate.localScale = Vector3.one * Mathf.Lerp(RevealStartScale, 1f, e);
        });
        yield return Tween(0.6f, t => gateTitleGroup.alpha = t);
        yield return Hold(TitleHold);

        // 2. Kepenge dalış: görüntü kepengin karanlığına (lacivert) kararır
        yield return Tween(DiveDuration, t =>
        {
            gate.localScale = Vector3.one * Mathf.Lerp(1f, DiveScale, t * t);
            gateTitleGroup.alpha = 1f - Mathf.Clamp01(t * 2.5f);
            SetFlash(Mathf.Clamp01((t - 0.3f) / 0.7f));
        });
        gate.gameObject.SetActive(false);

        // 3. Asansör: lacivert zeminde kat sayacı, sonra çatı opak olarak aşağıdan yükselir
        int shown = -1;
        yield return Tween(CounterDuration, t =>
        {
            floorGroup.alpha = Mathf.Clamp01(t * 4f);
            int floor = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, floors, t * (2f - t))));
            if (floor != shown)
            {
                shown = floor;
                floorText.text = GameLocalization.GetFormat("master_league_intro_floor", floor);
            }
        });
        roof.gameObject.SetActive(true);
        yield return Tween(RiseDuration, t =>
        {
            float e = 1f - (1f - t) * (1f - t) * (1f - t);   // asansör yavaşlayarak durur
            roof.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, 0f, e));
            floorGroup.alpha = 1f - Mathf.Clamp01((t - 0.55f) / 0.3f);
        });
        floorGroup.alpha = 0f;

        // 4. Çatı: "LİG N" (hafif zıplama) + açıklama + BAŞLA
        yield return Tween(0.45f, t =>
        {
            roofGroup.alpha = t;
            float punch = 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
            roofTitle.localScale = new Vector3(punch, punch, 1f);
        });
        roofTitle.localScale = Vector3.one;
        roofGroup.interactable = roofGroup.blocksRaycasts = true;
        started = true;
    }

    private void Finish()
    {
        if (!started) return;
        started = false;
        PlayerPrefs.SetInt(KeySeen, 1);
        PlayerPrefs.Save();
        StartCoroutine(Close());
    }

    private IEnumerator Close()
    {
        skip = false;
        yield return Tween(FadeOut, t => rootGroup.alpha = 1f - t);
        Destroy(gameObject);
    }

    // ── Zamanlama ───────────────────────────────────────────────────

    private IEnumerator Tween(float duration, System.Action<float> apply, bool resetSkip = true)
    {
        if (resetSkip) skip = false;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Dokunuş aşamayı 4 kat hızlandırır (kesip atlamaz: görsel durum tutarlı kalır).
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) * (skip ? 4f : 1f);
            apply(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        apply(1f);
    }

    private IEnumerator Hold(float seconds)
    {
        skip = false;
        float elapsed = 0f;
        while (elapsed < seconds && !skip)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetFlash(float a)
    {
        var c = flash.color;
        c.a = a;
        flash.color = c;
    }

    // ── Kurulum yardımcıları ────────────────────────────────────────

    private Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private RectTransform Picture(string name, Sprite sprite)
    {
        var img = NewImage(name, root);
        img.sprite = sprite;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        return rt;
    }

    // Görseli ekranı tamamen kaplayacak şekilde (oran korunur) boyutlandırır.
    private void Envelope(RectTransform rt)
    {
        var sprite = rt.GetComponent<Image>().sprite;
        Vector2 screen = root.rect.size;
        Vector2 size = sprite.rect.size;
        float scale = Mathf.Max(screen.x / size.x, screen.y / size.y);
        rt.sizeDelta = size * scale;
        rt.anchoredPosition = Vector2.zero;
    }

    // Pivotu kaydırır, görsel yerinde kalır (yakınlaşma kapının merkezine doğru olsun).
    private static void SetPivotKeepingPlace(RectTransform rt, Vector2 pivot)
    {
        Vector2 delta = pivot - rt.pivot;
        rt.pivot = pivot;
        rt.anchoredPosition += Vector2.Scale(delta, rt.sizeDelta);
    }

    private CanvasGroup Group(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go.GetComponent<CanvasGroup>();
    }

    private TMP_Text Label(Transform parent, string name, string text, float size, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMax = size;
        label.fontSizeMin = size * 0.5f;
        label.raycastTarget = false;
        TextSurfaceStyle.Apply(label, TextSurface.Blue);   // beyaz yüz + lacivert kontur (yazı kuralı)
        return label;
    }

    private void BuildStartButton(Transform parent)
    {
        var img = NewImage("BtnStart", parent);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.22f, 0f);
        rt.anchorMax = new Vector2(0.78f, 0.30f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        var label = Label(img.transform, "Label", GameLocalization.Get("master_league_intro_start"), 72f, Vector2.zero, Vector2.one);
        label.margin = new Vector4(44f, 6f, 44f, 10f);   // yazı bevel'e değmesin
        // Klasik yeşil popup butonu, popup'lardaki gibi KENDİ oranında (Simple + preserveAspect): çerçeve görselin
        // oranına göre kurulur, gerilmiş butona oturmaz.
        img.sprite = CommonPopupSkin.Shared.continueButton;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
        UiButtons.StyleLabel(label, UiButtons.Kind.GreenPlain);
        ButtonFrameBacking.Attach(button, CommonPopupSkin.Shared.continueButtonFrame);   // ligde metal çerçeve
        button.onClick.AddListener(Finish);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
