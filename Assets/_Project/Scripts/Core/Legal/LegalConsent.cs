using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Kullanım Şartları + Gizlilik Politikası kabul ekranı. METİNLER OYUNLA GELİR (Resources/Legal/*.txt, sürüm
/// version.txt) → internet olmadan da okunup kabul edilir. Web yalnız sürüm kontrolü: wondigames.com/legal/version.json
/// daha yeni bir sürüm bildirirse dört metin indirilip cihazda saklanır (persistentDataPath/legal) ve kabul yeniden
/// istenir. İndirme tamamlanmadan gereken sürüm yükselmez (okuyamayacağı metni kabul ettirmeyiz).
/// Kaynak tek: Website/legal/*_tr|en.html → build.py hem siteyi hem bu .txt'leri üretir (LEGAL_VERSION shell.py'de).
///   • Kapatma (X) yok, karartma arka planı bloklar: kabul edilmeden oynanmaz. Şartlar/Gizlilik düğmeleri metni oyun
///     içinde kaydırılabilir pencerede açar (LegalDocumentView), kabul penceresi açık kalır.
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

    private void BuildGate(bool isUpdate)
    {
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

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        var drt = (RectTransform)dim.transform;
        drt.SetParent(gate.transform, false);
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
        var dimImg = dim.GetComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.78f);
        dimImg.raycastTarget = true;

        // "Can bitti" penceresiyle aynı kart düzeni: üstte açıklama, iki büyük belge kartı, altta tek yeşil "Kabul Ediyorum".
        // Kartlar pencereyi kapatmaz (belge üstte açılır, kapanınca buraya dönülür); açılan belgenin alt yazısı "Göz attın" olur.
        var view = CommonPopupView.Create(gate.transform, GameLocalization.Get("legal_title"), null,
            CommonPopupSkin.Shared.saveProgressBackgroundMaterial);
        string[] slugs = { "terms", "privacy" };
        var options = new[]
        {
            new RuntimeChoicePopup.OfferButton(GameLocalization.Get("settings_terms_open_terms"),
                GameLocalization.Get("legal_card_terms_sub"), null, keepOpen: true),
            new RuntimeChoicePopup.OfferButton(GameLocalization.Get("settings_terms_open_privacy"),
                GameLocalization.Get("legal_card_privacy_sub"), null, keepOpen: true),
        };
        Button[] cards = null;
        int next = 0;   // handler kartlar sırasıyla çağrılır → indeks
        cards = RuntimeChoicePopup.BuildOfferBody(view,
            GameLocalization.Get(isUpdate ? "legal_body_update" : "legal_body"), options, _ =>
            {
                int i = next++;
                return () =>
                {
                    LegalDocumentView.Show(slugs[i]);
                    RuntimeChoicePopup.SetOfferSubtitle(cards[i], GameLocalization.Get("legal_card_viewed"), ViewedColor);
                };
            });
        RuntimeChoicePopup.BuildOfferAction(view, GameLocalization.Get("legal_accept"), Accept);
    }

    private static readonly Color ViewedColor = new Color(0.72f, 1f, 0.72f);

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
