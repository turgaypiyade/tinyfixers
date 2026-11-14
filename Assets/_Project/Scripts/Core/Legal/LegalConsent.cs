using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Kullanım Şartları + Gizlilik Politikası kabul ekranı. METİNLER OYUNLA GELİR (Resources/Legal/*.txt, sürüm
/// version.txt) → internet olmadan da okunup kabul edilir. Web yalnız sürüm kontrolü: wondigames.com/legal/version.json
/// daha yeni bir sürüm bildirirse dört metin indirilip cihazda saklanır (persistentDataPath/legal) ve kabul yeniden
/// istenir. İndirme tamamlanmadan gereken sürüm yükselmez (okuyamayacağı metni kabul ettirmeyiz).
/// Kaynak tek: Website/legal/*_tr|en.html → build.py hem siteyi hem bu .txt'leri üretir (LEGAL_VERSION shell.py'de).
///   • Kompakt kutu (Knock Out referansı): yükleme ekranı arkada görünür, kapatma (X) yok, kabul edilmeden oynanmaz.
///     Metin içi Şartlar/Gizlilik linkleri belgeyi oyun içinde açar (LegalDocumentView, LegalLinkText); kutu açık kalır.
///   • BOOT sahnesinde gösterilir (BootLoader, bulut kayıt geri yüklendikten sonra, ana menü açılmadan önce):
///     ana menünün öğretici/etkinlik/geri bildirim pencereleri kabul edilene dek hiç başlamaz. Web sürümü geç
///     gelirse yeni sürüm bir sonraki açılışta sorulur (oyun ortasında çıkmaz).
/// </summary>
public sealed class LegalConsent : MonoBehaviour
{
    // Uygulamayla gelen sürüm (Resources/Legal/version.txt; dosya yoksa bu değer).
    private const string FallbackVersion = "2026-10-17";
    private static string bundledVersion;
    private static string BundledVersion
    {
        get
        {
            if (bundledVersion != null) return bundledVersion;
            var asset = Resources.Load<TextAsset>("Legal/version");
            bundledVersion = asset != null && !string.IsNullOrWhiteSpace(asset.text) ? asset.text.Trim() : FallbackVersion;
            return bundledVersion;
        }
    }
    private static readonly string[] Docs = { "terms_tr", "terms_en", "privacy_tr", "privacy_en" };
    private const string VersionUrl = "https://wondigames.com/legal/version.json";
    private const string DefaultTerms = "https://wondigames.com/terms/";
    private const string DefaultPrivacy = "https://wondigames.com/privacy/";

    internal const string AcceptedKey = "legal_accepted_version";
    private const string RemoteKey = "legal_remote_version";
    private const string AcceptedAtKey = "legal_accepted_at";

    [Serializable] private class TextUrls { public string terms_tr, terms_en, privacy_tr, privacy_en; }
    [Serializable] private class VersionDoc { public string version; public string terms; public string privacy; public TextUrls texts; }

    private static LegalConsent instance;
    private static string termsUrl = DefaultTerms, privacyUrl = DefaultPrivacy;
    private GameObject gate;

    public static bool IsShowing => instance != null && instance.gate != null;

    /// Geçerli sürüm: gömülü ile metinleri İNDİRİLMİŞ son web sürümünden büyük olanı (yyyy-MM-dd sıralanır).
    private static string RequiredVersion
    {
        get
        {
            string remote = PlayerPrefs.GetString(RemoteKey, "");
            return string.CompareOrdinal(remote, BundledVersion) > 0 ? remote : BundledVersion;
        }
    }

    public static bool NeedsAcceptance => PlayerPrefs.GetString(AcceptedKey, "") != RequiredVersion;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive || instance != null) return;
        var go = new GameObject("[LegalConsent]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LegalConsent>();
        instance.StartCoroutine(instance.FetchRemote());
    }

    private bool fetchDone;

    /// BootLoader ana menüyü açmadan önce çağırır: web sürüm kontrolünü kısa süre bekler (en fazla 3 sn; çevrimdışıysa
    /// hemen), kabul gerekiyorsa pencereyi gösterir ve kabul edilene dek bekler.
    public static IEnumerator RunBootGate()
    {
        if (RuntimeSimulationSession.IsActive) yield break;
        if (instance == null) Bootstrap();
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!instance.fetchDone && Time.realtimeSinceStartup < deadline
               && Application.internetReachability != NetworkReachability.NotReachable)
            yield return null;
        if (!NeedsAcceptance) yield break;
        if (instance.gate == null) instance.BuildGate(PlayerPrefs.HasKey(AcceptedKey));
        while (instance != null && instance.gate != null) yield return null;
    }

    private IEnumerator FetchRemote()
    {
        try
        {
            yield return FetchRemoteInner();
        }
        finally { fetchDone = true; }
    }

    private IEnumerator FetchRemoteInner()
    {
        using var req = UnityWebRequest.Get(VersionUrl);
        req.timeout = 8;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) yield break;

        VersionDoc doc = null;
        try { doc = JsonUtility.FromJson<VersionDoc>(req.downloadHandler.text); }
        catch (Exception) { }
        if (doc == null || string.IsNullOrEmpty(doc.version)) yield break;
        if (!string.IsNullOrEmpty(doc.terms)) termsUrl = doc.terms;
        if (!string.IsNullOrEmpty(doc.privacy)) privacyUrl = doc.privacy;
        if (string.CompareOrdinal(doc.version, RequiredVersion) <= 0) yield break;   // değişmemiş

        // Yeni sürüm: dört metni indir, hepsi gelince kaydet ve sürümü yükselt.
        if (doc.texts == null) yield break;
        var urls = new[] { doc.texts.terms_tr, doc.texts.terms_en, doc.texts.privacy_tr, doc.texts.privacy_en };
        var bodies = new string[urls.Length];
        for (int i = 0; i < urls.Length; i++)
        {
            if (string.IsNullOrEmpty(urls[i])) yield break;
            using var t = UnityWebRequest.Get(urls[i]);
            t.timeout = 10;
            yield return t.SendWebRequest();
            if (t.result != UnityWebRequest.Result.Success || string.IsNullOrWhiteSpace(t.downloadHandler.text)) yield break;
            bodies[i] = t.downloadHandler.text;
        }
        try
        {
            System.IO.Directory.CreateDirectory(CacheDir);
            for (int i = 0; i < Docs.Length; i++)
                System.IO.File.WriteAllText(System.IO.Path.Combine(CacheDir, Docs[i] + ".txt"), bodies[i]);
        }
        catch (Exception e) { Debug.LogWarning($"[LegalConsent] Metin kaydedilemedi: {e.Message}"); yield break; }

        PlayerPrefs.SetString(RemoteKey, doc.version);
        PlayerPrefs.Save();
    }

    private static string CacheDir => System.IO.Path.Combine(Application.persistentDataPath, "legal");

    /// Belge metni (slug: terms/privacy) oyunun diline göre: indirilmiş güncel kopya, yoksa oyunla gelen kopya.
    public static string GetDocument(string slug)
    {
        string lang = GameLocalization.CurrentLanguage == "tr" ? "tr" : "en";
        string name = $"{slug}_{lang}";
        bool remoteNewer = string.CompareOrdinal(PlayerPrefs.GetString(RemoteKey, ""), BundledVersion) > 0;
        if (remoteNewer)
        {
            try
            {
                string path = System.IO.Path.Combine(CacheDir, name + ".txt");
                if (System.IO.File.Exists(path)) return System.IO.File.ReadAllText(path);
            }
            catch (Exception) { }
        }
        var asset = Resources.Load<TextAsset>("Legal/" + name);
        return asset != null ? asset.text : "";
    }

    /// Web sayfasının adresi (oyun içi pencerenin "Web'de aç" bağlantısı için).
    public static string WebUrl(string slug) =>
        (slug == "privacy" ? privacyUrl : termsUrl) + (GameLocalization.CurrentLanguage == "tr" ? "#tr" : "#en");

    // Kompakt kabul kutusu (kullanıcı 2026-11-10, Knock Out referansı): yükleme ekranı arkada görünür, ortada koyu yarı
    // saydam kutu; metin içinde altı çizili Şartlar/Gizlilik linkleri (dokununca belge üstte açılır, kutu açık kalır),
    // altta tek "Devam". Açık onay korunur (kabul edilmeden oyun açılmaz).
    private const float CardWidth = 820f;
    private static readonly Color CardColor = new Color(0.12f, 0.12f, 0.14f, 0.93f);
    private static readonly Color ContinueColor = new Color(0.25f, 0.62f, 1f, 1f);

    private void BuildGate(bool isUpdate)
    {
        if (!isUpdate) GameAnalytics.OnboardingStep(3);   // kabul kutusu gösterildi
        gate = new GameObject("LegalConsentGate");
        gate.transform.SetParent(transform, false);
        var canvas = gate.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32765;   // RuntimeChoicePopup (32760) ve yükleme ekranlarının üstünde
        var scaler = gate.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        gate.AddComponent<GraphicRaycaster>();

        // Hafif karartma: yükleme görseli seçilir kalır; dokunuşları yutar (kabul edilmeden arkaya geçilmez).
        var dim = NewRect("Dim", gate.transform);
        dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.35f);
        dimImg.raycastTarget = true;

        var skin = CommonPopupSkin.Shared;
        var card = NewRect("Card", gate.transform);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CardWidth, 0f);
        var bg = card.gameObject.AddComponent<Image>();
        bg.sprite = Resources.Load<Sprite>("UIEffects/LegalPanel");
        bg.type = Image.Type.Sliced;
        bg.color = CardColor;
        var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(60, 60, 56, 0);
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var font = skin != null ? skin.readableBodyFont : null;
        var body = Label(card, GameLocalization.Get(isUpdate ? "legal_compact_body_update" : "legal_compact_body"),
            44f, Color.white, font);
        body.raycastTarget = true;   // metin içi linkler
        body.gameObject.AddComponent<LegalLinkText>();
        Label(card, GameLocalization.Get("legal_compact_hint"), 34f, new Color(1f, 1f, 1f, 0.7f), font);

        var separator = NewRect("Separator", card);
        separator.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);
        separator.gameObject.AddComponent<LayoutElement>().preferredHeight = 2f;

        var button = NewRect("Continue", card);
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = 124f;
        var hit = button.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);   // görünmez dokunma alanı (tüm alt şerit)
        var press = button.gameObject.AddComponent<Button>();
        press.transition = Selectable.Transition.None;
        press.onClick.AddListener(Accept);
        var label = Label(button, GameLocalization.Get("legal_continue"), 48f, ContinueColor,
            skin != null && skin.buttonFont != null ? skin.buttonFont : font);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static TextMeshProUGUI Label(Transform parent, string value, float size, Color color, TMP_FontAsset font)
    {
        var text = NewRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    // Uzun etiketler (Terms of Service) düğmeye sığsın: üst sınır + sığmazsa küçül.
    internal static void SetLabelSize(Button button, float size)
    {
        foreach (var text in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.6f;
            text.fontSize = size;
        }
    }

    private void Accept()
    {
        GameAnalytics.OnboardingStep(4);   // kabul edildi
        PlayerPrefs.SetString(AcceptedKey, RequiredVersion);
        PlayerPrefs.SetString(AcceptedAtKey, DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();
        FirebaseCloudSaveService.MarkDirty();
        if (gate != null) Destroy(gate);
        gate = null;
    }

    /// Hesap silme / kayıt sıfırlama sonrası: kabul yeniden istensin.
    internal static void ResetAcceptance()
    {
        PlayerPrefs.DeleteKey(AcceptedKey);
        PlayerPrefs.DeleteKey(AcceptedAtKey);
    }
}
