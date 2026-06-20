using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dünya haritasında açılabilir bir bölge (ada/alan).
/// Yıldız HARCAMA modeli: görev listesinden tıklanınca starCost kadar yıldız düşülür,
/// bölge kalıcı olarak "açıldı" işaretlenir ve sis (fog) animasyonla kalkar.
///
/// Açılma durumu PlayerPrefs'te regionId ile saklanır (toplam yıldıza bağlı DEĞİL).
/// Bina/dekor ayrı; bu component sadece sisi + pin'i + kalıcı durumu yönetir.
/// </summary>
public sealed class WorldMapRegion : MonoBehaviour
{
    [Header("Kimlik & Liste Bilgisi")]
    [Tooltip("Benzersiz kalıcı anahtar. Açılma durumu 'region_unlocked_<id>' olarak saklanır. " +
             "Her bölgede FARKLI olmalı (örn: living_area, garden, harbor).")]
    [SerializeField] private string regionId;
    [Tooltip("Görev listesinde gösterilecek ad için lokalizasyon anahtarı (tinyfixers_localization.json).")]
    [SerializeField] private string nameLocalizationKey;
    [Tooltip("Lokalizasyon bulunamazsa kullanılacak yedek ad.")]
    [SerializeField] private string fallbackName = "Bölge";
    [Tooltip("Bu bölgeyi açmak için harcanacak yıldız.")]
    [SerializeField, Min(0)] private int starCost = 10;
    [Tooltip("Görev listesi satırında gösterilecek ikon (opsiyonel — bölge küçük görseli).")]
    [SerializeField] private Sprite taskIcon;
    [Tooltip("Oyun ilk açıldığında zaten açık başlasın mı? (örn. başlangıç/ev bölgesi).")]
    [SerializeField] private bool startUnlocked;

    [Header("Sis (Fog)")]
    [Tooltip("Bölgeyi örten sis/bulut. CanvasGroup'lu bir obje; açılınca fade-out olur. " +
             "Bulut parçaları (cloudPieces) bunun ÇOCUĞU olmalı ki birlikte solsunlar.")]
    [SerializeField] private CanvasGroup fog;
    [Tooltip("Kilitliyken sis opaklığı. 1 = tam kapalı, 0.6-0.8 = altındaki siluet hafif görünür.")]
    [SerializeField, Range(0f, 1f)] private float lockedFogAlpha = 0.75f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.6f;

    [Header("Bulut Parçaları (opsiyonel — aralanarak açılma)")]
    [Tooltip("Sis birden çok bulut parçasından oluşuyorsa: açılırken her parça kendi yönüne KAYARAK " +
             "dağılır ve alttaki resim ortaya çıkar. Boşsa sadece fade-out olur.\n" +
             "Parçalar fog'un (CanvasGroup) çocuğu olmalı.")]
    [SerializeField] private CloudPiece[] cloudPieces;
    [Tooltip("Parçaların dağılırken kayacağı mesafe (piksel).")]
    [SerializeField, Min(0f)] private float cloudDriftDistance = 500f;

    [System.Serializable]
    public struct CloudPiece
    {
        [Tooltip("Bulut parçası (Image'lı RectTransform).")]
        public RectTransform piece;
        [Tooltip("Dağılma yönü. Örn (-1,0)=sola, (1,0)=sağa, (0,1)=yukarı, (0,-1)=aşağı, (1,1)=sağ-üst.")]
        public Vector2 driftDirection;
    }

    [Header("Otomatik Bölme (elle bulut eklemeden, fog imajını 4'e ayır)")]
    [Tooltip("Açık: fog'un imajı otomatik 4 çeyreğe bölünüp köşelere dağılır. " +
             "Elle bulut/cloudPieces gerekmez. Fog'da bir Image + sprite olmalı. " +
             "Açıksa cloudPieces yok sayılır.")]
    [SerializeField] private bool autoSplitFog;
    [Tooltip("4 çeyreğin köşelere kayma mesafesi (piksel).")]
    [SerializeField, Min(0f)] private float splitDriftDistance = 400f;

    [Header("Pin (opsiyonel)")]
    [Tooltip("Bölge pini. Sisin DIŞINDA olmalı (fade olmasın).")]
    [SerializeField] private Image pin;
    [SerializeField] private Sprite lockedPinSprite;
    [SerializeField] private Sprite unlockedPinSprite;

    [Header("Hedef (görev listesi yıldızları buraya uçar)")]
    [Tooltip("Sinematikte yıldızların uçacağı / kutlamanın patlayacağı nokta. " +
             "Boşsa fog'un (yoksa bu objenin) transform'u kullanılır.")]
    [SerializeField] private RectTransform revealFocus;

    // ─── Public ──────────────────────────────────────────────────────────────

    public string RegionId => regionId;
    public string NameLocalizationKey => nameLocalizationKey;
    public string FallbackName => fallbackName;
    public int StarCost => starCost;
    public Sprite TaskIcon => taskIcon;

    public RectTransform RevealFocus =>
        revealFocus != null ? revealFocus :
        (fog != null ? (RectTransform)fog.transform : (RectTransform)transform);

    /// <summary>Yıldız/kutlama VFX'inin çıkacağı nokta = PIN konumu (yoksa RevealFocus).</summary>
    public RectTransform StarPoint =>
        pin != null ? (RectTransform)pin.transform : RevealFocus;

    private string Key => $"region_unlocked_{regionId}";

    public bool IsUnlocked
    {
        get => startUnlocked || PlayerPrefs.GetInt(Key, 0) == 1;
        private set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    private Coroutine fadeCo;

    // ─── State ───────────────────────────────────────────────────────────────

    /// <summary>Kalıcı duruma göre sisi anında uygula (animasyonsuz). Sahne açılışında çağrılır.</summary>
    public void ApplyInstant()
    {
        bool unlocked = IsUnlocked;

        if (pin != null)
        {
            var s = unlocked ? unlockedPinSprite : lockedPinSprite;
            if (s != null) pin.sprite = s;
        }

        if (fog == null) return;
        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }

        if (unlocked)
        {
            fog.alpha = 0f;
            fog.gameObject.SetActive(false);
        }
        else
        {
            fog.gameObject.SetActive(true);
            fog.alpha = lockedFogAlpha;
        }
    }

    /// <summary>Bölgeyi kalıcı aç + sisi animasyonla kaldır. WorldMapController çağırır.</summary>
    public IEnumerator RevealRoutine()
    {
        IsUnlocked = true;   // kesinti olsa bile kaydedildi

        if (pin != null && unlockedPinSprite != null) pin.sprite = unlockedPinSprite;

        if (fog == null) yield break;
        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }

        if (!fog.gameObject.activeSelf) fog.gameObject.SetActive(true);
        float start = fog.alpha <= 0.001f ? lockedFogAlpha : fog.alpha;
        fog.alpha = start;

        // Hareket edecek parçaları topla: autoSplit > cloudPieces > (yok = düz fade).
        var pieces = new List<RectTransform>();
        var starts = new List<Vector2>();
        var targets = new List<Vector2>();
        var tempPieces = new List<GameObject>();   // autoSplit'te yaratılan geçici çeyrekler
        Image hiddenFogImg = null;

        bool split = autoSplitFog && BuildSplitPieces(pieces, starts, targets, tempPieces, out hiddenFogImg);
        if (!split && cloudPieces != null)
        {
            for (int i = 0; i < cloudPieces.Length; i++)
            {
                var p = cloudPieces[i].piece;
                if (p == null) continue;
                Vector2 s = p.anchoredPosition;
                Vector2 dir = cloudPieces[i].driftDirection;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
                pieces.Add(p); starts.Add(s); targets.Add(s + dir.normalized * cloudDriftDistance);
            }
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float ek = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeDuration));

            fog.alpha = Mathf.Lerp(start, 0f, ek);   // hepsi birlikte solsun
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != null)
                    pieces[i].anchoredPosition = Vector2.Lerp(starts[i], targets[i], ek);

            yield return null;
        }

        fog.alpha = 0f;
        fog.gameObject.SetActive(false);

        // autoSplit temizliği: geçici çeyrekleri sil, orijinal bulut Image'ını geri aç.
        for (int i = 0; i < tempPieces.Count; i++)
            if (tempPieces[i] != null) Destroy(tempPieces[i]);
        if (hiddenFogImg != null) hiddenFogImg.enabled = true;

        // cloudPieces ise konumları başa al (autoSplit değilse).
        if (tempPieces.Count == 0)
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != null) pieces[i].anchoredPosition = starts[i];
    }

    /// <summary>Fog imajını 4 çeyreğe bölüp (RawImage uvRect) köşelere dağılacak geçici parçalar üretir.</summary>
    private bool BuildSplitPieces(List<RectTransform> pieces, List<Vector2> starts, List<Vector2> targets,
                                  List<GameObject> temp, out Image hiddenFogImg)
    {
        hiddenFogImg = null;

        var fogRT = (RectTransform)fog.transform;
        var fogImg = fog.GetComponent<Image>();
        if (fogImg == null) fogImg = fog.GetComponentInChildren<Image>();
        if (fogImg == null || fogImg.sprite == null || fogImg.sprite.texture == null) return false;

        var sprite = fogImg.sprite;
        var tex = sprite.texture;
        Rect tr = sprite.textureRect;
        float bu = tr.x / tex.width, bv = tr.y / tex.height;
        float bw = tr.width / tex.width, bh = tr.height / tex.height;

        Vector2 size = fogRT.rect.size;
        Vector2 half = size * 0.5f;
        Vector2 q = size * 0.25f;

        // 4 çeyrek: merkez offset + texture uv (sol-alt orijin).
        var quads = new (Vector2 center, Rect uv)[]
        {
            (new Vector2(-q.x, -q.y), new Rect(bu,          bv,          bw / 2f, bh / 2f)), // sol-alt
            (new Vector2( q.x, -q.y), new Rect(bu + bw / 2f, bv,          bw / 2f, bh / 2f)), // sağ-alt
            (new Vector2(-q.x,  q.y), new Rect(bu,          bv + bh / 2f, bw / 2f, bh / 2f)), // sol-üst
            (new Vector2( q.x,  q.y), new Rect(bu + bw / 2f, bv + bh / 2f, bw / 2f, bh / 2f)), // sağ-üst
        };

        foreach (var quad in quads)
        {
            var go = new GameObject("CloudQuad", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(fogRT, false);   // fog'un altında → CanvasGroup alpha'sıyla solar

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = half;
            rt.anchoredPosition = quad.center;

            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.uvRect = quad.uv;
            raw.color = fogImg.color;
            raw.raycastTarget = false;

            pieces.Add(rt);
            starts.Add(quad.center);
            Vector2 dir = quad.center.sqrMagnitude > 0.0001f ? quad.center.normalized : Vector2.up;
            targets.Add(quad.center + dir * splitDriftDistance);
            temp.Add(go);
        }

        fogImg.enabled = false;   // orijinal tek bulut gizlenir, yerini çeyrekler alır
        hiddenFogImg = fogImg;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(regionId))
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (string.IsNullOrEmpty(regionId))
                    Debug.LogWarning($"[WorldMapRegion] '{name}' için regionId boş — açılma durumu kaydedilemez.", this);
            };
    }
#endif
}
