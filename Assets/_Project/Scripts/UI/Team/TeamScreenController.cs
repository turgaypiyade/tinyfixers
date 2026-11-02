using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takım ekranı (sade sohbet). Üst bilgi (amblem, isim, üye sayısı), altında sohbet akışı
/// (gelen mesaj solda, benimki sağda), en altta "Can İste" / "Mesaj". Mesaj'a basınca
/// bottom bar üstünde tek satırlık input açılır. Bot can yardımları akışta kabul edilir.
/// </summary>
public sealed class TeamScreenController : MonoBehaviour
{
    [Header("Tema")]
    [SerializeField] private UITheme theme;

    [Header("Üst bilgi")]
    [SerializeField] private Image emblemImage;
    [SerializeField] private TMP_Text teamNameText;
    [SerializeField] private TMP_Text memberCountText;   // "40/50"

    [Header("Akış")]
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private TeamChatRow chatRowPrefab;
    [Tooltip("Can isteği kartı (takım arkadaşının isteği + kendi isteğim). Boşsa istekler sohbet satırı olarak kalır.")]
    [SerializeField] private TeamLifeRequestRow lifeRequestRowPrefab;
    [SerializeField] private ScrollRect scrollRect;      // yeni mesajda alta kaydırmak için

    [Header("Alt butonlar")]
    [SerializeField] private Button requestLifeButton;
    [SerializeField] private Button messageButton;
    [Tooltip("Takımdan ayrıl butonu (opsiyonel). Boşsa header'a runtime bir buton kurulur.")]
    [SerializeField] private Button leaveButton;
    [Tooltip("Takımdan ayrıl aksiyonlarında kullanılan kare kırmızı buton görseli.")]
    [SerializeField] private Sprite leaveRedSquareButtonSprite;

    [Header("Mesaj input (bottom bar üstü)")]
    [SerializeField] private GameObject messageInputRoot;   // başta kapalı
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private Button messagePostButton;

    [Header("Görsel")]
    [Tooltip("Takım amblemi yoksa kullanılacak varsayılan amblem.")]
    [SerializeField] private Sprite defaultEmblem;
    [Tooltip("Amblem havuzu — PlayerTeamState.EmblemIndex buradan sprite'a çevrilir.")]
    [SerializeField] private Sprite[] emblemPool;

    [Header("Takımsız durum (Ara/Oluştur)")]
    [Tooltip("Takım İÇİ görünümün kökü (header + sohbet + butonlar).")]
    [SerializeField] private GameObject inTeamRoot;
    [Tooltip("Takımsızken gösterilen Ara/Oluştur tarayıcısı.")]
    [SerializeField] private TeamBrowserController browser;

    private ITeamService service;
    private readonly List<GameObject> feed = new();
    private bool wired;
    private TMP_Text requestLifeLabel;
    private string defaultRequestLifeLabel;
    private readonly List<TeamChatRow> lifeReplyRows = new();
    private float lockLabelTimer;

    private void OnEnable()
    {
        WireButtons();
        ScreenTitleStyle.ApplyToScreen(transform, theme != null ? theme.headingFont : null);
        SocialBackdrop.ApplyToScreen(transform);
        LivesTimerService.EnsureExists();
        LivesManager.OnLivesChanged += RefreshLifeControls;
        if (messageInputRoot != null) messageInputRoot.SetActive(false);
        ApplyTeamState();
    }

    // Takım durumuna göre görünüm: takımsız → Ara/Oluştur tarayıcısı; takımlı → sohbet.
    private void ApplyTeamState()
    {
        bool hasTeam = PlayerTeamState.HasTeam;

        if (inTeamRoot != null) inTeamRoot.SetActive(hasTeam);
        if (browser != null) browser.gameObject.SetActive(!hasTeam);

        if (!hasTeam) return;

        // Katılma/kurma sonrası BackendServices.ResetTeam çağrılmış olabilir —
        // her açılışta güncel servisi al (singleton zaten cache'ler).
        if (service != null) service.OnChanged -= OnServiceChanged;
        service = BackendServices.Team;
        service.OnChanged += OnServiceChanged;   // gerçek sohbette canlı mesaj akışı
        Refresh(snapToBottom: true);
    }

    // Veri değişimi (Yardım'a basma, gelen mesaj): okunan yer korunur; yalnız zaten en alttaysa altta kalır.
    private void OnServiceChanged() => Refresh(snapToBottom: false);

    private void OnDisable()
    {
        CloseQuickMenu(instant: true);
        LivesManager.OnLivesChanged -= RefreshLifeControls;
        if (service != null) service.OnChanged -= OnServiceChanged;
    }

    // Browser'dan katılma/kurma bitti sinyali (mockup kurulumunda bağlanır).
    private void OnTeamEntered() => ApplyTeamState();

    private void Awake()
    {
        if (browser != null) browser.OnTeamEntered += OnTeamEntered;
    }

    private void OnDestroy()
    {
        if (browser != null) browser.OnTeamEntered -= OnTeamEntered;
    }

    private void WireButtons()
    {
        if (wired) return;
        if (requestLifeButton != null)
        {
            requestLifeButton.onClick.AddListener(OnRequestLife);
            requestLifeLabel = requestLifeButton.GetComponentInChildren<TMP_Text>(true);
            defaultRequestLifeLabel = GameLocalization.Get("team_request_life");
        }
        if (messageButton != null)     messageButton.onClick.AddListener(ToggleMessageInput);
        if (messagePostButton != null) messagePostButton.onClick.AddListener(OnPostMessage);
        if (messageInput != null)      messageInput.onSubmit.AddListener(_ => OnPostMessage());

        // Ayrılmak gibi geri dönüşsüz işlem başlıkta kırmızı durmaz: başlıkta yalnız "Takım Bilgisi"
        // (i) ikonu var; "Ayrıl" onun penceresinin içinde (RM düzeni).
        if (leaveButton == null) leaveButton = BuildInfoButton();
        if (leaveButton != null) leaveButton.onClick.AddListener(ShowTeamInfo);

        StyleBottomButtons();
        wired = true;
    }

    // Alt bar (RM düzeni): solda büyük yeşil "Can İste" (kalp ikonlu, geri sayım 2. satırda), sağda iki kare buton:
    // hızlı mesaj (turkuaz, gülen yüz) ve mesaj yaz (mavi, konuşma balonu).
    // Yeşil buton görseli (GreenButonwoStroke 289x116) kendi oranına yakın kullanılır; yükseklik artınca
    // sağdaki kare butonlar ve ikonlar aynı ölçüde büyür (hepsi BarButtonHeight'tan).
    private const float BarButtonHeight = 128f;
    private const float RequestLifeWidth = 400f;
    private static readonly Color LockedHeartTint = new Color(0.42f, 0.42f, 0.47f, 1f);
    private Image requestLifeHeart;
    private int requestLifeStyle = -1;   // 1 yeşil (istenebilir), 0 gri (kilitli) — gereksiz yeniden boyamayı önler
    private static readonly Color QuickTint = new Color(0.62f, 1f, 0.95f, 1f);   // mavi kapsül → turkuaz
    private static readonly string[] QuickMessageKeys = { "team_quick_1", "team_quick_2", "team_quick_3", "team_quick_4" };
    // Yalnız EmojiOne setinde gerçekten olanlar (1f60a yok → "?" kutusu çıkıyordu).
    private static readonly string[] QuickMessageEmoji = { "1f604", "1f605", "1f60e", "1f601" };
    private Button quickButton;

    private void StyleBottomButtons()
    {
        UiButtons.Apply(requestLifeButton, UiButtons.Kind.GreenPlain);   // popup'ların yeşil ana butonu
        UiButtons.Apply(messageButton, UiButtons.Kind.Blue);
        UiButtons.Apply(messagePostButton, UiButtons.Kind.GreenPlain);
        UiIcons.SetLeadingIcon(requestLifeButton, UiIcons.Heart, size: BarButtonHeight * 0.72f, leftPad: 24f);
        if (requestLifeButton != null)
        {
            var heart = requestLifeButton.transform.Find("LeadIcon");
            if (heart != null) requestLifeHeart = heart.GetComponent<Image>();
            // Kilitli hâli gri görselle gösterilir; Button'ın koyu karartma tonu üstüne binmesin.
            var colors = requestLifeButton.colors;
            colors.disabledColor = Color.white;
            requestLifeButton.colors = colors;
        }

        var bar = requestLifeButton != null ? requestLifeButton.transform.parent : null;
        if (bar == null) return;
        if (bar.TryGetComponent(out HorizontalLayoutGroup h))
        {
            h.padding = new RectOffset(28, 28, 4, 4);
            h.spacing = 20f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
        }
        SetBarSize(requestLifeButton, RequestLifeWidth, 0f);

        // Esnek boşluk: kare butonlar sağa yaslanır.
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.layer = bar.gameObject.layer;
        spacer.transform.SetParent(bar, false);
        spacer.GetComponent<LayoutElement>().flexibleWidth = 1f;

        quickButton = BuildQuickButton(bar);
        if (messageButton != null)
        {
            messageButton.transform.SetAsLastSibling();
            SetBarSize(messageButton, BarButtonHeight, 0f);
            SetIconOnly(messageButton, UiIcons.Chat);
        }
    }

    private static void SetBarSize(Button button, float width, float flexible)
    {
        if (button == null) return;
        if (!button.TryGetComponent(out LayoutElement le)) le = button.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = width;
        le.flexibleWidth = flexible;
        le.minHeight = le.preferredHeight = BarButtonHeight;
    }

    // Kare buton: yalnız ortada ikon (yazı gizli).
    private static void SetIconOnly(Button button, Sprite icon)
    {
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.gameObject.SetActive(false);
        if (icon == null) return;
        var t = button.transform.Find("CenterIcon");
        Image img;
        if (t != null) img = t.GetComponent<Image>();
        else
        {
            var go = new GameObject("CenterIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = button.gameObject.layer;
            go.transform.SetParent(button.transform, false);
            img = go.GetComponent<Image>();
        }
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(BarButtonHeight * 0.62f, BarButtonHeight * 0.62f);   // buton büyüdükçe ikon da
        rt.anchoredPosition = Vector2.zero;
    }

    // Hızlı mesaj butonu (turkuaz kare, gülen yüz): hazır kısa mesajlardan birini seçip gönderir.
    private Button BuildQuickButton(Transform bar)
    {
        var go = new GameObject("Btn_QuickMessage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.layer = bar.gameObject.layer;
        go.transform.SetParent(bar, false);
        var button = go.GetComponent<Button>();
        var image = go.GetComponent<Image>();
        button.targetGraphic = image;
        UiButtons.Apply(image, UiButtons.Kind.Blue);
        image.color = QuickTint;
        SetBarSize(button, BarButtonHeight, 0f);

        var labelGo = new GameObject("Emoji", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.layer = go.layer;
        labelGo.transform.SetParent(go.transform, false);
        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.richText = true;
        label.text = "<sprite name=\"1f604\">";   // TMP varsayılan sprite seti (EmojiOne)
        label.fontSize = BarButtonHeight * 0.55f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        button.onClick.AddListener(ShowQuickMessages);
        return button;
    }

    // ── Hızlı mesaj menüsü ────────────────────────────────────────────────
    // Popup yerine butonun HEMEN ÜSTÜNDE açılan menü (kullanıcı 2026-11-02). Açılış: mesajlar butonun içinden KISA (buton
    // boyunda hap) çıkıp yukarı kayar — önce en üstteki (en uzağa giden), alttakiler sırayla; yerine varınca sola doğru
    // uzayıp tam boya ulaşır, yazı o an belirir. Zemin düz yumuşak yeşil hap (görsel değil). Bir kez kurulur.
    private static readonly Color QuickItemColor = new Color(0.31f, 0.67f, 0.28f, 1f);   // yumuşak yeşil (koyulaştırıldı)
    private const float QuickItemWidth = 600f, QuickItemHeight = 100f, QuickItemSpacing = 12f, QuickMenuGap = 16f;
    // Yumuşak menü: her öğe yükseldikçe uzar (boy = kendi yolunun ilerlemesi). Uzun yol = uzun süre → hepsi birlikte oturur.
    private const float QuickSlideBase = 0.26f, QuickSlidePerRow = 0.10f, QuickStagger = 0.10f, QuickCloseSeconds = 0.12f;
    private RectTransform quickMenu;
    private CanvasGroup quickMenuGroup;
    private GameObject quickBlocker;
    private RectTransform[] quickItems;
    private TMP_Text[] quickLabels;
    private Coroutine quickAnim;
    private static Sprite quickPillSprite;

    private void ShowQuickMessages()
    {
        if (service == null) return;
        if (quickMenu != null && quickMenu.gameObject.activeSelf) { CloseQuickMenu(instant: false); return; }
        EnsureQuickMenu();
        for (int i = 0; i < quickLabels.Length; i++)
            quickLabels[i].text = QuickText(i);   // dil değişmiş olabilir

        var host = (RectTransform)quickMenu.parent;
        quickBlocker.transform.SetAsLastSibling();
        quickMenu.SetAsLastSibling();
        // Menü kökü = hızlı mesaj butonunun sağ-üst köşesinin biraz üstü; öğeler buradan yukarı dizilir.
        var corners = new Vector3[4];
        ((RectTransform)quickButton.transform).GetWorldCorners(corners);
        Vector2 local = host.InverseTransformPoint(corners[2]);
        quickMenu.anchorMin = quickMenu.anchorMax = host.pivot;
        quickMenu.anchoredPosition = local + new Vector2(0f, QuickMenuGap);

        quickBlocker.SetActive(true);
        quickMenu.gameObject.SetActive(true);
        quickMenuGroup.alpha = 1f;
        if (quickAnim != null) StopCoroutine(quickAnim);
        quickAnim = StartCoroutine(AnimateQuickOpen());
    }


    private string QuickText(int i) =>
        GameLocalization.Get(QuickMessageKeys[i]) + " <sprite name=\"" + QuickMessageEmoji[i] + "\">";

    private float QuickSlotY(int i) => (quickItems.Length - 1 - i) * (QuickItemHeight + QuickItemSpacing);   // i=0 en üst

    private void EnsureQuickMenu()
    {
        if (quickMenu != null) return;
        var host = inTeamRoot != null ? inTeamRoot.transform : transform;
        int layer = host.gameObject.layer;

        // Dışarı dokununca kapat: tam ekran görünmez yakalayıcı.
        quickBlocker = new GameObject("QuickMenuBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        quickBlocker.layer = layer;
        var brt = (RectTransform)quickBlocker.transform;
        brt.SetParent(host, false);
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        quickBlocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var bbtn = quickBlocker.GetComponent<Button>();
        bbtn.transition = Selectable.Transition.None;
        bbtn.onClick.AddListener(() => CloseQuickMenu(instant: false));
        quickBlocker.SetActive(false);

        var menuGo = new GameObject("QuickMenu", typeof(RectTransform), typeof(CanvasGroup));
        menuGo.layer = layer;
        quickMenu = (RectTransform)menuGo.transform;
        quickMenu.SetParent(host, false);
        quickMenu.pivot = new Vector2(1f, 0f);
        quickMenu.sizeDelta = Vector2.zero;   // öğeler elle konumlanır (animasyon düzen grubuyla çatışmasın)
        quickMenuGroup = menuGo.GetComponent<CanvasGroup>();

        var pill = QuickPillSprite();
        quickItems = new RectTransform[QuickMessageKeys.Length];
        quickLabels = new TMP_Text[QuickMessageKeys.Length];
        for (int i = 0; i < QuickMessageKeys.Length; i++)
        {
            int index = i;
            var item = new GameObject("QuickItem" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.layer = layer;
            var rt = (RectTransform)item.transform;
            rt.SetParent(quickMenu, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);   // sağ kenar sabit: uzama sola doğru
            rt.sizeDelta = new Vector2(QuickItemWidth, QuickItemHeight);
            var img = item.GetComponent<Image>();
            img.sprite = pill;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = pill != null ? pill.border.x / (QuickItemHeight * 0.5f) : 1f;   // tam yuvarlak uçlar
            img.color = QuickItemColor;
            var btn = item.GetComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.layer = layer;
            labelGo.transform.SetParent(rt, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.richText = true;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMax = 40f; label.fontSizeMin = 24f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(36f, 8f); lrt.offsetMax = new Vector2(-36f, -8f);
            TextSurfaceStyle.Apply(label, QuickItemColor);   // beyaz yüz + yeşilin koyusu kontur (yazı kuralı)
            quickItems[i] = rt;
            quickLabels[i] = label;

            btn.onClick.AddListener(() =>
            {
                if (this == null || service == null) return;
                service.SendMessage(QuickText(index));
                BuildFeed(snapToBottom: true);
                CloseQuickMenu(instant: true);
            });
        }
        menuGo.SetActive(false);
    }

    // Düz hap zemini: yarıçap = yükseklik/2, 9-slice. Bir kez üretilir (piksel hesabı; GPU'dan geri okuma yok).
    private static Sprite QuickPillSprite()
    {
        if (quickPillSprite != null) return quickPillSprite;
        const int size = 64; const float r = size * 0.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);   // kenarda 1 px yumuşatma
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        quickPillSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(r - 1f, r - 1f, r - 1f, r - 1f));
        return quickPillSprite;
    }

    private void CloseQuickMenu(bool instant)
    {
        if (quickMenu == null || !quickMenu.gameObject.activeSelf) return;
        if (quickBlocker != null) quickBlocker.SetActive(false);
        if (quickAnim != null) { StopCoroutine(quickAnim); quickAnim = null; }
        if (instant || !isActiveAndEnabled) { quickMenu.gameObject.SetActive(false); return; }
        quickAnim = StartCoroutine(AnimateQuickClose());
    }

    private System.Collections.IEnumerator AnimateQuickOpen()
    {
        int n = quickItems.Length;
        float total = 0f;
        for (int i = 0; i < n; i++) total = Mathf.Max(total, i * QuickStagger + QuickSlideTime(i));
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < n; i++)
            {
                float local = t - i * QuickStagger;   // i=0 (en üst, en uzun yol) önce yola çıkar
                float p = Mathf.Clamp01(local / QuickSlideTime(i));
                float e = 1f - (1f - p) * (1f - p) * (1f - p);   // ease-out cubic: yumuşak oturma
                var rt = quickItems[i];
                // Hepsi en alttaki mesajın yerinde (butonun hemen üstü, sağa yaslı) KÜÇÜK hap olarak doğar; yükseldikçe
                // sola doğru uzar ve yerine oturur. En alttaki yerinde küçükten tam boya gelir (kullanıcı 2026-11-02).
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(QuickSlotY(n - 1), QuickSlotY(i), e));
                rt.sizeDelta = new Vector2(Mathf.Lerp(QuickItemHeight, QuickItemWidth, e), QuickItemHeight);
                rt.gameObject.SetActive(local > 0f);
                quickLabels[i].alpha = Mathf.Clamp01((e - 0.55f) / 0.45f);   // yazı hap yeterince uzayınca belirir
            }
            if (t >= total) break;
            yield return null;
        }
        quickAnim = null;
    }

    // Yol uzunluğuna göre süre: en üst (3 satır yükselir) en uzun, en alt (yerinde büyür) en kısa → hepsi birlikte oturur.
    private float QuickSlideTime(int i) => QuickSlideBase + QuickSlidePerRow * (quickItems.Length - 1 - i);

    private System.Collections.IEnumerator AnimateQuickClose()
    {
        float t = 0f;
        while (t < QuickCloseSeconds)
        {
            t += Time.unscaledDeltaTime;
            quickMenuGroup.alpha = 1f - Mathf.Clamp01(t / QuickCloseSeconds);
            yield return null;
        }
        quickMenu.gameObject.SetActive(false);
        quickMenuGroup.alpha = 1f;
        quickAnim = null;
    }

    // Başlığın sağında yuvarlak (i) ikonu → takım bilgisi penceresi.
    private Button BuildInfoButton()
    {
        var parent = inTeamRoot != null ? inTeamRoot.transform : transform;
        var go = new GameObject("TeamInfoButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-20, -18); rt.sizeDelta = new Vector2(88, 88);
        var img = go.AddComponent<Image>();
        img.sprite = UiIcons.Info;
        img.preserveAspect = true;
        if (img.sprite == null) ApplyButtonImage(img, leaveRedSquareButtonSprite, new Color(0.25f, 0.45f, 0.85f));
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    private void ShowTeamInfo() => ShowTeamInfoPage(0);

    private void ShowTeamInfoPage(int page)
    {
        var info = service?.GetTeamInfo();
        string title = info != null && !string.IsNullOrEmpty(info.teamName) ? info.teamName : GameLocalization.Get("team_info_title");
        string body = info != null ? GameLocalization.Get("team_capacity") + ": " + info.MemberLabel : "";
        const int pageSize = 8;
        int count = info != null ? info.realMemberNames.Count : 0;
        int pages = Mathf.Max(1, (count + pageSize - 1) / pageSize);
        page = Mathf.Clamp(page, 0, pages - 1);
        if (count > 0)
        {
            body += "\n\n" + string.Join("\n", info.realMemberNames.GetRange(page * pageSize,
                Mathf.Min(pageSize, count - page * pageSize)));
            if (pages > 1) body += $"\n({page + 1}/{pages})";
        }
        var choices = new List<RuntimeChoicePopup.Choice>();
        if (pages > 1)
            choices.Add(new RuntimeChoicePopup.Choice("→", () =>
            { if (this != null) ShowTeamInfoPage((page + 1) % pages); }));
        choices.Add(new RuntimeChoicePopup.Choice(GameLocalization.Get("team_leave"), () =>
        { if (this != null) ShowLeaveConfirm(); }));
        choices.Add(new RuntimeChoicePopup.Choice(GameLocalization.Get("common_close"), null, primary: true));
        RuntimeChoicePopup.Show(title, body, choices.ToArray());
    }

    // ── Takımdan ayrıl (onaylı) ─────────────────────────────────────

    private void ShowLeaveConfirm()
    {
        RuntimeChoicePopup.Show(GameLocalization.Get("team_leave_title"), GameLocalization.Get("team_leave_confirm"),
            new RuntimeChoicePopup.Choice(GameLocalization.Get("team_leave"), () => { if (this != null) DoLeave(); }, primary: true),
            new RuntimeChoicePopup.Choice(GameLocalization.Get("common_cancel"), null));
    }

    private void DoLeave()
    {
        if (service != null) service.OnChanged -= OnServiceChanged;
        service = null;
        PlayerTeamState.LeaveTeam();
        BackendServices.ResetTeam();
        ApplyTeamState();   // takımsız görünüme (Ara/Oluştur) döner
    }

    private static void ApplyButtonImage(Image image, Sprite sprite, Color fallbackColor)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.color = sprite != null ? Color.white : fallbackColor;
        image.preserveAspect = sprite != null;
    }

    private void Refresh(bool snapToBottom)
    {
        if (service == null) return;
        var info = service.GetTeamInfo();
        if (info != null)
        {
            if (emblemImage != null)
            {
                // Öncelik: servis ambleminin kendisi → oyuncunun seçtiği havuz amblemi → varsayılan.
                var emblem = info.emblem;
                if (emblem == null && emblemPool != null && emblemPool.Length > 0)
                    emblem = emblemPool[PlayerTeamState.EmblemIndex % emblemPool.Length];
                if (emblem == null) emblem = defaultEmblem;
                emblemImage.sprite  = emblem;
                emblemImage.enabled = emblem != null;
                emblemImage.preserveAspect = true;
            }
            if (teamNameText != null)    teamNameText.text = info.teamName;
            SingleLineText.Fit(teamNameText);
            if (memberCountText != null) memberCountText.text = info.MemberLabel;
        }

        BuildFeed(snapToBottom);
        RefreshLifeControls();
    }

    private void BuildFeed(bool snapToBottom = true)
    {
        // Yeniden kurulumda okunan yer korunsun: içerik üstten pivotlu → anchoredPosition.y = üstten kaydırma.
        bool keepPosition = !snapToBottom && scrollRect != null && contentContainer != null
                            && scrollRect.verticalNormalizedPosition > 0.01f;
        float scrollY = contentContainer != null ? contentContainer.anchoredPosition.y : 0f;

        // Destroy kare sonuna ertelenir: eski satırlar o ana dek layout'ta kalıp içeriği bir kare iki katı
        // uzatıyor, kaydırma ona göre hesaplanıp sonra geri "oturuyordu". Hemen kapat → layout görmez.
        foreach (var go in feed) if (go != null) { go.SetActive(false); Destroy(go); }
        feed.Clear();
        lifeReplyRows.Clear();
        if (contentContainer == null) return;
        if (contentContainer.TryGetComponent(out VerticalLayoutGroup feedLayout))
        {
            feedLayout.spacing = 24f;   // ferah akış
            feedLayout.padding.left = feedLayout.padding.right = FeedSideMargin;   // kartlar kenardan nefes alsın
        }

        if (chatRowPrefab != null)
        {
            foreach (var m in GetFeedMessages().OrderBy(message => message.sentTicks))
            {
                if (m.avatar == null)
                {
                    // Benim mesajım → ProfileScreen'de SEÇTİĞİM avatar; gelen → havuzdan.
                    m.avatar = m.isMine
                        ? (PlayerAvatarProvider.Current ?? PickAvatar(m.senderName))
                        : PickAvatar(m.senderName);
                }
                if (lifeRequestRowPrefab != null && (m.botLifeRequest != null || m.myLifeRequest || m.realLifeRequest != null))
                {
                    feed.Add(BuildLifeCard(m).gameObject);
                    continue;
                }
                var row = Instantiate(chatRowPrefab, contentContainer);
                row.Bind(m, theme);
                if (m.lifeReply != null)
                {
                    var inbox = service.LifeInbox;
                    row.BindLifeReply(m.lifeReply, () => inbox.Accept(m.lifeReply.id));
                    lifeReplyRows.Add(row);
                }
                else if (m.botLifeRequest != null)
                {
                    var inbox = service.LifeInbox;
                    row.BindBotLifeRequest(m.botLifeRequest, () => inbox.HelpBot(m.botLifeRequest.id));
                    lifeReplyRows.Add(row);
                }
                feed.Add(row.gameObject);
            }
        }

        if (keepPosition) RestoreScroll(scrollY);
        else SnapToBottom();
    }

    private const int FeedSideMargin = 44;

    private void RestoreScroll(float scrollY)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
        var viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
        float max = Mathf.Max(0f, contentContainer.rect.height - viewport.rect.height);
        contentContainer.anchoredPosition = new Vector2(contentContainer.anchoredPosition.x, Mathf.Clamp(scrollY, 0f, max));
        scrollRect.StopMovement();
    }

    // Can isteği kartı: takım arkadaşının isteği (Yardım → 1 can) ya da kendi aktif isteğim (toplanan/istenen).
    private TeamLifeRequestRow BuildLifeCard(TeamChatMessage m)
    {
        var inbox = service.LifeInbox;
        var card = Instantiate(lifeRequestRowPrefab, contentContainer);
        if (m.realLifeRequest != null)
        {
            // Gerçek takım arkadaşı (Firestore): Yardım → helpers'a kendi uid'im, isteyene "Kabul Et" cevabı düşer.
            card.Bind(m.realLifeRequest, theme, r => service.Help(r), helped: m.realLifeRequest.helpedByMe);
        }
        else if (m.myLifeRequest)
        {
            var mine = new TeamLifeRequest
            {
                requesterName = m.senderName,
                current = inbox.RequestReceived,
                needed = Mathf.Max(1, inbox.RequestTotal)
            };
            card.Bind(mine, theme, null, mine: true);
        }
        else
        {
            var bot = m.botLifeRequest;
            var request = new TeamLifeRequest { requesterName = bot.sender, current = bot.helped ? 1 : 0, needed = 1 };
            card.Bind(request, theme, _ => inbox.HelpBot(bot.id), helped: bot.helped);
        }
        return card;
    }

    private IEnumerable<TeamChatMessage> GetFeedMessages()
    {
        foreach (var message in service.GetChat()) yield return message;
        foreach (var reply in service.LifeInbox.Replies)
        {
            yield return new TeamChatMessage
            {
                senderName = reply.sender,
                avatar = PickAvatar(reply.sender),
                text = GameLocalization.Get("team_chat_life_sent"),
                timeLabel = new System.DateTime(reply.sentTicks, System.DateTimeKind.Utc).ToLocalTime().ToString("HH:mm"),
                sentTicks = reply.sentTicks,
                lifeReply = reply,
                isMine = false
            };
        }
        foreach (var request in service.GetLifeRequests())
        {
            if (string.IsNullOrEmpty(request.requesterId)) continue;   // yalnız gerçek (Firestore) istekler
            yield return new TeamChatMessage
            {
                senderName = request.requesterName,
                sentTicks = request.createdTicks,
                realLifeRequest = request,
            };
        }
        // Kendi isteğim: en fazla 24 saat kart olarak (kaç can geldi); yeni istek (4 saat sonra) kartı sıfırlar.
        var inboxMine = service.LifeInbox;
        if (inboxMine.LastRequestTicks > 0 && inboxMine.RequestTotal > 0
            && System.DateTime.UtcNow.Ticks - inboxMine.LastRequestTicks < System.TimeSpan.TicksPerDay)
        {
            yield return new TeamChatMessage
            {
                senderName = PlayerProfile.PlayerName,
                sentTicks = inboxMine.LastRequestTicks,
                myLifeRequest = true,
                isMine = true
            };
        }
        foreach (var request in service.LifeInbox.BotRequests)
        {
            yield return new TeamChatMessage
            {
                senderName = request.sender,
                avatar = PickAvatar(request.sender),
                text = GameLocalization.Get(request.helped ? "team_chat_life_thanks" : "team_chat_life_ask"),
                timeLabel = new System.DateTime(request.sentTicks, System.DateTimeKind.Utc).ToLocalTime().ToString("HH:mm"),
                sentTicks = request.sentTicks,
                botLifeRequest = request,
                isMine = false
            };
        }
    }

    // Zaman kilidi (yeni üye 24sa / istek sonrası 24sa) sürerken buton üstündeki
    // geri sayım saniyede bir tazelenir; kilit bitince buton kendiliğinden açılır.
    private void Update()
    {
        if (service?.LifeInbox == null) return;
        lockLabelTimer -= Time.unscaledDeltaTime;
        if (lockLabelTimer > 0f) return;
        lockLabelTimer = 1f;
        if (service.LifeInbox.RequestLockRemaining(System.DateTime.UtcNow) > System.TimeSpan.Zero
            || (requestLifeButton != null && requestLifeButton.interactable != service.LifeInbox.CanRequest))
            RefreshLifeControls();
    }

    // Buton her zaman "Can İste": istenebilirken yeşil; istenemiyorsa (yeni üye 24 saat / istek 4 saat kilidi / can dolu) açık gri,
    // kilit sürüyorsa altında kalan süre.
    private void RefreshLifeControls()
    {
        var inbox = service?.LifeInbox;
        bool can = inbox != null && inbox.CanRequest;
        var lockLeft = inbox != null ? inbox.RequestLockRemaining(System.DateTime.UtcNow) : System.TimeSpan.Zero;
        if (requestLifeButton != null)
        {
            requestLifeButton.interactable = can;
            int style = can ? 1 : 0;
            if (style != requestLifeStyle)
            {
                requestLifeStyle = style;
                var kind = can ? UiButtons.Kind.GreenPlain : UiButtons.Kind.GrayPlain;
                UiButtons.Apply(requestLifeButton, kind, styleLabel: false);
                UiButtons.StyleLabel(requestLifeLabel, kind);
                if (requestLifeHeart != null) requestLifeHeart.color = can ? Color.white : LockedHeartTint;
            }
        }
        if (requestLifeLabel != null)
            requestLifeLabel.text = !can && lockLeft > System.TimeSpan.Zero
                ? $"{defaultRequestLifeLabel}\n<size=62%>{(int)lockLeft.TotalHours:00}:{lockLeft.Minutes:00}:{lockLeft.Seconds:00}</size>"
                : defaultRequestLifeLabel;
        foreach (var row in lifeReplyRows)
            if (row != null) row.RefreshLifeReply();
    }

    private void SnapToBottom()
    {
        if (scrollRect == null) return;
        if (contentContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;   // en yeni mesaj altta
    }

    // İsme göre deterministik avatar (AvatarLibrary'deki hayvan avatarlarından; aynı isim her seferinde aynı avatarı alır).
    private static Sprite PickAvatar(string name) => PlayerAvatarProvider.PickForSeed(name);

    // ── Aksiyonlar ──────────────────────────────────────────────────

    private void OnRequestLife()
    {
        if (service == null || !service.LifeInbox.CanRequest) return;
        service.RequestLife();
        RefreshLifeControls();
    }

    // Mesaj butonu: tek satırlık input alanını aç/kapat.
    private void ToggleMessageInput()
    {
        if (messageInputRoot == null) return;
        bool show = !messageInputRoot.activeSelf;
        messageInputRoot.SetActive(show);
        if (show && messageInput != null)
        {
            messageInput.text = "";
            messageInput.ActivateInputField();
        }
    }

    private void OnPostMessage()
    {
        if (messageInput == null) return;
        string text = messageInput.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        service.SendMessage(text);
        messageInput.text = "";
        BuildFeed(snapToBottom: true);
        if (messageInputRoot != null) messageInputRoot.SetActive(false);
    }
}
